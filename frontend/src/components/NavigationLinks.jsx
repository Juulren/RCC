import { NavLink } from 'react-router-dom'
import { copy } from '../content.js'

export default function NavigationLinks({ language, onNavigate }) {
  const labels = [
    language === 'es' ? 'Inicio' : 'Home',
    ...copy[language].nav,
    language === 'es' ? 'Reservar' : 'Book',
  ]
  return ['/', '/habitaciones', '/experiencias', '/destino', '/reservar'].map((path, index) => (
    <NavLink key={path} to={path} end onClick={onNavigate}>
      {labels[index]}
    </NavLink>
  ))
}
