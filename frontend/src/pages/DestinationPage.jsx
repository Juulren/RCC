import { Link } from 'react-router-dom'
import PageLayout from './PageLayout.jsx'
import CityHero from '../components/CityHero.jsx'
import usePageLanguage from './usePageLanguage.js'
import Reveal from '../components/Reveal.jsx'
import Icon from '../components/Icon.jsx'
import DestinationServices from '../services/DestinationServices.jsx'

export default function DestinationPage() {
  const [language, setLanguage] = usePageLanguage()
  const es = language === 'es'
  const places = es
    ? [
        [
          '01',
          'Hongyadong',
          'Tradición entre las luces.',
          'Arquitectura escalonada junto al río: una de las perspectivas que inspira el universo visual de esta página.',
        ],
        [
          '02',
          'Qiansimen',
          'Una línea sobre el río.',
          'El puente dibuja una conexión entre la ciudad y su reflejo. Una mirada distinta del mismo paisaje.',
        ],
        [
          '03',
          'Jialing',
          'El ritmo del agua.',
          'De día o al anochecer, el río pone el contrapunto a la energía del skyline.',
        ],
      ]
    : [
        [
          '01',
          'Hongyadong',
          'Tradition among the lights.',
          'Layered architecture by the river: one of the perspectives inspiring the visual world of this page.',
        ],
        [
          '02',
          'Qiansimen',
          'A line across the river.',
          'The bridge connects the city with its reflection. A different view of the same landscape.',
        ],
        [
          '03',
          'Jialing',
          'The rhythm of the water.',
          'By day or after dark, the river offers a counterpoint to the energy of the skyline.',
        ],
      ]
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={es ? 'Destino Chongqing' : 'Destination Chongqing'}
    >
      <CityHero
        language={language}
        className="destination-page-hero"
        decoration={
          <span className="destination-coordinate">
            29°33′ N<br />
            106°33′ E
          </span>
        }
      >
        <p className="eyebrow">CHONGQING / CHINA / UTC+8</p>
        <h1>
          {es ? 'Una ciudad.' : 'One city.'}
          <br />
          <em>{es ? 'Mil perspectivas.' : 'A thousand perspectives.'}</em>
        </h1>
        <p>
          {es
            ? 'Entre el agua, la montaña y la luz. Descubre la ciudad que inspira nuestra forma de entender la estancia.'
            : 'Between water, mountain and light. Discover the city that inspires our perspective on a stay.'}
        </p>
        <a className="text-link" href="#city-perspectives">
          {es ? 'Conocer el destino' : 'Discover the destination'}
          <Icon name="arrow" />
        </a>
      </CityHero>
      <section id="city-perspectives" className="shell section" aria-labelledby="city-title">
        <Reveal className="section-heading">
          <div>
            <p className="eyebrow">
              {es ? 'DE CERCA, A OTRA ESCALA' : 'UP CLOSE, ON A DIFFERENT SCALE'}
            </p>
            <h2 id="city-title">
              {es ? 'Mira más allá' : 'Look beyond'}
              <br />
              <span>{es ? 'del skyline.' : 'the skyline.'}</span>
            </h2>
          </div>
          <p className="section-intro">
            {es
              ? 'Tres elementos del paisaje nocturno de Chongqing. Una invitación a observar, sin convertir el viaje en una lista.'
              : 'Three elements of Chongqing’s nightscape. An invitation to observe, without turning a journey into a checklist.'}
          </p>
        </Reveal>
        <div className="place-list">
          {places.map(([number, name, title, text]) => (
            <Reveal key={number} className="place-row">
              <span className="place-number">{number}</span>
              <h3>{name}</h3>
              <div>
                <h4>{title}</h4>
                <p>{text}</p>
              </div>
              <Icon name="diagonal" size={25} />
            </Reveal>
          ))}
        </div>
      </section>
      <DestinationServices language={language} />
      <section className="inner-page-cta">
        <div className="shell">
          <p className="eyebrow">{es ? 'LA CIUDAD TE ESPERA' : 'THE CITY AWAITS'}</p>
          <h2>{es ? 'Encuentra tu lugar en Chongqing.' : 'Find your place in Chongqing.'}</h2>
          <Link to="/habitaciones" className="button button-cyan">
            {es ? 'Explorar habitaciones' : 'Explore rooms'}
            <Icon name="arrow" />
          </Link>
        </div>
      </section>
    </PageLayout>
  )
}
