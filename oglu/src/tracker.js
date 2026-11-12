'use strict';

const { EventEmitter } = require('events');
const { execFile } = require('child_process');
const path = require('path');

/** Lower-cased names of every running process. */
function listProcessNames() {
  return new Promise((resolve) => {
    const done = (err, stdout) => {
      const names = new Set();
      if (!err && stdout) {
        for (const line of String(stdout).split(/\r?\n/)) {
          let name = line.trim();
          if (!name) continue;
          if (process.platform === 'win32') {
            const m = /^"([^"]+)"/.exec(name);
            if (!m) continue;
            name = m[1];
          }
          names.add(name.toLowerCase());
        }
      }
      resolve({ ok: !err, names });
    };
    if (process.platform === 'win32') {
      execFile('tasklist.exe', ['/FO', 'CSV', '/NH'], { windowsHide: true, maxBuffer: 16 * 1024 * 1024 }, done);
    } else {
      execFile('ps', ['-A', '-o', 'comm='], { maxBuffer: 16 * 1024 * 1024 }, done);
    }
  });
}

/** Process image names that count as "this app is running". */
function processNamesFor(app) {
  const names = [path.basename(app.path)];
  for (const extra of String(app.trackProcess || '').split(/[,;]/)) {
    const n = extra.trim();
    if (n) names.push(/\.[a-z0-9]+$/i.test(n) || process.platform !== 'win32' ? n : `${n}.exe`);
  }
  return [...new Set(names.map((n) => n.toLowerCase()))];
}

function matches(names, running) {
  return names.some((n) => running.has(n) || (n.length > 15 && running.has(n.slice(0, 15))));
}

function run(cmd, args) {
  return new Promise((resolve) => {
    execFile(cmd, args, { windowsHide: true }, (err, stdout, stderr) => {
      resolve({ code: err ? (typeof err.code === 'number' ? err.code : -1) : 0, out: `${stdout || ''}${stderr || ''}`.trim() });
    });
  });
}

/**
 * Asks every process with one of these image names to close.
 * Without force this is a polite request (WM_CLOSE / SIGTERM) so games can save;
 * with force the whole process tree is terminated.
 */
async function killProcesses(names, { force = false } = {}) {
  const results = await Promise.all(names.map((n) => {
    if (process.platform === 'win32') {
      return run('taskkill.exe', force ? ['/IM', n, '/T', '/F'] : ['/IM', n]);
    }
    return run('pkill', [force ? '-KILL' : '-TERM', '-i', '-x', n.slice(0, 15)]);
  }));
  // taskkill: 0 ok, 128 not found. pkill: 0 ok, 1 not found.
  const notFound = process.platform === 'win32' ? 128 : 1;
  if (results.some((r) => r.code === 0) || results.every((r) => r.code === notFound)) return { ok: true };
  const detail = results.map((r) => r.out).find(Boolean) || '';
  const denied = /access|denied|отказано/i.test(detail);
  return {
    ok: false,
    error: denied ? 'Windows denied access. The app is probably running as administrator — run Oglu as administrator to close it.' : detail || 'The application did not respond.',
  };
}

/**
 * Tracks running sessions by polling the process list.
 * Emits 'tick' (id, ms) while an app runs and 'end' (id, summary) when it stops.
 */
class SessionTracker extends EventEmitter {
  constructor({ listProcesses = listProcessNames, now = Date.now, pollMs = 2000, graceMs = 20000, maxTickMs = 30000 } = {}) {
    super();
    this.listProcesses = listProcesses;
    this.now = now;
    this.pollMs = pollMs;
    this.graceMs = graceMs;
    this.maxTickMs = maxTickMs;
    this.sessions = new Map();
    this.timer = null;
    this.polling = false;
  }

  get size() {
    return this.sessions.size;
  }

  has(id) {
    return this.sessions.has(id);
  }

  info(id) {
    const s = this.sessions.get(id);
    return s ? { startedAt: s.startedAt, countedMs: s.countedMs, seen: s.seen } : null;
  }

  start(id, { names, childAlive = false }) {
    const t = this.now();
    this.sessions.set(id, { id, names, childAlive, startedAt: t, lastTick: t, countedMs: 0, seen: false, misses: 0 });
    this.ensureTimer();
  }

  childExited(id) {
    const s = this.sessions.get(id);
    if (s) s.childAlive = false;
  }

  ensureTimer() {
    if (!this.timer && this.sessions.size) {
      this.timer = setInterval(() => this.poll(), this.pollMs);
      if (this.timer.unref) this.timer.unref();
    }
  }

  async poll() {
    if (this.polling || !this.sessions.size) return;
    this.polling = true;
    try {
      const { ok, names } = await this.listProcesses();
      const t = this.now();
      for (const s of [...this.sessions.values()]) {
        // If the process list could not be read, only trust the child handle; never end a session on a failed read.
        const alive = s.childAlive || (ok && matches(s.names, names));
        // Clamp so time spent with the PC asleep is not counted.
        const delta = Math.max(0, Math.min(t - s.lastTick, this.maxTickMs));
        s.lastTick = t;
        if (alive) {
          s.seen = true;
          s.misses = 0;
          s.countedMs += delta;
          if (delta > 0) this.emit('tick', s.id, delta);
        } else if (!ok) {
          continue;
        } else if (!s.seen && t - s.startedAt < this.graceMs) {
          // Still starting up (launcher stubs, UAC prompts).
        } else if (++s.misses >= 2) {
          this.finish(s.id, t);
        }
      }
    } finally {
      this.polling = false;
      if (!this.sessions.size && this.timer) {
        clearInterval(this.timer);
        this.timer = null;
      }
    }
  }

  finish(id, t = this.now()) {
    const s = this.sessions.get(id);
    if (!s) return;
    this.sessions.delete(id);
    this.emit('end', id, { startedAt: s.startedAt, endedAt: t, countedMs: s.countedMs });
  }

  finishAll() {
    for (const id of [...this.sessions.keys()]) this.finish(id);
  }
}

module.exports = { SessionTracker, listProcessNames, processNamesFor, matches, killProcesses };
