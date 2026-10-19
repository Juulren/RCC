import { useEffect, useState } from 'react'
import { catalogApi } from './api.js'

export default function useCollection({ filter, search, page, attempt }) {
  const [state, setState] = useState({ status: 'loading', items: [], total: 0, totalPages: 0 })
  useEffect(() => {
    const controller = new AbortController()
    setState((previous) => ({ ...previous, status: 'loading' }))
    const timer = setTimeout(
      async () => {
        try {
          const query = new URLSearchParams({ page, pageSize: 20, search: search.trim() })
          if (filter === 'river') query.set('riverView', 'true')
          else if (filter !== 'all') query.set('category', filter)
          const result = await catalogApi(`/room-types?${query}`, { signal: controller.signal })
          if (!Array.isArray(result.items) || !Number.isInteger(result.total))
            throw new Error('Invalid catalogue')
          if (!controller.signal.aborted) setState({ ...result, status: 'ready' })
        } catch (error) {
          if (!controller.signal.aborted)
            setState({ status: 'error', items: [], total: 0, totalPages: 0 })
        }
      },
      search ? 250 : 0,
    )
    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [filter, search, page, attempt])
  return state
}
