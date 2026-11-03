// STZ WireCut — чтение и запись DXF (AutoCAD, Fusion 360)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WireCut
{
    public static class Dxf
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        class Pair { public int Code; public string Val; }

        static double D(string s)
        {
            double v; double.TryParse(s.Trim(), NumberStyles.Float, IC, out v); return v;
        }

        /// <summary>Чтение DXF: LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE, ELLIPSE.</summary>
        public static List<Seg> Read(string path, List<string> log)
        {
            string[] lines = File.ReadAllLines(path, Encoding.Default);
            List<Pair> pairs = new List<Pair>();
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int c;
                if (!int.TryParse(lines[i].Trim(), out c)) { i--; continue; }
                pairs.Add(new Pair { Code = c, Val = lines[i + 1].Trim() });
            }
            double unit = 1.0;
            List<Seg> res = new List<Seg>();
            Dictionary<string, int> skipped = new Dictionary<string, int>();
            bool inEnt = false, inBlocks = false;
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair p = pairs[i];
                if (p.Code == 9 && p.Val == "$INSUNITS" && i + 1 < pairs.Count)
                {
                    int u; int.TryParse(pairs[i + 1].Val, out u);
                    if (u == 1) unit = 25.4; else if (u == 5) unit = 10; else if (u == 6) unit = 1000;
                }
                if (p.Code == 2 && p.Val == "ENTITIES") inEnt = true;
                if (p.Code == 2 && p.Val == "BLOCKS") inBlocks = true;
                if (p.Code == 0 && p.Val == "ENDSEC") { inEnt = false; inBlocks = false; }
                if (!inEnt || inBlocks || p.Code != 0) continue;
                // собрать данные объекта
                int j = i + 1;
                List<Pair> ent = new List<Pair>();
                while (j < pairs.Count && pairs[j].Code != 0) { ent.Add(pairs[j]); j++; }
                string type = p.Val;
                try
                {
                    switch (type)
                    {
                        case "LINE": res.Add(ReadLine(ent)); break;
                        case "ARC": res.Add(ReadArc(ent, false)); break;
                        case "CIRCLE": res.Add(ReadArc(ent, true)); break;
                        case "LWPOLYLINE": ReadLw(ent, res); break;
                        case "POLYLINE":
                            {
                                List<Pair> verts = new List<Pair>();
                                int flags = 0; double ez = 1;
                                foreach (Pair q in ent) { if (q.Code == 70) flags = (int)D(q.Val); if (q.Code == 230) ez = D(q.Val); }
                                int k = j;
                                while (k < pairs.Count && !(pairs[k].Code == 0 && pairs[k].Val == "SEQEND"))
                                {
                                    if (pairs[k].Code == 0 && pairs[k].Val == "VERTEX") verts.Add(new Pair { Code = -1, Val = "" });
                                    else if (pairs[k].Code == 10 || pairs[k].Code == 20 || pairs[k].Code == 42) verts.Add(pairs[k]);
                                    k++;
                                }
                                // превратить в формат LWPOLYLINE
                                List<Pair> lw = new List<Pair>();
                                lw.Add(new Pair { Code = 70, Val = flags.ToString() });
                                lw.Add(new Pair { Code = 230, Val = ez.ToString(IC) });
                                foreach (Pair v in verts) if (v.Code >= 0) lw.Add(v);
                                ReadLw(lw, res);
                                j = k;
                                break;
                            }
                        case "SPLINE": ReadSpline(ent, res); break;
                        case "ELLIPSE": ReadEllipse(ent, res); break;
                        default:
                            if (type != "SEQEND" && type != "VERTEX")
                            {
                                if (!skipped.ContainsKey(type)) skipped[type] = 0;
                                skipped[type]++;
                            }
                            break;
                    }
                }
                catch (Exception ex) { log.Add("Ошибка объекта " + type + ": " + ex.Message); }
                i = j - 1;
            }
            foreach (KeyValuePair<string, int> kv in skipped)
                log.Add("Пропущено объектов " + kv.Key + ": " + kv.Value);
            if (unit != 1.0)
            {
                log.Add("Единицы чертежа пересчитаны в мм (×" + unit.ToString(IC) + ")");
                foreach (Seg s in res) s.Scale(new V2(0, 0), unit);
            }
            List<Seg> clean = new List<Seg>();
            foreach (Seg s in res) if (s != null && s.Length > 1e-9) clean.Add(s);
            return clean;
        }

        static double Get(List<Pair> e, int code, double def)
        {
            foreach (Pair p in e) if (p.Code == code) return D(p.Val);
            return def;
        }

        static Seg ReadLine(List<Pair> e)
        {
            return Seg.Line(new V2(Get(e, 10, 0), Get(e, 20, 0)), new V2(Get(e, 11, 0), Get(e, 21, 0)));
        }

        static Seg ReadArc(List<Pair> e, bool circle)
        {
            double cx = Get(e, 10, 0), cy = Get(e, 20, 0), r = Get(e, 40, 0);
            double ez = Get(e, 230, 1);
            if (circle) return Seg.Circle(new V2(ez < 0 ? -cx : cx, cy), r);
            double a0 = Get(e, 50, 0) * Math.PI / 180, a1 = Get(e, 51, 0) * Math.PI / 180;
            if (ez < 0)
            {
                // ось Z вниз (бывает у Fusion 360): зеркалим X
                cx = -cx;
                double na0 = Math.PI - a1, na1 = Math.PI - a0;
                a0 = na0; a1 = na1;
            }
            return Seg.Arc(new V2(cx, cy), r, a0, Geo.DirNorm(a1 - a0, true) == 0 ? 2 * Math.PI : Geo.DirNorm(a1 - a0, true));
        }

        static void ReadLw(List<Pair> e, List<Seg> res)
        {
            int flags = 0; double ez = 1;
            List<V2> pts = new List<V2>(); List<double> bul = new List<double>();
            double x = 0; bool haveX = false;
            foreach (Pair p in e)
            {
                if (p.Code == 70) flags = (int)D(p.Val);
                else if (p.Code == 230) ez = D(p.Val);
                else if (p.Code == 10) { x = D(p.Val); haveX = true; }
                else if (p.Code == 20 && haveX) { pts.Add(new V2(x, D(p.Val))); bul.Add(0); haveX = false; }
                else if (p.Code == 42 && bul.Count > 0) bul[bul.Count - 1] = D(p.Val);
            }
            if (ez < 0)
                for (int i = 0; i < pts.Count; i++) { pts[i] = new V2(-pts[i].X, pts[i].Y); bul[i] = -bul[i]; }
            bool closed = (flags & 1) != 0;
            int n = pts.Count;
            for (int i = 0; i < (closed ? n : n - 1); i++)
            {
                V2 a = pts[i], b = pts[(i + 1) % n];
                if (V2.Dist(a, b) < 1e-9) continue;
                res.Add(Seg.FromBulge(a, b, bul[i]));
            }
        }

        // Сплайн аппроксимируется короткими отрезками (точность ~0.002 мм)
        static void ReadSpline(List<Pair> e, List<Seg> res)
        {
            int deg = (int)Get(e, 71, 3);
            List<double> knots = new List<double>(), w = new List<double>();
            List<V2> cp = new List<V2>(), fit = new List<V2>();
            double x = 0; bool hx = false; double fx = 0; bool hfx = false;
            foreach (Pair p in e)
            {
                if (p.Code == 40) knots.Add(D(p.Val));
                else if (p.Code == 41) w.Add(D(p.Val));
                else if (p.Code == 10) { x = D(p.Val); hx = true; }
                else if (p.Code == 20 && hx) { cp.Add(new V2(x, D(p.Val))); hx = false; }
                else if (p.Code == 11) { fx = D(p.Val); hfx = true; }
                else if (p.Code == 21 && hfx) { fit.Add(new V2(fx, D(p.Val))); hfx = false; }
            }
            List<V2> poly = new List<V2>();
            if (cp.Count > deg && knots.Count == cp.Count + deg + 1)
            {
                double t0 = knots[deg], t1 = knots[cp.Count];
                int steps = Math.Max(50, cp.Count * 40);
                for (int i = 0; i <= steps; i++)
                {
                    double t = t0 + (t1 - t0) * i / steps;
                    poly.Add(DeBoor(deg, knots, cp, w, t));
                }
            }
            else poly = fit;
            for (int i = 1; i < poly.Count; i++)
                if (V2.Dist(poly[i - 1], poly[i]) > 1e-9) res.Add(Seg.Line(poly[i - 1], poly[i]));
        }

        static V2 DeBoor(int p, List<double> U, List<V2> P, List<double> W, double t)
        {
            int n = P.Count;
            int k = p;
            for (int i = p; i < n; i++) if (t >= U[i] && t <= U[i + 1]) { k = i; if (t < U[i + 1]) break; }
            double[] dx = new double[p + 1], dy = new double[p + 1], dw = new double[p + 1];
            for (int j = 0; j <= p; j++)
            {
                int idx = j + k - p;
                double ww = (W.Count == n) ? W[idx] : 1;
                dx[j] = P[idx].X * ww; dy[j] = P[idx].Y * ww; dw[j] = ww;
            }
            for (int r = 1; r <= p; r++)
                for (int j = p; j >= r; j--)
                {
                    double den = U[j + 1 + k - r] - U[j + k - p];
                    double a = den == 0 ? 0 : (t - U[j + k - p]) / den;
                    dx[j] = (1 - a) * dx[j - 1] + a * dx[j];
                    dy[j] = (1 - a) * dy[j - 1] + a * dy[j];
                    dw[j] = (1 - a) * dw[j - 1] + a * dw[j];
                }
            return new V2(dx[p] / dw[p], dy[p] / dw[p]);
        }

        static void ReadEllipse(List<Pair> e, List<Seg> res)
        {
            V2 c = new V2(Get(e, 10, 0), Get(e, 20, 0));
            V2 maj = new V2(Get(e, 11, 1), Get(e, 21, 0));
            double ratio = Get(e, 40, 1), p0 = Get(e, 41, 0), p1 = Get(e, 42, 2 * Math.PI);
            double ez = Get(e, 230, 1);
            if (p1 <= p0) p1 += 2 * Math.PI;
            V2 min = maj.Perp() * ratio;
            int steps = 180;
            V2 prev = new V2();
            for (int i = 0; i <= steps; i++)
            {
                double t = p0 + (p1 - p0) * i / steps;
                V2 q = c + maj * Math.Cos(t) + min * Math.Sin(t);
                if (ez < 0) q = new V2(-q.X, q.Y);
                if (i > 0) res.Add(Seg.Line(prev, q));
                prev = q;
            }
        }

        // ------------------------- запись -------------------------
        public class Layered { public Seg S; public string Layer; public int Color; }

        public static void Write(string path, List<Layered> items)
        {
            StringBuilder sb = new StringBuilder();
            Act2<int, string> w = (c, v) => { sb.Append(c).Append("\r\n").Append(v).Append("\r\n"); };
            w(0, "SECTION"); w(2, "HEADER"); w(9, "$ACADVER"); w(1, "AC1009"); w(9, "$INSUNITS"); w(70, "4"); w(0, "ENDSEC");
            w(0, "SECTION"); w(2, "ENTITIES");
            foreach (Layered it in items)
            {
                Seg s = it.S;
                if (s.Kind == SegKind.Line)
                {
                    w(0, "LINE"); w(8, it.Layer); w(62, it.Color.ToString());
                    w(10, N(s.P0.X)); w(20, N(s.P0.Y)); w(30, "0");
                    w(11, N(s.P1.X)); w(21, N(s.P1.Y)); w(31, "0");
                }
                else if (s.IsCircle)
                {
                    w(0, "CIRCLE"); w(8, it.Layer); w(62, it.Color.ToString());
                    w(10, N(s.C.X)); w(20, N(s.C.Y)); w(30, "0"); w(40, N(s.R));
                }
                else
                {
                    double a0 = s.Sw > 0 ? s.A0 : s.A0 + s.Sw;
                    double a1 = a0 + Math.Abs(s.Sw);
                    w(0, "ARC"); w(8, it.Layer); w(62, it.Color.ToString());
                    w(10, N(s.C.X)); w(20, N(s.C.Y)); w(30, "0"); w(40, N(s.R));
                    w(50, N(a0 * 180 / Math.PI)); w(51, N(a1 * 180 / Math.PI));
                }
            }
            w(0, "ENDSEC"); w(0, "EOF");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        static string N(double v) { return v.ToString("0.########", IC); }
    }
}
