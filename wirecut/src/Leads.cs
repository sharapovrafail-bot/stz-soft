// STZ WireCut — ручные точки входа / выхода проволоки (как подвод / отвод во Fusion 360)
// и начало координат (центральная точка плоскостей, как во Fusion 360): показ, привязка, перенос детали к нулю.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireCut
{
    /// <summary>Заход / выход, заданные вручную для одного контура.</summary>
    public class LeadMark
    {
        public V2 Entry;        // точка входа на контур (на линии чертежа)
        public V2? Thread;      // точка заправки (отверстие); null — по настройкам «1а. Заход проволоки»
        public V2? Exit;        // точка выхода с контура; null — там же, где вход
        public V2? Out;         // куда уходит проволока после выхода; null — обратно в точку заправки / по нормали
        public LeadMark Copy() { return new LeadMark { Entry = Entry, Thread = Thread, Exit = Exit, Out = Out }; }
        public void Move(V2 d)
        {
            Entry = Entry + d;
            if (Thread.HasValue) Thread = Thread.Value + d;
            if (Exit.HasValue) Exit = Exit.Value + d;
            if (Out.HasValue) Out = Out.Value + d;
        }
    }

    // ---------- общая геометрия входа / выхода ----------
    public static class LeadGeo
    {
        public static readonly Color GEntry = Color.FromArgb(90, 220, 120), GExit = Color.FromArgb(255, 95, 95);

        /// <summary>Контур в направлении резки (как в траектории: по / против часовой из свойств).</summary>
        public static Contour Oriented(Contour c, JobSettings s)
        {
            bool wantCcw = s.Dir == CutDir.CCW;
            return (c.Area > 0) != wantCcw ? c.Reversed() : c;
        }

        /// <summary>Ближайший замкнутый контур к точке.</summary>
        public static int Nearest(List<Contour> cs, V2 p, out double dist, out V2 onIt)
        {
            int best = -1; dist = double.MaxValue; onIt = p;
            for (int j = 0; j < cs.Count; j++)
                foreach (Seg s in cs[j].Segs) { double t; V2 q; double d = s.Project(p, out t, out q); if (d < dist) { dist = d; best = j; onIt = q; } }
            return best;
        }

        /// <summary>Что получится при таком выходе: перекрытие (+мм) или перемычка (−мм); 0 — выход в точке входа.</summary>
        public static double ExitLen(Contour oriented, V2 entry, V2 exit)
        {
            Contour c = oriented.StartAt(entry);
            double l = c.LenTo(exit), L = c.Length;
            if (l < 1e-4 || L - l < 1e-4) return 0;
            return l <= L / 2 ? l : -(L - l);
        }

        /// <summary>Нарисовать направление резки у входа и участок перекрытия / перемычки.</summary>
        public static void Draw(Graphics g, CadCanvas cv, Contour oriented, V2 entry, V2? exit, Font f)
        {
            Contour c = oriented.StartAt(entry);
            // стрелка направления резки
            V2 tg = c.Segs[0].TanStart; PointF a = cv.ToScr(entry), b = cv.ToScr(entry + tg * (26 / cv.Scale));
            using (Pen pa = new Pen(GEntry, 2.5f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(pa, a, b);
            if (!exit.HasValue) return;
            double e = ExitLen(oriented, entry, exit.Value);
            if (Math.Abs(e) < 1e-4) return;
            List<Seg> part = e > 0 ? c.Upto(exit.Value) : oriented.StartAt(exit.Value).Upto(entry);
            using (Pen pp = e > 0 ? new Pen(Theme.Orange, 4f) : new Pen(GExit, 4f) { DashStyle = DashStyle.Dash })
                foreach (Seg s in part) cv.DrawSeg(g, pp, s);
            if (part.Count > 0)
            {
                PointF m = cv.ToScr(part[part.Count / 2].PointAt(0.5));
                string t = e > 0 ? "перекрытие " + Geo.F(Math.Round(e, 1)) + " мм" : "перемычка " + Geo.F(Math.Round(-e, 1)) + " мм (не режется)";
                Label(g, f, t, m.X + 8, m.Y + 6, e > 0 ? Theme.Orange : GExit);
            }
        }

        public static void Label(Graphics g, Font f, string t, float x, float y, Color c)
        {
            SizeF sz = g.MeasureString(t, f);
            using (Brush bg = new SolidBrush(Color.FromArgb(215, 25, 29, 35))) g.FillRectangle(bg, x - 3, y - 2, sz.Width + 6, sz.Height + 3);
            using (Brush b = new SolidBrush(c)) g.DrawString(t, f, b, x, y);
        }
    }

    // ---------- команда ВХОД / ВЫХОД (по шагам, с подсказкой на чертеже) ----------
    public class LeadCmd : Cmd
    {
        int step;                     // 0 — вход, 1 — заправка, 2 — выход, 3 — сход
        LeadMark m;
        List<Contour> cs;             // замкнутые контуры чертежа
        int ci = -1;                  // выбранный контур
        Contour oc;                   // он же — в направлении резки
        int done;                     // сколько контуров задано за эту команду
        string lastExit = "";

        static readonly string[] Titles = { "ВХОД", "ЗАПРАВКА", "ВЫХОД", "СХОД" };
        static readonly string[] Helps = {
            "щёлкните по линии контура — здесь проволока начнёт резать",
            "щёлкните отверстие под проволоку — или Enter: заход по свойствам «1а»",
            "щёлкните на контуре, где закончить — или Enter: там же, где вход",
            "щёлкните, куда уйти проволоке после резки — или Enter: автоматически" };

        public override string Prompt
        {
            get { return "Вход / выход, шаг " + (step + 1) + " из 4 — " + Titles[step] + ": " + Helps[step] + "  (Enter / ПКМ — пропустить, Esc — выход):"; }
        }

        public override void Begin()
        {
            cs = new List<Contour>();
            foreach (Contour c in Geo.Chain(Cv.Ents, 0.01)) if (c.Closed) cs.Add(c);
            if (cs.Count == 0) { F.Msg("Нет замкнутых контуров — сначала начертите или откройте деталь."); F.EndCmd(); return; }
            F.Msg("Вход / выход проволоки: 4 шага, подсказка — вверху слева на чертеже. Готовые точки потом можно тянуть мышью, правая кнопка по точке — меню.");
        }

        V2 OnContour(V2 p, int only, out int k, out double d)
        {
            V2 q;
            if (only >= 0) { List<Contour> one = new List<Contour> { cs[only] }; LeadGeo.Nearest(one, p, out d, out q); k = only; return q; }
            k = LeadGeo.Nearest(cs, p, out d, out q); return q;
        }

        public override void Point(V2 p)
        {
            int k; double d;
            switch (step)
            {
                case 0:
                    {
                        V2 q = OnContour(p, -1, out k, out d);
                        if (k < 0 || d > MainForm.MarkReach) { F.Msg("  Рядом нет замкнутого контура — щёлкните по линии детали."); return; }
                        ci = k; oc = LeadGeo.Oriented(cs[ci], F.S); m = new LeadMark { Entry = q }; step = 1; Last = q;
                        break;
                    }
                case 1: m.Thread = p; step = 2; Last = m.Entry; break;
                case 2:
                    {
                        V2 q = OnContour(p, ci, out k, out d);
                        m.Exit = V2.Dist(q, m.Entry) < 1e-4 ? (V2?)null : q; step = 3; Last = m.Exit ?? m.Entry;
                        break;
                    }
                default: m.Out = p; Finish(); break;
            }
            Cv.Invalidate();
        }

        public override void Enter()
        {
            if (step == 0) { F.EndCmd(); return; }
            if (step == 3) { Finish(); return; }
            step++;
            Cv.Invalidate();
        }

        public override bool Text(string t)
        {
            if (step == 0 && Is(t, "У", "D", "УДАЛИТЬ", "DELETE"))
            {
                int n = Cv.LeadMarks.Count; Cv.LeadMarks.Clear(); F.GeometryChanged();
                F.Msg("  Удалено входов / выходов: " + n + ". Заход — автоматически."); F.EndCmd(); return true;
            }
            if (step > 0 && Is(t, "Н", "B", "НАЗАД", "BACK")) { step--; if (step == 0) m = null; Cv.Invalidate(); return true; }
            return false;
        }

        void Finish()
        {
            // одна запись на контур: старые вход/выход и точка заправки этого контура заменяются
            for (int i = Cv.LeadMarks.Count - 1; i >= 0; i--) { int k; double d; OnContour(Cv.LeadMarks[i].Entry, -1, out k, out d); if (k == ci && d < 0.01) Cv.LeadMarks.RemoveAt(i); }
            for (int i = Cv.ManualStarts.Count - 1; i >= 0; i--) { int k; double d; OnContour(Cv.ManualStarts[i], -1, out k, out d); if (k == ci) Cv.ManualStarts.RemoveAt(i); }
            Cv.LeadMarks.Add(m);
            double e = m.Exit.HasValue ? LeadGeo.ExitLen(oc, m.Entry, m.Exit.Value) : 0;
            lastExit = e > 0 ? "перекрытие " + Geo.F(Math.Round(e, 1)) + " мм" : e < 0 ? "перемычка " + Geo.F(Math.Round(-e, 1)) + " мм" : "выход в точке входа";
            done++;
            F.Msg("  Контур " + done + ": вход задан, " + (m.Thread.HasValue ? "заправка вручную" : "заправка по свойствам") + ", " + lastExit + (m.Out.HasValue ? ", сход вручную" : "") + ". Траектория обновлена.");
            F.BuildPath(false);      // сразу показать, что получилось
            F.LeadStep();            // шаг истории на каждый контур
            step = 0; m = null; ci = -1; oc = null; Last = null;
            Cv.Invalidate();
        }

        public override void Rubber(Graphics g, V2 c)
        {
            int k; double d;
            using (Font f = new Font(MainForm.UiFont, 9f, FontStyle.Bold))
            using (Pen pe = new Pen(LeadGeo.GEntry, 2.5f))
            using (Pen px = new Pen(LeadGeo.GExit, 2.5f))
            using (Pen pl = new Pen(Theme.Orange, 2f))
            {
                if (step == 0)
                {
                    V2 q = OnContour(c, -1, out k, out d);
                    if (k >= 0 && d <= MainForm.MarkReach) { Ring(g, pe, q); LeadGeo.Draw(g, Cv, LeadGeo.Oriented(cs[k], F.S), q, null, f); Tag(g, f, q, "ВХОД сюда", LeadGeo.GEntry); }
                }
                else
                {
                    Ring(g, pe, m.Entry);
                    if (step == 1) { Line(g, c, m.Entry); g.DrawLine(pl, Cv.ToScr(c), Cv.ToScr(m.Entry)); Tag(g, f, c, "ЗАПРАВКА · заход " + Geo.F(Math.Round(V2.Dist(c, m.Entry), 1)) + " мм", Theme.Orange); LeadGeo.Draw(g, Cv, oc, m.Entry, null, f); }
                    else
                    {
                        if (m.Thread.HasValue) g.DrawLine(pl, Cv.ToScr(m.Thread.Value), Cv.ToScr(m.Entry));
                        if (step == 2)
                        {
                            V2 q = OnContour(c, ci, out k, out d);
                            Ring(g, px, q); LeadGeo.Draw(g, Cv, oc, m.Entry, q, f); Tag(g, f, q, "ВЫХОД", LeadGeo.GExit);
                        }
                        else
                        {
                            V2 ex = m.Exit ?? m.Entry; LeadGeo.Draw(g, Cv, oc, m.Entry, m.Exit, f);
                            Ring(g, px, ex); using (Pen po = new Pen(LeadGeo.GExit, 2f) { DashStyle = DashStyle.Dash }) g.DrawLine(po, Cv.ToScr(ex), Cv.ToScr(c));
                            Tag(g, f, c, "СХОД", LeadGeo.GExit);
                        }
                    }
                }
                Panel(g, f);
            }
        }

        void Tag(Graphics g, Font f, V2 w, string t, Color col) { PointF s = Cv.ToScr(w); LeadGeo.Label(g, f, t, s.X + 12, s.Y - 24, col); }
        void Ring(Graphics g, Pen p, V2 w) { PointF s = Cv.ToScr(w); g.DrawEllipse(p, s.X - 8, s.Y - 8, 16, 16); }

        /// <summary>Подсказка по шагам в левом верхнем углу чертежа.</summary>
        void Panel(Graphics g, Font fb)
        {
            using (Font f = new Font(MainForm.UiFont, 9f))
            {
                int x = Cv.RulersOn ? 30 : 8, y = Cv.RulersOn ? 30 : 8, w = 470, lh = 21;
                Rectangle r = new Rectangle(x, y, w, 30 + 4 * lh + 26);
                using (Brush bg = new SolidBrush(Color.FromArgb(230, 30, 35, 41))) g.FillRectangle(bg, r);
                using (Pen bp = new Pen(Theme.Accent)) g.DrawRectangle(bp, r);
                using (Brush bt = new SolidBrush(Theme.Text)) g.DrawString("ВХОД / ВЫХОД ПРОВОЛОКИ" + (done > 0 ? "   ·   готово контуров: " + done : ""), fb, bt, x + 8, y + 6);
                for (int i = 0; i < 4; i++)
                {
                    bool cur = i == step, ok = i < step;
                    Color col = cur ? Theme.Orange : ok ? LeadGeo.GEntry : Theme.Muted;
                    string mark = cur ? "▶" : ok ? "✓" : "○";
                    using (Brush b = new SolidBrush(col)) g.DrawString(mark + " " + (i + 1) + ". " + Titles[i] + " — " + Helps[i], cur ? fb : f, b, x + 10, y + 30 + i * lh);
                }
                using (Brush bm = new SolidBrush(Theme.Muted))
                    g.DrawString("Enter / правая кнопка — пропустить шаг   ·   Н — шаг назад   ·   Esc — выход", f, bm, x + 10, y + 34 + 4 * lh);
            }
        }
    }

    // ---------- команда НОЛЬ: деталь к началу координат ----------
    public class OriginCmd : Cmd
    {
        public override string Prompt { get { return "Какая точка станет началом координат X0 Y0 — щёлкните (привязки работают)  [Центр габарита / Левый нижний угол]:"; } }
        public override void Point(V2 p) { Apply(p, "выбранная точка"); }
        public override bool Text(string t)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Seg s in Cv.Ents) s.Bounds(ref x0, ref y0, ref x1, ref y1);
            if (x0 > x1) { F.Msg("  Чертёж пуст."); return true; }
            if (Is(t, "Ц", "C", "ЦЕНТР", "CENTER")) { Apply(new V2((x0 + x1) / 2, (y0 + y1) / 2), "центр габарита"); return true; }
            if (Is(t, "Л", "L", "ЛЕВЫЙ", "LL")) { Apply(new V2(x0, y0), "левый нижний угол габарита"); return true; }
            return false;
        }
        void Apply(V2 p, string what)
        {
            V2 d = new V2(-p.X, -p.Y);
            if (d.Len < 1e-9) { F.Msg("  Эта точка уже в начале координат."); F.EndCmd(); return; }
            F.PushUndo();
            foreach (Seg s in Cv.Ents) s.Move(d);
            foreach (Dim dm in Cv.Dims) if (dm.A == null) { dm.P1 = dm.P1 + d; dm.P2 = dm.P2 + d; dm.C = dm.C + d; dm.V = dm.V + d; dm.Pos = dm.Pos + d; }
            for (int i = 0; i < Cv.ManualStarts.Count; i++) Cv.ManualStarts[i] = Cv.ManualStarts[i] + d;
            foreach (SideMark sm in Cv.SideMarks) sm.P = sm.P + d;
            foreach (LeadMark lm in Cv.LeadMarks) lm.Move(d);
            Cv.Center = Cv.Center + d;   // деталь остаётся на месте экрана, сдвигается ноль
            F.Msg("  Начало координат X0 Y0 — " + what + " (деталь сдвинута на X" + Geo.F(Math.Round(d.X, 3)) + " Y" + Geo.F(Math.Round(d.Y, 3)) + "). Ctrl+Z — вернуть.");
            F.GeometryChanged();
            F.EndCmd();
        }
    }

    public static class OriginDraw
    {
        /// <summary>Начало координат как во Fusion 360: точка в центре, стрелки X (красная) и Y (зелёная).</summary>
        public static void Draw(Graphics g, CadCanvas cv)
        {
            PointF o = cv.ToScr(new V2(0, 0));
            if (o.X < -60 || o.Y < -60 || o.X > cv.Width + 60 || o.Y > cv.Height + 60) return;
            const float L = 46;
            using (Pen px = new Pen(Color.FromArgb(235, 80, 80), 2.2f) { EndCap = LineCap.ArrowAnchor })
            using (Pen py = new Pen(Color.FromArgb(90, 210, 90), 2.2f) { EndCap = LineCap.ArrowAnchor })
            {
                g.DrawLine(px, o.X, o.Y, o.X + L, o.Y);
                g.DrawLine(py, o.X, o.Y, o.X, o.Y - L);
            }
            // плоскость XY — полупрозрачный квадрат, как плоскости у начала координат во Fusion
            using (Brush pl = new SolidBrush(Color.FromArgb(40, 120, 170, 255))) g.FillRectangle(pl, o.X + 4, o.Y - 26, 22, 22);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 200, 60))) g.FillEllipse(b, o.X - 5, o.Y - 5, 10, 10);
            using (Pen pb = new Pen(Color.FromArgb(40, 40, 40), 1f)) g.DrawEllipse(pb, o.X - 5, o.Y - 5, 10, 10);
            using (Font f = new Font(MainForm.UiFont, 9f, FontStyle.Bold))
            {
                using (Brush bx = new SolidBrush(Color.FromArgb(235, 80, 80))) g.DrawString("X", f, bx, o.X + L + 2, o.Y - 9);
                using (Brush by = new SolidBrush(Color.FromArgb(90, 210, 90))) g.DrawString("Y", f, by, o.X - 6, o.Y - L - 18);
                using (Brush b0 = new SolidBrush(Theme.Muted)) g.DrawString("0", f, b0, o.X - 15, o.Y + 2);
            }
        }

        /// <summary>Значки ручных входов / выходов на чертеже.</summary>
        public static void Leads(Graphics g, CadCanvas cv)
        {
            if (cv.HandleHover.HasValue)
            {
                PointF h = cv.ToScr(cv.HandleHover.Value);
                using (Pen ph = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dot }) g.DrawEllipse(ph, h.X - 12, h.Y - 12, 24, 24);
            }
            if (cv.LeadMarks.Count == 0) return;
            using (Pen pe = new Pen(Color.FromArgb(90, 220, 120), 2f))
            using (Pen px = new Pen(Color.FromArgb(255, 95, 95), 2f))
            using (Pen pt = new Pen(Theme.Orange, 1.5f))
            using (Pen pl = new Pen(Color.FromArgb(150, 255, 107, 53), 1f) { DashStyle = DashStyle.Dot })
            using (Font f = new Font(MainForm.UiFont, 8.5f, FontStyle.Bold))
            using (Brush be = new SolidBrush(Color.FromArgb(90, 220, 120)))
            using (Brush bx = new SolidBrush(Color.FromArgb(255, 95, 95)))
                foreach (LeadMark m in cv.LeadMarks)
                {
                    PointF e = cv.ToScr(m.Entry);
                    if ((cv.Path == null || cv.Dragging) && m.Thread.HasValue) g.DrawLine(pl, cv.ToScr(m.Thread.Value), e);
                    g.DrawEllipse(pe, e.X - 6, e.Y - 6, 12, 12); g.DrawString("ВХОД", f, be, e.X + 7, e.Y - 18);
                    if (m.Thread.HasValue) { PointF t = cv.ToScr(m.Thread.Value); g.DrawEllipse(pt, t.X - 5, t.Y - 5, 10, 10); g.DrawLine(pt, t.X - 7, t.Y, t.X + 7, t.Y); g.DrawLine(pt, t.X, t.Y - 7, t.X, t.Y + 7); using (Brush bo = new SolidBrush(Theme.Orange)) g.DrawString("ЗАПРАВКА", f, bo, t.X + 8, t.Y + 2); }
                    if (m.Exit.HasValue)
                    {
                        PointF x = cv.ToScr(m.Exit.Value);
                        g.DrawEllipse(px, x.X - 6, x.Y - 6, 12, 12); g.DrawString("ВЫХОД", f, bx, x.X + 7, x.Y + 2);
                    }
                    if (m.Out.HasValue)
                    {
                        PointF o = cv.ToScr(m.Out.Value); PointF from = cv.ToScr(m.Exit ?? m.Entry);
                        if (cv.Path == null || cv.Dragging) g.DrawLine(pl, from, o);
                        g.DrawLine(px, o.X - 5, o.Y - 5, o.X + 5, o.Y + 5); g.DrawLine(px, o.X - 5, o.Y + 5, o.X + 5, o.Y - 5);
                        g.DrawString("СХОД", f, bx, o.X + 8, o.Y + 2);
                    }
                }
        }
    }

    // ---------- правка готовых входов / выходов: тянуть мышью, правая кнопка — меню ----------
    public partial class MainForm
    {
        class LeadHandle { public LeadMark M; public int Part; public int Start = -1; }   // Part: 0 вход, 1 заправка, 2 выход, 3 сход, 4 точка «Заправка»
        LeadHandle dragH; List<Contour> dragCs; int dragC = -1;
        static readonly string[] PartName = { "ВХОД", "ЗАПРАВКА", "ВЫХОД", "СХОД", "точка заправки" };

        void InitLeadEdit()
        {
            Cv.OverHandle = delegate (Point px) { LeadHandle h = HandleAt(px); V2? hv = h == null ? (V2?)null : PosOf(h); if (!Equals(hv, Cv.HandleHover)) { Cv.HandleHover = hv; Cv.Invalidate(); } return h != null; };
            Cv.OnGrab = delegate (Point px)
            {
                LeadHandle h = HandleAt(px); if (h == null) return false;
                dragH = h; dragCs = null; dragC = -1;
                if (h.Part == 0 || h.Part == 2)
                {
                    dragCs = new List<Contour>(); foreach (Contour c in Geo.Chain(Cv.Ents, 0.01)) if (c.Closed) dragCs.Add(c);
                    double d; V2 q; dragC = LeadGeo.Nearest(dragCs, h.M.Entry, out d, out q);
                }
                stInfo.Text = "Тяните " + PartName[h.Part] + " мышью, отпустите — траектория пересчитается";
                return true;
            };
            Cv.OnDragTo = delegate (V2 w, int z)
            {
                LeadHandle h = dragH; if (h == null) return;
                if ((h.Part == 0 || h.Part == 2) && dragC >= 0)
                {
                    double d; V2 q; LeadGeo.Nearest(new List<Contour> { dragCs[dragC] }, w, out d, out q);
                    if (h.Part == 0) h.M.Entry = q; else h.M.Exit = q;
                    if (h.M.Exit.HasValue)
                    {
                        double e = LeadGeo.ExitLen(LeadGeo.Oriented(dragCs[dragC], S), h.M.Entry, h.M.Exit.Value);
                        stInfo.Text = e > 0 ? "Перекрытие " + Geo.F(Math.Round(e, 1)) + " мм" : e < 0 ? "Перемычка " + Geo.F(Math.Round(-e, 1)) + " мм (не режется)" : "Выход в точке входа";
                    }
                }
                else if (h.Part == 1) h.M.Thread = w;
                else if (h.Part == 3) h.M.Out = w;
                else if (h.Part == 4 && h.Start < Cv.ManualStarts.Count) Cv.ManualStarts[h.Start] = w;
                Cv.HandleHover = w;
            };
            Cv.OnDrop = delegate (V2 w, int z)
            {
                LeadHandle h = dragH; dragH = null; if (h == null) return;
                Msg("Перенесено: " + PartName[h.Part] + " — X" + Geo.F(Math.Round(PosOf(h).X, 3)) + " Y" + Geo.F(Math.Round(PosOf(h).Y, 3)) + (stInfo.Text.StartsWith("Пере") || stInfo.Text.StartsWith("Выход") ? " · " + stInfo.Text : ""));
                LeadChanged();
            };
            Cv.OnContext = delegate (Point px) { LeadHandle h = HandleAt(px); if (h == null) return false; LeadMenu(h, Cv.PointToScreen(px)); return true; };
        }

        V2 PosOf(LeadHandle h)
        {
            if (h.Part == 4) return Cv.ManualStarts[h.Start];
            return h.Part == 0 ? h.M.Entry : h.Part == 1 ? h.M.Thread.Value : h.Part == 2 ? h.M.Exit.Value : h.M.Out.Value;
        }

        LeadHandle HandleAt(Point px)
        {
            LeadHandle best = null; double bd = 10;
            Act2<LeadHandle, V2> tr = delegate (LeadHandle h, V2 w) { PointF s = Cv.ToScr(w); double d = Math.Sqrt((s.X - px.X) * (s.X - px.X) + (s.Y - px.Y) * (s.Y - px.Y)); if (d < bd) { bd = d; best = h; } };
            foreach (LeadMark m in Cv.LeadMarks)
            {
                tr(new LeadHandle { M = m, Part = 0 }, m.Entry);
                if (m.Thread.HasValue) tr(new LeadHandle { M = m, Part = 1 }, m.Thread.Value);
                if (m.Exit.HasValue) tr(new LeadHandle { M = m, Part = 2 }, m.Exit.Value);
                if (m.Out.HasValue) tr(new LeadHandle { M = m, Part = 3 }, m.Out.Value);
            }
            for (int i = 0; i < Cv.ManualStarts.Count; i++) tr(new LeadHandle { Part = 4, Start = i }, Cv.ManualStarts[i]);
            return best;
        }

        /// <summary>Шаг истории «Вход / выход» (после каждого готового контура).</summary>
        public void LeadStep() { opName = "Вход / выход"; opCmd = "ВХОД"; Checkpoint(); }

        void LeadChanged()
        {
            Cv.HandleHover = null;
            if (Cv.Path != null || Cv.LeadMarks.Count > 0) BuildPath(false); else Cv.Invalidate();
            stInfo.Text = "";
            opName = "Вход / выход"; opCmd = "ВХОД"; Checkpoint();
            Cv.Invalidate();
        }

        void LeadMenu(LeadHandle h, Point at)
        {
            ContextMenuStrip mn = new ContextMenuStrip(); Theme.Strip(mn); mn.ShowImageMargin = false;
            Act2<string, EventHandler> add = delegate (string t, EventHandler e) { ToolStripMenuItem it = new ToolStripMenuItem(t) { ForeColor = Theme.Text }; it.Click += e; mn.Items.Add(it); };
            if (h.Part == 4)
            {
                mn.Items.Add(new ToolStripLabel("Точка заправки") { ForeColor = Theme.Muted }); mn.Items.Add(new ToolStripSeparator());
                add("Удалить эту точку заправки", delegate { Cv.ManualStarts.RemoveAt(h.Start); Msg("Точка заправки удалена — заход по свойствам."); LeadChanged(); });
                add("Задать вход и выход для этого контура…", delegate { Run("ВХОД"); });
            }
            else
            {
                LeadMark m = h.M;
                mn.Items.Add(new ToolStripLabel("Вход / выход контура  ·  " + PartName[h.Part]) { ForeColor = Theme.Muted }); mn.Items.Add(new ToolStripSeparator());
                if (m.Thread.HasValue) add("Заправка — по свойствам «1а» (тип и длина захода)", delegate { m.Thread = null; Msg("Заправка — по свойствам захода."); LeadChanged(); });
                if (m.Exit.HasValue) add("Выход — в точке входа (полный обход, без перекрытия)", delegate { m.Exit = null; Msg("Выход — в точке входа."); LeadChanged(); });
                if (m.Out.HasValue) add("Сход — автоматически", delegate { m.Out = null; Msg("Сход — автоматически."); LeadChanged(); });
                if (!m.Exit.HasValue) add("Добавить выход / сход… (задать заново по шагам)", delegate { Cv.LeadMarks.Remove(m); LeadChanged(); Run("ВХОД"); });
                else add("Задать заново по шагам…", delegate { Cv.LeadMarks.Remove(m); LeadChanged(); Run("ВХОД"); });
                mn.Items.Add(new ToolStripSeparator());
                add("Удалить вход / выход этого контура", delegate { Cv.LeadMarks.Remove(m); Msg("Вход / выход контура удалён — заход автоматически."); LeadChanged(); });
                ToolStripLabel tipL = new ToolStripLabel("Подсказка: точки можно тянуть мышью") { ForeColor = Theme.Muted }; mn.Items.Add(new ToolStripSeparator()); mn.Items.Add(tipL);
            }
            mn.Closed += delegate { mn.BeginInvoke((MethodInvoker)delegate { mn.Dispose(); }); };
            mn.Show(at);
        }
    }
}
