import { test, expect, type Page } from '@playwright/test';

async function tokenFor(page: Page, email: string): Promise<string> {
  const login = await page.request.post('/api/auth/login', {
    data: { email, password: 'Password123!' },
  });
  expect(login.ok()).toBeTruthy();
  return (await login.json()).token;
}

// Seeds soft-deleted workspaces over the API as a standard user, because an
// administrator cannot create or delete a workspace — only administer one.
// A workspace holding projects cannot be deleted, so these are all empty.
async function seedDeletedWorkspaces(page: Page, names: string[]): Promise<string[]> {
  const headers = { Authorization: `Bearer ${await tokenFor(page, 'dev1@example.com')}` };
  const ids: string[] = [];

  for (const name of names) {
    const created = await page.request.post('/api/workspaces', { headers, data: { name } });
    expect(created.ok()).toBeTruthy();
    const { id } = await created.json();

    const deleted = await page.request.delete(`/api/workspaces/${id}`, { headers });
    expect(deleted.ok()).toBeTruthy();
    ids.push(id);
  }

  return ids;
}

/**
 * Required, not tidiness: a workspace left behind rejoins dev1's switcher list,
 * which workspace-switcher.spec asserts exactly.
 */
async function purge(page: Page, ids: string[]): Promise<void> {
  const admin = { Authorization: `Bearer ${await tokenFor(page, 'admin@example.com')}` };
  await page.request.post('/api/admin/workspaces/purge', { headers: admin, data: ids });
}

/** Only a member can delete a workspace, so this drops it to the trash first. */
async function deleteAndPurge(page: Page, ids: string[]): Promise<void> {
  const owner = { Authorization: `Bearer ${await tokenFor(page, 'dev1@example.com')}` };
  for (const id of ids) {
    await page.request.delete(`/api/workspaces/${id}`, { headers: owner });
  }
  await purge(page, ids);
}

test.describe('Admin Workspaces Trash', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('admin@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));
  });

  test('the trash link in the admin nav reaches the page', async ({ page }) => {
    await page.goto('/admin');
    await page.locator('.admin-sidenav a', { hasText: 'Workspaces' }).click();

    await expect(page).toHaveURL(/\/admin\/trash\/workspaces$/);
    await expect(page.getByRole('heading', { name: 'Deleted Workspaces' })).toBeVisible();
  });

  test('lists deleted workspaces with their owner and type', async ({ page }) => {
    const runId = `list-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const ids = await seedDeletedWorkspaces(page, [`Trash ${runId} One`]);

    await page.goto('/admin/trash/workspaces');
    await page.getByPlaceholder('Search by name or owner...').fill(runId);

    const row = page.locator('tr', { hasText: `Trash ${runId} One` });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Shared');
    await expect(row).toContainText('Dev User1');

    await purge(page, ids);
  });

  test('restores a workspace, which drops it out of the trash', async ({ page }) => {
    const runId = `restore-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const name = `Trash ${runId} Restorable`;
    const ids = await seedDeletedWorkspaces(page, [name]);

    await page.goto('/admin/trash/workspaces');
    await page.getByPlaceholder('Search by name or owner...').fill(runId);

    const row = page.locator('tr', { hasText: name });
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Restore' }).click();

    await expect(page.getByText('1 workspace restored.')).toBeVisible();
    await expect(page.locator('tr', { hasText: name })).toHaveCount(0);

    await deleteAndPurge(page, ids);
  });

  test('selects every row on the page and purges them in bulk', async ({ page }) => {
    const runId = `purge-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const names = [`Trash ${runId} A`, `Trash ${runId} B`, `Trash ${runId} C`];
    await seedDeletedWorkspaces(page, names);

    await page.goto('/admin/trash/workspaces');
    // Narrowing to this run's rows is what makes "select all on this page" safe:
    // the trash holds every other user's deleted workspaces too.
    await page.getByPlaceholder('Search by name or owner...').fill(runId);
    await expect(page.locator('tr', { hasText: `Trash ${runId}` })).toHaveCount(3);

    await page
      .getByRole('table', { name: 'Deleted Workspaces' })
      .getByRole('checkbox', { name: 'Select all rows on this page' })
      .click();
    await expect(page.getByRole('button', { name: 'Purge Selected (3)' })).toBeVisible();

    await page.getByRole('button', { name: 'Purge Selected (3)' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('3 workspaces will be permanently purged')).toBeVisible();
    await dialog.getByRole('button', { name: 'Purge' }).click();

    await expect(page.getByText('3 workspaces permanently purged.')).toBeVisible();
    await expect(page.locator('tr', { hasText: `Trash ${runId}` })).toHaveCount(0);
  });

  test('a purge is gone for good — the workspace does not come back', async ({ page }) => {
    const runId = `gone-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const name = `Trash ${runId} Doomed`;
    await seedDeletedWorkspaces(page, [name]);

    await page.goto('/admin/trash/workspaces');
    await page.getByPlaceholder('Search by name or owner...').fill(runId);

    await page.locator('tr', { hasText: name }).getByRole('button', { name: 'Purge' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Purge' }).click();
    await expect(page.getByText('1 workspace permanently purged.')).toBeVisible();

    await page.reload();
    await page.getByPlaceholder('Search by name or owner...').fill(runId);
    await expect(page.locator('tr', { hasText: name })).toHaveCount(0);
  });
});
