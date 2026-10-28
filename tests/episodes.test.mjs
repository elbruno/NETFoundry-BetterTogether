import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { episodes, parseEpisode, resourceUrl, calendar, episodeStatus } from '../src/lib/episodes.mjs';
import { statusFor } from '../src/scripts/site.mjs';

test('all four public episode guides load, with ten existing demos in the first', () => {
  assert.equal(episodes.length, 4);
  assert.equal(episodes[0].demos.length, 10);
  assert.equal(episodes[0].recording, 'https://www.youtube.com/watch?v=mHrs-JHdPaY');
  assert.deepEqual(episodes[0].recordings, [
    { title: 'Watch on the .NET Channel', url: 'https://www.youtube.com/watch?v=mHrs-JHdPaY' },
    { title: 'Watch on Microsoft Reactor', url: 'https://www.youtube.com/watch?v=n2CL10iTjp0' },
  ]);
  assert.match(episodes[0].html, /github.com\/elbruno\/NETFoundry-BetterTogether\/blob\/main\/episodes\/01-2026-10-15\/demos\/01-hello-decisions\/README.md/);
  assert.match(episodes[0].html, /github.com\/elbruno\/NETFoundry-BetterTogether\/blob\/main\/README.md/);
  assert.doesNotMatch(episodes[0].html, /href="demos\//);
});

test('Episode 1 credits both hosts and links guest labs without mixing them into local demos', () => {
  assert.deepEqual(episodes[0].hosts, [
    { title: 'Bruno Capuano', url: 'https://aka.ms/elbruno' },
    { title: 'Pablo Piovano', url: 'https://www.linkedin.com/in/ppiova/' },
  ]);
  assert.equal(episodes[0].guestResources.length, 3);
  assert.equal(episodes[0].guestResources[0].url, 'https://github.com/ppiova/foundry-field-notes-labs');
  assert.deepEqual(episodes[1].hosts, [
    { title: 'Bruno Capuano', url: 'https://aka.ms/elbruno' },
    { title: 'Jose Luis Latorre', url: 'https://www.linkedin.com/in/joslat/' },
  ]);
  assert.deepEqual(episodes[1].recordings, []);
  assert.deepEqual(episodes[1].guestResources, []);
});

test('series navigation is not a guest resource', () => {
  const source = '# Episode 2: Test\n\n## Overview\nA demo.\n\n## Guest demos and resources\n'
    + '[Guest lab](https://example.com/lab)\n\n[Back to the series](../../README.md)\n';
  assert.deepEqual(parseEpisode(episodes[1], source).guestResources, [
    { title: 'Guest lab', url: 'https://example.com/lab' },
  ]);
});

test('Episode 2 homepage card exposes the four-stage demo and co-speaker scope', () => {
  const episode = episodes[1];
  assert.match(episode.summary, /two topics: Hosted Agents using decision models and agent memory/);
  assert.match(episode.summary, /four stages: a C# console app calls a decision model in Microsoft Foundry/);
  assert.match(episode.summary, /Aspire displays the local agent's GenAI traces/);
  assert.doesNotMatch(episode.summary, /ElBruno\.AI\.Decisions/);
  assert.match(episode.html, /Co-speaker Jose Luis\s+Latorre joins to compare Foundry Memory Stores\s+with custom AgentMemory/);
  const seriesReadme = readFileSync('README.md', 'utf8');
  assert.match(seriesReadme, /focuses on Hosted Agents using decision models and agent memory/);
  assert.doesNotMatch(seriesReadme, /ElBruno\.AI\.Decisions/);
  assert.deepEqual(episode.tags, [
    'C#',
    'Microsoft Agent Framework',
    'Aspire',
    'Foundry Hosted Agents',
    'Microsoft-Decision-1',
    'Foundry Memory Stores',
    'AgentMemory',
    'Azure RBAC',
  ]);
  assert.match(episode.html, /Using custom AgentMemory from a Foundry Hosted Agent is exploratory/);
  assert.match(episode.html, /AgentEval and hybrid evaluation are possible related discussion if time allows/);
  assert.match(episode.html, /GraphBridge and GraphRAG may be mentioned only if ready/);
  assert.deepEqual(episode.demos.map(({ title }) => title), [
    'Call a Foundry decision model from C#',
    'Run the decision inside a local agent',
    'Inspect local agent GenAI traces with Aspire',
    'Run the agent as a Foundry Hosted Agent',
  ]);
  assert.deepEqual(episode.demos.map(({ url }) => url), [
    'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/01-console-decision-model/README.md',
    'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/02-local-agent/README.md',
    'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/03-local-agent-aspire/README.md',
    'https://github.com/elbruno/NETFoundry-BetterTogether/blob/main/episodes/02-2026-10-29/demos/04-foundry-hosted-agent/README.md',
  ]);
});

test('Episode 2 has four uniquely numbered source-backed demo directories', () => {
  const root = 'episodes/02-2026-10-29/demos';
  const directories = readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && /^\d{2}-/.test(entry.name))
    .map((entry) => entry.name).sort();
  assert.deepEqual(directories, [
    '01-console-decision-model',
    '02-local-agent',
    '03-local-agent-aspire',
    '04-foundry-hosted-agent',
  ]);
  for (const [directory, project] of [
    [directories[0], 'src/DecisionConsole/DecisionConsole.csproj'],
    [directories[1], 'src/LocalRoutingAgent/LocalRoutingAgent.csproj'],
    [directories[2], 'src/LocalRoutingAgent/LocalRoutingAgent.csproj'],
    [directories[3], 'src/SupportRoutingAgent/SupportRoutingAgent.csproj'],
  ]) {
    assert.ok(existsSync(`${root}/${directory}/README.md`));
    assert.ok(existsSync(`${root}/${directory}/${project}`));
  }
  assert.ok(existsSync(`${root}/${directories[2]}/apphost.cs`));
  assert.ok(existsSync(`${root}/${directories[3]}/azure.yaml`));
});

test('Episode 1 covers Microsoft-Decision-1, decision models, games, and official Microsoft links', () => {
  const readme = readFileSync('episodes/01-2026-10-15/README.md', 'utf8');
  assert.equal(episodes[0].title, 'Episode 1: .NET + Foundry, Better Together: Decision Models in Action');
  assert.match(episodes[0].summary, /Microsoft-Decision-1 and the emerging category of decision models/);
  assert.ok(episodes[0].tags.includes('Microsoft-Decision-1'));
  assert.ok(episodes[0].tags.includes('Decision models'));
  assert.match(readme, /Microsoft-Decision-1: Our model for fast decision-making/);
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
  const both = source.replace('## Demos and resources', '[Watch on Reactor](https://youtu.be/reactor-id)\n\n## Demos and resources');
  assert.equal(parseEpisode(metadata, both).recordings.length, 2);
  assert.throws(() => parseEpisode(metadata, both.replace('https://youtu.be/reactor-id', 'https://www.youtube.com/@MicrosoftReactor')), /confirmed YouTube/);
  assert.throws(() => parseEpisode(metadata, both.replace('https://youtu.be/reactor-id', 'not-a-url')), /invalid recording URL/);
});

test('browser and build status transitions agree at exact session boundaries', () => {
  const episode = { ...episodes[0], recording: null };
  for (const [now, expected] of [
    ['2026-10-15T15:59:59Z', 'upcoming'],
    [episode.start, 'live'],
    [episode.end, 'pending'],
  ]) {
    assert.equal(episodeStatus(episode, Date.parse(now)), expected);
    assert.equal(statusFor(episode.start, episode.end, null, Date.parse(now)), expected);
  }
  assert.equal(statusFor(episode.start, episode.end, 'https://youtu.be/confirmed', 0), 'recorded');
  assert.equal(episodeStatus(episodes[0]), 'recorded');
});

test('calendar downloads use UTC and handle November daylight saving changes', () => {
  assert.match(calendar(episodes[0]), /DTSTART:20261015T160000Z/);
  assert.match(calendar(episodes[2]), /DTSTART:20261112T170000Z/);
  assert.match(calendar(episodes[3]), /DTEND:20261203T180000Z/);
  assert.match(calendar(episodes[0]).replace(/\r\n /g, ''), /SUMMARY:Episode 1: \.NET \+ Foundry\\, Better Together: Decision Models in Action/);
  assert.ok(calendar(episodes[0]).endsWith('END:VCALENDAR\r\n'));
  assert.ok(calendar(episodes[0]).split('\r\n').every((line) => Buffer.byteLength(line) <= 75));
});

test('README HTML is escaped rather than executable', () => {
  const episode = parseEpisode(episodes[0], '# Episode 1: Test\n\n## Overview\nDemo.\n\n<script>alert(1)</script>\n');
  assert.doesNotMatch(episode.html, /<script>/);
  assert.match(episode.html, /&lt;script&gt;/);
});
