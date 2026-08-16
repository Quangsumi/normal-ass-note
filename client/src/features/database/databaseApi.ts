import { apiRequest } from '../../shared/api'
import type { NoteSyncRequest } from '../../shared/types'

export function oidcListNotes(contentNoteId?: string) {
  const query = contentNoteId
    ? `?contentNoteId=${encodeURIComponent(contentNoteId)}`
    : ''
  return apiRequest(`/notes${query}`)
}

export function oidcGetNote(noteId: string) {
  return apiRequest(`/notes/${encodeURIComponent(noteId)}`)
}

export function oidcSyncNotes(request: NoteSyncRequest, csrfToken: string) {
  return apiRequest('/notes/sync', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': csrfToken,
    },
    body: JSON.stringify(request),
  })
}

export function legacyListNotes(token: string, contentNoteId?: string) {
  const query = contentNoteId
    ? `?contentNoteId=${encodeURIComponent(contentNoteId)}`
    : ''
  return apiRequest(`/legacy/notes${query}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  })
}

export function legacyGetNote(token: string, noteId: string) {
  return apiRequest(`/legacy/notes/${encodeURIComponent(noteId)}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  })
}

export function legacySyncNotes(token: string, request: NoteSyncRequest) {
  return apiRequest('/legacy/notes/sync', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}
