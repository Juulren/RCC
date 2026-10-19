import { test, expect } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  // UI tests isolate the health call; C#/PostgreSQL have their own integration suite.
  await page.route('**/api/status', (route) => route.fulfill({ json: { application: 'RCC' } }))
  await page.route('**/api/auth/me', (route) => route.fulfill({ status: 401 }))
})

for (const width of [360, 768, 1440]) {
  for (const language of ['es', 'en']) {
    test(`diseño ${width}px / ${language}`, async ({ page }, testInfo) => {
      await page.setViewportSize({ width, height: 1000 })
      await page.goto('/')
      if (language === 'en') await page.getByRole('button', { name: 'Switch to English' }).click()
      await expect(page.locator('html')).toHaveAttribute('lang', language)
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
      await expect(page.getByRole('tab', { selected: true })).toBeVisible()
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
        ),
      ).toBe(true)
      const loaded = await page
        .locator('.hero-photo')
        .evaluate((image) => image.complete && image.naturalWidth > 0)
      expect(loaded).toBe(true)
      await page.screenshot({
        path: testInfo.outputPath(`home-${width}-${language}.png`),
        fullPage: true,
      })
      if (width === 360) {
        await page
          .getByRole('button', { name: language === 'es' ? 'Menú' : 'Menu', exact: true })
          .click()
      }
      await page
        .getByRole('navigation')
        .getByRole('link', { name: language === 'es' ? 'Habitaciones' : 'Rooms', exact: true })
        .click()
      await expect(page).toHaveURL(/\/habitaciones$/)
      const room = page
        .locator('article')
        .filter({ has: page.getByRole('heading', { name: 'Classic Room', exact: true }) })
      await expect(room).toBeVisible()
      await room
        .getByRole('button', {
          name: language === 'es' ? 'Conocer el espacio' : 'Explore this space',
        })
        .click()
      await expect(page.getByRole('dialog')).toBeVisible()
      await page.keyboard.press('Escape')
      await expect(page.getByRole('dialog')).not.toBeVisible()
      await expect(
        room.getByRole('button', {
          name: language === 'es' ? 'Conocer el espacio' : 'Explore this space',
        }),
      ).toBeFocused()
    })
  }
}

test('teclado: salto a contenido y pestañas', async ({ page }) => {
  await page.goto('/')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Saltar al contenido' })).toBeFocused()
  await page.keyboard.press('Enter')
  await page.getByRole('tab', { name: /Escapada urbana/ }).focus()
  await page.keyboard.press('ArrowRight')
  await expect(page.getByRole('tab', { name: /Junto al río/ })).toBeFocused()
  await expect(page.getByRole('heading', { name: 'River Retreat' })).toBeVisible()
})

test('movimiento reducido y fallback sin imágenes', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.route('**/images/*.webp', (route) => route.abort())
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  expect(
    await page
      .locator('.ambient-light')
      .evaluate((element) => getComputedStyle(element).animationName),
  ).toBe('none')
  await expect(page.locator('.ambient-light')).toHaveAttribute('data-visible', 'false')
  await expect(page.locator('.neon-canvas')).not.toBeVisible()
  await page.getByRole('tab', { name: /En familia/ }).click()
  await expect(page.getByRole('heading', { name: 'Family Residence' })).toBeVisible()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true)
})

test('fondo realmente animado y pausado al salir de pantalla', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.goto('/')
  const environment = page.locator('.ambient-light')
  const canvas = page.locator('.neon-canvas')
  await expect(environment).toHaveAttribute('data-visible', 'true')
  const first = await canvas.evaluate((element) => element.toDataURL())
  await expect.poll(() => canvas.evaluate((element) => element.toDataURL())).not.toBe(first)
  await page.getByRole('button', { name: 'Pausar fondo' }).click()
  await expect(environment).toHaveAttribute('data-visible', 'false')
  await expect(page.getByRole('button', { name: 'Reanudar fondo' })).toHaveAttribute(
    'aria-pressed',
    'true',
  )
  await page.getByRole('button', { name: 'Reanudar fondo' }).click()
  await expect(environment).toHaveAttribute('data-visible', 'true')
  await page.getByRole('heading', { name: /El arte de/ }).scrollIntoViewIfNeeded()
  await expect(environment).toHaveAttribute('data-visible', 'false')
  const paused = await canvas.evaluate((element) => element.toDataURL())
  await page.evaluate(
    () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))),
  )
  expect(await canvas.evaluate((element) => element.toDataURL())).toBe(paused)
})

test('idioma conservado tras recarga y reflujo equivalente a zoom al 200 por ciento', async ({
  page,
}) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Switch to English' }).click()
  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
  // A 1280px browser at 200% zoom exposes 640 CSS pixels to media queries.
  // CSS `zoom` does not update media queries and is not a substitute for browser zoom.
  await page.setViewportSize({ width: 640, height: 500 })
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true)
})
