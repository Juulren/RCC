import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import { officialSource } from './rooms.js'
import useCollection from '../catalog/useCollection.js'
import HotelSummary from '../catalog/HotelSummary.jsx'
import { money } from '../catalog/api.js'
import './catalog.css'
import Reveal from '../components/Reveal.jsx'
import SpotlightCard from '../components/SpotlightCard.jsx'
import PreviewDialog from '../components/PreviewDialog.jsx'
import Icon from '../components/Icon.jsx'

export default function RoomsPage() {
  const [language, setLanguage] = usePageLanguage()
  const [filter, setFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState(null)
  const [photo, setPhoto] = useState(0)
  const reduced = useReducedMotion()
  const es = language === 'es'
  const [page, setPage] = useState(1)
  const [attempt, setAttempt] = useState(0)
  const [featured, setFeatured] = useState(null)
  const collection = useCollection({ filter, search, page, attempt })
  const visibleRooms =
    collection.status === 'ready'
      ? collection.items.map((room) => ({
          ...room,
          name: es ? room.nameEs : room.nameEn,
          es: room.descriptionEs,
          en: room.descriptionEn,
          size: room.area,
          river: room.riverView,
        }))
      : []
  useEffect(() => {
    if (collection.status === 'ready' && filter === 'all' && !search.trim() && page === 1)
      setFeatured(
        collection.items.find((room) => room.slug === 'club-river') ?? collection.items[0] ?? null,
      )
  }, [collection, filter, search, page])
  const beds = (room) =>
    room.beds === 'king' ? '1 King' : es ? '1 King o 2 Twin' : '1 King or 2 Twins'
  const image = (photo) => photo.url
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={es ? 'Habitaciones y suites' : 'Rooms & suites'}
    >
      <section className="rooms-page-hero subpage-hero">
        <div className="shell rooms-hero-layout">
          <div className="subpage-title">
            <p className="eyebrow">REGENT CHONGQING / ROOMS & SUITES</p>
            <h1>
              {es ? 'Tu espacio.' : 'Your space.'}
              <br />
              <em>{es ? 'Otra perspectiva.' : 'A new perspective.'}</em>
            </h1>
            <p>
              {es
                ? 'Interiores cálidos, ventanales y una ciudad que cambia con cada mirada. Explora las habitaciones y suites del hotel.'
                : 'Warm interiors, expansive windows and a city that changes with every glance. Explore the hotel’s rooms and suites.'}
            </p>
            <a className="text-link" href="#collection">
              {es ? 'Descubrir la colección' : 'Discover the collection'}
              <Icon name="arrow" />
            </a>
          </div>
          <div className="rooms-hero-photo">
            <motion.img
              initial={reduced ? false : { scale: 1.08 }}
              animate={{ scale: 1 }}
              transition={{ duration: 1.4 }}
              src={featured?.images[0]?.url ?? '/images/chongqing-night.webp'}
              alt={
                featured ? (es ? featured.images[0].altEs : featured.images[0].altEn) : 'Chongqing'
              }
              fetchPriority="high"
            />
            <span className="editorial-caption">
              {featured ? (es ? featured.nameEs : featured.nameEn) : 'RCC COLLECTION'}{' '}
              <span>{featured ? `${featured.area} m²` : ''}</span>
            </span>
          </div>
        </div>
        <div className="shell collection-stats">
          <span>01 — {es ? 'HABITACIONES' : 'ROOMS'}</span>
          <span>02 — SUITES</span>
          <span>
            {es ? 'FOTOGRAFÍA OFICIAL · IHG / REGENT' : 'OFFICIAL PHOTOGRAPHY · IHG / REGENT'}
          </span>
        </div>
      </section>
      <HotelSummary language={language} />
      <section
        id="collection"
        className="shell section collection-section"
        aria-labelledby="collection-title"
      >
        <Reveal className="section-heading">
          <div>
            <p className="eyebrow">
              {es ? 'ELIGE CÓMO QUIERES DESCANSAR' : 'CHOOSE HOW YOU UNWIND'}
            </p>
            <h2 id="collection-title">
              {es ? 'Una estancia a tu medida.' : 'A stay that feels like you.'}
            </h2>
          </div>
          <p className="section-intro">
            {es
              ? 'Colección administrada por RCC. Tarifas en CNY por habitación y noche, de demostración.'
              : 'A collection managed by RCC. Demonstration rates in CNY per room per night.'}
          </p>
        </Reveal>
        <div className="collection-tools">
          <div
            className="collection-filters"
            aria-label={es ? 'Filtrar habitaciones' : 'Filter rooms'}
          >
            {[
              ['all', es ? 'Todas' : 'All'],
              ['room', es ? 'Habitaciones' : 'Rooms'],
              ['suite', 'Suites'],
              ['river', es ? 'Vista al río' : 'River view'],
            ].map(([value, label]) => (
              <button
                key={value}
                aria-pressed={filter === value}
                onClick={() => {
                  setFilter(value)
                  setPage(1)
                }}
              >
                {label}
              </button>
            ))}
          </div>
          <label className="collection-search">
            <span className="sr-only">{es ? 'Buscar habitación' : 'Search rooms'}</span>
            <input
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
              maxLength={100}
              placeholder={es ? 'Buscar un espacio…' : 'Find your space…'}
            />
            <Icon name="arrow" size={16} />
          </label>
        </div>
        <p className="collection-count" role="status">
          {collection.status === 'loading'
            ? es
              ? 'Cargando colección…'
              : 'Loading collection…'
            : `${collection.total} ${es ? 'espacios en esta selección' : 'spaces in this selection'}`}
        </p>
        {collection.status === 'error' && (
          <div className="collection-empty">
            <p role="alert">
              {es ? 'No pudimos cargar el catálogo.' : 'Could not load the catalogue.'}
            </p>
            <button className="button button-outline" onClick={() => setAttempt(attempt + 1)}>
              {es ? 'Reintentar catálogo' : 'Retry catalogue'}
            </button>
          </div>
        )}
        <motion.div layout={!reduced} className="room-collection-grid">
          <AnimatePresence mode="popLayout" initial={false}>
            {visibleRooms.map((room, index) => (
              <motion.article
                layout={!reduced}
                key={room.id}
                initial={reduced ? false : { opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, scale: reduced ? 1 : 0.97 }}
                transition={{ duration: reduced ? 0 : 0.3 }}
                className="official-room-card"
              >
                <SpotlightCard
                  className="collection-spotlight"
                  spotlightColor="rgba(128,228,223,.12)"
                >
                  <div className="collection-photo">
                    <img
                      src={image(room.images[0])}
                      alt={es ? room.images[0].altEs : room.images[0].altEn}
                      loading="lazy"
                    />
                    <span className="collection-badge">
                      {room.category === 'suite' ? 'SUITE' : 'ROOM'} / 0{index + 1}
                    </span>
                    <button
                      className="photo-open"
                      aria-label={`${es ? 'Ver galería de' : 'View gallery for'} ${room.name}`}
                      onClick={() => {
                        setSelected(room)
                        setPhoto(0)
                      }}
                    >
                      <Icon name="diagonal" />
                    </button>
                  </div>
                  <div className="collection-card-body">
                    <h3>{room.name}</h3>
                    <p>{room[language]}</p>
                    <ul className="room-features">
                      <li>
                        <Icon name="area" size={17} />
                        {room.size} m²
                      </li>
                      <li>
                        <Icon name="bed" size={17} />
                        {beds(room)}
                      </li>
                      {room.river && (
                        <li>
                          <Icon name="pin" size={17} />
                          {es ? 'Río Jialing' : 'Jialing River'}
                        </li>
                      )}
                    </ul>
                    <div className="catalog-price">
                      <strong>{money(room.nightlyRate, language)}</strong>
                      <span>{es ? '/ noche · demostración' : '/ night · demonstration'}</span>
                      <small>
                        {es ? 'Capacidad de demostración' : 'Demonstration capacity'}:{' '}
                        {room.capacity} · {es ? 'máx. adultos' : 'max adults'} {room.maxAdults} ·{' '}
                        {es ? 'máx. niños' : 'max children'} {room.maxChildren}
                      </small>
                    </div>
                    <Link className="button button-outline" to={`/reservar?type=${room.id}`}>
                      {es ? 'Buscar fechas' : 'Find dates'} →
                    </Link>
                    <button
                      className="text-link"
                      onClick={() => {
                        setSelected(room)
                        setPhoto(0)
                      }}
                    >
                      {es ? 'Conocer el espacio' : 'Explore this space'}
                      <Icon name="arrow" size={18} />
                    </button>
                  </div>
                </SpotlightCard>
              </motion.article>
            ))}
          </AnimatePresence>
        </motion.div>
        {collection.status === 'ready' && collection.total === 0 && (
          <div className="collection-empty">
            <h3>{es ? 'Todavía hay más por descubrir.' : 'There is more to discover.'}</h3>
            <p>
              {es
                ? 'Prueba con otro nombre o restablece los filtros.'
                : 'Try another name or reset your filters.'}
            </p>
            <button
              className="button button-outline"
              onClick={() => {
                setSearch('')
                setFilter('all')
                setPage(1)
              }}
            >
              {es ? 'Ver todos los espacios' : 'View all spaces'}
            </button>
          </div>
        )}
        {collection.totalPages > 1 && (
          <nav
            className="catalog-pagination"
            aria-label={es ? 'Páginas del catálogo' : 'Catalogue pages'}
          >
            <button
              className="button button-outline"
              disabled={page <= 1 || collection.status !== 'ready'}
              onClick={() => setPage(page - 1)}
            >
              {es ? 'Anterior' : 'Previous'}
            </button>
            <span>
              {page} / {collection.totalPages}
            </span>
            <button
              className="button button-outline"
              disabled={page >= collection.totalPages || collection.status !== 'ready'}
              onClick={() => setPage(page + 1)}
            >
              {es ? 'Siguiente' : 'Next'}
            </button>
          </nav>
        )}
        <aside className="official-source-note">
          <Icon name="sparkle" />
          <p>
            {es
              ? 'Fotografías de Regent / IHG con atribución. Tarifas, capacidades e inventario de RCC son de demostración y pueden ser editados. Consulta el buscador para ver disponibilidad por fechas; no es una oferta real del hotel.'
              : 'Attributed Regent / IHG photography. RCC rates, capacities and inventory are demonstrations and may be edited. Use the booking search for date-based availability; this is not a real hotel offer.'}
            <a href={officialSource} target="_blank" rel="noreferrer">
              {es ? 'Fuente: Regent Chongqing / IHG' : 'Source: Regent Chongqing / IHG'} ↗
            </a>
          </p>
        </aside>
      </section>
      {selected && (
        <PreviewDialog
          title={selected.name}
          closeLabel={es ? 'Cerrar' : 'Close'}
          onClose={() => setSelected(null)}
        >
          <div className="room-gallery">
            <AnimatePresence mode="wait" initial={false}>
              <motion.img
                key={photo}
                initial={{ opacity: reduced ? 1 : 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: reduced ? 1 : 0 }}
                transition={{ duration: reduced ? 0 : 0.2 }}
                src={image(selected.images[photo])}
                alt={es ? selected.images[photo].altEs : selected.images[photo].altEn}
              />
            </AnimatePresence>
          </div>
          <div className="gallery-controls">
            <button
              className="icon-button"
              aria-label={es ? 'Foto anterior' : 'Previous photo'}
              disabled={selected.images.length === 1}
              onClick={() =>
                setPhoto((photo + selected.images.length - 1) % selected.images.length)
              }
            >
              ←
            </button>
            <span>
              {photo + 1} / {selected.images.length}
            </span>
            <button
              className="icon-button"
              aria-label={es ? 'Foto siguiente' : 'Next photo'}
              disabled={selected.images.length === 1}
              onClick={() => setPhoto((photo + 1) % selected.images.length)}
            >
              →
            </button>
          </div>
          <p>{selected[language]}</p>
          <p>
            {selected.size} m² · {beds(selected)}
          </p>
          <p>
            {money(selected.nightlyRate, language)}{' '}
            {es ? '/ noche · tarifa de demostración' : '/ night · demonstration rate'}
          </p>
          <p>
            {es ? 'Capacidad de demostración' : 'Demonstration capacity'}: {selected.capacity} ·{' '}
            {es
              ? 'Cada habitación requiere al menos un adulto.'
              : 'Each room requires at least one adult.'}
          </p>
          <p className="dialog-note">
            {es
              ? 'Las reservas se incorporarán en los próximos módulos. Consulta la oferta oficial para conocer condiciones vigentes.'
              : 'Reservations will be added in future modules. Refer to the official offer for current conditions.'}
          </p>
          <a href={officialSource} target="_blank" rel="noreferrer">
            Regent Chongqing / IHG ↗
          </a>
        </PreviewDialog>
      )}
    </PageLayout>
  )
}
