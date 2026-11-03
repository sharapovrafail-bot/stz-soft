// STZ WireCut — таблица режимов резания (материал × толщина) для быстрой протяжки (молибден, AutoCut)
// Рекомендованные значения ориентировочные; таблицу можно править, она хранится в %APPDATA%\STZ_WireCut\regimes.csv
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WireCut
{
    public class RegimeRow
    {
        public string Material;
        public double T0, T1;       // толщина от (не вкл.) — до (вкл.), мм
        public double Ton;          // ширина импульса, мкс
        public double Ratio;        // кратность паузы (интервал = Ton × Ratio)
        public int Tubes;           // силовые трубки (ступени тока)
        public double Ip;           // средний ток, А
        public double U;            // напряжение, В
        public int VF;              // V-F (чувствительность подачи AutoCut)
        public double Rate;         // производительность, мм²/мин
        public double Gap;          // искровой зазор на сторону, мм
        public string Note = "";
        public double Toff { get { return Math.Round(Ton * Ratio, 1); } }
        public RegimeRow Clone() { return (RegimeRow)MemberwiseClone(); }
    }

    public static class RegimeDb
    {
        public static List<RegimeRow> Rows = new List<RegimeRow>();
        static string FileName { get { return Path.Combine(MainForm.AppDir, "regimes.csv"); } }
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // группы материалов: название, ключевые слова, поправки к режиму стали
        // (множитель Ton, добавка к кратности паузы, добавка трубок, множитель производительности, зазор, примечание)
        public class Group
        {
            public string Name; public string[] Keys; public double KTon, DRatio; public int DTubes; public double KRate, Gap; public string Note;
            public Group(string n, string k, double kt, double dr, int dt, double kr, double gap, string note)
            { Name = n; Keys = k.Split('|'); KTon = kt; DRatio = dr; DTubes = dt; KRate = kr; Gap = gap; Note = note; }
        }
        public static readonly Group[] Groups = {
            new Group("Закалённая сталь HRC 50–62", "hrc|закал|зак.", 0.9, 0.5, 0, 0.8, 0.010, "Резать после отпуска; при трещинах — снять напряжения, режим мягче"),
            new Group("Инструментальная сталь (Х12МФ, У8А, ШХ15, 9ХС)", "х12|у8|у10|у7|шх15|9хс|хвг|р6м5|р18|5хнм|4х5мф", 1.0, 0, 0, 0.9, 0.010, "Основной материал штампов"),
            new Group("Нержавеющая сталь (12Х18Н10Т, 20Х13)", "12х18|08х18|нерж|20х13|40х13|aisi", 1.0, 1, 0, 0.8, 0.010, "Вязкая — больше пауза, следить за обрывом проволоки"),
            new Group("Твёрдый сплав (ВК8, ВК15, Т15К6)", "вк|т15к|т5к|твёрд|тверд", 0.5, 2, -1, 0.35, 0.008, "Короткий импульс, малый ток — иначе микротрещины"),
            new Group("Медь (М1, М2)", "медь|м1|м2|мо", 1.5, 0, 1, 0.5, 0.015, "Высокая теплопроводность — длинный импульс, ниже скорость"),
            new Group("Латунь / бронза", "латун|бронз|лс59|л63|бр", 1.2, 0, 0, 0.8, 0.012, ""),
            new Group("Алюминий (Д16Т, АМг, АД31)", "алюм|д16|амг|ад31|в95", 1.0, 2, 0, 1.3, 0.015, "Большая пауза для выноса шлама, часто менять эмульсию"),
            new Group("Титан (ВТ1, ВТ6)", "титан|вт1|вт6|вт", 1.0, 1, 0, 0.6, 0.012, "Склонен к налипанию — пауза больше"),
            new Group("Графит", "графит", 1.2, 0, 0, 0.6, 0.012, ""),
            new Group("Конструкционная сталь (Ст3, 20, 45, 40Х, 65Г)", "ст3|сталь|45|40х|20|65г|35|30хгса|09г2с", 1.0, 0, 0, 1.0, 0.010, "")
        };

        // базовые режимы для стали 45 по толщине (молибден 0.18, быстрая протяжка)
        static readonly double[,] Base = {
            //  до,   Ton, кратн., трубки, Ip,  U,  V-F, мм²/мин
            {  10,    8,   4,     3,      1.5, 75,  40,  40 },
            {  30,   16,   5,     4,      2.5, 80,  50,  70 },
            {  60,   24,   6,     5,      3.5, 85,  50,  90 },
            { 100,   32,   6,     6,      4.5, 90,  55, 100 },
            { 200,   48,   7,     7,      5.0, 95,  60, 100 },
            { 300,   64,   8,     8,      5.5, 95,  65,  90 },
            { 500,   80,   9,     9,      6.0, 100, 70,  80 } };

        public static List<RegimeRow> Defaults()
        {
            List<RegimeRow> r = new List<RegimeRow>();
            foreach (Group g in Groups)
            {
                double prev = 0;
                for (int i = 0; i < Base.GetLength(0); i++)
                {
                    RegimeRow x = new RegimeRow
                    {
                        Material = g.Name, T0 = prev, T1 = Base[i, 0],
                        Ton = Math.Max(2, Math.Round(Base[i, 1] * g.KTon)), Ratio = Base[i, 2] + g.DRatio,
                        Tubes = Math.Max(1, (int)Base[i, 3] + g.DTubes), Ip = Math.Round(Base[i, 4] * (1 + 0.15 * g.DTubes / Math.Max(1, Base[i, 3])) * (g.KTon < 0.8 ? 0.7 : 1), 1),
                        U = Base[i, 5], VF = (int)Base[i, 6], Rate = Math.Round(Base[i, 7] * g.KRate), Gap = g.Gap, Note = g.Note
                    };
                    r.Add(x); prev = Base[i, 0];
                }
            }
            return r;
        }

        public static void Load()
        {
            Rows = Defaults();
            try
            {
                if (!File.Exists(FileName)) return;
                List<RegimeRow> l = new List<RegimeRow>();
                foreach (string line in File.ReadAllLines(FileName, Encoding.UTF8))
                {
                    string[] p = line.Split(';');
                    if (p.Length < 11 || p[0] == "Материал") continue;
                    l.Add(new RegimeRow
                    {
                        Material = p[0], T0 = D(p[1]), T1 = D(p[2]), Ton = D(p[3]), Ratio = D(p[4]), Tubes = (int)D(p[5]),
                        Ip = D(p[6]), U = D(p[7]), VF = (int)D(p[8]), Rate = D(p[9]), Gap = D(p[10]), Note = p.Length > 11 ? p[11] : ""
                    });
                }
                if (l.Count > 0) Rows = l;
            }
            catch { }
        }

        public static void Save()
        {
            Directory.CreateDirectory(MainForm.AppDir);
            StringBuilder sb = new StringBuilder("Материал;От, мм;До, мм;Ton, мкс;Кратность паузы;Трубки;Ток, А;U, В;V-F;мм²/мин;Зазор, мм;Примечание\r\n");
            foreach (RegimeRow r in Rows)
                sb.Append(r.Material).Append(';').Append(N(r.T0)).Append(';').Append(N(r.T1)).Append(';').Append(N(r.Ton)).Append(';').Append(N(r.Ratio)).Append(';')
                  .Append(r.Tubes).Append(';').Append(N(r.Ip)).Append(';').Append(N(r.U)).Append(';').Append(r.VF).Append(';').Append(N(r.Rate)).Append(';')
                  .Append(N(r.Gap)).Append(';').Append((r.Note ?? "").Replace(";", ",")).Append("\r\n");
            File.WriteAllText(FileName, sb.ToString(), Encoding.UTF8);
        }

        static double D(string s) { double v; double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, IC, out v); return v; }
        static string N(double v) { return v.ToString("0.###", IC); }

        /// <summary>Группа материала по названию марки (Х12МФ → инструментальная сталь и т.п.).</summary>
        public static string GroupOf(string material)
        {
            string m = (material ?? "").Trim().ToLower();
            foreach (RegimeRow r in Rows) if (r.Material.ToLower() == m) return r.Material;
            foreach (Group g in Groups) foreach (string k in g.Keys) if (k.Length > 0 && m.Contains(k)) return g.Name;
            return Groups[Groups.Length - 1].Name;
        }

        public static List<string> Materials()
        {
            List<string> l = new List<string>();
            foreach (RegimeRow r in Rows) if (!l.Contains(r.Material)) l.Add(r.Material);
            return l;
        }

        public static RegimeRow Find(string material, double h)
        {
            if (Rows.Count == 0) Load();
            string g = GroupOf(material);
            RegimeRow last = null;
            foreach (RegimeRow r in Rows)
            {
                if (r.Material != g) continue;
                if (h > r.T0 && h <= r.T1) return r;
                if (last == null || r.T1 > last.T1) last = r;
            }
            if (last != null) return last;
            return Rows.Count > 0 ? Rows[0] : Defaults()[0];
        }

        /// <summary>Подставить режим из таблицы в основной проход.</summary>
        public static RegimeRow ApplyTo(JobSettings s)
        {
            RegimeRow r = Find(s.Material, s.Thickness);
            PassParam p = s.P1;
            p.On = true; p.Name = "Основной"; p.Ton = r.Ton; p.Toff = r.Toff; p.Tubes = r.Tubes; p.Ip = r.Ip; p.U = r.U; p.VF = r.VF; p.Rate = r.Rate; p.Gap = r.Gap;
            return r;
        }

        public static string Advice(RegimeRow r, double h)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("РЕКОМЕНДОВАННЫЙ РЕЖИМ  (").Append(r.Material).Append(", ").Append(Geo.F(h)).Append(" мм)\r\n\r\n");
            sb.Append("Импульс (Pulse width)      ").Append(Geo.F(r.Ton)).Append(" мкс\r\n");
            sb.Append("Интервал (Pulse interval)  ×").Append(Geo.F(r.Ratio)).Append("  = ").Append(Geo.F(r.Toff)).Append(" мкс\r\n");
            sb.Append("Силовые трубки (ток)       ").Append(r.Tubes).Append("  (≈ ").Append(Geo.F(r.Ip)).Append(" А)\r\n");
            sb.Append("Напряжение                 ").Append(Geo.F(r.U)).Append(" В\r\n");
            sb.Append("V-F (подача)               ").Append(r.VF).Append("\r\n");
            sb.Append("Искровой зазор             ").Append(Geo.F(r.Gap)).Append(" мм на сторону\r\n");
            sb.Append("Смещение проволоки 0.18    ").Append(Geo.F(0.09 + r.Gap)).Append(" мм\r\n");
            sb.Append("Скорость резки ≈           ").Append(Geo.F(r.Rate)).Append(" мм²/мин  (≈ ").Append(Geo.F(Math.Round(r.Rate / Math.Max(1, h), 2))).Append(" мм/мин по контуру)\r\n");
            if (!string.IsNullOrEmpty(r.Note)) sb.Append("\r\nОсобенность: ").Append(r.Note).Append("\r\n");
            sb.Append("\r\nОБЩИЕ РЕКОМЕНДАЦИИ (DK7750, молибден)\r\n");
            sb.Append("• Проволока молибденовая Ø0.18 (0.16–0.20); при износе до 0.16 — заменить.\r\n");
            sb.Append("• Натяжение — равномерное, после намотки подтянуть; проволоку выверить по вертикали.\r\n");
            sb.Append("• Эмульсия (ДХ-2, JR1A и т.п.) 1:15–1:20, обновлять при потемнении; подача — с обеих сторон.\r\n");
            sb.Append("• Ток подбирать по амперметру: стабильная искра без коротких замыканий.\r\n");
            sb.Append("• V-F: если проволока «садится» (КЗ) — уменьшить; если ток прыгает и скорость мала — увеличить.\r\n");
            sb.Append("• Толстые заготовки (>100 мм): больше интервал, промывка обязательна, скорость ниже.\r\n");
            sb.Append("• Первая деталь — замер и при необходимости «Коррекция размера» в свойствах детали.\r\n");
            sb.Append("\r\nЗначения ориентировочные — уточните по своему опыту и сохраните в таблице.");
            return sb.ToString();
        }
    }

    // ======================= ОКНО «РЕЖИМЫ РЕЗАНИЯ» =======================
    public class RegimeForm : Form
    {
        MainForm main; ComboBox mat; NumericUpDown thick; DataGridView dg; TextBox adv; CheckBox onlyMat;
        static readonly string[] Cols = { "Материал", "От, мм", "До, мм", "Импульс Ton, мкс", "Кратность паузы", "Пауза Toff, мкс", "Трубки", "Ток, А", "U, В", "V-F", "мм²/мин", "Зазор, мм", "Примечание" };

        public RegimeForm(MainForm m)
        {
            main = m;
#if NET48
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
#endif
            Text = "Режимы резания — SIMOS DK7750 (молибден, AutoCut)";
            Size = new Size(1240, 760); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false;
            Font = new Font(MainForm.UiFont, MainForm.UiSize); BackColor = Theme.Surface; ForeColor = Theme.Text;
            ToolTip tip = new ToolTip { AutoPopDelay = 20000 };

            // --- выбор ---
            FlowLayoutPanel top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8, 10, 8, 4), BackColor = Theme.Raised };
            top.Controls.Add(new Label { Text = "Материал:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            mat = new ComboBox { Width = 420, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.Background, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            foreach (string s in RegimeDb.Materials()) mat.Items.Add(s);
            mat.SelectedItem = RegimeDb.GroupOf(m.S.Material);
            if (mat.SelectedIndex < 0 && mat.Items.Count > 0) mat.SelectedIndex = 0;
            top.Controls.Add(mat);
            top.Controls.Add(new Label { Text = "   Толщина, мм:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            thick = new NumericUpDown { Width = 90, Minimum = 1, Maximum = 600, DecimalPlaces = 1, Value = (decimal)Math.Max(1, Math.Min(600, m.S.Thickness)), BackColor = Theme.Background, ForeColor = Theme.Text };
            top.Controls.Add(thick);
            onlyMat = new CheckBox { Text = "только этот материал", Checked = true, AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
            top.Controls.Add(onlyMat);
            tip.SetToolTip(mat, "Группа материала. Марки (Х12МФ, 40Х, ВК8 …) относятся к группам автоматически.");
            tip.SetToolTip(thick, "Толщина заготовки — выбирается строка таблицы с этим диапазоном толщин.");

            // --- таблица ---
            dg = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 28, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Theme.Background, GridColor = Theme.Border, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ColumnHeadersHeight = 46
            };
            dg.DefaultCellStyle.BackColor = Theme.Background; dg.DefaultCellStyle.ForeColor = Theme.Text;
            dg.DefaultCellStyle.SelectionBackColor = Theme.Accent; dg.DefaultCellStyle.SelectionForeColor = Color.White;
            dg.ColumnHeadersDefaultCellStyle.BackColor = Theme.Raised; dg.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            dg.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dg.RowHeadersDefaultCellStyle.BackColor = Theme.Raised;
            int[] w = { 190, 60, 60, 80, 80, 80, 64, 64, 56, 52, 72, 70, 380 };
            for (int i = 0; i < Cols.Length; i++)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn { HeaderText = Cols[i], Width = w[i], SortMode = DataGridViewColumnSortMode.NotSortable };
                if (i == 5) { c.ReadOnly = true; c.DefaultCellStyle.ForeColor = Theme.Muted; }
                dg.Columns.Add(c);
            }
            dg.Columns[3].ToolTipText = "Ширина импульса — в AutoCut «Pulse width»";
            dg.Columns[4].ToolTipText = "Интервал = импульс × кратность — в AutoCut «Pulse interval»";
            dg.Columns[6].ToolTipText = "Количество силовых трубок (ступень тока) в генераторе";
            dg.Columns[9].ToolTipText = "V-F — чувствительность / частота подачи в AutoCut";
            dg.Columns[10].ToolTipText = "Производительность резки — по ней считаются время и стоимость";
            dg.Columns[11].ToolTipText = "Искровой зазор на сторону — смещение = Ø/2 + зазор";

            // --- рекомендации ---
            adv = new TextBox { Dock = DockStyle.Right, Width = 470, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = new Font("Consolas", 10.5f), BorderStyle = BorderStyle.None };

            // --- кнопки ---
            FlowLayoutPanel bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight, BackColor = Theme.Raised };
            Button apply = B("Применить к детали", Theme.Green, "Записать материал, толщину и режим в свойства детали (основной проход).");
            Button save = B("Сохранить таблицу", Theme.Accent, "Сохранить свои правки таблицы — они будут подставляться автоматически.");
            Button reset = B("Сбросить к рекомендованным", Theme.Background, "Вернуть все строки таблицы к заводским рекомендованным значениям.");
            Button close = B("Закрыть", Theme.Background, "Закрыть без применения.");
            bp.Controls.Add(apply); bp.Controls.Add(save); bp.Controls.Add(reset); bp.Controls.Add(close);
            CancelButton = close;
            foreach (Button b in new Button[] { apply, save, reset, close }) tip.SetToolTip(b, (string)b.Tag);

            Controls.Add(dg); Controls.Add(adv); Controls.Add(top); Controls.Add(bp);
#if NET48
            ResumeLayout(true);
#endif
            Fill();
            Shown += delegate { Highlight(); };
            mat.SelectedIndexChanged += delegate { Fill(); };
            onlyMat.CheckedChanged += delegate { Fill(); };
            thick.ValueChanged += delegate { Highlight(); };
            dg.CellEndEdit += delegate { Collect(); Highlight(); };
            dg.SelectionChanged += delegate { ShowAdvice(); };
            apply.Click += delegate
            {
                Collect();
                main.S.Material = (string)mat.SelectedItem; main.S.Thickness = (double)thick.Value;
                RegimeRow r = RegimeDb.ApplyTo(main.S);
                main.AfterRegime("Режим применён: " + r.Material + ", " + Geo.F(main.S.Thickness) + " мм — импульс " + Geo.F(r.Ton) + " мкс, пауза ×" + Geo.F(r.Ratio) + ", трубок " + r.Tubes + ", V-F " + r.VF);
                Close();
            };
            save.Click += delegate { Collect(); RegimeDb.Save(); main.Msg("Таблица режимов сохранена."); MessageBox.Show(this, "Таблица режимов сохранена.", Text); };
            reset.Click += delegate
            {
                if (MessageBox.Show(this, "Вернуть все режимы к рекомендованным? Ваши правки будут потеряны.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                RegimeDb.Rows = RegimeDb.Defaults(); RegimeDb.Save(); Fill();
            };
            close.Click += delegate { Close(); };
        }

        Button B(string t, Color c, string tipText)
        {
            Button b = new Button { Text = t, AutoSize = true, Height = 36, BackColor = c, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(4, 0, 8, 0), Tag = tipText, Padding = new Padding(8, 0, 8, 0) };
            b.FlatAppearance.BorderColor = Theme.Border;
            return b;
        }

        List<RegimeRow> shown = new List<RegimeRow>();
        void Fill()
        {
            Collect();
            dg.Rows.Clear(); shown.Clear();
            string g = (string)mat.SelectedItem;
            foreach (RegimeRow r in RegimeDb.Rows)
            {
                if (onlyMat.Checked && r.Material != g) continue;
                shown.Add(r);
                dg.Rows.Add(r.Material, N(r.T0), N(r.T1), N(r.Ton), N(r.Ratio), N(r.Toff), r.Tubes, N(r.Ip), N(r.U), r.VF, N(r.Rate), N(r.Gap), r.Note);
            }
            Highlight();
        }

        // перенести правки из таблицы обратно в базу
        void Collect()
        {
            if (dg == null || dg.Rows.Count == 0) return;
            List<RegimeRow> fromGrid = new List<RegimeRow>();
            foreach (DataGridViewRow row in dg.Rows)
            {
                if (row.IsNewRow) continue;
                string m = Convert.ToString(row.Cells[0].Value);
                if (string.IsNullOrEmpty(m)) continue;
                RegimeRow r = new RegimeRow
                {
                    Material = m, T0 = V(row, 1), T1 = V(row, 2), Ton = V(row, 3), Ratio = V(row, 4), Tubes = (int)V(row, 6), Ip = V(row, 7),
                    U = V(row, 8), VF = (int)V(row, 9), Rate = V(row, 10), Gap = V(row, 11), Note = Convert.ToString(row.Cells[12].Value)
                };
                row.Cells[5].Value = N(r.Toff);
                fromGrid.Add(r);
            }
            // заменить показанные строки на отредактированные
            foreach (RegimeRow r in shown) RegimeDb.Rows.Remove(r);
            RegimeDb.Rows.AddRange(fromGrid);
            shown = fromGrid;
            RegimeDb.Rows.Sort(delegate (RegimeRow a, RegimeRow b)
            {
                int ia = IndexOfGroup(a.Material), ib = IndexOfGroup(b.Material);
                return ia != ib ? ia.CompareTo(ib) : a.T1.CompareTo(b.T1);
            });
        }
        static int IndexOfGroup(string n) { for (int i = 0; i < RegimeDb.Groups.Length; i++) if (RegimeDb.Groups[i].Name == n) return i; return 99; }

        void Highlight()
        {
            double h = (double)thick.Value; string g = (string)mat.SelectedItem;
            foreach (DataGridViewRow row in dg.Rows)
            {
                if (row.IsNewRow) continue;
                bool hit = Convert.ToString(row.Cells[0].Value) == g && h > V(row, 1) && h <= V(row, 2);
                row.DefaultCellStyle.BackColor = hit ? Color.FromArgb(40, 90, 60) : Theme.Background;
                if (hit) { try { dg.CurrentCell = row.Cells[3]; } catch { } dg.ClearSelection(); row.Selected = true; try { dg.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index - 2); } catch { } }
            }
            ShowAdvice();
        }

        void ShowAdvice()
        {
            RegimeRow r = RegimeDb.Find((string)mat.SelectedItem, (double)thick.Value);
            adv.Text = RegimeDb.Advice(r, (double)thick.Value);
        }

        static double V(DataGridViewRow row, int i)
        {
            double v; MainForm.Num(Convert.ToString(row.Cells[i].Value), out v); return v;
        }
        static string N(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    }
}
