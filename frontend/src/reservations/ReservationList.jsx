import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import { catalogApi, money } from '../catalog/api.js'
import { bookingError, statusText } from './translations.js'
import PreviewDialog from '../components/PreviewDialog.jsx'
import './reservations.css'

export default function ReservationList({ language, admin = false }) {
  const es = language === 'es',
    auth = useAuth(),
    [params] = useSearchParams()
  const [data, setData] = useState(null),
    [error, setError] = useState(''),
    [page, setPage] = useState(1)
  const [filter, setFilter] = useState(''),
    [refresh, setRefresh] = useState(0),
    [selected, setSelected] = useState(null),
    [busy, setBusy] = useState(false)
  const [detail, setDetail] = useState(null),
    [detailError, setDetailError] = useState('')
  const [mutationError, setMutationError] = useState('')
  const [arrivalFrom, setArrivalFrom] = useState(''),
    [arrivalTo, setArrivalTo] = useState('')
  const prefix = admin ? '/admin/reservations' : '/reservations'
  const focused = admin ? null : params.get('reserva')
  useEffect(() => {
    if (auth.status !== 'authenticated') {
      setData(null)
      setDetail(null)
      return
    }
    const controller = new AbortController()
    setError('')
    catalogApi(
      `${prefix}?${new URLSearchParams({ page, ...(filter ? { status: filter } : {}), ...(admin && arrivalFrom ? { arrivalFrom } : {}), ...(admin && arrivalTo ? { arrivalTo } : {}) })}`,
      {
        signal: controller.signal,
      },
    )
      .then(setData)
      .catch((error) => {
        if (!controller.signal.aborted) {
          setData(null)
          setError(bookingError(error, language))
        }
      })
    return () => controller.abort()
  }, [
    auth.status,
    auth.user?.id,
    prefix,
    page,
    filter,
    refresh,
    language,
    admin,
    arrivalFrom,
    arrivalTo,
  ])
  useEffect(() => {
    if (!focused || auth.status !== 'authenticated') return
    const controller = new AbortController()
    setDetailError('')
    catalogApi(`/reservations/${encodeURIComponent(focused)}`, { signal: controller.signal })
      .then(setDetail)
      .catch((error) => {
        if (!controller.signal.aborted) {
          setDetail(null)
          setDetailError(
            error.status === 404
              ? es
                ? 'Reserva no encontrada.'
                : 'Reservation not found.'
              : bookingError(error, language),
          )
        }
      })
    return () => controller.abort()
  }, [focused, auth.status, auth.user?.id, refresh, es, language])
  useEffect(() => {
    const timer = setInterval(() => setRefresh((value) => value + 1), 30000)
    const onVisible = () => {
      if (!document.hidden) setRefresh((value) => value + 1)
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [])
  async function cancel() {
    setBusy(true)
    setMutationError('')
    try {
      await catalogApi(`/reservations/${selected.id}/cancel`, { method: 'POST', body: {} })
      setSelected(null)
      setRefresh((value) => value + 1)
    } catch (error) {
      setMutationError(bookingError(error, language))
      setSelected(null)
      setRefresh((value) => value + 1)
    } finally {
      setBusy(false)
    }
  }
  function card(reservation, featured = false) {
    return (
      <article
        className={`reservation-card ${featured ? 'reservation-featured' : ''}`}
        key={reservation.id}
      >
        <div className="reservation-card-heading">
          <div>
            <p className="eyebrow">
              {es ? 'REFERENCIA' : 'REFERENCE'} / {reservation.id.slice(0, 8)}
            </p>
            <h3>
              {reservation.arrival} → {reservation.departure}
            </h3>
          </div>
          <span className={`reservation-status status-${reservation.status}`}>
            {statusText(reservation.status, es)}
          </span>
        </div>
        <ul>
          {reservation.items.map((item) => (
            <li key={item.id}>
              <span>
                {es ? item.nameEs : item.nameEn}
                <small>
                  {item.adults} {es ? 'adultos' : 'adults'} ·{' '}
                  {es ? 'Edades de niños' : 'Children’s ages'}: {item.childAges.join(', ') || '—'}
                </small>
              </span>
              <b>{money(item.subtotal, language)}</b>
            </li>
          ))}
        </ul>
        <div className="quote-total">
          <span>Total CNY</span>
          <strong>{money(reservation.total, language)}</strong>
        </div>
        {reservation.status === 'PendingPayment' && (
          <p className="reservation-hold">
            {es ? 'Inventario bloqueado hasta' : 'Inventory held until'}{' '}
            <time dateTime={reservation.expiresAt}>
              {new Intl.DateTimeFormat(es ? 'es-MX' : 'en-GB', {
                timeZone: 'Asia/Shanghai',
                dateStyle: 'medium',
                timeStyle: 'medium',
              }).format(new Date(reservation.expiresAt))}
            </time>{' '}
            · Chongqing (UTC+8).{' '}
            {es
              ? 'El pago de prueba estará disponible en el módulo 6; esta reserva aún no está confirmada.'
              : 'Test payment will be available in module 6; this reservation is not confirmed yet.'}
          </p>
        )}
        {reservation.status === 'Expired' && (
          <p>
            {es
              ? 'El bloqueo venció y el inventario se liberó.'
              : 'The hold expired and inventory was released.'}
          </p>
        )}
        {!admin && reservation.canCancel && (
          <button className="button button-outline" onClick={() => setSelected(reservation)}>
            {es ? 'Cancelar reserva' : 'Cancel reservation'}
          </button>
        )}
        {!admin && reservation.status === 'PendingPayment' && !reservation.canCancel && (
          <p>
            {es
              ? 'La cancelación cierra el día de entrada, según la fecha de Chongqing.'
              : 'Cancellation closes on the arrival date in Chongqing.'}
          </p>
        )}
      </article>
    )
  }
  if (auth.status !== 'authenticated')
    return (
      <div className="booking-note">
        <p>
          {auth.status === 'loading'
            ? es
              ? 'Consultando sesión…'
              : 'Checking session…'
            : es
              ? 'Inicia sesión para consultar tus reservas.'
              : 'Sign in to view your reservations.'}
        </p>
        <Link className="button button-cyan" to="/cuenta">
          {es ? 'Mi estancia' : 'My stay'}
        </Link>
      </div>
    )
  return (
    <div className="reservation-list">
      {admin && (
        <div className="booking-dates">
          <label>
            {es ? 'Llegada desde' : 'Arrival from'}
            <input
              type="date"
              value={arrivalFrom}
              onChange={(e) => {
                setArrivalFrom(e.target.value)
                setPage(1)
              }}
            />
          </label>
          <label>
            {es ? 'Llegada hasta' : 'Arrival to'}
            <input
              type="date"
              value={arrivalTo}
              onChange={(e) => {
                setArrivalTo(e.target.value)
                setPage(1)
              }}
            />
          </label>
        </div>
      )}
      <div className="booking-actions">
        <button
          className="button button-outline"
          disabled={busy}
          onClick={() => setRefresh((value) => value + 1)}
        >
          {es ? 'Actualizar reservas' : 'Refresh reservations'}
        </button>
        {admin && (
          <label>
            {es ? 'Estado de reserva' : 'Reservation status'}
            <select
              value={filter}
              onChange={(e) => {
                setFilter(e.target.value)
                setPage(1)
                setData(null)
              }}
            >
              <option value="">{es ? 'Todos' : 'All'}</option>
              {['PendingPayment', 'Confirmed', 'Expired', 'Cancelled'].map((value) => (
                <option key={value} value={value}>
                  {statusText(value, es)}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
      {error && (
        <p role="alert" className="booking-error">
          {error}
        </p>
      )}
      {mutationError && (
        <p role="alert" className="booking-error">
          {mutationError}
        </p>
      )}
      {detailError && (
        <p role="alert" className="booking-error">
          {detailError}
        </p>
      )}
      {detail && (
        <section aria-label={es ? 'Reserva seleccionada' : 'Selected reservation'}>
          <h2>{es ? 'Tu reserva' : 'Your reservation'}</h2>
          {card(detail, true)}
        </section>
      )}
      {!data && !error && (
        <p role="status">{es ? 'Cargando reservas…' : 'Loading reservations…'}</p>
      )}
      {data && (
        <>
          <p role="status">
            {data.total} {es ? 'reservas' : 'reservations'} ·{' '}
            {es ? 'Actualización automática cada 30 segundos' : 'Refreshes every 30 seconds'}
          </p>
          {data.items.length === 0 && (
            <p>
              {es
                ? 'Todavía no hay reservas en esta selección.'
                : 'No reservations in this selection yet.'}
            </p>
          )}
          {data.items.filter((item) => item.id !== detail?.id).map((item) => card(item))}
          {data.totalPages > 1 && (
            <nav
              className="catalog-pagination"
              aria-label={es ? 'Páginas de reservas' : 'Reservation pages'}
            >
              <button
                className="button button-outline"
                disabled={page <= 1 || busy}
                onClick={() => {
                  setPage(page - 1)
                  setData(null)
                }}
              >
                {es ? 'Anterior' : 'Previous'}
              </button>
              <span>
                {page} / {data.totalPages}
              </span>
              <button
                className="button button-outline"
                disabled={page >= data.totalPages || busy}
                onClick={() => {
                  setPage(page + 1)
                  setData(null)
                }}
              >
                {es ? 'Siguiente' : 'Next'}
              </button>
            </nav>
          )}
        </>
      )}
      {selected && (
        <PreviewDialog
          title={es ? '¿Cancelar esta reserva?' : 'Cancel this reservation?'}
          onClose={() => setSelected(null)}
          closeDisabled={busy}
          closeLabel={es ? 'Cerrar' : 'Close'}
        >
          <p>
            {es
              ? 'Se liberarán todas las habitaciones. Para cambiar las fechas, cancela y crea otra reserva. Esta operación no se puede deshacer.'
              : 'All rooms will be released. To change dates, cancel and make another reservation. This action cannot be undone.'}
          </p>
          <button className="button button-cyan" disabled={busy} onClick={cancel}>
            {busy
              ? es
                ? 'Cancelando…'
                : 'Cancelling…'
              : es
                ? 'Sí, cancelar reserva'
                : 'Yes, cancel reservation'}
          </button>
        </PreviewDialog>
      )}
    </div>
  )
}
