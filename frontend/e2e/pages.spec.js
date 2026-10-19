import { test, expect } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  await page.route('**/api/status', (route) => route.fulfill({ json: { application: 'RCC' } }))
})

async function backdropGeometry(page) {
  return page.locator('.hero-photo').evaluate((image) => {
    const rect = image.getBoundingClientRect()
    return {
      x: rect.x,
      y: rect.y,
      width: rect.width,
      height: rect.height,
      position: getComputedStyle(image).objectPosition,
      fit: getComputedStyle(image).objectFit,
      shade: getComputedStyle(image.parentElement.querySelector('.hero-shade')).backgroundImage,
    }
  })
}

for (const width of [360, 768, 1440]) {
  for (const language of ['es', 'en']) {
    test(`páginas independientes ${width}px / ${language}`, async ({ page }, testInfo) => {
      await page.setViewportSize({ width, height: 1000 })
      await page.goto('/')
      if (language === 'en') await page.getByRole('button', { name: 'Switch to English' }).click()
      const homeGeometry = await backdropGeometry(page)
      await page.goto('/habitaciones')
      const routes = [
        ['habitaciones', language === 'es' ? 'Habitaciones' : 'Rooms'],
        ['experiencias', language === 'es' ? 'Experiencias' : 'Experiences'],
        ['destino', language === 'es' ? 'Destino' : 'Destination'],
      ]
      let cityGeometry
      for (const [route, label] of routes) {
        if (width === 360)
          await page
            .getByRole('button', { name: language === 'es' ? 'Menú' : 'Menu', exact: true })
            .click()
        await page.getByRole('navigation').getByRole('link', { name: label, exact: true }).click()
        await expect(page).toHaveURL(new RegExp(`/${route}$`))
        await page.reload()
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await expect(page.locator('html')).toHaveAttribute('lang', language)
        await expect(
          page
            .getByRole('navigation', { includeHidden: true })
            .getByRole('link', { name: label, exact: true, includeHidden: true }),
        ).toHaveAttribute('aria-current', 'page')
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
          ),
        ).toBe(true)
        const heroPhoto = page.locator('.rooms-hero-photo img, .city-hero-photo')
        if (await heroPhoto.count())
          await expect
            .poll(() => heroPhoto.evaluate((image) => image.complete && image.naturalWidth > 0))
            .toBe(true)
        if (route !== 'habitaciones') {
          const geometry = await backdropGeometry(page)
          expect(geometry).toEqual(homeGeometry)
          if (cityGeometry) expect(geometry).toEqual(cityGeometry)
          cityGeometry = geometry
        }
        await page.screenshot({ path: testInfo.outputPath(`${route}-${width}-${language}.png`) })
      }
      await page.goBack()
      await expect(page).toHaveURL(/\/experiencias$/)
      await page.goForward()
      await expect(page).toHaveURL(/\/destino$/)
      await page.getByRole('banner').getByRole('link', { name: 'RCC Chongqing' }).click()
      await expect(page.getByRole('tab', { selected: true })).toBeVisible()
      await expect(page).toHaveURL('/')
      await expect(
        page.getByRole('navigation', { includeHidden: true }).getByRole('link', {
          name: language === 'es' ? 'Inicio' : 'Home',
          exact: true,
          includeHidden: true,
        }),
      ).toHaveAttribute('aria-current', 'page')
    })
  }
}

test('fundido al cambiar de página y alternativa con movimiento reducido', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.goto('/experiencias')
  await page.getByRole('navigation').getByRole('link', { name: 'Destino', exact: true }).click()
  await expect(page).toHaveURL('/destino')
  await expect(page.locator('.route-view')).toHaveCSS('animation-name', 'route-enter')
  await expect(page.locator('.route-view')).toHaveCSS('transform', 'none')
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.getByRole('navigation').getByRole('link', { name: 'Inicio', exact: true }).click()
  await expect(page.locator('.route-view')).toHaveCSS('animation-name', 'none')
})

test('colección oficial: filtros, búsqueda, galería e idioma', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto('/habitaciones')
  const cards = page.locator('.official-room-card')
  await expect(cards).toHaveCount(7)
  await page.getByRole('button', { name: 'Suites', exact: true }).click()
  await expect(cards).toHaveCount(3)
  await page.getByRole('button', { name: 'Habitaciones', exact: true }).click()
  await expect(cards).toHaveCount(4)
  await page.getByRole('button', { name: 'Vista al río', exact: true }).click()
  await expect(cards).toHaveCount(3)
  await page.getByRole('button', { name: 'Todas', exact: true }).click()
  await page.getByRole('searchbox', { name: 'Buscar habitación' }).fill('Executive')
  await expect(cards).toHaveCount(1)
  await expect(cards).toContainText('117 m²')
  await page.getByRole('button', { name: 'Switch to English' }).click()
  await expect(page.getByRole('searchbox', { name: 'Search rooms' })).toHaveValue('Executive')
  await page.getByRole('button', { name: 'View gallery for Executive Suite' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByRole('img')).toHaveAttribute('src', '/images/regent/executive.webp')
  await dialog.getByRole('button', { name: 'Next photo' }).click()
  await expect(dialog.getByRole('img')).toHaveAttribute(
    'src',
    '/images/regent/executive-living.webp',
  )
  await expect
    .poll(() =>
      dialog.getByRole('img').evaluate((image) => image.complete && image.naturalWidth > 0),
    )
    .toBe(true)
  await page.keyboard.press('Escape')
  await expect(page.getByRole('button', { name: 'View gallery for Executive Suite' })).toBeFocused()
  await page.getByRole('searchbox').fill('no-such-room')
  await expect(cards).toHaveCount(0)
  await page.getByRole('button', { name: 'View all spaces' }).click()
  await expect(cards).toHaveCount(7)
})

test('experiencias: abrir detalle y cerrar con teclado', async ({ page }) => {
  await page.goto('/experiencias')
  const trigger = page.getByRole('button', { name: 'Imaginar este momento' }).first()
  await trigger.click()
  await expect(page.getByRole('dialog')).toContainText('Inspiración editorial')
  await page.keyboard.press('Escape')
  await expect(trigger).toBeFocused()
})
