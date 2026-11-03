const { app, BrowserWindow, Menu, shell, ipcMain, dialog } = require('electron');
const { spawn } = require('child_process');
const path = require('path'), fs = require('fs');
if (!app.requestSingleInstanceLock()) { app.quit(); }
let win, ready = false, pending = null;
const EXT = /\.(nc|txt|tap|cnc|min|mpf|ngc|ptp|eia|gcode)$/i;
function fileFromArgs(argv) { return argv.slice(1).find(a => !a.startsWith('-') && EXT.test(a) && fs.existsSync(a)) || null; }
function openFile(p) {
  if (!p) return;
  if (!ready) { pending = p; return; }
  try {
    const b64 = fs.readFileSync(p).toString('base64');
    win.webContents.executeJavaScript('window.__openNC && window.__openNC(' + JSON.stringify(b64) + ',' + JSON.stringify(path.basename(p)) + ')');
  } catch (e) { }
}
function create() {
  win = new BrowserWindow({
    width: 1400, height: 900, minWidth: 900, minHeight: 600,
    title: 'Симулятор УП Fanuc — версия 9.0',
    icon: path.join(__dirname, 'app', 'icon.png'),
    backgroundColor: '#e9edf0', show: false, autoHideMenuBar: true,
    webPreferences: { contextIsolation: true, nodeIntegration: false, spellcheck: false, preload: path.join(__dirname, 'preload.js') }
  });
  Menu.setApplicationMenu(null);
  win.webContents.setWindowOpenHandler(({ url }) => { if (/^https?:/.test(url)) shell.openExternal(url); return { action: 'deny' }; });
  win.webContents.on('will-navigate', (e, url) => { if (!url.startsWith('file:')) { e.preventDefault(); if (/^https?:/.test(url)) shell.openExternal(url); } });
  win.on('page-title-updated', e => e.preventDefault());
  win.webContents.on('did-finish-load', () => { ready = true; setTimeout(() => { if (pending) { const p = pending; pending = null; openFile(p); } }, 300); });
  win.loadFile(path.join(__dirname, 'app', 'index.html'));
  win.once('ready-to-show', () => { win.maximize(); win.show(); });
}
pending = fileFromArgs(process.argv);
app.on('second-instance', (e, argv) => { if (win) { if (win.isMinimized()) win.restore(); win.focus(); openFile(fileFromArgs(argv)); } });

