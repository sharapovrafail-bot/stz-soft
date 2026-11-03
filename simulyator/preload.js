const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('cncLink', {
  info: () => ipcRenderer.invoke('cnc:info'),
  saveCfg: (cfg) => ipcRenderer.invoke('cnc:saveCfg', cfg),
  pickStzDir: () => ipcRenderer.invoke('cnc:pickStzDir'),
  send: (o) => ipcRenderer.invoke('cnc:send', o),
  browse: (o) => ipcRenderer.invoke('cnc:browse', o),
  openStz: (o) => ipcRenderer.invoke('cnc:openStz', o),
  onProgress: (cb) => { ipcRenderer.removeAllListeners('cnc:progress'); ipcRenderer.on('cnc:progress', (e, p) => cb(p)); }
});
