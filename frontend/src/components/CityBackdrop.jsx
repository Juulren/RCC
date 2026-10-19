import { useState } from 'react'
import AmbientLight from './AmbientLight.jsx'
import { copy } from '../content.js'

export default function CityBackdrop({ language }) {
  const [paused, setPaused] = useState(false)
  const t = copy[language]
  return (
    <>
      <img
        className="hero-photo city-hero-photo"
        src="/images/chongqing-night.webp"
        alt=""
        fetchPriority="high"
        width="1280"
        height="853"
      />
      <div className="hero-shade city-hero-shade" aria-hidden="true" />
      <AmbientLight paused={paused} />
      <div className="shell backdrop-controls">
        <button className="motion-toggle" aria-pressed={paused} onClick={() => setPaused(!paused)}>
          {paused ? t.resumeMotion : t.pauseMotion}
          <span aria-hidden="true">{paused ? '▷' : 'Ⅱ'}</span>
        </button>
      </div>
    </>
  )
}
