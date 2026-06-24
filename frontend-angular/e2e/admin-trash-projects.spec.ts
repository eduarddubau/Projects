import { test, expect } from '@playwright/test';

test.describe('Admin Projects Trash', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev1@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.goto('/admin/trash/projects');
    await expect(page.getByRole('heading', { name: 'Projects Trash' })).toBeVisible();
  });

  test('loads with both restore and purge actions available', async ({ page }) => {
    await expect(page.locator('table[aria-label="Deleted Projects"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Restore' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Purge', exact: true }).first()).toBeVisible();
  });

  test('age filter narrows to only items older than the selected threshold', async ({ page }) => {
    await expect(page.locator('tr', { hasText: 'Deleted 5 Days Ago' }).first()).toBeVisible();

    await page.getByRole('radio', { name: '>30 days' }).click();

    await expect(page.locator('tr', { hasText: 'Deleted 5 Days Ago' })).toHaveCount(0);
    await expect(page.locator('tr', { hasText: 'Deleted 35 Days Ago' }).first()).toBeVisible();
  });

  test('selects all purgeable rows and purges them in bulk', async ({ page }) => {
    await page.getByRole('radio', { name: '>90 days' }).click();

    const purgeableRows = page.locator('tr', { hasText: 'Deleted 95 Days Ago' });
    const purgeableCount = await purgeableRows.count();
    expect(purgeableCount).toBeGreaterThan(0);

    await page.getByRole('table', { name: 'Deleted Projects' }).getByRole('checkbox', { name: 'Select all rows on this page' }).click();
    await expect(page.getByRole('button', { name: `Purge Selected (${purgeableCount})` })).toBeVisible();

    await page.getByRole('button', { name: `Purge Selected (${purgeableCount})` }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText(`${purgeableCount} projects will be permanently purged`)).toBeVisible();
    await dialog.getByRole('button', { name: 'Purge' }).click();

    await expect(page.getByText(`${purgeableCount} projects permanently purged.`)).toBeVisible();
    await expect(page.locator('tr', { hasText: 'Deleted 95 Days Ago' })).toHaveCount(0);
  });
});
