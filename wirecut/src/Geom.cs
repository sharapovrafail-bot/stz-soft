// STZ WireCut — геометрия: точки, отрезки, дуги, контуры, эквидистанта
using System;
using System.Collections.Generic;

namespace WireCut
{
    public struct V2
    {
        public double X, Y;
        public V2(double x, double y) { X = x; Y = y; }
        public static V2 operator +(V2 a, V2 b) { return new V2(a.X + b.X, a.Y + b.Y); }
        public static V2 operator -(V2 a, V2 b) { return new V2(a.X - b.X, a.Y - b.Y); }
        public static V2 operator -(V2 a) { return new V2(-a.X, -a.Y); }
        public static V2 operator *(V2 a, double k) { return new V2(a.X * k, a.Y * k); }
        public static V2 operator *(double k, V2 a) { return new V2(a.X * k, a.Y * k); }
        public double Len { get { return Math.Sqrt(X * X + Y * Y); } }
        public V2 Norm() { double l = Len; return l < 1e-15 ? new V2(0, 0) : new V2(X / l, Y / l); }
        public V2 Perp() { return new V2(-Y, X); } // поворот на +90° (влево)
        public static double Dot(V2 a, V2 b) { return a.X * b.X + a.Y * b.Y; }
        public static double Cross(V2 a, V2 b) { return a.X * b.Y - a.Y * b.X; }
        public static double Dist(V2 a, V2 b) { return (a - b).Len; }
        public double Angle { get { return Math.Atan2(Y, X); } }
        public static V2 Polar(double a, double r) { return new V2(r * Math.Cos(a), r * Math.Sin(a)); }
        public override string ToString() { return Geo.F(X) + ", " + Geo.F(Y); }
    }

    public enum SegKind { Line, Arc }

    /// <summary>Элемент контура: отрезок или дуга (A0 — нач. угол, Sw — угол раствора со знаком, + = против часовой).</summary>
    public class Seg
    {
        public SegKind Kind;
        public V2 P0, P1;          // отрезок
        public V2 C; public double R, A0, Sw; // дуга
        public bool Sel;           // выделен в редакторе

        public static Seg Line(V2 a, V2 b) { return new Seg { Kind = SegKind.Line, P0 = a, P1 = b }; }
        public static Seg Arc(V2 c, double r, double a0, double sw) { return new Seg { Kind = SegKind.Arc, C = c, R = r, A0 = a0, Sw = sw }; }
        public static Seg Circle(V2 c, double r) { return Arc(c, r, 0, 2 * Math.PI); }

        /// <summary>Дуга по двум точкам и bulge (как в полилинии DXF).</summary>
        public static Seg FromBulge(V2 p0, V2 p1, double bulge)
        {
            if (Math.Abs(bulge) < 1e-12) return Line(p0, p1);
            double sw = 4 * Math.Atan(bulge);
            double chord = V2.Dist(p0, p1);
            double r = chord / (2 * Math.Sin(Math.Abs(sw) / 2));
            V2 mid = (p0 + p1) * 0.5;
            V2 dir = (p1 - p0).Norm();
            double h = r * Math.Cos(Math.Abs(sw) / 2); // расстояние от хорды до центра (<0 для дуги >180°)
            // для bulge>0 (против часовой) центр слева от хорды
            V2 c = mid + dir.Perp() * (h * Math.Sign(bulge));
            double a0 = (p0 - c).Angle;
            return Arc(c, r, a0, sw);
        }

        /// <summary>Дуга через три точки.</summary>
        public static Seg Arc3(V2 a, V2 b, V2 c)
        {
            double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (Math.Abs(d) < 1e-12) return null;
            double ux = ((a.X * a.X + a.Y * a.Y) * (b.Y - c.Y) + (b.X * b.X + b.Y * b.Y) * (c.Y - a.Y) + (c.X * c.X + c.Y * c.Y) * (a.Y - b.Y)) / d;
            double uy = ((a.X * a.X + a.Y * a.Y) * (c.X - b.X) + (b.X * b.X + b.Y * b.Y) * (a.X - c.X) + (c.X * c.X + c.Y * c.Y) * (b.X - a.X)) / d;
            V2 cen = new V2(ux, uy);
            double r = V2.Dist(cen, a);
            bool ccw = V2.Cross(b - a, c - b) > 0;
            double a0 = (a - cen).Angle, a2 = (c - cen).Angle;
            return Arc(cen, r, a0, Geo.DirNorm(a2 - a0, ccw));
        }

