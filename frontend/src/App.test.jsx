import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from './App.jsx'

describe('Conexión frontend/API', () => {
  it('muestra conexión solo cuando la respuesta cumple el contrato', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ application: 'RCC' }) }))
    render(<App />)
    expect(await screen.findByText('Frontend conectado con la API C#.')).toBeVisible()
    expect(fetch).toHaveBeenCalledWith('/api/status', expect.objectContaining({ signal: expect.any(AbortSignal) }))
  })

  it('permite reintentar tras un fallo HTTP sin afirmar conexión', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, json: async () => ({ application: 'RCC' }) }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Frontend conectado con la API C#.')).toBeVisible()
  })

  it('no acepta HTML o una respuesta ajena como conexión válida', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ application: 'other' }) }))
    render(<App />)
    expect(await screen.findByText('No se pudo conectar con la API.')).toBeVisible()
  })
})
