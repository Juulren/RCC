import { useEffect, useRef } from 'react'
import Icon from './Icon.jsx'

export default function PreviewDialog({ title, closeLabel, onClose, children }) {
  const ref = useRef(null)
  const returnFocus = useRef(null)
  useEffect(() => {
    const dialog = ref.current
    returnFocus.current ??= document.activeElement
    if (!dialog.open) dialog.showModal()
    // Removing the dialog removes its native modal state. Avoid closing in cleanup:
    // React StrictMode rehearses effects and a queued close event would dismiss it.
    return () => {
      if (returnFocus.current instanceof HTMLElement && returnFocus.current.isConnected)
        returnFocus.current.focus()
    }
  }, [])
  return (
    <dialog ref={ref} onClose={onClose} aria-labelledby="dialog-title" className="preview-dialog">
      <button
        className="dialog-close icon-button"
        aria-label={closeLabel}
        onClick={() => ref.current.close()}
      >
        <Icon name="close" />
      </button>
      <p className="eyebrow">RCC / CHONGQING</p>
      <h2 id="dialog-title">{title}</h2>
      {children}
    </dialog>
  )
}
