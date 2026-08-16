import type { Dispatch, FormEvent, SetStateAction } from 'react'
import type {
  AuthMethod,
  LegacyAuthFormState,
  LegacyAuthMode,
  LegacyAuthState,
  OidcSessionState,
} from '../../shared/types'

type SavePanelProps = {
  activeAuthMethod: AuthMethod
  legacyAuth: LegacyAuthState | null
  legacyAuthForm: LegacyAuthFormState
  legacyAuthMode: LegacyAuthMode
  message: string
  oidcSession: OidcSessionState
  showLegacyAuth: boolean
  onLegacyAuthFormChange: Dispatch<SetStateAction<LegacyAuthFormState>>
  onLegacyLogout: () => void
  onLoadFromDatabase: () => void | Promise<void>
  onOidcLogin: () => void
  onOidcLogout: () => void
  onRefreshOidcSession: () => void | Promise<void>
  onSaveToDatabase: () => void | Promise<void>
  onSelectAuthMethod: (authMethod: AuthMethod) => void
  onSubmitLegacyAuth: (event: FormEvent<HTMLFormElement>) => void | Promise<void>
  onToggleLegacyAuth: () => void
  onToggleLegacyAuthMode: () => void
}

export function SavePanel({
  activeAuthMethod,
  legacyAuth,
  legacyAuthForm,
  legacyAuthMode,
  message,
  oidcSession,
  showLegacyAuth,
  onLegacyAuthFormChange,
  onLegacyLogout,
  onLoadFromDatabase,
  onOidcLogin,
  onOidcLogout,
  onRefreshOidcSession,
  onSaveToDatabase,
  onSelectAuthMethod,
  onSubmitLegacyAuth,
  onToggleLegacyAuth,
  onToggleLegacyAuthMode,
}: SavePanelProps) {
  function updateLegacyAuthField(field: keyof LegacyAuthFormState, value: string) {
    onLegacyAuthFormChange((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const activeAuthLabel = activeAuthMethod === 'oidc' ? 'OIDC cookie' : 'legacy JWT'

  return (
    <aside className="save-panel" contentEditable={false}>
      <div className="database-actions">
        <span>database auth: {activeAuthLabel}</span>
        <button type="button" onClick={() => void onSaveToDatabase()}>
          save to db
        </button>
        <button type="button" onClick={() => void onLoadFromDatabase()}>
          load from db
        </button>
      </div>

      <fieldset className={activeAuthMethod === 'oidc' ? 'auth-method active' : 'auth-method'}>
        <legend>OIDC / Keycloak</legend>

        {oidcSession.status === 'loading' && <span>checking OIDC session...</span>}

        {oidcSession.status === 'unavailable' && (
          <>
            <span>OIDC session unavailable</span>
            <button type="button" onClick={() => void onRefreshOidcSession()}>
              retry OIDC session
            </button>
          </>
        )}

        {oidcSession.status === 'anonymous' && (
          <>
            <span>not signed in with OIDC</span>
            <button type="button" onClick={onOidcLogin}>
              login with OIDC
            </button>
          </>
        )}

        {oidcSession.status === 'authenticated' && (
          <>
            <span>
              OIDC user: {oidcSession.displayName}
              {oidcSession.displayName !== oidcSession.userName
                ? ` (${oidcSession.userName})`
                : ''}
            </span>
            <button type="button" onClick={onOidcLogout}>
              logout OIDC
            </button>
          </>
        )}

        <button
          type="button"
          disabled={activeAuthMethod === 'oidc'}
          onClick={() => onSelectAuthMethod('oidc')}
        >
          {activeAuthMethod === 'oidc' ? 'using OIDC for database' : 'use OIDC for database'}
        </button>
      </fieldset>

      <fieldset className={activeAuthMethod === 'legacy' ? 'auth-method active' : 'auth-method'}>
        <legend>Legacy username/password JWT</legend>

        {legacyAuth ? (
          <>
            <span>legacy JWT user: {legacyAuth.userName}</span>
            <button type="button" onClick={onLegacyLogout}>
              logout legacy JWT
            </button>
          </>
        ) : (
          <>
            <span>not signed in with legacy JWT</span>
            <button type="button" onClick={onToggleLegacyAuth}>
              {showLegacyAuth ? 'hide legacy login' : 'show legacy login'}
            </button>
          </>
        )}

        {showLegacyAuth && !legacyAuth && (
          <form onSubmit={(event) => void onSubmitLegacyAuth(event)}>
            <input
              aria-label="legacy username"
              autoComplete="username"
              onChange={(event) => updateLegacyAuthField('userName', event.target.value)}
              placeholder="legacy username"
              value={legacyAuthForm.userName}
            />
            <input
              aria-label="legacy password"
              autoComplete={legacyAuthMode === 'login' ? 'current-password' : 'new-password'}
              onChange={(event) => updateLegacyAuthField('password', event.target.value)}
              placeholder="legacy password"
              type="password"
              value={legacyAuthForm.password}
            />
            {legacyAuthMode === 'register' && (
              <input
                aria-label="confirm legacy password"
                autoComplete="new-password"
                onChange={(event) =>
                  updateLegacyAuthField('confirmPassword', event.target.value)
                }
                placeholder="confirm legacy password"
                type="password"
                value={legacyAuthForm.confirmPassword}
              />
            )}
            <button type="submit">legacy {legacyAuthMode}</button>
            <button type="button" onClick={onToggleLegacyAuthMode}>
              {legacyAuthMode === 'login'
                ? 'need legacy registration'
                : 'use legacy login'}
            </button>
          </form>
        )}

        <button
          type="button"
          disabled={activeAuthMethod === 'legacy'}
          onClick={() => onSelectAuthMethod('legacy')}
        >
          {activeAuthMethod === 'legacy'
            ? 'using legacy JWT for database'
            : 'use legacy JWT for database'}
        </button>
      </fieldset>

      {oidcSession.status === 'unavailable' && (
        <output className="message">{oidcSession.error}</output>
      )}
      {message && <output className="message">{message}</output>}
    </aside>
  )
}
