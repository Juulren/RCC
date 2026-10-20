import { useEffect, useRef, useState } from 'react'
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
  const [paymentCapability, setPaymentCapability] = useState(null)
  const checkoutKeys = useRef({})
  useEffect(() => {
    const controller = new AbortController()
    catalogApi('/payments/status', { signal: controller.signal })
      .then(setPaymentCapability)
      .catch(() => {
        if (!controller.signal.aborted) setPaymentCapability({ available: false })
      })
    return () => controller.abort()
  }, [])
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
  async function checkout(reservation) {
    setBusy(true)
    setMutationError('')
    const storageKey = `rcc-checkout-${reservation.id}`
    if (!checkoutKeys.current[reservation.id]) {
      try {
        checkoutKeys.current[reservation.id] = sessionStorage.getItem(storageKey)
      } catch {
        /* Optional storage. */
      }
      checkoutKeys.current[reservation.id] ||= crypto.randomUUID()
      try {
        sessionStorage.setItem(storageKey, checkoutKeys.current[reservation.id])
      } catch {
        /* Retry key stays in memory. */
      }
    }
    try {
      const response = await catalogApi(`/reservations/${reservation.id}/checkout`, {
        method: 'POST',
        body: {},
        headers: { 'Idempotency-Key': checkoutKeys.current[reservation.id] },
      })
      if (response.status === 'Confirmed') setRefresh((value) => value + 1)
      else {
        const url = new URL(response.url)
        if (
          url.protocol !== 'https:' ||
          url.hostname !== 'checkout.stripe.com' ||
          url.username ||
          url.password
        )
          throw new Error('Invalid payment URL')
        window.location.assign(url.href)
      }
    } catch (error) {
      setMutationError(bookingError(error, language))
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
              ? 'Esta reserva aún no está confirmada. Reintentar el pago no extiende el bloqueo.'
              : 'This reservation is not confirmed yet. Retrying payment does not extend the hold.'}
          </p>
        )}
        {reservation.status === 'Expired' && (
          <p>
            {es
              ? 'El bloqueo venció y el inventario se liberó.'
              : 'The hold expired and inventory was released.'}
          </p>
        )}
        {!admin && reservation.status === 'PendingPayment' && (
          <div className="payment-action">
            <button
              className="button button-cyan"
              disabled={busy || !paymentCapability?.available}
              onClick={() => checkout(reservation)}
            >
              {busy
                ? es
                  ? 'Preparando pago…'
                  : 'Preparing payment…'
                : es
                  ? 'Pagar en Stripe · prueba'
                  : 'Pay with Stripe · test'}
            </button>
            <p>
              {paymentCapability === null
                ? es
                  ? 'Consultando pagos…'
                  : 'Checking payments…'
                : paymentCapability.available
                  ? es
                    ? 'Se abrirá Stripe Checkout de prueba. La confirmación depende del pago verificado por el servidor.'
                    : 'Stripe test Checkout will open. Confirmation depends on server-verified payment.'
                  : es
                    ? 'Los pagos de prueba no están disponibles en este momento.'
                    : 'Test payments are currently unavailable.'}
            </p>
          </div>
        )}
        {reservation.payments?.map((payment, i) => (
          <div className="payment-summary" key={i}>
            <p>
              {es ? 'Pago de prueba' : 'Test payment'}:{' '}
              {
                {
                  Creating: es ? 'preparando' : 'preparing',
                  Open: es ? 'pendiente' : 'pending',
                  Paid: es ? 'verificado' : 'verified',
                  Expired: es ? 'vencido' : 'expired',
                  Review: es ? 'requiere revisión' : 'needs review',
                  Refunded: es ? 'reembolsado' : 'refunded',
                }[payment.state]
              }
            </p>
            {payment.refund && (
              <p role="status">
                {es ? 'Reembolso de prueba' : 'Test refund'}:{' '}
                {
                  {
                    Pending: es ? 'pendiente' : 'pending',
                    Processing: es ? 'en proceso' : 'processing',
                    Succeeded: es ? 'completado' : 'completed',
                    Failed: es ? 'fallido; requiere revisión' : 'failed; needs review',
                    Review: es ? 'requiere revisión' : 'needs review',
                  }[payment.refund.state]
                }{' '}
                · {money(payment.refund.amount, language)}
              </p>
            )}
            {admin && (payment.lastError || payment.refund?.lastError) && (
              <p className="booking-error">
                {payment.lastError} {payment.refund?.lastError}
              </p>
            )}
          </div>
        ))}
        {!admin && reservation.canCancel && (
          <button
            className="button button-outline"
            disabled={busy}
            onClick={() => setSelected(reservation)}
          >
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
      {!admin && params.has('pago') && (
        <p className="booking-note" role="status">
          {es
            ? 'Has vuelto de Checkout. El regreso del navegador no confirma el pago: consulta el estado verificado de tu reserva. Si sigue pendiente, espera o actualiza.'
            : 'You returned from Checkout. Returning does not confirm payment: check your reservation’s verified status. If it is pending, wait or refresh.'}
        </p>
      )}
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
          {selected.status === 'Confirmed' && (
            <p>
              {es
                ? 'Se solicitará el reembolso total de prueba. La cancelación libera inventario inmediatamente; el reembolso puede tardar o requerir revisión.'
                : 'A full test refund will be requested. Cancellation releases inventory immediately; the refund may take time or require review.'}
            </p>
          )}
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
