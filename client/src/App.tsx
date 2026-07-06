import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { FormEvent, KeyboardEvent } from 'react'
import './App.css'

const DEFAULT_TITLE = 'normal ass note'
const LOCAL_AUTH_KEY = 'normal-ass-note.auth'
const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '')
const EDITOR_DOCUMENT = '<!doctype html><html contenteditable=""><head><style>html,body{min-height:100%}body{box-sizing:border-box;margin:8px;padding:0 10rem 2.5rem 0}img{max-width:100%;height:auto}@media(max-width:640px){body{padding-right:0;padding-bottom:5rem}}</style></head><body></body></html>'
const MAX_CONFIRM_TAB_NAMES = 20

type NoteTab = {
  id: string
  title: string
  content: string
}

type AuthState = {
  userName: string
  token: string
}

type AuthMode = 'login' | 'register'
type DatabaseAction = 'save' | 'load'

type SavedNote = {
  id: string
  title: string
  content: string
}

type DatabaseTabSnapshot = Pick<NoteTab, 'title' | 'content'>
type DatabaseSnapshot = Map<string, DatabaseTabSnapshot>

type DatabaseChangeSummary = {
  snapshotKnown: boolean
  currentTabs: string[]
  createdTabs: string[]
  changedTabs: string[]
  deletedTabs: string[]
}

