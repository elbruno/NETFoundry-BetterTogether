import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/browser',
  fullyParallel: true,
  use: {
    baseURL: 'http://127.0.0.1:4325/NETFoundry-BetterTogether/',
    browserName: 'chromium',
    channel: process.platform === 'win32' ? 'msedge' : undefined,
    viewport: { width: 1440, height: 1000 },
  },
  webServer: {
    command: 'npm run preview -- --host 127.0.0.1 --port 4325',
    url: 'http://127.0.0.1:4325/NETFoundry-BetterTogether/',
    reuseExistingServer: !process.env.CI,
  },
});
