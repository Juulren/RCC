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
          ? 'Comparamos habitaciones disponibles para la distribución que elegiste. Tú decides qué cotizar.'
          : 'We compare available rooms for your guest distribution. You decide what to quote.'}
      </p>
      {auth.status !== 'authenticated' ? (
        <Link className="text-link" to="/cuenta">
          {es ? 'Inicia sesión para recibir recomendaciones' : 'Sign in to get recommendations'}
        </Link>
      ) : (
        <form onSubmit={recommend}>
          <fieldset disabled={busy || disabled}>
            <legend>{es ? 'Tus prioridades' : 'Your priorities'}</legend>
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
              value={budget}
              onChange={(e) => setBudget(e.target.value)}
            />
            <div className="recommendation-preferences">
              {[
                ['river', es ? 'Vista al río' : 'River view'],
                ['space', es ? 'Más espacio' : 'More space'],
                ['value', es ? 'Menor precio' : 'Lower price'],
              ].map(([value, label]) => (
                <label key={value}>
                  <input
                    type="checkbox"
                    checked={preferences.includes(value)}
                    onChange={(e) =>
                      setPreferences(
                        e.target.checked
                          ? [...preferences, value]
                          : preferences.filter((x) => x !== value),
                      )
                    }
                  />
                  {label}
                </label>
              ))}
            </div>
            <p className="booking-note">
              {es
                ? 'Si la IA está habilitada, OpenAI recibe solo cifras de las opciones y estas preferencias. No enviamos tu identidad, fechas, edades ni datos de pago. Sin IA usamos un cálculo local.'
                : 'When AI is enabled, OpenAI receives only option figures and these preferences. We do not send your identity, dates, ages or payment data. Without AI we use a local calculation.'}
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
                ? 'OpenAI no está configurado o está desactivado en el servidor. Por ahora usamos el cálculo local.'
                : 'OpenAI is not configured or is disabled on the server. For now we use the local calculation.'}
            </p>
          )}
          {result.reason === 'AI_DAILY_LIMIT' && (
            <p>
              {es
                ? 'Se alcanzó el límite diario de IA; usamos el cálculo local.'
                : 'The daily AI limit was reached; using the local calculation.'}
            </p>
          )}
          {['AI_UNAVAILABLE', 'AI_INVALID_RESPONSE'].includes(result.reason) && (
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
