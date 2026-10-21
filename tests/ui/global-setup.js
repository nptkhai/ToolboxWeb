// Signs in once and stores the browser state for the checks:
// - a local ToolboxWeb account (registered on first run), with a little sample data;
// - optionally a Jira session, which needs the app to run with Jira__UseFakeData=true.
const fs = require('fs');
const { chromium } = require('@playwright/test');
const { OUT, LOCAL_STATE, JIRA_STATE, EMAIL, PASSWORD, INCLUDE_JIRA } = require('./settings');

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
  await Promise.all([page.waitForLoadState('networkidle'), page.click('#registerSubmit')]);
  // Registration signs the new user in and leaves the Identity pages.
  return !page.url().includes('/Identity/Account/');
}

async function seedSampleData(page, baseURL) {
  await page.goto(`${baseURL}/checklist.html`);
  if (await page.locator('.item-row').count() === 0) {
    for (const title of ['Review PR schema compare', 'Chuẩn bị báo cáo tuần', 'Fix bug login Jira']) {
      await page.fill('input[name="Title"]', title);
      await Promise.all([page.waitForLoadState('networkidle'), page.click('form[action*="Create"] button[type="submit"]')]);
    }
  }
  await page.goto(`${baseURL}/quick-links.html`);
  if (await page.locator('.item-row').count() === 0) {
    for (const [title, url, group] of [['Jira', 'https://jira.example.local', 'Work'], ['GitHub', 'https://github.com', 'Dev']]) {
      await page.fill('input[name="Title"]', title);
      await page.fill('input[name="Url"]', url);
      await page.fill('input[name="GroupName"]', group);
      await Promise.all([page.waitForLoadState('networkidle'), page.click('form[action*="Create"] button[type="submit"]')]);
    }
  }
}

async function signInJira(page, baseURL) {
  await page.goto(`${baseURL}/Identity/Account/Login`);
  await page.click('[data-login-switch="jira"]');
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
    const local = await browser.newContext({ locale: 'vi-VN' });
    const page = await local.newPage();
    if (!(await signInLocal(page, baseURL)) && !(await register(page, baseURL))) {
      throw new Error(`Could not sign in or register ${EMAIL} at ${baseURL}. Is the app running?`);
    }
    await seedSampleData(page, baseURL);
    await local.storageState({ path: LOCAL_STATE });

    if (INCLUDE_JIRA) {
      const jira = await browser.newContext({ locale: 'vi-VN' });
      await signInJira(await jira.newPage(), baseURL);
      await jira.storageState({ path: JIRA_STATE });
    }
  } finally {
    await browser.close();
  }
};
