import { test, expect } from '@playwright/test';

test.describe('Dashboard as home', () => {
  test('login lands on the dashboard; landing and logo route there when signed in', async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Post-login default destination is the dashboard.
    await page.waitForURL(/\/dashboard$/);
    await expect(page.locator('.page-title')).toContainText('dev2');

    // Visiting the marketing landing while signed in bounces to the dashboard.
    await page.goto('/');
    await page.waitForURL(/\/dashboard$/);

    // From elsewhere in the app, the brand logo returns to the dashboard ("home").
    await page.goto('/projects');
    await expect(page.getByRole('heading', { name: 'My Projects' })).toBeVisible();
    await page.locator('a.brand').click();
    await page.waitForURL(/\/dashboard$/);
  });
});
