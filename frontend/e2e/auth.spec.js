import { test, expect } from '@playwright/test'

// These journeys use real ASP.NET Identity and PostgreSQL. No auth API mocks.
for (const [width, language] of [
  [360, 'es'],
  [1440, 'en'],
]) {
  test(`registro, login, sesión, permisos y logout reales ${width}px / ${language}`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 1000 })
    await page.goto('/')
    if (language === 'en') await page.getByRole('button', { name: 'Switch to English' }).click()
    const es = language === 'es'
    if (width === 360) await page.getByRole('button', { name: 'Menú', exact: true }).click()
    await page
      .getByRole('banner')
      .getByRole('link', { name: es ? 'Mi estancia' : 'My stay', exact: true })
      .click()
    await expect(page).toHaveURL('/cuenta')
    await page
      .getByRole('button', { name: es ? 'Crear cuenta' : 'Create account', exact: true })
      .click()
    const email = `journey-${Date.now()}-${language}@example.test`
    const password = 'Rcc-E2E-2026!Strong'
    const emailField = page.getByRole('textbox', {
      name: es ? 'Correo electrónico' : 'Email address',
    })
    await emailField.fill(email)
    await page.getByLabel(es ? 'Contraseña' : 'Password', { exact: true }).fill(password)
    await page
      .getByRole('button', { name: es ? 'Crear mi cuenta' : 'Create my account', exact: true })
      .click()
    await expect(
      page.getByRole('status').filter({ hasText: es ? 'Cuenta creada' : 'Account created' }),
    ).toBeVisible()
    await page
      .getByLabel(es ? 'Contraseña' : 'Password', { exact: true })
      .fill('Incorrect-password!')
    await page
      .getByRole('button', {
        name: es ? 'Entrar a mi estancia' : 'Sign in to my stay',
        exact: true,
      })
      .click()
    await expect(page.getByRole('alert')).toContainText(
      es ? 'No se pudo iniciar sesión' : 'Could not sign in',
    )
    await page.getByLabel(es ? 'Contraseña' : 'Password', { exact: true }).fill(password)
    await page
      .getByRole('button', {
        name: es ? 'Entrar a mi estancia' : 'Sign in to my stay',
        exact: true,
      })
      .click()
    await expect(page.getByText(email, { exact: true })).toBeVisible()
    await page.reload()
    await expect(page.getByText(email, { exact: true })).toBeVisible()
    const cookie = (await page.context().cookies()).find((cookie) => cookie.name === 'Rcc.Session')
    expect(cookie.httpOnly).toBe(true)
    expect(cookie.sameSite).toBe('Lax')
    expect(await page.evaluate(() => Object.keys(localStorage))).toEqual(['rcc-language'])
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: testInfo.outputPath(`session-${width}-${language}.png`) })
    await page.goto('/admin')
    await expect(
      page
        .getByRole('status')
        .filter({
          hasText: es
            ? 'requiere una cuenta de administrador'
            : 'requires an administrator account',
        }),
    ).toBeVisible()
    await page.goto('/cuenta')
    await page.getByRole('button', { name: es ? 'Cerrar sesión' : 'Sign out', exact: true }).click()
    await expect(
      page.getByRole('form', { name: es ? 'Iniciar sesión' : 'Sign in', exact: true }),
    ).toBeVisible()
    expect((await page.request.get('/api/auth/me')).status()).toBe(401)
    await page.reload()
    await expect(
      page.getByRole('form', { name: es ? 'Iniciar sesión' : 'Sign in', exact: true }),
    ).toBeVisible()
  })
}

test('acceso anónimo protegido y recuperación tras fallo de conexión', async ({ page }) => {
  await page.goto('/admin')
  await expect(page.getByRole('status').filter({ hasText: 'Inicia sesión' })).toBeVisible()
  await page.route('**/api/auth/me', (route) => route.abort())
  await page.goto('/cuenta')
  await expect(page.getByRole('alert')).toContainText('No pudimos comprobar la sesión')
  await page.unroute('**/api/auth/me')
  await page.getByRole('button', { name: 'Volver a comprobar' }).click()
  await expect(page.getByRole('form', { name: 'Iniciar sesión', exact: true })).toBeVisible()
})
