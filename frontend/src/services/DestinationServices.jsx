import { useEffect, useState } from 'react'
import './services.css'

export default function DestinationServices({ language }) {
  const es = language === 'es'
  const [weather, setWeather] = useState(null)
  const [location, setLocation] = useState(null)
  const [attempt, setAttempt] = useState(0)
  const [showMap, setShowMap] = useState(false)
  const [mapState, setMapState] = useState('idle')
  useEffect(() => {
    const controller = new AbortController()
    setWeather(null)
    const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(12000)])
    fetch('/api/weather', { signal })
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then(setWeather)
      .catch(() => {
        if (!controller.signal.aborted) setWeather({ available: false })
      })
    fetch('/api/location', { signal })
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then(setLocation)
      .catch(() => {
        if (!controller.signal.aborted) setLocation({})
      })
    return () => controller.abort()
  }, [attempt])
  useEffect(() => {
    if (!showMap || mapState !== 'loading') return
    const timer = setTimeout(() => setMapState('error'), 12000)
    return () => clearTimeout(timer)
  }, [showMap, mapState])
  const current = weather?.current
  const search =
    location?.searchUrl || 'https://www.google.com/maps/search/?api=1&query=Regent+Chongqing+China'
  const condition = (code) => {
    if (code === 0) return es ? 'Despejado' : 'Clear'
    if (code <= 3) return es ? 'Nubosidad variable' : 'Variable cloud cover'
    if ([45, 48].includes(code)) return es ? 'Niebla' : 'Fog'
    if (code >= 95) return es ? 'Tormenta' : 'Thunderstorm'
    if ([71, 73, 75, 77, 85, 86].includes(code)) return es ? 'Nieve' : 'Snow'
    if (code >= 51) return es ? 'Precipitación' : 'Precipitation'
    return es ? 'Condiciones variables' : 'Variable conditions'
  }
  return (
    <section
      className="shell section destination-services"
      aria-label={es ? 'Clima y ubicación' : 'Weather and location'}
    >
      <article className="service-panel weather-panel">
        <p className="eyebrow">CHONGQING / UTC+8</p>
        <h2>{es ? 'El pulso de la ciudad.' : 'The pulse of the city.'}</h2>
        <div aria-live="polite">
          {!weather ? (
            <p>{es ? 'Consultando el clima…' : 'Checking the weather…'}</p>
          ) : weather.available && current ? (
            <>
              <strong className="weather-temperature">
                {Math.round(current.temperature)}°<span>C</span>
              </strong>
              <p>
                {condition(current.code)} · {es ? 'Sensación' : 'Feels like'}{' '}
                {Math.round(current.apparentTemperature)} °C
              </p>
              <p>
                {es ? 'Viento' : 'Wind'} {current.windSpeed} km/h
              </p>
              <small>
                {weather.stale ? (es ? 'Último dato disponible · ' : 'Last available data · ') : ''}
                {current.observedAt.replace('T', ' ')} (UTC+8)
              </small>
            </>
          ) : (
            <p>
              {es
                ? 'El clima no está disponible en este momento.'
                : 'Weather is currently unavailable.'}
            </p>
          )}
        </div>
        <p className="service-note">
          {es
            ? 'Condiciones actuales de referencia para Chongqing; no es un pronóstico para las fechas de tu reserva.'
            : 'Current conditions for the Chongqing area; this is not a forecast for your reservation dates.'}
        </p>
        <a href="https://open-meteo.com/" target="_blank" rel="noreferrer">
          {es ? 'Datos de Open-Meteo · CC BY 4.0' : 'Data by Open-Meteo · CC BY 4.0'}
        </a>
        <button className="text-link" onClick={() => setAttempt((a) => a + 1)}>
          {es ? 'Actualizar' : 'Refresh'}
        </button>
      </article>
      <article className="service-panel map-panel">
        <p className="eyebrow">REGENT CHONGQING / JIANGBEIZUI</p>
        <h2>{es ? 'Traza tu ruta.' : 'Find your way.'}</h2>
        <p>66 Jinshamen Road, Chongqing, China</p>
        {!showMap && (
          <p>
            {es
              ? 'Consulta la ubicación del hotel en Google Maps.'
              : 'Explore the hotel location on Google Maps.'}
          </p>
        )}
        {location?.embedUrl && !showMap && (
          <button
            className="button button-outline"
            onClick={() => {
              setShowMap(true)
              setMapState('loading')
            }}
          >
            {es ? 'Cargar mapa de Google' : 'Load Google map'}
          </button>
        )}
        {showMap && (
          <>
            {mapState === 'loading' && (
              <p role="status">{es ? 'Cargando mapa…' : 'Loading map…'}</p>
            )}
            {mapState === 'error' && (
              <p role="status">
                {es
                  ? 'Si el mapa no carga, utiliza el enlace directo.'
                  : 'If the map cannot load, use the direct link.'}
              </p>
            )}
            <iframe
              title={es ? 'Ubicación de Regent Chongqing' : 'Regent Chongqing location'}
              src={location.embedUrl}
              referrerPolicy="strict-origin-when-cross-origin"
              allowFullScreen
              onLoad={() => setMapState('loaded')}
              onError={() => setMapState('error')}
            />
          </>
        )}
        <a className="button button-cyan" href={search} target="_blank" rel="noreferrer">
          {es ? 'Abrir ubicación en Google Maps' : 'Open location in Google Maps'}
        </a>
        <small>
          {es
            ? 'Google se abre como servicio externo. El enlace sigue disponible si el mapa integrado falla.'
            : 'Google opens as an external service. The link remains available if the embedded map fails.'}
        </small>
      </article>
    </section>
  )
}
