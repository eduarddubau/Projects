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

    // Search to the self row so the assertion is independent of how many other
    // users exist / which page the row would otherwise land on.
    await page.getByPlaceholder('Search by name or email...').fill('dev1@example.com');
    const selfRow = page.locator('tr', { hasText: 'dev1@example.com' });
    await expect(selfRow.getByText('You')).toBeVisible();
    await expect(selfRow.getByRole('button', { name: 'Delete' })).toBeDisabled();
  });

  test('search narrows the list by name or email', async ({ page }) => {
    await page.getByPlaceholder('Search by name or email...').fill('dev2');
    await expect(page.locator('tr', { hasText: 'dev2@example.com' })).toBeVisible();
    await expect(page.locator('tr', { hasText: 'dev3@example.com' })).toHaveCount(0);
  });

  test('deletes a user to trash and restores it', async ({ page }) => {
    // Create a throwaway user so we never delete a seeded account other specs rely on.
    const token = await page.evaluate(() => localStorage.getItem('authToken'));
    const email = `delrestore-${Date.now()}@example.com`;
    const createResp = await page.request.post('/api/users', {
      headers: { Authorization: `Bearer ${token}` },
      data: { email, firstName: 'Del', lastName: 'Restore' }
    });
    expect(createResp.ok()).toBeTruthy();

    await page.reload();
    // Search to the new row so we don't depend on its page position as users accumulate.
    await page.getByPlaceholder('Search by name or email...').fill(email);
    const targetRow = page.locator('tr', { hasText: email });
    await expect(targetRow).toBeVisible();
    await targetRow.getByRole('button', { name: 'Delete' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('will be moved to trash')).toBeVisible();
    await dialog.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('deleted.')).toBeVisible();
    await expect(page.locator('tr', { hasText: email })).toHaveCount(0);

    await page.getByRole('button', { name: 'Trash' }).click();
    await expect(page.getByRole('heading', { name: 'Deleted Users' })).toBeVisible();

    await page.getByPlaceholder('Search by name or email...').fill(email);
    const trashRow = page.locator('tr', { hasText: email });
    await expect(trashRow).toBeVisible();
    await trashRow.getByRole('button', { name: 'Restore' }).click();

    await expect(page.getByText('restored.')).toBeVisible();
    await expect(page.locator('tr', { hasText: email })).toHaveCount(0);
  });

  test('permanently erases a deleted user (GDPR), removing them from trash for good', async ({ page }) => {
    // Create a throwaway user via the API so we never erase a seeded account other specs rely on.
    const token = await page.evaluate(() => localStorage.getItem('authToken'));
    const email = `erase-${Date.now()}@example.com`;
    const createResp = await page.request.post('/api/users', {
      headers: { Authorization: `Bearer ${token}` },
      data: { email, firstName: 'Erase', lastName: 'Target' }
    });
    expect(createResp.ok()).toBeTruthy();

    await page.reload();
    // Search to the new row so we don't depend on its page position as users accumulate.
    await page.getByPlaceholder('Search by name or email...').fill(email);
    const row = page.locator('tr', { hasText: email });
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('deleted.')).toBeVisible();

    await page.getByRole('button', { name: 'Trash' }).click();
    await expect(page.getByRole('heading', { name: 'Deleted Users' })).toBeVisible();

    await page.getByPlaceholder('Search by name or email...').fill(email);
    const trashRow = page.locator('tr', { hasText: email });
    await expect(trashRow).toBeVisible();
    await trashRow.getByRole('button', { name: 'Erase' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('permanently erased')).toBeVisible();
    await dialog.getByRole('button', { name: 'Erase' }).click();

    await expect(page.getByText('permanently erased.')).toBeVisible();
    await expect(page.locator('tr', { hasText: email })).toHaveCount(0);

    // Erasure is permanent: the user stays gone from the trash even after a reload.
    await page.reload();
    await expect(page.locator('tr', { hasText: email })).toHaveCount(0);
  });
});
