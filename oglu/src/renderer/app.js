'use strict';

const api = window.oglu;
const $ = (s, root = document) => root.querySelector(s);
const $$ = (s, root = document) => [...root.querySelectorAll(s)];
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

const svg = (body, extra = '') => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ${extra}>${body}</svg>`;
const ICONS = {
  play: svg('<path d="M7 4.5v15l12-7.5z" fill="currentColor"/>'),
  stop: svg('<rect x="5.5" y="5.5" width="13" height="13" rx="3" fill="currentColor" stroke="none"/>'),
  folder: svg('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  image: svg('<rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="9" cy="10" r="2"/><path d="m21 16-5-5-9 9"/>'),
  gear: svg('<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>'),
  sliders: svg('<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6"/>'),
  plus: svg('<path d="M12 5v14M5 12h14"/>'),
  close: svg('<path d="M6 6l12 12M18 6 6 18"/>', 'stroke-width="1.6"'),
  min: svg('<path d="M5 12h14"/>', 'stroke-width="1.6"'),
  max: svg('<rect x="5" y="5" width="14" height="14" rx="2"/>', 'stroke-width="1.6"'),
  restore: svg('<rect x="4" y="8" width="12" height="12" rx="2"/><path d="M8 8V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-2"/>', 'stroke-width="1.6"'),
  home: svg('<path d="M3 11 12 4l9 7"/><path d="M5 10v10h14V10"/>'),
  grid: svg('<rect x="3" y="3" width="7" height="7" rx="2"/><rect x="14" y="3" width="7" height="7" rx="2"/><rect x="3" y="14" width="7" height="7" rx="2"/><rect x="14" y="14" width="7" height="7" rx="2"/>'),
  clock: svg('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>'),
  calendar: svg('<rect x="3" y="5" width="18" height="16" rx="3"/><path d="M16 3v4M8 3v4M3 10h18"/>'),
  trophy: svg('<path d="M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0z"/><path d="M17 5h3v2a3 3 0 0 1-3 3M7 5H4v2a3 3 0 0 0 3 3"/>'),
  layers: svg('<path d="m12 3 9 5-9 5-9-5z"/><path d="m3 13 9 5 9-5"/>'),
  check: svg('<path d="m5 12 5 5 9-10"/>'),
  alert: svg('<path d="M12 9v4M12 17h.01"/><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/>'),
  info: svg('<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/>'),
  download: svg('<path d="M12 3v12M7 10l5 5 5-5M5 21h14"/>'),
  loader: svg('<path d="M12 3a9 9 0 1 0 9 9"/>'),
  trash: svg('<path d="M4 7h16M10 11v6M14 11v6M5 7l1 13h12l1-13M9 7V4h6v3"/>'),
  refresh: svg('<path d="M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5"/>'),
  file: svg('<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/>'),
};

function hydrateIcons(root = document) {
  root.querySelectorAll('[data-icon]').forEach((el) => {
    if (!el.firstElementChild) el.innerHTML = ICONS[el.dataset.icon] || '';
  });
}

// ---------- Formatting ----------

const dayKey = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const fmtHours = (sec) => {
  const h = sec / 3600;
  return h >= 1000 ? Math.floor(h).toLocaleString('en-US') : (Math.floor(h * 10) / 10).toFixed(1);
};
function fmtHM(sec) {
  sec = Math.max(0, Math.floor(sec));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  if (h) return `${h}h ${m}m`;
  if (m) return `${m}m`;
  return sec ? `${sec}s` : '0m';
}
function fmtClock(ms) {
  const s = Math.max(0, Math.floor(ms / 1000));
  const p = (n) => String(n).padStart(2, '0');
  return `${p(Math.floor(s / 3600))}:${p(Math.floor((s % 3600) / 60))}:${p(s % 60)}`;
}
function fmtAgo(ts) {
  if (!ts) return 'Never';
  const s = (Date.now() - ts) / 1000;
  if (s < 60) return 'Just now';
  if (s < 3600) return `${Math.round(s / 60)}m ago`;
  if (s < 86400) return `${Math.round(s / 3600)}h ago`;
  if (s < 86400 * 2) return 'Yesterday';
  if (s < 86400 * 30) return `${Math.round(s / 86400)}d ago`;
  return new Date(ts).toLocaleDateString('en-US', { day: 'numeric', month: 'short', year: 'numeric' });
}
const initial = (name) => (String(name).trim()[0] || '?').toUpperCase();
const iconHtml = (a) => (a.icon ? `<img src="${a.icon}" alt="" draggable="false">` : `<b>${esc(initial(a.name))}</b>`);
const ease = (t) => 1 - Math.pow(1 - t, 4);

// ---------- Accent colours (derived from each app's icon) ----------

const BRAND = '#e81c5a';
const accents = new Map();

function rgbToHsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  if (max === min) return [0, 0, l];
  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  const h = max === r ? (g - b) / d + (g < b ? 6 : 0) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return [h * 60, s, l];
}
function hslToHex(h, s, l) {
  const k = (n) => (n + h / 30) % 12;
  const a = s * Math.min(l, 1 - l);
  const f = (n) => Math.round(255 * (l - a * Math.max(-1, Math.min(k(n) - 3, Math.min(9 - k(n), 1))))).toString(16).padStart(2, '0');
  return `#${f(0)}${f(8)}${f(4)}`;
}
/** Keeps accents vivid and readable on the dark UI. */
function tuneAccent(h, s, l) {
  s = Math.max(s, 0.62);
  l = Math.min(Math.max(l, 0.5), 0.6);
  if (h >= 40 && h <= 80) l = Math.min(l, 0.5);
  return hslToHex(h, Math.min(s, 0.95), l);
}
function hashHue(str) {
  let h = 0;
  for (const c of String(str)) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return h % 360;
}
async function iconAccent(src) {
  const img = new Image();
  img.src = src;
  await img.decode();
  const c = document.createElement('canvas');
  c.width = c.height = 40;
  const ctx = c.getContext('2d', { willReadFrequently: true });
  ctx.drawImage(img, 0, 0, 40, 40);
  const d = ctx.getImageData(0, 0, 40, 40).data;
  const bins = Array.from({ length: 24 }, () => ({ w: 0, r: 0, g: 0, b: 0 }));
  for (let i = 0; i < d.length; i += 4) {
    if (d[i + 3] < 160) continue;
    const [h, s, l] = rgbToHsl(d[i], d[i + 1], d[i + 2]);
    if (s < 0.28 || l < 0.12 || l > 0.9) continue;
    const w = s * s * (1 - Math.abs(l - 0.5));
    const bin = bins[Math.floor(h / 15) % 24];
    bin.w += w; bin.r += d[i] * w; bin.g += d[i + 1] * w; bin.b += d[i + 2] * w;
  }
  const best = bins.reduce((x, y) => (y.w > x.w ? y : x));
  if (best.w < 8) return null;
  const [h, s, l] = rgbToHsl(best.r / best.w, best.g / best.w, best.b / best.w);
  return tuneAccent(h, s, l);
}
const accentKey = (a) => `${a.id}:${a.icon ? a.icon.length : 0}`;
function accentOf(a) {
  if (!a) return BRAND;
  return accents.get(accentKey(a)) || (a.icon ? BRAND : tuneAccent(hashHue(a.name), 0.75, 0.56));
}
async function computeAccents(apps) {
  await Promise.all(apps.filter((a) => a.icon && !accents.has(accentKey(a))).map(async (a) => {
    let c = null;
    try { c = await iconAccent(a.icon); } catch {}
    accents.set(accentKey(a), c || BRAND);
  }));
}
function inkFor(hex) {
  const n = parseInt(hex.slice(1), 16);
  const lum = (0.2126 * ((n >> 16) & 255) + 0.7152 * ((n >> 8) & 255) + 0.0722 * (n & 255)) / 255;
  return lum > 0.62 ? '#120b0f' : '#ffffff';
}
let emberRGB = '232,28,90';
function applyAccent(hex) {
  const root = document.documentElement.style;
  root.setProperty('--acc', hex);
  root.setProperty('--acc-ink', inkFor(hex));
  const n = parseInt(hex.slice(1), 16);
  emberRGB = `${(n >> 16) & 255},${(n >> 8) & 255},${n & 255}`;
}

