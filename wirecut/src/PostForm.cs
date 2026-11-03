// STZ WireCut — окно готовой УП: просмотр, правка, сохранение, отправка на станок
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WireCut
{
    public class PostForm : Form
    {
        MainForm main; List<OutFile> files;
        TabControl tabs; ProgressBar bar; Label status;
        List<CodeEditor> boxes = new List<CodeEditor>();

        public PostForm(MainForm m, List<OutFile> f)
        {
            main = m; files = f;
#if NET48
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
#endif
            Text = "Управляющая программа — " + m.S.PartName;
            Size = new Size(900, 680); StartPosition = FormStartPosition.CenterParent;
            Font = m.Font; ShowInTaskbar = false; MinimizeBox = false;

            BackColor = Theme.Background; ForeColor = Theme.Text; DoubleBuffered = true; MinimumSize = new Size(900, 550);
            tabs = new DarkTabs { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, ItemSize = new Size(160, 30) };
            tabs.DrawItem += delegate(object sender, DrawItemEventArgs e) { using (Brush bg = new SolidBrush(e.Index == tabs.SelectedIndex ? Theme.Raised : Theme.Surface)) e.Graphics.FillRectangle(bg,e.Bounds); TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,Font,e.Bounds,Theme.Text,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); };
            foreach (OutFile of in files)
            {
                TabPage p = new TabPage(of.Name);
                p.BackColor = Theme.Background;
                CodeEditor tb = new CodeEditor { Dock = DockStyle.Fill, Text = of.Text };
                p.Controls.Add(tb); tabs.TabPages.Add(p); boxes.Add(tb);
            }

            FlowLayoutPanel bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 88, BackColor = Theme.Surface, Padding = new Padding(6), FlowDirection = FlowDirection.LeftToRight };
            bp.Controls.Add(Btn("Сохранить все файлы...", delegate { SaveAll(null); }));
            bp.Controls.Add(Btn("Сохранить текущий как...", delegate { SaveCurrent(); }));
            bp.Controls.Add(Btn("Копировать", delegate { Clipboard.SetText(boxes[tabs.SelectedIndex].Text); status.Text = "Скопировано в буфер обмена."; }));
            Button send = Btn("Отправить на станок", delegate { Send(); });
            send.BackColor = Theme.Green; send.ForeColor = Color.White; send.FlatStyle = FlatStyle.Flat; send.Width = 170;
            bp.Controls.Add(send);
            Button close = Btn("Закрыть", delegate { Close(); });
            bp.Controls.Add(close); CancelButton = close;

            Panel st = new Panel { Dock = DockStyle.Bottom, Height = 24 };
            bar = new ProgressBar { Dock = DockStyle.Right, Width = 220, Visible = false };
            status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
            status.Text = "Способ передачи: " + RusEnumConverter.Text(m.S.Link) + (m.S.Link == LinkMethod.Com ? "  " + m.S.Port + " " + m.S.Baud : (m.S.Folder != "" ? "  → " + m.S.Folder : "  (папка не задана)"));
            st.Controls.Add(status); st.Controls.Add(bar);

            Controls.Add(tabs); Controls.Add(st); Controls.Add(bp);
#if NET48
            ResumeLayout(true);
#endif
        }

        ToolTip tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400 };
        Button Btn(string t, EventHandler h)
        {
            Button b = new DarkButton { Text = t, Width = 200, Height = 36 }; b.Click += h;
            string key = t.StartsWith("Сохранить все") ? "POST_SAVEALL" : t.StartsWith("Сохранить") ? "POST_SAVE" : t == "Копировать" ? "POST_COPY" : t.StartsWith("Отправить") ? "POST_SEND" : "POST_CLOSE";
            tip.SetToolTip(b, Tips.Get(key));
            return b;
        }

        string Cur(int i) { return boxes[i].Text; }

        static Encoding EncFor(OutFile f) { return f.Cyr ? Encoding.GetEncoding(1251) : Encoding.ASCII; }

        void SaveAll(string folder)
        {
            if (folder == null)
                using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Папка для сохранения УП", SelectedPath = main.S.Folder })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    folder = d.SelectedPath;
                }
            for (int i = 0; i < files.Count; i++)
                File.WriteAllText(Path.Combine(folder, files[i].Name), Cur(i), EncFor(files[i]));
            status.Text = "Сохранено файлов: " + files.Count + " в " + folder;
            main.Msg("УП сохранена: " + folder);
        }

        void SaveCurrent()
        {
            int i = tabs.SelectedIndex;
            using (SaveFileDialog d = new SaveFileDialog { FileName = files[i].Name, Filter = "Все файлы|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(d.FileName, Cur(i), EncFor(files[i])); status.Text = "Сохранено: " + d.FileName; }
        }

        void Send()
        {
            JobSettings s = main.S;
            int i = tabs.SelectedIndex;
            if (files[i].Name.EndsWith(".txt"))
            {
                // по умолчанию отправляем первую программу, а не карту наладки
                i = 0;
                if (files[0].Name.EndsWith(".txt")) { status.Text = "Нет программы для отправки."; return; }
            }
            if (s.Link == LinkMethod.Folder)
            {
                bool autocut = s.Profile == MachineProfile.DK7750;
                if (s.Folder == "" || !Directory.Exists(s.Folder))
                    if (main.AskMachineFolder(s.Folder == "" ? "Папка станка не задана." : "Папки «" + s.Folder + "» на этом компьютере нет.") == null) return;
                string folder = s.Folder;
                bool onMachine = MainForm.IsAutoCutWork(folder);
                SaveAll(folder);
                status.Text = "Передано: " + folder;
                if (autocut)
                {
                    string prog = files[i].Name;
                    MessageBox.Show(this,
                        (onMachine ? "Файл записан в папку AutoCut:\r\n" : "Файл записан на флешку:\r\n") + Path.Combine(folder, prog) + "\r\n\r\n" +
                        (onMachine ? "" : "1. На станке скопируйте его в " + Presets.AutoCutWork + "\r\n") +
                        "2. В AutoCut: открыть задание (Load / Open) → тип файла 3B → " + prog + "\r\n" +
                        "3. Проверить картинку и точку заправки, задать режимы (импульс, интервал, ток, V-F) по карте наладки.\r\n" +
                        "4. Прогон «Sim» (холостой) → затем запуск резки.",
                        "Передача в AutoCut — SIMOS DK7750", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            // COM-порт
            string text = Cur(i);
            if (MessageBox.Show(this, "Отправить «" + files[i].Name + "» в " + s.Port + " (" + s.Baud + " бод)?\r\nНа станке включите приём (READ / ВВОД).", "Передача по RS-232", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            bar.Visible = true; bar.Value = 0;
            Encoding enc = EncFor(files[i]);
            Thread th = new Thread(delegate ()
            {
                string err = MachineLink.SendCom(text, s, enc, delegate (int pct, int z) { BeginInvoke((MethodInvoker)delegate { bar.Value = Math.Min(100, pct); status.Text = "Передача… " + pct + "%"; }); });
                BeginInvoke((MethodInvoker)delegate
                {
                    bar.Visible = false;
                    status.Text = err == null ? "Передано по " + s.Port + " успешно." : "Ошибка: " + err;
                    main.Msg(err == null ? "УП передана на станок (" + s.Port + ")." : "Ошибка передачи: " + err);
                });
            });
            th.IsBackground = true; th.Start();
        }
    }

    public static class MachineLink
    {
        /// <summary>Передача текста по RS-232 построчно (с учётом XON/XOFF или RTS/CTS).</summary>
        public static string SendCom(string text, JobSettings s, Encoding enc, Act2<int, int> progress)
        {
            try
            {
                using (SerialPort sp = new SerialPort(s.Port, s.Baud, s.Parity, s.DataBits, s.StopBits))
                {
                    sp.Handshake = s.Handshake;
                    sp.Encoding = enc;
                    sp.WriteTimeout = 30000;
                    sp.Open();
                    string[] lines = text.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        sp.Write(lines[i] + "\r\n");
                        if (progress != null && i % 5 == 0) progress(100 * i / Math.Max(1, lines.Length), 0);
                    }
                    // дождаться опустошения буфера
                    int guard = 0;
                    while (sp.BytesToWrite > 0 && guard++ < 600) Thread.Sleep(50);
                    if (progress != null) progress(100, 0);
                    sp.Close();
                }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
