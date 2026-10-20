import { defineConfig } from '@playwright/test'
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/reservations.spec.js',
  workers: 1,
  timeout: 60000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-reservations' }]],
  outputDir: 'test-results-reservations',
  use: {
    baseURL:
      process.env.RCC_RESERVATIONS_BASE_URL ??
      process.env.RCC_CATALOG_BASE_URL ??
      'http://127.0.0.1:5080',
    browserName: 'chromium',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
})
