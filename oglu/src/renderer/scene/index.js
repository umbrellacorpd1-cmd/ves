// WebGL scenes for Oglu (bundled with three.js into vendor/scene.js by scripts/bundle.js).
// - createToken: the selected app's icon as a glossy 3D tile that floats, follows the mouse and spins on change.
// - createWarp: the launch animation — hyperspace streaks with the tile flying in.
import {
  AdditiveBlending, BufferAttribute, BufferGeometry, CanvasTexture, Color, DirectionalLight, ExtrudeGeometry,
  Group, LineBasicMaterial, LineSegments, Mesh, MeshBasicMaterial, MeshPhysicalMaterial, MeshStandardMaterial,
  PerspectiveCamera, PlaneGeometry, PMREMGenerator, PointLight, Points, PointsMaterial, Scene, Shape, ShapeGeometry,
  SRGBColorSpace, ACESFilmicToneMapping, WebGLRenderer, AmbientLight,
} from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';

const PINK = '#e81c5a';
const TAU = Math.PI * 2;
const easeOut = (t) => 1 - Math.pow(1 - t, 3);
const easeIn = (t) => t * t * t;

export function supported() {
  try {
    const c = document.createElement('canvas');
    return !!(c.getContext('webgl2') || c.getContext('webgl'));
  } catch {
    return false;
  }
}

function roundedRect(w, h, r) {
  const s = new Shape();
  const x = -w / 2;
  const y = -h / 2;
  s.moveTo(x + r, y);
  s.lineTo(x + w - r, y);
  s.absarc(x + w - r, y + r, r, -Math.PI / 2, 0, false);
  s.lineTo(x + w, y + h - r);
  s.absarc(x + w - r, y + h - r, r, 0, Math.PI / 2, false);
  s.lineTo(x + r, y + h);
  s.absarc(x + r, y + h - r, r, Math.PI / 2, Math.PI, false);
  s.lineTo(x, y + r);
  s.absarc(x + r, y + r, r, Math.PI, Math.PI * 1.5, false);
  return s;
}

/** Flat rounded face whose UVs span 0..1 so a square texture maps edge to edge. */
function faceGeometry(w, h, r) {
  const g = new ShapeGeometry(roundedRect(w, h, r), 32);
  const pos = g.attributes.position;
  const uv = g.attributes.uv;
  for (let i = 0; i < pos.count; i++) uv.setXY(i, (pos.getX(i) + w / 2) / w, (pos.getY(i) + h / 2) / h);
  uv.needsUpdate = true;
  return g;
}

