import { Link } from 'react-router-dom'
import PageLayout from './PageLayout.jsx'
import usePageLanguage from './usePageLanguage.js'
import ReservationList from '../reservations/ReservationList.jsx'

export default function ReservationsPage() {
  const [language, setLanguage] = usePageLanguage(),
    es = language === 'es'
  return (
    <PageLayout
      language={language}
      setLanguage={setLanguage}
      title={es ? 'Mis reservas' : 'My reservations'}
    >
      <section className="shell booking-page section">
        <div className="booking-heading">
          <p className="eyebrow">RCC / MY STAYS</p>
          <h1>{es ? 'Tus próximos capítulos.' : 'Your next chapters.'}</h1>
          <p>
            {es
              ? 'Consulta el estado de tu estancia. Fechas y vencimientos según Chongqing.'
              : 'Check your stay’s status. Dates and expiry use Chongqing time.'}
          </p>
          <Link className="text-link" to="/reservar">
            {es ? 'Nueva reserva' : 'New reservation'} →
          </Link>
        </div>
        <ReservationList language={language} />
      </section>
    </PageLayout>
  )
}