// ---------- State ----------

let state = { apps: [], selectedId: null, settings: {} };
const live = new Map(); // id -> interpolation base for live counters
const closing = new Map(); // id -> { since, force }
let view = 'home';
let sort = 'hours';
let stageKey;
let shelfKey;
let countUp = null;
let busy = false;
let shelfIntro = true;

const byId = (id) => state.apps.find((a) => a.id === id) || null;
const selected = () => byId(state.selectedId);

function syncLive() {
  const today = dayKey();
  for (const a of state.apps) {
    live.set(a.id, {
      total: a.totalSeconds,
      today: a.days[today] || 0,
      countedMs: a.session?.countedMs || 0,
      running: !!a.session,
      seen: !!a.session?.seen,
      at: performance.now(),
    });
    if (!a.session) closing.delete(a.id);
  }
}

/** Interpolated live numbers so counters move smoothly between 2s polls. */
function liveOf(a) {
  const l = live.get(a.id);
  if (!l) return { total: a.totalSeconds, today: 0, countedMs: 0, running: false, seen: false };
  const extra = l.running && l.seen ? Math.min(performance.now() - l.at, 2500) : 0;
  return { total: l.total + extra / 1000, today: l.today + extra / 1000, countedMs: l.countedMs + extra, running: l.running, seen: l.seen };
}

function weekSeconds(a, l) {
  let sum = 0;
  const d = new Date();
  for (let i = 0; i < 7; i++) {
    const k = dayKey(new Date(d.getFullYear(), d.getMonth(), d.getDate() - i));
    sum += i === 0 ? l.today : a.days[k] || 0;
  }
  return sum;
}

// ---------- Ambient background ----------

let ambKey = null;
let ambFront = $('#ambA');
function setAmbient(a) {
  const key = a ? a.cover || a.icon || `none:${a.id}` : 'none';
  if (key === ambKey) return;
  ambKey = key;
  const next = ambFront.id === 'ambA' ? $('#ambB') : $('#ambA');
  next.innerHTML = '';
  if (a?.cover) {
    const el = document.createElement('div');
    el.className = 'amb-cover';
    el.style.backgroundImage = `url("${a.cover}")`;
    next.appendChild(el);
  } else if (a?.icon) {
    next.innerHTML = `<img class="amb-icon amb-icon--1" src="${a.icon}" alt=""><img class="amb-icon amb-icon--2" src="${a.icon}" alt="">`;
  }
  next.classList.add('is-on');
  ambFront.classList.remove('is-on');
  ambFront = next;
}

// ---------- Shelf ----------

