import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import { useAuth } from '../auth/AuthContext.jsx'
import { catalogApi, money } from '../catalog/api.js'
import { bookingError } from '../reservations/translations.js'
import '../reservations/reservations.css'
import { officialSource } from './rooms.js'
import RecommendationPanel from '../recommendations/RecommendationPanel.jsx'

export function chongqingToday() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
}
const guest = () => ({ adults: 2, childAges: [], roomTypeId: '' })
function initialDraft() {
  const query = new URLSearchParams(window.location.search)
  if (query.has('arrival') || query.has('type')) {
    const count = Math.min(8, Math.max(1, Number(query.get('rooms')) || 1))
    const adults = Math.min(96, Math.max(count, Number(query.get('adults')) || count * 2))
    const children = Math.min(11, Math.max(0, Number(query.get('children')) || 0))
    return {
      arrival: query.get('arrival') ?? '',
      departure: query.get('departure') ?? '',
      rooms: Array.from({ length: count }, (_, i) => ({
        adults: Math.min(12, Math.floor(adults / count) + (i < adults % count ? 1 : 0)),
        childAges: Array.from(
          { length: Math.floor(children / count) + (i < children % count ? 1 : 0) },
          () => 0,
        ),
        roomTypeId: query.get('type') ?? '',
      })),
    }
  }
  try {
    const saved = JSON.parse(sessionStorage.getItem('rcc-booking-draft'))
    if (saved && Array.isArray(saved.rooms) && saved.rooms.length >= 1 && saved.rooms.length <= 8)
      return saved
  } catch {
    /* Storage may be unavailable. */
  }
  return { arrival: '', departure: '', rooms: [guest()] }
}
export default function BookingPage() {
  const [language, setLanguage] = usePageLanguage(),
    es = language === 'es'
  const auth = useAuth(),
    navigate = useNavigate(),
    reduced = useReducedMotion()
  const [draft, setDraft] = useState(initialDraft)
  const [availability, setAvailability] = useState(null),
    [quote, setQuote] = useState(null)
  const [accepted, setAccepted] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState('')
  const reserveAttempt = useRef({})
  useEffect(() => {
    try {
      sessionStorage.setItem('rcc-booking-draft', JSON.stringify(draft))
    } catch {
      /* Continue without persistence. */
    }
  }, [draft])
  function change(next, keepAvailability = false) {
    setDraft(next)
    setQuote(null)
    setAccepted(false)
    setError('')
    if (!keepAvailability) setAvailability(null)
  }
  function updateRoom(index, fields, keepAvailability = false) {
    change(
      {
        ...draft,
        rooms: draft.rooms.map((room, i) => (i === index ? { ...room, ...fields } : room)),
      },
      keepAvailability,
    )
  }
  async function search(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setQuote(null)
    setAccepted(false)
    setAvailability(null)
    try {
      setAvailability(
        await catalogApi('/availability', {
          method: 'POST',
          body: {
            ...draft,
            rooms: draft.rooms.map(({ adults, childAges }) => ({ adults, childAges })),
          },
        }),
      )
    } catch (error) {
      setError(bookingError(error, language))
    } finally {
      setBusy(false)
    }
  }
  async function getQuote() {
    setBusy(true)
    setError('')
    setQuote(null)
    setAccepted(false)
    try {
      setQuote(await catalogApi('/quotes', { method: 'POST', body: draft }))
    } catch (error) {
      setError(bookingError(error, language))
    } finally {
      setBusy(false)
    }
  }
  async function reserve() {
    setBusy(true)
    setError('')
    const body = { ...draft, priceFingerprint: quote.priceFingerprint, locale: language }
    const serialized = JSON.stringify(body)
    let attempt
    try {
      attempt = JSON.parse(sessionStorage.getItem('rcc-reservation-attempt'))
    } catch {
      /* Optional storage. */
    }
    if (attempt?.body !== serialized) attempt = { body: serialized, key: crypto.randomUUID() }
    try {
      sessionStorage.setItem('rcc-reservation-attempt', JSON.stringify(attempt))
    } catch {
      /* Retry key also lives in memory below. */
    }
    // Retain the key even in browsers where session storage is disabled.
    if (reserveAttempt.current.body !== serialized) reserveAttempt.current = attempt
    try {
      const reservation = await catalogApi('/reservations', {
        method: 'POST',
        body,
        headers: { 'Idempotency-Key': reserveAttempt.current.key },
      })
      reserveAttempt.current = {}
      try {
        sessionStorage.removeItem('rcc-reservation-attempt')
      } catch {
        /* Optional storage. */
      }
      navigate(`/mis-reservas?reserva=${reservation.id}`)
    } catch (error) {
      if (error.code === 'PRICE_CHANGED' && error.quote) {
        setQuote(error.quote)
        setAccepted(false)
      }
      if (error.code === 'ROOM_UNAVAILABLE') {
        setQuote(null)
        setAvailability(null)
        setAccepted(false)
      }
      setError(bookingError(error, language))
    } finally {
      setBusy(false)
    }
  }
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={es ? 'Reserva tu estancia' : 'Book your stay'}
    >
      <section className="shell booking-page section">
        <div className="booking-heading">
          <p className="eyebrow">RCC / YOUR STAY, YOUR PERSPECTIVE</p>
          <h1>{es ? 'Diseña tu estancia.' : 'Shape your stay.'}</h1>
          <p>
            {es
              ? 'Una ciudad extraordinaria. Un espacio para cada uno.'
              : 'An extraordinary city. A space for everyone.'}
          </p>
          <Link className="text-link" to="/mis-reservas">
            {es ? 'Mis reservas' : 'My reservations'} →
          </Link>
        </div>
        <ol className="booking-steps" aria-label={es ? 'Pasos de reserva' : 'Booking steps'}>
          {[
            es ? 'Fechas y huéspedes' : 'Dates & guests',
            es ? 'Tus espacios' : 'Your spaces',
            es ? 'Revisa y reserva' : 'Review & reserve',
          ].map((step, i) => (
            <li
              key={step}
              aria-current={(quote ? 2 : availability ? 1 : 0) === i ? 'step' : undefined}
            >
              <span>0{i + 1}</span>
              {step}
            </li>
          ))}
        </ol>
        <p className="booking-note">
          {es
            ? 'Demostración académica · CNY · Hora local de Chongqing (UTC+8). De 1 a 8 habitaciones, hasta 30 noches y llegada dentro de 365 días. Niños: 0–17 años; indica su edad real.'
            : 'Academic demonstration · CNY · Chongqing local time (UTC+8). 1–8 rooms, up to 30 nights, arriving within 365 days. Children: 0–17; enter their actual ages.'}
        </p>
        <form
          onSubmit={search}
          className="booking-form beam-border"
          aria-label={es ? 'Buscar estancia' : 'Search stay'}
        >
          <fieldset disabled={busy}>
            <legend>{es ? '01 / Fechas y huéspedes' : '01 / Dates & guests'}</legend>
            <div className="booking-dates">
              <label>
                {es ? 'Llegada' : 'Arrival'}
                <input
                  type="date"
                  required
                  min={chongqingToday()}
                  value={draft.arrival}
                  onChange={(e) => change({ ...draft, arrival: e.target.value })}
                />
              </label>
              <label>
                {es ? 'Salida' : 'Departure'}
                <input
                  type="date"
                  required
                  min={draft.arrival || chongqingToday()}
                  value={draft.departure}
                  onChange={(e) => change({ ...draft, departure: e.target.value })}
                />
              </label>
            </div>
            <div className="guest-distribution">
              {draft.rooms.map((room, i) => (
                <fieldset key={i} className="guest-room">
                  <legend>
                    {es ? 'Habitación' : 'Room'} {i + 1}
                  </legend>
                  <div className="guest-room-controls">
                    <label>
                      {es ? 'Adultos' : 'Adults'}
                      <input
                        aria-label={`${es ? 'Adultos habitación' : 'Adults room'} ${i + 1}`}
                        type="number"
                        min="1"
                        max="12"
                        required
                        value={room.adults}
                        onChange={(e) => updateRoom(i, { adults: Number(e.target.value) })}
                      />
                    </label>
                    <label>
                      {es ? 'Niños' : 'Children'}
                      <input
                        aria-label={`${es ? 'Niños habitación' : 'Children room'} ${i + 1}`}
                        type="number"
                        min="0"
                        max="11"
                        required
                        value={room.childAges.length}
                        onChange={(e) =>
                          updateRoom(i, {
                            childAges: Array.from(
                              { length: Math.min(11, Math.max(0, Number(e.target.value))) },
                              (_, j) => room.childAges[j] ?? 0,
                            ),
                          })
                        }
                      />
                    </label>
                    {draft.rooms.length > 1 && (
                      <button
                        className="text-link"
                        type="button"
                        onClick={() =>
                          change({ ...draft, rooms: draft.rooms.filter((_, j) => j !== i) })
                        }
                      >
                        {es ? 'Quitar habitación' : 'Remove room'} {i + 1}
                      </button>
                    )}
                  </div>
                  <div className="child-ages">
                    {room.childAges.map((age, j) => (
                      <label key={j}>
                        {es ? 'Edad' : 'Age'} {j + 1}
                        <input
                          aria-label={`${es ? 'Edad niño' : 'Child age'} ${j + 1}, ${es ? 'habitación' : 'room'} ${i + 1}`}
                          type="number"
                          min="0"
                          max="17"
                          required
                          value={age}
                          onChange={(e) =>
                            updateRoom(i, {
                              childAges: room.childAges.map((value, k) =>
                                k === j ? Number(e.target.value) : value,
                              ),
                            })
                          }
                        />
                      </label>
                    ))}
                  </div>
                </fieldset>
              ))}
            </div>
            <div className="booking-actions">
              <button
                className="button button-outline"
                type="button"
                disabled={draft.rooms.length >= 8}
                onClick={() => change({ ...draft, rooms: [...draft.rooms, guest()] })}
              >
                {es ? 'Añadir habitación' : 'Add room'} +
              </button>
              <button className="button button-cyan" type="submit">
                {busy
                  ? es
                    ? 'Consultando…'
                    : 'Searching…'
                  : es
                    ? 'Buscar disponibilidad'
                    : 'Search availability'}{' '}
                →
              </button>
            </div>
          </fieldset>
        </form>
        <AnimatePresence mode="wait">
          {availability && (
            <motion.section
              key="options"
              initial={reduced ? false : { opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              className="booking-options"
              aria-labelledby="spaces-title"
            >
              <h2 id="spaces-title">
                {es ? '02 / Elige tus espacios' : '02 / Choose your spaces'}
              </h2>
              <p>
                {es
                  ? 'El inventario se comparte entre selecciones. La cotización comprueba la combinación completa; buscar no bloquea habitaciones.'
                  : 'Selections share inventory. The quote checks the complete combination; searching does not hold rooms.'}
              </p>
              <RecommendationPanel
                draft={draft}
                language={language}
                disabled={busy}
                onChoose={(ids) =>
                  change(
                    {
                      ...draft,
                      rooms: draft.rooms.map((room, index) => ({
                        ...room,
                        roomTypeId: ids[index],
                      })),
                    },
                    true,
                  )
                }
              />
              {availability.selections.map(({ index, options }) => (
                <fieldset className="space-selection" disabled={busy} key={index}>
                  <legend>
                    {es ? 'Habitación' : 'Room'} {index + 1} · {draft.rooms[index].adults}{' '}
                    {es ? 'adultos' : 'adults'} / {draft.rooms[index].childAges.length}{' '}
                    {es ? 'niños' : 'children'}
                  </legend>
                  {options.length === 0 ? (
                    <p role="status">
                      {es
                        ? 'Sin espacios disponibles para estos huéspedes y fechas.'
                        : 'No spaces available for these guests and dates.'}
                    </p>
                  ) : (
                    <div className="space-grid">
                      {options.map(({ roomType: type, availableCount }) => (
                        <label
                          className={`space-card ${draft.rooms[index].roomTypeId === type.id ? 'selected' : ''}`}
                          key={type.id}
                        >
                          {type.image && (
                            <img
                              src={type.image.url}
                              alt={es ? type.image.altEs : type.image.altEn}
                              loading="lazy"
                            />
                          )}
                          <div>
                            <input
                              type="radio"
                              aria-label={es ? type.nameEs : type.nameEn}
                              name={`space-${index}`}
                              checked={draft.rooms[index].roomTypeId === type.id}
                              onChange={() => updateRoom(index, { roomTypeId: type.id }, true)}
                            />
                            <strong>{es ? type.nameEs : type.nameEn}</strong>
                            <span>
                              {type.area} m² · {availableCount} {es ? 'disponibles' : 'available'}
                            </span>
                            <b>
                              {money(type.nightlyRate, language)}{' '}
                              <small>/ {es ? 'noche' : 'night'}</small>
                            </b>
                          </div>
                        </label>
                      ))}
                    </div>
                  )}
                </fieldset>
              ))}
              <p className="booking-note">
                {es ? 'Fotografías acreditadas: ' : 'Photography credits: '}
                <a href={officialSource} target="_blank" rel="noreferrer">
                  Regent Chongqing / IHG
                </a>
                .{' '}
                {es
                  ? 'Inventario y tarifas ficticios de RCC.'
                  : 'Fictitious RCC inventory and rates.'}
              </p>
              <button
                className="button button-cyan"
                disabled={
                  busy ||
                  draft.rooms.some(
                    (room, i) =>
                      !availability.selections[i].options.some(
                        (option) => option.roomType.id === room.roomTypeId,
                      ),
                  )
                }
                onClick={getQuote}
              >
                {es ? 'Cotizar selección' : 'Get quote'} →
              </button>
            </motion.section>
          )}
        </AnimatePresence>
        {quote && (
          <section className="booking-quote" aria-labelledby="quote-title">
            <p className="eyebrow">03 / YOUR NEXT CHAPTER</p>
            <h2 id="quote-title">{es ? 'Tu estancia, en detalle.' : 'Your stay, in detail.'}</h2>
            <p>
              {quote.arrival} → {quote.departure} · {quote.nights} {es ? 'noches' : 'nights'}
            </p>
            {quote.items.map((line, i) => (
              <div className="quote-line" key={i}>
                <div>
                  <strong>
                    {i + 1}. {es ? line.nameEs : line.nameEn}
                  </strong>
                  <span>
                    {line.adults} {es ? 'adultos' : 'adults'} ·{' '}
                    {es ? 'Edades de niños' : 'Children’s ages'}: {line.childAges.join(', ') || '—'}
                    <br />
                    {money(line.nightlyRate, language)} × {quote.nights} {es ? 'noches' : 'nights'}
                  </span>
                </div>
                <b>{money(line.subtotal, language)}</b>
              </div>
            ))}
            <div className="quote-total">
              <span>Total CNY</span>
              <strong>{money(quote.total, language)}</strong>
            </div>
            <p>
              {es
                ? 'Sin cargos adicionales. Esta cotización no bloquea inventario. Al reservar, el servidor verifica de nuevo tarifa y disponibilidad. Tendrás 30 minutos para completar el pago de prueba, si está disponible. La confirmación requiere un pago verificado.'
                : 'No additional charges. This quote does not hold inventory. The server rechecks rates and availability when booking. You have 30 minutes to complete test payment, if available. Confirmation requires a verified payment.'}
            </p>
            <label className="quote-accept">
              <input
                type="checkbox"
                checked={accepted}
                disabled={busy}
                onChange={(e) => setAccepted(e.target.checked)}
              />
              {es
                ? 'Acepto esta cotización y las fechas seleccionadas.'
                : 'I accept this quote and the selected dates.'}
            </label>
            {auth.status === 'authenticated' ? (
              <button className="button button-cyan" disabled={!accepted || busy} onClick={reserve}>
                {busy
                  ? es
                    ? 'Reservando…'
                    : 'Reserving…'
                  : es
                    ? 'Reservar por 30 minutos'
                    : 'Reserve for 30 minutes'}{' '}
                →
              </button>
            ) : (
              <Link className="button button-cyan" to="/cuenta?continuar=reserva">
                {es ? 'Inicia sesión para reservar' : 'Sign in to reserve'} →
              </Link>
            )}
          </section>
        )}
        {error && (
          <p className="booking-error" role="alert">
            {error}
          </p>
        )}
      </section>
    </PageLayout>
  )
}
