import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import { catalogApi, money } from '../catalog/api.js'
import './recommendations.css'

export default function RecommendationPanel({ draft, language, disabled, onChoose }) {
  const auth = useAuth()
  const es = language === 'es'
  const [budget, setBudget] = useState('')
  const [preferences, setPreferences] = useState([])
  const [result, setResult] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [selected, setSelected] = useState(null)
  const request = useRef(null)
  const nights = (Date.parse(draft.departure) - Date.parse(draft.arrival)) / 86400000
  const roomCount = draft.rooms.length
  const adults = draft.rooms.reduce((total, room) => total + room.adults, 0)
  const children = draft.rooms.reduce((total, room) => total + room.childAges.length, 0)
  const validStay = Number.isInteger(nights) && nights > 0 && roomCount > 0
  const signature = JSON.stringify({
    arrival: draft.arrival,
    departure: draft.departure,
    rooms: draft.rooms.map(({ adults, childAges }) => ({ adults, childAges })),
    language,
    budget,
    preferences,
    user: auth.user?.id,
    status: auth.status,
  })
  useEffect(() => {
    request.current?.abort()
    setResult(null)
    setError('')
    setSelected(null)
    setBusy(false)
    return () => request.current?.abort()
  }, [signature])
  async function recommend(event) {
    event.preventDefault()
    const controller = new AbortController()
    request.current?.abort()
    request.current = controller
    setBusy(true)
    setError('')
    setResult(null)
    setSelected(null)
    try {
      const response = await catalogApi('/recommendations', {
        method: 'POST',
        timeoutMs: 130000,
        signal: controller.signal,
        body: {
          arrival: draft.arrival,
          departure: draft.departure,
          rooms: draft.rooms.map(({ adults, childAges }) => ({ adults, childAges })),
          budget: budget === '' ? null : Number(budget),
          preferences,
          locale: language,
        },
      })
      if (!controller.signal.aborted) setResult(response)
    } catch (err) {
      if (controller.signal.aborted) return
      if (err.status === 401 || err.status === 403) {
        auth.refresh().catch(() => {})
        setError(
          es ? 'Inicia sesión para pedir recomendaciones.' : 'Sign in to request recommendations.',
        )
      } else if (err.status === 429)
        setError(
          es
            ? 'Alcanzaste el límite de consultas. Espera un minuto.'
            : 'Request limit reached. Wait one minute.',
        )
      else
        setError(
          es
            ? 'No pudimos completar la consulta. Revisa los criterios y reintenta.'
            : 'Could not complete the request. Check your criteria and retry.',
        )
    } finally {
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  return (
    <section className="recommendation-panel" aria-labelledby="recommendations-title">
      <p className="eyebrow">RCC / STAY MATCH</p>
      <h3 id="recommendations-title">
        {es ? 'Encuentra una combinación para ti.' : 'Find a combination for you.'}
      </h3>
      <p>
        {es
          ? 'Te ayudamos a elegir entre las habitaciones disponibles para tus fechas y huéspedes. Ajusta tus prioridades y pide hasta tres opciones.'
          : 'Choose from available rooms for your dates and guests. Set your priorities and request up to three options.'}
      </p>
      {validStay && (
        <dl
          className="recommendation-stay"
          aria-label={es ? 'Tu búsqueda actual' : 'Your current search'}
        >
          <div>
            <dt>{es ? 'Estancia' : 'Stay'}</dt>
            <dd>
              {nights}{' '}
              {es ? (nights === 1 ? 'noche' : 'noches') : nights === 1 ? 'night' : 'nights'}
            </dd>
          </div>
          <div>
            <dt>{es ? 'Habitaciones' : 'Rooms'}</dt>
            <dd>{roomCount}</dd>
          </div>
          <div>
            <dt>{es ? 'Huéspedes' : 'Guests'}</dt>
            <dd>
              {adults}{' '}
              {es ? (adults === 1 ? 'adulto' : 'adultos') : adults === 1 ? 'adult' : 'adults'}
              {children > 0 &&
                ` · ${children} ${es ? (children === 1 ? 'niño' : 'niños') : children === 1 ? 'child' : 'children'}`}
            </dd>
          </div>
        </dl>
      )}
      {auth.status !== 'authenticated' ? (
        <Link className="text-link" to="/cuenta">
          {es ? 'Inicia sesión para recibir recomendaciones' : 'Sign in to get recommendations'}
        </Link>
      ) : (
        <form onSubmit={recommend}>
          <fieldset disabled={busy || disabled}>
            <legend>
              {es ? '¿Qué prefieres para tu estancia?' : 'What matters most for your stay?'}
            </legend>
            <label className="recommendation-budget" htmlFor="recommendation-budget">
              {es
                ? 'Presupuesto total de la estancia (CNY, opcional)'
                : 'Total stay budget (CNY, optional)'}
            </label>
            <input
              id="recommendation-budget"
              type="number"
              min="0.01"
              max="100000000"
              step="0.01"
              inputMode="decimal"
              placeholder={es ? 'Sin límite de presupuesto' : 'No budget limit'}
              aria-describedby="recommendation-budget-help"
              value={budget}
              onChange={(e) => setBudget(e.target.value)}
            />
            <p id="recommendation-budget-help" className="recommendation-help">
              {es
                ? 'CNY es el yuan chino. Este máximo cubre todas las habitaciones y noches, no es por persona. Déjalo vacío si no tienes un límite.'
                : 'CNY is the Chinese yuan. This limit covers all rooms and nights, not each guest. Leave empty if you have no limit.'}
            </p>
            {validStay && Number(budget) > 0 && Number.isFinite(Number(budget)) && (
              <p className="recommendation-budget-average">
                {es ? 'Tu límite equivale a una media de ' : 'Your limit averages '}
                <strong>{money(Number(budget) / (nights * roomCount), language)}</strong>
                {es
                  ? ' por habitación y noche. No es una tarifa ofrecida por el hotel.'
                  : ' per room per night. This is not a hotel rate.'}
              </p>
            )}
            <p className="recommendation-help">
              {es
                ? 'Puedes combinar prioridades. Son preferencias para ordenar opciones, no requisitos garantizados. La disponibilidad, la capacidad y tu presupuesto sí se respetan.'
                : 'You can combine priorities. These preferences rank options; they are not guaranteed requirements. Availability, capacity and your budget are always respected.'}
            </p>
            <div className="recommendation-preferences">
              {[
                [
                  'river',
                  es ? 'Vista al río' : 'River view',
                  es
                    ? 'Prioriza combinaciones con más habitaciones con vista al río.'
                    : 'Prefer combinations with more rooms overlooking the river.',
                ],
                [
                  'space',
                  es ? 'Más espacio' : 'More space',
                  es
                    ? 'Prioriza mayor superficie total en m² entre tus habitaciones.'
                    : 'Prefer a larger combined floor area in m² across your rooms.',
                ],
                [
                  'value',
                  es ? 'Menor precio' : 'Lower price',
                  es
                    ? 'Prioriza el menor total de la estancia dentro de tus opciones.'
                    : 'Prefer the lowest total stay price among your options.',
                ],
              ].map(([value, label, description]) => (
                <label key={value} className={preferences.includes(value) ? 'is-selected' : ''}>
                  <input
                    type="checkbox"
                    aria-label={label}
                    checked={preferences.includes(value)}
                    onChange={(e) =>
                      setPreferences(
                        e.target.checked
                          ? [...preferences, value]
                          : preferences.filter((x) => x !== value),
                      )
                    }
                  />
                  <span>
                    <strong>{label}</strong>
                    <small>{description}</small>
                  </span>
                </label>
              ))}
            </div>
            {preferences.length === 0 && (
              <p className="recommendation-help">
                {es
                  ? 'Sin preferencias, el cálculo local prioriza el menor precio total.'
                  : 'Without preferences, local calculation prioritizes the lowest total price.'}
              </p>
            )}
            <p className="booking-note">
              {es
                ? 'Si la IA está habilitada, el modelo configurado recibe solo cifras de las opciones y estas preferencias. No enviamos tu identidad, fechas, edades ni datos de pago. Sin IA usamos un cálculo local.'
                : 'When AI is enabled, the configured model receives only option figures and these preferences. We do not send your identity, dates, ages or payment data. Without AI we use a local calculation.'}
            </p>
            <button className="button button-outline" type="submit">
              {busy
                ? es
                  ? 'Comparando…'
                  : 'Comparing…'
                : es
                  ? 'Pedir recomendaciones'
                  : 'Get recommendations'}
            </button>
          </fieldset>
        </form>
      )}
      {error && <p role="alert">{error}</p>}
      {result && (
        <div className="recommendation-results" aria-live="polite">
          <p className="recommendation-mode">
            {result.mode === 'ai'
              ? es
                ? 'Ordenadas con IA · Opciones verificadas por RCC'
                : 'Ranked with AI · Options checked by RCC'
              : es
                ? 'Cálculo local · No intervino IA'
                : 'Local calculation · No AI was used'}
          </p>
          {result.reason === 'AI_NOT_CONFIGURED' && (
            <p>
              {es
                ? 'La IA no está configurada o está desactivada en el servidor. Por ahora usamos el cálculo local.'
                : 'AI is not configured or is disabled on the server. For now we use the local calculation.'}
            </p>
          )}
          {result.reason === 'AI_DAILY_LIMIT' && (
            <p>
              {es
                ? 'Se alcanzó el límite diario de IA; usamos el cálculo local.'
                : 'The daily AI limit was reached; using the local calculation.'}
            </p>
          )}
          {result.reason === 'AI_PROVIDER_QUOTA' && (
            <p>
              {es
                ? 'El proveedor de IA alcanzó su cuota disponible. Usamos el cálculo local; puedes intentarlo más tarde.'
                : 'The AI provider reached its available quota. Using local calculation; try again later.'}
            </p>
          )}
          {result.reason === 'AI_PROVIDER_AUTH' && (
            <p>
              {es
                ? 'El proveedor rechazó el acceso a la IA. Es necesario revisar la clave y sus permisos en el servidor. Usamos el cálculo local.'
                : 'The provider denied AI access. The server API key and permissions need to be checked. Using local calculation.'}
            </p>
          )}
          {result.reason === 'AI_MODEL_UNAVAILABLE' && (
            <p>
              {es
                ? 'El modelo de IA configurado no está disponible. Usamos el cálculo local.'
                : 'The configured AI model is unavailable. Using local calculation.'}
            </p>
          )}
          {result.reason === 'AI_PROVIDER_REQUEST' && (
            <p>
              {es
                ? 'El proveedor rechazó la solicitud de IA. Es necesario revisar la clave y la configuración del modelo. Usamos el cálculo local.'
                : 'The provider rejected the AI request. The API key and model configuration need to be checked. Using local calculation.'}
            </p>
          )}
          {result.reason === 'AI_PROVIDER_TIMEOUT' && (
            <p>
              {es
                ? 'La IA tardó demasiado en responder. Usamos el cálculo local.'
                : 'AI took too long to respond. Using local calculation.'}
            </p>
          )}
          {result.reason === 'AI_INVALID_RESPONSE' && (
            <p>
              {es
                ? 'La IA devolvió una respuesta que no pudimos validar. Usamos el cálculo local para mostrar opciones verificadas.'
                : 'AI returned a response we could not validate. Using local calculation to show verified options.'}
            </p>
          )}
          {result.reason === 'AI_UNAVAILABLE' && (
            <p>
              {es
                ? 'La IA no pudo completar la consulta; usamos el cálculo local.'
                : 'AI could not complete the request; using the local calculation.'}
            </p>
          )}
          {result.options.length === 0 && (
            <p>
              {es
                ? 'No encontramos una combinación para estos criterios. Prueba otras fechas, distribución o presupuesto.'
                : 'No combination found for these criteria. Try other dates, guest distribution or budget.'}
            </p>
          )}
          <div className="recommendation-grid">
            {result.options.map((option, i) => (
              <article className="recommendation-card" key={option.id}>
                <h4>
                  {es ? 'Opción' : 'Option'} {i + 1}
                </h4>
                <ul>
                  {option.quote.items.map((item, index) => (
                    <li key={index}>
                      {es ? item.nameEs : item.nameEn} · {item.adults} {es ? 'adultos' : 'adults'} /{' '}
                      {item.childAges.length} {es ? 'niños' : 'children'}
                    </li>
                  ))}
                </ul>
                <strong>{money(option.quote.total, language)}</strong>
                <small>
                  {es
                    ? 'Total de todas las habitaciones y noches'
                    : 'Total for all rooms and nights'}
                </small>
                <p>{option.explanation}</p>
                <button
                  className="button button-outline"
                  type="button"
                  disabled={disabled || busy}
                  onClick={() => {
                    onChoose(option.quote.items.map((item) => item.roomTypeId))
                    setSelected(option.id)
                  }}
                >
                  {selected === option.id
                    ? es
                      ? 'Seleccionada'
                      : 'Selected'
                    : es
                      ? 'Usar esta combinación'
                      : 'Use this combination'}
                </button>
              </article>
            ))}
          </div>
          <p className="booking-note">
            {es
              ? 'La sugerencia no bloquea inventario ni crea reservas. Las explicaciones de IA pueden contener errores. Solicita una cotización y confirma su precio antes de reservar.'
              : 'Suggestions do not hold inventory or create reservations. AI explanations may contain errors. Request a quote and confirm its price before reserving.'}
          </p>
        </div>
      )}
    </section>
  )
}
