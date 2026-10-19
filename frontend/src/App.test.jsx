import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from './App.jsx'
import { MemoryRouter } from 'react-router-dom'
import ConnectionStatus from './components/ConnectionStatus.jsx'
import { copy } from './content.js'

describe('Conexión frontend/API', () => {
  it('muestra conexión solo cuando la respuesta cumple el contrato', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ application: 'RCC' }) }),
    )
    render(<ConnectionStatus t={copy.es} />)
    expect(await screen.findByText('Conexión verificada')).toBeVisible()
    expect(fetch).toHaveBeenCalledWith(
      '/api/status',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    )
  })

  it('permite reintentar tras un fallo HTTP sin afirmar conexión', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce({ ok: false })
        .mockResolvedValueOnce({ ok: true, json: async () => ({ application: 'RCC' }) }),
    )
    render(<ConnectionStatus t={copy.es} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Conexión verificada')).toBeVisible()
  })

  it('no acepta HTML o una respuesta ajena como conexión válida', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ application: 'other' }) }),
    )
    render(<ConnectionStatus t={copy.es} />)
    expect(await screen.findByText('Conexión no disponible')).toBeVisible()
  })
})

describe('Diseño bilingüe de RCC', () => {
  function openApp() {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ application: 'RCC' }) }),
    )
    return render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    )
  }

  it('conserva fechas, huéspedes y habitación al cambiar idioma', async () => {
    openApp()
    fireEvent.change(screen.getByLabelText('Llegada'), { target: { value: '2027-12-10' } })
    fireEvent.change(screen.getByLabelText('Salida'), { target: { value: '2027-12-12' } })
    fireEvent.change(screen.getByLabelText('Adultos'), { target: { value: '3' } })
    fireEvent.click(screen.getByRole('tab', { name: /En familia/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Switch to English' }))
    expect(screen.getByLabelText('Arrival')).toHaveValue('2027-12-10')
    expect(screen.getByLabelText('Departure')).toHaveValue('2027-12-12')
    expect(screen.getByLabelText('Adults')).toHaveValue(3)
    expect(screen.getByRole('tab', { name: /With family/ })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(document.documentElement.lang).toBe('en')
    expect(localStorage.getItem('rcc-language')).toBe('en')
    await screen.findByText('Connection verified')
  })

  it('permite elegir pestañas con teclado y conserva el foco', async () => {
    openApp()
    const tab = screen.getByRole('tab', { name: /Escapada urbana/ })
    fireEvent.keyDown(tab, { key: 'ArrowRight' })
    expect(screen.getByRole('tab', { name: /Junto al río/ })).toHaveFocus()
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'River Retreat' })).toBeVisible(),
    )
    await screen.findByText('Conexión verificada')
  })

  it('muestra vista previa sin enviar una reserva a la API', async () => {
    openApp()
    fireEvent.change(screen.getByLabelText('Llegada'), { target: { value: '2027-12-10' } })
    fireEvent.change(screen.getByLabelText('Salida'), { target: { value: '2027-12-12' } })
    fireEvent.click(screen.getByRole('button', { name: 'Explorar estancia' }))
    expect(screen.getByRole('dialog')).toBeVisible()
    expect(screen.getByText(copy.es.previewInfo)).toBeVisible()
    await screen.findByText('Conexión verificada')
    expect(fetch).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('rechaza salida anterior a llegada antes de mostrar la vista previa', async () => {
    const { container } = openApp()
    fireEvent.change(screen.getByLabelText('Llegada'), { target: { value: '2027-12-12' } })
    fireEvent.change(screen.getByLabelText('Salida'), { target: { value: '2027-12-10' } })
    fireEvent.submit(container.querySelector('form'))
    expect(screen.getByRole('alert')).toHaveTextContent(copy.es.dateError)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await screen.findByText('Conexión verificada')
  })
})
