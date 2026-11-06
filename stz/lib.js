
const D2R=Math.PI/180,KEY='zuboraschet.project.v1',VER='1.0.0-beta.23',BUILD='2026-11-06',MAIL='rafail@sharapov-cnc.ru';
const inv=a=>Math.tan(a)-a;
function hobToast(txt,ok){try{let d=document.getElementById('hobToast');if(d)d.remove();d=document.createElement('div');d.id='hobToast';d.style.cssText='position:fixed;left:50%;bottom:48px;transform:translateX(-50%);z-index:99999;max-width:640px;padding:14px 22px;border-radius:10px;font:600 15px/1.4 IBM Plex Sans,Arial,sans-serif;color:#fff;box-shadow:0 12px 36px rgba(0,0,0,.45);background:'+(ok?'#1E8E3E':'#B3261E')+';cursor:pointer;';d.textContent=txt;d.onclick=()=>d.remove();document.body.appendChild(d);setTimeout(()=>{if(d.parentNode)d.remove();},ok?7000:12000);}catch(e){}}
function invInv(v){if(!(v>0))return 0.0005;let a=Math.min(1.2,Math.cbrt(3*v));for(let i=0;i<40;i++){const t=Math.tan(a);a-=(t-a-v)/(t*t);if(a<=0)a=1e-4;}return a;}
const fmt=(v,d=2)=>Number.isFinite(v)?v.toFixed(d).replace('.',',').replace('-','−'):'—';
const numStr=v=>Number.isFinite(v)?String(+v.toFixed(4)).replace('.',','):'';
/* Гипоидная передача: геометрия и наладки станков 528С / 525 / 5А27С1
   по «Руководству по расчёту геометрических размеров гипоидных зубчатых колёс и наладок
   для их нарезания на станках моделей 528С, 525 и 5А27С1» (СЗТЗС, Саратов, 1967).
   Номера p[n] = номера пунктов расчётных листов I–XI. Для пунктов с двумя значениями:
   p[n] = [вогнутая сторона шестерни, выпуклая сторона шестерни].
   ООО «СпецТехЗаказ» — общий модуль для STZ и «Зуборез 525». */
var HYPOID = (function () {
  const R = d => d * Math.PI / 180, D = r => r * 180 / Math.PI;
  const MACH = {
    "528С": { E2: 340, offDown: 100, offUp: 110, dmax: 800, mnMin: 2, mnMax: 8, heads: [76.2, 95.25, 114.3, 152.4, 228.6], Rl: 600, Cm1: 2.083, EmMax: 25, Cpsi: 137.6 },
    "525": { E2: 270, offDown: 65, offUp: 75, dmax: 500, mnMin: 2, mnMax: 6.25, heads: [76.2, 95.25, 114.3, 152.4], Rl: 405, Cm1: 1.71, EmMax: 20, Cpsi: 172 },
    "5А27С1": { E2: 240, offDown: 75, offUp: 75, dmax: 500, mnMin: 2, mnMax: 6.25, heads: [76.2, 95.25, 114.3, 152.4], Rl: 405, Cm1: 1.62, EmMax: 25, Cpsi: 0 }
  };
  // табл. 3: профильные углы двусторонних головок колеса (внутр. +, наруж. −)
  const T3 = { "42.5": { inn: [18, 14, 10], out: [-24.5, -28.5, -32.5] }, "45": { inn: [22.5, 20, 16, 12], out: [-22.5, -25, -29, -33] } };
  // табл. 4: односторонние головки шестерни
  const T4 = { inn: [-26, -28.5, -31, -33], out: [10, 14, 16, 18, 20] };
  // табл. 5: черновые головки шестерни
  const T5 = { inn: [14, 12, 10, 20, 18, 16], out: [28.5, 30.5, 32.5, 25, 27, 29] };
  // табл. 6: односторонние головки — пределы образующих ДИАМЕТРОВ
  const T6 = [
    { nom: '6"', h: 9.5, base: 67.7, out: [146.8, 156.2], inn: [149.1, 158.2] },
    { nom: '7,5"', h: 12.7, base: 74.5, out: [165.6, 180.1], inn: [166.4, 180.9] },
    { nom: '9"', h: 14.3, base: 80.8, out: [178.3, 182.8], inn: [178.1, 193.5] },
    { nom: '9"', h: 14.3, base: 87.2, out: [191.0, 205.5], inn: [191.8, 206.2] },
    { nom: '9"', h: 14.3, base: 93.5, out: [203.7, 218.2], inn: [204.5, 218.9] },
    { nom: '9"', h: 14.3, base: 103.1, out: [222.8, 237.2], inn: [223.5, 238.0] },
    { nom: '9"', h: 14.3, base: 108.2, out: [232.9, 247.4], inn: [233.7, 248.2] },
    { nom: '9"', h: 14.3, base: 114.9, out: [245.6, 260.1], inn: [246.4, 260.9] },
    { nom: '12"', h: 19.5, base: 122.4, out: [263.14, 282.19], inn: [265.2, 284.2] },
    { nom: '12"', h: 19.5, base: 130.1, out: [278.4, 297.4], inn: [280.4, 299.5] },
    { nom: '12"', h: 19.5, base: 138.9, out: [296.2, 315.2], inn: [298.2, 317.2] },
    { nom: '12"', h: 19.5, base: 146.6, out: [311.4, 330.5], inn: [313.4, 332.5] },
    { nom: '18"', h: 19.5, base: 205.5, out: [428.8, 458.7], inn: [457.2, 487.2] }];
  // табл. 7: модификатор
  const T7 = [[0.005, 0.17], [0.007, 0.21], [0.01, 0.25], [0.02, 0.33], [0.03, 0.40], [0.04, 0.5], [0.06, 0.63], [0.09, 0.75], [0.11, 0.83], [0.16, 1.0], [0.29, 1.33], [0.41, 1.6], [0.61, 2.0], [1.03, 2.83]];
  const KZ = z => z <= 6 ? 3.5 : z === 7 ? 3.6 : z === 8 ? 3.7 : z === 9 ? 3.8 : z === 10 ? 3.9 : 4.0;
  const KG = (z, i) => {
    if (z <= 20) return z <= 6 ? 0.11 : z === 7 ? 0.13 : z === 8 ? 0.15 : 0.17;
    const t = [[1, 0.5], [1.1, 0.45], [1.25, 0.425], [1.4, 0.4], [1.7, 0.375], [2, 0.35], [2.5, 0.325], [3.3, 0.3]];
    if (i >= 3.3) return 0.3; for (let k = 1; k < t.length; k++) if (i <= t[k][0]) { const a = t[k - 1], b = t[k]; return a[1] + (b[1] - a[1]) * (i - a[0]) / (b[0] - a[0]) } return 0.3
  };
  const BACK = m => m <= 1.25 ? [0.03, 0.08] : m <= 2.5 ? [0.05, 0.10] : m <= 4 ? [0.10, 0.15] : m <= 6 ? [0.15, 0.25] : m <= 8 ? [0.2, 0.25] : [0.3, 0.4];
  const nearest = (v, arr) => arr.reduce((a, b) => Math.abs(b - v) < Math.abs(a - v) ? b : a);
  const ceil01 = x => Math.ceil(x * 10 - 1e-9) / 10, round01 = x => Math.round(x * 10) / 10, floor01 = x => Math.floor(x * 10 + 1e-9) / 10;

  function hypoid(inp) {
    const M = MACH[inp.machine || "525"];
    const zsh = inp.z1, zk = inp.z2, F = inp.F, E = inp.E, Dk = inp.Dk;
    const handP = inp.hand1 || "L";            // направление спирали шестерни (L — левая)
    const warn = [], note = [];
    const p = [];
    p[1] = zsh; p[2] = zk; p[3] = zsh / zk; p[4] = F; p[5] = E; p[6] = Dk;
    const i = zk / zsh, msk = Dk / zk;
    // радиус головки
    let ru = inp.ru;
    if (!(ru > 0)) { const RT = [[127, 165, 76.2], [165, 216, 95.25], [203, 279, 111.3], [227, 381, 152.4], [400, 560, 228.6]]; let hit = null; RT.forEach(r => { if (Dk >= r[0] && Dk <= r[1]) hit = r[2] }); ru = hit || (Dk < 127 ? 76.2 : Dk < 400 ? 152.4 : 228.6); ru = nearest(ru, M.heads) }
    p[7] = ru; p[8] = inp.beta1 || 50; p[9] = Math.tan(R(p[8]));
    p[10] = inp.ctgPhi0 > 0 ? inp.ctgPhi0 : 1.2 * p[3];
    p[11] = 1 / Math.sqrt(1 + p[10] ** 2);
    p[12] = (p[6] - p[4] * p[11]) / 2;
    p[13] = p[5] * p[11] / p[12]; p[14] = Math.sqrt(1 - p[13] ** 2);
    p[15] = p[14] + p[9] * p[13]; p[16] = p[3] * p[12]; p[17] = p[15] * p[16];
    p[18] = (p[12] + p[3] * p[17]) / p[3];
    const Q = inp.Qconst > 0 ? inp.Qconst : 0.75;
    p[19] = Q * p[18];
    // попытки по η
    function attempt(tgEta) {
      const a = {}; a[20] = tgEta; a[21] = 1 / Math.sqrt(1 + tgEta ** 2); a[22] = a[20] * a[21];
      a[23] = p[5] - p[17] * a[22]; a[24] = a[23] / p[12]; a[25] = a[24] / Math.sqrt(1 - a[24] ** 2);
      a[26] = a[22] / a[25]; a[27] = 1 / Math.sqrt(1 + a[26] ** 2); a[28] = a[24] / a[27]; a[29] = Math.sqrt(1 - a[28] ** 2);
      a[30] = (p[15] - a[29]) / a[28]; a[31] = a[28] * (p[9] - a[30]); a[32] = p[3] * a[31];
      a[33] = a[24] - a[22] * a[32]; a[34] = a[33] / Math.sqrt(1 - a[33] ** 2);
      a[35] = a[22] / a[34]; a[36] = D(Math.atan(a[35])); a[37] = Math.cos(R(a[36]));
      a[38] = a[33] / a[37]; a[39] = D(Math.asin(a[38])); a[40] = Math.cos(R(a[39]));
      a[41] = (p[15] + a[31] - a[40]) / a[38]; a[42] = D(Math.atan(a[41])); a[43] = Math.cos(R(a[42]));
      a[44] = a[42] - a[39]; a[45] = Math.cos(R(a[44])); a[46] = Math.tan(R(a[44]));
      a[47] = a[20] / a[33]; a[48] = D(Math.atan(1 / a[47])); a[49] = Math.sin(R(a[48])); a[50] = Math.cos(R(a[48]));
      a[51] = (p[17] + p[12] * a[32]) / a[37]; a[52] = p[12] / a[50]; a[53] = a[51] + a[52];
      a[54] = p[12] * a[45] / a[49]; a[55] = a[43] * a[51] / a[35];
      a[56] = (a[41] * a[55] - a[46] * a[54]) / a[53]; a[57] = D(Math.atan(a[56])); a[58] = Math.cos(R(a[57]));
      a[59] = a[41] * a[56] / a[51]; a[60] = a[46] * a[56] / a[52]; a[61] = a[54] * a[55]; a[62] = (a[54] - a[55]) / a[61];
      a[63] = a[59] + a[60] + a[62]; a[64] = (a[41] - a[46]) / a[63]; a[65] = a[64] / a[58]; a[66] = p[7] / a[65];
      return a
    }
    const tries = [];
    let A = attempt(p[5] / p[19]); tries.push(A);
    if (Math.abs(A[65] - ru) / ru > 0.03) {
      const t2 = A[20] * (A[65] < ru ? 0.9 : 1.1); const A2 = attempt(t2); tries.push(A2);
      if (Math.abs(A2[65] - ru) / ru > 0.03) {
        const t3 = (A2[20] - A[20]) / (A2[66] - A[66]) * (1 - A[66]) + A[20];
        const A3 = attempt(t3); tries.push(A3); A = A3;
        if (Math.abs(A3[65] - ru) / ru > 0.03) warn.push(`Радиус кривизны ρ = ${A3[65].toFixed(2)} отличается от радиуса головки ${ru} больше чем на 3 % после 3 попыток — проверьте исходные данные (п. 65).`);
      } else A = A2;
    }
    for (let k = 20; k <= 66; k++) p[k] = A[k];
    p.tries = tries.map(t => ({ tgEta: t[20], rho: t[65], phi1: t[36], phi2: t[48], beta1: t[42], beta2: t[44] }));
    if (p[44] > 35.0001) warn.push(`Угол спирали колеса βк = ${p[44].toFixed(2)}° больше 35° (п. 44) — уменьшите угол спирали шестерни (например, 45°).`);
    // Лист II, одна колонка
    p[67] = p[34] ** 2 / (1 + p[34] ** 2); p[68] = Math.sqrt(1 + p[34] ** 2); p[69] = p[3] * p[68];
    p[70] = p[49] * p[51]; p[71] = p[12] * p[47] - p[70]; p[72] = p[12] / p[49]; p[73] = 0.5 * p[6] / p[49]; p[74] = p[73] - p[72];
    const Kz = inp.Kz > 0 ? inp.Kz : KZ(zsh);
    p[75] = Kz * p[12] * p[45] / p[2];
    p[76] = p[12] * p[46] / p[7]; p[77] = p[49] / p[45] - p[76];
    p[78] = inp.alphaSum > 0 ? inp.alphaSum : (zsh >= 8 ? 42.5 : 45);
    p[79] = Math.sin(R(p[78])); p[80] = p[78] / 2; p[81] = Math.cos(R(p[80])); p[82] = Math.tan(R(p[80]));
    p[83] = p[77] / p[82]; p[84] = 10560 * p[83] / p[2];             // минуты
    p[85] = inp.Kg > 0 ? inp.Kg : KG(zsh, i); p[86] = 1.125 - p[85];
    p[87] = p[75] * p[85]; p[88] = p[75] * p[86] + 0.05;
    p[89] = p[84] * p[85]; p[90] = Math.sin(R(p[89] / 60)); p[91] = p[84] - p[89]; p[92] = Math.sin(R(p[91] / 60));
    p[93] = p[87] + p[74] * p[90]; p[94] = p[88] + p[74] * p[92]; p[95] = 0.125 * p[75] + 0.05;
    p[96] = p[93] + p[94]; p[97] = p[96] - p[95];
    p[98] = p[48] + p[89] / 60; p[99] = Math.sin(R(p[98])); p[100] = Math.cos(R(p[98]));
    // Лист III
    p[101] = p[48] - p[91] / 60; p[102] = Math.sin(R(p[101])); p[103] = Math.cos(R(p[101])); p[104] = p[103] / p[102];
    p[105] = p[93] * p[50] / 0.5 + p[6]; p[106] = p[70] + p[74] * p[50]; p[107] = p[106] - p[93] * p[49];
    p[108] = (p[72] * p[90] - p[87]) / p[99]; p[109] = (p[72] * p[92] - p[88]) / p[102];
    p[110] = p[71] - p[108]; p[111] = p[71] + p[109];
    p[112] = p[12] + p[70] * p[104]; p[113] = p[5] / p[112]; p[114] = Math.sqrt(1 - p[113] ** 2); p[115] = p[113] / p[114];
    p[116] = p[103] * p[114]; p[117] = D(Math.asin(p[116])); p[118] = Math.cos(R(p[117])); p[119] = Math.tan(R(p[117]));
    p[120] = (p[102] * p[111] + p[95]) / p[103]; p[121] = (p[5] * p[113] - p[120]) / p[114];
    p[122] = p[69] * p[47]; p[123] = (p[47] ** 2 - p[122]) / (1 + p[122]); p[124] = (1 + p[123] * p[67]) / p[114]; p[125] = p[102] * p[124];
    p[126] = p[12] - p[50] * p[75] + p[50] * p[87]; p[127] = p[114] * p[126] + p[74] * p[125];
    p[128] = p[75] - p[75] * p[3]; p[129] = p[127] - p[125] * p[4];
    p[130] = p[127] + p[113] * p[128]; p[131] = round01(p[130]); p[132] = p[129] - p[113] * p[128]; p[133] = round01(p[132]);
    p[134] = p[121] + p[131]; p[135] = p[119] * p[134] / 0.5;
    p[136] = p[70] * p[100] / p[99] + p[12]; p[137] = p[5] / p[136]; p[138] = D(Math.asin(p[137])); p[139] = Math.cos(R(p[138]));
    p[140] = (p[99] * p[110] + p[95]) / p[100]; p[141] = (p[5] * p[137] - p[140]) / p[139];
    p[142] = p[100] * p[139]; p[143] = D(Math.asin(p[142])); p[144] = Math.cos(R(p[143])); p[145] = Math.tan(R(p[143]));
    const bl = BACK(msk); p[146] = inp.jmin > 0 ? inp.jmin : bl[0]; p[147] = inp.jmax > 0 ? inp.jmax : bl[1];
    p[148] = p[90] + p[92]; p[149] = p[96] - p[4] * p[148]; p[150] = p[73] - p[4];
    // Лист IV
    p[151] = p[117] - p[143]; p[152] = Math.sin(R(p[151])); p[153] = Math.cos(R(p[151])); p[154] = Math.tan(R(p[151]));
    p[155] = p[115] / p[102]; p[156] = Math.sqrt(1 + p[155] ** 2); p[157] = 1 / p[156]; p[158] = p[155] * p[157];
    p[159] = p[12] / p[102]; p[160] = p[12] / p[103]; p[161] = p[70] / p[102]; p[162] = p[161] * p[118]; p[163] = p[161] + p[160];
    p[164] = p[3] / p[114]; p[165] = p[12] * p[164] / p[70]; p[166] = (1 - p[165]) / p[155]; p[167] = D(Math.atan(p[166]));
    p[168] = Math.sqrt(1 + p[166] ** 2); p[169] = 1 / p[168]; p[170] = p[169] * p[166];
    p[171] = p[169] * p[157] - p[158] * p[170]; p[172] = D(Math.acos(p[171])); p[173] = Math.sqrt(1 - p[171] ** 2); p[174] = p[173] / p[171];
    p[175] = p[173] * p[161] / p[119]; p[176] = (p[175] - p[159] * p[170]) / p[163];
    p[177] = p[170] * p[171] / p[160]; p[178] = p[169] * p[173] / p[161] + p[177]; p[179] = p[171] / p[159];
    p[180] = p[119] * p[169] / p[161] - p[179]; p[181] = (p[176] * p[178] + p[180]) / p[158];
    p[182] = p[79] * p[176]; p[183] = Math.sqrt(1 + p[182] ** 2); p[184] = (p[183] - 1) / p[79] + p[82];
    p[185] = p[184] - p[176]; p[186] = D(Math.atan(p[185])); p[187] = -p[184] - p[176]; p[188] = D(Math.atan(p[187]));
    p[189] = (1 + p[104] * p[164]) / p[155]; p[190] = p[189] * p[169] - p[170];
    p[191] = p[75] - 2 * p[87]; p[192] = p[12] * p[169]; p[193] = Math.PI * p[192] / p[2];
    p[194] = 1 / p[189] + p[166]; p[195] = p[4] * p[194] * p[169] / (2 * p[193]);
    p[196] = p[185] - p[187]; p[197] = 0.5 * p[75] + p[95]; p[198] = p[193] - p[196] * p[197];
    p[199] = inp.Wk > 0 ? inp.Wk : ceil01(p[198]);
    const dW = msk <= 8 ? 0.5 : 0.8;
    p[200] = round01(p[199] - dW);
    // Лист V
    p[201] = (p[72] ** 2 - p[150] ** 2) / (2 * p[7]); p[202] = (p[72] * p[170] - p[201]) / p[150]; p[203] = D(Math.asin(p[202])); p[204] = Math.cos(R(p[203]));
    p[205] = 2 * Math.PI * p[49] / p[2]; p[206] = p[204] * p[205]; p[207] = p[95] + p[149];
    p[208] = p[150] * p[206] - p[196] * p[207]; p[209] = p[208] - p[199] + p[146];
    p[210] = floor01(p[209] - dW);
    p[211] = Math.ceil((0.5 * p[209] + 0.075) * 100) / 100;
    if (p[211] > p[210]) p[211] = p[210];
    p[212] = 1 / p[81] - p[82]; p[213] = p[81] * p[212]; p[214] = p[147] * p[150] / p[73]; p[215] = 0.5 * p[214] / p[82];
    p[216] = (p[95] - p[215]) / p[213]; p[217] = (p[211] - 0.4) / p[212];
    p[218] = Math.max(0.2, floor01(Math.min(p[216], p[217]) + 0.04));
    p[219] = Math.max(0.2, round01(p[216]));
    p[220] = p[142] * p[141] - p[142] * p[121]; p[221] = p[134] * p[152] / p[118] - p[220];
    // (222): в формуле «+ (199) − (198)», в числовом примере знак обратный (1,84) — принят по примеру
    p[222] = p[191] * p[184] - (p[199] - p[198]);
    p[223] = p[193] + p[222] - p[146]; p[224] = p[193] - p[222];
    p[225] = p[171] * p[223] / 2.83; p[226] = p[225] ** 2 / p[161] + p[75] - p[87];
    p[227] = 1 / p[104]; p[228] = p[170] * p[176]; p[229] = p[170] * p[159]; p[230] = 1 / p[181]; p[231] = p[227] - p[228];
    p[232] = 1 - p[181] * p[229]; p[233] = p[170] ** 2; p[234] = p[227] * p[232] - p[228] * p[233]; p[235] = p[169] ** 2 / p[168];
    p[236] = p[176] * p[235] / p[234]; p[237] = D(Math.atan(p[236])); p[238] = Math.cos(R(p[237]));
    p[239] = p[167] + p[237]; p[240] = Math.sin(R(p[239])); p[241] = Math.cos(R(p[239]));
    p[242] = p[159] * p[227] / p[231]; p[243] = p[242] / p[230]; p[244] = (p[243] - p[170]) / p[169]; p[245] = 1 + p[236] * p[244];
    p[246] = p[242] / p[245]; p[247] = -p[246] * p[236]; p[248] = (p[246] - p[159]) / p[103];
    p[249] = p[246] * p[241] / p[238]; p[250] = p[246] * p[240] / p[238];
    // Лист VI — колесо
    const t3 = T3[p[78] >= 44 ? "45" : "42.5"];
    const aVx = inp.aWheelConvex != null ? inp.aWheelConvex : nearest(p[186], t3.inn);    // выпуклая колеса
    const aVg = inp.aWheelConcave != null ? inp.aWheelConcave : nearest(p[188], t3.out);  // вогнутая колеса
    if (Math.abs(aVx - p[186]) > 2.5 || Math.abs(aVg - p[188]) > 2.5) warn.push(`Профильные углы резцов колеса ${p[186].toFixed(2)}° / ${p[188].toFixed(2)}° дальше ±2°30′ от стандартных (табл. 3).`);
    // колонки: [вогнутая шестерни ↔ выпуклая колеса, выпуклая шестерни ↔ вогнутая колеса]
    p[251] = [aVx, aVg]; p[252] = p[251].map(a => Math.sin(R(a))); p[253] = p[251].map(a => Math.cos(R(a))); p[254] = p[251].map(a => Math.tan(R(a)));
    p[255] = p[251]; p[256] = p[253]; p[257] = p[254];
    p[258] = p[113] * p[115] / p[5];
    p[264] = M.E2;
    p[265] = -p[88] - p[102] * p[248]; p[266] = p[88] / p[102] + p[248];
    p[267] = (p[7] - p[250]) / p[249]; p[268] = D(Math.atan(p[267])); p[269] = Math.cos(R(p[268]));
    p[270] = p[249] / p[269]; p[271] = p[167] + p[268];
    p[272] = p[270] / p[264]; if (p[272] > 1) warn.push(`Радиальная установка колеса ${p[270].toFixed(1)} мм больше эксцентриситета люльки станка.`);
    p[273] = D(Math.asin(Math.min(1, p[272]))); p[274] = 2 * p[273];
    const handW = handP === "L" ? "R" : "L";
    const norm = a => ((a % 360) + 360) % 360;
    p[275] = norm(0.5 * p[274] + (handW === "L" ? p[271] : -p[271]));
    p[277] = p[249] / p[192]; p[278] = p[2] * p[277];
    // Лист VI — шестерня
    p[282] = p[169] / p[171]; p[283] = p[240] * p[258]; p[284] = p[169] * p[238] / p[246] - p[283]; p[285] = p[283] * p[159];
    p[286] = p[283] * p[282] / p[227]; p[287] = p[172] - p[239]; p[288] = Math.sin(R(p[287])); p[289] = p[158] * p[286] / p[169];
    const S = (f) => [0, 1].map(f);
    p[290] = S(k => p[284] + p[289] * p[254][k]);
    p[291] = p[3] * p[102]; p[292] = (p[241] - p[285]) / p[16];
    p[293] = S(k => p[290][k] - p[119] * p[286]);
    p[294] = S(k => (p[169] * p[288] - p[158] * p[285]) / p[293][k]);
    p[295] = S(k => (p[292] * p[118] - p[286]) / p[293][k]);
    p[296] = p[157] * p[159]; p[297] = p[162] * p[171] / p[118];
    p[298] = S(k => p[297] * p[295][k]); p[299] = S(k => (p[298][k] * p[173] - p[294][k]) / p[171]);
    p[300] = 1 / p[190];
    // Лист VII
    p[301] = p[300] / p[296]; p[302] = 1 / p[7]; p[303] = S(k => p[256][k] * p[302] / p[253][k]);
    p[304] = inp.f > 0 ? inp.f : 0.4; p[305] = p[304] * p[4]; p[306] = p[169] / p[305];
    p[307] = S(k => (k === 0 ? -1 : 1) * 0.0254 * p[306] ** 2);
    p[308] = S(k => p[303][k] + p[307][k]); p[309] = p[104] * p[301]; p[310] = p[158] * p[309] / p[169]; p[311] = p[301] / p[282];
    p[312] = S(k => p[308][k] + p[310] * p[254][k] - p[311]); p[313] = S(k => p[312][k] - p[119] * p[309]);
    p[314] = p[311] * p[118] / p[291] + p[309]; p[315] = S(k => p[314] / p[295][k] + p[313][k]);
    p[316] = S(k => (1 - p[250] * p[308][k]) / p[249]);
    p[317] = S(k => p[316][k] * p[298][k] / p[315][k] + p[299][k]);
    p[318] = S(k => p[317][k] * p[171] + p[294][k]); p[319] = S(k => p[298][k] * p[317][k] / p[318][k]);
    p[320] = p[162] / p[118]; p[321] = S(k => (p[320] - p[319][k] * p[119]) / p[319][k]);
    p[322] = S(k => (p[252][k] * p[173] + p[321][k] * p[253][k]) / p[171]); p[323] = S(k => (p[254][k] + p[321][k] * p[173]) / p[171]);
    p[324] = S(k => -p[323][k] * p[319][k] / p[316][k]); p[325] = S(k => p[324][k] * p[315][k] / p[294][k]);
    p[326] = S(k => p[325][k] * p[318][k] - p[320]); p[327] = S(k => p[326][k] * p[320]); p[328] = S(k => (p[326][k] + p[320]) / p[327][k]);
    p[329] = S(k => (p[322][k] * p[174] + p[252][k]) / p[328][k]);
    p[330] = S(k => 0.5 * p[199] + (k === 0 ? 1 : -1) * p[88] * p[257][k]);
    p[331] = S(k => p[256][k] * p[330][k]); p[332] = S(k => p[253][k] * p[173]); p[333] = S(k => p[253][k] * p[171]);
    p[334] = p[87] + p[95]; p[335] = 10 * p[6] / (25.4 * p[2]); p[336] = p[162] * p[171] / p[1]; p[337] = 10 * p[336] ** 2; p[338] = 0.0254 * p[335] / p[337];
    p[339] = 0; p[340] = 0;
    p[341] = S(k => p[252][k] * p[118] - p[332][k] * p[116]); p[342] = S(k => p[332][k] * p[118] + p[252][k] * p[116]);
    p[343] = S(k => p[142] * p[342][k] + p[144] * p[341][k]);
    const aPinCalc = S(k => D(Math.asin(p[343][k])));
    p[344] = [inp.aPinConcave != null ? inp.aPinConcave : nearest(aPinCalc[0], T4.out), inp.aPinConvex != null ? inp.aPinConvex : nearest(aPinCalc[1], T4.inn)];
    p.aPinCalc = aPinCalc;
    p[345] = p[344].map(a => Math.sin(R(a))); p[346] = p[344].map(a => Math.cos(R(a))); p[347] = p[344].map(a => Math.tan(R(a)));
    p[348] = S(k => (p[345][k] - p[343][k]) / p[144]); p[349] = S(k => p[348][k] + p[341][k]); p[350] = S(k => p[333][k] ** 2 + p[341][k] ** 2);
    // Лист VIII
    p[351] = S(k => (p[144] * p[342][k] - p[142] * p[349][k]) / p[346][k]); p[352] = S(k => D(Math.asin(p[351][k])));
    p[353] = S(k => Math.cos(R(p[352][k]))); p[354] = S(k => Math.tan(R(p[352][k]))); p[355] = S(k => p[346][k] * p[353][k]);
    p[356] = S(k => (p[355][k] * p[341][k] - p[333][k] * p[349][k]) / p[350][k]); p[357] = S(k => D(Math.asin(p[356][k]))); p[358] = S(k => Math.cos(R(p[357][k])));
    p[359] = S(k => p[116] - p[116] * p[358][k]); p[360] = S(k => p[174] * p[356][k] + p[359][k]); p[361] = S(k => p[152] - p[144] * p[360][k]);
    p[362] = S(k => p[171] * p[361][k] / p[346][k]); p[363] = S(k => D(Math.asin(p[362][k]))); p[364] = S(k => Math.cos(R(p[363][k])));
    p[365] = S(k => p[362][k] / p[364][k]); p[366] = S(k => 1 - p[365][k] * p[322][k]); p[367] = S(k => p[365][k] * p[329][k] / p[366][k]);
    p[368] = S(k => p[367][k] * p[253][k] / p[317][k] + p[322][k]); p[369] = S(k => 1 - p[365][k] * p[368][k]);
    p[370] = S(k => (p[365][k] + p[368][k]) / p[369][k]); p[371] = S(k => p[369][k] * p[322][k] + p[369][k] * p[370][k]);
    p[372] = S(k => p[362][k] * p[364][k]); p[373] = S(k => 1 - p[371][k] * p[372][k]); p[374] = S(k => p[373][k] * p[317][k] / p[253][k]);
    p[375] = S(k => p[349][k] * p[356][k] / p[355][k]); p[376] = S(k => p[162] * p[358][k] - p[162] * p[375][k]); p[377] = p[162] * p[144];
    p[378] = S(k => p[162] * p[356][k] / p[355][k]); p[379] = S(k => p[334] + p[377] * p[358][k] - p[377]); p[380] = S(k => p[376][k] / p[144]);
    p[381] = S(k => p[380][k] + p[378][k] * p[345][k] - p[379][k]); p[382] = S(k => p[374][k] + p[378][k]);
    p[383] = S(k => p[370][k] * p[374][k] / p[382][k]); p[384] = S(k => 1 + p[370][k] * p[383][k]); p[385] = S(k => p[329][k] / p[373][k]);
    p[386] = S(k => p[385][k] - p[378][k] * p[384][k]); p[387] = S(k => 1 / p[380][k]); p[388] = S(k => (p[345][k] + p[354][k] * p[383][k]) / p[386][k]);
    p[389] = S(k => p[388][k] - p[387][k]); const sg = k => k === 0 ? 1 : -1;
    p[390] = S(k => p[389][k] + p[340] + sg(k) * p[338] * p[345][k]); p[391] = S(k => (1 + p[390][k] * p[380][k]) / p[390][k]);
    p[392] = S(k => (1 + sg(k) * p[338] * p[386][k]) / p[383][k]); p[393] = S(k => p[353][k] / p[392][k]);
    p[394] = S(k => (p[393][k] - p[345][k] * p[351][k]) / p[346][k]); p[395] = S(k => p[394][k] + p[145]); p[396] = S(k => p[380][k] / p[395][k]);
    p[397] = S(k => (p[347][k] + p[351][k] * p[394][k]) / p[353][k]); p[398] = S(k => p[396][k] * p[397][k] / p[391][k]);
    p[399] = S(k => p[383][k] * p[338] / p[392][k]); p[400] = S(k => 1 - sg(k) * p[382][k] * p[399][k]);
    // Лист IX
    p[401] = S(k => p[346][k] * p[382][k] / p[400][k]); p[402] = S(k => p[353][k] * p[401][k]); p[403] = S(k => p[353][k] * p[396][k]); p[404] = S(k => p[353][k] * p[376][k]);
    p[405] = S(k => (p[345][k] * p[379][k] + sg(k) * p[331][k]) / p[346][k]); p[406] = S(k => p[7] ** 2 * p[307][k]);
    p[407] = S(k => p[401][k] - p[405][k] - p[346][k] * p[378][k]); p[408] = S(k => p[407][k] - p[406][k]); p[409] = S(k => round01(p[408][k]));
    p[410] = S(k => p[409][k] - p[407][k]); p[411] = S(k => (p[401][k] - p[403][k] * p[354][k]) / p[403][k]); p[412] = S(k => (p[411][k] + p[398][k]) / p[402][k]);
    const dE = inp.dEpin || [0, 0];
    p[413] = S(k => dE[k] || 0); p[414] = S(k => p[5] + p[413][k] + p[247]);
    p[415] = S(k => p[396][k] * (p[402][k] + p[414][k]) / p[401][k]); p[416] = S(k => (p[415][k] - p[351][k] * p[414][k]) / p[353][k]);
    p[417] = S(k => p[381][k] - p[145] * p[416][k]); p[418] = S(k => -p[417][k] / p[142]);
    p[419] = S(k => (p[410][k] + p[411][k] * p[415][k]) / p[415][k]); p[420] = S(k => D(Math.atan(p[419][k]))); p[421] = S(k => Math.cos(R(p[420][k])));
    p[422] = S(k => p[415][k] / p[421][k]); p[423] = S(k => p[352][k] + p[420][k]); p[424] = S(k => Math.sin(R(p[423][k]))); p[425] = S(k => Math.cos(R(p[423][k])));
    const sH = handP === "L" ? 1 : -1;
    p[426] = S(k => sH * (p[398][k] + p[412][k] * p[414][k]));
    p[427] = S(k => p[415][k] / p[404][k]);
    p[428] = S(k => p[422][k] / p[264]); p[428].forEach((v, k) => { if (v > 1) warn.push(`Радиальная установка шестерни (${k ? "вып." : "вогн."}) больше эксцентриситета люльки.`) });
    p[429] = S(k => D(Math.asin(Math.min(1, p[428][k])))); p[430] = S(k => 2 * p[429][k]);
    p[431] = S(k => norm(0.5 * p[430][k] + (handP === "L" ? p[423][k] : -p[423][k])));
    // модификатор
    p[432] = S(k => p[426][k] >= 0 ? 0 : 180); p[433] = S(k => Math.sin(R(p[432][k]))); p[434] = S(k => Math.cos(R(p[432][k])));
    p[435] = S(k => p[426][k] / p[434][k]);
    p[436] = S(k => { const v = Math.abs(p[435][k]); let best = T7[0]; T7.forEach(r => { if (Math.abs(r[0] - v) < Math.abs(best[0] - v)) best = r }); return best[1] });
    p[437] = S(k => M.Cm1 * p[436][k]); p[438] = S(k => p[433][k] * p[435][k]); p[439] = S(k => p[437][k] - 2 * p[438][k]);
    p[440] = S(k => (p[438][k] / p[439][k]) ** 2); p[441] = S(k => p[440][k] ** 2); p[442] = S(k => 1 + p[440][k] + 2 * p[441][k]);
    p[443] = S(k => M.Rl * p[435][k] / p[437][k]); p[444] = S(k => Math.abs(p[443][k] * p[442][k] * p[437][k] / p[439][k]));
    p[445] = S(k => p[437][k] * p[444][k] / M.Rl); p[446] = S(k => 1 + p[433][k] * p[445][k]);
    p[444].forEach((v, k) => { if (v > M.EmMax) warn.push(`Эксцентриситет ролика модификатора (${k ? "вып." : "вогн."}) Eм = ${v.toFixed(2)} больше наибольшего ${M.EmMax} мм — модификатор не обеспечит, нужна корректировка по пятну.`) });
    p.psiM = S(k => 2 * D(Math.asin(Math.min(1, p[444][k] / M.EmMax))));
    p.discM = S(k => norm(-p[432][k] + (270 - p.psiM[k] / 2)));
    p[447] = S(k => p[1] * p[427][k] * p[446][k]);
    // черновое шестерни (лист XI)
    p[512] = p[87] + p[95]; p[513] = p[246] / p[238]; p[514] = p[343]; p[515] = S(k => D(Math.asin(p[514][k]))); p[516] = S(k => Math.cos(R(p[515][k])));
    p[517] = [nearest(Math.abs(p[515][0]), T5.inn), nearest(Math.abs(p[515][1]), T5.out)];
    p[518] = S(k => p[171] * p[253][k] / p[516][k]); p[519] = (p[518][0] + p[518][1]) / 2; p[520] = D(Math.acos(p[519])); p[521] = Math.tan(R(p[520]));
    p[522] = p[39] - p[237]; p[523] = Math.sin(R(p[522])); p[524] = Math.cos(R(p[522])); p[525] = Math.tan(R(p[522]));
    p[526] = p[520] - p[522]; p[527] = Math.sin(R(p[526])); p[528] = Math.cos(R(p[526]));
    p[529] = p[513] - p[7] * p[527]; p[530] = p[7] * p[528] / p[529]; p[531] = D(Math.atan(p[530])); p[532] = Math.cos(R(p[531]));
    p[533] = p[529] / p[532]; p[534] = p[531] + p[522]; p[535] = p[513] * p[523];
    p[536] = p[513] * p[524] / p[162]; p[537] = 1 + p[521] * p[525]; p[538] = p[536] * p[537]; p[539] = p[1] * p[538];
    p[542] = p[162] / p[142]; p[543] = p[542] - p[513] * p[524]; p[544] = p[543] * p[145] - p[512]; p[545] = -p[544] / p[142];
    p[546] = p[533] / p[264]; p[547] = D(Math.asin(Math.min(1, p[546]))); p[548] = 2 * p[547];
    p[549] = norm(0.5 * p[548] + (handP === "L" ? p[534] : -p[534]));
    // проверки по станку
    const mn = 2 * p[12] * p[45] / p[2];   // нормальный модуль в средней точке колеса
    p.mn = mn; p.msk = msk; p.i = i;
    if (E > 0 && E > M.offDown + 1e-9) warn.push(`Гипоидное смещение ${E} мм больше наибольшего для станка ${inp.machine || "525"} (вниз ${M.offDown} мм).`);
    if (Math.abs(p[247]) > Math.max(M.offDown, M.offUp)) warn.push(`Гипоидное смещение при нарезании колеса ${p[247].toFixed(2)} мм вне хода станка.`);
    p[414].forEach((v, k) => { if (Math.abs(v) > Math.max(M.offDown, M.offUp)) warn.push(`Гипоидное смещение шестерни ${v.toFixed(2)} мм вне хода станка.`) });
    if (Dk > M.dmax) warn.push(`Диаметр колеса ${Dk} больше наибольшего для станка (${M.dmax} мм).`);
    if (mn < M.mnMin - 1e-9 || mn > M.mnMax + 1e-9) warn.push(`Нормальный модуль в средней точке ${mn.toFixed(2)} вне пределов станка (${M.mnMin}…${M.mnMax}).`);
    const zmin = i <= 1 ? 20 : i <= 1.5 ? 18 : i <= 2 ? 16 : i <= 2.5 ? 14 : i <= 3 ? 12 : i <= 4 ? 9 : i <= 5 ? 7 : 6;
    if (zsh < zmin) warn.push(`Число зубьев шестерни ${zsh} меньше наименьшего (${zmin}) для передаточного ${i.toFixed(2)}.`);
    if (zsh + zk < 40) warn.push("Сумма чисел зубьев меньше 40.");
    const Emax = i >= 2.5 ? 0.2 * Dk : (0.2 - 0.045 * (2.5 - i) ** 2) * Dk;
    if (E > Emax) warn.push(`Смещение E = ${E} больше допустимого ${Emax.toFixed(1)} мм для этой пары.`);
    const Fmax = Math.min(Dk / (6 * p[11]), 10 * msk);
    if (F > Fmax + 1e-9) warn.push(`Ширина венца ${F} больше допустимой ${Fmax.toFixed(1)} мм.`);
    // односторонние головки по табл. 6
    p.headsT6 = S(k => { const d = 2 * p[409][k], key = k === 0 ? "out" : "inn"; const r = T6.find(t => d >= t[key][0] - 1e-9 && d <= t[key][1] + 1e-9); return r ? `${r.nom}, корпус ${r.base}` : null });
    p.headsT6.forEach((v, k) => { if (!v) warn.push(`Образующий диаметр ${k ? "внутренней" : "наружной"} головки ${(2 * p[409][k]).toFixed(1)} мм вне пределов нормали ВН451-55 (табл. 6) — нужна специальная головка.`) });
    return { p, warn, note, machine: inp.machine || "525", M, hand1: handP, hand2: handW }
  }
  // поправки по пятну (лист X): kind ∈ diag1, diag2, narrow, longer, toHeel, toTop; масштаб s (1 = табличная величина)
  function spotCorr(res, kind, s) {
    const p = res.p, sH = res.hand1 === "L" ? 1 : -1, out = [];
    p[451] = [0, 1].map(k => Math.sqrt(p[264] ** 2 - p[422][k] ** 2));
    const zsh = p[1];
    p[450] = [0, 1].map(k => 20 * p[447][k] / zsh);
    p[452] = p[450].map(v => v * 60);
    for (let k = 0; k < 2; k++) {
      const c = { dA: 0, dB: 0, deps: 0, dQ: 0, dzc: 0, dE: 0, dr: 0 };
      if (kind === "diag1") {          // способ I
        const dth = (k === 0 ? -60 : 60) * s, a455 = p[353][k] * p[452][k], a456 = p[415][k] * dth / a455;
        c.dzc = p[447][k] * dth / a455;   /* в формуле п.457 напечатано (452), числа примера — по (455) */
        c.dA = a456 / p[144]; c.dB = -c.dA * p[142];
        const a460 = p[425][k] * a456; c.deps = 6880 * a460 / p[451][k]; const a462 = 3440 * a456 / p[422][k]; c.dQ = 0.5 * c.deps + sH * p[424][k] * a462;
      } else if (kind === "diag2") {   // способ II
        c.dE = (k === 0 ? 2.5 : -2.5) * s; c.dA = -p[354][k] * c.dE / p[144]; c.dB = -p[142] * c.dA;
        const a469 = p[424][k] - p[425][k] * p[354][k]; c.deps = 6880 * c.dE * a469 / p[451][k];
        const a471 = (p[354][k] * p[424][k] + p[425][k]) / p[422][k]; c.dQ = 0.5 * c.deps - sH * 3440 * c.dE * a471;
      } else if (kind === "narrow") {
        c.dE = (k === 0 ? -2.5 : 2.5) * s; const a476 = p[351][k] * c.dE / p[415][k]; c.dzc = a476 * p[447][k];
        c.deps = 6880 * c.dE * p[424][k] / p[451][k]; c.dQ = 0.5 * c.deps - sH * 3440 * c.dE * p[425][k] / p[422][k];
      } else if (kind === "longer") {
        c.dr = (k === 0 ? -1 : 1) * s; const a490 = 0.5 * p[419][k] * p[421][k];
        c.deps = 6880 * c.dr * a490 / p[451][k]; c.dQ = 0.5 * c.deps - sH * 1720 * c.dr * p[421][k] / p[422][k];
      } else if (kind === "toHeel") {
        const dV = (k === 0 ? -1 : 1) * s; c.deps = 6880 * dV * p[424][k] / p[451][k]; const a497 = p[451][k] * c.deps / p[422][k];
        c.dQ = 0.5 * c.deps + sH * p[419][k] * a497 / 2; c.dV = dV;
      } else if (kind === "toTop") {
        c.dzc = (k === 0 ? 0.25 : -0.25) * s;
      }
      out.push(c)
    }
    return out
  }
  return { hypoid, spotCorr, MACH, T6, T7 };
})();


