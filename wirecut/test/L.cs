using System; using System.Collections.Generic; using WireCut;
class L { static void Main(){
  System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
  var segs = new List<Seg>{ Seg.Line(new V2(0,0),new V2(40,0)), Seg.Line(new V2(40,0),new V2(40,20)), Seg.Line(new V2(40,20),new V2(0,20)), Seg.Line(new V2(0,20),new V2(0,0)) };
  var cs = Geo.Chain(segs,1e-4);
  foreach (LeadKind lk in new LeadKind[]{LeadKind.Straight, LeadKind.Angled, LeadKind.Arc}) {
    var st = new JobSettings(); st.LeadType = lk; st.LeadIn = 5; st.LeadArcR = 2; st.LeadAngle = 45;
    var tp = Toolpath.Build(new List<ContourJob>{ new ContourJob{Geo=cs[0]} }, st);
    Console.WriteLine(lk+": S="+tp.Starts[0]);
    foreach (var m in tp.Moves) if (m.Lead) Console.WriteLine("   "+(m.S.Kind==SegKind.Line?"L ":"A ")+m.S.Start+" -> "+m.S.End+(m.S.IsArc?" R"+Math.Round(m.S.R,3)+" sw"+Math.Round(m.S.Sw*180/Math.PI,1):""));
    // проверка: путь непрерывен
    V2 prev = tp.Moves.Find(x=>x.S!=null).S.Start; bool ok=true; foreach(var m in tp.Moves) if(m.S!=null){ if(V2.Dist(prev,m.S.Start)>1e-6) ok=false; prev=m.S.End; }
    Console.WriteLine("   непрерывно: "+ok+", warn "+tp.Warn.Count);
  }
  var s2 = new JobSettings(); var tp2 = Toolpath.Build(new List<ContourJob>{ new ContourJob{Geo=cs[0], Side=PartKind.Die} }, s2);
  Console.WriteLine("Изнутри: S="+tp2.Starts[0]);
}}
