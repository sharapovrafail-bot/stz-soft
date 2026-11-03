// STZ WireCut — инструменты черчения (как в AutoCAD): ПОДОБИЕ, ОБРЕЗАТЬ, УДЛИНИТЬ, СОПРЯЖЕНИЕ, ФАСКА, МАССИВ;
// проверка и починка чертежа DXF
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WireCut
{
    // ---------- ПОДОБИЕ (эквидистанта контура) ----------
    public class OffsetCmd : Cmd
    {
        public static double Dist = 1;
        int step; Contour picked; int pickedIdx;
        public override string Prompt
        {
            get { return step == 0 ? "Расстояние смещения <" + Geo.F(Dist) + ">:" : step == 1 ? "Выберите контур для смещения (Enter — выход):" : "Укажите сторону смещения:"; }
        }
        public override bool Text(string t)
        {
            double v;
            if (step == 0 && MainForm.Num(t, out v) && v > 0) { Dist = v; step = 1; return true; }
            return false;
        }
        public override void Enter() { if (step == 0) { step = 1; return; } F.EndCmd(); }
        public override void Point(V2 p)
        {
            if (step == 0) return;
            if (step == 1)
            {
                List<Contour> cs = Geo.Chain(Cv.Ents, 0.01);
                double bd = 10 / Cv.Scale; picked = null;
                foreach (Contour c in cs) { int i; double t; double d = c.DistTo(p, out i, out t); if (d < bd) { bd = d; picked = c; pickedIdx = i; } }
                if (picked == null) { F.Msg("  Объект не найден."); return; }
                step = 2; return;
            }
            // сторона
            double sign;
            if (picked.Closed)
            {
                bool inside = picked.Contains(p), ccw = picked.Area > 0;
                sign = inside == ccw ? 1 : -1;      // + = влево по ходу
            }
            else
            {
                int i; double t; picked.DistTo(p, out i, out t);
                Seg s = picked.Segs[i]; V2 q = s.PointAt(t);
                V2 tan = s.Kind == SegKind.Line ? s.TanStart : s.TanAtAngle(s.A0 + s.Sw * t);
                sign = V2.Cross(tan, p - q) >= 0 ? 1 : -1;
            }
            List<string> warn = new List<string>();
            Contour o = Geo.Offset(picked, sign * Dist, warn);
            foreach (string w in warn) F.Msg("  " + w);
            if (o != null) { F.PushUndo(); foreach (Seg s in o.Segs) Cv.Ents.Add(s); Cv.Invalidate(); }
            step = 1;
        }
    }

    // ---------- ОБРЕЗАТЬ ----------
    public class TrimCmd : Cmd
    {
        public override string Prompt { get { return "Выберите часть объекта для обрезки (режется по ближайшим пересечениям; Enter — выход):"; } }
        public override void Point(V2 p)
        {
            Seg s = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
            if (s == null) { F.Msg("  Объект не найден."); return; }
            double tc; V2 q; s.Project(p, out tc, out q);
            List<double> ts = new List<double>();
            foreach (Seg o in Cv.Ents)
            {
                if (o == s) continue;
                foreach (V2 x in Geo.IntersectSegs(s, o)) { double t; V2 qq; s.Project(x, out t, out qq); ts.Add(t); }
            }
            ts.Sort();
            List<double> u = new List<double>();
            foreach (double t in ts) if (u.Count == 0 || t - u[u.Count - 1] > 1e-9) u.Add(t);
            List<Seg> keep = new List<Seg>();
            if (s.IsCircle)
            {
                if (s.IsCircle && u.Count > 0 && u[u.Count - 1] > 1 - 1e-9) u.RemoveAt(u.Count - 1);
                if (u.Count < 2) { F.Msg("  Окружность режется только между двумя пересечениями."); return; }
                int k = -1;
                for (int i = 0; i + 1 < u.Count; i++) if (tc >= u[i] && tc <= u[i + 1]) k = i;
                if (k >= 0) keep.Add(Seg.Arc(s.C, s.R, s.A0 + s.Sw * u[k + 1], s.Sw * (u[k] + 1 - u[k + 1])));
                else keep.Add(Seg.Arc(s.C, s.R, s.A0 + s.Sw * u[0], s.Sw * (u[u.Count - 1] - u[0])));
            }
            else
            {
                double prev = -1, next = 2;
                foreach (double t in u) { if (t < tc - 1e-9 && t > 1e-9) prev = t; if (t > tc + 1e-9 && t < 1 - 1e-9 && next > 1.5) next = t; }
                if (prev > 0) keep.Add(Geo.Sub(s, 0, prev));
                if (next < 1.5) keep.Add(Geo.Sub(s, next, 1));
            }
            F.PushUndo();
            int idx = Cv.Ents.IndexOf(s); Cv.Ents.RemoveAt(idx);
            Cv.Ents.InsertRange(idx, keep);
            Cv.Invalidate();
        }
    }

    // ---------- УДЛИНИТЬ ----------
    public class ExtendCmd : Cmd
    {
        public override string Prompt { get { return "Выберите конец объекта для удлинения до ближайшей границы (Enter — выход):"; } }
        public override void Point(V2 p)
        {
            Seg s = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
            if (s == null || s.IsCircle) { F.Msg("  Выберите отрезок или дугу."); return; }
            double tc; V2 q; s.Project(p, out tc, out q);
            bool atEnd = tc >= 0.5;
            double best = double.MaxValue; V2 bestP = new V2(); bool found = false;
            foreach (Seg o in Cv.Ents)
            {
                if (o == s) continue;
                foreach (V2 x in Geo.Intersect(s, o))
                {
                    double t; V2 qq;
                    if (o.Project(x, out t, out qq) > 1e-6) continue;   // точка должна лежать на границе
                    double d;
                    if (s.Kind == SegKind.Line)
                    {
                        V2 dir = atEnd ? s.P1 - s.P0 : s.P0 - s.P1; V2 from = atEnd ? s.P1 : s.P0;
                        d = V2.Dot(x - from, dir.Norm());
                    }
                    else
                    {
                        double a = (x - s.C).Angle;
                        d = atEnd ? Geo.DirNorm(a - (s.A0 + s.Sw), s.Sw > 0) : Geo.DirNorm(a - s.A0, s.Sw < 0);
                        d = Math.Abs(d) * s.R;
                    }
                    if (d > 1e-6 && d < best) { best = d; bestP = x; found = true; }
                }
            }
            if (!found) { F.Msg("  Нет границы на продолжении объекта."); return; }
            F.PushUndo();
            if (s.Kind == SegKind.Line) { if (atEnd) s.P1 = bestP; else s.P0 = bestP; }
            else
            {
                double ext = best / s.R * Math.Sign(s.Sw);
                if (atEnd) s.Sw += ext; else { s.A0 -= ext; s.Sw += ext; }
            }
            Cv.Invalidate();
        }
    }

    // ---------- СОПРЯЖЕНИЕ и ФАСКА (два отрезка) ----------
    public class CornerCmd : Cmd
    {
        public static double FilletR = 1, Ch1 = 1, Ch2 = 1;
        bool chamfer; Seg a; V2 pa; int opt; // opt: 1 — ввод радиуса/первой длины, 2 — второй длины
        public CornerCmd(bool chamfer) { this.chamfer = chamfer; }
        public override string Prompt
        {
            get
            {
                if (opt == 1) return chamfer ? "Первая длина фаски <" + Geo.F(Ch1) + ">:" : "Радиус сопряжения <" + Geo.F(FilletR) + ">:";
                if (opt == 2) return "Вторая длина фаски <" + Geo.F(Ch2) + ">:";
                if (a == null) return chamfer ? "Первый отрезок или [Длины] (фаска " + Geo.F(Ch1) + "×" + Geo.F(Ch2) + "):" : "Первый отрезок или [Радиус] (R" + Geo.F(FilletR) + "):";
                return "Второй отрезок:";
            }
        }
        public override bool Text(string t)
        {
            double v;
            if (opt == 0 && a == null && Is(t, "Р", "R", "РАДИУС", "Д", "D", "ДЛИНЫ")) { opt = 1; return true; }
            if (opt == 1 && MainForm.Num(t, out v) && v >= 0) { if (chamfer) { Ch1 = v; Ch2 = v; opt = 2; } else { FilletR = v; opt = 0; } return true; }
            if (opt == 2 && MainForm.Num(t, out v) && v >= 0) { Ch2 = v; opt = 0; return true; }
            return false;
        }
        public override void Enter() { if (opt == 1) { opt = chamfer ? 2 : 0; return; } if (opt == 2) { opt = 0; return; } F.EndCmd(); }
        public override void Point(V2 p)
        {
            if (opt != 0) return;
            Seg s = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
            if (s == null || s.Kind != SegKind.Line) { F.Msg("  Нужен отрезок (дуги в сопряжении пока не поддерживаются)."); return; }
            if (a == null) { a = s; pa = p; s.Sel = true; Cv.Invalidate(); return; }
            if (s == a) return;
            a.Sel = false;
            Do(a, pa, s, p);
            F.EndCmd();
        }
        void Do(Seg l1, V2 p1, Seg l2, V2 p2)
        {
            List<V2> xs = Geo.Intersect(l1, l2);
            if (xs.Count == 0) { F.Msg("  Отрезки параллельны."); return; }
            V2 P = xs[0];
            V2 u1 = Side(l1, p1, P), u2 = Side(l2, p2, P);
            double cos = Math.Max(-1, Math.Min(1, V2.Dot(u1, u2))), th = Math.Acos(cos);
            if (th < 1e-6 || th > Math.PI - 1e-6) { F.Msg("  Отрезки на одной прямой."); return; }
            F.PushUndo();
            V2 T1, T2;
            if (chamfer) { T1 = P + u1 * Ch1; T2 = P + u2 * Ch2; }
            else { double t = FilletR / Math.Tan(th / 2); T1 = P + u1 * t; T2 = P + u2 * t; }
            Cut(l1, u1, P, T1); Cut(l2, u2, P, T2);
            if (chamfer) { if (Ch1 > 0 || Ch2 > 0) Cv.Ents.Add(Seg.Line(T1, T2)); }
            else if (FilletR > 0)
            {
                V2 c = P + (u1 + u2).Norm() * (FilletR / Math.Sin(th / 2));
                double a0 = (T1 - c).Angle, a1 = (T2 - c).Angle;
                double sw = Geo.DirNorm(a1 - a0, true);
                if (sw > Math.PI) sw -= 2 * Math.PI;
                Cv.Ents.Add(Seg.Arc(c, FilletR, a0, sw));
            }
            Cv.Invalidate();
        }
        // направление от угла P в сторону, по которой щёлкнули (оставляемая часть)
        static V2 Side(Seg l, V2 pick, V2 P)
        {
            V2 d = (l.P1 - l.P0).Norm();
            double t; V2 q; l.Project(pick, out t, out q);
            double s = V2.Dot(q - P, d);
            if (Math.Abs(s) < 1e-9) s = V2.Dot((V2.Dist(l.P0, P) > V2.Dist(l.P1, P) ? l.P0 : l.P1) - P, d);
            return s >= 0 ? d : -d;
        }
        static void Cut(Seg l, V2 u, V2 P, V2 T)
        {
            // дальний (оставляемый) конец сохраняем, ближний к углу — переносим в T
            if (V2.Dot(l.P0 - P, u) >= V2.Dot(l.P1 - P, u)) l.P1 = T; else l.P0 = T;
        }
    }

    // ---------- МАССИВ ----------
    public class ArrayCmd : SelCmd
    {
        int step; bool polar; int nx = 2, ny = 1; double dx = 10, dy = 10; V2 cen; int n = 6; double ang = 360;
        protected override string Prompt2
        {
            get
            {
                switch (step)
                {
                    case 0: return "Тип массива [Прямоугольный/Круговой] <П>:";
                    case 1: return polar ? "Центр массива:" : "Количество по X <" + nx + ">:";
                    case 2: return polar ? "Количество элементов <" + n + ">:" : "Шаг по X, мм <" + Geo.F(dx) + ">:";
                    case 3: return polar ? "Угол заполнения, ° <" + Geo.F(ang) + ">:" : "Количество по Y <" + ny + ">:";
                    default: return "Шаг по Y, мм <" + Geo.F(dy) + ">:";
                }
            }
        }
        public override bool Text(string t)
        {
            if (selecting) return false;
            double v; bool num = MainForm.Num(t, out v);
            switch (step)
            {
                case 0:
                    if (Is(t, "К", "P", "КРУГОВОЙ", "POLAR")) { polar = true; step = 1; return true; }
                    if (Is(t, "П", "R", "ПРЯМОУГОЛЬНЫЙ", "RECT")) { polar = false; step = 1; return true; }
                    return false;
                case 1: if (!polar && num && v >= 1) { nx = (int)v; step = 2; return true; } return false;
                case 2: if (num) { if (polar) { if (v >= 2) n = (int)v; else return false; } else dx = v; step = 3; return true; } return false;
                case 3: if (num) { if (polar) { ang = v; Make(); } else { if (v < 1) return false; ny = (int)v; step = 4; } return true; } return false;
                case 4: if (num) { dy = v; Make(); return true; } return false;
            }
            return false;
        }
        protected override void Pick(V2 p) { if (step == 1 && polar) { cen = p; Last = p; step = 2; } }
        protected override void Finish()
        {
            // Enter — принять значение по умолчанию
            if (step == 0) { step = 1; return; }
            if (step == 1 && !polar) { step = 2; return; }
            if (step == 2) { step = 3; return; }
            if (step == 3) { if (polar) Make(); else step = 4; return; }
            if (step == 4) { Make(); return; }
        }
        void Make()
        {
            F.PushUndo();
            int cnt = 0;
            if (polar)
            {
                double stepA = ang * Math.PI / 180 / (Math.Abs(Math.Abs(ang) - 360) < 1e-9 ? n : n - 1);
                for (int k = 1; k < n; k++)
                    foreach (Seg s in Sel) { Seg c = s.Clone(); c.Sel = false; c.Rotate(cen, stepA * k); Cv.Ents.Add(c); cnt++; }
            }
            else
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        if (i == 0 && j == 0) continue;
                        foreach (Seg s in Sel) { Seg c = s.Clone(); c.Sel = false; c.Move(new V2(dx * i, dy * j)); Cv.Ents.Add(c); cnt++; }
                    }
            F.Msg("  Создано объектов: " + cnt);
            Done();
        }
    }

    public class RepairCmd : Cmd
    {
        public static double Tol = 0.05;
        public override string Prompt { get { return "Допуск сшивки концов, мм <" + Geo.F(Tol) + ">:"; } }
        public override bool Text(string t) { double v; if (MainForm.Num(t, out v) && v > 0) { Tol = v; Go(); return true; } return false; }
        public override void Enter() { Go(); }
        void Go() { F.EndCmd(); F.RepairDrawing(Tol); }
    }

    // ---------- ПРОВЕРКА и ПОЧИНКА чертежа ----------
    public static class DrawingCheck
    {
        const double Tol = 1e-4;

        static bool Same(Seg a, Seg b)
        {
            if (a.Kind != b.Kind) return false;
            if (a.Kind == SegKind.Arc && (V2.Dist(a.C, b.C) > Tol || Math.Abs(a.R - b.R) > Tol)) return false;
            if (a.IsCircle || b.IsCircle) return a.IsCircle && b.IsCircle;
            return (V2.Dist(a.Start, b.Start) < Tol && V2.Dist(a.End, b.End) < Tol) || (V2.Dist(a.Start, b.End) < Tol && V2.Dist(a.End, b.Start) < Tol);
        }

        static bool Overlap(Seg a, Seg b)
        {
            if (a.Kind != SegKind.Line || b.Kind != SegKind.Line) return false;
            V2 d = (a.P1 - a.P0).Norm(), e = (b.P1 - b.P0).Norm();
            if (Math.Abs(V2.Cross(d, e)) > 1e-7) return false;
            if (Math.Abs(V2.Cross(d, b.P0 - a.P0)) > Tol) return false;
            double t0 = V2.Dot(b.P0 - a.P0, d), t1 = V2.Dot(b.P1 - a.P0, d);
            double lo = Math.Max(0, Math.Min(t0, t1)), hi = Math.Min(a.Length, Math.Max(t0, t1));
            return hi - lo > Tol;
        }

        /// <summary>Проверка: нулевые, дубли, наложения, разрывы и висячие концы. Возвращает точки проблем.</summary>
        public static List<V2> Check(List<Seg> ents, List<string> rep)
        {
            List<V2> marks = new List<V2>();
            int zero = 0, dup = 0, ovl = 0, gaps = 0, open = 0;
            for (int i = 0; i < ents.Count; i++)
            {
                if (ents[i].Length < Tol) { zero++; marks.Add(ents[i].Start); continue; }
                for (int j = i + 1; j < ents.Count; j++)
                {
                    if (Same(ents[i], ents[j])) { dup++; marks.Add(ents[i].PointAt(0.5)); }
                    else if (Overlap(ents[i], ents[j])) { ovl++; marks.Add(ents[j].PointAt(0.5)); }
                }
            }
            // концы
            List<V2> ends = new List<V2>();
            foreach (Seg s in ents) if (!s.IsCircle && s.Length >= Tol) { ends.Add(s.Start); ends.Add(s.End); }
            for (int i = 0; i < ends.Count; i++)
            {
                bool conn = false; double near = double.MaxValue;
                for (int j = 0; j < ends.Count; j++)
                {
                    if (j == i || j == (i ^ 1)) continue;
                    double d = V2.Dist(ends[i], ends[j]);
                    if (d <= 0.01) { conn = true; break; }
                    if (d < near) near = d;
                }
                if (conn) continue;
                if (near <= 1.0) { gaps++; rep.Add("Разрыв " + Geo.F(Math.Round(near, 4)) + " мм у точки " + ends[i]); }
                else { open++; rep.Add("Висячий конец у точки " + ends[i]); }
                marks.Add(ends[i]);
            }
            rep.Insert(0, "Проверка чертежа: объектов " + ents.Count + "; нулевой длины " + zero + ", дублей " + dup + ", наложений " + ovl + ", разрывов " + gaps / 2 + ", висячих концов " + open);
            return marks;
        }

        /// <summary>Починка: убрать нулевые и дубли, слить наложения, сшить концы в пределах допуска.</summary>
        public static List<Seg> Repair(List<Seg> src, double tol, List<string> rep)
        {
            List<Seg> ents = new List<Seg>();
            int zero = 0, dup = 0, ovl = 0, snapped = 0;
            foreach (Seg s in src) { if (s.Length < Tol) zero++; else ents.Add(s.Clone()); }
            for (int i = 0; i < ents.Count; i++)
                for (int j = ents.Count - 1; j > i; j--)
                {
                    if (Same(ents[i], ents[j])) { ents.RemoveAt(j); dup++; continue; }
                    if (Overlap(ents[i], ents[j]))
                    {
                        // слить в один отрезок по крайним точкам
                        Seg a = ents[i], b = ents[j]; V2 d = (a.P1 - a.P0).Norm();
                        V2[] pts = { a.P0, a.P1, b.P0, b.P1 };
                        V2 lo = pts[0], hi = pts[0];
                        foreach (V2 q in pts) { if (V2.Dot(q - a.P0, d) < V2.Dot(lo - a.P0, d)) lo = q; if (V2.Dot(q - a.P0, d) > V2.Dot(hi - a.P0, d)) hi = q; }
                        a.P0 = lo; a.P1 = hi; ents.RemoveAt(j); ovl++;
                    }
                }
            // сшивка концов: точку дуги не двигаем, к ней подтягиваем отрезки
            for (int i = 0; i < ents.Count; i++)
                for (int e1 = 0; e1 < 2; e1++)
                {
                    Seg a = ents[i]; if (a.IsCircle) continue;
                    V2 pa = e1 == 0 ? a.Start : a.End;
                    for (int j = 0; j < ents.Count; j++)
                    {
                        if (j == i) continue;
                        Seg b = ents[j]; if (b.IsCircle) continue;
                        for (int e2 = 0; e2 < 2; e2++)
                        {
                            V2 pb = e2 == 0 ? b.Start : b.End;
                            double d = V2.Dist(pa, pb);
                            if (d < 1e-9 || d > tol) continue;
                            if (b.Kind == SegKind.Line) { if (e2 == 0) b.P0 = pa; else b.P1 = pa; snapped++; }
                            else if (a.Kind == SegKind.Line) { if (e1 == 0) a.P0 = pb; else a.P1 = pb; pa = pb; snapped++; }
                            else { ents.Add(Seg.Line(pa, pb)); snapped++; } // две дуги — перемычка
                        }
                    }
                }
            rep.Add("Починка (допуск " + Geo.F(tol) + " мм): удалено нулевых " + zero + ", дублей " + dup + ", слито наложений " + ovl + ", сшито концов " + snapped);
            return ents;
        }
    }
}