/* ===== STZ beta.15: гипоидная передача + рабочие чертежи конических и гипоидных колёс ===== */
const HYPMACH=['525','528С','5А27С1'];
let _hypKey='',_hypRes=null;
function hypRes(p){const k=JSON.stringify([p.m,p.z1,p.z2,p.b,p.E,p.bsh,p.hand,p.asum,p.mach,p.rhd]);if(k===_hypKey&&_hypRes)return _hypRes;
 const asum=[0,42.5,45,38][+p.asum||0]||0;
 _hypRes=HYPOID.hypoid({machine:HYPMACH[+p.mach||0],z1:+p.z1,z2:+p.z2,F:+p.b,E:+p.E,Dk:+p.m*p.z2,beta1:+p.bsh||50,hand1:+p.hand===1?'L':'R',alphaSum:asum,ru:[0,76.2,95.25,114.3,152.4][+p.rhd||0]||0});_hypKey=k;return _hypRes;}
function hypToBevel(p){let bm=35;try{const r=hypRes(p).p;bm=(r[42]+r[44])/2;}catch(e){}return {...p,alpha:20,betam:Math.min(40,bm)};}
/* геометрия зубчатого венца для чертежа: возвращает контур в координатах (s — вдоль своей оси от оси сопряжённого колеса / от вершины, r — радиус) */
function coneGeo(c,gi){const p=c.p,dg=v=>v*D2R;
 if(c.type==='hypoid'){const H=hypRes(p),q=H.p,T=(a,Z,s)=>(s+Z)*Math.tan(dg(a));
  if(gi===1){// колесо: s от оси шестерни вдоль оси колеса
   const phi=q[48],pa=-q[71],tipA=-q[110],rootA=-q[111];const u=[Math.cos(dg(phi)),Math.sin(dg(phi))],n=[-Math.sin(dg(phi)),Math.cos(dg(phi))];
   const Ph=[pa+q[73]*u[0],q[73]*u[1]],Pt=[pa+q[150]*u[0],q[150]*u[1]];
   const hit=(P,apex,ang)=>{const t=Math.tan(dg(ang));const k=((P[0]-apex)*t-P[1])/(n[1]-n[0]*t);return [P[0]+k*n[0],P[1]+k*n[1]];};
   return {kind:'wheel',z:p.z2,tipH:hit(Ph,tipA,q[98]),tipT:hit(Pt,tipA,q[98]),rootH:hit(Ph,rootA,q[101]),rootT:hit(Pt,rootA,q[101]),Ph,Pt,pa,da:q[105],d:q[6],delta:phi,deltaA:q[98],deltaF:q[101],hae:q[93],he:q[96],beta:q[44],hand:H.hand2,mate:p.z1,
    X:q[107],Re:q[73],Rm:q[72],F:p.b,thick:q[224],hthick:q[87],head:q[7],hyp:true,mod:+p.m};}
  const tipA=-q[121],rootA=-q[141],phi=q[36],n=[-Math.sin(dg(phi)),Math.cos(dg(phi))];
  const tipH=[q[131],q[135]/2],tipT=[q[133],T(q[117],q[121],q[133])];
  const hit=(P,apex,ang)=>{const t=Math.tan(dg(ang));const k=((P[0]-apex)*t-P[1])/(n[1]-n[0]*t);return [P[0]+k*n[0],P[1]+k*n[1]];};
  return {kind:'pinion',z:p.z1,tipH,tipT,rootH:hit(tipH,rootA,q[143]),rootT:hit(tipT,rootA,q[143]),pa:null,da:q[135],d:2*q[17],delta:phi,deltaA:q[117],deltaF:q[143],hae:q[221]-(q[96]-q[93]),he:q[221],beta:q[42],hand:H.hand1,mate:p.z2,
   X:q[131],Re:null,Rm:null,F:Math.hypot(q[131]-q[133],tipH[1]-tipT[1]),thick:q[223],hthick:q[226],head:q[7],hyp:true,mod:+p.m};}
 // коническая (оси пересекаются): s от вершины
 if(typeof g19On==='function'&&g19On(p)){const g=g19Calc(p),G0=gi===1?g.W:g.P,dl=G0.delta*D2R,u=[Math.cos(dl),Math.sin(dl)],n=[-Math.sin(dl),Math.cos(dl)];
  const Re=g.Re,b=+p.b,M=[g.Rm*u[0],g.Rm*u[1]],Ph=[Re*u[0],Re*u[1]],Pt=[(Re-b)*u[0],(Re-b)*u[1]];
  const line=(h,ang)=>({p:[M[0]+h*n[0],M[1]+h*n[1]],a:ang*D2R});
  const hitL=(P,L)=>{const d=[Math.cos(L.a),Math.sin(L.a)];const den=n[0]*d[1]-n[1]*d[0];const t=((L.p[0]-P[0])*d[1]-(L.p[1]-P[1])*d[0])/den;return [P[0]+t*n[0],P[1]+t*n[1]];};
  const Lt=line(G0.hamTip,G0.deltaA),Lf=line(-G0.hfm,G0.deltaF);
  return {kind:gi?'wheel':'pinion',z:G0.z,tipH:hitL(Ph,Lt),tipT:hitL(Pt,Lt),rootH:hitL(Ph,Lf),rootT:hitL(Pt,Lf),Ph,Pt,pa:0,da:G0.dae,d:G0.de,delta:G0.delta,deltaA:G0.deltaA,deltaF:G0.deltaF,hae:G0.hae,he:G0.he,beta:g.beta,
   hand:G0.hand,mate:gi?p.z1:p.z2,X:null,Re,Rm:g.Rm,F:b,thick:G0.sc,hthick:G0.hc,head:g.r0,hyp:false,mod:+p.m,g19:g,gi};}
 const d1r=Math.atan(p.z1/p.z2),dl=gi===1?Math.PI/2-d1r:d1r,Re=0.5*p.m*Math.hypot(p.z1,p.z2),b=Math.min(p.b,Re*0.45),m=p.m,ha=m,hf=1.2*m;
 const tA=Math.atan(ha/Re),tF=Math.atan(hf/Re),u=[Math.cos(dl),Math.sin(dl)],n=[-Math.sin(dl),Math.cos(dl)],Ph=[Re*u[0],Re*u[1]],Pt=[(Re-b)*u[0],(Re-b)*u[1]];
 const hit=(P,ang)=>{const t=Math.tan(ang);const k=(P[0]*t-P[1])/(n[1]-n[0]*t);return [P[0]+k*n[0],P[1]+k*n[1]];};
 const tipH=[Ph[0]+ha*n[0],Ph[1]+ha*n[1]],rootH=[Ph[0]-hf*n[0],Ph[1]-hf*n[1]];
 const z=gi?p.z2:p.z1,de=m*z,s=Math.PI*m/2,dve=de/Math.cos(dl);
 return {kind:gi?'wheel':'pinion',z,tipH,rootH,tipT:hit(Pt,dl+tA),rootT:hit(Pt,dl-tF),Ph,Pt,pa:0,da:de+2*ha*Math.cos(dl),d:de,delta:dl/D2R,deltaA:(dl+tA)/D2R,deltaF:(dl-tF)/D2R,hae:ha,he:ha+hf,beta:+p.betam||0,
  hand:+p.betam?(gi?(+p.hand===1?'правое':'левое'):(+p.hand===1?'левое':'правое')):'—',mate:gi?p.z1:p.z2,X:null,Re,Rm:Re-b/2,F:b,thick:s-s**3/(6*dve*dve),hthick:ha+s*s*Math.cos(dl)/(4*de),head:null,hyp:false,mod:m};}
/* Конструкция конической заготовки (условная) в координатах (s — вдоль оси от вершины, r — радиус): общая для чертежа и токарной заготовки */
function coneBody(c,gi,std,G){const p=c.p;
 const bores=(()=>{try{return boresList(c.type==='hypoid'?{...c,type:'bevel',p:hypToBevel(p)}:c,std);}catch(e){return [];}})(),bi=(bores.find(x=>x.role===gi+1)||{}).bi;
 const tH=G.tipH,tT=G.tipT,rH=G.rootH,rT=G.rootT;let body,mount,shaft=null,bore=0;
 if(G.kind==='wheel'){
  const rRootMin=Math.min(rH[1],rT[1]);bore=bi&&bi.d?bi.d:Math.round(0.55*rRootMin*2/5)*5;bore=Math.min(bore,2*rRootMin-12);
  mount=Math.max(tH[0],rH[0])+Math.max(8,0.5*G.F);const sF=Math.min(rT[0],tT[0])+(G.hyp?0:0)-0.5;const land=tH[0]+0.35*(mount-tH[0]);
  body=[rH,[land,tH[1]],[mount-1.5,tH[1]],[mount,tH[1]-1.5],[mount,bore/2+1],[mount-1,bore/2],[Math.min(sF,rT[0])+1,bore/2],[Math.min(sF,rT[0]),bore/2+1],[Math.min(sF,rT[0]),rT[1]-0.5],rT];
 }else{
  const rr=Math.min(rT[1],rH[1]);mount=Math.max(tH[0],rH[0])+Math.max(4,0.12*G.F);
  if(+p.key===2){const dj=Math.max(10,Math.floor(1.6*rr)),L1=Math.max(20,1.2*G.F),L2=Math.max(20,1.0*G.F);shaft={dj,L1,L2,d2:Math.max(8,Math.round(0.8*dj))};
   body=[rT,rH,[mount-1,tH[1]],[mount,tH[1]-1],[mount,dj/2],[mount+L1,dj/2],[mount+L1,shaft.d2/2],[mount+L1+L2-1,shaft.d2/2],[mount+L1+L2,shaft.d2/2-1],[mount+L1+L2,0],[tT[0],0],[tT[0],rT[1]]];}
  else{bore=bi&&bi.d?bi.d:Math.max(8,Math.round(1.1*rr/2)*2);const hubL=Math.max(10,0.8*G.F),Rh=Math.min(tH[1]-2,Math.max(bore/2+0.25*bore+3,rr));
   body=[rT,rH,[mount-1,tH[1]],[mount,tH[1]-1],[mount,Rh],[mount+hubL,Rh],[mount+hubL,bore/2],[tT[0],bore/2],[tT[0],rT[1]]];shaft={hub:hubL,Rh};}
 }
 return {body,mount,shaft,bore,bi};}
/* Токарная заготовка конического / гипоидного колеса: углы для точения, диаметры, осевые размеры, точки контура для УП и сортамент (круг ГОСТ 2590) */
const ROUND2590=[5,5.5,6,6.3,6.5,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,52,53,54,55,56,58,60,62,63,64,65,67,68,70,72,75,78,80,82,85,87,90,92,95,97,100,105,110,115,120,125,130,135,140,145,150,155,160,165,170,175,180,185,190,195,200,210,220,230,240,250,260,270,280,290,300,310,320,330,340,350,360,370,380,390,400];
const MAT_GRADE=['Сталь 45 ГОСТ 1050-2013','Сталь 40Х ГОСТ 4543-2016','Сталь 40Х ГОСТ 4543-2016','Сталь 20ХН3А ГОСТ 4543-2016','Полиамид 6 блочный (капролон)'];
function coneBlank(c,gi,std){const p=c.p,G=coneGeo(c,gi);if(!G||!G.tipH)return null;const B=coneBody(c,gi,std,G),R2=180/Math.PI;
 const tH=G.tipH,tT=G.tipT,rH=G.rootH,mount=B.mount;
 const aTip=Math.abs(Math.atan2(tH[1]-tT[1],tH[0]-tT[0]))*R2,aTipF=90-aTip;            // конус вершин: от оси / от торца
 const aBack=Math.abs(Math.atan2(rH[1]-tH[1],rH[0]-tH[0]))*R2,aBk=aBack>90?180-aBack:aBack,aBackF=90-aBk; // доп. конус
 const dZ=Math.abs(tH[0]-tT[0]),Lgen=Math.hypot(tH[0]-tT[0],tH[1]-tT[1]),z=s=>mount-s;
 const pts=[['носок, вершина зуба',2*tT[1],z(tT[0])],['пятка, вершина зуба d_ae',2*tH[1],z(tH[0])],['доп. конус, на ⌀ впадин',2*rH[1],z(rH[0])]];
 const xs=B.body.map(v=>v[0]).concat([tH[0],tT[0]]),rs=B.body.map(v=>v[1]).concat([tH[1],tT[1]]),Dmax=2*Math.max(...rs),Lp=Math.max(...xs)-Math.min(...xs);
 const mi=p.mat??1,plast=!!(MATS[mi]&&MATS[mi].plast),rho=(MATS[mi]&&MATS[mi].rho)||7850;
 const aD=Dmax<=30?3:Dmax<=80?4:Dmax<=120?5:Dmax<=180?6:Dmax<=260?8:10,aL=Dmax<=80?2:Dmax<=180?2.5:3;   // припуски на ⌀ и на каждый торец
 const Dbar=ROUND2590.find(d=>d>=Dmax+aD)||Math.ceil((Dmax+aD)/10)*10,Lcut=Math.ceil(Lp+2*aL),kerf=Dbar<=60?2:Dbar<=150?3:4;
 const mass=rho*Math.PI/4*Dbar*Dbar*Lcut*1e-9,forge=!plast&&Dbar>=150;
 return {G,B,kind:G.kind,shaft:!!(B.shaft&&B.shaft.dj),aTip,aTipF,aBack:aBk,aBackF,dae:2*tH[1],dai:2*tT[1],dZ,Lgen,C:mount-tH[0],mount,pts,Dmax,Lp,aD,aL,Dbar,Lcut,kerf,mass,forge,
  grade:MAT_GRADE[mi]||'',bar:plast?('стержень ⌀'+Dbar):('круг '+Dbar+' ГОСТ 2590-2006')};}
function dmsA(v){let t=Math.round(v*60),d=Math.floor(t/60),m=t%60;return d+'°'+String(m).padStart(2,'0')+'′';}
function blankLines(c,std,G,GL,tolDa){[0,1].forEach(gi=>{let b;try{b=coneBlank(c,gi,std);}catch(e){b=null;}if(!b)return;
 const nm=b.kind==='wheel'?'колеса':(b.shaft?'вала-шестерни':'шестерни'),T='Точение '+nm+': ',Z='Заготовка '+nm+': ',A=v=>dmsA(v)+' ('+fmt(v,3)+'°)',L=v=>fmt(v,2);
 G(T+'угол конуса вершин δa от оси',A(b.aTip),'');G(T+'угол конуса вершин δa от торца',A(b.aTipF),'');
 G(T+'доп. конус (торец зубьев) от оси',A(b.aBack),'');G(T+'доп. конус (торец зубьев) от торца',A(b.aBackF),'');
 G(T+'⌀ вершин на пятке d_ae',L(b.dae)+(tolDa?' '+tolDa:''),'мм');G(T+'⌀ вершин на носке',L(b.dai),'мм');
 G(T+'конус вершин по оси ΔZ',L(b.dZ),'мм');G(T+'конус вершин по образующей',L(b.Lgen),'мм');
 G(T+'базовый торец → вершина зуба на пятке C',L(b.C),'мм');G(T+'базовый торец → '+(b.G.hyp?'ось сопряжённого колеса':'вершина конуса'),L(b.mount),'мм');
 G(T+'точки УП: Z — от базового торца','X = ⌀','');b.pts.forEach(q=>G(T+'точка УП — '+q[0],'X'+fmt(q[1],3)+' Z'+fmt(q[2],3),''));
 G(Z+'деталь по контуру ⌀ × длина','⌀'+L(b.Dmax)+' × '+L(b.Lp),'мм');
 G(Z+'сортамент',b.bar.replace(' ГОСТ 2590-2006','')+(b.bar.indexOf('ГОСТ')>0?' ГОСТ 2590':''),'');
 G(Z+'длина отрезки (торцы 2 × '+fmt(b.aL,1)+', ⌀ +'+b.aD+')',b.Lcut+' + пропил '+b.kerf,'мм');
 {const gm=b.grade.match(/^(.*?) (ГОСТ .*)$/);G(Z+'материал'+(gm?' ('+gm[2]+')':''),gm?gm[1]:'капролон','');}G(Z+'масса заготовки',fmt(b.mass,b.mass<1?2:1),'кг');
 if(b.forge)G(Z+'вариант при ⌀ от 150 — поковка-диск (ГОСТ 7505), меньше стружки','⌀'+Math.ceil(b.Dmax+8)+' × '+Math.ceil(b.Lp+6),'мм');
 if(b.shaft)G(Z+'хвостовик условный — длину берите по чертежу вала','+ '+fmt(2*b.aL,0)+' на торцы','мм');
 });}
function drawingConeSVG(c,gi,std){
 const p=c.p,G=coneGeo(c,gi),H=c.type==='hypoid'?hypRes(p):null,q=H?H.p:null,o=[],W1=0.5,W2=0.25,FS="font-family:'GOST type B','ISOCPEUR','Arial Narrow',Arial,sans-serif;font-style:italic";
 const ln=(x1,y1,x2,y2,w,da)=>o.push(`<line x1="${x1.toFixed(2)}" y1="${y1.toFixed(2)}" x2="${x2.toFixed(2)}" y2="${y2.toFixed(2)}" stroke="#000" stroke-width="${w||W2}"${da?` stroke-dasharray="${da}"`:''}/>`);
 const tx=(x,y,t,sz,anc,rot)=>o.push(`<text x="${x.toFixed(2)}" y="${y.toFixed(2)}" font-size="${sz||3.5}" text-anchor="${anc||'start'}"${rot?` transform="rotate(${rot} ${x.toFixed(2)} ${y.toFixed(2)})"`:''} style="${FS}">${esc(t)}</text>`);
 const pg=(pts,fill,w)=>o.push(`<polygon points="${pts.map(v=>v[0].toFixed(2)+','+v[1].toFixed(2)).join(' ')}" fill="${fill}" stroke="#000" stroke-width="${w||W1}" stroke-linejoin="round"/>`);
 const pln=(pts,w)=>o.push(`<polyline points="${pts.map(v=>v[0].toFixed(2)+','+v[1].toFixed(2)).join(' ')}" fill="none" stroke="#000" stroke-width="${w||W1}" stroke-linejoin="round"/>`);
 const arr=(x,y,ang)=>{const L=2.5,Wd=0.55,cs=Math.cos(ang),sn=Math.sin(ang);o.push(`<polygon points="${x.toFixed(2)},${y.toFixed(2)} ${(x-L*cs+Wd*sn).toFixed(2)},${(y-L*sn-Wd*cs).toFixed(2)} ${(x-L*cs-Wd*sn).toFixed(2)},${(y-L*sn+Wd*cs).toFixed(2)}" fill="#000"/>`);};
 const dimV=(x,y1,y2,xr1,xr2,t)=>{const dir=x<xr1?-1:1;ln(xr1,y1,x+dir*1.5,y1);ln(xr2,y2,x+dir*1.5,y2);ln(x,y1,x,y2);arr(x,y1,-Math.PI/2);arr(x,y2,Math.PI/2);tx(x-1,(y1+y2)/2,t,3.5,'middle',-90);};
 const dimH=(y,x1,x2,yr1,yr2,t)=>{const dir=y<yr1?-1:1;ln(x1,yr1,x1,y+dir*1.5);ln(x2,yr2,x2,y+dir*1.5);ln(x1,y,x2,y);arr(x1,y,Math.PI);arr(x2,y,0);tx((x1+x2)/2,y-1,t,3.5,'middle');};
 const lead=(x,y,x2,y2,t,sz)=>{ln(x,y,x2,y2);arr(x,y,Math.atan2(y-y2,x-x2));const w=(t.length*(sz||3.2)*0.52);ln(x2,y2,x2+(x2>=x?w:-w),y2);tx(x2>=x?x2+0.8:x2-w+0.8,y2-0.8,t,sz||3.2);};
 const deg=v=>{let d=Math.floor(v+1e-9),mi=Math.round((v-d)*60);if(mi===60){d++;mi=0;}return d+'°'+String(mi).padStart(2,'0')+'′';};
 o.push('<defs><pattern id="hz" width="2.2" height="2.2" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><line x1="0" y1="0" x2="0" y2="2.2" stroke="#000" stroke-width="0.2"/></pattern></defs>');
 o.push('<rect x="0" y="0" width="420" height="297" fill="#fff"/>');o.push(`<rect x="20" y="5" width="395" height="287" fill="none" stroke="#000" stroke-width="${W1+0.2}"/>`);
 // заготовка (условная конструкция) в координатах (s,r)
 const tH=G.tipH,tT=G.tipT,rH=G.rootH,rT=G.rootT,{body,mount,shaft,bore,bi}=coneBody(c,gi,std,G);
 const xs=body.map(v=>v[0]).concat([tH[0],tT[0]]),rmax=Math.max(tH[1],tT[1]),smin=Math.min(...xs),smax=Math.max(...xs),Lx=smax-smin;
 const SC=[[2,1],[1,1],[1,1.5],[1,2],[1,2.5],[1,4],[1,5]].map(([a,b])=>({k:a/b,t:a+':'+b})),s0=SC.find(v=>2*rmax*v.k<=170&&Lx*v.k<=150)||SC[SC.length-1],k=s0.k;
 const Y0=125,XC=G.kind==='wheel'?120:140,X=s=>XC+(s-(smin+smax)/2)*k,YU=r=>Y0-r*k,YL=r=>Y0+r*k;
 const up=body.map(v=>[X(v[0]),YU(v[1])]),lo=body.map(v=>[X(v[0]),YL(v[1])]);
 pg(up,'url(#hz)');pg(lo,G.kind==='wheel'?'url(#hz)':'none');
 // зубчатый венец: контур зуба (не штрихуется)
 const tooth=[rT,tT,tH,rH];pg(tooth.map(v=>[X(v[0]),YU(v[1])]),'#fff');pg(tooth.map(v=>[X(v[0]),YL(v[1])]),'#fff');
 ln(X(rT[0]),YU(rT[1]),X(rH[0]),YU(rH[1]),W1);ln(X(rT[0]),YL(rT[1]),X(rH[0]),YL(rH[1]),W1);
 const DD='7,1.5,1,1.5';
 if(G.Ph&&G.Pt){for(const Yf of [YU,YL]){const a=G.Pt,b=G.Ph,ex=3/k;const ux=(b[0]-a[0]),uy=(b[1]-a[1]),L=Math.hypot(ux,uy);ln(X(a[0]-ux/L*ex),Yf(a[1]-uy/L*ex),X(b[0]+ux/L*ex),Yf(b[1]+uy/L*ex),W2,DD);}}
 ln(X(smin)-8,Y0,X(smax)+8,Y0,W2,DD);
 if(G.kind==='wheel'&&bore)ln(X(Math.min(rT[0],tT[0])),YL(bore/2+1),X(mount),YL(bore/2+1),W1);
 // размеры
 dimV(X(smin)-12,YU(tH[1]),YL(tH[1]),X(tH[0]),X(tH[0]),'⌀'+fmt(G.da,2)+' h11');
 if(G.Ph)dimV(X(smin)-22,YU(G.Ph[1]),YL(G.Ph[1]),X(G.Ph[0]),X(G.Ph[0]),'⌀'+fmt(2*G.Ph[1],2)+'*');
 if(bore)dimV(X(smax)+12,YU(bore/2),YL(bore/2),X(mount),X(mount),'⌀'+fmt(bore,0)+' H7');
 dimH(YL(rmax)+9,X(tH[0]),X(mount),YL(tH[1]),YL(tH[1]),fmt(mount-tH[0],2));
 {const lab=fmt(mount,2)+(G.hyp?(G.kind==='wheel'?' (монтажное, от оси шестерни)':' (монтажное, от оси колеса)'):' (базовое, от вершины)');
  if(X(0)>26)dimH(YL(rmax)+18,X(0),X(mount),Y0,YL(tH[1]),lab);
  else{const y=YL(rmax)+18;ln(X(mount),YL(tH[1]),X(mount),y+1.5);ln(26,y,X(mount),y);arr(X(mount),y,0);tx(28,y-1,'← до '+(G.hyp?(G.kind==='wheel'?'оси шестерни':'оси колеса'):'вершины конуса')+': '+lab,3.3);}}
 if(shaft&&shaft.dj){const xm=X(mount+shaft.L1/2);dimV(xm,YU(shaft.dj/2),YL(shaft.dj/2),xm,xm,'⌀'+shaft.dj+' k6');const x2=X(mount+shaft.L1+shaft.L2/2);dimV(x2,YU(shaft.d2/2),YL(shaft.d2/2),x2,x2,'⌀'+shaft.d2);}
 const mid=(a,b)=>[(a[0]+b[0])/2,(a[1]+b[1])/2],mt=mid(tT,tH),mr=mid(rT,rH);
 lead(X(mt[0]),YU(mt[1]),X(smin)-4,YU(rmax)-14,'δa = '+deg(G.deltaA)+' (конус вершин)');
 lead(X(mr[0]),YU(mr[1]),X(smin)-4,YU(rmax)-7,'δf = '+deg(G.deltaF)+' (конус впадин)');
 tx(X(smin)-30,YU(rmax)-21,'δ = '+deg(G.delta)+' (делительный конус)',3.2);
 if(G.hyp)tx(X(smin)-30,YU(rmax)-27,'Гипоидное смещение E = '+fmt(+p.E,2)+' мм (ось шестерни '+(+p.E>=0?'ниже':'выше')+' оси колеса)',3.2);
 // таблица параметров ГОСТ 2.405-75
 let TR;if(G.g19){const g=G.g19,sg=G.gi?-1:1;TR=[['Модуль нормальный средний','mn',fmt(g.mn,3)],['Число зубьев','z',String(G.z)],['Тип зуба','—','круговой'],['Осевая форма зуба','—',['','I','II','III'][g.form]],['Средний угол наклона зуба','βn',deg(g.beta)],['Направление линии зуба','—',G.hand==='L'?'левое':'правое'],
  ['Исходный контур','—','ГОСТ 16202-81'],['Коэффициент смещения','xn',fmt(sg*g.xn1,3)],['Коэф. изменения толщины зуба','xτ',fmt(sg*g.xt1,3)],['Угол делительного конуса','δ',deg(G.delta)],['Степень точности','—','7-С ГОСТ 1758-81'],
  ['Постоянная хорда (сред. сечение)','s̄c',fmt(G.thick,3)],['Высота до постоянной хорды','h̄c',fmt(G.hthick,3)],['Межосевой угол','Σ','90°'],['Внешний окружной модуль','mte',fmt(G.mod,5)],['Угол конуса впадин','δf',deg(G.deltaF)],
  ['Внешнее конусное расстояние','Re',fmt(g.Re,2)],['Среднее конусное расстояние','R',fmt(g.Rm,3)],['Средний делительный диаметр','d',fmt((G.gi?g.W:g.P).d,3)],['Внешняя высота зуба','he',fmt(G.he,3)],['Номинальный диаметр головки','d0',fmt(g.d0,1)]];
  if(g.form===2)TR.push(['Развод резцов','W',fmt(G.gi?g.W2:g.W1,2)]);TR.push(['Число зубьев сопряжённого колеса','z',String(G.mate)]);}else{
 TR=[[G.hyp?'Торцовый модуль колеса':'Внешний окружной модуль',G.hyp?'ms':'mte',fmt(G.mod,3)],['Число зубьев','z',String(G.z)],['Тип зуба','—',G.hyp?'круговой, гипоидный':(G.beta?'круговой':'прямой')],
  [G.hyp?'Угол спирали в середине венца':'Средний угол наклона зуба','βm',deg(G.beta)],['Направление линии зуба','—',G.hand==='L'?'левое':G.hand==='R'?'правое':G.hand],
  ['Исходный контур','—',G.hyp?'по наладке СЗТЗС':(G.beta?'ГОСТ 16202-81':'ГОСТ 13754-81')],[G.hyp?'Сумма углов давления':'Угол профиля',G.hyp?'αΣ':'α',G.hyp?deg(q[78]):fmt(+p.alpha||20,0)+'°'],
  ['Внешняя высота головки','hae',fmt(G.hae,2)],['Внешняя высота зуба','he',fmt(G.he,2)],['Угол делительного конуса','δ',deg(G.delta)],['Угол конуса впадин','δf',deg(G.deltaF)]];
 if(G.Re)TR.push(['Внешнее конусное расстояние','Re',fmt(G.Re,2)]);if(G.Rm)TR.push(['Среднее конусное расстояние','R',fmt(G.Rm,2)]);
 TR.push(['Ширина венца','b',fmt(G.F,G.F%1?2:0)]);if(G.head)TR.push(['Номинальный диаметр резцовой головки','d0',fmt(2*G.head,1)]);
 if(G.hyp&&G.kind==='pinion')TR.push(['Головки: вогнутая / выпуклая','—','⌀'+fmt(2*q[409][0],1)+' / ⌀'+fmt(2*q[409][1],1)]);
 TR.push(['Степень точности','—','7-B ГОСТ 1758-81'],[G.hyp?'Толщина по зубомеру (сред. точка)':'Толщина зуба по хорде (внешн.)','s̄',fmt(G.thick,2)],['Высота до хорды','h̄',fmt(G.hthick,2)],['Межосевой угол','Σ','90°']);
 if(G.hyp)TR.push(['Гипоидное смещение','E',fmt(+p.E,2)]);
 TR.push(['Сопряжённое колесо: число зубьев','z',String(G.mate)]);}
 const TX0=305,TY0=5,RH=Math.min(7,180/TR.length);o.push(`<rect x="${TX0}" y="${TY0}" width="110" height="${(RH*TR.length).toFixed(2)}" fill="none" stroke="#000" stroke-width="${W1}"/>`);
 ln(TX0+65,TY0,TX0+65,TY0+RH*TR.length,W1);ln(TX0+75,TY0,TX0+75,TY0+RH*TR.length,W1);
 TR.forEach((r,i)=>{const y=TY0+RH*i;if(i)ln(TX0,y,TX0+110,y,W2);tx(TX0+1.2,y+RH*0.68,r[0],2.7);tx(TX0+70,y+RH*0.68,r[1],2.9,'middle');tx(TX0+76.2,y+RH*0.68,r[2],2.8);});
 // технические требования
 const HARD=['200…240 HB','260…280 HB','45…50 HRC, закалка ТВЧ зубьев','Цементировать h 0,9…1,3 мм; зубья 58…63 HRC, сердцевина 30…42 HRC',''],MG=['Сталь 45 ГОСТ 1050-2013','Сталь 40Х ГОСТ 4543-2016','Сталь 40Х ГОСТ 4543-2016','Сталь 20ХН3А ГОСТ 4543-2016','Полиамид 6 блочный'];
 const mi=p.mat??1,tr=[];if(HARD[mi])tr.push(HARD[mi]+'.');tr.push('* Размер для справок.');
 tr.push(G.g19?'Нарезать в паре на станке 525 двусторонним способом (развод W) по карте «Зуборез 525»; притереть комплектно.':G.hyp?'Нарезать в паре на станке '+(HYPMACH[+p.mach||0])+' по карте наладки «Зуборез 525»; притереть комплектно; пятно контакта 0,4…0,6 b, посередине.':'Нарезать в паре, притереть комплектно; пятно контакта 0,5…0,7 b, ближе к носку.');
 tr.push('Комплект маркировать общим номером.','Неуказанные предельные отклонения: H14, h14, ±IT14/2.','Конструкция заготовки (ступица, отверстие, хвостовик) — условная, уточнить по узлу.');
 let ty=200;tr.forEach((t,i)=>{const wds=((i+1)+'. '+t).split(' ');let line='';wds.forEach(w=>{if((line+' '+w).length>70){tx(238,ty,line,3.1);ty+=4.6;line='   '+w;}else line=line?line+' '+w:w;});tx(238,ty,line,3.1);ty+=5.4;});
 // заготовка под токарную обработку (справочно)
 {let b=null;try{b=coneBlank(c,gi,std);}catch(e){}
  if(b){const x0=36,y0=234,RW=6,A=v=>dmsA(v)+' ('+fmt(v,3)+'°)',rows=[['Угол конуса вершин δa: от оси / от торца',A(b.aTip)+' / '+A(b.aTipF)],['Доп. конус (торец зубьев): от оси / от торца',A(b.aBack)+' / '+A(b.aBackF)],
    ['⌀ вершин: пятка d_ae / носок','⌀'+fmt(b.dae,2)+' h11 / ⌀'+fmt(b.dai,2)+'*'],['Конус вершин по оси ΔZ / базовый торец → пятка C',fmt(b.dZ,2)+' / '+fmt(b.C,2)],
    ['Точки для УП: X = ⌀, Z от базового торца',b.pts.map(q=>'X'+fmt(q[1],2)+' Z'+fmt(q[2],2)).join(' → ')],['Деталь по контуру ⌀ × L','⌀'+fmt(b.Dmax,2)+' × '+fmt(b.Lp,2)+(b.shaft?' (хвостовик условный)':'')],
    ['Заготовка (отрезать)',(b.bar[0].toUpperCase()+b.bar.slice(1))+', L = '+b.Lcut+' (+ пропил ≈'+b.kerf+'), ≈ '+fmt(b.mass,b.mass<1?2:1)+' кг'],['Материал заготовки',b.grade+(b.forge?' · вариант: поковка ⌀'+Math.ceil(b.Dmax+8)+' × '+Math.ceil(b.Lp+6):'')]];
   o.push(`<rect x="${x0}" y="${y0}" width="190" height="${RW*(rows.length+1)}" fill="none" stroke="#000" stroke-width="${W1}"/>`);
   tx(x0+95,y0+RW*0.7,'Заготовка под токарную обработку (справочно)',3.1,'middle');ln(x0+78,y0+RW,x0+78,y0+RW*(rows.length+1),W1);
   rows.forEach((r,i)=>{const y=y0+RW*(i+1);ln(x0,y,x0+190,y,i?W2:W1);tx(x0+1.2,y+RW*0.7,r[0],2.5);tx(x0+79.2,y+RW*0.7,r[1],2.6);});}}
 // основная надпись (форма 1, упрощённо)
 const bx=230,by=237;o.push(`<rect x="${bx}" y="${by}" width="185" height="55" fill="none" stroke="#000" stroke-width="${W1}"/>`);
 for(let i=1;i<11;i++)ln(bx,by+5*i,bx+65,by+5*i,i===4||i===5?W1:W2);ln(bx+7,by,bx+7,by+25,W1);[17,40,55,65].forEach(xx=>ln(bx+xx,by,bx+xx,by+55,W1));
 ln(bx+65,by+15,bx+185,by+15,W1);ln(bx+65,by+40,bx+185,by+40,W1);ln(bx+135,by+15,bx+135,by+55,W1);ln(bx+135,by+20,bx+185,by+20,W1);ln(bx+135,by+35,bx+185,by+35,W1);[152,167].forEach(xx=>ln(bx+xx,by+15,bx+xx,by+35,W1));
 ['Изм.','Лист','№ докум.','Подп.','Дата'].forEach((t,i)=>tx(bx+[3.5,12,28.5,47.5,60][i],by+23.8,t,2.2,'middle'));
 ['Разраб.','Пров.','Т.контр.','','Н.контр.','Утв.'].forEach((t,i)=>tx(bx+1,by+28.8+5*i,t,2.4));
 tx(bx+143.5,by+19,'Лит.',2.5,'middle');tx(bx+159.5,by+19,'Масса',2.5,'middle');tx(bx+176,by+19,'Масштаб',2.5,'middle');tx(bx+176,by+30,s0.t,3.5,'middle');tx(bx+145,by+39,'Лист 1',2.5,'middle');tx(bx+172,by+39,'Листов 1',2.5,'middle');
 const nm=(G.kind==='wheel'?'Колесо ':(shaft&&shaft.dj?'Вал-шестерня ':'Шестерня '))+(G.hyp?'гипоидное':'коническое');
 tx(bx+100,by+25,G.kind==='wheel'?nm:nm.replace('гипоидное','гипоидная').replace('коническое','коническая'),4,'middle');tx(bx+100,by+31,'z = '+G.z+(G.hyp?', E = '+fmt(+p.E,2):''),3.5,'middle');
 tx(bx+125,by+10,(c.name||'').slice(0,28),5,'middle');tx(bx+100,by+48,MG[mi]||'',3.4,'middle');tx(bx+160,by+48,'ООО «СпецТехЗаказ»',3.4,'middle');
 o.push(`<rect x="20" y="222" width="12" height="70" fill="none" stroke="#000" stroke-width="${W1}"/>`);tx(27,288,'Инв. № подл.',2.5,'start',-90);
 tx(240,15,'Ra 3,2 (√)',4.5);
 return `<svg xmlns="http://www.w3.org/2000/svg" width="420mm" height="297mm" viewBox="0 0 420 297">${o.join('')}</svg>`;}
