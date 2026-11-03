using System; using System.Collections.Generic; using WireCut;
class T { static void Main(){
  System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
  var segs = new List<Seg>();
  // квадрат 20x20 со скруглением R3 в одном углу + отверстие D8
  segs.Add(Seg.Line(new V2(0,0), new V2(20,0)));
  segs.Add(Seg.Line(new V2(20,0), new V2(20,17)));
  segs.Add(Seg.Arc(new V2(17,17),3,0,Math.PI/2));
  segs.Add(Seg.Line(new V2(17,20), new V2(0,20)));
  segs.Add(Seg.Line(new V2(0,20), new V2(0,0)));
  segs.Add(Seg.Circle(new V2(10,10),4));
  var items = new List<Dxf.Layered>(); foreach(var s in segs) items.Add(new Dxf.Layered{S=s,Layer="0",Color=7});
  Dxf.Write("t.dxf", items);
  var log = new List<string>(); var rd = Dxf.Read("t.dxf", log); Console.WriteLine("read "+rd.Count);
  var cs = Geo.Chain(rd, 1e-3); Console.WriteLine("contours "+cs.Count);
  foreach(var c in cs) Console.WriteLine(" closed="+c.Closed+" area="+c.Area+" n="+c.Segs.Count);
  var st = new JobSettings(); st.Machine = MachineType.Medium; Presets.Apply(st); st.Tab=2; st.Kind=PartKind.Punch; st.SizeCorr=0.005;
  var jobs = new List<ContourJob>();
  jobs.Add(new ContourJob{Geo=cs[1]}); // отверстие сначала — как матрица? используем пуансон для внешнего
  jobs.Add(new ContourJob{Geo=cs[0]});
  var tp = Toolpath.Build(jobs, st);
  foreach(var w in tp.Warn) Console.WriteLine("WARN "+w);
  var files = Post.Make(tp, st, "TEST");
  foreach(var f in files){ Console.WriteLine("==== "+f.Name); Console.WriteLine(f.Text); }
  // проверка эквидистанты: смещение квадрата наружу на 0.1
  var o = Geo.Offset(cs[0].Reversed(), 0.1, log);
}}
