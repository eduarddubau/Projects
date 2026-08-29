import { test, expect } from '@playwright/test';

test.describe('Projects CRUD', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
  });

  test('creates, edits, deletes, and restores a project via Trash', async ({ page }) => {
    const projectName = `E2E Project ${Date.now()}`;
    const updatedName = `${projectName} (updated)`;

    await page.getByRole('button', { name: 'New Project' }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(projectName);
    await dialog.getByLabel('Description').fill('Created by an E2E test.');
    await dialog.getByRole('button', { name: 'Create' }).click();

    await expect(page.getByText('Project created.')).toBeVisible();
    const row = page.locator('tr', { hasText: projectName });
    await expect(row).toBeVisible();

    // Edit happens on the detail page, through the header's actions menu.
    await row.click();
    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Edit' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(updatedName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Project updated.')).toBeVisible();

    // Delete from the same menu; the app returns to the list afterwards.
    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('Project deleted.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toHaveCount(0);

    await page.getByRole('button', { name: 'Trash' }).click();
    await expect(page.getByRole('heading', { name: 'Trash' })).toBeVisible();
    const trashRow = page.locator('tr', { hasText: updatedName });
    await expect(trashRow).toBeVisible();

    // Restoring lives on the project's own page now, so the trash row opens it.
    await trashRow.click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/projects\/[0-9a-f-]{36}$/);
    await expect(page.getByText('This project is in the trash.')).toBeVisible();
    // The board is not rendered for a trashed project.
    await expect(page.getByRole('button', { name: 'New Task' })).toHaveCount(0);
    // The whole record stands in for it, deletion metadata included.
    const fields = page.locator('.project-fields');
    await expect(fields).toContainText('Created by an E2E test.');
    await expect(fields.locator('dt', { hasText: 'Deleted At' })).toBeVisible();
    await expect(fields.locator('dt', { hasText: 'Deleted by' })).toBeVisible();
    await expect(fields.locator('dt', { hasText: 'Workspace' })).toBeVisible();

    await page.getByRole('button', { name: 'Restore' }).first().click();
    await expect(page.getByText('Project restored.')).toBeVisible();
    // The page stays put and turns back into a normal project.
    await expect(page.getByText('This project is in the trash.')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'New Task' })).toBeVisible();

    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toBeVisible();
  });

  // The trash stopped being owner-only when the task trash moved into it: a member deletes
  // tasks and has to be able to get them back. Only the Projects tab is still owner-gated.
  // dev2 owns its personal workspace but is a plain member of Acme Team.
  test('gives a plain member the task trash but not the projects one', async ({ page }) => {
    await page.locator('.ws-trigger').click();
    await page.locator('.ws-item', { hasText: 'Acme Team' }).click();
    await expect(page.locator('.ws-trigger')).toContainText('Acme Team');

    await page.locator('.ws-nav a', { hasText: 'Trash' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/trash\/tasks$/);
    await expect(page.locator('.trash-tabs a', { hasText: 'Projects' })).toHaveCount(0);

    // Hiding the tab is presentation, not authorization, so the URL is refused too. The
    // pattern is anchored so it cannot pass against the /trash URL the goto starts from.
    const workspace = /\/w\/[0-9a-f-]{36}/.exec(page.url())![0];
    await page.goto(`${workspace}/trash/projects`);
    await expect(page).toHaveURL(new RegExp(`${workspace}$`));
  });
});
