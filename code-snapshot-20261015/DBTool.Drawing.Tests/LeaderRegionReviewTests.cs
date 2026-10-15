using System.Text.Json;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing;
using DBTool.Drawing.PlanarRegions;
using DBTool.Drawing.StageContractsV2;
public static class LeaderRegionReviewOracles {
 static readonly FrameV2 XY=new("XY",new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1),1);
 static readonly FrameV2 XZ=new("XZ",new(0,0,0),new(1,0,0),new(0,0,1),new(0,-1,0),1);
 static readonly string[] Sources=["INDEPENDENT-L03-SYNTHETIC-ORACLE"];
 static RuleV2 Rule(string id,string status="Approved")=>new(id,"synthetic-test-only",status,"IndependentReviewer","2026-10-15T00:00:00Z",["*"],["*"],["*"],[],[],[],Sources);
 static RulesV2 Rules(string id="NR-18",string status="Approved")=>new("synthetic",[Rule(id,status)],1e-4,1e-4,null,null,1e-6,1e-8,1e-6,null);
 static readonly List<object> Results=[];
 static CurveV2 Line(string id,double x,double y,double xx,double yy)=>new(id,"Line",new(x,y,0),new(xx,yy,0),null,null,null,null,"Boundary");
 static LoopV2 Rect(string id,double x,double y,double w,double h,string role="Outer")=>new(id,role,[Line(id+"a",x,y,x+w,y),Line(id+"b",x+w,y,x+w,y+h),Line(id+"c",x+w,y+h,x,y+h),Line(id+"d",x,y+h,x,y)],true,w*h);
 static LoopV2 Circle(string id,double r,double cx=0,double cy=0,double start=0,int count=1)=>new(id,"Outer",Enumerable.Range(0,count).Select(i=>new CurveV2(id+i,"Arc",new(cx+r*Math.Cos(start+i*2*Math.PI/count),cy+r*Math.Sin(start+i*2*Math.PI/count),0),new(cx+r*Math.Cos(start+(i+1)*2*Math.PI/count),cy+r*Math.Sin(start+(i+1)*2*Math.PI/count),0),new(cx,cy,0),new(0,0,1),r,2*Math.PI/count,"Boundary")).ToArray(),true,Math.PI*r*r);
 static ComponentV2 Comp(string id,LoopV2[] loops,SolidPartV2[]? parts=null)=>new(id,"Concrete","A","M",XY,loops,0,100,100,[],[],Sources,parts??[],id,"Concrete");
 static Region2V2[] Emit(ComponentV2[] cs,string mode="TrueSection",double? offset=50,FrameV2? frame=null,RulesV2? rules=null,OpeningV2[]? openings=null,FrameV2? cutFrame=null,string viewId="V") {
  var vf=frame??XY; return new AssemblyBoundaryEmitter().Emit(new(cs,openings??[],[],"synthetic",[]),[],[new(viewId,"Section","A",vf,offset,1,false,true,null,cs.Select(c=>new ViewPartCutV2(c.id,cutFrame??vf,mode)).ToArray())],rules??Rules());
 }
 static double Area(Region2V2[] rs)=>rs.SelectMany(r=>r.loops).Sum(l=>l.signed_area_mm2);
 static PlanarOccupiedRegionResult Kernel(LoopV2[] a,LoopV2[] b,string kind="Union")=>new AnalyticPlanarOccupiedRegionKernel().Execute(new("R","A","M","V",[new("C",[new(0,"U","Union",a,Sources),new(1,"B",kind,b,Sources)],Sources)],new(1e-4,1e-8,1e-6,Sources,1e-6),Sources));
 static void Value(string id,double expected,Func<double> f,double tolerance=1e-6,string unit="mm2") {try {var actual=f();var pass=Math.Abs(actual-expected)<=tolerance;Results.Add(new{id,expected,actual,tolerance,unit,pass});Console.WriteLine($"{id}: expected {expected:R}; actual {actual:R}; {unit}; tolerance {tolerance:R}; pass={pass}");}catch(Exception e){Results.Add(new{id,expected,actual=e.Message,tolerance,unit,pass=false});Console.WriteLine($"{id}: EXCEPTION {e}");}}
 static void Fault(string id,Func<object> f,string expected="controlled fault") {try {f();Results.Add(new{id,expected,actual="NO_FAULT",unit="diagnostic",tolerance=0,pass=false});Console.WriteLine($"{id}: FAIL NO_FAULT");}catch(Exception e) when (e.GetType().Name == "DrawingStageFaultException"){bool pass=expected=="controlled fault"||e.Message.Contains(expected);Results.Add(new{id,expected,actual=e.Message,unit="diagnostic",tolerance=0,pass});Console.WriteLine($"{id}: controlled {e.Message}; pass={pass}");}catch(Exception e){Results.Add(new{id,expected,actual=e.GetType().Name+": "+e.Message,unit="diagnostic",tolerance=0,pass=false});Console.WriteLine($"{id}: UNCONTROLLED {e.GetType().Name}: {e.Message}");}}
 public static string Evaluate(){
  Results.Clear();
  var box=Rect("box",0,0,100,100);var hole=Rect("hole",20,20,60,60,"Inner");var island=Rect("island",30,30,40,40);var inner=Rect("inner",40,40,20,20,"Inner");
  Value("nested-hole-island-hole",7600,()=>Kernel([box,hole,island,inner],[]).TotalOccupiedAreaMm2);
  Value("unordered-nested-hole-island-hole",7600,()=>Kernel([inner,island,hole,box],[]).TotalOccupiedAreaMm2);
  Value("nested-parent-depth",3,()=>{var r=Kernel([box,hole,island,inner],[]);var leaf=r.Boundaries.Single(x=>Math.Abs(x.Boundary.signed_area_mm2)==400);int n=0;while(leaf.ParentLoopId!=null){n++;leaf=r.Boundaries.Single(x=>x.Boundary.id==leaf.ParentLoopId);}return n;},0,"count");
  Value("full-circle-versus-rotated-two-arcs-union",100*Math.PI,()=>Kernel([Circle("one",10)],[Circle("two",10,start:Math.PI/2,count:2)]).TotalOccupiedAreaMm2);
  Value("full-circle-versus-rotated-two-arcs-subtract",0,()=>Kernel([Circle("one",10)],[Circle("two",10,start:Math.PI/2,count:2)],"Subtract").TotalOccupiedAreaMm2);
  foreach(var r in new[]{.001,1d,10d,1e6,1e9}) {
   Value("overlap-disks-r"+r, r*r*(4*Math.PI/3+Math.Sqrt(3)/2),()=>Kernel([Circle("d1",r)],[Circle("d2",r,r)]).TotalOccupiedAreaMm2,Math.Max(1e-6,r*r*1e-12));
   Value("tangent-disks-r"+r,2*Math.PI*r*r,()=>Kernel([Circle("d1",r)],[Circle("d2",r,2*r)]).TotalOccupiedAreaMm2,Math.Max(1e-6,r*r*1e-12));
  }
  var disjointDepth=Comp("depth",[],[new("U","Union",XY,[box],0,40),new("I","Intersect",XY,[box],60,100)]);
  Value("disjoint-depth-intersection-section",0,()=>Area(Emit([disjointDepth])));
  Value("disjoint-depth-intersection-projection",0,()=>Area(Emit([disjointDepth],mode:"Projection",offset:null)));
  var below=Comp("below",[],[new("U","Union",XY,[box],0,100),new("S","Subtract",XY,[Rect("cut",30,30,40,40)],0,40)]);
  Value("below-face-subtract-section",10000,()=>Area(Emit([below])));
  Value("below-face-subtract-projection",10000,()=>Area(Emit([below],mode:"Projection",offset:null)));
  Value("section-uses-cut-frame",10000,()=>Area(Emit([Comp("C",[box])],frame:XZ,cutFrame:XY)));
  Fault("unrelated-approved-rule-not-tolerance-authority",()=>Emit([Comp("C",[box])],rules:Rules("NR-01")),"CSG_TOLERANCE_UNRESOLVED");
  Fault("unapproved-NR18-plus-approved-unrelated",()=>Emit([Comp("C",[box])],rules:Rules() with{entries=[Rule("NR-18","Draft"),Rule("NR-01")]}),"CSG_TOLERANCE_UNRESOLVED");
  Fault("blank-loop-id",()=>Emit([Comp("C",[box with{id=""}])]));
  Fault("duplicate-curve-id",()=>Emit([Comp("C",[box with{curves=box.curves.Select(c=>c with{id="duplicate"}).ToArray()}])]));
  Fault("blank-operation-id",()=>Emit([Comp("C",[],[new("","Union",XY,[box],0,100)])]));
  Fault("null-curve-entry",()=>Emit([Comp("C",[box with{curves=[null!]}])]));
  Fault("null-start-point",()=>Emit([Comp("C",[box with{curves=[box.curves[0] with{start=null!},..box.curves.Skip(1)]}])]));
  Fault("null-frame-origin",()=>Emit([Comp("C",[box]) with{frame=XY with{origin=null!}}]));
  Fault("empty-opening-footprint",()=>Emit([Comp("C",[box])],openings:[new("O","Void",["C"],XY,[],[],[],0,100,null,null,Sources)]));
  var shifted=XY with{id="Shifted",origin=new(1000,2000,3000)};
  var cshift=Comp("C",[box]) with{frame=shifted};
  Value("translated-source-section-area",10000,()=>Area(Emit([cshift],frame:shifted)));
  Value("translated-source-projection-depth",100,()=>Emit([cshift],frame:shifted,mode:"Projection",offset:null).Single().surface_depth_mm!.Value,1e-8,"mm");
  Fault("missing-dimensionless-basis-authority",()=>Emit([Comp("C",[box])],rules:Rules() with{basis_tolerance=null}),"CSG_TOLERANCE_UNRESOLVED");
  Fault("angular-tolerance-cannot-authorize-frame-scale",()=>Emit([Comp("C",[box]) with{frame=XY with{axis_u=new(1.005,0,0)}}],rules:Rules() with{angle_tolerance_rad=.1}),"CSG_FRAME_INVALID");
  return JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true});
 }
}

public sealed class LeaderRegionReviewTests
{
    [Xunit.Fact]
    public void IndependentAnalyticOraclesAndInvalidInputGuardsRemainCorrect()
    {
        using var result=JsonDocument.Parse(LeaderRegionReviewOracles.Evaluate());
        foreach(var row in result.RootElement.EnumerateArray())
            Xunit.Assert.True(row.GetProperty("pass").GetBoolean(),row.GetRawText());
    }
}
