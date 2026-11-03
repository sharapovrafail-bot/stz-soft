// STZ WireCut — точка входа
using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("STZ WireCut")]
[assembly: System.Reflection.AssemblyProduct("STZ WireCut — электроэрозионная вырезка")]
[assembly: System.Reflection.AssemblyCompany("Сделано специально по заказу ООО «СпецТехЗаказ»")]
[assembly: System.Reflection.AssemblyCopyright("© 2026 Рафаил Шарапов · rafail@sharapov-cnc.ru")]
[assembly: System.Reflection.AssemblyVersion("3.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.3.0.0")]

namespace WireCut
{
    static class Program
    {
#if NET48
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
#endif
        [STAThread]
        static void Main(string[] args)
        {
            // точка как разделитель дробной части — везде (важно для УП и DXF)
            CultureInfo ci = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            Thread.CurrentThread.CurrentCulture = ci;

#if NET48
            try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); } catch { }
#endif
            bool first;
            using (Mutex mx = new Mutex(true, "STZ_WireCut_SingleInstance", out first))
            {
                if (!first && args.Length > 0)
                {
                    // программа уже открыта — она сама подхватит файл обмена из AutoCAD
                    if (args[0].ToLower().EndsWith("acad_exchange.dxf")) return;
                    // двойной щелчок по файлу колеса STZ или DXF — передать уже открытой программе
                    try { System.IO.Directory.CreateDirectory(MainForm.AppDir); System.IO.File.WriteAllText(MainForm.OpenRequestFile, System.IO.Path.GetFullPath(args[0]), System.Text.Encoding.UTF8); return; } catch { }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("Ошибка: " + e.Exception.Message + "\r\n\r\n" + e.Exception.StackTrace, "STZ WireCut", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
                GC.KeepAlive(mx);
            }
        }
    }
}
