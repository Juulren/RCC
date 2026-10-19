import { useEffect, useState } from 'react'
import { catalogApi } from './api.js'

export default function HotelSummary({ language }) {
  const [hotel, setHotel] = useState(null)
  const [status, setStatus] = useState('loading')
  const [attempt, setAttempt] = useState(0)
  const es = language === 'es'
  useEffect(() => {
    const controller = new AbortController()
    setStatus('loading')
    catalogApi('/hotel', { signal: controller.signal })
      .then((data) => {
        if (!data.name || !Array.isArray(data.services)) throw new Error('Invalid hotel')
        if (!controller.signal.aborted) {
          setHotel(data)
          setStatus('ready')
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus('error')
      })
    return () => controller.abort()
  }, [attempt])
  return (
    <section
      className="shell catalog-hotel"
      aria-label={es ? 'Información del hotel' : 'Hotel information'}
    >
      {status === 'loading' ? (
        <p role="status">{es ? 'Cargando información del hotel…' : 'Loading hotel information…'}</p>
      ) : status === 'error' ? (
        <>
          <p role="alert">
            {es
              ? 'No pudimos cargar la información del hotel.'
              : 'Could not load hotel information.'}
          </p>
          <button className="text-button" onClick={() => setAttempt(attempt + 1)}>
            {es ? 'Reintentar' : 'Retry'}
          </button>
        </>
      ) : (
        <>
          <div>
            <p className="eyebrow">RCC / HOTEL</p>
            <h2>{hotel.name}</h2>
            <p>{es ? hotel.descriptionEs : hotel.descriptionEn}</p>
            <p>{hotel.address}</p>
            <small>
              {es ? 'Coordenadas aproximadas de referencia' : 'Approximate reference coordinates'}:{' '}
              {hotel.latitude}, {hotel.longitude} · {hotel.timeZone}
            </small>
          </div>
          <div>
            <p className="eyebrow">{es ? 'SERVICIOS Y CONTEXTO' : 'SERVICES & CONTEXT'}</p>
            <ul>
              {hotel.services.map((service, index) => (
                <li key={index}>{es ? service.nameEs : service.nameEn}</li>
              ))}
            </ul>
            <p>
              {es
                ? 'Proyecto académico. Servicios informativos; tarifas, capacidades e inventario son de demostración.'
                : 'Academic project. Informational services; rates, capacities and inventory are for demonstration.'}
            </p>
          </div>
        </>
      )}
    </section>
  )
}
