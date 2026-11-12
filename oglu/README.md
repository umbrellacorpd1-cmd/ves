# Oglu

A Windows launcher that starts your games and apps and tracks how many hours you spend in each one.

- Add any `.exe` (or a desktop shortcut) with the button or by drag & drop. Oglu pulls the full-resolution icon out of the exe and themes the launcher in its colours.
- **Play** shows a launch screen with your total hours, then counts the session live.
- **Close app** ends a running app straight from the launcher (also from the titlebar chip and the tray). It asks politely first; if the app doesn't respond within a few seconds the button turns into **Force close**.
- Per-app stats: total hours, today, this week, sessions, average and longest session, 14-day activity chart, plus a Library view sorted by most played, recent or name.
- Optional background image, launch arguments and "also track process" for launchers that start the real game under another name.
- Keeps counting from the system tray if you close the window while a game runs.

Data lives in `%APPDATA%\Oglu\library.json`.

## Download

Grab `Oglu-<version>-win-x64.zip` from the GitHub release, extract it, and run `Oglu\Oglu.exe`. Nothing else needs to be installed.

## Develop

```bash
npm install
npm start            # run the app
npm test             # unit tests (tracking, storage, parsing)
npm run e2e          # end-to-end run of the real app (Linux: needs xvfb-run)
npm run build:win    # build dist/Oglu-<version>-win-x64.zip (works from Linux, no Wine needed)
```

Built with Electron. Fonts (Unbounded, Inter, JetBrains Mono) are bundled, so the app works fully offline.
