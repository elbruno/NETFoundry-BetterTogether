import { test, expect } from '@playwright/test';

test('homepage links, public resources, images, and no runtime errors', async ({ page }) => {
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('./');
  await expect(page.locator('h1')).toContainText('Better Together');
  await expect(page.locator('.hero-copy').getByRole('link', { name: 'Bruno Capuano', exact: true })).toHaveAttribute('href', 'https://aka.ms/elbruno');
  await expect(page.getByText(/and guests for live explorations/)).toBeVisible();
  await expect(page.getByRole('link', { name: /Register for the series/ })).toHaveAttribute('href', 'https://developer.microsoft.com/en-us/reactor/series/S-1718/');
  await expect(page.locator('[data-episode]')).toHaveCount(4);
  await expect(page.locator('[data-episode]').first().locator('.tags')).toContainText('Microsoft-Decision-1');
  await expect(page.locator('[data-episode]').first().locator('.tags')).toContainText('Decision models');
  const episodeTwo = page.locator('[data-episode]').nth(1);
  await expect(episodeTwo.locator('.host-byline').getByRole('link', { name: 'Jose Luis Latorre' }))
    .toHaveAttribute('href', 'https://www.linkedin.com/in/joslat/');
  await expect(episodeTwo.locator('.card-summary')).toContainText('two topics: Hosted Agents using decision models and agent memory');
  await expect(episodeTwo.locator('.card-summary')).toContainText('three stages: a C# console app calls a decision model in Microsoft Foundry');
  await expect(episodeTwo.locator('.tags')).toContainText('Foundry Memory Stores');
  await expect(page.locator('body')).not.toContainText('ElBruno.AI.Decisions');
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
  await page.clock.install({ time: new Date('2026-10-29T15:00:00Z') });
  await page.goto('./');
  await expect(page.locator('#spotlight')).toBeVisible();
  await expect(page.locator('#spotlight-title')).toContainText('Episode 2');
  await page.getByRole('button', { name: 'Recorded', exact: true }).click();
  await expect(page.locator('[data-episode]:visible')).toHaveCount(1);
  await expect(page.locator('#empty-state')).toBeHidden();
  await page.getByRole('button', { name: 'Upcoming', exact: true }).click();
  await expect(page.locator('[data-episode]:visible')).toHaveCount(3);
  await page.clock.setSystemTime(new Date('2026-10-29T16:30:00Z'));
  await page.clock.runFor(30_000);
  await expect(page.locator('#spotlight-label')).toHaveText('LIVE NOW');
  await page.clock.setSystemTime(new Date('2026-10-29T17:01:00Z'));
  await page.clock.runFor(30_000);
  await expect(page.locator('#spotlight-title')).toContainText('Episode 3');
  await page.getByRole('button', { name: 'All episodes' }).click();
  await expect(page.locator('[data-episode]').first().locator('[data-status]')).toHaveText('Recording available');
  await expect(page.locator('[data-episode]').nth(1).locator('[data-status]')).toHaveText('Recording pending');
  await page.clock.setSystemTime(new Date('2026-12-04T18:00:00Z'));
  await page.getByRole('button', { name: 'Upcoming', exact: true }).click();
  await expect(page.locator('[data-episode]:visible')).toHaveCount(0);
  await expect(page.locator('#empty-state')).toBeVisible();
});

