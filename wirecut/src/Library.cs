// STZ WireCut — библиотека типовых деталей: шестерня прямозубая (эвольвента), отверстие со шпоночным пазом (ГОСТ 23360-78)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireCut
{
    // ======================= ШПОНОЧНЫЙ ПАЗ =======================
    public static class Gost23360
    {
        // d от, d до (включительно), b, h, t1 (вал), t2 (втулка)
        static readonly double[,] T = {
            {6,8,2,2,1.2,1.0},{8,10,3,3,1.8,1.4},{10,12,4,4,2.5,1.8},{12,17,5,5,3.0,2.3},{17,22,6,6,3.5,2.8},
            {22,30,8,7,4.0,3.3},{30,38,10,8,5.0,3.3},{38,44,12,8,5.0,3.3},{44,50,14,9,5.5,3.8},{50,58,16,10,6.0,4.3},
            {58,65,18,11,7.0,4.4},{65,75,20,12,7.5,4.9},{75,85,22,14,9.0,5.4},{85,95,25,14,9.0,5.4},{95,110,28,16,10.0,6.4},
            {110,130,32,18,11.0,7.4},{130,150,36,20,12.0,8.4},{150,170,40,22,13.0,9.4},{170,200,45,25,15.0,10.4},{200,230,50,28,17.0,11.4},
            {230,260,56,32,20.0,12.4},{260,290,63,32,20.0,12.4},{290,330,70,36,22.0,14.4},{330,380,80,40,25.0,15.4},{380,440,90,45,28.0,17.4},
            {440,500,100,50,31.0,19.5} };

        /// <summary>Радиус закругления r в углах дна паза (вал и втулка), ГОСТ 23360-78, табл. 1 — по ширине паза b.</summary>
        public static void RRange(double b, out double rmin, out double rmax)
        {
            if (b <= 3) { rmin = 0.08; rmax = 0.16; }          // 2×2, 3×3
            else if (b <= 6) { rmin = 0.16; rmax = 0.25; }     // 4×4 … 6×6
            else if (b <= 18) { rmin = 0.25; rmax = 0.40; }    // 8×7 … 18×11
            else if (b <= 32) { rmin = 0.40; rmax = 0.60; }    // 20×12 … 32×18
            else if (b <= 50) { rmin = 0.70; rmax = 1.00; }    // 36×20 … 50×28
            else if (b <= 80) { rmin = 1.20; rmax = 1.60; }    // 56×32 … 80×40
            else { rmin = 2.00; rmax = 2.50; }                 // 90×45, 100×50
        }

        public static bool Find(double d, out double b, out double h, out double t2)
        {
            b = h = t2 = 0;
            for (int i = 0; i < T.GetLength(0); i++)
                if (d > T[i, 0] && d <= T[i, 1]) { b = T[i, 2]; h = T[i, 3]; t2 = T[i, 5]; return true; }
            return false;
        }
    }

    [TypeConverter(typeof(RusEnumConverter))]
    public enum KeyFit
    {
        [Description("JS9 — нормальное (по умолчанию)")] JS9,
        [Description("P9 — плотное")] P9,
        [Description("D10 — свободное")] D10
    }

    [TypeConverter(typeof(RusEnumConverter))]
    public enum KeyRadius
    {
        [Description("По ГОСТ — среднее значение")] Mid,
        [Description("По ГОСТ — наименьший")] Min,
        [Description("По ГОСТ — наибольший")] Max,
        [Description("Свой радиус")] Custom,
        [Description("Без радиуса (острые углы)")] None
    }

    [TypeConverter(typeof(OrderConverter))]
    public class KeyParams
    {
        /// <summary>Наибольшее смещение проволоки (Ø/2 + зазор + припуск) — меньший радиус в углу проволока сделать не может. Ставит главное окно.</summary>
        public static double WireLimit = 0.10;
        public string Note;    // замечание после построения (радиус меньше возможного проволокой)
        const string C1 = "1. Отверстие", C2 = "2. Шпоночный паз (ГОСТ 23360-78)", C3 = "3. Вставка";
        double d = 40;
        [Category(C1), DisplayName("Диаметр отверстия d, мм"), Description("При изменении ширина и глубина паза подставляются по ГОСТ 23360-78")]
        public double D { get { return d; } set { d = value; double b, h, t2; if (Gost23360.Find(d, out b, out h, out t2)) { B = b; T2 = t2; KeyH = h; } } }
        [Category(C1), DisplayName("Отверстие уже расточено"), Description("Да — режется только паз (проволока заправляется в готовое отверстие). Нет — отверстие и паз одним контуром"), TypeConverter(typeof(RusBool))]
        public bool BoreDone { get; set; }
        [Category(C2), DisplayName("Ширина паза b, мм")] public double B { get; set; }
        [Category(C2), DisplayName("Глубина паза во втулке t2, мм")] public double T2 { get; set; }
        [Category(C2), DisplayName("Размер d + t2, мм"), Description("Контрольный размер от противоположной стенки отверстия до дна паза")]
        public double DT2 { get { return Math.Round(d + T2, 3); } }
        [Category(C2), DisplayName("Высота шпонки h, мм"), ReadOnly(true)] public double KeyH { get; set; }
        [Category(C2), DisplayName("Посадка по ширине")] public KeyFit Fit { get; set; }
        KeyRadius rmode = KeyRadius.Mid; double rCustom = 0.3;
        [Category(C2), DisplayName("Радиус в углах дна паза"), Description("ГОСТ 23360-78: закругление r в углах дна паза по ширине b (например, для 8…18 мм — r 0.25…0.40). «Свой» — впишите значение в поле «Радиус r»")]
        public KeyRadius RMode { get { return rmode; } set { if (value == KeyRadius.Custom && rmode != KeyRadius.Custom) rCustom = RGost(KeyRadius.Mid); rmode = value; } }
        [Category(C2), DisplayName("Радиус r, мм"), Description("Радиус в двух углах дна паза. Если ввести своё значение — режим станет «Свой радиус». 0 — острые углы")]
        public double Rv { get { return rmode == KeyRadius.Custom ? rCustom : RGost(rmode); } set { if (Math.Abs(value - Rv) > 1e-9) { rCustom = Math.Max(0, value); rmode = value <= 0 ? KeyRadius.None : KeyRadius.Custom; } } }
        [Category(C2), DisplayName("Радиус r по ГОСТ, мм"), Description("Допустимый диапазон радиуса для этой ширины паза")]
        public string RGostText { get { double a, b; Gost23360.RRange(B, out a, out b); return Geo.F(a) + " … " + Geo.F(b); } }
        double RGost(KeyRadius m)
        {
            double a, b; Gost23360.RRange(B, out a, out b);
            switch (m) { case KeyRadius.Min: return a; case KeyRadius.Max: return b; case KeyRadius.None: return 0; default: return Math.Round((a + b) / 2, 3); }
        }
        [Category(C2), DisplayName("Количество пазов"), Description("1 или 2 (под 180°)")] public int Count { get; set; }
        [Category(C2), DisplayName("Угол паза, °"), Description("0° — паз вверх (по оси +Y)")] public double Angle { get; set; }
        [Category(C3), DisplayName("Центр X, мм")] public double X { get; set; }
        [Category(C3), DisplayName("Центр Y, мм")] public double Y { get; set; }

        public KeyParams() { Count = 1; Fit = KeyFit.JS9; D = 40; }

        /// <summary>Середина поля допуска ширины паза во втулке (мм), для коррекции.</summary>
        public double FitMid()
        {
            double b = B;
            switch (Fit)
            {
                case KeyFit.P9: // P9: отклонения ≈ −(0.006…0.061) — середина
                    return b <= 3 ? -0.0185 : b <= 6 ? -0.027 : b <= 10 ? -0.0335 : b <= 18 ? -0.0395 : b <= 30 ? -0.047 : b <= 50 ? -0.0555 : -0.0655;
                case KeyFit.D10:
                    return b <= 3 ? 0.04 : b <= 6 ? 0.054 : b <= 10 ? 0.069 : b <= 18 ? 0.085 : b <= 30 ? 0.107 : b <= 50 ? 0.130 : 0.160;
                default: return 0; // JS9 симметрично
            }
        }

        public string Info()
        {
            return "Отверстие Ø" + Geo.F(d) + ", паз " + Geo.F(B) + " " + RusEnumConverter.Text(Fit).Split(' ')[0] + " × t2 " + Geo.F(T2) + " (d+t2 = " + Geo.F(DT2) + ")" +
                (Rv > 0 ? ", r " + Geo.F(Rv) : ", углы острые") + (Count > 1 ? ", 2 паза" : "") + (BoreDone ? ", только паз" : "");
        }

        /// <summary>Контур: окружность с пазом (или прямоугольник паза, заходящий в готовое отверстие).</summary>
        public List<Seg> Build(out string err)
        {
            err = null; Note = null;
            List<Seg> r = new List<Seg>();
            double R = d / 2, b = B + FitMid(), hb = b / 2, top = d + T2 - R; // дно паза от центра
            if (d <= 0 || b <= 0 || T2 <= 0 || hb >= R) { err = "Проверьте d, b, t2"; return r; }
            // радиус в углах дна паза: не меньше того, что делает проволока (иначе траектория не построится)
            double rf = Math.Max(0, Rv);
            if (rf > 0 && rf <= WireLimit)
            {
                Note = "r " + Geo.F(rf) + " меньше смещения проволоки (Ø/2 + зазор + припуск ≈ " + Geo.F(Math.Round(WireLimit, 3)) + ") — радиус увеличен до " + Geo.F(Math.Round(WireLimit + 0.005, 3)) + ", меньше проволока не сделает";
                rf = WireLimit + 0.005;
            }
            if (rf > hb * 0.95) { rf = hb * 0.95; Note = "Радиус уменьшен до " + Geo.F(Math.Round(rf, 3)) + " (больше половины ширины паза)"; }
            int n = Count >= 2 ? 2 : 1;
            V2 c = new V2(X, Y);
            double rot = (Angle + 90) * Math.PI / 180; // ось паза
            if (BoreDone)
            {
                // прямоугольник паза: от хорды внутри отверстия (на 1 мм ниже стенки) до дна
                double y0 = Math.Sqrt(R * R - hb * hb) - 1.0;
                for (int k = 0; k < n; k++)
                {
                    double a = rot + k * Math.PI;
                    Func2 T = delegate (double x, double y) { return c + Geo.Rot(L(x, y), new V2(), a - Math.PI / 2); };
                    r.Add(Seg.Line(T(-hb, y0), T(hb, y0)));
                    Slot(r, T, a, hb, y0, top, Math.Min(rf, (top - y0) * 0.9));
                }
                return r;
            }
            // отверстие + паз(ы) одним контуром
            double yc = Math.Sqrt(R * R - hb * hb);       // где стенка паза пересекает окружность
            double half = Math.Asin(hb / R);               // полуугол хорды паза
            for (int k = 0; k < n; k++)
            {
                double a = rot + k * Math.PI;
                Func2 T = delegate (double x, double y) { return c + Geo.Rot(L(x, y), new V2(), a - Math.PI / 2); };
                Slot(r, T, a, hb, yc, top, Math.Min(rf, (top - yc) * 0.9));
                // дуга отверстия до следующего паза (против часовой)
                double a0 = a + half, sw = (n == 2 ? Math.PI : 2 * Math.PI) - 2 * half;
                r.Add(Seg.Arc(c, R, a0, sw));
            }
            return r;
        }
        delegate V2 Func2(double x, double y);

        /// <summary>Стенки и дно паза (против часовой): правая стенка вверх, дно, левая стенка вниз; в углах дна — радиус rf.</summary>
        static void Slot(List<Seg> r, Func2 T, double a, double hb, double y0, double top, double rf)
        {
            if (rf <= 1e-6)
            {
                r.Add(Seg.Line(T(hb, y0), T(hb, top))); r.Add(Seg.Line(T(hb, top), T(-hb, top))); r.Add(Seg.Line(T(-hb, top), T(-hb, y0)));
                return;
            }
            double turn = a - Math.PI / 2;   // поворот местных координат паза
            r.Add(Seg.Line(T(hb, y0), T(hb, top - rf)));
            r.Add(Seg.Arc(T(hb - rf, top - rf), rf, 0 + turn, Math.PI / 2));
            r.Add(Seg.Line(T(hb - rf, top), T(-hb + rf, top)));
            r.Add(Seg.Arc(T(-hb + rf, top - rf), rf, Math.PI / 2 + turn, Math.PI / 2));
            r.Add(Seg.Line(T(-hb, top - rf), T(-hb, y0)));
        }
        static V2 L(double x, double y) { return new V2(x, y); }
    }

    // ======================= ШЕСТЕРНЯ =======================
    [TypeConverter(typeof(OrderConverter))]
    public class GearParams
    {
        const string C1 = "1. Зубчатый венец", C2 = "2. Исходный контур (ГОСТ 13755)", C3 = "3. Посадочное отверстие", C4 = "4. Расчётные размеры", C5 = "5. Вставка";
        [Category(C1), DisplayName("Модуль m, мм")] public double M { get; set; }
        [Category(C1), DisplayName("Число зубьев z")] public int Z { get; set; }
        [Category(C1), DisplayName("Смещение x"), Description("Коэффициент смещения исходного контура (+ — зуб толще у основания, против подрезания при z < 17)")] public double X { get; set; }
        [Category(C1), DisplayName("Утонение зуба, мм"), Description("Уменьшение толщины зуба по делительной окружности — боковой зазор в передаче (обычно 0.05…0.15)")] public double Thin { get; set; }
        [Category(C1), DisplayName("Внутренний венец"), Description("Да — зубья внутри (кольцо с внутренним зацеплением). Тогда «Отверстие Ø» = наружный Ø кольца"), TypeConverter(typeof(RusBool))] public bool Internal { get; set; }
        [Category(C1), DisplayName("Точек на эвольвенту"), Description("Сколько отрезков на один бок зуба (больше — точнее, длиннее УП)")] public int Pts { get; set; }
        [Category(C2), DisplayName("Угол профиля α, °")] public double Alpha { get; set; }
        [Category(C2), DisplayName("Коэфф. высоты головки ha*")] public double Ha { get; set; }
        [Category(C2), DisplayName("Коэфф. радиального зазора c*")] public double Cc { get; set; }
        [Category(C2), DisplayName("Коэфф. радиуса у ножки ρf*")] public double Rf { get; set; }
        double daSet; string daSig = "";
        string Sig() { return M.ToString("R") + "|" + Z + "|" + X.ToString("R") + "|" + Ha.ToString("R") + "|" + Alpha.ToString("R") + "|" + Internal; }
        [Category(C2), DisplayName("Ø вершин задан, мм"), Description("0 — по формуле ГОСТ 13755. Для пары со смещением STZ укорачивает вершины на уравнительное смещение Δy — тогда здесь Ø вершин из STZ или с чертежа. Сбрасывается сам при изменении m, z, x, ha*, α")]
        public double DaSet { get { return DaEff; } set { daSet = Math.Max(0, value); daSig = Sig(); } }
        /// <summary>Заданный Ø вершин, если параметры колеса с тех пор не менялись.</summary>
        double DaEff { get { return daSet > 0 && daSig == Sig() ? daSet : 0; } }
        string source = "";
        /// <summary>Откуда колесо (например, «из STZ: …») — пока параметры не меняли.</summary>
        [Browsable(false)] public string Source { get { return daSig == Sig() ? source : ""; } set { source = value ?? ""; daSig = Sig(); } }
        [Category(C3), DisplayName("Отверстие Ø, мм"), Description("0 — без отверстия")] public double Bore { get; set; }
        [Category(C3), DisplayName("Шпоночный паз по ГОСТ"), TypeConverter(typeof(RusBool))] public bool Key { get; set; }
        [Category(C3), DisplayName("Посадка паза")] public KeyFit Fit { get; set; }
        [Category(C4), DisplayName("Делительный Ø d")] public double Dd { get { return Math.Round(M * Z, 3); } }
        [Category(C4), DisplayName("Ø вершин da"), Description("Для внутреннего венца — внутренний Ø по вершинам зубьев")] public double Da { get { return DaEff > 0 ? Math.Round(DaEff, 3) : Math.Round(Internal ? M * Z - 2 * M * (Ha - X) : M * Z + 2 * M * (Ha + X), 3); } }
        [Category(C4), DisplayName("Ø впадин df")] public double Df { get { return Math.Round(Internal ? M * Z + 2 * M * (Ha + Cc + X) : M * Z - 2 * M * (Ha + Cc - X), 3); } }
        // переопределение диаметров (для шлицев): большой и малый Ø профиля, 0 — по формулам
        [Browsable(false)] public double OverOuterD { get; set; }
        [Browsable(false)] public double OverInnerD { get; set; }
        [Category(C4), DisplayName("Основной Ø db")] public double Db { get { return Math.Round(M * Z * Math.Cos(Alpha * Math.PI / 180), 3); } }
        [Category(C4), DisplayName("Число зубьев в длине W, k")] public int Kw
        {
            get
            {
                // ГОСТ 16532: z_w = z/π·(tg α_x − 2x·tg α / z − inv α) + 0,5 (с учётом смещения, как в STZ)
                double a = Alpha * Math.PI / 180, ax = Math.Acos(Math.Min(1, Z * Math.Cos(a) / (Z + 2 * X)));
                int zw = (int)Math.Floor(Z / Math.PI * (Math.Tan(ax) - 2 * X * Math.Tan(a) / Z - Inv(a)) + 1.0);
                return Math.Max(2, Math.Min(Z - 1, zw));
            }
        }
        [Category(C4), DisplayName("Длина общей нормали W, мм"), Description("Контроль микрометром с тарелками (с учётом утонения)")]
        public double W
        {
            get
            {
                double a = Alpha * Math.PI / 180, inv = Math.Tan(a) - a;
                if (Internal) return 0; // у внутреннего венца контроль по роликам
                return Math.Round(M * Math.Cos(a) * ((Kw - 0.5) * Math.PI + Z * inv) + 2 * X * M * Math.Sin(a) - Thin * Math.Cos(a), 3);
            }
        }
        [Category(C5), DisplayName("Центр X, мм")] public double Cx { get; set; }
        [Category(C5), DisplayName("Центр Y, мм")] public double Cy { get; set; }

        public GearParams() { M = 2; Z = 30; X = 0; Thin = 0.06; Pts = 16; Alpha = 20; Ha = 1; Cc = 0.25; Rf = 0.38; Bore = 20; Key = true; Fit = KeyFit.JS9; }

        public string Info() { return (Source != "" ? "[" + Source + "]  " : "") + (Internal ? "Венец внутренний m" : "Шестерня m") + Geo.F(M) + " z" + Z + (X != 0 ? " x" + Geo.F(X) : "") + ": da " + Geo.F(Da) + ", df " + Geo.F(Df) + (Internal ? "" : ", W(k=" + Kw + ") = " + Geo.F(W)); }

        static double Inv(double a) { return Math.Tan(a) - a; }

        public List<Seg> Build(List<string> warn)
        {
            List<Seg> res = new List<Seg>();
            if (M <= 0 || Z < 6) { warn.Add("Модуль > 0, зубьев не меньше 6"); return res; }
            double a = Alpha * Math.PI / 180;
            double r = M * Z / 2, rb = r * Math.Cos(a), ra = r + M * (Ha + X), rf = r - M * (Ha + Cc - X);
            double s = M * (Math.PI / 2 + 2 * X * Math.Tan(a)) - Thin;   // толщина зуба по делительной
            if (Internal)
            {
                // отверстие внутреннего венца = «шестерня», у которой зуб — это впадина венца
                ra = r + M * (Ha + Cc + X);                                // впадины венца
                rf = r - M * (Ha - X);                                     // вершины зубьев венца
                s = M * (Math.PI / 2 + 2 * X * Math.Tan(a)) + Thin;        // ширина впадины венца (+ боковой зазор)
            }
            if (DaEff > 0) { if (Internal) rf = DaEff / 2; else ra = DaEff / 2; }   // Ø вершин из STZ / чертежа
            if (OverOuterD > 0) ra = OverOuterD / 2;
            if (OverInnerD > 0) rf = OverInnerD / 2;
            double psi = s / (2 * r);                                     // полуугол толщины
            double gap = Math.PI / Z;                                      // полуугол шага
            // полярный угол бока на радиусе R (от оси зуба)
            Fn1<double, double> phi = delegate (double R) { double aR = Math.Acos(Math.Min(1, rb / R)); return psi + Inv(a) - Inv(aR); };
            if (phi(ra) <= 0.002) warn.Add("Зуб заострён на вершине — уменьшите смещение x или ha*");
            if (!Internal && OverOuterD <= 0 && Z < 17 && X < (17 - Z) / 17.0 - 1e-6) warn.Add("z < 17 без смещения: у нарезанной фрезой шестерни было бы подрезание; рекомендуется x ≥ " + Geo.F(Math.Round((17 - Z) / 17.0, 3)));

            double rStart = Math.Max(rb, rf);   // где начинается эвольвента
            // скругление у ножки: касается радиальной линии и окружности впадин
            double rho = (Internal ? 0.1 : Rf) * M, fillAng = 0, rLine = rStart;
            bool radial = rf < rb;
            if (radial)
            {
                double rhoMax = (rb * rb - rf * rf) / (2 * rf);
                rho = Math.Min(rho, rhoMax * 0.98);
                fillAng = Math.Asin(rho / (rf + rho));
                rLine = Math.Sqrt((rf + rho) * (rf + rho) - rho * rho);
            }
            double phiB = phi(rStart);
            if (phiB + fillAng >= gap - 1e-4) { warn.Add("Впадина слишком узкая — проверьте x, утонение, ha*/c*"); }

            // половина зуба (правый бок, угол < 0) в локальной системе: ось зуба = +X
            List<Seg> half = new List<Seg>();
            // дно впадины: от середины впадины (−gap) до начала скругления/бока
            double rootEnd = -(phiB + fillAng);
            if (rootEnd > -gap) half.Add(Seg.Arc(new V2(), rf, -gap, rootEnd + gap));
            if (radial)
            {
                if (rho > 1e-4)
                {
                    V2 fc = V2.Polar(-(phiB + fillAng), rf + rho);
                    V2 p0 = V2.Polar(-(phiB + fillAng), rf), p1 = V2.Polar(-phiB, rLine);
                    double a0 = (p0 - fc).Angle, a1 = (p1 - fc).Angle;
                    half.Add(Seg.Arc(fc, rho, a0, Geo.DirNorm(a1 - a0, false)));
                }
                if (rb - rLine > 1e-5) half.Add(Seg.Line(V2.Polar(-phiB, rLine), V2.Polar(-phiB, rb)));
            }
            // эвольвента от rStart до ra
            int n = Math.Max(4, Pts);
            V2 prev = V2.Polar(-phi(rStart), rStart);
            for (int i = 1; i <= n; i++)
            {
                // равномерно по углу развёрнутости — точки гуще у основания
                double t = (double)i / n;
                double aS = Math.Acos(Math.Min(1, rb / rStart)), aA = Math.Acos(rb / ra);
                double tS = Math.Tan(aS), tA = Math.Tan(aA);
                double tt = tS + (tA - tS) * t;
                double R = rb * Math.Sqrt(1 + tt * tt);
                V2 q = V2.Polar(-phi(R), R);
                half.Add(Seg.Line(prev, q)); prev = q;
            }
            // половина вершины
            double pa = phi(ra);
            if (pa > 1e-6) half.Add(Seg.Arc(new V2(), ra, -pa, pa));

            // полный зуб: половина + зеркальная (обратный порядок)
            List<Seg> tooth = new List<Seg>(half);
            for (int i = half.Count - 1; i >= 0; i--)
            {
                Seg m = half[i].Clone();
                m.Mirror(new V2(0, 0), new V2(1, 0)); // отражение относительно оси X
                tooth.Add(m.Reversed());
            }
            V2 cen = new V2(Cx, Cy);
            for (int k = 0; k < Z; k++)
            {
                double rot = 2 * Math.PI * k / Z + Math.PI / 2; // первый зуб — вверх
                foreach (Seg sg in tooth)
                {
                    Seg c = sg.Clone(); c.Rotate(new V2(), rot); c.Move(cen); res.Add(c);
                }
            }
            if (Internal)
            {
                if (Bore > 0) { if (Bore / 2 <= ra + M) warn.Add("Наружный Ø кольца слишком мал"); res.Add(Seg.Circle(cen, Bore / 2)); }
                return res;
            }
            // посадочное отверстие
            if (Bore > 0)
            {
                if (Bore / 2 >= rf - 2 * M) warn.Add("Отверстие слишком близко к впадинам");
                if (Key)
                {
                    KeyParams kp = new KeyParams { D = Bore, Fit = Fit, X = Cx, Y = Cy, Angle = 0, Count = 1 };
                    string err; res.AddRange(kp.Build(out err));
                    if (err != null) warn.Add(err);
                    if (kp.Note != null) warn.Add("Паз: " + kp.Note);
                    if (kp.B == 0) warn.Add("Для Ø" + Geo.F(Bore) + " нет паза в ГОСТ 23360 (Ø6…500)");
                }
                else res.Add(Seg.Circle(cen, Bore / 2));
            }
            return res;
        }
    }

    // ======================= ШЛИЦЫ =======================
    [TypeConverter(typeof(RusEnumConverter))]
    public enum SplineType
    {
        [Description("Прямобочные — ГОСТ 1139-80")] Straight,
        [Description("Эвольвентные — ГОСТ 6033-80")] Involute
    }
    [TypeConverter(typeof(RusEnumConverter))]
    public enum SplinePart
    {
        [Description("Втулка (шлицевое отверстие)")] Hub,
        [Description("Вал (наружные шлицы)")] Shaft
    }

    public class Gost1139Converter : ListConverter
    {
        public static readonly string[] List = {
            "Лёгкая 6×23×26×6","Лёгкая 6×26×30×6","Лёгкая 6×28×32×7","Лёгкая 8×32×36×6","Лёгкая 8×36×40×7","Лёгкая 8×42×46×8",
            "Лёгкая 8×46×50×9","Лёгкая 8×52×58×10","Лёгкая 8×56×62×10","Лёгкая 8×62×68×12","Лёгкая 10×72×78×12","Лёгкая 10×82×88×12",
            "Лёгкая 10×92×98×14","Лёгкая 10×102×108×16","Лёгкая 10×112×120×18",
            "Средняя 6×11×14×3","Средняя 6×13×16×3.5","Средняя 6×16×20×4","Средняя 6×18×22×5","Средняя 6×21×25×5","Средняя 6×23×28×6",
            "Средняя 6×26×32×6","Средняя 6×28×34×7","Средняя 8×32×38×6","Средняя 8×36×42×7","Средняя 8×42×48×8","Средняя 8×46×54×9",
            "Средняя 8×52×60×10","Средняя 8×56×65×10","Средняя 8×62×72×12","Средняя 10×72×82×12","Средняя 10×82×92×12",
            "Средняя 10×92×102×14","Средняя 10×102×112×16","Средняя 10×112×125×18" };
        protected override string[] Items() { return List; }
    }

    [TypeConverter(typeof(OrderConverter))]
    public class SplineParams
    {
        const string C1 = "1. Шлицевое соединение", C2 = "2. Прямобочные (ГОСТ 1139)", C3 = "3. Эвольвентные (ГОСТ 6033)", C4 = "4. Вставка";
        [Category(C1), DisplayName("Тип шлицев")] public SplineType Type { get; set; }
        [Category(C1), DisplayName("Деталь")] public SplinePart Part { get; set; }
        [Category(C1), DisplayName("Коррекция ширины, мм"), Description("Добавка к ширине паза втулки / толщине зуба вала (в поле допуска), «+» — шире паз / толще зуб")] public double Corr { get; set; }

        string desig = "Лёгкая 8×36×40×7";
        [Category(C2), DisplayName("Обозначение z×d×D×b"), Description("Выберите из ГОСТ 1139-80 или впишите свои размеры через ×"), TypeConverter(typeof(Gost1139Converter))]
        public string Desig
        {
            get { return desig; }
            set
            {
                desig = value;
                string t = value; int sp = t.IndexOf(' '); if (sp >= 0) t = t.Substring(sp + 1);
                string[] p = t.Replace('x', '×').Replace('х', '×').Split('×');
                double a, b, c, d;
                if (p.Length == 4 && MainForm.Num(p[0], out a) && MainForm.Num(p[1], out b) && MainForm.Num(p[2], out c) && MainForm.Num(p[3], out d))
                { Zs = (int)a; Dmin = b; Dmax = c; B = d; }
            }
        }
        [Category(C2), DisplayName("Число шлицев z")] public int Zs { get; set; }
        [Category(C2), DisplayName("Внутренний Ø d, мм")] public double Dmin { get; set; }
        [Category(C2), DisplayName("Наружный Ø D, мм")] public double Dmax { get; set; }
        [Category(C2), DisplayName("Ширина шлица b, мм")] public double B { get; set; }

        [Category(C3), DisplayName("Модуль m, мм"), Description("Ряд ГОСТ 6033: 0.5; 0.8; 1; 1.25; 1.5; 2; 2.5; 3; 3.5; 4; 5; 6; 8; 10")] public double M { get; set; }
        [Category(C3), DisplayName("Число зубьев z")] public int Zi { get; set; }
        [Category(C3), DisplayName("Номинальный Ø D, мм")] public double Dn { get; set; }
        [Category(C3), DisplayName("Смещение x·m, мм"), Description("По ГОСТ 6033: x·m = 0.5·(D − m·z − 1.1·m)")] public double Xm { get { return Math.Round(0.5 * (Dn - M * Zi - 1.1 * M), 4); } }
        [Category(C3), DisplayName("Ø вершин втулки, мм"), Description("D − 2m")] public double HubTip { get { return Math.Round(Dn - 2 * M, 3); } }
        [Category(C3), DisplayName("Ø вершин вала, мм"), Description("D − 0.2m (центрирование по боковым сторонам)")] public double ShaftTip { get { return Math.Round(Dn - 0.2 * M, 3); } }

        [Category(C4), DisplayName("Центр X, мм")] public double Cx { get; set; }
        [Category(C4), DisplayName("Центр Y, мм")] public double Cy { get; set; }

        public SplineParams() { Type = SplineType.Straight; Part = SplinePart.Hub; Desig = "Лёгкая 8×36×40×7"; M = 2; Zi = 18; Dn = 40; }

        public string Name()
        {
            return Type == SplineType.Straight ? "ШЛИЦЫ_" + Zs + "x" + Geo.F(Dmin) + "x" + Geo.F(Dmax) + "x" + Geo.F(B) : "ШЛИЦЫ_" + Geo.F(Dn) + "x" + Geo.F(M);
        }
        public string Info()
        {
            string p = Part == SplinePart.Hub ? "втулка" : "вал";
            if (Type == SplineType.Straight) return "Шлицы прямобочные " + Zs + "×" + Geo.F(Dmin) + "×" + Geo.F(Dmax) + "×" + Geo.F(B) + " (" + p + ")" + (Corr != 0 ? ", коррекция b " + Geo.F(Corr) : "");
            return "Шлицы эвольвентные " + Geo.F(Dn) + "×" + Geo.F(M) + " ГОСТ 6033, z" + Zi + ", x·m " + Geo.F(Xm) + " (" + p + ")";
        }

        public List<Seg> Build(List<string> warn)
        {
            List<Seg> r = new List<Seg>();
            V2 c = new V2(Cx, Cy);
            if (Type == SplineType.Straight)
            {
                double rd = Dmin / 2, rD = Dmax / 2;
                // втулка: паз шире на Corr; вал: зуб толще на Corr — в обоих случаях ширина b + Corr
                double hb = (B + Corr) / 2;
                if (Zs < 2 || rd <= hb || rD <= rd) { warn.Add("Проверьте z, d, D, b"); return r; }
                double ys = Math.Sqrt(rd * rd - hb * hb), yb = Math.Sqrt(rD * rD - hb * hb);
                double aS = Math.Atan2(ys, hb), aB = Math.Atan2(yb, hb);   // углы правой стенки на d и D (локально, ось шлица = +Y)
                double pitch = 2 * Math.PI / Zs;
                if (Math.PI - 2 * aS + 1e-9 >= pitch) warn.Add("Шлицы перекрываются — проверьте размеры");
                for (int k = 0; k < Zs; k++)
                {
                    double rot = k * pitch;
                    // правая стенка, верх по D, левая стенка, перемычка по d до следующего шлица
                    Seg[] seg = {
                        Seg.Line(new V2(hb, ys), new V2(hb, yb)),
                        Seg.Arc(new V2(), rD, aB, Math.PI - 2 * aB),
                        Seg.Line(new V2(-hb, yb), new V2(-hb, ys)),
                        Seg.Arc(new V2(), rd, Math.PI - aS, pitch - (Math.PI - 2 * aS)) };
                    foreach (Seg sg in seg) { Seg q = sg.Clone(); q.Rotate(new V2(), rot); q.Move(c); r.Add(q); }
                }
                return r;
            }
            // эвольвентные: тот же генератор, что у шестерни, α = 30°
            GearParams g = new GearParams { M = M, Z = Zi, Alpha = 30, Ha = 1, Cc = 0.1, Rf = 0.2, Bore = 0, Key = false, Pts = 12, Cx = Cx, Cy = Cy };
            g.X = (0.5 * (Dn - M * Zi - 1.1 * M)) / M;
            if (Part == SplinePart.Hub)
            {
                g.Internal = true; g.Thin = Corr;
                g.OverOuterD = Dn;              // впадины втулки
                g.OverInnerD = Dn - 2 * M;      // вершины зубьев втулки
            }
            else
            {
                g.Internal = false; g.Thin = -Corr;
                g.OverOuterD = Dn - 0.2 * M;    // вершины вала
                g.OverInnerD = Dn - 2.2 * M;    // впадины вала
            }
            return g.Build(warn);
        }
    }

    // ======================= ОКНО БИБЛИОТЕКИ =======================
    public class LibForm : Form
    {
        object prm; PropertyGrid grid; Panel preview; Label info;
        public List<Seg> Result = new List<Seg>();
        public string PartName, InfoText;

        public LibForm(object p, string title)
        {
            prm = p;
#if NET48
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
#endif
            Text = title; Size = new Size(1060, 700); StartPosition = FormStartPosition.CenterParent;
            Font = new Font(MainForm.UiFont, MainForm.UiSize); BackColor = Theme.Surface; ForeColor = Theme.Text; ShowInTaskbar = false; MinimizeBox = false;
            grid = new PropertyGrid { Dock = DockStyle.Left, Width = 440, SelectedObject = p, PropertySort = PropertySort.Categorized, ToolbarVisible = false };
            grid.BackColor = Theme.Surface; grid.ViewBackColor = Theme.Background; grid.ViewForeColor = Theme.Text; grid.LineColor = Theme.Raised;
            grid.CategoryForeColor = Theme.Accent; grid.HelpBackColor = Theme.Surface; grid.HelpForeColor = Theme.Muted;
            grid.PropertyValueChanged += delegate { grid.Refresh(); Rebuild(); };
            preview = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 33, 39) };
            preview.Paint += DrawPreview; preview.Resize += delegate { preview.Invalidate(); };
            info = new Label { Dock = DockStyle.Bottom, Height = 54, ForeColor = Theme.Text, BackColor = Theme.Raised, Padding = new Padding(8, 6, 8, 0) };
            FlowLayoutPanel bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = Theme.Surface };
            Button cancel = new Button { Text = "Отмена", Width = 110, Height = 32, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Theme.Raised, ForeColor = Theme.Text };
            Button ok = new Button { Text = "Вставить в чертёж", Width = 170, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Theme.Green, ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold) };
            ok.Click += delegate { if (Result.Count > 0) { DialogResult = DialogResult.OK; Close(); } };
            new ToolTip().SetToolTip(ok, Tips.Get("LIB_OK") + "\nПараметры слева — у каждого поля внизу есть пояснение.");
            bp.Controls.Add(cancel); bp.Controls.Add(ok);
            AcceptButton = ok; CancelButton = cancel;
            Controls.Add(preview); Controls.Add(grid); Controls.Add(info); Controls.Add(bp);