        public bool IsArc { get { return Kind == SegKind.Arc; } }
        public bool IsCircle { get { return Kind == SegKind.Arc && Math.Abs(Sw) >= 2 * Math.PI - 1e-9; } }
        public V2 Start { get { return Kind == SegKind.Line ? P0 : C + V2.Polar(A0, R); } }
        public V2 End { get { return Kind == SegKind.Line ? P1 : C + V2.Polar(A0 + Sw, R); } }
        public double Length { get { return Kind == SegKind.Line ? V2.Dist(P0, P1) : R * Math.Abs(Sw); } }

        public V2 TanAtAngle(double a) { return Sw > 0 ? new V2(-Math.Sin(a), Math.Cos(a)) : new V2(Math.Sin(a), -Math.Cos(a)); }
        public V2 TanStart { get { return Kind == SegKind.Line ? (P1 - P0).Norm() : TanAtAngle(A0); } }
        public V2 TanAt(double t) { return Kind == SegKind.Line ? (P1 - P0).Norm() : TanAtAngle(A0 + Sw * t); }
        public V2 TanEnd { get { return Kind == SegKind.Line ? (P1 - P0).Norm() : TanAtAngle(A0 + Sw); } }

        public V2 PointAt(double t)
        {
            if (Kind == SegKind.Line) return P0 + (P1 - P0) * t;
            return C + V2.Polar(A0 + Sw * t, R);
        }

        public Seg Clone() { return (Seg)MemberwiseClone(); }

        public Seg Reversed()
        {
            if (Kind == SegKind.Line) return Line(P1, P0);
            return Arc(C, R, A0 + Sw, -Sw);
        }

        public void SplitAt(double t, out Seg a, out Seg b)
        {
            if (Kind == SegKind.Line) { V2 m = PointAt(t); a = Line(P0, m); b = Line(m, P1); }
            else { a = Arc(C, R, A0, Sw * t); b = Arc(C, R, A0 + Sw * t, Sw * (1 - t)); }
        }

        /// <summary>Ближайшая точка элемента: возвращает параметр t и расстояние.</summary>
        public double Project(V2 p, out double t, out V2 q)
        {
            if (Kind == SegKind.Line)
            {
                V2 d = P1 - P0; double l2 = V2.Dot(d, d);
                t = l2 < 1e-18 ? 0 : V2.Dot(p - P0, d) / l2;
                if (t < 0) t = 0; if (t > 1) t = 1;
                q = PointAt(t); return V2.Dist(p, q);
            }
            double a = (p - C).Angle;
            double da = Geo.DirNorm(a - A0, Sw > 0);
            if (Math.Abs(da) <= Math.Abs(Sw) + 1e-12)
            {
                t = Math.Abs(Sw) < 1e-15 ? 0 : da / Sw; q = PointAt(t); return V2.Dist(p, q);
            }
            double d0 = V2.Dist(p, Start), d1 = V2.Dist(p, End);
            if (d0 < d1) { t = 0; q = Start; return d0; }
            t = 1; q = End; return d1;
        }

        public bool AngleInside(double a)
        {
            if (IsCircle) return true;
            double da = Geo.DirNorm(a - A0, Sw > 0);
            return Math.Abs(da) <= Math.Abs(Sw) + 1e-12;
        }

        public void TrimEnd(V2 p)
        {
            if (Kind == SegKind.Line) { P1 = p; return; }
            double old = Sw;
            double s = Geo.DirNorm((p - C).Angle - A0, Sw > 0);
            if (Math.Abs(s) < 1e-9 && Math.Abs(old) > Math.PI) s = Sw > 0 ? 2 * Math.PI : -2 * Math.PI;
            Sw = s;
        }

        public void TrimStart(V2 p)
        {
            if (Kind == SegKind.Line) { P0 = p; return; }
            double end = A0 + Sw, old = Sw;
            double na = (p - C).Angle;
            double s = Geo.DirNorm(end - na, Sw > 0);
            if (Math.Abs(s) < 1e-9 && Math.Abs(old) > Math.PI) s = Sw > 0 ? 2 * Math.PI : -2 * Math.PI;
            A0 = na; Sw = s;
        }

