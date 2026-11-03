// Передача УП на стойку Fanuc по Ethernet (FOCAS), та же логика, что в STZ & FANUC LAN
const koffi = require('koffi');
const path = require('path');
const fs = require('fs');

const EW_OK = 0, EW_BUFFER = 10, EW_DATA = 5, EW_NUMBER = 3;
const ERRORS = {
  1: 'функция не выполнена / не поддерживается этой стойкой', 2: 'ошибка длины данных', 3: 'ошибка номера',
  4: 'ошибка атрибута', 5: 'ошибка данных (программа с таким номером уже есть или ошибка в тексте)',
  6: 'нет нужной опции на стойке', 7: 'защита от записи (ключ защиты памяти / параметр)',
  8: 'переполнение памяти стойки', 9: 'ошибка параметра CNC', 11: 'неверный путь на стойке',
  12: 'неверный режим стойки (переключите в EDIT)', 13: 'стойка отклонила запрос (идёт обработка, аларм или фоновое редактирование)',
  15: 'на стойке аларм', 17: 'защита паролем', [-1]: 'стойка занята', [-2]: 'передача прервана RESET/STOP на стойке',
  [-8]: 'неверный номер соединения (handle) — переподключитесь', [-15]: 'не найдена fwlibe64.dll (должна лежать рядом с Fwlib64.dll)',
  [-16]: 'нет связи со стойкой (проверьте IP, кабель, питание станка)', [-17]: 'ошибка протокола'
};
class FocasError extends Error { constructor(msg, code) { super(msg); this.code = code; } }

let lib = null, F = {};
function load(dllPath) {
  if (lib) return;
  if (!fs.existsSync(dllPath)) throw new FocasError('Не найдена библиотека ' + dllPath);
  try {
    const k32 = koffi.load('kernel32.dll');
    const setDir = k32.func('bool __stdcall SetDllDirectoryW(const char16_t *p)');
    setDir(path.dirname(dllPath));
  } catch (e) { }
  lib = koffi.load(dllPath);
  const f = (sig) => lib.func(sig); // синхронно: handle FOCAS действует только в своём потоке
  F.connect = f('short cnc_allclibhndl3(const char *ip, uint16_t port, int32_t timeout, _Out_ uint16_t *h)');
  F.free = f('short cnc_freelibhndl(uint16_t h)');
  F.dwnstart = f('short cnc_dwnstart4(uint16_t h, short type, const char *folder)');
  F.download = f('short cnc_download4(uint16_t h, _Inout_ int32_t *len, const void *data)');
  F.dwnend = f('short cnc_dwnend4(uint16_t h)');
  F.del = f('short cnc_pdf_del(uint16_t h, const char *path)');
  F.drive = f('short cnc_rdpdf_drive(uint16_t h, void *drv)');
  F.subdirn = f('short cnc_rdpdf_subdirn(uint16_t h, void *inp, void *out)');
  F.alldir = f('short cnc_rdpdf_alldir(uint16_t h, void *num, void *inp, void *out)');
  F.dtail = f('short cnc_getdtailerr(uint16_t h, void *err)');
}
const chk = (what, r) => { if (r !== EW_OK) throw new FocasError(what + ': ' + (ERRORS[r] || 'ошибка') + ' (код ' + r + ')', r); };
const sleep = ms => { Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms); };

// как build_focas_payload в STZ & FANUC LAN
function buildPayload(text, fallbackName) {
  let lines = text.replace(/\r/g, '').split('\n').filter(l => l.trim() && l.trim() !== '%');
  if (!lines.length) throw new FocasError('Программа пустая');
  let added = null;
  const first = lines[0].trimStart();
  if (!(first[0] && first[0].toUpperCase() === 'O') && !first.startsWith('<')) {
    const stem = String(fallbackName || 'PROGRAM').replace(/\.[^.]*$/, '').replace(/[^A-Za-z0-9_\-.]/g, '');
    added = /^\d+$/.test(stem) ? 'O' + stem.padStart(4, '0') : '<' + (stem || 'PROGRAM') + '>';
    lines.unshift(added);
  }
  let m = /^\s*(O\d+)/i.exec(lines[0]), cncName = m ? m[1].toUpperCase() : null;
  if (!cncName && (m = /^\s*<([^>]+)>/.exec(lines[0]))) cncName = m[1];
  const body = '\n' + lines.join('\n') + '\n%';
  let nonAscii = 0;
  const buf = Buffer.alloc(body.length);
  for (let i = 0; i < body.length; i++) { const c = body.charCodeAt(i); if (c > 126 && c !== 127) { nonAscii++; buf[i] = 63; } else buf[i] = c; }
  return { payload: buf, cncName, added, nonAscii };
}