function renderShelf() {
  const track = $('#shelf');
  const key = state.apps.map((a) => `${a.id}|${a.name}|${a.icon ? a.icon.length : 0}|${accentOf(a)}`).join(',');
  if (key !== shelfKey) {
    shelfKey = key;
    track.innerHTML = state.apps.map((a, i) => `<button class="tile" data-id="${a.id}" style="--i:${i};--tile-acc:${accentOf(a)}" title="${esc(a.name)}">
        <span class="tile__icon">${iconHtml(a)}<i class="tile__live"></i></span>
        <span class="tile__label"><b>${esc(a.name)}</b><span class="mono" data-shelf-hours="${a.id}"></span></span>
      </button>`).join('')
      + `<button class="tile tile--add" data-act="add" style="--i:${state.apps.length}" title="Add application (Ctrl+O)"><span class="tile__icon">${ICONS.plus}</span></button>`;
    track.classList.toggle('shelf--intro', shelfIntro);
    if (state.apps.length) shelfIntro = false;
  }
  updateShelf();
}

function updateShelf() {
  for (const el of $$('#shelf .tile[data-id]')) {
    const a = byId(el.dataset.id);
    if (!a) continue;
    const l = liveOf(a);
    el.classList.toggle('is-active', a.id === state.selectedId);
    el.classList.toggle('is-live', l.running);
    el.classList.toggle('is-missing', !a.exists);
    el.querySelector('[data-shelf-hours]').textContent = l.running ? `● LIVE · ${fmtClock(l.countedMs)}` : `${fmtHours(l.total)} h played`;
  }
}

function scrollShelfToActive() {
  const track = $('#shelf');
  const el = track.querySelector('.tile.is-active');
  if (!el) return;
  // Wait for the tile to finish expanding before measuring.
  setTimeout(() => {
    const left = el.offsetLeft - track.clientWidth / 2 + el.offsetWidth / 2;
    track.scrollTo({ left: Math.max(0, left) });
  }, 60);
}

// ---------- Home stage ----------

function renderHome() {
  const a = selected();
  $('#home').classList.toggle('is-empty', !a);
  const key = a ? `${a.id}|${a.cover || ''}|${a.icon ? a.icon.length : 0}` : 'empty';
  if (key !== stageKey) {
    stageKey = key;
    $('#stage').innerHTML = a ? stageTemplate() : emptyTemplate();
    $('#dock').innerHTML = a ? dockTemplate() : '';
    hydrateIcons($('#stage'));
    hydrateIcons($('#dock'));
    if (a) {
      $('#artTile').innerHTML = iconHtml(a);
      buildChart(a);
      startCountUp(a);
    }
  }
  if (a) updateHome(a);
  setAmbient(a);
  applyAccent(accentOf(a));
}

function emptyTemplate() {
  return `<section class="empty">
    <div class="orb swap-art"><span class="art__orbit"></span><span class="art__orbit art__orbit--2"></span><div class="orb__core"><img src="../../assets/icon.png" alt=""></div></div>
    <h1 class="swap" style="--d:.1s">Build your <span>library</span></h1>
    <p class="swap" style="--d:.16s">Add any game or program — Oglu launches it and counts every hour you spend inside.</p>
    <button class="play swap" style="--d:.22s" data-act="add"><span class="play__icon">${ICONS.plus}</span><span class="play__text"><b>Select application</b></span></button>
    <div class="empty__hint mono swap" style="--d:.28s">or drag &amp; drop an .exe anywhere</div>
    <div class="steps">
      <div class="step swap" style="--d:.34s"><div class="step__n">01</div><b>Pick the .exe</b><span>Its icon and colours style the launcher.</span></div>
      <div class="step swap" style="--d:.4s"><div class="step__n">02</div><b>Press Play</b><span>Oglu starts it and begins counting.</span></div>
      <div class="step swap" style="--d:.46s"><div class="step__n">03</div><b>Close it any time</b><span>Right from the launcher or the tray.</span></div>
    </div>
  </section>`;
}

function stageTemplate() {
  return `<div class="stage__info">
      <div class="eyebrow swap"><span class="pill" id="pill"></span><span class="eyebrow__meta mono" id="eyebrowMeta"></span></div>
      <h1 class="title swap" style="--d:.05s" id="title"></h1>
      <div class="hours swap" style="--d:.1s">
        <span class="hours__num" id="hoursNum">0.0</span>
        <span class="hours__label"><b>Hours</b><span>spent in app</span></span>
      </div>
      <div class="subline swap" style="--d:.14s" id="subline"></div>
      <div class="actions swap" style="--d:.2s">
        <button class="play" id="playBtn"><span class="play__icon" id="playIcon"></span><span class="play__text"><b id="playText">Play</b><span class="play__timer mono" id="playTimer"></span></span></button>
        <button class="stop" id="closeBtn" hidden><span class="stop__icon" id="closeIcon"></span><span id="closeText">Close app</span></button>
        <span class="tools">
          <button class="tool" data-act="reveal" title="Show in folder" data-icon="folder"></button>
          <button class="tool" data-act="cover" title="Set background image" data-icon="image"></button>
          <button class="tool" data-act="settings" title="App settings" data-icon="gear"></button>
        </span>
      </div>
    </div>
    <div class="stage__art swap-art">
      <div class="art"><span class="art__orbit"></span><span class="art__orbit art__orbit--2"></span><div class="art__tile" id="artTile"></div></div>
      <div class="art__caption mono" id="artCaption"></div>
    </div>`;
}

function dockTemplate() {
  const stat = (icon, label, id) => `<div class="dstat"><div class="dstat__head"><span data-icon="${icon}"></span><span class="label">${label}</span></div><div class="dstat__val" id="${id}">—</div><div class="dstat__sub" id="${id}Sub"></div></div>`;
  return `${stat('clock', 'Sessions', 'statSessions')}${stat('calendar', 'Last played', 'statLast')}${stat('layers', 'Avg session', 'statAvg')}${stat('trophy', 'Longest', 'statLong')}
    <div class="dchart"><div class="dchart__head"><span class="label">Last 14 days</span><b id="metaWeek"></b></div><div class="chart" id="chart"></div></div>`;
}

