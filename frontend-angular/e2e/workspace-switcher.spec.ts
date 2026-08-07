import { test, expect, type Page } from '@playwright/test';

async function login(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(email);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'));
}

test.describe('Workspace switcher', () => {
  test('lists workspaces, persists a switch, and is cleared between accounts', async ({ page }) => {
    await login(page, 'dev1@example.com');

    const trigger = page.locator('.ws-trigger');
    await expect(trigger).toBeVisible();
    await expect(trigger).toContainText('My Workspace');

    await trigger.click();
    await expect(page.locator('.ws-item-name')).toHaveText(['My Workspace', 'Acme Team']);
    await expect(page.locator('.role-chip')).toHaveCount(1);

    // Clicking an item both selects and closes, so there is no second open here.
    await page.locator('.ws-item', { hasText: 'Acme Team' }).click();
    await expect(trigger).toContainText('Acme Team');
    expect(await page.evaluate(() => localStorage.getItem('pj-currentWorkspaceId'))).toBeTruthy();

    // The bug this checks: without AuthService.logout() calling clear(), the cached
    // list survives sign-out and dev2 sees dev1's stored selection.
    await page.getByRole('button', { name: 'dev1 dev1@example.com' }).click();
    await page.getByRole('menuitem', { name: 'Sign Out' }).click();
    await page.waitForURL((url) => url.pathname === '/');
    expect(await page.evaluate(() => localStorage.getItem('pj-currentWorkspaceId'))).toBeNull();

    await login(page, 'dev2@example.com');
    await expect(trigger).toContainText('My Workspace');
    await trigger.click();
    await expect(page.locator('.ws-item-name')).toHaveText(['My Workspace', 'Acme Team']);
  });
});
