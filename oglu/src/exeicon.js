'use strict';

/**
 * Reads the main icon straight out of a Windows .exe (PE resources), picking the
 * largest image — usually 256px — instead of the 32px icon the shell API returns.
 * Only the header and resource section are read, so huge game executables are cheap.
 */
const fs = require('fs');

const RT_ICON = 3;
const RT_GROUP_ICON = 14;
const PNG_SIG = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const MAX_RSRC = 64 * 1024 * 1024;

function readAt(fd, pos, len) {
  const b = Buffer.alloc(len);
  const n = fs.readSync(fd, b, 0, len, pos);
  return n === len ? b : b.subarray(0, n);
}

function readResourceSection(file) {
  const fd = fs.openSync(file, 'r');
  try {
    const dos = readAt(fd, 0, 64);
    if (dos.length < 64 || dos.readUInt16LE(0) !== 0x5a4d) return null; // "MZ"
    const peOff = dos.readUInt32LE(0x3c);
    const coff = readAt(fd, peOff, 24);
    if (coff.length < 24 || coff.readUInt32LE(0) !== 0x00004550) return null; // "PE\0\0"
    const nSections = coff.readUInt16LE(6);
    const optSize = coff.readUInt16LE(20);
    const opt = readAt(fd, peOff + 24, optSize);
    const magic = opt.length >= 2 ? opt.readUInt16LE(0) : 0;
    const dd = magic === 0x20b ? 112 : magic === 0x10b ? 96 : -1;
    if (dd < 0 || opt.length < dd + 24) return null;
    const rsrcRva = opt.readUInt32LE(dd + 16);
    if (!rsrcRva) return null;
    const secs = readAt(fd, peOff + 24 + optSize, nSections * 40);
    for (let i = 0; i + 1 <= nSections && i * 40 + 40 <= secs.length; i++) {
      const s = i * 40;
      const vsize = secs.readUInt32LE(s + 8);
      const va = secs.readUInt32LE(s + 12);
      const rawSize = secs.readUInt32LE(s + 16);
      const rawPtr = secs.readUInt32LE(s + 20);
      if (rsrcRva >= va && rsrcRva < va + Math.max(vsize, rawSize)) {
        return { data: readAt(fd, rawPtr, Math.min(rawSize, MAX_RSRC)), va, dirOff: rsrcRva - va };
      }
    }
    return null;
  } finally {
    fs.closeSync(fd);
  }
}

function dirEntries(r, off) {
  const d = r.data;
  const p = r.dirOff + off;
  if (p + 16 > d.length) return [];
  const n = d.readUInt16LE(p + 12) + d.readUInt16LE(p + 14);
  const out = [];
  for (let i = 0; i < n; i++) {
    const e = p + 16 + i * 8;
    if (e + 8 > d.length) break;
    const name = d.readUInt32LE(e);
    const target = d.readUInt32LE(e + 4);
    out.push({ named: name >>> 31 === 1, id: name & 0x7fffffff, isDir: target >>> 31 === 1, off: target & 0x7fffffff });
  }
  return out;
}

/** Descends through the (language) subdirectories to the first data blob. */
function firstData(r, entry) {
  let e = entry;
  for (let depth = 0; e && e.isDir && depth < 4; depth++) e = dirEntries(r, e.off)[0];
  if (!e || e.isDir) return null;
  const p = r.dirOff + e.off;
  if (p + 8 > r.data.length) return null;
  const start = r.data.readUInt32LE(p) - r.va;
  const size = r.data.readUInt32LE(p + 4);
  if (start < 0 || start + size > r.data.length) return null;
  return r.data.subarray(start, start + size);
}

/** Returns { mime, data, width, height } for the exe's main icon, or null. */
function extractIcon(file) {
  let r;
  try {
    r = readResourceSection(file);
  } catch {
    return null;
  }
  if (!r) return null;
  const top = dirEntries(r, 0);
  const groupDir = top.find((e) => !e.named && e.id === RT_GROUP_ICON && e.isDir);
  const iconDir = top.find((e) => !e.named && e.id === RT_ICON && e.isDir);
  if (!groupDir || !iconDir) return null;
  const icons = dirEntries(r, iconDir.off);

  // Explorer shows the first icon group in resource order.
  for (const g of dirEntries(r, groupDir.off)) {
    const grp = firstData(r, g);
    if (!grp || grp.length < 6) continue;
    const cands = [];
    for (let i = 0, n = grp.readUInt16LE(4); i < n; i++) {
      const p = 6 + i * 14;
      if (p + 14 > grp.length) break;
      cands.push({
        w: grp[p] || 256,
        h: grp[p + 1] || 256,
        colors: grp[p + 2],
        planes: grp.readUInt16LE(p + 4),
        bits: grp.readUInt16LE(p + 6),
        id: grp.readUInt16LE(p + 12),
      });
    }
    cands.sort((a, b) => b.w * b.h - a.w * a.h || b.bits - a.bits);
    for (const c of cands) {
      const ent = icons.find((e) => !e.named && e.id === c.id);
      const img = ent && firstData(r, ent);
      if (!img || img.length < 8) continue;
      if (img.subarray(0, 8).equals(PNG_SIG)) return { mime: 'image/png', data: Buffer.from(img), width: c.w, height: c.h };
      // Classic DIB icon: wrap it in a one-image .ico, which Chromium decodes natively.
      const head = Buffer.alloc(22);
      head.writeUInt16LE(1, 2);
      head.writeUInt16LE(1, 4);
      head[6] = c.w >= 256 ? 0 : c.w;
      head[7] = c.h >= 256 ? 0 : c.h;
      head[8] = c.colors;
      head.writeUInt16LE(c.planes || 1, 10);
      head.writeUInt16LE(c.bits, 12);
      head.writeUInt32LE(img.length, 14);
      head.writeUInt32LE(22, 18);
      return { mime: 'image/x-icon', data: Buffer.concat([head, img]), width: c.w, height: c.h };
    }
  }
  return null;
}

function extractIconDataUrl(file) {
  const icon = extractIcon(file);
  return icon ? `data:${icon.mime};base64,${icon.data.toString('base64')}` : null;
}

module.exports = { extractIcon, extractIconDataUrl };
