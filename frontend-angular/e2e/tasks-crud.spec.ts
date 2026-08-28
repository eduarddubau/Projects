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

  await page.locator('.ws-nav a', { hasText: 'Home' }).click();
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

    await page.getByRole('radio', { name: 'List' }).click();

    // Search rather than scan: the list pages at 10, and every rerun adds a task.
    await page.getByLabel('Search tasks').fill(title);
    const row = page.locator('tr', { hasText: title });
    await expect(row).toBeVisible();

    await row.click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill(updated);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Task updated.')).toBeVisible();
    await expect(page.locator('tr', { hasText: updated })).toBeVisible();

    // Deleting is inside the editor now, so it opens the form and asks from there.
    await page.locator('tr', { hasText: updated }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await page
      .getByRole('dialog')
      .filter({ hasText: 'Delete task?' })
      .getByRole('button', { name: 'Delete' })
      .click();
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
    await page.getByRole('radio', { name: 'List' }).click();
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

  test('opens on the board and the toggle round-trips through the URL', async ({ page }) => {
    await openSeededProject(page);

    // No ?view= in the URL: a bare project link is the board.
    expect(new URL(page.url()).searchParams.get('view')).toBeNull();
    await expect(page.getByRole('heading', { name: 'To do' })).toBeVisible();

    await page.getByRole('radio', { name: 'List' }).click();
    await expect(page).toHaveURL(/view=list/);
    await expect(page.getByRole('table', { name: 'Tasks' })).toBeVisible();

    await page.getByRole('radio', { name: 'Board' }).click();
    await expect(page).toHaveURL(/view=board/);
    await expect(page.getByRole('heading', { name: 'In progress' })).toBeVisible();
  });

  test('a ?view=list link opens directly on the list', async ({ page }) => {
    await openSeededProject(page);
    await page.goto(`${page.url().split('?')[0]}?view=list`);

    await expect(page.getByRole('table', { name: 'Tasks' })).toBeVisible();
  });

  // WCAG 2.2 SC 2.5.7 requires a no-drag path for everything dragging can do, and
  // Playwright's drag against the CDK is flaky, so the menu is what gets tested.
  test('moves a card between columns through the card menu', async ({ page }) => {
    await openSeededProject(page);
    const title = `E2E Board ${Date.now()}`;

    await page.getByRole('button', { name: 'New Task' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill(title);
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Task created.')).toBeVisible();

    const todo = page.locator('section', { hasText: 'To do' }).first();
    await expect(todo.locator('article', { hasText: title })).toBeVisible();

    await page.getByRole('button', { name: `Actions for ${title}` }).click();
    await page.getByRole('menuitem', { name: 'Move to' }).click();
    await page.getByRole('menuitem', { name: 'Done' }).click();

    const done = page.locator('section', { hasText: 'Done' }).first();
    await expect(done.locator('article', { hasText: title })).toBeVisible();

    // Survives a reload, so the move reached the server rather than only the signal.
    await page.reload();
    await expect(
      page.locator('section', { hasText: 'Done' }).first().locator('article', { hasText: title }),
    ).toBeVisible();

    await page.getByRole('button', { name: `Actions for ${title}` }).click();
    await page.getByRole('menuitem', { name: 'Delete task' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Task deleted.')).toBeVisible();
  });

  test('reorders within a column through the card menu', async ({ page }) => {
    await openSeededProject(page);

    const todo = page.locator('section', { hasText: 'To do' }).first();
    const titleAt = async (index: number) =>
      (await todo.locator('article .card-title').nth(index).innerText()).trim();

    const second = await titleAt(1);
    await page.getByRole('button', { name: `Actions for ${second}` }).click();
    await page.getByRole('menuitem', { name: 'Move up' }).click();

    await expect(todo.locator('article .card-title').first()).toHaveText(second);

    // Put it back so a rerun starts from the same board.
    await page.getByRole('button', { name: `Actions for ${second}` }).click();
    await page.getByRole('menuitem', { name: 'Move down' }).click();
    await expect(todo.locator('article .card-title').nth(1)).toHaveText(second);
  });

  test('moving a project unassigns anyone the target workspace does not hold', async ({ page }) => {
    await openSeededProject(page);

    // Its own project: the move takes every task in it out of Acme Team.
    const name = `Movable ${Date.now()}`;
    await page.locator('.ws-nav a', { hasText: 'Home' }).click();
    await page.getByRole('button', { name: 'New Project' }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByLabel('Name').fill(name);
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Project created.')).toBeVisible();

    await page.locator('tr', { hasText: name }).click();
    await expect(page.getByRole('heading', { name })).toBeVisible();

    await page.getByRole('button', { name: 'New Task' }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Title').fill('Theirs');
    await dialog.getByLabel('Assignee').click();
    await page.getByRole('option', { name: 'Dev User2' }).click();
    await dialog.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText('Task created.')).toBeVisible();

    const card = page.locator('article', { hasText: 'Theirs' });
    await expect(card.getByLabel('Dev User2')).toBeVisible();

    // dev2 is in Acme Team but not in dev1's personal workspace.
    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Move to workspace' }).click();
    await page.getByRole('menuitem', { name: 'My Workspace' }).click();

    await expect(page.getByText('1 task was unassigned')).toBeVisible();
    await expect(card.getByText('Unassigned')).toBeVisible();

    await page.getByRole('button', { name: 'Project actions' }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.getByText('Project deleted.')).toBeVisible();
  });

  test('the first card cannot move up and the last cannot move down', async ({ page }) => {
    await openSeededProject(page);

    const todo = page.locator('section', { hasText: 'To do' }).first();
    const first = (await todo.locator('article .card-title').first().innerText()).trim();

    await page.getByRole('button', { name: `Actions for ${first}` }).click();
    await expect(page.getByRole('menuitem', { name: 'Move up' })).toBeDisabled();
    await expect(page.getByRole('menuitem', { name: 'Move down' })).toBeEnabled();
    await page.keyboard.press('Escape');

    const last = (await todo.locator('article .card-title').last().innerText()).trim();
    await page.getByRole('button', { name: `Actions for ${last}` }).click();
    await expect(page.getByRole('menuitem', { name: 'Move down' })).toBeDisabled();
    await expect(page.getByRole('menuitem', { name: 'Move up' })).toBeEnabled();
  });
});
