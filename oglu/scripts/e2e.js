'use strict';
// End-to-end check of the real Electron app (run under xvfb on Linux):
// add an app, launch it, close it from the launcher, verify tracking and persistence.
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
  // A real long-running binary with a distinctive process name stands in for a game.
  const fake = path.join(cfg, 'nightracer');
  fs.copyFileSync('/bin/sleep', fake);
  fs.chmodSync(fake, 0o755);

  const app = await electron.launch({ args: ['--no-sandbox', root], env: { ...process.env, XDG_CONFIG_HOME: cfg } });
  const win = await app.firstWindow();
  const errors = [];
  win.on('pageerror', (e) => errors.push(e.message));
  win.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  await sleep(600);
  await win.screenshot({ path: path.join(shots, 'e2e-0-splash.png') });
  await win.waitForSelector('#splash', { state: 'detached', timeout: 8000 });
  await sleep(1400);
  await win.screenshot({ path: path.join(shots, 'e2e-1-empty.png') });

  await app.evaluate(({ dialog }, p) => { dialog.showOpenDialog = async () => ({ canceled: false, filePaths: [p] }); }, fake);
  await win.click('.empty [data-act="add"]');
  await win.waitForSelector('#playBtn');
  await win.click('[data-act="settings"]');
  await win.waitForSelector('#fName');
  await sleep(700);
  await win.screenshot({ path: path.join(shots, 'e2e-2-settings.png') });
  await win.fill('#fName', 'Night Racer');
  await win.fill('#fArgs', '600');
  await win.click('[data-d="save"]');
  await sleep(900);

  await win.click('#playBtn');
  await sleep(1300);
  await win.screenshot({ path: path.join(shots, 'e2e-3-launch.png') });
  await sleep(4200);
  assert.match(await win.textContent('#playText'), /in game/i);
  assert.ok(await win.isVisible('#liveChip'), 'live chip visible');
  assert.ok(await win.isVisible('#closeBtn'), 'close button visible while running');
  await win.screenshot({ path: path.join(shots, 'e2e-4-running.png') });

  // Close the game from the launcher; the session must end long before the 600s sleep would.
  const t0 = Date.now();
  await win.click('#closeBtn');
  await win.waitForFunction(() => !document.querySelector('#playBtn.is-running'), null, { timeout: 10000 });
  console.log(`closed from launcher in ${Date.now() - t0} ms`);
  assert.ok(!(await win.isVisible('#closeBtn')), 'close button hidden after exit');
  await sleep(1200);
  await win.screenshot({ path: path.join(shots, 'e2e-5-after.png') });

  const lib = JSON.parse(fs.readFileSync(path.join(cfg, 'Oglu', 'library.json'), 'utf8'));
  const a = lib.apps[0];
  console.log('tracked seconds', a.totalSeconds.toFixed(1), 'sessions', a.sessions);
  assert.strictEqual(a.name, 'Night Racer');
  assert.strictEqual(a.sessions, 1);
  assert.ok(a.totalSeconds >= 3 && a.totalSeconds <= 12, `unexpected total ${a.totalSeconds}`);

  await win.click('#tabs [data-view="library"]');
  await sleep(1200);
  assert.strictEqual(await win.locator('.lcard[data-open]').count(), 1);
  await win.screenshot({ path: path.join(shots, 'e2e-6-library.png') });

  await app.close();
  assert.deepStrictEqual(errors, [], `renderer errors: ${errors.join('\n')}`);
  console.log('E2E OK, screenshots in', shots);
})().catch((err) => {
  console.error(err);
  process.exit(1);
});
