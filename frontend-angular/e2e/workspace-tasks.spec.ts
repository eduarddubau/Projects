import { test, expect, type Page } from '@playwright/test';

// The seeded Acme tasks drift as other specs run, so this file creates its own
// fixture through the API and removes it in a hook — never in the test body, where
// a mid-test failure would leak it into the shared seed.
const OWNER = 'dev1@example.com';

let createdTaskId: string | null = null;
let token = '';

async function login(page: Page) {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(OWNER);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/w\/[0-9a-f-]+$/);
  token = (await page.evaluate(() => localStorage.getItem('pj-authToken'))) ?? '';
}

// Built from the local calendar fields, never toISOString(): west of UTC that shifts the
// day forward and yields today, so the "overdue" fixture would not be overdue. This is the
// bug iso-date.ts exists to prevent, and it reaches e2e too.
function yesterday(): string {
  const date = new Date();
  date.setDate(date.getDate() - 1);
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

test.describe('Workspace tasks', () => {
  let overdueTitle: string;

  test.beforeEach(async ({ page }) => {
    await login(page);
    const headers = { Authorization: `Bearer ${token}` };

    // By name, not "the first one": the shared projects are seeded in one batch with
    // near-identical timestamps, so the CreatedAt-descending order between them is not
    // stable. DbSeeder carries the same warning for the same reason.
    const workspaces = await (await page.request.get('/api/workspaces', { headers })).json();
    const acme = workspaces.find((w: { name: string }) => w.name === 'Acme Team');
    const projects: { id: string; name: string }[] = await (
      await page.request.get(`/api/workspaces/${acme.id}/projects`, { headers })
    ).json();
    const project = projects.find((p) => p.name === 'Acme Website Redesign')!;

    overdueTitle = `E2E Overdue ${Date.now()}`;
    const created = await page.request.post(`/api/projects/${project.id}/tasks`, {
      headers,
      // Unassigned and already late, so it lands in two filters and not in a third.
      data: { title: overdueTitle, status: 'Todo', dueDate: yesterday() },
    });
    createdTaskId = (await created.json()).id;

    await page.locator('.ws-trigger').click();
    await page.getByRole('menuitem', { name: 'Acme Team' }).click();
    await expect(page.locator('.ws-trigger')).toContainText('Acme Team');
    await page.locator('.ws-nav a', { hasText: 'Tasks' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/tasks$/);
  });

  test.afterEach(async ({ page }) => {
    if (!createdTaskId) return;
    await page.request.delete(`/api/tasks/${createdTaskId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    createdTaskId = null;
  });

  test('opens on my own tasks, which do not include an unassigned one', async ({ page }) => {
    await expect(page.locator('.filter-bar [aria-checked="true"]')).toContainText('Assigned to me');
    await expect(page.locator('.task-row', { hasText: overdueTitle })).toHaveCount(0);
  });

  test('the unassigned filter finds work nobody has picked up', async ({ page }) => {
    await page.getByRole('radio', { name: 'Unassigned' }).click();

    await expect(page).toHaveURL(/\?filter=unassigned$/);
    await expect(page.locator('.task-row', { hasText: overdueTitle })).toBeVisible();
  });

  // Every row under this filter is late by definition, so every due date is marked.
  test('the overdue filter narrows the list and marks each row late', async ({ page }) => {
    await page.getByRole('radio', { name: 'Overdue' }).click();

    await expect(page).toHaveURL(/\?filter=overdue$/);
    const rows = page.locator('.task-row');
    await expect(rows.filter({ hasText: overdueTitle })).toBeVisible();
    expect(await page.locator('.due-overdue').count()).toBe(await rows.count());
    await expect(page.locator('.group-title')).toHaveText([/Overdue\s*\d+/]);
  });

  // The row is read away from its board, so it has to say where it came from and go back.
  test('a task row names its project and opens it', async ({ page }) => {
    await page.getByRole('radio', { name: 'Unassigned' }).click();
    const row = page.locator('.task-row', { hasText: overdueTitle });
    await expect(row.locator('.task-project')).not.toBeEmpty();

    await row.locator('.task-link').click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects\/[0-9a-f-]+$/);
  });

  test('a filtered list is a link that survives a reload', async ({ page }) => {
    await page.getByRole('radio', { name: 'Unassigned' }).click();
    await expect(page).toHaveURL(/\?filter=unassigned$/);

    await page.reload();

    await expect(page.locator('.filter-bar [aria-checked="true"]')).toContainText('Unassigned');
    await expect(page.locator('.task-row', { hasText: overdueTitle })).toBeVisible();
  });
});
