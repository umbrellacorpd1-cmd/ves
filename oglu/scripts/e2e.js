'use strict';
// End-to-end check of the real Electron app (run under xvfb on Linux):
// add an app, launch it, verify tracking and persistence, capture screenshots.
const { _electron: electron } = require('playwright-core');
const fs = require('fs');
const os = require('os');
const path = require('path');
const assert = require('assert');

const root = path.join(__dirname, '..');
const shots = process.env.OGLU_SHOTS || path.join(root, 'dist', 'screens');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  fs.mkdirSync(shots, { recursive: true });
  const cfg = fs.mkdtempSync(path.join(os.tmpdir(), 'oglu-e2e-'));
  // A real binary with a distinctive process name stands in for a game.
  const fake = path.join(cfg, 'nightracer');
  fs.copyFileSync('/bin/sleep', fake);
  fs.chmodSync(fake, 0o755);

  const env = { ...process.env, XDG_CONFIG_HOME: cfg };
  const app = await electron.launch({ args: ['--no-sandbox', root], env });
  const win = await app.firstWindow();
  await win.setViewportSize({ width: 1280, height: 800 }).catch(() => {});
  await sleep(600);
  await win.screenshot({ path: path.join(shots, '0-splash.png') });
  await win.waitForSelector('#splash', { state: 'detached', timeout: 8000 });
  await sleep(1400);
  await win.screenshot({ path: path.join(shots, '1-empty.png') });

  await app.evaluate(({ dialog }, p) => { dialog.showOpenDialog = async () => ({ canceled: false, filePaths: [p] }); }, fake);
  await win.click('.empty [data-act="add"]');
  await win.waitForSelector('#playBtn');
  await win.click('[data-act="settings"]');
  await win.waitForSelector('#fName');
  await sleep(700);
  await win.screenshot({ path: path.join(shots, '2-settings.png') });
  await win.fill('#fName', 'Night Racer');
  await win.fill('#fArgs', '5');
  await win.click('[data-d="save"]');
  await sleep(900);

  await win.click('#playBtn');
  await sleep(1300);
  await win.screenshot({ path: path.join(shots, '3-launch.png') });
  await sleep(2600);
  const running = await win.textContent('#playText');
  await win.screenshot({ path: path.join(shots, '4-running.png') });
  assert.ok(/in game|starting/i.test(running), `expected running state, got ${running}`);
  assert.ok(await win.isVisible('#liveChip'), 'live chip visible');

  await win.waitForFunction(() => !document.querySelector('#playBtn.is-running'), null, { timeout: 15000 });
  await sleep(1200);
  await win.screenshot({ path: path.join(shots, '5-after.png') });

  const lib = JSON.parse(fs.readFileSync(path.join(cfg, 'Oglu', 'library.json'), 'utf8'));
  const a = lib.apps[0];
  console.log('tracked seconds', a.totalSeconds.toFixed(1), 'sessions', a.sessions, 'history', JSON.stringify(a.history));
  assert.strictEqual(a.name, 'Night Racer');
  assert.strictEqual(a.sessions, 1);
  assert.ok(a.totalSeconds >= 3 && a.totalSeconds <= 9, `unexpected total ${a.totalSeconds}`);

  await app.close();
  console.log('E2E OK, screenshots in', shots);
})().catch((err) => {
  console.error(err);
  process.exit(1);
});
