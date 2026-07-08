import type { Dispatch, FormEvent, SetStateAction } from 'react'
import type { AuthFormState, AuthMode, AuthState } from '../../shared/types'

type SavePanelProps = {
  auth: AuthState | null
  authForm: AuthFormState
  authMode: AuthMode
  message: string
  showAuth: boolean
  onAuthFormChange: Dispatch<SetStateAction<AuthFormState>>
  onLoadFromDatabase: () => void | Promise<void>
  onLogout: () => void
  onSaveToDatabase: () => void | Promise<void>
  onSubmitAuth: (event: FormEvent<HTMLFormElement>) => void | Promise<void>
  onToggleAuthMode: () => void
}

export function SavePanel({
  auth,
  authForm,
  authMode,
  message,
  showAuth,
  onAuthFormChange,
  onLoadFromDatabase,
  onLogout,
  onSaveToDatabase,
  onSubmitAuth,
  onToggleAuthMode,
}: SavePanelProps) {
  function updateAuthField(field: keyof AuthFormState, value: string) {
    onAuthFormChange((current) => ({
      ...current,
      [field]: value,
    }))
  }

  return (
    <aside className="save-panel" contentEditable={false}>
      <button type="button" onClick={() => void onSaveToDatabase()}>
        save to db
      </button>
      <button type="button" onClick={() => void onLoadFromDatabase()}>
        load from db
      </button>
      {auth && (
        <button type="button" onClick={onLogout}>
          logout {auth.userName}
        </button>
      )}

      {showAuth && (
        <form onSubmit={(event) => void onSubmitAuth(event)}>
          <input
            aria-label="username"
            autoComplete="username"
            onChange={(event) => updateAuthField('userName', event.target.value)}
            placeholder="username"
            value={authForm.userName}
          />
          <input
            aria-label="password"
            autoComplete={authMode === 'login' ? 'current-password' : 'new-password'}
            onChange={(event) => updateAuthField('password', event.target.value)}
            placeholder="password"
            type="password"
            value={authForm.password}
          />
          {authMode === 'register' && (
            <input
              aria-label="confirm password"
              autoComplete="new-password"
              onChange={(event) => updateAuthField('confirmPassword', event.target.value)}
              placeholder="confirm password"
              type="password"
              value={authForm.confirmPassword}
            />
          )}
          <button type="submit">{authMode}</button>
          <button type="button" onClick={onToggleAuthMode}>
            {authMode === 'login' ? 'need register' : 'use login'}
          </button>
        </form>
      )}

      {message && <output className="message">{message}</output>}
    </aside>
  )
}
