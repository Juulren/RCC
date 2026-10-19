import { useEffect, useState } from 'react'

export default function usePageLanguage() {
  const [language, setLanguage] = useState(() => {
    try {
      return localStorage.getItem('rcc-language') === 'en' ? 'en' : 'es'
    } catch {
      return 'es'
    }
  })
  useEffect(() => {
    document.documentElement.lang = language
    try {
      localStorage.setItem('rcc-language', language)
    } catch {
      /* Optional preference. */
    }
  }, [language])
  return [language, setLanguage]
}
