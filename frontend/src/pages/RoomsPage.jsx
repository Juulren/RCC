import { useMemo, useState } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import { officialRooms, officialSource } from './rooms.js'
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
  const visibleRooms = useMemo(
    () =>
      officialRooms.filter(
        (room) =>
          (filter === 'all' || room.category === filter || (filter === 'river' && room.river)) &&
          room.name.toLowerCase().includes(search.trim().toLowerCase()),
      ),
    [filter, search],
  )
  const beds = (room) =>
    room.beds === 'king' ? '1 King' : es ? '1 King o 2 Twin' : '1 King or 2 Twins'
  const image = (id) => `/images/regent/${id}.webp`
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
              src={image('club-river')}
              alt="Regent Club Room River View · Regent Chongqing"
              fetchPriority="high"
            />
            <span className="editorial-caption">
              REGENT CLUB ROOM RIVER VIEW <span>60 m²</span>
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
              ? 'Desde los 57 m² de una Classic Room hasta los 215 m² de la Regent Suite.'
              : 'From a 57 m² Classic Room to the 215 m² Regent Suite.'}
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
              <button key={value} aria-pressed={filter === value} onClick={() => setFilter(value)}>
                {label}
              </button>
            ))}
          </div>
          <label className="collection-search">
            <span className="sr-only">{es ? 'Buscar habitación' : 'Search rooms'}</span>
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={es ? 'Buscar un espacio…' : 'Find your space…'}
            />
            <Icon name="arrow" size={16} />
          </label>
        </div>
        <p className="collection-count" role="status">
          {visibleRooms.length} {es ? 'espacios en esta selección' : 'spaces in this selection'}
        </p>
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
                      alt={`${room.name} · Regent Chongqing`}
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
        {visibleRooms.length === 0 && (
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
              }}
            >
              {es ? 'Ver todos los espacios' : 'View all spaces'}
            </button>
          </div>
        )}
        <aside className="official-source-note">
          <Icon name="sparkle" />
          <p>
            {es
              ? 'Fotografías, nombres, camas y superficies tomados de la página oficial. Esta selección es informativa: no muestra precios ni disponibilidad en tiempo real.'
              : 'Photography, names, beds and floor areas are sourced from the official page. This is an informational collection, without live rates or availability.'}
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
                alt={`${selected.name} · ${photo + 1}/${selected.images.length} · IHG / Regent`}
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
