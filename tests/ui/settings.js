const os = require('os');
const path = require('path');

const OUT = process.env.TBX_UI_OUT || path.join(os.tmpdir(), 'toolboxweb-ui');

module.exports = {
  OUT,
  LOCAL_STATE: path.join(OUT, 'state-local.json'),
  JIRA_STATE: path.join(OUT, 'state-jira.json'),
  EMAIL: process.env.TBX_UI_EMAIL || 'ui.check@example.local',
  PASSWORD: process.env.TBX_UI_PASSWORD || 'UiCheck@12345',
  // Set TBX_UI_JIRA=1 when the app runs with Jira__UseFakeData=true (Development only).
  INCLUDE_JIRA: process.env.TBX_UI_JIRA === '1' || process.env.TBX_UI_MODE === 'web',
  // Set TBX_UI_MODE=web when the app runs with App__Mode=Web (Jira sign-in only, no database).
  // Use another TBX_UI_OUT for it, so its baseline stays apart from the server one.
  WEB_MODE: process.env.TBX_UI_MODE === 'web',
  // Set TBX_UI_FILES_ROOT to a folder when the app allows the check account to use "Files &
  // folders" (Files__AllowedUsers__0, Files__AllowedFolders__0): the folder is added as a root and
  // the page is checked. Without it the Files page is skipped.
  FILES_ROOT: process.env.TBX_UI_FILES_ROOT || ''
};
