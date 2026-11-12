'use strict';
// Builds a portable Windows x64 package and zips it: dist/Oglu-<version>-win-x64.zip
const path = require('path');
const fs = require('fs');
const { execFileSync } = require('child_process');
const { packager } = require('@electron/packager');

const root = path.join(__dirname, '..');
const pkg = require('../package.json');
const electronVersion = require('electron/package.json').version;
const outDir = path.join(root, 'dist', 'build');
const zipPath = path.join(root, 'dist', `Oglu-${pkg.version}-win-x64.zip`);

const README = `OGLU ${pkg.version} — app launcher & playtime tracker
====================================================

HOW TO START
  1. Extract this zip anywhere (e.g. your Desktop or C:\\Games).
  2. Open the "Oglu" folder and double-click Oglu.exe.
  3. Click "Select application", pick a game's .exe, and press PLAY.

Nothing else needs to be installed. Keep all files in the Oglu folder together.

If Windows shows "Windows protected your PC", click "More info" -> "Run anyway"
(Oglu is not code-signed).

HOW TIME IS COUNTED
  Time counts while the app you launched from Oglu is running. If the .exe is only
  a launcher that starts the real game under another name, enter that name in the
  app's settings under "Also track process" (e.g. GTA5.exe).
  Closing Oglu's window while a game runs keeps it counting in the system tray.

YOUR DATA
  %APPDATA%\\Oglu\\library.json  (playtime, sessions, settings)
`;

async function patchExe(exe) {
  const ResEdit = await import('resedit');
  const image = ResEdit.NtExecutable.from(fs.readFileSync(exe), { ignoreCert: true });
  const res = ResEdit.NtExecutableResource.from(image);

  const ico = ResEdit.Data.IconFile.from(fs.readFileSync(path.join(root, 'assets', 'icon.ico')));
  const groups = ResEdit.Resource.IconGroupEntry.fromEntries(res.entries);
  const groupId = groups[0]?.id ?? 1;
  const lang = groups[0]?.lang ?? 1033;
  ResEdit.Resource.IconGroupEntry.replaceIconsForResource(res.entries, groupId, lang, ico.icons.map((i) => i.data));

  const [major, minor, patch] = pkg.version.split('.').map(Number);
  const vi = ResEdit.Resource.VersionInfo.fromEntries(res.entries)[0] || ResEdit.Resource.VersionInfo.createEmpty();
  const langs = vi.getAllLanguagesForStringValues();
  for (const l of langs.length ? langs : [{ lang: 1033, codepage: 1200 }]) {
    vi.setStringValues(l, {
      ProductName: 'Oglu',
      FileDescription: 'Oglu',
      CompanyName: 'Oglu',
      InternalName: 'Oglu',
      OriginalFilename: 'Oglu.exe',
      LegalCopyright: `© ${new Date().getFullYear()} Oglu`,
      FileVersion: pkg.version,
      ProductVersion: pkg.version,
    });
  }
  vi.setFileVersion(major, minor, patch, 0);
  vi.setProductVersion(major, minor, patch, 0);
  vi.outputToResourceEntries(res.entries);
  res.outputResource(image);
  fs.writeFileSync(exe, Buffer.from(image.generate()));
}

(async () => {
  fs.rmSync(outDir, { recursive: true, force: true });
  fs.rmSync(zipPath, { force: true });

  const [appDir] = await packager({
    dir: root,
    out: outDir,
    platform: 'win32',
    arch: 'x64',
    name: 'Oglu',
    executableName: 'Oglu',
    appVersion: pkg.version,
    electronVersion,
    asar: true,
    // Oglu has no runtime dependencies; everything in node_modules is build tooling.
    prune: false,
    overwrite: true,
    ignore: [/^\/node_modules($|\/)/, /^\/dist($|\/)/, /^\/scripts($|\/)/, /^\/test($|\/)/, /^\/assets\/icon\.svg$/, /^\/README\.md$/, /^\/\.gitignore$/, /^\/package-lock\.json$/],
  });

  // Chromium falls back to en-US for missing locales; dropping the rest saves ~40 MB.
  const locales = path.join(appDir, 'locales');
  for (const f of fs.readdirSync(locales)) if (!['en-US.pak', 'ru.pak'].includes(f)) fs.rmSync(path.join(locales, f));

  const finalDir = path.join(outDir, 'Oglu');
  fs.renameSync(appDir, finalDir);
  await patchExe(path.join(finalDir, 'Oglu.exe'));
  fs.writeFileSync(path.join(finalDir, 'README.txt'), README.replace(/\n/g, '\r\n'));

  execFileSync('zip', ['-9', '-r', '-q', '-X', zipPath, 'Oglu'], { cwd: outDir });
  console.log(`Built ${path.relative(root, zipPath)} (${(fs.statSync(zipPath).size / 1048576).toFixed(1)} MB)`);
})().catch((err) => {
  console.error(err);
  process.exit(1);
});
