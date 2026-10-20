import { useState } from 'react'
import { Link } from 'react-router-dom'
import { authRequest, useAuth } from '../auth/AuthContext.jsx'
import { authCopy } from '../auth/translations.js'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import Icon from '../components/Icon.jsx'
import './auth.css'

export default function AccountPage() {
  const [language, setLanguage] = usePageLanguage()
  const t = authCopy[language]
  const auth = useAuth()
  const [mode, setMode] = useState('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [show, setShow] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState(false)
  const displayError = t.errors[error] ?? t.errors.NETWORK
  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setCreated(false)
    try {
      if (mode === 'register') {
        await authRequest('register', { email, password })
        setMode('login')
        setCreated(true)
      } else await auth.login({ email, password })
      setPassword('')
      setShow(false)
    } catch (error) {
      setError(error.code ?? 'NETWORK')
    } finally {
      setBusy(false)
    }
  }
  async function signOut() {
    setBusy(true)
    setError('')
    try {
      await auth.logout()
    } catch (error) {
      setError(error.code ?? 'NETWORK')
    } finally {
      setBusy(false)
    }
  }
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={language === 'es' ? 'Mi estancia' : 'My stay'}
    >
      <section className="shell account-layout section">
        <div className="account-intro">
          <p className="eyebrow">RCC / YOUR NEXT CHAPTER</p>
          <h1>{auth.user ? t.welcome : t.title}</h1>
          <p>{t.intro}</p>
          <div className="account-city" role="img" aria-label="Chongqing" />
          <span className="account-caption">CHONGQING · A NEW PERSPECTIVE</span>
        </div>
        <div className="account-panel">
          {auth.status === 'loading' ? (
            <p role="status">{t.loading}</p>
          ) : auth.status === 'error' ? (
            <>
              <p role="alert">{t.unavailable}</p>
              <button
                className="button button-outline"
                disabled={busy}
                onClick={async () => {
                  setBusy(true)
                  try {
                    await auth.refresh()
                  } catch {
                  } finally {
                    setBusy(false)
                  }
                }}
              >
                {t.retry}
              </button>
            </>
          ) : auth.user ? (
            <>
              <p className="eyebrow">{t.session}</p>
              <h2>{language === 'es' ? 'Bienvenido a RCC.' : 'Welcome to RCC.'}</h2>
              <p className="account-email">{auth.user.email}</p>
              <div className="account-roles">
                {auth.user.roles.map((role) => (
                  <span key={role}>{role === 'Admin' ? t.admin : t.customer}</span>
                ))}
              </div>
              <Link className="text-link" to="/mis-reservas">
                {language === 'es' ? 'Mis reservas' : 'My reservations'} →
              </Link>
              <Link className="button button-outline" to="/reservar">
                {language === 'es' ? 'Continuar reserva' : 'Continue booking'} →
              </Link>
              <Link className="button button-cyan" to="/habitaciones">
                {t.explore}
                <Icon name="arrow" />
              </Link>
              {auth.user.roles.includes('Admin') && (
                <Link className="text-link" to="/admin">
                  {t.adminLink}
                  <Icon name="diagonal" />
                </Link>
              )}
              <button className="button button-outline" onClick={signOut} disabled={busy}>
                {busy ? t.busy : t.logout}
              </button>
              <p className="account-privacy">{t.private}</p>
            </>
          ) : (
            <>
              <div
                className="auth-mode"
                aria-label={language === 'es' ? 'Acceso a la cuenta' : 'Account access'}
              >
                {['login', 'register'].map((value) => (
                  <button
                    key={value}
                    aria-pressed={mode === value}
                    disabled={busy}
                    onClick={() => {
                      setMode(value)
                      setPassword('')
                      setError('')
                      setShow(false)
                      setCreated(false)
                    }}
                  >
                    {t[value]}
                  </button>
                ))}
              </div>
              <h2>{t[mode]}</h2>
              {created && (
                <p role="status" className="auth-success">
                  {t.created}
                </p>
              )}
              <form onSubmit={submit} aria-label={t[mode]}>
                <label htmlFor="auth-email">{t.email}</label>
                <input
                  id="auth-email"
                  name="email"
                  type="email"
                  autoComplete="username"
                  required
                  maxLength={254}
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  disabled={busy}
                />
                <label htmlFor="auth-password">{t.password}</label>
                <div className="auth-password">
                  <input
                    id="auth-password"
                    name="password"
                    type={show ? 'text' : 'password'}
                    autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
                    required
                    minLength={mode === 'register' ? 12 : 1}
                    maxLength={128}
                    aria-describedby={mode === 'register' ? 'password-hint' : undefined}
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    disabled={busy}
                  />
                  <button
                    type="button"
                    aria-label={show ? t.hide : t.show}
                    aria-pressed={show}
                    onClick={() => setShow(!show)}
                    disabled={busy}
                  >
                    {show ? t.hide : t.show}
                  </button>
                </div>
                {mode === 'register' && (
                  <p id="password-hint" className="password-hint">
                    {t.hint}
                  </p>
                )}
                <button className="button button-cyan" type="submit" disabled={busy}>
                  {busy ? t.busy : mode === 'register' ? t.submitRegister : t.submitLogin}
                  <Icon name="arrow" />
                </button>
              </form>
            </>
          )}
          {error && (
            <p className="auth-error" role="alert">
              {displayError}
            </p>
          )}
        </div>
      </section>
    </PageLayout>
  )
}
