import { test, expect } from '@playwright/test'

const classic = '41000000-0000-0000-0000-000000000001'
async function write(request, path, data, method = 'POST', headers = {}) {
  const { token } = await (await request.get('/api/auth/csrf')).json()
  return request.fetch(`/api${path}`, {
    method,
    headers: { 'X-CSRF-TOKEN': token, ...headers },
    data,
  })
}
async function account(page, es) {
  const email = `stay-${crypto.randomUUID()}@example.test`,
    password = 'Rcc-Stay-2026!Test'
  expect((await write(page.request, '/auth/register', { email, password })).status()).toBe(201)
  await page.goto('/cuenta')
  if (!es) await page.getByRole('button', { name: 'Switch to English' }).click()
  await page.getByLabel(es ? 'Correo electrónico' : 'Email address', { exact: true }).fill(email)
  await page.getByLabel(es ? 'Contraseña' : 'Password', { exact: true }).fill(password)
  await page
    .getByRole('button', { name: es ? 'Entrar a mi estancia' : 'Sign in to my stay', exact: true })
    .click()
  await expect(page.getByText(email, { exact: true })).toBeVisible()
}
function dates(offset = 10) {
  const base = new Date()
  base.setUTCDate(base.getUTCDate() + offset)
  const format = (date) =>
    new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Asia/Shanghai',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(date)
  const arrival = format(base)
  base.setUTCDate(base.getUTCDate() + 2)
  return { arrival, departure: format(base) }
}
async function search(page, es, count = 1) {
  const stay = dates()
  await page.goto('/reservar')
  await page.getByLabel(es ? 'Llegada' : 'Arrival', { exact: true }).fill(stay.arrival)
  await page.getByLabel(es ? 'Salida' : 'Departure', { exact: true }).fill(stay.departure)
  for (let i = 1; i < count; i++)
    await page.getByRole('button', { name: es ? 'Añadir habitación' : 'Add room' }).click()
  await page
    .getByRole('button', {
      name: es ? 'Buscar disponibilidad' : 'Search availability',
      exact: false,
    })
    .click()
  await expect(
    page.getByRole('heading', { name: es ? '02 / Elige tus espacios' : '02 / Choose your spaces' }),
  ).toBeVisible()
  return stay
}
async function selectClassic(page, es) {
  await expect(page.locator('.space-selection').first()).toBeVisible()
  for (const selection of await page.locator('.space-selection').all())
    await selection.getByRole('radio', { name: 'Classic Room', exact: true }).check()
  await page.getByRole('button', { name: es ? 'Cotizar selección' : 'Get quote' }).click()
  await expect(
    page.getByRole('heading', { name: es ? 'Tu estancia, en detalle.' : 'Your stay, in detail.' }),
  ).toBeVisible()
  await page
    .getByRole('checkbox', {
      name: es
        ? 'Acepto esta cotización y las fechas seleccionadas.'
        : 'I accept this quote and the selected dates.',
    })
    .check()
}

for (const [width, es] of [
  [1440, true],
  [768, false],
  [360, false],
]) {
  test(`reserva real de dos habitaciones, cancelación y propiedad ${width}px`, async ({
    page,
    browser,
  }, info) => {
    await page.setViewportSize({ width, height: 1000 })
    await account(page, es)
    await search(page, es, 2)
    await page.getByLabel(es ? 'Niños habitación 1' : 'Children room 1', { exact: true }).fill('1')
    await page
      .getByLabel(es ? 'Edad niño 1, habitación 1' : 'Child age 1, room 1', { exact: true })
      .fill('7')
    await expect(page.locator('.booking-options')).toHaveCount(0)
    await page
      .getByRole('button', { name: es ? 'Buscar disponibilidad' : 'Search availability' })
      .click()
    await selectClassic(page, es)
    await expect(page.locator('.booking-quote .quote-line')).toHaveCount(2)
    await expect(page.locator('.booking-quote')).toContainText('7')
    await page.screenshot({ path: info.outputPath(`quote-${width}.png`), fullPage: true })
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    ).toBeTruthy()
    // Lose the first response after the real API commits, then retry the same attempt.
    if (width === 1440)
      await page.route('**/api/reservations', async (route) => {
        const response = await route.fetch()
        expect(response.status()).toBe(201)
        await route.abort('failed')
        await page.unroute('**/api/reservations')
      })
    await page
      .getByRole('button', { name: es ? 'Reservar por 30 minutos' : 'Reserve for 30 minutes' })
      .click()
    if (width === 1440) {
      await expect(page.getByRole('alert')).toContainText('reintentar')
      await page.getByRole('button', { name: 'Reservar por 30 minutos' }).click()
    }
    await expect(page).toHaveURL(/mis-reservas\?reserva=/)
    const id = new URL(page.url()).searchParams.get('reserva')
    await expect(
      page.getByText(es ? 'Pendiente de pago' : 'Pending payment', { exact: true }),
    ).toBeVisible()
    const list = await (await page.request.get('/api/reservations')).json()
    expect(list.total).toBe(1)
    expect(list.items[0].items).toHaveLength(2)
    const outsider = await browser.newContext({ baseURL: new URL(page.url()).origin })
    expect((await outsider.request.get(`/api/reservations/${id}`)).status()).toBe(401)
    await outsider.close()
    await page.reload()
    await page
      .getByRole('button', { name: es ? 'Cancelar reserva' : 'Cancel reservation', exact: true })
      .click()
    await page
      .getByRole('dialog')
      .getByRole('button', { name: es ? 'Sí, cancelar reserva' : 'Yes, cancel reservation' })
      .click()
    await expect(page.getByText(es ? 'Cancelada' : 'Cancelled', { exact: true })).toBeVisible()
    const stay = dates()
    const quote = await page.request.post('/api/quotes', {
      data: {
        ...stay,
        rooms: [
          { roomTypeId: classic, adults: 2, childAges: [] },
          { roomTypeId: classic, adults: 2, childAges: [] },
        ],
      },
    })
    expect(quote.status()).toBe(200)
  })
}

