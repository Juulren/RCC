import { test, expect } from '@playwright/test'
import { mockCatalogue, catalogItems } from './fixtures/catalog.js'

for (const width of [360, 768, 1440]) {
  for (const language of ['es', 'en']) {
    test(`recommendation priorities and safe diagnosis ${width}px ${language}`, async ({
      page,
    }, info) => {
      const es = language === 'es'
      await page.setViewportSize({ width, height: 1000 })
      await page.emulateMedia({ reducedMotion: 'reduce' })
      await mockCatalogue(page)
      await page.route('**/api/status', (route) => route.fulfill({ json: { application: 'RCC' } }))
      await page.route('**/api/auth/me', (route) =>
        route.fulfill({
          json: { id: 'fixture', email: 'fixture@example.test', roles: ['Customer'] },
        }),
      )
      await page.route('**/api/auth/csrf', (route) => route.fulfill({ json: { token: 'fixture' } }))
      await page.route('**/api/availability', (route) =>
        route.fulfill({
          json: {
            selections: [{ index: 0, options: [{ roomType: catalogItems[0], availableCount: 2 }] }],
          },
        }),
      )
      let calls = 0
      await page.route('**/api/recommendations', (route) => {
        const body = route.request().postDataJSON()
        expect(body.budget).toBe(4000)
        expect(body.preferences).toEqual(['river'])
        calls++
        return route.fulfill({
          json: {
            mode: calls === 1 ? 'deterministic' : 'ai',
            reason: calls === 1 ? 'AI_PROVIDER_QUOTA' : 'AI_RANKED',
            options: [
              {
                id: 'option-0',
                explanation: 'CNY 1760.00',
                quote: {
                  total: 1760,
                  items: [
                    {
                      roomTypeId: catalogItems[0].id,
                      nameEs: 'Classic Room',
                      nameEn: 'Classic Room',
                      adults: 2,
                      childAges: [],
                    },
                  ],
                },
              },
            ],
          },
        })
      })
      const arrival = new Date(Date.now() + 10 * 86400000).toISOString().slice(0, 10)
      const departure = new Date(Date.now() + 12 * 86400000).toISOString().slice(0, 10)
      await page.goto('/reservar')
      if (!es) await page.getByRole('button', { name: 'Switch to English' }).click()
      await page.getByLabel(es ? 'Llegada' : 'Arrival', { exact: true }).fill(arrival)
      await page.getByLabel(es ? 'Salida' : 'Departure', { exact: true }).fill(departure)
      await page
        .getByRole('button', { name: es ? /Buscar disponibilidad/ : /Search availability/ })
        .click()
      const panel = page.locator('.recommendation-panel')
      await expect(panel.getByText(es ? '2 noches' : '2 nights')).toBeVisible()
      await expect(panel).toContainText(es ? 'no es por persona' : 'not each guest')
      await panel
        .getByLabel(
          es
            ? 'Presupuesto total de la estancia (CNY, opcional)'
            : 'Total stay budget (CNY, optional)',
        )
        .fill('4000')
      await expect(panel.locator('.recommendation-budget-average')).toContainText('2,000')
      const river = panel.getByRole('checkbox', {
        name: es ? 'Vista al río' : 'River view',
        exact: true,
      })
      await river.focus()
      await page.keyboard.press('Space')
      await expect(river).toBeChecked()
      await panel
        .getByRole('button', { name: es ? 'Pedir recomendaciones' : 'Get recommendations' })
        .click()
      await expect(panel).toContainText(
        es ? 'alcanzó su cuota disponible' : 'reached its available quota',
      )
      await expect(
        panel.getByRole('button', { name: es ? 'Usar esta combinación' : 'Use this combination' }),
      ).toBeVisible()
      await panel
        .getByRole('button', { name: es ? 'Pedir recomendaciones' : 'Get recommendations' })
        .click()
      await expect(panel).toContainText(es ? 'Ordenadas con IA' : 'Ranked with AI')
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true)
      await panel.screenshot({ path: info.outputPath(`priorities-${width}-${language}.png`) })
    })
  }
}
