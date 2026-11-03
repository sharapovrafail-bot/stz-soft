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
if (typeof module !== "undefined") module.exports = G19326;
