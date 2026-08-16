export type NoteTab = {
  id: string
  title: string
  content: string
  contentLoaded: boolean
}

export type AuthMethod = 'oidc' | 'legacy'

export type LegacyAuthState = {
  userName: string
  token: string
}

export type LegacyAuthFormState = {
  userName: string
  password: string
  confirmPassword: string
}

export type LegacyAuthMode = 'login' | 'register'

export type OidcSessionDebug = {
  issuer?: string | null
  subject?: string | null
  sessionId?: string | null
  appUserId?: string | null
  cookieExpiresAtUtc?: string | null
}

export type OidcSessionResponse =
  | {
      authenticated: false
      csrfToken: string
    }
  | {
      authenticated: true
      userName: string
      displayName: string
      email?: string | null
      csrfToken: string
      debug?: OidcSessionDebug | null
    }

export type OidcSessionState =
  | { status: 'loading' }
  | { status: 'unavailable'; error: string }
  | ({ status: 'anonymous' } & Extract<OidcSessionResponse, { authenticated: false }>)
  | ({ status: 'authenticated' } & Extract<OidcSessionResponse, { authenticated: true }>)

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
