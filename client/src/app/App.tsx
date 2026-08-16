import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'
import { apiRequest, readError } from '../shared/api'
import {
  readActiveAuthMethod,
  readLegacyAuth,
  writeActiveAuthMethod,
  writeLegacyAuth,
} from '../features/auth/authStorage'
import {
  fetchOidcSession,
  startOidcLogin,
  submitOidcLogout,
} from '../features/auth/oidcSession'
import { SavePanel } from '../features/auth/SavePanel'
import {
  legacyGetNote,
  legacyListNotes,
  legacySyncNotes,
  oidcGetNote,
  oidcListNotes,
  oidcSyncNotes,
} from '../features/database/databaseApi'
import {
  createDatabaseSnapshot,
  createDatabaseSyncRequest,
  formatDatabaseSaveConfirmation,
  getDatabaseChangeSummary,
  hasDatabaseChanges,
  markDatabaseSnapshotContentLoaded,
} from '../features/database/databaseChanges'
import type { DatabaseSnapshot } from '../features/database/databaseChanges'
import { TabBar } from '../features/notes/TabBar'
import { useNoteTabs } from '../features/notes/useNoteTabs'
import { fromSavedNote, normalizeContent } from '../features/notes/notes'
import { API_BASE, EDITOR_DOCUMENT } from '../shared/constants'
import type {
  AuthMethod,
  DatabaseAction,
  LegacyAuthFormState,
  LegacyAuthMode,
  LegacyAuthState,
  NoteTab,
  OidcSessionState,
  SavedNote,
} from '../shared/types'

const EMPTY_LEGACY_AUTH_FORM: LegacyAuthFormState = {
  userName: '',
  password: '',
  confirmPassword: '',
}

