import { test, expect, type Page } from '@playwright/test';

// /dashboard is still the one URL every "go home" path targets, but it renders
// nothing itself — a guard forwards it to the home of the current workspace.
const workspaceHome = /\/w\/[0-9a-f-]+$/;

async function login(page: Page): Promise<void> {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
}

test.describe('Workspace home', () => {
  test('login lands on the workspace home; landing and logo route there when signed in', async ({
    page,
  }) => {
    await login(page);

    // Post-login default destination is /dashboard, which forwards to a workspace.
    await page.waitForURL(workspaceHome);
    await expect(page.locator('.page-title')).toContainText('dev2');
    await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();

    // Visiting the marketing landing while signed in bounces home.
    await page.goto('/');
    await page.waitForURL(workspaceHome);

    // From elsewhere in the app, the brand logo returns home.
    await page.getByRole('button', { name: 'Trash' }).click();
    await expect(page).toHaveURL(/\/projects\/trash$/);
    await page.locator('a.brand').click();
    await page.waitForURL(workspaceHome);
  });

  // The old /dashboard bookmark has to keep working, and must not be where the user
  // ends up — that route has no page behind it.
  test('a bookmarked /dashboard forwards to a workspace', async ({ page }) => {
    await login(page);
    await page.waitForURL(workspaceHome);

    await page.goto('/dashboard');

    await page.waitForURL(workspaceHome);
    await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
  });
});
