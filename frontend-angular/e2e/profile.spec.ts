import { test, expect } from '@playwright/test';

// Uses dev3: other specs reference that account by email only, so the
// temporary rename in this spec cannot break them.
test.describe('My Profile', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev3@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.getByRole('button', { name: 'dev3@example.com' }).click();
    await page.getByRole('menuitem', { name: 'My Profile' }).click();
    await expect(page.getByRole('heading', { name: 'My Profile' })).toBeVisible();
  });

  test('shows the account details', async ({ page }) => {
    // The header shows the email too, so scope to the card.
    const card = page.locator('mat-card');
    await expect(card.getByText('dev3@example.com')).toBeVisible();
    await expect(card.getByText('Member since')).toBeVisible();
    await expect(card.getByText('Account ID')).toBeVisible();
  });

  test('edits the name and the header picks it up', async ({ page }) => {
    const newFirstName = `E2e${Date.now()}`;

    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('First Name').fill(newFirstName);
    await page.getByLabel('Last Name').fill('Renamed');
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page.getByText('Profile updated.')).toBeVisible();
    await expect(page.getByRole('heading', { name: `${newFirstName} Renamed` })).toBeVisible();

    // The post-save token refresh feeds the new name into the header.
    await expect(page.getByRole('button', { name: `${newFirstName} dev3@example.com` })).toBeVisible();

    // Restore the seeded name so repeated runs start from the same state.
    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('First Name').fill('Dev');
    await page.getByLabel('Last Name').fill('User3');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByRole('heading', { name: 'Dev User3' })).toBeVisible();
  });

  test('rejects an empty first name', async ({ page }) => {
    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('First Name').fill('');
    await page.getByLabel('Last Name').click();

    await expect(page.getByText('First name is required')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeDisabled();
  });
});
