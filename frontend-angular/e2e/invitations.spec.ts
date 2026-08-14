import { test, expect, Page } from '@playwright/test';

// Every test works inside a workspace it creates, so the seeded Acme Team is
// never mutated — that is what makes the leave flow safe to run here when it was
// not in unit 5. Cleanup deletes the workspace through the API in a hook, never
// at the end of a test body where a mid-test failure would leak it.
let createdName: string | null = null;

// Files run in parallel and workspace-switcher.spec asserts the exact workspace
// list of BOTH dev1 and dev2, so neither account can be touched here — adding a
// workspace to either fails that spec while these run. dev3 and the admin are
// the two seeded accounts nothing else makes list assertions about.
const OWNER = 'dev3@example.com';
const INVITEE = 'admin@example.com';
const INVITEE_NAME = 'Admin User';

async function login(page: Page, email: string) {
  // These tests switch accounts mid-test, and guestGuard redirects an already
  // signed-in visitor away from /login — so drop the session before asking for
  // the form. goto() reloads the app, which re-reads the now-empty storage.
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());

  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(email);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/** Creates a workspace through the UI and lands on its members page. */
async function createWorkspaceAndOpenMembers(page: Page, name: string) {
  await page.goto('/workspaces');
  await page.getByRole('button', { name: 'New workspace' }).click();
  await page.locator('input[formcontrolname="name"]').fill(name);
  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.locator('.ws-card.is-current')).toContainText(name);

  await page.locator('.ws-trigger').click();
  await page.getByRole('menuitem', { name: 'Members' }).click();
  await expect(page.locator('.page-subtitle')).toHaveText(name);
}

test.describe('Invitations', () => {
  test.afterEach(async ({ page }) => {
    if (!createdName) return;
    const name = createdName;
    createdName = null;

    // Straight to the API, never through the UI: the page may be signed in as
    // dev2 or parked on a failed assertion, and guestGuard bounces an already
    // signed-in visitor away from /login — which silently skipped this whole
    // hook the first time and leaked four workspaces.
    const auth = await page.request.post('/api/auth/login', {
      data: { email: OWNER, password: 'Password123!' },
    });
    if (!auth.ok()) return;
    const headers = { Authorization: `Bearer ${((await auth.json()) as { token: string }).token}` };

    const res = await page.request.get('/api/workspaces', { headers });
    if (!res.ok()) return;
    for (const w of (await res.json()) as { id: string; name: string }[]) {
      if (w.name === name) await page.request.delete(`/api/workspaces/${w.id}`, { headers });
    }
  });

  test('inviting an unknown address shows a link that is rendered, not just copied', async ({
    page,
  }) => {
    createdName = `E2E Invite ${Date.now()}`;
    await login(page, OWNER);
    await createWorkspaceAndOpenMembers(page, createdName);

    await page.getByRole('button', { name: 'Invite people' }).click();
    await page.locator('input[formcontrolname="email"]').fill(`nobody-${Date.now()}@example.com`);
    await page.getByRole('button', { name: 'Send invite' }).click();

    // The token is returned exactly once, so the dialog must show it as text —
    // the clipboard needs a secure context and cannot be the only route.
    const link = page.locator('.invite-link');
    await expect(link).toContainText('/invitations/accept?token=');
    // Scoped to the dialog: the snackbar's dismiss action is also called Close.
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click();

    // And it lands in the pending list, which is what makes it revocable.
    await expect(page.getByRole('heading', { name: 'Pending invitations' })).toBeVisible();
    await page.getByRole('button', { name: 'Revoke' }).click();
    await expect(page.getByRole('heading', { name: 'Pending invitations' })).toBeHidden();
  });

  // The invitee already has an account, so the API adds them outright and there
  // is no link — the discriminated union's other branch.
  test('inviting a known address adds them straight away, and they can leave', async ({ page }) => {
    createdName = `E2E Leave ${Date.now()}`;
    await login(page, OWNER);
    await createWorkspaceAndOpenMembers(page, createdName);

    await page.getByRole('button', { name: 'Invite people' }).click();
    await page.locator('input[formcontrolname="email"]').fill(INVITEE);
    await page.getByRole('button', { name: 'Send invite' }).click();

    await expect(page.getByRole('dialog')).toBeHidden();
    const members = page.getByRole('table', { name: 'Workspace members' });
    await expect(members.locator('tbody tr')).toHaveCount(2);
    await expect(members).toContainText(INVITEE_NAME);

    // Now the half unit 5 could not test safely: the invitee leaves a workspace
    // this test created, so nothing seeded is disturbed.
    await login(page, INVITEE);
    await page.locator('.ws-trigger').click();
    await page.getByRole('menuitem', { name: createdName }).click();
    await page.locator('.ws-trigger').click();
    await page.getByRole('menuitem', { name: 'Members' }).click();

    await page.getByRole('button', { name: 'Leave workspace' }).click();
    await page.getByRole('button', { name: 'Confirm' }).click();

    await expect(page).toHaveURL(/\/workspaces$/);
    await expect(page.locator('.ws-grid')).not.toContainText(createdName);
    // The store repaired its own invariant, so the header still names a real one.
    await expect(page.locator('.ws-trigger')).not.toContainText(createdName);
  });

  test('a used-up invitation link explains itself instead of failing silently', async ({
    page,
  }) => {
    await login(page, OWNER);

    await page.goto('/invitations/accept?token=definitely-not-a-real-token');

    await expect(page.getByRole('heading', { name: 'That did not work' })).toBeVisible();
    await expect(page.locator('.page-container')).toContainText('not valid, or it has expired');
  });

  test('an invitation link with no token says so rather than calling the API', async ({ page }) => {
    await login(page, OWNER);

    await page.goto('/invitations/accept');

    await expect(page.locator('.page-container')).toContainText('missing its invitation token');
  });
});
