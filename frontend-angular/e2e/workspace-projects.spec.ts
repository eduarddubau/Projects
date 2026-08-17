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

  await page.locator('.nav-inline a', { hasText: 'Home' }).click();

  // The row, not the URL: switching workspace already navigated to the new home, so a
  // /w/{any-id} match is satisfied by the workspace being left as much as the one being
  // entered and pins nothing. An Acme project appearing is the real evidence.
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

    // Inside the menu: delete moved there, so a page-level button query would
    // now pass whether or not a member is offered the action.
    await page.getByRole('button', { name: 'Project actions' }).click();
    await expect(page.getByRole('menuitem', { name: 'Delete' })).toHaveCount(0);
    await expect(page.getByRole('menuitem', { name: 'Move to workspace' })).toHaveCount(0);
    await page.keyboard.press('Escape');

    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Edit' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Description').fill(`Edited by dev2 at ${Date.now()}`);
    await dialog.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('Project updated.')).toBeVisible();
  });

  test('an owner moves a project between workspaces and the URL follows', async ({ page }) => {
    await login(page, 'dev1@example.com');

    // A project of its own, so a rerun never fights the seeded ones over a name.
    const name = `Movable ${Date.now()}`;
    await page.locator('.nav-inline a', { hasText: 'Home' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+$/);
    const personalId = new URL(page.url()).pathname.split('/')[2];

    await page.getByRole('button', { name: 'New Project' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(name);
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Project created.')).toBeVisible();

    await page.locator('tr', { hasText: name }).click();
    await expect(page).toHaveURL(new RegExp(`/w/${personalId}/projects/[0-9a-f-]+$`));

    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Move to workspace' }).click();
    await page.getByRole('menuitem', { name: 'Acme Team' }).click();

    await expect(page.getByText('Project moved to Acme Team.')).toBeVisible();
    await expect(page).not.toHaveURL(new RegExp(`/w/${personalId}/`));
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects\/[0-9a-f-]+$/);

    // Clean up, or every rerun leaves another project in Acme Team.
    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Project deleted.')).toBeVisible();
  });

  test('the owner of the workspace is offered delete', async ({ page }) => {
    await login(page, 'dev1@example.com');
    await openAcmeProjects(page);

    const row = page.locator('tr', { hasText: 'Acme Q3 Roadmap' });
    await expect(row.getByRole('button', { name: 'Delete' })).toBeVisible();
  });
});
