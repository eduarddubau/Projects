import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  // The app under test is served by the Angular dev server, which buckles when too
  // many workers cold-load it at once (login page times out). Cap concurrency so
  // the suite stays reliable; CI gets retries on top for the occasional hiccup.
  workers: 2,
  retries: process.env['CI'] ? 2 : 1,
  reporter: 'list',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