function chartDays() {
  const d = new Date();
  return Array.from({ length: 14 }, (_, i) => new Date(d.getFullYear(), d.getMonth(), d.getDate() - 13 + i));
}

function buildChart(a) {
  const today = dayKey();
  $('#chart').innerHTML = chartDays().map((d, i) => {
    const k = dayKey(d);
    return `<div class="bar${k === today ? ' is-today' : ''}" data-day="${k}" style="--delay:${0.45 + i * 0.03}s">
      <div class="bar__track"><div class="bar__fill"></div></div>
      <span class="bar__label">${d.toLocaleDateString('en-US', { weekday: 'short' }).slice(0, 2)}</span>
      <span class="bar__tip"></span>
    </div>`;
  }).join('');
  requestAnimationFrame(() => requestAnimationFrame(() => updateChart(a)));
}

function updateChart(a) {
  const chart = $('#chart');
  if (!chart) return;
  const l = liveOf(a);
  const today = dayKey();
  const bars = [...chart.children];
  const vals = bars.map((b) => (b.dataset.day === today ? l.today : a.days[b.dataset.day] || 0));
  const max = Math.max(1800, ...vals);
  bars.forEach((b, i) => {
    const v = vals[i];
    b.style.setProperty('--h', `${((v / max) * 100).toFixed(1)}%`);
    b.classList.toggle('is-zero', v < 30);
    const date = new Date(`${b.dataset.day}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', day: 'numeric', month: 'short' });
    b.querySelector('.bar__tip').textContent = `${date} · ${fmtHM(v)}`;
  });
  $('#metaWeek').textContent = `${fmtHours(weekSeconds(a, l))} h this week`;
}

function startCountUp(a) {
  const target = liveOf(a).total;
  const t0 = performance.now();
  const el = $('#hoursNum');
  countUp = { id: a.id };
  const step = (t) => {
    if (!countUp || countUp.id !== a.id || !el.isConnected) return;
    const p = Math.min(1, (t - t0) / 1300);
    el.textContent = fmtHours(target * ease(p));
    if (p < 1) requestAnimationFrame(step);
    else countUp = null;
  };
  requestAnimationFrame(step);
}

function setIcon(el, name) {
  if (el.dataset.icon !== name) {
    el.dataset.icon = name;
    el.innerHTML = ICONS[name];
  }
}

function updateHome(a) {
  const pill = $('#pill');
  if (!pill) return;
  const l = liveOf(a);
  const status = l.running ? 'running' : a.exists ? 'ready' : 'missing';
  pill.className = `pill pill--${status}`;
  pill.innerHTML = `<i></i>${status === 'running' ? (l.seen ? 'Running' : 'Starting') : status === 'ready' ? 'Ready to play' : 'File missing'}`;
  $('#eyebrowMeta').textContent = l.running ? `Session ${fmtClock(l.countedMs)}` : a.lastPlayedAt ? `Last played ${fmtAgo(a.lastPlayedAt)}` : 'Never played';
  $('#title').textContent = a.name;
  $('#title').title = a.name;
  if (!countUp) $('#hoursNum').textContent = fmtHours(l.total);
  $('#subline').innerHTML = `<span><b>${fmtHM(l.today)}</b> today</span><i></i><span><b>${fmtHM(weekSeconds(a, l))}</b> this week</span><i></i><span><b>${a.sessions.toLocaleString('en-US')}</b> session${a.sessions === 1 ? '' : 's'}</span>`;
  $('#artCaption').textContent = a.path.split(/[\\/]/).pop();
  $('#artCaption').title = a.path;

  const btn = $('#playBtn');
  btn.classList.toggle('is-running', l.running);
  btn.classList.toggle('is-missing', !l.running && !a.exists);
  btn.classList.toggle('is-busy', busy);
  setIcon($('#playIcon'), busy ? 'loader' : a.exists || l.running ? 'play' : 'file');
  $('#playText').textContent = busy ? 'Starting' : l.running ? (l.seen ? 'In game' : 'Starting') : a.exists ? 'Play' : 'Locate file';
  $('#playTimer').textContent = l.running ? fmtClock(l.countedMs) : '';

  const c = closing.get(a.id);
  const cb = $('#closeBtn');
  cb.hidden = !l.running;
  cb.classList.toggle('is-closing', !!c && !c.force);
  cb.classList.toggle('is-force', !!c?.force);
  cb.title = c?.force ? 'It is not responding — end the process immediately (unsaved progress may be lost)' : 'Ask the application to close';
  setIcon($('#closeIcon'), c && !c.force ? 'loader' : 'stop');
  $('#closeText').textContent = c?.force ? 'Force close' : c ? 'Closing…' : 'Close app';

  $('#statSessions').textContent = a.sessions.toLocaleString('en-US');
  $('#statSessionsSub').textContent = a.sessions ? `since ${new Date(a.addedAt).toLocaleDateString('en-US', { day: 'numeric', month: 'short' })}` : 'No sessions yet';
  $('#statLast').textContent = l.running ? 'Now' : fmtAgo(a.lastPlayedAt);
  $('#statLastSub').textContent = a.lastPlayedAt ? new Date(a.lastPlayedAt).toLocaleString('en-US', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : 'Press Play to start';
  $('#statAvg').textContent = a.sessions ? fmtHM(a.totalSeconds / a.sessions) : '—';
  $('#statAvgSub').textContent = a.sessions ? 'per session' : 'Needs one session';
  $('#statLong').textContent = a.longestSession ? fmtHM(a.longestSession) : '—';
  $('#statLongSub').textContent = a.longestSession ? 'personal record' : 'Set your first record';
  updateChart(a);
}

// ---------- Library ----------

function sortedApps() {
  const apps = [...state.apps];
  if (sort === 'hours') apps.sort((x, y) => liveOf(y).total - liveOf(x).total);
  else if (sort === 'recent') apps.sort((x, y) => (y.lastPlayedAt || 0) - (x.lastPlayedAt || 0));
  else apps.sort((x, y) => x.name.localeCompare(y.name));
  return apps;
}

function renderLibrary(intro = false) {
  if (view !== 'library') return;
  const apps = sortedApps();
  const total = apps.reduce((s, a) => s + liveOf(a).total, 0);
  const max = Math.max(1, ...apps.map((a) => liveOf(a).total));
  $('#libMeta').innerHTML = `<b>${apps.length}</b> app${apps.length === 1 ? '' : 's'} · <b id="libTotal">${fmtHours(total)}</b> hours played`;
  const grid = $('#libGrid');
  grid.classList.toggle('lib--intro', intro);
  grid.innerHTML = apps.map((a, i) => {
    const l = liveOf(a);
    const badge = l.running ? '<span class="pill pill--running"><i></i>Live</span>' : !a.exists ? '<span class="pill pill--missing"><i></i>Missing</span>' : '';
    return `<button class="lcard${a.exists ? '' : ' is-missing'}${badge ? ' has-badge' : ''}" data-open="${a.id}" style="--acc:${accentOf(a)};--i:${i}">
      <span class="lcard__glare"></span>
      <span class="lcard__rank">${String(i + 1).padStart(2, '0')}</span>
      <span class="lcard__top"><span class="lcard__icon">${iconHtml(a)}</span>${badge}</span>
      <span class="lcard__name">${esc(a.name)}</span>
      <span class="lcard__hours"><b data-lib-hours="${a.id}">${fmtHours(l.total)}</b>hours</span>
      <span class="lcard__bar"><i style="width:${Math.max(2, (l.total / max) * 100).toFixed(1)}%"></i></span>
      <span class="lcard__foot mono"><span>${a.lastPlayedAt ? fmtAgo(a.lastPlayedAt) : 'Never played'}</span><span>${total ? Math.round((l.total / total) * 100) : 0}% of total</span></span>
    </button>`;
  }).join('') + `<button class="lcard lcard--add" data-act="add" style="--i:${apps.length}">${ICONS.plus}<span>Add application</span></button>`;
}

function setView(v) {
  if (v === view) return;
  view = v;
  $$('#tabs button').forEach((b) => b.classList.toggle('is-active', b.dataset.view === v));
  $('#home').classList.toggle('is-active', v === 'home');
  $('#library').classList.toggle('is-active', v === 'library');
  moveInk();
  if (v === 'library') renderLibrary(true);
}

function moveInk() {
  const b = $('#tabs button.is-active');
  const ink = $('#tabsInk');
  ink.style.width = `${b.offsetWidth}px`;
  ink.style.transform = `translateX(${b.offsetLeft}px)`;
}

// ---------- Live chip & ticker ----------

function renderLiveChip() {
  const running = state.apps.filter((a) => liveOf(a).running);
  const chip = $('#liveChip');
  chip.hidden = !running.length;
  if (!running.length) return;
  const a = running.find((x) => x.id === state.selectedId) || running[0];
  chip.dataset.id = a.id;
  const icon = $('#liveIcon');
  const want = iconHtml(a);
  if (icon.dataset.src !== want) {
    icon.dataset.src = want;
    icon.innerHTML = want;
  }
  $('#liveName').textContent = running.length > 1 ? `${a.name} +${running.length - 1}` : a.name;
  $('#liveTime').textContent = fmtClock(liveOf(a).countedMs);
}

setInterval(() => {
  const now = Date.now();
  for (const [id, c] of closing) {
    if (!c.force && now - c.since > 7000) c.force = true;
  }
  if (!state.apps.some((a) => live.get(a.id)?.running)) return;
  const a = selected();
  if (a) updateHome(a);
  updateShelf();
  renderLiveChip();
  for (const el of $$('[data-lib-hours]')) {
    const x = byId(el.dataset.libHours);
    if (x) el.textContent = fmtHours(liveOf(x).total);
  }
}, 500);

// ---------- Launch overlay ----------

let launchTimer = null;
function showLaunch(a) {
  const ov = $('#launch');
  $('#launchTile').innerHTML = iconHtml(a);
  $('#launchBg').innerHTML = a.icon ? `<img src="${a.icon}" alt="">` : '';
  $('#launchName').textContent = a.name;
  $('#launchSub').textContent = a.sessions
    ? `across ${a.sessions} session${a.sessions === 1 ? '' : 's'} · last played ${fmtAgo(a.lastPlayedAt).toLowerCase()}`
    : 'First launch — the clock starts now.';
  const target = a.totalSeconds;
  const el = $('#launchHours');
  el.textContent = fmtHours(0);
  ov.classList.remove('is-open');
  void ov.offsetWidth;
  ov.classList.add('is-open');
  ov.setAttribute('aria-hidden', 'false');
  const t0 = performance.now() + 450;
  const step = (t) => {
    if (!ov.classList.contains('is-open')) return;
    const p = Math.max(0, Math.min(1, (t - t0) / 1500));
    el.textContent = fmtHours(target * ease(p));
    if (p < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
  clearTimeout(launchTimer);
  launchTimer = setTimeout(hideLaunch, 3100);
}
function hideLaunch() {
  clearTimeout(launchTimer);
  const ov = $('#launch');
  ov.classList.remove('is-open');
  ov.setAttribute('aria-hidden', 'true');
}
$('#launch').addEventListener('click', hideLaunch);

// ---------- Actions ----------

async function launch(a) {
  if (busy || liveOf(a).running) return;
  if (!a.exists) return changePath(a);
  busy = true;
  updateHome(a);
  showLaunch(a);
  let r;
  try {
    r = await api.launch(a.id);
  } finally {
    busy = false;
  }
  if (!r?.ok) {
    hideLaunch();
    toast('error', 'Could not launch', r?.error || 'Unknown error');
  }
  const cur = selected();
  if (cur) updateHome(cur);
}

async function closeApp(a) {
  const c = closing.get(a.id);
  if (c && !c.force) return;
  const force = !!c?.force;
  closing.set(a.id, { since: Date.now(), force: false });
  updateHome(selected() || a);
  const r = await api.closeApp(a.id, force);
  if (!r?.ok) {
    closing.delete(a.id);
    toast('error', `Could not close ${a.name}`, r?.error || 'Unknown error');
  }
  const cur = selected();
  if (cur) updateHome(cur);
}

async function addApp(file) {
  const r = await api.addApp(file);
  if (!r) return;
  if (!r.ok) return toast('error', 'Could not add', r.error);
  setView('home');
  toast(r.existed ? 'info' : 'success', r.existed ? 'Already in your library' : 'Application added', r.existed ? 'Selected it for you.' : 'Press Play to start tracking.');
}

async function changePath(a) {
  const r = await api.changePath(a.id);
  if (r && !r.ok) toast('error', 'Could not update', r.error);
  else if (r?.ok) toast('success', 'File location updated');
}

async function setCover(a) {
  const r = await api.setCover(a.id);
  if (r?.ok) toast('success', 'Background updated');
}

function select(id) {
  if (!byId(id)) return;
  if (id !== state.selectedId) {
    state.selectedId = id;
    api.select(id);
    updateShelf();
    renderHome();
    renderLiveChip();
  }
  scrollShelfToActive();
}

function step(dir) {
  const i = state.apps.findIndex((a) => a.id === state.selectedId);
  const next = state.apps[Math.min(state.apps.length - 1, Math.max(0, i + dir))];
  if (next) select(next.id);
}

document.addEventListener('click', (e) => {
  const tile = e.target.closest('.tile[data-id]');
  if (tile) return select(tile.dataset.id);
  const card = e.target.closest('[data-open]');
  if (card) {
    select(card.dataset.open);
    return setView('home');
  }
  const tab = e.target.closest('#tabs button');
  if (tab) return setView(tab.dataset.view);
  const sortBtn = e.target.closest('#sortSeg button');
  if (sortBtn) {
    sort = sortBtn.dataset.sort;
    $$('#sortSeg button').forEach((b) => b.classList.toggle('is-active', b === sortBtn));
    return renderLibrary(true);
  }
  const a = selected();
  if (e.target.closest('#playBtn') && a) return launch(a);
  if (e.target.closest('#closeBtn') && a) return closeApp(a);
  if (e.target.closest('#liveStop')) {
    const x = byId($('#liveChip').dataset.id);
    return x && closeApp(x);
  }
  const act = e.target.closest('[data-act]')?.dataset.act;
  if (!act) return;
  if (act === 'add') addApp();
  else if (a && act === 'reveal') api.reveal(a.id);
  else if (a && act === 'cover') setCover(a);
  else if (a && act === 'settings') openAppSettings(a);
});

$('#addTop').addEventListener('click', () => addApp());
$('#prefsBtn').addEventListener('click', openPrefs);
$$('[data-win]').forEach((b) => b.addEventListener('click', () => api.windowAction(b.dataset.win)));
$('#shelf').addEventListener('wheel', (e) => {
  if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
    e.preventDefault();
    $('#shelf').scrollBy({ left: e.deltaY, behavior: 'auto' });
  }
}, { passive: false });

// 3D tilt + glare on library cards
document.addEventListener('pointermove', (e) => {
  const card = e.target.closest?.('.lcard:not(.lcard--add)');
  if (!card) return;
  const r = card.getBoundingClientRect();
  const x = (e.clientX - r.left) / r.width;
  const y = (e.clientY - r.top) / r.height;
  card.style.setProperty('--mx', `${x * 100}%`);
  card.style.setProperty('--my', `${y * 100}%`);
  card.style.setProperty('--ry', `${(x - 0.5) * 12}deg`);
  card.style.setProperty('--rx', `${(0.5 - y) * 10}deg`);
});
document.addEventListener('pointerout', (e) => {
  const card = e.target.closest?.('.lcard');
  if (card && !card.contains(e.relatedTarget)) {
    card.style.setProperty('--rx', '0deg');
    card.style.setProperty('--ry', '0deg');
  }
});

document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') { closeDrawer(); hideLaunch(); return; }
  if (e.ctrlKey && e.key.toLowerCase() === 'o') { e.preventDefault(); addApp(); return; }
  if (e.target.closest('input, .drawer.is-open') || view !== 'home') return;
  if (e.key === 'ArrowRight') step(1);
  else if (e.key === 'ArrowLeft') step(-1);
  else if (e.key === 'Enter' && selected() && !e.target.closest('button')) launch(selected());
});

window.addEventListener('resize', moveInk);

// ---------- Drawer ----------

function openDrawer(html) {
  const d = $('#drawer');
  d.innerHTML = html;
  hydrateIcons(d);
  d.classList.remove('is-open');
  void d.offsetWidth;
  d.classList.add('is-open');
  d.setAttribute('aria-hidden', 'false');
  $('#drawerBackdrop').classList.add('is-open');
}
function closeDrawer() {
  $('#drawer').classList.remove('is-open');
  $('#drawer').setAttribute('aria-hidden', 'true');
  $('#drawerBackdrop').classList.remove('is-open');
}
$('#drawerBackdrop').addEventListener('click', closeDrawer);

function armConfirm(btn, label, action) {
  if (btn.classList.contains('is-armed')) {
    btn.classList.remove('is-armed');
    action();
    return;
  }
  const original = btn.innerHTML;
  btn.classList.add('is-armed');
  btn.textContent = label;
  setTimeout(() => {
    if (btn.isConnected && btn.classList.contains('is-armed')) {
      btn.classList.remove('is-armed');
      btn.innerHTML = original;
    }
  }, 3000);
}

function openAppSettings(a) {
  openDrawer(`
    <div class="drawer__head"><div class="drawer__title"><span class="drawer__icon">${iconHtml(a)}</span><div><span class="label">App settings</span><h2>${esc(a.name)}</h2></div></div><button class="drawer__close" data-close data-icon="close"></button></div>
    <div class="drawer__body">
      <label class="field"><span>Display name</span><input class="input" id="fName" maxlength="60"></label>
      <label class="field"><span>Launch arguments <em>optional</em></span><input class="input mono" id="fArgs" placeholder="-windowed -novid"></label>
      <label class="field"><span>Also track process <em>optional</em></span><input class="input mono" id="fTrack" placeholder="e.g. GTA5.exe">
        <small>Use this when the .exe is a launcher that starts the real game under a different name. Separate several with commas. "Close app" closes these too.</small></label>
      <div class="field"><span>Executable</span><div class="pathbox mono" id="fPath"></div>
        <div class="row"><button class="btn" data-d="change"><span data-icon="file"></span>Change file…</button><button class="btn btn--ghost" data-d="reveal"><span data-icon="folder"></span>Show in folder</button></div></div>
      <div class="field"><span>Background image <em>optional — replaces the icon backdrop</em></span>
        <div class="row"><button class="btn" data-d="cover"><span data-icon="image"></span>Choose image…</button>${a.cover ? '<button class="btn btn--ghost" data-d="clear-cover">Use icon backdrop</button>' : ''}</div></div>
      <div class="danger"><span class="label">Danger zone</span>
        <div class="row"><button class="btn btn--danger" data-d="reset"><span data-icon="refresh"></span>Reset playtime</button><button class="btn btn--danger" data-d="remove"><span data-icon="trash"></span>Remove from library</button></div></div>
    </div>
    <div class="drawer__foot"><button class="btn btn--ghost" data-close>Cancel</button><button class="btn btn--primary" data-d="save"><span data-icon="check"></span>Save changes</button></div>`);
  $('#fName').value = a.name;
  $('#fArgs').value = a.args;
  $('#fTrack').value = a.trackProcess;
  $('#fPath').textContent = a.path;
  $('#drawer').onclick = async (e) => {
    if (e.target.closest('[data-close]')) return closeDrawer();
    const btn = e.target.closest('[data-d]');
    if (!btn) return;
    const d = btn.dataset.d;
    if (d === 'save') {
      await api.updateApp(a.id, { name: $('#fName').value, args: $('#fArgs').value, trackProcess: $('#fTrack').value });
      closeDrawer();
      toast('success', 'Saved', `${$('#fName').value.trim() || a.name} updated.`, a);
    } else if (d === 'change') {
      await changePath(a);
      const cur = byId(a.id);
      if (cur) $('#fPath').textContent = cur.path;
    } else if (d === 'reveal') api.reveal(a.id);
    else if (d === 'cover') { await setCover(a); closeDrawer(); }
    else if (d === 'clear-cover') { await api.clearCover(a.id); closeDrawer(); }
    else if (d === 'reset') armConfirm(btn, 'Click again to reset', async () => { await api.resetStats(a.id); stageKey = null; closeDrawer(); toast('info', 'Playtime reset', a.name, a); });
    else if (d === 'remove') armConfirm(btn, 'Click again to remove', async () => {
      const r = await api.removeApp(a.id);
      if (r && !r.ok) return toast('error', 'Could not remove', r.error);
      closeDrawer();
      toast('info', 'Removed from library', a.name, a);
    });
  };
  $('#drawer').onchange = null;
}

function openPrefs() {
  const s = state.settings;
  openDrawer(`
    <div class="drawer__head"><div class="drawer__title"><span class="drawer__icon"><img src="../../assets/icon.png" alt=""></span><div><span class="label">Preferences</span><h2>Oglu settings</h2></div></div><button class="drawer__close" data-close data-icon="close"></button></div>
    <div class="drawer__body">
      <label class="toggle"><input type="checkbox" data-setting="minimizeOnLaunch" ${s.minimizeOnLaunch ? 'checked' : ''}><span class="toggle__track"><i></i></span>
        <span class="toggle__text"><b>Minimize when an app starts</b><small>Oglu gets out of the way and comes back when you close the app.</small></span></label>
      <label class="toggle"><input type="checkbox" data-setting="closeToTray" ${s.closeToTray ? 'checked' : ''}><span class="toggle__track"><i></i></span>
        <span class="toggle__text"><b>Keep tracking in the tray</b><small>Closing the window while an app runs keeps Oglu counting in the system tray.</small></span></label>
      <div class="field"><span>Your data</span><div class="pathbox mono">${esc(state.dataDir || '')}</div>
        <div class="row"><button class="btn" data-d="data"><span data-icon="folder"></span>Open data folder</button></div></div>
      <div class="about"><b>OGLU ${esc(state.version ? `v${state.version}` : '')}</b>
        <span>After you press Play, time counts for as long as the application's process is running. Time while your PC sleeps isn't counted.</span>
        <span class="mono">Ctrl+O add app · ←/→ switch · Enter play · Esc close panels</span></div>
    </div>`);
  $('#drawer').onclick = (e) => {
    if (e.target.closest('[data-close]')) return closeDrawer();
    if (e.target.closest('[data-d="data"]')) api.openDataDir();
  };
  $('#drawer').onchange = (e) => {
    const k = e.target.dataset.setting;
    if (k) api.updateSettings({ [k]: e.target.checked });
  };
}

// ---------- Toasts ----------

function toast(type, title, text = '', app = null) {
  const icon = type === 'success' ? 'check' : type === 'error' ? 'alert' : 'info';
  const life = type === 'error' ? 6500 : 4000;
  const el = document.createElement('div');
  el.className = `toast toast--${type}`;
  el.style.setProperty('--life', `${life}ms`);
  const iconInner = app?.icon ? `<img src="${app.icon}" alt="">` : ICONS[icon];
  el.innerHTML = `<span class="toast__icon">${iconInner}</span><span class="toast__text"><b>${esc(title)}</b>${text ? `<span>${esc(text)}</span>` : ''}</span>`;
  $('#toasts').appendChild(el);
  const remove = () => { el.classList.add('is-leaving'); setTimeout(() => el.remove(), 350); };
  const t = setTimeout(remove, life);
  el.addEventListener('click', () => { clearTimeout(t); remove(); });
}

// ---------- Drag & drop ----------

let dragDepth = 0;
const hasFiles = (e) => [...(e.dataTransfer?.types || [])].includes('Files');
window.addEventListener('dragenter', (e) => { if (!hasFiles(e)) return; e.preventDefault(); dragDepth++; $('#dropzone').classList.add('is-active'); });
window.addEventListener('dragover', (e) => { if (hasFiles(e)) e.preventDefault(); });
window.addEventListener('dragleave', () => { if (--dragDepth <= 0) { dragDepth = 0; $('#dropzone').classList.remove('is-active'); } });
window.addEventListener('drop', async (e) => {
  e.preventDefault();
  dragDepth = 0;
  $('#dropzone').classList.remove('is-active');
  for (const f of e.dataTransfer?.files || []) {
    const p = api.pathForFile(f);
    if (p) await addApp(p);
  }
});

// ---------- Embers ----------

(function embers() {
  const c = $('#embers');
  const ctx = c.getContext('2d');
  let w = 0;
  let h = 0;
  let dots = [];
  const resize = () => {
    const dpr = window.devicePixelRatio || 1;
    w = c.clientWidth;
    h = c.clientHeight;
    c.width = w * dpr;
    c.height = h * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    dots = Array.from({ length: Math.round((w * h) / 24000) }, () => ({
      x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.7 + 0.4,
      vy: -(Math.random() * 0.3 + 0.06), ph: Math.random() * Math.PI * 2, white: Math.random() < 0.3,
    }));
  };
  resize();
  window.addEventListener('resize', resize);
  const frame = (t) => {
    ctx.clearRect(0, 0, w, h);
    for (const d of dots) {
      d.y += d.vy;
      d.x += Math.sin(t / 2000 + d.ph) * 0.18;
      if (d.y < -5) { d.y = h + 5; d.x = Math.random() * w; }
      const a = 0.2 + 0.4 * Math.sin(t / 900 + d.ph) ** 2;
      ctx.beginPath();
      ctx.arc(d.x, d.y, d.r, 0, Math.PI * 2);
      ctx.fillStyle = `rgba(${d.white ? '255,255,255' : emberRGB},${a})`;
      ctx.fill();
    }
    requestAnimationFrame(frame);
  };
  if (!matchMedia('(prefers-reduced-motion: reduce)').matches) requestAnimationFrame(frame);
})();

// ---------- Boot ----------

let stateSeq = 0;
async function applyState(s) {
  const seq = ++stateSeq;
  await computeAccents(s.apps);
  if (seq !== stateSeq) return;
  state = s;
  syncLive();
  renderShelf();
  renderHome();
  renderLiveChip();
  renderLibrary();
}

api.on('state', applyState);

api.on('tick', ({ id, totalSeconds, today, session }) => {
  live.set(id, { total: totalSeconds, today, countedMs: session?.countedMs || 0, running: !!session, seen: !!session?.seen, at: performance.now() });
});

api.on('session-end', ({ id, seconds, totalSeconds }) => {
  const a = byId(id);
  closing.delete(id);
  if (!a) return;
  if (seconds >= 1) toast('success', `Session ended · +${fmtHM(seconds)}`, `${a.name} — ${fmtHours(totalSeconds)} hours in total`, a);
  else toast('info', `${a.name} closed`, 'It exited before any time was counted.', a);
  requestAnimationFrame(() => $('#hoursNum')?.classList.add('bump'));
  setTimeout(() => $('#hoursNum')?.classList.remove('bump'), 800);
});

api.on('win-state', ({ maximized }) => setIcon($('#maxBtn'), maximized ? 'restore' : 'max'));

hydrateIcons();
const splashStart = performance.now();
api.getState().then(async (s) => {
  await applyState(s);
  moveInk();
  const wait = Math.max(0, 1900 - (performance.now() - splashStart));
  setTimeout(() => {
    $('#splash').classList.add('splash--out');
    setTimeout(() => $('#splash').remove(), 900);
    // Replay the entrance animations now that the splash is gone.
    stageKey = null;
    shelfKey = null;
    shelfIntro = true;
    renderShelf();
    renderHome();
    scrollShelfToActive();
  }, wait);
});
