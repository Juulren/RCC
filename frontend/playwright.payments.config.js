import { defineConfig } from '@playwright/test'
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/payments.spec.js',
  workers: 1,
  timeout: 60000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-payments' }]],
  outputDir: 'test-results-payments',
  use: {
    baseURL: process.env.RCC_PAYMENT_BASE_URL ?? 'http://127.0.0.1:5084',
    browserName: 'chromium',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
})
