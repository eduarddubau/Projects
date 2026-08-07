import { test, expect } from '@playwright/test';

// Uses dev3, whose display name the user-dashboard spec also reads. The edit
// test below therefore restores through the API in afterEach rather than through
// the UI at the end of the test: a mid-test failure would otherwise leave the
// account renamed for every later run, which is exactly what happened once.
test.describe('My Profile', () => {
  test.afterEach(async ({ page }) => {
    const token = await page.evaluate(() => localStorage.getItem('pj-authToken'));
    if (!token) return;
    await page.request.put('/api/profile', {
      headers: { Authorization: `Bearer ${token}` },
      data: { firstName: 'Dev', lastName: 'User3', email: 'dev3@example.com', nickname: 'dev3' },
    });
  });

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

  test('edits the profile and the header picks it up', async ({ page }) => {
    const newNickname = `E2e${Date.now()}`;

    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('First Name').fill('Renamed');
    await page.getByLabel('Nickname').fill(newNickname);
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page.getByText('Profile updated.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Renamed User3' })).toBeVisible();

    // The header renders the nickname in preference to the first name, so the
    // nickname is what proves the post-save token refresh reached it. Editing the
    // first name alone cannot: the header would never have shown it.
    await expect(
      page.getByRole('button', { name: `${newNickname} dev3@example.com` }),
    ).toBeVisible();
  });

  test('rejects an empty first name', async ({ page }) => {
    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('First Name').fill('');
    await page.getByLabel('Last Name').click();

    await expect(page.getByText('First name is required')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeDisabled();
  });
});
