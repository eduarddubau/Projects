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

/** What DbSeeder gives the shared project, so a spec that edits it can put it back. */
const SEEDED_REDESIGN = {
  name: 'Acme Website Redesign',
  description: 'Shared workspace project, created by the owner.',
};

/**
 * Puts the seeded project back, using the page's own token the way profile.spec does.
 *
 * The rule this file was breaking: **a spec that mutates shared seed data must restore in a
 * hook, never in the test body.** Editing the description left it reading
 * `Edited by dev2 at <epoch>` permanently, cumulative across runs — and every assertion used
 * `hasText`, which matches by substring, so nothing ever failed over it. The exact-name
 * lookup in workspace-tasks.spec is what finally surfaced it.
 *
 * Throws rather than returning quietly on anything unexpected: a restore hook that gives up
 * in silence recreates exactly the drift it exists to prevent, and the next spec to notice
 * would again be one that happens to compare by equality.
 */
async function restoreSeededProject(page: Page): Promise<void> {
  const token = await page.evaluate(() => localStorage.getItem('pj-authToken'));
  if (!token) return; // The test never signed in, so it cannot have edited anything.

  const headers = { Authorization: `Bearer ${token}` };

  const workspaces = (await getJson(page, '/api/workspaces', headers)) as {
    id: string;
    name: string;
  }[];
  const acme = workspaces.find((w) => w.name === 'Acme Team');
  if (!acme) throw new Error('Acme Team is missing — the seed is already broken.');

  const projects = (await getJson(page, `/api/workspaces/${acme.id}/projects`, headers)) as {
    id: string;
    name: string;
    description?: string;
  }[];

  // Exact match, not startsWith: a prefix could catch a differently-named project and
  // rename *it* to the seeded name, leaving two rows that the exact lookups downstream
  // cannot tell apart.
  const clean = projects.find((p) => p.name === SEEDED_REDESIGN.name);
  if (clean && clean.description === SEEDED_REDESIGN.description) return;

  const drifted = clean ?? projects.find((p) => p.name.startsWith(SEEDED_REDESIGN.name));
  if (!drifted) throw new Error(`No project resembling "${SEEDED_REDESIGN.name}" to restore.`);

  const put = await page.request.put(`/api/projects/${drifted.id}`, {
    headers,
    data: SEEDED_REDESIGN,
  });
  if (!put.ok()) {
    throw new Error(`Restoring the seeded project failed with ${put.status()}.`);
  }
}

async function getJson(page: Page, url: string, headers: Record<string, string>): Promise<unknown> {
  const response = await page.request.get(url, { headers });
  if (!response.ok()) throw new Error(`GET ${url} failed with ${response.status()}.`);
  return response.json();
}

/** Returns the workspace id the list is showing, so callers can pin it. */
async function openAcmeProjects(page: Page): Promise<string> {
  await page.locator('.ws-trigger').click();
  await page.locator('.ws-item', { hasText: 'Acme Team' }).click();

  // Wait for the switch to land before clicking: the nav link is built from the
  // selected id, and clicking it too early navigates to the previous workspace.
  await expect(page.locator('.ws-trigger')).toContainText('Acme Team');

  await page.locator('.ws-nav a', { hasText: 'Projects' }).click();

  // The row, not the URL: switching workspace already navigated to the new home, so a
  // /w/{any-id} match is satisfied by the workspace being left as much as the one being
  // entered and pins nothing. An Acme project appearing is the real evidence.
  await expect(page.locator('tr', { hasText: 'Acme Website Redesign' })).toBeVisible();

  return new URL(page.url()).pathname.split('/')[2];
}

test.describe('Workspace-scoped projects', () => {
  // Only the one test below edits the seeded project, and the suite runs on two workers
  // because the dev server buckles — so the other four skip the round-trips entirely. The
  // flag is set before the edit, so a mid-test failure still restores.
  let editedTheSeededProject = false;

  test.beforeEach(() => {
    editedTheSeededProject = false;
  });

  // A hook, not the test body: it has to run even when the body fails partway.
  test.afterEach(async ({ page }) => {
    if (editedTheSeededProject) await restoreSeededProject(page);
  });

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

    // No row-level assertion: the projects table has no actions column any more, so
    // "a member sees no Delete here" would pass for everyone and prove nothing. The
    // menu checks below are the real test.
    const row = page.locator('tr', { hasText: 'Acme Website Redesign' });
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
    editedTheSeededProject = true;
    await dialog.getByLabel('Description').fill(`Edited by dev2 at ${Date.now()}`);
    await dialog.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('Project updated.')).toBeVisible();
  });

  test('an owner moves a project between workspaces and the URL follows', async ({ page }) => {
    await login(page, 'dev1@example.com');

    // A project of its own, so a rerun never fights the seeded ones over a name.
    const name = `Movable ${Date.now()}`;
    await page.locator('.ws-nav a', { hasText: 'Projects' }).click();
    await expect(page).toHaveURL(/\/w\/[0-9a-f-]+\/projects$/);
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

  // The counterpart to the member test above. Delete left the table entirely, so this has
  // to look where the action actually lives now — the detail page's actions menu.
  test('the owner of the workspace is offered delete', async ({ page }) => {
    await login(page, 'dev1@example.com');
    await openAcmeProjects(page);

    await page.locator('tr', { hasText: 'Acme Q3 Roadmap' }).click();
    await page.getByRole('button', { name: 'Project actions' }).click();

    await expect(page.getByRole('menuitem', { name: 'Delete' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: 'Move to workspace' })).toBeVisible();
  });
});
