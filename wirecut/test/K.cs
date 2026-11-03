using System; using System.Collections.Generic; using WireCut;
class K { static void Main(){
  System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
  Action3("Прямобочные втулка", new SplineParams());
  var s2 = new SplineParams(); s2.Type = SplineType.Involute; Action3("Эвольв. втулка 40x2 z18", s2);
  var s3 = new SplineParams(); s3.Type = SplineType.Involute; s3.Part = SplinePart.Shaft; Action3("Эвольв. вал", s3);
  var g = new GearParams(); g.Internal = true; g.Z = 60; g.M = 2; g.Bore = 150; var w=new List<string>(); var segs=g.Build(w);
  var cs=Geo.Chain(segs,1e-4); Console.WriteLine("Внутр. венец: контуров "+cs.Count+" замкн "+cs[0].Closed+"/"+cs[cs.Count-1].Closed+"  da="+g.Da+" df="+g.Df+" warn "+w.Count);
  Rad(cs[0]); 
  // починка: квадрат с разрывом 0.02 и дублем
  var e = new List<Seg>{ Seg.Line(new V2(0,0),new V2(10,0)), Seg.Line(new V2(10,0.02),new V2(10,10)), Seg.Line(new V2(10,10),new V2(0,10)), Seg.Line(new V2(0,10),new V2(0,0)), Seg.Line(new V2(0,10),new V2(10,10)), Seg.Line(new V2(2,0),new V2(5,0))};
  var rep=new List<string>(); var marks=DrawingCheck.Check(e,rep); Console.WriteLine(rep[0]+" marks "+marks.Count);
  var fixedE=DrawingCheck.Repair(e,0.05,rep); Console.WriteLine(rep[rep.Count-1]); rep.Clear();
  DrawingCheck.Check(fixedE,rep); Console.WriteLine("после: "+rep[0]+"  контур замкнут: "+Geo.Chain(fixedE,0.01)[0].Closed);
}
static void Rad(Contour c){ double mn=1e9,mx=0; foreach(var s in c.Segs) for(int i=0;i<=8;i++){double r=s.PointAt(i/8.0).Len; mn=Math.Min(mn,r); mx=Math.Max(mx,r);} Console.WriteLine("   Ø min "+Math.Round(2*mn,3)+"  Ø max "+Math.Round(2*mx,3)); }
static void Action3(string name, SplineParams sp){
  var w=new List<string>(); var segs=sp.Build(w); var cs=Geo.Chain(segs,1e-4);
  Console.WriteLine(name+": "+sp.Info()+" | контуров "+cs.Count+" замкнут "+cs[0].Closed+" сегм "+cs[0].Segs.Count+" warn "+string.Join(";",w.ToArray()));
  Rad(cs[0]);
  var st=new JobSettings(); st.Kind = sp.Part==SplinePart.Hub?PartKind.Die:PartKind.Punch;
  var tp=Toolpath.Build(new List<ContourJob>{new ContourJob{Geo=cs[0]}}, st); Console.WriteLine("   траектория "+Math.Round(tp.CutLen,1)+" мм, предупр. "+tp.Warn.Count+" "+string.Join(";",tp.Warn.ToArray()));
}}
