'use strict';
// Renders assets/icon.svg into icon.png (256px) and a multi-size icon.ico.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const dir = path.join(__dirname, '..', 'assets');
const svg = fs.readFileSync(path.join(dir, 'icon.svg'));
const sizes = [16, 24, 32, 48, 64, 128, 256];

(async () => {
  const pngs = await Promise.all(sizes.map((s) => sharp(svg, { density: 384 }).resize(s, s).png().toBuffer()));
  fs.writeFileSync(path.join(dir, 'icon.png'), pngs[pngs.length - 1]);
  // ICO container with PNG-compressed entries (supported since Windows Vista).
  const header = Buffer.alloc(6 + 16 * sizes.length);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  sizes.forEach((s, i) => {
    const e = 6 + i * 16;
    header.writeUInt8(s >= 256 ? 0 : s, e);
    header.writeUInt8(s >= 256 ? 0 : s, e + 1);
    header.writeUInt16LE(1, e + 4);
    header.writeUInt16LE(32, e + 6);
    header.writeUInt32LE(pngs[i].length, e + 8);
    header.writeUInt32LE(offset, e + 12);
    offset += pngs[i].length;
  });
  fs.writeFileSync(path.join(dir, 'icon.ico'), Buffer.concat([header, ...pngs]));
  console.log('icons written');
})();
