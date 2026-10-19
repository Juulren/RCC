import { lazy, Suspense, useEffect } from 'react'
import { Link, Route, Routes, useLocation } from 'react-router-dom'
import App from './App.jsx'

const RoomsPage = lazy(() => import('./pages/RoomsPage.jsx'))
const ExperiencesPage = lazy(() => import('./pages/ExperiencesPage.jsx'))
const DestinationPage = lazy(() => import('./pages/DestinationPage.jsx'))

export default function SiteRoutes() {
  const { pathname } = useLocation()
  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
  }, [pathname])
  return (
    <Suspense
      fallback={
        <div className="route-loading" role="status">
          RCC · Chongqing…
        </div>
      }
    >
      <Routes>
        <Route path="/" element={<App />} />
        <Route path="/habitaciones" element={<RoomsPage />} />
        <Route path="/experiencias" element={<ExperiencesPage />} />
        <Route path="/destino" element={<DestinationPage />} />
        <Route
          path="*"
          element={
            <main className="route-loading">
              <h1>404</h1>
              <Link className="button button-cyan" to="/">
                RCC · Home / Inicio
              </Link>
            </main>
          }
        />
      </Routes>
    </Suspense>
  )
}
