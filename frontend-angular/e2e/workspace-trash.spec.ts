import { test, expect, type Page } from '@playwright/test';

// workspace-switcher.spec asserts the exact workspace lists of dev1 and dev2, so
// this file uses dev3 and cleans up everything it creates.
const OWNER = 'dev3@example.com';

async function login(page: Page) {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());

  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(OWNER);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/\/w\/[0-9a-f-]+$/);
}

async function createWorkspace(page: Page, name: string) {
  await page.goto('/workspaces');
  await page.getByRole('button', { name: 'New workspace' }).click();
  await page.locator('input[formcontrolname="name"]').fill(name);
  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.locator('.ws-card.is-current')).toContainText(name);
}

async function deleteCurrentWorkspace(page: Page, name: string) {
  await page.locator('.ws-trigger').click();
  await page.getByRole('menuitem', { name: 'Workspace settings' }).click();
  await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/settings$/);

  await page.getByRole('button', { name: 'Delete workspace' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(name);
  await dialog.getByRole('textbox').fill(name);
  await dialog.getByRole('button', { name: 'Delete workspace' }).click();
  await expect(page).toHaveURL(/\/workspaces$/);
}

/** Purges over the API so a rerun never accumulates workspaces. */
async function purge(page: Page, names: string[]) {
  const owner = await page.request.post('/api/auth/login', {
    data: { email: OWNER, password: 'Password123!' },
  });
  const ownerHeaders = { Authorization: `Bearer ${(await owner.json()).token}` };

  const live = await page.request.get('/api/workspaces', { headers: ownerHeaders });
  for (const w of await live.json()) {
    if (names.includes(w.name)) {
      await page.request.delete(`/api/workspaces/${w.id}`, { headers: ownerHeaders });
    }
  }

  const admin = await page.request.post('/api/auth/login', {
    data: { email: 'admin@example.com', password: 'Password123!' },
  });
  const adminHeaders = { Authorization: `Bearer ${(await admin.json()).token}` };
  const trash = await page.request.get('/api/admin/workspaces/trash', { headers: adminHeaders });
  const ids = (await trash.json()).filter((w: { name: string }) => names.includes(w.name));

  if (ids.length > 0) {
    await page.request.post('/api/admin/workspaces/purge', {
      headers: adminHeaders,
      data: ids.map((w: { id: string }) => w.id),
    });
  }
}

test.describe('Workspace trash', () => {
  test('a deleted workspace lands in the trash and restores from there', async ({ page }) => {
    const name = `E2E Trash ${Date.now()}`;
    await login(page);
    await createWorkspace(page, name);
    await deleteCurrentWorkspace(page, name);

    await page.goto('/workspaces/trash');
    const card = page.locator('.ws-card', { hasText: name });
    await expect(card).toBeVisible();

    await card.getByRole('button', { name: 'Restore' }).click();
    await expect(page.getByText(`"${name}" restored.`)).toBeVisible();
    await expect(page.locator('.ws-card', { hasText: name })).toHaveCount(0);

    // Back in the switcher without a reload, which is the point of upserting it.
    await expect(page.locator('.ws-trigger')).toBeVisible();
    await page.locator('.ws-trigger').click();
    await expect(page.locator('.ws-item-name', { hasText: name })).toBeVisible();

    await purge(page, [name]);
  });

  test('the undo action on the delete snackbar brings it straight back', async ({ page }) => {
    const name = `E2E Undo ${Date.now()}`;
    await login(page);
    await createWorkspace(page, name);
    await deleteCurrentWorkspace(page, name);

    await page.getByRole('button', { name: 'Undo' }).click();

    // Undo returns to the settings page of the workspace it restored.
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/settings$/);
    await expect(page.locator('.ws-trigger')).toContainText(name);

    await page.goto('/workspaces/trash');
    await expect(page.locator('.ws-card', { hasText: name })).toHaveCount(0);

    await purge(page, [name]);
  });

  test('the workspace list links to the trash and back', async ({ page }) => {
    await login(page);
    await page.goto('/workspaces');
    await page.getByRole('button', { name: 'Trash' }).click();

    await expect(page).toHaveURL(/\/workspaces\/trash$/);
    await expect(page.getByRole('heading', { name: 'Deleted Workspaces' })).toBeVisible();

    await page.getByRole('button', { name: 'All workspaces' }).click();
    await expect(page).toHaveURL(/\/workspaces$/);
  });
});
