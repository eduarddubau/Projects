import { test, expect } from '@playwright/test';

// dev3's own projects are not touched by other specs. The recent list is not
// dev3's alone though — it spans every workspace they belong to, Acme Team
// included — so nothing here may assume which projects land in it.
test.describe('User dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev3@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.getByRole('link', { name: 'Dashboard' }).click();
    await page.waitForURL(/\/dashboard$/);
    // Wait on a tile rather than the greeting: the tiles only render once the
    // dashboard request resolves, and the greeting's name is dev3's display name,
    // which the profile spec mutates.
    await expect(page.locator('.kpi').first()).toBeVisible();
  });

  test('shows the personalized greeting and kpi tiles', async ({ page }) => {
    // The name in the greeting is itself the profile link; the separate "View
    // profile" link and the role chip went with the dashboard refactor. Asserting
    // the link, not its text, keeps this independent of the profile spec.
    await expect(page.locator('.page-title .name-link')).toHaveAttribute('href', '/profile');

    // Two tiles, not three: "Last activity" is no longer surfaced as a tile.
    await expect(page.locator('.kpi', { hasText: 'Active Projects' })).toBeVisible();
    await expect(page.locator('.kpi', { hasText: 'In Trash' })).toBeVisible();
    await expect(page.locator('.kpi')).toHaveCount(2);
  });

  test('opens a recent project from the list', async ({ page }) => {
    // Whichever project is first, not a named one — see the note above the describe.
    const row = page.locator('tbody tr').first();
    await expect(row).toBeVisible();
    const name = (await row.locator('td').first().innerText()).trim();
    await row.click();

    await page.waitForURL(/\/projects\/[0-9a-f-]+$/);
    // The detail page renders the name in mat-card-title, which has no heading role.
    await expect(page.locator('mat-card-title')).toHaveText(name);
  });

  test('links to the profile page', async ({ page }) => {
    await page.locator('.page-title .name-link').click();

    await expect(page.getByRole('heading', { name: 'My Profile' })).toBeVisible();
  });

  test('New Project button opens the create dialog on the projects page', async ({ page }) => {
    await page.getByRole('link', { name: 'New Project' }).click();

    await expect(page).toHaveURL(/\/projects/);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel('Name')).toBeVisible();
  });
});
