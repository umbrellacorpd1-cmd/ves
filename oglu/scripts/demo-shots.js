'use strict';
// Seeds a demo library and captures screenshots of the populated UI (run under xvfb).
const { _electron: electron } = require('playwright-core');
const fs = require('fs');
const os = require('os');
const path = require('path');

const root = path.join(__dirname, '..');
const shots = path.join(root, 'dist', 'screens');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const dayKey = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

function history(seed, avgH) {
  const days = {};
  const now = new Date();
  for (let i = 0; i < 40; i++) {
    const r = Math.abs(Math.sin(seed * 31 + i * 7.3));
    if (r > 0.3) days[dayKey(new Date(now.getFullYear(), now.getMonth(), now.getDate() - i))] = Math.round(r * avgH * 3600);
  }
  return days;
}

(async () => {
  fs.mkdirSync(shots, { recursive: true });
  const cfg = fs.mkdtempSync(path.join(os.tmpdir(), 'oglu-demo-'));
  const bin = (name) => { const p = path.join(cfg, name); fs.copyFileSync('/bin/sleep', p); fs.chmodSync(p, 0o755); return p; };
  const now = Date.now();
  const app = (id, name, file, totalH, sessions, longestH, lastAgoH, seed, avg) => ({
    id, name, path: file, args: '30', trackProcess: '', cover: null, coverVersion: 0,
    totalSeconds: totalH * 3600, sessions, longestSession: longestH * 3600, lastPlayedAt: now - lastAgoH * 3600e3,
    addedAt: now - 90 * 86400e3, days: history(seed, avg), history: [],
  });
  const lib = {
    version: 1,
    selectedId: 'a1',
    settings: { minimizeOnLaunch: false, closeToTray: true },
    apps: [
      app('a1', 'Majestic RP', bin('gta5'), 412.6, 187, 9.2, 20, 1, 3.4),
      app('a2', 'Counter Strike 2', bin('cs2'), 238.1, 402, 5.1, 50, 2, 1.8),
      app('a3', 'Minecraft', bin('minecraft'), 96.4, 64, 6.7, 130, 3, 1.2),
      app('a4', 'Rust', bin('rustclient'), 51.9, 22, 7.5, 400, 4, 0.9),
      app('a5', 'Old Launcher', path.join(cfg, 'missing.exe'), 12.3, 9, 2.1, 2000, 5, 0.3),
    ],
  };
  fs.mkdirSync(path.join(cfg, 'Oglu'), { recursive: true });
  fs.writeFileSync(path.join(cfg, 'Oglu', 'library.json'), JSON.stringify(lib));

  const el = await electron.launch({ args: ['--no-sandbox', root], env: { ...process.env, XDG_CONFIG_HOME: cfg } });
  const win = await el.firstWindow();
  await win.waitForSelector('#splash', { state: 'detached', timeout: 8000 });
  await sleep(2200);
  await win.screenshot({ path: path.join(shots, 'demo-1-dashboard.png') });
  await win.hover('.bar.is-today');
  await win.hover('.card >> nth=1');
  await win.click('.app-item >> nth=4');
  await sleep(1800);
  await win.screenshot({ path: path.join(shots, 'demo-2-missing.png') });
  await win.click('.app-item >> nth=1');
  await sleep(400);
  await win.click('#playBtn');
  await sleep(1900);
  await win.screenshot({ path: path.join(shots, 'demo-3-launch.png') });
  await sleep(3500);
  await win.screenshot({ path: path.join(shots, 'demo-4-running.png') });
  await win.click('#prefsBtn');
  await sleep(900);
  await win.screenshot({ path: path.join(shots, 'demo-5-prefs.png') });
  await el.close();
  console.log('demo screenshots written');
})().catch((e) => { console.error(e); process.exit(1); });
