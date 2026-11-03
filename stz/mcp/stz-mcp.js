#!/usr/bin/env node
// MCP-сервер «STZ Расчёт зубьев» — подключение Claude Desktop к запущенной программе.
// Протокол MCP (JSON-RPC 2.0 через stdin/stdout). Программа должна быть открыта:
// она слушает http://127.0.0.1:47321/api (только этот компьютер).
'use strict';
const http = require('http');
const PORT = +process.env.STZ_PORT || 47321;

function api(method, args) {
  return new Promise((resolve, reject) => {
    const body = Buffer.from(JSON.stringify({ method, args: args || {} }), 'utf8');
    const req = http.request({ host: '127.0.0.1', port: PORT, path: '/api', method: 'POST', timeout: 120000,
      headers: { 'Content-Type': 'application/json', 'Content-Length': body.length } }, res => {
      let d = ''; res.setEncoding('utf8'); res.on('data', c => d += c);
      res.on('end', () => { try { const j = JSON.parse(d); j.ok ? resolve(j.result) : reject(new Error(j.error || 'ошибка программы')); } catch (e) { reject(new Error('неверный ответ программы')); } });
    });
    req.on('timeout', () => req.destroy(new Error('программа не отвечает')));
    req.on('error', e => reject(new Error(e.code === 'ECONNREFUSED' ? 'Программа «STZ Расчёт зубьев» не запущена. Откройте её и повторите.' : e.message)));
    req.end(body);
  });
}

const P = (props, req) => ({ type: 'object', properties: props || {}, required: req || [] });
const TOOLS = [
  { name: 'stz_info', method: 'info', description: 'Справочник программы: типы передач (spur, helical, herring, internal, rack, bevel, hypoid, worm, globoid, planet, chain, belt, spline), ключи параметров с названиями, единицами и пределами, стандарты, форматы экспорта. Вызови первым.', inputSchema: P() },
  { name: 'stz_state', method: 'state', description: 'Текущий проект: список передач и полный расчёт выбранной (геометрия, прочность, контроль, предупреждения).', inputSchema: P() },
  { name: 'stz_select', method: 'select', description: 'Выбрать передачу проекта по id.', inputSchema: P({ id: { type: 'string' } }, ['id']) },
  { name: 'stz_add', method: 'add', description: 'Добавить новую передачу в проект и сделать её текущей. Неуказанные параметры — по умолчанию.', inputSchema: P({ type: { type: 'string' }, name: { type: 'string' }, params: { type: 'object', description: 'ключ → число (мм, кВт, об/мин)' } }, ['type']) },
  { name: 'stz_set_type', method: 'set_type', description: 'Сменить тип текущей передачи (параметры сбрасываются на значения по умолчанию).', inputSchema: P({ type: { type: 'string' } }, ['type']) },
  { name: 'stz_set_params', method: 'set_params', description: 'Изменить параметры текущей передачи и получить пересчёт. Значения всегда в мм, кВт, об/мин, градусах, даже при стандарте AGMA. Для списков — номер варианта (0, 1, 2…).', inputSchema: P({ params: { type: 'object' } }, ['params']) },
  { name: 'stz_set_standard', method: 'set_std', description: 'Стандарт расчёта: ГОСТ, ISO или AGMA.', inputSchema: P({ std: { type: 'string', enum: ['ГОСТ', 'ISO', 'AGMA'] } }, ['std']) },
  { name: 'stz_set_project', method: 'set_project', description: 'Новый проект или переименование. new=true — очистить проект и начать с прямозубой пары.', inputSchema: P({ name: { type: 'string' }, new: { type: 'boolean' } }) },
  { name: 'stz_export', method: 'export', description: 'Сохранить файл в папку «Документы\\STZ»: step (твёрдое тело текущей передачи, для Fusion 360/КОМПАС/SolidWorks), assembly_step (весь редуктор), dxf, svg, drawing_svg / drawing_dxf (рабочий чертёж A3 ГОСТ 2.403), project (.zrp). Возвращает пути файлов.', inputSchema: P({ format: { type: 'string', enum: ['step', 'assembly_step', 'dxf', 'svg', 'drawing_svg', 'drawing_dxf', 'project'] }, gear: { type: 'integer', description: 'для чертежа: 0 — шестерня, 1 — колесо' } }, ['format']) }
];

const send = m => process.stdout.write(JSON.stringify(m) + '\n');
const reply = (id, result) => send({ jsonrpc: '2.0', id, result });
const fail = (id, code, message) => send({ jsonrpc: '2.0', id, error: { code, message } });

async function handle(msg) {
  const { id, method, params } = msg;
  if (id === undefined || id === null) return; // уведомления
  if (method === 'initialize') return reply(id, { protocolVersion: (params && params.protocolVersion) || '2024-11-05', capabilities: { tools: {} }, serverInfo: { name: 'stz-raschet-zubev', version: '1.0.0' },
    instructions: 'Инструменты управляют программой расчёта передач «STZ Расчёт зубьев» (ООО «СпецТехЗаказ»). Начни с stz_info и stz_state. Отвечай пользователю по-русски.' });
  if (method === 'ping') return reply(id, {});
  if (method === 'tools/list') return reply(id, { tools: TOOLS.map(({ method, ...t }) => t) });
  if (method === 'tools/call') {
    const t = TOOLS.find(x => x.name === (params && params.name));
    if (!t) return fail(id, -32602, 'нет инструмента ' + (params && params.name));
    try { const r = await api(t.method, (params && params.arguments) || {}); return reply(id, { content: [{ type: 'text', text: JSON.stringify(r, null, 1) }] }); }
    catch (e) { return reply(id, { content: [{ type: 'text', text: 'Ошибка: ' + e.message }], isError: true }); }
  }
  fail(id, -32601, 'метод не поддерживается: ' + method);
}

let buf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', ch => {
  buf += ch; let i;
  while ((i = buf.indexOf('\n')) >= 0) {
    const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1);
    if (!line) continue;
    let msg; try { msg = JSON.parse(line); } catch (e) { fail(null, -32700, 'parse error'); continue; }
    handle(msg).catch(e => msg.id != null && fail(msg.id, -32603, e.message));
  }
});
process.stdin.on('end', () => process.exit(0));
