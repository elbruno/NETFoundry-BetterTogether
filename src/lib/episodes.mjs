import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { Marked } from 'marked';
import { statusFor } from '../scripts/site.mjs';

export const repository = 'https://github.com/elbruno/NETFoundry-BetterTogether';
export const registration = 'https://developer.microsoft.com/en-us/reactor/series/S-1718/';

/** @typedef {{ id: string, start: string, end: string, event: string, tags: string[] }} ScheduleEntry */
/** @typedef {{ title: string, path: string, description: string, url: string }} Demo */
/** @typedef {{ title: string, url: string }} EpisodeLink */
/** @typedef {ScheduleEntry & { number: number, title: string, summary: string, eventUrl: string, sourceUrl: string, recording: string | null, recordings: EpisodeLink[], hosts: EpisodeLink[], guestResources: EpisodeLink[], demos: Demo[], html: string }} Episode */

// Only scheduling and discovery metadata live here. Learning content stays in each public README.
const schedule = [
  { id: '01-2026-10-15', start: '2026-10-15T16:00:00Z', end: '2026-10-15T17:00:00Z', event: '27630', tags: ['Microsoft-Decision-1', 'Decision models', 'C#', 'Ollama', 'Decision games'] },
  { id: '02-2026-10-29', start: '2026-10-29T16:00:00Z', end: '2026-10-29T17:00:00Z', event: '27631', tags: ['C#', 'Foundry'] },
  { id: '03-2026-11-12', start: '2026-11-12T17:00:00Z', end: '2026-11-12T18:00:00Z', event: '27632', tags: ['C#', 'Foundry'] },
  { id: '04-2026-12-03', start: '2026-12-03T17:00:00Z', end: '2026-12-03T18:00:00Z', event: '27634', tags: ['C#', 'Foundry'] },
];

export function section(markdown, heading) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const start = lines.findIndex((line) => line.trim().toLowerCase() === `## ${heading}`.toLowerCase());
  if (start === -1) return '';
  const end = lines.findIndex((line, index) => index > start && /^## /.test(line));
  return lines.slice(start + 1, end === -1 ? undefined : end).join('\n').trim();
}

