// STZ WireCut — параметры обработки, режимы и построение траектории проволоки
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace WireCut
{
    // ---------------- перечисления с русскими названиями ----------------
    [TypeConverter(typeof(RusEnumConverter))]
    public enum MachineType
    {
        [Description("Быстрая протяжка (молибден, HSWEDM)")] Fast,
        [Description("Средняя протяжка (молибден, многопроход)")] Medium,
        [Description("Медленная протяжка (латунь)")] Slow
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum MachineProfile
    {
        [Description("SIMOS DK7750 · AutoCut 6.0 (цех)")] DK7750,
        [Description("Свой станок (настройки вручную)")] Custom
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum LeadKind
    {
        [Description("Прямой (перпендикулярно контуру)")] Straight,
        [Description("Под углом к контуру")] Angled,
        [Description("По дуге (плавный, без следа)")] Arc
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum PartKind
    {
        [Description("Пуансон — деталь внутри контура")] Punch,
        [Description("Матрица — вырезаем отверстие")] Die
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum CutDir
    {
        [Description("По часовой")] CW,
        [Description("Против часовой")] CCW
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum OutFormat
    {
        [Description("ISO (G-код)")] Iso,
        [Description("3B (HL / HF / AutoCut)")] B3,
        [Description("ISO + 3B")] Both
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum ZeroMode
    {
        [Description("Точка заправки = X0 Y0")] Start,
        [Description("Ноль чертежа")] Drawing
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum CommentMode
    {
        [Description("Русские")] Rus,
        [Description("Латиница (транслит)")] Lat,
        [Description("Без комментариев")] None
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum CompMode
    {
        [Description("В программе (эквидистанта рассчитана)")] Cam,
        [Description("На стойке (G41/G42 + D)")] Cnc
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum LinkMethod
    {
        [Description("COM-порт (RS-232)")] Com,
        [Description("Папка / флешка / сеть")] Folder
    }

    public class RusEnumConverter : EnumConverter
    {
        Type t;
        public RusEnumConverter(Type type) : base(type) { t = type; }
        public static string Text(object v)
        {
            FieldInfo fi = v.GetType().GetField(v.ToString());
            if (fi == null) return v.ToString();
            object[] a = fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return a.Length > 0 ? ((DescriptionAttribute)a[0]).Description : v.ToString();
        }
        public override object ConvertTo(ITypeDescriptorContext c, CultureInfo cu, object v, Type dt)
        {
            if (dt == typeof(string) && v != null && v.GetType() == t) return Text(v);
            return base.ConvertTo(c, cu, v, dt);
        }
        public override object ConvertFrom(ITypeDescriptorContext c, CultureInfo cu, object v)
        {
            if (v is string)
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    object e = fi.GetValue(null);
                    if (Text(e) == (string)v || fi.Name == (string)v) return e;
                }
            return base.ConvertFrom(c, cu, v);
        }
    }

    /// <summary>Кнопка «…» у пути папки: выбор папки (флешка, AutoCut\Work, сеть).</summary>
    public class FolderEditor : System.Drawing.Design.UITypeEditor
    {
        public override System.Drawing.Design.UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext c) { return System.Drawing.Design.UITypeEditorEditStyle.Modal; }
        public override object EditValue(ITypeDescriptorContext c, IServiceProvider sp, object v)
        {
            using (System.Windows.Forms.FolderBrowserDialog d = new System.Windows.Forms.FolderBrowserDialog { Description = "Папка станка: сюда «На станок» будет записывать УП (AutoCut\\Work, флешка или сетевая папка)", ShowNewFolderButton = true })
            {
                string cur = v as string;
                if (!string.IsNullOrEmpty(cur) && Directory.Exists(cur)) d.SelectedPath = cur;
                return d.ShowDialog() == System.Windows.Forms.DialogResult.OK ? d.SelectedPath : v;
            }
        }
    }

    /// <summary>Показывает свойства в порядке объявления (а не по алфавиту).</summary>
    public class OrderConverter : ExpandableObjectConverter
    {
        public override bool GetPropertiesSupported(ITypeDescriptorContext c) { return true; }
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext c, object v, Attribute[] attrs)
        {
            PropertyDescriptorCollection pc = TypeDescriptor.GetProperties(v, attrs, true);
            PropertyInfo[] pis = v.GetType().GetProperties();
            Array.Sort(pis, delegate (PropertyInfo a, PropertyInfo b) { return a.MetadataToken.CompareTo(b.MetadataToken); });
            string[] names = new string[pis.Length];
            for (int i = 0; i < pis.Length; i++) names[i] = pis[i].Name;
            return pc.Sort(names);
        }
    }

    public class RusBool : BooleanConverter
    {
        public override object ConvertTo(ITypeDescriptorContext c, CultureInfo cu, object v, Type dt)
        {
            if (dt == typeof(string) && v is bool) return (bool)v ? "Да" : "Нет";
            return base.ConvertTo(c, cu, v, dt);
        }
        public override object ConvertFrom(ITypeDescriptorContext c, CultureInfo cu, object v)
        {
            if (v is string) { string t = ((string)v).Trim().ToLower(); return t == "да" || t == "true" || t == "1"; }
            return base.ConvertFrom(c, cu, v);
        }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c) { return new StandardValuesCollection(new object[] { true, false }); }
    }

    public class ListConverter : StringConverter
    {
        protected virtual string[] Items() { return new string[0]; }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext c) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext c) { return false; }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c) { return new StandardValuesCollection(Items()); }
    }
    public class MaterialConverter : ListConverter
    {
        protected override string[] Items()
        {
            List<string> l = new List<string>(RegimeDb.Materials());
            l.AddRange(new string[] { "Сталь 45", "40Х", "Х12МФ", "Х12МФ (HRC 58-62)", "У8А", "ШХ15", "65Г", "12Х18Н10Т",
                "ВК8", "Медь М1", "Латунь ЛС59", "Алюминий Д16Т", "Титан ВТ6", "Графит" });
            return l.ToArray();
        }
    }
    public class PortConverter : ListConverter
    {
        protected override string[] Items()
        {
            try { return System.IO.Ports.SerialPort.GetPortNames(); } catch { return new string[] { "COM1" }; }
        }
    }
    public class BaudConverter : TypeConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext c) { return true; }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c)
        { return new StandardValuesCollection(new int[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 }); }
        public override bool CanConvertFrom(ITypeDescriptorContext c, Type s) { return s == typeof(string) || base.CanConvertFrom(c, s); }
        public override object ConvertFrom(ITypeDescriptorContext c, CultureInfo cu, object v) { return int.Parse(v.ToString()); }
    }

    // ---------------- один проход ----------------
    [TypeConverter(typeof(OrderConverter))]
    public class PassParam
    {
        [DisplayName("Включён"), Description("Выполнять этот проход"), TypeConverter(typeof(RusBool))] public bool On { get; set; }
        [DisplayName("Название"), Description("Название прохода для карты наладки")] public string Name { get; set; }
        [DisplayName("Искровой зазор, мм"), Description("Межэлектродный зазор на сторону для этого режима")] public double Gap { get; set; }
        [DisplayName("Припуск, мм"), Description("Сколько оставить на следующие проходы (на последнем проходе = 0)")] public double Stock { get; set; }
        [DisplayName("Ton, мкс"), Description("Длительность импульса")] public double Ton { get; set; }
        [DisplayName("Toff, мкс"), Description("Пауза между импульсами")] public double Toff { get; set; }
        [DisplayName("Ток Ip, А"), Description("Рабочий ток (в AutoCut — число силовых трубок / ток)")] public double Ip { get; set; }
        [DisplayName("Напряжение, В"), Description("Напряжение холостого хода генератора")] public double U { get; set; }
        [DisplayName("Силовые трубки"), Description("Ступень тока генератора (число силовых трубок в AutoCut)")] public int Tubes { get; set; }
        [DisplayName("V-F (подача)"), Description("Чувствительность / частота подачи в AutoCut (на экране станка — V-F)")] public int VF { get; set; }
        [DisplayName("Производит., мм²/мин"), Description("Примерная скорость резки (площадь реза в минуту) — для расчёта времени")] public double Rate { get; set; }
        [DisplayName("№ режима стойки"), Description("Номер технологии/режима в станке (подставляется в шаблон кода режима)")] public int RegNo { get; set; }
        public override string ToString() { return On ? Name + (Stock > 0 ? ", припуск " + Geo.F(Stock) : "") : "выкл."; }
    }

    // ---------------- все параметры задания ----------------
    [TypeConverter(typeof(OrderConverter))]
    public class JobSettings
    {
        const string C1a = "1а. Заход проволоки", C1 = "1. Деталь", C2 = "2. Проволока и станок", C3 = "3. Проходы (режимы)", C4 = "4. Управляющая программа", C5 = "5. Связь со станком", C6 = "6. Стоимость";

        [Category(C1), DisplayName("Название детали"), Description("Имя детали — пойдёт в имя файла УП, заголовок программы и карту наладки")] public string PartName { get; set; }
        [Category(C1), DisplayName("Номер чертежа"), Description("Обозначение по чертежу (для карты наладки)")] public string DrawingNo { get; set; }
        [Category(C1), DisplayName("Материал"), TypeConverter(typeof(MaterialConverter))] public string Material { get; set; }
        [Category(C1), DisplayName("Толщина, мм"), Description("Высота реза (толщина заготовки)")] public double Thickness { get; set; }
        [Category(C1), DisplayName("Тип вырезки"), Description("Пуансон — нужна деталь внутри контура; Матрица — нужно отверстие")] public PartKind Kind { get; set; }
        [Category(C1), DisplayName("Направление обхода"), Description("Куда идёт проволока по контуру. Обычно по часовой")] public CutDir Dir { get; set; }
        [Category(C1a), DisplayName("Длина захода, мм"), Description("Расстояние от точки заправки до контура (если точка не указана вручную мышью)")] public double LeadIn { get; set; }
        [Category(C1a), DisplayName("Тип захода"), Description("Прямой — по нормали к контуру. Под углом — наклонно, по ходу резки. По дуге — плавно по касательной: на детали не остаётся «ступеньки». Точки входа, заправки, выхода и схода вручную для каждого контура — кнопка «Вход/выход» (Эрозия)")] public LeadKind LeadType { get; set; }
        [Category(C1a), DisplayName("Угол захода, °"), Description("Для захода под углом: отклонение от перпендикуляра к контуру (0° — перпендикулярно, 45° — наклонно)")] public double LeadAngle { get; set; }
        [Category(C1a), DisplayName("Радиус дуги захода, мм"), Description("Для захода по дуге: радиус плавного входа на контур")] public double LeadArcR { get; set; }
        [Category(C1), DisplayName("Перемычка, мм"), Description("Оставить перемычку в конце контура, чтобы деталь не упала (0 — без перемычки)")] public double Tab { get; set; }
        [Category(C1), DisplayName("Внутр. контуры = отверстия"), TypeConverter(typeof(RusBool)), Description("Контур внутри другого контура режется как отверстие, и раньше наружного")] public bool AutoNest { get; set; }
        [Category(C1), DisplayName("Коррекция размера, мм"), Description("Сдвиг в поле допуска на сторону: «+» — размер больше (пуансон больше, отверстие больше), «−» — меньше")] public double SizeCorr { get; set; }

        [Category(C2), DisplayName("Профиль станка"), Description("Выбор станка подставляет формат УП, коды, режимы, ход стола и папку программы стойки")] public MachineProfile Profile { get; set; }
        [Category(C2), DisplayName("Ход стола X, мм"), Description("Паспортный ход стола по X — траектория проверяется на выход за него")] public double TravelX { get; set; }
        [Category(C2), DisplayName("Ход стола Y, мм"), Description("Паспортный ход стола по Y")] public double TravelY { get; set; }
        [Category(C2), DisplayName("Макс. толщина реза, мм"), Description("Наибольшая высота заготовки по паспорту станка")] public double MaxThick { get; set; }
        [Category(C2), DisplayName("Тип станка"), Description("Быстрая протяжка — молибден, один проход (DK7750). Средняя — молибден, многопроход. Медленная — латунь")] public MachineType Machine { get; set; }
        [Category(C2), DisplayName("Диаметр проволоки, мм"), Description("Фактический диаметр проволоки (молибден обычно 0.18). Смещение = половина диаметра + зазор")] public double WireD { get; set; }
        [Category(C2), DisplayName("Конусность, °"), Description("Резка с наклоном (U/V) — в следующей версии, сейчас не используется"), ReadOnly(true)] public double Taper { get; set; }

        [Category(C3), DisplayName("Режимы вручную"), TypeConverter(typeof(RusBool)), Description("Да — режимы проходов (Ton, Toff, ток, трубки, V-F, зазор…) задаёте сами, программа их НЕ меняет при смене материала, толщины и станка. Нет — подставляются из таблицы режимов (F4). Становится «Да» само, как только вы поменяете любое значение в проходе")] public bool RegimeManual { get; set; }
        [Category(C3), DisplayName("Проход 1 (черновой)")] public PassParam P1 { get; set; }
        [Category(C3), DisplayName("Проход 2")] public PassParam P2 { get; set; }
        [Category(C3), DisplayName("Проход 3")] public PassParam P3 { get; set; }
        [Category(C3), DisplayName("Проход 4")] public PassParam P4 { get; set; }

        [Category(C4), DisplayName("Формат УП"), Description("3B — родной формат AutoCut / HL / HF. ISO — G-код. Можно оба сразу")] public OutFormat Format { get; set; }
        [Category(C4), DisplayName("Номер программы O"), Description("Номер программы в ISO (O0001)")] public int ProgNo { get; set; }
        [Category(C4), DisplayName("Ноль программы"), Description("Откуда считаются координаты в ISO: от точки заправки (обычно) или от нуля чертежа")] public ZeroMode Zero { get; set; }
        [Category(C4), DisplayName("Коррекция на проволоку"), Description("В программе — смещение уже посчитано (нужно для 3B). На стойке — в УП контур детали и G41/G42")] public CompMode Comp { get; set; }
        [Category(C4), DisplayName("Комментарии в УП"), Description("Комментарии в скобках в ISO. AutoCut кириллицу не понимает — латиница или без комментариев")] public CommentMode Comments { get; set; }
        [Category(C4), DisplayName("Шаг нумерации кадров"), Description("N10, N20, N30… — шаг 10")] public int NStep { get; set; }
        [Category(C4), DisplayName("Код паузы"), Description("Код остановки (перед резкой перемычки и т.п.)")] public string PauseCode { get; set; }
        [Category(C4), DisplayName("Код конца программы"), Description("Код конца программы (M02 или M30)")] public string EndCode { get; set; }
        [Category(C4), DisplayName("Код обрезки проволоки"), Description("Если станок без автозаправки — оставьте M00")] public string CutWireCode { get; set; }
        [Category(C4), DisplayName("Код заправки проволоки")] public string ThreadCode { get; set; }
        [Category(C4), DisplayName("Шаблон кода режима"), Description("Например E{0} или M98 P{0} — {0} заменяется № режима прохода. Пусто — только комментарий")] public string RegimeTemplate { get; set; }
        [Category(C4), DisplayName("Строки в начале УП"), Description("Свои коды в начале (через ; ), например G21;G40")] public string StartLines { get; set; }
        [Category(C4), DisplayName("Расширение файла ISO"), Description("Расширение файла ISO, для AutoCut — .ISO")] public string IsoExt { get; set; }
        [Category(C4), DisplayName("Расширение файла 3B"), Description("Расширение файла 3B, для AutoCut — .3B")] public string B3Ext { get; set; }

        [Category(C5), DisplayName("Способ передачи"), Description("Как «Отправить на станок»: в папку (AutoCut Work / флешка / сеть) или по COM-порту")] public LinkMethod Link { get; set; }
        [Category(C5), DisplayName("COM-порт"), TypeConverter(typeof(PortConverter))] public string Port { get; set; }
        [Category(C5), DisplayName("Скорость, бод"), TypeConverter(typeof(BaudConverter))] public int Baud { get; set; }
        [Category(C5), DisplayName("Биты данных"), Description("7 или 8, как в настройках стойки")] public int DataBits { get; set; }
        [Category(C5), DisplayName("Чётность"), Description("None / Even / Odd")] public System.IO.Ports.Parity Parity { get; set; }
        [Category(C5), DisplayName("Стоп-биты"), Description("Обычно One (1)")] public System.IO.Ports.StopBits StopBits { get; set; }
        [Category(C5), DisplayName("Протокол"), Description("XOnXOff — программный, RequestToSend — аппаратный")] public System.IO.Ports.Handshake Handshake { get; set; }
        [Category(C5), DisplayName("Папка станка"), Editor(typeof(FolderEditor), typeof(System.Drawing.Design.UITypeEditor)), Description("Куда «На станок» пишет УП: папка Work программы AutoCut, флешка, сетевая папка. Кнопка «…» — выбрать папку, или впишите путь вручную. Меню «Эрозия → Папка станка…»")] public string Folder { get; set; }

        [Category(C6), DisplayName("Цена часа станка, руб"), Description("Стоимость часа работы эрозийного станка (с проволокой, электроэнергией, амортизацией)")] public double PriceHour { get; set; }
        [Category(C6), DisplayName("Наладка, мин"), Description("Установка, выверка, заправка проволоки")] public double SetupMin { get; set; }
        [Category(C6), DisplayName("Материал, руб"), Description("Стоимость заготовки (0 — материал заказчика)")] public double MaterialCost { get; set; }
        [Category(C6), DisplayName("Количество деталей, шт")] public int Qty { get; set; }

        public JobSettings()
        {
            PriceHour = 1500; SetupMin = 30; MaterialCost = 0; Qty = 1;
            LeadType = LeadKind.Straight; LeadAngle = 45; LeadArcR = 2;
            PartName = "ДЕТАЛЬ"; DrawingNo = ""; Material = "Х12МФ"; Thickness = 30;
            Kind = PartKind.Punch; AutoNest = true; Dir = CutDir.CW; LeadIn = 5; Tab = 0; SizeCorr = 0;
            Machine = MachineType.Fast; WireD = 0.18; Taper = 0;
            P1 = new PassParam(); P2 = new PassParam(); P3 = new PassParam(); P4 = new PassParam(); RegimeManual = false;
            Format = OutFormat.Both; ProgNo = 1; Zero = ZeroMode.Start; Comp = CompMode.Cam; Comments = CommentMode.Rus; NStep = 10;
            PauseCode = "M00"; EndCode = "M02"; CutWireCode = "M00"; ThreadCode = "M00"; RegimeTemplate = ""; StartLines = "";
            IsoExt = ".nc"; B3Ext = ".3b";
            Link = LinkMethod.Folder; Port = "COM1"; Baud = 9600; DataBits = 8;
            Parity = System.IO.Ports.Parity.None; StopBits = System.IO.Ports.StopBits.One;
            Handshake = System.IO.Ports.Handshake.XOnXOff; Folder = "";
            Presets.ApplyProfile(this);
        }

        void ApplyProfileAfterLoad() { Profile = MachineProfile.DK7750; Folder = ""; Presets.ApplyProfile(this); }

        public PassParam[] Passes { get { return new PassParam[] { P1, P2, P3, P4 }; } }

        public List<PassParam> ActivePasses()
        {
            List<PassParam> l = new List<PassParam>();
            foreach (PassParam p in Passes) if (p.On) l.Add(p);
            if (l.Count == 0) { P1.On = true; l.Add(P1); }
            return l;
        }

        /// <summary>Смещение центра проволоки от контура для прохода (на последнем проходе припуск = 0).</summary>
        public double OffsetOf(PassParam p) { return OffsetOf(p, Kind); }
        public double OffsetOf(PassParam p, PartKind kind)
        {
            List<PassParam> a = ActivePasses();
            double stock = (a[a.Count - 1] == p) ? 0 : p.Stock;
            // коррекция размера: у пуансона «+» = больше детали (проволока дальше), у матрицы «+» = больше отверстие
            return WireD / 2 + p.Gap + stock + (kind == PartKind.Punch ? SizeCorr : -SizeCorr);
        }

        // ---------- сохранение в INI ----------
        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            WriteObj(sb, this, "");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        static void WriteObj(StringBuilder sb, object o, string prefix)
        {
            foreach (PropertyInfo pi in o.GetType().GetProperties())
            {
                if (!pi.CanWrite || pi.GetIndexParameters().Length > 0) continue;
                object v = pi.GetValue(o, null);
                if (v is PassParam) { WriteObj(sb, v, prefix + pi.Name + "."); continue; }
                string s = v == null ? "" : (v is double ? ((double)v).ToString("R", CultureInfo.InvariantCulture) : v.ToString());
                sb.Append(prefix).Append(pi.Name).Append('=').Append(s.Replace("\r", "").Replace("\n", " ")).Append("\r\n");
            }
        }
        public void Load(string path)
        {
            if (!File.Exists(path)) return;
            bool hasProfile = false;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (line.StartsWith("Profile=")) hasProfile = true;
                int k = line.IndexOf('='); if (k <= 0) continue;
                string key = line.Substring(0, k), val = line.Substring(k + 1);
                object target = this; string name = key;
                int dot = key.IndexOf('.');
                if (dot > 0)
                {
                    PropertyInfo pp = GetType().GetProperty(key.Substring(0, dot));
                    if (pp == null) continue;
                    target = pp.GetValue(this, null); name = key.Substring(dot + 1);
                }
                PropertyInfo pi = target.GetType().GetProperty(name);
                if (pi == null || !pi.CanWrite) continue;
                try
                {
                    Type t = pi.PropertyType; object v;
                    if (t == typeof(string)) v = val;
                    else if (t == typeof(double)) v = double.Parse(val, CultureInfo.InvariantCulture);
                    else if (t == typeof(int)) v = int.Parse(val);
                    else if (t == typeof(bool)) v = bool.Parse(val);
                    else if (t.IsEnum) v = Enum.Parse(t, val);
                    else continue;
                    pi.SetValue(target, v, null);
                }
                catch { }
            }
            // старые настройки (до появления профилей) — подставить профиль станка цеха
            if (!hasProfile) ApplyProfileAfterLoad();
        }
    }

    /// <summary>Примерные режимы (ориентир для наладки, уточняются по паспорту станка).</summary>
    public static class Presets
    {
        public const string AutoCutWork = @"C:\AUTOCUT_6-20171116\AUTOCUT_6-20171116\Work";

        /// <summary>Профиль станка: формат, коды, ход стола, папка стойки.</summary>
        public static void ApplyProfile(JobSettings s)
        {
            switch (s.Profile)
            {
                case MachineProfile.DK7750:
                    // SIMOS DK7750, стойка AutoCut 6.0 (Windows 7), быстрая протяжка, молибден 0.18
                    s.Machine = MachineType.Fast; s.WireD = 0.18;
                    s.TravelX = 650; s.TravelY = 550; s.MaxThick = 500;
                    s.Format = OutFormat.B3;           // AutoCut надёжнее всего читает 3B
                    s.Comp = CompMode.Cam;             // 3B без коррекции — смещение считает программа
                    s.Zero = ZeroMode.Start;
                    s.Comments = CommentMode.Lat;      // в ISO для AutoCut — только латиница
                    s.PauseCode = "M00"; s.EndCode = "M02"; s.CutWireCode = "M00"; s.ThreadCode = "M00";
                    s.RegimeTemplate = ""; s.StartLines = "";
                    s.IsoExt = ".ISO"; s.B3Ext = ".3B";
                    if (string.IsNullOrEmpty(s.Folder)) s.Folder = AutoCutWork;
                    s.Link = LinkMethod.Folder;
                    break;
                default:
                    if (s.TravelX <= 0) s.TravelX = 400;
                    if (s.TravelY <= 0) s.TravelY = 300;
                    if (s.MaxThick <= 0) s.MaxThick = 300;
                    break;
            }
            Apply(s);
        }

        public static void Apply(JobSettings s)
        {
            double h = s.Thickness;
            // черновой режим по толщине
            double ton, toff, ip, u, rate;
            if (h <= 20) { ton = 12; toff = 60; ip = 2.5; u = 75; rate = 60; }
            else if (h <= 50) { ton = 20; toff = 120; ip = 3.5; u = 80; rate = 80; }
            else if (h <= 100) { ton = 32; toff = 200; ip = 4.5; u = 85; rate = 100; }
            else if (h <= 200) { ton = 48; toff = 330; ip = 5.0; u = 90; rate = 100; }
            else { ton = 64; toff = 480; ip = 5.5; u = 95; rate = 90; }

            if (s.RegimeManual) { if (s.Machine != MachineType.Slow && s.WireD > 0.21) s.WireD = 0.18; return; }   // режимы задал пользователь — не трогаем
            Set(s.P1, true, "Черновой", 0.010, 0.04, ton, toff, ip, u, rate, 1);
            if (s.Machine != MachineType.Slow) RegimeDb.ApplyTo(s);   // основной режим — из таблицы режимов (материал × толщина)
            switch (s.Machine)
            {
                case MachineType.Fast:
                    s.WireD = s.WireD > 0.21 ? 0.18 : s.WireD;
                    s.P1.Stock = 0;
                    Set(s.P2, false, "Чистовой 1", 0.005, 0.01, 6, 30, 1.5, 70, 150, 2);
                    Set(s.P3, false, "Чистовой 2", 0.003, 0, 3, 18, 0.8, 65, 250, 3);
                    Set(s.P4, false, "Доводка", 0.002, 0, 2, 12, 0.5, 60, 300, 4);
                    break;
                case MachineType.Medium:
                    if (s.WireD > 0.21) s.WireD = 0.18;
                    Set(s.P2, true, "Чистовой 1", 0.005, 0.01, 6, 30, 1.5, 70, 150, 2);
                    Set(s.P3, true, "Чистовой 2", 0.003, 0, 3, 18, 0.8, 65, 250, 3);
                    Set(s.P4, false, "Доводка", 0.002, 0, 2, 12, 0.5, 60, 300, 4);
                    break;
                default: // латунь 0.25, медленная протяжка
                    s.WireD = 0.25;
                    Set(s.P1, true, "Черновой", 0.020, 0.06, 1.2, 10, 12, 60, 150, 1);
                    Set(s.P2, true, "Чистовой 1", 0.008, 0.015, 0.6, 8, 6, 55, 300, 2);
                    Set(s.P3, true, "Чистовой 2", 0.004, 0.003, 0.3, 6, 3, 50, 500, 3);
                    Set(s.P4, false, "Доводка", 0.002, 0, 0.2, 5, 1.5, 45, 700, 4);
                    break;
            }
        }
        static void Set(PassParam p, bool on, string name, double gap, double stock, double ton, double toff, double ip, double u, double rate, int reg)
        {
            p.On = on; p.Name = name; p.Gap = gap; p.Stock = stock; p.Ton = ton; p.Toff = toff; p.Ip = ip; p.U = u; p.Rate = rate; p.RegNo = reg;
        }
    }

    // ---------------- траектория ----------------
    public enum MoveKind { Cut, Rapid, Stop, Regime, Comment, End }

    public class Move
    {
        public MoveKind K;
        public Seg S;
        public string Text;
        public int Pass;          // номер прохода (1..4)
        public PassParam P;
        public int Contour;       // номер контура
        public int Comp;          // для режима «на стойке»: 41 / 42 / 40 / 0
        public bool Lead;         // врезание / выход
    }

    public class ContourJob
    {
        public Contour Geo;
        public V2? ManualStart;
        public LeadMark Lead;      // вход / выход, заданные вручную (кнопка «Вход/выход»)
        public PartKind? Side;     // заход для этого контура: снаружи (пуансон) / изнутри (матрица); null — авто
    }

    public class Toolpath
    {
        public List<Move> Moves = new List<Move>();
        public List<string> Warn = new List<string>();
        public List<V2> Starts = new List<V2>();
        public double CutLen, Minutes, SizeX, SizeY;

        /// <summary>Время на партию, мин: наладка + резка × количество.</summary>
        public double TotalMinutes(JobSettings s) { return s.SetupMin + Minutes * Math.Max(1, s.Qty); }
        public double Cost(JobSettings s) { return TotalMinutes(s) / 60.0 * s.PriceHour + s.MaterialCost; }
        public V2 Origin;

        public static Toolpath Build(List<ContourJob> jobs, JobSettings s)
        {
            Toolpath tp = new Toolpath();
            List<PassParam> passes = s.ActivePasses();
            // глубина вложенности: внутренние контуры — первыми и как матрица
            int[] depth = new int[jobs.Count];
            for (int i = 0; i < jobs.Count; i++)
                if (jobs[i].Geo.Closed)
                    for (int j = 0; j < jobs.Count; j++)
                        if (i != j && jobs[j].Geo.Closed && jobs[j].Geo.Contains(jobs[i].Geo.Start)) depth[i]++;
            List<int> order = new List<int>();
            for (int i = 0; i < jobs.Count; i++) order.Add(i);
            if (s.AutoNest) order.Sort(delegate (int a, int b) { return depth[a] != depth[b] ? depth[b].CompareTo(depth[a]) : a.CompareTo(b); });
            int ci = 0;
            foreach (int jo in order)
            {
                ContourJob job = jobs[jo];
                PartKind kind = (s.AutoNest && depth[jo] % 2 == 1) ? (s.Kind == PartKind.Punch ? PartKind.Die : PartKind.Punch) : s.Kind;
                if (job.Side.HasValue) kind = job.Side.Value;   // сторона захода задана для контура вручную
                ci++;
                Contour c = job.Geo.Clone();
                if (!c.Closed)
                {
                    // открытый контур — режем по линии без коррекции
                    V2 st = c.Start;
                    tp.Starts.Add(st);
                    if (ci > 1) tp.Transition(st, ci);
                    tp.Add(MoveKind.Comment, null, "КОНТУР " + ci + " (ОТКРЫТЫЙ, БЕЗ КОРРЕКЦИИ)", ci, passes[0], 1);
                    tp.Add(MoveKind.Regime, null, null, ci, passes[0], 1);
                    foreach (Seg sg in c.Segs) tp.Add(MoveKind.Cut, sg.Clone(), null, ci, passes[0], 1);
                    tp.Warn.Add("Контур " + ci + " не замкнут — режется по линии без смещения");
                    continue;
                }
                double area = c.Area;
                bool wantCcw = s.Dir == CutDir.CCW;
                if ((area > 0) != wantCcw) { c = c.Reversed(); area = -area; }
                bool ccw = area > 0;
                // сторона проволоки: + слева, − справа
                double side = (kind == PartKind.Punch ? -1 : 1) * (ccw ? 1 : -1);

                if (job.Lead != null) c = c.StartAt(job.Lead.Entry);
                else if (job.ManualStart.HasValue) c = c.StartAt(job.ManualStart.Value);
                else
                {
                    // авто: середина самого длинного прямого участка
                    int best = -1; double bl = 0;
                    for (int i = 0; i < c.Segs.Count; i++)
                        if (c.Segs[i].Kind == SegKind.Line && c.Segs[i].Length > bl) { bl = c.Segs[i].Length; best = i; }
                    if (best < 0 || bl < s.LeadIn)
                    {
                        // мелкие элементы (шестерня, шлицы): врезание в самую дальнюю от центра точку
                        // для пуансона (вершина зуба), в ближайшую — для матрицы. Рабочие боковые поверхности не трогаем.
                        double sx = 0, sy = 0, sl = 0;
                        foreach (Seg sg in c.Segs) { V2 m = sg.PointAt(0.5); double l = sg.Length; sx += m.X * l; sy += m.Y * l; sl += l; }
                        V2 cen = new V2(sx / sl, sy / sl);
                        int bi = 0; double bd = kind == PartKind.Punch ? -1 : double.MaxValue;
                        for (int i = 0; i < c.Segs.Count; i++)
                        {
                            double d = V2.Dist(c.Segs[i].PointAt(0.5), cen);
                            if (kind == PartKind.Punch ? d > bd : d < bd) { bd = d; bi = i; }
                        }
                        c = c.StartAt(bi, 0.5);
                    }
                    else c = c.StartAt(best, 0.5);
                }

                // контуры проходов
                List<Contour> oc = new List<Contour>();
                bool fail = false;
                foreach (PassParam p in passes)
                {
                    double off = s.Comp == CompMode.Cam ? s.OffsetOf(p, kind) : 0;
                    Contour o = Geo.Offset(c, side * off, tp.Warn);
                    if (o == null || o.Segs.Count == 0) { fail = true; break; }
                    oc.Add(o);
                }
                if (fail) { tp.Warn.Add("Контур " + ci + " пропущен"); continue; }

                // ---- заход проволоки: прямой / под углом / по дуге ----
                V2 S;
                List<Seg> lead = BuildLead(s, job, oc[0], side, out S);
                tp.Starts.Add(S);
                if (ci > 1) tp.Transition(S, ci);
                tp.Add(MoveKind.Comment, null, "КОНТУР " + ci + ": " + (kind == PartKind.Punch ? "ЗАХОД СНАРУЖИ (ПУАНСОН)" : "ЗАХОД ИЗНУТРИ (МАТРИЦА)") + ", " + (ccw ? "ПРОТИВ ЧАСОВОЙ" : "ПО ЧАСОВОЙ"), ci, passes[0], 1);
                int compCode = side > 0 ? 41 : 42;

                double tab = s.Tab;
                if (tab > 0 && tab >= oc[0].Length * 0.5) { tp.Warn.Add("Перемычка длиннее половины контура " + ci + " — отключена"); tab = 0; }
                if (tab <= 0 && passes.Count > 1 && kind == PartKind.Punch)
                    tp.Warn.Add("Контур " + ci + ": пуансон в несколько проходов без перемычки — деталь выпадет после 1-го прохода. Задайте перемычку.");

                List<List<Seg>> mains = new List<List<Seg>>(), tails = new List<List<Seg>>();
                foreach (Contour o in oc)
                {
                    List<Seg> h = new List<Seg>(), t = new List<Seg>();
                    if (tab > 0) Geo.SplitByLength(o.Segs, o.Length - tab, h, t);
                    else foreach (Seg sg in o.Segs) h.Add(sg.Clone());
                    mains.Add(h); tails.Add(t);
                }

                // ручной выход раньше входа (по ходу резки) — резка до точки выхода, остаток — перемычка
                bool manualTab = false;
                if (job.Lead != null && job.Lead.Exit.HasValue && tab <= 0)
                {
                    double lex = oc[0].LenTo(job.Lead.Exit.Value), L0 = oc[0].Length;
                    if (lex > L0 * 0.5 && passes.Count == 1)
                    {
                        List<Seg> h = new List<Seg>(), t = new List<Seg>();
                        Geo.SplitByLength(oc[0].Segs, lex, h, t);
                        if (h.Count > 0)
                        {
                            mains[0] = h; manualTab = true;
                            tp.Warn.Add("Контур " + ci + ": выход раньше входа — между ними остаётся перемычка " + Geo.F(Math.Round(L0 - lex, 2)) + " мм, деталь НЕ отделена (дорезать отдельно)");
                        }
                    }
                }

                V2 pos = S; bool atT = false;
                for (int k = 0; k < passes.Count; k++)
                {
                    PassParam p = passes[k];
                    int pn = Array.IndexOf(s.Passes, p) + 1;
                    tp.Add(MoveKind.Regime, null, null, ci, p, pn);
                    V2 P0 = oc[k].Start;
                    if (k == 0)
                    {
                        tp.AddLead(lead, "ВРЕЗАНИЕ", ci, p, pn, s.Comp == CompMode.Cnc ? compCode : 0);
                        foreach (Seg sg in mains[k]) tp.Add(MoveKind.Cut, sg, null, ci, p, pn);
                        atT = tab > 0;
                        pos = atT ? mains[k][mains[k].Count - 1].End : P0;
                        if (!atT) pos = oc[k].End;
                    }
                    else if (tab > 0)
                    {
                        if (atT)
                        {
                            V2 T = mains[k][mains[k].Count - 1].End;
                            tp.Link(pos, T, ci, p, pn);
                            foreach (Seg sg in Geo.ReverseList(mains[k])) tp.Add(MoveKind.Cut, sg, null, ci, p, pn);
                            pos = P0; atT = false;
                        }
                        else
                        {
                            tp.Link(pos, P0, ci, p, pn);
                            foreach (Seg sg in mains[k]) tp.Add(MoveKind.Cut, sg, null, ci, p, pn);
                            pos = mains[k][mains[k].Count - 1].End; atT = true;
                        }
                    }
                    else
                    {
                        tp.Link(pos, P0, ci, p, pn);
                        foreach (Seg sg in mains[k]) tp.Add(MoveKind.Cut, sg, null, ci, p, pn);
                        pos = oc[k].End;
                    }
                }
                PassParam lp = passes[passes.Count - 1];
                int lpn = Array.IndexOf(s.Passes, lp) + 1;
                if (manualTab) { pos = mains[0][mains[0].Count - 1].End; tp.Add(MoveKind.Comment, null, "ПЕРЕМЫЧКА МЕЖДУ ВЫХОДОМ И ВХОДОМ НЕ РЕЖЕТСЯ", ci, lp, lpn); }
                if (atT)
                {
                    tp.Add(MoveKind.Comment, null, "ВОЗВРАТ ПО ПРОРЕЗИ", ci, lp, lpn);
                    foreach (Seg sg in Geo.ReverseList(mains[passes.Count - 1])) tp.Add(MoveKind.Cut, sg, null, ci, lp, lpn);
                    pos = oc[passes.Count - 1].Start;
                }
                LeadMark lm = job.Lead;
                if (lm != null && tab > 0 && (lm.Exit.HasValue || lm.Out.HasValue))
                { tp.Warn.Add("Контур " + ci + ": при перемычке ручной выход не используется — выход через перемычку"); lm = null; }
                if (lm != null && (lm.Exit.HasValue || lm.Out.HasValue))
                {
                    // ---- ручной выход: перекрытие по контуру до точки выхода, затем сход ----
                    Contour ol = oc[passes.Count - 1];
                    if (lm.Exit.HasValue && !manualTab)
                    {
                        List<Seg> over = ol.Upto(lm.Exit.Value);
                        if (over.Count > 0)
                        {
                            double ol2 = 0; foreach (Seg x in over) ol2 += x.Length;
                            if (ol2 > ol.Length * 0.5) tp.Warn.Add("Контур " + ci + ": перекрытие до точки выхода " + Geo.F(Math.Round(ol2, 1)) + " мм — больше половины контура (в несколько проходов перемычку выходом не оставить; проверьте направление обхода)");
                            for (int i = 0; i < over.Count; i++) tp.Add(MoveKind.Cut, over[i], i == 0 ? "ПЕРЕКРЫТИЕ ДО ТОЧКИ ВЫХОДА" : null, ci, lp, lpn);
                            pos = over[over.Count - 1].End;
                        }
                    }
                    V2 outP;
                    if (lm.Out.HasValue) outP = lm.Out.Value;
                    else if (lm.Exit.HasValue)
                    {
                        // по нормали от контура в сторону проволоки на длину захода
                        int si; double st; ol.DistTo(pos, out si, out st);
                        V2 tg = ol.Segs[si].TanAt(st);
                        outP = pos + tg.Perp() * (Math.Sign(side) * Math.Max(s.LeadIn, 0.5));
                    }
                    else outP = S;
                    List<Seg> outs = new List<Seg>();
                    if (V2.Dist(pos, outP) > 1e-6) outs.Add(Seg.Line(pos, outP));
                    tp.AddLead(outs, "ВЫХОД", ci, lp, lpn, s.Comp == CompMode.Cnc ? 40 : 0);
                }
                else
                {
                // выход — обратно по пути захода
                List<Seg> back = new List<Seg>();
                if (V2.Dist(pos, oc[0].Start) > 1e-6) back.Add(Seg.Line(pos, oc[0].Start));
                back.AddRange(Geo.ReverseList(lead));
                tp.AddLead(back, "ВЫХОД В ТОЧКУ ЗАПРАВКИ", ci, lp, lpn, s.Comp == CompMode.Cnc ? 40 : 0);
                }

                if (tab > 0)
                {
                    tp.Add(MoveKind.Stop, null, "ЗАКРЕПИТЬ ДЕТАЛЬ - РЕЗКА ПЕРЕМЫЧКИ", ci, passes[0], 1);
                    tp.Add(MoveKind.Regime, null, null, ci, passes[0], 1);
                    tp.AddLead(lead, "ВРЕЗАНИЕ К ПЕРЕМЫЧКЕ", ci, passes[0], 1, s.Comp == CompMode.Cnc ? compCode : 0);
                    foreach (Seg sg in Geo.ReverseList(tails[0])) tp.Add(MoveKind.Cut, sg, null, ci, passes[0], 1);
                    tp.Add(MoveKind.Comment, null, "ДЕТАЛЬ ОТДЕЛЕНА", ci, passes[0], 1);
                }
            }
            tp.Add(MoveKind.End, null, "КОНЕЦ ПРОГРАММЫ", ci, passes[0], 1);
            tp.Origin = tp.Starts.Count > 0 ? tp.Starts[0] : new V2();
            // проверка хода стола и толщины по паспорту станка
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (Move m in tp.Moves) if (m.S != null) m.S.Bounds(ref x0, ref y0, ref x1, ref y1);
                if (x1 > x0)
                {
                    tp.SizeX = x1 - x0; tp.SizeY = y1 - y0;
                    if (s.TravelX > 0 && tp.SizeX > s.TravelX) tp.Warn.Add("Траектория по X " + Geo.F(Math.Round(tp.SizeX, 1)) + " мм больше хода стола " + Geo.F(s.TravelX) + " мм");
                    if (s.TravelY > 0 && tp.SizeY > s.TravelY) tp.Warn.Add("Траектория по Y " + Geo.F(Math.Round(tp.SizeY, 1)) + " мм больше хода стола " + Geo.F(s.TravelY) + " мм");
                }
                if (s.MaxThick > 0 && s.Thickness > s.MaxThick) tp.Warn.Add("Толщина " + Geo.F(s.Thickness) + " мм больше допустимой для станка (" + Geo.F(s.MaxThick) + " мм)");
            }
            // длина и время
            foreach (Move m in tp.Moves)
                if (m.K == MoveKind.Cut)
                {
                    double l = m.S.Length; tp.CutLen += l;
                    double v = m.P.Rate > 0 && s.Thickness > 0 ? m.P.Rate / s.Thickness : 1;
                    tp.Minutes += l / v;
                }
            return tp;
        }

        /// <summary>Путь захода от точки заправки S до начала контура прохода 1.</summary>
        static List<Seg> BuildLead(JobSettings s, ContourJob job, Contour oc0, double side, out V2 S)
        {
            List<Seg> lead = new List<Seg>();
            V2 P0 = oc0.Start, tan = oc0.Segs[0].TanStart;
            V2 nW = tan.Perp() * Math.Sign(side);           // от контура в сторону проволоки (отход)
            if (job.Lead != null && job.Lead.Thread.HasValue) { S = job.Lead.Thread.Value; lead.Add(Seg.Line(S, P0)); return lead; }
            if (job.ManualStart.HasValue) { S = job.ManualStart.Value; lead.Add(Seg.Line(S, P0)); return lead; }
            double L = Math.Max(s.LeadIn, 0.5);
            switch (s.LeadType)
            {
                case LeadKind.Angled:
                    {
                        double ph = Math.Max(0, Math.Min(80, s.LeadAngle)) * Math.PI / 180;
                        V2 d = nW * Math.Cos(ph) - tan * Math.Sin(ph);   // подход наклонно, по ходу резки
                        S = P0 + d * L; lead.Add(Seg.Line(S, P0));
                        return lead;
                    }
                case LeadKind.Arc:
                    {
                        double r = Math.Max(0.2, Math.Min(s.LeadArcR, L));
                        V2 cc = P0 + nW * r;
                        double aE = (P0 - cc).Angle;
                        double sw = V2.Cross(tan, nW) > 0 ? Math.PI / 2 : -Math.PI / 2;
                        Seg arc = Seg.Arc(cc, r, aE - sw, sw);   // четверть окружности, на контур — по касательной
                        V2 A = arc.Start;
                        S = A - arc.TanStart * Math.Max(L - r, 0.5);
                        lead.Add(Seg.Line(S, A)); lead.Add(arc);
                        return lead;
                    }
                default:
                    S = P0 + nW * L; lead.Add(Seg.Line(S, P0));
                    return lead;
            }
        }

        public void AddLead(List<Seg> segs, string text, int ci, PassParam p, int pn, int comp)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                Move m = Add(MoveKind.Cut, segs[i].Clone(), i == 0 ? text : null, ci, p, pn);
                m.Lead = true; m.Comp = i == 0 ? comp : 0;
            }
        }

        Move Add(MoveKind k, Seg sg, string text, int ci, PassParam p, int pn)
        {
            Move m = new Move { K = k, S = sg, Text = text, Contour = ci, P = p, Pass = pn };
            Moves.Add(m); return m;
        }

        void Link(V2 a, V2 b, int ci, PassParam p, int pn)
        {
            if (V2.Dist(a, b) > 1e-6) Add(MoveKind.Cut, Seg.Line(a, b), "ПЕРЕХОД НА ПРОХОД " + pn, ci, p, pn);
        }

        void Transition(V2 next, int ci)
        {
            V2 cur = Moves.Count > 0 ? LastPos() : next;
            Add(MoveKind.Stop, null, "ОТРЕЗАТЬ ПРОВОЛОКУ", ci, null, 0).Comp = -1;
            Add(MoveKind.Rapid, Seg.Line(cur, next), "ПЕРЕХОД К КОНТУРУ " + ci, ci, null, 0);
            Add(MoveKind.Stop, null, "ЗАПРАВИТЬ ПРОВОЛОКУ В ОТВ. КОНТУРА " + ci, ci, null, 0).Comp = -2;
        }

        public V2 LastPos()
        {
            for (int i = Moves.Count - 1; i >= 0; i--) if (Moves[i].S != null) return Moves[i].S.End;
            return new V2();
        }
    }
}
