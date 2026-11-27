// Signs in once and stores the browser state for the checks:
// - a local ToolboxWeb account (registered on first run), with a little sample data;
// - optionally a Jira session, which needs the app to run with Jira__UseFakeData=true.
const fs = require('fs');
const { chromium } = require('@playwright/test');
const { OUT, LOCAL_STATE, JIRA_STATE, EMAIL, PASSWORD, INCLUDE_JIRA, WEB_MODE, FILES_ROOT } = require('./settings');

async function signInLocal(page, baseURL) {
  await page.goto(`${baseURL}/Identity/Account/Login`);
  await page.fill('#local-email', EMAIL);
  await page.fill('#local-password', PASSWORD);
  await Promise.all([page.waitForLoadState('networkidle'), page.click('[data-login-panel="local"] button[type="submit"]')]);
  return !page.url().includes('/Account/Login');
}

async function register(page, baseURL) {
  await page.goto(`${baseURL}/Identity/Account/Register`);
  await page.fill('#Input_Email', EMAIL);
  await page.fill('#Input_Password', PASSWORD);
  await page.fill('#Input_ConfirmPassword', PASSWORD);
  await Promise.all([page.waitForLoadState('networkidle'), page.click('form button[type="submit"].auth-login-submit')]);
  // Registration signs the new user in and leaves the Identity pages.
  return !page.url().includes('/Identity/Account/');
}

async function seedSampleData(page, baseURL) {
  await page.goto(`${baseURL}/checklist.html`);
  if (await page.locator('.tbx-list .tbx-row').count() === 0) {
    // The to-do form sits in a modal (list page template).
    for (const title of ['Review PR schema compare', 'Chuẩn bị báo cáo tuần', 'Fix bug login Jira']) {
      await page.click('[data-list-new]');
      await page.fill('#todo-title', title);
      await Promise.all([page.waitForNavigation(), page.click('[data-todo-form-submit]')]);
    }
  }
  await page.goto(`${baseURL}/quick-links.html`);
  if (await page.locator('[data-link-row]').count() === 0) {
    // The link form sits in a modal too.
    for (const [title, url, group] of [['Jira', 'https://jira.example.local', 'Work'], ['GitHub', 'https://github.com', 'Dev']]) {
      await page.click('[data-list-new]');
      await page.fill('#link-url', url);
      await page.fill('#link-title', title);
      await page.fill('#link-group', group);
      await Promise.all([page.waitForNavigation(), page.click('[data-link-form-submit]')]);
    }
  }
}

// "Files & folders": adds FILES_ROOT as a root (once) and opens one file so the page has content.
async function addFilesRoot(page, baseURL) {
  await page.goto(`${baseURL}/files.html`, { waitUntil: 'networkidle' });
  const added = await page.evaluate(async (folder) => {
    const config = JSON.parse(document.getElementById('files-config')?.textContent || 'null');
    if (!config) return 'no access';
    const roots = await (await fetch(config.urls.roots)).json();
    if (roots.data.some((r) => r.path === folder)) return 'present';
    const answer = await (await fetch(config.urls.addRoot, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: config.token },
      body: JSON.stringify({ path: folder })
    })).json();
    return answer.success ? 'added' : answer.message;
  }, FILES_ROOT);
  if (added !== 'added' && added !== 'present') {
    throw new Error(`Could not add ${FILES_ROOT} as a root for the Files page: ${added}`);
  }

  // Open the first Markdown file: the page remembers its tabs in this browser, so every
  // viewport (the phone one too, where the tree is folded away) shows it.
  await page.reload({ waitUntil: 'networkidle' });
  await page.locator('.fx-tree [role="treeitem"] .fx-node-name', { hasText: /\.md$/ }).first().click();
  await page.waitForSelector('.fx-md');
  await page.waitForTimeout(500);
}

async function signInJira(page, baseURL) {
  await page.goto(`${baseURL}/Identity/Account/Login`);
  // The web copy shows the Jira panel only, without the switch.
  if (await page.locator('[data-login-switch="jira"]').count()) {
    await page.click('[data-login-switch="jira"]');
  }
  await page.fill('#jira-base-url', 'https://jira.fake.local');
  await page.fill('#jira-username', 'ui.check');
  await page.fill('#jira-password', 'not-used');
  await Promise.all([page.waitForLoadState('networkidle'), page.click('[data-login-panel="jira"] button[type="submit"]')]);
  if (page.url().includes('/Account/Login')) {
    throw new Error('Jira sign-in failed. Start the app with ASPNETCORE_ENVIRONMENT=Development and Jira__UseFakeData=true, or unset TBX_UI_JIRA.');
  }
}

module.exports = async (config) => {
  const baseURL = config.projects[0].use.baseURL;
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  try {
    // The web copy has no local accounts (App__Mode=Web): only the Jira sign-in is prepared.
    if (!WEB_MODE) {
      const local = await browser.newContext({ locale: 'vi-VN' });
      // Sample data is entered on plain pages, not inside the kept-screens shell.
      await local.addCookies([{ name: 'toolbox.keepAlive', value: 'off', url: baseURL }]);
      const page = await local.newPage();
      if (!(await signInLocal(page, baseURL)) && !(await register(page, baseURL))) {
        throw new Error(`Could not sign in or register ${EMAIL} at ${baseURL}. Is the app running?`);
      }
      await seedSampleData(page, baseURL);
      if (FILES_ROOT) {
        await addFilesRoot(page, baseURL);
      }
      await local.storageState({ path: LOCAL_STATE });
    }

    if (INCLUDE_JIRA) {
      const jira = await browser.newContext({ locale: 'vi-VN' });
      await jira.addCookies([{ name: 'toolbox.keepAlive', value: 'off', url: baseURL }]);
      await signInJira(await jira.newPage(), baseURL);
      await jira.storageState({ path: JIRA_STATE });
    }
  } finally {
    await browser.close();
  }
};
