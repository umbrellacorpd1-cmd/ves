'use strict';

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { extractIcon, extractIconDataUrl } = require('../src/exeicon');

/** Classic (non-PNG) 32x32 32bpp icon: BITMAPINFOHEADER + pixels + AND mask. */
function bmpIco(size = 32) {
  const header = Buffer.alloc(40);
  header.writeUInt32LE(40, 0);
  header.writeInt32LE(size, 4);
  header.writeInt32LE(size * 2, 8);
  header.writeUInt16LE(1, 12);
  header.writeUInt16LE(32, 14);
  const pixels = Buffer.alloc(size * size * 4, 0xff);
  const mask = Buffer.alloc(size * 4);
  const img = Buffer.concat([header, pixels, mask]);
  const dir = Buffer.alloc(22);
  dir.writeUInt16LE(1, 2);
  dir.writeUInt16LE(1, 4);
  dir[6] = size;
  dir[7] = size;
  dir.writeUInt16LE(1, 10);
  dir.writeUInt16LE(32, 12);
  dir.writeUInt32LE(img.length, 14);
  dir.writeUInt32LE(22, 18);
  return Buffer.concat([dir, img]);
}

async function makeExe(icoBuffer) {
  const R = await import('resedit');
  const exe = R.NtExecutable.createEmpty(true, false);
  const res = R.NtExecutableResource.from(exe);
  const ico = R.Data.IconFile.from(icoBuffer);
  R.Resource.IconGroupEntry.replaceIconsForResource(res.entries, 101, 1033, ico.icons.map((i) => i.data));
  res.outputResource(exe);
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'oglu-ico-')), 'game.exe');
  fs.writeFileSync(file, Buffer.from(exe.generate()));
  return file;
}

test('extracts the largest PNG icon from an exe', async () => {
  const file = await makeExe(fs.readFileSync(path.join(__dirname, '..', 'assets', 'icon.ico')));
  const icon = extractIcon(file);
  assert.ok(icon);
  assert.strictEqual(icon.mime, 'image/png');
  assert.strictEqual(icon.width, 256);
  assert.ok(icon.data.subarray(1, 4).equals(Buffer.from('PNG')));
});

test('wraps classic bitmap icons in a valid .ico', async () => {
  const file = await makeExe(bmpIco(32));
  const icon = extractIcon(file);
  assert.strictEqual(icon.mime, 'image/x-icon');
  assert.strictEqual(icon.width, 32);
  assert.strictEqual(icon.data.readUInt16LE(4), 1, 'one image');
  assert.strictEqual(icon.data.readUInt32LE(18), 22, 'image data follows the header');
  assert.strictEqual(icon.data.length, 22 + 40 + 32 * 32 * 4 + 32 * 4);
  assert.ok(extractIconDataUrl(file).startsWith('data:image/x-icon;base64,'));
});

test('returns null for non-PE files and missing files', () => {
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'oglu-ico-')), 'x.exe');
  fs.writeFileSync(file, 'not an exe');
  assert.strictEqual(extractIcon(file), null);
  assert.strictEqual(extractIcon(path.join(os.tmpdir(), 'does-not-exist.exe')), null);
});
