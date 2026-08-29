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

/**
 * Deletes through the task editor, which is where the action lives. No confirmation step:
 * the snackbar's Undo replaced it, and the trash below is the backstop behind that.
 */
async function deleteTask(page: Page, title: string) {
  await page.getByLabel('Search tasks').fill(title);
  await page.locator('tr', { hasText: title }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByText('Task deleted.')).toBeVisible();
}

async function createTask(page: Page, title: string) {
  await page.getByRole('button', { name: 'New Task' }).click();
  await page.getByRole('dialog').getByLabel('Title').fill(title);
  await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click();
  await expect(page.getByText('Task created.')).toBeVisible();
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

  // The fast path, and the reason the confirmation dialog could go: the way back is on the
  // snackbar, at the moment the mistake is still in mind.
  test('undoes a delete straight from the snackbar', async ({ page }) => {
    await openSeededProjectAsMember(page);
    const title = `E2E Undo ${Date.now()}`;

    await createTask(page, title);
    await page.getByRole('radio', { name: 'List' }).click();

    await page.getByLabel('Search tasks').fill(title);
    await page.locator('tr', { hasText: title }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();

    // Gone from the list, with no confirmation asked anywhere along the way.
    await expect(page.getByText('Task deleted.')).toBeVisible();
    await expect(page.locator('tr', { hasText: title })).toHaveCount(0);

    await page.getByRole('button', { name: 'Undo' }).click();
    await expect(page.locator('tr', { hasText: title })).toBeVisible();

    // Leave nothing live behind; the soft-deleted row ages out of the retention window.
    await deleteTask(page, title);
  });

  // The backstop, for anyone who let the snackbar go. Reachable by a member, which is the
  // whole permission argument: deleting a task is member-open, so recovery has to be too.
  test('a member restores from the workspace trash, which names the project', async ({ page }) => {
    await openSeededProjectAsMember(page);
    const title = `E2E WsTrash ${Date.now()}`;

    await createTask(page, title);
    await page.getByRole('radio', { name: 'List' }).click();
    await deleteTask(page, title);

    await page.locator('.ws-nav a', { hasText: 'Trash' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/trash\/tasks$/);

    await page.getByLabel('Search deleted tasks').fill(title);
    const row = page.locator('tr', { hasText: title });
    await expect(row).toBeVisible();
    // Listed away from its board, so it has to say where it came from.
    await expect(row).toContainText('Acme Website Redesign');

    // No actions column: the row opens the whole record, and Restore is in there.
    await row.click();
    const record = page.getByRole('dialog');
    await expect(record.getByRole('heading', { name: title })).toBeVisible();
    await expect(record).toContainText('Acme Website Redesign');
    // Read-only — the trash offers no way to edit what it is holding.
    await expect(record.getByRole('button', { name: 'Save' })).toHaveCount(0);

    await record.getByRole('button', { name: 'Restore' }).click();
    await expect(page.getByText('Task restored.')).toBeVisible();
    await expect(page.locator('tr', { hasText: title })).toHaveCount(0);

    // Leave nothing live behind, as the sibling tests do: a restored card would sit in the
    // shared seeded project forever, while a soft-deleted one ages out of the window.
    await page.goBack();
    await page.getByRole('radio', { name: 'List' }).click();
    await deleteTask(page, title);
  });
});
