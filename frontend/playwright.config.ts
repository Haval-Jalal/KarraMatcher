import { defineConfig, devices } from '@playwright/test'

/**
 * E2E-testerna av de fem kritiska flödena (`#71`, SPEC.md §9).
 *
 * Kör mot en riktig stack: backend på Postgres, frontend via Vite-dev (som proxyar `/api`).
 * `E2E_BASE_URL` pekar på frontenden (sidnavigering), `E2E_API_URL` på backenden (test- och
 * kalender-endpoints som inte går via proxyn). Deterministiskt: en worker, seriell ordning,
 * eftersom flödena delar en databas.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  globalSetup: './e2e/global-setup.ts',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'on-first-retry',
    ignoreHTTPSErrors: true,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
