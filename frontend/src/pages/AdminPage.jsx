import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import { authCopy } from '../auth/translations.js'
import './auth.css'
import AdminCatalog from '../catalog/AdminCatalog.jsx'
import { useAuth } from '../auth/AuthContext.jsx'
import ReservationList from '../reservations/ReservationList.jsx'

export default function AdminPage() {
  const [language, setLanguage] = usePageLanguage()
  const t = authCopy[language]
  const auth = useAuth()
  const [status, setStatus] = useState('loading')
  const [attempt, setAttempt] = useState(0)
  const onAccessLost = useCallback((code) => setStatus(code === 401 ? 'anonymous' : 'denied'), [])
  useEffect(() => {
    if (auth.status === 'loading') {
      setStatus('loading')
      return
    }
    if (auth.status === 'anonymous') {
      setStatus('anonymous')
      return
    }
    if (auth.status === 'error') {
      setStatus('error')
      return
    }
    const controller = new AbortController()
    setStatus('loading')
    fetch('/api/admin/access', {
      credentials: 'same-origin',
      cache: 'no-store',
      signal: controller.signal,
    })
      .then(async (response) => {
        if (controller.signal.aborted) return
        if (response.status === 401) setStatus('anonymous')
        else if (response.status === 403) setStatus('denied')
        else if (response.ok && (await response.json()).role === 'Admin') setStatus('allowed')
        else setStatus('error')
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('error')
      })
    return () => controller.abort()
  }, [attempt, auth.user?.id, auth.status])
  return (
    <PageLayout language={language} setLanguage={setLanguage} title={t.adminTitle}>
      <section className="shell section admin-access">
        <p className="eyebrow">RCC / ADMIN</p>
        <h1>{t.adminTitle}</h1>
        <p role="status">
          {status === 'loading'
            ? t.loading
            : status === 'allowed'
              ? t.adminIntro
              : status === 'denied'
                ? t.denied
                : status === 'anonymous'
                  ? t.signInFirst
                  : t.unavailable}
        </p>
        {status === 'allowed' && <AdminCatalog language={language} onAccessLost={onAccessLost} />}
        {status === 'allowed' && (
          <section>
            <h2>{language === 'es' ? 'Reservas del hotel' : 'Hotel reservations'}</h2>
            <ReservationList language={language} admin />
          </section>
        )}
        {status === 'error' && (
          <button className="button button-outline" onClick={() => setAttempt(attempt + 1)}>
            {t.retry}
          </button>
        )}
        <Link className="button button-cyan" to="/cuenta">
          {language === 'es' ? 'Mi estancia' : 'My stay'}
        </Link>
      </section>
    </PageLayout>
  )
}
