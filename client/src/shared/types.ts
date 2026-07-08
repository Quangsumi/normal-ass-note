export type NoteTab = {
  id: string
  title: string
  content: string
  contentLoaded: boolean
}

export type AuthState = {
  userName: string
  token: string
}

export type AuthFormState = {
  userName: string
  password: string
  confirmPassword: string
}

export type AuthMode = 'login' | 'register'
export type DatabaseAction = 'save' | 'load'
export type SavedNote = {
  id: string
  title: string
  content?: string | null
  sortOrder?: number
}

export type NoteSyncInput = {
  id: string
  title: string
  content?: string
  sortOrder: number
}

export type NoteSyncRequest = {
  notes: NoteSyncInput[]
  deletedNoteIds: string[]
}