/* ---------- связь со станком (STZ & FANUC LAN) ---------- */
const CFG = () => path.join(app.getPath('userData'), 'cnc-link.json');
function readJson(p, def) { try { return JSON.parse(fs.readFileSync(p, 'utf8')); } catch (e) { return def; } }
function cfg() { return Object.assign({ stzDir: 'C:\\PTT\\dist', machineIndex: null, folders: {} }, readJson(CFG(), {})); }
function bundledDll() {
  if (process.env.SIM_FOCAS_DLL) return process.env.SIM_FOCAS_DLL;
  // библиотеки FANUC FOCAS в программу не входят (это файлы FANUC): ищем их рядом с программой и в папке STZ FANUC LAN
  const c = cfg();
  const cands = [path.join(process.resourcesPath || '', 'focas', 'Fwlib64.dll'), path.join(__dirname, 'focas', 'Fwlib64.dll'),
    path.join(c.stzDir || '', 'Fwlib64.dll'), path.join(path.dirname(c.stzDir || ''), 'Fwlib64.dll'), 'C:\\PTT\\dist\\Fwlib64.dll', 'C:\\PTT\\Fwlib64.dll'];
  return cands.find(p => fs.existsSync(p)) || cands[0];
}
function stzInfo(c) {
  const sp = path.join(c.stzDir, 'ptt_settings.json');
  const st = readJson(sp, null);
  let exe = null;
  try { exe = fs.readdirSync(c.stzDir).filter(f => /^STZ.*\.exe$/i.test(f)).sort().reverse().map(f => path.join(c.stzDir, f))[0] || null; } catch (e) { }
  return { settingsPath: sp, settings: st, exe };
}
ipcMain.handle('cnc:info', () => {
  const c = cfg(), s = stzInfo(c);
  const machines = (s.settings && Array.isArray(s.settings.machines) ? s.settings.machines : []).map(m => ({ name: m.name, ip: m.ip, port: m.port || 8193 }));
  let mi = c.machineIndex;
  if (mi === null || mi === undefined) mi = s.settings && Number.isInteger(s.settings.machine_index) ? s.settings.machine_index : 0;
  return { stzDir: c.stzDir, stzExe: s.exe, stzSettings: !!s.settings, machines, machineIndex: mi, folders: c.folders, dll: bundledDll(), dllOk: fs.existsSync(bundledDll()) };
});
ipcMain.handle('cnc:saveCfg', (e, part) => { const c = Object.assign(cfg(), part || {}); try { fs.mkdirSync(path.dirname(CFG()), { recursive: true }); fs.writeFileSync(CFG(), JSON.stringify(c, null, 2)); } catch (er) { } return true; });
ipcMain.handle('cnc:pickStzDir', async () => {
  const r = await dialog.showOpenDialog(win, { title: 'Папка с STZ_FANUC_LAN (где лежит ptt_settings.json)', properties: ['openDirectory'], defaultPath: cfg().stzDir });
  if (r.canceled || !r.filePaths[0]) return null;
  const c = Object.assign(cfg(), { stzDir: r.filePaths[0] }); fs.writeFileSync(CFG(), JSON.stringify(c, null, 2)); return r.filePaths[0];
});
let sending = false;
ipcMain.handle('cnc:send', async (e, o) => {
  if (sending) return { ok: false, error: 'Уже идёт передача' };
  sending = true;
  try {
    const focas = require('./focas.js');
    const res = await focas.sendAsync(Object.assign({}, o, { dll: bundledDll() }), p => { try { win.webContents.send('cnc:progress', p); } catch (er) { } });
    return Object.assign({ ok: true }, res);
  } catch (er) {
    return { ok: false, exists: !!er.exists, cncName: er.cncName, code: er.code, error: er.exists ? 'exists' : String(er.message || er) };
  } finally { sending = false; }
});
let browsing = false;
ipcMain.handle('cnc:browse', async (e, o) => {
  if (sending || browsing) return { ok: false, error: 'Стойка занята другой операцией, подождите' };
  browsing = true;
  try { const focas = require('./focas.js'); return Object.assign({ ok: true }, await focas.browseAsync(Object.assign({}, o, { dll: bundledDll() }))); }
  catch (er) { return { ok: false, error: String(er.message || er) }; }
  finally { browsing = false; }
});
ipcMain.handle('cnc:openStz', async (e, o) => {
  const c = cfg(), s = stzInfo(c);
  if (!s.exe) return { ok: false, error: 'В папке ' + c.stzDir + ' не найден STZ_FANUC_LAN*.exe' };
  const dir = path.join(app.getPath('documents'), 'Симулятор УП Fanuc', 'На станок');
  fs.mkdirSync(dir, { recursive: true });
  const base = String(o.name || 'PROGRAM').replace(/[\\/:*?"<>|]/g, '_');
  const file = path.join(dir, base + '.nc');
  const txt = String(o.text || '').replace(/\r/g, '').replace(/[^\x00-\x7E]/g, '?').replace(/\n/g, '\r\n');
  fs.writeFileSync(file, txt, 'latin1');
  // STZ & FANUC LAN открывается в папке pc_last_dir — подставляем папку с этой УП
  if (s.settings) { try { s.settings.pc_last_dir = dir; fs.writeFileSync(s.settingsPath, JSON.stringify(s.settings, null, 2)); } catch (er) { } }
  try { const ch = spawn(s.exe, [], { cwd: path.dirname(s.exe), detached: true, stdio: 'ignore' }); ch.unref(); }
  catch (er) { return { ok: false, error: String(er.message || er) }; }
  return { ok: true, file };
});
app.whenReady().then(create);
app.on('window-all-closed', () => app.quit());
