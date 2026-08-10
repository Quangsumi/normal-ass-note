import { apiRequest, readError } from '../../shared/api'
import { API_BASE } from '../../shared/constants'
import type { OidcSessionResponse, OidcSessionState } from '../../shared/types'

export async function fetchOidcSession(): Promise<OidcSessionState> {
  const response = await apiRequest('/auth/session')

  if (!response) {
    return {
      status: 'unavailable',
      error: `OIDC session check failed: API not reachable at ${API_BASE}`,
    }
  }

  if (!response.ok) {
    return {
      status: 'unavailable',
      error: await readError(response),
    }
  }

  try {
    const session = (await response.json()) as OidcSessionResponse

    if (!session.csrfToken || typeof session.authenticated !== 'boolean') {
      throw new Error('invalid OIDC session response')
    }

    return session.authenticated
      ? { status: 'authenticated', ...session }
      : { status: 'anonymous', ...session }
  } catch {
    return {
      status: 'unavailable',
      error: 'OIDC session check returned an invalid response',
    }
  }
}

export function startOidcLogin(returnUrl = '/') {
  const loginUrl = new URL(`${API_BASE}/auth/login`, window.location.origin)
  loginUrl.searchParams.set('returnUrl', returnUrl)
  window.location.assign(loginUrl)
}

export function submitOidcLogout(csrfToken: string) {
  const form = document.createElement('form')
  form.method = 'post'
  form.action = new URL(`${API_BASE}/auth/logout`, window.location.origin).toString()

  const antiforgeryInput = document.createElement('input')
  antiforgeryInput.type = 'hidden'
  antiforgeryInput.name = '__RequestVerificationToken'
  antiforgeryInput.value = csrfToken

  form.appendChild(antiforgeryInput)
  document.body.appendChild(form)
  form.submit()
}
