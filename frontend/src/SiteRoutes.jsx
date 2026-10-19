import { useEffect } from 'react'
import { Link, Route, Routes, useLocation } from 'react-router-dom'
import App from './App.jsx'

import RoomsPage from './pages/RoomsPage.jsx'
import ExperiencesPage from './pages/ExperiencesPage.jsx'
import DestinationPage from './pages/DestinationPage.jsx'
import AccountPage from './pages/AccountPage.jsx'
import AdminPage from './pages/AdminPage.jsx'

export default function SiteRoutes() {
  const { pathname } = useLocation()
  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
  }, [pathname])
  return (
    <div key={pathname} className="route-view">
      <Routes>
        <Route path="/" element={<App />} />
        <Route path="/habitaciones" element={<RoomsPage />} />
        <Route path="/experiencias" element={<ExperiencesPage />} />
        <Route path="/destino" element={<DestinationPage />} />
        <Route path="/cuenta" element={<AccountPage />} />
        <Route path="/admin" element={<AdminPage />} />
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
    </div>
  )
}