function loadImage(src) {
  return new Promise((resolve) => {
    if (!src) return resolve(null);
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

function drawBrandMark(g, cx, cy, size, color) {
  const k = size / 64;
  g.save();
  g.translate(cx - 32 * k, cy - 32 * k);
  g.strokeStyle = color;
  g.fillStyle = color;
  g.lineWidth = 7 * k;
  g.lineCap = 'round';
  const start = (-48 * Math.PI) / 180;
  g.beginPath();
  g.arc(32 * k, 32 * k, 24 * k, start, start + (125 / (TAU * 24)) * TAU);
  g.stroke();
  g.lineJoin = 'round';
  g.lineWidth = 2 * k;
  g.beginPath();
  g.moveTo(27 * k, 22.5 * k);
  g.lineTo(43 * k, 32 * k);
  g.lineTo(27 * k, 41.5 * k);
  g.closePath();
  g.fill();
  g.stroke();
  g.restore();
}

/** Square texture for a tile face: dark glass with the app icon, or pink with a letter / the Oglu mark. */
async function faceTexture({ src, letter, brand }) {
  const c = document.createElement('canvas');
  c.width = c.height = 512;
  const g = c.getContext('2d');
  const img = brand ? null : await loadImage(src);
  const pinkFace = brand || !img;
  const bg = g.createLinearGradient(0, 0, 512, 512);
  if (pinkFace) {
    bg.addColorStop(0, '#ff4b83');
    bg.addColorStop(0.5, PINK);
    bg.addColorStop(1, '#9c0c44');
  } else {
    bg.addColorStop(0, '#2a2a31');
    bg.addColorStop(1, '#0b0b0e');
  }
  g.fillStyle = bg;
  g.fillRect(0, 0, 512, 512);
  const glow = g.createRadialGradient(256, 300, 10, 256, 300, 300);
  glow.addColorStop(0, pinkFace ? 'rgba(255,255,255,0.22)' : 'rgba(232,28,90,0.42)');
  glow.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = glow;
  g.fillRect(0, 0, 512, 512);
  // Glossy top highlight
  const gloss = g.createLinearGradient(0, 0, 0, 260);
  gloss.addColorStop(0, 'rgba(255,255,255,0.16)');
  gloss.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = gloss;
  g.fillRect(0, 0, 512, 260);

  if (img) {
    g.shadowColor = 'rgba(0,0,0,0.55)';
    g.shadowBlur = 36;
    g.shadowOffsetY = 18;
    g.imageSmoothingQuality = 'high';
    g.drawImage(img, 92, 88, 328, 328);
  } else if (brand) {
    g.shadowColor = 'rgba(80,0,30,0.45)';
    g.shadowBlur = 30;
    g.shadowOffsetY = 12;
    drawBrandMark(g, 256, 256, 330, '#ffffff');
  } else {
    await document.fonts?.load?.('italic 900 300px Montserrat').catch(() => {});
    g.shadowColor = 'rgba(80,0,30,0.45)';
    g.shadowBlur = 30;
    g.shadowOffsetY = 12;
    g.fillStyle = '#ffffff';
    g.font = 'italic 900 320px Montserrat, "Segoe UI", sans-serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(String(letter || '?').slice(0, 1).toUpperCase(), 266, 276);
  }
  const tex = new CanvasTexture(c);
  tex.colorSpace = SRGBColorSpace;
  tex.anisotropy = 4;
  return tex;
}

function radialTexture(inner, outer = 'rgba(0,0,0,0)') {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d');
  const r = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  r.addColorStop(0, inner);
  r.addColorStop(1, outer);
  g.fillStyle = r;
  g.fillRect(0, 0, 128, 128);
  const t = new CanvasTexture(c);
  t.colorSpace = SRGBColorSpace;
  return t;
}

function makeRenderer(canvas) {
  const renderer = new WebGLRenderer({ canvas, alpha: true, antialias: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.toneMapping = ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  renderer.setClearColor(0x000000, 0);
  return renderer;
}

function setupLights(scene, renderer) {
  const pmrem = new PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  scene.environmentIntensity = 0.55;
  pmrem.dispose();
  scene.add(new AmbientLight(0xffffff, 0.25));
  const key = new DirectionalLight(0xffffff, 2.2);
  key.position.set(2, 3, 5);
  scene.add(key);
  const pink = new PointLight(0xff2d6f, 38, 0, 2);
  pink.position.set(-2.6, -1.4, 2.2);
  scene.add(pink);
  const blue = new PointLight(0x5b6cff, 26, 0, 2);
  blue.position.set(2.8, 1.8, 1.6);
  scene.add(blue);
}

function buildTile() {
  const W = 1.7;
  const R = 0.42;
  const D = 0.2;
  const BT = 0.07;
  const body = new Mesh(
    new ExtrudeGeometry(roundedRect(W, W, R), { depth: D, bevelEnabled: true, bevelThickness: BT, bevelSize: 0.07, bevelSegments: 8, curveSegments: 40 }),
    new MeshPhysicalMaterial({ color: 0x16161b, metalness: 0.6, roughness: 0.26, clearcoat: 1, clearcoatRoughness: 0.1 }),
  );
  body.geometry.center();
  const faceGeo = faceGeometry(W - 0.08, W - 0.08, R - 0.04);
  const mat = () => new MeshStandardMaterial({ roughness: 0.34, metalness: 0.05, emissive: 0xffffff, emissiveIntensity: 0.5 });
  const front = new Mesh(faceGeo, mat());
  front.position.z = D / 2 + BT + 0.004;
  const back = new Mesh(faceGeo, mat());
  back.rotation.y = Math.PI;
  back.position.z = -(D / 2 + BT + 0.004);
  const group = new Group();
  group.add(body, front, back);
  const setFace = (mesh, tex) => {
    const m = mesh.material;
    if (m.map && m.map !== tex) m.map.dispose();
    m.map = tex;
    m.emissiveMap = tex;
    m.needsUpdate = true;
  };
  return { group, setFront: (t) => setFace(front, t), setBack: (t) => setFace(back, t) };
}

function makeSparks(count, spread) {
  const geo = new BufferGeometry();
  const pos = new Float32Array(count * 3);
  const col = new Float32Array(count * 3);
  const speed = new Float32Array(count);
  const palette = [new Color('#ff2d6f'), new Color('#ffffff'), new Color('#ff7aa8'), new Color('#7d6bff')];
  for (let i = 0; i < count; i++) {
    pos[i * 3] = (Math.random() - 0.5) * spread.x;
    pos[i * 3 + 1] = (Math.random() - 0.5) * spread.y;
    pos[i * 3 + 2] = (Math.random() - 0.5) * spread.z;
    const c = palette[i % palette.length];
    col.set([c.r, c.g, c.b], i * 3);
    speed[i] = 0.15 + Math.random() * 0.35;
  }
  geo.setAttribute('position', new BufferAttribute(pos, 3));
  geo.setAttribute('color', new BufferAttribute(col, 3));
  const points = new Points(geo, new PointsMaterial({
    size: 0.07, map: radialTexture('rgba(255,255,255,1)'), vertexColors: true, transparent: true,
    depthWrite: false, blending: AdditiveBlending, opacity: 0.9,
  }));
  const update = (dt, boost = 1) => {
    for (let i = 0; i < count; i++) {
      let y = pos[i * 3 + 1] + speed[i] * dt * boost;
      if (y > spread.y / 2) y = -spread.y / 2;
      pos[i * 3 + 1] = y;
    }
    geo.attributes.position.needsUpdate = true;
  };
  return { points, update };
}

function fitCanvas(renderer, camera, canvas, fit) {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  if (!w || !h) return false;
  if (canvas.width !== Math.round(w * renderer.getPixelRatio()) || canvas.height !== Math.round(h * renderer.getPixelRatio())) {
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    if (fit) camera.position.z = fit / Math.min(1, camera.aspect);
    camera.updateProjectionMatrix();
  }
  return true;
}

let brandTexture = null;
const brandFace = () => brandTexture || (brandTexture = faceTexture({ brand: true }));

/** Floating 3D tile for the selected app. */
export function createToken(canvas) {
  const renderer = makeRenderer(canvas);
  const scene = new Scene();
  const camera = new PerspectiveCamera(30, 1, 0.1, 50);
  const fitDist = 1.25 / Math.tan((15 * Math.PI) / 180);
  camera.position.set(0, 0, fitDist);
  setupLights(scene, renderer);

  const halo = new Mesh(new PlaneGeometry(4.6, 4.6), new MeshBasicMaterial({
    map: radialTexture('rgba(255,255,255,0.75)'), transparent: true, depthWrite: false, blending: AdditiveBlending, opacity: 0.35,
  }));
  halo.position.z = -1;
  scene.add(halo);
  const tile = buildTile();
  scene.add(tile.group);
  const sparks = makeSparks(70, { x: 3.6, y: 3.2, z: 1.6 });
  scene.add(sparks.points);
  brandFace().then((t) => tile.setBack(t));

  let running = false;
  let paused = false;
  let px = 0;
  let py = 0;
  let tpx = 0;
  let tpy = 0;
  let spin = null;
  let pending = null;
  let current = '';
  let t = 0;
  let last = performance.now();
  let raf = 0;

  const frame = (now) => {
    raf = requestAnimationFrame(frame);
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    if (paused || !fitCanvas(renderer, camera, canvas, fitDist)) return;
    t += dt;
    px += (tpx - px) * 0.06;
    py += (tpy - py) * 0.06;
    let spinAngle = 0;
    if (spin) {
      const p = Math.min(1, (now - spin.start) / 1000);
      spinAngle = TAU * easeOut(p);
      if (pending && spinAngle >= Math.PI / 2) {
        tile.setFront(pending);
        pending = null;
      }
      if (p >= 1) spin = null;
    }
    tile.group.rotation.y = spinAngle + Math.sin(t * 0.7) * 0.3 + px * 0.5;
    tile.group.rotation.x = Math.sin(t * 0.9) * 0.07 - py * 0.3;
    tile.group.position.y = Math.sin(t * 1.3) * 0.07;
    halo.material.opacity = running ? 0.42 + 0.18 * Math.sin(t * 5) : 0.32;
    sparks.update(dt, running ? 3 : 1);
    renderer.render(scene, camera);
  };
  raf = requestAnimationFrame(frame);

  return {
    async setIcon({ src, letter, brand }, animate = true) {
      const key = brand ? 'brand' : `${letter}|${src ? src.length + src.slice(-32) : ''}`;
      if (key === current) return;
      current = key;
      const tex = brand ? await brandFace() : await faceTexture({ src, letter });
      if (key !== current) return;
      if (animate && tile.group.parent) {
        pending = tex;
        spin = { start: performance.now() };
      } else {
        tile.setFront(tex);
      }
    },
    setPointer(x, y) { tpx = x; tpy = y; },
    setRunning(v) { running = !!v; },
    pause(v) { paused = !!v; if (!paused) last = performance.now(); },
    dispose() { cancelAnimationFrame(raf); renderer.dispose(); },
  };
}

/** Full-screen "jump to hyperspace" used while an app launches. */
export function createWarp(canvas) {
  const renderer = makeRenderer(canvas);
  const scene = new Scene();
  const camera = new PerspectiveCamera(62, 1, 0.1, 400);
  camera.position.set(0, 0, 6);
  setupLights(scene, renderer);

  const N = 900;
  const pos = new Float32Array(N * 6);
  const col = new Float32Array(N * 6);
  const ang = new Float32Array(N);
  const rad = new Float32Array(N);
  const zs = new Float32Array(N);
  const palette = [new Color('#ff2d6f'), new Color('#ffffff'), new Color('#ff7aa8'), new Color('#8f7bff'), new Color('#ffffff')];
  const reset = (i, far) => {
    ang[i] = Math.random() * TAU;
    rad[i] = 1.6 + Math.pow(Math.random(), 0.7) * 16;
    zs[i] = far ? -300 + Math.random() * 40 : -300 + Math.random() * 300;
  };
  for (let i = 0; i < N; i++) {
    reset(i, false);
    const c = palette[i % palette.length];
    col.set([c.r, c.g, c.b, 0, 0, 0], i * 6);
  }
  const geo = new BufferGeometry();
  geo.setAttribute('position', new BufferAttribute(pos, 3));
  geo.setAttribute('color', new BufferAttribute(col, 3));
  const lines = new LineSegments(geo, new LineBasicMaterial({ vertexColors: true, transparent: true, blending: AdditiveBlending, depthWrite: false }));
  scene.add(lines);

  const halo = new Mesh(new PlaneGeometry(7, 7), new MeshBasicMaterial({
    map: radialTexture('rgba(232,28,90,0.9)'), transparent: true, depthWrite: false, blending: AdditiveBlending, opacity: 0.6,
  }));
  scene.add(halo);
  const tile = buildTile();
  scene.add(tile.group);
  brandFace().then((t) => tile.setBack(t));

  let raf = 0;
  let start = 0;
  let last = 0;

  const speedAt = (s) => {
    if (s < 0.15) return 8;
    if (s < 1.05) return 8 + 232 * easeIn((s - 0.15) / 0.9);
    if (s < 2.3) return 240;
    return Math.max(16, 240 - (s - 2.3) * 420);
  };

  const frame = (now) => {
    raf = requestAnimationFrame(frame);
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    if (!fitCanvas(renderer, camera, canvas)) return;
    const s = (now - start) / 1000;
    const v = speedAt(s);
    const trail = Math.min(60, 0.4 + v * 0.07);
    for (let i = 0; i < N; i++) {
      zs[i] += v * dt;
      if (zs[i] > 8) reset(i, true);
      const x = Math.cos(ang[i]) * rad[i];
      const y = Math.sin(ang[i]) * rad[i] + 0.6;
      pos.set([x, y, zs[i], x, y, zs[i] - trail], i * 6);
    }
    geo.attributes.position.needsUpdate = true;

    const fly = easeOut(Math.min(1, s / 1.15));
    tile.group.position.set(0, 0.95 + Math.sin(s * 1.6) * 0.06, -80 + 80 * fly);
    tile.group.rotation.y = 5 * Math.PI * (1 - fly) + Math.sin(s * 0.9) * 0.25;
    tile.group.rotation.x = Math.sin(s * 1.1) * 0.08;
    tile.group.scale.setScalar(1.15);
    halo.position.set(0, 0.95, tile.group.position.z - 1.2);
    halo.material.opacity = 0.35 + 0.35 * Math.min(1, v / 240);
    camera.fov = 62 + 14 * Math.min(1, v / 240);
    camera.position.x = (Math.random() - 0.5) * 0.02 * (v / 240);
    camera.position.y = (Math.random() - 0.5) * 0.02 * (v / 240);
    camera.updateProjectionMatrix();
    renderer.render(scene, camera);
  };

  return {
    async play({ src, letter, brand }) {
      const tex = brand ? await brandFace() : await faceTexture({ src, letter });
      tile.setFront(tex);
      for (let i = 0; i < N; i++) reset(i, false);
      cancelAnimationFrame(raf);
      start = last = performance.now();
      raf = requestAnimationFrame(frame);
    },
    stop() {
      cancelAnimationFrame(raf);
      raf = 0;
    },
  };
}
