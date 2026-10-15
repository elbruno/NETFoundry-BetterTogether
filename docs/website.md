# Series website

The audience website uses Astro static output on GitHub Pages. Only `dist/` is
published, not the repository checkout, demo configuration, or private planning.
No private repository is needed to build it.

## Content updates

- Keep each episode's title, overview, recording, demos, and resources in its
  public `episodes/NN-YYYY-MM-DD/README.md`. The site renders these guides
  directly and resolves relative resource links back to GitHub.
- Use `## Overview`, `## Recording`, and `## Demos and resources` headings.
  List demos as `1. [Demo title](demos/NN-demo-name/README.md): description`.
  Every linked demo README must exist or the build fails.
- Once confirmed, put the .NET Channel recording link under `## Recording`
  as `[Watch on the .NET Channel](https://www.youtube.com/watch?v=ACTUAL_VIDEO_ID)`,
  replacing the example with the actual watch URL. Do not add the placeholder.
  A channel homepage is not a recording. Keep the unavailable message until
  a real recording is confirmed.
- Add or update schedule entries in `src/lib/episodes.mjs`. Dates and event
  links must be checked against Reactor. Store timestamps in UTC, accounting
  for daylight saving time. Topics must not be invented for future episodes.
- Keep the root README as the repository's concise episode index.
- The first episode's learning shortcuts use demos 01, 02, 06, and 07 in order.
  Update `src/pages/index.astro` deliberately if the learning path changes.

Rendering escapes raw HTML in Markdown. Use Markdown syntax rather than embedded
HTML for guide content. Site links continue to work under the repository's
GitHub Pages base path. Existing episode folders and GitHub links stay unchanged.

## Local development

From the repository root, using Node.js 22.12 or later:

```powershell
npm ci
npm test
npm run check
npm run build
npm run preview
```

For browser checks on Linux or macOS, run `npx playwright install chromium` once and then
`npm run test:browser` after a build. On Windows the tests use installed Microsoft
Edge; CI uses Playwright Chromium. Tests cover theme persistence, OS theme
changes, status transitions, filters, calendar downloads, responsive layout,
storage restrictions, and reading without JavaScript.

Open the printed preview origin at `/NETFoundry-BetterTogether/`. For hot reload,
use `npm run dev`; restart the server after editing source episode READMEs or
scheduling metadata. The website does not start or modify any .NET demos.

The initial dependency versions are Astro 6.4.8 and marked 18.1.0. The committed
lockfile controls installation. The site imports the existing public series banner;
it does not copy unpublished promotional artwork from private preparation.

## Hosting and publishing

The expected URL is https://elbruno.github.io/NETFoundry-BetterTogether/.
In repository **Settings > Pages**, the source must be **GitHub Actions**.
The `Series website` workflow checks pull requests without deploying them.
Changes pushed to `main` affecting the site or episode READMEs build and deploy
the website. You can also run it manually from Actions.

Publishing still requires an authorized commit and push. Local changes are not
live until the workflow completes successfully. If the first deployment requests
approval for the `github-pages` environment, a maintainer must approve it.
GitHub Pages deployment permissions are limited to the deploy job. No deployment
secrets, custom domain, paid hosting, or Azure resources are required.

To stop hosting, disable Pages in repository settings. To undo a content change,
restore the affected website content through a normal reviewed commit and deploy.
Do not delete shared resources or change repository visibility.

## Browser behavior

System is the default theme. Light or Dark overrides are stored locally when
browser storage is available. System mode tracks OS changes; the optional
`scoutTheme=light` or `scoutTheme=dark` query parameter supports previews.
If storage is blocked, the theme still works for the current page and a browser
console warning explains that it cannot be saved.

Dates are shown in the viewer's timezone with UTC in the tooltip; without
JavaScript, readable UTC dates and all episode content remain available.
The episode states update in the browser, so reaching a scheduled end time
never implies that a recording exists. Calendar files use UTC and actual
confirmed event URLs. There is no analytics, autoplay, third-party font request,
or embedded video tracking. Recording buttons open the confirmed YouTube page.
