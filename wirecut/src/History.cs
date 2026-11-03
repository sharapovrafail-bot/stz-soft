// STZ WireCut — история изменений как во Fusion 360: отменить / повторить (Ctrl+Z / Ctrl+Y),
// временная шкала внизу чертежа — щелчок по шагу возвращает чертёж к этому шагу.
// Совместимо с .NET 2.0 (без LINQ и пр.).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireCut
{
    /// <summary>Один шаг истории — полный снимок чертежа после действия.</summary>
    public class HistState
    {
        public string Name;          // что сделано («Прямоугольник», «Скругление»…)
        public string Cmd;           // команда — для значка
        public DateTime Time;
        public List<Seg> Ents = new List<Seg>();
        public List<Dim> Dims = new List<Dim>();
        public List<int> DimRef = new List<int>();   // индекс объекта, к которому привязан размер (-1 — свободный)
        public List<V2> Starts = new List<V2>();
        public List<SideMark> Sides = new List<SideMark>();
        public List<LeadMark> Leads = new List<LeadMark>();
        public long Sig;
    }

    public class History
    {
        public const int Max = 200;              // сколько шагов хранить
        public List<HistState> Items = new List<HistState>();
        public int Cur = -1;                     // текущий шаг
        public bool CanUndo { get { return Cur > 0; } }
        public bool CanRedo { get { return Cur >= 0 && Cur < Items.Count - 1; } }
        public HistState Current { get { return Cur >= 0 && Cur < Items.Count ? Items[Cur] : null; } }

        // ---------- подпись состояния (быстрое сравнение «изменилось ли что-то») ----------
        static void H(ref long h, double v) { unchecked { h = (h ^ BitConverter.DoubleToInt64Bits(v)) * 1099511628211L; } }
        static void H(ref long h, long v) { unchecked { h = (h ^ v) * 1099511628211L; } }
        static void H(ref long h, V2 v) { H(ref h, v.X); H(ref h, v.Y); }

        public static long Signature(CadCanvas cv)
        {
            List<int> refs = new List<int>();
            foreach (Dim d in cv.Dims) refs.Add(d.A != null ? cv.Ents.IndexOf(d.A) : -1);
            return SigOf(cv.Ents, cv.Dims, refs, cv.ManualStarts, cv.SideMarks, cv.LeadMarks);
        }

        static long SigOf(List<Seg> ents, List<Dim> dims, List<int> dimRef, List<V2> starts, List<SideMark> sides, List<LeadMark> leads)
        {
            long h = unchecked((long)14695981039346656037UL);
            H(ref h, (long)ents.Count);
            foreach (Seg s in ents)
            {
                H(ref h, (long)s.Kind);
                if (s.Kind == SegKind.Line) { H(ref h, s.P0); H(ref h, s.P1); }
                else { H(ref h, s.C); H(ref h, s.R); H(ref h, s.A0); H(ref h, s.Sw); }
            }
            H(ref h, (long)dims.Count);
            for (int i = 0; i < dims.Count; i++)
            {
                Dim d = dims[i];
                H(ref h, (long)d.K);
                if (dimRef[i] >= 0) { H(ref h, (long)dimRef[i]); H(ref h, d.Off); H(ref h, d.Rel); }   // положение привязанного размера зависит от масштаба экрана — не учитываем
                else { H(ref h, d.P1); H(ref h, d.P2); H(ref h, d.C); H(ref h, d.R); H(ref h, d.V); H(ref h, d.U1); H(ref h, d.U2); H(ref h, d.Pos); }
            }
            H(ref h, (long)starts.Count);
            foreach (V2 p in starts) H(ref h, p);
            H(ref h, (long)sides.Count);
            foreach (SideMark m in sides) { H(ref h, m.P); H(ref h, m.Outside ? 1L : 0L); }
            H(ref h, (long)leads.Count);
            foreach (LeadMark m in leads)
            {
                H(ref h, m.Entry);
                H(ref h, m.Thread.HasValue ? 1L : 0L); if (m.Thread.HasValue) H(ref h, m.Thread.Value);
                H(ref h, m.Exit.HasValue ? 1L : 0L); if (m.Exit.HasValue) H(ref h, m.Exit.Value);
                H(ref h, m.Out.HasValue ? 1L : 0L); if (m.Out.HasValue) H(ref h, m.Out.Value);
            }
            return h;
        }

        static HistState Capture(CadCanvas cv, string name, string cmd, long sig)
        {
            HistState st = new HistState { Name = name, Cmd = cmd, Time = DateTime.Now, Sig = sig };
            foreach (Seg s in cv.Ents) { Seg c = s.Clone(); c.Sel = false; st.Ents.Add(c); }
            foreach (Dim d in cv.Dims) { st.Dims.Add(d.Clone()); st.DimRef.Add(d.A != null ? cv.Ents.IndexOf(d.A) : -1); }
            st.Starts.AddRange(cv.ManualStarts);
            foreach (SideMark m in cv.SideMarks) st.Sides.Add(new SideMark { P = m.P, Outside = m.Outside });
            foreach (LeadMark m in cv.LeadMarks) st.Leads.Add(m.Copy());
            return st;
        }

        /// <summary>Записать шаг, если чертёж изменился. После возврата назад новое действие стирает «будущие» шаги.</summary>
        public bool Commit(CadCanvas cv, string name, string cmd)
        {
            long sig = Signature(cv);
            if (Cur >= 0 && Items[Cur].Sig == sig) return false;
            if (Cur < Items.Count - 1) Items.RemoveRange(Cur + 1, Items.Count - Cur - 1);
            Items.Add(Capture(cv, string.IsNullOrEmpty(name) ? "Изменение" : name, cmd, sig));
            if (Items.Count > Max) Items.RemoveAt(0);
            Cur = Items.Count - 1;
            return true;
        }

        // ---------- удаление шага (как «Удалить» на шкале Fusion 360) ----------
        static string Rn(double v) { return Math.Round(v, 6).ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
        static string Kv(V2 v) { return Rn(v.X) + "," + Rn(v.Y); }
        static string SegKey(Seg s) { return s.Kind == SegKind.Line ? "L" + Kv(s.P0) + ";" + Kv(s.P1) : "A" + Kv(s.C) + ";" + Rn(s.R) + ";" + Rn(s.A0) + ";" + Rn(s.Sw); }
        static string DimKey(Dim d, string seg) { return seg != null ? "D" + (int)d.K + "|" + seg + "|" + Rn(d.Off) + "|" + Kv(d.Rel) : "F" + (int)d.K + "|" + Kv(d.P1) + Kv(d.P2) + Kv(d.C) + Rn(d.R) + Kv(d.V) + Kv(d.U1) + Kv(d.U2) + Kv(d.Pos); }
        static string LeadKey(LeadMark m) { return Kv(m.Entry) + "|" + (m.Thread.HasValue ? Kv(m.Thread.Value) : "-") + "|" + (m.Exit.HasValue ? Kv(m.Exit.Value) : "-") + "|" + (m.Out.HasValue ? Kv(m.Out.Value) : "-"); }
        static string SideKey(SideMark m) { return Kv(m.P) + (m.Outside ? "+" : "-"); }

        /// <summary>Разность «было → стало»: что добавлено (ключ → количество).</summary>
        static Dictionary<string, int> Minus(List<string> a, List<string> b)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            foreach (string k in a) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }
            foreach (string k in b) { int n; if (d.TryGetValue(k, out n)) { if (n <= 1) d.Remove(k); else d[k] = n - 1; } }
            return d;
        }
        static bool Take(Dictionary<string, int> d, string k) { int n; if (!d.TryGetValue(k, out n) || n <= 0) return false; if (n == 1) d.Remove(k); else d[k] = n - 1; return true; }
        static List<string> Keys<T>(List<T> l, Fn1<T, string> f) { List<string> r = new List<string>(); foreach (T x in l) r.Add(f(x)); return r; }

        /// <summary>Удалить шаг k: убрать его изменения из всех следующих шагов. Возвращает пояснение; null — нельзя.</summary>
        public string DeleteStep(int k)
        {
            if (k <= 0 || k >= Items.Count) return null;
            HistState a = Items[k - 1], b = Items[k];
            List<string> ka = Keys(a.Ents, delegate (Seg x) { return SegKey(x); }), kb = Keys(b.Ents, delegate (Seg x) { return SegKey(x); });
            Dictionary<string, int> addE = Minus(kb, ka), remE = Minus(ka, kb);
            Dictionary<string, Seg> remSeg = new Dictionary<string, Seg>();
            foreach (Seg x in a.Ents) { string key = SegKey(x); if (remE.ContainsKey(key) && !remSeg.ContainsKey(key)) remSeg[key] = x; }
            Fn1<HistState, List<string>> dimKeys = delegate (HistState st) { List<string> r = new List<string>(); for (int i = 0; i < st.Dims.Count; i++) r.Add(DimKey(st.Dims[i], st.DimRef[i] >= 0 && st.DimRef[i] < st.Ents.Count ? SegKey(st.Ents[st.DimRef[i]]) : null)); return r; };
            List<string> dka = dimKeys(a), dkb = dimKeys(b);
            Dictionary<string, int> addD = Minus(dkb, dka), remD = Minus(dka, dkb);
            Dictionary<string, int> addS = Minus(Keys(b.Starts, delegate (V2 x) { return Kv(x); }), Keys(a.Starts, delegate (V2 x) { return Kv(x); })), remS = Minus(Keys(a.Starts, delegate (V2 x) { return Kv(x); }), Keys(b.Starts, delegate (V2 x) { return Kv(x); }));
            Dictionary<string, int> addM = Minus(Keys(b.Sides, delegate (SideMark x) { return SideKey(x); }), Keys(a.Sides, delegate (SideMark x) { return SideKey(x); })), remM = Minus(Keys(a.Sides, delegate (SideMark x) { return SideKey(x); }), Keys(b.Sides, delegate (SideMark x) { return SideKey(x); }));
            Dictionary<string, int> addL = Minus(Keys(b.Leads, delegate (LeadMark x) { return LeadKey(x); }), Keys(a.Leads, delegate (LeadMark x) { return LeadKey(x); })), remL = Minus(Keys(a.Leads, delegate (LeadMark x) { return LeadKey(x); }), Keys(b.Leads, delegate (LeadMark x) { return LeadKey(x); }));
            int nAdd = 0, nRem = 0; foreach (int v in addE.Values) nAdd += v; foreach (int v in remE.Values) nRem += v;
            int missed = 0;
            for (int j = k + 1; j < Items.Count; j++)
            {
                HistState st = Items[j];
                // объекты: убрать добавленные шагом k, вернуть удалённые им
                Dictionary<string, int> take = new Dictionary<string, int>(addE);
                List<Seg> ne = new List<Seg>(); int[] map = new int[st.Ents.Count];
                for (int i = 0; i < st.Ents.Count; i++) { if (Take(take, SegKey(st.Ents[i]))) map[i] = -1; else { map[i] = ne.Count; ne.Add(st.Ents[i]); } }
                if (j == Items.Count - 1) foreach (int v in take.Values) missed += v;
                Dictionary<string, int> back = new Dictionary<string, int>(remE);
                foreach (Seg x in ne) Take(back, SegKey(x));
                foreach (KeyValuePair<string, int> kv in back) for (int n = 0; n < kv.Value; n++) ne.Add(remSeg[kv.Key].Clone());
                // размеры
                Dictionary<string, int> takeD = new Dictionary<string, int>(addD);
                List<Dim> nd = new List<Dim>(); List<int> nr = new List<int>();
                for (int i = 0; i < st.Dims.Count; i++)
                {
                    int r = st.DimRef[i];
                    string key = DimKey(st.Dims[i], r >= 0 && r < st.Ents.Count ? SegKey(st.Ents[r]) : null);
                    if (Take(takeD, key)) continue;
                    if (r >= 0 && (r >= map.Length || map[r] < 0)) continue;   // объект размера удалён
                    nd.Add(st.Dims[i]); nr.Add(r >= 0 ? map[r] : -1);
                }
                for (int i = 0; i < a.Dims.Count; i++)
                {
                    int r = a.DimRef[i]; string seg = r >= 0 && r < a.Ents.Count ? SegKey(a.Ents[r]) : null;
                    if (!remD.ContainsKey(DimKey(a.Dims[i], seg))) continue;
                    int ni = -1; if (seg != null) { for (int q = 0; q < ne.Count; q++) if (SegKey(ne[q]) == seg) { ni = q; break; } if (ni < 0) continue; }
                    nd.Add(a.Dims[i].Clone()); nr.Add(ni);
                }
                // точки заправки, стороны захода, входы / выходы
                List<V2> ns = new List<V2>(); Dictionary<string, int> tS = new Dictionary<string, int>(addS);
                foreach (V2 x in st.Starts) if (!Take(tS, Kv(x))) ns.Add(x);
                foreach (V2 x in a.Starts) if (remS.ContainsKey(Kv(x)) && !ns.Contains(x)) ns.Add(x);
                List<SideMark> nm = new List<SideMark>(); Dictionary<string, int> tM = new Dictionary<string, int>(addM);
                foreach (SideMark x in st.Sides) if (!Take(tM, SideKey(x))) nm.Add(x);
                foreach (SideMark x in a.Sides) if (remM.ContainsKey(SideKey(x))) nm.Add(new SideMark { P = x.P, Outside = x.Outside });
                List<LeadMark> nl = new List<LeadMark>(); Dictionary<string, int> tL = new Dictionary<string, int>(addL);
                foreach (LeadMark x in st.Leads) if (!Take(tL, LeadKey(x))) nl.Add(x);
                foreach (LeadMark x in a.Leads) if (remL.ContainsKey(LeadKey(x))) nl.Add(x.Copy());
                st.Ents = ne; st.Dims = nd; st.DimRef = nr; st.Starts = ns; st.Sides = nm; st.Leads = nl;
                st.Sig = SigOf(ne, nd, nr, ns, nm, nl);
            }
            string name = b.Name;
            Items.RemoveAt(k);
            if (Cur >= k) Cur--;
            string rep = "Удалён шаг «" + name + "»" + (nAdd + nRem > 0 ? " (" + (nAdd > 0 ? "убрано объектов " + nAdd : "") + (nAdd > 0 && nRem > 0 ? ", " : "") + (nRem > 0 ? "возвращено " + nRem : "") + ")" : "") + ".";
            if (missed > 0) rep += " Внимание: " + missed + " объект(ов) этого шага позже изменены (обрезка, перенос…) — они остались, проверьте чертёж.";
            return rep;
        }

        /// <summary>Забыть шаги до k (чертёж не меняется, шаг k становится началом шкалы).</summary>
        public void TrimBefore(int k)
        {
            if (k <= 0 || k >= Items.Count) return;
            Items.RemoveRange(0, k); Cur = Math.Max(0, Cur - k);
        }

        /// <summary>Вернуть чертёж к шагу i (копия — сохранённый шаг не портится).</summary>
        public void Restore(CadCanvas cv, int i)
        {
            if (i < 0 || i >= Items.Count) return;
            HistState st = Items[i];
            List<Seg> ents = new List<Seg>();
            foreach (Seg s in st.Ents) ents.Add(s.Clone());
            List<Dim> dims = new List<Dim>();
            for (int k = 0; k < st.Dims.Count; k++)
            {
                Dim d = st.Dims[k].Clone();
                int r = st.DimRef[k];
                d.A = r >= 0 && r < ents.Count ? ents[r] : null;
                dims.Add(d);
            }
            cv.Ents = ents; cv.Dims = dims;
            cv.ManualStarts = new List<V2>(st.Starts);
            List<SideMark> sides = new List<SideMark>();
            foreach (SideMark m in st.Sides) sides.Add(new SideMark { P = m.P, Outside = m.Outside });
            cv.SideMarks = sides;
            List<LeadMark> leads = new List<LeadMark>();
            foreach (LeadMark m in st.Leads) leads.Add(m.Copy());
            cv.LeadMarks = leads;
            cv.Marks.Clear(); cv.QuickShown.Clear();
            cv.Hover = null; cv.EditDim = null; cv.HoverDim = null;
            Cur = i;
        }
    }

    /// <summary>Временная шкала истории (как внизу окна Fusion 360).</summary>
    public class HistoryBar : Control
    {
        public History H;
        public Act2<int, int> OnPick;            // щелчок по шагу (индекс, 0)
        public Act2<int, Point> OnContext;       // правая кнопка по шагу (индекс, точка на экране)
        int scroll;                              // сдвиг ленты, пикселей
        int hover = -1;
        readonly List<Rectangle> rects = new List<Rectangle>();
        readonly ToolTip tip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 15000 };
        string tipText = "";
        const int Gap = 4, Pad = 8, Icon = 18;

        public HistoryBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Raised; Height = 40; Cursor = Cursors.Hand;
        }

        static string Short(string s) { return s.Length > 22 ? s.Substring(0, 21) + "…" : s; }

        /// <summary>Прокрутить так, чтобы текущий шаг был виден.</summary>
        public void ShowCurrent()
        {
            Layout0();
            if (H != null && H.Cur >= 0 && H.Cur < rects.Count)
            {
                Rectangle r = rects[H.Cur];
                int total = rects[rects.Count - 1].Right + 20;
                scroll = Math.Max(0, Math.Min(total - Width, (r.Left + r.Right) / 2 - Width / 2));   // текущий шаг — по центру шкалы
            }
            Invalidate();
        }

        void Layout0()
        {
            rects.Clear();
            if (H == null) return;
            int x = 6;
            for (int i = 0; i < H.Items.Count; i++)
            {
                string t = (i + 1) + ". " + Short(H.Items[i].Name);
                int w = TextRenderer.MeasureText(t, Font).Width + Icon + Pad * 2 + 2;
                rects.Add(new Rectangle(x, 5, w, Height - 10));
                x += w + Gap;
            }
            int total = x + 20;
            if (scroll > Math.Max(0, total - Width)) scroll = Math.Max(0, total - Width);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Layout0();
            if (H == null || H.Items.Count == 0) return;
            // линия шкалы
            using (Pen ln = new Pen(Theme.Border, 2)) g.DrawLine(ln, 0, Height / 2, Width, Height / 2);
            for (int i = 0; i < rects.Count; i++)
            {
                Rectangle r = rects[i]; r.Offset(-scroll, 0);
                if (r.Right < 0 || r.Left > Width) continue;
                bool cur = i == H.Cur, future = i > H.Cur;
                Color bg = cur ? Theme.Accent : i == hover ? Theme.Border : future ? Theme.Background : Theme.Surface;
                using (GraphicsPath p = Theme.Shape(r)) using (Brush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (future) using (Pen pd = new Pen(Theme.Border) { DashStyle = DashStyle.Dash }) using (GraphicsPath p = Theme.Shape(r)) g.DrawPath(pd, p);
                Color fg = cur ? Color.White : future ? Theme.Muted : Theme.Text;
                using (Bitmap ic = Theme.Icon(string.IsNullOrEmpty(H.Items[i].Cmd) ? "ИСТОРИЯ" : H.Items[i].Cmd, Icon))
                    g.DrawImage(ic, r.Left + Pad, r.Top + (r.Height - Icon) / 2, Icon, Icon);
                Rectangle tr = new Rectangle(r.Left + Pad + Icon + 2, r.Top, r.Width - Pad - Icon - 2, r.Height);
                TextRenderer.DrawText(g, (i + 1) + ". " + Short(H.Items[i].Name), Font, tr, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
            // маркер текущего положения (как ползунок отката во Fusion)
            if (H.Cur >= 0 && H.Cur < rects.Count)
            {
                int mx = rects[H.Cur].Right - scroll + Gap / 2;
                using (Pen pm = new Pen(Theme.Orange, 3)) g.DrawLine(pm, mx, 2, mx, Height - 2);
                g.FillPolygon(new SolidBrush(Theme.Orange), new Point[] { new Point(mx - 5, 1), new Point(mx + 5, 1), new Point(mx, 7) });
            }
        }

        int HitIndex(Point p)
        {
            for (int i = 0; i < rects.Count; i++) { Rectangle r = rects[i]; r.Offset(-scroll, 0); if (r.Contains(p)) return i; }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitIndex(e.Location);
            if (i != hover) { hover = i; Invalidate(); }
            string t = "";
            if (i >= 0 && H != null)
            {
                HistState st = H.Items[i];
                t = "Шаг " + (i + 1) + ": " + st.Name + "  (" + st.Time.ToString("HH:mm:ss") + ")\nОбъектов: " + st.Ents.Count + (st.Dims.Count > 0 ? ", размеров: " + st.Dims.Count : "") +
                    (i == H.Cur ? "\nТекущий шаг." : i > H.Cur ? "\nЩелчок — вернуть (повторить) до этого шага." : "\nЩелчок — откатить чертёж к этому шагу.\nСледующие шаги останутся — их можно вернуть, пока не сделано новое действие.") + "\nПравая кнопка — удалить шаг, очистить историю.";
            }
            if (t != tipText) { tipText = t; tip.SetToolTip(this, t); }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = HitIndex(e.Location);
            if (i >= 0 && e.Button == MouseButtons.Left && OnPick != null) OnPick(i, 0);
            if (i >= 0 && e.Button == MouseButtons.Right && OnContext != null) OnContext(i, PointToScreen(e.Location));
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll = Math.Max(0, scroll - e.Delta / 2);
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }
    }

    public partial class MainForm
    {
        public History Hist = new History();
        HistoryBar histBar;
        Panel histPanel;
        Label histInfo;
        Timer histTimer;
        string opName, opCmd;        // название текущего действия — для записи в историю

        // названия шагов истории: команда (все варианты) → понятное имя
        static readonly Dictionary<string, string[]> OpNames = new Dictionary<string, string[]>();
        static void N(string name, string icon, params string[] keys) { foreach (string k in keys) OpNames[k] = new string[] { name, icon }; }
        static MainForm()
        {
            N("Отрезок", "ОТРЕЗОК", "L", "LINE", "ОТРЕЗОК", "О", "PL", "PLINE");
            N("Круг", "КРУГ", "C", "CIRCLE", "КРУГ", "К");
            N("Дуга", "ДУГА", "A", "ARC", "ДУГА", "Д");
            N("Прямоугольник", "ПРЯМОУГ", "REC", "RECTANG", "ПРЯМОУГ", "ПР");
            N("Стереть", "СОТРИ", "E", "ERASE", "СОТРИ", "С", "СТЕРЕТЬ");
            N("Перенести", "ПЕРЕНЕСТИ", "M", "MOVE", "ПЕРЕНЕСТИ", "ПЕР");
            N("Копировать", "КОПИРОВАТЬ", "CO", "CP", "COPY", "КОПИРОВАТЬ", "КОП");
            N("Повернуть", "ПОВЕРНУТЬ", "RO", "ROTATE", "ПОВЕРНУТЬ", "ПОВ");
            N("Зеркало", "ЗЕРКАЛО", "MI", "MIRROR", "ЗЕРКАЛО", "ЗЕР");
            N("Масштаб", "МАСШТАБ", "SC", "SCALE", "МАСШТАБ", "МАС");
            N("Подобие", "ПОДОБИЕ", "O", "OFFSET", "ПОДОБИЕ");
            N("Обрезать", "ОБРЕЗАТЬ", "TR", "TRIM", "ОБРЕЗАТЬ");
            N("Удлинить", "УДЛИНИТЬ", "EX", "EXTEND", "УДЛИНИТЬ");
            N("Сопряжение", "СОПРЯЖЕНИЕ", "F", "FILLET", "СОПРЯЖЕНИЕ");
            N("Фаска", "ФАСКА", "CHA", "CHAMFER", "ФАСКА");
            N("Массив", "МАССИВ", "AR", "ARRAY", "МАССИВ");
            N("Починка чертежа", "ПОЧИНИТЬ", "REPAIR", "ПОЧИНИТЬ");
            N("Шестерня", "ШЕСТЕРНЯ", "GEAR", "ШЕСТЕРНЯ");
            N("Шпоночный паз", "ШПОНКА", "KEY", "KEYWAY", "ШПОНКА", "ПАЗ");
            N("Шлицы", "ШЛИЦЫ", "SPLINE", "ШЛИЦЫ");
            N("Размер", "РАЗМЕР", "РАЗМЕР", "DIM");
            N("Размер радиуса", "РАДИУС", "РАДИУС");
            N("Размер диаметра", "ДИАМЕТР", "ДИАМЕТР");
            N("Угловой размер", "УГОЛ", "УГОЛ");
            N("Удалить размеры", "УДАЛИТЬРАЗМЕРЫ", "УДАЛИТЬРАЗМЕРЫ", "DIMCLEAR");
            N("Точка заправки", "СТАРТ", "ST", "START", "СТАРТ", "ЗАПРАВКА");
            N("Сторона захода", "ЗАХОД", "ЗАХОД", "LEAD");
            N("Вход / выход", "ВХОД", "ВХОД", "ВЫХОД", "ВХОДВЫХОД", "LEADIN");
            N("Начало координат", "НОЛЬ", "НОЛЬ", "ORIGIN", "НАЧАЛО");
            N("Шестерня из STZ", "ИЗSTZ", "ИЗSTZ", "STZ", "ИЗ STZ");
            N("Сброс заправки", "СБРОС", "СБРОС", "СБРОСЗАПРАВКИ", "RESETSTART");
            N("Новый чертёж", "НОВЫЙ", "NEW", "НОВЫЙ");
            N("Открыть DXF", "ОТКРЫТЬ", "OPEN", "ОТКРЫТЬ", "IMPORT");
        }

        /// <summary>Начало действия: запомнить его название для истории.</summary>
        void OpBegin(string c)
        {
            string[] v;
            if (OpNames.TryGetValue(c, out v)) { opName = v[0]; opCmd = v[1]; }
            else { opName = null; opCmd = null; }
        }

        /// <summary>Записать шаг истории, если чертёж изменился.</summary>
        public void Checkpoint()
        {
            if (Cv == null) return;
            if (Hist.Commit(Cv, opName ?? "Изменение", opCmd ?? "ИСТОРИЯ")) { HistChanged(); opName = null; opCmd = null; }
        }

        void HistChanged()
        {
            if (histBar == null) return;
            histBar.ShowCurrent();
            HistState c = Hist.Current;
            histInfo.Text = c == null ? "" : "Шаг " + (Hist.Cur + 1) + " из " + Hist.Items.Count;
            if (miUndo != null)
            {
                miUndo.Text = Hist.CanUndo ? "Отменить: " + Hist.Items[Hist.Cur].Name : "Отменить";
                miRedo.Text = Hist.CanRedo ? "Повторить: " + Hist.Items[Hist.Cur + 1].Name : "Повторить";
                miUndo.Enabled = Hist.CanUndo; miRedo.Enabled = Hist.CanRedo;
            }
        }

        /// <summary>Прервать команду (если идёт) и сохранить то, что она успела начертить.</summary>
        void StopForHistory()
        {
            if (Active != null) { Active = null; Cv.EditDim = null; RefreshPrompt(); }
            if (Cv.SimOn) SimStop();
            Checkpoint();
        }

        public void Undo()
        {
            StopForHistory();
            if (!Hist.CanUndo) { Msg("Нечего отменять — это начало истории."); return; }
            string what = Hist.Items[Hist.Cur].Name;
            GoToStep(Hist.Cur - 1, false);
            Msg("Отменено: " + what + "   (Ctrl+Y — повторить)");
        }

        public void Redo()
        {
            StopForHistory();
            if (!Hist.CanRedo) { Msg("Нечего повторять."); return; }
            GoToStep(Hist.Cur + 1, false);
            Msg("Повторено: " + Hist.Items[Hist.Cur].Name);
        }

        void GoToStep(int i, bool say)
        {
            if (say) StopForHistory();
            if (i < 0 || i >= Hist.Items.Count || i == Hist.Cur) return;
            Hist.Restore(Cv, i);
            RebuildIfPath();
            Cv.Invalidate();
            HistChanged();
            if (say) Msg("История: шаг " + (i + 1) + " — " + Hist.Items[i].Name + (Hist.CanRedo ? "   (дальнейшие шаги можно вернуть щелчком по шкале или Ctrl+Y; новое действие их сотрёт)" : ""));
        }

        void ToggleHistory()
        {
            histPanel.Visible = !histPanel.Visible;
            Msg("Шкала истории: " + (histPanel.Visible ? "показана" : "скрыта"));
            HistChanged();
        }

        ToolStripMenuItem miUndo, miRedo;

        /// <summary>Панель истории: кнопки ⏮ ◀ ▶ ⏭ и временная шкала.</summary>
        Control BuildHistoryBar()
        {
            histPanel = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Raised, Padding = new Padding(4, 2, 4, 2) };
            FlowLayoutPanel btns = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Raised };
            btns.Controls.Add(new Label { Text = "ИСТОРИЯ", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(2, 11, 6, 0) });
            btns.Controls.Add(HistBtn("|◄", "К началу истории (шаг 1)", delegate { GoToStep(0, true); }));
            btns.Controls.Add(HistBtn("◄", "Отменить последнее действие (Ctrl+Z)", delegate { Undo(); }));
            btns.Controls.Add(HistBtn("►", "Повторить отменённое действие (Ctrl+Y)", delegate { Redo(); }));
            btns.Controls.Add(HistBtn("►|", "К последнему шагу", delegate { GoToStep(Hist.Items.Count - 1, true); }));
            histInfo = new Label { Dock = DockStyle.Right, AutoSize = true, ForeColor = Theme.Muted, Padding = new Padding(8, 11, 4, 0) };
            histBar = new HistoryBar { Dock = DockStyle.Fill, H = Hist, Font = new Font(UiFont, UiSize - 1f) };
            histBar.OnPick = delegate (int i, int z) { GoToStep(i, true); };
            histBar.OnContext = delegate (int i, Point at) { HistMenu(i, at); };
            histPanel.Controls.Add(histBar); histPanel.Controls.Add(btns); histPanel.Controls.Add(histInfo);

            histTimer = new Timer { Interval = 400 };     // страховка: изменения вне команд (меню, свойства, AutoCAD)
            histTimer.Tick += delegate { if (Active == null && !Cv.SimOn) Checkpoint(); };
            histTimer.Start();
            return histPanel;
        }

        /// <summary>Правая кнопка по шагу шкалы: удалить шаг, вернуться, очистить историю.</summary>
        void HistMenu(int i, Point at)
        {
            StopForHistory();
            if (i >= Hist.Items.Count) return;
            string nm = (i + 1) + ". " + Hist.Items[i].Name;
            ContextMenuStrip m = new ContextMenuStrip(); Theme.Strip(m); m.ShowImageMargin = false;
            ToolStripLabel head = new ToolStripLabel("Шаг " + nm) { ForeColor = Theme.Muted }; m.Items.Add(head);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem del = new ToolStripMenuItem("Удалить этот шаг") { ForeColor = Theme.Text, Enabled = i > 0 };
            del.ToolTipText = "Убрать то, что сделал этот шаг, из чертежа и из всех следующих шагов (как «Удалить» на шкале Fusion 360).";
            del.Click += delegate
            {
                string rep = Hist.DeleteStep(i);
                if (rep == null) return;
                Hist.Restore(Cv, Hist.Cur); RebuildIfPath(); Cv.Invalidate(); HistChanged();
                Msg(rep + " Ctrl+Z этого не вернёт — проверьте чертёж.");
            };
            m.Items.Add(del);
            ToolStripMenuItem go = new ToolStripMenuItem("Вернуться к этому шагу") { ForeColor = Theme.Text, Enabled = i != Hist.Cur };
            go.Click += delegate { GoToStep(i, true); };
            m.Items.Add(go);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem trim = new ToolStripMenuItem("Удалить все шаги до этого (чертёж не меняется)") { ForeColor = Theme.Text, Enabled = i > 0 };
            trim.Click += delegate { Hist.TrimBefore(i); HistChanged(); Msg("История очищена до шага «" + Hist.Items[0].Name + "»."); };
            m.Items.Add(trim);
            ToolStripMenuItem clr = new ToolStripMenuItem("Очистить всю историю (оставить текущий чертёж)") { ForeColor = Theme.Text, Enabled = Hist.Items.Count > 1 };
            clr.Click += delegate { int c = Hist.Cur; Hist.Items.RemoveRange(c + 1, Hist.Items.Count - c - 1); Hist.TrimBefore(c); Hist.Items[0].Name = "Начало"; HistChanged(); Msg("История очищена — текущий чертёж стал началом."); };
            m.Items.Add(clr);
            m.Closed += delegate { m.BeginInvoke((MethodInvoker)delegate { m.Dispose(); }); };
            m.Show(at);
        }

        Button HistBtn(string t, string tipText, EventHandler h)
        {
            Button b = new Button { Text = t, AutoSize = true, Width = 44, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface, ForeColor = Theme.Text, Margin = new Padding(2, 2, 2, 0), Padding = new Padding(0), Font = new Font(UiFont, UiSize + 1f, FontStyle.Bold) };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.Click += h; tip.SetToolTip(b, tipText);
            return b;
        }
    }
}
