// STZ WireCut — измерения детали: линейки, размеры (линейный, радиус, диаметр, угол), габарит, подсказка у курсора
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WireCut
{
    public enum DimKind { Horizontal, Vertical, Aligned, Radius, Diameter, Angle }

    public class Dim
    {
        public DimKind K;
        public V2 P1, P2;     // точки измерения (для угла — вершина и не используется)
        public V2 C; public double R;           // радиус / диаметр
        public V2 V, U1, U2;                    // угол: вершина и направления лучей
        public V2 Pos;        // положение размерной линии / текста
        public Seg A;         // объект, к которому привязан размер (следует за ним); null — свободный размер
        public double Off;    // отступ размерной линии от отрезка, пикселей экрана
        public V2 Rel;        // положение надписи относительно центра дуги / окружности
        public RectangleF LabelRect;   // где нарисована цифра (для щелчка)
        public Dim Clone() { return (Dim)MemberwiseClone(); }

        /// <summary>Обновить размер по объекту. false — объект удалён.</summary>
        public bool Sync(CadCanvas cv)
        {
            if (A == null) return true;
            if (!cv.Ents.Contains(A)) return false;
            if (K == DimKind.Radius || K == DimKind.Diameter) { C = A.C; R = A.R; Pos = C + Rel; return true; }
            if (A.Kind != SegKind.Line) return false;
            P1 = A.Start; P2 = A.End;
            V2 dir = K == DimKind.Horizontal ? new V2(1, 0) : K == DimKind.Vertical ? new V2(0, 1) : (P2 - P1).Norm();
            Pos = (P1 + P2) * 0.5 + dir.Perp() * (Off / cv.Scale);   // отступ — в пикселях экрана
            return true;
        }

        public double Value
        {
            get
            {
                switch (K)
                {
                    case DimKind.Horizontal: return Math.Abs(P2.X - P1.X);
                    case DimKind.Vertical: return Math.Abs(P2.Y - P1.Y);
                    case DimKind.Aligned: return V2.Dist(P1, P2);
                    case DimKind.Radius: return R;
                    case DimKind.Diameter: return 2 * R;
                    default: return Math.Acos(Math.Max(-1, Math.Min(1, V2.Dot(U1, U2)))) * 180 / Math.PI;
                }
            }
        }

        public string Text
        {
            get
            {
                string v = Math.Round(Value, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                switch (K) { case DimKind.Radius: return "R" + v; case DimKind.Diameter: return "Ø" + v; case DimKind.Angle: return v + "°"; default: return v; }
            }
        }
    }

    public static class DimDraw
    {
        public static readonly Color Col = Color.FromArgb(110, 205, 255);

        public static void Draw(Graphics g, CadCanvas cv, Dim d, bool ghost) { Draw(g, cv, d, ghost, true); }

        public static void Draw(Graphics g, CadCanvas cv, Dim d, bool ghost, bool label)
        {
            if (!d.Sync(cv)) return;
            Color col = cv.EditDim == d ? Theme.Orange : (ghost ? Color.FromArgb(160, Col) : Col);
            using (Pen p = new Pen(col, cv.EditDim == d ? 2f : 1.2f))
            using (Brush b = new SolidBrush(col))
            {
                switch (d.K)
                {
                    case DimKind.Horizontal: case DimKind.Vertical: case DimKind.Aligned:
                        {
                            V2 dir = d.K == DimKind.Horizontal ? new V2(1, 0) : d.K == DimKind.Vertical ? new V2(0, 1) : (d.P2 - d.P1).Norm();
                            if (dir.Len < 1e-9) dir = new V2(1, 0);
                            V2 n = dir.Perp();
                            V2 A = d.P1 + n * V2.Dot(d.Pos - d.P1, n), B = d.P2 + n * V2.Dot(d.Pos - d.P2, n);
                            // выносные линии с небольшим зазором и выходом за размерную линию
                            Ext(g, cv, p, d.P1, A); Ext(g, cv, p, d.P2, B);
                            g.DrawLine(p, cv.ToScr(A), cv.ToScr(B));
                            Arrow(g, b, cv.ToScr(A), cv.ToScr(B)); Arrow(g, b, cv.ToScr(B), cv.ToScr(A));
                            if (label) d.LabelRect = Label(g, cv, (A + B) * 0.5, d.Text, b);
                            break;
                        }
                    case DimKind.Radius: case DimKind.Diameter:
                        {
                            V2 u = (d.Pos - d.C).Norm(); if (u.Len < 1e-9) u = new V2(1, 0);
                            V2 P = d.C + u * d.R;
                            V2 S = d.K == DimKind.Diameter ? d.C - u * d.R : d.C;
                            g.DrawLine(p, cv.ToScr(S), cv.ToScr(d.Pos));
                            Arrow(g, b, cv.ToScr(P), cv.ToScr(d.C));
                            if (d.K == DimKind.Diameter) Arrow(g, b, cv.ToScr(S), cv.ToScr(d.C));
                            else { PointF c = cv.ToScr(d.C); g.DrawLine(p, c.X - 4, c.Y, c.X + 4, c.Y); g.DrawLine(p, c.X, c.Y - 4, c.X, c.Y + 4); }
                            if (label) d.LabelRect = Label(g, cv, d.Pos, d.Text, b);
                            break;
                        }
                    case DimKind.Angle:
                        {
                            double r = Math.Max(V2.Dist(d.Pos, d.V), 1e-6);
                            double a1 = d.U1.Angle, a2 = d.U2.Angle;
                            double sw = Geo.DirNorm(a2 - a1, true);
                            if (sw > Math.PI) { double t = a1; a1 = a2; a2 = t; sw = 2 * Math.PI - sw; }
                            Seg arc = Seg.Arc(d.V, r, a1, sw);
                            cv.DrawSeg(g, p, arc);
                            g.DrawLine(p, cv.ToScr(d.V), cv.ToScr(d.V + V2.Polar(a1, r * 1.08)));
                            g.DrawLine(p, cv.ToScr(d.V), cv.ToScr(d.V + V2.Polar(a2, r * 1.08)));
                            if (label) d.LabelRect = Label(g, cv, d.V + V2.Polar(a1 + sw / 2, r), d.Text, b);
                            break;
                        }
                }
            }
        }

        static void Ext(Graphics g, CadCanvas cv, Pen p, V2 from, V2 to)
        {
            PointF a = cv.ToScr(from), b = cv.ToScr(to);
            double dx = b.X - a.X, dy = b.Y - a.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1) return;
            float ux = (float)(dx / l), uy = (float)(dy / l);
            g.DrawLine(p, a.X + ux * 3, a.Y + uy * 3, b.X + ux * 6, b.Y + uy * 6);
        }

        public static void Arrow(Graphics g, Brush b, PointF tip, PointF from)
        {
            double dx = tip.X - from.X, dy = tip.Y - from.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1) return;
            float ux = (float)(dx / l), uy = (float)(dy / l), s = 11, w = 3.5f;
            PointF[] tri = { tip, new PointF(tip.X - ux * s - uy * w, tip.Y - uy * s + ux * w), new PointF(tip.X - ux * s + uy * w, tip.Y - uy * s - ux * w) };
            g.FillPolygon(b, tri);
        }

        public static RectangleF Label(Graphics g, CadCanvas cv, V2 at, string text, Brush b)
        {
            using (Font f = new Font(MainForm.UiFont, 10.5f, FontStyle.Bold))
            {
                PointF p = cv.ToScr(at);
                SizeF sz = g.MeasureString(text, f);
                RectangleF r = new RectangleF(p.X - sz.Width / 2 - 3, p.Y - sz.Height / 2 - 1, sz.Width + 6, sz.Height + 2);
                using (Brush bg = new SolidBrush(Color.FromArgb(215, 25, 30, 36))) g.FillRectangle(bg, r);
                g.DrawString(text, f, b, r.X + 3, r.Y + 1);
                return r;
            }
        }

        /// <summary>Поле ввода размера (как во Fusion 360): 0 — обычное, 1 — активное (ввод), 2 — заданное.</summary>
        public static void Field(Graphics g, CadCanvas cv, V2 at, string text, int state)
        {
            using (Font f = new Font(MainForm.UiFont, 11f, FontStyle.Bold))
            {
                PointF p = cv.ToScr(at);
                SizeF sz = g.MeasureString(text, f);
                RectangleF r = new RectangleF(p.X - sz.Width / 2 - 6, p.Y - sz.Height / 2 - 3, sz.Width + 12, sz.Height + 6);
                Color back = state == 1 ? Color.FromArgb(245, 255, 255, 255) : Color.FromArgb(235, 30, 36, 44);
                Color fore = state == 1 ? Color.FromArgb(20, 30, 40) : state == 2 ? Color.FromArgb(255, 200, 120) : Color.FromArgb(225, 235, 240);
                Color border = state == 1 ? Theme.Accent : state == 2 ? Theme.Orange : Col;
                using (Brush bg = new SolidBrush(back)) g.FillRectangle(bg, r);
                using (Pen pb = new Pen(border, state == 0 ? 1f : 2f)) g.DrawRectangle(pb, r.X, r.Y, r.Width, r.Height);
                using (Brush fb = new SolidBrush(fore)) g.DrawString(text, f, fb, r.X + 6, r.Y + 3);
            }
        }

        /// <summary>Временный размер для выделенного объекта.</summary>
        public static Dim Quick(CadCanvas cv, Seg s)
        {
            if (s.Kind == SegKind.Line)
            {
                if (s.Length < 1e-9) return null;
                V2 dd = s.P1 - s.P0;
                DimKind k = Math.Abs(dd.Y) < 1e-9 ? DimKind.Horizontal : Math.Abs(dd.X) < 1e-9 ? DimKind.Vertical : DimKind.Aligned;
                return new Dim { K = k, A = s, Off = 22 };
            }
            if (s.IsCircle) return new Dim { K = DimKind.Diameter, A = s, Rel = V2.Polar(Math.PI / 4, s.R + 30 / cv.Scale) };
            return new Dim { K = DimKind.Radius, A = s, Rel = V2.Polar(s.A0 + s.Sw / 2, s.R * 0.55) };
        }

        /// <summary>Габаритные размеры всей геометрии (снизу — длина, справа — ширина).</summary>
        public static void Extents(Graphics g, CadCanvas cv)
        {
            if (cv.Ents.Count == 0) return;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Seg s in cv.Ents) s.Bounds(ref x0, ref y0, ref x1, ref y1);
            double off = 26 / cv.Scale;
            Draw(g, cv, new Dim { K = DimKind.Horizontal, P1 = new V2(x0, y0), P2 = new V2(x1, y0), Pos = new V2(0, y0 - off) }, false);
            Draw(g, cv, new Dim { K = DimKind.Vertical, P1 = new V2(x1, y0), P2 = new V2(x1, y1), Pos = new V2(x1 + off, 0) }, false);
        }

        /// <summary>Линейки сверху и слева с делениями в мм.</summary>
        public static void Rulers(Graphics g, CadCanvas cv, Point mouse, bool mouseIn)
        {
            const int H = 24;
            Color bg = Color.FromArgb(235, 40, 46, 54), tick = Color.FromArgb(150, 160, 170);
            SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
            using (Brush bb = new SolidBrush(bg)) { g.FillRectangle(bb, 0, 0, cv.Width, H); g.FillRectangle(bb, 0, 0, H + 14, cv.Height); }
            // шаг подписи — «красивое» число так, чтобы между подписями было ≥ 70 px
            double raw = 70 / cv.Scale, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), step = mag;
            foreach (double k in new double[] { 1, 2, 5, 10 }) { if (k * mag >= raw) { step = k * mag; break; } }
            double minor = step / 10;
            V2 w0 = cv.ToWorld(new Point(0, cv.Height)), w1 = cv.ToWorld(new Point(cv.Width, 0));
            using (Pen p = new Pen(tick))
            using (Font f = new Font(MainForm.UiFont, 8.5f))
            using (Brush tb = new SolidBrush(Color.FromArgb(215, 220, 225)))
            {
                // горизонтальная
                long i0 = (long)Math.Floor(w0.X / minor), i1 = (long)Math.Ceiling(w1.X / minor);
                if (i1 - i0 < 3000)
                    for (long i = i0; i <= i1; i++)
                    {
                        float x = cv.ToScr(new V2(i * minor, 0)).X; if (x < H + 14) continue;
                        bool major = i % 10 == 0, half = i % 5 == 0;
                        g.DrawLine(p, x, H, x, H - (major ? 12 : half ? 8 : 4));
                        if (major) g.DrawString(Fmt(i * minor), f, tb, x + 2, 1);
                    }
                // вертикальная
                long j0 = (long)Math.Floor(w0.Y / minor), j1 = (long)Math.Ceiling(w1.Y / minor);
                if (j1 - j0 < 3000)
                    for (long j = j0; j <= j1; j++)
                    {
                        float y = cv.ToScr(new V2(0, j * minor)).Y; if (y < H) continue;
                        bool major = j % 10 == 0, half = j % 5 == 0;
                        g.DrawLine(p, H + 14, y, H + 14 - (major ? 12 : half ? 8 : 4), y);
                        if (major)
                        {
                            GraphicsState st = g.Save();
                            g.TranslateTransform(1, y - 2); g.RotateTransform(-90);
                            g.DrawString(Fmt(j * minor), f, tb, 0, 0);
                            g.Restore(st);
                        }
                    }
                g.DrawLine(p, 0, H, cv.Width, H); g.DrawLine(p, H + 14, 0, H + 14, cv.Height);
                // положение курсора
                if (mouseIn)
                    using (Pen pc = new Pen(Theme.Orange, 2f))
                    {
                        g.DrawLine(pc, mouse.X, 0, mouse.X, H);
                        g.DrawLine(pc, 0, mouse.Y, H + 14, mouse.Y);
                    }
                g.DrawString("мм", f, tb, 4, 4);
            }
            g.SmoothingMode = sm;
        }

        static string Fmt(double v) { v = Math.Round(v, 6); return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Подсказка у курсора: размеры элемента под ним.</summary>
        public static string Describe(Seg s)
        {
            if (s == null) return null;
            if (s.Kind == SegKind.Line)
            {
                V2 d = s.P1 - s.P0; double a = Math.Atan2(d.Y, d.X) * 180 / Math.PI; if (a < 0) a += 360;
                return "Отрезок  L " + Geo.F(Math.Round(d.Len, 3)) + "   угол " + Geo.F(Math.Round(a, 2)) + "°\ndX " + Geo.F(Math.Round(d.X, 3)) + "   dY " + Geo.F(Math.Round(d.Y, 3));
            }
            if (s.IsCircle) return "Окружность  Ø " + Geo.F(Math.Round(2 * s.R, 3)) + "   R " + Geo.F(Math.Round(s.R, 3)) + "\nцентр " + s.C;
            return "Дуга  R " + Geo.F(Math.Round(s.R, 3)) + "   угол " + Geo.F(Math.Round(Math.Abs(s.Sw) * 180 / Math.PI, 2)) + "°\nдлина " + Geo.F(Math.Round(s.Length, 3)) + "   центр " + s.C;
        }

        public static void Hint(Graphics g, Point at, string text)
        {
            using (Font f = new Font(MainForm.UiFont, 9.5f))
            {
                SizeF sz = g.MeasureString(text, f);
                RectangleF r = new RectangleF(at.X + 18, at.Y + 18, sz.Width + 10, sz.Height + 6);
                using (Brush bg = new SolidBrush(Color.FromArgb(230, 20, 24, 30))) g.FillRectangle(bg, r);
                using (Pen p = new Pen(Col)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                using (Brush b = new SolidBrush(Color.FromArgb(235, 235, 235))) g.DrawString(text, f, b, r.X + 5, r.Y + 3);
            }
        }
    }

    // ---------- команды размеров ----------
    public class DimLinearCmd : Cmd
    {
        V2? p1, p2; bool aligned;
        public override string Prompt { get { return !p1.HasValue ? "Первая точка размера (привязка к концам и центрам):" : !p2.HasValue ? "Вторая точка:" : "Положение размерной линии или [Выровненный] (" + (aligned ? "по наклону" : "гор./верт.") + "):"; } }
        public override void Point(V2 p)
        {
            if (!p1.HasValue) { p1 = p; Last = p; return; }
            if (!p2.HasValue) { if (V2.Dist(p1.Value, p) > 1e-9) p2 = p; return; }
            Cv.Dims.Add(Make(p)); Cv.Invalidate();
            F.Msg("  Размер: " + Make(p).Text + " мм");
            F.EndCmd();
        }
        public override bool Text(string t)
        {
            if (p2.HasValue && Is(t, "В", "A", "ВЫРОВНЕННЫЙ", "ALIGNED")) { aligned = !aligned; return true; }
            return false;
        }
        Dim Make(V2 pos)
        {
            V2 a = p1.Value, b = p2.Value, mid = (a + b) * 0.5;
            DimKind k = aligned ? DimKind.Aligned : (Math.Abs(pos.Y - mid.Y) >= Math.Abs(pos.X - mid.X) ? DimKind.Horizontal : DimKind.Vertical);
            if (!aligned && Math.Abs(b.X - a.X) < 1e-9) k = DimKind.Vertical;
            if (!aligned && Math.Abs(b.Y - a.Y) < 1e-9) k = DimKind.Horizontal;
            return new Dim { K = k, P1 = a, P2 = b, Pos = pos };
        }
        public override void Rubber(Graphics g, V2 c)
        {
            if (p1.HasValue && !p2.HasValue) Line(g, p1.Value, c);
            if (p2.HasValue) DimDraw.Draw(g, Cv, Make(c), true);
        }
    }

    public class DimRadiusCmd : Cmd
    {
        bool diam; Seg arc;
        public DimRadiusCmd(bool diameter) { diam = diameter; }
        public override string Prompt { get { return arc == null ? "Выберите дугу или окружность:" : "Положение надписи:"; } }
        public override void Point(V2 p)
        {
            if (arc == null)
            {
                Seg s = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
                if (s == null || s.Kind != SegKind.Arc) { F.Msg("  Нужна дуга или окружность."); return; }
                arc = s; return;
            }
            Dim d = Make(p); Cv.Dims.Add(d); Cv.Invalidate();
            F.Msg("  Размер: " + d.Text + " мм"); F.EndCmd();
        }
        Dim Make(V2 pos) { return new Dim { K = diam ? DimKind.Diameter : DimKind.Radius, C = arc.C, R = arc.R, Pos = pos }; }
        public override void Rubber(Graphics g, V2 c) { if (arc != null) DimDraw.Draw(g, Cv, Make(c), true); }
    }

    public class DimAngleCmd : Cmd
    {
        Seg l1; V2 pk1; Dim d;
        public override string Prompt { get { return l1 == null ? "Первый отрезок угла:" : d == null ? "Второй отрезок:" : "Положение дуги размера:"; } }
        public override void Point(V2 p)
        {
            if (d != null) { d.Pos = p; Cv.Dims.Add(d); Cv.Invalidate(); F.Msg("  Угол: " + d.Text); F.EndCmd(); return; }
            Seg s = Cv.HitTest(System.Drawing.Point.Round(Cv.ToScr(p)));
            if (s == null || s.Kind != SegKind.Line) { F.Msg("  Нужен отрезок."); return; }
            if (l1 == null) { l1 = s; pk1 = p; return; }
            List<V2> xs = Geo.Intersect(l1, s);
            if (xs.Count == 0) { F.Msg("  Отрезки параллельны."); return; }
            V2 v = xs[0];
            d = new Dim { K = DimKind.Angle, V = v, U1 = Dir(l1, pk1, v), U2 = Dir(s, p, v), Pos = p };
        }
        static V2 Dir(Seg l, V2 pick, V2 v)
        {
            double t; V2 q; l.Project(pick, out t, out q);
            V2 u = q - v; if (u.Len < 1e-9) u = (V2.Dist(l.P0, v) > V2.Dist(l.P1, v) ? l.P0 : l.P1) - v;
            return u.Norm();
        }
        public override void Rubber(Graphics g, V2 c) { if (d != null) { d.Pos = c; DimDraw.Draw(g, Cv, d, true); } }
    }
}
