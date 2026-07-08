import { LOCAL_AUTH_KEY } from '../../shared/constants'
import type { AuthState } from '../../shared/types'

export function readLocalAuth() {
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

export function writeLocalAuth(auth: AuthState | null) {
  if (auth) {
    localStorage.setItem(LOCAL_AUTH_KEY, JSON.stringify(auth))
    return
  }

  localStorage.removeItem(LOCAL_AUTH_KEY)
}
