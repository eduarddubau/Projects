import { test, expect, type Page } from '@playwright/test';

async function login(page: Page) {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'));
}

/**
 * dev2 is a plain **member** of the seeded "Acme Team" workspace — dev1 owns it. That is
 * the whole point of this file: task deletion stays open to members because it is
 * recoverable by the same member, rather than being taken away from them.
 */
async function openSeededProjectAsMember(page: Page) {
  await login(page);

  await page.locator('.ws-trigger').click();
  await page.locator('.ws-item', { hasText: 'Acme Team' }).click();

  await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
  await page.locator('tr', { hasText: 'Acme Website Redesign' }).click();
  await expect(page.getByRole('heading', { name: 'Acme Website Redesign' })).toBeVisible();
}

/** Deletes through the task editor, which is where the action lives. */
async function deleteTask(page: Page, title: string) {
  await page.getByLabel('Search tasks').fill(title);
  await page.locator('tr', { hasText: title }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
  await page
    .getByRole('dialog')
    .filter({ hasText: 'Delete task?' })
    .getByRole('button', { name: 'Delete' })
    .click();
  await expect(page.getByText('Task deleted.')).toBeVisible();
}

test.describe('Task trash', () => {
  test('a member deletes a task and gets it back from Recently deleted', async ({ page }) => {
    await openSeededProjectAsMember(page);
    const title = `E2E Trash ${Date.now()}`;

    await page.getByRole('button', { name: 'New Task' }).click();
    await page.getByRole('dialog').getByLabel('Title').fill(title);
    await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Task created.')).toBeVisible();

    // The list view searches; the board would need scrolling past every seeded card.
    await page.getByRole('radio', { name: 'List' }).click();
    await deleteTask(page, title);
    await expect(page.locator('tr', { hasText: title })).toHaveCount(0);

    await page.getByRole('button', { name: 'Recently deleted' }).click();
    const dialog = page.getByRole('dialog');
    const row = dialog.locator('.trash-row', { hasText: title });
    await expect(row).toBeVisible();

    await row.getByRole('button', { name: `Restore ${title}` }).click();
    await expect(page.getByText('Task restored.')).toBeVisible();
    await expect(dialog.locator('.trash-row', { hasText: title })).toHaveCount(0);

    await dialog.getByRole('button', { name: 'Close' }).click();

    // Back on the board it left, not merely absent from the trash.
    await page.getByLabel('Search tasks').fill(title);
    await expect(page.locator('tr', { hasText: title })).toBeVisible();

    // Leave nothing live behind; the soft-deleted row ages out of the retention window.
    await deleteTask(page, title);
  });

  // In dev2's own workspace, in a project this test creates and removes, so the assertion
  // cannot be broken by residue another spec left in the shared one.
  test('the trash says so when a project has lost nothing', async ({ page }) => {
    await login(page);
    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();

    const name = `E2E Empty Trash ${Date.now()}`;
    await page.getByRole('button', { name: 'New Project' }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(name);
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Project created.')).toBeVisible();

    await page.locator('tr', { hasText: name }).click();
    await page.getByRole('button', { name: 'Recently deleted' }).click();

    dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Nothing has been deleted here recently.')).toBeVisible();
    await dialog.getByRole('button', { name: 'Close' }).click();

    // dev2 owns its personal workspace, so it can clear up the project it just made.
    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Project deleted.')).toBeVisible();
  });
});
