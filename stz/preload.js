const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('stz', {
  isApp: true,
  info: () => ipcRenderer.invoke('stz:info'),
  check: () => ipcRenderer.invoke('stz:check'),
  installFromUrl: (url, version) => ipcRenderer.invoke('stz:installUrl', url, version),
  install: (html, version) => ipcRenderer.invoke('stz:install', html, version),
  reset: () => ipcRenderer.invoke('stz:reset'),
  mcp: () => ipcRenderer.invoke('stz:mcp'),
  hobOpen: () => ipcRenderer.invoke('stz:hobOpen'),
  z525Open: () => ipcRenderer.invoke('stz:z525Open'),
  z525Send: (name, text) => ipcRenderer.invoke('stz:z525Send', name, text),
  hobSend: (name, text) => ipcRenderer.invoke('stz:hobSend', name, text),
  wirecutSend: (name, text) => ipcRenderer.invoke('stz:wirecutSend', name, text),
  licStatus: () => ipcRenderer.invoke('stz:lic'),
  licActivate: key => ipcRenderer.invoke('stz:licActivate', key),
  mcpInstall: () => ipcRenderer.invoke('stz:mcpInstall'),
  savePDF: (name, html) => ipcRenderer.invoke('stz:savePDF', name, html)
});
