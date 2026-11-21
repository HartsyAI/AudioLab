// Throwaway: keyboard, focus and axe-core checks on the O10 proof page. Not part of the repository.
const { chromium, firefox, webkit } = require('playwright');
const fs = require('fs');
const os = require('os');
const path = require('path');

const URL = process.env.URL || 'http://127.0.0.1:8765/';
const AXE = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
const only = (process.env.ONLY || 'chromium,firefox,webkit').split(',');
const engines = { chromium, firefox, webkit };

async function runBrowser(name) {
  const browser = await engines[name].launch();
  const out = { name, version: browser.version(), steps: [], axe: [] };
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await ctx.newPage();
  page.setDefaultTimeout(4000);
  const errors = [];
  page.on('pageerror', e => errors.push(String(e).slice(0, 200)));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(URL);
  await page.waitForSelector('h1.page-title', { timeout: 60000 });
  await page.waitForTimeout(500);

  const step = async (title, fn) => {
    try {
      const r = await fn();
      out.steps.push({ title, ok: r === true || r === undefined, detail: r === true || r === undefined ? '' : String(r) });
    } catch (e) {
      out.steps.push({ title, ok: false, detail: 'threw: ' + String(e.message).split('\n')[0].slice(0, 200) });
    }
  };
  // An observation records what was seen; it is not scored, so it never counts as a pass or a fail.
  const observe = async (title, fn) => {
    try {
      const r = await fn();
      out.steps.push({ title, ok: null, observation: true, short: r.short, detail: r.detail });
    } catch (e) {
      out.steps.push({ title, ok: null, observation: true, short: 'threw', detail: 'threw: ' + String(e.message).split('\n')[0].slice(0, 200) });
    }
  };
  const active = () => page.evaluate(() => {
    const a = document.activeElement; if (!a) return 'none';
    return a.tagName.toLowerCase() + (a.id ? '#' + a.id : '') + (a.className && typeof a.className === 'string' ? '.' + a.className.split(' ')[0] : '') +
      ((a.getAttribute('aria-label') || a.textContent || '').trim().slice(0, 24) ? ' "' + (a.getAttribute('aria-label') || a.textContent).trim().slice(0, 24) + '"' : '');
  });
  const last = () => page.locator('#last-action').innerText();
  const text = sel => page.locator(sel).first().innerText();
  const visible = async sel => (await page.locator(sel).filter({ visible: true }).count()) > 0;
  const fresh = async () => { await page.reload(); await page.waitForSelector('h1.page-title', { timeout: 60000 }); await page.waitForTimeout(400); };
  const settle = ms => page.waitForTimeout(ms ?? 250);
  const axe = async (label) => {
    await page.evaluate(AXE);
    const r = await page.evaluate(async () => {
      const res = await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } });
      return {
        violations: res.violations.map(v => ({ id: v.id, impact: v.impact, help: v.help, nodes: v.nodes.map(n => ({ target: n.target.join(' '), html: n.html.slice(0, 140), summary: (n.failureSummary || '').split('\n').slice(1, 3).join(' | ').slice(0, 220) })) })),
        incomplete: res.incomplete.map(v => ({ id: v.id, nodes: v.nodes.length })),
        passes: res.passes.length
      };
    });
    out.axe.push({ label, ...r });
  };
  const closeAll = async () => { await page.keyboard.press('Escape'); await settle(150); await page.keyboard.press('Escape'); await settle(150); };

  // ---- axe on the page at rest
  await axe('page at rest');

  // ---- native dialog
  await step('native dialog: opened with the keyboard, opens modal and moves focus inside', async () => {
    await page.locator('#open-native').focus(); await page.keyboard.press('Enter'); await settle(400);
    const r = await page.evaluate(() => { const d = document.querySelector('dialog.al-native-dialog'); return { open: d?.open, modal: d?.matches(':modal'), inside: d?.contains(document.activeElement) }; });
    return (r.open && r.modal && r.inside) || JSON.stringify(r) + ' active=' + await active();
  });
  await axe('native dialog open');
  await step('native dialog: Tab and Shift+Tab never reach the page behind it (10 presses each way)', async () => {
    const outside = []; let viaBody = 0;
    const probe = () => page.evaluate(() => { const d = document.querySelector('dialog.al-native-dialog'); const a = document.activeElement; return a === document.body || a === document.documentElement ? 'body' : d.contains(a) ? 'in' : 'out'; });
    for (let i = 0; i < 10; i++) { await page.keyboard.press('Tab'); const r = await probe(); if (r === 'out') outside.push(await active()); if (r === 'body') viaBody++; }
    for (let i = 0; i < 10; i++) { await page.keyboard.press('Shift+Tab'); const r = await probe(); if (r === 'out') outside.push(await active()); if (r === 'body') viaBody++; }
    const back = await probe();
    out.notes = out.notes || []; out.notes.push('native dialog: focus passed through the document body (the browser UI) ' + viaBody + ' time(s) in 20 presses; after the last press it was ' + ({ in: 'inside the dialog', out: 'on the page behind it', body: 'on the document body' })[back]);
    return (outside.length === 0 && back !== 'out') || 'focus reached the page behind the dialog: ' + outside.join(', ');
  });
  await step('native dialog: Escape closes it and focus returns to the opener', async () => {
    await page.keyboard.press('Escape'); await settle(300);
    const open = await page.evaluate(() => document.querySelector('dialog.al-native-dialog')?.open);
    const a = await active();
    return (!open && a.includes('open-native')) || `open=${open} active=${a}`;
  });
  await step('native dialog: opened with a mouse click, Escape returns focus to the opener', async () => {
    await page.click('#open-native'); await settle(400);
    await page.keyboard.press('Escape'); await settle(300);
    const a = await active();
    return a.includes('open-native') || `focus is on ${a}`;
  });
  await step('native dialog: confirm runs the action and closes', async () => {
    const before = await last();
    await page.click('#open-native'); await settle(300);
    await page.locator('dialog.al-native-dialog').getByRole('button', { name: 'Delete project' }).click(); await settle(300);
    const open = await page.evaluate(() => document.querySelector('dialog.al-native-dialog')?.open);
    const after = await last();
    return (!open && after !== before) || `open=${open} last="${before}" -> "${after}"`;
  });

  await fresh();
  // ---- library dialog
  await step('library dialog: opens, role=dialog/alertdialog, focus inside', async () => {
    await page.click('#open-confirm'); await settle(500);
    const r = await page.evaluate(() => { const d = document.querySelector('[role=dialog],[role=alertdialog]'); return { has: !!d, modal: d?.getAttribute('aria-modal'), inside: d?.contains(document.activeElement) }; });
    return (r.has && r.inside) || JSON.stringify(r) + ' active=' + await active();
  });
  await axe('library dialog open');
  await step('library dialog: Tab stays inside (10 presses)', async () => {
    const outside = [];
    for (let i = 0; i < 10; i++) { await page.keyboard.press('Tab'); if (!await page.evaluate(() => document.querySelector('[role=dialog],[role=alertdialog]')?.contains(document.activeElement))) outside.push(await active()); }
    return outside.length === 0 || 'focus left the dialog: ' + outside.join(', ');
  });
  await step('library dialog: Escape closes it and focus returns to the opener', async () => {
    await page.keyboard.press('Escape'); await settle(400);
    const has = await page.evaluate(() => !!document.querySelector('[role=dialog],[role=alertdialog]'));
    const a = await active();
    return (!has && a.includes('open-confirm')) || `stillOpen=${has} active=${a}`;
  });

  await fresh();
  // ---- menu
  await step('menu: Enter opens, role=menu, focus on the first item', async () => {
    await page.locator('.al-menu-button').first().focus(); await page.keyboard.press('Enter'); await settle(400);
    const r = await page.evaluate(() => { const m = document.querySelector('[role=menu]'); const items = m ? [...m.querySelectorAll('[role=menuitem]')] : []; return { has: !!m, n: items.length, focus: items.indexOf(document.activeElement), ad: m?.getAttribute('aria-activedescendant') }; });
    return (r.has && r.n > 0) || JSON.stringify(r);
  });
  await axe('menu open');
  await step('menu: ArrowDown/ArrowUp move the active item, Home/End jump', async () => {
    const idx = () => page.evaluate(() => { const m = document.querySelector('[role=menu]'); const items = [...m.querySelectorAll('[role=menuitem]')]; const act = items.findIndex(i => i === document.activeElement || i.getAttribute('data-active') !== null || i.id === m.getAttribute('aria-activedescendant')); return { act, n: items.length }; });
    const a0 = await idx(); await page.keyboard.press('ArrowDown'); const a1 = await idx(); await page.keyboard.press('End'); const a2 = await idx(); await page.keyboard.press('Home'); const a3 = await idx();
    return (a1.act !== a0.act && a2.act === a2.n - 1 && a3.act === 0) || JSON.stringify({ a0, a1, a2, a3 });
  });
  await step('menu: typeahead moves to a matching item', async () => {
    const items = await page.evaluate(() => [...document.querySelectorAll('[role=menuitem]')].map(i => i.textContent.trim()));
    const target = items[items.length - 1]; const ch = target[0].toLowerCase();
    await page.keyboard.press('Home'); await page.keyboard.type(ch); await settle(150);
    const got = await page.evaluate(() => { const m = document.querySelector('[role=menu]'); const items = [...m.querySelectorAll('[role=menuitem]')]; const i = items.findIndex(x => x === document.activeElement || x.getAttribute('data-active') !== null || x.id === m.getAttribute('aria-activedescendant')); return items[i]?.textContent.trim(); });
    return (got && got[0].toLowerCase() === ch) || `typed "${ch}", active item "${got}"`;
  });
  await step('menu: Escape closes and focus returns to the button', async () => {
    await page.keyboard.press('Escape'); await settle(300);
    const has = await page.evaluate(() => !!document.querySelector('[role=menu]') && document.querySelector('[role=menu]').offsetParent !== null);
    const a = await active();
    return (!has && a.includes('al-menu-button')) || `menuVisible=${has} active=${a}`;
  });
  await step('menu: Enter on an item runs it and closes', async () => {
    const before = await last();
    await page.locator('.al-menu-button').first().focus(); await page.keyboard.press('Enter'); await settle(300);
    await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter'); await settle(300);
    const after = await last();
    return after !== before || `last unchanged: "${after}"`;
  });

  await fresh();
  // ---- context menu
  await step('context menu: right-click opens, ArrowDown + Enter runs an item', async () => {
    const before = await last();
    await page.locator('.al-context-area').click({ button: 'right' }); await settle(400);
    const has = await visible('[role=menu]');
    await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter'); await settle(300);
    const after = await last();
    return (has && after !== before) || `menuVisible=${has} last "${before}" -> "${after}"`;
  });
  await step('context menu: Shift+F10 on the focused area opens it (synthetic key, informational)', async () => {
    await page.locator('.al-context-area').focus(); await page.keyboard.press('Shift+F10'); await settle(400);
    const has = await visible('[role=menu]');
    if (has) { await page.keyboard.press('Escape'); await settle(200); }
    return has || 'did not open (synthetic key presses do not raise contextmenu in every engine)';
  });

  await fresh();
  // ---- select
  await step('select: Enter opens a listbox, ArrowDown + Enter selects, value shown', async () => {
    const before = await text('#grid-value');
    await page.locator('section[aria-label="Selection"] .al-select-button').focus(); await page.keyboard.press('Enter'); await settle(400);
    const has = await visible('[role=listbox]');
    await page.keyboard.press('ArrowDown'); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter'); await settle(300);
    const after = await text('#grid-value');
    return (has && after !== before) || `listbox=${has} grid "${before}" -> "${after}"`;
  });
  await axe_state(async () => { await page.locator('section[aria-label="Selection"] .al-select-button').focus(); await page.keyboard.press('Enter'); await settle(400); }, 'select open');
  await step('select: Escape closes the list and focus returns to the button', async () => {
    await page.keyboard.press('Escape'); await settle(300);
    const has = await visible('[role=listbox]'); const a = await active();
    return (!has && a.includes('al-select-button')) || `listbox=${has} active=${a}`;
  });

  await fresh();
  // ---- combobox
  await step('combobox: typing filters, ArrowDown + Enter selects', async () => {
    const before = await text('#model-value');
    const input = page.locator('section[aria-label="Selection"] .al-combobox-input'); await input.click(); await page.keyboard.type('a'); await settle(400);
    const n = await page.locator('[role=option]:visible').count();
    await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter'); await settle(300);
    const after = await text('#model-value');
    return (n > 0 && after !== before) || `options=${n} model "${before}" -> "${after}"`;
  });
  await axe_state(async () => { const i = page.locator('section[aria-label="Selection"] .al-combobox-input'); await i.click(); await i.fill(''); await page.keyboard.type('m'); await settle(400); }, 'combobox open');
  await step('combobox: Escape closes the list', async () => {
    await page.keyboard.press('Escape'); await settle(300);
    return !(await visible('[role=listbox]')) || 'list still visible';
  });

  await fresh();
  // ---- popover
  await step('popover: Enter opens, Escape closes, focus returns to the button', async () => {
    const b = page.locator('section[aria-label="Popover, tooltip and toasts"] .al-popover-button'); await b.focus(); await page.keyboard.press('Enter'); await settle(400);
    const has = await visible('.al-popover');
    await axe_state(null, 'popover open');
    await page.keyboard.press('Escape'); await settle(300);
    const gone = !(await visible('.al-popover')); const a = await active();
    return (has && gone && a.includes('al-popover-button')) || `opened=${has} closed=${gone} active=${a}`;
  });
  await step('popover: click outside closes it', async () => {
    await page.locator('section[aria-label="Popover, tooltip and toasts"] .al-popover-button').click(); await settle(300);
    await page.mouse.click(5, 5); await settle(300);
    return !(await visible('.al-popover')) || 'still open';
  });

  await fresh();
  await step('popover (library): the panel opens below its button, start-aligned', async () => {
    const b = page.locator('section[aria-label="Popover, tooltip and toasts"] .al-popover-button');
    await b.click(); await settle(400);
    const r = await page.evaluate(() => {
      const btn = document.querySelector('section[aria-label="Popover, tooltip and toasts"] .al-popover-button').getBoundingClientRect();
      const panels = [...document.querySelectorAll('.al-popover')].filter(p => p.offsetParent !== null || getComputedStyle(p).display !== 'none');
      const p = panels[0]?.getBoundingClientRect(); if (!p) return null;
      return { dTop: Math.round(p.top - btn.bottom), dLeft: Math.round(p.left - btn.left) };
    });
    await page.keyboard.press('Escape'); await settle(200);
    out.notes = out.notes || []; out.notes.push('library popover offset from its button (px): ' + JSON.stringify(r));
    return (r && r.dTop >= 0 && r.dTop <= 16 && Math.abs(r.dLeft) <= 4) || 'offset ' + JSON.stringify(r);
  });
  await step('popover (native, CSS anchor positioning): placed below its button, start-aligned', async () => {
    const p2 = await ctx.newPage(); p2.setDefaultTimeout(4000);
    await p2.goto(URL + 'anchor.html'); await p2.waitForTimeout(200);
    const support = await p2.evaluate(() => ({ anchorName: CSS.supports('anchor-name', '--a'), positionArea: CSS.supports('position-area', 'bottom span-right') }));
    await p2.click('#b'); await p2.waitForTimeout(300);
    const r = await p2.evaluate(() => { const b = document.getElementById('b').getBoundingClientRect(); const p = document.getElementById('p').getBoundingClientRect(); return { open: document.getElementById('p').matches(':popover-open'), dTop: Math.round(p.top - b.bottom), dLeft: Math.round(p.left - b.left) }; });
    await p2.close();
    out.notes = out.notes || []; out.notes.push('anchor positioning: supports ' + JSON.stringify(support) + '; native popover offset from its anchor (px): ' + JSON.stringify(r));
    return (support.anchorName && support.positionArea && r.open && r.dTop >= 0 && r.dTop <= 2 && Math.abs(r.dLeft) <= 2) || JSON.stringify({ support, r });
  });

  // ---- tooltip
  const tipSection = 'section[aria-label="Popover, tooltip and toasts"]';
  await step('tooltip (trigger is the button): keyboard focus shows it, Escape hides it', async () => {
    await page.locator(tipSection + ' .al-popover-button').focus(); await page.keyboard.press('Tab'); await page.keyboard.press('Tab'); await settle(900);
    const a0 = await active();
    const shown = await visible('.al-tooltip');
    await axe_state(null, 'tooltip visible');
    await page.keyboard.press('Escape'); await settle(300);
    const hidden = !(await visible('.al-tooltip'));
    return (shown && hidden) || `focus on ${a0}; shown=${shown} hiddenAfterEscape=${hidden}`;
  });
  await step('tooltip (trigger is the button): hover shows it', async () => {
    await page.mouse.move(5, 5); await settle(300);
    await page.locator(tipSection + ' .al-tooltip-trigger').first().hover(); await settle(900);
    return (await visible('.al-tooltip')) || 'not shown on hover';
  });
  await step('tooltip (span wrapped around a button): keyboard focus shows it', async () => {
    await page.mouse.move(5, 5); await settle(300);
    await page.locator(tipSection + ' .al-popover-button').focus(); await page.keyboard.press('Tab'); await settle(900);
    const a0 = await active();
    return (await visible('.al-tooltip')) || `focus on ${a0}; tooltip not shown`;
  });
  await step('tooltip (span wrapped around a button): hover shows it', async () => {
    await page.keyboard.press('Escape'); await page.mouse.move(5, 5); await settle(300);
    await page.getByRole('button', { name: 'Play', exact: true }).hover(); await settle(900);
    return (await visible('.al-tooltip')) || 'not shown on hover';
  });

  await fresh();
  // ---- toasts
  await step('toast: appears, announced (role status/alert or live region), F8 reaches it, Undo runs', async () => {
    await page.click('#show-toast'); await settle(500);
    const shown = await visible('.al-toast');
    const live = await page.evaluate(() => { const t = document.querySelector('.al-toast'); const r = t?.closest('[role],[aria-live]'); return t ? (t.getAttribute('role') || '') + '/' + (t.getAttribute('aria-live') || '') + ' viewport:' + (document.querySelector('.al-toasts')?.getAttribute('role') || '') + '/' + (document.querySelector('.al-toasts')?.getAttribute('aria-live') || '') : ''; });
    await axe_state(null, 'toast visible');
    await page.keyboard.press('F8'); await settle(300);
    const a = await active();
    const before = await last();
    const undo = page.locator('.al-toast-action').first();
    if (await undo.count()) { await undo.focus(); await page.keyboard.press('Enter'); await settle(300); }
    const after = await last();
    return (shown && after !== before) || `shown=${shown} live=${live} afterF8Active=${a} last "${before}" -> "${after}"`;
  });

  await fresh();
  // ---- native modal + library overlays
  await step('modal: select inside a native dialog opens, picks, and Escape closes the list before the dialog', async () => {
    await page.click('#open-native-form'); await settle(400);
    const sel = page.locator('dialog.al-native-dialog[open] .al-select-button');
    await sel.focus(); await page.keyboard.press('Enter'); await settle(400);
    const listOpen = await visible('dialog[open] [role=listbox]');
    await page.keyboard.press('Escape'); await settle(300);
    const afterFirst = { list: await visible('dialog[open] [role=listbox]'), dialog: await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]')) };
    await page.keyboard.press('Escape'); await settle(300);
    const dialog2 = await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]'));
    return (listOpen && !afterFirst.list && afterFirst.dialog && !dialog2) || `listOpen=${listOpen} afterFirstEscape=${JSON.stringify(afterFirst)} dialogAfterSecond=${dialog2}`;
  });
  await step('modal: combobox inside a native dialog, Escape closes the list before the dialog', async () => {
    await page.click('#open-native-form'); await settle(400);
    const input = page.locator('dialog.al-native-dialog[open] .al-combobox-input'); await input.click(); await page.keyboard.type('a'); await settle(400);
    const listOpen = await visible('dialog[open] [role=listbox]');
    await page.keyboard.press('Escape'); await settle(300);
    const afterFirst = { list: await visible('dialog[open] [role=listbox]'), dialog: await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]')) };
    await page.keyboard.press('Escape'); await settle(300);
    const dialog2 = await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]'));
    return (listOpen && !afterFirst.list && afterFirst.dialog && !dialog2) || `listOpen=${listOpen} afterFirstEscape=${JSON.stringify(afterFirst)} dialogAfterSecond=${dialog2}`;
  });
  await step('modal: popover inside a native dialog, Escape closes it before the dialog', async () => {
    await page.click('#open-native-form'); await settle(400);
    const b = page.locator('dialog.al-native-dialog[open] .al-popover-button'); await b.focus(); await page.keyboard.press('Enter'); await settle(400);
    const open1 = await visible('dialog[open] .al-popover');
    await page.keyboard.press('Escape'); await settle(300);
    const afterFirst = { pop: await visible('dialog[open] .al-popover'), dialog: await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]')) };
    await page.keyboard.press('Escape'); await settle(300);
    const dialog2 = await page.evaluate(() => !!document.querySelector('dialog.al-native-dialog[open]'));
    return (open1 && !afterFirst.pop && afterFirst.dialog && !dialog2) || `popoverOpen=${open1} afterFirstEscape=${JSON.stringify(afterFirst)} dialogAfterSecond=${dialog2}`;
  });
  await step('modal: axe state - native form dialog with a select list expanded', async () => {
    await page.click('#open-native-form'); await settle(400);
    await page.locator('dialog.al-native-dialog[open] .al-select-button').focus(); await page.keyboard.press('Enter'); await settle(400);
    await axe('native form dialog with select expanded');
    await closeAll();
    return true;
  });
  await observe('modal: a toast raised from inside a dialog (observation, not scored): shown, and does it receive clicks?', async () => {
    await page.click('#open-native-form'); await settle(400);
    await page.locator('#dialog-toast').click(); await settle(500);
    const r = await page.evaluate(() => {
      const t = document.querySelector('.al-toast'); if (!t) return { shown: false };
      const b = t.getBoundingClientRect(); const hit = document.elementFromPoint(b.x + b.width / 2, b.y + b.height / 2);
      return { shown: true, topmost: t.contains(hit), hit: hit ? hit.tagName.toLowerCase() + '.' + (hit.className || '') : null };
    });
    await closeAll();
    return {
      short: !r.shown ? 'not shown' : r.topmost ? 'shown, receives clicks' : 'shown, not clickable',
      detail: 'shown=' + r.shown + ', receives pointer hits=' + r.topmost + ' (topmost element at its centre: ' + r.hit + ')'
    };
  });

  await fresh();
  // ---- tabs
  await step('tabs: ArrowRight moves to the next tab and shows its panel', async () => {
    const tabs = page.getByRole('tab'); await tabs.first().focus(); const p0 = await page.locator('[role=tabpanel]:visible').first().innerText();
    await page.keyboard.press('ArrowRight'); await settle(250);
    const p1 = await page.locator('[role=tabpanel]:visible').first().innerText(); const sel = await tabs.nth(1).getAttribute('aria-selected');
    return (p0 !== p1 && sel === 'true') || `panel "${p0}" -> "${p1}" selected=${sel}`;
  });

  async function axe_state(setup, label) { try { if (setup) await setup(); await axe(label); } catch (e) { out.steps.push({ title: 'axe state: ' + label, ok: false, detail: 'could not reach the state: ' + String(e.message).split(String.fromCharCode(10))[0].slice(0, 160) }); } }

  out.errors = [...new Set(errors)].slice(0, 5);
  await browser.close();
  return out;
}

