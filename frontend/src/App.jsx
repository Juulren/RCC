import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AnimatePresence,
  motion,
  useReducedMotion,
  useScroll,
  useSpring,
  useTransform,
} from 'motion/react'
import { copy } from './content.js'
import Icon from './components/Icon.jsx'
import AmbientLight from './components/AmbientLight.jsx'
import SpotlightCard from './components/SpotlightCard.jsx'
import PreviewDialog from './components/PreviewDialog.jsx'
import ConnectionStatus from './components/ConnectionStatus.jsx'
import Reveal from './components/Reveal.jsx'
import NavigationLinks from './components/NavigationLinks.jsx'

function initialLanguage() {
  try {
    return localStorage.getItem('rcc-language') === 'en' ? 'en' : 'es'
  } catch {
    return 'es'
  }
}
export function todayInChongqing() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
}

export default function App() {
  const heroRef = useRef(null)
  const reducedMotion = useReducedMotion()
  const { scrollYProgress } = useScroll({ target: heroRef, offset: ['start start', 'end start'] })
  const heroY = useTransform(scrollYProgress, [0, 1], [0, reducedMotion ? 0 : 130])
  const pageScroll = useScroll()
  const progress = useSpring(pageScroll.scrollYProgress, { stiffness: 100, damping: 30 })
  const [language, setLanguage] = useState(initialLanguage)
  const [backgroundPaused, setBackgroundPaused] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const [room, setRoom] = useState(0)
  const [dialog, setDialog] = useState(null)
  const [arrival, setArrival] = useState('')
  const [departure, setDeparture] = useState('')
  const [adults, setAdults] = useState(2)
  const [children, setChildren] = useState(0)
  const [rooms, setRooms] = useState(1)
  const [dateError, setDateError] = useState(false)
  const t = copy[language]
  const today = todayInChongqing()
  const prices = [880, 1280, 1680]
  const money = new Intl.NumberFormat(language === 'es' ? 'es-MX' : 'en-GB', {
    style: 'currency',
    currency: 'CNY',
    maximumFractionDigits: 0,
  }).format(prices[room])

  useEffect(() => {
    document.documentElement.lang = language
    document.title =
      language === 'es'
        ? 'RCC · Otra perspectiva de Chongqing'
        : 'RCC · Another perspective on Chongqing'
    try {
      localStorage.setItem('rcc-language', language)
    } catch {
      /* Private browsing remains usable. */
    }
  }, [language])

  const toggleLanguage = () => {
    setLanguage(language === 'es' ? 'en' : 'es')
  }
  const roomSummary = `${rooms} ${rooms === 1 ? t.roomSingular : t.rooms.toLowerCase()} · ${adults + children} ${adults + children === 1 ? t.guestSingular : t.guests.toLowerCase()}`
  const formatDate = (date) =>
    new Intl.DateTimeFormat(language === 'es' ? 'es-MX' : 'en-GB', {
      dateStyle: 'medium',
      timeZone: 'UTC',
    }).format(new Date(`${date}T00:00:00Z`))
  function exploreStay(event) {
    event.preventDefault()
    if (!arrival || !departure || arrival < today || departure <= arrival) {
      setDateError(true)
      return
    }
    setDateError(false)
    setDialog('stay')
  }

  return (
    <>
      <a href="#main" className="skip-link">
        {t.skip}
      </a>
      {!reducedMotion && (
        <motion.div className="scroll-progress" style={{ scaleX: progress }} aria-hidden="true" />
      )}
      <header className="site-header">
        <div className="shell header-inner">
          <Link to="/" className="brand" aria-label="RCC Chongqing">
            <span className="brand-symbol" aria-hidden="true">
              R
            </span>
            <span>
              RCC<small>CHONGQING</small>
            </span>
          </Link>
          <nav
            className={`navigation ${menuOpen ? 'is-open' : ''}`}
            id="main-navigation"
            aria-label={language === 'es' ? 'Principal' : 'Main'}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setMenuOpen(false)
                document.getElementById('menu-toggle').focus()
              }
            }}
          >
            <NavigationLinks language={language} onNavigate={() => setMenuOpen(false)} />
            <button
              className="mobile-account"
              onClick={() => {
                setMenuOpen(false)
                setDialog('account')
              }}
            >
              {t.account}
            </button>
          </nav>
          <div className="header-actions">
            <button
              className="language-switch"
              onClick={toggleLanguage}
              aria-label={language === 'es' ? 'Switch to English' : 'Cambiar a español'}
            >
              <span className={language === 'es' ? 'active' : ''}>ES</span>
              <span aria-hidden="true">/</span>
              <span className={language === 'en' ? 'active' : ''}>EN</span>
            </button>
            <button className="account-button" onClick={() => setDialog('account')}>
              {t.account}
              <Icon name="diagonal" size={15} />
            </button>
            <button
              id="menu-toggle"
              className="menu-toggle icon-button"
              aria-label={menuOpen ? t.close : t.menu}
              aria-expanded={menuOpen}
              aria-controls="main-navigation"
              onClick={() => setMenuOpen(!menuOpen)}
            >
              <Icon name={menuOpen ? 'close' : 'menu'} />
            </button>
          </div>
        </div>
      </header>

      <main id="main">
        <section ref={heroRef} id="top" className="hero" aria-labelledby="hero-title">
          <motion.img
            className="hero-photo"
            style={{ y: heroY }}
            src="/images/chongqing-night.webp"
            alt=""
            fetchPriority="high"
            width="1280"
            height="853"
          />
          <div className="hero-shade" aria-hidden="true" />
          <AmbientLight paused={backgroundPaused} />
          <div className="shell hero-layout">
            <div className="hero-content">
              <p className="hero-location">
                <span className="small-line" />
                <Icon name="pin" size={13} />
                {t.location}
              </p>
              <p className="eyebrow hero-eyebrow">{t.eyebrow}</p>
              <h1 id="hero-title">
                {t.title[0]}
                <br />
                {t.title[1]}
                <br />
                <em>{t.title[2]}</em>
              </h1>
              <p className="hero-intro">{t.intro}</p>
              <a className="button button-light" href="#rooms">
                {t.explore}
                <Icon name="diagonal" size={18} />
              </a>
            </div>
            <div className="hero-editorial" aria-hidden="true">
              <span>重庆</span>
              <p>
                THE RIVER.
                <br />
                THE CITY.
                <br />
                YOUR PERSPECTIVE.
              </p>
            </div>
            <div className="hero-bottom">
              <span>{t.inspired}</span>
              <a href="#stay">
                {t.scroll}
                <span aria-hidden="true">↓</span>
              </a>
              <span>29°33′ N / 106°33′ E</span>
            </div>
            <button
              className="motion-toggle"
              aria-pressed={backgroundPaused}
              onClick={() => setBackgroundPaused(!backgroundPaused)}
            >
              {backgroundPaused ? t.resumeMotion : t.pauseMotion}
              <span aria-hidden="true">{backgroundPaused ? '▷' : 'Ⅱ'}</span>
            </button>
          </div>
        </section>

        <section className="shell stay-section" id="stay" aria-label={t.book}>
          <div className="stay-heading">
            <span className="eyebrow">{t.book}</span>
            <span className="preview-tag">{t.searchNote}</span>
          </div>
          <form className="stay-form beam-border" onSubmit={exploreStay}>
            <label className="date-field">
              <Icon name="calendar" />
              <span>
                {t.arrival}
                <input
                  aria-describedby={dateError ? 'date-error' : undefined}
                  type="date"
                  required
                  min={today}
                  value={arrival}
                  onChange={(e) => {
                    setArrival(e.target.value)
                    setDateError(false)
                  }}
                />
              </span>
            </label>
            <label className="date-field">
              <Icon name="calendar" />
              <span>
                {t.departure}
                <input
                  aria-describedby={dateError ? 'date-error' : undefined}
                  type="date"
                  required
                  min={arrival || today}
                  value={departure}
                  onChange={(e) => {
                    setDeparture(e.target.value)
                    setDateError(false)
                  }}
                />
              </span>
            </label>
            <details className="guest-picker">
              <summary>
                <Icon name="people" />
                <span>
                  <span className="field-label">{t.guests}</span>
                  <strong>{roomSummary}</strong>
                </span>
                <span className="chevron" aria-hidden="true">
                  ⌄
                </span>
              </summary>
              <div className="guest-panel">
                <p>{t.selection}</p>
                {[
                  [t.rooms, rooms, setRooms, 1, 5],
                  [t.adults, adults, setAdults, 1, 10],
                  [t.children, children, setChildren, 0, 8],
                ].map(([label, value, update, min, max]) => (
                  <label key={label}>
                    {label}
                    <input
                      type="number"
                      min={min}
                      max={max}
                      value={value}
                      onChange={(e) =>
                        update(Math.min(max, Math.max(min, Number(e.target.value) || min)))
                      }
                    />
                  </label>
                ))}
              </div>
            </details>
            <button className="button button-cyan search-button" type="submit">
              {t.search}
              <Icon name="arrow" />
            </button>
          </form>
          {dateError && (
            <p id="date-error" className="field-error" role="alert">
              {t.dateError}
            </p>
          )}
        </section>

        <Reveal
          as="section"
          id="rooms"
          className="shell section rooms-section"
          aria-labelledby="rooms-title"
        >
          <div className="section-heading">
            <div>
              <p className="eyebrow">{t.roomEyebrow}</p>
              <h2 id="rooms-title">
                {t.roomTitle}
                <br />
                <span>{t.roomAccent}</span>
              </h2>
            </div>
            <p className="section-intro">{t.roomIntro}</p>
          </div>
          <div
            className="room-tabs"
            style={{ '--active-tab': room }}
            role="tablist"
            aria-label={t.nav[0]}
          >
            {t.tabs.map((label, index) => (
              <button
                key={index}
                id={`room-tab-${index}`}
                role="tab"
                aria-selected={room === index}
                aria-controls="room-panel"
                tabIndex={room === index ? 0 : -1}
                onClick={() => setRoom(index)}
                onKeyDown={(event) => {
                  let next
                  if (event.key === 'ArrowRight') next = (index + 1) % 3
                  if (event.key === 'ArrowLeft') next = (index + 2) % 3
                  if (event.key === 'Home') next = 0
                  if (event.key === 'End') next = 2
                  if (next !== undefined) {
                    event.preventDefault()
                    setRoom(next)
                    document.getElementById(`room-tab-${next}`).focus()
                  }
                }}
              >
                <span className="tab-index">0{index + 1}</span>
                {label}
              </button>
            ))}
          </div>
          <div id="room-panel" role="tabpanel" aria-labelledby={`room-tab-${room}`}>
            <AnimatePresence mode="wait" initial={false}>
              <motion.div
                key={room}
                initial={{ opacity: reducedMotion ? 1 : 0, y: reducedMotion ? 0 : 24 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: reducedMotion ? 1 : 0, y: reducedMotion ? 0 : -16 }}
                transition={{ duration: reducedMotion ? 0 : 0.28, ease: [0.22, 1, 0.36, 1] }}
              >
                <SpotlightCard className="featured-room" spotlightColor="rgba(85, 230, 237, 0.1)">
                  <div className="room-photo-wrap">
                    <img
                      src="/images/room-preview.webp"
                      width="1280"
                      height="853"
                      loading="lazy"
                      alt={t.roomAlt}
                    />
                    <span className="image-caption">{t.illustration}</span>
                    <span className="room-number" aria-hidden="true">
                      0{room + 1}
                    </span>
                  </div>
                  <div className="room-content">
                    <p className="eyebrow">RCC / COLLECTION 0{room + 1}</p>
                    <h3>{t.roomNames[room]}</h3>
                    <p className="room-description">{t.roomDescriptions[room]}</p>
                    <ul className="room-features">
                      {t.features[room].map((feature, index) => (
                        <li key={index}>
                          <Icon name={['people', 'bed', 'area'][index]} size={18} />
                          {feature}
                        </li>
                      ))}
                    </ul>
                    <div className="room-price">
                      <span>{t.from}</span>
                      <strong>{money}</strong>
                      <span>{t.perNight}</span>
                    </div>
                    <button className="button button-outline" onClick={() => setDialog('room')}>
                      {t.details}
                      <Icon name="diagonal" size={17} />
                    </button>
                    <small className="sample-note">{t.sample}</small>
                  </div>
                </SpotlightCard>
              </motion.div>
            </AnimatePresence>
          </div>
        </Reveal>

        <Reveal
          as="section"
          id="experiences"
          className="experience-section section"
          aria-labelledby="experience-title"
        >
          <div className="shell">
            <div className="section-heading">
              <div>
                <p className="eyebrow">{t.experienceEyebrow}</p>
                <h2 id="experience-title">
                  {t.experienceTitle} <span>{t.experienceAccent}</span>
                </h2>
              </div>
              <span className="section-decoration" aria-hidden="true">
                享受当下
              </span>
            </div>
            <div className="experience-grid">
              {t.experiences.map((item, index) => (
                <Reveal key={item.number} delay={index * 0.12} className="experience-reveal">
                  <SpotlightCard
                    className={`experience-card experience-card-${index}`}
                    spotlightColor="rgba(85,230,237,.12)"
                  >
                    <div className="experience-card-top">
                      <Icon name={['pin', 'sparkle', 'cup'][index]} size={30} />
                      <span>{item.number}</span>
                    </div>
                    <p className="eyebrow">{item.label}</p>
                    <h3>{item.title}</h3>
                    <p>{item.text}</p>
                  </SpotlightCard>
                </Reveal>
              ))}
            </div>
          </div>
        </Reveal>

        <Reveal
          as="section"
          id="destination"
          className="shell section destination-section"
          aria-labelledby="destination-title"
        >
          <div className="destination-photo">
            <img
              src="/images/chongqing-night.webp"
              loading="lazy"
              width="1280"
              height="853"
              alt={t.cityAlt}
            />
            <div className="destination-photo-caption">
              <span>重庆 / CHONGQING</span>
              <Icon name="pin" />
            </div>
          </div>
          <div className="destination-content">
            <p className="eyebrow">{t.destinationEyebrow}</p>
            <h2 id="destination-title">
              {t.destinationTitle}
              <br />
              <span>{t.destinationAccent}</span>
            </h2>
            <p>{t.destinationText}</p>
            <div className="destination-facts">
              <span>
                <Icon name="pin" />
                CHONGQING, CN
              </span>
              <span>{t.timezone}</span>
            </div>
            <a className="text-link" href="#stay">
              {t.destinationLink}
              <Icon name="arrow" />
            </a>
          </div>
        </Reveal>

        <Reveal as="section" className="shell section faq-section" aria-labelledby="faq-title">
          <div>
            <p className="eyebrow">{t.faqEyebrow}</p>
            <h2 id="faq-title">{t.faqTitle}</h2>
          </div>
          <div className="faq-list">
            {t.faqs.map(([question, answer]) => (
              <details key={question}>
                <summary>
                  {question}
                  <Icon name="plus" />
                </summary>
                <p>{answer}</p>
              </details>
            ))}
          </div>
        </Reveal>

        <Reveal as="section" className="closing-section">
          <div className="shell">
            <span className="eyebrow">RCC · CHONGQING</span>
            <h2>
              {t.cta}
              <br />
              <span>{t.ctaAccent}</span>
            </h2>
            <a className="button button-cyan" href="#stay">
              {t.ctaButton}
              <Icon name="diagonal" />
            </a>
          </div>
        </Reveal>
      </main>
      <footer className="shell site-footer">
        <div className="footer-main">
          <a href="#top" className="brand">
            RCC<small>CHONGQING</small>
          </a>
          <p>{t.demo}</p>
          <button className="text-button" onClick={() => setDialog('credits')}>
            {t.credits}
            <Icon name="diagonal" size={14} />
          </button>
        </div>
        <div className="footer-bottom">
          <span>© {new Date().getFullYear()} RCC · A NEW PERSPECTIVE</span>
          <ConnectionStatus t={t} />
        </div>
      </footer>

      {dialog && (
        <PreviewDialog
          title={
            dialog === 'account'
              ? t.accountTitle
              : dialog === 'room'
                ? t.roomNames[room]
                : dialog === 'credits'
                  ? t.creditTitle
                  : t.summary
          }
          closeLabel={t.close}
          onClose={() => setDialog(null)}
        >
          {dialog === 'credits' ? (
            <>
              <p>{t.creditText}</p>
              <a
                href="https://commons.wikimedia.org/wiki/File:Chongqing_Skyline_At_Night.png"
                target="_blank"
                rel="noreferrer"
              >
                {t.creditsSky}
              </a>
              <a
                href="https://creativecommons.org/licenses/by-sa/4.0/"
                target="_blank"
                rel="noreferrer"
              >
                CC BY-SA 4.0
              </a>
              <a
                href="https://commons.wikimedia.org/wiki/File:Hotel_bedroom_windows_(Unsplash).jpg"
                target="_blank"
                rel="noreferrer"
              >
                {t.creditsRoom}
              </a>
              <p>{t.creditsComponents}</p>
              <a
                href="https://reactbits.dev/components/spotlight-card"
                target="_blank"
                rel="noreferrer"
              >
                React Bits
              </a>
              <a href="/licenses/react-bits.txt" target="_blank" rel="noreferrer">
                {language === 'es' ? 'Licencia de React Bits' : 'React Bits license'}
              </a>
              <a href="https://www.reacticx.com/" target="_blank" rel="noreferrer">
                Reacticx
              </a>
            </>
          ) : (
            <>
              <p>
                {dialog === 'account'
                  ? t.accountText
                  : dialog === 'room'
                    ? t.roomDescriptions[room]
                    : `${formatDate(arrival)} → ${formatDate(departure)} · ${roomSummary}`}
              </p>
              {dialog !== 'account' && <p className="dialog-note">{t.previewInfo}</p>}
            </>
          )}
        </PreviewDialog>
      )}
    </>
  )
}
