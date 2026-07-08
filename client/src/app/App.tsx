import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest, readError } from '../shared/api'
import './App.css'
import { readLocalAuth, writeLocalAuth } from '../features/auth/authStorage'
import { SavePanel } from '../features/auth/SavePanel'
import { TabBar } from '../features/notes/TabBar'
import { API_BASE, EDITOR_DOCUMENT } from '../shared/constants'
import {
  createDatabaseSnapshot,
  createDatabaseSyncRequest,
  formatDatabaseSaveConfirmation,
  getDatabaseChangeSummary,
  hasDatabaseChanges,
  markDatabaseSnapshotContentLoaded,
} from '../features/database/databaseChanges'
import type { DatabaseSnapshot } from '../features/database/databaseChanges'
import { useNoteTabs } from '../features/notes/useNoteTabs'
import { fromSavedNote, normalizeContent } from '../features/notes/notes'
import type {
  AuthFormState,
  AuthMode,
  AuthState,
  DatabaseAction,
  NoteTab,
  SavedNote,
} from '../shared/types'

const EMPTY_AUTH_FORM: AuthFormState = {
  userName: '',
  password: '',
  confirmPassword: '',
}

function App() {
  const [auth, setAuth] = useState<AuthState | null>(readLocalAuth)
  const [authMode, setAuthMode] = useState<AuthMode>('login')
  const [authForm, setAuthForm] = useState<AuthFormState>(EMPTY_AUTH_FORM)
  const [showAuth, setShowAuth] = useState(false)
  const [pendingDatabaseAction, setPendingDatabaseAction] =
    useState<DatabaseAction | null>(null)
  const [message, setMessage] = useState('')
  const databaseSnapshotRef = useRef<DatabaseSnapshot | null>(null)

  const {
    activeId,
    closeTab,
    createTab,
    draftTitle,
    editingId,
    editorRef,
    finishRename,
    handleEditorLoad,
    handleRenameKey,
    replaceWorkingTabs,
    resetTabs,
    selectTab,
    setDraftTitle,
    setTabsInMemory,
    startRename,
    tabBarRef,
    tabs,
    tabsWithCurrentEditor,
  } = useNoteTabs(() => setMessage(''), ensureDatabaseTabContent)

  useEffect(() => {
    if (!auth) {
      databaseSnapshotRef.current = null
    }

    writeLocalAuth(auth)
  }, [auth])

  function requireDatabaseAuth(action: DatabaseAction) {
    if (auth) {
      return auth.token
    }

    setPendingDatabaseAction(action)
    setShowAuth(true)
    setMessage(
      action === 'save'
        ? 'login or register to save to database'
        : 'login or register to load database notes',
    )
    return null
  }

  async function saveToDatabase() {
    const nextTabs = tabsWithCurrentEditor()
    const token = requireDatabaseAuth('save')
    if (token) {
      await confirmAndSyncDatabaseNotes(token, nextTabs)
    }
  }

  async function loadFromDatabase() {
    const token = requireDatabaseAuth('load')
    if (token) {
      await fetchDatabaseNotes(token)
    }
  }

  async function fetchDatabaseNotes(token: string) {
    setMessage('loading database notes')
    const contentNoteQuery = activeId
      ? `?contentNoteId=${encodeURIComponent(activeId)}`
      : ''

    const response = await apiRequest(`/notes${contentNoteQuery}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    })

    if (!response) {
      setMessage(`database load failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!(await handleDatabaseResponse(response))) {
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    const nextTabs = savedNotes.map(fromSavedNote)
    const nextActiveId =
      nextTabs.find((tab) => tab.id === activeId && tab.contentLoaded)?.id ??
      nextTabs.find((tab) => tab.contentLoaded)?.id

    databaseSnapshotRef.current = createDatabaseSnapshot(nextTabs)
    replaceWorkingTabs(nextTabs, nextActiveId)
    setMessage(
      nextTabs.length === 0
        ? 'database has no notes yet'
        : `loaded ${nextTabs.length} note${nextTabs.length === 1 ? '' : 's'} from database`,
    )
  }

  async function ensureDatabaseTabContent(tabId: string, currentTabs: NoteTab[]) {
    const targetTab = currentTabs.find((tab) => tab.id === tabId)
    if (!targetTab || targetTab.contentLoaded) {
      return currentTabs
    }

    if (!auth) {
      setShowAuth(true)
      setMessage('login or register to load note content')
      return null
    }

    setMessage('loading note content')

    const response = await apiRequest(`/notes/${encodeURIComponent(tabId)}`, {
      headers: {
        Authorization: `Bearer ${auth.token}`,
      },
    })

    if (!response) {
      setMessage(`database load failed: API not reachable at ${API_BASE}`)
      return null
    }

    if (!(await handleDatabaseResponse(response))) {
      return null
    }

    const savedNote = (await response.json()) as SavedNote
    const content = normalizeContent(savedNote.content)

    markDatabaseSnapshotContentLoaded(databaseSnapshotRef.current, tabId, content)

    return currentTabs.map((tab) =>
      tab.id === tabId
        ? {
            ...tab,
            content,
            contentLoaded: true,
          }
        : tab,
    )
  }

  async function confirmAndSyncDatabaseNotes(token: string, nextTabs: NoteTab[]) {
    const summary = getDatabaseChangeSummary(nextTabs, databaseSnapshotRef.current)

    if (!hasDatabaseChanges(summary)) {
      setMessage('no database changes to save')
      return
    }

    if (!window.confirm(formatDatabaseSaveConfirmation(summary))) {
      setMessage('database save canceled')
      return
    }

    await syncDatabaseNotes(token, nextTabs)
  }

  async function syncDatabaseNotes(token: string, nextTabs: NoteTab[]) {
    setMessage('saving to database')
    const syncRequest = createDatabaseSyncRequest(nextTabs, databaseSnapshotRef.current)

    const response = await apiRequest('/notes/sync', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(syncRequest),
    })

    if (!response) {
      setMessage(`database save failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!(await handleDatabaseResponse(response))) {
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    const syncedTabs = mergeSyncedNotes(nextTabs, savedNotes, syncRequest.deletedNoteIds)
    databaseSnapshotRef.current = createDatabaseSnapshot(syncedTabs)
    setTabsInMemory(syncedTabs)
    const changeCount = syncRequest.notes.length + syncRequest.deletedNoteIds.length
    setMessage(`saved ${changeCount} database change${changeCount === 1 ? '' : 's'}`)
  }

  async function handleDatabaseResponse(response: Response) {
    if (response.status === 401) {
      setAuth(null)
      setShowAuth(true)
      setMessage('session expired')
      return false
    }

    if (!response.ok) {
      setMessage(await readError(response))
      return false
    }

    return true
  }

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const response = await apiRequest(`/auth/${authMode}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(createAuthPayload(authMode, authForm)),
    })

    if (!response) {
      setMessage(`auth failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!response.ok) {
      setMessage(await readError(response))
      return
    }

    const nextAuth = (await response.json()) as AuthState
    const databaseAction = pendingDatabaseAction
    setAuth(nextAuth)
    setShowAuth(false)
    setPendingDatabaseAction(null)
    setAuthForm(EMPTY_AUTH_FORM)
    setMessage(`${authMode === 'login' ? 'logged in' : 'registered'} as ${nextAuth.userName}`)

    if (databaseAction === 'save') {
      await confirmAndSyncDatabaseNotes(nextAuth.token, tabsWithCurrentEditor())
    }

    if (databaseAction === 'load') {
      await fetchDatabaseNotes(nextAuth.token)
    }
  }

  function toggleAuthMode() {
    setAuthMode((current) => (current === 'login' ? 'register' : 'login'))
    setAuthForm((current) => ({ ...current, confirmPassword: '' }))
  }

  function logout() {
    setAuth(null)
    setShowAuth(false)
    setPendingDatabaseAction(null)
    setAuthForm(EMPTY_AUTH_FORM)
    resetTabs()
    setMessage('logged out')
  }

  return (
    <div className="app">
      <iframe
        className="editor"
        onLoad={handleEditorLoad}
        ref={editorRef}
        srcDoc={EDITOR_DOCUMENT}
        title="note editor"
      />

      <SavePanel
        auth={auth}
        authForm={authForm}
        authMode={authMode}
        message={message}
        showAuth={showAuth}
        onAuthFormChange={setAuthForm}
        onLoadFromDatabase={loadFromDatabase}
        onLogout={logout}
        onSaveToDatabase={saveToDatabase}
        onSubmitAuth={submitAuth}
        onToggleAuthMode={toggleAuthMode}
      />

      <TabBar
        activeId={activeId}
        draftTitle={draftTitle}
        editingId={editingId}
        tabBarRef={tabBarRef}
        tabs={tabs}
        onCloseTab={closeTab}
        onCreateTab={createTab}
        onDraftTitleChange={setDraftTitle}
        onFinishRename={finishRename}
        onRenameKeyDown={handleRenameKey}
        onSelectTab={selectTab}
        onStartRename={startRename}
      />
    </div>
  )
}

function createAuthPayload(authMode: AuthMode, authForm: AuthFormState) {
  if (authMode === 'register') {
    return authForm
  }

  return {
    userName: authForm.userName,
    password: authForm.password,
  }
}

function mergeSyncedNotes(
  currentTabs: NoteTab[],
  savedNotes: SavedNote[],
  deletedNoteIds: string[],
) {
  const deletedIds = new Set(deletedNoteIds)
  const savedById = new Map(savedNotes.map((note) => [note.id, note]))

  return currentTabs
    .filter((tab) => !deletedIds.has(tab.id))
    .map((tab) => {
      const savedNote = savedById.get(tab.id)
      if (!savedNote) {
        return tab
      }

      if (savedNote.content == null) {
        return {
          ...tab,
          title: savedNote.title || tab.title,
        }
      }

      return {
        ...tab,
        title: savedNote.title || tab.title,
        content: normalizeContent(savedNote.content),
        contentLoaded: true,
      }
    })
}

export default App
