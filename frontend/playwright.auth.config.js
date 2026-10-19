import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/auth.spec.js',
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-auth' }]],
  outputDir: 'test-results-auth',
  use: {
    baseURL: process.env.RCC_AUTH_BASE_URL ?? 'http://127.0.0.1:5080',
    browserName: 'chromium',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
})