/* расчётная ветка calc() для гипоидной передачи */
function calcHypoid(p,G,GL,W,gearStr,undercut,AG,LU){
 const H=hypRes(p),q=H.p,dm=v=>{const s=v<0?'−':'';v=Math.abs(v);let d=Math.floor(v+1e-9),mi=Math.round((v-d)*60);if(mi===60){d++;mi=0;}return s+d+'°'+String(mi).padStart(2,'0')+'′';};
 const u=p.z2/p.z1;
 G('Передаточное число u',u);GL('Гипоидное смещение E',+p.E);GL('Начальный диаметр колеса D_к',q[6]);G('Станок для расчёта наладки',HYPMACH[+p.mach||0],'');
 G('Угол спирали шестерни β_ш',dm(q[42]),'');G('Угол спирали колеса β_к',dm(q[44]),'');G('Угол начального конуса шестерни φ_ш',dm(q[36]),'');G('Угол начального конуса колеса φ_к',dm(q[48]),'');
 GL('Радиус в середине венца: шестерня r_ш',q[17]);GL('Радиус в середине венца: колесо r_к',q[12]);GL('Нормальный модуль в средней точке m_n',q.mn);
 GL('Длина образующей колеса L_ек',q[73]);GL('Наружный диаметр колеса D_ек',q[105]);GL('Наружный диаметр шестерни D_еш',q[135]);
 GL('Высота зуба колеса h_к',q[96]);GL('Высота головки колеса h_гк',q[93]);GL('Высота зуба шестерни h_ш',q[221]);
 G('Угол наружного / внутреннего конуса колеса',dm(q[98])+' / '+dm(q[101]),'');G('Угол наружного / внутреннего конуса шестерни',dm(q[117])+' / '+dm(q[143]),'');
 GL('Колесо: от основания наружного конуса до оси шестерни X_к',q[107]);GL('Шестерня: от основания наружного конуса до оси колеса X_ш',q[131]);GL('Шестерня: от внутреннего торца до оси колеса X_шо',q[133]);
 GL('Вершина наружного конуса колеса Z_ке',q[110]);GL('Вершина внутреннего конуса колеса Z_кi',q[111]);GL('Вершина наружного конуса шестерни Z_ше',q[121]);GL('Вершина внутреннего конуса шестерни Z_шi',q[141]);
 GL('Номинальный радиус резцовой головки r_и',q[7]);GL('Развод чистовой головки колеса W_к',q[199]);GL('Развод черновой головки колеса',q[200]);GL('Развод черновой головки шестерни',q[210]);
 G('Профильные углы резцов колеса (внутр. / наруж.)',dm(q[251][0])+' / '+dm(q[251][1]),'');G('Профильные углы резцов шестерни (вогн. / вып.)',dm(q[344][0])+' / '+dm(q[344][1]),'');
 GL('Образующий диаметр головки шестерни: вогнутая сторона',2*q[409][0]);GL('Образующий диаметр головки шестерни: выпуклая сторона',2*q[409][1]);
 GL('Толщина по зубомеру: шестерня',q[223]);GL('Высота по зубомеру: шестерня',q[226]);GL('Толщина по зубомеру: колесо',q[224]);GL('Высота по зубомеру: колесо',q[87]);
 GL('Гипоидное смещение при нарезании колеса E_к',q[247]);GL('Гипоидное смещение при нарезании шестерни E_ш',q[414][0]);
 G('Коэффициент продольного перекрытия ε_L',q[195]);
 H.warn.forEach(t=>W('warn',t));
 W('info','Геометрия и наладки — по «Руководству по расчёту гипоидных колёс и наладок станков 528С, 525, 5А27С1» (СЗТЗС, 1967). Полную карту наладки по проходам даёт «Зуборез 525»: Экспорт → «Пара → наладка Зуборез 525».');
 const al=q[80]*D2R,bm=((q[42]+q[44])/2)*D2R,vz1=p.z1/Math.cos(q[36]*D2R),vz2=p.z2/Math.cos(q[48]*D2R),ea=pair(q.mn,vz1,vz2,al,bm,0,0,false).ea;
 undercut(Math.round(vz1/Math.cos(bm)**3),0,al,bm,'шестерни (по эквивалентному z_v)');
 gearStr({d1:2*q[17],u,b:+p.b,m:q.mn,ea,beta:bm,at:Math.atan(Math.tan(al)/Math.cos(bm)),z1:vz1,z2:vz2,P:p.P,n1:p.n1,mat:p.mat,K:p.K,kb:Math.min(2,1.22+0.21*u)},'шестерня','колесо');
 return {ratio:u,sum:`u = ${fmt(u,3)} · E = ${LU(+p.E)} · D_к = ${LU(q[6])}`};}

/* Конические передачи с круговыми зубьями по ГОСТ 19326-73 (Σ = 90°)
   + наладка станка 525 двусторонним способом (обе детали двусторонними головками, развод W).
   Формулы осевой формы II проверены по чертежам 23-340-01-01 / 23-340-02-01 (пара 8/48):
   Σθf, θf1/θf2, развод 1,3, высоты hfe и диаметры вершин совпали с чертежом.
   ООО «СпецТехЗаказ» — общий модуль для STZ и «Зуборез 525». */
var G19326 = (function () {
  const R = d => d * Math.PI / 180, D = r => r * 180 / Math.PI;
  const inv = a => Math.tan(a) - a;
  function invInv(v) { let a = Math.min(1.2, Math.cbrt(3 * v)); for (let i = 0; i < 50; i++) { const t = Math.tan(a); a -= (t - a - v) / (t * t); if (a <= 0) a = 1e-4 } return a }
  const HEADS = [76.2, 95.25, 114.3, 152.4, 228.6, 304.8];   // номинальные радиусы головок
  /* inp: z1,z2, mte, b, beta (средний угол спирали), hand1 'L'|'R', alpha (20), xn1, xt1, form (1|2|3),
          ha (1), c (0.25), d0 (диаметр головки, мм), Rm (расчётное среднее конусное, 0 — Re−0,5b) или mn (средний нормальный модуль),
          dha1, dha2 (уменьшение высоты головки, мм, на внешнем торце), E2 (удвоенный эксцентриситет станка, 270 у 525) */
  function calc(inp) {
    const z1 = +inp.z1, z2 = +inp.z2, al = R(+inp.alpha || 20), be = R(+inp.beta || 35), ha = inp.ha > 0 ? +inp.ha : 1, cs = inp.c >= 0 && inp.c !== '' && inp.c != null ? +inp.c : 0.25;
    const xn1 = +inp.xn1 || 0, xt1 = +inp.xt1 || 0, form = +inp.form || 2, warn = [], note = [];
    const d1r = Math.atan(z1 / z2), d2r = Math.PI / 2 - d1r, zc = Math.hypot(z1, z2), u = z2 / z1;
    let mte = +inp.mte, Re, Rm, mn;
    if (mte > 0) { Re = 0.5 * mte * zc; if (inp.mn > 0) { mn = +inp.mn; Rm = Re * mn / (mte * Math.cos(be)); } else { Rm = inp.Rm > 0 ? +inp.Rm : Re - 0.5 * inp.b; mn = mte * Rm / Re * Math.cos(be) } }
    else { mn = +inp.mn; Rm = inp.Rm > 0 ? +inp.Rm : 0; mte = mn / Math.cos(be) * (Rm + 0.5 * inp.b) / Rm; Re = 0.5 * mte * zc }
    const b = +inp.b, bCalc = 2 * (Re - Rm), le = Re - Rm, mm = mn / Math.cos(be);
    if (Math.abs(bCalc - b) > 0.05) note.push(`Расчётная ширина венца 2·(Re − R) = ${bCalc.toFixed(2)} мм не равна фактической b = ${b} мм. Расчётное сечение (где mn и β заданы) — R = ${Rm.toFixed(3)} мм; высоты и углы на внешнем торце считаются от него, ширина венца — фактическая.`);
    const r0 = (+inp.d0 > 0 ? +inp.d0 : 2 * HEADS.reduce((a, h) => Math.abs(h - 1.1 * Rm) < Math.abs(a - 1.1 * Rm) ? h : a, HEADS[0])) / 2;
    // средние высоты и толщины
    const ham1 = (ha + xn1) * mn, ham2 = (ha - xn1) * mn, hfm1 = (ha + cs - xn1) * mn, hfm2 = (ha + cs + xn1) * mn;
    const sn1 = (0.5 * Math.PI + 2 * xn1 * Math.tan(al) + xt1) * mn, sn2 = Math.PI * mn - sn1;
    // углы ножек
    let tf1, tf2, sumF = NaN;
    if (form === 1) { tf1 = Math.atan(hfm1 / Rm); tf2 = Math.atan(hfm2 / Rm) }
    else if (form === 2) {
      sumF = R(90 * mte / (Re * Math.tan(al) * Math.cos(be)) * (1 - Rm * Math.sin(be) / r0));
      tf1 = sumF * (0.5 - 2 * xn1 * Math.tan(al) / Math.PI); tf2 = sumF - tf1;
      if (1 - Rm * Math.sin(be) / r0 <= 0.05) warn.push('Головка слишком мала для этого угла спирали и конусного расстояния (1 − R·sinβ/r₀ ≈ 0) — возьмите головку большего диаметра.');
    } else { tf1 = 0; tf2 = 0 }
    const ta1 = form === 3 ? 0 : tf2, ta2 = form === 3 ? 0 : tf1;      // постоянный радиальный зазор
    const dha1 = +inp.dha1 || 0, dha2 = +inp.dha2 || 0;
    const hae1 = ham1 + le * Math.tan(ta1) - dha1, hae2 = ham2 + le * Math.tan(ta2) - dha2;
    const hfe1 = hfm1 + le * Math.tan(tf1), hfe2 = hfm2 + le * Math.tan(tf2);
    const de1 = mte * z1, de2 = mte * z2;
    const G = (z, dl, hae, hfe, ta, tf, ham, hfm, sn, dha, hand, dhm) => {
      const d = mm * z, dae = mte * z + 2 * hae * Math.cos(dl), B = Re * Math.cos(dl) - hae * Math.sin(dl);
      // проверка заострения вершины в среднем нормальном сечении (эквивалентное цилиндрическое колесо)
      const zvn = z / (Math.cos(dl) * Math.cos(be) ** 3), rv = mn * zvn / 2, rb = rv * Math.cos(al), ra = rv + ham - dhm;
      const aa = Math.acos(Math.min(1, rb / ra)), sa = 2 * ra * (sn / (2 * rv) + inv(al) - inv(aa));
      // постоянная хорда и высота до неё в среднем нормальном сечении
      const sc = sn * Math.cos(al) ** 2, hc = ham - dhm - 0.25 * sn * Math.sin(2 * al);
      return { z, d, de: mte * z, dae, hae, hfe, he: hae + hfe, ham, hamTip: ham - dhm, hfm, sn, delta: D(dl), deltaA: D(dl + Math.atan(Math.tan(ta) - (dha - dhm) / le)), deltaF: D(dl - tf), thetaA: D(ta), thetaF: D(tf), B, sa, sc, hc, zvn, hand };
    };
    const hand2 = inp.hand1 === 'R' ? 'L' : 'R';
    // уменьшение высоты головки в среднем сечении: по умолчанию как на внешнем торце (угол вершин не меняется); можно задать отдельно
    const dhm1 = inp.dhm1 != null ? +inp.dhm1 : dha1, dhm2 = inp.dhm2 != null ? +inp.dhm2 : dha2;
    const P = G(z1, d1r, hae1, hfe1, ta1, tf1, ham1, hfm1, sn1, dha1, inp.hand1 === 'R' ? 'R' : 'L', dhm1), W = G(z2, d2r, hae2, hfe2, ta2, tf2, ham2, hfm2, sn2, dha2, hand2, dhm2);
    [P, W].forEach((g, i) => { const lim = 0.25 * mn; if (g.sa < lim) warn.push(`${i ? 'Колесо' : 'Шестерня'}: толщина зуба на вершине в среднем сечении ${g.sa.toFixed(2)} мм меньше 0,25·mn (${lim.toFixed(2)}) — зуб заострён. Уменьшите высоту головки (δha) или смещение xn.`) });
    // развод (ширина впадины на дне в среднем сечении) — форма II, чистовое нарезание двусторонней головкой
    const W1 = Math.PI * mn - sn1 - 2 * hfm1 * Math.tan(al), W2 = Math.PI * mn - sn2 - 2 * hfm2 * Math.tan(al);
    if (W1 < 0.3 || W2 < 0.3) warn.push(`Развод получается очень малым (${W1.toFixed(2)} / ${W2.toFixed(2)} мм) — проверьте модуль, смещение и c*.`);
    const Kbe = b / Re; if (Kbe > 0.35) warn.push(`b/Re = ${Kbe.toFixed(3)} больше 0,35.`);
    if (b > 10 * mte) warn.push('Ширина венца больше 10·mte.');
    return { z1, z2, u, mte, mn, mm, Re, Rm, b, bCalc, le, beta: D(be), alpha: D(al), xn1, xt1, form, ha, c: cs, r0, d0: 2 * r0, sumF: D(sumF), P, W, W1, W2, warn, note, hand1: P.hand, hand2 };
  }
  /* Наладка 525 (без наклона шпинделя головки) двусторонним способом.
     Деталь ставится на угол впадин δf; вершина ДЕЛИТЕЛЬНОГО конуса — в центре станка (осевая = монтажное расстояние),
     а так как у формы II вершина конуса впадин с ней не совпадает, образующая впадин выводится в плоскость вершин резцов
     смещением стола вдоль оси люльки на ΔB. Головка проходит через среднюю точку впадины под углом β. */
  function setup(g, opts) {
    opts = opts || {}; const E2 = opts.E2 || 270, out = {};
    const be = R(g.beta), r0 = g.r0, V = r0 * Math.cos(be);
    for (const [k, G0] of [['P', g.P], ['W', g.W]]) {
      const dl = R(G0.delta), tf = R(G0.thetaF), df = dl - tf;
      const Px = g.Rm * Math.cos(dl) + G0.hfm * Math.sin(dl), Pr = g.Rm * Math.sin(dl) - G0.hfm * Math.cos(dl);   // средняя точка впадины
      const x0 = Px - Pr / Math.tan(df);                       // вершина конуса впадин на оси детали (от вершины делит. конуса)
      const dist = Px * Math.sin(df) - Pr * Math.cos(df);       // расстояние от вершины делит. конуса до образующей впадин (со знаком)
      const Lq = Px * Math.cos(df) + Pr * Math.sin(df);         // средняя точка впадины в плоскости люльки — от оси люльки
      // как в инструкции ЭНИМС: для обычной пары (вершины совпадают) H = R − r₀·sinβ; для формы II добавляется сдвиг средней точки впадины
      const H = g.Rm - r0 * Math.sin(be) + (Lq - g.Rm / Math.cos(tf)), U = Math.hypot(H, V), q = D(Math.atan2(V, H));
      const ecc = U <= E2 ? 2 * D(Math.asin(U / E2)) : NaN;
      const zc = G0.z * Math.cos(tf) / Math.sin(dl);            // число зубьев производящего колеса
      const Q = ((ecc / 2 + (G0.hand === 'L' ? q : -q)) % 360 + 360) % 360;
      out[k] = { bab: D(df), dB: -dist, rootApex: x0, Lq, H, V, U, q, ecc, Q, zc, hand: G0.hand };
      if (!(U <= E2)) out.err = `Радиальная установка ${U.toFixed(1)} мм больше наибольшей для станка — возьмите головку меньшего диаметра.`;
    }
    return out;
  }
  return { calc, setup, HEADS };
})();


/* ===== STZ beta.16: коническая передача с круговым зубом по ГОСТ 19326 ===== */
function g19Inp(p){return {z1:+p.z1,z2:+p.z2,mte:+p.m,b:+p.b,beta:+p.betam,hand1:+p.hand===1?'L':'R',alpha:+p.alpha||20,xn1:+p.xn1||0,xt1:+p.xt1||0,form:[2,1,3][+p.axf||0],
 d0:[0,152.4,190.5,228.6,304.8][+p.d0h||0]||0,Rm:+p.Rmc>0?+p.Rmc:0,dha1:+p.dha1>0?+p.dha1:0}}
function g19On(p){return +p.g19===1&&+p.betam>=10}
/* расчёт с подгонкой угла конуса вершин под чертёж: уменьшение высоты головки в среднем сечении δh_am подбирается так,
   чтобы δa совпал с заданным (диаметр вершин d_ae при этом не меняется) */
function g19Calc(p){const inp=g19Inp(p),D=Math.PI/180;let g=G19326.calc(inp);const a1=+p.da1c||0,a2=+p.da2c||0;
 if(a1>0||a2>0){if(a1>0)inp.dhm1=(inp.dha1||0)-g.le*(Math.tan(g.P.thetaA*D)-Math.tan((a1-g.P.delta)*D));
  if(a2>0)inp.dhm2=(inp.dha2||0)-g.le*(Math.tan(g.W.thetaA*D)-Math.tan((a2-g.W.delta)*D));g=G19326.calc(inp);}
 return g}
function calcBevelG19(p,G,GL,W){const g=g19Calc(p),S=G19326.setup(g),P=g.P,Wh=g.W,dms=a=>{let t=Math.round(a*3600),d=Math.floor(t/3600),mi=Math.floor((t%3600)/60),se=t%60;return d+'°'+String(mi).padStart(2,'0')+'′'+String(se).padStart(2,'0')+'″'};
 G('Расчёт геометрии','ГОСТ 19326-73, осевая форма '+['','I','II','III'][g.form],'');GL('Средний нормальный модуль m_n',g.mn);GL('Среднее конусное расстояние R',g.Rm);
 G('Смещение x_n1 / x_n2',fmt(g.xn1,3)+' / '+fmt(-g.xn1,3),'');G('Изменение толщины x_τ1 / x_τ2',fmt(g.xt1,3)+' / '+fmt(-g.xt1,3),'');
 G('Угол ножки θf₁ / θf₂',dms(P.thetaF)+' / '+dms(Wh.thetaF),'');G('Угол конуса впадин δf₁ / δf₂',dms(P.deltaF)+' / '+dms(Wh.deltaF),'');G('Угол конуса вершин δa₁ / δa₂',dms(P.deltaA)+' / '+dms(Wh.deltaA),'');
 GL('Внешняя высота головки h_ae1',P.hae);GL('Внешняя высота головки h_ae2',Wh.hae);GL('Внешняя высота ножки h_fe1',P.hfe);GL('Внешняя высота ножки h_fe2',Wh.hfe);GL('Внешняя высота зуба h_e1',P.he);GL('Внешняя высота зуба h_e2',Wh.he);
 GL('Диаметр вершин d_ae1 (ГОСТ 19326)',P.dae);GL('Диаметр вершин d_ae2 (ГОСТ 19326)',Wh.dae);GL('Средний делительный d₁ / d₂',P.d);GL('Средний делительный d₂',Wh.d);
 GL('Нормальная толщина s_n1 (сред. сечение)',P.sn);GL('Нормальная толщина s_n2',Wh.sn);GL('Постоянная хорда s̄c1 / высота h̄c1',P.sc);GL('Высота до постоянной хорды h̄c1',P.hc);GL('Постоянная хорда s̄c2',Wh.sc);GL('Высота до постоянной хорды h̄c2',Wh.hc);
 GL('Толщина на вершине s_a1 (сред.)',P.sa);GL('Толщина на вершине s_a2 (сред.)',Wh.sa);
 if(g.form===2){GL('Развод головки шестерни W₁',g.W1);GL('Развод головки колеса W₂',g.W2);}GL('Диаметр резцовой головки d₀',g.d0);
 G('Наладка 525 (двусторонний способ): бабка шестерня / колесо',dms(S.P.bab)+' / '+dms(S.W.bab),'');GL('Смещение стола шестерня (к люльке)',S.P.dB);GL('Смещение стола колесо (к люльке)',S.W.dB);
 g.warn.forEach(t=>W('warn',t));g.note.forEach(t=>W('info',t));
 W('info','Коническая пара рассчитана по ГОСТ 19326-73. Полную карту наладки станка 525 по проходам даёт «Зуборез 525»: Экспорт → «Пара → наладка Зуборез 525».');return g;}

/* STZ — объёмная модель конической пары с круговым (или прямым) зубом для STEP.
   Зуб строится по сечениям вдоль конусного расстояния (на дополнительных конусах, эвольвента эквивалентного колеса),
   высоты головки/ножки и толщина — из G19326.calc (форма II для ГОСТ 19326, иначе форма I без смещения),
   линия зуба — дуга окружности резцовой головки r0 (средний угол β в расчётном сечении R).
   Тело — замкнутая сетка: внутренний торец (плоскость) → дополнительный конус → зуб → дополнительный конус → наружный торец. */
function bevel3D(c, c0, boreFor) {
  const p = c.p, inv = a => Math.tan(a) - a;
  let inp, g;
  if (typeof g19On === 'function' && g19On(p)) g = g19Calc(p);
  else inp = { z1: +p.z1, z2: +p.z2, mte: +p.m, b: +p.b, beta: +p.betam || 0, hand1: +p.hand === 1 ? 'L' : 'R', alpha: +p.alpha || 20, xn1: 0, xt1: 0, form: 1, ha: 1, c: 0.2 };
  if (!g) g = G19326.calc(inp);
  if (!(g.Re > 0) || !(g.mn > 0)) return null;
  const Re = g.Re, Rm = g.Rm, b = Math.min(+p.b || g.b, 0.6 * Re), s0 = Re - b, be = g.beta * Math.PI / 180, al = g.alpha * Math.PI / 180;
  const at = Math.atan(Math.tan(al) / Math.cos(be)), r0 = g.r0;
  const spiralOn = g.beta > 0.5;
  const Rc = Math.sqrt(Rm * Rm + r0 * r0 - 2 * Rm * r0 * Math.sin(be));
  const gam = s => Math.acos(Math.max(-1, Math.min(1, (s * s + Rc * Rc - r0 * r0) / (2 * s * Rc))));
  const sg = Math.sign(gam(Rm + 1) - gam(Rm - 1)) || 1, sigma = (g.hand1 === 'L' ? 1 : -1) * sg;
  const phiC = s => spiralOn ? sigma * (gam(s) - gam(Rm)) : 0;
  const NS = 9, NF = 9, lin = (s, vm, ve) => vm + (s - Rm) * (ve - vm) / (Re - Rm || 1);
  const gearRows = (G, z, dl, base, role) => {
    const k = G.sn / (Math.PI * g.mn), zv = z / Math.cos(dl), psi = Math.PI * k / zv, step = 2 * Math.PI / zv;
    const sd = Math.sin(dl), cd = Math.cos(dl), rows = [];
    const ha = s => Math.max(0.1 * g.mn, lin(s, G.hamTip, G.hae)), hf = s => Math.max(0.2 * g.mn, lin(s, G.hfm, G.hfe));
    const map = (s, R, a, Rv, th0) => { const u = R - Rv; return [s * sd + u * cd, th0 + a / cd, s * cd - u * sd]; }; // r, θ, вдоль оси от вершины
    for (let i = 0; i < NS; i++) {
      const s = s0 + b * i / (NS - 1), Rv = s * sd / cd, rb = Rv * Math.cos(at), rt = Rv + ha(s), rr = Rv - hf(s);
      const th = R => { const RR = Math.max(R, rb); const v = psi + inv(at) - inv(Math.acos(Math.min(1, rb / RR))); return Math.max(v, 0.04 * psi); };
      const tooth = [];
      for (let q = 0; q < NF; q++) { const R = rr + (rt - rr) * q / (NF - 1); tooth.push([R, -th(R)]); }
      tooth.push([rt, 0]);
      for (let q = NF - 1; q >= 0; q--) { const R = rr + (rt - rr) * q / (NF - 1); tooth.push([R, th(R)]); }
      const t1 = th(rr); tooth.push([rr, t1 + (step - 2 * t1) / 3], [rr, t1 + 2 * (step - 2 * t1) / 3]);
      const sp = phiC(s) / sd, row = [];
      for (let n = 0; n < z; n++) { const th0 = base + 2 * Math.PI * n / z + sp; for (const [R, a] of tooth) row.push(map(s, R, a, Rv, th0)); }
      rows.push(row);
    }
    // кольца торцов на дополнительных конусах (плоские)
    const gap = Math.max(1.5, 1.0 * g.mte), bi = boreFor ? boreFor(c, role, 0.5 * s0 * sd) : null;
    let rBore = bi && bi.d ? bi.d / 2 : 0;
    const ringAt = (s, hfs) => { const rmin = Math.max(0.3 * s * sd, rBore + 2); let t = hfs + gap; if (s * sd - t * cd < rmin) t = (s * sd - rmin) / cd; if (t < hfs + 0.3) t = hfs + 0.3; return { r: s * sd - t * cd, zl: s * cd + t * sd }; };
    const Ai = ringAt(s0, hf(s0)), Ao = ringAt(Re, hf(Re));
    if (rBore && rBore > Math.min(Ai.r, Ao.r) - 1.5) rBore = 0;
    const Nj = rows[0].length;
    const ring = (A, src) => src.map((q, j) => { const uni = src[0][1] + 2 * Math.PI * j / Nj; return [A.r, 0.5 * (q[1] + uni), A.zl]; });
    const all = [ring(Ai, rows[0])].concat(rows, [ring(Ao, rows[NS - 1])]);
    const bore = rBore ? [Ao.zl, Ai.zl].map(zl => Array.from({ length: 64 }, (_, j) => [rBore, 2 * Math.PI * j / 64, zl])) : null;
    return { all, bore, bands: [[0, 1], [1, NS], [NS, NS + 1]] };
  };
  const d1 = g.P.delta * Math.PI / 180, d2 = g.W.delta * Math.PI / 180;
  const P1 = gearRows(g.P, g.z1, d1, Math.PI, 1), P2 = gearRows(g.W, g.z2, d2, Math.PI + Math.PI / g.z2, 2);
  const toW1 = ([r, t, zl]) => [-zl, r * Math.cos(t), r * Math.sin(t)], toW2 = ([r, t, zl]) => [r * Math.cos(t), -zl, r * Math.sin(t)];
  const fin = (Q, toW) => ({ rows: Q.all.map(r => r.map(toW)), bore: Q.bore ? Q.bore.map(r => r.map(toW)) : null, bands: Q.bands });
  const id = { p: (x, y, z) => [x, y, z], d: (x, y, z) => [x, y, z] };
  const hyp = c0 && c0.type === 'hypoid', E = hyp ? (+c0.p.E || 0) : 0;
  const XF1 = { p: id.p, d: id.d };   // гипоидное смещение E добавляет stageSolids
  const solids = [
    { mesh: fin(P1, toW1), label: (hyp ? 'Shesternya gipoid z' : 'Shesternya z') + g.z1, XF: XF1, pts: [] },
    { mesh: fin(P2, toW2), label: (hyp ? 'Koleso gipoid z' : 'Koleso z') + g.z2, XF: id, pts: [] }];
  const inAx = { p: [-Re * Math.cos(d1), 0, 0], d: [-1, 0, 0] }, outAx = { p: [0, -Re * Math.cos(d2), 0], d: [0, -1, 0] };
  return { solids, inAx, outAx, g };
}

