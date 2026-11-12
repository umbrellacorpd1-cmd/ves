'use strict';

const api = window.oglu;
const $ = (s, root = document) => root.querySelector(s);
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

const svg = (body, extra = '') => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ${extra}>${body}</svg>`;
const ICONS = {
  play: svg('<path d="M7 4.5v15l12-7.5z" fill="currentColor"/>'),
  folder: svg('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  image: svg('<rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="9" cy="10" r="2"/><path d="m21 16-5-5-9 9"/>'),
  gear: svg('<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>'),
  sliders: svg('<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6"/>'),
  plus: svg('<path d="M12 5v14M5 12h14"/>'),
  close: svg('<path d="M6 6l12 12M18 6 6 18"/>', 'stroke-width="1.6"'),
  min: svg('<path d="M5 12h14"/>', 'stroke-width="1.6"'),
  max: svg('<rect x="5" y="5" width="14" height="14" rx="2"/>', 'stroke-width="1.6"'),
  restore: svg('<rect x="4" y="8" width="12" height="12" rx="2"/><path d="M8 8V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-2"/>', 'stroke-width="1.6"'),
  clock: svg('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>'),
  flame: svg('<path d="M12 22c4 0 7-2.7 7-7 0-4-3-6-4-10-2 2-2 4-2 5-1-1-2-2-2-4-3 2-6 5.5-6 9 0 4.3 3 7 7 7z"/>'),
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
const LOGO = `<svg viewBox="0 0 64 64"><defs><linearGradient id="lg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff5c8a"/><stop offset=".55" stop-color="#e81c5a"/><stop offset="1" stop-color="#ff6a3d"/></linearGradient></defs><circle cx="32" cy="32" r="24" fill="none" stroke="url(#lg)" stroke-width="7" stroke-linecap="round" stroke-dasharray="125 26" transform="rotate(-48 32 32)"/><path d="M27 22.5 L43 32 L27 41.5 Z" fill="url(#lg)" stroke="url(#lg)" stroke-width="2" stroke-linejoin="round"/></svg>`;

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
const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
function fmtAgo(ts) {
  if (!ts) return 'Never';
  const diff = (ts - Date.now()) / 1000;
  const a = Math.abs(diff);
  if (a < 60) return 'Just now';
  if (a < 3600) return rtf.format(Math.round(diff / 60), 'minute');
  if (a < 86400) return rtf.format(Math.round(diff / 3600), 'hour');
  if (a < 86400 * 30) return rtf.format(Math.round(diff / 86400), 'day');
  return new Date(ts).toLocaleDateString('en-US', { day: 'numeric', month: 'short', year: 'numeric' });
}
const initial = (name) => (String(name).trim()[0] || '?').toUpperCase();
function fmtAgoShort(ts) {
  if (!ts) return 'Never';
  const s = (Date.now() - ts) / 1000;
  if (s < 60) return 'Just now';
  if (s < 3600) return `${Math.round(s / 60)}m ago`;
  if (s < 86400) return `${Math.round(s / 3600)}h ago`;
  if (s < 86400 * 2) return 'Yesterday';
  if (s < 86400 * 30) return `${Math.round(s / 86400)}d ago`;
  return new Date(ts).toLocaleDateString('en-US', { day: 'numeric', month: 'short' });
}
/** Keep the drive/root and file name of long paths: C:\Games\…\Bin\Game.exe */
function shortPath(p, max = 64) {
  if (p.length <= max) return p;
  const sep = p.includes('\\') ? '\\' : '/';
  const parts = p.split(sep);
  let tail = parts.pop();
  const head = parts.shift();
  while (parts.length && `${head}${sep}…${sep}${parts[parts.length - 1]}${sep}${tail}`.length <= max) tail = `${parts.pop()}${sep}${tail}`;
  return `${head}${sep}…${sep}${tail}`;
}
const iconHtml = (a) => (a.icon ? `<img src="${a.icon}" alt="">` : `<b>${esc(initial(a.name))}</b>`);
const ease = (t) => 1 - Math.pow(1 - t, 4);

// ---------- State ----------

let state = { apps: [], selectedId: null, settings: {} };
const live = new Map(); // id -> { total, today, countedMs, running, seen, at }
let renderedKey;
let countUp = null;
let busy = false;
let firstSidebar = true;

const selected = () => state.apps.find((a) => a.id === state.selectedId) || null;

function syncLive() {
  const today = dayKey();
  for (const a of state.apps) {
    live.set(a.id, {
      total: a.totalSeconds,
      today: a.days[today] || 0,
      countedMs: a.session?.countedMs || 0,
      running: !!a.session,
      seen: !!a.session?.seen,
      startedAt: a.session?.startedAt || 0,
      at: performance.now(),
    });
  }
}

/** Interpolated live numbers so counters move smoothly between 2s polls. */
function liveOf(a) {
  const l = live.get(a.id);
  if (!l) return { total: a.totalSeconds, today: 0, countedMs: 0, running: false };
  const extra = l.running && l.seen ? Math.min(performance.now() - l.at, 2500) : 0;
  return { total: l.total + extra / 1000, today: l.today + extra / 1000, countedMs: l.countedMs + extra, running: l.running, seen: l.seen };
}

// ---------- Sidebar ----------

function renderSidebar() {
  const list = $('#appList');
  $('#libCount').textContent = state.apps.length;
  if (!state.apps.length) {
    list.innerHTML = '<div class="applist__empty">No applications yet</div>';
  } else {
    list.innerHTML = state.apps.map((a, i) => {
      const l = liveOf(a);
      const status = l.running ? ' · <em>LIVE</em>' : !a.exists ? ' · <span class="warn">MISSING</span>' : '';
      return `<button class="app-item${a.id === state.selectedId ? ' is-active' : ''}" data-id="${a.id}" style="--i:${i}">
        <span class="app-item__icon">${iconHtml(a)}${l.running ? '<i class="app-item__live"></i>' : ''}</span>
        <span class="app-item__body">
          <span class="app-item__name">${esc(a.name)}</span>
          <span class="app-item__meta mono"><span data-hours="${a.id}">${fmtHours(l.total)}</span> h${status}</span>
        </span>
      </button>`;
    }).join('');
  }
  list.classList.toggle('applist--intro', firstSidebar);
  if (firstSidebar && state.apps.length) firstSidebar = false;
  updateTotals();
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

function updateTotals() {
  let total = 0;
  let week = 0;
  for (const a of state.apps) {
    const l = liveOf(a);
    total += l.total;
    week += weekSeconds(a, l);
  }
  $('#totalHours').textContent = fmtHours(total);
  $('#totalWeek').textContent = `${fmtHours(week)} h this week`;
  $('#totalBar').style.width = `${Math.min(100, (week / (7 * 3600 * 4)) * 100).toFixed(1)}%`;
}

// ---------- Main ----------

function renderMain() {
  const a = selected();
  const key = a ? a.id : 'empty';
  if (key !== renderedKey) {
    renderedKey = key;
    $('#main').innerHTML = a ? heroTemplate(a) : emptyTemplate();
    hydrateIcons($('#main'));
    if (a) {
      buildChart(a);
      startCountUp(a);
    }
  }
  if (a) updateMain(a);
}

function emptyTemplate() {
  return `<section class="empty">
    <div class="orb reveal"><div class="orb__ring"></div><div class="orb__ring orb__ring--2"></div><div class="orb__core">${LOGO}</div></div>
    <h1 class="reveal" style="--d:.1s">Your library is <span>empty</span></h1>
    <p class="reveal" style="--d:.18s">Add any .exe — Oglu launches it and counts every hour you spend inside.</p>
    <button class="play reveal" style="--d:.26s" data-act="add"><span class="play__icon" data-icon="plus"></span><span>Select application</span></button>
    <div class="empty__hint mono reveal" style="--d:.34s">or drag &amp; drop an .exe anywhere</div>
    <div class="steps">
      <div class="step spot reveal" style="--d:.42s"><div class="step__n">01</div><b>Pick the .exe</b><span>Any game or program on your PC.</span></div>
      <div class="step spot reveal" style="--d:.5s"><div class="step__n">02</div><b>Press Play</b><span>Oglu starts it and begins counting.</span></div>
      <div class="step spot reveal" style="--d:.58s"><div class="step__n">03</div><b>Watch hours grow</b><span>Totals, sessions and daily activity.</span></div>
    </div>
  </section>`;
}

function heroTemplate(a) {
  return `<section class="hero reveal" id="hero">
      ${a.cover ? `<div class="hero__cover" style="background-image:url('${a.cover}')"></div>` : `<div class="hero__stripes"></div><div class="hero__letter">${esc(initial(a.name))}</div>`}
      <div class="hero__shine"></div>
      <i class="corner corner--tl"></i><i class="corner corner--tr"></i><i class="corner corner--bl"></i><i class="corner corner--br"></i>
      <div class="hero__content">
        <div class="hero__top reveal" style="--d:.08s"><span class="pill" id="pill"></span><span class="hero__path mono" id="heroPath"></span></div>
        <h1 class="hero__title reveal" style="--d:.14s" id="heroTitle"></h1>
        <div class="hours reveal" style="--d:.22s">
          <span class="label mono">Time spent in app</span>
          <div class="hours__value"><span class="hours__num" id="heroHours">0.0</span><span class="hours__unit">hours</span></div>
          <div class="hours__sub" id="heroSub"></div>
        </div>
        <div class="hero__actions reveal" style="--d:.3s">
          <button class="play" id="playBtn"><span class="play__icon" id="playIcon"></span><span id="playText">Play</span><span class="play__timer" id="playTimer"></span></button>
          <button class="icon-btn" data-act="reveal" title="Show in folder" data-icon="folder"></button>
          <button class="icon-btn" data-act="cover" title="Set cover image" data-icon="image"></button>
          <button class="icon-btn" data-act="settings" title="App settings" data-icon="gear"></button>
        </div>
      </div>
      <div class="hero__side reveal" style="--d:.2s">
        <div class="tile" id="heroTile">${iconHtml(a)}</div>
        <div class="hero__proc mono" id="heroProc"></div>
      </div>
    </section>
    <section class="stats">
      ${statCard('clock', 'Sessions', 'statSessions', '.36s')}
      ${statCard('calendar', 'Last launched', 'statLast', '.42s')}
      ${statCard('layers', 'Average session', 'statAvg', '.48s')}
      ${statCard('trophy', 'Longest session', 'statLong', '.54s')}
    </section>
    <section class="panel reveal" style="--d:.6s">
      <div class="panel__head">
        <div><span class="label mono">Activity</span><h3>Last 14 days</h3></div>
        <div class="panel__meta"><span>Today<b id="metaToday">0.0 h</b></span><span>This week<b id="metaWeek">0.0 h</b></span></div>
      </div>
      <div class="chart" id="chart"></div>
    </section>`;
}

function statCard(icon, label, id, d) {
  return `<div class="card spot reveal" style="--d:${d}">
    <div class="card__icon" data-icon="${icon}"></div>
    <div class="card__body">
      <span class="label mono">${label}</span>
      <div class="card__value" id="${id}">—</div>
      <div class="card__sub" id="${id}Sub"></div>
    </div>
  </div>`;
}

function chartDays() {
  const d = new Date();
  return Array.from({ length: 14 }, (_, i) => new Date(d.getFullYear(), d.getMonth(), d.getDate() - 13 + i));
}

function buildChart(a) {
  const today = dayKey();
  $('#chart').innerHTML = chartDays().map((d, i) => {
    const k = dayKey(d);
    return `<div class="bar${k === today ? ' is-today' : ''}" data-day="${k}" style="--delay:${0.5 + i * 0.035}s">
      <div class="bar__track"><div class="bar__fill"></div></div>
      <span class="bar__label">${d.toLocaleDateString('en-US', { weekday: 'short' }).slice(0, 2)}</span>
      <span class="bar__tip"></span>
    </div>`;
  }).join('');
  // Next frame so bars grow from zero.
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
  $('#metaToday').textContent = `${fmtHours(l.today)} h`;
  $('#metaWeek').textContent = `${fmtHours(weekSeconds(a, l))} h`;
}

function startCountUp(a) {
  const target = liveOf(a).total;
  const from = 0;
  const t0 = performance.now();
  const dur = 1400;
  countUp = { id: a.id };
  const el = $('#heroHours');
  const step = (t) => {
    if (!countUp || countUp.id !== a.id || !el.isConnected) return;
    const p = Math.min(1, (t - t0) / dur);
    el.textContent = fmtHours(from + (target - from) * ease(p));
    if (p < 1) requestAnimationFrame(step);
    else countUp = null;
  };
  requestAnimationFrame(step);
}

function updateMain(a) {
  const l = liveOf(a);
  const pill = $('#pill');
  if (!pill) return;
  const status = l.running ? 'running' : a.exists ? 'ready' : 'missing';
  pill.className = `pill pill--${status}`;
  pill.innerHTML = `<i></i>${status === 'running' ? (l.seen ? 'Running' : 'Starting') : status === 'ready' ? 'Ready' : 'File missing'}`;
  $('#heroPath').textContent = shortPath(a.path);
  $('#heroPath').title = a.path;
  $('#heroTitle').textContent = a.name;
  $('#heroProc').textContent = a.path.split(/[\\/]/).pop();
  if (!countUp) $('#heroHours').textContent = fmtHours(l.total);
  $('#heroSub').innerHTML = `<span><b>${fmtHM(l.total)}</b> in total</span><i class="sep"></i><span><b>${fmtHM(l.today)}</b> today</span>${a.sessions ? `<i class="sep"></i><span>last played <b>${esc(fmtAgo(a.lastPlayedAt))}</b></span>` : ''}`;

  const tile = $('#heroTile');
  const want = a.icon ? `<img src="${a.icon}" alt="">` : `<b>${esc(initial(a.name))}</b>`;
  if (tile.dataset.src !== want) {
    tile.dataset.src = want;
    tile.innerHTML = want;
  }

  const btn = $('#playBtn');
  btn.classList.toggle('is-running', l.running);
  btn.classList.toggle('is-missing', !l.running && !a.exists);
  btn.classList.toggle('is-busy', busy);
  const icon = busy ? 'loader' : l.running ? 'play' : a.exists ? 'play' : 'file';
  const iconEl = $('#playIcon');
  if (iconEl.dataset.icon !== icon) {
    iconEl.dataset.icon = icon;
    iconEl.innerHTML = ICONS[icon];
  }
  $('#playText').textContent = busy ? 'Starting' : l.running ? (l.seen ? 'In game' : 'Starting') : a.exists ? 'Play' : 'Locate file';
  $('#playTimer').textContent = l.running ? fmtClock(l.countedMs) : '';

  $('#statSessions').textContent = a.sessions.toLocaleString('en-US');
  $('#statSessionsSub').textContent = a.sessions ? `since ${new Date(a.addedAt).toLocaleDateString('en-US', { day: 'numeric', month: 'short' })}` : 'No sessions yet';
  $('#statLast').textContent = l.running ? 'Now' : fmtAgoShort(a.lastPlayedAt);
  $('#statLastSub').textContent = a.lastPlayedAt ? new Date(a.lastPlayedAt).toLocaleString('en-US', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : 'Press Play to start';
  $('#statAvg').textContent = a.sessions ? fmtHM(a.totalSeconds / a.sessions) : '—';
  $('#statAvgSub').textContent = a.sessions ? 'per session' : 'Needs one session';
  $('#statLong').textContent = a.longestSession ? fmtHM(a.longestSession) : '—';
  $('#statLongSub').textContent = a.longestSession ? 'personal record' : 'Set your first record';
  updateChart(a);
}

// ---------- Live ticker ----------

function renderLiveChip() {
  const running = state.apps.filter((a) => liveOf(a).running);
  const chip = $('#liveChip');
  chip.hidden = !running.length;
  if (running.length) {
    const a = running[0];
    $('#liveName').textContent = running.length > 1 ? `${a.name} +${running.length - 1}` : a.name;
    $('#liveTime').textContent = fmtClock(liveOf(a).countedMs);
  }
}

setInterval(() => {
  if (!state.apps.some((a) => live.get(a.id)?.running)) return;
  const a = selected();
  if (a) updateMain(a);
  for (const x of state.apps) {
    const el = document.querySelector(`[data-hours="${x.id}"]`);
    if (el) el.textContent = fmtHours(liveOf(x).total);
  }
  updateTotals();
  renderLiveChip();
}, 500);

// ---------- Launch overlay ----------

let launchTimer = null;
function showLaunch(a) {
  const ov = $('#launch');
  $('#launchTile').innerHTML = iconHtml(a);
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

async function launch(a) {
  if (busy || liveOf(a).running) return;
  if (!a.exists) return changePath(a);
  busy = true;
  updateMain(a);
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
  if (cur) updateMain(cur);
}

// ---------- Actions ----------

async function addApp(file) {
  const r = await api.addApp(file);
  if (!r) return;
  if (!r.ok) return toast('error', 'Could not add', r.error);
  toast(r.existed ? 'info' : 'success', r.existed ? 'Already in your library' : 'Application added', r.existed ? 'Selected it for you.' : 'Press Play to start tracking.');
}

async function changePath(a) {
  const r = await api.changePath(a.id);
  if (r && !r.ok) toast('error', 'Could not update', r.error);
  else if (r?.ok) toast('success', 'File location updated');
}

async function setCover(a) {
  const r = await api.setCover(a.id);
  if (r?.ok) {
    renderedKey = null;
    toast('success', 'Cover updated');
  }
}

function select(id) {
  if (id === state.selectedId) return;
  state.selectedId = id;
  api.select(id);
  renderSidebar();
  renderMain();
  $('#main').scrollTop = 0;
}

document.addEventListener('click', (e) => {
  const item = e.target.closest('.app-item');
  if (item) return select(item.dataset.id);
  const a = selected();
  if (e.target.closest('#playBtn') && a) return launch(a);
  const act = e.target.closest('[data-act]')?.dataset.act;
  if (!act) return;
  if (act === 'add') addApp();
  else if (a && act === 'reveal') api.reveal(a.id);
  else if (a && act === 'cover') setCover(a);
  else if (a && act === 'settings') openAppSettings(a);
});

$('#addBtn').addEventListener('click', () => addApp());
$('#prefsBtn').addEventListener('click', openPrefs);
document.querySelectorAll('[data-win]').forEach((b) => b.addEventListener('click', () => api.windowAction(b.dataset.win)));

// Cursor spotlight
document.addEventListener('pointermove', (e) => {
  const el = e.target.closest?.('.spot');
  if (!el) return;
  const r = el.getBoundingClientRect();
  el.style.setProperty('--mx', `${e.clientX - r.left}px`);
  el.style.setProperty('--my', `${e.clientY - r.top}px`);
});

document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') { closeDrawer(); hideLaunch(); }
  if (e.ctrlKey && e.key.toLowerCase() === 'o') { e.preventDefault(); addApp(); }
});

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
    <div class="drawer__head"><div><span class="label mono">App settings</span><h2>${esc(a.name)}</h2></div><button class="icon-btn" data-close data-icon="close"></button></div>
    <div class="drawer__body">
      <label class="field"><span>Display name</span><input class="input" id="fName" maxlength="60"></label>
      <label class="field"><span>Launch arguments <em>optional</em></span><input class="input mono" id="fArgs" placeholder="-windowed -novid"></label>
      <label class="field"><span>Also track process <em>optional</em></span><input class="input mono" id="fTrack" placeholder="e.g. GTA5.exe">
        <small>Use this when the .exe is a launcher that starts the real game under a different name. Separate several with commas.</small></label>
      <div class="field"><span>Executable</span><div class="pathbox mono" id="fPath"></div>
        <div class="row"><button class="btn" data-d="change"><span data-icon="file"></span>Change file…</button><button class="btn btn--ghost" data-d="reveal"><span data-icon="folder"></span>Show in folder</button></div></div>
      <div class="field"><span>Cover image</span>
        <div class="row"><button class="btn" data-d="cover"><span data-icon="image"></span>Choose image…</button>${a.cover ? '<button class="btn btn--ghost" data-d="clear-cover">Remove cover</button>' : ''}</div></div>
      <div class="danger"><span class="label mono">Danger zone</span>
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
      toast('success', 'Saved', `${$('#fName').value.trim() || a.name} updated.`);
    } else if (d === 'change') {
      await changePath(a);
      const cur = state.apps.find((x) => x.id === a.id);
      if (cur) $('#fPath').textContent = cur.path;
    } else if (d === 'reveal') api.reveal(a.id);
    else if (d === 'cover') { await setCover(a); closeDrawer(); }
    else if (d === 'clear-cover') { await api.clearCover(a.id); renderedKey = null; closeDrawer(); }
    else if (d === 'reset') armConfirm(btn, 'Click again to reset', async () => { await api.resetStats(a.id); renderedKey = null; closeDrawer(); toast('info', 'Playtime reset'); });
    else if (d === 'remove') armConfirm(btn, 'Click again to remove', async () => {
      const r = await api.removeApp(a.id);
      if (r && !r.ok) return toast('error', 'Could not remove', r.error);
      closeDrawer();
      toast('info', 'Removed from library', a.name);
    });
  };
}

function openPrefs() {
  const s = state.settings;
  openDrawer(`
    <div class="drawer__head"><div><span class="label mono">Preferences</span><h2>Oglu settings</h2></div><button class="icon-btn" data-close data-icon="close"></button></div>
    <div class="drawer__body">
      <label class="toggle"><input type="checkbox" data-setting="minimizeOnLaunch" ${s.minimizeOnLaunch ? 'checked' : ''}><span class="toggle__track"><i></i></span>
        <span class="toggle__text"><b>Minimize when an app starts</b><small>Oglu gets out of the way and comes back when you close the app.</small></span></label>
      <label class="toggle"><input type="checkbox" data-setting="closeToTray" ${s.closeToTray ? 'checked' : ''}><span class="toggle__track"><i></i></span>
        <span class="toggle__text"><b>Keep tracking in the tray</b><small>Closing the window while an app runs keeps Oglu counting in the system tray.</small></span></label>
      <div class="field"><span>Your data</span><div class="pathbox mono">${esc(state.dataDir || '')}</div>
        <div class="row"><button class="btn" data-d="data"><span data-icon="folder"></span>Open data folder</button></div></div>
      <div class="about"><b>OGLU ${esc(state.version ? `v${state.version}` : '')}</b>
        <span>After you press Play, time counts for as long as the application's process is running. Time while your PC sleeps isn't counted.</span>
        <span class="mono">Ctrl+O — add app · Esc — close panels</span></div>
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

