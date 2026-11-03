// STZ WireCut — приём колеса из программы «STZ Расчёт зубьев» (файл .stzgear, JSON).
// STZ: Экспорт → «Шестерня → эрозия (STZ WireCut)». Здесь: Библиотека → «Из STZ» или двойной щелчок по файлу.
// Совместимо с .NET 2.0: свой маленький разборщик JSON.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WireCut
{
    /// <summary>Минимальный разборщик JSON: объект → Dictionary, массив → List, число → double, строка, bool, null.</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; object v = Val(s, ref i); return v; }
        static void Ws(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '﻿')) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("конец файла");
            char c = s[i];
            if (c == '{')
            {
                Dictionary<string, object> d = new Dictionary<string, object>(); i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i);
                    if (s[i] != ':') throw new FormatException("ожидалось «:»"); i++;
                    d[k] = Val(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("ожидалось «,» или «}»");
                }
            }
            if (c == '[')
            {
                List<object> l = new List<object>(); i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Val(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("ожидалось «,» или «]»");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) throw new FormatException("неизвестный символ «" + c + "»");
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("ожидалась строка"); i++;
            StringBuilder b = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { b.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'u': b.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: b.Append(e); break;
                }
            }
            i++; return b.ToString();
        }
    }

    /// <summary>Колесо из «STZ Расчёт зубьев».</summary>
    public class StzGear
    {
        public string Name = "", From = "", Note = "", Mat = "", Acc = "";
        public bool Internal;
        public double M, X, Alpha = 20, Ha = 1, Cs = 0.25, D, Da, Df, Db, W, B, Bore;
        public int Z, Zw, Mate;
        public bool HasKey; public double KeyB, KeyH, KeyT2;

        static double N(Dictionary<string, object> d, string k, double def)
        {
            object v; if (d.TryGetValue(k, out v) && v is double) return (double)v;
            return def;
        }
        static string S(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v is string ? (string)v : "";
        }

        public static StzGear Load(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            Dictionary<string, object> d = MiniJson.Parse(text) as Dictionary<string, object>;
            if (d == null) throw new FormatException("это не файл колеса STZ");
            string fmt = S(d, "format");
            if (fmt != "STZ-WireCut-gear") throw new FormatException(fmt == "" ? "это не файл колеса STZ (нет поля format)" : "неизвестный формат «" + fmt + "»");
            StzGear g = new StzGear();
            g.Name = S(d, "name"); g.From = S(d, "from"); g.Note = S(d, "note"); g.Mat = S(d, "mat"); g.Acc = S(d, "acc");
            object iv; g.Internal = d.TryGetValue("internal", out iv) && iv is bool && (bool)iv;
            g.M = N(d, "m", 0); g.Z = (int)Math.Round(N(d, "z", 0)); g.X = N(d, "x", 0);
            g.Alpha = N(d, "alpha", 20); g.Ha = N(d, "ha", 1); g.Cs = N(d, "cs", 0.25);
            g.D = N(d, "d", 0); g.Da = N(d, "da", 0); g.Df = N(d, "df", 0); g.Db = N(d, "db", 0);
            g.W = N(d, "W", 0); g.Zw = (int)Math.Round(N(d, "zw", 0)); g.Mate = (int)Math.Round(N(d, "mate", 0));
            g.B = N(d, "b", 0); g.Bore = N(d, "bore", 0);
            object kv;
            if (d.TryGetValue("key", out kv) && kv is Dictionary<string, object>)
            {
                Dictionary<string, object> k = (Dictionary<string, object>)kv;
                g.HasKey = true; g.KeyB = N(k, "b", 0); g.KeyH = N(k, "h", 0); g.KeyT2 = N(k, "t2", 0);
            }
            if (g.M <= 0 || g.Z < 6) throw new FormatException("в файле нет модуля или числа зубьев");
            return g;
        }

        /// <summary>Перенести в параметры окна «Шестерня».</summary>
        public void ApplyTo(GearParams p)
        {
            p.M = M; p.Z = Z; p.X = Internal ? 0 : X; p.Alpha = Alpha; p.Ha = Ha; p.Cc = Cs;
            p.Internal = Internal;
            p.Bore = Bore;
            p.Key = !Internal && HasKey && Bore > 0;
            p.Cx = 0; p.Cy = 0;
            p.DaSet = Da > 0 ? Da : 0;      // Ø вершин из STZ (с уравнительным смещением пары)
            p.Source = "из STZ: " + Name;
        }
    }

    public partial class MainForm
    {
        /// <summary>Открыть файл колеса из STZ и показать окно «Шестерня» с заполненными размерами.</summary>
        public void ImportStz(string path)
        {
            StzGear g;
            try { g = StzGear.Load(path); }
            catch (Exception ex) { Msg("Ошибка: файл колеса STZ не прочитан — " + ex.Message); return; }
            Msg("Колесо из STZ: " + g.Name + "  (" + Path.GetFileName(path) + (g.From != "" ? ", " + g.From : "") + ")");
            if (g.Note != "") Msg("  " + g.Note);
            g.ApplyTo(gearPrm);
            // сверка: то же колесо, посчитанное WireCut, без утонения
            double thin = gearPrm.Thin; gearPrm.Thin = 0;
            double da = gearPrm.Da, df = gearPrm.Df, w = gearPrm.W; int k = gearPrm.Kw;
            gearPrm.Thin = thin;
            List<string> diff = new List<string>();
            if (g.Df > 0 && Math.Abs(df - g.Df) > 0.01) diff.Add("df: STZ " + Geo.F(g.Df) + ", WireCut " + Geo.F(df));
            if (!g.Internal && g.W > 0 && (k != g.Zw || Math.Abs(w - g.W) > 0.005)) diff.Add("W: STZ " + Geo.F(Math.Round(g.W, 3)) + " (k " + g.Zw + "), WireCut " + Geo.F(w) + " (k " + k + ")");
            if (diff.Count == 0) Msg("  Сверка с расчётом WireCut: da, df" + (g.Internal ? "" : ", W") + " совпадают." + (gearPrm.Thin > 0 && !g.Internal ? " С утонением " + Geo.F(gearPrm.Thin) + " мм W = " + Geo.F(gearPrm.W) + " (поправьте утонение по чертежу)." : ""));
            else Msg("Внимание: расчёт STZ и WireCut различается — " + string.Join("; ", diff.ToArray()));
            // толщина заготовки = ширина венца, материал — если такой есть в таблице
            if (g.B > 0) { S.Thickness = g.B; Msg("  Толщина = ширина венца b " + Geo.F(g.B) + " мм (проверьте по заготовке)."); }
            if (g.Mat.IndexOf("40Х") >= 0) S.Material = "40Х";
            else if (g.Mat.IndexOf("45") >= 0 && g.Mat.IndexOf("Сталь") >= 0) S.Material = "Сталь 45";
            Presets.Apply(S); grid.Refresh();
            opName = "Шестерня из STZ"; opCmd = "ИЗSTZ";
            Library(0, "Шестерня из STZ Расчёт зубьев — проверьте и вставьте");
            Checkpoint();   // шаг истории «Шестерня из STZ»
        }

        void OpenStz()
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Колесо из STZ (*.stzgear)|*.stzgear|Все файлы|*.*", Title = "Открыть колесо из «STZ Расчёт зубьев»" })
                if (d.ShowDialog(this) == DialogResult.OK) ImportStz(d.FileName);
        }

        public static bool IsStzFile(string path) { return path != null && path.ToLowerInvariant().EndsWith(".stzgear"); }

        // ---- открытие файла, когда программа уже запущена (двойной щелчок по .stzgear) ----
        public static string OpenRequestFile { get { return Path.Combine(AppDir, "open_request.txt"); } }
        FileSystemWatcher reqWatch; Timer reqTimer;
        void StartOpenRequestWatch()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                reqTimer = new Timer { Interval = 400 };
                reqTimer.Tick += delegate
                {
                    reqTimer.Stop();
                    string f;
                    try { if (!File.Exists(OpenRequestFile)) return; f = File.ReadAllText(OpenRequestFile, Encoding.UTF8).Trim(); File.Delete(OpenRequestFile); }
                    catch { return; }
                    if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Maximized;
                    Activate();
                    if (IsStzFile(f)) ImportStz(f); else if (File.Exists(f)) ImportDrawing(f, true);
                };
                reqWatch = new FileSystemWatcher(AppDir, "open_request.txt");
                reqWatch.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                FileSystemEventHandler h = delegate { BeginInvoke((MethodInvoker)delegate { reqTimer.Stop(); reqTimer.Start(); }); };
                reqWatch.Changed += h; reqWatch.Created += h;
                reqWatch.EnableRaisingEvents = true;
            }
            catch { }
        }
    }
}
