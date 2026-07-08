export function getEditorDocument(editor: HTMLIFrameElement | null) {
  return editor?.contentDocument ?? null
}

export function getEditorContent(editor: HTMLIFrameElement | null) {
  return getEditorDocument(editor)?.body?.innerHTML ?? ''
}

export function writeEditorContent(
  editor: HTMLIFrameElement | null,
  content: string,
  tabId: string,
  force = false,
) {
  const doc = getEditorDocument(editor)
  if (!editor || !doc?.body) {
    return
  }

  if (!force && editor.dataset.tabId === tabId && doc.hasFocus()) {
    return
  }

  doc.body.innerHTML = content
  editor.dataset.tabId = tabId
}

export function insertEditorHtml(editor: HTMLIFrameElement | null, html: string) {
  const doc = getEditorDocument(editor)
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

export function updateEditorBottomPadding(
  editor: HTMLIFrameElement | null,
  tabBar: HTMLElement | null,
) {
  const doc = getEditorDocument(editor)
  if (!doc?.body || !tabBar) {
    return
  }

  const tabBarHeight = Math.ceil(tabBar.getBoundingClientRect().height)
  doc.body.style.paddingBottom = `${tabBarHeight + 8}px`
}

export function readFileAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader()
    reader.addEventListener('load', () => resolve(String(reader.result ?? '')))
    reader.addEventListener('error', () => reject(reader.error))
    reader.readAsDataURL(file)
  })
}

export function escapeHtml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}
