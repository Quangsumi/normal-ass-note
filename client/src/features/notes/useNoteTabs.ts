import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { DEFAULT_TITLE } from '../../shared/constants'
import {
  escapeHtml,
  getEditorContent,
  insertEditorHtml,
  readFileAsDataUrl,
  updateEditorBottomPadding,
  writeEditorContent,
} from '../editor/editorDom'
import { createBlankTab } from './notes'
import type { NoteTab } from '../../shared/types'

type EnsureTabContent = (
  tabId: string,
  currentTabs: NoteTab[],
) => NoteTab[] | null | Promise<NoteTab[] | null>

export function useNoteTabs(
  clearMessage: () => void,
  ensureTabContent?: EnsureTabContent,
) {
  const [tabs, setTabs] = useState<NoteTab[]>(() => [createBlankTab()])
  const [activeId, setActiveId] = useState(() => tabs[0]?.id ?? '')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draftTitle, setDraftTitle] = useState('')

  const activeIdRef = useRef(activeId)
  const editorRef = useRef<HTMLIFrameElement>(null)
  const tabBarRef = useRef<HTMLElement>(null)
  const tabsRef = useRef(tabs)

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
    if (activeTabId && activeTab?.contentLoaded) {
      writeEditorContent(editorRef.current, activeTabContent ?? '', activeTabId)
    }
  }, [activeTab?.contentLoaded, activeTabContent, activeTabId])

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

  function setTabsInMemory(nextTabs: NoteTab[]) {
    tabsRef.current = nextTabs
    setTabs(nextTabs)
  }

  function setActiveTabInMemory(tabId: string) {
    activeIdRef.current = tabId
    setActiveId(tabId)
  }

  function replaceWorkingTabs(nextTabs: NoteTab[], nextActiveId?: string) {
    const workingTabs = nextTabs.length > 0 ? nextTabs : [createBlankTab()]
    const nextActiveTab =
      workingTabs.find((tab) => tab.id === nextActiveId) ?? workingTabs[0]

    setTabsInMemory(workingTabs)
    setActiveTabInMemory(nextActiveTab.id)

    if (nextActiveTab.contentLoaded) {
      writeEditorContent(editorRef.current, nextActiveTab.content, nextActiveTab.id, true)
    }
  }

  function resetTabs() {
    replaceWorkingTabs([createBlankTab()])
  }

  function commitEditorToMemory() {
    const currentTabs = tabsRef.current
    const active = currentTabs.find((tab) => tab.id === activeIdRef.current)
    if (!active || !active.contentLoaded) {
      return
    }

    const content = getEditorContent(editorRef.current)
    tabsRef.current = currentTabs.map((tab) =>
      tab.id === active.id ? { ...tab, content, contentLoaded: true } : tab,
    )
  }

  function tabsWithCurrentEditor() {
    const currentTabs = tabsRef.current
    const active = currentTabs.find((tab) => tab.id === activeIdRef.current)
    if (!active || !active.contentLoaded) {
      return currentTabs
    }

    const content = getEditorContent(editorRef.current)
    const nextTabs = currentTabs.map((tab) =>
      tab.id === active.id ? { ...tab, content, contentLoaded: true } : tab,
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

    if (activeTab?.contentLoaded) {
      writeEditorContent(frame, activeTab.content, activeTab.id)
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
      insertEditorHtml(editorRef.current, `<img src="${src}" alt="${escapeHtml(image.name)}">`)
    }

    commitEditorToMemory()
  }

  async function selectTab(tabId: string) {
    const currentTabs = tabsWithCurrentEditor()
    const readyTabs = await prepareTabsForActivation(tabId, currentTabs)
    if (!readyTabs) {
      return
    }

    setTabsInMemory(readyTabs)
    setActiveTabInMemory(tabId)
    clearMessage()
  }

  function createTab() {
    const nextTab = createBlankTab()
    setTabsInMemory([...tabsWithCurrentEditor(), nextTab])
    setActiveTabInMemory(nextTab.id)
    clearMessage()
  }

  async function closeTab(tabId: string) {
    const currentTabs = tabsWithCurrentEditor()
    const closingTab = currentTabs.find((tab) => tab.id === tabId)
    if (!closingTab || !confirmTabDelete(closingTab, currentTabs.length)) {
      return
    }

    const closingIndex = currentTabs.findIndex((tab) => tab.id === tabId)
    const nextTabs =
      currentTabs.length === 1 ? [createBlankTab()] : currentTabs.filter((tab) => tab.id !== tabId)

    let workingTabs = nextTabs
    let nextActiveId = activeIdRef.current

    if (activeId === tabId || currentTabs.length === 1) {
      const nextActiveIndex = Math.max(0, closingIndex - 1)
      nextActiveId = nextTabs[nextActiveIndex]?.id ?? nextTabs[0].id
      const readyTabs = await prepareTabsForActivation(nextActiveId, nextTabs)
      if (!readyTabs) {
        return
      }

      workingTabs = readyTabs
    }

    setTabsInMemory(workingTabs)
    setActiveTabInMemory(nextActiveId)

    if (editingId === tabId) {
      setEditingId(null)
      setDraftTitle('')
    }
    clearMessage()
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

  async function prepareTabsForActivation(tabId: string, currentTabs: NoteTab[]) {
    const tab = currentTabs.find((currentTab) => currentTab.id === tabId)
    if (!tab || tab.contentLoaded || !ensureTabContent) {
      return currentTabs
    }

    return ensureTabContent(tabId, currentTabs)
  }

  return {
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
  }
}

function confirmTabDelete(tab: NoteTab, tabCount: number) {
  const title = tab.title || DEFAULT_TITLE
  const message =
    tabCount === 1
      ? `Delete "${title}"? This will clear the current note and open a blank tab.`
      : `Delete "${title}"?`

  return window.confirm(message)
}