        public void Bounds(ref double x0, ref double y0, ref double x1, ref double y1)
        {
            Grow(Start, ref x0, ref y0, ref x1, ref y1);
            Grow(End, ref x0, ref y0, ref x1, ref y1);
            if (Kind == SegKind.Arc)
                for (int k = 0; k < 4; k++)
                {
                    double a = k * Math.PI / 2;
                    if (AngleInside(a)) Grow(C + V2.Polar(a, R), ref x0, ref y0, ref x1, ref y1);
                }
        }
        static void Grow(V2 p, ref double x0, ref double y0, ref double x1, ref double y1)
        {
            if (p.X < x0) x0 = p.X; if (p.Y < y0) y0 = p.Y; if (p.X > x1) x1 = p.X; if (p.Y > y1) y1 = p.Y;
        }

        // Преобразования для команд редактирования
        public void Move(V2 d) { P0 += d; P1 += d; C += d; }
        public void Rotate(V2 b, double ang)
        {
            P0 = Geo.Rot(P0, b, ang); P1 = Geo.Rot(P1, b, ang); C = Geo.Rot(C, b, ang); A0 += ang;
        }
        public void Scale(V2 b, double k)
        {
            P0 = b + (P0 - b) * k; P1 = b + (P1 - b) * k; C = b + (C - b) * k; R *= Math.Abs(k);
        }
        public void Mirror(V2 a, V2 b)
        {
            if (Kind == SegKind.Line) { P0 = Geo.Refl(P0, a, b); P1 = Geo.Refl(P1, a, b); return; }
            V2 s = Geo.Refl(Start, a, b);
            C = Geo.Refl(C, a, b);
            A0 = (s - C).Angle; Sw = -Sw;
        }
    }

    public class Contour
    {
        public List<Seg> Segs = new List<Seg>();
        public bool Closed;

        public V2 Start { get { return Segs[0].Start; } }
        public V2 End { get { return Segs[Segs.Count - 1].End; } }
        public double Length { get { double l = 0; foreach (Seg s in Segs) l += s.Length; return l; } }

        /// <summary>Площадь со знаком (+ = обход против часовой).</summary>
        public double Area
        {
            get
            {
                double a = 0;
                foreach (Seg s in Segs)
                {
                    if (s.Kind == SegKind.Line) a += 0.5 * (s.P0.X * s.P1.Y - s.P1.X * s.P0.Y);
                    else
                    {
                        double a0 = s.A0, a1 = s.A0 + s.Sw;
                        a += 0.5 * (s.R * (s.C.X * (Math.Sin(a1) - Math.Sin(a0)) - s.C.Y * (Math.Cos(a1) - Math.Cos(a0))) + s.R * s.R * s.Sw);
                    }
                }
                return a;
            }
        }

        /// <summary>Точка внутри замкнутого контура (луч, дуги разбиты на хорды).</summary>
        public bool Contains(V2 p)
        {
            List<V2> poly = new List<V2>();
            foreach (Seg s in Segs)
            {
                int n = s.Kind == SegKind.Line ? 1 : Math.Max(8, (int)(Math.Abs(s.Sw) * 24));
                for (int k = 0; k < n; k++) poly.Add(s.PointAt((double)k / n));
            }
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                    (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    inside = !inside;
            }
            return inside;
        }

        public Contour Clone()
        {
            Contour c = new Contour { Closed = Closed };
            foreach (Seg s in Segs) c.Segs.Add(s.Clone());
            return c;
        }

        public Contour Reversed()
        {
            Contour c = new Contour { Closed = Closed };
            for (int i = Segs.Count - 1; i >= 0; i--) c.Segs.Add(Segs[i].Reversed());
            return c;
        }

        public double DistTo(V2 p, out int idx, out double t)
        {
            double best = double.MaxValue; idx = 0; t = 0;
            for (int i = 0; i < Segs.Count; i++)
            {
                double tt; V2 q;
                double d = Segs[i].Project(p, out tt, out q);
                if (d < best) { best = d; idx = i; t = tt; }
            }
            return best;
        }

        /// <summary>Длина по контуру от начала до ближайшей к p точки.</summary>
        public double LenTo(V2 p)
        {
            int i; double t; DistTo(p, out i, out t);
            double l = 0; for (int k = 0; k < i; k++) l += Segs[k].Length;
            return l + Segs[i].Length * t;
        }

