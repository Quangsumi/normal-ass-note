import { DEFAULT_TITLE } from '../../shared/constants'
import type { NoteTab, SavedNote } from '../../shared/types'

export function createBlankTab(): NoteTab {
  return {
    id: crypto.randomUUID?.() ?? Math.random().toString(36).slice(2),
    title: DEFAULT_TITLE,
    content: '',
  }
}

export function fromSavedNote(note: SavedNote): NoteTab {
  return {
    id: note.id,
    title: note.title || DEFAULT_TITLE,
    content: normalizeContent(note.content),
  }
}

export function normalizeContent(content: string | undefined) {
  if (!content) {
    return ''
  }

  if (content.includes('<')) {
    return content
  }

  return content.replace(/\n/g, '<br>')
}