#if NET48
            ResumeLayout(true);
#endif
            Rebuild();
        }

        void Rebuild()
        {
            List<string> warn = new List<string>();
            if (prm is GearParams)
            {
                GearParams g = (GearParams)prm;
                Result = g.Build(warn);
                InfoText = g.Info() + (g.Bore > 0 ? ";  отверстие Ø" + Geo.F(g.Bore) + (g.Key ? " с пазом" : "") : "");
                PartName = "ШЕСТЕРНЯ_m" + Geo.F(g.M) + "_z" + g.Z;
            }
            else if (prm is SplineParams)
            {
                SplineParams sp = (SplineParams)prm;
                Result = sp.Build(warn);
                InfoText = sp.Info();
                PartName = sp.Name();
            }
            else
            {
                KeyParams k = (KeyParams)prm;
                string err; Result = k.Build(out err);
                if (err != null) warn.Add(err);
                if (k.Note != null) warn.Add(k.Note);
                if (k.B == 0) warn.Add("Ø вне таблицы ГОСТ 23360 (6…500 мм) — задайте b и t2 вручную");
                InfoText = k.Info();
                PartName = "ПАЗ_d" + Geo.F(k.D) + "_b" + Geo.F(k.B);
            }
            info.Text = InfoText + (warn.Count > 0 ? "\r\n⚠ " + string.Join("; ", warn.ToArray()) : "");
            info.ForeColor = warn.Count > 0 ? Theme.Orange : Theme.Text;
            preview.Invalidate();
        }

        void DrawPreview(object o, PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Result.Count == 0) return;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Seg s in Result) s.Bounds(ref x0, ref y0, ref x1, ref y1);
            double w = Math.Max(x1 - x0, 1e-3), h = Math.Max(y1 - y0, 1e-3);
            double k = Math.Min((preview.Width - 40) / w, (preview.Height - 40) / h);
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            using (Pen p = new Pen(Color.FromArgb(235, 235, 235), 1.3f))
                foreach (Seg s in Result)
                {
                    int n = s.Kind == SegKind.Line ? 1 : Math.Max(6, (int)(Math.Abs(s.Sw) * 30));
                    PointF prev = P(s.Start, cx, cy, k);
                    for (int i = 1; i <= n; i++) { PointF q = P(s.PointAt((double)i / n), cx, cy, k); g.DrawLine(p, prev, q); prev = q; }
                }
            using (Pen ax = new Pen(Color.FromArgb(90, 61, 139, 253)) { DashStyle = DashStyle.DashDot })
            {
                PointF c = P(new V2(cx, cy), cx, cy, k);
                g.DrawLine(ax, c.X - 20, c.Y, c.X + 20, c.Y); g.DrawLine(ax, c.X, c.Y - 20, c.X, c.Y + 20);
            }
        }
        PointF P(V2 w, double cx, double cy, double k) { return new PointF((float)((w.X - cx) * k + preview.Width / 2.0), (float)(preview.Height / 2.0 - (w.Y - cy) * k)); }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    }
}
