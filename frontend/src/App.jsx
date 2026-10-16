import { useEffect, useState } from 'react'

export default function App() {
  const [status, setStatus] = useState('loading')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setStatus('loading')
    fetch('/api/status', { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error('API unavailable')
        const result = await response.json()
        if (result.application !== 'RCC') throw new Error('Unexpected response')
        if (!controller.signal.aborted) setStatus('connected')
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('error')
      })
    return () => controller.abort()
  }, [attempt])

  return (
    <main>
      <header><a className="brand" href="/" aria-label="RCC, inicio">RCC<span>CHONGQING</span></a><span className="badge">DEMO ACADÉMICA</span></header>
      <section aria-labelledby="title">
        <p className="eyebrow">01 / BASE TÉCNICA</p>
        <h1 id="title">El comienzo de<br /><em>otra perspectiva.</em></h1>
        <p className="intro">Estamos construyendo una experiencia de alojamiento inspirada en Chongqing. La base del proyecto está lista para desarrollar el diseño y las reservas por módulos.</p>
        <div className="connection">
          <span className={`indicator ${status}`} aria-hidden="true" />
          <p role="status">{status === 'loading' ? 'Comprobando conexión con la API…' : status === 'connected' ? 'Frontend conectado con la API C#.' : 'No se pudo conectar con la API.'}</p>
          {status === 'error' && <button onClick={() => setAttempt((value) => value + 1)}>Reintentar</button>}
        </div>
      </section>
      <footer>Proyecto independiente de demostración. Las reservas y los pagos todavía no están habilitados.</footer>
    </main>
  )
}
