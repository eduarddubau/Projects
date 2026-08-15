import { test, expect, type Page } from '@playwright/test';

// dev1 owns Acme Team; dev2 and dev3 join as members. That split is what these
// specs exercise: shared visibility for everyone, removal for owners only.
async function login(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(email);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'));
}

/** Returns the workspace id the list is showing, so callers can pin it. */
async function openAcmeProjects(page: Page): Promise<string> {
  await page.locator('.ws-trigger').click();
  await page.locator('.ws-item', { hasText: 'Acme Team' }).click();

  // Wait for the switch to land before clicking: the nav link is built from the
  // selected id, and clicking it too early navigates to the previous workspace.
  await expect(page.locator('.ws-trigger')).toContainText('Acme Team');

  await page.locator('.nav-inline a', { hasText: 'Projects' }).click();

  // The URL first. The dashboard's recent list spans workspaces, so it also holds
  // an "Acme Website Redesign" row — asserting on the row alone is satisfied
  // before the navigation has happened at all.
  await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects$/);
  await expect(page.locator('tr', { hasText: 'Acme Website Redesign' })).toBeVisible();

  return new URL(page.url()).pathname.split('/')[2];
}

test.describe('Workspace-scoped projects', () => {
  test('a member sees the shared projects and the URL names the workspace', async ({ page }) => {
    await login(page, 'dev2@example.com');
    const workspaceId = await openAcmeProjects(page);

    await expect(page.locator('tr', { hasText: 'Acme Q3 Roadmap' })).toBeVisible();
    expect(workspaceId).toMatch(/^[0-9a-f-]+$/);
  });

  test('switching workspace changes which projects are listed', async ({ page }) => {
    await login(page, 'dev2@example.com');
    await openAcmeProjects(page);

    await page.locator('.ws-trigger').click();
    await page.locator('.ws-item', { hasText: 'My Workspace' }).click();

    await expect(page.locator('tr', { hasText: 'Acme Website Redesign' })).toHaveCount(0);
  });

  test('a member may edit a shared project but is offered no delete', async ({ page }) => {
    await login(page, 'dev2@example.com');
    const workspaceId = await openAcmeProjects(page);

    const row = page.locator('tr', { hasText: 'Acme Website Redesign' });
    await expect(row.getByRole('button', { name: 'Delete' })).toHaveCount(0);

    await row.click();

    // Pin the workspace across the navigation. A bare /projects/<id> match also
    // accepts a detail page that fell back to the personal workspace, where dev2
    // is an owner and the delete button legitimately appears.
    await expect(page).toHaveURL(new RegExp(`/w/${workspaceId}/projects/[0-9a-f-]+$`));
    await expect(page.getByRole('button', { name: 'Delete' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Edit' }).click();
    const description = page.getByLabel('Description');
    await description.fill(`Edited by dev2 at ${Date.now()}`);
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('Project updated.')).toBeVisible();
  });

  test('the owner of the workspace is offered delete', async ({ page }) => {
    await login(page, 'dev1@example.com');
    await openAcmeProjects(page);

    const row = page.locator('tr', { hasText: 'Acme Q3 Roadmap' });
    await expect(row.getByRole('button', { name: 'Delete' })).toBeVisible();
  });
});
