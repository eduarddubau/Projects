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
    await expect(page.locator('.page-eyebrow')).toContainText('My Workspace');
  });

  test('links to the profile page', async ({ page }) => {
    await page.locator('.page-title .name-link').click();

    await expect(page.getByRole('heading', { name: 'My Profile' })).toBeVisible();
  });

  // Home leads with your own work, not the workspace's containers.
  test('digests my tasks and the recently updated projects', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'My tasks' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Recent projects' })).toBeVisible();

    // The full table is one click away, not on this page.
    await expect(page.locator('tbody tr')).toHaveCount(0);
  });

  test('the recent projects shortcut opens the projects page', async ({ page }) => {
    await page
      .locator('.home-section', { hasText: 'Recent projects' })
      .getByRole('link', { name: 'View all' })
      .click();

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects$/);
    await expect(page.getByRole('button', { name: 'New Project' })).toBeVisible();
  });

  test('the my-tasks shortcut opens the tasks page', async ({ page }) => {
    await page
      .locator('.home-section', { hasText: 'My tasks' })
      .getByRole('link', { name: 'View all' })
      .click();

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/tasks$/);
    await expect(page.getByRole('heading', { name: 'Tasks', level: 1 })).toBeVisible();
  });
});
