const { app, BrowserWindow, Menu, shell, ipcMain, net } = require('electron');
const lic = require('./lic.js');
const path = require('path');
const fs = require('fs');
const http = require('http');
const { spawn } = require('child_process');

// Локальный API для Claude (MCP): только 127.0.0.1, порт можно сменить переменной STZ_PORT
const API_PORT = +process.env.STZ_PORT || 47321;
let mainWin = null, apiUp = false;
const isMac = process.platform === 'darwin';
const outDir = () => path.join(app.getPath('documents'), 'STZ');
const mcpScript = () => app.isPackaged ? path.join(process.resourcesPath, 'mcp', 'stz-mcp.js') : path.join(__dirname, 'mcp', 'stz-mcp.js');

function startApi() {
  http.createServer((req, res) => {
    if (req.method !== 'POST' || req.url !== '/api') { res.writeHead(404); return res.end(); }
    let b = ''; req.setEncoding('utf8'); req.on('data', d => b += d);
    req.on('end', async () => {
      let out;
      try {
        const { method, args } = JSON.parse(b || '{}');
        if (!mainWin || mainWin.isDestroyed()) throw new Error('окно программы закрыто');
        const r = await mainWin.webContents.executeJavaScript(`window.STZ_API?window.STZ_API.call(${JSON.stringify(String(method))},${JSON.stringify(args || {})}):{error:'программа ещё загружается'}`, true);
        if (r && r.error) throw new Error(r.error);
        if (r && Array.isArray(r.files)) {
          fs.mkdirSync(outDir(), { recursive: true });
          r.files = r.files.map(f => { const p = path.join(outDir(), String(f.name).replace(/[\\/:*?"<>|]+/g, '_')); fs.writeFileSync(p, f.text, 'utf8'); return { name: f.name, path: p, bytes: Buffer.byteLength(f.text) }; });
        }
        out = { ok: true, result: r };
      } catch (e) { out = { ok: false, error: e.message || String(e) }; }
      res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify(out));
    });
  }).on('error', e => { apiUp = false; console.error('STZ API:', e.message); }).listen(API_PORT, '127.0.0.1', () => { apiUp = true; });
}

ipcMain.handle('stz:mcp', () => ({ exe: process.execPath, script: mcpScript(), port: API_PORT, up: apiUp, out: outDir() }));

// Автоподключение к Claude Desktop: дописывает сервер "stz" в claude_desktop_config.json
function claudeCfgPaths() {
  const ad = app.getPath('appData'), list = [path.join(ad, 'Claude', 'claude_desktop_config.json')];
  try {
    const pk = path.join(process.env.LOCALAPPDATA || path.join(ad, '..', 'Local'), 'Packages');
    if (fs.existsSync(pk)) fs.readdirSync(pk).filter(n => /^Claude_/i.test(n)).forEach(n => list.push(path.join(pk, n, 'LocalCache', 'Roaming', 'Claude', 'claude_desktop_config.json')));
  } catch (e) {}
  return list;
}
ipcMain.handle('stz:mcpInstall', () => {
  if (!app.isPackaged) return { error: 'Автоподключение работает только в установленной программе (.exe).' };
  if (!fs.existsSync(mcpScript())) return { error: 'Не найден файл ' + mcpScript() + '. Переустановите программу.' };
  const done = [];
  for (const p of claudeCfgPaths()) {
    try {
      let cfg = {};
      if (fs.existsSync(p)) { const t = fs.readFileSync(p, 'utf8').trim(); if (t) { cfg = JSON.parse(t); fs.writeFileSync(p + '.bak', t, 'utf8'); } }
      else if (done.length) continue;
      cfg.mcpServers = cfg.mcpServers || {};
      cfg.mcpServers.stz = { command: process.execPath, args: [mcpScript()], env: { ELECTRON_RUN_AS_NODE: '1' } };
      fs.mkdirSync(path.dirname(p), { recursive: true });
      fs.writeFileSync(p, JSON.stringify(cfg, null, 2), 'utf8');
      done.push(p);
    } catch (e) { return { error: 'Не удалось записать ' + p + ': ' + e.message }; }
  }
  return { ok: true, paths: done };
});


// Программа наладки зубофрезерного станка 5К32А (входит в установщик, лежит в resources/hob)
let hobProc = null;
const hobName = isMac ? 'Zubofreza_5K32A' : 'Zubofreza_5K32A.exe';
const hobExe = () => app.isPackaged ? path.join(process.resourcesPath, 'hob', hobName) : path.join(__dirname, 'hob', hobName);

// macOS: программы станков (Go) работают как локальный сервер, окно для них открывает STZ
const macWins = {}, macProcs = new Set();
function macOpen(key, exe, title, w, h) {
  const cur = macWins[key];
  if (cur) { if (cur.win && !cur.win.isDestroyed()) { cur.win.show(); cur.win.focus(); } return; }
  const st = macWins[key] = { win: null };
  const p = spawn(exe, [], { env: { ...process.env, STZ_EMBED: '1' }, stdio: ['ignore', 'pipe', 'ignore'] });
  macProcs.add(p);
  let buf = '';
  const done = () => { macProcs.delete(p); if (macWins[key] === st) delete macWins[key]; if (st.win && !st.win.isDestroyed()) st.win.destroy(); };
  p.on('error', done); p.on('exit', done);
  p.stdout.on('data', d => {
    if (st.win) return;
    const m = /http:\/\/127\.0\.0\.1:\d+\//.exec(buf += d);
    if (!m) return;
    const win = st.win = new BrowserWindow({ width: w, height: h, minWidth: 900, minHeight: 600, title, backgroundColor: '#1C1D1F',
      webPreferences: { contextIsolation: true, sandbox: true } });
    win.on('page-title-updated', e => e.preventDefault());
    win.webContents.setWindowOpenHandler(({ url }) => /^(blob:|data:|about:blank|http:\/\/127\.0\.0\.1:)/.test(url) ? { action: 'allow' } : (shell.openExternal(url), { action: 'deny' }));
    win.on('closed', () => { try { p.kill(); } catch (e) {} });
    win.loadURL(m[0]);
  });
}
const macHob = () => macOpen('hob', hobExe(), 'Зубофреза 5К32А — наладка зубофрезерного станка · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru', 1366, 860);
const macZ525 = () => macOpen('z525', z525Exe(), 'Зуборез 525 — наладка станка для конических колёс · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru', 1366, 860);
app.on('will-quit', () => { for (const p of macProcs) { try { p.kill(); } catch (e) {} } });
const hobJobsDir = () => path.join(app.getPath('documents'), 'Зубофреза 5К32А', 'Детали');
function hobLaunch() {
  if (!lic.ok()) return { error: 'Пробный период закончился — введите ключ в STZ' };
  if (!fs.existsSync(hobExe())) return { error: 'программа «Зубофреза 5К32А» не установлена (выберите её при установке STZ)' };
  if (isMac) { macHob(); return { ok: true }; }
  hobProc = spawn(hobExe(), [], { detached: true, stdio: 'ignore' });
  hobProc.on('error', () => { hobProc = null; });
  hobProc.unref();
  return { ok: true };
}
// Программа наладки зуборезного станка 525 (resources/z525)
const z525Name = isMac ? 'Zuborez_525' : 'Zuborez_525.exe';
const z525Exe = () => app.isPackaged ? path.join(process.resourcesPath, 'z525', z525Name) : path.join(__dirname, 'z525', z525Name);
ipcMain.handle('stz:z525Open', () => {
  try {
    if (!lic.ok()) return { error: 'Пробный период закончился — введите ключ в STZ' };
    if (!fs.existsSync(z525Exe())) return { error: 'программа «Зуборез 525» не установлена (выберите её при установке STZ)' };
    if (isMac) macZ525();
    else { const p = spawn(z525Exe(), [], { detached: true, stdio: 'ignore' }); p.on('error', () => {}); p.unref(); }
    return { ok: true };
  } catch (e) { return { error: e.message }; }
});
const z525JobsDir = () => path.join(app.getPath('documents'), 'Зуборез 525', 'Детали');
ipcMain.handle('stz:z525Send', (e, name, text) => {
  try {
    JSON.parse(String(text));
    if (!lic.ok()) return { error: 'Пробный период закончился — введите ключ в STZ' };
    if (!fs.existsSync(z525Exe())) return { error: 'программа «Зуборез 525» не установлена (выберите её при установке STZ)' };
    const fn = String(name || 'пара.json').replace(/[\\/:*?"<>|]+/g, '_').slice(0, 80).replace(/(\.json)?$/i, '.json');
    fs.mkdirSync(z525JobsDir(), { recursive: true });
    fs.writeFileSync(path.join(z525JobsDir(), fn), String(text), 'utf8');
    if (isMac) macZ525();
    else { const p = spawn(z525Exe(), [], { detached: true, stdio: 'ignore' }); p.on('error', () => {}); p.unref(); }
    return { ok: true, file: path.join(z525JobsDir(), fn) };
  } catch (err) { return { error: err.message }; }
});
// Программа STZ WireCut (проволочно-вырезной станок) — отдельная бесплатная программа; ищем по реестру и в Program Files
const wirecutJobsDir = () => path.join(app.getPath('documents'), 'STZ WireCut', 'Детали');
function wirecutExe() {
  const cands = [];
  try {
    const { execFileSync } = require('child_process');
    for (const view of ['/reg:64', '/reg:32']) {
      try {
        const out = execFileSync('reg', ['query', 'HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\STZWireCut', '/v', 'DisplayIcon', view], { encoding: 'utf8', windowsHide: true });
        const m = out.match(/DisplayIcon\s+REG_\w+\s+(.+)/);
        if (m) cands.push(m[1].trim().replace(/^"|"$/g, ''));
      } catch (e) {}
    }
  } catch (e) {}
  for (const pf of [process.env.ProgramW6432, process.env.ProgramFiles, process.env['ProgramFiles(x86)']]) if (pf) cands.push(path.join(pf, 'STZ WireCut', 'STZ_WireCut.exe'));
  return cands.find(p => { try { return fs.existsSync(p); } catch (e) { return false; } }) || null;
}
ipcMain.handle('stz:wirecutSend', (e, name, text) => {
  try {
    JSON.parse(String(text));
    const exe = isMac ? null : wirecutExe();
    if (!exe) return { error: 'программа «STZ WireCut» не найдена на этом компьютере', noExe: true };
    const fn = String(name || 'колесо.stzgear').replace(/[\\/:*?"<>|]+/g, '_').slice(0, 90).replace(/(\.stzgear)?$/i, '.stzgear');
    fs.mkdirSync(wirecutJobsDir(), { recursive: true });
    const file = path.join(wirecutJobsDir(), fn);
    fs.writeFileSync(file, String(text), 'utf8');
    const p = spawn(exe, [file], { detached: true, stdio: 'ignore' }); p.on('error', () => {}); p.unref();
    return { ok: true, file };
  } catch (err) { return { error: err.message }; }
});
ipcMain.handle('stz:hobOpen', () => { try { return hobLaunch(); } catch (e) { return { error: e.message }; } });
ipcMain.handle('stz:hobSend', (e, name, text) => {
  try {
    JSON.parse(String(text));
    const fn = String(name || 'деталь.json').replace(/[\\/:*?"<>|]+/g, '_').slice(0, 80).replace(/(\.json)?$/i, '.json');
    fs.mkdirSync(hobJobsDir(), { recursive: true });
    fs.writeFileSync(path.join(hobJobsDir(), fn), String(text), 'utf8');
    const r = hobLaunch();
    return r.error ? { error: r.error } : { ok: true, file: path.join(hobJobsDir(), fn), already: !!r.already };
  } catch (err) { return { error: err.message }; }
});

// Адрес файла version.json с описанием последней версии (см. ИНСТРУКЦИЯ.md, раздел «Обновления»)
const UPDATE_URL = 'https://sharapov-cnc.ru/stz/version.json';

const updDir = () => path.join(app.getPath('userData'), 'update');
const updHtml = () => path.join(updDir(), 'index.html');
const updMeta = () => path.join(updDir(), 'meta.json');

function cmpVer(a, b) {
  const P = v => { const s = String(v).split('-'); return { n: s[0].split('.').map(x => +x || 0), pre: s[1] ? +((s[1].match(/\d+/) || [0])[0]) : Infinity }; };
  const A = P(a), B = P(b);
  for (let i = 0; i < 3; i++) { const d = (A.n[i] || 0) - (B.n[i] || 0); if (d) return d; }
  return A.pre === B.pre ? 0 : A.pre > B.pre ? 1 : -1;
}

function readUpd() {
  try {
    if (!fs.existsSync(updHtml())) return null;
    const m = JSON.parse(fs.readFileSync(updMeta(), 'utf8'));
    // после установки нового .exe более старое обновление не используется
    if (m.base !== app.getVersion() && (!m.version || cmpVer(m.version, app.getVersion()) <= 0)) {
      fs.rmSync(updDir(), { recursive: true, force: true });
      return null;
    }
    return m;
  } catch (e) { return null; }
}

const entry = () => readUpd() ? updHtml() : path.join(__dirname, 'app', 'index.html');

function saveUpd(html, version) {
  if (typeof html !== 'string' || html.length < 10000 || !/<html/i.test(html)) throw new Error('файл не похож на программу STZ');
  fs.mkdirSync(updDir(), { recursive: true });
  fs.writeFileSync(updHtml() + '.tmp', html, 'utf8');
  fs.renameSync(updHtml() + '.tmp', updHtml());
  fs.writeFileSync(updMeta(), JSON.stringify({ version: version || '', base: app.getVersion(), date: new Date().toISOString() }));
}

const reloadFor = e => { const w = BrowserWindow.fromWebContents(e.sender); if (w) w.loadFile(entry()); };

ipcMain.handle('stz:info', () => { const m = readUpd(); return { appVersion: app.getVersion(), updated: !!m, updVersion: m ? m.version : '', updateUrl: UPDATE_URL }; });

ipcMain.handle('stz:check', async () => {
  try {
    const r = await net.fetch(UPDATE_URL + '?t=' + Date.now(), { cache: 'no-store' });
    if (!r.ok) return { error: 'HTTP ' + r.status };
    const j = await r.json();
    return { version: String(j.version || ''), url: new URL(j.url || 'index.html', UPDATE_URL).href, notes: j.notes || '' };
  } catch (e) { return { error: e.message }; }
});

ipcMain.handle('stz:installUrl', async (e, url, version) => {
  try {
    const r = await net.fetch(url, { cache: 'no-store' });
    if (!r.ok) return { error: 'HTTP ' + r.status };
    saveUpd(await r.text(), version);
    reloadFor(e);
    return { ok: true };
  } catch (err) { return { error: err.message }; }
});

ipcMain.handle('stz:install', (e, html, version) => {
  try { saveUpd(html, version); reloadFor(e); return { ok: true }; } catch (err) { return { error: err.message }; }
});

ipcMain.handle('stz:reset', e => { try { fs.rmSync(updDir(), { recursive: true, force: true }); } catch (err) {} reloadFor(e); return { ok: true }; });

let lockWin = null, apiStarted = false;
const licOk = st => st.state === 'trial' || st.state === 'licensed';
const LOCK_HTML = `<!doctype html><meta charset="utf-8"><title>STZ — лицензия</title><body style="margin:0;background:#1C1D1F;color:#eee;font-family:system-ui;display:flex;align-items:center;justify-content:center;height:100vh"><div style="max-width:480px;padding:24px"><h2 id="t" style="margin-top:0">Пробный период закончился</h2><p id="m"></p><p>Код этого компьютера:<br><b id="id" style="font-size:24px;letter-spacing:1px;user-select:all"></b></p><p>Сообщите этот код разработчику, он пришлёт ключ. Ключ работает только на этом компьютере и открывает все программы (STZ, Зубофреза 5К32А, Зуборез 525).</p><input id="k" placeholder="XXXX-XXXX-XXXX-XXXX" style="width:100%;box-sizing:border-box;font-size:18px;padding:8px;margin:6px 0;text-transform:uppercase"><div id="e" style="color:#ff7b6b;min-height:22px"></div><button id="go" style="font-size:16px;padding:8px 18px">Активировать</button></div><script>
window.stz.licStatus().then(s=>{document.getElementById('id').textContent=s.id;document.getElementById('m').textContent=s.msg||'';if(s.state!=='expired')document.getElementById('t').textContent='Программа заблокирована'});
document.getElementById('go').onclick=async()=>{const r=await window.stz.licActivate(document.getElementById('k').value);if(!r.ok)document.getElementById('e').textContent='Ключ не подходит. Проверьте код и ключ.'};
</script>`;
function showLock() {
  if (lockWin && !lockWin.isDestroyed()) { lockWin.focus(); return; }
  lockWin = new BrowserWindow({ width: 560, height: 520, title: 'STZ — лицензия', backgroundColor: '#1C1D1F', autoHideMenuBar: true, resizable: false,
    webPreferences: { contextIsolation: true, sandbox: true, preload: path.join(__dirname, 'preload.js') } });
  lockWin.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(LOCK_HTML));
}
ipcMain.handle('stz:lic', () => lic.check());
ipcMain.handle('stz:licActivate', (e, key) => {
  const ok = lic.activate(key);
  if (ok) { const old = lockWin; lockWin = null; startApp(); if (old && !old.isDestroyed()) old.close(); }
  return { ok, status: lic.check() };
});

function createWindow(st) {
  const win = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1000,
    minHeight: 640,
    title: 'STZ Расчёт зубьев — версия ' + app.getVersion() + ' · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru',
    backgroundColor: '#1C1D1F',
    autoHideMenuBar: true,
    webPreferences: { contextIsolation: true, sandbox: true, preload: path.join(__dirname, 'preload.js') }
  });
  // на Mac без меню не работают Cmd+C / Cmd+V / Cmd+Q
  app.setAboutPanelOptions({ applicationName: 'STZ Расчёт зубьев', applicationVersion: app.getVersion(), copyright: '© 2026 Рафаил Шарапов · rafail@sharapov-cnc.ru', credits: 'Автор: Рафаил Шарапов\nrafail@sharapov-cnc.ru\nСделано специально по заказу ООО «СпецТехЗаказ»' });
  Menu.setApplicationMenu(isMac ? Menu.buildFromTemplate([{ role: 'appMenu' }, { role: 'editMenu' }, { role: 'windowMenu' }]) : null);
  mainWin = win;
  const base = 'STZ Расчёт зубьев — версия ' + app.getVersion() + ' · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru';
  const setTitle = s => { win.setTitle(s && s.state === 'trial' ? base + ' · пробный период, осталось ' + s.daysLeft + ' дн.' : base); };
  win.on('page-title-updated', e => e.preventDefault());
  setTitle(st);
  win.loadFile(entry());
  win.webContents.setWindowOpenHandler(({ url }) => { shell.openExternal(url); return { action: 'deny' }; });
}

function startApp() {
  const st = lic.check();
  if (!licOk(st)) { showLock(); return; }
  if (!apiStarted) { apiStarted = true; startApi(); }
  if (!mainWin || mainWin.isDestroyed()) createWindow(st);
}
const base0 = 'STZ Расчёт зубьев — версия ' + app.getVersion() + ' · Сделано по заказу ООО «СпецТехЗаказ» · Рафаил Шарапов · rafail@sharapov-cnc.ru';
setInterval(() => {
  const st = lic.check();
  if (!licOk(st)) { showLock(); if (mainWin && !mainWin.isDestroyed()) mainWin.destroy(); }
  else if (mainWin && !mainWin.isDestroyed() && st.state === 'trial') mainWin.setTitle(base0 + ' · пробный период, осталось ' + st.daysLeft + ' дн.');
}, 3600 * 1000);

app.whenReady().then(startApp);
app.on('window-all-closed', () => app.quit());
