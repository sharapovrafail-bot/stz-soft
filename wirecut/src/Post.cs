// STZ WireCut — постпроцессоры: ISO (G-код) и 3B (HL / HF / AutoCut), карта наладки
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WireCut
{
    public class OutFile
    {
        public string Name;   // имя файла
        public string Text;
        public bool Cyr;      // содержит кириллицу (сохранять в cp1251)
    }

    public static class Post
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        public static List<OutFile> Make(Toolpath tp, JobSettings s, string baseName)
        {
            List<OutFile> r = new List<OutFile>();
            if (s.Format != OutFormat.B3) r.Add(new OutFile { Name = baseName + s.IsoExt, Text = Iso(tp, s), Cyr = s.Comments == CommentMode.Rus });
            if (s.Format != OutFormat.Iso)
            {
                List<string> parts = B3(tp, s);
                for (int i = 0; i < parts.Count; i++)
                    r.Add(new OutFile { Name = baseName + (parts.Count > 1 ? "_" + (i + 1) : "") + s.B3Ext, Text = parts[i] });
            }
            r.Add(new OutFile { Name = baseName + "_карта.txt", Text = Card(tp, s), Cyr = true });
            return r;
        }

        // ---------------- ISO ----------------
        static string X(double v)
        {
            v = Math.Round(v, 3);
            if (Math.Abs(v) < 0.0005) v = 0;
            string t = v.ToString("0.###", IC);
            if (t.IndexOf('.') < 0) t += ".";
            return t;
        }

        public static string Iso(Toolpath tp, JobSettings s)
        {
            StringBuilder sb = new StringBuilder();
            int n = 0;
            V2 o = s.Zero == ZeroMode.Start ? tp.Origin : new V2(0, 0);
            Act2<string, string> blk = (code, cmt) =>
            {
                n += s.NStep;
                sb.Append('N').Append(n).Append(' ').Append(code);
                if (!string.IsNullOrEmpty(cmt) && s.Comments != CommentMode.None) sb.Append(" (").Append(C(cmt, s)).Append(')');
                sb.Append("\r\n");
            };
            Act2<string, string> line = (txt, dummy) =>
            {
                if (s.Comments == CommentMode.None) return;
                sb.Append('(').Append(C(txt, s)).Append(")\r\n");
            };
            sb.Append("%\r\n");
            sb.Append('O').Append(s.ProgNo.ToString("0000"));
            if (s.Comments != CommentMode.None) sb.Append(" (").Append(C(s.PartName, s)).Append(')');
            sb.Append("\r\n");
            line("ДЕТАЛЬ: " + s.PartName + (s.DrawingNo != "" ? "  ЧЕРТ. " + s.DrawingNo : ""), null);
            line("МАТЕРИАЛ: " + s.Material + "  ТОЛЩИНА " + Geo.F(s.Thickness) + " ММ", null);
            line("СТАНОК: " + RusEnumConverter.Text(s.Profile), null);
            line("ПРОВОЛОКА D" + Geo.F(s.WireD) + "  " + RusEnumConverter.Text(s.Machine), null);
            foreach (PassParam p in s.ActivePasses())
            {
                int pn = Array.IndexOf(s.Passes, p) + 1;
                string d = s.Comp == CompMode.Cnc ? "  D" + pn + "=" + Geo.F(s.OffsetOf(p)) : "  СМЕЩ " + Geo.F(s.OffsetOf(p));
                line("ПРОХОД " + pn + " " + p.Name.ToUpper() + d + "  TON" + Geo.F(p.Ton) + " TOFF" + Geo.F(p.Toff) + " IP" + Geo.F(p.Ip), null);
            }
            line("ДЛИНА РЕЗА " + Geo.F(Math.Round(tp.CutLen, 1)) + " ММ  ВРЕМЯ ~" + TimeStr(tp.Minutes), null);
            line("ТОЧКА ЗАПРАВКИ (ЧЕРТ.): X" + X(tp.Origin.X) + " Y" + X(tp.Origin.Y), null);

            foreach (string extra in s.StartLines.Split(';'))
                if (extra.Trim().Length > 0) blk(extra.Trim(), null);
            V2 st = tp.Origin - o;
            blk("G90 G92 X" + X(st.X) + " Y" + X(st.Y), s.Zero == ZeroMode.Start ? "НОЛЬ = ТОЧКА ЗАПРАВКИ" : "ПРОВОЛОКА В ТОЧКЕ ЗАПРАВКИ");

            int compOn = 0;
            foreach (Move m in tp.Moves)
            {
                switch (m.K)
                {
                    case MoveKind.Comment: line(m.Text, null); break;
                    case MoveKind.Regime:
                        {
                            string t = "ПРОХОД " + m.Pass + ": " + m.P.Name.ToUpper() + ", СМЕЩ " + Geo.F(s.OffsetOf(m.P));
                            if (!string.IsNullOrEmpty(s.RegimeTemplate)) blk(string.Format(s.RegimeTemplate, m.P.RegNo), t);
                            else line(t, null);
                            break;
                        }
                    case MoveKind.Stop:
                        {
                            string code = m.Comp == -1 ? s.CutWireCode : m.Comp == -2 ? s.ThreadCode : s.PauseCode;
                            blk(code, m.Text); break;
                        }
                    case MoveKind.Rapid:
                        {
                            V2 e = m.S.End - o;
                            blk("G00 X" + X(e.X) + " Y" + X(e.Y), m.Text); break;
                        }
                    case MoveKind.End: blk(s.EndCode, m.Text); break;
                    case MoveKind.Cut:
                        {
                            Seg g = m.S;
                            if (g.Length < 0.0005) break;
                            string pre = "";
                            if (m.Comp == 41 || m.Comp == 42) { pre = "G" + m.Comp + " D" + m.Pass + " "; compOn = m.Pass; }
                            else if (m.Comp == 40) { pre = "G40 "; compOn = 0; }
                            else if (s.Comp == CompMode.Cnc && compOn != 0 && compOn != m.Pass)
                            {
                                pre = "D" + m.Pass + " "; compOn = m.Pass;
                            }
                            V2 e = g.End - o;
                            if (g.Kind == SegKind.Line)
                                blk(pre + "G01 X" + X(e.X) + " Y" + X(e.Y), m.Text);
                            else
                            {
                                if (g.IsCircle)
                                {
                                    // полная окружность — двумя половинами
                                    Seg a, b; g.SplitAt(0.5, out a, out b);
                                    ArcBlock(blk, pre, a, o, m.Text);
                                    ArcBlock(blk, "", b, o, null);
                                }
                                else ArcBlock(blk, pre, g, o, m.Text);
                            }
                            break;
                        }
                }
            }
            sb.Append("%\r\n");
            return sb.ToString();
        }

        static void ArcBlock(Act2<string, string> blk, string pre, Seg g, V2 o, string cmt)
        {
            V2 e = g.End - o; V2 ij = g.C - g.Start;
            blk(pre + (g.Sw < 0 ? "G02" : "G03") + " X" + X(e.X) + " Y" + X(e.Y) + " I" + X(ij.X) + " J" + X(ij.Y), cmt);
        }

        // ---------------- 3B ----------------
        // Формат: B x B y B J G Z  (мкм). Для дуги x,y — начало относительно центра.
        public static List<string> B3(Toolpath tp, JobSettings s)
        {
            List<string> files = new List<string>();
            StringBuilder sb = new StringBuilder();
            long cx = 0, cy = 0; // текущая точка, мкм (абсолютно, от точки заправки контура)
            V2 org = tp.Origin; bool started = false;
            foreach (Move m in tp.Moves)
            {
                if (m.K == MoveKind.Rapid)
                {
                    // новый файл для следующего контура
                    if (sb.Length > 0) { sb.Append("DD\r\n"); files.Add(sb.ToString()); sb = new StringBuilder(); }
                    org = m.S.End; cx = 0; cy = 0; started = false;
                    continue;
                }
                if (m.K == MoveKind.Stop && m.Comp == 0 && started) { sb.Append("D\r\n"); continue; }
                if (m.K != MoveKind.Cut) continue;
                started = true;
                Seg g = m.S;
                if (g.Kind == SegKind.Line)
                {
                    long ex = U(g.End.X - org.X), ey = U(g.End.Y - org.Y);
                    long dx = ex - cx, dy = ey - cy;
                    if (dx == 0 && dy == 0) continue;
                    long ax = Math.Abs(dx), ay = Math.Abs(dy);
                    string G = ax >= ay ? "GX" : "GY";
                    long J = ax >= ay ? ax : ay;
                    string z;
                    if (dx > 0 && dy >= 0) z = "L1"; else if (dx <= 0 && dy > 0) z = "L2"; else if (dx < 0 && dy <= 0) z = "L3"; else z = "L4";
                    sb.Append("B").Append(ax).Append("B").Append(ay).Append("B").Append(J).Append(G).Append(z).Append("\r\n");
                    cx = ex; cy = ey;
                }
                else
                {
                    List<Seg> parts = new List<Seg>();
                    if (Math.Abs(g.Sw) > Math.PI * 1.5) { Seg a, b; g.SplitAt(0.5, out a, out b); parts.Add(a); parts.Add(b); }
                    else parts.Add(g);
                    foreach (Seg q in parts)
                    {
                        long ccx = U(q.C.X - org.X), ccy = U(q.C.Y - org.Y);
                        long ex = U(q.End.X - org.X), ey = U(q.End.Y - org.Y);
                        long xs = cx - ccx, ys = cy - ccy, xe = ex - ccx, ye = ey - ccy;
                        bool ccw = q.Sw > 0;
                        double a0 = Math.Atan2(ys, xs), a1 = Math.Atan2(ye, xe);
                        double sw = Geo.DirNorm(a1 - a0, ccw);
                        if (Math.Abs(q.Sw) < Math.PI && Math.Abs(sw) > 1.5 * Math.PI) sw = 0;
                        double R = Math.Sqrt((double)xs * xs + (double)ys * ys);
                        bool countX = Math.Abs(xe) <= Math.Abs(ye); // конец ближе к оси Y → счёт по X
                        double J = 0;
                        // точки разбиения на границах четвертей
                        double prevX = xs, prevY = ys;
                        double step = Math.PI / 2;
                        double ang = a0;
                        double rem = Math.Abs(sw);
                        while (rem > 1e-12)
                        {
                            double next;
                            if (ccw) next = (Math.Floor(ang / step + 1e-9) + 1) * step;
                            else next = (Math.Ceiling(ang / step - 1e-9) - 1) * step;
                            double d = Math.Abs(next - ang);
                            if (d >= rem - 1e-12)
                            {
                                J += countX ? Math.Abs(xe - prevX) : Math.Abs(ye - prevY);
                                break;
                            }
                            double nx = R * Math.Cos(next), ny = R * Math.Sin(next);
                            J += countX ? Math.Abs(nx - prevX) : Math.Abs(ny - prevY);
                            prevX = nx; prevY = ny; ang = next; rem -= d;
                        }
                        string z = ccw ? "NR" + QuadN(xs, ys) : "SR" + QuadS(xs, ys);
                        sb.Append("B").Append(Math.Abs(xs)).Append("B").Append(Math.Abs(ys)).Append("B").Append((long)Math.Round(J))
                          .Append(countX ? "GX" : "GY").Append(z).Append("\r\n");
                        cx = ex; cy = ey;
                    }
                }
            }
            if (sb.Length > 0) { sb.Append("DD\r\n"); files.Add(sb.ToString()); }
            return files;
        }

        static long U(double mm) { return (long)Math.Round(mm * 1000.0); }
        // четверть начала дуги против часовой: +X → 1, +Y → 2, −X → 3, −Y → 4
        static int QuadN(long x, long y) { if (x > 0 && y >= 0) return 1; if (x <= 0 && y > 0) return 2; if (x < 0 && y <= 0) return 3; return 4; }
        // по часовой: +Y → 1, −X → 2, −Y → 3, +X → 4
        static int QuadS(long x, long y) { if (x >= 0 && y > 0) return 1; if (x < 0 && y >= 0) return 2; if (x <= 0 && y < 0) return 3; return 4; }

        // ---------------- карта наладки ----------------
        public static string Card(Toolpath tp, JobSettings s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("КАРТА НАЛАДКИ — ЭЛЕКТРОЭРОЗИОННАЯ ВЫРЕЗКА\r\n");
            sb.Append("STZ WireCut, ").Append(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).Append("\r\n");
            sb.Append("==========================================================\r\n");
            sb.Append("Деталь:      ").Append(s.PartName).Append(s.DrawingNo != "" ? "   черт. " + s.DrawingNo : "").Append("\r\n");
            sb.Append("Материал:    ").Append(s.Material).Append(",  толщина ").Append(Geo.F(s.Thickness)).Append(" мм\r\n");
            sb.Append("Тип:         ").Append(RusEnumConverter.Text(s.Kind)).Append(",  обход ").Append(RusEnumConverter.Text(s.Dir).ToLower()).Append("\r\n");
            sb.Append("Профиль:     ").Append(RusEnumConverter.Text(s.Profile)).Append(",  ход стола ").Append(Geo.F(s.TravelX)).Append("×").Append(Geo.F(s.TravelY)).Append(" мм\r\n");
            sb.Append("Габарит:     ").Append(Geo.F(Math.Round(tp.SizeX, 2))).Append(" × ").Append(Geo.F(Math.Round(tp.SizeY, 2))).Append(" мм (по траектории)\r\n");
            sb.Append("Станок:      ").Append(RusEnumConverter.Text(s.Machine)).Append(",  проволока D").Append(Geo.F(s.WireD)).Append(" мм\r\n");
            sb.Append("Коррекция:   ").Append(RusEnumConverter.Text(s.Comp)).Append(s.SizeCorr != 0 ? ",  сдвиг в допуск " + Geo.F(s.SizeCorr) + " мм" : "").Append("\r\n");
            if (s.Tab > 0) sb.Append("Перемычка:   ").Append(Geo.F(s.Tab)).Append(" мм (режется последней, черновым режимом — затем зачистить)\r\n");
            sb.Append("\r\nРЕЖИМ ДЛЯ AutoCut: импульс (Pulse width) = Ton, интервал (Pulse interval) = Toff/Ton, трубки, V-F\r\n");
            sb.Append("ПРОХОДЫ (из таблицы режимов «Эрозия → Режимы резания»):\r\n");
            sb.Append(" №  Название       Смещение  Зазор   Припуск  Ton   Toff  Тр. Ip,А  U,В  V-F  мм²/мин\r\n");
            foreach (PassParam p in s.ActivePasses())
            {
                int pn = Array.IndexOf(s.Passes, p) + 1;
                sb.Append(' ').Append(pn).Append("  ").Append(p.Name.PadRight(14)).Append(' ')
                  .Append(Geo.F(s.OffsetOf(p)).PadRight(9)).Append(' ').Append(Geo.F(p.Gap).PadRight(7)).Append(' ')
                  .Append(Geo.F(p.Stock).PadRight(8)).Append(' ').Append(Geo.F(p.Ton).PadRight(5)).Append(' ')
                  .Append(Geo.F(p.Toff).PadRight(5)).Append(' ').Append(p.Tubes.ToString().PadRight(3)).Append(' ').Append(Geo.F(p.Ip).PadRight(5)).Append(' ')
                  .Append(Geo.F(p.U).PadRight(4)).Append(' ').Append(p.VF.ToString().PadRight(4)).Append(' ').Append(Geo.F(p.Rate)).Append("\r\n");
            }
            sb.Append("\r\nТОЧКИ ЗАПРАВКИ (координаты чертежа, мм):\r\n");
            for (int i = 0; i < tp.Starts.Count; i++)
            {
                V2 d = tp.Starts[i] - tp.Origin;
                sb.Append(" Контур ").Append(i + 1).Append(":  X ").Append(Geo.F(tp.Starts[i].X)).Append("  Y ").Append(Geo.F(tp.Starts[i].Y));
                if (i > 0) sb.Append("    (от 1-й точки: dX ").Append(Geo.F(d.X)).Append("  dY ").Append(Geo.F(d.Y)).Append(')');
                sb.Append("\r\n");
            }
            if (tp.Starts.Count > 1 && s.Format != OutFormat.Iso)
                sb.Append(" Для 3B каждый контур — отдельный файл (_1, _2 …): после контура отрезать проволоку,\r\n переехать на dX/dY, заправить и запустить следующий файл.\r\n");
            sb.Append("\r\nДлина реза:  ").Append(Geo.F(Math.Round(tp.CutLen, 1))).Append(" мм\r\n");
            sb.Append("Время ~      ").Append(TimeStr(tp.Minutes)).Append(" (оценка по производительности режимов)\r\n");
            sb.Append("Стоимость:   ").Append(Math.Round(tp.Cost(s))).Append(" руб  (").Append(Math.Max(1, s.Qty)).Append(" шт, наладка ").Append(Geo.F(s.SetupMin))
              .Append(" мин, ").Append(Geo.F(s.PriceHour)).Append(" руб/ч").Append(s.MaterialCost > 0 ? ", материал " + Geo.F(s.MaterialCost) + " руб" : "").Append(")\r\n");
            if (tp.Warn.Count > 0)
            {
                sb.Append("\r\nВНИМАНИЕ:\r\n");
                foreach (string w in tp.Warn) sb.Append(" - ").Append(w).Append("\r\n");
            }
            sb.Append("\r\nПеред резкой: выверить проволоку по вертикали, проверить натяжение и подачу жидкости,\r\nпрогнать УП в симуляции и «по воздуху».\r\n");
            return sb.ToString();
        }

        public static string TimeStr(double min)
        {
            int m = (int)Math.Round(min);
            return (m / 60) + " ч " + (m % 60).ToString("00") + " мин";
        }

        // ---------------- комментарии ----------------
        static string C(string t, JobSettings s)
        {
            if (t == null) return "";
            t = t.ToUpper().Replace("(", "[").Replace(")", "]");
            if (s.Comments == CommentMode.Lat) t = Translit(t);
            return t;
        }

        static readonly string[] TR = {
            "А","A","Б","B","В","V","Г","G","Д","D","Е","E","Ё","E","Ж","ZH","З","Z","И","I","Й","Y","К","K","Л","L","М","M",
            "Н","N","О","O","П","P","Р","R","С","S","Т","T","У","U","Ф","F","Х","KH","Ц","TS","Ч","CH","Ш","SH","Щ","SCH",
            "Ъ","","Ы","Y","Ь","","Э","E","Ю","YU","Я","YA","²","2","«","\"","»","\"","—","-","°","DEG" };

        public static string Translit(string t)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in t)
            {
                string c = ch.ToString(); bool found = false;
                for (int i = 0; i < TR.Length; i += 2) if (TR[i] == c) { sb.Append(TR[i + 1]); found = true; break; }
                if (!found) sb.Append(ch < 128 ? c : "?");
            }
            return sb.ToString();
        }
    }
}