function detailErr(h) {
  try { const b = Buffer.alloc(8); if (F.dtail(h, b) === EW_OK) return { no: b.readInt16LE(0), dt: b.readInt32LE(4) }; } catch (e) { }
  return null;
}
function lineAt(payload, pos) { let n = 0; for (let i = 0; i < Math.min(pos, payload.length); i++) if (payload[i] === 10) n++; return n; }
function existsIn(h, folder, name) {
  try { return listDir(h, folder).some(e => !e.isDir && e.name.toUpperCase() === name.toUpperCase()); } catch (e) { return null; }
}
const existsErr = (name) => { const e = new FocasError('exists', EW_DATA); e.exists = true; e.cncName = name; return e; };

function send(o, progress) {
  load(o.dll);
  let folder = String(o.folder || '').trim().replace(/\\/g, '/').replace(/^\/*/, '//').replace(/([^/])\/{2,}/g, '$1/');
  if (!folder.endsWith('/')) folder += '/';
  const { payload, cncName, added } = buildPayload(o.text, o.name);
  const hh = [0];
  chk('Подключение к ' + o.ip, F.connect(o.ip, o.port || 8193, 10, hh));
  const h = hh[0];
  try {
    // как в STZ & FANUC LAN: сначала смотрим, нет ли уже такой программы в папке
    if (cncName && !o.replace) {
      progress && progress({ msg: 'Проверяю папку ' + folder + ' …' });
      if (existsIn(h, folder, cncName)) throw existsErr(cncName);
    }
    if (o.replace && cncName) {
      progress && progress({ msg: 'Удаляю старую ' + cncName + ' …' });
      const r = F.del(h, folder + cncName);
      if (r !== EW_OK && r !== 5 && r !== 3) chk('Удаление ' + cncName, r);
    }
    chk('Начало записи в ' + folder, F.dwnstart(h, 0, folder));
    let ok = false, failR = null, failPos = 0, det = null;
    try {
      const CH = 1280; let pos = 0, stalls = 0;
      while (pos < payload.length) {
        const chunk = payload.subarray(pos, Math.min(pos + CH, payload.length));
        const len = [chunk.length];
        const r = F.download(h, len, chunk);
        if (r === EW_BUFFER || (r === EW_OK && len[0] <= 0)) { if (++stalls > 600) throw new FocasError('Стойка не принимает данные (таймаут)'); sleep(50); continue; }
        if (r !== EW_OK) { failR = r; failPos = pos; det = detailErr(h); break; }
        stalls = 0; pos += len[0];
        progress && progress({ done: pos, total: payload.length });
      }
      ok = failR === null;
    } finally {
      const r = F.dwnend(h);
      if (ok && r !== EW_OK) { failR = r; failPos = payload.length; det = detailErr(h) || det; }
    }
    if (failR !== null) {
      // стойка отклонила: если программа с таким номером всё-таки есть — предложить замену
      if (failR === EW_DATA && cncName && !o.replace && existsIn(h, folder, cncName)) throw existsErr(cncName);
      const ln = lineAt(payload, failPos);
      let msg = 'Стойка отклонила программу: ' + (ERRORS[failR] || 'ошибка') + ' (код ' + failR + (det ? ', детально ' + det.no + '/' + det.dt : '') + ')';
      if (failPos < payload.length) msg += '. Принято ~' + Math.round(100 * failPos / payload.length) + '% (около строки ' + ln + ').';
      if (failR === EW_DATA) msg += ' Программы ' + (cncName || '') + ' в папке ' + folder + ' нет — проверьте: свободная память стойки (страница PROG → папки), нет ли ' + (cncName || 'этого номера') + ' в другой папке, не идёт ли редактирование/выполнение на стойке.';
      const e = new FocasError(msg, failR); throw e;
    }
    return { cncName, added, bytes: payload.length, folder };
  } finally { try { F.free(h); } catch (e) { } }
}

/* ---------- чтение папок стойки (как в STZ & FANUC LAN) ---------- */
const cstr = (b, off, len) => { let e = off; while (e < off + len && b[e] !== 0) e++; return b.toString('latin1', off, e).trim(); };
function inDir(path, reqNum, sizeKind, type) {
  const b = Buffer.alloc(220); b.write(path, 0, 211, 'latin1');
  b.writeInt16LE(reqNum || 0, 212); b.writeInt16LE(sizeKind || 0, 214); b.writeInt16LE(type || 0, 216); return b;
}
function readDrives(h) {
  const b = Buffer.alloc(4 + 16 * 12);
  chk('Чтение дисков стойки', F.drive(h, b));
  const n = Math.max(0, Math.min(b.readInt16LE(0), 16)), out = [];
  for (let i = 0; i < n; i++) { const d = cstr(b, 4 + i * 12, 12); if (d) out.push(d); }
  return out;
}
function countDir(h, path) {
  const o = Buffer.alloc(64);
  let r; try { r = F.subdirn(h, inDir(path), o); } catch (e) { return null; }
  if (r !== EW_OK) return null;
  return Math.max(0, o.readInt16LE(0)) + Math.max(0, o.readInt16LE(2));
}
function readBatch(h, path, start, batch) {
  const SZ = 128, num = Buffer.alloc(2), out = Buffer.alloc(SZ * batch);
  num.writeInt16LE(batch, 0);
  const r = F.alldir(h, num, inDir(path, start, 1, 1), out);
  const n = num.readInt16LE(0), items = [];
  if (r === EW_OK) for (let i = 0; i < Math.max(0, Math.min(n, batch)); i++) {
    const o = i * SZ, name = cstr(out, o + 28, 36);
    if (!name) continue;
    const y = out.readInt16LE(o + 2), pad = v => String(v).padStart(2, '0');
    items.push({ name, isDir: out.readInt16LE(o) === 0, size: out.readInt32LE(o + 20), comment: cstr(out, o + 64, 52),
      date: y ? y + '-' + pad(out.readInt16LE(o + 4)) + '-' + pad(out.readInt16LE(o + 6)) + ' ' + pad(out.readInt16LE(o + 8)) + ':' + pad(out.readInt16LE(o + 10)) : '' });
  }
  return { r, items, n };
}
function listDir(h, path) {
  const total = countDir(h, path);
  if (total === 0) return [];
  const batch = 10; let lastR = EW_OK;
  for (const base of [0, 1]) {
    let entries = [], start = base, failed = false;
    for (let k = 0; k < 1000; k++) {
      const { r, items, n } = readBatch(h, path, start, batch);
      if (r === EW_NUMBER) { if (entries.length) break; failed = true; lastR = r; break; }
      chk('Чтение папки ' + path, r);
      entries = entries.concat(items);
      if (n < batch || (total !== null && entries.length >= total)) break;
      start += n;
    }
    if (!failed) return entries;
  }
  if (total === null && lastR === EW_NUMBER) return [];
  chk('Чтение папки ' + path, lastR);
  return [];
}
function browse(o) {
  load(o.dll);
  const hh = [0];
  chk('Подключение к ' + o.ip, F.connect(o.ip, o.port || 8193, 10, hh));
  const h = hh[0];
  try {
    if (!o.path) {
      let drives = [], derr = null;
      try { drives = readDrives(h); } catch (e) { derr = e.message; }
      if (!drives.length) drives = ['CNC_MEM'];
      if (!drives.includes('CF_BIN')) drives.splice(drives.includes('CNC_MEM') ? 1 : 0, 0, 'CF_BIN');
      return { drives, derr };
    }
    const seen = new Set(), entries = listDir(h, o.path).filter(e => !seen.has(e.name) && seen.add(e.name));
    entries.sort((a, b) => (a.isDir === b.isDir ? a.name.localeCompare(b.name) : a.isDir ? -1 : 1));
    return { path: o.path, entries };
  } finally { try { F.free(h); } catch (e) { } }
}

// запуск в отдельном потоке: все вызовы FOCAS идут из одного потока, интерфейс не замирает
const { Worker, isMainThread, parentPort, workerData } = require('worker_threads');
function runAsync(o, progress) {
  return new Promise((resolve, reject) => {
    const w = new Worker(__filename.replace('app.asar' + require('path').sep, 'app.asar.unpacked' + require('path').sep), { workerData: o });
    w.on('message', m => {
      if (m.t === 'p') progress && progress(m.p);
      else if (m.t === 'ok') resolve(m.r);
      else if (m.t === 'err') { const e = new FocasError(m.e.message, m.e.code); Object.assign(e, m.e); reject(e); }
    });
    w.on('error', e => reject(new FocasError(String(e && e.message || e))));
    w.on('exit', c => { if (c !== 0) reject(new FocasError('Поток передачи завершился с кодом ' + c)); });
  });
}
if (!isMainThread && workerData && workerData.dll) {
  try { const r = workerData.op === 'browse' ? browse(workerData) : send(workerData, p => parentPort.postMessage({ t: 'p', p })); parentPort.postMessage({ t: 'ok', r }); }
  catch (e) { parentPort.postMessage({ t: 'err', e: { message: String(e && e.message || e), code: e && e.code, exists: !!(e && e.exists), cncName: e && e.cncName } }); }
}
const sendAsync = (o, p) => runAsync(Object.assign({}, o, { op: 'send' }), p);
const browseAsync = (o) => runAsync(Object.assign({}, o, { op: 'browse' }));
module.exports = { send, browse, sendAsync, browseAsync, buildPayload, FocasError };
