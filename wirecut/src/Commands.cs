// STZ WireCut — команды черчения и редактирования (по образцу AutoCAD)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WireCut
{
    public abstract class Cmd
    {
        public MainForm F;
        public V2? Last;                       // база для @-ввода и орто
        public abstract string Prompt { get; }
        public virtual void Begin() { }
        public virtual void Point(V2 p) { }
        public virtual bool Text(string t) { return false; }
        public virtual void Enter() { F.EndCmd(); }
        public virtual void Rubber(Graphics g, V2 c) { }
        public virtual bool ShowsDims { get { return false; } }   // команда сама рисует размеры (без подсказки длины у курсора)
        public virtual bool Tab() { return false; }                // Tab — следующее поле размера

        protected CadCanvas Cv { get { return F.Cv; } }
        protected static Pen RubPen() { return new Pen(Color.FromArgb(255, 200, 80), 1f) { DashStyle = DashStyle.Dash }; }
        protected void Line(Graphics g, V2 a, V2 b) { using (Pen p = RubPen()) g.DrawLine(p, Cv.ToScr(a), Cv.ToScr(b)); }
        protected void Draw(Graphics g, Seg s) { using (Pen p = RubPen()) Cv.DrawSeg(g, p, s); }
        protected void Add(Seg s) { Cv.Ents.Add(s); Cv.Invalidate(); }
        protected static bool Is(string t, params string[] v) { foreach (string x in v) if (string.Equals(t, x, StringComparison.OrdinalIgnoreCase)) return true; return false; }
    }

    // ОТРЕЗОК, КРУГ, ДУГА, ПРЯМОУГ — в Sketch.cs (размеры как во Fusion 360)

    // ---------- базовая команда с выбором объектов ----------
    public abstract class SelCmd : Cmd
    {
        protected bool selecting = true;
        protected List<Seg> Sel = new List<Seg>();
        public override void Begin()
        {
            foreach (Seg s in Cv.Ents) if (s.Sel) Sel.Add(s);
            if (Sel.Count > 0) { selecting = false; Selected(); }
        }
        protected virtual void Selected() { }
        protected virtual string Prompt2 { get { return ""; } }
        public override string Prompt { get { return selecting ? "Выберите объекты (Enter — закончить):" : Prompt2; } }
        public override void Point(V2 p)
        {
            if (selecting)
            {
                Seg h = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
                if (h != null && !h.Sel) { h.Sel = true; Sel.Add(h); Cv.Invalidate(); }
                return;
            }
            Pick(p);
        }
        protected virtual void Pick(V2 p) { }
        public override void Enter()
        {
            if (selecting)
            {
                if (Sel.Count == 0) { F.EndCmd(); return; }
                selecting = false; Selected(); return;
            }
            Finish();
        }
        protected virtual void Finish() { F.EndCmd(); }
        protected void Done() { foreach (Seg s in Cv.Ents) s.Sel = false; Cv.Invalidate(); F.EndCmd(); }
        protected void Ghost(Graphics g, Fn1<Seg, Seg> tr) { foreach (Seg s in Sel) Draw(g, tr(s.Clone())); }
    }

    public class EraseCmd : SelCmd
    {
        protected override void Selected()
        {
            F.PushUndo();
            F.DropMarksOf(Sel);
            foreach (Seg s in Sel) Cv.Ents.Remove(s);
            F.Msg("  Удалено объектов: " + Sel.Count);
            Done();
        }
    }

    public class MoveCmd : SelCmd
    {
        bool copy; V2? b;
        public MoveCmd(bool copy) { this.copy = copy; }
        protected override string Prompt2 { get { return !b.HasValue ? "Базовая точка:" : "Вторая точка" + (copy ? " (Enter — закончить):" : ":"); } }
        protected override void Pick(V2 p)
        {
            if (!b.HasValue) { b = p; Last = p; return; }
            V2 d = p - b.Value;
            F.PushUndo();
            if (copy) { foreach (Seg s in Sel) { Seg c = s.Clone(); c.Sel = false; c.Move(d); Cv.Ents.Add(c); } Cv.Invalidate(); return; }
            foreach (Seg s in Sel) s.Move(d);
            Done();
        }
        protected override void Finish() { Done(); }
        public override void Rubber(Graphics g, V2 c)
        {
            if (selecting || !b.HasValue) return;
            V2 d = c - b.Value; Line(g, b.Value, c);
            Ghost(g, delegate (Seg s) { s.Move(d); return s; });
        }
    }

    public class RotateCmd : SelCmd
    {
        V2? b;
        protected override string Prompt2 { get { return !b.HasValue ? "Базовая точка:" : "Угол поворота (градусы) или точка:"; } }
        protected override void Pick(V2 p)
        {
            if (!b.HasValue) { b = p; Last = p; return; }
            Rot((p - b.Value).Angle);
        }
        void Rot(double a)
        {
            F.PushUndo(); foreach (Seg s in Sel) s.Rotate(b.Value, a); Done();
        }
        public override bool Text(string t)
        {
            double v;
            if (b.HasValue && t.IndexOf(',') < 0 && MainForm.Num(t, out v)) { Rot(v * Math.PI / 180); return true; }
            return false;
        }
        public override void Rubber(Graphics g, V2 c)
        {
            if (selecting || !b.HasValue) return;
            double a = (c - b.Value).Angle; Line(g, b.Value, c);
            Ghost(g, delegate (Seg s) { s.Rotate(b.Value, a); return s; });
        }
    }

    public class ScaleCmd : SelCmd
    {
        V2? b;
        protected override string Prompt2 { get { return !b.HasValue ? "Базовая точка:" : "Масштабный коэффициент:"; } }
        protected override void Pick(V2 p) { if (!b.HasValue) { b = p; Last = p; } }
        public override bool Text(string t)
        {
            double v;
            if (b.HasValue && t.IndexOf(',') < 0 && MainForm.Num(t, out v) && v > 0)
            { F.PushUndo(); foreach (Seg s in Sel) s.Scale(b.Value, v); Done(); return true; }
            return false;
        }
    }

    public class MirrorCmd : SelCmd
    {
        V2? a, b;
        protected override string Prompt2 { get { return !a.HasValue ? "Первая точка оси отражения:" : !b.HasValue ? "Вторая точка оси:" : "Удалить исходные объекты? [Да/Нет] <Нет>:"; } }
        protected override void Pick(V2 p)
        {
            if (!a.HasValue) { a = p; Last = p; return; }
            if (!b.HasValue && V2.Dist(a.Value, p) > 1e-9) { b = p; }
        }
        public override bool Text(string t)
        {
            if (!b.HasValue) return false;
            if (Is(t, "Д", "Y", "ДА", "YES")) { Do(true); return true; }
            if (Is(t, "Н", "N", "НЕТ", "NO")) { Do(false); return true; }
            return false;
        }
        protected override void Finish() { if (b.HasValue) Do(false); else Done(); }
        void Do(bool del)
        {
            F.PushUndo();
            foreach (Seg s in Sel)
            {
                Seg c = s.Clone(); c.Sel = false; c.Mirror(a.Value, b.Value); Cv.Ents.Add(c);
                if (del) Cv.Ents.Remove(s);
            }
            Done();
        }
        public override void Rubber(Graphics g, V2 c)
        {
            if (selecting || !a.HasValue) return;
            V2 bb = b.HasValue ? b.Value : c;
            if (V2.Dist(a.Value, bb) < 1e-9) return;
            Line(g, a.Value, bb);
            Ghost(g, delegate (Seg s) { s.Mirror(a.Value, bb); return s; });
        }
    }

    // ---------- измерения ----------
    public class DistCmd : Cmd
    {
        public override string Prompt { get { return !Last.HasValue ? "Первая точка:" : "Вторая точка:"; } }
        public override void Point(V2 p)
        {
            if (!Last.HasValue) { Last = p; return; }
            V2 d = p - Last.Value;
            F.Msg("  Расстояние = " + Geo.F(d.Len) + ",  угол = " + Geo.F(d.Angle * 180 / Math.PI) + "°,  dX = " + Geo.F(d.X) + ",  dY = " + Geo.F(d.Y));
            F.EndCmd();
        }
        public override void Rubber(Graphics g, V2 c) { if (Last.HasValue) Line(g, Last.Value, c); }
    }

    public class IdCmd : Cmd
    {
        public override string Prompt { get { return "Укажите точку:"; } }
        public override void Point(V2 p) { F.Msg("  X = " + Geo.F(p.X) + "   Y = " + Geo.F(p.Y)); F.EndCmd(); }
    }

    public class SideMark { public V2 P; public bool Outside; }

    // ---------- сторона захода для контура ----------
    public class SideCmd : Cmd
    {
        bool outside = true;
        public override string Prompt { get { return "Щёлкните у контура — заход " + (outside ? "СНАРУЖИ" : "ИЗНУТРИ") + "  [Снаружи/Изнутри/Авто — сбросить все] (Enter — готово):"; } }
        public override bool Text(string t)
        {
            if (Is(t, "С", "S", "СНАРУЖИ", "OUT", "Н")) { outside = true; return true; }
            if (Is(t, "И", "В", "I", "ИЗНУТРИ", "IN")) { outside = false; return true; }
            if (Is(t, "А", "A", "АВТО", "AUTO")) { Cv.SideMarks.Clear(); F.Msg("  Сторона захода — автоматически (по типу вырезки)."); F.GeometryChanged(); return true; }
            return false;
        }
        public override void Point(V2 p)
        {
            for (int i = Cv.SideMarks.Count - 1; i >= 0; i--) if (V2.Dist(Cv.SideMarks[i].P, p) < 15 / Cv.Scale) Cv.SideMarks.RemoveAt(i);
            Cv.SideMarks.Add(new SideMark { P = p, Outside = outside });
            F.Msg("  Заход " + (outside ? "снаружи" : "изнутри") + " для контура у точки " + p);
            F.GeometryChanged();
        }
    }

    // ---------- точка заправки ----------
    public class StartCmd : Cmd
    {
        public override string Prompt { get { return "Точка заправки проволоки (у контура) или [Удалить все] (Enter — готово):"; } }
        public override void Point(V2 p)
        {
            // заменить точку, если рядом уже есть
            for (int i = Cv.ManualStarts.Count - 1; i >= 0; i--)
                if (V2.Dist(Cv.ManualStarts[i], p) < 8 / Cv.Scale) Cv.ManualStarts.RemoveAt(i);
            Cv.ManualStarts.Add(p);
            F.Msg("  Точка заправки: " + p);
            if (Cv.Path != null) F.BuildPath(false);
            Cv.Invalidate();
        }
        public override bool Text(string t)
        {
            if (Is(t, "У", "D", "УДАЛИТЬ", "DELETE")) { Cv.ManualStarts.Clear(); if (Cv.Path != null) F.BuildPath(false); Cv.Invalidate(); F.Msg("  Точки заправки удалены — будут выбраны автоматически."); F.EndCmd(); return true; }
            return false;
        }
    }
}
