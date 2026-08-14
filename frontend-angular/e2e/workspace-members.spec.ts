import { test, expect, Page } from '@playwright/test';

// Read-only against the seed: dev1 owns Acme Team, dev2..devN are Members
// (DbSeeder.SeedSharedWorkspaceAsync). Leaving is deliberately not covered here
// — the only safe subject is dev2, and restoring them needs the add-member API;
// a failed cleanup would leave the seed wrong until the volume is dropped, since
// the seeder skips a shared workspace that already exists. Unit 6's invite flow
// makes that repeatable.

async function login(page: Page, email: string) {
  await page.goto('/login');
  await page.locator('input[formcontrolname="email"]').fill(email);
  await page.locator('input[formcontrolname="password"]').fill('Password123!');
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/\/dashboard/);
}

/**
 * Scoped to the members table by its aria-label: Acme Team can also render a
 * pending-invitations table, and a bare `tbody tr` would count both.
 */
function memberRows(page: Page) {
  return page.getByRole('table', { name: 'Workspace members' }).locator('tbody tr');
}

/** Switches to Acme Team, then follows the switcher's Members item into it. */
async function openAcmeMembers(page: Page) {
  await page.locator('.ws-trigger').click();
  await page.getByRole('menuitem', { name: 'Acme Team' }).click();

  await page.locator('.ws-trigger').click();
  await page.getByRole('menuitem', { name: 'Members' }).click();

  await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/members$/);
}

test.describe('Workspace members', () => {
  test('the switcher reaches the members page, which lists the workspace members', async ({
    page,
  }) => {
    await login(page, 'dev1@example.com');
    await openAcmeMembers(page);

    await expect(page.getByRole('heading', { name: 'Members' })).toBeVisible();
    await expect(page.locator('.page-subtitle')).toHaveText('Acme Team');
    // Every seeded dev user joins Acme Team, so the count follows DEV_USER_COUNT
    // rather than being fixed here — one row per member is what matters.
    await expect(memberRows(page).first()).toBeVisible();
    expect(await memberRows(page).count()).toBeGreaterThan(1);
    // Column headers come from the dictionary, so this also proves the i18n keys
    // resolve rather than rendering as raw paths.
    await expect(
      page.getByRole('table', { name: 'Workspace members' }).locator('thead'),
    ).toContainText('Joined');
  });

  test('an owner can act on other members but not on their own row', async ({ page }) => {
    await login(page, 'dev1@example.com');
    await openAcmeMembers(page);

    const ownRow = memberRows(page).filter({ hasText: 'You' });
    await expect(ownRow).toHaveCount(1);
    await expect(ownRow.locator('button')).toHaveCount(0);

    const otherRow = memberRows(page).filter({ hasNot: page.getByText('You') });
    // Role menu plus remove; the exact count is not the point, having any is.
    expect(await otherRow.locator('button').count()).toBeGreaterThan(0);
  });

  test('a plain member is offered no actions at all', async ({ page }) => {
    await login(page, 'dev2@example.com');
    await openAcmeMembers(page);

    await expect(memberRows(page).first()).toBeVisible();
    await expect(
      page.getByRole('table', { name: 'Workspace members' }).locator('tbody button'),
    ).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Leave workspace' })).toBeVisible();
  });

  // The guard's whole reason for rebuilding the URL rather than redirecting to a
  // fixed path: the trailing /members has to survive.
  test('an unknown workspace id redirects to a real one, keeping the page', async ({ page }) => {
    await login(page, 'dev1@example.com');

    await page.goto('/w/00000000-0000-0000-0000-000000000000/members');

    await expect(page).toHaveURL(/\/w\/[0-9a-f-]{36}\/members$/);
    await expect(page).not.toHaveURL(/00000000-0000-0000-0000-000000000000/);
    await expect(page.getByRole('heading', { name: 'Members' })).toBeVisible();
  });
});
