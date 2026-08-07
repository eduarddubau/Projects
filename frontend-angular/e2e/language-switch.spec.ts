import { test, expect } from '@playwright/test';

test.describe('Language switching', () => {
  test('switches to Romanian, persists via cookie across reload (SSR), and back', async ({
    page,
    context,
  }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: /Manage your projects/ })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');

    await page.getByRole('button', { name: 'Change language' }).click();
    await page.getByRole('menuitem', { name: 'Română' }).click();

    await expect(page.getByRole('heading', { name: /Gestionează-ți proiectele/ })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'ro');

    const langCookie = (await context.cookies()).find((c) => c.name === 'lang');
    expect(langCookie?.value).toBe('ro');

    // Reload hits the SSR path: the server must render Romanian from the cookie.
    await page.reload();
    await expect(page.getByRole('heading', { name: /Gestionează-ți proiectele/ })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'ro');

    // The switcher itself is now labeled in Romanian.
    await page.getByRole('button', { name: 'Schimbă limba' }).click();
    await page.getByRole('menuitem', { name: 'English' }).click();
    await expect(page.getByRole('heading', { name: /Manage your projects/ })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  });
});