        /// <summary>Путь по контуру от его начала до ближайшей к p точки (для перекрытия до точки выхода).</summary>
        public List<Seg> Upto(V2 p)
        {
            List<Seg> r = new List<Seg>();
            int i; double t; DistTo(p, out i, out t);
            for (int k = 0; k < i; k++) r.Add(Segs[k].Clone());
            if (t > 1e-9)
            {
                Seg s = Segs[i];
                r.Add(s.Kind == SegKind.Line ? Seg.Line(s.P0, s.PointAt(t)) : Seg.Arc(s.C, s.R, s.A0, s.Sw * t));
            }
            double l = 0; foreach (Seg x in r) l += x.Length;
            if (l < 1e-6 || l > Length - 1e-6) r.Clear();   // точка выхода = точка входа
            return r;
        }

        /// <summary>Начать замкнутый контур с ближайшей к p точки.</summary>
        public Contour StartAt(V2 p)
        {
            int i; double t;
            DistTo(p, out i, out t);
            return StartAt(i, t);
        }

        public Contour StartAt(int i, double t)
        {
            Contour r = new Contour { Closed = true };
            int n = Segs.Count;
            double len = Segs[i].Length;
            if (t * len < 1e-6) { for (int k = 0; k < n; k++) r.Segs.Add(Segs[(i + k) % n].Clone()); return r; }
            if ((1 - t) * len < 1e-6) { for (int k = 0; k < n; k++) r.Segs.Add(Segs[(i + 1 + k) % n].Clone()); return r; }
            Seg a, b; Segs[i].SplitAt(t, out a, out b);
            if (Segs[i].IsCircle && n == 1)
            {
                // окружность: начинаем с указанной точки, полный оборот
                Seg full = Segs[i].Clone(); full.A0 = Segs[i].A0 + Segs[i].Sw * t;
                r.Segs.Add(full); return r;
            }
            r.Segs.Add(b);
            for (int k = 1; k < n; k++) r.Segs.Add(Segs[(i + k) % n].Clone());
            r.Segs.Add(a);
            return r;
        }
    }

