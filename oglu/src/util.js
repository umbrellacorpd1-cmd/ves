'use strict';

/** Split a command-line argument string, honouring double and single quotes. */
function splitArgs(input) {
  const out = [];
  let cur = '';
  let quote = null;
  let has = false;
  for (const ch of String(input || '')) {
    if (quote) {
      if (ch === quote) quote = null;
      else cur += ch;
    } else if (ch === '"' || ch === "'") {
      quote = ch;
      has = true;
    } else if (/\s/.test(ch)) {
      if (has || cur) out.push(cur);
      cur = '';
      has = false;
    } else {
      cur += ch;
    }
  }
  if (has || cur) out.push(cur);
  return out;
}

/** Local calendar day key, e.g. 2026-11-12. */
function dayKey(date = new Date()) {
  const d = date instanceof Date ? date : new Date(date);
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

/** "gta_v-launcher.exe" -> "Gta V Launcher" style display name. */
function prettifyName(fileName) {
  const base = String(fileName).replace(/\.(exe|lnk|app)$/i, '').replace(/[_\-.]+/g, ' ').trim();
  if (!base) return 'Application';
  return base
    .split(/\s+/)
    .map((w) => (w === w.toLowerCase() ? w.charAt(0).toUpperCase() + w.slice(1) : w))
    .join(' ');
}

module.exports = { splitArgs, dayKey, prettifyName };
