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

  // A section carries over between workspaces; a project cannot. Switching used to swap
  // only the workspace id and stay on the URL, leaving Acme's project rendered under the
  // personal workspace — the API resolves a project by id and checks membership, not path.
  test('switching away from a project lands on the new workspace home', async ({ page }) => {
    await login(page, 'dev2@example.com');

    await page.locator('.ws-trigger').click();
    await page.locator('.ws-item', { hasText: 'Acme Team' }).click();
    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await page.locator('tr', { hasText: 'Acme Website Redesign' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/projects\/[0-9a-f-]{36}$/);

    await page.locator('.ws-trigger').click();
    await page.locator('.ws-item', { hasText: 'My Workspace' }).click();

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}$/);
    await expect(page.locator('.ws-trigger')).toContainText('My Workspace');
    await expect(page.getByRole('heading', { name: 'My tasks' })).toBeVisible();
  });

  // The counterpart: a section is meaningful in any workspace, so it survives the switch.
  test('switching from a section stays on that section', async ({ page }) => {
    await login(page, 'dev2@example.com');

    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/projects$/);

    await page.locator('.ws-trigger').click();
    await page.locator('.ws-item', { hasText: 'Acme Team' }).click();

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/projects$/);
    await expect(page.locator('tr', { hasText: 'Acme Website Redesign' })).toBeVisible();
  });
});
