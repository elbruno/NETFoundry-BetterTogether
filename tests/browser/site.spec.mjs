import { test, expect } from '@playwright/test';

test('homepage links, public resources, images, and no runtime errors', async ({ page }) => {
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('./');
  await expect(page.locator('h1')).toContainText('Better Together');
  await expect(page.getByRole('link', { name: 'Bruno Capuano', exact: true })).toHaveAttribute('href', 'https://aka.ms/elbruno');
  await expect(page.getByText(/and guests for live explorations/)).toBeVisible();
  await expect(page.getByRole('link', { name: /Register for the series/ })).toHaveAttribute('href', 'https://developer.microsoft.com/en-us/reactor/series/S-1718/');
  await expect(page.locator('[data-episode]')).toHaveCount(4);
  await expect(page.locator('[data-episode]').first().locator('.tags')).toContainText('Microsoft-Decision-1');
  await expect(page.locator('[data-episode]').first().locator('.tags')).toContainText('Decision models');
  await expect(page.locator('.learning-card')).toHaveCount(4);
  await expect.poll(() => page.locator('.hero-art img').evaluate((image) => image.complete && image.naturalWidth > 0)).toBe(true);
  expect(errors).toEqual([]);
});

test('system theme follows OS changes, explicit choices survive reload', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('./');
  await expect(page.locator('#theme')).toHaveValue('system');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.locator('#theme').selectOption('light');
  await page.reload();
  await expect(page.locator('#theme')).toHaveValue('light');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.locator('#theme').selectOption('system');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
});

test('status filters and spotlight use the actual session window', async ({ page }) => {
  await page.clock.install({ time: new Date('2026-10-15T15:00:00Z') });
  await page.goto('./');
  await expect(page.locator('#spotlight')).toBeVisible();
  await expect(page.locator('#spotlight-title')).toContainText('Episode 1');
  await page.getByRole('button', { name: 'Recorded', exact: true }).click();
  await expect(page.locator('[data-episode]:visible')).toHaveCount(0);
  await expect(page.locator('#empty-state')).toBeVisible();
  await page.getByRole('button', { name: 'Upcoming', exact: true }).click();
  await expect(page.locator('[data-episode]:visible')).toHaveCount(4);
  await page.clock.setSystemTime(new Date('2026-10-15T16:30:00Z'));
  await page.clock.runFor(30_000);
  await expect(page.locator('#spotlight-label')).toHaveText('LIVE NOW');
  await page.clock.setSystemTime(new Date('2026-10-15T17:01:00Z'));
  await page.clock.runFor(30_000);
  await expect(page.locator('#spotlight-title')).toContainText('Episode 2');
  await page.getByRole('button', { name: 'All episodes' }).click();
  await expect(page.locator('[data-episode]').first().locator('[data-status]')).toHaveText('Recording pending');
});

test('episode guides render demos and working internal navigation', async ({ page, request }) => {
  await page.goto('episodes/01-2026-10-15/');
  await expect(page.locator('.episode-sidebar nav a')).toHaveCount(9);
  await expect(page.locator('.prose h2').filter({ hasText: 'Overview' })).toBeVisible();
  await expect(page.locator('.prose h2').filter({ hasText: "What we'll explore" })).toBeVisible();
  await expect(page.getByRole('link', { name: /Microsoft-Decision-1: our model for fast decision-making/ })).toHaveAttribute('href', 'https://commandline.microsoft.com/microsoft-decision-1-model-foundry/');
  await expect(page.getByRole('link', { name: /Deploy and use Microsoft-Decision-1 in Microsoft Foundry/ })).toHaveAttribute('href', 'https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision');
  await expect(page.locator('.episode-topic-list')).toContainText('Decision models');
  await expect(page.locator('.copy-button').first()).toBeVisible();
  await expect(page.locator('.prose a[href^="demos/"]')).toHaveCount(0);
  const calendar = await request.get('calendar/01-2026-10-15.ics');
  expect(calendar.ok()).toBe(true);
  expect(await calendar.text()).toContain('DTSTART:20261015T160000Z');
  await page.getByRole('link', { name: 'Episode 2', exact: false }).last().click();
  await expect(page).toHaveURL(/episodes\/02-2026-10-29\/$/);
  await expect(page.getByText('Demos will be added when available.')).toBeVisible();
});

test('mobile layout has no horizontal overflow on homepage or guide', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  for (const path of ['./', 'episodes/01-2026-10-15/']) {
    await page.goto(path);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(page.locator('#theme')).toBeVisible();
    await expect(page.locator('h1')).toBeVisible();
  }
});

test('site remains usable without JavaScript', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  try {
    await page.goto('http://127.0.0.1:4325/NETFoundry-BetterTogether/');
    await expect(page.locator('[data-episode]')).toHaveCount(4);
    await expect(page.locator('#episode-filters')).toBeHidden();
    await expect(page.locator('.theme-control')).toBeHidden();
    await expect(page.locator('time').first()).toContainText('UTC');
    await expect(page.locator('.learning-card').first()).toBeVisible();
  } finally {
    await context.close();
  }
});

test('blocked preference storage does not break theme selection', async ({ page }) => {
  await page.addInitScript(() => {
    Storage.prototype.getItem = () => { throw new DOMException('Blocked', 'SecurityError'); };
    Storage.prototype.setItem = () => { throw new DOMException('Blocked', 'SecurityError'); };
  });
  await page.goto('./');
  await page.locator('#theme').selectOption('dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(page.locator('[data-episode]')).toHaveCount(4);
});
