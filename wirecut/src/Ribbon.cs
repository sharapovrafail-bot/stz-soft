// STZ WireCut — лента с вкладками (как во Fusion 360), сворачиваемые лента и панель свойств:
// больше места под чертёж на маленьком мониторе у станка.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WireCut
{
    public partial class MainForm
    {
        // 4 вкладки — каждая помещается в одну строку даже на мониторе станка 1024 пикселя
        static readonly string[] TabNames = { "ЧЕРТЁЖ", "ПРАВКА И РАЗМЕРЫ", "БИБЛИОТЕКА", "ЭРОЗИЯ" };
        /// <summary>Маленький монитор (как у станка): уже панель свойств, ниже командная строка.</summary>
        public static bool SmallScreen { get { try { Rectangle r = Screen.PrimaryScreen.WorkingArea; return r.Width < 1300 || r.Height < 860; } catch { return false; } } }
        Control tabRow;
        int popupTab = -1, popupIndex;
        FlowLayoutPanel[] ribbonPages;
        Label[] tabLabels;
        int ribbonTab;
        bool ribbonCollapsed;
        Panel leftPanel; Splitter leftSplit;
        Control simPanel;
        Label btnProps, btnRibbon;
        string UiPath { get { return Path.Combine(AppDir, "ui.ini"); } }

        /// <summary>Строка вкладок; страницы с группами — в ribbonPages (добавляются в окно отдельно).</summary>
        Control BuildRibbonTabs()
        {
            ribbonPages = new FlowLayoutPanel[TabNames.Length];
            tabLabels = new Label[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
                ribbonPages[i] = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Theme.Surface, Visible = i == 0 };

            Panel row = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.Background };
            FlowLayoutPanel tabs = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Background, Padding = new Padding(6, 0, 0, 0) };
            for (int i = 0; i < TabNames.Length; i++)
            {
                int k = i;
                Label t = new Label { Text = TabNames[i], AutoSize = false, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 2, 0), Cursor = Cursors.Hand, Font = new Font(UiFont, UiSize - 0.5f, FontStyle.Bold) };
                t.Width = TextRenderer.MeasureText(t.Text, t.Font).Width + 34;
                t.Click += delegate { SelectTab(k); };
                t.DoubleClick += delegate { ToggleRibbon(); };
                tip.SetToolTip(t, "Вкладка ленты. Двойной щелчок — свернуть / развернуть ленту (Ctrl+F1).");
                tabLabels[i] = t; tabs.Controls.Add(t);
            }
            FlowLayoutPanel right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Background, FlowDirection = FlowDirection.RightToLeft };
            btnRibbon = RowBtn("▲ Лента", "Свернуть / развернуть ленту (Ctrl+F1). Свёрнутая лента открывается щелчком по вкладке.", delegate { ToggleRibbon(); });
            btnProps = RowBtn("◧ Свойства", "Показать / скрыть панель свойств слева (F2) — чертёж станет шире.", delegate { ToggleProps(); });
            right.Controls.Add(btnRibbon); right.Controls.Add(btnProps);
            row.Controls.Add(tabs); row.Controls.Add(right);
            tabRow = row;
            PaintTabs();
            return row;
        }

        Label RowBtn(string t, string tipText, EventHandler h)
        {
            Label b = new Label { Text = t, AutoSize = false, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, BackColor = Theme.Background, Cursor = Cursors.Hand, Margin = new Padding(2, 0, 2, 0) };
            b.Width = TextRenderer.MeasureText(t, Font).Width + 20;
            b.Click += h; tip.SetToolTip(b, tipText);
            b.MouseEnter += delegate { b.BackColor = Theme.Raised; };
            b.MouseLeave += delegate { b.BackColor = Theme.Background; };
            return b;
        }

        void SelectTab(int k)
        {
            // свёрнутая лента: вкладка выпадает поверх чертежа и прячется после выбора команды (как в Office)
            if (ribbonCollapsed) { if (popupTab == k) HidePopup(); else ShowPopup(k); return; }
            ribbonTab = k;
            ApplyRibbon();
        }

        void ToggleRibbon() { HidePopup(); ribbonCollapsed = !ribbonCollapsed; ApplyRibbon(); }

        void ShowPopup(int k)
        {
            HidePopup();
            FlowLayoutPanel pg = ribbonPages[k];
            popupIndex = Controls.GetChildIndex(pg);
            pg.Dock = DockStyle.None;
            pg.MaximumSize = new Size(ClientSize.Width, 0);
            pg.Location = new Point(0, tabRow.Bottom);
            pg.Visible = true;
            pg.BringToFront();
            popupTab = k; ribbonTab = k;
            PaintTabs();
        }

        /// <summary>Спрятать выпавшую вкладку свёрнутой ленты.</summary>
        void HidePopup()
        {
            if (popupTab < 0 || ribbonPages == null) return;
            FlowLayoutPanel pg = ribbonPages[popupTab];
            popupTab = -1;
            pg.Visible = false;
            pg.Dock = DockStyle.Top;
            pg.MaximumSize = Size.Empty;
            Controls.SetChildIndex(pg, popupIndex);   // порядок прилипания — как был
            PaintTabs();
        }

        void ApplyRibbon()
        {
            HidePopup();
            SuspendLayout();
            for (int i = 0; i < ribbonPages.Length; i++) ribbonPages[i].Visible = !ribbonCollapsed && i == ribbonTab;
            ResumeLayout(true);
            PaintTabs();
        }

        void PaintTabs()
        {
            for (int i = 0; i < tabLabels.Length; i++)
            {
                bool on = i == ribbonTab && (!ribbonCollapsed || popupTab == i);
                tabLabels[i].BackColor = on ? Theme.Surface : Theme.Background;
                tabLabels[i].ForeColor = i == ribbonTab ? Theme.Accent : Theme.Muted;
            }
            if (btnRibbon != null) btnRibbon.Text = ribbonCollapsed ? "▼ Лента" : "▲ Лента";
            if (btnProps != null) btnProps.ForeColor = leftPanel == null || leftPanel.Visible ? Theme.Muted : Theme.Orange;
        }

        void ToggleProps()
        {
            bool v = !leftPanel.Visible;
            leftPanel.Visible = v; leftSplit.Visible = v;
            PaintTabs();
            Msg("Панель свойств: " + (v ? "показана" : "скрыта (F2 — показать)"));
        }

        /// <summary>Вид окна (вкладка, лента, панель свойств, шкала истории) — %APPDATA%\STZ_WireCut\ui.ini.</summary>
        void LoadUi()
        {
            try
            {
                if (!File.Exists(UiPath)) return;
                foreach (string line in File.ReadAllLines(UiPath))
                {
                    int e = line.IndexOf('='); if (e < 0) continue;
                    string k = line.Substring(0, e).Trim(), v = line.Substring(e + 1).Trim();
                    if (k == "tab") { int t; if (int.TryParse(v, out t) && t >= 0 && t < TabNames.Length) ribbonTab = t; }
                    if (k == "tabs") tabsSaved = v;
                    if (k == "ribbon") ribbonCollapsed = v == "0";
                    if (k == "props" && v == "0") { leftPanel.Visible = false; leftSplit.Visible = false; }
                    if (k == "propsw") { int w; if (int.TryParse(v, out w) && w >= 250 && w <= 900) leftPanel.Width = w; }
                    if (k == "history" && v == "0" && histPanel != null) histPanel.Visible = false;
                }
                if (tabsSaved != "4" && ribbonTab == 2) ribbonTab = 3;   // в 2.6–2.9 «ЭРОЗИЯ» была третьей вкладкой
                ApplyRibbon();
            }
            catch { }
        }
        string tabsSaved = "";

        void SaveUi()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                File.WriteAllLines(UiPath, new string[] {
                    "tabs=4", "tab=" + ribbonTab, "ribbon=" + (ribbonCollapsed ? "0" : "1"),
                    "props=" + (leftPanel.Visible ? "1" : "0"), "propsw=" + leftPanel.Width,
                    "history=" + (histPanel != null && !histPanel.Visible ? "0" : "1") });
            }
            catch { }
        }
    }
}
