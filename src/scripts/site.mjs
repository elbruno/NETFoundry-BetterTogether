export function statusFor(start, end, recording, now = Date.now()) {
  if (recording) return 'recorded';
  if (now < Date.parse(start)) return 'upcoming';
  if (now < Date.parse(end)) return 'live';
  return 'pending';
}

export function initializeSite() {
  const root = document.documentElement;
  const media = matchMedia('(prefers-color-scheme: dark)');
  const select = document.querySelector('#theme');
  const applyTheme = (preference) => {
    root.dataset.themePreference = preference;
    root.dataset.theme = preference === 'system' ? (media.matches ? 'dark' : 'light') : preference;
  };
  if (select instanceof HTMLSelectElement) {
    select.value = root.dataset.themePreference ?? 'system';
    select.closest('.theme-control').hidden = false;
    select.addEventListener('change', () => {
      applyTheme(select.value);
      try {
        localStorage.setItem('netfoundry-theme', select.value);
      } catch {
        console.warn('Theme preference cannot be saved; it will apply to this page only.');
      }
    });
    media.addEventListener('change', () => {
      if (root.dataset.themePreference === 'system') applyTheme('system');
    });
    window.addEventListener('storage', (event) => {
      if (event.key === 'netfoundry-theme') {
        const preference = ['light', 'dark', 'system'].includes(event.newValue) ? event.newValue : 'system';
        select.value = preference;
        applyTheme(preference);
      }
    });
  }

  const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
  const timezone = formatter.resolvedOptions().timeZone;
  document.querySelectorAll('[data-local-date]').forEach((element) => {
    element.textContent = `${formatter.format(new Date(element.dateTime))} (${timezone})`;
    element.title = `${new Date(element.dateTime).toUTCString()}`;
  });
  document.querySelectorAll('[data-timezone]').forEach((element) => { element.textContent = `Your timezone: ${timezone}.`; });

  const cards = [...document.querySelectorAll('[data-episode]')];
  const filters = document.querySelector('#episode-filters');
  const labels = { upcoming: 'Upcoming', live: 'Live now', pending: 'Recording pending', recorded: 'Recording available' };
  let filter = 'all';
  function updateEpisodes() {
    let count = 0;
    cards.forEach((card) => {
      const status = statusFor(card.dataset.start, card.dataset.end, card.dataset.recording);
      card.dataset.state = status;
      card.querySelector('[data-status]').textContent = labels[status];
      card.hidden = filter !== 'all' && !(filter === 'upcoming' ? ['upcoming', 'live'].includes(status) : status === 'recorded');
      if (!card.hidden) count++;
    });
    const result = document.querySelector('#filter-result');
    if (result) result.textContent = `${count} ${count === 1 ? 'episode' : 'episodes'} shown.`;
    const empty = document.querySelector('#empty-state');
    if (empty) empty.hidden = count > 0;
    const spotlight = document.querySelector('#spotlight');
    const next = cards.find((card) => ['upcoming', 'live'].includes(card.dataset.state));
    if (spotlight && next) {
      spotlight.hidden = false;
      document.querySelector('#spotlight-label').textContent = next.dataset.state === 'live' ? 'LIVE NOW' : 'UP NEXT';
      document.querySelector('#spotlight-title').textContent = next.dataset.title;
      document.querySelector('#spotlight-date').textContent = `${formatter.format(new Date(next.dataset.start))} (${timezone})`;
      const event = document.querySelector('#spotlight-event');
      event.href = next.dataset.event;
      event.textContent = next.dataset.state === 'live' ? 'Open the live event' : 'Register on Reactor';
      document.querySelector('#spotlight-calendar').href = next.dataset.calendar;
    } else if (spotlight) {
      spotlight.hidden = true;
    }
  }
  if (cards.length) {
    filters.hidden = false;
    filters.addEventListener('click', (event) => {
      const button = event.target.closest('button[data-filter]');
      if (!button) return;
      filter = button.dataset.filter;
      filters.querySelectorAll('button').forEach((element) => element.setAttribute('aria-pressed', String(element === button)));
      updateEpisodes();
    });
    updateEpisodes();
    window.setInterval(updateEpisodes, 30_000);
  }

  document.querySelectorAll('.prose pre').forEach((pre) => {
    if (!navigator.clipboard?.writeText) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'copy-button';
    button.textContent = 'Copy';
    button.setAttribute('aria-label', 'Copy code');
    button.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(pre.querySelector('code')?.textContent ?? '');
        button.textContent = 'Copied';
      } catch {
        button.textContent = 'Copy failed';
        console.warn('Clipboard access was denied. Select and copy the code manually.');
      }
      window.setTimeout(() => { button.textContent = 'Copy'; }, 2000);
    });
    pre.append(button);
  });
}
