import { test, expect } from '@playwright/test';

// dev3's seeded projects are not touched by other specs, so the recent list
// and navigation targets here are stable under parallel runs.
test.describe('User dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev3@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  });

  test('shows stat tiles and the account card', async ({ page }) => {
    await expect(page.locator('.stat-card', { hasText: 'Active Projects' })).toBeVisible();
    await expect(page.locator('.stat-card', { hasText: 'In Trash' })).toBeVisible();
    await expect(page.locator('.stat-card', { hasText: 'Last activity' })).toBeVisible();

    const accountCard = page.locator('.account-card');
    await expect(accountCard.getByText('dev3@example.com')).toBeVisible();
    await expect(accountCard.getByText('Member')).toBeVisible();
  });

  test('opens a recent project from the list', async ({ page }) => {
    const row = page.locator('tr', { hasText: 'Ongoing Research Project no 3' });
    await expect(row).toBeVisible();
    await row.click();

    await page.waitForURL(/\/projects\/[0-9a-f-]+$/);
    // The detail page renders the name in mat-card-title, which has no heading role.
    await expect(page.locator('mat-card-title')).toHaveText('Ongoing Research Project no 3');
  });

  test('links to the profile page', async ({ page }) => {
    await page.getByRole('link', { name: 'View profile' }).click();

    await expect(page.getByRole('heading', { name: 'My Profile' })).toBeVisible();
  });
});
