import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import RecommendationPanel from './RecommendationPanel.jsx'
const { api, auth } = vi.hoisted(() => ({
  api: vi.fn(),
  auth: { status: 'authenticated', user: { id: 'fixture' }, refresh: vi.fn() },
}))
vi.mock('../auth/AuthContext.jsx', () => ({ useAuth: () => auth }))
vi.mock('../catalog/api.js', async (original) => ({ ...(await original()), catalogApi: api }))
const draft = {
  arrival: '2026-12-01',
  departure: '2026-12-03',
  rooms: [{ adults: 2, childAges: [], roomTypeId: '' }],
}
const result = {
  mode: 'ai',
  reason: 'AI_RANKED',
  options: [
    {
      id: 'option-0',
      explanation: '<img src=x onerror=alert(1)>',
      quote: {
        total: 200,
        items: [
          {
            roomTypeId: 'server-id',
            nameEs: 'Classic',
            nameEn: 'Classic',
            adults: 2,
            childAges: [],
          },
        ],
      },
    },
  ],
}
beforeEach(() => {
  api.mockReset()
  auth.status = 'authenticated'
})
function view(props = {}) {
  return (
    <MemoryRouter>
      <RecommendationPanel
        draft={draft}
        language="es"
        disabled={false}
        onChoose={() => {}}
        {...props}
      />
    </MemoryRouter>
  )
}
it('requires sign-in before any recommendation request', () => {
  auth.status = 'anonymous'
  render(view())
  expect(
    screen.getByRole('link', { name: 'Inicia sesión para recibir recomendaciones' }),
  ).toHaveAttribute('href', '/cuenta')
  expect(api).not.toHaveBeenCalled()
})
it('renders AI text safely and selecting only changes the draft', async () => {
  api.mockResolvedValue(result)
  const choose = vi.fn()
  const { container } = render(view({ onChoose: choose }))
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  expect(await screen.findByText('<img src=x onerror=alert(1)>')).toBeVisible()
  expect(container.querySelector('img')).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Usar esta combinación' }))
  expect(choose).toHaveBeenCalledWith(['server-id'])
  expect(api).toHaveBeenCalledTimes(1)
  expect(api.mock.calls[0][0]).toBe('/recommendations')
})
it('discards an in-flight response after the stay changes', async () => {
  let resolve
  api.mockImplementation(
    () =>
      new Promise((done) => {
        resolve = done
      }),
  )
  const { rerender } = render(view())
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1))
  const signal = api.mock.calls[0][1].signal
  rerender(view({ draft: { ...draft, arrival: '2026-12-02' } }))
  expect(signal.aborted).toBe(true)
  await act(async () => resolve(result))
  expect(screen.queryByText('<img src=x onerror=alert(1)>')).toBeNull()
})
it('explains rate limits and permits a later local retry', async () => {
  api
    .mockRejectedValueOnce({ status: 429 })
    .mockResolvedValueOnce({ mode: 'deterministic', reason: 'NO_MATCH', options: [] })
  render(view())
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Espera un minuto')
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  expect(await screen.findByText('Cálculo local · No intervino IA')).toBeVisible()
  expect(screen.queryByRole('alert')).toBeNull()
})
