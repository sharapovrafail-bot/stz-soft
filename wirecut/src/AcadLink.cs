// STZ WireCut — связь с AutoCAD: LISP-команда WCUT и автоприём контуров
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WireCut
{
    public static class AcadLink
    {
        public static string ExchangeFile { get { return Path.Combine(MainForm.AppDir, "acad_exchange.dxf"); } }
        public static string LispFile { get { return Path.Combine(MainForm.AppDir, "STZ_WIRECUT.lsp"); } }

        static FileSystemWatcher fsw;
        static Timer debounce;
        static MainForm form;

        /// <summary>Следить за файлом обмена: когда AutoCAD записал контуры — загрузить их.</summary>
        public static void StartWatch(MainForm f)
        {
            form = f;
            try
            {
                Directory.CreateDirectory(MainForm.AppDir);
                debounce = new Timer { Interval = 700 };
                debounce.Tick += delegate
                {
                    debounce.Stop();
                    if (File.Exists(ExchangeFile))
                    {
                        form.Msg("Получены контуры из AutoCAD.");
                        form.ImportDxf(ExchangeFile, true);
                        form.Activate();
                    }
                };
                fsw = new FileSystemWatcher(MainForm.AppDir, "acad_exchange.dxf");
                fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                FileSystemEventHandler h = delegate { form.BeginInvoke((MethodInvoker)delegate { debounce.Stop(); debounce.Start(); }); };
                fsw.Changed += h; fsw.Created += h;
                fsw.EnableRaisingEvents = true;
            }
            catch { }
        }

        static string Esc(string p) { return p.Replace("\\", "\\\\"); }

        public static void Install(MainForm f)
        {
            Directory.CreateDirectory(MainForm.AppDir);
            string exe = Application.ExecutablePath;
            StringBuilder L = new StringBuilder();
            L.AppendLine(";;; STZ WireCut — передача контуров из AutoCAD в программу эрозии");
            L.AppendLine(";;; Команда WCUT (или EROZIA): выбрать контуры -> программа STZ WireCut откроет их сама.");
            L.AppendLine(";;; Сделано специально по заказу ООО «СпецТехЗаказ»");
            L.AppendLine("(vl-load-com)");
            L.AppendLine("(defun stzwc:n (x) (rtos x 2 8))");
            L.AppendLine("(defun stzwc:w (fh c v) (write-line (itoa c) fh) (write-line v fh))");
            L.AppendLine("(defun stzwc:ez (ed / n) (setq n (assoc 210 ed)) (if n (stzwc:n (cadddr n)) \"1\"))");
            L.AppendLine("(defun c:WCUT (/ ss i e ed typ dir f fh cnt bad)");
            L.AppendLine("  (setq dir (strcat (getenv \"APPDATA\") \"\\\\STZ_WireCut\"))");
            L.AppendLine("  (vl-mkdir dir)");
            L.AppendLine("  (setq f (strcat dir \"\\\\acad_exchange.dxf\"))");
            L.AppendLine("  (princ \"\\nВыберите контуры для эрозии (отрезки, дуги, круги, полилинии): \")");
            L.AppendLine("  (if (setq ss (ssget '((0 . \"LINE,ARC,CIRCLE,LWPOLYLINE,SPLINE,ELLIPSE\"))))");
            L.AppendLine("    (progn");
            L.AppendLine("      (setq fh (open f \"w\") i 0 cnt 0 bad 0)");
            L.AppendLine("      (stzwc:w fh 0 \"SECTION\") (stzwc:w fh 2 \"ENTITIES\")");
            L.AppendLine("      (repeat (sslength ss)");
            L.AppendLine("        (setq e (ssname ss i) ed (entget e) typ (cdr (assoc 0 ed)) i (1+ i))");
            L.AppendLine("        (cond");
            L.AppendLine("          ((= typ \"LINE\")");
            L.AppendLine("            (stzwc:w fh 0 \"LINE\")");
            L.AppendLine("            (stzwc:w fh 10 (stzwc:n (cadr (assoc 10 ed)))) (stzwc:w fh 20 (stzwc:n (caddr (assoc 10 ed))))");
            L.AppendLine("            (stzwc:w fh 11 (stzwc:n (cadr (assoc 11 ed)))) (stzwc:w fh 21 (stzwc:n (caddr (assoc 11 ed))))");
            L.AppendLine("            (setq cnt (1+ cnt)))");
            L.AppendLine("          ((= typ \"CIRCLE\")");
            L.AppendLine("            (stzwc:w fh 0 \"CIRCLE\")");
            L.AppendLine("            (stzwc:w fh 10 (stzwc:n (cadr (assoc 10 ed)))) (stzwc:w fh 20 (stzwc:n (caddr (assoc 10 ed))))");
            L.AppendLine("            (stzwc:w fh 40 (stzwc:n (cdr (assoc 40 ed)))) (stzwc:w fh 230 (stzwc:ez ed))");
            L.AppendLine("            (setq cnt (1+ cnt)))");
            L.AppendLine("          ((= typ \"ARC\")");
            L.AppendLine("            (stzwc:w fh 0 \"ARC\")");
            L.AppendLine("            (stzwc:w fh 10 (stzwc:n (cadr (assoc 10 ed)))) (stzwc:w fh 20 (stzwc:n (caddr (assoc 10 ed))))");
            L.AppendLine("            (stzwc:w fh 40 (stzwc:n (cdr (assoc 40 ed))))");
            L.AppendLine("            (stzwc:w fh 50 (stzwc:n (* 180.0 (/ (cdr (assoc 50 ed)) pi))))");
            L.AppendLine("            (stzwc:w fh 51 (stzwc:n (* 180.0 (/ (cdr (assoc 51 ed)) pi))))");
            L.AppendLine("            (stzwc:w fh 230 (stzwc:ez ed))");
            L.AppendLine("            (setq cnt (1+ cnt)))");
            L.AppendLine("          ((= typ \"LWPOLYLINE\")");
            L.AppendLine("            (stzwc:w fh 0 \"LWPOLYLINE\")");
            L.AppendLine("            (stzwc:w fh 70 (itoa (cdr (assoc 70 ed)))) (stzwc:w fh 230 (stzwc:ez ed))");
            L.AppendLine("            (foreach p ed");
            L.AppendLine("              (cond ((= (car p) 10) (stzwc:w fh 10 (stzwc:n (cadr p))) (stzwc:w fh 20 (stzwc:n (caddr p))))");
            L.AppendLine("                    ((= (car p) 42) (stzwc:w fh 42 (stzwc:n (cdr p))))))");
            L.AppendLine("            (setq cnt (1+ cnt)))");
            L.AppendLine("          (T");
            L.AppendLine("            ;; сплайн / эллипс -> разбить на отрезки через временную полилинию нельзя без ET, передаём точки");
            L.AppendLine("            (stzwc:spl fh e) (setq bad (1+ bad)))");
            L.AppendLine("        )");
            L.AppendLine("      )");
            L.AppendLine("      (stzwc:w fh 0 \"ENDSEC\") (write-line \"0\" fh) (write-line \"EOF\" fh)");
            L.AppendLine("      (close fh)");
            L.AppendLine("      (princ (strcat \"\\nПередано объектов: \" (itoa (+ cnt bad)) (if (> bad 0) (strcat \" (сплайнов/эллипсов аппроксимировано: \" (itoa bad) \")\") \"\")))");
            L.AppendLine("      (startapp \"" + Esc(exe) + "\" (strcat \"\\\"\" f \"\\\"\"))");
            L.AppendLine("    )");
            L.AppendLine("  )");
            L.AppendLine("  (princ)");
            L.AppendLine(")");
            L.AppendLine(";; сплайн или эллипс — 200 точек по кривой (vlax-curve)");
            L.AppendLine("(defun stzwc:spl (fh e / o p0 p1 len k n)");
            L.AppendLine("  (setq o (vlax-ename->vla-object e) n 200");
            L.AppendLine("        len (vlax-curve-getDistAtParam o (vlax-curve-getEndParam o)) k 0");
            L.AppendLine("        p0 (vlax-curve-getPointAtDist o 0.0))");
            L.AppendLine("  (repeat n");
            L.AppendLine("    (setq k (1+ k) p1 (vlax-curve-getPointAtDist o (* len (/ (float k) n))))");
            L.AppendLine("    (if (null p1) (setq p1 (vlax-curve-getEndPoint o)))");
            L.AppendLine("    (stzwc:w fh 0 \"LINE\")");
            L.AppendLine("    (stzwc:w fh 10 (stzwc:n (car p0))) (stzwc:w fh 20 (stzwc:n (cadr p0)))");
            L.AppendLine("    (stzwc:w fh 11 (stzwc:n (car p1))) (stzwc:w fh 21 (stzwc:n (cadr p1)))");
            L.AppendLine("    (setq p0 p1))");
            L.AppendLine(")");
            L.AppendLine("(defun c:EROZIA () (c:WCUT))");
            L.AppendLine("(princ \"\\nSTZ WireCut: команда WCUT (EROZIA) загружена.\")");
            L.AppendLine("(princ)");
            File.WriteAllText(LispFile, L.ToString(), Encoding.GetEncoding(1251));

            string load = "(load \"" + LispFile.Replace("\\", "/") + "\")";
            try { Clipboard.SetText(load); } catch { }
            f.Msg("Файл команды для AutoCAD создан: " + LispFile);
            MessageBox.Show(f,
                "Файл команды создан:\r\n" + LispFile + "\r\n\r\n" +
                "Как подключить в AutoCAD (один раз):\r\n" +
                "1. Команда _APPLOAD (ЗАГПРИЛ) → «Автозагрузка» (Startup Suite) → «Добавить» → выбрать файл выше.\r\n" +
                "   Или вставьте в командную строку AutoCAD (уже скопировано в буфер):\r\n   " + load + "\r\n\r\n" +
                "2. В AutoCAD: команда WCUT (или EROZIA) → выбрать контуры → Enter.\r\n" +
                "   STZ WireCut откроется (или обновится, если уже открыта) с выбранными контурами.\r\n\r\n" +
                "Обратно: «Файл → Экспорт траектории в DXF» — траектория по слоям для проверки в AutoCAD.",
                "Связь с AutoCAD", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