export function resourceUrl(href, id, image = false) {
  if (/^(https?:|mailto:|#)/i.test(href)) return href;
  if (/^[a-z][a-z\d+.-]*:/i.test(href) || href.startsWith('//')) {
    throw new Error(`Unsupported resource URL in ${id}: ${href}`);
  }
  const path = new URL(href, `https://content.invalid/episodes/${id}/`);
  const host = image ? 'https://raw.githubusercontent.com/elbruno/NETFoundry-BetterTogether/main' : `${repository}/blob/main`;
  return `${host}${path.pathname}${path.search}${path.hash}`;
}

/**
 * @param {string} markdown
 * @param {string} heading
 * @returns {EpisodeLink[]}
 */
function sectionLinks(markdown, heading) {
  const parser = new Marked();
  const links = [];
  parser.walkTokens(parser.lexer(section(markdown, heading)), (token) => {
    if (token.type === 'link') links.push({ title: token.text, url: token.href });
  });
  return links;
}

/**
 * @param {ScheduleEntry} metadata
 * @param {string} markdown
 * @returns {Episode}
 */
export function parseEpisode(metadata, markdown) {
  const title = markdown.match(/^# (.+)$/m)?.[1]?.trim();
  const overview = section(markdown, 'Overview');
  if (!title || !overview) throw new Error(`Episode ${metadata.id} needs a title and Overview section.`);
  const recordings = sectionLinks(markdown, 'Recording');
  for (const { url: href } of recordings) {
    let valid = false;
    try {
      const url = new URL(href);
      valid = url.protocol === 'https:' && (
        (['www.youtube.com', 'youtube.com'].includes(url.hostname) && url.pathname === '/watch' && Boolean(url.searchParams.get('v')?.trim())) ||
        (url.hostname === 'youtu.be' && url.pathname.length > 1)
      );
    } catch {
      throw new Error(`Episode ${metadata.id} has an invalid recording URL: ${href}`);
    }
    if (!valid) throw new Error(`Episode ${metadata.id} needs a confirmed YouTube watch URL in Recording: ${href}`);
  }
  const recording = recordings[0]?.url ?? null;
  const hosts = sectionLinks(markdown, 'Hosts').map((link) => ({ ...link, url: resourceUrl(link.url, metadata.id) }));
  const guestResources = sectionLinks(markdown, 'Guest demos and resources').map((link) => ({ ...link, url: resourceUrl(link.url, metadata.id) }));
  const demos = [...section(markdown, 'Demos and resources').matchAll(/^\d+\.\s+\[([^\]]+)\]\((demos\/[^)]+\/README\.md)\):\s*(.+)$/gm)]
    .map(([, title, path, description]) => ({
      title,
      path,
      description,
      url: resourceUrl(path, metadata.id),
    }));
  const renderer = new Marked({
    walkTokens(token) {
      if (token.type === 'link' || token.type === 'image') {
        token.href = resourceUrl(token.href, metadata.id, token.type === 'image');
      }
    },
    renderer: {
      html({ text }) {
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
      },
    },
  });
  return {
    ...metadata,
    number: Number(metadata.id.slice(0, 2)),
    title,
    summary: overview.split(/\n\s*\n/)[0].replace(/\s+/g, ' ').split(/(?<=[.!?])\s+(?=[A-Z])/).slice(0, 2).join(' '),
    eventUrl: `https://developer.microsoft.com/en-us/reactor/events/${metadata.event}/`,
    sourceUrl: `${repository}/blob/main/episodes/${metadata.id}/README.md`,
    recording,
    recordings,
    hosts,
    guestResources,
    demos,
    html: renderer.parse(markdown.replace(/^# .+\r?\n/, '')),
  };
}

export const episodes = schedule.map((metadata) => {
  const root = resolve('episodes', metadata.id);
  const markdown = readFileSync(resolve(root, 'README.md'), 'utf8');
  const episode = parseEpisode(metadata, markdown);
  for (const demo of episode.demos) {
    if (!existsSync(resolve(root, demo.path))) {
      throw new Error(`Missing demo README: ${metadata.id}/${demo.path}`);
    }
  }
  return episode;
});

export function episodeStatus(episode, now = Date.now()) {
  return statusFor(episode.start, episode.end, episode.recording, now);
}

export const statusLabels = {
  upcoming: 'Upcoming',
  live: 'Live now',
  pending: 'Recording pending',
  recorded: 'Recording available',
};

export function calendar(episode) {
  const escape = (value) => value.replace(/\\/g, '\\\\').replace(/\r?\n/g, '\\n').replace(/;/g, '\\;').replace(/,/g, '\\,');
  const timestamp = (value) => value.replace(/[-:]/g, '');
  const fold = (line) => {
    let output = '';
    let length = 0;
    for (const character of line) {
      const bytes = Buffer.byteLength(character, 'utf8');
      if (length + bytes > 75) {
        output += '\r\n ';
        length = 1;
      }
      output += character;
      length += bytes;
    }
    return output;
  };
  return [
    'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//NETFoundry Better Together//Series//EN',
    'BEGIN:VEVENT', `UID:${episode.id}@netfoundry-bt`,
    'DTSTAMP:20261015T000000Z',
    `DTSTART:${timestamp(episode.start)}`, `DTEND:${timestamp(episode.end)}`,
    `SUMMARY:${escape(episode.title)}`,
    `DESCRIPTION:${escape(`Join Bruno Capuano and guests. Event and registration: ${episode.eventUrl}`)}`,
    `URL:${episode.eventUrl}`, 'END:VEVENT', 'END:VCALENDAR', '',
  ].map(fold).join('\r\n');
}