function toast(type, title, text = '') {
  const icon = type === 'success' ? 'check' : type === 'error' ? 'alert' : 'info';
  const life = type === 'error' ? 6000 : 4000;
  const el = document.createElement('div');
  el.className = `toast toast--${type}`;
  el.style.setProperty('--life', `${life}ms`);
  el.innerHTML = `<span class="toast__icon">${ICONS[icon]}</span><span class="toast__text"><b>${esc(title)}</b>${text ? `<span>${esc(text)}</span>` : ''}</span>`;
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

// ---------- Particles ----------

(function particles() {
  const c = $('#particles');
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
    dots = Array.from({ length: Math.round((w * h) / 26000) }, () => ({
      x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.6 + 0.4,
      vy: -(Math.random() * 0.25 + 0.05), ph: Math.random() * Math.PI * 2, hue: Math.random() < 0.7 ? '255,77,125' : '255,255,255',
    }));
  };
  resize();
  window.addEventListener('resize', resize);
  const frame = (t) => {
    ctx.clearRect(0, 0, w, h);
    for (const d of dots) {
      d.y += d.vy;
      d.x += Math.sin(t / 2000 + d.ph) * 0.15;
      if (d.y < -5) { d.y = h + 5; d.x = Math.random() * w; }
      const a = 0.25 + 0.35 * Math.sin(t / 900 + d.ph) ** 2;
      ctx.beginPath();
      ctx.arc(d.x, d.y, d.r, 0, Math.PI * 2);
      ctx.fillStyle = `rgba(${d.hue},${a})`;
      ctx.fill();
    }
    requestAnimationFrame(frame);
  };
  if (!matchMedia('(prefers-reduced-motion: reduce)').matches) requestAnimationFrame(frame);
})();

