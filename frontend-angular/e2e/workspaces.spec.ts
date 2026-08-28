import { test, expect } from '@playwright/test';

// Creating a workspace leaves server state behind and there is no delete UI
// until unit 7, so anything created here is removed through the API in a hook —
// never at the end of the test body, where a mid-test failure would leak it.
let createdName: string | null = null;

test.describe('Workspaces', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev1@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+$/);
  });

  test.afterEach(async ({ page }) => {
    if (!createdName) return;
    const name = createdName;
    createdName = null;

    const token = await page.evaluate(() => localStorage.getItem('pj-authToken'));
    if (!token) return;
    const headers = { Authorization: `Bearer ${token}` };

    const res = await page.request.get('/api/workspaces', { headers });
    if (!res.ok()) return;
    for (const w of (await res.json()) as { id: string; name: string }[]) {
      if (w.name === name) await page.request.delete(`/api/workspaces/${w.id}`, { headers });
    }
  });

  test('the switcher reaches the list, which renders one card per workspace', async ({ page }) => {
    await page.locator('.ws-trigger').click();

    // Both actions previously sat inside the @for and rendered once per
    // workspace; the counts are the regression guard.
    await expect(page.getByRole('menuitem', { name: 'Manage workspaces' })).toHaveCount(1);
    await expect(page.getByRole('menuitem', { name: 'New workspace' })).toHaveCount(1);

    await page.getByRole('menuitem', { name: 'Manage workspaces' }).click();
    await expect(page).toHaveURL(/\/workspaces$/);
    await expect(page.getByRole('heading', { name: 'Workspaces' })).toBeVisible();

    // dev1 is seeded with a personal workspace plus Acme Team.
    expect(await page.locator('.ws-card').count()).toBeGreaterThan(1);
    await expect(page.locator('.ws-card.is-current')).toHaveCount(1);

    // The personal workspace shows the translated label, never the stored
    // English possessive the API keeps.
    await expect(page.locator('.ws-card').first()).toContainText('My Workspace');
    await expect(page.locator('.ws-grid')).not.toContainText("'s Workspace");
  });

  test('the new-workspace item opens the dialog and clears the query param', async ({ page }) => {
    await page.locator('.ws-trigger').click();
    await page.getByRole('menuitem', { name: 'New workspace' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();
    // afterNextRender strips ?new=1 with replaceUrl, so Back cannot reopen it.
    await expect(page).toHaveURL(/\/workspaces$/);
  });

  // The list is reachable without passing through a workspace, so it loads its
  // own workspaces and honours ?new=1 on a cold arrival, not only on a hand-off
  // from the switcher.
  test('a direct link to the list loads it and still opens the dialog', async ({ page }) => {
    await page.goto('/workspaces?new=1');

    await expect(page.locator('.ws-card').first()).toBeVisible();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page).toHaveURL(/\/workspaces$/);
  });

  test('creating a workspace adds it to the list and selects it', async ({ page }) => {
    createdName = `E2E ${Date.now()}`;

    await page.goto('/workspaces');
    // count() does not retry, and the page fetches its own list.
    await expect(page.locator('.ws-card').first()).toBeVisible();
    const before = await page.locator('.ws-card').count();

    await page.getByRole('button', { name: 'New workspace' }).click();
    await page.locator('input[formcontrolname="name"]').fill(createdName);
    await page.getByRole('button', { name: 'Create' }).click();

    await expect(page.locator('.ws-card')).toHaveCount(before + 1);
    await expect(page.locator('.ws-grid')).toContainText(createdName);

    // Created workspaces become current without a reload: the POST result is
    // upserted into the same signal store the list renders from.
    await expect(page.locator('.ws-card.is-current')).toContainText(createdName);
  });
});