test('tarifa cambiada exige nueva aceptación y sesión vencida no crea reservas', async ({
  page,
  browser,
}) => {
  await account(page, true)
  await search(page, true)
  await selectClassic(page, true)
  const admin = await browser.newContext({ baseURL: new URL(page.url()).origin })
  const email = process.env.RCC_E2E_ADMIN_EMAIL,
    password = process.env.RCC_E2E_ADMIN_PASSWORD
  if (!email || !password)
    throw new Error('Use scripts/test-reservations.ps1 con base aislada y administrador temporal.')
  expect((await write(admin.request, '/auth/login', { email, password })).status()).toBe(204)
  const catalogue = await (await admin.request.get('/api/admin/room-types')).json()
  const original = catalogue.items.find((type) => type.id === classic)
  const update = { ...original, nightlyRate: original.nightlyRate + 1 }
  let saved
  try {
    const response = await write(admin.request, `/admin/room-types/${classic}`, update, 'PUT')
    expect(response.status()).toBe(200)
    saved = await response.json()
    await page.getByRole('button', { name: 'Reservar por 30 minutos' }).click()
    await expect(page.getByRole('alert')).toContainText('La tarifa cambió')
    await expect(
      page.getByRole('checkbox', {
        name: 'Acepto esta cotización y las fechas seleccionadas.',
        exact: true,
      }),
    ).not.toBeChecked()
    await expect(page.getByRole('button', { name: 'Reservar por 30 minutos' })).toBeDisabled()
    await page
      .getByRole('checkbox', {
        name: 'Acepto esta cotización y las fechas seleccionadas.',
        exact: true,
      })
      .check()
    expect((await write(page.request, '/auth/logout', {})).status()).toBe(204)
    await page.getByRole('button', { name: 'Reservar por 30 minutos' }).click()
    await expect(page.getByRole('alert')).toContainText('Inicia sesión de nuevo')
  } finally {
    if (saved)
      expect(
        (
          await write(
            admin.request,
            `/admin/room-types/${classic}`,
            { ...original, revision: saved.revision },
            'PUT',
          )
        ).status(),
      ).toBe(200)
    await admin.close()
  }
})

for (const [width, es] of [
  [1440, true],
  [360, false],
]) {
  test(`recomendación local real, presupuesto y elección sin reserva ${width}px`, async ({
    page,
  }, info) => {
    await page.setViewportSize({ width, height: 1000 })
    await account(page, es)
    await search(page, es, 2)
    const panel = page.locator('.recommendation-panel')
    await expect(panel).toBeVisible()
    await panel
      .getByLabel(
        es
          ? 'Presupuesto total de la estancia (CNY, opcional)'
          : 'Total stay budget (CNY, optional)',
      )
      .fill('1')
    await panel
      .getByRole('button', { name: es ? 'Pedir recomendaciones' : 'Get recommendations' })
      .click()
    await expect(panel).toContainText(
      es ? 'No encontramos una combinación' : 'No combination found',
    )
    await panel
      .getByLabel(
        es
          ? 'Presupuesto total de la estancia (CNY, opcional)'
          : 'Total stay budget (CNY, optional)',
      )
      .fill('3600')
    await panel.getByRole('checkbox', { name: es ? 'Vista al río' : 'River view' }).check()
    await panel
      .getByRole('button', { name: es ? 'Pedir recomendaciones' : 'Get recommendations' })
      .click()
    await expect(panel).toContainText(
      es ? 'Cálculo local · No intervino IA' : 'Local calculation · No AI was used',
    )
    await expect(panel.locator('.recommendation-card')).toHaveCount(1)
    expect((await (await page.request.get('/api/reservations')).json()).total).toBe(0)
    await panel.scrollIntoViewIfNeeded()
    await panel.screenshot({ path: info.outputPath(`recommendation-${width}.png`) })
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true)
    await panel
      .getByRole('button', { name: es ? 'Usar esta combinación' : 'Use this combination' })
      .click()
    await expect(
      panel.getByRole('button', { name: es ? 'Seleccionada' : 'Selected' }),
    ).toBeVisible()
    for (const selection of await page.locator('.space-selection').all())
      await expect(
        selection.getByRole('radio', { name: 'Classic Room', exact: true }),
      ).toBeChecked()
    await page.getByRole('button', { name: es ? 'Cotizar selección' : 'Get quote' }).click()
    await expect(page.locator('.quote-total')).toContainText('3,520')
    expect((await (await page.request.get('/api/reservations')).json()).total).toBe(0)
    await page.getByLabel(es ? 'Adultos habitación 1' : 'Adults room 1', { exact: true }).fill('1')
    await expect(page.locator('.recommendation-results')).toHaveCount(0)
    await expect(page.locator('.quote-total')).toHaveCount(0)
  })
}
