import { test, expect } from '@playwright/test';

// dev3's own projects are not touched by other specs. Everything on this page is
// scoped to one workspace now, so unlike the old cross-workspace dashboard, the rows
// here are dev3's personal workspace alone.
test.describe('Workspace home', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev3@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/w\/[0-9a-f-]+$/);

    // Wait on a tile rather than the greeting: the tiles only render once the counts
    // resolve, and the greeting's name is dev3's display name, which profile mutates.
    await expect(page.locator('.kpi').first()).toBeVisible();
  });

  test('shows the personalized greeting and the two task tiles', async ({ page }) => {
    // The name in the greeting is itself the profile link. Asserting the link, not its
    // text, keeps this independent of the profile spec.
    await expect(page.locator('.page-title .name-link')).toHaveAttribute('href', '/profile');

    await expect(page.locator('.kpi', { hasText: 'Open tasks' })).toBeVisible();
    await expect(page.locator('.kpi', { hasText: 'Assigned to me' })).toBeVisible();
    await expect(page.locator('.kpi')).toHaveCount(2);

    // Both tiles now count work, not containers: "In Trash" is an action rather than
    // a metric, and a project count only repeats the table below it.
    await expect(page.locator('.kpi', { hasText: 'In Trash' })).toHaveCount(0);
    await expect(page.locator('.kpi', { hasText: 'Active projects' })).toHaveCount(0);
  });

  // The switcher governs the whole page, so the page has to say which workspace it is
  // showing. Personal workspaces render the translated label, not their derived name.
  test('names the workspace everything on it belongs to', async ({ page }) => {
    await expect(page.locator('.workspace-scope')).toContainText('My Workspace');
  });

  test('lists the workspace projects and opens one', async ({ page }) => {
    const row = page.locator('tbody tr').first();
    await expect(row).toBeVisible();
    const name = (await row.locator('td').nth(1).innerText()).trim();
    await row.click();

    await page.waitForURL(/\/projects\/[0-9a-f-]+$/);
    await expect(page.getByRole('heading', { name, level: 1 })).toBeVisible();
  });

  test('links to the profile page', async ({ page }) => {
    await page.locator('.page-title .name-link').click();

    await expect(page.getByRole('heading', { name: 'My Profile' })).toBeVisible();
  });

  // The button used to deep-link to a separate projects page; the table is on this
  // page now, so it opens the dialog in place.
  test('New Project opens the create dialog without leaving the page', async ({ page }) => {
    const url = page.url();
    await page.getByRole('button', { name: 'New Project' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel('Name')).toBeVisible();
    expect(page.url()).toBe(url);
  });

  // Every other table in the app filtered; this one's search box was never wired.
  test('filters the project list from the search box', async ({ page }) => {
    const rows = page.locator('tbody tr');
    const before = await rows.count();
    expect(before).toBeGreaterThan(0);

    await page.getByLabel('Search projects').fill('zzz-no-such-project');

    await expect(page.getByText('No projects found matching your search.')).toBeVisible();
    await expect(rows).toHaveCount(0);
  });
});
