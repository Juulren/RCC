import { NavLink } from 'react-router-dom'
import { copy } from '../content.js'

export default function NavigationLinks({ language, onNavigate }) {
  const labels = [language === 'es' ? 'Inicio' : 'Home', ...copy[language].nav]
  return ['/', '/habitaciones', '/experiencias', '/destino'].map((path, index) => (
    <NavLink key={path} to={path} end onClick={onNavigate}>
      {labels[index]}
    </NavLink>
  ))
}
