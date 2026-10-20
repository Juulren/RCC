import { test, expect } from '@playwright/test'
import { mockCatalogue } from './fixtures/catalog.js'

test.beforeEach(async ({ page }) => {
  await mockCatalogue(page)
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 401 }))
  await page.route('**/api/status', (route) => route.fulfill({ json: { application: 'RCC' } }))
})
for (const width of [360, 768, 1440]) {
  for (const language of ['es', 'en']) {
    test(`destination services ${width}px ${language}`, async ({ page }, info) => {
      await page.setViewportSize({ width, height: 1000 })
      await page.route('**/api/weather', (route) =>
        route.fulfill({
          json: {
            available: true,
            stale: true,
            current: {
              temperature: 23.4,
              apparentTemperature: 25.2,
              code: 3,
              windSpeed: 7.1,
              observedAt: '2026-10-20T12:00',
            },
          },
        }),
      )
      await page.goto('/destino')
      if (language === 'en') await page.getByRole('button', { name: 'Switch to English' }).click()
      const panel = page.locator('.destination-services')
      await panel.scrollIntoViewIfNeeded()
      await expect(panel).toContainText('23°')
      await expect(panel).toContainText(
        language === 'es' ? 'Último dato disponible' : 'Last available data',
      )
      await expect(panel.getByRole('link', { name: /Open-Meteo/ })).toHaveAttribute(
        'href',
        'https://open-meteo.com/',
      )
      const map = panel.getByRole('link', { name: /Google Maps/ })
      await expect(map).toHaveAttribute('href', /^https:\/\/www.google.com\/maps\/search/)
      await expect(panel.locator('iframe')).toHaveCount(0)
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true)
      await panel.screenshot({ path: info.outputPath(`services-${width}-${language}.png`) })
    })
  }
}
test('network failure keeps map link and weather can recover on retry', async ({ page }) => {
  await page.route('**/api/weather', (route) => route.abort())
  await page.route('**/api/location', (route) => route.abort())
  await page.goto('/destino')
  const panel = page.locator('.destination-services')
  await expect(panel).toContainText('El clima no está disponible')
  await expect(panel.getByRole('link', { name: /Google Maps/ })).toBeVisible()
  await page.route('**/api/weather', (route) =>
    route.fulfill({
      json: {
        available: true,
        stale: false,
        current: {
          temperature: 21,
          apparentTemperature: 20,
          code: 0,
          windSpeed: 4,
          observedAt: '2026-10-20T12:00',
        },
      },
    }),
  )
  await panel.getByRole('button', { name: 'Actualizar', exact: true }).click()
  await expect(panel).toContainText('21°')
})
test('configured map loads only on user action and keeps external fallback', async ({ page }) => {
  await page.route('**/api/location', (route) =>
    route.fulfill({
      json: {
        embedUrl: 'https://www.google.com/maps/embed/v1/place?key=fixture&q=Regent',
        searchUrl: 'https://www.google.com/maps/search/?api=1&query=Regent',
      },
    }),
  )
  await page.route('https://www.google.com/maps/embed/**', (route) => route.abort())
  await page.goto('/destino')
  await expect(page.locator('.map-panel iframe')).toHaveCount(0)
  await page.getByRole('button', { name: 'Cargar mapa de Google' }).click()
  await expect(page.locator('.map-panel iframe')).toHaveAttribute(
    'title',
    'Ubicación de Regent Chongqing',
  )
  await expect(page.getByRole('link', { name: 'Abrir ubicación en Google Maps' })).toBeVisible()
})
