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
  formatDatabaseSaveConfirmation,
  getDatabaseChangeSummary,
  hasDatabaseChanges,
} from '../features/database/databaseChanges'
import type { DatabaseSnapshot } from '../features/database/databaseChanges'
import { useNoteTabs } from '../features/notes/useNoteTabs'
import { fromSavedNote } from '../features/notes/notes'
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
  } = useNoteTabs(() => setMessage(''))

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

    const response = await apiRequest('/notes', {
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
    databaseSnapshotRef.current = createDatabaseSnapshot(nextTabs)
    replaceWorkingTabs(nextTabs)
    setMessage(
      nextTabs.length === 0
        ? 'database has no notes yet'
        : `loaded ${nextTabs.length} note${nextTabs.length === 1 ? '' : 's'} from database`,
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

    const response = await apiRequest('/notes/sync', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ notes: nextTabs }),
    })

    if (!response) {
      setMessage(`database save failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!(await handleDatabaseResponse(response))) {
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    const syncedTabs = savedNotes.map(fromSavedNote)
    databaseSnapshotRef.current = createDatabaseSnapshot(syncedTabs)
    setTabsInMemory(syncedTabs)
    setMessage(`saved ${syncedTabs.length} note${syncedTabs.length === 1 ? '' : 's'} to database`)
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

export default App
