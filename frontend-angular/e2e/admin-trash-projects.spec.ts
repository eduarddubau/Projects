import { test, expect, type Page } from '@playwright/test';

// Seeds soft-deleted projects with backdated DeletedAt via the Development-only
// test-seed endpoint, so each test owns its fixtures and never depends on (or
// consumes) shared seed data.
//
// Seeded as a standard user, not the admin: fixtures land in the caller's own
// personal workspace, and an administrator has none to seed into.
async function seedDeletedProjects(
  page: Page,
  projects: { name: string; deletedDaysAgo: number }[],
): Promise<void> {
  const login = await page.request.post('/api/auth/login', {
    data: { email: 'dev1@example.com', password: 'Password123!' },
  });
  expect(login.ok()).toBeTruthy();
  const { token } = await login.json();

  const resp = await page.request.post('/api/test-seed/deleted-projects', {
    headers: { Authorization: `Bearer ${token}` },
    data: { projects },
  });
  expect(resp.ok()).toBeTruthy();
}

test.describe('Admin Projects Trash', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('admin@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.goto('/admin/trash/projects');
    await expect(page.getByRole('heading', { name: 'Deleted Projects' })).toBeVisible();
  });

  test('loads with both restore and purge actions available', async ({ page }) => {
    // A within-window row (restore only) and a past-window row (restore + purge).
    const runId = `load-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    await seedDeletedProjects(page, [
      { name: `Trash ${runId} Recent`, deletedDaysAgo: 5 },
      { name: `Trash ${runId} Ancient`, deletedDaysAgo: 95 },
    ]);
    await page.reload();
    await page.getByPlaceholder('Search by name...').fill(runId);

    await expect(page.locator('table[aria-label="Deleted Projects"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Restore' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Purge', exact: true }).first()).toBeVisible();
  });

  test('age filter narrows to only items older than the selected threshold', async ({ page }) => {
    const runId = `age-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const recent = `Trash ${runId} Recent 5d`;
    const stale = `Trash ${runId} Stale 35d`;
    await seedDeletedProjects(page, [
      { name: recent, deletedDaysAgo: 5 },
      { name: stale, deletedDaysAgo: 35 },
    ]);
    await page.reload();
    await page.getByPlaceholder('Search by name...').fill(runId);

    await expect(page.locator('tr', { hasText: recent })).toBeVisible();

    await page.getByRole('radio', { name: '>30 days' }).click();

    await expect(page.locator('tr', { hasText: recent })).toHaveCount(0);
    await expect(page.locator('tr', { hasText: stale })).toBeVisible();
  });

  test('selects all purgeable rows and purges them in bulk', async ({ page }) => {
    const runId = `purge-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
    const ancientNames = [
      `Trash ${runId} Ancient A`,
      `Trash ${runId} Ancient B`,
      `Trash ${runId} Ancient C`,
    ];
    await seedDeletedProjects(page, [
      // A within-window row that must NOT be purgeable, plus three past >90 days.
      { name: `Trash ${runId} Recent`, deletedDaysAgo: 5 },
      ...ancientNames.map((name) => ({ name, deletedDaysAgo: 95 })),
    ]);
    await page.reload();

    // Searching this run's id isolates the table to only its rows, so "select
    // all on this page" reaches exactly the purgeable rows we seeded.
    await page.getByPlaceholder('Search by name...').fill(runId);
    await page.getByRole('radio', { name: '>90 days' }).click();

    const purgeableRows = page.locator('tr', { hasText: `Trash ${runId} Ancient` });
    await expect(purgeableRows).toHaveCount(3);

    await page
      .getByRole('table', { name: 'Deleted Projects' })
      .getByRole('checkbox', { name: 'Select all rows on this page' })
      .click();
    await expect(page.getByRole('button', { name: 'Purge Selected (3)' })).toBeVisible();

    await page.getByRole('button', { name: 'Purge Selected (3)' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('3 projects will be permanently purged')).toBeVisible();
    await dialog.getByRole('button', { name: 'Purge' }).click();

    await expect(page.getByText('3 projects permanently purged.')).toBeVisible();
    await expect(page.locator('tr', { hasText: `Trash ${runId} Ancient` })).toHaveCount(0);
  });
});