test('episode guides render demos and working internal navigation', async ({ page, request }) => {
  await page.goto('episodes/01-2026-10-15/');
  await expect(page.getByRole('navigation', { name: 'Episode demos' }).locator('a')).toHaveCount(10);
  await expect(page.locator('.prose h2').filter({ hasText: 'Overview' })).toBeVisible();
  await expect(page.locator('.prose h2').filter({ hasText: "What we'll explore" })).toBeVisible();
  await expect(page.getByRole('link', { name: /Microsoft-Decision-1: our model for fast decision-making/i })).toHaveAttribute('href', 'https://commandline.microsoft.com/microsoft-decision-1-model-foundry/');
  await expect(page.getByRole('link', { name: /Deploy and use Microsoft-Decision-1 in Microsoft Foundry/ })).toHaveAttribute('href', 'https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision');
  await expect(page.locator('.episode-topic-list')).toContainText('Decision models');
  await expect(page.locator('.copy-button').first()).toBeVisible();
  await expect(page.locator('.prose a[href^="demos/"]')).toHaveCount(0);
  const calendar = await request.get('calendar/01-2026-10-15.ics');
  expect(calendar.ok()).toBe(true);
  expect(await calendar.text()).toContain('DTSTART:20261015T160000Z');
  await page.getByRole('link', { name: 'Episode 2', exact: false }).last().click();
  await expect(page).toHaveURL(/episodes\/02-2026-10-29\/$/);
  const episodeTwoDemos = page.getByRole('navigation', { name: 'Episode demos' });
  await expect(episodeTwoDemos.locator('a')).toHaveCount(3);
  await expect(episodeTwoDemos.getByRole('link', { name: 'Call a Foundry decision model from C#' }))
    .toHaveAttribute('href', 'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/01-console-decision-model/README.md');
  await expect(episodeTwoDemos.getByRole('link', { name: 'Run the decision inside a local agent' }))
    .toHaveAttribute('href', 'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/02-local-agent/README.md');
  await expect(episodeTwoDemos.getByRole('link', { name: 'Run the agent as a Foundry Hosted Agent' }))
    .toHaveAttribute('href', 'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/03-foundry-hosted-agent/README.md');
  const episodeTwoHeader = page.locator('.episode-heading');
  await expect(episodeTwoHeader.getByRole('link', { name: 'Jose Luis Latorre' }))
    .toHaveAttribute('href', 'https://www.linkedin.com/in/joslat/');
  await expect(page.locator('.prose')).toContainText('Foundry Memory Stores');
  await expect(page.locator('.prose')).toContainText('AgentEval');
  await expect(page.locator('.prose')).toContainText('two topics: Hosted Agents using decision models and agent memory');
  await expect(page.locator('.prose')).toContainText('AgentEval and hybrid evaluation are possible related discussion if time allows');
});

test('completed episode shows both channel recordings, co-host, and guest resources', async ({ page }) => {
  await page.goto('./');
  const card = page.locator('[data-episode]').first();
  await expect(card.getByRole('link', { name: 'Pablo Piovano' })).toHaveAttribute('href', 'https://www.linkedin.com/in/ppiova/');
  await expect(card.getByRole('link', { name: /Watch on the .NET Channel/ })).toHaveAttribute('href', 'https://www.youtube.com/watch?v=mHrs-JHdPaY');
  await expect(card.getByRole('link', { name: /Watch on Microsoft Reactor/ })).toHaveAttribute('href', 'https://www.youtube.com/watch?v=n2CL10iTjp0');
  await page.goto('episodes/01-2026-10-15/');
  const header = page.locator('.episode-heading');
  await expect(header.getByRole('link', { name: 'Pablo Piovano' })).toHaveAttribute('href', 'https://www.linkedin.com/in/ppiova/');
  await expect(header.getByRole('link', { name: /Watch on the .NET Channel/ })).toHaveAttribute('href', 'https://www.youtube.com/watch?v=mHrs-JHdPaY');
  await expect(header.getByRole('link', { name: /Watch on Microsoft Reactor/ })).toHaveAttribute('href', 'https://www.youtube.com/watch?v=n2CL10iTjp0');
  await expect(header.getByRole('link', { name: 'Add to calendar' })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Guest resources' }).getByRole('link', { name: /Foundry Field Notes labs/ })).toHaveAttribute('href', 'https://github.com/ppiova/foundry-field-notes-labs');
  await expect(page.getByRole('navigation', { name: 'Guest resources' }).locator('a')).toHaveCount(3);
  await expect(page.getByText('Recording not yet available.', { exact: false })).toHaveCount(0);
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
