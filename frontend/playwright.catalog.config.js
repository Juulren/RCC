import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/catalog.spec.js',
  workers: 1,
  timeout: 60000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-catalog' }]],
  outputDir: 'test-results-catalog',
  use: {
    baseURL: process.env.RCC_CATALOG_BASE_URL ?? 'http://127.0.0.1:5080',
    browserName: 'chromium',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
})
