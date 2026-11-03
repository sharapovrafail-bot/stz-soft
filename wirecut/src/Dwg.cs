// STZ WireCut — открытие чертежей DWG (AutoCAD): DWG → DXF внешним конвертером, затем обычное чтение DXF.
// 1) ODA File Converter, если установлен (бесплатный, лучше всего читает новые DWG);
// 2) встроенный LibreDWG (dwg2dxf.exe в папке программы \dwg, GNU GPL v3, https://www.gnu.org/software/libredwg/).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WireCut
{
    public static class DwgConv
    {
        public static bool IsDwg(string path) { return path != null && path.ToLowerInvariant().EndsWith(".dwg"); }

        /// <summary>Встроенный LibreDWG: папка dwg рядом с программой.</summary>
        public static string LibreDwg
        {
            get
            {
                string p = Path.Combine(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "dwg"), "dwg2dxf.exe");
                return File.Exists(p) ? p : null;
            }
        }

        /// <summary>ODA File Converter (Program Files\ODA\ODAFileConverter x.y\ODAFileConverter.exe).</summary>
        public static string Oda
        {
            get
            {
                List<string> roots = new List<string>();
                foreach (string v in new string[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" })
                {
                    string r = Environment.GetEnvironmentVariable(v);
                    if (!string.IsNullOrEmpty(r) && !roots.Contains(r)) roots.Add(r);
                }
                foreach (string r in roots)
                {
                    try
                    {
                        string oda = Path.Combine(r, "ODA");
                        if (!Directory.Exists(oda)) continue;
                        string[] dirs = Directory.GetDirectories(oda, "ODAFileConverter*");
                        Array.Sort(dirs); Array.Reverse(dirs);              // самая новая версия первой
                        foreach (string d in dirs) { string exe = Path.Combine(d, "ODAFileConverter.exe"); if (File.Exists(exe)) return exe; }
                    }
                    catch { }
                }
                return null;
            }
        }

        /// <summary>Рабочая папка с латинским путём (конвертеры не всегда понимают кириллицу в путях).</summary>
        static string WorkDir()
        {
            string b = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string d = Path.Combine(Path.Combine(b, "STZ_WireCut"), "dwg_" + Process.GetCurrentProcess().Id);
            try { if (Directory.Exists(d)) Directory.Delete(d, true); Directory.CreateDirectory(d); }
            catch
            {
                d = Path.Combine(Path.GetTempPath(), "stzdwg_" + Process.GetCurrentProcess().Id);
                if (Directory.Exists(d)) Directory.Delete(d, true);
                Directory.CreateDirectory(d);
            }
            return d;
        }

        static int Run(string exe, string args, string dir, int ms, StringBuilder log)
        {
            ProcessStartInfo si = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir
            };
            using (Process p = Process.Start(si))
            {
                // читать вывод в фоне, чтобы процесс не завис на полном буфере
                StringBuilder o = new StringBuilder();
                p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (o) o.AppendLine(e.Data); };
                p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (o) o.AppendLine(e.Data); };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                if (!p.WaitForExit(ms)) { try { p.Kill(); } catch { } log.AppendLine("превышено время " + ms / 1000 + " с"); return -1; }
                p.WaitForExit();
                lock (o) log.Append(o.ToString());
                return p.ExitCode;
            }
        }

        /// <summary>DWG → DXF. Возвращает путь к DXF или null; how — каким конвертером, err — почему не вышло.</summary>
        public static string ToDxf(string dwg, out string how, out string err)
        {
            how = null; err = null;
            string oda = Oda, lib = LibreDwg;
            if (oda == null && lib == null)
            {
                err = "нет конвертера DWG: переустановите STZ WireCut (папка dwg) или поставьте бесплатный ODA File Converter";
                return null;
            }
            if (!File.Exists(dwg)) { err = "файл не найден: " + dwg; return null; }
            string dir = WorkDir();
            string ok = null;
            try { ok = Convert(dwg, dir, oda, lib, out how, out err); return ok; }
            finally { if (ok == null) try { Directory.Delete(dir, true); } catch { } }
        }

        static string Convert(string dwg, string dir, string oda, string lib, out string how, out string err)
        {
            how = null; err = null;
            string inDir = Path.Combine(dir, "in"), outDir = Path.Combine(dir, "out");
            Directory.CreateDirectory(inDir); Directory.CreateDirectory(outDir);
            string src = Path.Combine(inDir, "drawing.dwg");
            File.Copy(dwg, src, true);
            List<string> errs = new List<string>();

            if (oda != null)
            {
                StringBuilder log = new StringBuilder();
                try
                {
                    // ODAFileConverter "вход" "выход" версия тип рекурсия аудит "фильтр"
                    int rc = Run(oda, "\"" + inDir + "\" \"" + outDir + "\" ACAD2000 DXF 0 1 \"*.DWG\"", dir, 120000, log);
                    string res = Path.Combine(outDir, "drawing.dxf");
                    if (File.Exists(res) && new FileInfo(res).Length > 0) { how = "ODA File Converter"; return res; }
                    errs.Add("ODA File Converter: код " + rc + " " + Short(log.ToString()));
                }
                catch (Exception ex) { errs.Add("ODA File Converter: " + ex.Message); }
            }
            if (lib != null)
            {
                StringBuilder log = new StringBuilder();
                try
                {
                    string res = Path.Combine(outDir, "drawing_l.dxf");
                    int rc = Run(lib, "-y --as r2000 -o \"" + res + "\" \"" + src + "\"", Path.GetDirectoryName(lib), 120000, log);
                    if (File.Exists(res) && new FileInfo(res).Length > 0) { how = "LibreDWG"; return res; }
                    errs.Add("LibreDWG: код " + rc + " " + Short(log.ToString()));
                }
                catch (Exception ex) { errs.Add("LibreDWG: " + ex.Message); }
            }
            err = string.Join("; ", errs.ToArray());
            return null;
        }

        static string Short(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > 300 ? s.Substring(s.Length - 300) : s;
        }

        /// <summary>Удалить временные файлы конвертации.</summary>
        public static void Clean(string dxf)
        {
            try { string d = Path.GetDirectoryName(Path.GetDirectoryName(dxf)); if (d.IndexOf("dwg") >= 0) Directory.Delete(d, true); } catch { }
        }
    }

    public partial class MainForm
    {
        /// <summary>Открыть чертёж: DXF напрямую, DWG — через конвертер.</summary>
        public void ImportDrawing(string path, bool replace)
        {
            if (!DwgConv.IsDwg(path)) { ImportDxf(path, replace); return; }
            Msg("DWG: " + Path.GetFileName(path) + " — перевожу в DXF…");
            Cursor = Cursors.WaitCursor; Application.DoEvents();
            string how, err, dxf;
            try { dxf = DwgConv.ToDxf(path, out how, out err); }
            catch (Exception ex) { dxf = null; how = null; err = ex.Message; }
            finally { Cursor = Cursors.Default; }
            if (dxf == null)
            {
                Msg("Ошибка: DWG не открыт — " + err);
                Msg("  Что сделать: в AutoCAD / Fusion 360 «Сохранить как» → DXF и открыть его; или поставить бесплатный ODA File Converter (opendesign.com) — программа найдёт его сама.");
                return;
            }
            ImportDxf(dxf, replace, path);
            Msg("  DWG переведён в DXF (" + how + "). Проверьте размеры: «Проверить» и габарит (F12).");
            DwgConv.Clean(dxf);
            if (Active == null) { Checkpoint(); }   // шаг истории «DWG …», если открыто не через команду
        }
    }
}
