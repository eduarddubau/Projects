import { test, expect } from '@playwright/test';

test.describe('My Projects CRUD', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    await page.goto('/projects');
    await expect(page.getByRole('heading', { name: 'My Projects' })).toBeVisible();
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

    // Edit happens on the detail page, opened by clicking the row.
    await row.click();
    await page.getByRole('button', { name: 'Edit' }).click();
    await page.getByLabel('Name').fill(updatedName);
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Project updated.')).toBeVisible();

    // Delete from the detail page; the app returns to the list afterwards.
    await page.getByRole('button', { name: 'Delete' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('Project deleted.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'My Projects' })).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toHaveCount(0);

    await page.getByRole('button', { name: 'Dev dev2@example.com' }).click();
    await page.getByRole('menuitem', { name: 'Trash' }).click();
    await expect(page.getByRole('heading', { name: 'Trash' })).toBeVisible();
    const trashRow = page.locator('tr', { hasText: updatedName });
    await expect(trashRow).toBeVisible();

    await trashRow.getByRole('button', { name: 'Restore' }).click();
    await expect(page.getByText('Project restored.')).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toHaveCount(0);

    await page.getByRole('button', { name: 'Back to My Projects' }).click();
    await expect(page.getByRole('heading', { name: 'My Projects' })).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toBeVisible();
  });
});
