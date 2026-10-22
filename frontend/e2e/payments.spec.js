import { test, expect } from '@playwright/test'

async function write(page, path, data) {
  const { token } = await (await page.request.get('/api/auth/csrf')).json()
  return page.request.post(`/api${path}`, { headers: { 'X-CSRF-TOKEN': token }, data })
}
async function driver(page, id, action) {
  const token = process.env.RCC_PAYMENT_TEST_TOKEN
  if (!token)
    throw new Error(
      'Use scripts/test-payments.ps1; the payment test harness requires a temporary driver token.',
    )
  expect(
    (
      await page.request.post(`/__test/payments/${id}/${action}`, {
        headers: { 'X-Test-Token': token },
      })
    ).status(),
  ).toBe(204)
}
async function booking(page, es) {
  const email = `pay-${crypto.randomUUID()}@example.test`,
    password = 'Rcc-Payment-E2E-2026!'
  expect((await write(page, '/auth/register', { email, password })).status()).toBe(201)
  expect((await write(page, '/auth/login', { email, password })).status()).toBe(204)
  await page.goto('/reservar')
  if (!es) await page.getByRole('button', { name: 'Switch to English' }).click()
  const date = new Date()
  date.setUTCDate(date.getUTCDate() + 10)
  const format = () =>
    new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Asia/Shanghai',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(date)
  await page.getByLabel(es ? 'Llegada' : 'Arrival', { exact: true }).fill(format())
  date.setUTCDate(date.getUTCDate() + 2)
  await page.getByLabel(es ? 'Salida' : 'Departure', { exact: true }).fill(format())
  await page.getByRole('button', { name: es ? 'Añadir habitación' : 'Add room' }).click()
  await page
    .getByRole('button', { name: es ? 'Buscar disponibilidad' : 'Search availability' })
    .click()
  await expect(page.locator('.space-selection')).toHaveCount(2)
  for (const section of await page.locator('.space-selection').all())
    await section.getByRole('radio', { name: 'Classic Room', exact: true }).check()
  await page.getByRole('button', { name: es ? 'Cotizar selección' : 'Get quote' }).click()
  await page
    .getByRole('checkbox', {
      name: es
        ? 'Acepto esta cotización y las fechas seleccionadas.'
        : 'I accept this quote and the selected dates.',
      exact: true,
    })
    .check()
  await page
    .getByRole('button', { name: es ? 'Reservar por 30 minutos' : 'Reserve for 30 minutes' })
    .click()
  await expect(page).toHaveURL(/mis-reservas\?reserva=/)
  return new URL(page.url()).searchParams.get('reserva')
}
async function openProvider(page, es) {
  // Only the external page is simulated. All RCC endpoints, PostgreSQL, SDK
  // signature verification and durable processing run in the real C# app.
  await page.route('https://checkout.stripe.com/**', (route) =>
    route.fulfill({
      contentType: 'text/html',
      body: '<!doctype html><html><body><h1>Stripe simulation for automated tests</h1><p>No real provider call or card data.</p></body></html>',
    }),
  )
  await page
    .getByRole('button', { name: es ? 'Pagar en Stripe · prueba' : 'Pay with Stripe · test' })
    .click()
  await expect(
    page.getByRole('heading', { name: 'Stripe simulation for automated tests' }),
  ).toBeVisible()
}

for (const [width, es] of [
  [1440, true],
  [360, false],
]) {
  test(`pago simulado: rechazo, reintento, confirmación y reembolso ${width}px`, async ({
    page,
  }, info) => {
    await page.setViewportSize({ width, height: 1000 })
    const id = await booking(page, es),
      origin = new URL(page.url()).origin
    const before = await (await page.request.get(`/api/reservations/${id}`)).json()
    await openProvider(page, es)
    await driver(page, id, 'declined')
    await page.goto(`${origin}/mis-reservas?reserva=${id}&pago=retorno`)
    await expect(
      page.getByText(es ? 'Pendiente de pago' : 'Pending payment', { exact: true }),
    ).toBeVisible()
    expect((await (await page.request.get(`/api/reservations/${id}`)).json()).expiresAt).toBe(
      before.expiresAt,
    )
    await openProvider(page, es)
    await driver(page, id, 'paid')
    await page.goto(`${origin}/mis-reservas?reserva=${id}&pago=retorno`)
    await expect(page.getByText(es ? 'Confirmada' : 'Confirmed', { exact: true })).toBeVisible()
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    ).toBeTruthy()
    await page
      .locator('.reservation-featured')
      .screenshot({ path: info.outputPath(`verified-${width}.png`) })
    await page
      .getByRole('button', { name: es ? 'Cancelar reserva' : 'Cancel reservation', exact: true })
      .click()
    await page
      .getByRole('dialog')
      .getByRole('button', { name: es ? 'Sí, cancelar reserva' : 'Yes, cancel reservation' })
      .click()
    await expect(page.getByText(es ? 'Cancelada' : 'Cancelled', { exact: true })).toBeVisible()
    await expect(page.locator('.payment-summary')).toContainText(es ? 'pendiente' : 'pending')
    await driver(page, id, 'drain')
    await page
      .getByRole('button', { name: es ? 'Actualizar reservas' : 'Refresh reservations' })
      .click()
    await expect(page.locator('.payment-summary')).toContainText(es ? 'completado' : 'completed')
    await page
      .locator('.reservation-featured')
      .screenshot({ path: info.outputPath(`refund-${width}.png`) })
  })
}

test('retorno sin webhook no confirma; reconciliación y pago tardío no recuperan inventario', async ({
  page,
}) => {
  const id = await booking(page, true),
    origin = new URL(page.url()).origin
  await openProvider(page, true)
  await page.goto(`${origin}/mis-reservas?reserva=${id}&pago=retorno`)
  await expect(page.getByText('Pendiente de pago', { exact: true })).toBeVisible()
  await driver(page, id, 'expire')
  await driver(page, id, 'paid-without-webhook')
  await driver(page, id, 'reconcile')
  await driver(page, id, 'drain')
  await page.reload()
  await expect(page.getByText('Vencida', { exact: true })).toBeVisible()
  await expect(page.locator('.payment-summary')).toContainText('completado')
  await expect(page.getByRole('button', { name: 'Cancelar reserva', exact: true })).toHaveCount(0)
})
