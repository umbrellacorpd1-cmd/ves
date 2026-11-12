'use strict';
// Bundles the three.js scenes into a single classic script the sandboxed renderer can load.
const path = require('path');
const esbuild = require('esbuild');

const root = path.join(__dirname, '..');

esbuild.buildSync({
  entryPoints: [path.join(root, 'src', 'renderer', 'scene', 'index.js')],
  outfile: path.join(root, 'src', 'renderer', 'vendor', 'scene.js'),
  bundle: true,
  minify: true,
  format: 'iife',
  globalName: 'OgluScene',
  target: 'chrome130',
  legalComments: 'eof',
  logLevel: 'warning',
});
console.log('scene bundle written');
