import { useEffect, useRef } from 'react'

export default function AmbientLight({ paused = false }) {
  const ref = useRef(null)
  const canvasRef = useRef(null)
  useEffect(() => {
    const element = ref.current
    const canvas = canvasRef.current
    const media = window.matchMedia?.('(prefers-reduced-motion: reduce)')
    let context
    try {
      if (window.CanvasRenderingContext2D) context = canvas.getContext('2d')
    } catch {
      /* Static CSS fallback. */
    }
    let frame = 0
    let last = 0
    let width = 0
    let height = 0
    let visible = true
    const resize = () => {
      width = element.clientWidth
      height = element.clientHeight
      const ratio = Math.min(window.devicePixelRatio || 1, 1.5)
      canvas.width = Math.round(width * ratio)
      canvas.height = Math.round(height * ratio)
      context?.setTransform(ratio, 0, 0, ratio, 0, 0)
    }
    const draw = (now) => {
      frame = requestAnimationFrame(draw)
      if (now - last < 1000 / 30) return
      if (!width || !height) return
      last = now
      const time = now / 1000
      context.clearRect(0, 0, width, height)
      // Bounded geometry: three river-like light trails, 18/36 drifting light points.
      for (let beam = 0; beam < 3; beam++) {
        const x = width * (0.57 + beam * 0.16) + Math.sin(time * 0.22 + beam) * 65
        const gradient = context.createLinearGradient(0, 0, 0, height)
        const color = beam === 1 ? '242,99,155' : '85,230,237'
        gradient.addColorStop(0, `rgba(${color},0)`)
        gradient.addColorStop(0.45, `rgba(${color},0.5)`)
        gradient.addColorStop(1, `rgba(${color},0)`)
        context.beginPath()
        context.moveTo(x - 90, -30)
        context.bezierCurveTo(x + 150, height * 0.3, x - 130, height * 0.65, x + 80, height + 30)
        context.strokeStyle = gradient
        context.lineWidth = width < 600 ? 1.5 : 2
        context.shadowBlur = 18
        context.shadowColor = `rgba(${color},0.8)`
        context.stroke()
      }
      const count = width < 600 ? 18 : 36
      context.shadowBlur = 7
      context.shadowColor = '#80e4df'
      for (let point = 0; point < count; point++) {
        const x = (((point * 137.5 + Math.sin(time * 0.2 + point) * 24) % width) + width) % width
        const y = (height - ((time * (7 + (point % 4)) + point * 71) % height) + height) % height
        context.beginPath()
        context.arc(x, y, point % 3 === 0 ? 1.7 : 0.8, 0, Math.PI * 2)
        context.fillStyle = `rgba(160,244,239,${0.3 + Math.sin(time + point) * 0.2})`
        context.fill()
      }
      context.shadowBlur = 0
    }
    const sync = () => {
      cancelAnimationFrame(frame)
      const running = visible && !document.hidden && !media?.matches && !paused
      element.dataset.visible = String(running)
      if (running && context) frame = requestAnimationFrame(draw)
      else context?.clearRect(0, 0, width, height)
    }
    const observer = window.IntersectionObserver
      ? new IntersectionObserver(([entry]) => {
          visible = entry.isIntersecting
          sync()
        })
      : null
    observer?.observe(element)
    resize()
    sync()
    window.addEventListener('resize', resize)
    media?.addEventListener('change', sync)
    document.addEventListener('visibilitychange', sync)
    return () => {
      cancelAnimationFrame(frame)
      observer?.disconnect()
      window.removeEventListener('resize', resize)
      media?.removeEventListener('change', sync)
      document.removeEventListener('visibilitychange', sync)
    }
  }, [paused])
  return (
    <div ref={ref} className="ambient-light" data-visible="false" aria-hidden="true">
      <div className="aurora-layer aurora-cyan" />
      <div className="aurora-layer aurora-rose" />
      <canvas ref={canvasRef} className="neon-canvas" />
      <div className="hero-orbit" />
    </div>
  )
}
