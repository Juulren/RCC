import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { copy } from '../content.js'
import Icon from '../components/Icon.jsx'
import PreviewDialog from '../components/PreviewDialog.jsx'
import ConnectionStatus from '../components/ConnectionStatus.jsx'
import NavigationLinks from '../components/NavigationLinks.jsx'
import './pages.css'

export default function PageLayout({ language, setLanguage, title, children }) {
  const t = copy[language]
  const es = language === 'es'
  const [menuOpen, setMenuOpen] = useState(false)
  const [account, setAccount] = useState(false)
  useEffect(() => {
    document.title = `${title} · RCC Chongqing`
  }, [title])
  return (
    <div className="subpage-shell">
      <a href="#page-main" className="skip-link">
        {t.skip}
      </a>
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
            id="page-navigation"
            aria-label={es ? 'Principal' : 'Main'}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setMenuOpen(false)
                document.getElementById('page-menu-toggle').focus()
              }
            }}
          >
            <NavigationLinks language={language} onNavigate={() => setMenuOpen(false)} />
            <button
              className="mobile-account"
              onClick={() => {
                setMenuOpen(false)
                setAccount(true)
              }}
            >
              {t.account}
            </button>
          </nav>
          <div className="header-actions">
            <button
              className="language-switch"
              onClick={() => setLanguage(es ? 'en' : 'es')}
              aria-label={es ? 'Switch to English' : 'Cambiar a español'}
            >
              <span className={es ? 'active' : ''}>ES</span>
              <span aria-hidden="true">/</span>
              <span className={!es ? 'active' : ''}>EN</span>
            </button>
            <button className="account-button" onClick={() => setAccount(true)}>
              {t.account}
              <Icon name="diagonal" size={15} />
            </button>
            <button
              id="page-menu-toggle"
              className="menu-toggle icon-button"
              aria-label={menuOpen ? t.close : t.menu}
              aria-expanded={menuOpen}
              aria-controls="page-navigation"
              onClick={() => setMenuOpen(!menuOpen)}
            >
              <Icon name={menuOpen ? 'close' : 'menu'} />
            </button>
          </div>
        </div>
      </header>
      <main id="page-main">{children}</main>
      <footer className="shell site-footer">
        <div className="footer-main">
          <Link to="/" className="brand">
            RCC<small>CHONGQING</small>
          </Link>
          <p>{t.demo}</p>
          <Link className="text-link" to="/">
            {es ? 'Volver al inicio' : 'Back to home'}
            <Icon name="arrow" size={16} />
          </Link>
        </div>
        <div className="footer-bottom">
          <span>RCC · A NEW PERSPECTIVE</span>
          <ConnectionStatus t={t} />
        </div>
        <p className="page-photo-credit">
          {es ? 'Fotografía de Chongqing: ' : 'Chongqing photography: '}
          <a
            href="https://commons.wikimedia.org/wiki/File:Chongqing_Skyline_At_Night.png"
            target="_blank"
            rel="noreferrer"
          >
            Maple Doctor
          </a>
          {' · '}
          <a
            href="https://creativecommons.org/licenses/by-sa/4.0/"
            target="_blank"
            rel="noreferrer"
          >
            CC BY-SA 4.0
          </a>
          {es
            ? ' · Adaptada a WebP y recortada con CSS.'
            : ' · Adapted to WebP and cropped with CSS.'}
        </p>
      </footer>
      {account && (
        <PreviewDialog
          title={t.accountTitle}
          closeLabel={t.close}
          onClose={() => setAccount(false)}
        >
          <p>{t.accountText}</p>
        </PreviewDialog>
      )}
    </div>
  )
}
