'use strict';

const { contextBridge, ipcRenderer, webUtils } = require('electron');

const EVENTS = new Set(['state', 'tick', 'session-end', 'win-state']);

contextBridge.exposeInMainWorld('oglu', {
  getState: () => ipcRenderer.invoke('state'),
  addApp: (file) => ipcRenderer.invoke('add-app', file || null),
  pathForFile: (file) => webUtils.getPathForFile(file),
  changePath: (id) => ipcRenderer.invoke('change-path', id),
  updateApp: (id, patch) => ipcRenderer.invoke('update-app', id, patch),
  removeApp: (id) => ipcRenderer.invoke('remove-app', id),
  resetStats: (id) => ipcRenderer.invoke('reset-stats', id),
  setCover: (id) => ipcRenderer.invoke('set-cover', id),
  clearCover: (id) => ipcRenderer.invoke('clear-cover', id),
  reveal: (id) => ipcRenderer.invoke('reveal', id),
  openDataDir: () => ipcRenderer.invoke('open-data-dir'),
  select: (id) => ipcRenderer.invoke('select', id),
  launch: (id) => ipcRenderer.invoke('launch', id),
  closeApp: (id, force) => ipcRenderer.invoke('close-app', id, !!force),
  updateSettings: (patch) => ipcRenderer.invoke('settings', patch),
  windowAction: (action) => ipcRenderer.invoke('win', action),
  on: (channel, fn) => {
    if (!EVENTS.has(channel)) throw new Error(`Unknown channel ${channel}`);
    const listener = (_e, data) => fn(data);
    ipcRenderer.on(channel, listener);
    return () => ipcRenderer.removeListener(channel, listener);
  },
});
