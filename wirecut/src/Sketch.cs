// STZ WireCut — черчение с размерами «как во Fusion 360»:
// размеры видны сразу при построении, число вводится в подсвеченное поле, Tab — следующее поле, Enter — создать;
// после создания размеры остаются на объекте и следуют за ним.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WireCut
{
    public abstract class SketchCmd : Cmd
    {
        protected string[] Fields = new string[0];
        protected double?[] Lock = new double?[0];
        protected int Act;
        protected V2 Cur;                       // последнее положение курсора
        public override bool ShowsDims { get { return true; } }

        protected void InitFields(params string[] names) { Fields = names; Lock = new double?[names.Length]; Act = 0; }
        protected void ResetFields() { for (int i = 0; i < Lock.Length; i++) Lock[i] = null; Act = 0; }
        protected bool AnyLock { get { foreach (double? v in Lock) if (v.HasValue) return true; return false; } }
        protected abstract bool Ready { get; }  // базовая точка задана — поля активны
        protected abstract void Commit();       // создать объект по полям (+ курсор для незаданных)

        public override bool Text(string t)
        {
            double v;
            if (Ready && Fields.Length > 0 && t.IndexOf(',') < 0 && t.IndexOf('<') < 0 && !t.StartsWith("@") && MainForm.Num(t, out v))
            {
                Lock[Act] = v;
                Act++;
                if (Act >= Fields.Length) { Commit(); }
                return true;
            }
            return false;
        }

        public override bool Tab()
        {
            if (!Ready || Fields.Length == 0) return false;
            double v;
            string typed = F.InputText.Trim();
            if (typed.Length > 0 && MainForm.Num(typed, out v)) { Lock[Act] = v; F.InputText = ""; }
            Act = (Act + 1) % Fields.Length;
            Cv.Invalidate();
            return true;
        }

        /// <summary>Поле размера на чертеже: активное — с набираемым текстом, заданное — с замком.</summary>
        protected void FieldBox(Graphics g, V2 at, int idx, double live, string prefix, string suffix)
        {
            string txt; int state;
            if (idx == Act && F.InputText.Trim().Length > 0) { txt = F.InputText.Trim() + "▌"; state = 1; }
            else if (Lock[idx].HasValue) { txt = Fmt(Lock[idx].Value); state = 2; }
            else { txt = Fmt(live); state = idx == Act ? 1 : 0; }
            DimDraw.Field(g, Cv, at, prefix + txt + suffix, state);
        }
        protected static string Fmt(double v) { return Math.Round(v, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
        protected double L(int i, double live) { return Lock[i].HasValue ? Lock[i].Value : live; }

        protected double Px(double px) { return px / Cv.Scale; }
        protected void AutoDim(Dim d) { if (Cv.AutoDims) Cv.Dims.Add(d); }
    }

    // ---------- ОТРЕЗОК: поля Длина, Угол ----------
    public class LineCmd : SketchCmd
    {
        V2? first; int count;
        public LineCmd() { InitFields("Длина", "Угол"); }
        protected override bool Ready { get { return Last.HasValue; } }
        public override string Prompt
        {
            get
            {
                if (!Last.HasValue) return "Первая точка:";
                return "Следующая точка — или введите " + (Act == 0 ? "ДЛИНУ" : "УГОЛ") + " (Tab — другое поле)" + (count >= 2 ? " [Замкнуть/Отменить]:" : " [Отменить]:");
            }
        }
        public override void Point(V2 p)
        {
            if (!Last.HasValue) { F.PushUndo(); first = p; Last = p; ResetFields(); return; }
            // заданные поля имеют приоритет над щелчком
            if (AnyLock) { Cur = p; Commit(); return; }
            AddTo(p);
        }
        void AddTo(V2 p)
        {
            if (V2.Dist(Last.Value, p) < 1e-9) return;
            Seg s = Seg.Line(Last.Value, p); Add(s);
            AutoDim(new Dim { K = DimKind.Aligned, A = s, Off = 22 });
            Last = p; count++; ResetFields();
        }
        protected override void Commit()
        {
            V2 d = Cur - Last.Value;
            double len = L(0, d.Len), ang = L(1, Math.Atan2(d.Y, d.X) * 180 / Math.PI);
            if (len <= 0) { ResetFields(); return; }
            AddTo(Last.Value + V2.Polar(ang * Math.PI / 180, len));
        }
        public override void Enter() { if (Last.HasValue && AnyLock) { Commit(); return; } F.EndCmd(); }
        public override bool Text(string t)
        {
            if (Is(t, "З", "C", "ЗАМКНУТЬ", "CLOSE") && count >= 2) { AddTo(first.Value); F.EndCmd(); return true; }
            if (Is(t, "О", "U", "ОТМЕНИТЬ", "UNDO") && count > 0)
            {
                Seg s = Cv.Ents[Cv.Ents.Count - 1]; Cv.Ents.RemoveAt(Cv.Ents.Count - 1); Last = s.P0; count--; Cv.Invalidate(); return true;
            }
            return base.Text(t);
        }
        public override void Rubber(Graphics g, V2 c)
        {
            Cur = c;
            if (!Last.HasValue) return;
            V2 a = Last.Value, d = c - a;
            double len = L(0, d.Len), ang = L(1, Math.Atan2(d.Y, d.X) * 180 / Math.PI);
            V2 b = a + V2.Polar(ang * Math.PI / 180, len);
            Line(g, a, b);
            if (len < 1e-9) return;
            V2 u = (b - a).Norm(), n = u.Perp();
            // размер длины параллельно отрезку
            DimDraw.Draw(g, Cv, new Dim { K = DimKind.Aligned, P1 = a, P2 = b, Pos = (a + b) * 0.5 + n * Px(26) }, true, false);
            FieldBox(g, (a + b) * 0.5 + n * Px(26), 0, d.Len, "", "");
            // угол от горизонтали
            double r = Px(46);
            using (Pen p = new Pen(Color.FromArgb(140, DimDraw.Col)))
            {
                g.DrawLine(p, Cv.ToScr(a), Cv.ToScr(a + new V2(r * 1.3, 0)));
                double a1 = Math.Atan2(u.Y, u.X);
                Cv.DrawSeg(g, p, Seg.Arc(a, r, 0, a1));
            }
            FieldBox(g, a + V2.Polar(Math.Atan2(u.Y, u.X) / 2, r + Px(26)), 1, Math.Atan2(d.Y, d.X) * 180 / Math.PI, "", "°");
        }
    }

    // ---------- ПРЯМОУГОЛЬНИК: поля Ширина, Высота ----------
    public class RectCmd : SketchCmd
    {
        public RectCmd() { InitFields("Ширина", "Высота"); }
        protected override bool Ready { get { return Last.HasValue; } }
        public override string Prompt { get { return !Last.HasValue ? "Первый угол:" : "Второй угол — или введите " + (Act == 0 ? "ШИРИНУ" : "ВЫСОТУ") + " (Tab — другое поле, Enter — создать):"; } }
        public override void Point(V2 p)
        {
            if (!Last.HasValue) { Last = p; ResetFields(); return; }
            Cur = p; Commit();
        }
        V2 Corner(V2 c)
        {
            V2 a = Last.Value;
            double sx = c.X >= a.X ? 1 : -1, sy = c.Y >= a.Y ? 1 : -1;
            return new V2(a.X + sx * L(0, Math.Abs(c.X - a.X)), a.Y + sy * L(1, Math.Abs(c.Y - a.Y)));
        }
        protected override void Commit()
        {
            V2 a = Last.Value, b = Corner(Cur);
            if (Math.Abs(a.X - b.X) < 1e-9 || Math.Abs(a.Y - b.Y) < 1e-9) { ResetFields(); return; }
            F.PushUndo();
            Seg[] r = Rect(a, b);
            foreach (Seg s in r) Cv.Ents.Add(s);
            // размеры: ширина — по нижней стороне, высота — по правой
            Seg bottom = r[0].P0.Y <= r[2].P0.Y ? r[0] : r[2];
            Seg right = r[1].P0.X >= r[3].P0.X ? r[1] : r[3];
            AutoDim(new Dim { K = DimKind.Horizontal, A = bottom, Off = -30 });
            AutoDim(new Dim { K = DimKind.Vertical, A = right, Off = -30 });
            Cv.Invalidate(); F.EndCmd();
        }
        public override void Enter() { if (Last.HasValue) Commit(); else F.EndCmd(); }
        static Seg[] Rect(V2 a, V2 b)
        {
            V2 c = new V2(b.X, a.Y), d = new V2(a.X, b.Y);
            return new Seg[] { Seg.Line(a, c), Seg.Line(c, b), Seg.Line(b, d), Seg.Line(d, a) };
        }
        public override void Rubber(Graphics g, V2 c)
        {
            Cur = c;
            if (!Last.HasValue) return;
            V2 a = Last.Value, b = Corner(c);
            foreach (Seg s in Rect(a, b)) Draw(g, s);
            double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            DimDraw.Draw(g, Cv, new Dim { K = DimKind.Horizontal, P1 = new V2(x0, y0), P2 = new V2(x1, y0), Pos = new V2(0, y0 - Px(30)) }, true, false);
            DimDraw.Draw(g, Cv, new Dim { K = DimKind.Vertical, P1 = new V2(x1, y0), P2 = new V2(x1, y1), Pos = new V2(x1 + Px(30), 0) }, true, false);
            FieldBox(g, new V2((x0 + x1) / 2, y0 - Px(30)), 0, Math.Abs(c.X - a.X), "", "");
            FieldBox(g, new V2(x1 + Px(30), (y0 + y1) / 2), 1, Math.Abs(c.Y - a.Y), "", "");
        }
    }

    // ---------- КРУГ: поле Диаметр (Р — ввод радиуса) ----------
    public class CircleCmd : SketchCmd
    {
        bool radius;
        public CircleCmd() { InitFields("Диаметр"); }
        protected override bool Ready { get { return Last.HasValue; } }
        public override string Prompt { get { return !Last.HasValue ? "Центр круга:" : (radius ? "Радиус (число или точка) [Диаметр]:" : "ДИАМЕТР (число или точка) [Радиус]:"); } }
        public override void Point(V2 p)
        {
            if (!Last.HasValue) { Last = p; ResetFields(); return; }
            Cur = p; Commit();
        }
        public override bool Text(string t)
        {
            if (Last.HasValue && Is(t, "Р", "R", "РАДИУС")) { radius = true; return true; }
            if (Last.HasValue && Is(t, "Д", "D", "ДИАМЕТР")) { radius = false; return true; }
            double v;
            if (Last.HasValue && t.IndexOf(',') < 0 && MainForm.Num(t, out v)) { Lock[0] = radius ? v * 2 : v; Commit(); return true; }
            return false;
        }
        protected override void Commit()
        {
            double d = L(0, 2 * V2.Dist(Last.Value, Cur));
            if (d < 1e-6) return;
            F.PushUndo();
            Seg s = Seg.Circle(Last.Value, d / 2); Add(s);
            AutoDim(new Dim { K = DimKind.Diameter, A = s, Rel = V2.Polar(Math.PI / 4, d / 2 + Px(34)) });
            F.EndCmd();
        }
        public override void Rubber(Graphics g, V2 c)
        {
            Cur = c;
            if (!Last.HasValue) return;
            double d = L(0, 2 * V2.Dist(Last.Value, c));
            if (d < 1e-9) return;
            Draw(g, Seg.Circle(Last.Value, d / 2));
            V2 pos = Last.Value + V2.Polar(Math.PI / 4, d / 2 + Px(34));
            DimDraw.Draw(g, Cv, new Dim { K = DimKind.Diameter, C = Last.Value, R = d / 2, Pos = pos }, true, false);
            FieldBox(g, pos, 0, 2 * V2.Dist(Last.Value, c), "Ø", "");
        }
    }

    // ---------- ДУГА по 3 точкам: живой радиус ----------
    public class Arc3Cmd : SketchCmd
    {
        List<V2> pts = new List<V2>();
        protected override bool Ready { get { return false; } }
        protected override void Commit() { }
        public override string Prompt { get { return pts.Count == 0 ? "Начальная точка дуги:" : pts.Count == 1 ? "Вторая точка дуги:" : "Конечная точка дуги:"; } }
        public override void Point(V2 p)
        {
            pts.Add(p); Last = p;
            if (pts.Count == 3)
            {
                Seg a = Seg.Arc3(pts[0], pts[1], pts[2]);
                if (a == null) { F.Msg("  Точки на одной прямой."); pts.RemoveAt(2); return; }
                F.PushUndo(); Add(a);
                AutoDim(new Dim { K = DimKind.Radius, A = a, Rel = V2.Polar(a.A0 + a.Sw / 2, a.R * 0.55) });
                F.EndCmd();
            }
        }
        public override void Rubber(Graphics g, V2 c)
        {
            if (pts.Count == 1)
            {
                Line(g, pts[0], c);
                if (V2.Dist(pts[0], c) > 1e-9) DimDraw.Draw(g, Cv, new Dim { K = DimKind.Aligned, P1 = pts[0], P2 = c, Pos = (pts[0] + c) * 0.5 + (c - pts[0]).Norm().Perp() * Px(24) }, true, true);
            }
            if (pts.Count == 2)
            {
                Seg a = Seg.Arc3(pts[0], pts[1], c);
                if (a == null) return;
                Draw(g, a);
                DimDraw.Draw(g, Cv, new Dim { K = DimKind.Radius, C = a.C, R = a.R, Pos = a.C + V2.Polar(a.A0 + a.Sw / 2, a.R * 0.55) }, true, true);
            }
        }
    }
}
