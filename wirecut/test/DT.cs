using System; using System.Windows.Forms; using WireCut;
public static class DT { static MainForm f;
  static void Ok(bool c,string m){ Console.WriteLine((c?"OK   ":"FAIL ")+m); }
  [STAThread] public static void Main(string[] a){
    f = new MainForm(null);
    Timer t = new Timer{Interval=800};
    t.Tick += delegate { t.Stop(); try {
      Ok(DwgConv.LibreDwg!=null, "встроенный конвертер найден: "+DwgConv.LibreDwg);
      f.ImportDrawing(a[0], true);
      int n=f.Cv.Ents.Count; var cs=Geo.Chain(f.Cv.Ents,0.01); int cl=0; foreach(var c in cs) if(c.Closed) cl++;
      Ok(n==11 && cl==4, "DWG открыт: объектов "+n+", замкнутых контуров "+cl);
      Ok(f.S.PartName=="ПЛИТА_42" && f.Text.EndsWith("Плита_42.dwg"), "имя детали и заголовок по DWG: "+f.S.PartName);
      Ok(f.Hist.Items[f.Hist.Items.Count-1].Name.StartsWith("DWG"), "шаг истории: "+f.Hist.Items[f.Hist.Items.Count-1].Name);
      f.ImportDrawing("/tmp/нет.dwg", true); Ok(f.Cv.Ents.Count==11, "битый/нет файла — сообщение, чертёж не тронут");
    } catch(Exception ex){ Console.WriteLine("EXC "+ex); }
    f.Close(); };
    t.Start(); Application.Run(f);
}}
