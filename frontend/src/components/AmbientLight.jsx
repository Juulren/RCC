import { useEffect, useRef } from 'react'

export default function AmbientLight() {
  const ref = useRef(null)
  useEffect(() => {
    const element = ref.current
    if (!window.IntersectionObserver) return
    const observer = new IntersectionObserver(([entry]) => {
      element.dataset.visible = String(entry.isIntersecting && !document.hidden)
    })
    const visibility = () => {
      element.dataset.visible = String(
        !document.hidden && element.getBoundingClientRect().bottom > 0,
      )
    }
    observer.observe(element)
    document.addEventListener('visibilitychange', visibility)
    return () => {
      observer.disconnect()
      document.removeEventListener('visibilitychange', visibility)
    }
  }, [])
  return <div ref={ref} className="ambient-light" data-visible="false" aria-hidden="true" />
}
