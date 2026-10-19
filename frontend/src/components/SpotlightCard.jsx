// Adapted from React Bits SpotlightCard, commit 1eeb6f105c68b964289d85dabbe84a1d551f3797.
// License: ../../licenses/react-bits.txt. CSS sizing customized by the RCC design system.
'use client'

import { useRef } from 'react'
import './SpotlightCard.css'

const SpotlightCard = ({
  children,
  className = '',
  spotlightColor = 'rgba(255, 255, 255, 0.25)',
}) => {
  const divRef = useRef(null)

  const handleMouseMove = (e) => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
    const rect = divRef.current.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top

    divRef.current.style.setProperty('--mouse-x', `${x}px`)
    divRef.current.style.setProperty('--mouse-y', `${y}px`)
    divRef.current.style.setProperty('--spotlight-color', spotlightColor)
    divRef.current.style.setProperty('--tilt-x', `${-(y / rect.height - 0.5) * 5}deg`)
    divRef.current.style.setProperty('--tilt-y', `${(x / rect.width - 0.5) * 5}deg`)
  }

  return (
    <div
      ref={divRef}
      onMouseMove={handleMouseMove}
      onMouseLeave={() => {
        divRef.current.style.setProperty('--tilt-x', '0deg')
        divRef.current.style.setProperty('--tilt-y', '0deg')
      }}
      className={`card-spotlight ${className}`}
    >
      {children}
    </div>
  )
}

export default SpotlightCard
