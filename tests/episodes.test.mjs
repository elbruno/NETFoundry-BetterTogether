import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { episodes, parseEpisode, resourceUrl, calendar, episodeStatus } from '../src/lib/episodes.mjs';
import { statusFor } from '../src/scripts/site.mjs';

test('all four public episode guides load, with nine existing demos in the first', () => {
  assert.equal(episodes.length, 4);
  assert.equal(episodes[0].demos.length, 9);
  assert.equal(episodes[0].recording, null);
  assert.match(episodes[0].html, /github.com\/elbruno\/NETFoundry-BetterTogether\/blob\/main\/episodes\/01-2026-10-15\/demos\/01-hello-decisions\/README.md/);
  assert.match(episodes[0].html, /github.com\/elbruno\/NETFoundry-BetterTogether\/blob\/main\/README.md/);
  assert.doesNotMatch(episodes[0].html, /href="demos\//);
});

test('Episode 1 covers Microsoft-Decision-1, decision models, games, and official Microsoft links', () => {
  const readme = readFileSync('episodes/01-2026-10-15/README.md', 'utf8');
  assert.match(episodes[0].summary, /Microsoft-Decision-1 and the emerging category of decision models/);
  assert.ok(episodes[0].tags.includes('Microsoft-Decision-1'));
  assert.ok(episodes[0].tags.includes('Decision models'));
  assert.match(readme, /Microsoft-Decision-1: our model for fast decision-making/);
  assert.match(readme, /Deploy and use Microsoft-Decision-1 in Microsoft Foundry/);
  assert.match(readme, /Microsoft models sold by Azure in the Foundry catalog/);
  assert.match(readme, /decision models/);
  assert.match(readme, /finish by watching a model choose actions in games/);
  assert.match(readme, /Decision Space Game/);
});

test('relative resources preserve fragments, parent navigation, and raw image URLs', () => {
  assert.equal(resourceUrl('demos/example/README.md#setup', '01-2026-10-15'), 'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/01-2026-10-15/demos/example/README.md#setup');
  assert.equal(resourceUrl('../../README.md', '01-2026-10-15'), 'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/README.md');
  assert.match(resourceUrl('assets/game.png', '01-2026-10-15', true), /^https:\/\/raw.githubusercontent.com/);
  assert.equal(resourceUrl('#overview', '01-2026-10-15'), '#overview');
  assert.throws(() => resourceUrl('javascript:alert(1)', '01-2026-10-15'), /Unsupported/);
});

test('recordings are derived only from the Recording section, not unrelated YouTube links', () => {
  const metadata = episodes[0];
  const source = '# Episode 1: Test\n\n## Overview\nA demo.\n\n## Recording\n[Watch](https://www.youtube.com/watch?v=confirmed-id)\n\n## Demos and resources\n';
  assert.equal(parseEpisode(metadata, source).recording, 'https://www.youtube.com/watch?v=confirmed-id');
  assert.equal(parseEpisode(metadata, source.replace('[Watch](https://www.youtube.com/watch?v=confirmed-id)', 'Not available.')).recording, null);
  assert.throws(() => parseEpisode(metadata, source.replace('watch?v=confirmed-id', '@dotnet')), /confirmed YouTube/);
  assert.equal(parseEpisode(metadata, source.replace('https://www.youtube.com/watch?v=confirmed-id', 'https://youtu.be/confirmed-id')).recording, 'https://youtu.be/confirmed-id');
});

test('browser and build status transitions agree at exact session boundaries', () => {
  const episode = episodes[0];
  for (const [now, expected] of [
    ['2026-10-15T15:59:59Z', 'upcoming'],
    [episode.start, 'live'],
    [episode.end, 'pending'],
  ]) {
    assert.equal(episodeStatus(episode, Date.parse(now)), expected);
    assert.equal(statusFor(episode.start, episode.end, null, Date.parse(now)), expected);
  }
  assert.equal(statusFor(episode.start, episode.end, 'https://youtu.be/confirmed', 0), 'recorded');
});

test('calendar downloads use UTC and handle November daylight saving changes', () => {
  assert.match(calendar(episodes[0]), /DTSTART:20261015T160000Z/);
  assert.match(calendar(episodes[2]), /DTSTART:20261112T170000Z/);
  assert.match(calendar(episodes[3]), /DTEND:20261203T180000Z/);
  assert.match(calendar(episodes[0]), /SUMMARY:Episode 1: .NET \+ Foundry\\, Better together/);
  assert.ok(calendar(episodes[0]).endsWith('END:VCALENDAR\r\n'));
  assert.ok(calendar(episodes[0]).split('\r\n').every((line) => Buffer.byteLength(line) <= 75));
});

test('README HTML is escaped rather than executable', () => {
  const episode = parseEpisode(episodes[0], '# Episode 1: Test\n\n## Overview\nDemo.\n\n<script>alert(1)</script>\n');
  assert.doesNotMatch(episode.html, /<script>/);
  assert.match(episode.html, /&lt;script&gt;/);
});
