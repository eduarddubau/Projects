import { test, expect, Page } from '@playwright/test';

// workspace-switcher.spec asserts the exact workspace lists of dev1 and dev2,
// so parallel files must not touch either account.
let createdName: string | null = null;

const OWNER = 'dev3@example.com';

async function login(page: Page, email: string) {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());

  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(email);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/\/dashboard/);
}

async function createWorkspace(page: Page, name: string) {
  await page.goto('/workspaces');
  await page.getByRole('button', { name: 'New workspace' }).click();
  await page.locator('input[formcontrolname="name"]').fill(name);
  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.locator('.ws-card.is-current')).toContainText(name);
}

async function openSettings(page: Page) {
  await page.locator('.ws-trigger').click();
  await page.getByRole('menuitem', { name: 'Workspace settings' }).click();
  await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/settings$/);
}

test.describe('Workspace settings', () => {
  test.afterEach(async ({ page }) => {
    if (!createdName) return;
    const name = createdName;
    createdName = null;

    const auth = await page.request.post('/api/auth/login', {
      data: { email: OWNER, password: 'Password123!' },
    });
    if (!auth.ok()) return;
    const headers = { Authorization: `Bearer ${((await auth.json()) as { token: string }).token}` };

    const res = await page.request.get('/api/workspaces', { headers });
    if (!res.ok()) return;
    for (const w of (await res.json()) as { id: string; name: string }[]) {
      if (w.name === name) await page.request.delete(`/api/workspaces/${w.id}`, { headers });
    }
  });

  test('renaming a workspace updates it everywhere that shows the name', async ({ page }) => {
    createdName = `E2E Settings ${Date.now()}`;
    await login(page, OWNER);
    await createWorkspace(page, createdName);
    await openSettings(page);

    const renamed = `${createdName} Renamed`;
    const save = page.getByRole('button', { name: 'Save changes' });
    await expect(save).toBeDisabled();

    await page.locator('input[formcontrolname="name"]').fill(renamed);
    await expect(save).toBeEnabled();
    await save.click();

    await expect(page.locator('.ws-trigger')).toContainText(renamed);
    await expect(save).toBeDisabled();
    createdName = renamed;
  });

  // The typed-name confirmation was relaxed to a plain warn confirm once deleting
  // became recoverable; see the trash spec for where the workspace goes.
  test('deleting asks once, then removes it from the list and the switcher', async ({ page }) => {
    createdName = `E2E Delete ${Date.now()}`;
    const name = createdName;
    await login(page, OWNER);
    await createWorkspace(page, name);
    await openSettings(page);

    await page.getByRole('button', { name: 'Delete workspace' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText(name);
    await dialog.getByRole('button', { name: 'Delete workspace' }).click();

    await expect(page).toHaveURL(/\/workspaces$/);
    await expect(page.locator('.ws-grid')).not.toContainText(name);
    await expect(page.locator('.ws-trigger')).not.toContainText(name);
    createdName = null;
  });

  test('offers no settings entry for a personal workspace', async ({ page }) => {
    await login(page, OWNER);

    await page.locator('.ws-trigger').click();
    await expect(page.getByRole('menuitem', { name: 'Members' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: 'Workspace settings' })).toBeHidden();
  });

  test('explains itself when the page is opened directly anyway', async ({ page }) => {
    await login(page, OWNER);
    // The guard resolves an unknown id to the personal workspace.
    await page.goto('/w/00000000-0000-0000-0000-000000000000/settings');

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/settings$/);
    await expect(page.locator('.page-container')).toContainText(
      "Your personal workspace can't be renamed or deleted.",
    );
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeHidden();
  });

  // Regression: the URL and the header used to be able to disagree.
  test('switching workspace carries the settings page over to it', async ({ page }) => {
    createdName = `E2E Switch ${Date.now()}`;
    await login(page, OWNER);
    await createWorkspace(page, createdName);
    await openSettings(page);

    const before = page.url();

    await page.locator('.ws-trigger').click();
    await page.getByRole('menuitem', { name: 'My Workspace' }).click();

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/settings$/);
    expect(page.url()).not.toBe(before);
    await expect(page.locator('.page-container')).toContainText(
      "Your personal workspace can't be renamed or deleted.",
    );
  });
});