/* Грани STEP для сетки bevel3D. E — запись сущности, f — формат числа. */
function meshFacesSTEP(sh, E, f) {
  const X = sh.XF, M = sh.mesh, fc = [];
  const sb = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]], dt = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  let rows = M.rows.map(r => r.slice()), bore = M.bore ? M.bore.map(r => r.slice()) : null;
  const Nj = rows[0].length, last = rows.length - 1;
  // ориентация: знак объёма (обход как в гранях ниже)
  const vol = () => {
    let V = 0; const tri = (a, b, c) => { V += dt(a, cr(b, c)); };
    const strips = (R, a, c, N) => { for (let i = a; i < c; i++) for (let j = 0; j < N; j++) { const k = (j + 1) % N, A = R[i][j], B = R[i][k], C = R[i + 1][k], D = R[i + 1][j]; tri(A, B, C); tri(A, C, D); } };
    strips(rows, 0, last, Nj); if (bore) strips(bore, 0, 1, bore[0].length);
    const cen = r => [0, 1, 2].map(t => r.reduce((s, q) => s + q[t], 0) / r.length);
    const fan = (r, rev) => { const o = cen(r), N = r.length; for (let j = 0; j < N; j++) { const a = r[j], b = r[(j + 1) % N]; rev ? tri(o, b, a) : tri(o, a, b); } };
    fan(rows[0], true); fan(rows[last], false); if (bore) { fan(bore[1], false); fan(bore[0], true); }
    return V;
  };
  if (vol() < 0) { rows = rows.map(r => r.slice().reverse()); if (bore) bore = bore.map(r => r.slice().reverse()); }
  const PT = q => { const r = X ? X.p(q[0], q[1], q[2]) : q; return E(`CARTESIAN_POINT('',(${f(r[0])},${f(r[1])},${f(r[2])}))`); };
  const DR = q => { const l = Math.hypot(q[0], q[1], q[2]) || 1, r0 = [q[0] / l, q[1] / l, q[2] / l], r = X ? X.d(r0[0], r0[1], r0[2]) : r0; return E(`DIRECTION('',(${f(r[0])},${f(r[1])},${f(r[2])}))`); };
  const build = (R, bands) => {
    const N = R[0].length, pid = R.map(r => r.map(PT)), vid = {}, hE = {}, rE = {};
    const V = (i, j) => { const k = i + ':' + j; if (!vid[k]) vid[k] = E(`VERTEX_POINT('',${pid[i][j]})`); return vid[k]; };
    const line = (i0, j0, i1, j1) => { const A = R[i0][j0], B = R[i1][j1], d = sb(B, A), L = Math.hypot(d[0], d[1], d[2]), tf = X ? (() => { const a = X.p(A[0], A[1], A[2]), bb = X.p(B[0], B[1], B[2]); return Math.hypot(bb[0] - a[0], bb[1] - a[1], bb[2] - a[2]); })() : L;
      return E(`EDGE_CURVE('',${V(i0, j0)},${V(i1, j1)},${E(`LINE('',${pid[i0][j0]},${E(`VECTOR('',${DR(d)},${f(tf)})`)})`)},.T.)`); };
    const H = (i, j) => { const k = i + ':' + j; if (!hE[k]) hE[k] = line(i, j, i, (j + 1) % N); return hE[k]; };
    const RL = (a, c, j) => { const k = a + ':' + c + ':' + j; if (!rE[k]) { if (c - a === 1) rE[k] = line(a, j, c, j); else { const ids = []; for (let i = a; i <= c; i++) ids.push(pid[i][j]); const n = c - a + 1;
          rE[k] = E(`EDGE_CURVE('',${V(a, j)},${V(c, j)},${E(`B_SPLINE_CURVE_WITH_KNOTS('',1,(${ids.join(',')}),.UNSPECIFIED.,.F.,.F.,(2,${Array(n - 2).fill(1).concat([2]).join(',')}),(${Array.from({ length: n }, (_, i) => f(i)).join(',')}),.UNSPECIFIED.)`)},.T.)`); } } return rE[k]; };
    const OE = (e, fw) => E(`ORIENTED_EDGE('',*,*,${e},${fw ? '.T.' : '.F.'})`);
    for (const [a, c] of bands) for (let j = 0; j < N; j++) {
      const k = (j + 1) % N, n = c - a + 1, colA = [], colB = [];
      for (let i = a; i <= c; i++) { colA.push(pid[i][j]); colB.push(pid[i][k]); }
      const surf = E(`B_SPLINE_SURFACE_WITH_KNOTS('',1,1,((${colA.join(',')}),(${colB.join(',')})),.UNSPECIFIED.,.F.,.F.,.F.,(2,2),(2,${Array(n - 2).fill(1).concat([2]).join(',')}),(0.,1.),(${Array.from({ length: n }, (_, i) => f(i)).join(',')}),.UNSPECIFIED.)`);
      const loop = E(`EDGE_LOOP('',(${[OE(H(a, j), true), OE(RL(a, c, k), true), OE(H(c, j), false), OE(RL(a, c, j), false)].join(',')}))`);
      fc.push(E(`ADVANCED_FACE('',(${E(`FACE_OUTER_BOUND('',${loop},.T.)`)}),${surf},.T.)`));
    }
    return { H, OE, N };
  };
  const O = build(rows, M.bands), Bo = bore ? build(bore, [[0, 1]]) : null;
  const newell = r => { let n = [0, 0, 0]; for (let j = 0; j < r.length; j++) { const a = r[j], b = r[(j + 1) % r.length]; n[0] += (a[1] - b[1]) * (a[2] + b[2]); n[1] += (a[2] - b[2]) * (a[0] + b[0]); n[2] += (a[0] - b[0]) * (a[1] + b[1]); } return n; };
  const cap = (outerRow, outerFw, innerB) => {
    const N = O.N, oes = [];
    if (outerFw) for (let j = 0; j < N; j++) oes.push(O.OE(O.H(outerRow, j), true)); else for (let j = N - 1; j >= 0; j--) oes.push(O.OE(O.H(outerRow, j), false));
    const loops = [E(`FACE_OUTER_BOUND('',${E(`EDGE_LOOP('',(${oes.join(',')}))`)},.T.)`)];
    if (innerB) { const [row, fw] = innerB, M2 = Bo.N, ies = []; if (fw) for (let j = 0; j < M2; j++) ies.push(Bo.OE(Bo.H(row, j), true)); else for (let j = M2 - 1; j >= 0; j--) ies.push(Bo.OE(Bo.H(row, j), false));
      loops.push(E(`FACE_BOUND('',${E(`EDGE_LOOP('',(${ies.join(',')}))`)},.T.)`)); }
    const r = rows[outerRow], seq = outerFw ? r : r.slice().reverse(), nn = newell(seq), ref = sb(r[1], r[0]);
    const pl = E(`PLANE('',${E(`AXIS2_PLACEMENT_3D('',${PT(r[0])},${DR(nn)},${DR(sb(ref, nn.map(x => x * dt(ref, nn) / dt(nn, nn))))})`)})`);
    fc.push(E(`ADVANCED_FACE('',(${loops.join(',')}),${pl},.T.)`));
  };
  // ряд 0 полоса использует H(0) прямо → торец обратно; последний ряд — наоборот. Отверстие: ряды [наруж., внутр.]
  cap(0, false, bore ? [1, true] : null);
  cap(last, true, bore ? [0, false] : null);
  return fc;
}


const MATS=[{n:'Сталь 45, улучшение (HB 200…240)',H:515,F:250,Hlim:510,SH:1.1,Flim:396,SF:1.7,HB:220},{n:'40Х, улучшение (HB 260…280)',H:580,F:290,Hlim:610,SH:1.1,Flim:486,SF:1.7,HB:270},{n:'40Х, закалка ТВЧ (HRC 45…50)',H:860,F:370,Hlim:1016,SH:1.2,Flim:600,SF:1.7,HB:460,hard:1},{n:'20ХН3А, цементация (HRC 56…62)',H:1200,F:460,Hlim:1357,SH:1.2,Flim:800,SF:1.55,HB:600,hard:1},{n:'Капролон / полиамид',H:45,F:30,plast:1}];
[[7850,110],[7850,140],[7850,140],[7850,260],[1150,450]].forEach(([r,pr],i)=>{MATS[i].rho=r;MATS[i].price=pr;});
const MODES=[{n:'Постоянный',H:1,F6:1,F9:1},{n:'Тяжёлый',H:0.5,F6:0.3,F9:0.2},{n:'Средний равновероятный',H:0.25,F6:0.143,F9:0.1},{n:'Средний нормальный',H:0.18,F6:0.065,F9:0.036},{n:'Лёгкий',H:0.125,F6:0.038,F9:0.016},{n:'Особо лёгкий',H:0.063,F6:0.013,F9:0.004}];
const CHAINS=[{n:'ПР-8-4,6',t:8,d1:5,Q:4.6},{n:'ПР-12,7-18,2',t:12.7,d1:8.51,Q:18.2},{n:'ПР-15,875-22,7',t:15.875,d1:10.16,Q:22.7},{n:'ПР-19,05-31,8',t:19.05,d1:11.91,Q:31.8},{n:'ПР-25,4-60',t:25.4,d1:15.88,Q:60},{n:'ПР-31,75-89',t:31.75,d1:19.05,Q:89}];
const BELTS=[{n:'GT2, шаг 2 мм',p:2,h:0.75,F:2.2,zmin:12},{n:'T5, шаг 5 мм',p:5,h:1.2,F:5,zmin:10},{n:'HTD 5M, шаг 5 мм',p:5,h:2.06,F:8,zmin:14},{n:'HTD 8M, шаг 8 мм',p:8,h:3.38,F:15,zmin:22},{n:'T10, шаг 10 мм',p:10,h:2.5,F:11,zmin:12}];
const GD={m:2,z1:20,z2:40,alpha:20,x1:0,x2:0,ha:1,cs:0.25,hand:0,b:20,P:5,n1:1450,mat:1,K:1.3};
const TYPES={
 spur:{l:'Прямозубая',f:'Цилиндрическая прямозубая передача',k:['m','z1','z2','alpha','x1','x2','ha','cs','b','mat','P','n1','K'],d:GD,desc:'Параллельные валы. Простая и технологичная; при окружной скорости выше 6 м/с лучше перейти на косозубую.'},
 helical:{l:'Косозубая',f:'Цилиндрическая косозубая передача',k:['m','z1','z2','beta','hand','alpha','x1','x2','ha','cs','b','mat','P','n1','K'],d:{...GD,beta:15},desc:'Зубья под углом β: плавнее и тише прямозубой, выше нагрузочная способность, но появляется осевая сила.'},
 herring:{l:'Шевронная',f:'Цилиндрическая шевронная передача',k:['m','z1','z2','beta','alpha','x1','x2','ha','cs','b','gap','mat','P','n1','K'],d:{...GD,beta:30,b:60,gap:20,P:7.5},desc:'Две косозубые половины с противоположным наклоном: осевые силы взаимно уравновешены, поэтому допустим угол β = 25…40° и высокая нагрузка. Применяется в мощных редукторах.'},
 internal:{l:'Внутренняя',f:'Передача с внутренним зацеплением',k:['m','z1','z2','alpha','x1','ha','cs','b','mat','P','n1','K'],d:{...GD,z1:18,z2:60,P:3},desc:'Шестерня внутри венца: компактно, валы вращаются в одну сторону. Разность z₂ − z₁ должна быть не меньше 10.'},
 rack:{l:'Реечная',f:'Реечная передача',k:['m','z1','alpha','x1','b','L','mat','P','n1','K'],d:{m:2,z1:20,alpha:20,x1:0,b:20,L:240,P:1.5,n1:120,mat:1,K:1.3},desc:'Преобразует вращение в прямолинейное движение. Ход за один оборот равен π·m·z.'},
 bevel:{l:'Коническая',f:'Коническая передача (прямой / круговой зуб), Σ = 90°',k:['m','z1','z2','alpha','betam','hand','b','g19','xn1','xt1','axf','d0h','Rmc','dha1','da1c','da2c','mat','P','n1','K'],d:{m:3,z1:18,z2:36,alpha:20,b:22,P:4,n1:960,mat:2,K:1.4},desc:'Пересекающиеся валы под 90°. Модуль задаётся внешний окружной m_e; ширина венца обычно ≤ 0,3·R_e.'},
 hypoid:{l:'Гипоидная',f:'Гипоидная передача (оси скрещиваются, смещение E), Σ = 90°',k:['m','z1','z2','E','bsh','hand','b','asum','rhd','mach','mat','P','n1','K'],d:{m:5.11098,z1:10,z2:41,E:38.1,bsh:50,hand:1,b:31.75,asum:0,rhd:0,mach:0,P:40,n1:1500,mat:3,K:1.5,key:2},desc:'Оси шестерни и колеса скрещиваются: ось шестерни смещена на E. Геометрия и наладки станков 525 / 528С / 5А27С1 — по методике СЗТЗС 1967 г. (Глисон). Модуль — торцовый модуль колеса m_s = D_к / z₂.'},
 worm:{l:'Червячная',f:'Червячная передача (архимедов червяк)',k:['m','z1','z2','q','alpha','b','P','n1','f','K'],d:{m:4,z1:2,z2:40,q:10,alpha:20,b:32,P:3,n1:1450,f:0.03,K:1.2},desc:'Большое передаточное число в одной ступени и возможное самоторможение. Венец колеса — бронза, червяк — закалённая сталь.'},
 globoid:{l:'Глобоидная',f:'Глобоидная червячная передача',k:['m','z1','z2','q','alpha','b','P','n1','f','K'],d:{m:8,z1:1,z2:40,q:8,alpha:20,b:48,P:2.2,n1:1450,f:0.025,K:1.2,fan:1},desc:'Червяк в форме песочных часов охватывает сразу несколько зубьев колеса (z′ ≈ 0,1·z₂). Нагрузочная способность в 1,4…2 раза выше обычной червячной при тех же габаритах, КПД выше. Сложнее в изготовлении, чувствительна к точности сборки по межосевому и осевому положению червяка.'},
 planet:{l:'Планетарная',f:'Планетарная передача 2K-H (корона неподвижна)',k:['m','zs','zp','N','alpha','b','mat','P','n1','K'],d:{m:2,zs:24,zp:24,N:3,alpha:20,b:20,P:5,n1:1450,mat:3,K:1.3},desc:'Вход — солнце, выход — водило, корона закреплена. Число зубьев короны z_b = z_a + 2·z_g, i = 1 + z_b / z_a.'},
 chain:{l:'Цепная',f:'Цепная передача роликовой цепью',k:['chain','z1','z2','a','P','n1'],d:{chain:1,z1:17,z2:34,a:420,P:3,n1:400},desc:'Звёздочки по ГОСТ 591, цепи по ГОСТ 13568. Нечётное число зубьев малой звёздочки и чётное число звеньев — лучший вариант.'},
 belt:{l:'Зубч. ремень',f:'Передача зубчатым ремнём',k:['prof','z1','z2','a','bw','P','n1'],d:{prof:2,z1:20,z2:40,a:160,bw:15,P:0.5,n1:1500},desc:'Синхронная передача без проскальзывания. В зацеплении с малым шкивом должно быть не меньше 6 зубьев ремня.'},
 spline:{l:'Шлицевая',f:'Шлицевое эвольвентное соединение, α = 30°',k:['m','z','l','P','n1','sig'],d:{m:2,z:14,l:30,P:7.5,n1:1450,sig:100},desc:'Соединение вала со ступицей по ГОСТ 6033. Проверяется на смятие рабочих граней.'}
};
const EXTRA={spur:['ca','crown','acc','key','hub','dv1','dv2'],helical:['ca','crown','acc','key','hub','dv1','dv2'],herring:['ca','crown','acc','key','dv1','dv2'],internal:['acc','key','hub','dv1'],rack:['acc','key','dv1'],bevel:['key','dv1','dv2'],hypoid:['key','dv1','dv2'],worm:['key','hub','dv2'],globoid:['key','hub','dv2'],planet:['acc','key','dv1'],chain:['key','hub','dv1','dv2'],belt:['key','hub','dv1','dv2'],spline:[]};
Object.keys(TYPES).forEach(k=>{const T=TYPES[k],sh=!(k==='planet'||k==='spline');T.k=T.k.concat(T.k.includes('mat')?['mode','Lh']:sh?['Lh']:[],EXTRA[k],sh?['arr','span','brg']:[],k==='spline'?[]:['rate']);{const iP=T.k.indexOf('P');if(iP>=0)T.k.splice(iP,0,'link');}T.d={link:0,rate:2500,betam:0,ca:0,crown:0,gap:20,dv1:0,dv2:0,key:0,acc:1,hub:0,mode:2,Lh:10000,arr:(k==='bevel'||k==='hypoid'||k==='chain'||k==='belt')?2:0,span:0,brg:0,...T.d};});
TYPES.worm.k.splice(TYPES.worm.k.indexOf('K')+1,0,'fan','tA');TYPES.worm.d={fan:0,tA:20,...TYPES.worm.d};
TYPES.globoid.k.splice(TYPES.globoid.k.indexOf('K')+1,0,'fan','tA');TYPES.globoid.d={tA:20,...TYPES.globoid.d};
const BRG2=[[10,'200',5.1,2.4,9,30],[12,'201',6.8,3.05,10,32],[15,'202',7.65,3.7,11,35],[17,'203',9.56,4.5,12,40],[20,'204',12.7,6.2,14,47],[25,'205',14,6.95,15,52],[30,'206',19.5,10,16,62],[35,'207',25.5,13.7,17,72],[40,'208',32,17.8,18,80],[45,'209',33.2,18.6,19,85],[50,'210',35.1,19.8,20,90],[55,'211',43.6,25,21,100],[60,'212',52,31,22,110],[65,'213',56,34,23,120],[70,'214',61.8,37.5,24,125],[75,'215',66.3,41,25,130],[80,'216',70.2,45,26,140],[85,'217',83.2,53,28,150],[90,'218',95.6,62,30,160],[95,'219',108,69.5,32,170],[100,'220',124,79,34,180]];
const BRG3=[[20,'304',15.9,7.8,15,52],[25,'305',22.5,11.4,17,62],[30,'306',28.1,14.6,19,72],[35,'307',33.2,18,21,80],[40,'308',41,22.4,23,90],[45,'309',52.7,30,25,100],[50,'310',65.8,36,27,110],[55,'311',71.5,41.5,29,120],[60,'312',81.9,48,31,130],[65,'313',92.3,56,33,140],[70,'314',104,63,35,150],[75,'315',112,72.5,37,160],[80,'316',124,80,39,170],[85,'317',133,90,41,180],[90,'318',143,99,43,190],[100,'320',174,132,47,215]];
const BRG7=[[20,'7204',21,0.36,1.67,15.25,47],[25,'7205',24,0.36,1.67,16.25,52],[30,'7206',31.5,0.36,1.64,17.25,62],[35,'7207',38.5,0.37,1.62,18.25,72],[40,'7208',46.5,0.38,1.56,19.75,80],[45,'7209',50,0.41,1.45,20.75,85],[50,'7210',56,0.37,1.6,21.75,90],[55,'7211',65,0.41,1.46,22.75,100],[60,'7212',78,0.35,1.71,23.75,110],[65,'7213',84,0.37,1.62,24.75,120],[70,'7214',96,0.37,1.62,26.25,125],[75,'7215',107,0.39,1.55,27.25,130],[80,'7216',112,0.42,1.43,28.25,140],[85,'7217',130,0.43,1.38,30.5,150],[90,'7218',158,0.38,1.56,32.5,160],[100,'7220',185,0.37,1.6,37,180]];
const EYB=[[0.014,0.19,2.3],[0.028,0.22,1.99],[0.056,0.26,1.71],[0.084,0.28,1.55],[0.11,0.3,1.45],[0.17,0.34,1.31],[0.28,0.38,1.15],[0.42,0.42,1.04],[0.56,0.44,1]];
function eyBall(r){if(r<=EYB[0][0])return EYB[0].slice(1);for(let i=1;i<EYB.length;i++)if(r<=EYB[i][0]){const a=EYB[i-1],b=EYB[i],t=(r-a[0])/(b[0]-a[0]);return [a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t];}return EYB[EYB.length-1].slice(1);}
function bearingLife(kind,row,FrA,FrB,Fa,n,Kb){const L=(Pq,e)=>Pq>0?Math.pow(row[2]*1000/Pq,e)*1e6/(60*n):Infinity;
 if(kind==='ball'){const C0=row[3]*1000,P=(Fr,Fx)=>{if(Fx<=0)return Fr*Kb;const [e,Y]=eyBall(Fx/C0);return (Fx>e*Fr?0.56*Fr+Y*Fx:Fr)*Kb;};return [L(P(FrA,Fa),3),L(P(FrB,0),3)];}
 const e=row[3],Y=row[4],SA=0.83*e*FrA,SB=0.83*e*FrB;let FaA,FaB;if(SB+Fa>=SA){FaA=SB+Fa;FaB=SB;}else{FaA=SA;FaB=SA-Fa;}
 const P=(Fr,Fx)=>(Fx>e*Fr?0.4*Fr+Y*Fx:Fr)*Kb;return [L(P(FrA,FaA),10/3),L(P(FrB,FaB),10/3)];}
function shaftLoads(c){if(c.type==='hypoid')return shaftLoads({...c,type:'bevel',p:hypToBevel(c.p)});const p=c.p,t=c.type==='herring'?'helical':c.type,al=(p.alpha||20)*D2R,T1=9550*p.P/p.n1,out=[],arr=p.arr||0,mk=(role,name,n,T,Ft,Fr,Fa,dg,ar,bw,lAuto)=>out.push({role,name,n,T,Ft,Fr,Fa,dg,arr:ar,bw,lAuto});
 if(t==='spur'||t==='helical'||t==='internal'){const be=t==='helical'?p.beta*D2R:0,r=pair(p.m,p.z1,p.z2,al,be,p.x1,t==='internal'?0:p.x2,t==='internal',p.ha??1,p.cs??0.25),u=p.z2/p.z1,Ft=2000*T1/r.d1;
  const Fa=c.type==='herring'?0:Ft*Math.tan(be);mk(1,'Вал шестерни',p.n1,T1,Ft,Ft*Math.tan(r.atw),Fa,r.d1,arr,p.b);if(t!=='internal')mk(2,'Вал колеса',p.n1/u,T1*u,Ft,Ft*Math.tan(r.atw),Fa,r.d2,arr,p.b);}
 else if(t==='rack'){const d1=p.m*p.z1,Ft=2000*T1/d1;mk(1,'Вал шестерни',p.n1,T1,Ft,Ft*Math.tan(al),0,d1,arr,p.b);}
 else if(t==='bevel'){const d1r=Math.atan(p.z1/p.z2),Re=0.5*p.m*Math.hypot(p.z1,p.z2),u=p.z2/p.z1,dm1=p.m*p.z1*(1-0.5*Math.min(p.b,Re*0.45)/Re),Ft=2000*T1/dm1,bm=(p.betam||0)*D2R,Fr1=Math.abs(Ft*(Math.tan(al)*Math.cos(d1r)/Math.cos(bm)-Math.tan(bm)*Math.sin(d1r))),Fa1=Ft*(Math.tan(al)*Math.sin(d1r)/Math.cos(bm)+Math.tan(bm)*Math.cos(d1r));
  mk(1,'Вал шестерни',p.n1,T1,Ft,Fr1,Fa1,dm1,arr,p.b);mk(2,'Вал колеса',p.n1/u,T1*u,Ft,Fa1,Fr1,dm1*u,0,p.b);}
 else if(t==='worm'||t==='globoid'){const d1=p.q*p.m,d2=p.z2*p.m,ga=Math.atan(p.z1/p.q),eta=0.95*Math.tan(ga)/Math.tan(ga+Math.atan(p.f)),i=p.z2/p.z1,T2=T1*i*eta,Ft1=2000*T1/d1,Ft2=2000*T2/d2,Fr=Ft2*Math.tan(al);
  mk(1,'Вал червяка',p.n1,T1,Ft1,Fr,Ft2,d1,0,(11+0.06*p.z2)*p.m,d2+2*p.m);mk(2,'Вал колеса',p.n1/i,T2,Ft2,Fr,Ft1,d2,0,p.b);}
 else if(t==='chain'||t==='belt'){const g=t==='chain'?chainGeo(p):beltGeo(p),D1=t==='chain'?g.Dd(p.z1):g.d1,D2=t==='chain'?g.Dd(p.z2):g.d2,v=Math.PI*D1*p.n1/60000,Fq=(t==='chain'?1.15:1.25)*1000*p.P/v,i=p.z2/p.z1,w=t==='chain'?g.C.t*0.9:p.bw;
  mk(1,t==='chain'?'Вал ведущей звёздочки':'Вал ведущего шкива',p.n1,T1,Fq,0,0,D1,arr,w);mk(2,t==='chain'?'Вал ведомой звёздочки':'Вал ведомого шкива',p.n1/i,T1*i,Fq,0,0,D2,arr,w);}
 return out;}
const ACC=[{n:'7-C',q:7,hob:'AA'},{n:'8-B',q:8,hob:'A'},{n:'8-C',q:8,hob:'A'},{n:'9-B',q:9,hob:'B'},{n:'10-A',q:10,hob:'C'}];
const LBL={worm:{z1:'Заходов червяка z₁',z2:'Зубьев колеса z₂',dv2:'Вал колеса d_в2'},rack:{z1:'Зубьев шестерни z'},chain:{z1:'Зубьев ведущей звёздочки',z2:'Зубьев ведомой звёздочки',dv1:'Вал ведущей звёздочки',dv2:'Вал ведомой звёздочки'},belt:{z1:'Зубьев ведущего шкива',z2:'Зубьев ведомого шкива',dv1:'Вал ведущего шкива',dv2:'Вал ведомого шкива'},bevel:{m:'Внешний модуль m_e'},planet:{dv1:'Вал солнца d_в'}};
LBL.globoid=LBL.worm;LBL.hypoid={m:'Торцовый модуль колеса m_s',z1:'Зубьев шестерни z_ш',z2:'Зубьев колеса z_к',b:'Ширина венца колеса F',dv1:'Вал шестерни d_в1',dv2:'Вал колеса d_в2'};const LEN=[1/25.4,'дюйм'];
const F={
 m:{l:'Модуль m',u:'мм',g:'Геометрия',min:0.2,max:50,h:'Модуль по ГОСТ 9563 / ISO 54. Предпочтительный ряд: 1; 1,25; 1,5; 2; 2,5; 3; 4; 5; 6; 8; 10. В режиме AGMA вводится диаметральный шаг P_d = 25,4 / m.'},
 z1:{l:'Зубьев шестерни z₁',g:'Геометрия',int:1,min:1,max:400,h:'Число зубьев ведущего колеса. Без смещения при z < 17 возникает подрез ножки зуба.'},
 z2:{l:'Зубьев колеса z₂',g:'Геометрия',int:1,min:4,max:600,h:'Число зубьев ведомого колеса. Передаточное число u = z₂ / z₁.'},
 zs:{l:'Зубьев солнца z_a',g:'Геометрия',int:1,min:9,max:300,h:'Центральное (солнечное) колесо — входной вал.'},
 zp:{l:'Зубьев сателлита z_g',g:'Геометрия',int:1,min:9,max:300,h:'Сателлиты обкатываются между солнцем и неподвижной короной.'},
 N:{l:'Число сателлитов n_w',g:'Геометрия',int:1,min:1,max:8,h:'Обычно 3. Равномерная сборка возможна, если (z_a + z_b) / n_w — целое число.'},
 z:{l:'Число зубьев z',g:'Геометрия',int:1,min:6,max:80,h:'Число шлицев соединения.'},
 alpha:{l:'Угол профиля α',u:'°',g:'Геометрия',min:14.5,max:30,h:'Угол исходного контура. ГОСТ 13755 и ISO 53 — 20°. AGMA допускает также 14,5° и 25°.'},
 beta:{l:'Угол наклона β',u:'°',g:'Геометрия',min:0,max:45,h:'Для косозубых колёс обычно 8…20°. Больший угол — плавнее работа, но выше осевая сила на подшипники.'},
 gap:{l:'Канавка посередине g',u:'мм',ag:LEN,g:'Геометрия',min:1,max:500,h:'Канавка для выхода червячной фрезы между полушевронами. Ориентир g ≈ (10…15)·m. Ширина b — полная, рабочая ширина — b − g.'},
 betam:{l:'Средний угол наклона β_m',u:'°',g:'Геометрия',min:0,max:40,h:'0 — прямой зуб. 30…35° — круговой зуб (Gleason): плавнее, тише, нагрузочная способность выше в 1,5…2 раза. В STEP зуб строится с закруткой по длине (линейчатая аппроксимация кругового зуба).'},
 g19:{l:'Геометрия кругового зуба',g:'Геометрия',sel:()=>[{v:'0',l:'Упрощённая (h_a = m_te)'},{v:'1',l:'ГОСТ 19326-73 (x_n, x_τ, форма зуба)'}],h:'ГОСТ 19326 — расчёт конических колёс с круговыми зубьями: смещение x_n, изменение толщины x_τ, осевая форма зуба I/II/III, углы ножек, развод, постоянная хорда. Нужен угол β_m ≥ 10°.'},
 xn1:{l:'Смещение шестерни x_n1 (ГОСТ 19326)',g:'Геометрия',min:-1,max:1,h:'Коэффициент радиального смещения шестерни, у колеса x_n2 = −x_n1. Для малых z₁ и больших u — по таблице ГОСТ 19326 (например, z₁ 8, u 6 → около 0,5).'},
 xt1:{l:'Изменение толщины x_τ1',g:'Геометрия',min:-0.5,max:0.5,h:'Коэффициент изменения расчётной толщины зуба шестерни (тангенциальное смещение), x_τ2 = −x_τ1.'},
 axf:{l:'Осевая форма зуба',g:'Геометрия',sel:()=>[{v:'0',l:'II — понижающиеся (постоянная ширина впадины)'},{v:'1',l:'I — пропорционально понижающиеся'},{v:'2',l:'III — равновысокие'}],h:'Форма II — для нарезания двусторонней головкой обеих деталей (развод W). Форма I — пропорциональная (метод ЭНИМС с односторонними головками).'},
 d0h:{l:'Резцовая головка d₀',g:'Геометрия',sel:()=>[{v:'0',l:'Подобрать'},{v:'1',l:'6″ — ⌀152,4'},{v:'2',l:'7½″ — ⌀190,5'},{v:'3',l:'9″ — ⌀228,6'},{v:'4',l:'12″ — ⌀304,8'}],h:'Номинальный диаметр зуборезной головки — входит в сумму углов ножек формы II.'},
 Rmc:{l:'Среднее конусное расстояние R (с чертежа)',u:'мм',ag:LEN,g:'Геометрия',min:0,max:2000,h:'0 — R = Re − 0,5·b. Если на чертеже R не совпадает с Re − 0,5b (ширину венца уменьшали), впишите R с чертежа: в нём заданы m_n и β.'},
 dha1:{l:'Укорочение головки шестерни δh_a1',u:'мм',g:'Геометрия',min:0,max:5,h:'Уменьшение высоты головки зуба шестерни против заострения вершины (по ГОСТ 19326). 0 — без укорочения.'},
 da1c:{l:'Угол конуса вершин шестерни δa1 (с чертежа)',u:'°',g:'Геометрия',min:0,max:90,h:'0 — по расчёту. Если на чертеже угол вершин задан другим (головку зуба срезают к внешнему торцу), впишите его в градусах: 12°06′ = 12,1. Диаметр вершин d_ae не меняется, меняются угол вершин, высота до хорды и 3D-модель.'},
 da2c:{l:'Угол конуса вершин колеса δa2 (с чертежа)',u:'°',g:'Геометрия',min:0,max:90,h:'0 — по расчёту. Впишите угол с чертежа в градусах: 83°07′ = 83,1167.'},
 E:{l:'Гипоидное смещение E',u:'мм',ag:LEN,g:'Геометрия',min:0,max:200,h:'Смещение оси шестерни относительно оси колеса. Не больше 0,2·D_к (при u ≥ 2,5). Станок 525: до 65 мм вниз / 75 вверх, 528С: 100 / 110.'},
 bsh:{l:'Угол спирали шестерни β_ш (желаемый)',u:'°',g:'Геометрия',min:20,max:60,h:'Обычно 50°. Угол спирали колеса получается меньше на угол ε′₁; если он больше 35°, уменьшите β_ш (например, 45°).'},
 asum:{l:'Сумма углов давления α_Σ',g:'Геометрия',sel:()=>[{v:'0',l:'Авто: 42°30′ (z₁ ≥ 8), иначе 45°'},{v:'1',l:'42°30′ — общее машиностроение'},{v:'2',l:'45° — грузовые, тракторы'},{v:'3',l:'38° — легковые автомобили'}],h:'Удвоенный средний угол давления. Влияет на профильные углы резцов и высоту зуба.'},
 rhd:{l:'Резцовая головка',g:'Геометрия',sel:()=>[{v:'0',l:'Подобрать по диаметру колеса'},{v:'1',l:'6″ (r 76,2)'},{v:'2',l:'7½″ (r 95,25)'},{v:'3',l:'9″ (r 114,3)'},{v:'4',l:'12″ (r 152,4)'}],h:'Номинальный радиус головки влияет на кривизну зуба и все наладки. Если радиус кривизны не сходится с радиусом головки — программа предупредит.'},
 mach:{l:'Станок для наладки',g:'Геометрия',sel:()=>[{v:'0',l:'525 (2E = 270)'},{v:'1',l:'528С (2E = 340)'},{v:'2',l:'5А27С1 (2E = 240)'}],h:'Станок с ненаклоняющимся шпинделем резцовой головки. От него зависят пределы гипоидного смещения, радиальные установки и модификатор.'},
 ca:{l:'Фланкирование головки C_a',u:'мкм',g:'Модификация профиля',min:0,max:500,h:'Срез профиля у вершины зуба (tip relief), начиная с 0,4·m от вершины. Компенсирует деформацию зубьев и погрешность шага: смягчает удар при входе в зацепление, снижает шум. Ориентир C_a ≈ (8…15)·m мкм. Моделируется в эскизе, DXF и STEP.'},
 crown:{l:'Бочкообразность C_β',u:'мкм',g:'Модификация профиля',min:0,max:200,h:'Продольная модификация: зуб тоньше к торцам на C_β. Снижает концентрацию нагрузки при перекосе валов — коэффициент K_Hβ уменьшается. Ориентир 10…25 мкм. В STEP не моделируется — указывается в таблице чертежа.'},
 x1:{l:'Смещение x₁',g:'Геометрия',min:-1,max:1.5,h:'Коэффициент смещения исходного контура шестерни. Положительное смещение устраняет подрез и упрочняет ножку зуба.'},
 x2:{l:'Смещение x₂',g:'Геометрия',min:-1,max:1.5,h:'Коэффициент смещения колеса. Сумма x₁ + x₂ изменяет межосевое расстояние.'},
 ha:{l:'Коэф. высоты головки h*ₐ',g:'Геометрия',min:0.5,max:1.5,h:'Коэффициент высоты головки зуба исходного контура. ГОСТ 13755 — 1,0. Укороченный зуб — 0,8…0,95 (берите из таблицы параметров на чертеже). Меняет d_a, d_f, высоту до хорды, перекрытие.'},
 cs:{l:'Коэф. радиального зазора c*',g:'Геометрия',min:0,max:0.5,h:'Коэффициент радиального зазора исходного контура. ГОСТ 13755 — 0,25. Глубина впадины от делительной окружности = (h*ₐ + c* − x)·m.'},
 b:{l:'Ширина венца b',u:'мм',ag:LEN,g:'Геометрия',min:1,max:1000,h:'Рабочая ширина зубчатого венца. Рекомендуется b ≈ (0,2…0,4)·a_w.'},
 L:{l:'Длина рейки',u:'мм',ag:LEN,g:'Геометрия',min:20,max:5000,h:'Длина зубчатой рейки — ограничивает ход.'},
 q:{l:'Коэффициент диаметра q',g:'Геометрия',min:6,max:25,h:'q = d₁ / m. Стандартный ряд ГОСТ 2144: 8; 10; 12,5; 16; 20.'},
 a:{l:'Межосевое (желаемое)',u:'мм',ag:LEN,g:'Геометрия',min:10,max:10000,h:'Желаемое межосевое расстояние. Фактическое уточняется по целому числу звеньев цепи или зубьев ремня.'},
 bw:{l:'Ширина ремня',u:'мм',ag:LEN,g:'Геометрия',min:3,max:200,h:'Ширина зубчатого ремня. Несущая способность пропорциональна ширине.'},
 l:{l:'Рабочая длина шлицев',u:'мм',ag:LEN,g:'Геометрия',min:3,max:500,h:'Длина контакта вала и ступицы.'},
 sig:{l:'Допускаемое смятие [σсм]',u:'МПа',g:'Нагрузка',min:1,max:400,h:'Неподвижное соединение стальных деталей — 60…120 МПа, подвижное под нагрузкой — 5…15 МПа.'},
 P:{l:'Мощность P',u:'кВт',ag:[1.341,'hp'],g:'Нагрузка',min:0.001,max:100000,h:'Мощность на ведущем валу.'},
 n1:{l:'Частота вращения n₁',u:'об/мин',g:'Нагрузка',min:0.1,max:100000,h:'Частота вращения ведущего вала.'},
 K:{l:'Внешняя динамика K_A',g:'Нагрузка',min:1,max:3,h:'Коэффициент внешней динамической нагрузки (ГОСТ 21354, K_A). Электродвигатель и равномерная нагрузка — 1,0…1,25; умеренные толчки — 1,25…1,5; сильные удары — 1,75 и выше. Внутренняя динамика K_v, концентрация K_β и распределение K_α считаются программой по скорости, ширине венца и степени точности.'},
 link:{l:'Вход ступени',g:'Нагрузка',sel:()=>[{v:'0',l:'Задать мощность и обороты'},{v:'1',l:'От предыдущей ступени (её выход)'}],h:'Многоступенчатый редуктор: мощность и частота на входе берутся с выходного вала предыдущего компонента сборки с учётом его КПД. Ведомое колесо предыдущей ступени и ведущее этой сидят на одном валу — так они и выгружаются в «Сборку в STEP».'},
 mode:{l:'Режим нагрузки',g:'Нагрузка',sel:()=>MODES.map((m,i)=>({v:String(i),l:m.n+' · μ_H = '+fmt(m.H,3)})),h:'Типовые режимы нагружения ГОСТ 21354-87: определяют эквивалентное число циклов N_E = μ·60·n·L_h. Постоянный — работа на полной нагрузке; средний равновероятный — типичный общепромышленный привод; лёгкий — большую часть времени недогружен.'},
 Lh:{l:'Требуемый ресурс L_h',u:'ч',g:'Нагрузка',int:1,min:100,max:500000,h:'Срок службы передачи в часах работы. Ориентиры: бытовые приводы — 2 000…5 000; редукторы общего назначения — 10 000…25 000; непрерывная работа в три смены — 40 000…60 000.'},
 f:{l:'Коэффициент трения f',g:'Нагрузка',min:0.01,max:0.2,h:'Пара сталь — бронза при хорошей смазке: 0,02…0,05.'},
 key:{l:'Отверстие под вал',g:'Вал и шпонка',sel:()=>[{v:'0',l:'Со шпоночным пазом ГОСТ 23360'},{v:'1',l:'Гладкое'},{v:'2',l:'Без отверстия (вал-шестерня)'}],h:'Призматическая шпонка по ГОСТ 23360-78: ширина b, высота h и глубина паза ступицы t₂ подбираются по диаметру вала. Отверстие и паз попадают в STEP, DXF и SVG.'},
 fan:{l:'Обдув корпуса',g:'Нагрузка',sel:()=>[{v:'0',l:'Нет (естественное охлаждение)'},{v:'1',l:'Вентилятор на валу червяка'}],h:'Тепловой расчёт червячного редуктора: вентилятор увеличивает теплоотдачу примерно в 1,6 раза.'},
 tA:{l:'Температура воздуха',u:'°C',g:'Нагрузка',min:-40,max:60,h:'Температура окружающей среды в цехе. Допустимая температура масла — 90 °C (кратковременно 95 °C).'},
 arr:{l:'Расположение колеса на валу',g:'Валы и подшипники',sel:()=>[{v:'0',l:'Между опорами, симметрично'},{v:'1',l:'Между опорами, несимметрично'},{v:'2',l:'Консольно (за опорой)'}],h:'Схема вала: колесо посередине между подшипниками, смещённое к одной опоре (a = 0,3·l) или на выступающем конце вала. Консольная схема даёт наибольшие нагрузки на опору и изгиб вала.'},
 span:{l:'Расстояние между опорами l',u:'мм',ag:LEN,g:'Валы и подшипники',min:0,max:5000,h:'Между серединами подшипников. 0 — оценить автоматически по ширине венца и подшипника; для червяка — по диаметру колеса.'},
 hand:{l:'Направление зуба шестерни',g:'Геометрия',sel:()=>[{v:'0',l:'Правое (колесо — левое)'},{v:'1',l:'Левое (колесо — правое)'}],h:'Берите из таблицы параметров на чертеже шестерни. У парного колеса внешнего зацепления направление всегда противоположное. Уходит в карту наладки 5К32А (выбор фрезы и угол головки), в чертёж и в 3D/STEP.'},
 brg:{l:'Тип подшипников',g:'Валы и подшипники',sel:()=>[{v:'0',l:'Подобрать автоматически'},{v:'1',l:'Шариковые радиальные ГОСТ 8338'},{v:'2',l:'Роликовые конические ГОСТ 27365'}],h:'Авто: при заметной осевой силе (косозубые, конические, червячные) — конические роликовые, иначе шариковые радиальные. Сначала лёгкая серия, при нехватке ресурса — средняя. Внутренний диаметр — по диаметру вала.'},
 rate:{l:'Стоимость станко-часа',u:'₽/ч',g:'Изготовление',min:0,max:100000,h:'Для оценки себестоимости: металл заготовки (масса × цена материала за кг) + время токарной обработки и зубонарезания × стоимость часа. Цены материалов задаются в окне «Материалы».'},
 hub:{l:'Конструкция колеса',g:'Вал и шпонка',sel:()=>[{v:'0',l:'Сплошное'},{v:'1',l:'Ступица и выточки'},{v:'2',l:'Выточки и отверстия облегчения'}],h:'Для колёс среднего и большого диаметра: ступица ⌀1,55·d × 1,2·d, диск C = 0,3·b, обод S ≥ 2,5·m. Снижает массу на 30–50 %. Попадает в STEP, DXF, SVG и чертёж. Если места мало — колесо остаётся сплошным.'},
 dv1:{l:'Вал шестерни d_в1',u:'мм',ag:LEN,g:'Вал и шпонка',min:0,max:500,h:'Диаметр отверстия под вал. 0 — подобрать автоматически по моменту ([τ] = 20 МПа) из ряда ГОСТ 6636. Если отверстие не помещается (обод тоньше 2,5·m), программа уменьшит его и предупредит.'},
 dv2:{l:'Вал колеса d_в2',u:'мм',ag:LEN,g:'Вал и шпонка',min:0,max:800,h:'Диаметр отверстия под вал ведомого колеса. 0 — подобрать автоматически по моменту на ведомом валу.'},
 acc:{l:'Степень точности',g:'Изготовление',sel:()=>ACC.map((a,i)=>({v:String(i),l:a.n+' ГОСТ 1643-81'})),h:'Цифра — степень точности: 7 — точные редукторы, 8 — общее машиностроение, 9 — тихоходные передачи. Буква — вид сопряжения (боковой зазор). Влияет на допуски и класс червячной фрезы.'},
 mat:{l:'Материал колёс',g:'Материал',sel:()=>MATS.map((m,i)=>({v:String(i),l:m.n})),h:'Определяет допускаемые контактные [σH] и изгибные [σF] напряжения.'},
 chain:{l:'Цепь (ГОСТ 13568)',g:'Геометрия',sel:()=>CHAINS.map((c,i)=>({v:String(i),l:c.n+' — шаг '+fmt(c.t,c.t%1?3:0).replace(/,?0+$/,'')+' мм'})),h:'Приводная роликовая однорядная цепь. Цифры: шаг, мм — разрушающая нагрузка, кН.'},
 prof:{l:'Профиль ремня',g:'Геометрия',sel:()=>BELTS.map((b,i)=>({v:String(i),l:b.n})),h:'HTD — полукруглый зуб, для больших нагрузок; T — трапециевидный; GT2 — для точного позиционирования.'}
};
const STD_INFO={'ГОСТ':'ГОСТ 13755, 16532, 21354 · мм','ISO':'ISO 53, 21771, 6336 · мм','AGMA':'AGMA 201, 2001 · дюймы'};
const METH={'ГОСТ':'ГОСТ 21354-87 · ресурс, K_v, K_β, K_α','ISO':'ISO 6336, метод B (по ГОСТ 21354)','AGMA':'по ГОСТ 21354, единицы AGMA'};
const TPL=[
 {n:'Редуктор цилиндрический, i = 4',t:'helical',p:{m:2.5,z1:20,z2:80,beta:12,b:32,P:7.5,n1:1450,mat:2}},
 {n:'Шевронная пара, β = 30°',t:'herring',p:{m:3,z1:22,z2:88,beta:30,b:90,gap:24,P:15,n1:1000,mat:1}},
 {n:'Глобоидная пара 1 : 40',t:'globoid',p:{}},
 {n:'Планетарный, 3 сателлита, i = 4',t:'planet',p:{m:1.5,zs:24,zp:24,N:3,b:18,P:4,n1:1450,mat:3}},
 {n:'Червячная пара 1 : 40',t:'worm',p:{m:5,z1:1,z2:40,q:10,b:40,P:2.2,n1:1450}},
 {n:'Коническая пара 1 : 2',t:'bevel',p:{m:3,z1:20,z2:40,b:24}},
 {n:'Коническая 8/48 по ГОСТ 19326 (23-340, МНЛЗ-2)',t:'bevel',p:{m:3.20414,z1:8,z2:48,alpha:20,betam:35,hand:1,b:25.5,g19:1,xn1:0.511,xt1:-0.011,axf:0,d0h:1,Rmc:59.40546,dha1:0.72,da1c:12.1,da2c:83.1167,mat:2,key:2}},
 {n:'Гипоидная пара 10/41, E 38,1 (пример СЗТЗС)',t:'hypoid',p:{m:5.11098,z1:10,z2:41,E:38.1,bsh:50,hand:1,b:31.75,mach:1}},
 {n:'Цепной привод ПР-15,875',t:'chain',p:{chain:2,z1:19,z2:38,a:600,P:4,n1:300}},
 {n:'Ремень HTD 5M, 1 : 2,5',t:'belt',p:{prof:2,z1:24,z2:60,a:220,bw:25,P:1.1,n1:1450}},
 {n:'Реечный привод m 3',t:'rack',p:{m:3,z1:18,b:30,L:400,P:2,n1:90}},
 {n:'Шлицы m 2 × z 18',t:'spline',p:{m:2,z:18,l:36,P:15,n1:1000}}
];
let UID=1;const nid=()=>Date.now().toString(36)+(UID++);