// ---------- Boot ----------

function applyState(s) {
  state = s;
  syncLive();
  renderSidebar();
  renderMain();
  renderLiveChip();
}

api.on('state', applyState);

api.on('tick', ({ id, totalSeconds, today, session }) => {
  live.set(id, { total: totalSeconds, today, countedMs: session?.countedMs || 0, running: !!session, seen: !!session?.seen, at: performance.now() });
});

api.on('session-end', ({ id, seconds, totalSeconds }) => {
  const a = state.apps.find((x) => x.id === id);
  if (!a) return;
  if (seconds >= 1) toast('success', `Session ended · +${fmtHM(seconds)}`, `${a.name} — ${fmtHours(totalSeconds)} hours in total`);
  else toast('info', `${a.name} closed`, 'It exited before any time was counted.');
  requestAnimationFrame(() => $('#heroHours')?.classList.add('bump'));
  setTimeout(() => $('#heroHours')?.classList.remove('bump'), 800);
});

api.on('win-state', ({ maximized }) => {
  const b = $('#maxBtn');
  b.dataset.icon = maximized ? 'restore' : 'max';
  b.innerHTML = ICONS[b.dataset.icon];
});

hydrateIcons();
const splashStart = performance.now();
api.getState().then((s) => {
  applyState(s);
  $('#version').textContent = `v${s.version}`;
  const wait = Math.max(0, 1900 - (performance.now() - splashStart));
  setTimeout(() => {
    $('#splash').classList.add('splash--out');
    setTimeout(() => $('#splash').remove(), 900);
    if (renderedKey && selected()) { renderedKey = null; renderMain(); }
  }, wait);
});
