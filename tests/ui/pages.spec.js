// Checks every main screen against the measurable parts of the UI Standard in AGENTS.md:
// browser console errors, horizontal overflow, form controls without a label, text smaller
// than 12px, and a full-page screenshot compared with the baseline.
//
// Ratchet: "npm run baseline" records the current finding counts per page and viewport next
// to the screenshots. A normal run fails only when a count goes UP compared with that record,
// so known problems do not hide new ones. Without a recorded baseline, any finding fails.
// Failures are soft, so one run reports every finding instead of stopping at the first one.
const fs = require('fs');
const path = require('path');
const { test, expect } = require('@playwright/test');
const { OUT, LOCAL_STATE, JIRA_STATE, INCLUDE_JIRA } = require('./settings');

const ANONYMOUS_PAGES = [
  { name: 'login', path: '/Identity/Account/Login' },
  { name: 'register', path: '/Identity/Account/Register' }
];

const LOCAL_PAGES = [
  { name: 'dashboard', path: '/dashboard.html' },
  { name: 'activity', path: '/activity.html' },
  { name: 'notes', path: '/notes.html' },
  { name: 'checklist', path: '/checklist.html' },
  { name: 'focus-timer', path: '/focus-timer.html' },
  { name: 'quick-links', path: '/quick-links.html' },
  { name: 'prompt-templates', path: '/prompt-templates.html' },
  { name: 'schema-compare', path: '/schema-compare.html' },
  { name: 'sql-profiler', path: '/sql-profiler.html' },
  { name: 'component', path: '/component.html' },
  { name: 'demo-kendo', path: '/demo-kendo.html' },
  { name: 'tabulator-demo', path: '/tabulator-demo.html' },
  { name: 'privacy', path: '/privacy.html' },
  { name: 'account-profile', path: '/Identity/Account/Manage' },
  { name: 'account-email', path: '/Identity/Account/Manage/Email' },
  { name: 'account-password', path: '/Identity/Account/Manage/ChangePassword' },
  { name: 'access-denied', path: '/jira-dashboard.html' }
];

const JIRA_PAGES = [
  { name: 'jira-dashboard', path: '/jira-dashboard.html' },
  { name: 'jira-worklist-search', path: '/jira-worklist.html' },
  { name: 'jira-worklist-board', path: '/jira-worklist.html', click: '#jira-tab-board' },
  { name: 'jira-worklist-auto', path: '/jira-worklist.html', click: '#jira-tab-auto' }
];

// Elements whose content changes with the clock or the weather service.
const VOLATILE = '[data-current-time], [data-current-date], [data-current-weather]';

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(value, null, 2));
}

function fixedLocalMorning() {
  // Keep today's date (the Jira sample data is relative to today) but pin the hour, so the
  // time-of-day background is the same on every run.
  const now = new Date();
  now.setHours(10, 0, 0, 0);
  return now;
}

async function collectFindings(page) {
  return page.evaluate(() => {
    const isVisible = (el) => {
      const style = getComputedStyle(el);
      return style.visibility !== 'hidden' && style.display !== 'none' && el.getClientRects().length > 0;
    };
    const describe = (el) => {
      const id = el.id ? `#${el.id}` : '';
      const cls = el.classList.length ? `.${[...el.classList].slice(0, 2).join('.')}` : '';
      const name = el.getAttribute('name') ? `[name="${el.getAttribute('name')}"]` : '';
      return `${el.tagName.toLowerCase()}${id}${cls}${name}`;
    };

    const doc = document.documentElement;
    const overflowX = Math.max(0, doc.scrollWidth - doc.clientWidth);

    const skipTypes = new Set(['hidden', 'submit', 'button', 'reset', 'image']);
    const unlabeled = [...document.querySelectorAll('input, select, textarea')]
      .filter((el) => !skipTypes.has((el.getAttribute('type') || '').toLowerCase()))
      .filter(isVisible)
      .filter((el) => !(el.labels && el.labels.length)
        && !el.getAttribute('aria-label')
        && !el.getAttribute('aria-labelledby')
        && !el.getAttribute('title'))
      .map(describe);

    const smallText = [];
    for (const el of document.body.querySelectorAll('*')) {
      const ownText = [...el.childNodes]
        .filter((n) => n.nodeType === Node.TEXT_NODE)
        .map((n) => n.textContent.trim())
        .join(' ')
        .trim();
      if (!ownText || !isVisible(el)) continue;
      const size = parseFloat(getComputedStyle(el).fontSize);
      if (size < 12) smallText.push(`${describe(el)} ${size}px "${ownText.slice(0, 40)}"`);
    }

    return { overflowX, unlabeled, smallText };
  });
}