function pair(m,z1,z2,al,be,x1,x2,internal,ha=1,cc=0.25){ha=+ha;cc=+cc;if(!(ha>0))ha=1;if(!(cc>=0))cc=0.25;
 const mt=m/Math.cos(be),at=Math.atan(Math.tan(al)/Math.cos(be));
 const d1=mt*z1,d2=mt*z2,db1=d1*Math.cos(at),db2=d2*Math.cos(at);
 const zs=internal?z2-z1:z1+z2,xs=internal?x2-x1:x1+x2;
 const atw=Math.abs(xs)<1e-9?at:invInv(inv(at)+2*xs*Math.tan(al)/zs);
 const a0=internal?(d2-d1)/2:(d1+d2)/2,aw=a0*Math.cos(at)/Math.cos(atw);
 const dy=internal?0:xs-(aw-a0)/m;
 const da1=d1+2*m*(ha+x1-dy),df1=d1-2*m*(ha+cc-x1);
 let da2,df2;if(internal){da2=d2-2*m*(ha-x2);df2=d2+2*m*(ha+cc+x2);}else{da2=d2+2*m*(ha+x2-dy);df2=d2-2*m*(ha+cc-x2);}
 const ra1=da1/2,ra2=da2/2,rb1=db1/2,rb2=db2/2,pb=Math.PI*mt*Math.cos(at);
 const s1=Math.sqrt(Math.max(0,ra1*ra1-rb1*rb1)),s2=Math.sqrt(Math.max(0,ra2*ra2-rb2*rb2));
 const ea=internal?(s1-s2+aw*Math.sin(atw))/pb:(s1+s2-aw*Math.sin(atw))/pb;
 return {mt,at,atw,d1,d2,db1,db2,da1,da2,df1,df2,aw,a0,ea};
}
function strengthGear(o){
 const M=MATS[o.mat]||MATS[0],kb=o.kb||1,q=o.q||8,Lh=o.Lh||10000,MD=MODES[o.mode??2]||MODES[2],hard=!!M.hard;
 const T1=9550*o.P/o.n1,Ft=2000*T1/o.d1,v=Math.PI*o.d1*o.n1/60000,hel=o.beta>1e-6,bd=o.b/o.d1,ea=Math.min(Math.max(o.ea,1),2.5);
 const KHv=1+(hel?0.4:1)*(hard?0.04:0.05)*v*(q-4)/4,KFv=1+(hel?1.5:2)*(KHv-1),KHb=1+(hard?0.2:0.1)*bd*(o.crown>0?Math.max(0.5,1-o.crown/40):1),KFb=1+1.5*(KHb-1);
 const KHa=hel?Math.min(1.6,1+0.06*(q-5)):1,KFa=hel?(4+(ea-1)*(q-5))/(4*ea):1;
 const Ze=hel?Math.sqrt(1/ea):Math.sqrt((4-ea)/3),ZH=Math.sqrt(2*Math.cos(o.beta)/Math.sin(2*o.at)),sg=o.internal?-1:1,u=o.u,KA=o.K;
 const sH=190*ZH*Ze*Math.sqrt(Math.max(0,Ft*KA*KHv*KHb*KHa*(u+sg)/(o.b*o.d1*u*kb)));
 const yf=(z,x)=>3.47+13.2/z-27.9*x/z+0.092*x*x,cb=Math.cos(o.beta)**3,Yb=Math.max(0.7,1-o.beta/D2R/140),Ye=hel?1/ea:1,kF=Yb*Ye*Ft*KA*KFv*KFb*KFa/(o.b*o.m*kb);
 const sF1=yf(o.z1/cb,o.x1||0)*kF,sF2=yf(Math.min(o.z2,400)/cb,o.x2||0)*kF;
 const n2=u>1e4?o.n1:o.n1/u,c1=o.cyc||1,c2=o.cyc2||1,NHlim=Math.min(12e7,30*Math.pow(M.HB||200,2.4)),zmax=hard?1.8:2.6,qF=hard?9:6,ymax=hard?2.5:4,muF=hard?MD.F9:MD.F6;
 const allow=(nn,cc)=>{if(M.plast)return {H:M.H,F:M.F};const NH=60*nn*cc*Lh*MD.H,NF=60*nn*cc*Lh*muF;
  const ZN=NH<=NHlim?Math.min(zmax,Math.pow(NHlim/NH,1/6)):Math.max(0.75,Math.pow(NHlim/NH,1/20)),YN=NF<4e6?Math.min(ymax,Math.pow(4e6/NF,1/qF)):1;
  return {H:M.Hlim*ZN*0.95/M.SH,F:M.Flim*YN/M.SF};};
 const A1=allow(o.n1,c1),A2=allow(n2,c2);
 const lifeH=(sig,nn,cc)=>{const need=sig*M.SH/(M.Hlim*0.95);if(need<0.75)return Infinity;if(need>zmax)return 0;const N=NHlim*Math.pow(need,need<=1?-20:-6);return N/(60*nn*cc*MD.H);};
 const lifeF=(sig,nn,cc)=>{const need=sig*M.SF/M.Flim;if(need<=1)return Infinity;if(need>ymax)return 0;return 4e6*Math.pow(need,-qF)/(60*nn*cc*muF);};
 const Lres=M.plast?null:Math.min(lifeH(sH,o.n1,c1),lifeH(sH,n2,c2),lifeF(sF1,o.n1,c1),lifeF(sF2,n2,c2));
 return {T1,Ft,v,sH,sF1,sF2,M,KHv,KHb,KHa,KFv,KFb,KFa,aH:Math.min(A1.H,A2.H),aF1:A1.F,aF2:A2.F,Lres};
}
function chainGeo(p){const C=CHAINS[p.chain]||CHAINS[1],t=C.t,z1=p.z1,z2=p.z2,k=((z2-z1)/(2*Math.PI))**2;const Lt=2*p.a/t+(z1+z2)/2+k*t/p.a;let Lr=Math.ceil(Lt);const odd=Lr%2===1;if(odd)Lr++;const A=Lr-(z1+z2)/2;const a=t/4*(A+Math.sqrt(Math.max(0,A*A-8*k)));return {C,t,Lr,odd,a,Dd:z=>t/Math.sin(Math.PI/z),De:z=>t*(0.5+1/Math.tan(Math.PI/z))};}
function beltGeo(p){const B=BELTS[p.prof]||BELTS[2];const d1=B.p*p.z1/Math.PI,d2=B.p*p.z2/Math.PI;const L0=2*p.a+Math.PI*(d1+d2)/2+(d2-d1)**2/(4*p.a);const zb=Math.max(Math.round(L0/B.p),1),Lb=zb*B.p;const q=(Lb-Math.PI*(d1+d2)/2)/4;const a=q+Math.sqrt(Math.max(0,q*q-(d2-d1)**2/8));return {B,d1,d2,zb,Lb,a};}

const SHAFT=[6,7,8,9,10,11,12,14,16,17,18,19,20,22,24,25,26,28,30,32,34,36,38,40,42,45,48,50,53,56,60,63,65,70,75,80,85,90,95,100,105,110,120,125,130,140,150,160,170,180,190,200,220];
const KEYS=[[0,8,2,2,1.2,1],[8,10,3,3,1.8,1.4],[10,12,4,4,2.5,1.8],[12,17,5,5,3,2.3],[17,22,6,6,3.5,2.8],[22,30,8,7,4,3.3],[30,38,10,8,5,3.3],[38,44,12,8,5,3.3],[44,50,14,9,5.5,3.8],[50,58,16,10,6,4.3],[58,65,18,11,7,4.4],[65,75,20,12,7.5,4.9],[75,85,22,14,9,5.4],[85,95,25,14,9,5.4],[95,110,28,16,10,6.4],[110,130,32,18,11,7.4],[130,150,36,20,12,8.4],[150,170,40,22,13,9.4],[170,200,45,25,15,10.4],[200,1e4,50,28,17,11.4]];
const keyFor=d=>{const k=KEYS.find(r=>d>r[0]&&d<=r[1])||KEYS[0];return {b:k[2],h:k[3],t1:k[4],t2:k[5]};};
const rminOf=pts=>{let m=1e9;for(let i=0;i<pts.length;i+=2)m=Math.min(m,Math.hypot(pts[i],pts[i+1]));return m;};
function borePolygon(d,key){const r=d/2;if(!key)return Array.from({length:72},(_,i)=>[r*Math.cos(Math.PI*i/36),r*Math.sin(Math.PI*i/36)]);
 const hb=key.b/2,a0=Math.asin(Math.min(0.99,hb/r)),yt=r+key.t2,a1=Math.PI/2+a0,a2=Math.PI/2-a0+2*Math.PI,out=[];
 for(let i=0;i<=64;i++){const a=a1+(a2-a1)*i/64;out.push([r*Math.cos(a),r*Math.sin(a)]);}out.push([hb,yt],[-hb,yt]);return out;}
function shaftReq(c){const p=c.p,T1=9550*(p.P||0)/(p.n1||1);let i=('z2' in p&&p.z1)?p.z2/p.z1:1,eta=1;
 if(c.type==='worm'||c.type==='globoid'){const ga=Math.atan(p.z1/p.q),rho=Math.atan(p.f);eta=0.95*Math.tan(ga)/Math.tan(ga+rho);}
 return {1:{T:T1,d:p.dv1},2:{T:T1*i*eta,d:p.dv2}};}
function boreFor(c,role,rmin){const p=c.p,R=shaftReq(c)[role],mode=p.key||0;if(mode===2)return null;
 const dAuto=SHAFT.find(v=>v>=Math.cbrt(Math.max(R.T,0.01)*250))||220,dReq=R.d>0?R.d:dAuto,rim=p.m?2.5*p.m:Math.max(2,0.12*rmin);
 const fits=d=>d/2+(mode===0?keyFor(d).t2:0)<=rmin-rim;let d=dReq,clamped=false;
 if(!fits(d)){clamped=true;const ok=SHAFT.filter(v=>v<dReq&&fits(v));d=ok.length?ok[ok.length-1]:0;}
 if(!d)return {d:0,dReq,clamped:true,none:true,T:R.T};
 const key=mode===0?keyFor(d):null;return {d,dReq,auto:!(R.d>0),clamped,key,T:R.T,pts:borePolygon(d,key)};}
function hubFor(c,rmin,bi,b){const p=c.p,mode=p.hub||0;if(!mode)return null;const m=p.m||rmin*0.05;
 const S=Math.max(2.5*m,0.05*b+2.2*m),Rr=Math.floor((rmin-S)*2)/2,Rh=Math.ceil(Math.max(1.55*bi.d/2,bi.d/2+(bi.key?bi.key.t2:0)+0.2*bi.d)),lst=Math.ceil(Math.max(b,1.2*bi.d)),C=Math.round(Math.max(0.3*b,3)*2)/2,e=(b-C)/2;
 if(Rr-Rh<Math.max(4*m,8)||e<1)return {none:true};
 let holes=null;if(mode===2){const R0=Math.round((Rr+Rh)/2),w=Rr-Rh,n=R0>60?6:4,d0=Math.floor(Math.min(0.55*w,Math.PI*R0/n));if(d0>=5)holes={n,R0,d0};}
 return {S:rmin-Rr,Rr,Rh,dst:2*Rh,lst,C,e,holes};}
function boresList(c,std){if(c.type==='hypoid')return boresList({...c,type:'bevel',p:hypToBevel(c.p)},std);const p=c.p,t=c.type,NM={1:t==='planet'?'солнца':t==='chain'?'ведущей звёздочки':t==='belt'?'ведущего шкива':'шестерни',2:t==='chain'?'ведомой звёздочки':t==='belt'?'ведомого шкива':'колеса'};
 if(t==='spline')return [];
 if(t==='bevel'){const al=p.alpha*D2R,d1r=Math.atan(p.z1/p.z2),Re=0.5*p.m*Math.hypot(p.z1,p.z2),lam=(Re-Math.min(p.b,Re*0.45))/Re;return [[1,p.z1,d1r],[2,p.z2,Math.PI/2-d1r]].map(([r,z,dl])=>({role:r,name:NM[r],bi:boreFor(c,r,lam*rminOf(bevelPts(z,p.m,al,dl)))}));}
 return buildScene(c,0,std).bores.map(x=>({role:x.role,name:NM[x.role],bi:x.bi,hub:x.hub}));}
const ROLLS=[1,1.25,1.5,1.75,2,2.25,2.5,3,3.5,4,4.5,5,5.5,6,7,8,9,10,11,12,14,16,18,20,22,25,28,32];
function gearCtl(m,z,x,al,be,da,b){
 const at=Math.atan(Math.tan(al)/Math.cos(be)),bb=Math.asin(Math.sin(be)*Math.cos(al)),ax=Math.acos(Math.min(1,z*Math.cos(at)/(z+2*x*Math.cos(be))));
 let zw=Math.round(z/Math.PI*(Math.tan(ax)/Math.cos(bb)**2-2*x*Math.tan(al)/z-inv(at))+0.5);zw=Math.max(2,Math.min(zw,z-1));
 const W=m*Math.cos(al)*(Math.PI*(zw-0.5)+2*x*Math.tan(al)+z*inv(at)),D=ROLLS.find(v=>v>=1.68*m)||+(1.7*m).toFixed(2);
 const aM=invInv(inv(at)+D/(m*z*Math.cos(al))+2*x*Math.tan(al)/z-Math.PI/(2*z)),dM=m*z/Math.cos(be)*Math.cos(at)/Math.cos(aM);
 const M=z%2===0?dM+D:dM*Math.cos(Math.PI/(2*z))+D,sc=m*(Math.PI/2*Math.cos(al)**2+x*Math.sin(2*al)),d=m*z/Math.cos(be),hc=0.5*(da-d-sc*Math.tan(al));
 return {zw,W,D,M,sc,hc,s:m*(Math.PI/2+2*x*Math.tan(al)),Wok:be?b>W*Math.sin(bb)+5:true};}
function ftBl(q,b,d){const T={5:[16,8,5.1,4],6:[20,10,6.4,5],7:[24,12,8,6],8:[40,20,12.8,10],9:[64,32,20,16],10:[100,50,32,25],11:[160,80,50,40],12:[250,125,80,60]},r=T[q]||T[8];return r[b<=40?0:b<=100?1:b<=160?2:3]*d/100;}
function tolISO(m,d,b,Q){const k=Math.pow(2,(Q-5)/2),R=v=>Math.round(v*k);return {fpt:R(0.3*(m+0.4*Math.sqrt(d))+4),Fp:R(0.3*m+1.25*Math.sqrt(d)+7),Fa:R(3.2*Math.sqrt(m)+0.22*Math.sqrt(d)+0.7),Fb:R(0.1*Math.sqrt(d)+0.63*Math.sqrt(b)+4.2),Fr:R(0.24*m+Math.sqrt(d)+5.6)};}
function partMass(c,std){const S=buildScene(c,0,std),out=[],ar=a=>{let s=0;for(let i=0;i<a.length;i+=2){const j=(i+2)%a.length;s+=a[i]*a[j+1]-a[j]*a[i+1];}return Math.abs(s)/2;};
 for(const sh of S.shapes){if(!sh.role)continue;const b=S.depth,dv=(sh.hole||0)*2,hb=sh.hub&&!sh.hub.none?sh.hub:null;let V=(ar(sh.pts)-(sh.bore?ar(sh.bore.flat()):0))*b;
  if(hb){V-=2*hb.e*Math.PI*(hb.Rr**2-hb.Rh**2);V+=(hb.lst-b)*Math.PI*(hb.Rh**2-(dv/2)**2);if(hb.holes)V-=hb.holes.n*Math.PI*(hb.holes.d0/2)**2*(b-2*hb.e);}
  let Ro=0;for(let i=0;i<sh.pts.length;i+=2)Ro=Math.max(Ro,Math.hypot(sh.pts[i],sh.pts[i+1]));out.push({role:sh.role,V,b,Ro,lst:hb?hb.lst:b,z:sh.pts.opt?sh.pts.opt.z:(sh.pts.length/60|0)});}
 return out;}
