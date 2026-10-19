import React from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import SiteRoutes from './SiteRoutes.jsx'
import './styles.css'
import './motion.css'

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <SiteRoutes />
    </BrowserRouter>
  </React.StrictMode>,
)
