import React from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import SiteRoutes from './SiteRoutes.jsx'
import { AuthProvider } from './auth/AuthContext.jsx'
import './styles.css'
import './motion.css'

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <SiteRoutes />
      </AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
)