const cutterNo=zv=>zv<14?1:zv<17?2:zv<21?3:zv<26?4:zv<35?5:zv<55?6:zv<135?7:8;
function calc(c,std){
 const HER=c.type==='herring',p=HER?{...c.p,b:Math.max(1,c.p.b-c.p.gap)}:c.p,t=HER?'helical':c.type,g=[],s=[],w=[],ctl=[],AG=std==='AGMA';
 const GL=(l,v)=>g.push({l,v:fmt(AG?v/25.4:v,AG?4:2),u:AG?'дюйм':'мм'});
 const G=(l,v,u,d)=>g.push({l,v:typeof v==='string'?v:fmt(v,d??3),u:u||''});
 const ok=S=>S>=1.05?'ok':S>=0.95?'warn':'bad';
 const SS=(l,v,stt)=>s.push({l,v:fmt(AG?v*145.04:v,AG?0:1),u:AG?'psi':'МПа',st:stt||''});
 const SF=(l,v)=>s.push({l,v:fmt(AG?v*0.2248:v,0),u:AG?'lbf':'Н',st:''});
 const ST=(l,v)=>s.push({l,v:fmt(AG?v*8.851:v,AG?0:1),u:AG?'lbf·дюйм':'Н·м',st:''});
 const SV=(l,v,u,d,stt)=>s.push({l,v:fmt(v,d??2),u:u||'',st:stt||''});
 const W=(lvl,tx)=>w.push({lvl,t:tx});
 const LU=v=>AG?fmt(v/25.4,3)+' дюйм':fmt(v,2)+' мм';
 let ratio=1,sum='',noN2=false;
 const gearStr=(o,lab1,lab2)=>{const r=strengthGear({...o,q:(ACC[p.acc]||ACC[1]).q,Lh:p.Lh,mode:p.mode});ST('Момент на ведущем T₁',r.T1);SF('Окружная сила F_t',r.Ft);SV('Окружная скорость v',r.v,'м/с');
  s.push({l:'K_Hv · K_Hβ · K_Hα',v:[r.KHv,r.KHb,r.KHa].map(q=>fmt(q,2)).join(' · '),u:'',st:''});s.push({l:'K_Fv · K_Fβ · K_Fα',v:[r.KFv,r.KFb,r.KFa].map(q=>fmt(q,2)).join(' · '),u:'',st:''});
  const SH=r.aH/r.sH,F1=r.aF1/r.sF1,F2=r.aF2/r.sF2,SFm=Math.min(F1,F2);
  SS('Контактное напряжение σ_H',r.sH,ok(SH));SS('Допускаемое [σ_H] на ресурс',r.aH);SS('Изгиб σ_F, '+lab1,r.sF1,ok(F1));SS('Допускаемое [σ_F], '+lab1,r.aF1);SS('Изгиб σ_F, '+lab2,r.sF2,ok(F2));SS('Допускаемое [σ_F], '+lab2,r.aF2);
  SV('Запас по контакту S_H',SH,'',2,ok(SH));SV('Запас по изгибу S_F',SFm,'',2,ok(SFm));
  if(r.Lres!=null){const L=r.Lres,req=p.Lh||10000;s.push({l:'Расчётный ресурс до усталости',v:L===Infinity?'не ограничен':L>=1e6?'> 1 000 000':Math.round(L).toLocaleString('ru-RU'),u:L===Infinity?'':'ч',st:L>=req?'ok':L>=0.8*req?'warn':'bad'});
   if(L<0.8*req)W('bad',`Ресурс ${L<1?'менее 1':Math.round(L).toLocaleString('ru-RU')} ч меньше требуемого ${req.toLocaleString('ru-RU')} ч.`);}
  if(SH<0.95)W('bad','Контактная прочность не обеспечена: увеличьте модуль, ширину венца или выберите более твёрдый материал.');
  if(SFm<0.95)W('bad','Изгибная прочность зуба не обеспечена: увеличьте модуль или положительное смещение.');
  return r;};
 const undercut=(z,x,at,be,name)=>{const zmin=2*(1-x)*Math.cos(be)/Math.sin(at)**2;if(z<zmin-1e-6){const xn=1-z*Math.sin(at)**2/(2*Math.cos(be));W('warn',`Подрез ножки зуба ${name}: z = ${z} меньше z_min ≈ ${fmt(zmin,1)}. Задайте смещение x ≥ ${fmt(xn,2)}.`);}};
 if(t==='spur'||t==='helical'||t==='internal'){
  const inn=t==='internal',be=t==='helical'?p.beta*D2R:0,al=p.alpha*D2R,x2=inn?0:p.x2;
  const r=pair(p.m,p.z1,p.z2,al,be,p.x1,x2,inn,p.ha??1,p.cs??0.25);ratio=p.z2/p.z1;
  G('Передаточное число u',ratio);GL('Межосевое расстояние a_w',r.aw);
  if(Math.abs(r.aw-r.a0)>1e-4)GL('Делительное межосевое a',r.a0);
  if(be)GL('Торцовый модуль m_t',r.mt);
  GL('Делительный диаметр d₁',r.d1);GL('Делительный диаметр d₂',r.d2);GL('Диаметр вершин d_a1',r.da1);GL('Диаметр вершин d_a2',r.da2);GL('Диаметр впадин d_f1',r.df1);GL('Диаметр впадин d_f2',r.df2);GL('Основной диаметр d_b1',r.db1);GL('Основной диаметр d_b2',r.db2);
  G('Угол зацепления α_tw',r.atw/D2R,'°');G('Торцовое перекрытие ε_α',r.ea);
  G('КПД зацепления η',100*(1-0.5*Math.PI*0.06*r.ea*(1/p.z1+(inn?-1:1)/p.z2)),'%',2);
  if(be){if(HER){GL('Рабочая ширина b − g',p.b);GL('Ширина полушеврона',p.b/2);if(p.beta<20)W('info','Для шевронных колёс обычно β = 25…40° — при малом угле преимущество перед косозубой невелико.');}const eb=(HER?p.b/2:p.b)*Math.sin(be)/(Math.PI*p.m);G('Осевое перекрытие ε_β',eb);if(eb<1)W('info','Осевое перекрытие ε_β < 1 — увеличьте ширину венца или угол β для плавности.');}
  GL('Шаг по делительной p',Math.PI*r.mt);
  const HA=p.ha??1,CS=p.cs??0.25,F1=filletOf({z:p.z1,m:p.m,mt:r.mt,at:r.at,x:p.x1,ha:HA,cc:CS}),F2=inn?null:filletOf({z:p.z2,m:p.m,mt:r.mt,at:r.at,x:x2,ha:HA,cc:CS});
  if(Math.abs(HA-1)>1e-6||Math.abs(CS-0.25)>1e-6){G('Коэф. высоты головки h*ₐ',HA,'',3);G('Коэф. радиального зазора c*',CS,'',3);const dfH=r.d1-2*p.m*(1.25-p.x1);W('info',`Нестандартный исходный контур: h*ₐ = ${fmt(HA,3)}, c* = ${fmt(CS,3)} (ГОСТ 13755: 1 и 0,25). Стандартная червячная фреза (головка 1,25·m) при смещении x₁ = ${fmt(p.x1,3)} даст d_f1 ≈ ${fmt(dfH,2)} мм — глубину набирайте по W, а не по d_f.`);}
  if(F1||F2)GL('Радиус переходной кривой ρ_f',(F1||F2).rho);if(F1)GL('Граничный диаметр d_l1',2*F1.rE);if(F2)GL('Граничный диаметр d_l2',2*F2.rE);
  if(!inn){const rp1=Math.sqrt(r.db1**2/4+(r.aw*Math.sin(r.atw)-Math.sqrt(Math.max(0,r.da2**2/4-r.db2**2/4)))**2),rp2=Math.sqrt(r.db2**2/4+(r.aw*Math.sin(r.atw)-Math.sqrt(Math.max(0,r.da1**2/4-r.db1**2/4)))**2);
   GL('Нижняя точка активного профиля d_p1',2*rp1);GL('Нижняя точка активного профиля d_p2',2*rp2);
   if(F1&&rp1<F1.rE-1e-3)W('bad',`Интерференция: вершина колеса заходит на переходную кривую шестерни (d_p1 ${fmt(2*rp1,2)} < d_l1 ${fmt(2*F1.rE,2)}). Увеличьте x₁ или уменьшите x₂.`);
   if(F2&&rp2<F2.rE-1e-3)W('bad',`Интерференция: вершина шестерни заходит на переходную кривую колеса (d_p2 ${fmt(2*rp2,2)} < d_l2 ${fmt(2*F2.rE,2)}). Увеличьте x₂.`);}
  undercut(p.z1,p.x1,r.at,be,'шестерни');if(!inn)undercut(p.z2,x2,r.at,be,'колеса');
  if(r.ea<1.1)W('bad',`Коэффициент перекрытия ε_α = ${fmt(r.ea,2)} меньше 1,1 — непрерывность зацепления не обеспечена.`);else if(r.ea<1.2)W('warn',`ε_α = ${fmt(r.ea,2)} — минимально допустимое значение.`);
  if(inn&&p.z2-p.z1<10)W('bad','Разность z₂ − z₁ меньше 10: интерференция вершин при внутреннем зацеплении.');
  const sr=gearStr({d1:r.d1,u:ratio,b:p.b,m:p.m,ea:r.ea,beta:be,at:r.at,z1:p.z1,z2:p.z2,x1:p.x1,x2,P:p.P,n1:p.n1,mat:p.mat,K:p.K,internal:inn,crown:p.crown},'шестерня','колесо');
  if(be&&!HER){const Fa=sr.Ft*Math.tan(be);SF('Осевая сила F_a',Fa);}
  if(t==='spur'&&sr.v>6)W('info',`Окружная скорость ${fmt(sr.v,1)} м/с — для снижения шума рекомендуется косозубая передача.`);
  if(p.b/r.aw>0.6)W('info','Ширина венца больше 0,6·a_w — проверьте перекос валов.');
  sum=`u = ${fmt(ratio,3)} · a_w = ${LU(r.aw)}`;
 } else if(t==='rack'){
  const al=p.alpha*D2R,d1=p.m*p.z1,da=d1+2*p.m*(1+p.x1),df=d1-2*p.m*(1.25-p.x1),stroke=Math.PI*d1;
  const ea=(Math.sqrt((da/2)**2-(d1*Math.cos(al)/2)**2)-d1/2*Math.sin(al)+p.m*(1-p.x1)/Math.sin(al))/(Math.PI*p.m*Math.cos(al));
  GL('Делительный диаметр d',d1);GL('Диаметр вершин d_a',da);GL('Диаметр впадин d_f',df);GL('Шаг рейки p',Math.PI*p.m);GL('Ход рейки за оборот',stroke);GL('Высота до делительной линии рейки',d1/2+p.x1*p.m);G('Перекрытие ε_α',ea);
  const vr=stroke*p.n1/60000;G('Скорость рейки',vr,'м/с',3);G('Оборотов на полный ход',p.L/stroke,'',2);
  undercut(p.z1,p.x1,al,0,'шестерни');
  gearStr({d1,u:1e6,b:p.b,m:p.m,ea,beta:0,at:al,z1:p.z1,z2:400,x1:p.x1,x2:0,P:p.P,n1:p.n1,mat:p.mat,K:p.K},'шестерня','рейка');
  sum=`ход за оборот ${LU(stroke)} · v = ${fmt(vr,3)} м/с`;noN2=true;
 } else if(t==='bevel'){
  const al=p.alpha*D2R,u=p.z2/p.z1,d1r=Math.atan(1/u),d2r=Math.PI/2-d1r,Re=0.5*p.m*Math.hypot(p.z1,p.z2),kbe=p.b/Re;
  const de1=p.m*p.z1,de2=p.m*p.z2,dm1=de1*(1-0.5*kbe),mm=p.m*(1-0.5*kbe);ratio=u;
  G('Передаточное число u',u);G('Угол делительного конуса δ₁',d1r/D2R,'°');G('Угол делительного конуса δ₂',d2r/D2R,'°');GL('Внешнее конусное расстояние R_e',Re);
  GL('Внешний делительный d_e1',de1);GL('Внешний делительный d_e2',de2);if(!g19On(p)){GL('Диаметр вершин d_ae1',de1+2*p.m*Math.cos(d1r));GL('Диаметр вершин d_ae2',de2+2*p.m*Math.cos(d2r));GL('Средний делительный d_m1',dm1);GL('Средний модуль m_m',mm);}
  G('Эквивалентное z_v1',p.z1/Math.cos(d1r),'',1);G('Эквивалентное z_v2',p.z2/Math.cos(d2r),'',1);G('Коэффициент ширины K_be',kbe);
  if(kbe>0.3)W('warn',`K_be = ${fmt(kbe,2)} > 0,3 — уменьшите ширину венца b.`);
  {}
  if(g19On(p))calcBevelG19(p,G,GL,W);else undercut(Math.round(p.z1/Math.cos(d1r)),0,al,0,'шестерни (по эквивалентному z_v)');
  const vz1=p.z1/Math.cos(d1r),vz2=p.z2/Math.cos(d2r);const bm=(p.betam||0)*D2R,ea=pair(mm,vz1,vz2,al,bm,0,0,false).ea;let tolDa='';{const ACq=(ACC[p.acc]||ACC[1]).q,mn=mm*Math.cos(bm),dmm=dm1,r=ACq<=5?0:ACq<=8?1:2,T3=[[70,35,70,35,110,55,150,75,200,100],[100,50,100,50,150,75,200,100,250,125],[150,75,150,75,200,100,250,125,300,150]][r],c=mn<=3.5?0:mn<=6.3?1:mn<=10?2:mn<=16?3:4,T4=mn<=1.5?[15,30]:mn<=10?[12,25]:[8,15],T5={5:[18,25,30,35],6:[25,32,40,45],7:[32,40,50,55],8:[40,50,60,65],9:[50,60,70,80],10:[60,70,80,95]}[ACq]||[40,50,60,65],cd=x=>x<=125?0:x<=400?1:x<=800?2:3;
   G('Заготовка: допуск d_ae (Хлебалин, табл. 3)',`−${fmt(T3[2*c]/1000,3)}`,'мм');G('Заготовка: допуск C (торец — окружность вершин)',`−${fmt(T3[2*c+1]/1000,3)}`,'мм');G('Заготовка: допуск угла конуса вершин δ_a',`+${T4[0]}′`,'');G('Заготовка: допуск угла доп. конуса',`±${T4[1]}′`,'');G('Заготовка: биение конуса вершин E_a шестерни / колеса',`≤ ${T5[cd(dmm)]} / ${T5[cd(dmm*u)]}`,'мкм');tolDa='−'+fmt(T3[2*c]/1000,3);}
  blankLines(c,std,G,GL,tolDa);
  if(bm){G('Средний угол наклона β_m',p.betam,'°',1);G('Тип зуба',p.betam>=25?'круговой':'косой','');}
  gearStr({d1:dm1,u,b:p.b,m:mm,ea,beta:bm,at:Math.atan(Math.tan(al)/Math.cos(bm)),z1:vz1,z2:vz2,P:p.P,n1:p.n1,mat:p.mat,K:p.K,kb:bm?Math.min(2,1.22+0.21*u):0.85},'шестерня','колесо');
  sum=`u = ${fmt(u,3)} · R_e = ${LU(Re)}`;
 } else if(t==='hypoid'){const r=calcHypoid(p,G,GL,W,gearStr,undercut,AG,LU);ratio=r.ratio;sum=r.sum;blankLines(c,std,G,GL,'');
 } else if(t==='worm'){
  const d1=p.q*p.m,d2=p.z2*p.m,aw=0.5*p.m*(p.q+p.z2),ga=Math.atan(p.z1/p.q),i=p.z2/p.z1;ratio=i;
  const da1=d1+2*p.m,df1=d1-2.4*p.m,da2=d2+2*p.m,daM2=da2+6*p.m/(p.z1+2),rho=Math.atan(p.f),eta=0.95*Math.tan(ga)/Math.tan(ga+rho);
  const vs=Math.PI*d1*p.n1/(60000*Math.cos(ga)),T2=9550*p.P*eta*i/p.n1,sH=(480/d2)*Math.sqrt(T2*1000*p.K/d1);
  const allow=Math.max(110,230*Math.min(1.25,1.66*Math.pow(Math.max(vs,0.2),-0.352)));
  G('Передаточное число i',i);GL('Межосевое расстояние a_w',aw);GL('Делительный диаметр червяка d₁',d1);GL('Диаметр вершин червяка d_a1',da1);GL('Диаметр впадин червяка d_f1',df1);GL('Делительный диаметр колеса d₂',d2);GL('Диаметр вершин колеса d_a2',da2);GL('Наибольший диаметр колеса d_aM2',daM2);G('Угол подъёма витка γ',ga/D2R,'°');GL('Осевой шаг p',Math.PI*p.m);GL('Ход витка p_z',Math.PI*p.m*p.z1);
  ST('Момент на колесе T₂',T2);SF('Окружная сила на колесе F_t2',2000*T2/d2);SV('Скорость скольжения v_s',vs,'м/с');SV('КПД η',eta*100,'%',1);SV('Потери в тепло',p.P*(1-eta),'кВт',2);
  SS('Контактное напряжение σ_H',sH,ok(allow/sH));SS('Допускаемое [σ_H] (бронза)',allow);SV('Запас по контакту S_H',allow/sH,'',2,ok(allow/sH));
  {const A=20*Math.pow(aw/1000,1.7),Kt=(p.fan?1.6:1)*14,tA=p.tA??20,tm=tA+1000*p.P*(1-eta)/(Kt*A*1.3),Areq=1000*p.P*(1-eta)/(Kt*1.3*Math.max(1,90-tA));
   SV('Поверхность охлаждения корпуса A',A,'м²',2);SV('Температура масла t_м',tm,'°C',0,tm<=90?'ok':tm<=95?'warn':'bad');
   if(tm>95)W('bad',`Перегрев: масло нагреется до ${fmt(tm,0)} °C (допустимо 90 °C). ${p.fan?'':'Установите вентилятор на вал червяка, '}увеличьте корпус до A ≈ ${fmt(Areq,2)} м², добавьте рёбра или змеевик охлаждения.`);
   else if(tm>90)W('warn',`Масло нагреется до ${fmt(tm,0)} °C — на пределе.`);}
  if(ga<rho)W('info','Передача самотормозящая: γ меньше угла трения — колесо не сможет вращать червяк.');
  if(![1,2,4].includes(p.z1))W('warn','По ГОСТ 2144 число заходов червяка — 1, 2 или 4.');
  if(p.z2<26)W('warn','z₂ < 26 — возможен подрез зубьев колеса.');
  if(allow/sH<0.95)W('bad','Контактная прочность венца не обеспечена: увеличьте модуль или межосевое расстояние.');
  sum=`i = ${fmt(i,2)} · a_w = ${LU(aw)} · η = ${fmt(eta*100,0)} %`;
 } else if(t==='globoid'){
  const Gg=globoGeo(p),d1=2*Gg.r1,d2=2*Gg.r2,aw=Gg.aw,ga=Gg.ga,i=Gg.i;ratio=i;
  const rho=Math.atan(p.f),eta=0.95*Math.tan(ga)/Math.tan(ga+rho),vs=Math.PI*d1*p.n1/(60000*Math.cos(ga)),T2=9550*p.P*eta*i/p.n1,kg=Math.max(1.3,Math.min(2,0.35*Gg.zc)),sH=(480/d2)*Math.sqrt(T2*1000*p.K/(d1*kg));
  const allow=Math.max(110,230*Math.min(1.25,1.66*Math.pow(Math.max(vs,0.2),-0.352))),dae=2*(aw-Math.sqrt(Math.max(0,Gg.Ra**2-(Gg.b1/2)**2))),df1=d1-2*Gg.hf;
  G('Передаточное число i',i);GL('Межосевое расстояние a_w',aw);GL('Делительный диаметр червяка d₁ (в горле)',d1);GL('Диаметр вершин червяка d_a1 (в горле)',d1+2*Gg.ha);GL('Наибольший диаметр червяка d_ae1',dae);GL('Диаметр впадин червяка d_f1 (в горле)',df1);
  GL('Делительный диаметр колеса d₂',d2);GL('Диаметр вершин колеса d_a2',d2+2*Gg.ha);GL('Диаметр впадин колеса d_f2',d2-2*Gg.hf);GL('Расчётный (основной) диаметр d_b',2*Gg.rb);
  G('Зубьев колеса в обхвате z′',String(Gg.zc),'');G('Угол обхвата 2Φ',2*Gg.Phi/D2R,'°',1);GL('Длина нарезки червяка b₁',Gg.b1);G('Угол подъёма в горле γ',ga/D2R,'°');GL('Высота головки h_a',Gg.ha);GL('Осевой шаг p',Math.PI*p.m);GL('Ход витка p_z',Math.PI*p.m*p.z1);
  ST('Момент на колесе T₂',T2);SF('Окружная сила на колесе F_t2',2000*T2/d2);SV('Скорость скольжения v_s',vs,'м/с');SV('КПД η',eta*100,'%',1);SV('Потери в тепло',p.P*(1-eta),'кВт',2);
  SV('Коэффициент многопарности K_гл',kg,'',2);SS('Контактное напряжение σ_H',sH,ok(allow/sH));SS('Допускаемое [σ_H] (бронза)',allow);SV('Запас по контакту S_H',allow/sH,'',2,ok(allow/sH));
  {const A=20*Math.pow(aw/1000,1.7),Kt=(p.fan?1.6:1)*14,tA=p.tA??20,tm=tA+1000*p.P*(1-eta)/(Kt*A*1.3),Areq=1000*p.P*(1-eta)/(Kt*1.3*Math.max(1,90-tA));
   SV('Поверхность охлаждения корпуса A',A,'м²',2);SV('Температура масла t_м',tm,'°C',0,tm<=90?'ok':tm<=95?'warn':'bad');
   if(tm>95)W('bad',`Перегрев: масло нагреется до ${fmt(tm,0)} °C (допустимо 90 °C). ${p.fan?'':'Установите вентилятор на вал червяка, '}увеличьте корпус до A ≈ ${fmt(Areq,2)} м², добавьте рёбра или змеевик охлаждения.`);
   else if(tm>90)W('warn',`Масло нагреется до ${fmt(tm,0)} °C — на пределе.`);}
  if(ga<rho)W('info','Передача самотормозящая: γ меньше угла трения — колесо не сможет вращать червяк.');
  if(![1,2,3,4].includes(p.z1))W('warn','Для глобоидных передач число заходов червяка — 1…4.');
  if(i<10||i>63)W('info',`Глобоидные передачи обычно применяют при i = 10…63 (сейчас ${fmt(i,1)}).`);
  if(p.z2<30)W('warn','z₂ < 30 — мал обхват, преимущество глобоидной передачи теряется.');
  if(p.b>df1)W('warn',`Ширина колеса b = ${fmt(p.b,1)} больше d_f1 = ${fmt(df1,1)} мм — рекомендуется b ≈ (0,9…1,0)·d_f1.`);
  if(allow/sH<0.95)W('bad','Контактная прочность венца не обеспечена: увеличьте модуль или межосевое расстояние.');
  W('info','STEP: червяк выгружается сердечником и витками отдельными телами — во Fusion 360 объедините их командой Combine → Join (Объединить).');
  W('info','Глобоидный червяк очень чувствителен к осевому положению: при сборке выставляйте его по горлу (середине нарезки) с точностью ±0,02…0,05 мм.');
  sum=`i = ${fmt(i,2)} · a_w = ${LU(aw)} · z′ = ${Gg.zc} · η = ${fmt(eta*100,0)} %`;
 } else if(t==='planet'){
  const al=p.alpha*D2R,zr=p.zs+2*p.zp,i=1+zr/p.zs;ratio=i;const r=pair(p.m,p.zs,p.zp,al,0,0,0,false);
  G('Передаточное число i (солнце → водило)',i);G('Зубьев короны z_b',String(zr),'');GL('Межосевое a_w (радиус водила)',r.aw);GL('Диаметр солнца d_a',r.d1);GL('Диаметр сателлита d_g',r.d2);GL('Диаметр короны d_b',p.m*zr);GL('Вершины солнца',r.da1);GL('Вершины сателлита',r.da2);GL('Вершины короны (внутр.)',p.m*zr-2*p.m);G('Перекрытие солнце–сателлит ε_α',r.ea);
  {const ps1=0.5*Math.PI*0.06*r.ea*(1/p.zs+1/p.zp),ps2=0.5*Math.PI*0.06*1.7*(1/p.zp-1/zr);G('КПД передачи η',100*(1-(ps1+ps2)*zr/(p.zs+zr)),'%',2);}
  if((p.zs+zr)%p.N!==0)W('bad',`Условие сборки не выполнено: (z_a + z_b) / n_w = ${fmt((p.zs+zr)/p.N,2)} — не целое. Измените числа зубьев или число сателлитов.`);
  if((p.zs+p.zp)*Math.sin(Math.PI/p.N)<=p.zp+2)W('bad','Условие соседства нарушено: вершины соседних сателлитов задевают друг друга.');
  undercut(p.zs,0,al,0,'солнца');
  gearStr({d1:r.d1,u:p.zp/p.zs,b:p.b,m:p.m,ea:r.ea,beta:0,at:al,z1:p.zs,z2:p.zp,P:p.P/p.N*1.15,n1:p.n1,mat:p.mat,K:p.K,cyc:p.N},'солнце','сателлит');
  s.unshift({l:'Нагрузка на 1 сателлит (с неравномерностью 1,15)',v:fmt(1.15/p.N*100,0),u:'%',st:''});
  sum=`i = ${fmt(i,3)} · z_b = ${zr} · n_w = ${p.N}`;
 } else if(t==='chain'){
  const k=chainGeo(p),C=k.C;ratio=p.z2/p.z1;const v=p.z1*k.t*p.n1/60000,Ft=1000*p.P/v,S=C.Q*1000/(Ft*1.3+ 0.7*v*v);
  G('Передаточное число i',ratio);GL('Шаг цепи t',k.t);GL('Делительный диаметр ведущей',k.Dd(p.z1));GL('Делительный диаметр ведомой',k.Dd(p.z2));GL('Диаметр вершин ведущей',k.De(p.z1));GL('Диаметр вершин ведомой',k.De(p.z2));G('Число звеньев цепи',String(k.Lr),'шт');GL('Фактическое межосевое a',k.a);GL('Длина цепи',k.Lr*k.t);
  SV('Скорость цепи v',v,'м/с');SF('Окружная сила F_t',Ft);SF('Разрушающая нагрузка цепи',C.Q*1000);SV('Запас прочности S',S,'',1,S>=8?'ok':S>=6?'warn':'bad');
  if(k.odd)W('info','Число звеньев округлено до чётного — переходное звено не требуется.');
  if(p.z1<13)W('warn','Малая звёздочка: при z < 13 резко растёт износ и неравномерность хода.');
  if(v>15)W('warn','Скорость цепи выше 15 м/с — нужна принудительная смазка.');
  if(S<6)W('bad','Запас прочности цепи мал — выберите цепь с большим шагом или двухрядную.');
  if(ratio>7)W('info','Передаточное число больше 7 — рассмотрите двухступенчатую передачу.');
  sum=`i = ${fmt(ratio,3)} · ${k.Lr} звеньев · a = ${LU(k.a)}`;
 } else if(t==='belt'){
  const k=beltGeo(p),B=k.B;ratio=p.z2/p.z1;const th1=Math.PI-2*Math.asin(Math.min(1,(k.d2-k.d1)/(2*k.a))),zm=p.z1*th1/(2*Math.PI);
  const v=Math.PI*k.d1*p.n1/60000,Ft=1000*p.P/v,cap=B.F*p.bw*Math.min(zm,6)/6,S=cap/Ft;
  G('Передаточное число i',ratio);GL('Делительный диаметр ведущего',k.d1);GL('Делительный диаметр ведомого',k.d2);G('Зубьев ремня',String(k.zb),'шт');GL('Длина ремня по делительной',k.Lb);GL('Фактическое межосевое a',k.a);G('Угол обхвата малого шкива',th1/D2R,'°',1);G('Зубьев в зацеплении',zm,'',1);
  SV('Скорость ремня v',v,'м/с');SF('Окружная сила F_t',Ft);SF('Допускаемое усилие (ориентир.)',cap);SV('Запас S',S,'',2,S>=1.3?'ok':S>=1?'warn':'bad');
  if(zm<6)W('warn',`В зацеплении ${fmt(zm,1)} зуба — меньше 6. Увеличьте межосевое или число зубьев малого шкива.`);
  if(p.z1<B.zmin)W('warn',`Для профиля ${B.n} минимальное число зубьев шкива — ${B.zmin}.`);
  if(S<1)W('bad','Ремень перегружен — увеличьте ширину ремня или перейдите на больший шаг.');
  sum=`i = ${fmt(ratio,3)} · ремень ${k.zb} зуб. · a = ${LU(k.a)}`;
 } else if(t==='spline'){
  const d=p.m*p.z,D=p.m*(p.z+1),T=9550*p.P/p.n1,h=0.8*p.m,sg=2*T*1000/(0.75*p.z*h*p.l*d);
  GL('Делительный диаметр d',d);GL('Номинальный диаметр D',D);GL('Диаметр вершин вала',D-0.2*p.m);GL('Диаметр впадин вала',d-1.2*p.m);GL('Диаметр вершин втулки',d-0.9*p.m);GL('Шаг p',Math.PI*p.m);GL('Рабочая высота h',h);G('Обозначение',`${fmt(D,0)} × ${fmt(p.m,p.m%1?2:0)} × 9g ГОСТ 6033`,'');
  ST('Передаваемый момент T',T);SS('Напряжение смятия σ_см',sg,ok(p.sig/sg));SS('Допускаемое [σ_см]',p.sig);SV('Запас',p.sig/sg,'',2,ok(p.sig/sg));
  if(p.sig/sg<0.95)W('bad','Смятие выше допускаемого — увеличьте длину или диаметр соединения.');
  if(p.l>1.5*D)W('info','Длина больше 1,5·D — нагрузка распределяется неравномерно, рост длины почти не помогает.');
  sum=`${fmt(D,0)} × ${fmt(p.m,2)} · T = ${fmt(T,1)} Н·м`;noN2=true;
 }
 const GEARS=[],al0=(p.alpha||20)*D2R;
 if(t==='spur'||t==='helical'||t==='internal'){const inn=t==='internal',be=t==='helical'?p.beta*D2R:0,r=pair(p.m,p.z1,p.z2,al0,be,p.x1,inn?0:p.x2,inn,p.ha??1,p.cs??0.25);
  GEARS.push({n:'шестерни',h:'Шестерня',z:p.z1,x:p.x1,da:r.da1,d:r.d1,be,mate:p.z2,hand:be?(HER?'шевронное':(+p.hand===1?'левое':'правое')):'—'});if(!inn)GEARS.push({n:'колеса',h:'Колесо',z:p.z2,x:p.x2,da:r.da2,d:r.d2,be,mate:p.z1,hand:be?(HER?'шевронное':(+p.hand===1?'правое':'левое')):'—'});}
 else if(t==='rack')GEARS.push({n:'шестерни',h:'Шестерня',z:p.z1,x:p.x1,da:p.m*p.z1+2*p.m*(1+p.x1),d:p.m*p.z1,be:0,mate:'рейка',hand:'—'});
 else if(t==='planet'){const r=pair(p.m,p.zs,p.zp,al0,0,0,0,false);GEARS.push({n:'солнца',h:'Солнце',z:p.zs,x:0,da:r.da1,d:r.d1,be:0,mate:p.zp,hand:'—'},{n:'сателлита',h:'Сателлит',z:p.zp,x:0,da:r.da2,d:r.d2,be:0,mate:p.zs+' / '+(p.zs+2*p.zp),hand:'—'});}
 const ACv=ACC[p.acc]||ACC[1];
 const CL=(l,v,dd)=>ctl.push({l,v:fmt(AG?v/25.4:v,AG?4:(dd??3)),u:AG?'дюйм':'мм'}),CT=(l,v,u)=>ctl.push({l,v,u:u||''});
 GEARS.forEach(G0=>{const k=gearCtl(p.m,G0.z,G0.x,al0,G0.be,G0.da,p.b),tl=tolISO(p.m,G0.d,p.b,ACv.q),n=G0.n;G0.k=k;G0.tol=tl;
  CL('Длина общей нормали W '+n,k.W);CT('Зубьев в длине W '+n,String(k.zw));CL('Размер по роликам M '+n,k.M);CL('Диаметр ролика D '+n,k.D,2);
  CL('Постоянная хорда s̄_c '+n,k.sc);CL('Высота до хорды h̄_c '+n,k.hc);CT('Модульная фреза '+n,'№ '+cutterNo(G0.z/Math.cos(G0.be)**3));
  CT('Допуски '+n+', степень '+ACv.q,`F_r ${tl.Fr} · F_p ${tl.Fp} · f_pt ±${tl.fpt} · F_α ${tl.Fa} · F_β ${tl.Fb}`,'мкм');
  CT('Заготовка '+n+': биение базового торца F_T','≤ '+fmt(ftBl(ACv.q,p.b,G0.d),0),'мкм');CT('Заготовка '+n+': биение Ø вершин (если по нему выверка)','≤ '+fmt(0.6*tl.Fr,0)+' (0,6·F_r)','мкм');
  if(!k.Wok)W('info',`Ширина венца мала для измерения W ${n} — контролируйте размер по роликам M.`);});
 if(p.ca&&GEARS.length)CT('Фланкирование головки C_a',`${p.ca} мкм, от ${fmt(0.4*p.m,2)} мм до вершины`);if(p.crown&&GEARS.length)CT('Бочкообразность C_β',`${p.crown} мкм на торцах`);
 if(GEARS.length)CT('Червячная фреза',`m ${fmt(p.m,2)}, α ${fmt(p.alpha,0)}°, класс ${ACv.hob} ГОСТ 9324`);
 boresList(c,std).forEach(x=>{const bi=x.bi,n=x.name;if(!bi)return;if(bi.none){W('warn',`Отверстие под вал ${n} не помещается — нужен вал-шестерня.`);return;}
  CL('Отверстие под вал '+n+(bi.auto?' (по T = '+fmt(bi.T,0)+' Н·м)':''),bi.d,1);
  if(bi.key)CT('Шпонка '+n,`${bi.key.b}×${bi.key.h}, d + t₂ = ${fmt(bi.d+bi.key.t2,1)} мм`);
  if(bi.clamped)W('warn',`Отверстие ${n} ⌀${fmt(bi.dReq,0)} не помещается (обод тоньше 2,5·m) — принято ⌀${fmt(bi.d,0)}. Проверьте вал или сделайте вал-шестерню.`);
  const hb=x.hub;if(hb&&hb.none)W('info',`Конструкция ${n}: между ободом и ступицей мало места — колесо оставлено сплошным.`);
  else if(hb){CT('Ступица '+n,`⌀${fmt(hb.dst,0)} × ${fmt(hb.lst,0)}`,'мм');CT('Диск / обод '+n,`C = ${fmt(hb.C,1)} / S = ${fmt(hb.S,1)}`,'мм');if(hb.holes)CT('Отверстия облегчения '+n,`${hb.holes.n} × ⌀${hb.holes.d0} на ⌀${2*hb.holes.R0}`,'мм');}});
 if(t!=='spline'){const BLn=boresList(c,std);let tot=0,cnt=0;const R0=v=>Math.round(v).toLocaleString('ru-RU');
  partMass(c,std).forEach(q=>{const nm=(BLn.find(x=>x.role===q.role)||{}).name||'',M0=(t==='worm'||t==='globoid')&&q.role===2?{rho:8800,price:950}:(MATS[p.mat??0]||MATS[0]),rho=(M0.rho||7850)*1e-9,mass=q.V*rho,Db=Math.ceil(2*q.Ro+5),Lb=Math.ceil(Math.max(q.b,q.lst)+5),Mb=Math.PI/4*Db*Db*Lb*rho;
   const mm=p.m||(t==='chain'?chainGeo(p).t:t==='belt'?beltGeo(p).B.p:4)/Math.PI,cb=t==='helical'?Math.cos(p.beta*D2R):1,hrs=0.3+Db*Lb/40000+(q.z?q.z*(q.b/cb+2.5*mm+10)/300/60:0),met=Mb*(M0.price||0),cost=met+hrs*(p.rate??2500);tot+=cost;cnt++;
   CT('Масса '+nm,`${fmt(mass,mass<1?3:2)} кг · заготовка ⌀${Db}×${Lb} — ${fmt(Mb,2)} кг`);
   CT('Себестоимость '+nm,`≈ ${R0(cost)} ₽ (металл ${R0(met)} ₽ + ${fmt(hrs,1)} ч)`);});
  if(cnt>1)CT('Себестоимость комплекта',`≈ ${R0(tot)} ₽`);}
 const sb=[],SHF=shaftLoads(c);
 if(SHF.length){const Kb=Math.max(1.2,p.K||1.3),req=p.Lh||10000,BL=boresList(c,std),CV=AG?{F:[0.2248,'lbf',0],M:[8.851,'lbf·дюйм',0],S:[145.04,'psi',0],L:[1/25.4,'дюйм',2]}:{F:[1,'Н',0],M:[1,'Н·м',1],S:[1,'МПа',1],L:[1,'мм',0]};
  const SBv=(l,v,k,stt)=>sb.push({l,v:fmt(v*CV[k][0],CV[k][2]),u:CV[k][1],st:stt||''}),SBt=(l,v,u,stt)=>sb.push({l,v,u:u||'',st:stt||''}),fH=L=>L===Infinity||L>1e6?'> 1 000 000':Math.round(L).toLocaleString('ru-RU');
  SHF.forEach(S=>{const bi=(BL.find(x=>x.role===S.role)||{}).bi,WRM=S.role===1&&(t==='worm'||t==='globoid'),GG=WRM&&t==='globoid'?globoGeo(p):null,df1=WRM?(GG?2*(GG.r1-GG.hf):p.q*p.m-2.4*p.m):0;
   const d=WRM?(GG?Math.floor(2*GG.rj):Math.floor(0.8*df1)):bi&&bi.d?bi.d:bi&&bi.dReq?bi.dReq:(SHAFT.find(v=>v>=Math.cbrt(Math.max(S.T,0.01)*250))||220);
   const db=d-2>=20?Math.min(100,Math.floor((d-2)/5)*5):([10,12,15,17].filter(v=>v<=d-2).pop()||10),Fq=Math.hypot(S.Ft,S.Fr);
   let kind=(p.brg===1||db<20)?'ball':p.brg===2?'tap':(S.Fa>0.35*Fq?'tap':'ball');
   let cand=kind==='ball'?[BRG2,BRG3].map(tb=>tb.find(r=>r[0]===db)).filter(Boolean):[BRG7.find(r=>r[0]===db)].filter(Boolean);
   if(!cand.length){kind='ball';cand=[BRG2.reduce((a,r)=>Math.abs(r[0]-db)<Math.abs(a[0]-db)?r:a)];}
   const Bw0=kind==='ball'?cand[0][4]:cand[0][5],l=Math.round(p.span>0?p.span:S.lAuto||(S.arr===2?Math.max(60,2.5*db):S.bw+Bw0+24)),cc=S.bw/2+Bw0/2+12,Ma=S.Fa*S.dg/2;
   let RAh,RAv,RBh,RBv,Mh,Mv,dsec=WRM?df1:d;
   if(S.arr===2){RAh=S.Ft*cc/l;RBh=S.Ft*(l+cc)/l;RAv=(S.Fr*cc+Ma)/l;RBv=(S.Fr*(l+cc)+Ma)/l;Mh=S.Ft*cc;Mv=Math.max(Math.abs(S.Fr*cc+Ma),Math.abs(Ma));dsec=db;}
   else{const a=S.arr===1?0.3*l:l/2,b=l-a;RAh=S.Ft*b/l;RBh=S.Ft*a/l;RAv=(S.Fr*b+Ma)/l;RBv=(S.Fr*a-Ma)/l;Mh=RAh*a;Mv=Math.max(Math.abs(RAv*a),Math.abs(RBv*b));}
   const RA=Math.hypot(RAh,RAv),RB=Math.hypot(RBh,RBv),M=Math.hypot(Mh,Mv),sig=Math.sqrt(M*M+0.75*(S.T*1000)**2)/(0.1*dsec**3),SA=60;
   let pick=null,LL=null;for(const row of cand){LL=bearingLife(kind,row,RA,RB,S.Fa,S.n,Kb);pick=row;if(Math.min(LL[0],LL[1])>=req)break;}
   const Lm=Math.min(LL[0],LL[1]),nm=S.name;
   SBt(nm+': схема',`l = ${fmt(l*CV.L[0],CV.L[2])} ${CV.L[1]} · ${['симметрично','несимметрично','консольно'][S.arr]}`);
   SBt(nm+': силы F_t / F_r / F_a',[S.Ft,S.Fr,S.Fa].map(q=>fmt(q*CV.F[0],0)).join(' / '),CV.F[1]);
   SBt(nm+': реакции R_A / R_B',fmt(RA*CV.F[0],0)+' / '+fmt(RB*CV.F[0],0),CV.F[1]);
   SBv(nm+': изгибающий момент M',M/1000,'M');
   SBv(nm+`: σ_экв в сечении ⌀${fmt(dsec,0)}`,sig,'S',sig<=SA?'ok':sig<=1.1*SA?'warn':'bad');
   SBt(nm+': подшипники',(kind==='ball'?'шарик. ':'конич. ')+pick[1]+' · '+pick[0]+'×'+(kind==='ball'?pick[5]+'×'+pick[4]:pick[6]+'×'+pick[5]));
   SBt(nm+': ресурс L₁₀h A / B',fH(LL[0])+' / '+fH(LL[1]),'ч',Lm>=req?'ok':Lm>=0.8*req?'warn':'bad');
   if(sig>1.1*SA)W('bad',`${nm}: эквивалентное напряжение ${fmt(sig,0)} МПа больше допускаемого ${SA} МПа — увеличьте диаметр вала или уменьшите пролёт.`);
   if(Lm<0.8*req)W('warn',`${nm}: ресурс подшипников ${fH(Lm)} ч меньше требуемого ${req.toLocaleString('ru-RU')} ч — увеличьте диаметр вала${kind==='ball'?' или выберите конические роликовые':''}.`);});}
 const n2=p.n1/ratio;
 if(!noN2)sum+=` · n₂ = ${fmt(n2,n2<100?1:0)} об/мин`;
 return {g,s,w,ratio,sum,n2,ctl,gears:GEARS,acc:ACv,sb};
}

const GC=new Map();
function filletOf(o){const {z,m,mt,at,x=0,internal=false,ha=1,cc=0.25}=o;if(internal||cc<=0)return null;
 const r=mt*z/2,rb=r*Math.cos(at),ta=Math.tan(at),ca=Math.cos(at),hd=(ha+cc)*m,w=d=>Math.PI*mt/4-d*ta;
 let rho=1.52*cc*m,xc=w(hd-rho)-rho/ca;if(xc<0){rho=(Math.PI*mt/4-hd*ta)/(1/ca-ta);if(!(rho>0))return null;xc=0;}
 const v0=r+x*m-(hd-rho),q=r-v0;if(q<=1e-6)return null;
 const P=ph=>{const X=xc-r*ph,dy=v0-r,L=Math.hypot(X,dy),px=X+rho*X/L,py=v0+rho*dy/L,c=Math.cos(ph),s=Math.sin(ph),xg=px*c+py*s,yg=-px*s+py*c;return [Math.hypot(xg,yg),Math.atan2(yg,xg)-Math.PI/2];};
 const ps=xc/r,pe=(xc-q/ta)/r,E=P(pe),step=2*Math.PI/z;if(E[0]<rb*(1+1e-6))return null;
 const psi=(Math.PI/2+2*x*ta*m/mt)/z,thE=psi+inv(at)-inv(Math.acos(rb/E[0]));if(Math.abs(step/2+E[1]-thE)>0.003)return null;
 return {P,ps,pe,rE:E[0],rho,step};}
function gearPts(o){
 const key=JSON.stringify(o);if(GC.has(key))return GC.get(key);
 const {z,m,mt,at,x=0,internal=false,ha=1,cc=0.25,dpsi=0}=o;
 const r=mt*z/2,rb=r*Math.cos(at);let r1,r2,psi;
 const xk=2*x*Math.tan(at)*m/mt;
 if(!internal){r1=r-m*(ha+cc-x);r2=r+m*(ha+x);psi=(Math.PI/2+xk)/z;}
 else{r1=r-m*(ha-x);r2=r+m*(ha+cc+x);psi=(Math.PI/2-xk)/z+dpsi;}
 const ia=inv(at),CA=o.ca||0,Rs=r2-0.4*m,rel=R=>CA>0&&R>Rs?CA*((R-Rs)/(r2-Rs))**2/R:0,th=R=>Math.max(0,psi+ia-inv(Math.acos(Math.min(1,rb/Math.max(R,rb))))-rel(R));
 const pts=[],n=8,P=(R,a)=>pts.push(R*Math.cos(a),R*Math.sin(a)),step=2*Math.PI/z,FL=filletOf(o),r0=FL?FL.rE:r1,NF=4;
 for(let i=0;i<z;i++){const c=i*step;
  if(FL)for(let k=0;k<NF;k++){const q=FL.P(FL.ps+(FL.pe-FL.ps)*k/NF);P(q[0],c-(step/2+q[1]));}
  for(let k=0;k<=n;k++){const R=r0+(r2-r0)*k/n;P(R,c-th(R));}
  const t2=th(r2);for(let k=1;k<3;k++)P(r2,c-t2+2*t2*k/3);
  for(let k=n;k>=0;k--){const R=r0+(r2-r0)*k/n;P(R,c+th(R));}
  if(FL){for(let k=NF-1;k>=0;k--){const q=FL.P(FL.ps+(FL.pe-FL.ps)*k/NF);P(q[0],c+step/2+q[1]);}const a0=c+step/2-FL.ps,a1=c+step/2+FL.ps;if(a1-a0>1e-6)for(let k=1;k<4;k++)P(r1,a0+(a1-a0)*k/4);}
  else{const t1=th(r1),a0=c+t1,a1=c+step-t1;for(let k=1;k<4;k++)P(r1,a0+(a1-a0)*k/4);}}
 pts.opt=o;if(GC.size>300)GC.clear();GC.set(key,pts);return pts;
}
function arcCP(cx,cy,R,a0,a1){const n=Math.max(1,Math.ceil(Math.abs(a1-a0)/(Math.PI/6))),d=(a1-a0)/n,kk=4/3*Math.tan(d/4),o=[];
 for(let i=0;i<n;i++){const a=a0+i*d,b=a+d;if(!i)o.push([cx+R*Math.cos(a),cy+R*Math.sin(a)]);o.push([cx+R*Math.cos(a)-kk*R*Math.sin(a),cy+R*Math.sin(a)+kk*R*Math.cos(a)],[cx+R*Math.cos(b)+kk*R*Math.sin(b),cy+R*Math.sin(b)-kk*R*Math.cos(b)],[cx+R*Math.cos(b),cy+R*Math.sin(b)]);}return o;}
function boreSegs(d,key){const r=d/2;if(!key)return [{deg:3,cp:arcCP(0,0,r,0,Math.PI)},{deg:3,cp:arcCP(0,0,r,Math.PI,2*Math.PI)}];
 const hb=key.b/2,a0=Math.asin(Math.min(0.99,hb/r)),y0=Math.sqrt(r*r-hb*hb),yt=r+key.t2;
 return [{deg:3,cp:arcCP(0,0,r,Math.PI/2+a0,Math.PI/2-a0+2*Math.PI)},{deg:1,cp:[[hb,y0],[hb,yt]]},{deg:1,cp:[[hb,yt],[-hb,yt]]},{deg:1,cp:[[-hb,yt],[-hb,y0]]}];}
