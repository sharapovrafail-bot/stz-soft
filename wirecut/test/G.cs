using System; using System.Collections.Generic; using WireCut;
class G { static void Main(){
  System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
  foreach (int z in new int[]{30, 12, 60}) {
    var g = new GearParams(); g.Z = z; g.M = 2; if (z==12) { g.X = 0.3; g.Bore=0; }
    var w = new List<string>(); var segs = g.Build(w);
    var cs = Geo.Chain(segs, 1e-4);
    double x0=1e9,y0=1e9,x1=-1e9,y1=-1e9; foreach(var s in segs) s.Bounds(ref x0,ref y0,ref x1,ref y1);
    Console.WriteLine(g.Info()+" | segs "+segs.Count+" contours "+cs.Count);
    foreach(var c in cs) Console.WriteLine("   closed="+c.Closed+" n="+c.Segs.Count+" area="+Math.Round(c.Area,2));
    Console.WriteLine("   габарит "+Math.Round(x1-x0,3)+" x "+Math.Round(y1-y0,3)+"  (da="+g.Da+")");
    foreach(var s in w) Console.WriteLine("   WARN "+s);
    // max radius check & min radius
    double rmin=1e9, rmax=0; foreach(var s in cs[0].Segs){ for(int i=0;i<=10;i++){ double r=s.PointAt(i/10.0).Len; rmin=Math.Min(rmin,r); rmax=Math.Max(rmax,r);} }
    Console.WriteLine("   r min "+Math.Round(rmin*2,3)+" (df="+g.Df+")  r max "+Math.Round(rmax*2,3));
    var st = new JobSettings(); var jobs = new List<ContourJob>(); foreach(var c in cs) jobs.Add(new ContourJob{Geo=c});
    var tp = Toolpath.Build(jobs, st); Console.WriteLine("   path moves "+tp.Moves.Count+" len "+Math.Round(tp.CutLen,1)+" warn "+tp.Warn.Count);
    foreach(var s in tp.Warn) Console.WriteLine("     "+s);
    var files = Post.Make(tp, st, "G"); foreach(var f in files) if(f.Name.EndsWith(".3B")) Console.WriteLine("   "+f.Name+" lines "+f.Text.Split('\n').Length);
  }
  var k = new KeyParams(); k.D = 40; var e=""; var ks = k.Build(out e); var kc=Geo.Chain(ks,1e-4);
  Console.WriteLine(k.Info()+" contours "+kc.Count+" closed "+kc[0].Closed+" area "+Math.Round(kc[0].Area,2));
  k.Count=2; ks=k.Build(out e); kc=Geo.Chain(ks,1e-4); Console.WriteLine("2 паза: contours "+kc.Count+" closed "+kc[0].Closed);
  k.Count=1; k.BoreDone=true; ks=k.Build(out e); kc=Geo.Chain(ks,1e-4); Console.WriteLine("только паз: closed "+kc[0].Closed+" area "+Math.Round(kc[0].Area,2));
  double y1b=-1e9; foreach(var s in ks){ double a=0,b=0,c=0,d=-1e9; s.Bounds(ref a,ref b,ref c,ref d); if(d>y1b) y1b=d; }
  Console.WriteLine("дно паза Y="+y1b+" (ожид. d+t2-R = "+(40+3.3-20)+")");
}}
