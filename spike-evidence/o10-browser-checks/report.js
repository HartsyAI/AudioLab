// Builds results.md (per-check pass/fail table and axe summary) from the per-browser JSON files.
const fs = require('fs');
const names = ['chromium', 'webkit', 'firefox'];
const data = names.map(n => JSON.parse(fs.readFileSync(`${__dirname}/${n}.json`, 'utf8')));
const meta = data[0].meta;
const lines = [];
lines.push('# O10 proof page: keyboard, focus and axe-core results', '');
lines.push(`Machine: ${meta.os}, ${meta.cpu}, ${meta.cores} cores, ${meta.ramGB} GB. Node ${meta.node}, Playwright ${meta.playwright}, axe-core ${meta.axe}.`);
lines.push('Browsers: ' + data.map(d => `${d.results[0].name} ${d.results[0].version}`).join(', ') + '.', '');
const titles = data[0].results[0].steps.map(s => s.title);
lines.push('| Check | ' + names.join(' | ') + ' |', '|---|' + names.map(() => '---|').join(''));
for (const t of titles) {
  const cells = data.map(d => { const s = d.results[0].steps.find(x => x.title === t); return !s ? 'n/a' : s.ok ? 'pass' : 'FAIL'; });
  lines.push(`| ${t} | ${cells.join(' | ')} |`);
}
lines.push('', '## Failures', '');
data.forEach(d => d.results[0].steps.filter(s => !s.ok).forEach(s => lines.push(`- ${d.results[0].name}: ${s.title}: ${s.detail.replace(/\s+/g, ' ')}`)));
lines.push('', '## axe-core (WCAG 2.0 to 2.2 A and AA)', '', '| State | ' + names.join(' | ') + ' |', '|---|' + names.map(() => '---|').join(''));
const labels = data[0].results[0].axe.map(a => a.label);
for (const l of labels) {
  const cells = data.map(d => { const a = d.results[0].axe.find(x => x.label === l); return !a ? 'n/a' : `${a.violations.length} violation(s)${a.violations.length ? ' (' + a.violations.map(v => v.id + ' x' + v.nodes.length).join(', ') + ')' : ''}, ${a.incomplete.length} incomplete`; });
  lines.push(`| ${l} | ${cells.join(' | ')} |`);
}
lines.push('', '## Observations', '');
data.forEach(d => (d.results[0].notes || []).forEach(n => lines.push(`- ${d.results[0].name}: ${n}`)));
fs.writeFileSync(`${__dirname}/results.md`, lines.join('\n') + '\n');
console.log(lines.join('\n'));
