// UI check for ToolboxWeb. Everything it writes (screenshots, baseline, report, login state)
// goes to TBX_UI_OUT, which defaults to a folder in the OS temp directory, never into the repo.
const os = require('os');
const path = require('path');
const { defineConfig } = require('@playwright/test');

const OUT = process.env.TBX_UI_OUT || path.join(os.tmpdir(), 'toolboxweb-ui');

module.exports = defineConfig({
  testDir: __dirname,
  testMatch: '*.spec.js',
  outputDir: path.join(OUT, 'results'),
  snapshotPathTemplate: path.join(OUT, 'baseline', '{projectName}', '{arg}{ext}'),
  globalSetup: require.resolve('./global-setup.js'),
  timeout: 90_000,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: path.join(OUT, 'report'), open: 'never' }]],
  expect: {
    toHaveScreenshot: { maxDiffPixelRatio: 0.02, animations: 'disabled', caret: 'hide' }
  },
  use: {
    baseURL: process.env.TBX_BASE_URL || 'http://localhost:5080',
    locale: 'vi-VN',
    timezoneId: process.env.TBX_UI_TIMEZONE || 'Asia/Ho_Chi_Minh',
    ignoreHTTPSErrors: true
  },
  projects: [
    { name: 'desktop-light', use: { viewport: { width: 1440, height: 900 } }, metadata: { theme: 'light' } },
    { name: 'desktop-dark', use: { viewport: { width: 1440, height: 900 } }, metadata: { theme: 'dark' } },
    { name: 'mobile-light', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }, metadata: { theme: 'light' } }
  ]
});
