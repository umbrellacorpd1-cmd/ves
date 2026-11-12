'use strict';

const { app, BrowserWindow, ipcMain, dialog, shell, Tray, Menu, nativeImage, Notification } = require('electron');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const { spawn } = require('child_process');
const { pathToFileURL } = require('url');
const { Store } = require('./store');
const { SessionTracker, processNamesFor } = require('./tracker');
const { splitArgs, dayKey, prettifyName } = require('./util');

const isWin = process.platform === 'win32';
const ASSETS = path.join(__dirname, '..', 'assets');
const IMAGE_EXT = ['png', 'jpg', 'jpeg', 'webp', 'gif'];

app.setAppUserModelId('com.oglu.launcher');

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  main();
}

function main() {
  let store;
  let win = null;
  let tray = null;
  let quitting = false;
  let trayHintShown = false;
  const minimizedFor = new Set();
  const iconCache = new Map();
  const tracker = new SessionTracker();

  const send = (channel, payload) => {
    if (win && !win.isDestroyed()) win.webContents.send(channel, payload);
  };

  function showWindow() {
    if (!win) return createWindow();
    if (win.isMinimized()) win.restore();
    win.show();
    win.focus();
  }

  async function iconFor(file) {
    if (iconCache.has(file)) return iconCache.get(file);
    let url = null;
    try {
      if (fs.existsSync(file)) {
        const img = await app.getFileIcon(file, { size: 'large' });
        if (!img.isEmpty()) url = img.toDataURL();
      }
    } catch {}
    iconCache.set(file, url);
    return url;
  }

  async function publicApp(a) {
    const since = dayKey(Date.now() - 60 * 86400000);
    const days = {};
    for (const [k, v] of Object.entries(a.days)) if (k >= since) days[k] = v;
    return {
      id: a.id,
      name: a.name,
      path: a.path,
      args: a.args,
      trackProcess: a.trackProcess,
      cover: a.cover && fs.existsSync(a.cover) ? `${pathToFileURL(a.cover).href}?v=${a.coverVersion}` : null,
      totalSeconds: a.totalSeconds,
      sessions: a.sessions,
      longestSession: a.longestSession,
      lastPlayedAt: a.lastPlayedAt,
      addedAt: a.addedAt,
      days,
      exists: fs.existsSync(a.path),
      icon: await iconFor(a.path),
      session: tracker.info(a.id),
    };
  }

  async function snapshot() {
    return {
      apps: await Promise.all(store.data.apps.map(publicApp)),
      selectedId: store.data.selectedId,
      settings: store.data.settings,
      platform: process.platform,
      version: app.getVersion(),
      dataDir: app.getPath('userData'),
    };
  }

  let pushQueued = false;
  function pushState() {
    if (pushQueued) return;
    pushQueued = true;
    setImmediate(async () => {
      pushQueued = false;
      send('state', await snapshot());
      updateTray();
    });
  }

  // ---------- Tracking ----------

  tracker.on('tick', (id, ms) => {
    const a = store.get(id);
    if (!a) return;
    const sec = ms / 1000;
    const k = dayKey();
    a.totalSeconds += sec;
    a.days[k] = (a.days[k] || 0) + sec;
    store.saveSoon();
    send('tick', { id, totalSeconds: a.totalSeconds, today: a.days[k], session: tracker.info(id) });
  });

  tracker.on('end', (id, s) => {
    const a = store.get(id);
    if (a) {
      const sec = s.countedMs / 1000;
      if (sec >= 1) {
        a.sessions += 1;
        a.longestSession = Math.max(a.longestSession, sec);
        a.history.push({ start: s.startedAt, end: s.endedAt, seconds: Math.round(sec) });
        a.history = a.history.slice(-100);
      }
      a.lastPlayedAt = s.endedAt;
      store.saveNow();
      send('session-end', { id, seconds: sec, totalSeconds: a.totalSeconds });
    }
    if (minimizedFor.delete(id) && !quitting && win && !win.isDestroyed() && tracker.size === 0) showWindow();
    pushState();
  });

  function spawnDetached(file, args, cwd) {
    return new Promise((resolve, reject) => {
      let child;
      try {
        child = spawn(file, args, { cwd, detached: true, stdio: 'ignore', windowsHide: false });
      } catch (err) {
        reject(err);
        return;
      }
      child.once('error', reject);
      child.once('spawn', () => {
        child.removeListener('error', reject);
        child.on('error', () => {});
        child.unref();
        resolve(child);
      });
    });
  }

  async function launch(id) {
    const a = store.get(id);
    if (!a) return { ok: false, error: 'Unknown application.' };
    if (tracker.has(id)) return { ok: false, error: `${a.name} is already running.` };
    if (!fs.existsSync(a.path)) return { ok: false, error: 'The file was not found. Choose its new location in the app settings.' };

    let child = null;
    try {
      child = await spawnDetached(a.path, splitArgs(a.args), path.dirname(a.path));
    } catch (err) {
      if (!isWin) return { ok: false, error: err.message };
      // Apps that require administrator rights can't be spawned directly; ShellExecute shows the UAC prompt.
      const msg = await shell.openPath(a.path);
      if (msg) return { ok: false, error: msg };
    }

    tracker.start(id, { names: processNamesFor(a), childAlive: !!child });
    if (child) child.once('exit', () => tracker.childExited(id));
    a.lastPlayedAt = Date.now();
    store.saveSoon(1000);

    if (store.data.settings.minimizeOnLaunch) {
      setTimeout(() => {
        if (tracker.has(id) && win && !win.isDestroyed() && win.isVisible()) {
          minimizedFor.add(id);
          win.minimize();
        }
      }, 3200);
    }
    pushState();
    return { ok: true };
  }

  // ---------- Library ----------

  function resolveShortcut(file) {
    if (isWin && /\.lnk$/i.test(file)) {
      try {
        const link = shell.readShortcutLink(file);
        return { target: link.target, args: link.args || '' };
      } catch {
        return null;
      }
    }
    return { target: file, args: '' };
  }

  function addApp(file) {
    const resolved = resolveShortcut(file);
    if (!resolved || !resolved.target) return { ok: false, error: 'That shortcut could not be read.' };
    const target = resolved.target;
    let stat;
    try { stat = fs.statSync(target); } catch { return { ok: false, error: 'File not found.' }; }
    if (!stat.isFile()) return { ok: false, error: 'Please choose an application file.' };
    if (isWin && !/\.exe$/i.test(target)) return { ok: false, error: 'Please choose an .exe file.' };

    const same = (p) => (isWin ? p.toLowerCase() === target.toLowerCase() : p === target);
    const existing = store.data.apps.find((a) => same(a.path));
    if (existing) {
      store.data.selectedId = existing.id;
      store.saveSoon(500);
      pushState();
      return { ok: true, id: existing.id, existed: true };
    }

    const a = {
      id: crypto.randomUUID(),
      name: prettifyName(path.basename(file)),
      path: target,
      args: resolved.args,
      trackProcess: '',
      cover: null,
      coverVersion: 0,
      totalSeconds: 0,
      sessions: 0,
      longestSession: 0,
      lastPlayedAt: null,
      addedAt: Date.now(),
      days: {},
      history: [],
    };
    store.data.apps.push(a);
    store.data.selectedId = a.id;
    store.saveNow();
    pushState();
    return { ok: true, id: a.id };
  }

  async function pickFile(title, filters) {
    const r = await dialog.showOpenDialog(win, { title, properties: ['openFile'], filters });
    return r.canceled || !r.filePaths[0] ? null : r.filePaths[0];
  }

  const exeFilters = isWin ? [{ name: 'Applications', extensions: ['exe', 'lnk'] }] : [];

  // ---------- IPC ----------

  ipcMain.handle('state', () => snapshot());

  ipcMain.handle('add-app', async (_e, file) => {
    const p = typeof file === 'string' && file ? file : await pickFile('Select an application', exeFilters);
    return p ? addApp(p) : null;
  });

  ipcMain.handle('change-path', async (_e, id) => {
    const a = store.get(id);
    if (!a) return null;
    const p = await pickFile(`Locate ${a.name}`, exeFilters);
    if (!p) return null;
    const r = resolveShortcut(p);
    if (!r || !fs.existsSync(r.target)) return { ok: false, error: 'File not found.' };
    a.path = r.target;
    iconCache.delete(a.path);
    store.saveNow();
    pushState();
    return { ok: true };
  });

  ipcMain.handle('update-app', (_e, id, patch) => {
    const a = store.get(id);
    if (!a || !patch) return null;
    if (typeof patch.name === 'string' && patch.name.trim()) a.name = patch.name.trim().slice(0, 60);
    if (typeof patch.args === 'string') a.args = patch.args.slice(0, 1000);
    if (typeof patch.trackProcess === 'string') a.trackProcess = patch.trackProcess.trim().slice(0, 200);
    store.saveSoon(500);
    pushState();
    return { ok: true };
  });

  ipcMain.handle('remove-app', (_e, id) => {
    if (tracker.has(id)) return { ok: false, error: 'Close the application before removing it.' };
    const a = store.get(id);
    if (!a) return null;
    if (a.cover) fs.rm(a.cover, { force: true }, () => {});
    store.data.apps = store.data.apps.filter((x) => x.id !== id);
    if (store.data.selectedId === id) store.data.selectedId = store.data.apps[0]?.id ?? null;
    store.saveNow();
    pushState();
    return { ok: true };
  });

  ipcMain.handle('reset-stats', (_e, id) => {
    const a = store.get(id);
    if (!a) return null;
    Object.assign(a, { totalSeconds: 0, sessions: 0, longestSession: 0, days: {}, history: [] });
    store.saveNow();
    pushState();
    return { ok: true };
  });

  ipcMain.handle('set-cover', async (_e, id) => {
    const a = store.get(id);
    if (!a) return null;
    const p = await pickFile('Choose a cover image', [{ name: 'Images', extensions: IMAGE_EXT }]);
    if (!p) return null;
    const dir = path.join(app.getPath('userData'), 'covers');
    fs.mkdirSync(dir, { recursive: true });
    const dest = path.join(dir, `${a.id}${path.extname(p).toLowerCase()}`);
    if (a.cover && a.cover !== dest) fs.rm(a.cover, { force: true }, () => {});
    fs.copyFileSync(p, dest);
    a.cover = dest;
    a.coverVersion += 1;
    store.saveNow();
    pushState();
    return { ok: true };
  });

  ipcMain.handle('clear-cover', (_e, id) => {
    const a = store.get(id);
    if (!a || !a.cover) return null;
    fs.rm(a.cover, { force: true }, () => {});
    a.cover = null;
    store.saveNow();
    pushState();
    return { ok: true };
  });

  ipcMain.handle('reveal', (_e, id) => {
    const a = store.get(id);
    if (a && fs.existsSync(a.path)) shell.showItemInFolder(a.path);
  });

  ipcMain.handle('open-data-dir', () => shell.openPath(app.getPath('userData')));

  ipcMain.handle('select', (_e, id) => {
    if (store.get(id)) {
      store.data.selectedId = id;
      store.saveSoon();
    }
  });

  ipcMain.handle('launch', (_e, id) => launch(id));

  ipcMain.handle('settings', (_e, patch) => {
    for (const k of ['minimizeOnLaunch', 'closeToTray']) {
      if (typeof patch?.[k] === 'boolean') store.data.settings[k] = patch[k];
    }
    store.saveSoon(500);
    pushState();
    return store.data.settings;
  });

  ipcMain.handle('win', (_e, action) => {
    if (!win) return;
    if (action === 'minimize') win.minimize();
    else if (action === 'maximize') (win.isMaximized() ? win.unmaximize() : win.maximize());
    else if (action === 'close') win.close();
  });

  // ---------- Window & tray ----------

  function createWindow() {
    win = new BrowserWindow({
      width: 1280,
      height: 800,
      minWidth: 1000,
      minHeight: 660,
      frame: false,
      show: false,
      title: 'Oglu',
      backgroundColor: '#0b0b0e',
      icon: path.join(ASSETS, isWin ? 'icon.ico' : 'icon.png'),
      webPreferences: {
        preload: path.join(__dirname, 'preload.js'),
        contextIsolation: true,
        nodeIntegration: false,
        sandbox: true,
        spellcheck: false,
      },
    });
    win.setMenu(null);
    win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
    win.once('ready-to-show', () => win.show());
    win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    win.webContents.on('will-navigate', (e) => e.preventDefault());
    win.on('maximize', () => send('win-state', { maximized: true }));
    win.on('unmaximize', () => send('win-state', { maximized: false }));
    win.on('close', (e) => {
      if (!quitting && tracker.size > 0 && store.data.settings.closeToTray && tray) {
        e.preventDefault();
        win.hide();
        if (!trayHintShown && Notification.isSupported()) {
          trayHintShown = true;
          new Notification({ title: 'Oglu is still tracking', body: 'Your playtime keeps counting. Oglu lives in the system tray.' }).show();
        }
      }
    });
    win.on('closed', () => { win = null; });
  }

  function updateTray() {
    if (!tray) return;
    const running = store.data.apps.filter((a) => tracker.has(a.id));
    tray.setToolTip(running.length ? `Oglu — tracking ${running.map((a) => a.name).join(', ')}` : 'Oglu');
    const launchItems = store.data.apps.slice(0, 10).map((a) => ({
      label: tracker.has(a.id) ? `● ${a.name}` : a.name,
      enabled: !tracker.has(a.id),
      click: () => launch(a.id).then((r) => { if (!r.ok) dialog.showErrorBox('Oglu', r.error); }),
    }));
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: 'Open Oglu', click: showWindow },
      ...(launchItems.length ? [{ type: 'separator' }, ...launchItems] : []),
      { type: 'separator' },
      { label: 'Quit Oglu', click: () => { quitting = true; app.quit(); } },
    ]));
  }

  function createTray() {
    try {
      const img = nativeImage.createFromPath(path.join(ASSETS, 'icon.png')).resize({ width: 32, height: 32 });
      tray = new Tray(img);
      tray.on('click', showWindow);
      tray.on('double-click', showWindow);
      updateTray();
    } catch {
      tray = null;
    }
  }

  app.on('second-instance', showWindow);

  app.on('before-quit', () => {
    quitting = true;
    tracker.finishAll();
    if (store) store.saveNow();
  });

  app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') app.quit();
  });

  app.whenReady().then(() => {
    store = new Store(path.join(app.getPath('userData'), 'library.json'));
    createWindow();
    createTray();
  });
}
