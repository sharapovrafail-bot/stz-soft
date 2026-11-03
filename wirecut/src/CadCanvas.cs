// STZ WireCut — чертёжное поле в стиле AutoCAD: зум колесом, панорама средней кнопкой, привязки, рамка выбора
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireCut
{
    public enum SnapKind { None, End, Mid, Center, Quad, Grid, Origin }

    public class CadCanvas : Control
    {
        public List<Seg> Ents = new List<Seg>();
        public Toolpath Path;
        public List<V2> ManualStarts = new List<V2>();
        public List<V2> Marks = new List<V2>();
        public List<Dim> Dims = new List<Dim>();        // размеры на чертеже
        public List<SideMark> SideMarks = new List<SideMark>();   // сторона захода, заданная для контура
        public List<LeadMark> LeadMarks = new List<LeadMark>();   // вход / выход, заданные вручную
        public bool RulersOn = true, ExtentsOn = false;  // линейки и габарит
        public bool AutoDims = true;                      // размеры на новых объектах (как во Fusion)
        public Seg Hover;                                // элемент под курсором
        public Dim EditDim, HoverDim;                    // изменяемый размер / размер под курсором
        public List<Dim> QuickShown = new List<Dim>();   // временные размеры выделенных объектов
        public Fn1<Dim, bool> OnDimPick;                 // щелчок по числу размера
        // перетаскивание точек входа / выхода / заправки и меню по правой кнопке
        public Fn1<Point, bool> OnGrab, OnContext, OverHandle;
        public Act2<V2, int> OnDragTo, OnDrop;
        public bool Dragging;
        public V2? HandleHover;

        public Dim DimAt(Point p)
        {
            for (int i = Dims.Count - 1; i >= 0; i--) if (Dims[i].LabelRect.Contains(p)) return Dims[i];
            foreach (Dim d in QuickShown) if (d.LabelRect.Contains(p)) return d;
            return null;
        }   // проблемные места после ПРОВЕРКИ

        public double Scale = 4;        // пикселей на мм
        public V2 Center = new V2(50, 50);
        public bool GridOn = true, SnapOn = false, OrthoOn = false, OsnapOn = true;
        public double GridStep = 10, SnapStep = 1;

        public V2 CurW;                 // мировая точка курсора (с привязкой)
        public SnapKind CurSnap = SnapKind.None;
        public Point MousePx;
        bool mouseIn;

        // симуляция
        public bool SimOn; public double SimPos; public List<Seg> SimSegs = new List<Seg>(); public List<double> SimCum = new List<double>();

        // обратные вызовы в главное окно
        public Fn1<V2, bool> OnPick;                      // клик точкой (true — команда приняла точку)
        public Act2<Graphics, V2> OnRubber;               // резиновая нить команды
        public Act2<V2, V2> OnWindow;                     // рамка выбора (две мировые точки)
        public Fn<bool> CommandActive;
        public Act2<KeyEventArgs, int> OnKey;
        public Act2<char, int> OnChar;
        public Act2<V2, int> OnMoveWorld;

        Point panStart; V2 panCenter; bool panning;
        Point? winStart; Point winCur;

        static readonly Color Bg = Theme.Background;
        public string RubberHint = "";

        public CadCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Bg;
            CurW = new V2();
            base.Cursor = Cursors.Cross;
        }

        // ---------- преобразования ----------
        public PointF ToScr(V2 w) { return new PointF((float)((w.X - Center.X) * Scale + Width / 2.0), (float)(Height / 2.0 - (w.Y - Center.Y) * Scale)); }
        public V2 ToWorld(Point p) { return new V2((p.X - Width / 2.0) / Scale + Center.X, (Height / 2.0 - p.Y) / Scale + Center.Y); }

        public void ZoomExtents()
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Seg s in Ents) s.Bounds(ref x0, ref y0, ref x1, ref y1);
            if (Path != null) foreach (Move m in Path.Moves) if (m.S != null) m.S.Bounds(ref x0, ref y0, ref x1, ref y1);
            foreach (Dim d in Dims) { if (d.Pos.X < x0) x0 = d.Pos.X; if (d.Pos.X > x1) x1 = d.Pos.X; if (d.Pos.Y < y0) y0 = d.Pos.Y; if (d.Pos.Y > y1) y1 = d.Pos.Y; }
            if (x0 > x1) { x0 = 0; y0 = 0; x1 = 100; y1 = 100; }
            int margin = (ExtentsOn || Dims.Count > 0 || RulersOn) ? 130 : 40;   // место под размеры и линейки
            double w = Math.Max(x1 - x0, 1), h = Math.Max(y1 - y0, 1);
            Center = new V2((x0 + x1) / 2, (y0 + y1) / 2);
            Scale = Math.Min((Width - margin) / w, (Height - margin) / h);
            if (Scale <= 0 || double.IsInfinity(Scale)) Scale = 4;
            Invalidate();
        }

        // ---------- мышь ----------
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            V2 before = ToWorld(e.Location);
            double k = e.Delta > 0 ? 1.25 : 0.8;
            Scale = Math.Max(1e-3, Math.Min(1e5, Scale * k));
            V2 after = ToWorld(e.Location);
            Center = Center + (before - after);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Middle)
            {
                if (e.Clicks == 2) { ZoomExtents(); return; }
                panning = true; panStart = e.Location; panCenter = Center; base.Cursor = Cursors.SizeAll; return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (CommandActive != null && CommandActive())
                {
                    if (OnPick != null) OnPick(CurW);
                    Invalidate(); return;
                }
                // точка входа / выхода / заправки — тянуть мышью
                if (OnGrab != null && OnGrab(e.Location)) { Dragging = true; base.Cursor = Cursors.SizeAll; Invalidate(); return; }
                // щелчок по числу размера — изменить размер
                Dim hd = DimAt(e.Location);
                if (hd != null && OnDimPick != null) { OnDimPick(hd); Invalidate(); return; }
                // выбор объекта кликом или начало рамки
                Seg hit = HitTest(e.Location);
                if (hit != null)
                {
                    if ((ModifierKeys & Keys.Shift) != 0) hit.Sel = false; else hit.Sel = !hit.Sel;
                    Invalidate(); return;
                }
                winStart = e.Location; winCur = e.Location;
            }
            if (e.Button == MouseButtons.Right && !(CommandActive != null && CommandActive()) && OnContext != null && OnContext(e.Location)) return;   // меню точки
            if (e.Button == MouseButtons.Right && OnKey != null) OnKey(new KeyEventArgs(Keys.Enter), 0); // ПКМ = Enter, как в AutoCAD
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle) { panning = false; base.Cursor = Cursors.Cross; }
            if (e.Button == MouseButtons.Left && Dragging) { Dragging = false; base.Cursor = Cursors.Cross; if (OnDrop != null) OnDrop(CurW, 0); Invalidate(); return; }
            if (e.Button == MouseButtons.Left && winStart.HasValue)
            {
                Point a = winStart.Value; winStart = null;
                if (Math.Abs(a.X - e.X) > 3 || Math.Abs(a.Y - e.Y) > 3)
                {
                    bool crossing = e.X < a.X; // справа налево — секущая рамка
                    V2 w0 = ToWorld(a), w1 = ToWorld(e.Location);
                    double x0 = Math.Min(w0.X, w1.X), x1 = Math.Max(w0.X, w1.X), y0 = Math.Min(w0.Y, w1.Y), y1 = Math.Max(w0.Y, w1.Y);
                    foreach (Seg s in Ents)
                    {
                        double bx0 = double.MaxValue, by0 = double.MaxValue, bx1 = double.MinValue, by1 = double.MinValue;
                        s.Bounds(ref bx0, ref by0, ref bx1, ref by1);
                        bool inside = bx0 >= x0 && bx1 <= x1 && by0 >= y0 && by1 <= y1;
                        bool touch = !(bx1 < x0 || bx0 > x1 || by1 < y0 || by0 > y1);
                        if (inside || (crossing && touch)) s.Sel = true;
                    }
                    if (OnWindow != null) OnWindow(w0, w1);
                }
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            MousePx = e.Location; mouseIn = true;
            if (panning)
            {
                Center = panCenter + new V2(-(e.X - panStart.X) / Scale, (e.Y - panStart.Y) / Scale);
                Invalidate(); return;
            }
            if (winStart.HasValue) winCur = e.Location;
            CurW = SnapPoint(e.Location, out CurSnap);
            if (Dragging) { if (OnDragTo != null) OnDragTo(CurW, 0); if (OnMoveWorld != null) OnMoveWorld(CurW, 0); Invalidate(); return; }
            bool overH = (CommandActive == null || !CommandActive()) && !winStart.HasValue && OverHandle != null && OverHandle(e.Location);
            HoverDim = winStart.HasValue ? null : DimAt(e.Location);
            Hover = (winStart.HasValue || HoverDim != null) ? null : HitTest(e.Location);
            base.Cursor = overH ? Cursors.SizeAll : HoverDim != null && (CommandActive == null || !CommandActive()) ? Cursors.Hand : Cursors.Cross;
            if (OnMoveWorld != null) OnMoveWorld(CurW, 0);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { mouseIn = false; Invalidate(); }

        protected override bool IsInputKey(Keys k) { return true; }
        protected override void OnKeyDown(KeyEventArgs e) { if (OnKey != null) OnKey(e, 0); }
        protected override void OnKeyPress(KeyPressEventArgs e) { if (OnChar != null) OnChar(e.KeyChar, 0); }

        public Seg HitTest(Point p)
        {
            V2 w = ToWorld(p); double tol = 6 / Scale;
            Seg best = null; double bd = tol;
            foreach (Seg s in Ents)
            {
                double t; V2 q; double d = s.Project(w, out t, out q);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ---------- объектная привязка ----------
        V2 SnapPoint(Point px, out SnapKind kind)
        {
            V2 w = ToWorld(px);
            kind = SnapKind.None;
            if (OsnapOn)
            {
                double tol = 10 / Scale, bd = tol; V2 best = w;
                foreach (Seg s in Ents)
                {
                    Try(s.Start, SnapKind.End, w, ref bd, ref best, ref kind);
                    Try(s.End, SnapKind.End, w, ref bd, ref best, ref kind);
                    if (!s.IsCircle) Try(s.PointAt(0.5), SnapKind.Mid, w, ref bd, ref best, ref kind);
                    if (s.IsArc)
                    {
                        Try(s.C, SnapKind.Center, w, ref bd, ref best, ref kind);
                        for (int k = 0; k < 4; k++)
                        {
                            double a = k * Math.PI / 2;
                            if (s.AngleInside(a)) Try(s.C + V2.Polar(a, s.R), SnapKind.Quad, w, ref bd, ref best, ref kind);
                        }
                    }
                }
                if (!Dragging) foreach (V2 st in ManualStarts) Try(st, SnapKind.End, w, ref bd, ref best, ref kind);
                if (!Dragging) foreach (LeadMark lm in LeadMarks)
                {
                    Try(lm.Entry, SnapKind.End, w, ref bd, ref best, ref kind);
                    if (lm.Thread.HasValue) Try(lm.Thread.Value, SnapKind.End, w, ref bd, ref best, ref kind);
                    if (lm.Exit.HasValue) Try(lm.Exit.Value, SnapKind.End, w, ref bd, ref best, ref kind);
                }
                Try(new V2(0, 0), SnapKind.Origin, w, ref bd, ref best, ref kind);   // начало координат
                if (kind != SnapKind.None) return best;
            }
            if (SnapOn && SnapStep > 0)
            {
                kind = SnapKind.Grid;
                return new V2(Math.Round(w.X / SnapStep) * SnapStep, Math.Round(w.Y / SnapStep) * SnapStep);
            }
            return w;
        }

        static void Try(V2 p, SnapKind k, V2 w, ref double bd, ref V2 best, ref SnapKind kind)
        {
            double d = V2.Dist(p, w);
            if (d < bd) { bd = d; best = p; kind = k; }
        }

        // ---------- отрисовка ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (GridOn) DrawGrid(g);
            DrawAxes(g);

            using (Pen pEnt = new Pen(Color.FromArgb(235, 235, 235), 1.2f))
            using (Pen pSel = new Pen(Color.FromArgb(80, 160, 255), 2f) { DashStyle = DashStyle.Dash })
                foreach (Seg s in Ents) DrawSeg(g, s.Sel ? pSel : pEnt, s);

            if (Path != null) DrawPath(g);
            OriginDraw.Draw(g, this);
            OriginDraw.Leads(g, this);
            if (Marks.Count > 0)
                using (Pen pm = new Pen(Color.FromArgb(255, 80, 80), 2f))
                    foreach (V2 mk in Marks) { PointF q = ToScr(mk); g.DrawEllipse(pm, q.X - 9, q.Y - 9, 18, 18); }
            Dims.RemoveAll(delegate (Dim dm) { return !dm.Sync(this); });   // размеры удалённых объектов
            foreach (Dim dm in Dims) DimDraw.Draw(g, this, dm, false);
            int selN = 0; QuickShown.Clear();
            foreach (Seg s in Ents) if (s.Sel && ++selN <= 60) { Dim q = DimDraw.Quick(this, s); if (q != null) { if (EditDim != null && EditDim.A == s && !Dims.Contains(EditDim)) q = EditDim; DimDraw.Draw(g, this, q, true); QuickShown.Add(q); } }
            if (ExtentsOn) DimDraw.Extents(g, this);

            using (Pen ps = new Pen(Theme.Orange, 1.5f))
                foreach (V2 st in ManualStarts) { PointF p = ToScr(st); g.DrawEllipse(ps, p.X - 5, p.Y - 5, 10, 10); g.DrawLine(ps, p.X - 7, p.Y, p.X + 7, p.Y); g.DrawLine(ps, p.X, p.Y - 7, p.X, p.Y + 7); }

            using (Font fm = new Font(MainForm.UiFont, 9.5f, FontStyle.Bold))
                foreach (SideMark sm in SideMarks)
                {
                    PointF q = ToScr(sm.P);
                    string t = sm.Outside ? "ЗАХОД СНАРУЖИ" : "ЗАХОД ИЗНУТРИ";
                    SizeF sz = g.MeasureString(t, fm);
                    using (Brush bg = new SolidBrush(Color.FromArgb(220, sm.Outside ? 40 : 120, 90, sm.Outside ? 160 : 40)))
                        g.FillRectangle(bg, q.X + 6, q.Y - sz.Height - 4, sz.Width + 6, sz.Height + 2);
                    g.DrawString(t, fm, Brushes.White, q.X + 9, q.Y - sz.Height - 3);
                    g.FillEllipse(Brushes.White, q.X - 3, q.Y - 3, 6, 6);
                }
            if (SimOn) DrawSim(g);

            if (OnRubber != null && mouseIn) OnRubber(g, CurW);

            if (winStart.HasValue)
            {
                Point a = winStart.Value;
                Rectangle r = new Rectangle(Math.Min(a.X, winCur.X), Math.Min(a.Y, winCur.Y), Math.Abs(a.X - winCur.X), Math.Abs(a.Y - winCur.Y));
                bool crossing = winCur.X < a.X;
                using (Brush b = new SolidBrush(crossing ? Color.FromArgb(40, 80, 200, 80) : Color.FromArgb(40, 80, 120, 255)))
                    g.FillRectangle(b, r);
                using (Pen p = new Pen(crossing ? Color.LightGreen : Color.CornflowerBlue) { DashStyle = crossing ? DashStyle.Dash : DashStyle.Solid })
                    g.DrawRectangle(p, r);
            }

            DrawUcs(g);
            DrawLegend(g);
            if (RulersOn) DimDraw.Rulers(g, this, MousePx, mouseIn);
            if (mouseIn && !panning) { DrawCrosshair(g); DrawCursorHint(g); }
        }

        void DrawGrid(Graphics g)
        {
            double step = GridStep;
            while (step * Scale < 12) step *= 5;
            while (step * Scale > 120) step /= 5;
            V2 w0 = ToWorld(new Point(0, Height)), w1 = ToWorld(new Point(Width, 0));
            using (Pen p = new Pen(Theme.Grid))
            using (Pen p5 = new Pen(Theme.MajorGrid))
            {
                long i0 = (long)Math.Floor(w0.X / step), i1 = (long)Math.Ceiling(w1.X / step);
                if (i1 - i0 < 400)
                    for (long i = i0; i <= i1; i++) { float x = ToScr(new V2(i * step, 0)).X; g.DrawLine(i % 5 == 0 ? p5 : p, x, 0, x, Height); }
                long j0 = (long)Math.Floor(w0.Y / step), j1 = (long)Math.Ceiling(w1.Y / step);
                if (j1 - j0 < 400)
                    for (long j = j0; j <= j1; j++) { float y = ToScr(new V2(0, j * step)).Y; g.DrawLine(j % 5 == 0 ? p5 : p, 0, y, Width, y); }
            }
        }

        void DrawAxes(Graphics g)
        {
            PointF o = ToScr(new V2(0, 0));
            using (Pen px = new Pen(Color.FromArgb(120, 200, 60, 60)))
            using (Pen py = new Pen(Color.FromArgb(120, 60, 200, 60)))
            {
                g.DrawLine(px, 0, o.Y, Width, o.Y);
                g.DrawLine(py, o.X, 0, o.X, Height);
            }
        }

        public void DrawSeg(Graphics g, Pen p, Seg s)
        {
            if (s.Kind == SegKind.Line) { g.DrawLine(p, ToScr(s.P0), ToScr(s.P1)); return; }
            double rpx = s.R * Scale;
            if (rpx < 0.5) return;
            if (rpx > 20000)
            {
                // очень крупная дуга — полилинией
                int n = 64; PointF prev = ToScr(s.Start);
                for (int i = 1; i <= n; i++) { PointF q = ToScr(s.PointAt((double)i / n)); g.DrawLine(p, prev, q); prev = q; }
                return;
            }
            PointF c = ToScr(s.C);
            float sw = (float)(-s.Sw * 180 / Math.PI), st = (float)(-s.A0 * 180 / Math.PI);
            try { g.DrawArc(p, (float)(c.X - rpx), (float)(c.Y - rpx), (float)(2 * rpx), (float)(2 * rpx), st, sw); } catch { }
        }

        static readonly Color[] PassCol = Theme.Passes;

        void DrawPath(Graphics g)
        {
            using (Pen lead = new Pen(Theme.Orange, 1.3f) { DashStyle = DashStyle.Dash })
            using (Pen rapid = new Pen(Theme.Muted, 1f) { DashStyle = DashStyle.DashDot })
                foreach (Move m in Path.Moves)
                {
                    if (m.S == null) continue;
                    if (m.K == MoveKind.Rapid) { DrawSeg(g, rapid, m.S); continue; }
                    if (m.Lead) { DrawSeg(g, lead, m.S); continue; }
                    using (Pen p = new Pen(PassCol[Math.Max(0, Math.Min(3, m.Pass - 1))], m.Pass <= 1 ? 1.6f : 1.1f))
                        DrawSeg(g, p, m.S);
                }
            using (Brush b = new SolidBrush(Theme.Orange))
            using (Font f = new Font(MainForm.UiFont, 10f))
                for (int i = 0; i < Path.Starts.Count; i++)
                {
                    PointF p = ToScr(Path.Starts[i]);
                    g.FillEllipse(b, p.X - 4, p.Y - 4, 8, 8);
                    g.DrawString((i + 1).ToString(), f, b, p.X + 5, p.Y - 14);
                }
        }

        void DrawSim(Graphics g)
        {
            if (SimSegs.Count == 0) return;
            using (Pen done = new Pen(Color.FromArgb(255, 80, 200), 2.5f))
            {
                for (int i = 0; i < SimSegs.Count; i++)
                {
                    double a = SimCum[i], b = SimCum[i + 1];
                    if (SimPos >= b) DrawSeg(g, done, SimSegs[i]);
                    else
                    {
                        double t = b > a ? (SimPos - a) / (b - a) : 0;
                        if (t > 0) { Seg h, r; SimSegs[i].SplitAt(t, out h, out r); DrawSeg(g, done, h); }
                        V2 w = SimSegs[i].PointAt(Math.Max(0, t));
                        PointF p = ToScr(w);
                        using (Brush br = new SolidBrush(Color.FromArgb(120, 255, 80, 200))) g.FillEllipse(br, p.X - 9, p.Y - 9, 18, 18);
                        g.FillEllipse(Brushes.White, p.X - 3, p.Y - 3, 6, 6);
                        break;
                    }
                }
            }
        }

        void DrawUcs(Graphics g)
        {
            float x = RulersOn ? 52 : 18, y = Height - 18;
            using (Pen p = new Pen(Color.FromArgb(200, 220, 220, 220), 1.5f))
            using (Font f = new Font(MainForm.UiFont, 10f))
            {
                p.EndCap = LineCap.ArrowAnchor;
                p.Color = Theme.Orange; g.DrawLine(p, x, y, x + 40, y); p.Color = Theme.Green; g.DrawLine(p, x, y, x, y - 40);
                g.DrawString("X", f, Brushes.Gainsboro, x + 42, y - 7);
                g.DrawString("Y", f, Brushes.Gainsboro, x - 5, y - 54);
            }
        }

        void DrawCrosshair(Graphics g)
        {
            PointF c = ToScr(CurW);
            if (CurSnap == SnapKind.None || CurSnap == SnapKind.Grid) c = new PointF(MousePx.X, MousePx.Y);
            using (Pen p = new Pen(Color.FromArgb(220, 230, 230, 230)))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.DrawLine(p, 0, c.Y, Width, c.Y); g.DrawLine(p, c.X, 0, c.X, Height);
                if (CommandActive == null || !CommandActive()) g.DrawRectangle(p, c.X - 4, c.Y - 4, 8, 8);
            }
            if (CurSnap != SnapKind.None && CurSnap != SnapKind.Grid)
            {
                PointF s = ToScr(CurW);
                using (Pen p = new Pen(Color.Gold, 2f))
                {
                    switch (CurSnap)
                    {
                        case SnapKind.End: g.DrawRectangle(p, s.X - 6, s.Y - 6, 12, 12); break;
                        case SnapKind.Mid: g.DrawPolygon(p, new PointF[] { new PointF(s.X, s.Y - 7), new PointF(s.X + 7, s.Y + 6), new PointF(s.X - 7, s.Y + 6) }); break;
                        case SnapKind.Center: g.DrawEllipse(p, s.X - 6, s.Y - 6, 12, 12); break;
                        case SnapKind.Origin: g.DrawEllipse(p, s.X - 8, s.Y - 8, 16, 16); g.DrawLine(p, s.X - 11, s.Y, s.X + 11, s.Y); g.DrawLine(p, s.X, s.Y - 11, s.X, s.Y + 11); break;
                        case SnapKind.Quad: g.DrawPolygon(p, new PointF[] { new PointF(s.X, s.Y - 7), new PointF(s.X + 7, s.Y), new PointF(s.X, s.Y + 7), new PointF(s.X - 7, s.Y) }); break;
                    }
                }
            }
        }

        void DrawLegend(Graphics g)
        {
            if (Path == null) return;
            int x = Math.Max(4, Width - 154);
            int ly = RulersOn ? 30 : 12;
            g.TranslateTransform(0, ly - 12);
            using (Brush bg = new SolidBrush(Theme.Surface)) g.FillRectangle(bg, x, 12, 142, 148);
            using (Font f = new Font(MainForm.UiFont, 10f))
            using (Brush text = new SolidBrush(Theme.Muted))
                for (int i = 0; i < 6; i++)
                {
                    using (Pen pen = new Pen(i < 4 ? PassCol[i] : i == 4 ? Theme.Orange : Theme.Muted, 2))
                    { if (i >= 4) pen.DashStyle = i == 4 ? DashStyle.Dash : DashStyle.DashDot; g.DrawLine(pen, x+10, 26+i*23, x+32, 26+i*23); }
                    g.DrawString(i < 4 ? "Проход " + (i+1) : i == 4 ? "Врезание" : "Переход", f, text, x+40, 18+i*23);
                }
            g.ResetTransform();
        }
        void DrawCursorHint(Graphics g)
        {
            string snap = CurSnap == SnapKind.End ? "Конточка" : CurSnap == SnapKind.Mid ? "Середина" : CurSnap == SnapKind.Center ? "Центр" : CurSnap == SnapKind.Quad ? "Квадрант" : CurSnap == SnapKind.Origin ? "Начало координат X0 Y0" : "";
            string text = snap + (snap.Length > 0 && RubberHint.Length > 0 ? "\n" : "") + RubberHint;
            string hov = (Hover != null && !winStart.HasValue) ? DimDraw.Describe(Hover) : null;   // размеры элемента под курсором
            if (HoverDim != null && (CommandActive == null || !CommandActive())) hov = "Размер " + HoverDim.Text + (HoverDim.A != null && HoverDim.K != DimKind.Angle ? "\nЩёлкните, чтобы изменить" : "");
            if (hov != null) text = text + (text.Length > 0 ? "\n" : "") + hov;
            if (text.Length == 0) return;
            using (Font f = new Font(MainForm.UiFont, 11f))
            {
                SizeF size = g.MeasureString(text,f); float x = Math.Max(2,Math.Min(Width-size.Width-16,MousePx.X+20)); float y = Math.Max(2,Math.Min(Height-size.Height-12,MousePx.Y+24));
                using (Brush bg = new SolidBrush(Theme.Surface)) using (Brush fg = new SolidBrush(Theme.Text)) using (Pen border = new Pen(Theme.Border))
                { g.FillRectangle(bg,x,y,size.Width+12,size.Height+8); g.DrawRectangle(border,x,y,size.Width+12,size.Height+8); g.DrawString(text,f,fg,x+6,y+4); }
            }
        }

        // ---------- симуляция ----------
        public void SimPrepare()
        {
            SimSegs.Clear(); SimCum.Clear(); SimCum.Add(0); SimPos = 0;
            if (Path == null) return;
            double acc = 0;
            foreach (Move m in Path.Moves)
                if (m.S != null) { SimSegs.Add(m.S); acc += Math.Max(m.S.Length, 1e-6); SimCum.Add(acc); }
        }
        public double SimTotal { get { return SimCum.Count > 0 ? SimCum[SimCum.Count - 1] : 0; } }
    }
}
