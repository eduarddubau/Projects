import { test, expect } from '@playwright/test';

// Palette lives in localStorage only — no server state — so nothing needs
// restoring between tests; Playwright gives each test its own browser context.
test.describe('Colour scheme switching', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.locator('input[formcontrolname="email"]').fill('dev2@example.com');
    await page.locator('input[formcontrolname="password"]').fill('Password123!');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('applies a scheme, keeps the menu open, and survives reload via the pre-paint script', async ({
    page,
  }) => {
    await page.locator('.profile-button').click();
    const schemes = page.getByRole('radiogroup', { name: 'Color scheme' });
    await expect(schemes).toBeVisible();

    await schemes.getByRole('radio', { name: 'Emerald' }).click();

    // The menu must stay open so several schemes can be tried in one go. This is
    // the regression guard: mat-menu only closes on mat-menu-item clicks, and a
    // stray stopPropagation handler used to sit here defending against that.
    await expect(schemes).toBeVisible();
    await expect(schemes.getByRole('radio', { name: 'Emerald' })).toHaveAttribute(
      'aria-checked',
      'true',
    );
    await expect(page.locator('html')).toHaveAttribute('data-palette', 'emerald');
    expect(await page.evaluate(() => localStorage.getItem('pj-palette'))).toBe('emerald');

    // index.html re-applies the palette before first paint, so a reload must not
    // flash the default. That script cannot import the storage-key constant, so
    // this is the only thing holding the two spellings together.
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-palette', 'emerald');
  });

  // 'violet' is the default and is represented by the attribute being absent,
  // not by data-palette="violet" — so switching back has to remove it.
  test('returning to the default scheme clears the attribute', async ({ page }) => {
    // Arrive already on a non-default scheme rather than clicking two swatches in
    // a row: each swatch has a tooltip, and the cdk overlay pane it leaves behind
    // keeps hit-testing over its neighbours. This also proves the stored value is
    // restored on load, not just written on click.
    await page.evaluate(() => localStorage.setItem('pj-palette', 'rose'));
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-palette', 'rose');

    await page.locator('.profile-button').click();
    const schemes = page.getByRole('radiogroup', { name: 'Color scheme' });
    await schemes.getByRole('radio', { name: 'Violet' }).click();

    await expect(page.locator('html')).not.toHaveAttribute('data-palette', /.*/);
    expect(await page.evaluate(() => localStorage.getItem('pj-palette'))).toBe('violet');
  });
});
