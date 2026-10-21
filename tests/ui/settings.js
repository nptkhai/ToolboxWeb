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
  INCLUDE_JIRA: process.env.TBX_UI_JIRA === '1'
};
