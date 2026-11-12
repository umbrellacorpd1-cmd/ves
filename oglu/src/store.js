'use strict';

const fs = require('fs');
const path = require('path');

const MAX_DAYS = 400;
const MAX_HISTORY = 100;

function defaults() {
  return {
    version: 1,
    apps: [],
    selectedId: null,
    settings: { minimizeOnLaunch: true, closeToTray: true },
  };
}

function normalizeApp(a) {
  const days = a.days && typeof a.days === 'object' ? a.days : {};
  const keys = Object.keys(days).sort();
  for (const k of keys.slice(0, Math.max(0, keys.length - MAX_DAYS))) delete days[k];
  return {
    id: String(a.id),
    name: String(a.name || 'Application'),
    path: String(a.path || ''),
    args: String(a.args || ''),
    trackProcess: String(a.trackProcess || ''),
    cover: a.cover ? String(a.cover) : null,
    coverVersion: Number(a.coverVersion) || 0,
    totalSeconds: Number(a.totalSeconds) || 0,
    sessions: Number(a.sessions) || 0,
    longestSession: Number(a.longestSession) || 0,
    lastPlayedAt: a.lastPlayedAt ? Number(a.lastPlayedAt) : null,
    addedAt: Number(a.addedAt) || Date.now(),
    days,
    history: Array.isArray(a.history) ? a.history.slice(-MAX_HISTORY) : [],
  };
}

function normalize(d) {
  const base = defaults();
  if (!d || typeof d !== 'object') return base;
  const apps = Array.isArray(d.apps) ? d.apps.filter((a) => a && a.id).map(normalizeApp) : [];
  return {
    version: 1,
    apps,
    selectedId: apps.some((a) => a.id === d.selectedId) ? d.selectedId : apps[0]?.id ?? null,
    settings: { ...base.settings, ...(d.settings && typeof d.settings === 'object' ? d.settings : {}) },
  };
}

class Store {
  constructor(file) {
    this.file = file;
    this.timer = null;
    this.data = this.load();
  }

  load() {
    try {
      return normalize(JSON.parse(fs.readFileSync(this.file, 'utf8')));
    } catch (err) {
      if (err.code !== 'ENOENT') {
        // Keep the unreadable file for manual recovery, then fall back to the last good backup.
        try { fs.copyFileSync(this.file, `${this.file}.corrupt-${Date.now()}`); } catch {}
        try { return normalize(JSON.parse(fs.readFileSync(`${this.file}.bak`, 'utf8'))); } catch {}
      }
      return defaults();
    }
  }

  get(id) {
    return this.data.apps.find((a) => a.id === id) || null;
  }

  saveSoon(ms = 5000) {
    if (!this.timer) this.timer = setTimeout(() => this.saveNow(), ms);
  }

  saveNow() {
    clearTimeout(this.timer);
    this.timer = null;
    const json = JSON.stringify(this.data, null, 2);
    fs.mkdirSync(path.dirname(this.file), { recursive: true });
    const tmp = `${this.file}.tmp`;
    try {
      fs.writeFileSync(tmp, json);
      if (fs.existsSync(this.file)) fs.copyFileSync(this.file, `${this.file}.bak`);
      fs.renameSync(tmp, this.file);
    } catch {
      // Rename can fail on Windows if another process (e.g. antivirus) holds the file.
      fs.writeFileSync(this.file, json);
    }
  }
}

module.exports = { Store, normalize, normalizeApp, defaults };
