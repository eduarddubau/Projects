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

  test('creates, edits, and deletes a project', async ({ page }) => {
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

    await row.getByRole('button', { name: 'Edit' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(updatedName);
    await dialog.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('Project updated.')).toBeVisible();
    const updatedRow = page.locator('tr', { hasText: updatedName });
    await expect(updatedRow).toBeVisible();

    await updatedRow.getByRole('button', { name: 'Delete' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('Project deleted.')).toBeVisible();
    await expect(page.locator('tr', { hasText: updatedName })).toHaveCount(0);
  });
});
