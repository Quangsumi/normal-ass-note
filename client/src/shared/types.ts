export type NoteTab = {
  id: string
  title: string
  content: string
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
export type SavedNote = NoteTab