function gearSegs(o){
 const {z,m,mt,at,x=0,internal=false,ha=1,cc=0.25,dpsi=0}=o,r=mt*z/2,rb=r*Math.cos(at);let r1,r2,psi;
 const xk=2*x*Math.tan(at)*m/mt;if(!internal){r1=r-m*(ha+cc-x);r2=r+m*(ha+x);psi=(Math.PI/2+xk)/z;}else{r1=r-m*(ha-x);r2=r+m*(ha+cc+x);psi=(Math.PI/2-xk)/z+dpsi;}
 const ia=inv(at),CA=o.ca||0,Rs=r2-0.4*m,LL=r2-Rs,rel=R=>CA>0&&R>Rs?CA*((R-Rs)/LL)**2/R:0,drel=R=>CA>0&&R>Rs?CA*(2*(R-Rs)/(LL*LL*R)-((R-Rs)/LL)**2/(R*R)):0,thr=R=>psi+ia-inv(Math.acos(Math.min(1,rb/Math.max(R,rb))))-rel(R),th=R=>Math.max(0,thr(R));
 const dth=R=>{if(R<=rb||thr(R)<=0)return 0;const a=Math.acos(rb/R);return -Math.sin(a)*rb/(R*R*Math.cos(a)**2)-drel(R);};
 const PP=(R,a)=>[R*Math.cos(a),R*Math.sin(a)],step=2*Math.PI/z,E=[],FL=filletOf(o),rs=FL?FL.rE:Math.min(Math.max(r1,rb),r2),N=6;
 const flank=(c,s)=>{const out=[],cp=[];
  if(FL){const NF=4,pt=ph=>{const q=FL.P(ph);return PP(q[0],c+s*(step/2+q[1]));},d=(FL.pe-FL.ps)/NF,ep=Math.abs(d)*1e-3,der=ph=>{const A=pt(ph+ep),B=pt(ph-ep);return [(A[0]-B[0])/(2*ep),(A[1]-B[1])/(2*ep)];},fc=[];
   for(let k=0;k<NF;k++){const a=FL.ps+d*k,b=a+d,A=pt(a),B=pt(b),dA=der(a),dB=der(b);if(!fc.length)fc.push(A);fc.push([A[0]+dA[0]*d/3,A[1]+dA[1]*d/3],[B[0]-dB[0]*d/3,B[1]-dB[1]*d/3],B);}
   out.push({deg:3,cp:fc});}
  else if(rs-r1>1e-4){const a=c+s*th(r1);out.push({deg:1,cp:[PP(r1,a),PP(rs,a)]});}
  for(let k=0;k<N;k++){const Ra=rs+(r2-rs)*(k/N)**2,Rb=rs+(r2-rs)*((k+1)/N)**2,hR=Rb-Ra;if(hR<1e-6)continue;
   const pt=R=>PP(R,c+s*th(R)),dv=R=>{const a=c+s*th(R),ap=s*dth(R);return [Math.cos(a)-R*ap*Math.sin(a),Math.sin(a)+R*ap*Math.cos(a)];};
   const A=pt(Ra),B=pt(Rb),dA=dv(Ra),dB=dv(Rb);if(!cp.length)cp.push(A);cp.push([A[0]+dA[0]*hR/3,A[1]+dA[1]*hR/3],[B[0]-dB[0]*hR/3,B[1]-dB[1]*hR/3],B);}
  if(cp.length)out.push({deg:3,cp});return out;};
 const rev=es=>es.slice().reverse().map(e=>({deg:e.deg,cp:e.cp.slice().reverse()}));
 for(let i=0;i<z;i++){const c=i*step,t2=th(r2),t1=th(r1);
  E.push(...flank(c,-1));if(t2>1e-6)E.push({deg:3,cp:arcCP(0,0,r2,c-t2,c+t2)});E.push(...rev(flank(c,1)));
  if(FL){if(FL.ps>1e-6)E.push({deg:3,cp:arcCP(0,0,r1,c+step/2-FL.ps,c+step/2+FL.ps)});}
  else if(step-2*t1>1e-6)E.push({deg:3,cp:arcCP(0,0,r1,c+t1,c+step-t1)});}
 return E;}
function wormSegs(p){const m=p.m,al=p.alpha*D2R,pa=Math.PI*m,lead=pa*p.z1,rp=p.q*m/2,ra=rp+m,rf=rp-1.2*m,ta=Math.tan(al);
 const ph=r=>2*Math.PI*(pa/4-(r-rp)*ta)/lead,dph=-2*Math.PI*ta/lead,PP=(R,a)=>[R*Math.cos(a),R*Math.sin(a)],E=[],N=4,step=2*Math.PI/p.z1;
 const flank=(c,s)=>{const cp=[];for(let k=0;k<N;k++){const Ra=rf+(ra-rf)*k/N,Rb=rf+(ra-rf)*(k+1)/N,hR=Rb-Ra,pt=R=>PP(R,c+s*ph(R)),dv=R=>{const a=c+s*ph(R),ap=s*dph;return [Math.cos(a)-R*ap*Math.sin(a),Math.sin(a)+R*ap*Math.cos(a)];};
  const A=pt(Ra),B=pt(Rb),dA=dv(Ra),dB=dv(Rb);if(!cp.length)cp.push(A);cp.push([A[0]+dA[0]*hR/3,A[1]+dA[1]*hR/3],[B[0]-dB[0]*hR/3,B[1]-dB[1]*hR/3],B);}return cp;};
 for(let i=0;i<p.z1;i++){const c=i*step,t2=ph(ra),t1=ph(rf);E.push({deg:3,cp:flank(c,-1)});E.push({deg:3,cp:arcCP(0,0,ra,c-t2,c+t2)});E.push({deg:3,cp:flank(c,1).reverse()});if(step-2*t1>1e-6)E.push({deg:3,cp:arcCP(0,0,rf,c+t1,c+step-t1)});}
 return E;}
function polarPts(key,z,fn,S=28){if(GC.has(key))return GC.get(key);const pts=[];for(let i=0;i<z;i++)for(let j=0;j<S;j++){const u=-Math.PI/z+2*Math.PI/z*j/S,a=2*Math.PI*i/z+u,R=fn(u);pts.push(R*Math.cos(a),R*Math.sin(a));}GC.set(key,pts);return pts;}
function rackEdge(xa,xb,yp,dir,m,al,shift){
 const p=Math.PI*m,ta=Math.tan(al),yt=yp+dir*m,yr=yp-dir*1.25*m,wT=p/4-m*ta,wR=p/4+1.25*m*ta,out=[xa,yr];
 for(let k=Math.floor((xa-shift)/p)-1;k<100000;k++){const X=shift+p/2+k*p;if(X-p/2>xb)break;for(const q of [[X-wR,yr],[X-wT,yt],[X+wT,yt],[X+wR,yr]])if(q[0]>xa&&q[0]<xb)out.push(q[0],q[1]);}
 out.push(xb,yr);return out;
}
function revPairs(a){const o=[];for(let i=a.length-2;i>=0;i-=2)o.push(a[i],a[i+1]);return o;}
function beltPath(R1,R2,a){
 const gm=Math.asin(Math.max(-1,Math.min(1,(R2-R1)/a))),tu=Math.PI/2+gm,tl=-Math.PI/2-gm;
 const A1=R1*(Math.PI-2*gm),T=Math.sqrt(Math.max(0,a*a-(R2-R1)**2)),A2=R2*(Math.PI+2*gm),L=A1+2*T+A2;
 const p1l=[R1*Math.cos(tl),R1*Math.sin(tl)],p2l=[a+R2*Math.cos(tl),R2*Math.sin(tl)],p2u=[a+R2*Math.cos(tu),R2*Math.sin(tu)],p1u=[R1*Math.cos(tu),R1*Math.sin(tu)];
 const lin=(A,B,f)=>[A[0]+(B[0]-A[0])*f,A[1]+(B[1]-A[1])*f,Math.atan2(B[1]-A[1],B[0]-A[0])];
 const at=s=>{s=((s%L)+L)%L;if(s<A1){const g=tu+s/R1;return [R1*Math.cos(g),R1*Math.sin(g),g+Math.PI/2];}s-=A1;if(s<T)return lin(p1l,p2l,s/T);s-=T;if(s<A2){const g=tl+s/R2;return [a+R2*Math.cos(g),R2*Math.sin(g),g+Math.PI/2];}s-=A2;return lin(p2u,p1u,s/T);};
 return {L,at,tu,tl,s2:A1+T};
}
const PAL={a:{fill:'#3B4149',side:'#262A30',stroke:'#ECE8DF'},b:{fill:'#55372A',side:'#33221A',stroke:'#F2956A'},c:{fill:'#2C3B4E',side:'#1C2633',stroke:'#93B8E6'},d:{fill:'#2F3338',side:'#202327',stroke:'#7D848C'}};
function boundsOf(shapes){let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;for(const sh of shapes){const c=Math.cos(sh.rot),s=Math.sin(sh.rot),a=sh.pts;for(let i=0;i<a.length;i+=2){const x=sh.cx+a[i]*c-a[i+1]*s,y=sh.cy+a[i]*s+a[i+1]*c;if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y;}if(sh.outerR){x0=Math.min(x0,sh.cx-sh.outerR);x1=Math.max(x1,sh.cx+sh.outerR);y0=Math.min(y0,sh.cy-sh.outerR);y1=Math.max(y1,sh.cy+sh.outerR);}}return [x0,y0,x1,y1];}

function buildScene(c,th,std){if(c.type==='hypoid'){const S=buildScene({...c,type:'bevel',p:hypToBevel(c.p)},th,std);try{S.dims.push({a:[0,0],b:[0,-(+c.p.E||0)],t:'E = '+fmt(+c.p.E,2)+' мм'});}catch(e){}return S;}
 const HER=c.type==='herring',p=c.p,t=HER?'helical':c.type,al=(p.alpha||20)*D2R,S={shapes:[],extras:[],dims:[],depth:p.b||20,no3d:false,bounds:null};
 const LU=v=>std==='AGMA'?fmt(v/25.4,3)+' дюйм':fmt(v,2)+' мм';
 if(t==='spur'||t==='helical'||t==='internal'){
  const inn=t==='internal',be=t==='helical'?p.beta*D2R:0,x2=inn?0:p.x2,r=pair(p.m,p.z1,p.z2,al,be,p.x1,x2,inn,p.ha??1,p.cs??0.25),tw=Math.tan(be)*(+p.hand===1?-1:1);
  const CA=(p.ca||0)/1000,HC={ha:p.ha??1,cc:p.cs??0.25},g1=gearPts({z:p.z1,m:p.m,mt:r.mt,at:r.at,x:p.x1,ca:CA,...HC}),g2=gearPts(inn?{z:p.z2,m:p.m,mt:r.mt,at:r.at,x:x2,internal:inn,...HC}:{z:p.z2,m:p.m,mt:r.mt,at:r.at,x:x2,ca:CA,...HC});
  if(!inn){
   S.shapes.push({pts:g1,cx:0,cy:0,rot:th,...PAL.a,hole:r.df1*0.18,role:1,twist:tw/(r.d1/2),circ:[r.d1/2,r.db1/2],label:'z₁ = '+p.z1});
   S.shapes.push({pts:g2,cx:r.aw,cy:0,rot:Math.PI+Math.PI/p.z2-th*p.z1/p.z2,...PAL.b,hole:r.df2*0.18,role:2,twist:-tw/(r.d2/2),circ:[r.d2/2,r.db2/2],label:'z₂ = '+p.z2});
  } else {
   S.shapes.push({pts:g2,cx:0,cy:0,rot:th*p.z1/p.z2,...PAL.b,outerR:r.df2/2+3*p.m,circ:[r.d2/2,r.db2/2]});
   S.shapes.push({pts:g1,cx:r.aw,cy:0,rot:th,...PAL.a,hole:r.df1*0.18,role:1,circ:[r.d1/2,r.db1/2],label:'z₁ = '+p.z1});
  }
  S.dims.push({a:[0,0],b:[r.aw,0],t:'a_w = '+LU(r.aw)});
 } else if(t==='rack'){
  const r1=p.m*p.z1/2,g1=gearPts({z:p.z1,m:p.m,mt:p.m,at:al,x:p.x1}),A=Math.max(0,p.L/2-r1*1.4),thR=Math.sin(th*0.35)*A/r1,shift=thR*r1,yp=-(r1+p.x1*p.m);
  const xa=-p.L/2+shift,xb=p.L/2+shift,yb=yp-4.25*p.m,e=rackEdge(xa,xb,yp,1,p.m,al,shift);
  S.shapes.push({pts:[...e,xb,yb,xa,yb],cx:0,cy:0,rot:0,...PAL.b});
  S.shapes.push({pts:g1,cx:0,cy:0,rot:-Math.PI/2+thR,...PAL.a,hole:r1*0.3,role:1,circ:[r1,r1*Math.cos(al)],label:'z = '+p.z1});
  S.extras.push((ctx,P,s,st)=>{if(!st.circles)return;ctx.setLineDash([6,4]);ctx.strokeStyle='rgba(141,179,226,0.85)';ctx.lineWidth=1;const a=P(xa,yp),b=P(xb,yp);ctx.beginPath();ctx.moveTo(a[0],a[1]);ctx.lineTo(b[0],b[1]);ctx.stroke();ctx.setLineDash([]);});
  S.bounds=[-p.L/2-A,yb,p.L/2+A,r1+2*p.m];
 } else if(t==='bevel'){
  S.no3d=true;const d1r=Math.atan(p.z1/p.z2),d2r=Math.PI/2-d1r,Re=0.5*p.m*Math.hypot(p.z1,p.z2),b=Math.min(p.b,Re*0.6),m=p.m;
  const mk=(u,w,dl,z,rot,pal,phase)=>{const cs=Math.cos(dl),sn=Math.sin(dl);
   const pt=(s,sd,h)=>{const gx=u[0]*cs+w[0]*sn*sd,gy=u[1]*cs+w[1]*sn*sd,nx=-u[0]*sn+w[0]*cs*sd,ny=-u[1]*sn+w[1]*cs*sd;return [gx*s+nx*h,gy*s+ny*h];};
   const rh=0.16*m*z,xb=Re*cs+1.8*m,H1=[u[0]*xb+w[0]*rh,u[1]*xb+w[1]*rh],H2=[u[0]*xb-w[0]*rh,u[1]*xb-w[1]*rh];
   const poly=[pt(Re-b,1,m),pt(Re,1,m),pt(Re,1,-1.25*m),H1,H2,pt(Re,-1,-1.25*m),pt(Re,-1,m),pt(Re-b,-1,m)].flat();
   S.shapes.push({pts:poly,cx:0,cy:0,rot:0,...pal,solid:false});
   S.extras.push((ctx,P,sc,st)=>{for(let k=0;k<z;k++){const f=rot+phase+2*Math.PI*k/z,cf=Math.cos(f);if(cf<0.05)continue;const Q=s=>{const ax=s*cs-m*sn*0.3,rr=(s*sn+m*cs*0.3)*Math.sin(f);return P(u[0]*ax+w[0]*rr,u[1]*ax+w[1]*rr);};const A=Q(Re-b),B=Q(Re);ctx.strokeStyle=pal.stroke;ctx.globalAlpha=0.25+0.7*cf;ctx.lineWidth=1.2;ctx.beginPath();ctx.moveTo(A[0],A[1]);ctx.lineTo(B[0],B[1]);ctx.stroke();}ctx.globalAlpha=1;
    if(st.circles){ctx.setLineDash([6,4]);ctx.strokeStyle='rgba(141,179,226,0.8)';ctx.lineWidth=1;for(const sd of [1,-1]){const O=P(0,0),E=P(...pt(Re,sd,0));ctx.beginPath();ctx.moveTo(O[0],O[1]);ctx.lineTo(E[0],E[1]);ctx.stroke();}ctx.setLineDash([10,3,2,3]);ctx.strokeStyle='rgba(255,255,255,0.3)';const O=P(0,0),E=P(u[0]*(xb+2*m),u[1]*(xb+2*m));ctx.beginPath();ctx.moveTo(O[0],O[1]);ctx.lineTo(E[0],E[1]);ctx.stroke();ctx.setLineDash([]);}});};
  mk([0,-1],[-1,0],d2r,p.z2,-th*p.z1/p.z2,PAL.b,Math.PI/p.z2);
  mk([-1,0],[0,-1],d1r,p.z1,th,PAL.a,0);
  S.dims.push({a:[0,0],b:[-Re*Math.cos(d1r),-Re*Math.sin(d1r)],t:'R_e = '+LU(Re)});
  const bb=boundsOf(S.shapes);S.bounds=[bb[0],bb[1],Math.max(bb[2],m*2),Math.max(bb[3],m*2)];
 } else if(t==='worm'){
  S.no3d=true;const m=p.m,d1=p.q*m,d2=p.z2*m,aw=0.5*m*(p.q+p.z2),pa=Math.PI*m,lead=pa*p.z1,s=th*lead/(2*Math.PI),b1=(11+0.06*p.z2)*m,xa=-b1/2,xb=b1/2;
  const sl=b1/2+1.1*d1;S.shapes.push({pts:[-sl,aw-0.28*d1,sl,aw-0.28*d1,sl,aw+0.28*d1,-sl,aw+0.28*d1],cx:0,cy:0,rot:0,...PAL.d,solid:false});
  const bot=rackEdge(xa,xb,aw-d1/2,-1,m,al,s),top=rackEdge(xa,xb,aw+d1/2,1,m,al,s+lead/2);
  S.shapes.push({pts:[...bot,...revPairs(top)],cx:0,cy:0,rot:0,...PAL.a,solid:false});
  S.shapes.push({pts:gearPts({z:p.z2,m,mt:m,at:al}),cx:0,cy:0,rot:Math.PI/2-s/(d2/2),...PAL.b,hole:d2*0.16,role:2,circ:[d2/2],label:'z₂ = '+p.z2});
  S.extras.push((ctx,P)=>{ctx.strokeStyle='rgba(236,232,223,0.35)';ctx.lineWidth=1;for(let k=-60;k<60;k++){const X=s+pa/2+k*pa;const x2=X+lead/2;if(X<xa||x2>xb)continue;const A=P(X,aw-d1/2+m*0.6),B=P(x2,aw+d1/2-m*0.6);ctx.beginPath();ctx.moveTo(A[0],A[1]);ctx.lineTo(B[0],B[1]);ctx.stroke();}});
  S.dims.push({a:[0,0],b:[0,aw],t:'a_w = '+LU(aw)});
  S.bounds=[-Math.max(sl,d2/2+m),-d2/2-2*m,Math.max(sl,d2/2+m),aw+d1/2+2*m];
 } else if(t==='globoid'){
  S.no3d=true;const Gg=globoGeo(p),m=p.m,aw=Gg.aw,r2=Gg.r2,z2=p.z2,rot=Math.PI/2-Math.PI/z2-th*p.z1/z2,xe=Gg.b1/2,sl=xe+Gg.Lj,rt=x=>aw-Math.sqrt(Math.max(0,Gg.Ra*Gg.Ra-x*x));
  S.shapes.push({pts:[-sl,aw-Gg.rj,sl,aw-Gg.rj,sl,aw+Gg.rj,-sl,aw+Gg.rj],cx:0,cy:0,rot:0,...PAL.d,solid:false});
  const body=[],N=28;for(let k=0;k<=N;k++){const x=-xe+2*xe*k/N;body.push(x,aw+rt(x));}for(let k=N;k>=0;k--){const x=-xe+2*xe*k/N;body.push(x,Math.sqrt(Math.max(0,Gg.Rr**2-x*x)));}
  S.shapes.push({pts:body,cx:0,cy:0,rot:0,...PAL.a,solid:false});
  for(let k=0;k<z2;k++){const sc0=rot+Math.PI/z2+2*Math.PI*k/z2,dd=((sc0-Math.PI/2)%(2*Math.PI)+3*Math.PI)%(2*Math.PI)-Math.PI;if(Math.abs(dd)>Gg.Phi)continue;
   const tp=[],n=5,R1=Gg.Rr+0.05*m;for(let j=0;j<=n;j++){const R=Gg.Ra+(R1-Gg.Ra)*j/n;tp.push(R*Math.cos(sc0-Gg.w(R)),R*Math.sin(sc0-Gg.w(R)));}for(let j=n;j>=0;j--){const R=Gg.Ra+(R1-Gg.Ra)*j/n;tp.push(R*Math.cos(sc0+Gg.w(R)),R*Math.sin(sc0+Gg.w(R)));}
   S.shapes.push({pts:tp,cx:0,cy:0,rot:0,...PAL.a,solid:false});}
  S.shapes.push({pts:globoWheelPts(Gg),cx:0,cy:0,rot,...PAL.b,hole:d2Safe(r2),role:2,circ:[r2],label:'z₂ = '+z2});
  S.extras.push((ctx,P)=>{ctx.strokeStyle='rgba(236,232,223,0.35)';ctx.lineWidth=1;const pa=Math.PI*m,lead=pa*p.z1,s=th*lead/(2*Math.PI);for(let k=-120;k<120;k++){const X=s+pa/2+k*pa,x2=X+lead/4;if(X<-xe||x2>xe)continue;const A=P(X,aw+0.25*Gg.r1),B=P(x2,aw+rt(x2)-0.6*m);ctx.beginPath();ctx.moveTo(A[0],A[1]);ctx.lineTo(B[0],B[1]);ctx.stroke();}});
  S.dims.push({a:[0,0],b:[0,aw],t:'a_w = '+LU(aw)});
  S.bounds=[-Math.max(sl,r2+m),-r2-2*m,Math.max(sl,r2+m),aw+rt(xe)+2*m];
 } else if(t==='planet'){
  const m=p.m,zr=p.zs+2*p.zp,r=pair(m,p.zs,p.zp,al,0,0,0,false),aw=r.aw,thc=th*p.zs/(p.zs+zr),ro=m*zr/2+4.25*m;
  S.shapes.push({pts:gearPts({z:zr,m,mt:m,at:al,internal:true}),cx:0,cy:0,rot:p.zp%2===0?Math.PI/zr:0,...PAL.c,outerR:ro,circ:[m*zr/2]});
  S.shapes.push({pts:gearPts({z:p.zs,m,mt:m,at:al}),cx:0,cy:0,rot:th,...PAL.a,hole:r.df1*0.2,role:1,circ:[r.d1/2],label:'z_a = '+p.zs});
  const gp=gearPts({z:p.zp,m,mt:m,at:al});
  for(let k=0;k<p.N;k++){const f=thc+2*Math.PI*k/p.N;S.shapes.push({pts:gp,cx:aw*Math.cos(f),cy:aw*Math.sin(f),rot:f+Math.PI+Math.PI/p.zp-(th-f)*p.zs/p.zp,...PAL.b,hole:r.df2*0.2,circ:[r.d2/2],label:k===0?'z_g = '+p.zp:''});}
  S.extras.push((ctx,P,sc)=>{ctx.strokeStyle='rgba(147,184,230,0.45)';ctx.lineWidth=Math.max(2,r.df2*0.12*sc);ctx.lineCap='round';const O=P(0,0);for(let k=0;k<p.N;k++){const f=thc+2*Math.PI*k/p.N,Q=P(aw*Math.cos(f),aw*Math.sin(f));ctx.beginPath();ctx.moveTo(O[0],O[1]);ctx.lineTo(Q[0],Q[1]);ctx.stroke();}ctx.lineCap='butt';});
  S.dims.push({a:[0,0],b:[aw*Math.cos(thc+Math.PI/p.N),aw*Math.sin(thc+Math.PI/p.N)],t:'a_w = '+LU(aw)});
  S.bounds=[-ro,-ro,ro,ro];
 } else if(t==='chain'){
  const k=chainGeo(p),C=k.C,tt=k.t,R1=p.z1*tt/(2*Math.PI),R2=p.z2*tt/(2*Math.PI),a=k.a,path=beltPath(R1,R2,a),off=th*R1;
  const sp=z=>polarPts('spr'+z+'_'+tt,z,u=>{const Rd=k.Dd(z)/2,Ra=k.De(z)/2,rs=0.5025*C.d1+0.05,s=Rd*Math.sin(Math.abs(u));return Math.min(Ra,s<rs?Rd*Math.cos(u)-Math.sqrt(rs*rs-s*s):Rd*Math.cos(u)+(s-rs)*1.5);},30);
  S.depth=tt*0.55;
  S.shapes.push({pts:sp(p.z1),cx:0,cy:0,rot:path.tu+off/R1,...PAL.a,hole:R1*0.3,role:1,circ:[R1],label:'z₁ = '+p.z1});
  S.shapes.push({pts:sp(p.z2),cx:a,cy:0,rot:path.tl+(off-path.s2)/R2,...PAL.b,hole:R2*0.3,role:2,circ:[R2],label:'z₂ = '+p.z2});
  S.extras.push((ctx,P,sc)=>{const n=Math.round(path.L/tt),pts=[];for(let i=0;i<n;i++){const q=path.at(i*tt+off);pts.push(P(q[0],q[1]));}
   ctx.lineJoin='round';ctx.strokeStyle='#858D96';ctx.lineWidth=C.d1*0.9*sc;ctx.beginPath();pts.forEach((q,i)=>i?ctx.lineTo(q[0],q[1]):ctx.moveTo(q[0],q[1]));ctx.closePath();ctx.stroke();
   ctx.strokeStyle='#1D2024';ctx.lineWidth=Math.max(1,C.d1*0.28*sc);ctx.stroke();
   for(const q of pts){ctx.beginPath();ctx.arc(q[0],q[1],C.d1/2*sc,0,Math.PI*2);ctx.fillStyle='#C9CED4';ctx.fill();ctx.strokeStyle='#5E656C';ctx.lineWidth=1;ctx.stroke();}});
  S.dims.push({a:[0,0],b:[a,0],t:'a = '+LU(a)});
  const Rm=Math.max(k.De(p.z1),k.De(p.z2))/2+tt;S.bounds=[-k.De(p.z1)/2-tt,-Rm,a+k.De(p.z2)/2+tt,Rm];
 } else if(t==='belt'){
  const k=beltGeo(p),B=k.B,R1=k.d1/2,R2=k.d2/2,a=k.a,path=beltPath(R1,R2,a),off=th*R1,tb=B.h*0.8+0.8;
  const pl=z=>polarPts('pl'+z+'_'+p.prof,z,u=>{const Rp=B.p*z/(2*Math.PI),Ro=Rp-0.3,wo=0.32*B.p/Ro,wb=0.2*B.p/(Ro-B.h),au=Math.abs(u);return au<wb?Ro-B.h:au<wo?Ro-B.h*(wo-au)/(wo-wb):Ro;},32);
  S.depth=p.bw;
  S.shapes.push({pts:pl(p.z1),cx:0,cy:0,rot:path.tu+off/R1,...PAL.a,hole:R1*0.3,role:1,circ:[R1],label:'z₁ = '+p.z1});
  S.shapes.push({pts:pl(p.z2),cx:a,cy:0,rot:path.tl+(off-path.s2)/R2,...PAL.b,hole:R2*0.3,role:2,circ:[R2],label:'z₂ = '+p.z2});
  S.extras.push((ctx,P,sc)=>{ctx.strokeStyle='#8C857A';ctx.lineJoin='round';ctx.lineWidth=tb*sc;ctx.beginPath();const N=Math.max(80,Math.round(path.L/1.5));for(let i=0;i<=N;i++){const q=path.at(path.L*i/N),o=tb/2;const Q=P(q[0]+Math.sin(q[2])*o,q[1]-Math.cos(q[2])*o);i?ctx.lineTo(Q[0],Q[1]):ctx.moveTo(Q[0],Q[1]);}ctx.stroke();
   ctx.lineWidth=0.4*B.p*sc;for(let i=0;i<k.zb;i++){const q=path.at(i*B.p+off),A=P(q[0],q[1]),Bq=P(q[0]-Math.sin(q[2])*-0+(-Math.sin(q[2]))*B.h*0.9*-1*-1,q[1]+Math.cos(q[2])*B.h*0.9);ctx.beginPath();ctx.moveTo(A[0],A[1]);ctx.lineTo(Bq[0],Bq[1]);ctx.stroke();}});
  S.dims.push({a:[0,0],b:[a,0],t:'a = '+LU(a)});
  const Rm=R2+tb+2;S.bounds=[-R1-tb-2,-Rm,a+R2+tb+2,Rm];
 } else if(t==='spline'){
  const at=30*D2R,m=p.m,d=m*p.z,rot=th*0.25;
  S.shapes.push({pts:gearPts({z:p.z,m,mt:m,at,internal:true,ha:0.45,cc:0.12,dpsi:0.015}),cx:0,cy:0,rot,...PAL.c,outerR:d/2+4.5*m,circ:[d/2]});
  S.shapes.push({pts:gearPts({z:p.z,m,mt:m,at,ha:0.45,cc:0.12}),cx:0,cy:0,rot,...PAL.a,hole:d*0.14});
  S.depth=p.l;S.bounds=[-(d/2+4.5*m),-(d/2+4.5*m),d/2+4.5*m,d/2+4.5*m];
 }
 if(!S.bounds)S.bounds=boundsOf(S.shapes);
 S.bores=[];for(const sh of S.shapes){if(!sh.role)continue;const bi=boreFor(c,sh.role,rminOf(sh.pts));const hub=bi&&bi.d?hubFor(c,rminOf(sh.pts),bi,S.depth):null;sh.hub=hub;S.bores.push({role:sh.role,bi,hub});if(bi&&bi.d){sh.hole=bi.d/2;sh.bore=bi.pts;sh.boreK={d:bi.d,key:bi.key};}else{sh.hole=0;sh.bore=null;sh.boreK=null;}}
 if(HER)S.shapes.forEach(sh=>{if(sh.role){sh.herr={g:Math.max(1,Math.min(p.gap,p.b-2)),rc:rminOf(sh.pts)-0.3*p.m};sh.hub=null;S.bores.forEach(x=>{if(x.role===sh.role)x.hub=null;});}});
 if(t==='spur'||t==='helical'||t==='chain'||t==='belt')S.dimOff=S.bounds[3];
 return S;
}
function bevelPts(z,m,at,dl){
 const zv=z/Math.cos(dl),Rv=m*zv/2,r=m*z/2,rb=Rv*Math.cos(at),r1=Rv-1.2*m,r2=Rv+m,psi=Math.PI/(2*zv),ia=inv(at),step=2*Math.PI/zv,n=8;
 const th=R=>Math.max(0,psi+ia-inv(Math.acos(Math.min(1,rb/Math.max(R,rb))))),tooth=[];
 for(let k=0;k<=n;k++){const R=r1+(r2-r1)*k/n;tooth.push([R,-th(R)]);}
 const t2=th(r2);for(let k=1;k<3;k++)tooth.push([r2,-t2+2*t2*k/3]);
 for(let k=n;k>=0;k--){const R=r1+(r2-r1)*k/n;tooth.push([R,th(R)]);}
 const t1=th(r1);for(let k=1;k<4;k++)tooth.push([r1,t1+(step-2*t1)*k/4]);
 const pts=[],sc=zv/z,cd=Math.cos(dl);
 for(let i=0;i<z;i++){const c0=2*Math.PI*i/z;for(const [rho,ps] of tooth){const R=r+(rho-Rv)*cd,a=c0+ps/sc;pts.push(R*Math.cos(a),R*Math.sin(a));}}
 return pts;
}
const esc=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
function printSVG(svg){const f=document.createElement('iframe');f.style.cssText='position:fixed;right:0;bottom:0;width:0;height:0;border:0';document.body.appendChild(f);const d=f.contentDocument;d.open();
 d.write('<!doctype html><html><head><meta charset="utf-8"><title>Чертёж</title><style>@page{size:420mm 297mm;margin:0}html,body{margin:0}svg{display:block;width:420mm;height:297mm}</style></head><body>'+svg+'</body></html>');d.close();
 setTimeout(()=>{try{f.contentWindow.focus();f.contentWindow.print();}catch(e){}setTimeout(()=>f.remove(),2000);},400);}
