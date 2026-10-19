import { useEffect, useState } from 'react'

export default function ConnectionStatus({ t }) {
  const [status, setStatus] = useState(0)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const timeout = setTimeout(() => controller.abort(), 8000)
    let active = true
    setStatus(0)
    fetch('/api/status', { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok || (await response.json()).application !== 'RCC')
          throw new Error('Unavailable')
        if (active) setStatus(1)
      })
      .catch(() => {
        if (active) setStatus(2)
      })
      .finally(() => clearTimeout(timeout))
    return () => {
      active = false
      clearTimeout(timeout)
      controller.abort()
    }
  }, [attempt])
  return (
    <div className="connection">
      <span className={`status-dot status-${status}`} aria-hidden="true" />
      <span role="status">{t.api[status]}</span>
      {status === 2 && (
        <button className="text-button" onClick={() => setAttempt((value) => value + 1)}>
          {t.retry}
        </button>
      )}
    </div>
  )
}
