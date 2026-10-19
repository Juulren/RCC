import { test, expect } from '@playwright/test'

async function write(page, path, method, data) {
  const { token } = await (await page.request.get('/api/auth/csrf')).json()
  return page.request.fetch(`/api${path}`, { method, headers: { 'X-CSRF-TOKEN': token }, data })
}

for (const [width, language] of [
  [1440, 'es'],
  [360, 'en'],
]) {
  test(`administración real: hotel, tipos, galería e inventario ${width}px / ${language}`, async ({
    page,
  }, testInfo) => {
    const email = process.env.RCC_E2E_ADMIN_EMAIL,
      password = process.env.RCC_E2E_ADMIN_PASSWORD
    if (!email || !password)
      throw new Error(
        'Configure RCC_E2E_ADMIN_EMAIL y RCC_E2E_ADMIN_PASSWORD en una base de pruebas; use scripts/test-catalog.ps1.',
      )
    const es = language === 'es',
      suffix = `${language}-${Date.now()}`,
      slug = `e2e-${suffix}`
    const nameEs = `Suite prueba ${suffix}`,
      nameEn = `Test suite ${suffix}`,
      name = es ? nameEs : nameEn
    await page.setViewportSize({ width, height: 1000 })
    await page.goto('/cuenta')
    if (!es) await page.getByRole('button', { name: 'Switch to English' }).click()
    await page
      .getByRole('textbox', { name: es ? 'Correo electrónico' : 'Email address' })
      .fill(email)
    await page.getByLabel(es ? 'Contraseña' : 'Password', { exact: true }).fill(password)
    await page
      .getByRole('button', {
        name: es ? 'Entrar a mi estancia' : 'Sign in to my stay',
        exact: true,
      })
      .click()
    await page
      .getByRole('link', { name: es ? 'Acceder a administración' : 'Open administration' })
      .click()
    await page
      .getByRole('button', { name: es ? 'Nuevo tipo' : 'New room type', exact: true })
      .click()
    let dialog = page.getByRole('dialog')
    await dialog.getByLabel(es ? 'Identificador' : 'Identifier', { exact: true }).fill(slug)
    await dialog.getByLabel(es ? 'Nombre (ES)' : 'Name (ES)', { exact: true }).fill(nameEs)
    await dialog.getByLabel(es ? 'Nombre (EN)' : 'Name (EN)', { exact: true }).fill(nameEn)
    await dialog
      .getByLabel(es ? 'Descripción (ES)' : 'Description (ES)', { exact: true })
      .fill(`Descripción ${suffix}`)
    await dialog
      .getByLabel(es ? 'Descripción (EN)' : 'Description (EN)', { exact: true })
      .fill(`Description ${suffix}`)
    await dialog
      .getByLabel(es ? 'Capacidad total de demostración' : 'Demonstration total capacity', {
        exact: true,
      })
      .fill('4')
    await dialog.getByLabel(es ? 'Máximo de adultos' : 'Maximum adults', { exact: true }).fill('3')
    await dialog.getByLabel(es ? 'Máximo de niños' : 'Maximum children', { exact: true }).fill('2')
    await dialog
      .getByLabel(es ? 'Tarifa de demostración CNY / noche' : 'Demonstration rate CNY / night', {
        exact: true,
      })
      .fill('1234.56')
    await dialog
      .getByLabel(`${es ? 'Texto alternativo' : 'Alternative text'} (ES) 1`, { exact: true })
      .fill('Dormitorio de prueba')
    await dialog
      .getByLabel(`${es ? 'Texto alternativo' : 'Alternative text'} (EN) 1`, { exact: true })
      .fill('Test bedroom')
    await dialog
      .getByRole('button', { name: es ? 'Añadir imagen' : 'Add image', exact: true })
      .click()
    await dialog
      .getByLabel(`${es ? 'Texto alternativo' : 'Alternative text'} (ES) 2`, { exact: true })
      .fill('Baño de prueba')
    await dialog
      .getByLabel(`${es ? 'Texto alternativo' : 'Alternative text'} (EN) 2`, { exact: true })
      .fill('Test bathroom')
    await dialog
      .getByRole('button', { name: `${es ? 'Subir imagen' : 'Move image up'} 2`, exact: true })
      .click()
    await dialog
      .getByRole('button', { name: es ? 'Guardar tipo' : 'Save room type', exact: true })
      .click()
    await expect(dialog).not.toBeVisible()
    await expect(page.getByRole('heading', { name, exact: true })).toBeVisible()
    let type = (await (await page.request.get(`/api/room-types?search=${slug}`)).json()).items[0]
    expect(type.nightlyRate).toBe(1234.56)
    expect(type.images[0].url).toBe('/images/regent/classic-bathroom.webp')
    await page.goto('/habitaciones')
    await page
      .getByRole('searchbox', { name: es ? 'Buscar habitación' : 'Search rooms' })
      .fill(slug)
    await expect(page.locator('.official-room-card')).toHaveCount(1)
    await expect(page.getByRole('heading', { name, exact: true })).toBeVisible()
    await expect(page.locator('.official-room-card')).toContainText('1,234.56')
    await page.goto('/admin')
    await page
      .getByRole('button', { name: `${es ? 'Editar' : 'Edit'} ${name}`, exact: true })
      .click()
    dialog = page.getByRole('dialog')
    expect(
      (
        await write(page, `/admin/room-types/${type.id}`, 'PUT', { ...type, nightlyRate: 1300 })
      ).status(),
    ).toBe(200)
    await dialog
      .getByLabel(es ? 'Tarifa de demostración CNY / noche' : 'Demonstration rate CNY / night', {
        exact: true,
      })
      .fill('1400')
    await dialog
      .getByRole('button', { name: es ? 'Guardar tipo' : 'Save room type', exact: true })
      .click()
    await expect(dialog.getByRole('alert')).toContainText(es ? 'Otra sesión' : 'Another session')
    await expect(
      dialog.getByLabel(
        es ? 'Tarifa de demostración CNY / noche' : 'Demonstration rate CNY / night',
        { exact: true },
      ),
    ).toHaveValue('1400')
    await dialog
      .getByRole('button', {
        name: es ? 'Descartar cambios y recargar' : 'Discard changes and reload',
        exact: true,
      })
      .click()
    await expect(dialog).not.toBeVisible()
    type = await (await page.request.get(`/api/room-types/${type.id}`)).json()
    expect(type.nightlyRate).toBe(1300)
    await page
      .getByRole('button', { name: es ? 'Habitaciones físicas' : 'Physical rooms', exact: true })
      .click()
    await page
      .getByRole('button', { name: es ? 'Nueva habitación' : 'New room', exact: true })
      .click()
    dialog = page.getByRole('dialog')
    const number = `E-${language}-${Date.now().toString().slice(-7)}`.toUpperCase()
    await dialog
      .getByLabel(es ? 'Número de habitación' : 'Room number', { exact: true })
      .fill(number)
    await dialog
      .getByLabel(es ? 'Tipo de habitación' : 'Room type', { exact: true })
      .selectOption(type.id)
    await dialog
      .getByRole('button', { name: es ? 'Guardar habitación' : 'Save room', exact: true })
      .click()
    await expect(dialog).not.toBeVisible()
    await page
      .getByRole('button', { name: `${es ? 'Editar' : 'Edit'} ${number}`, exact: true })
      .click()
    await expect(
      page.getByRole('dialog').getByLabel(es ? 'Tipo de habitación' : 'Room type', { exact: true }),
    ).toBeDisabled()
    await page
      .getByRole('dialog')
      .getByRole('checkbox', { name: es ? 'Habitación operativa' : 'Operational room' })
      .uncheck()
    await page
      .getByRole('dialog')
      .getByRole('button', { name: es ? 'Guardar habitación' : 'Save room', exact: true })
      .click()
    await expect(page.getByRole('dialog')).not.toBeVisible()
    await page.reload()
    await page
      .getByRole('button', { name: es ? 'Habitaciones físicas' : 'Physical rooms', exact: true })
      .click()
    const row = page.locator('.catalog-admin-list > li').filter({ hasText: number })
    await expect(row).toContainText(es ? 'Inactivo' : 'Inactive')
    await page.getByRole('button', { name: 'Hotel', exact: true }).click()
    const hotel = await (await page.request.get('/api/hotel')).json()
    await page
      .getByLabel(es ? 'Nombre del hotel' : 'Hotel name', { exact: true })
      .fill(`RCC ${suffix}`)
    await page
      .getByRole('button', { name: es ? 'Añadir servicio' : 'Add service', exact: true })
      .click()
    const serviceNumber = hotel.services.length + 1
    await page
      .getByLabel(`${es ? 'Servicio' : 'Service'} ${serviceNumber} (ES)`, { exact: true })
      .fill(`Servicio ${suffix}`)
    await page
      .getByLabel(`${es ? 'Servicio' : 'Service'} ${serviceNumber} (EN)`, { exact: true })
      .fill(`Service ${suffix}`)
    await page
      .getByRole('button', { name: es ? 'Guardar hotel' : 'Save hotel', exact: true })
      .click()
    await expect(
      page.getByRole('status').filter({ hasText: es ? 'Cambios guardados' : 'Changes saved' }),
    ).toBeVisible()
    await page.goto('/habitaciones')
    await expect(page.getByRole('heading', { name: `RCC ${suffix}`, exact: true })).toBeVisible()
    await expect(
      page.getByText(`${es ? 'Servicio' : 'Service'} ${suffix}`, { exact: true }),
    ).toBeVisible()
    await page.goto('/admin')
    await page
      .getByRole('button', { name: `${es ? 'Editar' : 'Edit'} ${name}`, exact: true })
      .click()
    await page
      .getByRole('dialog')
      .getByRole('checkbox', {
        name: es ? 'Publicado en el catálogo' : 'Published in the catalogue',
      })
      .uncheck()
    await page
      .getByRole('dialog')
      .getByRole('button', { name: es ? 'Guardar tipo' : 'Save room type', exact: true })
      .click()
    await expect(page.getByRole('dialog')).not.toBeVisible()
    expect((await page.request.get(`/api/room-types/${type.id}`)).status()).toBe(404)
    expect(
      (await (await page.request.get(`/api/admin/rooms?roomTypeId=${type.id}`)).json()).total,
    ).toBe(1)
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({
      path: testInfo.outputPath(`admin-${width}-${language}.png`),
      fullPage: true,
    })
    if (!es) {
      await page.getByRole('button', { name: `Edit ${name}`, exact: true }).click()
      expect((await write(page, '/auth/logout', 'POST', {})).status()).toBe(204)
      await page
        .getByRole('dialog')
        .getByRole('button', { name: 'Save room type', exact: true })
        .click()
      await expect(page.getByRole('status').filter({ hasText: 'Sign in to access' })).toBeVisible()
    }
  })
}