function drawingSVG(c,gi,std){
 if(c.type==='bevel'||c.type==='hypoid')return drawingConeSVG(c,gi,std);
 const R=calc(c,std),G=(R.gears||[])[gi];if(!G||!G.k)return '';
 const p=c.p,sc0=buildScene(c,0,std),cand=sc0.shapes.filter(s=>s.pts.opt&&s.pts.opt.z===G.z&&!s.outerR&&!s.pts.opt.internal),sh=cand.find(s=>s.role===gi+1)||cand[0];if(!sh)return '';
 const ra=G.da/2,rf=rminOf(sh.pts),d=G.d,b=p.b,m=p.m,bk=sh.boreK,hb=sh.hub&&!sh.hub.none?sh.hub:null,ch=+Math.max(0.5,Math.min(2.5,0.5*m)).toFixed(1);
 const dv=bk?bk.d:sh.hole?2*sh.hole:0,key=bk&&bk.key,t2=key?key.t2:0,e=hb?hb.e:0,zh0=hb?b/2-hb.lst/2:0,zh1=hb?b/2+hb.lst/2:b,zmin=Math.min(0,zh0),zmax=Math.max(b,zh1);
 const SC=[[5,1],[4,1],[2.5,1],[2,1],[1,1],[1,2],[1,2.5],[1,4],[1,5],[1,10],[1,20]].map(([a,q])=>({k:a/q,t:a+':'+q})),s0=SC.find(o=>2*ra*o.k<=165&&(zmax-zmin)*o.k<=105)||SC[SC.length-1],s=s0.k;
 const Y0=128,XC=125,X=z=>XC+(z-(zmin+zmax)/2)*s,YU=r=>Y0-r*s,YL=r=>Y0+r*s,o=[],W1=0.5,W2=0.25,FS="font-family:'GOST type B','ISOCPEUR','Arial Narrow',Arial,sans-serif;font-style:italic";
 const ln=(x1,y1,x2,y2,w,da)=>o.push(`<line x1="${x1.toFixed(2)}" y1="${y1.toFixed(2)}" x2="${x2.toFixed(2)}" y2="${y2.toFixed(2)}" stroke="#000" stroke-width="${w||W2}"${da?` stroke-dasharray="${da}"`:''}/>`);
 const tx=(x,y,t,sz,anc,rot)=>o.push(`<text x="${x.toFixed(2)}" y="${y.toFixed(2)}" font-size="${sz||3.5}" text-anchor="${anc||'start'}"${rot?` transform="rotate(${rot} ${x.toFixed(2)} ${y.toFixed(2)})"`:''} style="${FS}">${esc(t)}</text>`);
 const pg=(pts,fill,w)=>o.push(`<polygon points="${pts.map(q=>q[0].toFixed(2)+','+q[1].toFixed(2)).join(' ')}" fill="${fill}" stroke="#000" stroke-width="${w||W1}" stroke-linejoin="round"/>`);
 const pln=(pts,w)=>o.push(`<polyline points="${pts.map(q=>q[0].toFixed(2)+','+q[1].toFixed(2)).join(' ')}" fill="none" stroke="#000" stroke-width="${w||W1}" stroke-linejoin="round"/>`);
 const arr=(x,y,ang)=>{const L=2.5,Wd=0.55,cs=Math.cos(ang),sn=Math.sin(ang);o.push(`<polygon points="${x.toFixed(2)},${y.toFixed(2)} ${(x-L*cs+Wd*sn).toFixed(2)},${(y-L*sn-Wd*cs).toFixed(2)} ${(x-L*cs-Wd*sn).toFixed(2)},${(y-L*sn+Wd*cs).toFixed(2)}" fill="#000"/>`);};
 const dimV=(x,y1,y2,xr1,xr2,t)=>{const dir=x<xr1?-1:1;ln(xr1,y1,x+dir*1.5,y1);ln(xr2,y2,x+dir*1.5,y2);ln(x,y1,x,y2);arr(x,y1,-Math.PI/2);arr(x,y2,Math.PI/2);tx(x-1,(y1+y2)/2,t,3.5,'middle',-90);};
 const dimH=(y,x1,x2,yr1,yr2,t)=>{const dir=y<yr1?-1:1;ln(x1,yr1,x1,y+dir*1.5);ln(x2,yr2,x2,y+dir*1.5);ln(x1,y,x2,y);arr(x1,y,Math.PI);arr(x2,y,0);tx((x1+x2)/2,y-1,t,3.5,'middle');};
 const rough=(x,y,t)=>{pln([[x-1.8,y-3],[x,y],[x+3.6,y-6.2],[x+4+t.length*1.7,y-6.2]],W2);tx(x+4,y-6.9,t,3);};
 o.push('<defs><pattern id="hz" width="2.2" height="2.2" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><line x1="0" y1="0" x2="0" y2="2.2" stroke="#000" stroke-width="0.2"/></pattern></defs>');
 o.push('<rect x="0" y="0" width="420" height="297" fill="#fff"/>');
 o.push(`<rect x="20" y="5" width="395" height="287" fill="none" stroke="#000" stroke-width="${W1+0.2}"/>`);
 const prof=rk=>hb?[[0,rf],[b,rf],[b,hb.Rr],[b-e,hb.Rr],[b-e,hb.Rh],[zh1,hb.Rh],[zh1,rk],[zh0,rk],[zh0,hb.Rh],[e,hb.Rh],[e,hb.Rr],[0,hb.Rr]]:[[0,rf],[b,rf],[b,rk],[0,rk]];
 pg(prof(dv/2+t2).map(q=>[X(q[0]),YU(q[1])]),'url(#hz)');pg(prof(dv/2).map(q=>[X(q[0]),YL(q[1])]),'url(#hz)');
 const tooth=[[0,rf],[0,ra-ch],[ch,ra],[b-ch,ra],[b,ra-ch],[b,rf]];pln(tooth.map(q=>[X(q[0]),YU(q[1])]));pln(tooth.map(q=>[X(q[0]),YL(q[1])]));
 ln(X(0)+ch*s,YU(ra-ch)+0,X(b)-ch*s,YU(ra-ch),W2);ln(X(0)+ch*s,YL(ra-ch),X(b)-ch*s,YL(ra-ch),W2);
 if(key)ln(X(zh0),YU(dv/2),X(zh1),YU(dv/2),W1);
 const DD='7,1.5,1,1.5';ln(X(0)-3,YU(d/2),X(b)+3,YU(d/2),W2,DD);ln(X(0)-3,YL(d/2),X(b)+3,YL(d/2),W2,DD);ln(X(zmin)-7,Y0,X(zmax)+7,Y0,W2,DD);
 dimV(X(zmin)-12,YU(ra),YL(ra),X(0),X(0),'⌀'+fmt(G.da,2)+' h11');
 dimH(YU(ra)-8,X(0),X(b),YU(ra-ch),YU(ra-ch),fmt(b,b%1?1:0));
 let xr=X(zmax)+10;
 if(hb){dimH(YL(ra)+9,X(zh0),X(zh1),YL(hb.Rh),YL(hb.Rh),fmt(hb.lst,0));dimH(YL(ra)+18,X(e),X(b-e),YL(hb.Rr),YL(hb.Rr),fmt(hb.C,1));
  dimV(xr,YU(hb.Rh),YL(hb.Rh),X(zh1),X(zh1),'⌀'+fmt(hb.dst,0));xr+=10;dimV(xr,YU(hb.Rr),YL(hb.Rr),X(b),X(b),'⌀'+fmt(2*hb.Rr,1));xr+=10;}
 if(dv)dimV(xr,YU(dv/2+t2),YL(dv/2),X(zh1),X(zh1),key?fmt(dv+t2,1)+' +0,2':'⌀'+fmt(dv,0)+' H7');
 {const x0=X(0)+ch*s*0.5,y0=YU(ra)+ch*s*0.5;ln(x0,y0,x0-10,y0-10);ln(x0-10,y0-10,x0-30,y0-10);tx(x0-29,y0-11,ch+'×45° 2 фаски',3);}
 rough(X(b*0.5),YU(d/2),'Ra 1,6');if(dv)rough(X((zh0+zh1)/2)+6,YL(dv/2),'Ra 1,6');
 if(dv){ln(X(zmin)-14,Y0-dv/4*s-6,X(zmin)-5,Y0-dv/4*s-6,W1*0.7);arr(X(zmin)-5,Y0-dv/4*s-6,0);tx(X(zmin)-11,Y0-dv/4*s-8,'А',5,'middle');}
 if(dv){const LX=332,LY=148,rS=dv/2*s;
  if(key){const hbS=key.b/2*s,y0S=Math.sqrt(Math.max(0,rS*rS-hbS*hbS)),ytS=(dv/2+t2)*s;
   o.push(`<path d="M${(LX+hbS).toFixed(2)},${(LY-y0S).toFixed(2)} L${(LX+hbS).toFixed(2)},${(LY-ytS).toFixed(2)} L${(LX-hbS).toFixed(2)},${(LY-ytS).toFixed(2)} L${(LX-hbS).toFixed(2)},${(LY-y0S).toFixed(2)} A${rS.toFixed(2)},${rS.toFixed(2)} 0 1 0 ${(LX+hbS).toFixed(2)},${(LY-y0S).toFixed(2)} Z" fill="none" stroke="#000" stroke-width="${W1}"/>`);
   dimH(LY-ytS-6,LX-hbS,LX+hbS,LY-ytS,LY-ytS,key.b+' Js9');tx(LX,LY-ytS-15,'А',5,'middle');}
  else{o.push(`<circle cx="${LX}" cy="${LY}" r="${rS.toFixed(2)}" fill="none" stroke="#000" stroke-width="${W1}"/>`);tx(LX,LY-rS-8,'А',5,'middle');}
  ln(LX-rS-4,LY,LX+rS+4,LY,W2,DD);ln(LX,LY-rS-3,LX,LY+rS+4,W2,DD);dimH(LY+rS+7,LX-rS,LX+rS,LY,LY,'⌀'+fmt(dv,0)+' H7');}
 rough(282,16,'Ra 6,3');tx(300,14.4,'(',5);pln([[302.4,10.6],[303.6,12.6],[305.8,8.4]],W2);tx(305.2,14.4,')',5);
 const deg=v=>{let dg=Math.floor(v+1e-9),mi=Math.round((v-dg)*60);if(mi===60){dg++;mi=0;}return dg+'°'+String(mi).padStart(2,'0')+'′';};
 const TRw=[['Модуль','m',fmt(m,2)],['Число зубьев','z',String(G.z)],['Угол наклона','β',deg(G.be/D2R)],['Направление линии зуба','—',G.be?(G.hand==='шевронное'?'Шевронное':G.hand==='правое'?'Правое':'Левое'):'—'],['Нормальный исходный контур','—','ГОСТ 13755-2015'],['Коэффициент смещения','x',fmt(G.x,3)],['Степень точности','—',R.acc.n+' ГОСТ 1643-81'],['Длина общей нормали','W',fmt(G.k.W,3)],['Число зубьев в длине W','zw',String(G.k.zw)],['Делительный диаметр','d',fmt(d,3)],['Число зубьев сопряж. колеса',gi?'z₁':'z₂',String(G.mate)]];
 if(Math.abs((p.ha??1)-1)>1e-6||Math.abs((p.cs??0.25)-0.25)>1e-6){TRw[4]=['Исходный контур','—','ГОСТ 13755, α '+fmt(p.alpha||20,0)+'°'];TRw.push(['Коэф. высоты головки','h*a',fmt(p.ha??1,3)],['Коэф. радиального зазора','c*',fmt(p.cs??0.25,3)]);}if(p.ca)TRw.push(['Фланкирование головки','C_a',p.ca+' мкм']);if(p.crown)TRw.push(['Бочкообразность','C_β',p.crown+' мкм']);
 const TX0=305,TY0=5,RH=8;o.push(`<rect x="${TX0}" y="${TY0}" width="110" height="${RH*TRw.length}" fill="none" stroke="#000" stroke-width="${W1}"/>`);
 ln(TX0+65,TY0,TX0+65,TY0+RH*TRw.length,W1);ln(TX0+75,TY0,TX0+75,TY0+RH*TRw.length,W1);
 TRw.forEach((r,i)=>{const y=TY0+RH*i;if(i)ln(TX0,y,TX0+110,y,i===7||i===9?W1:W2);tx(TX0+1.5,y+5.4,r[0],3);tx(TX0+70,y+5.4,r[1],3.2,'middle');tx(TX0+76.5,y+5.4,r[2],3);});
 const HARD=['200…240 HB','260…280 HB','45…50 HRC, закалка ТВЧ зубьев','56…62 HRC, цементация h 0,8…1,2 мм',''],MG=['Сталь 45 ГОСТ 1050-2013','Сталь 40Х ГОСТ 4543-2016','Сталь 40Х ГОСТ 4543-2016','Сталь 20ХН3А ГОСТ 4543-2016','Полиамид 6 блочный'];
 const mi=c.type==='worm'&&gi===0?0:(p.mat??1);const tr=[];if(HARD[mi])tr.push(HARD[mi]+'.');tr.push('Неуказанные предельные отклонения размеров: H14, h14, ±IT14/2.');tr.push('Неуказанные фаски 1×45°, радиусы скруглений 1 мм.');
 if(hb&&hb.holes)tr.push(`Отверстия облегчения: ${hb.holes.n} отв. ⌀${hb.holes.d0} равномерно на ⌀${2*hb.holes.R0}.`);if(key)tr.push('Паз под шпонку ГОСТ 23360-78.');
 tr.forEach((t,i)=>tx(238,200+i*6.5,(i+1)+'. '+t,3.3));
 let A=0;for(let i=0;i<sh.pts.length;i+=2){const j=(i+2)%sh.pts.length;A+=sh.pts[i]*sh.pts[j+1]-sh.pts[j]*sh.pts[i+1];}A=Math.abs(A)/2;
 let V=A*b-Math.PI*(dv/2)**2*b-(key?key.b*t2*b:0);if(hb){V-=2*e*Math.PI*(hb.Rr**2-hb.Rh**2);V+=(hb.lst-b)*Math.PI*(hb.Rh**2-(dv/2)**2);if(hb.holes)V-=hb.holes.n*Math.PI*(hb.holes.d0/2)**2*(b-2*e);}
 const mass=V*((MATS[mi]&&MATS[mi].rho)||7850)*1e-9;
 const bx=230,by=237;o.push(`<rect x="${bx}" y="${by}" width="185" height="55" fill="none" stroke="#000" stroke-width="${W1}"/>`);
 for(let i=1;i<11;i++)ln(bx,by+5*i,bx+65,by+5*i,i===4||i===5?W1:W2);ln(bx+7,by,bx+7,by+25,W1);[17,40,55,65].forEach(xx=>ln(bx+xx,by,bx+xx,by+55,W1));
 ln(bx+65,by+15,bx+185,by+15,W1);ln(bx+65,by+40,bx+185,by+40,W1);ln(bx+135,by+15,bx+135,by+55,W1);ln(bx+135,by+20,bx+185,by+20,W1);ln(bx+135,by+35,bx+185,by+35,W1);ln(bx+150,by+15,bx+150,by+35,W1);ln(bx+167,by+15,bx+167,by+35,W1);ln(bx+155,by+35,bx+155,by+40,W2);
 [['Изм.',bx+0.6],['Лист',bx+7.6],['№ докум.',bx+18],['Подп.',bx+41],['Дата',bx+56]].forEach(([t,x])=>tx(x,by+24,t,2.6));
 ['Разраб.','Пров.','Т.контр.','','Н.контр.','Утв.'].forEach((t,i)=>tx(bx+0.8,by+29+5*i-0.2+(i>2?0:0),t,2.8));
 const dt=new Date(),ds=String(dt.getDate()).padStart(2,'0')+'.'+String(dt.getMonth()+1).padStart(2,'0')+'.'+String(dt.getFullYear()).slice(2);tx(bx+55.6,by+29,ds,2.6);
 const code=`STZ.${String(G.z).padStart(3,'0')}.${String(Math.round(m*100)).padStart(4,'0')}.0${gi+1}`;
 tx(bx+125,by+10,code,6,'middle');
 const nm=c.type==='planet'?(gi?'Сателлит':'Колесо солнечное'):(gi?'Колесо зубчатое':'Шестерня');tx(bx+100,by+29.5,nm,5.5,'middle');
 tx(bx+142.5,by+18.8,'Лит.',2.8,'middle');tx(bx+158.5,by+18.8,'Масса',2.8,'middle');tx(bx+176,by+18.8,'Масштаб',2.8,'middle');tx(bx+158.5,by+29.5,fmt(mass,mass<1?3:2),3.5,'middle');tx(bx+176,by+29.5,s0.t,3.5,'middle');
 tx(bx+137,by+38.8,'Лист 1',2.8);tx(bx+157,by+38.8,'Листов 1',2.8);tx(bx+100,by+49.5,(MATS[mi]&&MATS[mi].grade)||MG[mi]||(MATS[mi]?MATS[mi].n:''),3.6,'middle');tx(bx+160,by+49.5,'ООО «СпецТехЗаказ»',3.4,'middle');
 o.push(`<rect x="20" y="5" width="70" height="14" fill="none" stroke="#000" stroke-width="${W1}"/>`);tx(55,14,code,4,'middle',180);
 tx(415,295.6,'Формат A3',3,'end');tx(300,295.6,'Копировал',3);
 return `<svg xmlns="http://www.w3.org/2000/svg" width="420mm" height="297mm" viewBox="0 0 420 297">${o.join('')}</svg>`;
}
function ratioOf(c){const p=c.p,t=c.type;if(t==='planet')return 1+(p.zs+2*p.zp)/p.zs;if(t==='rack'||t==='spline')return null;return p.z2/p.z1;}
function etaOf(c){const p=c.p,t=c.type==='herring'?'helical':c.type,al=(p.alpha||20)*D2R,nb=0.99;
 if(t==='spur'||t==='helical'||t==='internal'){const be=t==='helical'?p.beta*D2R:0,inn=t==='internal',r=pair(p.m,p.z1,p.z2,al,be,p.x1,inn?0:p.x2,inn,p.ha??1,p.cs??0.25);return (1-0.5*Math.PI*0.06*r.ea*(1/p.z1+(inn?-1:1)/p.z2))*nb*nb;}
 if(t==='worm'||t==='globoid'){const ga=Math.atan(p.z1/p.q);return 0.95*Math.tan(ga)/Math.tan(ga+Math.atan(p.f))*nb*nb;}
 return ({bevel:0.97*nb*nb,planet:0.97,chain:0.96*nb*nb,belt:0.97*nb*nb,rack:0.97*nb})[t]||1;}
function effComps(cs){const out=[];let prev=null;for(const c of cs){let e=c;const lk=!!(prev&&prev.out&&c.p.link);if(lk)e={...c,p:{...c.p,P:prev.out.P,n1:prev.out.n}};
 const i=ratioOf(e),eta=etaOf(e),o=i&&e.p.P&&e.p.n1?{i,eta,P:e.p.P*eta,n:e.p.n1/i,T:9550*e.p.P*eta/(e.p.n1/i)}:null;const r={...e,out:o,linked:lk};out.push(r);prev=r;}return out;}
function svgToDXF(svg){
 const U=s=>String(s).replace(/[^\x00-\x7F]/g,ch=>'\\U+'+ch.charCodeAt(0).toString(16).toUpperCase().padStart(4,'0'));
 const L=['0','SECTION','2','HEADER','9','$DWGCODEPAGE','3','ANSI_1251','9','$INSUNITS','70','4','0','ENDSEC','0','SECTION','2','TABLES','0','TABLE','2','LAYER','70','4'];
 [['KONTUR',7,'CONTINUOUS'],['TONKIE',8,'CONTINUOUS'],['OSEVYE',1,'CONTINUOUS'],['TEKST',5,'CONTINUOUS']].forEach(([n,c,lt])=>L.push('0','LAYER','2',n,'70','0','62',String(c),'6',lt));
 L.push('0','ENDTAB','0','ENDSEC','0','SECTION','2','ENTITIES');
 const H=297,Y=y=>(H-y).toFixed(3),Xf=x=>(+x).toFixed(3),lay=w=>w>=0.45?'KONTUR':'TONKIE';
 const doc=new DOMParser().parseFromString(svg,'image/svg+xml'),num=(el,a)=>parseFloat(el.getAttribute(a)||'0');
 const poly=(pts,closed,layer)=>{L.push('0','LWPOLYLINE','8',layer,'90',String(pts.length),'70',closed?'1':'0');pts.forEach(q=>L.push('10',Xf(q[0]),'20',Y(q[1])));};
 const pp=s=>s.trim().split(/\s+/).map(t=>t.split(',').map(Number));
 doc.querySelectorAll('line').forEach(el=>{const da=el.getAttribute('stroke-dasharray'),w=num(el,'stroke-width'),pr=el.parentNode&&el.parentNode.nodeName==='pattern';if(pr)return;L.push('0','LINE','8',da?(da.includes(',1,')||da.split(',').length>2?'OSEVYE':'TONKIE'):lay(w),'10',Xf(num(el,'x1')),'20',Y(num(el,'y1')),'11',Xf(num(el,'x2')),'21',Y(num(el,'y2')));});
 doc.querySelectorAll('polyline').forEach(el=>poly(pp(el.getAttribute('points')),false,lay(num(el,'stroke-width'))));
 doc.querySelectorAll('polygon').forEach(el=>{const pts=pp(el.getAttribute('points')),f=el.getAttribute('fill')||'';if(f==='#000'&&pts.length===3){L.push('0','SOLID','8','TONKIE');[0,1,2,2].forEach((k,i)=>L.push(String(10+i),Xf(pts[k][0]),String(20+i),Y(pts[k][1])));return;}
  poly(pts,true,lay(num(el,'stroke-width')));
  if(f.includes('url(#hz')){const xs=pts.map(q=>q[0]),ys=pts.map(q=>q[1]),x0=Math.min(...xs),x1=Math.max(...xs),y0=Math.min(...ys),y1=Math.max(...ys),st=2.2*Math.SQRT2;
   for(let c=x0-(y1-y0);c<x1;c+=st){const segs=[];for(let i=0;i<pts.length;i++){const a=pts[i],b=pts[(i+1)%pts.length],fa=a[0]+a[1]-c-y0,fb=b[0]+b[1]-c-y0;if((fa<0)!==(fb<0)){const t=fa/(fa-fb);segs.push([a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t]);}}
    segs.sort((p,q)=>p[0]-q[0]);for(let k=0;k+1<segs.length;k+=2)L.push('0','LINE','8','TONKIE','10',Xf(segs[k][0]),'20',Y(segs[k][1]),'11',Xf(segs[k+1][0]),'21',Y(segs[k+1][1]));}}});
 doc.querySelectorAll('rect').forEach(el=>{if(el.getAttribute('fill')==='#fff')return;const x=num(el,'x'),y=num(el,'y'),w=num(el,'width'),h=num(el,'height');poly([[x,y],[x+w,y],[x+w,y+h],[x,y+h]],true,lay(num(el,'stroke-width')));});
 doc.querySelectorAll('circle').forEach(el=>L.push('0','CIRCLE','8',lay(num(el,'stroke-width')),'10',Xf(num(el,'cx')),'20',Y(num(el,'cy')),'40',num(el,'r').toFixed(3)));
 doc.querySelectorAll('path').forEach(el=>{const d=el.getAttribute('d')||'',m=d.match(/-?\d+(\.\d+)?/g);if(!m)return;const n=m.map(Number),pts=[];
  const mA=d.match(/M([-\d.]+),([-\d.]+) L([-\d.]+),([-\d.]+) L([-\d.]+),([-\d.]+) L([-\d.]+),([-\d.]+) A([-\d.]+),([-\d.]+) 0 1 0 ([-\d.]+),([-\d.]+)/);
  if(mA){const v=mA.slice(1).map(Number);poly([[v[0],v[1]],[v[2],v[3]],[v[4],v[5]],[v[6],v[7]]],false,'KONTUR');const r=v[8],cx=(v[6]+v[10])/2,cy=v[7]+Math.sqrt(Math.max(0,r*r-((v[10]-v[6])/2)**2)),a0=Math.atan2(-(v[7]-cy),v[6]-cx)*180/Math.PI,a1=Math.atan2(-(v[11]-cy),v[10]-cx)*180/Math.PI;
   L.push('0','ARC','8','KONTUR','10',Xf(cx),'20',Y(cy),'40',r.toFixed(3),'50',a0.toFixed(3),'51',a1.toFixed(3));}});
 doc.querySelectorAll('text').forEach(el=>{const t=el.textContent;if(!t.trim())return;const x=num(el,'x'),y=num(el,'y'),sz=num(el,'font-size')||3.5,anc=el.getAttribute('text-anchor'),tr=el.getAttribute('transform')||'',rm=tr.match(/rotate\(([-\d.]+)/),rot=rm?-parseFloat(rm[1]):0,h=anc==='middle'?'1':anc==='end'?'2':'0';
  L.push('0','TEXT','8','TEKST','10',Xf(x),'20',Y(y),'40',(sz*0.72).toFixed(2),'1',U(t),'50',rot.toFixed(1),'51','15');if(h!=='0')L.push('72',h,'11',Xf(x),'21',Y(y));});
 L.push('0','ENDSEC','0','EOF');return L.join('\r\n');}
const FORM=[['Передаточное число','u = z₂ / z₁ (планетарная: i = 1 + z_b / z_a; червячная: i = z₂ / z₁)'],['Межосевое расстояние a_w','a_w = a·cos α_t / cos α_tw,  a = m_t·(z₁ ± z₂) / 2'],['Делительное межосевое','a = m_t·(z₁ ± z₂) / 2'],['Межосевое a_w (радиус водила)','a_w = m·(z_a + z_g) / 2'],
 ['Торцовый модуль','m_t = m / cos β'],['Делительный диаметр','d = m·z / cos β  (червяк: d₁ = q·m; звёздочка: d = t / sin(180°/z))'],['Диаметр вершин','d_a = d + 2m·(h*a + x − Δy),  h*a = 1'],['Диаметр впадин','d_f = d − 2m·(h*a + c* − x),  c* = 0,25'],['Основной диаметр','d_b = d·cos α_t,  tg α_t = tg α / cos β'],
 ['Угол зацепления','inv α_tw = inv α_t + 2·(x₁ ± x₂)·tg α / (z₁ ± z₂),  inv α = tg α − α'],['Торцовое перекрытие','ε_α = [√(r_a1² − r_b1²) ± √(r_a2² − r_b2²) − a_w·sin α_tw] / (π·m_t·cos α_t)'],['Перекрытие','ε_α = [√(r_a1² − r_b1²) + √(r_a2² − r_b2²) − a_w·sin α_tw] / p_b'],['Осевое перекрытие','ε_β = b·sin β / (π·m)'],
 ['Шаг','p = π·m_t'],['Радиус переходной','ρ_f = 0,38·m — скругление вершины исходной рейки ГОСТ 13755; ножка — трохоида, описанная им при обкатке'],['Граничный диаметр','d_l — точка сопряжения эвольвенты с переходной кривой'],['Нижняя точка активного','d_p = 2·√(r_b² + (a_w·sin α_tw − √(r_a,сопр² − r_b,сопр²))²); условие без интерференции: d_p ≥ d_l'],
 ['Момент','T = 9550·P / n, Н·м (P в кВт, n в об/мин)'],['Окружная сила','F_t = 2000·T / d'],['Окружная скорость','v = π·d·n / 60 000, м/с'],['Осевая сила','F_a = F_t·tg β'],['K_Hv','ГОСТ 21354: K_Hv = 1 + δ_H·g₀·v·√(a_w/u) / (w_t·K_A) — по скорости и степени точности; K_Hβ — по ψ_bd = b/d₁; K_Hα — по степени точности'],['K_Fv','K_Fv ≈ 1 + 2·(K_Hv − 1); K_Fβ ≈ 1 + 1,5·(K_Hβ − 1); K_Fα — по степени точности и ε_α'],
 ['Контактное напряжение','σ_H = Z_E·Z_H·Z_ε·√(F_t·K_A·K_Hv·K_Hβ·K_Hα·(u ± 1) / (b·d₁·u)),  Z_E = 190 √МПа,  Z_H = √(2cos β / sin 2α_t)'],['Допускаемое [σ_H]','[σ_H] = σ_Hlim·Z_N·Z_R / S_H,  Z_N = (N_HG / N_HE)^(1/6),  N_HE = μ_H·60·n·L_h,  N_HG = 30·HB^2,4'],
 ['Изгиб σ_F','σ_F = Y_FS·Y_β·Y_ε·F_t·K_A·K_Fv·K_Fβ·K_Fα / (b·m),  Y_FS = 3,47 + 13,2/z_v − 27,9·x/z_v + 0,092·x²'],['Допускаемое [σ_F]','[σ_F] = σ_Flim·Y_N / S_F,  Y_N = (4·10⁶ / N_FE)^(1/q),  q = 6 (HB ≤ 350) или 9'],['Запас','S = [σ] / σ — норма при S ≥ 1'],['Расчётный ресурс','L_h = N_lim·(σ / σ_lim)^(−q) / (60·n·μ) — до достижения предела выносливости по контакту или изгибу'],
 ['Длина общей нормали','W = m·cos α·[π·(z_w − 0,5) + 2x·tg α + z·inv α_t],  z_w = z·α_x/π + 0,5'],['Зубьев в длине W','z_w = z/π·(tg α_x / cos²β_b − 2x·tg α / z − inv α_t) + 0,5, округлить'],['Размер по роликам','inv α_M = inv α_t + D/(m·z·cos α) + 2x·tg α/z − π/(2z);  M = d·cos α_t / cos α_M + D (z чётное)'],['Диаметр ролика','D ≈ 1,68·m, округлённо до стандартного'],
 ['Постоянная хорда','s̄_c = m·(π/2·cos²α + x·sin 2α)'],['Высота до хорды','h̄_c = 0,5·(d_a − d − s̄_c·tg α)'],['Допуски','ISO 1328 / ГОСТ 1643: f_pt, F_p, F_α, F_β, F_r по m, d, b и степени точности'],['Модульная фреза','№ фрезы из набора 8 шт. по эквивалентному z_v = z / cos³β'],
 ['КПД','η = 0,95·tg γ / tg(γ + ρ′),  ρ′ = arctg f'],['Угол подъёма','γ = arctg(z₁ / q)'],['Скорость скольжения','v_s = π·d₁·n₁ / (60 000·cos γ)'],['Ход витка','p_z = π·m·z₁'],['Осевой шаг','p = π·m'],['Наибольший диаметр колеса','d_aM2 = d_a2 + 6m / (z₁ + 2)'],['Потери в тепло','P_пот = P·(1 − η)'],
 ['Угол делительного конуса','δ₁ = arctg(z₁/z₂),  δ₂ = 90° − δ₁'],['Внешнее конусное','R_e = 0,5·m_e·√(z₁² + z₂²)'],['Внешний делительный','d_e = m_e·z'],['Средний делительный','d_m = d_e·(1 − 0,5·K_be)'],['Средний модуль','m_m = m_e·(1 − 0,5·K_be)'],['Эквивалентное','z_v = z / cos δ'],['Коэффициент ширины','K_be = b / R_e ≤ 0,3'],
 ['Число звеньев','L_t = 2a/t + (z₁ + z₂)/2 + ((z₂ − z₁)/2π)²·t/a, округлить до чётного'],['Фактическое межосевое','a = t/4·[L − (z₁+z₂)/2 + √((L − (z₁+z₂)/2)² − 8·((z₂−z₁)/2π)²)]'],['Скорость цепи','v = z₁·t·n₁ / 60 000'],['Запас прочности S','S = Q / (F_t·K_д + q·v²)'],['Зубьев ремня','z_р = L / p'],['Угол обхвата','θ = 180° − 2·arcsin((d₂ − d₁) / 2a)'],['Зубьев в зацеплении','z₀ = z₁·θ / 360°, должно быть ≥ 6'],
 ['Напряжение смятия','σ_см = 2T / (0,75·z·h·l·d) ≤ [σ_см]'],['Масса','m = ρ·V, V — объём модели с отверстием, пазом, ступицей и облегчением'],['Себестоимость','C = m_заг·Ц + t·C_ч'],['σ_экв','σ_экв = √(M² + 0,75·T²) / (0,1·d³)'],['ресурс L₁₀h','L₁₀h = (C / P)^p·10⁶ / (60·n),  p = 3 (шарик.) или 10/3 (ролик.),  P = (X·F_r + Y·F_a)·K_б'],['реакции','ΣM = 0 в двух плоскостях; R = √(R_г² + R_в²)']];
function cmpVer(a,b){const P=v=>{const s=String(v).split('-');return {n:s[0].split('.').map(x=>+x||0),pre:s[1]?+((s[1].match(/\d+/)||[0])[0]):Infinity};};const A=P(a),B=P(b);for(let i=0;i<3;i++){const d=(A.n[i]||0)-(B.n[i]||0);if(d)return d;}return A.pre===B.pre?0:A.pre>B.pre?1:-1;}
FORM.unshift(['Зубьев колеса в обхвате','z′ ≈ 0,1·z₂ (ГОСТ 24438-80)'],['Угол обхвата','2Φ = 360°·(z′ − 0,45) / z₂'],['Длина нарезки','b₁ = d₂·sin Φ'],['Наибольший диаметр червяка','d_ae1 = 2·[a_w − √((d₂/2 − h_a)² − (b₁/2)²)] — диаметр на торцах нарезки'],['Расчётный (основной)','d_b = d₂·sin α — прямолинейная образующая витка касается этой окружности'],['Высота головки','h_a = 0,7·m,  h_f = 0,9·m,  c = 0,2·m'],['Угол подъёма в горле','γ = arctg(z₁ / q)'],['Коэффициент многопарности','K_гл ≈ 0,35·z′ (1,3…2) — во сколько раз нагрузка делится между зубьями в обхвате; σ_H = (480/d₂)·√(T₂·K / (d₁·K_гл))'],['в горле','в среднем сечении червяка (горле), где он тоньше всего']);
function formulaOf(l){const q=FORM.find(f=>l.includes(f[0]));return q?q[1]:'';}
const d2Safe=r2=>2*r2*0.16;
function globoGeo(p){const m=p.m,al=(p.alpha||20)*D2R,z1=p.z1,z2=p.z2,i=z2/z1,r2=m*z2/2,r1=p.q*m/2,aw=r1+r2,ha=0.7*m,hf=0.9*m,rb=r2*Math.sin(al),zc=Math.max(2,Math.round(0.1*z2)),Phi=Math.PI*(zc-0.45)/z2,del=Math.PI/(2*z2);
 const w=R=>del+Math.acos(Math.min(1,rb/R))-Math.acos(rb/r2),Ra=r2-ha,Rr=r2+hf,Rt=Rr+0.35*m,b1=2*r2*Math.sin(Phi),xc=Math.max(Rt*Math.sin(Phi+w(Rt)),Ra*Math.sin(Phi+w(Ra)))+0.3*m,rho=x=>aw-Math.sqrt(Math.max(0,Rr*Rr-x*x)),ga=Math.atan(z1/p.q);
 const rj=Math.max(2,Math.min(0.75*(r1-hf),0.85*rho(0))),Lj=Math.round(Math.max(3*rj,20));
 return {m,al,z1,z2,i,r1,r2,aw,ha,hf,rb,zc,Phi,del,w,Ra,Rr,Rt,b1,xc,rho,ga,tw:Math.tan(ga)/r2,rj,Lj};}
function globoWheelPts(G){const key='glw'+[G.z2,G.m,G.r1,G.al].join(':');if(GC.has(key))return GC.get(key);
 const ra=G.r2+G.ha,rf=G.r2-G.hf,step=2*Math.PI/G.z2,pts=[],P=(R,a)=>pts.push(R*Math.cos(a),R*Math.sin(a));
 for(let i=0;i<G.z2;i++){const s=i*step+step/2,wa=G.w(ra),wf=G.w(rf);P(ra,s-wa);P(rf,s-wf);for(let k=1;k<3;k++)P(rf,s-wf+2*wf*k/3);P(rf,s+wf);P(ra,s+wa);const a0=s+wa,a1=s+step-wa;for(let k=1;k<3;k++)P(ra,a0+(a1-a0)*k/3);}
 GC.set(key,pts);return pts;}
let CAPT=null;const AIK='zuboraschet.claude.v1',AI_MODEL='claude-sonnet-4-5';
const pkeys=t=>[...new Set([...(TYPES[t]?TYPES[t].k:[]),...((EXTRA&&EXTRA[t])||[])])];
function download(name,text,mime){if(CAPT){CAPT.push({name,text});return;}const b=new Blob([text],{type:mime});const u=URL.createObjectURL(b);const a=document.createElement('a');a.href=u;a.download=name;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(u),3000);}
function frpParse(buf){const b=new Uint8Array(buf),dec=new TextDecoder('windows-1251'),out=[];const u16=i=>b[i]|(b[i+1]<<8);for(let i=0;i<b.length-8;i++){if(b[i]===77&&b[i+1]===101&&b[i+2]===109&&b[i+3]===111){let j=i+4;while(j<b.length&&b[j]>=48&&b[j]<=57)j++;if(j===i+4||b[j]!==0)continue;const name=dec.decode(b.slice(i,j));let k=j;while(k<b.length-4&&!(b[k]===255&&b[k+1]===255&&b[k+2]===255&&b[k+3]===31))k++;if(k>=b.length-4)break;let q=k+4;const n=u16(q);q+=2;const L=[];for(let t=0;t<n&&t<50&&q<b.length;t++){const len=u16(q);q+=2;L.push(dec.decode(b.slice(q,q+len)));q+=len;if(t<n-1)q+=1;}out.push({name,text:L.join('\n')});i=q-1;}}return out;}
function frpToComp(buf,fname){const M=frpParse(buf),T=M.map(x=>x.text.trim());const at=(re,n)=>{const i=T.findIndex(t=>re.test(t));if(i<0)return [];const r=[];for(let k=i+1;k<T.length&&r.length<n;k++){if(T[k]==='')continue;r.push(T[k]);}return r;};const num=v=>v==null?NaN:parseFloat(String(v).split('\n')[0].replace(',','.').replace(/[^0-9.+-]/g,''));const dms=v=>{const m=String(v||'').match(/(-?\d+)\D+(\d+)\D+(\d+)/);return m?(+m[1])+(+m[2])/60+(+m[3])/3600:num(v);};const tol=v=>{const p=String(v||'').split('\n').map(num).filter(isFinite);return p;};if(T.some(t=>/внутреннего зацепления/i.test(t)))throw new Error('Расчёт внутреннего зацепления — открытие пока не поддерживается');const m=num(at(/^Модуль/,1)[0]),[z1,z2]=at(/^Число зубьев$/,2).map(num),be=dms(at(/^Угол наклона зубьев/,1)[0]),al=dms(at(/^Угол профиля исходного контура/,1)[0]);const ha=num(at(/^Коэффициент высоты головки/,1)[0]),cs=num(at(/^Коэффициент радиального зазора/,1)[0]),[b1]=at(/^Ширина зубчатого венца/,2).map(num),[x1,x2]=at(/^Коэффициент смещения исходного контура/,2).map(num);const acc=at(/^Степень точности/,1)[0]||'',aw=num(at(/^Межосевое расстояние/,1)[0]),[da1,da2]=at(/^Диаметр вершин/,2).map(num),[w1]=at(/^Длина общей нормали, мм/,2).map(tol),[zw1]=at(/^Число зубьев в длине общей нормали/,2).map(num);if(!(m>0&&z1>0&&z2>0))throw new Error('В файле не найдены модуль и числа зубьев');const ai=['7-C','8-B','8-C','9-B','10-A'].indexOf(acc.trim().toUpperCase().replace('С','C').replace('В','B').replace('А','A'));const type=be>1e-6?'helical':'spur',p={m,z1,z2,alpha:isFinite(al)?+al.toFixed(4):20,x1:isFinite(x1)?x1:0,x2:isFinite(x2)?x2:0,ha:isFinite(ha)?ha:1,cs:isFinite(cs)?cs:0.25};if(type==='helical')p.beta=+be.toFixed(4);if(isFinite(b1))p.b=b1;if(ai>=0)p.acc=ai;return {type,p,name:String(fname||'Расчёт').replace(/\.frp$/i,''),ref:{aw,da1,da2,w1,zw1}};}
function newComp(type,comps,params,name){const n=comps.filter(c=>c.type===type).length+1;return {id:nid(),type,name:name||TYPES[type].l+' '+n,p:{...TYPES[type].d,...(params||{})}};}
function seed(){const a=newComp('helical',[],TPL[0].p,'Быстроходная ступень');const b=newComp('planet',[a],TPL[1].p,'Планетарный ряд');const c=newComp('chain',[a,b],{},'Цепь на конвейер');return [a,b,c];}

window.__T={calc,drawingSVG,coneBlank,F};