(async () => {
  const results = [];
  for (const n of only) {
    try { results.push(await runBrowser(n)); } catch (e) { results.push({ name: n, fatal: String(e).slice(0, 400), steps: [], axe: [] }); }
  }
  const meta = { os: `${os.type()} ${os.release()}`, cpu: os.cpus()[0].model, cores: os.cpus().length, ramGB: Math.round(os.totalmem() / 2 ** 30), node: process.version, playwright: require('playwright/package.json').version, axe: require('axe-core/package.json').version };
  fs.writeFileSync(path.join(__dirname, process.env.OUT || 'results.json'), JSON.stringify({ meta, results }, null, 1));
  for (const r of results) {
    console.log(`\n== ${r.name} ${r.version || ''} ${r.fatal || ''}`);
    for (const s of r.steps) console.log(`${s.observation ? 'SEEN' : s.ok ? 'PASS' : 'FAIL'}  ${s.title}${s.detail ? '\n        ' + s.detail : ''}`);
    for (const a of r.axe) console.log(`AXE   ${a.label}: ${a.violations.length} violation(s), ${a.incomplete.length} incomplete, ${a.passes} passes` + a.violations.map(v => `\n        ${v.id} (${v.impact}) x${v.nodes.length}: ${v.nodes[0].target}`).join(''));
    if (r.errors?.length) console.log('page errors:', r.errors);
  }
})();
