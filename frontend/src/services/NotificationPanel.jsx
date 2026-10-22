import { useEffect, useState } from 'react'
import { catalogApi } from '../catalog/api.js'
import './services.css'
export default function NotificationPanel({ language, onAccessLost }) {
  const es = language === 'es'
  const [data, setData] = useState(null)
  const [error, setError] = useState(false)
  const [page, setPage] = useState(1)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    setError(false)
    setData(null)
    catalogApi(`/admin/notifications?page=${page}`)
      .then((result) => {
        if (active) setData(result)
      })
      .catch((err) => {
        if (active) {
          setError(true)
          if ([401, 403].includes(err.status)) onAccessLost(err.status)
        }
      })
    return () => {
      active = false
    }
  }, [page, attempt, onAccessLost])
  const states = es
    ? { Pending: 'Pendiente', Accepted: 'Aceptado por Resend', Review: 'Requiere revisión' }
    : { Pending: 'Pending', Accepted: 'Accepted by Resend', Review: 'Review required' }
  return (
    <section className="notification-panel">
      <h2>{es ? 'Correos de reservas' : 'Reservation emails'}</h2>
      <p>
        {es
          ? 'La aceptación del proveedor no confirma la entrega en la bandeja del destinatario.'
          : 'Provider acceptance does not confirm delivery to the recipient’s inbox.'}
      </p>
      {error && (
        <p role="alert">{es ? 'No pudimos consultar los correos.' : 'Could not load emails.'}</p>
      )}
      {!data && !error && <p role="status">{es ? 'Cargando…' : 'Loading…'}</p>}
      {data && (
        <>
          {!data.configured && (
            <p role="status">
              {es
                ? 'Envío desactivado: configura Resend para procesar los pendientes.'
                : 'Sending disabled: configure Resend to process pending emails.'}
            </p>
          )}
          {!data.items.length && (
            <p>{es ? 'Todavía no hay correos en la cola.' : 'No queued emails yet.'}</p>
          )}
          <ul className="notification-list">
            {data.items.map((item) => (
              <li key={item.id}>
                <strong>
                  {item.kind === 'Confirmed'
                    ? es
                      ? 'Confirmación'
                      : 'Confirmation'
                    : es
                      ? 'Cancelación'
                      : 'Cancellation'}
                </strong>
                <code>{item.reservationId}</code>
                <span>{states[item.state]}</span>
                <small>
                  {es ? 'Intentos' : 'Attempts'}: {item.attempts}
                  {item.lastError ? ` · ${item.lastError}` : ''}
                </small>
              </li>
            ))}
          </ul>
          <div className="service-actions">
            <button
              className="button button-outline"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              {es ? 'Anterior' : 'Previous'}
            </button>
            <span>
              {page} / {Math.max(1, data.totalPages)}
            </span>
            <button
              className="button button-outline"
              disabled={page >= data.totalPages}
              onClick={() => setPage(page + 1)}
            >
              {es ? 'Siguiente' : 'Next'}
            </button>
          </div>
        </>
      )}
      <button className="text-link" onClick={() => setAttempt((a) => a + 1)}>
        {es ? 'Actualizar correos' : 'Refresh emails'}
      </button>
    </section>
  )
}