function App() {
  const [tabs, setTabs] = useState<NoteTab[]>(() => [createBlankTab()])
  const [activeId, setActiveId] = useState(() => tabs[0]?.id ?? '')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draftTitle, setDraftTitle] = useState('')
  const [auth, setAuth] = useState<AuthState | null>(readLocalAuth)
  const [authMode, setAuthMode] = useState<AuthMode>('login')
  const [authForm, setAuthForm] = useState({
    userName: '',
    password: '',
    confirmPassword: '',
  })
  const [showAuth, setShowAuth] = useState(false)
  const [pendingDatabaseAction, setPendingDatabaseAction] = useState<DatabaseAction | null>(null)
  const [message, setMessage] = useState('')
  const editorRef = useRef<HTMLIFrameElement>(null)
  const tabBarRef = useRef<HTMLElement>(null)
  const tabsRef = useRef(tabs)
  const activeIdRef = useRef(activeId)
  const databaseSnapshotRef = useRef<DatabaseSnapshot | null>(null)

  const activeTab = tabs.find((tab) => tab.id === activeId) ?? tabs[0]
  const activeTabContent = activeTab?.content
  const activeTabId = activeTab?.id

  useEffect(() => {
    if (!activeId && tabs[0]) {
      setActiveTabInMemory(tabs[0].id)
    }
  }, [activeId, tabs])

  useEffect(() => {
    tabsRef.current = tabs
    activeIdRef.current = activeId
  }, [activeId, tabs])

  useLayoutEffect(() => {
    if (!activeTabId) {
      return
    }

    writeEditorContent(activeTabContent ?? '', activeTabId)
  }, [activeTabContent, activeTabId])

  useLayoutEffect(() => {
    const tabBar = tabBarRef.current
    if (!tabBar) {
      return
    }

    const updatePadding = () => updateEditorBottomPadding(editorRef.current, tabBar)
    const observer = new ResizeObserver(updatePadding)
    observer.observe(tabBar)
    updatePadding()

    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    if (auth) {
      localStorage.setItem(LOCAL_AUTH_KEY, JSON.stringify(auth))
      return
    }

    databaseSnapshotRef.current = null
    localStorage.removeItem(LOCAL_AUTH_KEY)
  }, [auth])

  function setTabsInMemory(nextTabs: NoteTab[]) {
    tabsRef.current = nextTabs
    setTabs(nextTabs)
  }

  function setActiveTabInMemory(tabId: string) {
    activeIdRef.current = tabId
    setActiveId(tabId)
  }

  function replaceWorkingTabs(nextTabs: NoteTab[]) {
    const workingTabs = nextTabs.length > 0 ? nextTabs : [createBlankTab()]
    setTabsInMemory(workingTabs)
    setActiveTabInMemory(workingTabs[0].id)
    writeEditorContent(workingTabs[0].content, workingTabs[0].id, true)
  }

  function commitEditorToMemory() {
    const currentTabs = tabsRef.current
    const currentActiveId = activeIdRef.current
    const active = currentTabs.find((tab) => tab.id === currentActiveId)
    if (!active) {
      return
    }

    const content = getEditorContent(editorRef.current)
    const nextTabs = currentTabs.map((tab) =>
      tab.id === active.id ? { ...tab, content } : tab,
    )
    tabsRef.current = nextTabs
  }

  function tabsWithCurrentEditor() {
    const currentTabs = tabsRef.current
    const currentActiveId = activeIdRef.current
    const active = currentTabs.find((tab) => tab.id === currentActiveId)
    if (!active) {
      return currentTabs
    }

    const content = getEditorContent(editorRef.current) ?? active.content
    const nextTabs = currentTabs.map((tab) =>
      tab.id === active.id ? { ...tab, content } : tab,
    )
    setTabsInMemory(nextTabs)
    return nextTabs
  }

  function handleEditorLoad() {
    const frame = editorRef.current
    const doc = frame?.contentDocument
    if (!frame || !doc) {
      return
    }

    doc.documentElement.contentEditable = 'true'
    doc.body.contentEditable = 'true'
    doc.addEventListener('input', commitEditorToMemory)
    doc.addEventListener('blur', commitEditorToMemory, true)
    doc.addEventListener('paste', handleEditorPaste)

    if (activeTab) {
      writeEditorContent(activeTab.content, activeTab.id)
    }

    updateEditorBottomPadding(frame, tabBarRef.current)
  }

  async function handleEditorPaste(event: globalThis.ClipboardEvent) {
    const images = Array.from(event.clipboardData?.files ?? []).filter((file) =>
      file.type.startsWith('image/'),
    )

    if (images.length === 0) {
      window.setTimeout(commitEditorToMemory, 0)
      return
    }

    event.preventDefault()

    for (const image of images) {
      const src = await readFileAsDataUrl(image)
      insertEditorHtml(`<img src="${src}" alt="${escapeHtml(image.name)}">`)
    }

    commitEditorToMemory()
  }

  function selectTab(tabId: string) {
    tabsWithCurrentEditor()
    setActiveTabInMemory(tabId)
    setMessage('')
  }

  function createTab() {
    const nextTab = createBlankTab()
    const nextTabs = [...tabsWithCurrentEditor(), nextTab]
    setTabsInMemory(nextTabs)
    setActiveTabInMemory(nextTab.id)
    setMessage('')
  }

  function closeTab(tabId: string) {
    const currentTabs = tabsWithCurrentEditor()
    const closingTab = currentTabs.find((tab) => tab.id === tabId)
    if (!closingTab) {
      return
    }

    const title = closingTab.title || DEFAULT_TITLE
    const message =
      currentTabs.length === 1
        ? `Delete "${title}"? This will clear the current note and open a blank tab.`
        : `Delete "${title}"?`

    if (!window.confirm(message)) {
      return
    }

    setTabsInMemory((() => {
      const closingIndex = currentTabs.findIndex((tab) => tab.id === tabId)

      if (currentTabs.length === 1) {
        const nextTab = createBlankTab()
        setActiveTabInMemory(nextTab.id)
        return [nextTab]
      }

      const nextTabs = currentTabs.filter((tab) => tab.id !== tabId)
      if (activeId === tabId) {
        setActiveTabInMemory(nextTabs[Math.max(0, closingIndex - 1)]?.id ?? nextTabs[0].id)
      }

      return nextTabs
    })())

    if (editingId === tabId) {
      setEditingId(null)
      setDraftTitle('')
    }
    setMessage('')
  }

  function startRename(tab: NoteTab) {
    setEditingId(tab.id)
    setDraftTitle(tab.title)
  }

  function finishRename(tabId: string) {
    const title = draftTitle.trim() || DEFAULT_TITLE
    const nextTabs = tabsWithCurrentEditor().map((tab) =>
        tab.id === tabId ? { ...tab, title } : tab,
    )
    setTabsInMemory(nextTabs)
    setEditingId(null)
    setDraftTitle('')
  }

  function handleRenameKey(event: KeyboardEvent<HTMLInputElement>, tab: NoteTab) {
    if (event.key === 'Enter') {
      finishRename(tab.id)
    }

    if (event.key === 'Escape') {
      setEditingId(null)
      setDraftTitle('')
    }
  }

  async function saveToDatabase() {
    const nextTabs = tabsWithCurrentEditor()
    if (!auth) {
      setPendingDatabaseAction('save')
      setShowAuth(true)
      setMessage('login or register to save to database')
      return
    }

    await confirmAndSyncDatabaseNotes(auth.token, nextTabs)
  }

  async function loadFromDatabase() {
    if (!auth) {
      setPendingDatabaseAction('load')
      setShowAuth(true)
      setMessage('login or register to load database notes')
      return
    }

    await fetchDatabaseNotes(auth.token)
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

    if (response.status === 401) {
      setAuth(null)
      setShowAuth(true)
      setMessage('session expired')
      return
    }

    if (!response.ok) {
      setMessage(await readError(response))
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    if (savedNotes.length === 0) {
      databaseSnapshotRef.current = createDatabaseSnapshot([])
      replaceWorkingTabs([createBlankTab()])
      setMessage('database has no notes yet')
      return
    }

    const nextTabs = savedNotes.map(fromSavedNote)
    databaseSnapshotRef.current = createDatabaseSnapshot(nextTabs)
    replaceWorkingTabs(nextTabs)
    setMessage(`loaded ${nextTabs.length} note${nextTabs.length === 1 ? '' : 's'} from database`)
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

    if (response.status === 401) {
      setAuth(null)
      setShowAuth(true)
      setMessage('session expired')
      return
    }

    if (!response.ok) {
      setMessage(await readError(response))
      return
    }

    const savedNotes = (await response.json()) as SavedNote[]
    const syncedTabs = savedNotes.map(fromSavedNote)
    databaseSnapshotRef.current = createDatabaseSnapshot(syncedTabs)
    setTabsInMemory(syncedTabs)
    setMessage(`saved ${syncedTabs.length} note${syncedTabs.length === 1 ? '' : 's'} to database`)
  }

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const authPayload =
      authMode === 'register'
        ? authForm
        : {
            userName: authForm.userName,
            password: authForm.password,
          }

    const response = await apiRequest(`/auth/${authMode}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(authPayload),
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
    setAuthForm({ userName: '', password: '', confirmPassword: '' })
    setMessage(`${authMode === 'login' ? 'logged in' : 'registered'} as ${nextAuth.userName}`)

    if (databaseAction === 'save') {
      await confirmAndSyncDatabaseNotes(nextAuth.token, tabsWithCurrentEditor())
    }

    if (databaseAction === 'load') {
      await fetchDatabaseNotes(nextAuth.token)
    }
  }

  function logout() {
    const nextTab = createBlankTab()
    setAuth(null)
    setShowAuth(false)
    setPendingDatabaseAction(null)
    setAuthForm({ userName: '', password: '', confirmPassword: '' })
    replaceWorkingTabs([nextTab])
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

      <aside className="save-panel" contentEditable={false}>
        <button type="button" onClick={saveToDatabase}>
          save to db
        </button>
        <button type="button" onClick={loadFromDatabase}>
          load from db
        </button>
        {auth && (
          <button type="button" onClick={logout}>
            logout {auth.userName}
          </button>
        )}

        {showAuth && (
          <form onSubmit={submitAuth}>
            <input
              aria-label="username"
              autoComplete="username"
              onChange={(event) =>
                setAuthForm((current) => ({
                  ...current,
                  userName: event.target.value,
                }))
              }
              placeholder="username"
              value={authForm.userName}
            />
            <input
              aria-label="password"
              autoComplete={authMode === 'login' ? 'current-password' : 'new-password'}
              onChange={(event) =>
                setAuthForm((current) => ({
                  ...current,
                  password: event.target.value,
                }))
              }
              placeholder="password"
              type="password"
              value={authForm.password}
            />
            {authMode === 'register' && (
              <input
                aria-label="confirm password"
                autoComplete="new-password"
                onChange={(event) =>
                  setAuthForm((current) => ({
                    ...current,
                    confirmPassword: event.target.value,
                  }))
                }
                placeholder="confirm password"
                type="password"
                value={authForm.confirmPassword}
              />
            )}
            <button type="submit">{authMode}</button>
            <button
              type="button"
              onClick={() =>
                setAuthMode((current) => {
                  const nextMode = current === 'login' ? 'register' : 'login'
                  setAuthForm((form) => ({ ...form, confirmPassword: '' }))
                  return nextMode
                })
              }
            >
              {authMode === 'login' ? 'need register' : 'use login'}
            </button>
          </form>
        )}

        {message && <output className="message">{message}</output>}
      </aside>

      <nav aria-label="note tabs" className="tab-bar" contentEditable={false} ref={tabBarRef}>
        {tabs.map((tab) =>
          editingId === tab.id ? (
            <input
              aria-label="rename tab"
              autoFocus
              className="tab-rename"
              key={tab.id}
              onBlur={() => finishRename(tab.id)}
              onChange={(event) => setDraftTitle(event.target.value)}
              onKeyDown={(event) => handleRenameKey(event, tab)}
              value={draftTitle}
            />
          ) : (
            <span className="tab-item" key={tab.id}>
              <button
                aria-current={tab.id === activeId ? 'page' : undefined}
                className="tab-title"
                onClick={() => selectTab(tab.id)}
                onDoubleClick={() => startRename(tab)}
                type="button"
              >
                {tab.title}
              </button>
              <button
                aria-label={`close ${tab.title}`}
                className="tab-close"
                onClick={() => closeTab(tab.id)}
                type="button"
              >
                X
              </button>
            </span>
          ),
        )}
        <button type="button" onClick={createTab}>
          +
        </button>
      </nav>
    </div>
  )
}

function readLocalAuth() {
  const savedAuth = localStorage.getItem(LOCAL_AUTH_KEY)
  if (!savedAuth) {
    return null
  }

  try {
    return JSON.parse(savedAuth) as AuthState
  } catch {
    return null
  }
}

function createDatabaseSnapshot(tabs: NoteTab[]): DatabaseSnapshot {
  const snapshot: DatabaseSnapshot = new Map()

  for (const tab of tabs) {
    snapshot.set(tab.id, {
      title: tab.title,
      content: tab.content,
    })
  }

  return snapshot
}

function getDatabaseChangeSummary(
  currentTabs: NoteTab[],
  snapshot: DatabaseSnapshot | null,
): DatabaseChangeSummary {
  if (!snapshot) {
    return {
      snapshotKnown: false,
      currentTabs: currentTabs.map((tab) => formatTabName(tab.title)),
      createdTabs: [],
      changedTabs: [],
      deletedTabs: [],
    }
  }

  const currentIds = new Set<string>()
  const createdTabs: string[] = []
  const changedTabs: string[] = []

  for (const tab of currentTabs) {
    currentIds.add(tab.id)

    const savedTab = snapshot.get(tab.id)
    if (!savedTab) {
      createdTabs.push(formatTabName(tab.title))
      continue
    }

    if (savedTab.title !== tab.title || savedTab.content !== tab.content) {
      changedTabs.push(formatTabName(tab.title))
    }
  }

  const deletedTabs: string[] = []
  for (const [tabId, savedTab] of snapshot) {
    if (!currentIds.has(tabId)) {
      deletedTabs.push(formatTabName(savedTab.title))
    }
  }

  return {
    snapshotKnown: true,
    currentTabs: [],
    createdTabs,
    changedTabs,
    deletedTabs,
  }
}

function hasDatabaseChanges(summary: DatabaseChangeSummary) {
  if (!summary.snapshotKnown) {
    return summary.currentTabs.length > 0
  }

  return (
    summary.createdTabs.length +
      summary.changedTabs.length +
      summary.deletedTabs.length >
    0
  )
}

function formatDatabaseSaveConfirmation(summary: DatabaseChangeSummary) {
  if (!summary.snapshotKnown) {
    return [
      'No database copy has been loaded this session.',
      'Save current tabs to database?',
      '',
      'This will replace database notes with the current tab set.',
      '',
      ...formatTabGroup('Tabs to save', summary.currentTabs, '*'),
    ].join('\n')
  }

  return [
    'Save these database changes?',
    '',
    ...formatTabGroup('New tabs', summary.createdTabs, '+'),
    ...formatTabGroup('Changed tabs', summary.changedTabs, '~'),
    ...formatTabGroup('Deleted tabs', summary.deletedTabs, '-'),
  ].join('\n')
}

function formatTabGroup(label: string, tabNames: string[], prefix: string) {
  if (tabNames.length === 0) {
    return []
  }

  const visibleTabNames = tabNames.slice(0, MAX_CONFIRM_TAB_NAMES)
  const hiddenCount = tabNames.length - visibleTabNames.length
  const lines = [
    `${label} (${tabNames.length}):`,
    ...visibleTabNames.map((tabName) => `${prefix} ${tabName}`),
  ]

  if (hiddenCount > 0) {
    lines.push(`${prefix} ...and ${hiddenCount} more`)
  }

  lines.push('')
  return lines
}

function formatTabName(title: string) {
  const name = title.trim().replace(/\s+/g, ' ') || DEFAULT_TITLE

  if (name.length <= 80) {
    return name
  }

  return `${name.slice(0, 77)}...`
}

function createBlankTab(): NoteTab {
  return {
    id: crypto.randomUUID?.() ?? Math.random().toString(36).slice(2),
    title: DEFAULT_TITLE,
    content: '',
  }
}

function fromSavedNote(note: SavedNote): NoteTab {
  return {
    id: note.id,
    title: note.title || DEFAULT_TITLE,
    content: normalizeContent(note.content),
  }
}

function normalizeContent(content: string | undefined) {
  if (!content) {
    return ''
  }

  if (content.includes('<')) {
    return content
  }

  return content.replace(/\n/g, '<br>')
}

function getEditorDocument(editor: HTMLIFrameElement | null) {
  return editor?.contentDocument ?? null
}

function getEditorContent(editor: HTMLIFrameElement | null) {
  return getEditorDocument(editor)?.body?.innerHTML ?? ''
}

function writeEditorContent(content: string, tabId: string, force = false) {
  const frame = document.querySelector<HTMLIFrameElement>('.editor')
  const doc = getEditorDocument(frame)
  if (!frame || !doc?.body) {
    return
  }

  if (!force && frame.dataset.tabId === tabId && doc.hasFocus()) {
    return
  }

  doc.body.innerHTML = content
  frame.dataset.tabId = tabId
}

function updateEditorBottomPadding(editor: HTMLIFrameElement | null, tabBar: HTMLElement | null) {
  const doc = getEditorDocument(editor)
  if (!doc?.body || !tabBar) {
    return
  }

  const tabBarHeight = Math.ceil(tabBar.getBoundingClientRect().height)
  doc.body.style.paddingBottom = `${tabBarHeight + 8}px`
}

function insertEditorHtml(html: string) {
  const frame = document.querySelector<HTMLIFrameElement>('.editor')
  const doc = getEditorDocument(frame)
  if (!doc?.body) {
    return
  }

  const selection = doc.getSelection()
  if (!selection || selection.rangeCount === 0) {
    doc.body.insertAdjacentHTML('beforeend', html)
    return
  }

  const range = selection.getRangeAt(0)
  range.deleteContents()

  const fragment = range.createContextualFragment(html)
  const lastNode = fragment.lastChild
  range.insertNode(fragment)

  if (lastNode) {
    range.setStartAfter(lastNode)
    range.collapse(true)
    selection.removeAllRanges()
    selection.addRange(range)
  }
}

function readFileAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader()
    reader.addEventListener('load', () => resolve(String(reader.result ?? '')))
    reader.addEventListener('error', () => reject(reader.error))
    reader.readAsDataURL(file)
  })
}

function escapeHtml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

async function apiRequest(path: string, init?: RequestInit) {
  try {
    return await fetch(`${API_BASE}${path}`, init)
  } catch {
    return null
  }
}

async function readError(response: Response) {
  try {
    const problem = await response.json()
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(' ')
    }

    return problem.title ?? problem.detail ?? 'request failed'
  } catch {
    try {
      return await response.text()
    } catch {
      return 'request failed'
    }
  }
}

export default App
