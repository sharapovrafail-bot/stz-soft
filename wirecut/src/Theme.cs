using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireCut
{
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(30,35,41), Surface = Color.FromArgb(37,43,51), Raised = Color.FromArgb(45,52,61);
        public static readonly Color Text = Color.FromArgb(230,230,230), Muted = Color.FromArgb(154,164,175), Accent = Color.FromArgb(61,139,253);
        public static readonly Color Green = Color.FromArgb(47,168,79), Orange = Color.FromArgb(255,107,53), Error = Color.FromArgb(255,111,111);
        public static readonly Color Border = Color.FromArgb(63,73,85), Grid = Color.FromArgb(38,44,52), MajorGrid = Color.FromArgb(46,54,64);
        public static readonly Color[] Passes = { Color.FromArgb(88,207,133), Color.FromArgb(85,193,230), Color.FromArgb(236,199,94), Color.FromArgb(194,145,235) };
        public const string Brand = "Сделано специально по заказу ООО «СпецТехЗаказ»\nРафаил Шарапов · rafail@sharapov-cnc.ru";
        public static GraphicsPath Shape(Rectangle r)
        {
            GraphicsPath p = new GraphicsPath();
#if NET48
            int d = Math.Min(10, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left,r.Top,d,d,180,90); p.AddArc(r.Right-d,r.Top,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.Left,r.Bottom-d,d,d,90,90); p.CloseFigure();
#else
            p.AddRectangle(r);
#endif
            return p;
        }
        public static void Strip(ToolStrip s) { s.BackColor = Surface; s.ForeColor = Text; s.Renderer = new DarkRenderer(); }
        public static Bitmap Icon(string command, int size)
        {
            Bitmap b = new Bitmap(size,size);
            using (Graphics g = Graphics.FromImage(b))
            using (Pen p = new Pen(command == "УП" ? Text : command == "СТАРТ" ? Orange : Accent, 1.7f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size/24f,size/24f);
                switch (command)
                {
                    case "ОТКРЫТЬ": g.DrawPolygon(p,new Point[] {new Point(2,7),new Point(9,7),new Point(11,4),new Point(21,4),new Point(21,19),new Point(2,19)}); g.DrawLine(p,3,10,20,10); break;
                    case "СОХРАНИТЬ": g.DrawRectangle(p,3,3,18,18); g.DrawRectangle(p,7,3,10,6); g.DrawRectangle(p,7,14,10,7); break;
                    case "ОТРЕЗОК": g.DrawLine(p,4,20,20,4); g.DrawRectangle(p,2,18,4,4); g.DrawRectangle(p,18,2,4,4); break;
                    case "КРУГ": g.DrawEllipse(p,3,3,18,18); g.DrawLine(p,9,12,15,12); g.DrawLine(p,12,9,12,15); break;
                    case "ДУГА": g.DrawArc(p,3,3,18,18,180,160); break;
                    case "ПРЯМОУГ": g.DrawRectangle(p,3,5,18,14); break;
                    case "СОТРИ": g.DrawPolygon(p,new Point[]{new Point(3,15),new Point(13,3),new Point(21,10),new Point(11,21),new Point(8,21)}); break;
                    case "ПЕРЕНЕСТИ": p.StartCap=LineCap.ArrowAnchor; p.EndCap=LineCap.ArrowAnchor; g.DrawLine(p,2,12,22,12); g.DrawLine(p,12,2,12,22); break;
                    case "СИМ": g.DrawPolygon(p,new Point[]{new Point(6,3),new Point(21,12),new Point(6,21)}); break;
                    case "СТАРТ": g.DrawEllipse(p,6,6,12,12); g.DrawLine(p,12,1,12,23); g.DrawLine(p,1,12,23,12); break;
                    case "УП": g.DrawLines(p,new Point[]{new Point(14,2),new Point(4,13),new Point(12,13),new Point(9,22),new Point(21,9),new Point(13,9),new Point(14,2)}); break;
                    case "ПУТЬ": g.DrawRectangle(p,3,3,15,15); p.DashStyle=DashStyle.Dash; g.DrawRectangle(p,6,6,15,15); break;
                    case "НОВЫЙ": g.DrawPolygon(p,new Point[]{new Point(5,2),new Point(15,2),new Point(20,7),new Point(20,22),new Point(5,22)}); g.DrawLines(p,new Point[]{new Point(15,2),new Point(15,7),new Point(20,7)}); break;
                    case "ПОДОБИЕ": g.DrawEllipse(p,3,3,18,18); g.DrawEllipse(p,8,8,8,8); break;
                    case "МАССИВ": for (int i=0;i<3;i++) for (int j=0;j<3;j++) g.DrawRectangle(p,2+i*7,2+j*7,5,5); break;
                    case "ОБРЕЗАТЬ": g.DrawLine(p,12,2,12,22); g.DrawLine(p,2,12,10,12); { Pen d=(Pen)p.Clone(); d.DashStyle=DashStyle.Dot; g.DrawLine(d,14,12,22,12); d.Dispose(); } break;
                    case "УДЛИНИТЬ": g.DrawLine(p,20,2,20,22); g.DrawLine(p,2,12,12,12); p.EndCap=LineCap.ArrowAnchor; g.DrawLine(p,12,12,18,12); break;
                    case "СОПРЯЖЕНИЕ": g.DrawLine(p,3,21,3,11); g.DrawArc(p,3,3,16,16,180,90); g.DrawLine(p,11,3,21,3); break;
                    case "ФАСКА": g.DrawLine(p,3,21,3,10); g.DrawLine(p,3,10,10,3); g.DrawLine(p,10,3,21,3); break;
                    case "ПРОВЕРКА": g.DrawEllipse(p,3,3,13,13); g.DrawLine(p,14,14,21,21); break;
                    case "ПОЧИНИТЬ": g.DrawLine(p,4,20,14,10); g.DrawEllipse(p,12,3,9,9); break;
                    case "ШЛИЦЫ": g.DrawEllipse(p,4,4,16,16); for (int i=0;i<6;i++){ double an=i*Math.PI/3; g.DrawLine(p,(float)(12+6*Math.Cos(an)),(float)(12+6*Math.Sin(an)),(float)(12+10*Math.Cos(an)),(float)(12+10*Math.Sin(an))); } break;
                    case "РЕЖИМЫ": g.DrawRectangle(p,3,3,18,18); g.DrawLine(p,3,9,21,9); g.DrawLine(p,3,15,21,15); g.DrawLine(p,10,3,10,21); break;
                    case "РАЗМЕР": g.DrawLine(p,3,6,3,20); g.DrawLine(p,21,6,21,20); p.StartCap=LineCap.ArrowAnchor; p.EndCap=LineCap.ArrowAnchor; g.DrawLine(p,4,13,20,13); break;
                    case "РАДИУС": g.DrawArc(p,2,2,20,20,180,180); g.DrawLine(p,12,12,19,5); g.DrawEllipse(p,11,11,2,2); break;
                    case "ДИАМЕТР": g.DrawEllipse(p,3,3,18,18); g.DrawLine(p,5,19,19,5); break;
                    case "УГОЛ": g.DrawLine(p,3,21,21,21); g.DrawLine(p,3,21,17,5); g.DrawArc(p,-7,11,20,20,-50,50); break;
                    case "ГАБАРИТ": g.DrawRectangle(p,6,6,12,10); g.DrawLine(p,6,20,18,20); g.DrawLine(p,21,6,21,16); break;
                    case "ЛИНЕЙКИ": g.DrawRectangle(p,2,8,20,8); for (int i=0;i<5;i++) g.DrawLine(p,4+i*4,8,4+i*4,i%2==0?13:11); break;
                    case "УДАЛИТЬРАЗМЕРЫ": g.DrawLine(p,3,12,21,12); g.DrawLine(p,7,5,17,19); g.DrawLine(p,17,5,7,19); break;
                    case "АВТОРАЗМЕРЫ": g.DrawRectangle(p,3,9,12,12); g.DrawLine(p,3,4,15,4); g.DrawLine(p,19,9,19,21); g.DrawLine(p,17,4,22,4); break;
                    case "ЗАХОД": g.DrawRectangle(p,10,6,12,12); p.EndCap=LineCap.ArrowAnchor; g.DrawLine(p,1,21,10,12); break;
                    case "КОПИРОВАТЬ": g.DrawRectangle(p,3,3,12,12); g.DrawRectangle(p,9,9,12,12); break;
                    case "ПОВЕРНУТЬ": g.DrawArc(p,4,4,16,16,200,250); g.DrawLines(p,new Point[]{new Point(15,4),new Point(20,6),new Point(17,11)}); break;
                    case "ЗЕРКАЛО": { Pen d=(Pen)p.Clone(); d.DashStyle=DashStyle.Dash; g.DrawLine(d,12,1,12,23); d.Dispose(); g.DrawPolygon(p,new Point[]{new Point(9,6),new Point(9,19),new Point(2,19)}); g.DrawPolygon(p,new Point[]{new Point(15,6),new Point(15,19),new Point(22,19)}); break; }
                    case "МАСШТАБ": g.DrawRectangle(p,2,12,9,9); g.DrawRectangle(p,2,2,20,20); g.DrawLine(p,11,12,20,4); break;
                    case "ОТМЕНИТЬ": g.DrawArc(p,5,6,15,13,180,-230); g.DrawLines(p,new Point[]{new Point(2,8),new Point(5,13),new Point(10,10)}); break;
                    case "ПОВТОРИТЬ": g.DrawArc(p,4,6,15,13,0,230); g.DrawLines(p,new Point[]{new Point(22,8),new Point(19,13),new Point(14,10)}); break;
                    case "ИСТОРИЯ": g.DrawLine(p,2,12,22,12); g.FillEllipse(Brushes.White,3,9,6,6); g.DrawEllipse(p,10,9,6,6); g.DrawEllipse(p,17,9,5,6); g.DrawLine(p,13,3,13,21); break;
                    case "ВХОД": g.DrawRectangle(p,9,5,13,14); { Pen gp=new Pen(Color.FromArgb(90,220,120),2.2f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(gp,1,22,9,14); gp.Dispose(); Pen rp=new Pen(Color.FromArgb(255,95,95),2.2f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(rp,15,19,8,23); rp.Dispose(); } break;
                    case "НОЛЬ": { Pen xr=new Pen(Color.FromArgb(235,80,80),2f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(xr,5,19,22,19); xr.Dispose(); Pen yg=new Pen(Color.FromArgb(90,210,90),2f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(yg,5,19,5,2); yg.Dispose(); } g.FillEllipse(new SolidBrush(Color.FromArgb(255,200,60)),2,16,7,7); break;
                    case "ИЗSTZ": for (int i = 0; i < 6; i++) { g.ResetTransform(); g.ScaleTransform(size/24f,size/24f); g.TranslateTransform(9,12); g.RotateTransform(i*60); g.DrawRectangle(p,-1.5f,-8,3,3); } g.ResetTransform(); g.ScaleTransform(size/24f,size/24f); g.DrawEllipse(p,4,7,10,10); { Pen ar=new Pen(Theme.Orange,2f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(ar,15,12,23,12); ar.Dispose(); } break;
                    case "НАСТАНОК": g.DrawRectangle(p,2,13,13,9); g.DrawLine(p,5,13,5,9); g.DrawLine(p,12,13,12,9); { Pen ar=new Pen(Theme.Orange,2.4f){EndCap=LineCap.ArrowAnchor}; g.DrawLine(ar,9,9,22,2); ar.Dispose(); } break;
                    case "СБРОС": g.DrawEllipse(p,4,4,12,12); g.DrawLine(p,10,1,10,19); g.DrawLine(p,1,10,19,10); { Pen r=new Pen(Theme.Error,2.2f); g.DrawLine(r,14,14,22,22); g.DrawLine(r,22,14,14,22); r.Dispose(); } break;
                    case "ДИСТ": g.DrawLine(p,3,12,21,12); g.DrawLine(p,3,7,3,17); g.DrawLine(p,21,7,21,17); g.DrawLine(p,7,9,7,15); g.DrawLine(p,12,9,12,15); g.DrawLine(p,17,9,17,15); break;
                    case "ПОКАЗАТЬ": g.DrawRectangle(p,7,7,10,10); g.DrawLines(p,new Point[]{new Point(2,7),new Point(2,2),new Point(7,2)}); g.DrawLines(p,new Point[]{new Point(17,2),new Point(22,2),new Point(22,7)}); g.DrawLines(p,new Point[]{new Point(22,17),new Point(22,22),new Point(17,22)}); g.DrawLines(p,new Point[]{new Point(7,22),new Point(2,22),new Point(2,17)}); break;
                    case "ШЕСТЕРНЯ": for (int i = 0; i < 8; i++) { g.ResetTransform(); g.ScaleTransform(size/24f,size/24f); g.TranslateTransform(12,12); g.RotateTransform(i*45); g.DrawRectangle(p,-2,-11,4,4); } g.ResetTransform(); g.ScaleTransform(size/24f,size/24f); g.DrawEllipse(p,5,5,14,14); g.DrawEllipse(p,10,10,4,4); break;
                    case "ШПОНКА": g.DrawArc(p,3,5,18,18,-60,300); g.DrawLines(p,new Point[]{new Point(9,7),new Point(9,2),new Point(15,2),new Point(15,7)}); break;
                    default: g.DrawRectangle(p,3,3,18,18); g.DrawLine(p,7,12,17,12); g.DrawLine(p,12,7,12,17); break;
                }
            }
            return b;
        }
    }
    public sealed class DarkColors : ProfessionalColorTable
    {
        public DarkColors() { UseSystemColors=false; }
        public override Color MenuItemSelected { get { return Theme.Raised; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Raised; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Raised; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Background; } }
        public override Color MenuItemPressedGradientMiddle { get { return Theme.Background; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Background; } }
    }
    public sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges=false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Theme.Surface); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor=e.Item.Enabled ? e.Item.ForeColor : Theme.Muted; base.OnRenderItemText(e); }
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r=new Rectangle(1,1,e.Item.Width-3,e.Item.Height-3);
            using (GraphicsPath p=Theme.Shape(r)) using (Brush b=new SolidBrush(e.Item.Pressed ? Theme.Accent : e.Item.Selected ? Theme.Raised : e.Item.BackColor)) e.Graphics.FillPath(b,p);
        }
    }
    public class DarkButton : Button
    {
        bool hover, pressed;
        public DarkButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true); BackColor=Theme.Raised; ForeColor=Theme.Text; FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; }
        protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Surface : Parent.BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            Rectangle r=new Rectangle(1,1,Width-3,Height-3);
            using (GraphicsPath p=Theme.Shape(r))
            using (Brush b=new SolidBrush(!Enabled ? Theme.Surface : pressed ? Theme.Accent : hover ? ControlPaint.Light(BackColor,.15f) : BackColor))
            using (Pen pen=new Pen(Focused ? Theme.Accent : Theme.Border)) { e.Graphics.FillPath(b,p); e.Graphics.DrawPath(pen,p); }
            TextRenderer.DrawText(e.Graphics,Text,Font,r,Enabled ? ForeColor : Theme.Muted,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
    public sealed class StatusPill : ToolStripStatusLabel
    {
        public bool Active;
        protected override void OnPaint(PaintEventArgs e)
        {
            using(GraphicsPath p=Theme.Shape(new Rectangle(1,2,Width-3,Height-5))) using(Brush b=new SolidBrush(Active ? Theme.Accent : Theme.Raised)) e.Graphics.FillPath(b,p);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(0,0,Width,Height),Active ? Theme.Text : Theme.Muted,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    public sealed class DarkTabs : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 15 || !IsHandleCreated) return;
            using (Graphics g = Graphics.FromHwnd(Handle))
            using (Brush bg = new SolidBrush(Theme.Surface))
            using (Pen border = new Pen(Theme.Border))
            {
                int right = TabCount == 0 ? 0 : GetTabRect(TabCount - 1).Right + 2;
                g.FillRectangle(bg, right, 0, Math.Max(0, Width - right), DisplayRectangle.Top - 2);
                Rectangle r = DisplayRectangle; r.Inflate(2, 2); g.DrawRectangle(border, r);
            }
        }
    }
    public sealed class BrandForm : Form
    {
        Timer timer;
        public BrandForm(bool splash)
        {
            Text="О программе"; ClientSize=new Size(560,290); BackColor=Theme.Background; ForeColor=Theme.Text;
            Font=new Font(MainForm.UiFont,10); FormBorderStyle=FormBorderStyle.FixedDialog; StartPosition=FormStartPosition.CenterParent;
            MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false; DoubleBuffered=true;
            if(splash) { FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.CenterScreen; timer=new Timer(); timer.Interval=1250; timer.Tick+=delegate { Close(); }; Shown+=delegate { timer.Start(); }; }
            else { DarkButton close=new DarkButton { Text="Закрыть",Bounds=new Rectangle(410,240,125,34) }; close.Click+=delegate { Close(); }; Controls.Add(close); AcceptButton=close; CancelButton=close; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            Image logo=Logo;
            if(logo!=null) { g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.DrawImage(logo,20,18,92,92); }
            else using(Pen p=new Pen(Theme.Accent,3)) { g.DrawRectangle(p,28,36,65,56); g.DrawLine(p,60,18,60,110); p.Color=Theme.Orange; for(int i=0;i<8;i++) { double a=i*Math.PI/4; g.DrawLine(p,60+(float)Math.Cos(a)*10,65+(float)Math.Sin(a)*10,60+(float)Math.Cos(a)*22,65+(float)Math.Sin(a)*22); } }
            using(Font title=new Font(MainForm.UiFont,26,FontStyle.Bold)) using(Brush b=new SolidBrush(Theme.Text)) g.DrawString("STZ WireCut",title,b,118,35);
            using(Brush b=new SolidBrush(Theme.Muted)) { g.DrawString("Версия " + MainForm.Ver,Font,b,122,86); g.DrawString("Подготовка УП для проволочно-вырезных станков\nDXF · вход / выход · история · ГОСТ-библиотека · симуляция · ISO / 3B",Font,b,28,135); }
            using(Brush b=new SolidBrush(Theme.Text)) g.DrawString(Theme.Brand,Font,b,new RectangleF(28,200,510,38));
        }
        protected override void Dispose(bool disposing) { if(disposing && timer!=null) timer.Dispose(); base.Dispose(disposing); }
        static Image logo; static bool logoTried;
        /// <summary>Логотип (та же картинка, что и значок программы) — встроен в exe как ресурс logo.png.</summary>
        public static Image Logo
        {
            get
            {
                if(!logoTried)
                {
                    logoTried=true;
                    try { System.IO.Stream st=typeof(BrandForm).Assembly.GetManifestResourceStream("WireCut.logo.png"); if(st!=null) logo=Image.FromStream(st); } catch { logo=null; }
                }
                return logo;
            }
        }
    }
}
