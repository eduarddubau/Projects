import { test, expect } from '@playwright/test';

test.describe('Auth token lifecycle', () => {
  test('login stores both tokens; logout revokes them server-side and clears storage', async ({
    page,
  }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/login'));

    // Both tokens are persisted after login.
    const stored = await page.evaluate(() => ({
      access: localStorage.getItem('pj-authToken'),
      refresh: localStorage.getItem('pj-refreshToken'),
    }));
    expect(stored.access).toBeTruthy();
    expect(stored.refresh).toBeTruthy();

    // Logout calls the revoke endpoint and clears both tokens.
    const revoke = page.waitForRequest(
      (r) => r.url().includes('/api/auth/logout') && r.method() === 'POST',
    );
    await page.getByRole('button', { name: 'dev2 dev2@example.com' }).click();
    await page.getByRole('menuitem', { name: 'Sign Out' }).click();
    await revoke;
    await page.waitForURL((url) => url.pathname === '/');

    const cleared = await page.evaluate(() => ({
      access: localStorage.getItem('pj-authToken'),
      refresh: localStorage.getItem('pj-refreshToken'),
    }));
    expect(cleared.access).toBeNull();
    expect(cleared.refresh).toBeNull();
  });
});
