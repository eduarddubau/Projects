import { test, expect } from '@playwright/test';

/** dev1 owns the seeded "Acme Team" workspace, which holds the seeded task fixtures. */
async function openSeededProject(page: import('@playwright/test').Page) {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill('dev1@example.com');
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'));

  await page.locator('.ws-trigger').click();
  await page.locator('.ws-item', { hasText: 'Acme Team' }).click();

  await page.locator('.nav-inline a', { hasText: 'Projects' }).click();
  await page.locator('tr', { hasText: 'Acme Website Redesign' }).click();
  await expect(page.getByRole('heading', { name: 'Acme Website Redesign' })).toBeVisible();
}

test.describe('Tasks', () => {
  test('creates, edits and deletes a task', async ({ page }) => {
    await openSeededProject(page);
    const title = `E2E Task ${Date.now()}`;
    const updated = `${title} (updated)`;

    await page.getByRole('button', { name: 'New Task' }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill(title);
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Task created.')).toBeVisible();

    // Search rather than scan: the list pages at 10, and every rerun adds a task.
    await page.getByLabel('Search tasks').fill(title);
    const row = page.locator('tr', { hasText: title });
    await expect(row).toBeVisible();

    await row.getByRole('button', { name: 'Edit task' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill(updated);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Task updated.')).toBeVisible();
    await expect(page.locator('tr', { hasText: updated })).toBeVisible();

    await page
      .locator('tr', { hasText: updated })
      .getByRole('button', { name: 'Delete task' })
      .click();
    dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Task deleted.')).toBeVisible();
    await expect(page.locator('tr', { hasText: updated })).toHaveCount(0);
  });

  test('refuses a due date before the start date', async ({ page }) => {
    await openSeededProject(page);

    await page.getByRole('button', { name: 'New Task' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill('Backwards dates');
    await dialog.getByLabel('Start date').fill('8/25/2026');
    await dialog.getByLabel('Due date').fill('8/20/2026');

    await expect(dialog.getByText('The due date cannot be before the start date.')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Create' })).toBeDisabled();
  });

  test('filters the list to overdue tasks', async ({ page }) => {
    await openSeededProject(page);
    // Wait for rows before counting, or the filter is compared against an empty table.
    const rows = page.locator('table tbody tr');
    await expect(rows.first()).toBeVisible();
    const before = await rows.count();

    // The seed includes one overdue card and several that are not.
    await page.getByRole('button', { name: 'Overdue' }).click();
    await expect(rows.first()).toBeVisible();
    const after = await rows.count();

    expect(after).toBeGreaterThan(0);
    expect(after).toBeLessThan(before);
  });
});
