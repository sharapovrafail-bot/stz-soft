// STZ WireCut — «На станок»: одним нажатием траектория → УП → в папку AutoCut (Work) или по RS-232, без окна УП.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WireCut
{
    public partial class MainForm
    {
        /// <summary>Похоже на папку Work программы AutoCut (компьютер станка), а не на флешку.</summary>
        public static bool IsAutoCutWork(string folder)
        {
            string f = folder.ToUpperInvariant();
            return f.IndexOf("AUTOCUT") >= 0 || f.TrimEnd('\\').EndsWith("WORK");
        }

        /// <summary>Выбрать папку станка вручную и запомнить её (settings.ini). null — отмена.</summary>
        public string AskMachineFolder(string why)
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { ShowNewFolderButton = true,
                Description = (why != null ? why + " " : "") + "Выберите папку станка — сюда «На станок» будет записывать УП: папку Work программы AutoCut (" + Presets.AutoCutWork + "), флешку или сетевую папку. Папка запомнится." })
            {
                if (S.Folder != "" && Directory.Exists(S.Folder)) d.SelectedPath = S.Folder;
                if (d.ShowDialog(this) != DialogResult.OK) { Msg("Папка станка не выбрана" + (S.Folder != "" ? " — осталась: " + S.Folder : ".")); return null; }
                S.Folder = d.SelectedPath; S.Link = LinkMethod.Folder;
            }
            try { S.Save(SettingsPath); } catch { }
            grid.Refresh();
            Msg("Папка станка: " + S.Folder + " (запомнена; поменять — «Эрозия → Папка станка…» или Свойства → 5. Связь со станком)");
            return S.Folder;
        }

        static Encoding EncOf(OutFile f) { return f.Cyr ? Encoding.GetEncoding(1251) : Encoding.ASCII; }

        /// <summary>Отправить УП на станок сразу (кнопка «На станок», Ctrl+F10).</summary>
        void SendToMachine()
        {
            if (!BuildPath(false)) return;
            Toolpath tp = Cv.Path;
            // предупреждения траектории — спросить, а не отправлять молча
            if (tp.Warn.Count > 0)
            {
                string w = string.Join("\r\n• ", tp.Warn.ToArray());
                if (MessageBox.Show(this, "Есть предупреждения по траектории:\r\n\r\n• " + w + "\r\n\r\nВсё равно отправить на станок?", "На станок — проверьте",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) { Msg("Отправка отменена — исправьте предупреждения или отправьте всё равно."); return; }
            }
            string baseName = MakeSafe(S.PartName);
            List<OutFile> files = Post.Make(tp, S, baseName);
            OutFile prog = null;
            foreach (OutFile f in files) if (!f.Name.EndsWith(".txt")) { prog = f; break; }
            if (prog == null) { Msg("Ошибка: нет программы для отправки (проверьте «Формат УП» в свойствах)."); return; }

            if (S.Link == LinkMethod.Com)
            {
                if (MessageBox.Show(this, "Отправить «" + prog.Name + "» в " + S.Port + " (" + S.Baud + " бод)?\r\nНа станке включите приём (READ / ВВОД).", "На станок — RS-232",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                Msg("Передача " + prog.Name + " в " + S.Port + "…");
                string text = prog.Text; Encoding enc = EncOf(prog); JobSettings s = S;
                Thread th = new Thread(delegate ()
                {
                    string err = MachineLink.SendCom(text, s, enc, delegate (int pct, int z) { });
                    BeginInvoke((MethodInvoker)delegate { Msg(err == null ? "УП «" + prog.Name + "» передана на станок (" + s.Port + ")." : "Ошибка передачи: " + err); });
                });
                th.IsBackground = true; th.Start();
                return;
            }

            // папка станка — та, что задана в свойствах («5. Связь со станком → Папка станка») или меню «Эрозия → Папка станка…»
            bool autocut = S.Profile == MachineProfile.DK7750;
            if (S.Folder == "" || !Directory.Exists(S.Folder))
                if (AskMachineFolder(S.Folder == "" ? "Папка станка не задана." : "Папки «" + S.Folder + "» на этом компьютере нет.") == null) return;
            string folder = S.Folder;
            bool onMachine = IsAutoCutWork(folder);
            try
            {
                foreach (OutFile f in files) File.WriteAllText(Path.Combine(folder, f.Name), f.Text, EncOf(f));
            }
            catch (Exception ex) { Msg("Ошибка записи в " + folder + ": " + ex.Message); return; }
            Msg((onMachine ? "► Отправлено на станок: " : "► Записано на флешку: ") + Path.Combine(folder, prog.Name) + "  (+ карта наладки " + baseName + "_карта.txt)");
            if (autocut)
                Msg(onMachine ? "  AutoCut: Load / Open → тип 3B → " + prog.Name + " → проверить картинку → режимы по карте → Sim (холостой) → резка."
                              : "  На станке скопируйте в " + Presets.AutoCutWork + ", затем в AutoCut: Load → 3B → " + prog.Name + ".");
            stInfo.Text = (onMachine ? "Отправлено: " : "На флешке: ") + prog.Name;
        }
    }
}
