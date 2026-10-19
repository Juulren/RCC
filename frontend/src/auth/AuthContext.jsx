import { createContext, useCallback, useContext, useEffect, useState } from 'react'

const AuthContext = createContext(null)

export class AuthError extends Error {
  constructor(code) {
    super(code)
    this.code = code
  }
}

async function readSession(signal) {
  const response = await fetch('/api/auth/me', {
    credentials: 'same-origin',
    cache: 'no-store',
    signal,
  })
  if (response.status === 401) return null
  if (!response.ok) throw new AuthError('NETWORK')
  const user = await response.json()
  if (!user.id || !user.email || !Array.isArray(user.roles)) throw new AuthError('NETWORK')
  return user
}

export async function authRequest(action, body) {
  try {
    // Tokens are identity-bound: request a fresh one after every login/logout.
    const tokenResponse = await fetch('/api/auth/csrf', {
      credentials: 'same-origin',
      cache: 'no-store',
    })
    if (!tokenResponse.ok) throw new AuthError('NETWORK')
    const { token } = await tokenResponse.json()
    if (!token) throw new AuthError('NETWORK')
    const response = await fetch(`/api/auth/${action}`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify(body ?? {}),
    })
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}))
      throw new AuthError(
        response.status === 429
          ? 'RATE_LIMITED'
          : (problem.code ?? (response.status === 401 ? 'SESSION_EXPIRED' : 'NETWORK')),
      )
    }
  } catch (error) {
    throw error instanceof AuthError ? error : new AuthError('NETWORK')
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [status, setStatus] = useState('loading')
  const refresh = useCallback(async () => {
    try {
      const current = await readSession()
      setUser(current)
      setStatus(current ? 'authenticated' : 'anonymous')
      return current
    } catch (error) {
      setStatus('error')
      throw error
    }
  }, [])
  useEffect(() => {
    const controller = new AbortController()
    readSession(controller.signal)
      .then((current) => {
        if (controller.signal.aborted) return
        setUser(current)
        setStatus(current ? 'authenticated' : 'anonymous')
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('error')
      })
    const onFocus = () => {
      refresh().catch(() => {})
    }
    window.addEventListener('focus', onFocus)
    return () => {
      controller.abort()
      window.removeEventListener('focus', onFocus)
    }
  }, [refresh])
  const login = async (credentials) => {
    await authRequest('login', credentials)
    await refresh()
  }
  const logout = async () => {
    try {
      await authRequest('logout')
    } catch (error) {
      if (error.code !== 'SESSION_EXPIRED') throw error
    }
    setUser(null)
    setStatus('anonymous')
  }
  return (
    <AuthContext.Provider value={{ user, status, login, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  )
}

export const useAuth = () => useContext(AuthContext)
