import { ACTIVE_AUTH_METHOD_KEY, LEGACY_AUTH_KEY } from '../../shared/constants'
import type { AuthMethod, LegacyAuthState } from '../../shared/types'

export function readLegacyAuth() {
  const savedAuth = localStorage.getItem(LEGACY_AUTH_KEY)
  if (!savedAuth) {
    return null
  }

  try {
    const auth = JSON.parse(savedAuth) as Partial<LegacyAuthState>
    return typeof auth.userName === 'string' && typeof auth.token === 'string'
      ? (auth as LegacyAuthState)
      : null
  } catch {
    return null
  }
}

export function writeLegacyAuth(auth: LegacyAuthState | null) {
  if (auth) {
    localStorage.setItem(LEGACY_AUTH_KEY, JSON.stringify(auth))
    return
  }

  localStorage.removeItem(LEGACY_AUTH_KEY)
}

export function readActiveAuthMethod(): AuthMethod {
  return localStorage.getItem(ACTIVE_AUTH_METHOD_KEY) === 'legacy' ? 'legacy' : 'oidc'
}

export function writeActiveAuthMethod(authMethod: AuthMethod) {
  localStorage.setItem(ACTIVE_AUTH_METHOD_KEY, authMethod)
}