    public static class Geo
    {
        public const double Eps = 1e-9;
        public static string F(double v) { return Math.Round(v, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Нормализация угла в направлении: ccw → [0,2π), cw → (-2π,0].</summary>
        public static double DirNorm(double d, bool ccw)
        {
            double t = d % (2 * Math.PI);
            if (ccw) { if (t < 0) t += 2 * Math.PI; }
            else { if (t > 0) t -= 2 * Math.PI; }
            return t;
        }

        public static V2 Rot(V2 p, V2 b, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a); V2 d = p - b;
            return b + new V2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        }

        public static V2 Refl(V2 p, V2 a, V2 b)
        {
            V2 d = (b - a).Norm(); V2 v = p - a;
            V2 proj = a + d * V2.Dot(v, d);
            return proj * 2 - p;
        }

        /// <summary>Сборка элементов в контуры по совпадающим концам.</summary>
        public static List<Contour> Chain(List<Seg> src, double tol)
        {
            List<Contour> res = new List<Contour>();
            List<Seg> pool = new List<Seg>();
            foreach (Seg s in src) if (s.Length > 1e-9) pool.Add(s.Clone());
            bool[] used = new bool[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                Contour c = new Contour();
                c.Segs.Add(pool[i]);
                if (pool[i].IsCircle) { c.Closed = true; res.Add(c); continue; }
                // вперёд
                bool grown = true;
                while (grown && V2.Dist(c.End, c.Start) > tol)
                {
                    grown = false;
                    V2 e = c.End;
                    for (int j = 0; j < pool.Count; j++)
                    {
                        if (used[j] || pool[j].IsCircle) continue;
                        if (V2.Dist(pool[j].Start, e) <= tol) { c.Segs.Add(pool[j]); used[j] = true; grown = true; break; }
                        if (V2.Dist(pool[j].End, e) <= tol) { c.Segs.Add(pool[j].Reversed()); used[j] = true; grown = true; break; }
                    }
                }
                // назад
                grown = true;
                while (grown && V2.Dist(c.End, c.Start) > tol)
                {
                    grown = false;
                    V2 st = c.Start;
                    for (int j = 0; j < pool.Count; j++)
                    {
                        if (used[j] || pool[j].IsCircle) continue;
                        if (V2.Dist(pool[j].End, st) <= tol) { c.Segs.Insert(0, pool[j]); used[j] = true; grown = true; break; }
                        if (V2.Dist(pool[j].Start, st) <= tol) { c.Segs.Insert(0, pool[j].Reversed()); used[j] = true; grown = true; break; }
                    }
                }
                c.Closed = V2.Dist(c.End, c.Start) <= tol;
                // подтянуть концы точно друг к другу
                for (int k = 1; k < c.Segs.Count; k++) Snap(c.Segs[k - 1], c.Segs[k]);
                if (c.Closed && c.Segs.Count > 1) Snap(c.Segs[c.Segs.Count - 1], c.Segs[0]);
                res.Add(c);
            }
            return res;
        }

        static void Snap(Seg a, Seg b)
        {
            if (a.Kind == SegKind.Line && b.Kind == SegKind.Line) { b.P0 = a.P1; return; }
            if (a.Kind == SegKind.Line) { a.P1 = b.Start; return; }
            if (b.Kind == SegKind.Line) { b.P0 = a.End; return; }
        }

        // ---------- пересечения (бесконечные линии / полные окружности) ----------
        public static List<V2> Intersect(Seg a, Seg b)
        {
            List<V2> r = new List<V2>();
            if (a.Kind == SegKind.Line && b.Kind == SegKind.Line)
            {
                V2 d1 = a.P1 - a.P0, d2 = b.P1 - b.P0;
                double den = V2.Cross(d1, d2);
                if (Math.Abs(den) < 1e-12) return r;
                double t = V2.Cross(b.P0 - a.P0, d2) / den;
                r.Add(a.P0 + d1 * t);
                return r;
            }
            if (a.Kind == SegKind.Arc && b.Kind == SegKind.Line) return Intersect(b, a);
            if (a.Kind == SegKind.Line)
            {
                V2 d = (a.P1 - a.P0).Norm();
                V2 f = a.P0 - b.C;
                double bb = V2.Dot(f, d), cc = V2.Dot(f, f) - b.R * b.R;
                double disc = bb * bb - cc;
                if (disc < -1e-9) return r;
                if (disc < 0) disc = 0;
                double s = Math.Sqrt(disc);
                r.Add(a.P0 + d * (-bb - s)); r.Add(a.P0 + d * (-bb + s));
                return r;
            }
            {
                V2 d = b.C - a.C; double dist = d.Len;
                if (dist < 1e-12) return r;
                double x = (dist * dist + a.R * a.R - b.R * b.R) / (2 * dist);
                double h2 = a.R * a.R - x * x;
                if (h2 < -1e-9) return r;
                if (h2 < 0) h2 = 0;
                double h = Math.Sqrt(h2);
                V2 u = d * (1 / dist);
                V2 m = a.C + u * x;
                r.Add(m + u.Perp() * h); r.Add(m - u.Perp() * h);
                return r;
            }
        }

        /// <summary>
        /// Эквидистанта контура. d &gt; 0 — смещение влево по ходу, d &lt; 0 — вправо.
        /// Наружные углы скругляются дугой радиуса |d| (как реально идёт проволока).
        /// </summary>
        public static Contour Offset(Contour src, double d, List<string> warn)
        {
            if (Math.Abs(d) < 1e-12) return src.Clone();
            int n = src.Segs.Count;
            List<Seg> off = new List<Seg>();
            foreach (Seg s in src.Segs)
            {
                Seg o = s.Clone();
                if (s.Kind == SegKind.Line)
                {
                    V2 nrm = (s.P1 - s.P0).Norm().Perp() * d;
                    o.P0 = s.P0 + nrm; o.P1 = s.P1 + nrm;
                }
                else
                {
                    o.R = s.R - d * Math.Sign(s.Sw);
                    if (o.R < 1e-4)
                    {
                        warn.Add("Радиус R" + F(s.R) + " меньше смещения " + F(Math.Abs(d)) + " — проволока не впишется (точка " + s.Start + ")");
                        return null;
                    }
                }
                off.Add(o);
            }
            Seg[] joins = new Seg[n];
            int last = src.Closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int j = (i + 1) % n;
                Seg a = off[i], b = off[j];
                V2 V = src.Segs[i].End;
                V2 ta = src.Segs[i].TanEnd, tb = src.Segs[j].TanStart;
                double cr = V2.Cross(ta, tb), dot = V2.Dot(ta, tb);
                V2 pa = a.End, pb = b.Start;
                if (V2.Dist(pa, pb) < 1e-7) continue;
                bool outside = (d > 0 && cr < -1e-12) || (d < 0 && cr > 1e-12) || (Math.Abs(cr) <= 1e-12 && dot < 0);
                if (outside)
                {
                    bool ccw = d < 0; // при смещении влево и повороте вправо проволока огибает угол по часовой
                    double a0 = (pa - V).Angle, a1 = (pb - V).Angle;
                    joins[i] = Arc(V, Math.Abs(d), a0, DirNorm(a1 - a0, ccw));
                    continue;
                }
                List<V2> xs = Intersect(a, b);
                if (xs.Count == 0) { joins[i] = Seg.Line(pa, pb); continue; }
                V2 m = (pa + pb) * 0.5; V2 best = xs[0];
                foreach (V2 x in xs) if (V2.Dist(x, m) < V2.Dist(best, m)) best = x;
                V2 oldDirA = a.Kind == SegKind.Line ? a.P1 - a.P0 : new V2();
                V2 oldDirB = b.Kind == SegKind.Line ? b.P1 - b.P0 : new V2();
                a.TrimEnd(best); b.TrimStart(best);
                if ((a.Kind == SegKind.Line && V2.Dot(a.P1 - a.P0, oldDirA) <= 0) ||
                    (b.Kind == SegKind.Line && V2.Dot(b.P1 - b.P0, oldDirB) <= 0))
                    warn.Add("Элемент короче смещения у точки " + V + " — проверьте траекторию");
            }
            Contour r = new Contour { Closed = src.Closed };
            for (int i = 0; i < n; i++)
            {
                if (off[i].Length > 1e-7) r.Segs.Add(off[i]);
                if (joins[i] != null && joins[i].Length > 1e-7) r.Segs.Add(joins[i]);
            }
            return r;
        }

