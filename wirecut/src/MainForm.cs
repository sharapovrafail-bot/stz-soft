// STZ WireCut — главное окно: меню, панель свойств, чертёжное поле, командная строка
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WireCut
{
    public partial class MainForm : Form
    {
        public const float UiSize = 10.5f;   // крупный шрифт — для маленького монитора у станка
#if NET48
        public const string Ver = "3.4 (Win10/11)";
        public const string UiFont = "Segoe UI";
#else
        public const string Ver = "3.4 (Win7)";
        public const string UiFont = "Tahoma";
#endif
        public CadCanvas Cv;
        public JobSettings S = new JobSettings();
        PropertyGrid grid;
        RichTextBox hist;
        TextBox input;
        ListBox suggestions;
        Label promptLbl;
        ToolStripStatusLabel stCoord, stSnap, stGrid, stOrtho, stOsnap, stInfo;
        ComboBox simSpeed; TrackBar simBar; Label simInfo;
        Timer simTimer;
        public Cmd Active;
        string lastCmd = "";
        string curFile = "";
        public static string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STZ_WireCut");
        string SettingsPath { get { return Path.Combine(AppDir, "settings.ini"); } }

        public MainForm(string openFile)
        {
#if NET48
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;   // чёткий интерфейс при масштабе 125–200 %
#endif
            Text = "STZ WireCut " + Ver + " — электроэрозионная вырезка";
            Size = new Size(1280, 820);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            Font = new Font(UiFont, UiSize);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            try { Directory.CreateDirectory(AppDir); S.Load(SettingsPath); } catch { }

            BackColor = Theme.Background; ForeColor = Theme.Text; DoubleBuffered = true;
            MinimumSize = new Size(800, 560);   // монитор станка бывает 1024×768
            using (BrandForm splash = new BrandForm(true)) splash.ShowDialog();
            BuildUi();
            LoadUi();
            Hist.Commit(Cv, "Начало", "НОВЫЙ"); HistChanged();
            AcadLink.StartWatch(this);
            StartOpenRequestWatch();
            InitLeadEdit();
            Shown += delegate
            {
                Cv.ZoomExtents();
                Msg("STZ WireCut " + Ver + ". Введите команду или ? для списка команд.");
                if (!string.IsNullOrEmpty(openFile) && File.Exists(openFile)) { if (IsStzFile(openFile)) ImportStz(openFile); else ImportDrawing(openFile, true); }
                input.Focus();
            };
            FormClosing += delegate { try { S.Save(SettingsPath); } catch { } SaveUi(); };
#if NET48
            ResumeLayout(true);
#endif
        }

        // ================= интерфейс =================
        void BuildUi()
        {
            // --- меню ---
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem mFile = new ToolStripMenuItem("Файл");
            mFile.DropDownItems.Add(Mi("Новый", Keys.Control | Keys.N, delegate { Run("НОВЫЙ"); }));
            mFile.DropDownItems.Add(Mi("Открыть DXF / DWG...", Keys.Control | Keys.O, delegate { Run("ОТКРЫТЬ"); }));
            mFile.DropDownItems.Add(Mi("Добавить DXF / DWG в чертёж...", Keys.None, delegate { OpenDxf(false); }));
            mFile.DropDownItems.Add(Mi("Сохранить DXF...", Keys.Control | Keys.S, delegate { Run("СОХРАНИТЬ"); }));
            mFile.DropDownItems.Add(Mi("Экспорт траектории в DXF...", Keys.None, delegate { ExportPathDxf(); }));
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(Mi("Сохранить параметры как...", Keys.None, delegate { SaveParamsAs(); }));
            mFile.DropDownItems.Add(Mi("Загрузить параметры...", Keys.None, delegate { LoadParams(); }));
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(Mi("Выход", Keys.Alt | Keys.F4, delegate { Close(); }));

            ToolStripMenuItem mEdit = new ToolStripMenuItem("Правка");
            miUndo = Mi("Отменить", Keys.None, delegate { Run("ОТМЕНИТЬ"); }); miUndo.ShortcutKeyDisplayString = "Ctrl+Z";
            miRedo = Mi("Повторить", Keys.None, delegate { Run("ПОВТОРИТЬ"); }); miRedo.ShortcutKeyDisplayString = "Ctrl+Y";
            mEdit.DropDownItems.Add(miUndo); mEdit.DropDownItems.Add(miRedo);
            mEdit.DropDownItems.Add(Mi("Шкала истории (показать / скрыть)", Keys.None, delegate { Run("ИСТОРИЯ"); }));
            mEdit.DropDownItems.Add(new ToolStripSeparator());
            mEdit.DropDownItems.Add(Mi("Удалить выбранное   (Del)", Keys.None, delegate { Run("СОТРИ"); }));
            mEdit.DropDownItems.Add(Mi("Выделить всё", Keys.None, delegate { foreach (Seg s in Cv.Ents) s.Sel = true; Cv.Invalidate(); }));

            ToolStripMenuItem mDraw = new ToolStripMenuItem("Рисование");
            mDraw.DropDownItems.Add(Mi("Отрезок   (ОТРЕЗОК / L)", Keys.None, delegate { Run("ОТРЕЗОК"); }));
            mDraw.DropDownItems.Add(Mi("Круг   (КРУГ / C)", Keys.None, delegate { Run("КРУГ"); }));
            mDraw.DropDownItems.Add(Mi("Дуга по 3 точкам   (ДУГА / A)", Keys.None, delegate { Run("ДУГА"); }));
            mDraw.DropDownItems.Add(Mi("Прямоугольник   (ПРЯМОУГ / REC)", Keys.None, delegate { Run("ПРЯМОУГ"); }));

            ToolStripMenuItem mMod = new ToolStripMenuItem("Редактировать");
            mMod.DropDownItems.Add(Mi("Перенести   (ПЕРЕНЕСТИ / M)", Keys.None, delegate { Run("ПЕРЕНЕСТИ"); }));
            mMod.DropDownItems.Add(Mi("Копировать   (КОПИРОВАТЬ / CO)", Keys.None, delegate { Run("КОПИРОВАТЬ"); }));
            mMod.DropDownItems.Add(Mi("Повернуть   (ПОВЕРНУТЬ / RO)", Keys.None, delegate { Run("ПОВЕРНУТЬ"); }));
            mMod.DropDownItems.Add(Mi("Зеркало   (ЗЕРКАЛО / MI)", Keys.None, delegate { Run("ЗЕРКАЛО"); }));
            mMod.DropDownItems.Add(Mi("Масштаб   (МАСШТАБ / SC)", Keys.None, delegate { Run("МАСШТАБ"); }));
            mMod.DropDownItems.Add(Mi("Стереть   (СОТРИ / E)", Keys.None, delegate { Run("СОТРИ"); }));
            mMod.DropDownItems.Add(Mi("Подобие   (ПОДОБИЕ / O)", Keys.None, delegate { Run("ПОДОБИЕ"); }));
            mMod.DropDownItems.Add(Mi("Обрезать   (ОБРЕЗАТЬ / TR)", Keys.None, delegate { Run("ОБРЕЗАТЬ"); }));
            mMod.DropDownItems.Add(Mi("Удлинить   (УДЛИНИТЬ / EX)", Keys.None, delegate { Run("УДЛИНИТЬ"); }));
            mMod.DropDownItems.Add(Mi("Сопряжение   (СОПРЯЖЕНИЕ / F)", Keys.None, delegate { Run("СОПРЯЖЕНИЕ"); }));
            mMod.DropDownItems.Add(Mi("Фаска   (ФАСКА / CHA)", Keys.None, delegate { Run("ФАСКА"); }));
            mMod.DropDownItems.Add(Mi("Массив   (МАССИВ / AR)", Keys.None, delegate { Run("МАССИВ"); }));
            mMod.DropDownItems.Add(new ToolStripSeparator());
            mMod.DropDownItems.Add(Mi("Проверить чертёж   (ПРОВЕРКА)", Keys.None, delegate { Run("ПРОВЕРКА"); }));
            mMod.DropDownItems.Add(Mi("Починить чертёж   (ПОЧИНИТЬ)", Keys.None, delegate { Run("ПОЧИНИТЬ"); }));
            mMod.DropDownItems.Add(new ToolStripSeparator());
            mMod.DropDownItems.Add(Mi("Расстояние   (ДИСТ / DI)", Keys.None, delegate { Run("ДИСТ"); }));
            mMod.DropDownItems.Add(Mi("Координаты точки   (ID)", Keys.None, delegate { Run("ID"); }));

            ToolStripMenuItem mDim = new ToolStripMenuItem("Размеры");
            mDim.DropDownItems.Add(Mi("Линейный размер   (РАЗМЕР / DIM)", Keys.None, delegate { Run("РАЗМЕР"); }));
            mDim.DropDownItems.Add(Mi("Радиус   (РАДИУС)", Keys.None, delegate { Run("РАДИУС"); }));
            mDim.DropDownItems.Add(Mi("Диаметр   (ДИАМЕТР)", Keys.None, delegate { Run("ДИАМЕТР"); }));
            mDim.DropDownItems.Add(Mi("Угол   (УГОЛ)", Keys.None, delegate { Run("УГОЛ"); }));
            mDim.DropDownItems.Add(new ToolStripSeparator());
            mDim.DropDownItems.Add(Mi("Габаритные размеры вкл/выкл   (ГАБАРИТ)", Keys.F12, delegate { Run("ГАБАРИТ"); }));
            mDim.DropDownItems.Add(Mi("Линейки вкл/выкл   (ЛИНЕЙКИ)", Keys.F11, delegate { Run("ЛИНЕЙКИ"); }));
            mDim.DropDownItems.Add(Mi("Удалить все размеры", Keys.None, delegate { Run("УДАЛИТЬРАЗМЕРЫ"); }));
            ToolStripMenuItem mView = new ToolStripMenuItem("Вид");
            mView.DropDownItems.Add(Mi("Показать всё", Keys.None, delegate { Cv.ZoomExtents(); }));
            mView.DropDownItems.Add(Mi("Панель свойств — показать / скрыть", Keys.F2, delegate { ToggleProps(); }));
            mView.DropDownItems.Add(Mi("Лента — свернуть / развернуть", Keys.Control | Keys.F1, delegate { ToggleRibbon(); }));
            mView.DropDownItems.Add(Mi("Шкала истории — показать / скрыть", Keys.None, delegate { Run("ИСТОРИЯ"); }));
            mView.DropDownItems.Add(new ToolStripSeparator());
            mView.DropDownItems.Add(Mi("Сетка (F7)", Keys.None, delegate { Toggle(Keys.F7); }));
            mView.DropDownItems.Add(Mi("Шаговая привязка (F9)", Keys.None, delegate { Toggle(Keys.F9); }));
            mView.DropDownItems.Add(Mi("Орто (F8)", Keys.None, delegate { Toggle(Keys.F8); }));
            mView.DropDownItems.Add(Mi("Объектная привязка (F3)", Keys.None, delegate { Toggle(Keys.F3); }));

            ToolStripMenuItem mEdm = new ToolStripMenuItem("Эрозия");
            mEdm.DropDownItems.Add(Mi("Точка заправки   (СТАРТ)", Keys.None, delegate { Run("СТАРТ"); }));
            mEdm.DropDownItems.Add(Mi("Заход снаружи / изнутри для контура...   (ЗАХОД)", Keys.None, delegate { Run("ЗАХОД"); }));
            mEdm.DropDownItems.Add(Mi("Сбросить точки заправки и стороны захода", Keys.None, delegate { Run("СБРОС"); }));
            mEdm.DropDownItems.Add(Mi("Построить траекторию   (ПУТЬ)", Keys.F5, delegate { Run("ПУТЬ"); }));
            mEdm.DropDownItems.Add(Mi("Симуляция   (СИМ)", Keys.F6, delegate { Run("СИМ"); }));
            mEdm.DropDownItems.Add(Mi("Создать УП   (УП)", Keys.F10, delegate { Run("УП"); }));
            mEdm.DropDownItems.Add(Mi("Отправить на станок сразу   (НАСТАНОК)", Keys.Control | Keys.F10, delegate { Run("НАСТАНОК"); }));
            mEdm.DropDownItems.Add(Mi("Папка станка (куда писать УП)...   (ПАПКА)", Keys.None, delegate { Run("ПАПКА"); }));
            mEdm.DropDownItems.Add(new ToolStripSeparator());
            mEdm.DropDownItems.Add(Mi("Режимы резания (материал × толщина)...   (РЕЖИМЫ)", Keys.F4, delegate { Run("РЕЖИМЫ"); }));
            mEdm.DropDownItems.Add(Mi("Открыть УП из файла и отправить...", Keys.None, delegate { SendExistingFile(); }));

            ToolStripMenuItem mLib = new ToolStripMenuItem("Библиотека");
            mLib.DropDownItems.Add(Mi("Шестерня прямозубая...   (ШЕСТЕРНЯ)", Keys.None, delegate { Run("ШЕСТЕРНЯ"); }));
            mLib.DropDownItems.Add(Mi("Отверстие со шпоночным пазом (ГОСТ 23360)...   (ШПОНКА)", Keys.None, delegate { Run("ШПОНКА"); }));
            mLib.DropDownItems.Add(Mi("Шлицы прямобочные / эвольвентные...   (ШЛИЦЫ)", Keys.None, delegate { Run("ШЛИЦЫ"); }));
            mLib.DropDownItems.Add(new ToolStripSeparator());
            mLib.DropDownItems.Add(Mi("Колесо из «STZ Расчёт зубьев» (.stzgear)...   (ИЗSTZ)", Keys.None, delegate { Run("ИЗSTZ"); }));
            ToolStripMenuItem mAcad = new ToolStripMenuItem("AutoCAD");
            mAcad.DropDownItems.Add(Mi("Установить команду WCUT в AutoCAD...", Keys.None, delegate { AcadLink.Install(this); }));
            mAcad.DropDownItems.Add(Mi("Загрузить последнее из AutoCAD", Keys.None, delegate { if (File.Exists(AcadLink.ExchangeFile)) ImportDxf(AcadLink.ExchangeFile, true); else Msg("Файл обмена ещё не создан — выполните WCUT в AutoCAD."); }));
            mAcad.DropDownItems.Add(Mi("Открыть папку обмена", Keys.None, delegate { Directory.CreateDirectory(AppDir); System.Diagnostics.Process.Start(AppDir); }));

            ToolStripMenuItem mHelp = new ToolStripMenuItem("Справка");
            mHelp.DropDownItems.Add(Mi("Руководство пользователя", Keys.F1, delegate { OpenManual(); }));
            mHelp.DropDownItems.Add(Mi("Что нового в версии " + Ver.Split(' ')[0] + "...", Keys.None, delegate { WhatsNew(); }));
            mHelp.DropDownItems.Add(Mi("Список команд в командной строке", Keys.None, delegate { Run("?"); }));
            mHelp.DropDownItems.Add(Mi("О программе", Keys.None, delegate { About(); }));

            menu.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mDraw, mMod, mDim, mView, mEdm, mLib, mAcad, mHelp });

            // --- панель инструментов ---
            // лента: группы переносятся на вторую строку, если экран узкий
            // вкладки как во Fusion 360: на экране одна строка групп, а не три — больше места под чертёж
            Control ribbon = BuildRibbonTabs();
            FlowLayoutPanel groups = ribbonPages[0];
            SmallGroup(groups, "Файл", 1, new string[,] { { "Новый", "НОВЫЙ" }, { "Открыть", "ОТКРЫТЬ" }, { "Сохранить", "СОХРАНИТЬ" } });
            SmallGroup(groups, "История", 1, new string[,] { { "Назад", "ОТМЕНИТЬ" }, { "Вперёд", "ПОВТОРИТЬ" }, { "Шкала", "ИСТОРИЯ" } });
            SmallGroup(groups, "Рисование", 2, new string[,] { { "Отрезок", "ОТРЕЗОК" }, { "Круг", "КРУГ" }, { "Дуга", "ДУГА" }, { "Прямоуг.", "ПРЯМОУГ" }, { "Подобие", "ПОДОБИЕ" }, { "Массив", "МАССИВ" } });
            // компактная панель 3×3, как «Редактирование» в AutoCAD
            groups = ribbonPages[1];
            SmallGroup(groups, "Редактирование", 3, new string[,] { { "Стереть", "СОТРИ" }, { "Перенести", "ПЕРЕНЕСТИ" }, { "Копировать", "КОПИРОВАТЬ" },
                             { "Повернуть", "ПОВЕРНУТЬ" }, { "Зеркало", "ЗЕРКАЛО" }, { "Масштаб", "МАСШТАБ" },
                             { "Расстояние", "ДИСТ" }, { "Показать всё", "ПОКАЗАТЬ" }, { "Ноль X0 Y0", "НОЛЬ" } });
            groups = ribbonPages[0];
            SmallGroup(groups, "Изменить", 2, new string[,] { { "Обрезать", "ОБРЕЗАТЬ" }, { "Удлинить", "УДЛИНИТЬ" }, { "Сопряжение", "СОПРЯЖЕНИЕ" }, { "Фаска", "ФАСКА" }, { "Проверить", "ПРОВЕРКА" }, { "Починить", "ПОЧИНИТЬ" } });
            groups = ribbonPages[1];
            SmallGroup(groups, "Размеры", 3, new string[,] { { "Линейный", "РАЗМЕР" }, { "Радиус", "РАДИУС" }, { "Диаметр", "ДИАМЕТР" }, { "Угол", "УГОЛ" }, { "Габарит", "ГАБАРИТ" }, { "Линейки", "ЛИНЕЙКИ" }, { "Удалить разм.", "УДАЛИТЬРАЗМЕРЫ" }, { "Авторазмеры", "АВТОРАЗМЕРЫ" } });
            groups = ribbonPages[2];
            SmallGroup(groups, "Библиотека", 2, new string[,] { { "Шестерня", "ШЕСТЕРНЯ" }, { "Шлицы", "ШЛИЦЫ" }, { "Шпон. паз", "ШПОНКА" }, { "Из STZ", "ИЗSTZ" } });
            groups = ribbonPages[3];
            SmallGroup(groups, "Заход проволоки", 1, new string[,] { { "Вход/выход", "ВХОД" }, { "Заход С / И", "ЗАХОД" }, { "Сброс", "СБРОС" } });
            ToolStrip edmBar = RibbonGroup(groups, "Эрозия");
            edmBar.Items.Add(Tb("Заправка", "СТАРТ")); edmBar.Items.Add(Tb("Траектория", "ПУТЬ"));
            edmBar.Items.Add(Tb("Режимы", "РЕЖИМЫ")); edmBar.Items.Add(Tb("Симуляция", "СИМ")); edmBar.Items.Add(Tb("Создать УП", "УП")); edmBar.Items.Add(Tb("На станок", "НАСТАНОК"));
            Theme.Strip(menu);

            // --- статус ---
            StatusStrip ss = new StatusStrip { Height = 32, SizingGrip = false, ShowItemToolTips = true };
            Theme.Strip(ss);
            stCoord = new ToolStripStatusLabel("X 0.000   Y 0.000") { AutoSize = false, Width = 190, Margin = new Padding(16, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
            stSnap = StBtn("ШАГ", Keys.F9); stGrid = StBtn("СЕТКА", Keys.F7); stOrtho = StBtn("ОРТО", Keys.F8); stOsnap = StBtn("ПРИВЯЗКА", Keys.F3);
            stInfo = new ToolStripStatusLabel("") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            ss.Items.AddRange(new ToolStripItem[] { stSnap, stGrid, stOrtho, stOsnap, stInfo, stCoord });

            // --- командная строка ---
            Panel cmdPanel = new Panel { Dock = DockStyle.Bottom, Height = SmallScreen ? 74 : 100, BackColor = Theme.Surface };
            hist = new RichTextBox { DetectUrls = false, Multiline = true, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Theme.Surface, ForeColor = Theme.Muted, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 11f) };
            Panel inRow = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Theme.Background };
            promptLbl = new Label { Text = "Команда:", Dock = DockStyle.Left, AutoSize = true, ForeColor = Theme.Accent, Font = new Font("Consolas", 12f), Padding = new Padding(4, 5, 2, 0) };
            input = new TextBox { Dock = DockStyle.Fill, BackColor = Theme.Background, ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 12.5f) };
            inRow.Controls.Add(input); inRow.Controls.Add(promptLbl);
            cmdPanel.Controls.Add(hist); cmdPanel.Controls.Add(inRow);
            suggestions = new ListBox { Visible = false, IntegralHeight = false, Height = 150, Width = 250, BackColor = Theme.Raised, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = input.Font };
            Controls.Add(suggestions);
            suggestions.MouseClick += delegate { CompleteCommand(); input.Focus(); };
            input.TextChanged += delegate { UpdateSuggestions(); };
            input.LostFocus += delegate { if (!suggestions.Focused) suggestions.Hide(); };
            Resize += delegate { suggestions.Hide(); };
            input.KeyDown += InputKeyDown;
            input.TextChanged += delegate { if (Active != null && Active.ShowsDims) Cv.Invalidate(); };
            input.KeyPress += delegate (object o, KeyPressEventArgs e) { if (e.KeyChar == ' ' || e.KeyChar == '\r' || e.KeyChar == (char)27) e.Handled = true; };

            // --- левая панель свойств ---
            Panel left = new Panel { Dock = DockStyle.Left, Width = SmallScreen ? 330 : 430, BackColor = Theme.Surface };
            leftPanel = left;
            grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = S, PropertySort = PropertySort.Categorized, ToolbarVisible = false, HelpVisible = true };
            grid.BackColor = Theme.Surface; grid.ForeColor = Theme.Text; grid.ViewBackColor = Theme.Background; grid.ViewForeColor = Theme.Text;
            grid.HelpBackColor = Theme.Surface; grid.HelpForeColor = Theme.Muted; grid.LineColor = Theme.Raised; grid.CategoryForeColor = Theme.Accent;
            grid.PropertyValueChanged += GridChanged;
            // «Траектория» и «Симуляция» — на ленте; здесь только главное, чтобы свойствам хватало места на маленьком мониторе
            FlowLayoutPanel btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(4), FlowDirection = FlowDirection.LeftToRight };
            btns.Controls.Add(BigBtn("Создать УП", Theme.Green, delegate { Run("УП"); }));
            btns.Controls.Add(BigBtn("На станок ▶", Theme.Orange, delegate { Run("НАСТАНОК"); }));
            btns.Resize += delegate { int bw = Math.Max(100, (btns.ClientSize.Width - btns.Padding.Horizontal - 14) / 2); foreach (Control bb in btns.Controls) bb.Width = bw; };
            Label brand = new Label { Dock = DockStyle.Bottom, Height = 44, Text = Theme.Brand, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter };
            left.Controls.Add(grid); left.Controls.Add(new Label { Text = "СВОЙСТВА", Dock = DockStyle.Top, Height = 26, ForeColor = Theme.Muted, Padding = new Padding(12, 5, 0, 0) }); left.Controls.Add(btns); left.Controls.Add(brand);
            Splitter split = new Splitter { Dock = DockStyle.Left, Width = 4, BackColor = Theme.Border };
            leftSplit = split;

            // --- чертёжное поле ---
            Cv = new CadCanvas { Dock = DockStyle.Fill };
            Cv.CommandActive = delegate { return Active != null; };
            Cv.OnDimPick = delegate (Dim d) { Start(new DimEditCmd(d)); input.Focus(); return true; };
            Cv.OnPick = delegate (V2 p) { if (Active != null) { Active.Point(Ortho(p)); RefreshPrompt(); } return true; };
            Cv.OnRubber = delegate (Graphics g, V2 c) { if (Active != null) { Active.Rubber(g, Ortho(c)); if (Active.Last.HasValue && !Active.ShowsDims) { V2 d = Ortho(c) - Active.Last.Value; Cv.RubberHint = "Длина " + d.Len.ToString("0.###", CultureInfo.InvariantCulture) + " мм · угол " + (Math.Atan2(d.Y, d.X) * 180 / Math.PI).ToString("0.##", CultureInfo.InvariantCulture) + "°"; } else Cv.RubberHint = ""; } else Cv.RubberHint = ""; };
            Cv.OnMoveWorld = delegate (V2 w, int z) { stCoord.Text = "X " + Geo.F(Math.Round(w.X, 3)) + "   Y " + Geo.F(Math.Round(w.Y, 3)); };
            Cv.OnKey = delegate (KeyEventArgs e, int z) { CanvasKey(e); };
            Cv.OnChar = delegate (char ch, int z)
            {
                if (ch > ' ' && ch != (char)127) { input.Focus(); input.AppendText(ch.ToString()); input.SelectionStart = input.Text.Length; }
            };

            // центр: панель симуляции над чертежом + чертёжное поле
            Panel center = new Panel { Dock = DockStyle.Fill };
            center.Controls.Add(Cv);
            center.Controls.Add(BuildSimBar());
            center.Controls.Add(BuildHistoryBar());
            Controls.Add(center);
            Controls.Add(split);
            Controls.Add(left);
            Controls.Add(cmdPanel);
            foreach (FlowLayoutPanel pg in ribbonPages) Controls.Add(pg);   // страницы вкладок — под строкой вкладок
            Cv.MouseDown += delegate { HidePopup(); };
            Controls.Add(ribbon);
            Controls.Add(menu);
            Controls.Add(ss);
            MainMenuStrip = menu;
            UpdateToggles();

            simTimer = new Timer { Interval = 30 };
            simTimer.Tick += SimTick;
        }


        ToolStrip RibbonGroup(FlowLayoutPanel parent, string title)
        {
            Panel group = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0,0,1,0), Padding = new Padding(3,0,3,0), BackColor = Theme.Surface };
            ToolStrip strip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, AutoSize = true, MinimumSize = new Size(0, RibH), CanOverflow = false, LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow };
            Theme.Strip(strip);
            Label label = new Label { Text = title, Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted };
            group.Controls.Add(strip); group.Controls.Add(label); parent.Controls.Add(group);
            strip.SizeChanged += delegate { group.MinimumSize = new Size((strip.AutoSize ? strip.PreferredSize.Width : strip.Width) + 6, RibH + 26); };
            return strip;
        }
        static readonly string[] CommandNames = { "L", "LINE", "ОТРЕЗОК", "О", "PL", "PLINE", "C", "CIRCLE", "КРУГ", "К", "A", "ARC", "ДУГА", "Д", "REC", "RECTANG", "ПРЯМОУГ", "ПР", "E", "ERASE", "СОТРИ", "С", "СТЕРЕТЬ", "M", "MOVE", "ПЕРЕНЕСТИ", "ПЕР", "CO", "CP", "COPY", "КОПИРОВАТЬ", "КОП", "RO", "ROTATE", "ПОВЕРНУТЬ", "ПОВ", "MI", "MIRROR", "ЗЕРКАЛО", "ЗЕР", "SC", "SCALE", "МАСШТАБ", "МАС", "DI", "DIST", "ДИСТ", "ID", "ST", "START", "СТАРТ", "ЗАПРАВКА", "Z", "ZE", "ZOOM", "ПОКАЗАТЬ", "ПОК", "U", "UNDO", "ОТМЕНИТЬ", "ОТМ", "REDO", "ПОВТОРИТЬ", "ВЕРНУТЬ", "ИСТОРИЯ", "HISTORY", "P", "PATH", "ПУТЬ", "ТРАЕКТОРИЯ", "SIM", "СИМ", "СИМУЛЯЦИЯ", "UP", "POST", "УП", "OPEN", "ОТКРЫТЬ", "IMPORT", "SAVE", "СОХРАНИТЬ", "NEW", "НОВЫЙ", "WCUT", "ACAD", "AUTOCAD", "?", "HELP", "СПРАВКА", "GEAR", "ШЕСТЕРНЯ", "KEY", "KEYWAY", "ШПОНКА", "ПАЗ", "SPLINE", "ШЛИЦЫ", "O", "OFFSET", "ПОДОБИЕ", "TR", "TRIM", "ОБРЕЗАТЬ", "EX", "EXTEND", "УДЛИНИТЬ", "F", "FILLET", "СОПРЯЖЕНИЕ", "CHA", "CHAMFER", "ФАСКА", "AR", "ARRAY", "МАССИВ", "CHECK", "ПРОВЕРКА", "REPAIR", "ПОЧИНИТЬ", "РЕЖИМЫ", "REGIME", "РАЗМЕР", "DIM", "РАДИУС", "ДИАМЕТР", "УГОЛ", "ГАБАРИТ", "ЛИНЕЙКИ", "УДАЛИТЬРАЗМЕРЫ", "АВТОРАЗМЕРЫ", "ЗАХОД", "LEAD", "СБРОС", "RESETSTART", "ВХОД", "ВЫХОД", "ВХОДВЫХОД", "LEADIN", "НОЛЬ", "ORIGIN", "ИЗSTZ", "STZ", "НАСТАНОК", "SEND", "ОТПРАВИТЬ", "ПАПКА", "FOLDER" };
        void UpdateSuggestions()
        {
            suggestions.Items.Clear(); string prefix = input.Text.Trim();
            if (Active != null || prefix.Length == 0 || !input.Focused) { suggestions.Hide(); return; }
            foreach (string name in CommandNames) if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) suggestions.Items.Add(name);
            if (suggestions.Items.Count == 0) { suggestions.Hide(); return; }
            suggestions.Height = Math.Min(150, suggestions.ItemHeight * suggestions.Items.Count + 4);
            Point pos = PointToClient(input.PointToScreen(Point.Empty)); suggestions.Location = new Point(pos.X, pos.Y - suggestions.Height - 3);
            suggestions.SelectedIndex = 0; suggestions.Show(); suggestions.BringToFront();
        }
        void CompleteCommand()
        {
            if (suggestions.SelectedItem == null) return;
            string name = suggestions.SelectedItem.ToString(); input.Text = name; input.SelectionStart = input.Text.Length; suggestions.Hide();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab && input != null && input.Focused && suggestions.Visible)
            { CompleteCommand(); return true; }
            // Tab — следующее поле размера (как во Fusion 360)
            if (keyData == Keys.Tab && Active != null && Active.Tab()) { RefreshPrompt(); return true; }
            // история: Ctrl+Z — назад, Ctrl+Y / Ctrl+Shift+Z — вперёд (работает и в командной строке)
            if (keyData == (Keys.Control | Keys.Z)) { Run("ОТМЕНИТЬ"); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Run("ПОВТОРИТЬ"); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (simTimer != null) simTimer.Dispose();
            base.OnFormClosed(e);
        }

        ToolStripMenuItem Mi(string t, Keys k, EventHandler h)
        {
            ToolStripMenuItem m = new ToolStripMenuItem(t, null, h); m.ForeColor = Theme.Text;
            if (k != Keys.None) { try { m.ShortcutKeys = k; } catch { m.ShortcutKeyDisplayString = k.ToString(); } }
            return m;
        }
        ToolStripButton Tb(string t, string cmd)
        {
            ToolStripButton b = new ToolStripButton(t) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = Theme.Icon(cmd, 30), TextImageRelation = TextImageRelation.ImageAboveText, AutoSize = false, Width = Math.Max((cmd == "ПУТЬ" || cmd == "СИМ" || cmd == "УП") ? 104 : 80, TextRenderer.MeasureText(t, Font).Width + 18), Height = RibH - 4, ForeColor = cmd == "СТАРТ" ? Theme.Orange : Theme.Text, BackColor = cmd == "УП" ? Theme.Green : Theme.Surface, ImageScaling = ToolStripItemImageScaling.None };
            b.ToolTipText = Tips.For(t, cmd);
            b.Click += delegate { Run(cmd); };
            b.Disposed += delegate { if (b.Image != null) b.Image.Dispose(); };
            return b;
        }
        const int SmallW = 134, RibH = 90, SmallH = 29;
        /// <summary>Компактная группа ленты: маленькие кнопки в cols столбцов по 3 строки.</summary>
        void SmallGroup(FlowLayoutPanel groups, string title, int cols, string[,] items)
        {
            ToolStrip bar = RibbonGroup(groups, title);
            bar.AutoSize = false; bar.LayoutStyle = ToolStripLayoutStyle.Flow;
            bar.Size = new Size(cols * SmallW + 6, RibH); bar.MinimumSize = bar.Size;
            for (int i = 0; i < items.GetLength(0); i++) bar.Items.Add(TbSmall(items[i, 0], items[i, 1]));
            bar.Parent.MinimumSize = new Size(bar.Width + 6, RibH + 26);
        }
        ToolStripButton TbSmall(string t, string cmd)
        {
            ToolStripButton b = new ToolStripButton(t) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = Theme.Icon(cmd, 20), TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Width = SmallW, Height = SmallH, ForeColor = Theme.Text, ImageScaling = ToolStripItemImageScaling.None, Margin = new Padding(0) };
            b.ToolTipText = Tips.For(t, cmd);
            b.Click += delegate { Run(cmd); };
            return b;
        }
        ToolStripStatusLabel StBtn(string t, Keys k)
        {
            ToolStripStatusLabel l = new StatusPill { Text = t, Padding = new Padding(9, 3, 9, 3), Margin = new Padding(2, 0, 2, 0), ToolTipText = Tips.Get(k.ToString()) + "\nЩёлкните, чтобы включить / выключить." };
            l.Click += delegate { Toggle(k); };
            return l;
        }
        ToolTip tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 200 };
        Button BigBtn(string t, Color c, EventHandler h)
        {
            Button b = new DarkButton { Text = t, Width = 204, Height = 52, BackColor = c, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(UiFont, UiSize + 0.5f, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            b.Click += h;
            tip.SetToolTip(b, Tips.Get(t == "Создать УП" ? "УП" : t.StartsWith("На станок") ? "НАСТАНОК" : "РЕЖИМЫ"));
            return b;
        }

        void Toggle(Keys k)
        {
            if (k == Keys.F7) Cv.GridOn = !Cv.GridOn;
            if (k == Keys.F8) Cv.OrthoOn = !Cv.OrthoOn;
            if (k == Keys.F9) Cv.SnapOn = !Cv.SnapOn;
            if (k == Keys.F3) Cv.OsnapOn = !Cv.OsnapOn;
            UpdateToggles(); Cv.Invalidate();
        }
        void UpdateToggles()
        {
            ((StatusPill)stGrid).Active = Cv.GridOn; ((StatusPill)stOrtho).Active = Cv.OrthoOn;
            ((StatusPill)stSnap).Active = Cv.SnapOn; ((StatusPill)stOsnap).Active = Cv.OsnapOn;
            stGrid.Invalidate(); stOrtho.Invalidate(); stSnap.Invalidate(); stOsnap.Invalidate();
        }

        void GridChanged(object o, PropertyValueChangedEventArgs e)
        {
            string name = e.ChangedItem.PropertyDescriptor != null ? e.ChangedItem.PropertyDescriptor.Name : "";
            bool inPass = e.ChangedItem.PropertyDescriptor != null && e.ChangedItem.PropertyDescriptor.ComponentType == typeof(PassParam);
            if (inPass)
            {
                if (!S.RegimeManual) { S.RegimeManual = true; grid.Refresh(); Msg("Режимы — ВРУЧНУЮ: ваши значения проходов больше не заменяются таблицей при смене материала / толщины. Вернуть таблицу — «Режимы вручную = Нет»."); }
            }
            else if (name == "RegimeManual")
            {
                if (S.RegimeManual) Msg("Режимы вручную: задайте Ton, Toff, ток, трубки, V-F в «Проход 1…4» — программа их не меняет.");
                else { Presets.Apply(S); grid.Refresh(); Msg("Режимы снова из таблицы (материал × толщина, F4)."); }
            }
            else if (name == "Folder")
            {
                Msg(S.Folder == "" ? "Папка станка не задана — «На станок» спросит её." : Directory.Exists(S.Folder) ? "Папка станка: " + S.Folder : "Внимание: папки «" + S.Folder + "» на этом компьютере нет — «На станок» попросит выбрать.");
            }
            else if (name == "Profile")
            {
                Presets.ApplyProfile(S); grid.Refresh();
                Msg("Профиль станка: " + RusEnumConverter.Text(S.Profile) + ". Формат " + RusEnumConverter.Text(S.Format) + ", папка: " + S.Folder);
            }
            else if ((name == "Machine" || name == "Thickness" || name == "Material") && S.RegimeManual)
            {
                Presets.Apply(S); grid.Refresh();
                Msg("Режимы вручную — проходы не изменены. Режим из таблицы: «Режимы вручную = Нет» или F4.");
            }
            else if (name == "Machine" || name == "Thickness" || name == "Material")
            {
                Presets.Apply(S); grid.Refresh();
                RegimeRow rr = RegimeDb.Find(S.Material, S.Thickness);
                Msg("Режим из таблицы: " + rr.Material + ", " + Geo.F(S.Thickness) + " мм — импульс " + Geo.F(rr.Ton) + " мкс, пауза ×" + Geo.F(rr.Ratio) + ", трубок " + rr.Tubes + ", V-F " + rr.VF + "  (Эрозия → Режимы резания, F4)");
            }
            try { S.Save(SettingsPath); } catch { }
            RebuildIfPath();
        }

        // ================= командная строка =================
        public void Msg(string t)
        {
            string lower = t.ToLowerInvariant();
            hist.SelectionStart = hist.TextLength; hist.SelectionLength = 0;
            hist.SelectionColor = lower.IndexOf("ошиб") >= 0 || lower.IndexOf("неверн") >= 0 || lower.IndexOf("неизвест") >= 0 ? Theme.Error : lower.IndexOf("внимание") >= 0 ? Theme.Orange : t.StartsWith("Команда:") ? Theme.Accent : Theme.Muted;
            hist.AppendText((hist.TextLength > 0 ? "\r\n" : "") + t);
            hist.SelectionStart = hist.TextLength; hist.ScrollToCaret();
        }

        void RefreshPrompt()
        {
            if (suggestions != null) suggestions.Hide();
            promptLbl.Text = Active != null ? Active.Prompt : "Команда:";
            Cv.Invalidate();
        }

        void InputKeyDown(object o, KeyEventArgs e)
        {
            if (suggestions.Visible && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)) { int d = e.KeyCode == Keys.Down ? 1 : -1; suggestions.SelectedIndex = Math.Max(0, Math.Min(suggestions.Items.Count - 1, suggestions.SelectedIndex + d)); e.SuppressKeyPress = true; return; }
            if (suggestions.Visible && e.KeyCode == Keys.Tab) { CompleteCommand(); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                string t = input.Text.Trim(); input.Text = "";
                Submit(t); Cv.Focus();
            }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; input.Text = ""; Cancel(); Cv.Focus(); }
            else if (e.KeyCode == Keys.Up && input.Text == "" && lastCmd != "") { input.Text = lastCmd; input.SelectionStart = input.Text.Length; e.SuppressKeyPress = true; }
        }

        void CanvasKey(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                case Keys.Space:
                    { string t = input.Text.Trim(); input.Text = ""; Submit(t); break; }
                case Keys.Escape: input.Text = ""; Cancel(); break;
                case Keys.Back: if (input.Text.Length > 0) input.Text = input.Text.Substring(0, input.Text.Length - 1); break;
                case Keys.F3: case Keys.F7: case Keys.F8: case Keys.F9: Toggle(e.KeyCode); break;
                case Keys.Delete: if (Active == null) Run("СОТРИ"); break;
                case Keys.A: if (e.Control) { foreach (Seg s in Cv.Ents) s.Sel = true; Cv.Invalidate(); } break;
            }
        }

        void Submit(string t)
        {
            if (Active == null)
            {
                if (t == "") { if (lastCmd != "") Run(lastCmd); return; }
                Run(t); return;
            }
            Msg(Active.Prompt + " " + t);
            if (t == "") { Active.Enter(); RefreshPrompt(); return; }
            if (Active.Text(t)) { RefreshPrompt(); return; }
            V2 p;
            if (ParsePoint(t, out p)) { Active.Point(p); RefreshPrompt(); return; }
            Msg("  Неверный ввод. Точка: x,y  @dx,dy  @длина<угол  или число (расстояние по курсору)");
        }

        bool ParsePoint(string t, out V2 p)
        {
            p = new V2();
            t = t.Replace(" ", "");
            bool rel = t.StartsWith("@");
            if (rel) t = t.Substring(1);
            V2 b = (rel && Active != null && Active.Last.HasValue) ? Active.Last.Value : new V2(0, 0);
            double x, y;
            int lt = t.IndexOf('<');
            if (lt > 0)
            {
                if (Num(t.Substring(0, lt), out x) && Num(t.Substring(lt + 1), out y))
                { p = (rel ? b : new V2()) + V2.Polar(y * Math.PI / 180, x); return true; }
                return false;
            }
            string[] a = t.Split(',');
            if (a.Length == 2 && Num(a[0], out x) && Num(a[1], out y)) { p = (rel ? b : new V2()) + new V2(x, y); return true; }
            // только число: «прямой ввод расстояния» по направлению курсора
            if (a.Length == 1 && Num(t, out x) && Active != null && Active.Last.HasValue)
            {
                V2 dir = (Ortho(Cv.CurW) - Active.Last.Value).Norm();
                if (dir.Len < 0.5) dir = new V2(1, 0);
                p = Active.Last.Value + dir * x; return true;
            }
            return false;
        }

        public static bool Num(string s, out double v)
        {
            return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        V2 Ortho(V2 p)
        {
            if (!Cv.OrthoOn || Active == null || !Active.Last.HasValue) return p;
            V2 l = Active.Last.Value;
            return Math.Abs(p.X - l.X) >= Math.Abs(p.Y - l.Y) ? new V2(p.X, l.Y) : new V2(l.X, p.Y);
        }

        public void Cancel()
        {
            HidePopup();
            Cv.EditDim = null;
            if (Active != null) { Msg("*Прервано*"); Active = null; Checkpoint(); opName = null; opCmd = null; }
            else foreach (Seg s in Cv.Ents) s.Sel = false;
            if (Cv.SimOn) SimStop();
            RefreshPrompt();
        }

        public void GeometryChanged() { RebuildIfPath(); Cv.Invalidate(); }
        public void EndCmd() { Cv.EditDim = null; Active = null; Checkpoint(); opName = null; opCmd = null; RefreshPrompt(); }

        public void Run(string raw)
        {
            HidePopup();   // выпавшая лента (свёрнутый режим) прячется после выбора команды
            if (Active != null) { Active = null; Cv.EditDim = null; Checkpoint(); }   // прерванная команда — её результат тоже шаг истории
            string c = raw.Trim().ToUpper();
            OpBegin(c);
            Msg("Команда: " + raw.Trim());
            lastCmd = raw.Trim();
            switch (c)
            {
                case "L": case "LINE": case "ОТРЕЗОК": case "О": case "PL": case "PLINE": Start(new LineCmd()); break;
                case "C": case "CIRCLE": case "КРУГ": case "К": Start(new CircleCmd()); break;
                case "A": case "ARC": case "ДУГА": case "Д": Start(new Arc3Cmd()); break;
                case "REC": case "RECTANG": case "ПРЯМОУГ": case "ПР": Start(new RectCmd()); break;
                case "E": case "ERASE": case "СОТРИ": case "С": case "СТЕРЕТЬ": Start(new EraseCmd()); break;
                case "M": case "MOVE": case "ПЕРЕНЕСТИ": case "ПЕР": Start(new MoveCmd(false)); break;
                case "CO": case "CP": case "COPY": case "КОПИРОВАТЬ": case "КОП": Start(new MoveCmd(true)); break;
                case "RO": case "ROTATE": case "ПОВЕРНУТЬ": case "ПОВ": Start(new RotateCmd()); break;
                case "MI": case "MIRROR": case "ЗЕРКАЛО": case "ЗЕР": Start(new MirrorCmd()); break;
                case "SC": case "SCALE": case "МАСШТАБ": case "МАС": Start(new ScaleCmd()); break;
                case "DI": case "DIST": case "ДИСТ": Start(new DistCmd()); break;
                case "ID": Start(new IdCmd()); break;
                case "ST": case "START": case "СТАРТ": case "ЗАПРАВКА": Start(new StartCmd()); break;
                case "Z": case "ZE": case "ZOOM": case "ПОКАЗАТЬ": case "ПОК": Cv.ZoomExtents(); break;
                case "U": case "UNDO": case "ОТМЕНИТЬ": case "ОТМ": Undo(); break;
                case "REDO": case "ПОВТОРИТЬ": case "ВЕРНУТЬ": case "ВПЕРЁД": Redo(); break;
                case "ИСТОРИЯ": case "HISTORY": case "HIST": ToggleHistory(); break;
                case "P": case "PATH": case "ПУТЬ": case "ТРАЕКТОРИЯ": BuildPath(true); break;
                case "SIM": case "СИМ": case "СИМУЛЯЦИЯ": Simulate(); break;
                case "UP": case "POST": case "УП": MakeProgram(); break;
                case "НАСТАНОК": case "SEND": case "ОТПРАВИТЬ": SendToMachine(); break;
                case "ПАПКА": case "FOLDER": AskMachineFolder(null); break;
                case "OPEN": case "ОТКРЫТЬ": case "IMPORT": OpenDxf(true); break;
                case "SAVE": case "СОХРАНИТЬ": SaveDxf(); break;
                case "NEW": case "НОВЫЙ": PushUndo(); Cv.Ents.Clear(); Cv.ManualStarts.Clear(); Cv.SideMarks.Clear(); Cv.LeadMarks.Clear(); Cv.Dims.Clear(); Cv.Path = null; curFile = ""; Cv.Invalidate(); Msg("Новый чертёж. Старый не потерян — «◄ Назад» (Ctrl+Z) или щелчок по шкале истории вернёт его."); break;
                case "WCUT": case "ACAD": case "AUTOCAD": AcadLink.Install(this); break;
                case "?": case "HELP": Help(); break;
                case "СПРАВКА": case "РУКОВОДСТВО": case "MANUAL": OpenManual(); break;
                case "GEAR": case "ШЕСТЕРНЯ": case "ШЕСТ": Library(0); break;
                case "KEY": case "KEYWAY": case "ШПОНКА": case "ПАЗ": Library(1); break;
                case "SPLINE": case "ШЛИЦЫ": case "ШЛИЦ": Library(2); break;
                case "РЕЖИМЫ": case "РЕЖИМ": case "REGIME": OpenRegimes(); break;
                case "ЗАХОД": case "СТОРОНА": case "LEAD": Start(new SideCmd()); break;
                case "СБРОС": case "СБРОСЗАПРАВКИ": case "RESETSTART": ResetMarks(); break;
                case "ВХОД": case "ВЫХОД": case "ВХОДВЫХОД": case "LEADIN": Start(new LeadCmd()); break;
                case "НОЛЬ": case "ORIGIN": case "НАЧАЛО": Start(new OriginCmd()); break;
                case "ИЗSTZ": case "STZ": case "ИЗ STZ": OpenStz(); break;
                case "РАЗМЕР": case "DIM": case "DLI": case "РЗ": Start(new DimLinearCmd()); break;
                case "РАДИУС": case "DRA": Start(new DimRadiusCmd(false)); break;
                case "ДИАМЕТР": case "DDI": Start(new DimRadiusCmd(true)); break;
                case "УГОЛ": case "DAN": Start(new DimAngleCmd()); break;
                case "ГАБАРИТ": case "EXTENTS": Cv.ExtentsOn = !Cv.ExtentsOn; Msg("Габаритные размеры: " + (Cv.ExtentsOn ? "показаны" : "скрыты")); Cv.Invalidate(); break;
                case "ЛИНЕЙКИ": case "RULERS": Cv.RulersOn = !Cv.RulersOn; Msg("Линейки: " + (Cv.RulersOn ? "включены" : "выключены")); Cv.Invalidate(); break;
                case "АВТОРАЗМЕРЫ": case "AUTODIM": Cv.AutoDims = !Cv.AutoDims; Msg("Размеры на новых объектах: " + (Cv.AutoDims ? "включены" : "выключены")); break;
                case "УДАЛИТЬРАЗМЕРЫ": case "DIMCLEAR": Msg("Удалено размеров: " + Cv.Dims.Count); Cv.Dims.Clear(); Cv.Invalidate(); break;
                case "O": case "OFFSET": case "ПОДОБИЕ": case "ПОД": Start(new OffsetCmd()); break;
                case "TR": case "TRIM": case "ОБРЕЗАТЬ": case "ОБР": Start(new TrimCmd()); break;
                case "EX": case "EXTEND": case "УДЛИНИТЬ": case "УДЛ": Start(new ExtendCmd()); break;
                case "F": case "FILLET": case "СОПРЯЖЕНИЕ": case "СОП": Start(new CornerCmd(false)); break;
                case "CHA": case "CHAMFER": case "ФАСКА": case "ФАС": Start(new CornerCmd(true)); break;
                case "AR": case "ARRAY": case "МАССИВ": case "МАС2": Start(new ArrayCmd()); break;
                case "CHECK": case "ПРОВЕРКА": case "ПРОВЕРИТЬ": CheckDrawing(); break;
                case "REPAIR": case "ПОЧИНИТЬ": case "ПОЧИНКА": Start(new RepairCmd()); break;
                default: Msg("Неизвестная команда «" + raw + "». Введите ? для списка."); break;
            }
            if (Active == null) { Checkpoint(); opName = null; opCmd = null; }
            RefreshPrompt();
        }

        void Start(Cmd c)
        {
            if (c is DimEditCmd) { opName = "Изменение размера"; opCmd = "РАЗМЕР"; }
            Cv.EditDim = null; c.F = this; Active = c; c.Begin(); if (Active != null) RefreshPrompt();
        }

        void Help()
        {
            Msg("КОМАНДЫ (рус / англ):  ОТРЕЗОК L · КРУГ C · ДУГА A · ПРЯМОУГ REC · СОТРИ E · ПЕРЕНЕСТИ M · КОПИРОВАТЬ CO · ПОВЕРНУТЬ RO · ЗЕРКАЛО MI · МАСШТАБ SC");
            Msg("  ПОДОБИЕ O · ОБРЕЗАТЬ TR · УДЛИНИТЬ EX · СОПРЯЖЕНИЕ F · ФАСКА CHA · МАССИВ AR · ПРОВЕРКА · ПОЧИНИТЬ · ШЕСТЕРНЯ · ШЛИЦЫ · ШПОНКА");
            Msg("  РАЗМЕР DIM · РАДИУС · ДИАМЕТР · УГОЛ · ГАБАРИТ (F12) · ЛИНЕЙКИ (F11) · УДАЛИТЬРАЗМЕРЫ");
            Msg("  ДИСТ DI · ID · ПОКАЗАТЬ Z · ОТМЕНИТЬ U (Ctrl+Z) · ПОВТОРИТЬ (Ctrl+Y) · ИСТОРИЯ · ОТКРЫТЬ · СОХРАНИТЬ · НОВЫЙ · ИЗSTZ · WCUT (связь с AutoCAD)");
            Msg("  ЭРОЗИЯ: СТАРТ ST (точка заправки) · ВХОД (вход / выход проволоки по шагам) · ЗАХОД · СБРОС · НОЛЬ · ПУТЬ P (F5) · СИМ (F6) · РЕЖИМЫ (F4) · УП (F10) · НАСТАНОК (Ctrl+F10) · ПАПКА (папка станка)");
            Msg("  Мышь: точки ВХОД / ЗАПРАВКА / ВЫХОД / СХОД тянуть левой кнопкой, правая кнопка по точке — меню; правая кнопка по шагу истории — удалить шаг.");
            Msg("  Вручную: папка станка — ПАПКА или Свойства → 5; режимы — Свойства → 3. Проходы («Режимы вручную = Да» — таблица не перезапишет).");
            Msg("  Ввод точки: 10,20  @5,0  @30<45  или число = расстояние по курсору. Enter/Пробел/ПКМ — подтвердить/повторить, Esc — отмена.");
            Msg("  Колесо — зум, средняя кнопка — панорама, двойной щелчок средней — показать всё. F3 привязка, F7 сетка, F8 орто, F9 шаг.");
        }

        GearParams gearPrm = new GearParams();
        KeyParams keyPrm = new KeyParams();

        /// <summary>Библиотека: шестерня или отверстие со шпоночным пазом.</summary>
        SplineParams splPrm = new SplineParams();
        void Library(int kind, string titleOverride = null)
        {
            bool gear = kind == 0;
            object prm = kind == 0 ? (object)gearPrm : kind == 1 ? (object)keyPrm : splPrm;
            string title = titleOverride ?? (kind == 0 ? "Шестерня прямозубая / внутренний венец (эвольвента)" : kind == 1 ? "Отверстие со шпоночным пазом — ГОСТ 23360-78" : "Шлицы — ГОСТ 1139-80 / ГОСТ 6033-80");
            // наименьший радиус, который делает проволока в углу отверстия (для радиусов паза по ГОСТ)
            double wl = 0; foreach (PassParam pp in S.ActivePasses()) wl = Math.Max(wl, S.OffsetOf(pp, PartKind.Die));
            KeyParams.WireLimit = wl > 0 ? wl : 0.10;
            using (LibForm f = new LibForm(prm, title))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                PushUndo();
                foreach (Seg sg in Cv.Ents) sg.Sel = false;
                Cv.Ents.AddRange(f.Result);
                S.PartName = f.PartName;
                if (kind == 0) S.Kind = (gearPrm.Internal && gearPrm.Bore <= 0) ? PartKind.Die : PartKind.Punch;
                else if (kind == 1) S.Kind = PartKind.Die;
                else S.Kind = splPrm.Part == SplinePart.Hub ? PartKind.Die : PartKind.Punch;
                S.AutoNest = true;
                grid.Refresh();
                Cv.Path = null; Cv.ZoomExtents();
                Msg("Вставлено: " + f.InfoText + "  (объектов " + f.Result.Count + ")");
                Msg("  Тип вырезки: " + RusEnumConverter.Text(S.Kind) + (gear && !gearPrm.Internal && gearPrm.Bore > 0 ? ", отверстие режется первым как матрица" : "") + ". Дальше — F5 траектория, F10 УП.");
            }
        }

        void CheckDrawing()
        {
            List<string> rep = new List<string>();
            Cv.Marks = DrawingCheck.Check(Cv.Ents, rep);
            Msg(rep[0]);
            for (int i = 1; i < rep.Count && i <= 12; i++) Msg("  " + rep[i]);
            if (rep.Count > 13) Msg("  … ещё " + (rep.Count - 13));
            Msg(Cv.Marks.Count > 0 ? "  Проблемные места отмечены красными кругами. Команда ПОЧИНИТЬ — исправить автоматически." : "  Чертёж в порядке.");
            Cv.Invalidate();
        }

        public void RepairDrawing(double tol)
        {
            List<string> rep = new List<string>();
            PushUndo();
            Cv.Ents = DrawingCheck.Repair(Cv.Ents, tol, rep);
            foreach (string r in rep) Msg(r);
            CheckDrawing();
            RebuildIfPath();
        }

        /// <summary>Текст, набираемый в командной строке (показывается в поле размера на чертеже).</summary>
        public string InputText { get { return input.Text; } set { input.Text = value; } }


        void OpenManual()
        {
            string f = Path.Combine(Application.StartupPath, "Руководство.html");
            if (!File.Exists(f)) { Msg("Файл руководства не найден: " + f); return; }
            try { System.Diagnostics.Process.Start(f); } catch (Exception ex) { Msg("Не удалось открыть руководство: " + ex.Message); }
        }

        /// <summary>Справка → «Что нового»: изменения последних версий простыми словами.</summary>
        void WhatsNew()
        {
            string t =
"ВЕРСИЯ 3.3\r\n" +
"• Открытие DWG (AutoCAD): Файл → «Открыть DXF / DWG…» (Ctrl+O) — в окне выбора видны и .dxf, и .dwg. DWG переводится в DXF встроенным конвертером LibreDWG (ставится вместе с программой) или бесплатным ODA File Converter, если он установлен. Двойной щелчок по DWG по-прежнему открывает AutoCAD; для STZ WireCut — правая кнопка по файлу → «Открыть с помощью» → STZ WireCut.\r\n" +
"• После открытия DWG проверьте размеры: «Проверить» и габарит (F12). Если чертёж не открылся — сохраните его в AutoCAD / Fusion 360 как DXF.\r\n\r\n" +
"ВЕРСИЯ 3.2\r\n" +
"• Папка станка — вручную. Меню «Эрозия → Папка станка…» (команда ПАПКА) или Свойства → «5. Связь со станком» → «Папка станка»: кнопка «…» или впишите путь. Это может быть папка Work программы AutoCut, флешка или сетевая папка. Папка запоминается; «На станок» и окно УП пишут только туда. Если папки нет (вынули флешку) — программа попросит выбрать и запомнит.\r\n" +
"• Режимы резания — вручную. Свойства → «3. Проходы (режимы)» → раскройте «Проход 1» и впишите свои Ton, Toff, ток, трубки, V-F, зазор. «Режимы вручную» станет «Да» само — при смене материала, толщины или станка ваши значения больше не заменяются таблицей. Поставьте «Нет» — режим снова из таблицы (F4).\r\n\r\n" +
"ВЕРСИЯ 3.1\r\n" +
"• Вход / выход проволоки (вкладка ЭРОЗИЯ → «Вход/выход»): подсказка по 4 шагам вверху слева на чертеже — ВХОД, ЗАПРАВКА, ВЫХОД, СХОД. Стрелка показывает направление резки, перекрытие рисуется оранжевым, перемычка — красным пунктиром, рядом длина. Enter или правая кнопка — пропустить шаг, Н — шаг назад. Траектория пересчитывается сразу.\r\n" +
"• Готовые точки можно тянуть мышью (вход и выход скользят по контуру). Правая кнопка по точке — меню: заправка по свойствам, выход в точке входа, сход автоматически, задать заново, удалить.\r\n" +
"• История: правая кнопка по шагу на шкале под чертежом — «Удалить этот шаг» (убирается только это действие, всё сделанное позже остаётся), «Вернуться к этому шагу», «Удалить все шаги до этого», «Очистить всю историю».\r\n\r\n" +
"ВЕРСИЯ 3.0\r\n" +
"• Для монитора станка: 4 вкладки ленты (ЧЕРТЁЖ / ПРАВКА И РАЗМЕРЫ / БИБЛИОТЕКА / ЭРОЗИЯ) в одну строку, Ctrl+F1 — свернуть ленту, F2 — скрыть панель свойств.\r\n" +
"• Кнопка «На станок ▶» (Ctrl+F10): траектория → УП → сразу в папку станка, без окна УП. При предупреждениях программа сначала спросит.\r\n\r\n" +
"Подробно — Руководство (F1), раздел «Что нового».";
            using (Form f = new Form { Text = "Что нового — STZ WireCut " + Ver, StartPosition = FormStartPosition.CenterParent, Size = new Size(Math.Min(760, Screen.FromControl(this).WorkingArea.Width - 40), Math.Min(560, Screen.FromControl(this).WorkingArea.Height - 40)), BackColor = Theme.Background, ForeColor = Theme.Text, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.Sizable })
            {
                TextBox tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = t, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = new Font(UiFont, UiSize + 0.5f) };
                Panel bot = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.Background };
                Button man = new Button { Text = "Руководство (F1)", Width = 170, Height = 32, Left = 10, Top = 7, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text, BackColor = Theme.Raised };
                Button ok = new Button { Text = "Закрыть", Width = 120, Height = 32, Top = 7, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Theme.Accent, DialogResult = DialogResult.OK };
                bot.Controls.Add(man); bot.Controls.Add(ok);
                f.Controls.Add(tb); f.Controls.Add(bot);
                f.Load += delegate { ok.Left = bot.ClientSize.Width - ok.Width - 10; tb.Select(0, 0); };
                man.Click += delegate { f.Close(); OpenManual(); };
                f.AcceptButton = ok; f.CancelButton = ok;
                f.ShowDialog(this);
            }
        }

        void About()
        {
            using (BrandForm about = new BrandForm(false)) about.ShowDialog(this);
        }

        // ================= файлы =================
        /// <summary>Перед изменением чертежа: сохранить в историю всё, что изменилось до этого (шаг записывается после действия).</summary>
        public void PushUndo()
        {
            Cv.Marks.Clear();
            string n = opName, c = opCmd;
            opName = null; opCmd = null; Checkpoint();
            opName = n; opCmd = c;
        }

        void OpenDxf(bool replace)
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Чертежи DXF и DWG (*.dxf;*.dwg)|*.dxf;*.dwg|DXF (*.dxf)|*.dxf|DWG — AutoCAD (*.dwg)|*.dwg|Все файлы|*.*", Title = "Открыть чертёж DXF / DWG (AutoCAD, Fusion 360)" })
                if (d.ShowDialog(this) == DialogResult.OK) ImportDrawing(d.FileName, replace);
        }

        public void ImportDxf(string path, bool replace) { ImportDxf(path, replace, null); }

        /// <summary>shown — исходный файл для имени детали и заголовка (DWG, переведённый в DXF).</summary>
        public void ImportDxf(string path, bool replace, string shown)
        {
            try
            {
                List<string> log = new List<string>();
                List<Seg> segs = Dxf.Read(path, log);
                PushUndo();
                string src = path;
                if (shown != null) path = shown;
                opName = (path == AcadLink.ExchangeFile ? "Из AutoCAD" : (shown != null ? "DWG " : "DXF ") + Path.GetFileName(path)); opCmd = "ОТКРЫТЬ";
                if (replace) { Cv.Ents.Clear(); Cv.ManualStarts.Clear(); Cv.SideMarks.Clear(); Cv.LeadMarks.Clear(); Cv.Dims.Clear(); }
                Cv.Ents.AddRange(segs);
                Cv.Path = null; stInfo.Text = "";
                if (replace && path != AcadLink.ExchangeFile) { curFile = path; S.PartName = Path.GetFileNameWithoutExtension(path).ToUpper(); grid.Refresh(); }
                Text = "STZ WireCut " + Ver + " — " + Path.GetFileName(path);
                Msg("Загружено объектов: " + segs.Count + " из " + Path.GetFileName(path));
                foreach (string l in log) Msg("  " + l);
                List<Contour> cs = Geo.Chain(Cv.Ents, 0.01);
                int closed = 0; foreach (Contour c in cs) if (c.Closed) closed++;
                Msg("  Контуров: " + cs.Count + " (замкнутых " + closed + ")");
                Cv.ZoomExtents();
            }
            catch (Exception ex) { Msg((shown != null ? "Ошибка чтения DWG (после перевода в DXF): " : "Ошибка чтения DXF: ") + ex.Message); }
        }

        void SaveDxf()
        {
            using (SaveFileDialog d = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = S.PartName + ".dxf" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                List<Dxf.Layered> it = new List<Dxf.Layered>();
                foreach (Seg s in Cv.Ents) it.Add(new Dxf.Layered { S = s, Layer = "CONTOUR", Color = 7 });
                Dxf.Write(d.FileName, it);
                Msg("Сохранено: " + d.FileName);
            }
        }

        void ExportPathDxf()
        {
            if (Cv.Path == null && !BuildPath(false)) return;
            using (SaveFileDialog d = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = S.PartName + "_траектория.dxf" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                List<Dxf.Layered> it = new List<Dxf.Layered>();
                foreach (Seg s in Cv.Ents) it.Add(new Dxf.Layered { S = s, Layer = "CONTOUR", Color = 7 });
                foreach (Move m in Cv.Path.Moves)
                    if (m.S != null) it.Add(new Dxf.Layered { S = m.S, Layer = m.K == MoveKind.Rapid ? "RAPID" : m.Lead ? "LEADIN" : "PASS" + m.Pass, Color = m.K == MoveKind.Rapid ? 8 : m.Lead ? 2 : 3 + m.Pass });
                Dxf.Write(d.FileName, it);
                Msg("Траектория сохранена в DXF (слои PASS1..4, LEADIN, RAPID): " + d.FileName);
            }
        }

        void SaveParamsAs()
        {
            using (SaveFileDialog d = new SaveFileDialog { Filter = "Параметры (*.ini)|*.ini", FileName = S.Material + "_" + Geo.F(S.Thickness) + "мм.ini" })
                if (d.ShowDialog(this) == DialogResult.OK) { S.Save(d.FileName); Msg("Параметры сохранены: " + d.FileName); }
        }
        void LoadParams()
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Параметры (*.ini)|*.ini" })
                if (d.ShowDialog(this) == DialogResult.OK) { S.Load(d.FileName); grid.Refresh(); RebuildIfPath(); Msg("Параметры загружены: " + d.FileName); }
        }

        // ================= эрозия =================
        void RebuildIfPath() { if (Cv.Path != null) BuildPath(false); }

        /// <summary>Точка заправки / метка захода действует на контур не дальше этого расстояния, мм.</summary>
        public const double MarkReach = 25;

        /// <summary>Сбросить все точки заправки и стороны захода — заправка снова ставится автоматически.</summary>
        void ResetMarks()
        {
            int n = Cv.ManualStarts.Count + Cv.LeadMarks.Count, m = Cv.SideMarks.Count;
            Cv.ManualStarts.Clear(); Cv.SideMarks.Clear(); Cv.LeadMarks.Clear();
            RebuildIfPath(); Cv.Invalidate();
            Msg(n + m == 0 ? "Точек заправки и меток захода нет — всё и так автоматически." :
                "Сброшено: точек заправки / входа-выхода " + n + ", меток захода " + m + ". Заправка и сторона захода — автоматически (Ctrl+Z — вернуть).");
        }

        /// <summary>Перед удалением объектов: убрать точки заправки и метки захода, которые относились к ним.</summary>
        public void DropMarksOf(List<Seg> removing)
        {
            int n = 0;
            for (int k = Cv.ManualStarts.Count - 1; k >= 0; k--) if (NearestIn(Cv.ManualStarts[k], removing)) { Cv.ManualStarts.RemoveAt(k); n++; }
            for (int k = Cv.SideMarks.Count - 1; k >= 0; k--) if (NearestIn(Cv.SideMarks[k].P, removing)) { Cv.SideMarks.RemoveAt(k); n++; }
            for (int k = Cv.LeadMarks.Count - 1; k >= 0; k--) if (NearestIn(Cv.LeadMarks[k].Entry, removing)) { Cv.LeadMarks.RemoveAt(k); n++; }
            if (n > 0) Msg("  Вместе с объектами убраны их точки заправки / метки захода: " + n);
        }
        bool NearestIn(V2 p, List<Seg> removing)
        {
            Seg best = null; double bd = double.MaxValue;
            foreach (Seg s in Cv.Ents) { double t; V2 q; double d = s.Project(p, out t, out q); if (d < bd) { bd = d; best = s; } }
            return best == null || removing.Contains(best);
        }

        public bool BuildPath(bool verbose)
        {
            bool anySel = false;
            foreach (Seg s in Cv.Ents) if (s.Sel) { anySel = true; break; }
            List<Seg> src = new List<Seg>();
            foreach (Seg s in Cv.Ents) if (!anySel || s.Sel) src.Add(s);
            if (src.Count == 0) { Msg("Нет геометрии — откройте DXF или начертите контур."); return false; }
            List<Contour> cs = Geo.Chain(src, 0.01);
            List<ContourJob> jobs = new List<ContourJob>();
            foreach (Contour c in cs) jobs.Add(new ContourJob { Geo = c });
            // ручные вход / выход — к контуру, на котором стоит точка входа
            foreach (LeadMark lm in Cv.LeadMarks)
            {
                ContourJob best = null; double bd = double.MaxValue;
                foreach (ContourJob j in jobs)
                {
                    if (!j.Geo.Closed || j.Lead != null) continue;
                    int i; double t; double d = j.Geo.DistTo(lm.Entry, out i, out t);
                    if (d < bd) { bd = d; best = j; }
                }
                if (best != null && bd <= 0.05) best.Lead = lm;
                else if (verbose) Msg("Внимание: точка входа X" + Geo.F(Math.Round(lm.Entry.X, 3)) + " Y" + Geo.F(Math.Round(lm.Entry.Y, 3)) + " не на контуре (контур изменён?) — не используется. Задайте заново: «Вход/выход».");
            }
            // ручные точки заправки — к ближайшему замкнутому контуру
            foreach (V2 st in Cv.ManualStarts)
            {
                ContourJob best = null; double bd = double.MaxValue;
                foreach (ContourJob j in jobs)
                {
                    if (!j.Geo.Closed || j.ManualStart.HasValue || j.Lead != null) continue;
                    int i; double t; double d = j.Geo.DistTo(st, out i, out t);
                    if (d < bd) { bd = d; best = j; }
                }
                if (best != null && bd <= MarkReach) best.ManualStart = st;
                else if (verbose) Msg("Внимание: точка заправки X" + Geo.F(Math.Round(st.X, 3)) + " Y" + Geo.F(Math.Round(st.Y, 3)) + " дальше " + Geo.F(MarkReach) + " мм от контуров — не используется. Убрать: «Сброс» (Эрозия).");
            }
            // сторона захода, заданная вручную, — к ближайшему замкнутому контуру
            foreach (SideMark sm in Cv.SideMarks)
            {
                ContourJob best = null; double bd = double.MaxValue;
                foreach (ContourJob j in jobs)
                {
                    if (!j.Geo.Closed) continue;
                    int i; double t; double d = j.Geo.DistTo(sm.P, out i, out t);
                    if (d < bd) { bd = d; best = j; }
                }
                if (best != null && bd <= MarkReach) best.Side = sm.Outside ? PartKind.Punch : PartKind.Die;
            }
            Toolpath tp = Toolpath.Build(jobs, S);
            Cv.Path = tp;
            Cv.SimOn = false;
            if (simPanel != null) simPanel.Visible = false;
            if (verbose)
            {
                Msg("Траектория: контуров " + tp.Starts.Count + (anySel ? " (только выбранные)" : "") + ", длина реза " + Geo.F(Math.Round(tp.CutLen, 1)) + " мм, время ~" + Post.TimeStr(tp.Minutes));
                Msg("  Стоимость ≈ " + Math.Round(tp.Cost(S)) + " руб (" + Math.Max(1, S.Qty) + " шт, наладка " + Geo.F(S.SetupMin) + " мин, " + Geo.F(S.PriceHour) + " руб/ч) — настройка в «6. Стоимость»");
                foreach (PassParam p in S.ActivePasses())
                    Msg("  Проход " + (Array.IndexOf(S.Passes, p) + 1) + " «" + p.Name + "»: смещение " + Geo.F(S.OffsetOf(p)) + " мм");
            }
            foreach (string w in tp.Warn) Msg("  ВНИМАНИЕ: " + w);
            stInfo.Text = "Рез " + Geo.F(Math.Round(tp.CutLen, 1)) + " мм · время ~" + Post.TimeStr(tp.Minutes) + " · ≈ " + Math.Round(tp.Cost(S)) + " руб";
            Cv.Invalidate();
            return tp.Moves.Count > 1;
        }

        // ================= симуляция =================
        Control BuildSimBar()
        {
            FlowLayoutPanel p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Raised, Padding = new Padding(6, 4, 6, 4), WrapContents = true, Visible = false };
            simPanel = p;   // показывается только во время симуляции (F6) — остальное время место отдано чертежу
            p.Controls.Add(new Label { Text = "СИМУЛЯЦИЯ", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(2, 9, 10, 0) });
            p.Controls.Add(SimBtn("|◄ В начало", "SIM_BEGIN", delegate { SimBegin(); }));
            Button play = SimBtn("► Пуск", "SIM_PLAY", delegate { SimPlay(); }); play.BackColor = Theme.Green; p.Controls.Add(play);
            p.Controls.Add(SimBtn("|| Пауза", "SIM_PAUSE", delegate { SimPause(); }));
            p.Controls.Add(SimBtn("■ Стоп", "SIM_STOP", delegate { SimStop(); }));
            p.Controls.Add(SimBtn("►| Шаг", "SIM_STEP", delegate { SimStep(); }));
            p.Controls.Add(new Label { Text = "Скорость:", AutoSize = true, Margin = new Padding(12, 9, 4, 0) });
            simSpeed = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, BackColor = Theme.Background, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 5, 8, 0) };
            simSpeed.Items.AddRange(new object[] { "1 — медленно", "2 — средне", "3 — быстро", "4 — очень быстро" }); simSpeed.SelectedIndex = 1;
            tip.SetToolTip(simSpeed, Tips.Get("SIMSPEED"));
            p.Controls.Add(simSpeed);
            simBar = new TrackBar { Width = 260, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, Height = 30, Margin = new Padding(4, 4, 8, 0), BackColor = Theme.Raised };
            tip.SetToolTip(simBar, Tips.Get("SIM_BAR"));
            simBar.Scroll += delegate { SimEnsure(); Cv.SimPos = Cv.SimTotal * simBar.Value / 1000.0; SimShow(); };
            p.Controls.Add(simBar);
            simInfo = new Label { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 9, 0, 0), Text = "—" };
            p.Controls.Add(simInfo);
            return p;
        }

        Button SimBtn(string t, string tipKey, EventHandler h)
        {
            Button b = new Button { Text = t, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface, ForeColor = Theme.Text, Margin = new Padding(2, 1, 2, 0), Padding = new Padding(4, 0, 4, 0) };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.Click += h; tip.SetToolTip(b, Tips.Get(tipKey));
            return b;
        }

        bool SimEnsure()
        {
            if (Cv.SimOn && Cv.SimSegs.Count > 0) return true;
            if (Cv.Path == null && !BuildPath(false)) return false;
            Cv.SimPrepare(); Cv.SimOn = true;
            return Cv.SimSegs.Count > 0;
        }
        void SimPlay()
        {
            if (!SimEnsure()) return;
            if (Cv.SimPos >= Cv.SimTotal - 1e-9) Cv.SimPos = 0;
            simTimer.Start(); SimShow();
        }
        void SimPause() { simTimer.Stop(); SimShow(); }
        void SimStop() { simTimer.Stop(); Cv.SimOn = false; Cv.SimPos = 0; SimShow(); }
        void SimBegin() { if (!SimEnsure()) return; Cv.SimPos = 0; SimShow(); }
        void SimStep()
        {
            if (!SimEnsure()) return;
            simTimer.Stop();
            foreach (double c in Cv.SimCum) if (c > Cv.SimPos + 1e-6) { Cv.SimPos = c; break; }
            SimShow();
        }
        void SimShow()
        {
            if (simPanel != null && simPanel.Visible != Cv.SimOn) simPanel.Visible = Cv.SimOn;
            double tot = Cv.SimTotal;
            if (simBar != null) simBar.Value = tot > 0 ? Math.Max(0, Math.Min(1000, (int)(Cv.SimPos / tot * 1000))) : 0;
            if (simInfo != null)
            {
                double frac = tot > 0 ? Cv.SimPos / tot : 0;
                simInfo.Text = Cv.SimOn ? Geo.F(Math.Round(Cv.SimPos, 1)) + " / " + Geo.F(Math.Round(tot, 1)) + " мм   ·   " +
                    (Cv.Path != null ? Post.TimeStr(Cv.Path.Minutes * frac) + " из " + Post.TimeStr(Cv.Path.Minutes) : "") + (simTimer.Enabled ? "" : "   (пауза)") : "—";
            }
            Cv.Invalidate();
        }

        void Simulate()
        {
            // F6 и кнопка ленты: пуск / пауза
            if (simTimer.Enabled) { SimPause(); return; }
            SimPlay();
        }

        public void AfterRegime(string msg)
        {
            grid.Refresh();
            try { S.Save(SettingsPath); } catch { }
            Msg(msg);
            RebuildIfPath();
        }

        void OpenRegimes()
        {
            using (RegimeForm f = new RegimeForm(this)) f.ShowDialog(this);
        }

        void SimTick(object o, EventArgs e)
        {
            double[] sp = { 4, 20, 80, 400 };
            double v = sp[Math.Max(0, simSpeed.SelectedIndex)];
            // скорость в мм/с экрана, но не меньше 1/600 длины за тик
            Cv.SimPos += Math.Max(v * simTimer.Interval / 1000.0, Cv.SimTotal / 3000);
            if (Cv.SimPos >= Cv.SimTotal) { Cv.SimPos = Cv.SimTotal; simTimer.Stop(); Msg("Симуляция завершена."); }
            SimShow();
        }

        void MakeProgram()
        {
            if (!BuildPath(false)) return;
            string baseName = MakeSafe(S.PartName);
            List<OutFile> files = Post.Make(Cv.Path, S, baseName);
            using (PostForm f = new PostForm(this, files)) f.ShowDialog(this);
        }

        public static string MakeSafe(string n)
        {
            if (string.IsNullOrEmpty(n)) return "PROG";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Replace(' ', '_');
        }

        void SendExistingFile()
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "Программы (*.nc;*.iso;*.3b;*.txt)|*.nc;*.iso;*.3b;*.txt|Все файлы|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                OutFile f = new OutFile { Name = Path.GetFileName(d.FileName), Text = File.ReadAllText(d.FileName, Encoding.Default) };
                using (PostForm pf = new PostForm(this, new List<OutFile> { f })) pf.ShowDialog(this);
            }
        }
    }
}
