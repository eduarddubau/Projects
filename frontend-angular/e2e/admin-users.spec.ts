import { test, expect } from '@playwright/test';

test.describe('Admin Users', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev1@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.goto('/admin/users');
    await expect(page.getByRole('heading', { name: 'Active Users' })).toBeVisible();
  });

  test('lists active users and prevents deleting your own account', async ({ page }) => {
    await expect(page.locator('table[aria-label="Users"]')).toBeVisible();

    const selfRow = page.locator('tr', { hasText: 'dev1@example.com' });
    await expect(selfRow.getByText('You')).toBeVisible();
    await expect(selfRow.getByRole('button', { name: 'Delete' })).toBeDisabled();
  });

  test('search narrows the list by name or email', async ({ page }) => {
    await expect(page.locator('tr', { hasText: 'dev2@example.com' })).toBeVisible();

    await page.getByPlaceholder('Search by name or email...').fill('dev2');
    await expect(page.locator('tr', { hasText: 'dev2@example.com' })).toBeVisible();
    await expect(page.locator('tr', { hasText: 'dev3@example.com' })).toHaveCount(0);
  });

  test('deletes a user to trash and restores it', async ({ page }) => {
    const targetRow = page.locator('tr', { hasText: 'dev3@example.com' });
    await targetRow.getByRole('button', { name: 'Delete' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('will be moved to trash')).toBeVisible();
    await dialog.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('deleted.')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'dev3@example.com' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Trash' }).click();
    await expect(page.getByRole('heading', { name: 'Users Trash' })).toBeVisible();

    const trashRow = page.locator('tr', { hasText: 'dev3@example.com' });
    await expect(trashRow).toBeVisible();
    await trashRow.getByRole('button', { name: 'Restore' }).click();

    await expect(page.getByText('restored.')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'dev3@example.com' })).toHaveCount(0);
  });
});