        static Seg Arc(V2 c, double r, double a0, double sw) { return Seg.Arc(c, r, a0, sw); }

        /// <summary>Пересечения в пределах обоих элементов.</summary>
        public static List<V2> IntersectSegs(Seg a, Seg b)
        {
            List<V2> r = new List<V2>();
            foreach (V2 x in Intersect(a, b))
            {
                double t; V2 q;
                if (a.Project(x, out t, out q) < 1e-6 && b.Project(x, out t, out q) < 1e-6) r.Add(x);
            }
            return r;
        }

        /// <summary>Часть элемента между параметрами t0..t1.</summary>
        public static Seg Sub(Seg s, double t0, double t1)
        {
            if (s.Kind == SegKind.Line) return Seg.Line(s.PointAt(t0), s.PointAt(t1));
            return Seg.Arc(s.C, s.R, s.A0 + s.Sw * t0, s.Sw * (t1 - t0));
        }

        /// <summary>Разделить цепочку элементов на «голову» длиной L и «хвост».</summary>
        public static void SplitByLength(List<Seg> segs, double L, List<Seg> head, List<Seg> tail)
        {
            double acc = 0;
            foreach (Seg s in segs)
            {
                double l = s.Length;
                if (acc >= L - 1e-9) { tail.Add(s.Clone()); acc += l; continue; }
                if (acc + l <= L + 1e-9) { head.Add(s.Clone()); acc += l; continue; }
                Seg a, b; s.SplitAt((L - acc) / l, out a, out b);
                head.Add(a); tail.Add(b); acc += l;
            }
        }

        public static List<Seg> ReverseList(List<Seg> segs)
        {
            List<Seg> r = new List<Seg>();
            for (int i = segs.Count - 1; i >= 0; i--) r.Add(segs[i].Reversed());
            return r;
        }
    }
}

namespace WireCut
{
    // делегаты (в .NET 2.0 нет Func/Action с несколькими параметрами)
    public delegate void Act2<A, B>(A a, B b);
    public delegate R Fn<R>();
    public delegate R Fn1<A, R>(A a);
}
