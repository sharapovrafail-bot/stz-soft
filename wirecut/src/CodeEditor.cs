using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WireCut
{
    // Formatting is confined to visible lines; text, selection and native undo are untouched.
    public sealed class CodeEditor : UserControl
    {
        readonly RichTextBox editor;
        readonly LineGutter gutter;
        readonly Timer timer;
        bool coloring;
        string originalText = "", originalNormalized = "";
        string newline = "\r\n";
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        static readonly Regex Tokens = new Regex(@"\([^\r\n)]*\)|;[^\r\n]*|[NGMXYIJ][+-]?(?:\d+(?:\.\d*)?|\.\d+)", RegexOptions.IgnoreCase);
        public CodeEditor()
        {
            BackColor=Theme.Background;
            editor=new RichTextBox { Dock=DockStyle.Fill, BorderStyle=BorderStyle.None, BackColor=Theme.Background, ForeColor=Theme.Text, Font=new Font("Consolas",12), WordWrap=false, DetectUrls=false, AcceptsTab=true, HideSelection=false, ScrollBars=RichTextBoxScrollBars.Both, MaxLength=0 };
            gutter=new LineGutter(editor) { Dock=DockStyle.Left, Width=60, BackColor=Theme.Surface };
            Controls.Add(editor); Controls.Add(gutter);
            timer=new Timer { Interval=180 };
            timer.Tick+=delegate { timer.Stop(); Highlight(); };
            editor.TextChanged+=delegate { if(!coloring) { timer.Stop(); timer.Start(); gutter.Invalidate(); } };
            editor.VScroll+=delegate { gutter.Invalidate(); timer.Stop(); timer.Start(); };
            editor.Resize+=delegate { gutter.Invalidate(); timer.Stop(); timer.Start(); };
        }
        public override string Text
        {
            get
            {
                if(editor == null) return "";
                // RichEdit exposes LF; preserve the exact loaded text when untouched,
                // and its line-ending convention when saving edited machine programs.
                string value = editor.Text;
                return value == originalNormalized ? originalText : value.Replace("\r\n", "\n").Replace("\n", newline);
            }
            set
            {
                if(editor == null) return;
                originalText = value ?? "";
                newline = originalText.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : originalText.IndexOf('\n') >= 0 ? "\n" : "\r\n";
                editor.Text = originalText; originalNormalized = editor.Text;
            }
        }
        bool noColor; // подсветка недоступна — показываем текст без цвета
        void Highlight()
        {
            if(noColor) return;
            try { HighlightCore(); }
            catch { noColor = true; coloring = false; try { SendMessage(editor.Handle,11,new IntPtr(1),IntPtr.Zero); editor.Invalidate(); } catch { } }
        }
        void HighlightCore()
        {
            if(coloring || !editor.IsHandleCreated) return;
            int start=editor.SelectionStart, length=editor.SelectionLength;
            int first=editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(0,0)));
            int last=editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(editor.ClientSize.Width,editor.ClientSize.Height)));
            int a=editor.GetFirstCharIndexFromLine(first), b=editor.GetFirstCharIndexFromLine(last+1);
            if(a<0) return; if(b<0) b=editor.TextLength;
            string text=editor.Text.Substring(a,b-a);
            coloring=true;
            // TOM Suspend/Resume prevents formatting from entering the user's undo history.
            IntPtr ole=IntPtr.Zero; ITextDocument document=null;
            NativePoint scroll=new NativePoint();
            SendPoint(editor.Handle,0x04DD,IntPtr.Zero,ref scroll);
            try
            {
                SendPointer(editor.Handle,0x043C,IntPtr.Zero,out ole);
                if(ole==IntPtr.Zero) return;
                document=Marshal.GetObjectForIUnknown(ole) as ITextDocument;
                if(document==null) return;
                int result; document.Undo(-9999995,out result);
                SendMessage(editor.Handle,11,IntPtr.Zero,IntPtr.Zero);
                editor.Select(a,b-a); editor.SelectionColor=Theme.Text;
                foreach(Match match in Tokens.Matches(text))
                {
                    char c=char.ToUpperInvariant(match.Value[0]);
                    editor.Select(a+match.Index,match.Length);
                    editor.SelectionColor=c=='N' ? Theme.Muted : c=='G' ? Theme.Accent : c=='M' ? Theme.Orange : c=='(' || c==';' ? Theme.Green : Theme.Text;
                }
            }
            finally
            {
                editor.Select(start,length);
                if(document!=null) { int result; document.Undo(-9999994,out result); Marshal.ReleaseComObject(document); }
                if(ole!=IntPtr.Zero) Marshal.Release(ole);
                SendPoint(editor.Handle,0x04DE,IntPtr.Zero,ref scroll);
                SendMessage(editor.Handle,11,new IntPtr(1),IntPtr.Zero); editor.Invalidate(); gutter.Invalidate(); coloring=false;
            }
        }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X,Y; }
        [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr SendPoint(IntPtr h,int m,IntPtr w,ref NativePoint p);
        [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr SendPointer(IntPtr h,int m,IntPtr w,out IntPtr p);
        // ITextDocument is the RichEdit TOM interface; preceding methods preserve vtable order.
        [ComImport, Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        interface ITextDocument
        {
            [return: MarshalAs(UnmanagedType.BStr)] string GetName();
            [return: MarshalAs(UnmanagedType.Interface)] object GetSelection();
            int GetStoryCount();
            [return: MarshalAs(UnmanagedType.Interface)] object GetStoryRanges();
            int GetSaved(); void SetSaved(int value); float GetDefaultTabStop(); void SetDefaultTabStop(float value);
            void New(); void Open([In, MarshalAs(UnmanagedType.Struct)] ref object value,int flags,int codePage);
            void Save([In, MarshalAs(UnmanagedType.Struct)] ref object value,int flags,int codePage);
            void Freeze(out int count); void Unfreeze(out int count); void BeginEditCollection(); void EndEditCollection();
            void Undo(int count,out int result);
        }
        protected override void Dispose(bool disposing) { if(disposing) timer.Dispose(); base.Dispose(disposing); }
    }
    sealed class LineGutter : Control
    {
        readonly RichTextBox editor;
        public LineGutter(RichTextBox box) { editor=box; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint,true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            int first=editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(Point.Empty));
            int last=editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(0,editor.ClientSize.Height)));
            using(Brush b=new SolidBrush(Theme.Muted)) using(StringFormat format=new StringFormat())
            {
                format.Alignment=StringAlignment.Far;
                for(int i=first;i<=last;i++) { int index=editor.GetFirstCharIndexFromLine(i); if(index<0) break; int y=editor.GetPositionFromCharIndex(index).Y; e.Graphics.DrawString((i+1).ToString(),editor.Font,b,new RectangleF(0,y,Width-9,editor.Font.Height+3),format); }
            }
        }
    }
}
