'use strict';
// Seeds a demo library and captures screenshots of the populated UI (run under xvfb).
// Icons come from real Windows executables; set OGLU_DEMO_EXES to a folder of .exe files.
const { _electron: electron } = require('playwright-core');
const fs = require('fs');
const os = require('os');
const path = require('path');
const sharp = require('sharp');

const root = path.join(__dirname, '..');
const shots = path.join(root, 'dist', 'screens');
const exes = process.env.OGLU_DEMO_EXES || '/tmp/wineprefix/drive_c/windows/system32';
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
  const runner = path.join(cfg, 'nightracer');
  fs.copyFileSync('/bin/sleep', runner);
  fs.chmodSync(runner, 0o755);
  const cover = path.join(cfg, 'cover.png');
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="900">
    <defs><linearGradient id="s" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1a0630"/><stop offset=".55" stop-color="#5b0f4f"/><stop offset="1" stop-color="#ff3d6e"/></linearGradient>
    <linearGradient id="sun" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffd36b"/><stop offset="1" stop-color="#ff2d75"/></linearGradient></defs>
    <rect width="1600" height="900" fill="url(#s)"/><circle cx="1120" cy="470" r="230" fill="url(#sun)"/>
    ${Array.from({ length: 9 }, (_, i) => `<rect x="860" y="${420 + i * 26}" width="520" height="${4 + i * 2}" fill="#5b0f4f"/>`).join('')}
    <rect y="600" width="1600" height="300" fill="#12041f"/>
    ${Array.from({ length: 24 }, (_, i) => `<line x1="${800 + (i - 12) * 40}" y1="600" x2="${800 + (i - 12) * 260}" y2="900" stroke="#ff3d9e" stroke-opacity=".7" stroke-width="2"/>`).join('')}
    ${Array.from({ length: 8 }, (_, i) => `<line x1="0" y1="${605 + i * i * 6}" x2="1600" y2="${605 + i * i * 6}" stroke="#ff3d9e" stroke-opacity=".6" stroke-width="2"/>`).join('')}
  </svg>`)).png().toFile(cover);

  const now = Date.now();
  const app = (id, name, file, totalH, sessions, longestH, lastAgoH, seed, avg, extra = {}) => ({
    id, name, path: file, args: '', trackProcess: '', cover: null, coverVersion: 0,
    totalSeconds: totalH * 3600, sessions, longestSession: longestH * 3600, lastPlayedAt: now - lastAgoH * 3600e3,
    addedAt: now - 90 * 86400e3, days: history(seed, avg), history: [], ...extra,
  });
  const ie = path.join(exes, '..', '..', 'Program Files', 'Internet Explorer', 'iexplore.exe');
  const lib = {
    version: 1,
    selectedId: 'a1',
    settings: { minimizeOnLaunch: false, closeToTray: true },
    apps: [
      app('a1', 'Minesweeper', path.join(exes, 'winemine.exe'), 412.6, 187, 9.2, 20, 1, 3.4),
      app('a2', 'Night Racer', runner, 238.1, 402, 5.1, 50, 2, 1.8, { args: '600', cover }),
      app('a3', 'Internet Explorer', ie, 96.4, 64, 6.7, 130, 3, 1.2),
      app('a4', 'Wine Config', path.join(exes, 'winecfg.exe'), 51.9, 22, 7.5, 400, 4, 0.9),
      app('a5', 'Task Manager', path.join(exes, 'taskmgr.exe'), 33.0, 41, 2.5, 700, 6, 0.6),
      app('a6', 'Notepad', path.join(exes, 'notepad.exe'), 18.4, 30, 1.9, 1500, 7, 0.4),
      app('a7', 'Old Launcher', path.join(cfg, 'missing.exe'), 12.3, 9, 2.1, 2000, 5, 0.3),
    ],
  };
  fs.mkdirSync(path.join(cfg, 'Oglu'), { recursive: true });
  fs.writeFileSync(path.join(cfg, 'Oglu', 'library.json'), JSON.stringify(lib));

  const el = await electron.launch({ args: ['--no-sandbox', root], env: { ...process.env, XDG_CONFIG_HOME: cfg } });
  const win = await el.firstWindow();
  await sleep(900);
  await win.screenshot({ path: path.join(shots, 'demo-0-splash.png') });
  await win.waitForSelector('#splash', { state: 'detached', timeout: 8000 });
  await sleep(2600);
  await win.screenshot({ path: path.join(shots, 'demo-1-home.png') });
  await win.click('.tile[data-id="a3"]');
  await sleep(2200);
  await win.screenshot({ path: path.join(shots, 'demo-2-switch.png') });
  await win.click('.tile[data-id="a2"]');
  await sleep(700);
  await win.click('#playBtn');
  await sleep(1900);
  await win.screenshot({ path: path.join(shots, 'demo-3-launch.png') });
  await sleep(3800);
  await win.hover('#closeBtn');
  await sleep(400);
  await win.screenshot({ path: path.join(shots, 'demo-4-running.png') });
  await win.click('#tabs [data-view="library"]');
  await sleep(1600);
  await win.hover('.lcard >> nth=0');
  await win.mouse.move(380, 330);
  await sleep(500);
  await win.screenshot({ path: path.join(shots, 'demo-5-library.png') });
  await win.click('.lcard[data-open="a1"]');
  await sleep(700);
  await win.click('[data-act="settings"]');
  await sleep(900);
  await win.screenshot({ path: path.join(shots, 'demo-6-settings.png') });
  // Oglu stays in the tray while a game runs, so close the game before quitting.
  await win.click('[data-close]');
  await win.click('#liveStop');
  await win.waitForSelector('#liveChip', { state: 'hidden', timeout: 10000 });
  await el.close();
  console.log('demo screenshots written');
})().catch((e) => { console.error(e); process.exit(1); });
