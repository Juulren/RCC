import { useState } from 'react'
import { Link } from 'react-router-dom'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import Reveal from '../components/Reveal.jsx'
import SpotlightCard from '../components/SpotlightCard.jsx'
import PreviewDialog from '../components/PreviewDialog.jsx'
import Icon from '../components/Icon.jsx'

const stories = {
  es: [
    {
      time: '09:00',
      tag: 'SIN PRISA',
      title: 'Un comienzo más lento.',
      text: 'Abre las cortinas. Deja que la luz marque el ritmo. Hay días que empiezan mejor sin una agenda llena.',
      icon: 'cup',
    },
    {
      time: '17:30',
      tag: 'OTRA PERSPECTIVA',
      title: 'La ciudad, a tu manera.',
      text: 'Camina, observa y encuentra ese rincón que no estaba en tus planes. La mejor ruta también puede ser la que improvisas.',
      icon: 'pin',
    },
    {
      time: '20:00',
      tag: 'CUANDO TODO SE ILUMINA',
      title: 'Quédate una luz más.',
      text: 'El río cambia de color. El skyline dibuja otra ciudad. Al caer la noche, Chongqing invita a volver a mirar.',
      icon: 'sparkle',
    },
  ],
  en: [
    {
      time: '09:00',
      tag: 'TAKE YOUR TIME',
      title: 'A slower beginning.',
      text: 'Open the curtains. Let the light set the pace. Some days begin better without a crowded agenda.',
      icon: 'cup',
    },
    {
      time: '17:30',
      tag: 'A NEW PERSPECTIVE',
      title: 'The city, your way.',
      text: 'Walk, observe and find the corner you never planned to see. Sometimes the best route is the one you invent.',
      icon: 'pin',
    },
    {
      time: '20:00',
      tag: 'WHEN EVERYTHING LIGHTS UP',
      title: 'Stay for one more light.',
      text: 'The river changes colour. The skyline becomes another city. As night falls, Chongqing invites you to look again.',
      icon: 'sparkle',
    },
  ],
}

export default function ExperiencesPage() {
  const [language, setLanguage] = usePageLanguage()
  const [selected, setSelected] = useState(null)
  const es = language === 'es'
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={es ? 'Experiencias' : 'Experiences'}
    >
      <section className="experience-page-hero subpage-hero">
        <div className="experience-city" aria-hidden="true" />
        <div className="shell experience-hero-content">
          <p className="eyebrow">RCC / THE ART OF BEING HERE</p>
          <h1>
            {es ? 'No solo venir.' : 'Not just visiting.'}
            <br />
            <em>{es ? 'Sentir el lugar.' : 'Feeling the place.'}</em>
          </h1>
          <p>
            {es
              ? 'Una ciudad para explorar. Un refugio al que volver. Entre ambos, momentos que hacen tuyo el viaje.'
              : 'A city to explore. A retreat to return to. Between them, moments that make the journey yours.'}
          </p>
          <a className="button button-light" href="#moments">
            {es ? 'Descubrir momentos' : 'Discover moments'}
            <Icon name="diagonal" />
          </a>
          <span className="experience-large-type" aria-hidden="true">
            感受
          </span>
        </div>
      </section>
      <section
        id="moments"
        className="shell section moments-section"
        aria-labelledby="moments-title"
      >
        <Reveal className="section-heading">
          <div>
            <p className="eyebrow">
              {es ? 'UN DÍA. INFINITAS FORMAS DE VIVIRLO.' : 'ONE DAY. ENDLESS WAYS TO LIVE IT.'}
            </p>
            <h2 id="moments-title">
              {es ? 'Menos itinerario.' : 'Less itinerary.'}
              <br />
              <span>{es ? 'Más momentos.' : 'More moments.'}</span>
            </h2>
          </div>
          <p className="section-intro">
            {es
              ? 'Ideas para inspirar tu estancia, a tu ritmo. Los horarios son una propuesta editorial, no actividades programadas del hotel.'
              : 'Ideas to inspire a stay at your own pace. Times are editorial suggestions, not scheduled hotel activities.'}
          </p>
        </Reveal>
        <div className="moment-grid">
          {stories[language].map((story, index) => (
            <Reveal key={index} delay={index * 0.12}>
              <SpotlightCard
                className={`moment-card moment-card-${index}`}
                spotlightColor="rgba(128,228,223,.16)"
              >
                <div className="moment-orbit" aria-hidden="true">
                  <Icon name={story.icon} size={48} />
                </div>
                <span className="moment-time">{story.time}</span>
                <p className="eyebrow">{story.tag}</p>
                <h3>{story.title}</h3>
                <p>{story.text}</p>
                <button className="text-link" onClick={() => setSelected(story)}>
                  {es ? 'Imaginar este momento' : 'Imagine this moment'}
                  <Icon name="arrow" />
                </button>
              </SpotlightCard>
            </Reveal>
          ))}
        </div>
      </section>
      <Reveal as="section" className="shell experience-feature section">
        <div className="experience-feature-photo">
          <img
            src="/images/chongqing-night.webp"
            alt={
              es
                ? 'Chongqing de noche junto al río Jialing'
                : 'Chongqing at night by the Jialing River'
            }
            loading="lazy"
          />
        </div>
        <div>
          <p className="eyebrow">
            {es
              ? 'EL DESTINO TAMBIÉN ES LA EXPERIENCIA'
              : 'THE DESTINATION IS PART OF THE EXPERIENCE'}
          </p>
          <h2>
            {es ? 'Sal a descubrir.' : 'Go and discover.'}
            <br />
            <span>{es ? 'Vuelve a desconectar.' : 'Return to unwind.'}</span>
          </h2>
          <p>
            {es
              ? 'Puentes, luces y perspectivas a distintas alturas. Descubre el contexto que inspira el diseño de RCC y elige por dónde empezar.'
              : 'Bridges, lights and perspectives at different heights. Discover the setting that inspires RCC and choose where to begin.'}
          </p>
          <Link to="/destino" className="button button-outline">
            {es ? 'Explorar Chongqing' : 'Explore Chongqing'}
            <Icon name="diagonal" />
          </Link>
        </div>
      </Reveal>
      <section className="inner-page-cta">
        <div className="shell">
          <p className="eyebrow">{es ? 'TU PROPIO RITMO' : 'YOUR OWN PACE'}</p>
          <h2>{es ? 'Cada viaje necesita su refugio.' : 'Every journey needs a retreat.'}</h2>
          <Link to="/habitaciones" className="button button-cyan">
            {es ? 'Encontrar mi espacio' : 'Find my space'}
            <Icon name="arrow" />
          </Link>
        </div>
      </section>
      {selected && (
        <PreviewDialog
          title={selected.title}
          closeLabel={es ? 'Cerrar' : 'Close'}
          onClose={() => setSelected(null)}
        >
          <p>{selected.text}</p>
          <p className="dialog-note">
            {es
              ? 'Inspiración editorial para tu viaje. No es una actividad reservable ni una promesa de servicios incluidos.'
              : 'Editorial inspiration for your journey. This is not a bookable activity or a promise of included services.'}
          </p>
        </PreviewDialog>
      )}
    </PageLayout>
  )
}
