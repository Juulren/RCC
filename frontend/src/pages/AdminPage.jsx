import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import { authCopy } from '../auth/translations.js'
import './auth.css'

export default function AdminPage() {
  const [language, setLanguage] = usePageLanguage()
  const t = authCopy[language]
  const [status, setStatus] = useState('loading')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    setStatus('loading')
    fetch('/api/admin/access', {
      credentials: 'same-origin',
      cache: 'no-store',
      signal: controller.signal,
    })
      .then(async (response) => {
        if (response.status === 401) setStatus('anonymous')
        else if (response.status === 403) setStatus('denied')
        else if (response.ok && (await response.json()).role === 'Admin') setStatus('allowed')
        else setStatus('error')
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('error')
      })
    return () => controller.abort()
  }, [attempt])
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
