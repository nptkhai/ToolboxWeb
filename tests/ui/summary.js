// Prints a Markdown summary of the last run's findings (one row per page and viewport).
// Usage: node summary.js [> summary.md]
const fs = require('fs');
const path = require('path');
const { OUT } = require('./settings');

const root = path.join(OUT, 'findings');
if (!fs.existsSync(root)) {
  console.error(`No findings in ${root}. Run "npm run check" first.`);
  process.exit(1);
}

const rows = [];
for (const project of fs.readdirSync(root).sort()) {
  for (const file of fs.readdirSync(path.join(root, project)).sort()) {
    const f = JSON.parse(fs.readFileSync(path.join(root, project, file), 'utf8'));
    rows.push(`| ${f.page} | ${project} | ${f.consoleErrors.length} | ${f.overflowX > 1 ? f.overflowX : 0} | ${f.unlabeled.length} | ${f.smallText.length} |`);
  }
}

console.log('| Page | Viewport | Console errors | Overflow (px) | Unlabeled controls | Text < 12px |');
console.log('|---|---|---|---|---|---|');
console.log(rows.join('\n'));