function definePages(pages, storageState) {
  for (const target of pages) {
    test(target.name, async ({ page, baseURL }, testInfo) => {
      const theme = testInfo.project.metadata.theme || 'light';
      const consoleErrors = [];
      page.on('pageerror', (error) => consoleErrors.push(`pageerror: ${error.message}`));
      page.on('console', (message) => {
        if (message.type() !== 'error') return;
        const source = message.location().url || '';
        // Failures of external services (for example the weather API) are environment noise.
        if (source && baseURL && !source.startsWith(baseURL)) return;
        consoleErrors.push(`console: ${message.text()}`);
      });

      await page.clock.setFixedTime(fixedLocalMorning());
      await page.addInitScript((value) => {
        try {
          localStorage.setItem('toolbox.theme', value);
          localStorage.setItem('toolbox.sidebar', 'expanded');
        } catch { /* storage can be blocked; the page must still render */ }
      }, theme);

      await page.goto(target.path, { waitUntil: 'networkidle' });
      if (target.click) {
        await page.click(target.click);
        await page.waitForLoadState('networkidle');
      }
      await page.waitForTimeout(800);

      const findings = await collectFindings(page);
      const result = { page: target.name, url: page.url(), project: testInfo.project.name, consoleErrors, ...findings };
      const counts = {
        consoleErrors: consoleErrors.length,
        overflowX: findings.overflowX > 1 ? findings.overflowX : 0,
        unlabeled: findings.unlabeled.length,
        smallText: findings.smallText.length
      };
      writeJson(path.join(OUT, 'findings', testInfo.project.name, `${target.name}.json`), { ...result, counts });
      await testInfo.attach('findings', { body: JSON.stringify(result, null, 2), contentType: 'application/json' });

      const baselineFile = path.join(OUT, 'baseline', testInfo.project.name, `${target.name}.findings.json`);
      const updating = ['all', 'changed'].includes(testInfo.config.updateSnapshots);
      if (updating) {
        writeJson(baselineFile, counts);
      }
      const allowed = fs.existsSync(baselineFile)
        ? JSON.parse(fs.readFileSync(baselineFile, 'utf8'))
        : { consoleErrors: 0, overflowX: 0, unlabeled: 0, smallText: 0 };

      expect.soft(counts.consoleErrors, `browser console errors (baseline ${allowed.consoleErrors}): ${consoleErrors.slice(0, 3).join(' | ')}`).toBeLessThanOrEqual(allowed.consoleErrors);
      expect.soft(counts.overflowX, `horizontal overflow px (baseline ${allowed.overflowX})`).toBeLessThanOrEqual(allowed.overflowX);
      expect.soft(counts.unlabeled, `form controls without a label (baseline ${allowed.unlabeled}): ${findings.unlabeled.slice(0, 5).join(' | ')}`).toBeLessThanOrEqual(allowed.unlabeled);
      expect.soft(counts.smallText, `text smaller than 12px (baseline ${allowed.smallText}): ${findings.smallText.slice(0, 5).join(' | ')}`).toBeLessThanOrEqual(allowed.smallText);
      await expect.soft(page).toHaveScreenshot(`${target.name}.png`, { fullPage: true, mask: [page.locator(VOLATILE)] });
    });
  }
}

test.describe('anonymous', () => {
  test.use({ storageState: { cookies: [], origins: [] } });
  definePages(ANONYMOUS_PAGES);
});

test.describe('local account', () => {
  test.use({ storageState: LOCAL_STATE });
  definePages(LOCAL_PAGES);
});

test.describe('jira account (fake data)', () => {
  test.skip(!INCLUDE_JIRA, 'Set TBX_UI_JIRA=1 and run the app with Jira__UseFakeData=true to include Jira screens.');
  test.use({ storageState: INCLUDE_JIRA ? JIRA_STATE : { cookies: [], origins: [] } });
  definePages(JIRA_PAGES);
});
