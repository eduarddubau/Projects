import { test, expect } from '@playwright/test';

// dev3's own projects are not touched by other specs, so the rows here are that
// account's personal workspace alone. These tests moved off the workspace home when
// the projects table became a destination of its own.
test.describe('Projects page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev3@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/w\/[0-9a-f-]+$/);

    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects$/);
  });

  test('names the workspace its projects belong to', async ({ page }) => {
    await expect(page.locator('.page-eyebrow')).toContainText('My Workspace');
  });

  test('lists the workspace projects and opens one', async ({ page }) => {
    const row = page.locator('tbody tr').first();
    await expect(row).toBeVisible();
    const name = (await row.locator('td').nth(1).innerText()).trim();
    await row.click();

    await page.waitForURL(/\/projects\/[0-9a-f-]+$/);
    await expect(page.getByRole('heading', { name, level: 1 })).toBeVisible();
  });

  test('New Project opens the create dialog without leaving the page', async ({ page }) => {
    const url = page.url();
    await page.getByRole('button', { name: 'New Project' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel('Name')).toBeVisible();
    expect(page.url()).toBe(url);
  });

  // Every other table in the app filtered; this one's search box was once never wired.
  test('filters the project list from the search box', async ({ page }) => {
    const rows = page.locator('tbody tr');
    const before = await rows.count();
    expect(before).toBeGreaterThan(0);

    await page.getByLabel('Search projects').fill('zzz-no-such-project');

    await expect(page.getByText('No projects found matching your search.')).toBeVisible();
    await expect(rows).toHaveCount(0);
  });
});
