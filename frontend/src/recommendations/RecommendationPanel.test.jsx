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
  expect(api.mock.calls[0][1].timeoutMs).toBe(130000)
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

it('explains when OpenAI is not configured while retaining local options', async () => {
  api.mockResolvedValue({ ...result, mode: 'deterministic', reason: 'AI_NOT_CONFIGURED' })
  render(view())
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  expect(await screen.findByText(/La IA no está configurada/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Usar esta combinación' })).toBeVisible()
})

it.each([
  ['AI_PROVIDER_QUOTA', 'alcanzó su cuota disponible', 'reached its available quota'],
  ['AI_PROVIDER_AUTH', 'rechazó el acceso', 'denied AI access'],
  [
    'AI_MODEL_UNAVAILABLE',
    'modelo de IA configurado no está disponible',
    'AI model is unavailable',
  ],
  ['AI_PROVIDER_REQUEST', 'rechazó la solicitud', 'rejected the AI request'],
  ['AI_PROVIDER_TIMEOUT', 'tardó demasiado', 'too long'],
  ['AI_INVALID_RESPONSE', 'no pudimos validar', 'could not validate'],
])(
  'explains %s in both languages while keeping verified options',
  async (reason, spanish, english) => {
    api.mockResolvedValue({ ...result, mode: 'deterministic', reason })
    const { rerender } = render(view())
    fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
    expect(await screen.findByText(new RegExp(spanish))).toBeVisible()
    expect(screen.getByRole('button', { name: 'Usar esta combinación' })).toBeVisible()
    rerender(view({ language: 'en' }))
    fireEvent.click(screen.getByRole('button', { name: 'Get recommendations' }))
    expect(await screen.findByText(new RegExp(english))).toBeVisible()
    expect(api).toHaveBeenCalledTimes(2)
  },
)

it('explains the whole-stay budget and sends actual controlled preferences', async () => {
  api.mockResolvedValue(result)
  render(view())
  expect(screen.getByLabelText('Tu búsqueda actual')).toBeVisible()
  expect(screen.getByText('2 noches')).toBeVisible()
  expect(screen.getByText('2 adultos')).toBeVisible()
  expect(screen.getByText(/no es por persona/)).toBeVisible()
  expect(screen.getByText(/Sin preferencias/)).toBeVisible()
  fireEvent.change(screen.getByLabelText('Presupuesto total de la estancia (CNY, opcional)'), {
    target: { value: '4000' },
  })
  expect(screen.getByText(/2,000/)).toBeVisible()
  fireEvent.click(screen.getByRole('checkbox', { name: 'Vista al río' }))
  expect(screen.queryByText(/Sin preferencias/)).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Pedir recomendaciones' }))
  await waitFor(() => expect(api).toHaveBeenCalledTimes(1))
  expect(api.mock.calls[0][1].body).toMatchObject({
    budget: 4000,
    preferences: ['river'],
    rooms: [{ adults: 2, childAges: [] }],
  })
})