function App() {
  const [activeAuthMethod, setActiveAuthMethod] =
    useState<AuthMethod>(readActiveAuthMethod)
  const [legacyAuth, setLegacyAuth] = useState<LegacyAuthState | null>(readLegacyAuth)
  const [legacyAuthMode, setLegacyAuthMode] = useState<LegacyAuthMode>('login')
  const [legacyAuthForm, setLegacyAuthForm] =
    useState<LegacyAuthFormState>(EMPTY_LEGACY_AUTH_FORM)
  const [oidcSession, setOidcSession] =
    useState<OidcSessionState>({ status: 'loading' })
  const [showLegacyAuth, setShowLegacyAuth] = useState(false)
  const [pendingDatabaseAction, setPendingDatabaseAction] =
    useState<DatabaseAction | null>(null)
  const [message, setMessage] = useState('')
  const activeAuthMethodRef = useRef(activeAuthMethod)
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

  const refreshOidcSession = useCallback(async (showLoading = false) => {
    if (showLoading) {
      setOidcSession({ status: 'loading' })
    }

    const nextSession = await fetchOidcSession()
    setOidcSession(nextSession)
    return nextSession
  }, [])

  useEffect(() => {
    writeLegacyAuth(legacyAuth)
  }, [legacyAuth])

  useEffect(() => {
    writeActiveAuthMethod(activeAuthMethod)
  }, [activeAuthMethod])

  useEffect(() => {
    void fetchOidcSession().then(setOidcSession)

    const revalidateOidcSession = () => {
      if (document.visibilityState === 'visible') {
        void refreshOidcSession()
      }
    }

    window.addEventListener('focus', revalidateOidcSession)
    document.addEventListener('visibilitychange', revalidateOidcSession)

    return () => {
      window.removeEventListener('focus', revalidateOidcSession)
      document.removeEventListener('visibilitychange', revalidateOidcSession)
    }
  }, [refreshOidcSession])

  function requireDatabaseAuth(action: DatabaseAction, authMethod: AuthMethod) {
    if (authMethod === 'legacy') {
      if (legacyAuth) {
        return true
      }

      setPendingDatabaseAction(action)
      setShowLegacyAuth(true)
      setMessage(
        action === 'save'
          ? 'use legacy login or registration to save with the legacy JWT method'
          : 'use legacy login or registration to load with the legacy JWT method',
      )
      return false
    }

    if (oidcSession.status === 'authenticated') {
      return true
    }

    if (oidcSession.status === 'loading') {
      setMessage('wait for the OIDC session check to finish')
      return false
    }

    if (oidcSession.status === 'unavailable') {
      setMessage('the OIDC session endpoint is unavailable; retry the OIDC session check')
      return false
    }

    setMessage(
      action === 'save'
        ? 'login with OIDC before saving with the OIDC cookie method'
        : 'login with OIDC before loading with the OIDC cookie method',
    )
    return false
  }

  async function saveToDatabase() {
    const authMethod = activeAuthMethodRef.current
    const nextTabs = tabsWithCurrentEditor()

    if (requireDatabaseAuth('save', authMethod)) {
      await confirmAndSyncDatabaseNotes(authMethod, nextTabs)
    }
  }

  async function loadFromDatabase() {
    const authMethod = activeAuthMethodRef.current

    if (requireDatabaseAuth('load', authMethod)) {
      await fetchDatabaseNotes(authMethod)
    }
  }

  async function fetchDatabaseNotes(
    authMethod: AuthMethod,
    legacyToken = legacyAuth?.token,
  ) {
    setMessage(`loading database notes with ${formatAuthMethod(authMethod)}`)

    const response =
      authMethod === 'oidc'
        ? await oidcListNotes(activeId || undefined)
        : legacyToken
          ? await legacyListNotes(legacyToken, activeId || undefined)
          : null

    if (activeAuthMethodRef.current !== authMethod) {
      return
    }

    if (!response) {
      setMessage(`database load failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!(await handleDatabaseResponse(response, authMethod))) {
      return
    }

    if (activeAuthMethodRef.current !== authMethod) {
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
        ? `database has no notes for ${formatAuthMethod(authMethod)}`
        : `loaded ${nextTabs.length} note${nextTabs.length === 1 ? '' : 's'} with ${formatAuthMethod(authMethod)}`,
    )
  }

  async function ensureDatabaseTabContent(tabId: string, currentTabs: NoteTab[]) {
    const targetTab = currentTabs.find((tab) => tab.id === tabId)
    if (!targetTab || targetTab.contentLoaded) {
      return currentTabs
    }

    const authMethod = activeAuthMethodRef.current
    if (!requireDatabaseAuth('load', authMethod)) {
      return null
    }

    setMessage(`loading note content with ${formatAuthMethod(authMethod)}`)

    const response =
      authMethod === 'oidc'
        ? await oidcGetNote(tabId)
        : legacyAuth
          ? await legacyGetNote(legacyAuth.token, tabId)
          : null

    if (activeAuthMethodRef.current !== authMethod) {
      return null
    }

    if (!response) {
      setMessage(`database load failed: API not reachable at ${API_BASE}`)
      return null
    }

    if (!(await handleDatabaseResponse(response, authMethod))) {
      return null
    }

    if (activeAuthMethodRef.current !== authMethod) {
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

  async function confirmAndSyncDatabaseNotes(
    authMethod: AuthMethod,
    nextTabs: NoteTab[],
    legacyToken = legacyAuth?.token,
  ) {
    const summary = getDatabaseChangeSummary(nextTabs, databaseSnapshotRef.current)

    if (!hasDatabaseChanges(summary)) {
      setMessage('no database changes to save')
      return
    }

    if (!window.confirm(formatDatabaseSaveConfirmation(summary))) {
      setMessage('database save canceled')
      return
    }

    await syncDatabaseNotes(authMethod, nextTabs, legacyToken)
  }

  async function syncDatabaseNotes(
    authMethod: AuthMethod,
    nextTabs: NoteTab[],
    legacyToken = legacyAuth?.token,
  ) {
    setMessage(`saving to database with ${formatAuthMethod(authMethod)}`)
    const syncRequest = createDatabaseSyncRequest(nextTabs, databaseSnapshotRef.current)

    const response =
      authMethod === 'oidc'
        ? oidcSession.status === 'authenticated'
          ? await oidcSyncNotes(syncRequest, oidcSession.csrfToken)
          : null
        : legacyToken
          ? await legacySyncNotes(legacyToken, syncRequest)
          : null

    if (activeAuthMethodRef.current !== authMethod) {
      return
    }

    if (!response) {
      setMessage(`database save failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!(await handleDatabaseResponse(response, authMethod))) {
      return
    }

    if (activeAuthMethodRef.current !== authMethod) {
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    const syncedTabs = mergeSyncedNotes(nextTabs, savedNotes, syncRequest.deletedNoteIds)
    databaseSnapshotRef.current = createDatabaseSnapshot(syncedTabs)
    setTabsInMemory(syncedTabs)
    const changeCount = syncRequest.notes.length + syncRequest.deletedNoteIds.length
    setMessage(
      `saved ${changeCount} database change${changeCount === 1 ? '' : 's'} with ${formatAuthMethod(authMethod)}`,
    )
  }

  async function handleDatabaseResponse(response: Response, authMethod: AuthMethod) {
    if (response.status === 401) {
      databaseSnapshotRef.current = null

      if (authMethod === 'legacy') {
        setLegacyAuth(null)
        setShowLegacyAuth(true)
        setMessage('legacy JWT expired; use legacy login again')
      } else {
        const refreshedSession = await refreshOidcSession()
        setMessage(
          refreshedSession.status === 'authenticated'
            ? 'OIDC request was unauthorized even though the session endpoint is authenticated'
            : 'OIDC session expired; login with OIDC again',
        )
      }

      return false
    }

    if (!response.ok) {
      const error = await readError(response)

      if (authMethod === 'oidc' && response.status === 400) {
        await refreshOidcSession()
        setMessage(`${error} A fresh OIDC CSRF token was requested; retry the save.`)
      } else {
        setMessage(error)
      }

      return false
    }

    return true
  }

  async function submitLegacyAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const response = await apiRequest(`/legacy/auth/${legacyAuthMode}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(createLegacyAuthPayload(legacyAuthMode, legacyAuthForm)),
    })

    if (!response) {
      setMessage(`legacy auth failed: API not reachable at ${API_BASE}`)
      return
    }

    if (!response.ok) {
      setMessage(await readError(response))
      return
    }

    const nextLegacyAuth = (await response.json()) as LegacyAuthState
    const databaseAction = pendingDatabaseAction
    setLegacyAuth(nextLegacyAuth)
    setShowLegacyAuth(false)
    setPendingDatabaseAction(null)
    setLegacyAuthForm(EMPTY_LEGACY_AUTH_FORM)
    setMessage(
      `${legacyAuthMode === 'login' ? 'logged in' : 'registered'} as ${nextLegacyAuth.userName} with legacy JWT`,
    )

    if (activeAuthMethodRef.current !== 'legacy') {
      return
    }

    if (databaseAction === 'save') {
      await confirmAndSyncDatabaseNotes(
        'legacy',
        tabsWithCurrentEditor(),
        nextLegacyAuth.token,
      )
    }

    if (databaseAction === 'load') {
      await fetchDatabaseNotes('legacy', nextLegacyAuth.token)
    }
  }

  function toggleLegacyAuthMode() {
    setLegacyAuthMode((current) => (current === 'login' ? 'register' : 'login'))
    setLegacyAuthForm((current) => ({ ...current, confirmPassword: '' }))
  }

  function selectAuthMethod(nextAuthMethod: AuthMethod) {
    if (nextAuthMethod === activeAuthMethodRef.current) {
      return
    }

    tabsWithCurrentEditor()
    const nextLabel = formatAuthMethod(nextAuthMethod)

    if (
      !window.confirm(
        `Switch database authentication to ${nextLabel}?\n\nThe current working tabs and database snapshot will be cleared so notes from two accounts cannot be mixed.`,
      )
    ) {
      return
    }

    activeAuthMethodRef.current = nextAuthMethod
    setActiveAuthMethod(nextAuthMethod)
    setPendingDatabaseAction(null)
    setShowLegacyAuth(false)
    databaseSnapshotRef.current = null
    resetTabs()
    setMessage(`using ${nextLabel} for database operations`)
  }

  function loginWithOidc() {
    if (
      window.confirm(
        'OIDC login navigates away from this page. Unsaved in-memory notes will be lost. Continue to Keycloak?',
      )
    ) {
      startOidcLogin('/')
    }
  }

  function logoutOidc() {
    if (oidcSession.status !== 'authenticated') {
      return
    }

    if (
      window.confirm(
        'OIDC logout navigates away from this page. Unsaved in-memory notes will be lost. Continue?',
      )
    ) {
      submitOidcLogout(oidcSession.csrfToken)
    }
  }

  function logoutLegacy() {
    setLegacyAuth(null)
    setShowLegacyAuth(false)
    setPendingDatabaseAction(null)
    setLegacyAuthForm(EMPTY_LEGACY_AUTH_FORM)

    if (activeAuthMethodRef.current === 'legacy') {
      databaseSnapshotRef.current = null
      resetTabs()
    }

    setMessage('logged out of legacy JWT')
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
        activeAuthMethod={activeAuthMethod}
        legacyAuth={legacyAuth}
        legacyAuthForm={legacyAuthForm}
        legacyAuthMode={legacyAuthMode}
        message={message}
        oidcSession={oidcSession}
        showLegacyAuth={showLegacyAuth}
        onLegacyAuthFormChange={setLegacyAuthForm}
        onLegacyLogout={logoutLegacy}
        onLoadFromDatabase={loadFromDatabase}
        onOidcLogin={loginWithOidc}
        onOidcLogout={logoutOidc}
        onRefreshOidcSession={() => {
          void refreshOidcSession(true)
        }}
        onSaveToDatabase={saveToDatabase}
        onSelectAuthMethod={selectAuthMethod}
        onSubmitLegacyAuth={submitLegacyAuth}
        onToggleLegacyAuth={() => setShowLegacyAuth((current) => !current)}
        onToggleLegacyAuthMode={toggleLegacyAuthMode}
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

function createLegacyAuthPayload(
  authMode: LegacyAuthMode,
  authForm: LegacyAuthFormState,
) {
  if (authMode === 'register') {
    return authForm
  }

  return {
    userName: authForm.userName,
    password: authForm.password,
  }
}

function formatAuthMethod(authMethod: AuthMethod) {
  return authMethod === 'oidc' ? 'OIDC cookie' : 'legacy JWT'
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
