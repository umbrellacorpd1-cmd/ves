'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { splitArgs, dayKey, prettifyName } = require('../src/util');
const { SessionTracker, processNamesFor, matches } = require('../src/tracker');
const { Store } = require('../src/store');

test('splitArgs handles quotes and spaces', () => {
  assert.deepStrictEqual(splitArgs('-windowed  -res "1920 1080" \'a b\' ""'), ['-windowed', '-res', '1920 1080', 'a b', '']);
  assert.deepStrictEqual(splitArgs(''), []);
});

test('dayKey and prettifyName', () => {
  assert.strictEqual(dayKey(new Date(2026, 0, 5, 23, 59)), '2026-01-05');
  assert.strictEqual(prettifyName('gta_v-launcher.exe'), 'Gta V Launcher');
  assert.strictEqual(prettifyName('GTA5.exe'), 'GTA5');
});

test('processNamesFor includes extra tracked processes', () => {
  const names = processNamesFor({ path: '/games/Launcher.exe', trackProcess: 'Game.exe, other' });
  assert.ok(names.includes('launcher.exe'));
  assert.ok(names.includes('game.exe'));
  assert.ok(matches(['averyveryverylongname'], new Set(['averyveryverylo'])));
});

function fakeTracker(procs) {
  let t = 0;
  const tracker = new SessionTracker({ listProcesses: async () => ({ ok: true, names: procs }), now: () => t, graceMs: 10000, maxTickMs: 30000 });
  tracker.ensureTimer = () => {};
  return { tracker, advance: (ms) => { t += ms; } };
}

test('counts time while the process runs and ends after it disappears', async () => {
  const procs = new Set(['game.exe']);
  const { tracker, advance } = fakeTracker(procs);
  let counted = 0;
  let ended = null;
  tracker.on('tick', (_id, ms) => { counted += ms; });
  tracker.on('end', (_id, s) => { ended = s; });
  tracker.start('a', { names: ['game.exe'] });
  for (let i = 0; i < 5; i++) { advance(2000); await tracker.poll(); }
  assert.strictEqual(counted, 10000);
  procs.clear();
  advance(2000); await tracker.poll();
  assert.strictEqual(ended, null, 'one miss is tolerated');
  advance(2000); await tracker.poll();
  assert.ok(ended);
  assert.strictEqual(ended.countedMs, 10000);
  assert.strictEqual(tracker.size, 0);
});

test('waits for a slow-starting process and clamps sleep gaps', async () => {
  const procs = new Set();
  const { tracker, advance } = fakeTracker(procs);
  let counted = 0;
  tracker.on('tick', (_id, ms) => { counted += ms; });
  tracker.start('a', { names: ['game.exe'] });
  advance(4000); await tracker.poll();
  assert.ok(tracker.has('a'), 'still in grace period');
  procs.add('game.exe');
  advance(2000); await tracker.poll();
  advance(3600 * 1000); await tracker.poll(); // PC was asleep
  assert.strictEqual(counted, 2000 + 30000);
});

test('ends a session that never started after the grace period', async () => {
  const { tracker, advance } = fakeTracker(new Set());
  let ended = false;
  tracker.on('end', () => { ended = true; });
  tracker.start('a', { names: ['game.exe'] });
  for (let i = 0; i < 7; i++) { advance(2000); await tracker.poll(); }
  assert.ok(ended);
});

test('a failed process listing never ends a session', async () => {
  let t = 0;
  const tracker = new SessionTracker({ listProcesses: async () => ({ ok: false, names: new Set() }), now: () => t, graceMs: 0 });
  tracker.ensureTimer = () => {};
  tracker.start('a', { names: ['game.exe'] });
  for (let i = 0; i < 5; i++) { t += 2000; await tracker.poll(); }
  assert.ok(tracker.has('a'));
});

test('store persists, backs up and recovers from corruption', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'oglu-'));
  const file = path.join(dir, 'library.json');
  const s1 = new Store(file);
  s1.data.apps.push({ id: 'x', name: 'X', path: '/x', totalSeconds: 3600, days: {}, history: [] });
  s1.saveNow();
  s1.saveNow();
  const s2 = new Store(file);
  assert.strictEqual(s2.get('x').totalSeconds, 3600);
  assert.strictEqual(s2.data.selectedId, 'x');
  fs.writeFileSync(file, '{broken');
  const s3 = new Store(file);
  assert.strictEqual(s3.get('x').totalSeconds, 3600, 'restored from .bak');
});
