using System.Text.Json;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing;
using DBTool.Drawing.StageContractsV2;
using DBTool.Drawing.PlanarRegions;

public static class L03OccupiedRegionOracles
{
 static readonly FrameV2 XY=new("XY",new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1),1);
 static readonly RulesV2 Rules=new("oracle",[new("NR-18","1","Approved","Leader","2026-10-15",["*"],["*"],["*"],[],[],[],["L03-SPEC"])],.0001,.0001,null,null,1e-6,1e-8,.000001,null);
 static readonly List<object> Rows=[];
 static CurveV2 L(string id,double x,double y,double x1,double y1)=>new(id,"Line",new(x,y,0),new(x1,y1,0),null,null,null,null,"Boundary");
 static LoopV2 Rect(string id,double x,double y,double w,double h,string role="Outer")=>new(id,role,[L(id+"0",x,y,x+w,y),L(id+"1",x+w,y,x+w,y+h),L(id+"2",x+w,y+h,x,y+h),L(id+"3",x,y+h,x,y)],true,w*h);
 static LoopV2 Disk(string id,double x)=>new(id,"Outer",[new(id+"a","Arc",new(x+10,0,0),new(x+10,0,0),new(x,0,0),new(0,0,1),10,2*Math.PI,"Boundary")],true,100*Math.PI);
 static ComponentV2 C(string id,LoopV2[] loops, SolidPartV2[]? parts=null, FrameV2? f=null)=>new(id,"Concrete","A","M",f??XY,loops,0,100,100,[],[],["L03-SPEC"],parts??[],id,"Concrete");
 static Region2V2[] Emit(ComponentV2[] cs,OpeningV2[]? os=null,FrameV2? view=null,string mode="TrueSection",double? offset=50) {
  var f=view??XY;
  return new AssemblyBoundaryEmitter().Emit(new(cs,os??[],[],"oracle",[]),[],[new("V","Section","A",f,mode=="Projection"?null:offset,1,false,true,null,cs.Select(c=>new ViewPartCutV2(c.id,f,mode)).ToArray())],Rules);
 }
 static void Check(string id,double expected,Func<double> run,double tol=1e-7,string unit="mm2") {
  try {double actual=run();bool pass=Math.Abs(actual-expected)<=tol;Rows.Add(new{id,expected,actual,unit,tolerance=tol,pass});Console.WriteLine($"{id}: expected {expected:R}, actual {actual:R} {unit}, tol {tol:R}: {(pass?"PASS":"FAIL")}");}
  catch(Exception ex){Rows.Add(new{id,expected,actual=ex.Message,unit,tolerance=tol,pass=false});Console.WriteLine($"{id}: FAIL {ex.Message}");}
 }
 static double Area(Region2V2[] rs)=>rs.SelectMany(r=>r.loops).Sum(l=>l.signed_area_mm2);
 static void Fault(string id,Func<object> run,string code) {
  try{run();Rows.Add(new{id,expected=code,actual="NO_FAULT",unit="diagnostic",tolerance=0,pass=false});Console.WriteLine($"{id}: FAIL NO_FAULT");}
  catch(Exception ex){bool pass=ex.Message.Contains(code);Rows.Add(new{id,expected=code,actual=ex.Message,unit="diagnostic",tolerance=0,pass});Console.WriteLine($"{id}: {(pass?"PASS":"FAIL")} {ex.Message}");}
 }
 public static int Run() {
  Rows.Clear();
  var outer=Rect("outer",0,0,100,100);var hole=Rect("hole",30,30,40,40,"Inner");var donut=C("donut",[outer,hole]);
  var orientedHole=hole with{curves=hole.curves.Reverse().Select(c=>c with{start=c.end,end=c.start}).ToArray(),signed_area_mm2=-1600};
  Check("ACN04-preserve-oriented-donut",8400,()=>Area(Emit([C("oriented",[outer,orientedHole])])),1e-7);
  Check("ACN04-donut",8400,()=>Area(Emit([donut])));
  var cutter=Rect("cutter",40,40,20,20);
  Check("void-cutter-does-not-remove-twice",8400,()=>Area(Emit([donut],[new("O","Void",["donut"],XY,[cutter],[],[cutter],0,100,null,null,["L03-SPEC"])])));
  Check("donut-union-island",8800,()=>Area(Emit([donut,C("island",[Rect("island",40,40,20,20)])])));
  var half=new LoopV2("half","Outer",[new("halfarc","Arc",new(10,0,0),new(-10,0,0),new(0,0,0),new(0,0,1),10,Math.PI,"Boundary"),L("diameter",-10,0,10,0)],true,50*Math.PI);
  Check("ACN05-half-disk",50*Math.PI,()=>Area(Emit([C("half",[half])])));
  var reflected=new FrameV2("Reflected",new(0,0,0),new(1,0,0),new(0,-1,0),new(0,0,1),-1);
  Check("reflected-lower-midpoint",-10,()=>{var a=Emit([C("half",[half])],view:reflected).SelectMany(r=>r.loops).SelectMany(l=>l.curves).Single(a=>a.kind=="Arc");var sweep=a.sweep_rad!.Value*(a.normal!.z<0?-1:1);return a.center!.y_mm+a.radius_mm!.Value*Math.Sin(Math.Atan2(a.start.y_mm-a.center.y_mm,a.start.x_mm-a.center.x_mm)+sweep/2);},1e-8,"mm");
  Check("two-disjoint-full-circles",200*Math.PI,()=>Area(Emit([C("d0",[Disk("d0",0)]),C("d1",[Disk("d1",40)])])));
  var broken=Rect("broken",200,0,10,10);broken=broken with{curves=broken.curves[..3]};
  Fault("unconsumed-edge-is-atomic-fault",()=>Emit([C("invalid",[outer,broken])]),"CSG_LOOP_NOT_CLOSED");
  var XZ=new FrameV2("XZ",new(0,0,0),new(1,0,0),new(0,0,1),new(0,-1,0),1);
  Check("submillimeter-section",.04,()=>Area(Emit([C("thin",[Rect("thin",0,0,.0004,100)])],view:XZ,offset:-50)),1e-8);
  Check("ACN03-engulfed-explicit-empty",0,()=>Area(Emit([C("empty",[],[new("U","Union",XY,[outer],0,100),new("S","Subtract",XY,[Rect("engulf",-10,-10,120,120)],0,100)])])));
  Fault("zero-sweep-rejected",()=>Emit([C("bad",[Disk("bad",0) with{curves=[Disk("bad",0).curves[0] with{sweep_rad=0}]}])]),"CURVE_DOMAIN_INVALID");
  Fault("multiturn-rejected",()=>Emit([C("bad",[Disk("bad",0) with{curves=[Disk("bad",0).curves[0] with{sweep_rad=4*Math.PI}]}])]),"CURVE_DOMAIN_INVALID");
  Fault("inconsistent-endpoint-rejected",()=>Emit([C("bad",[Disk("bad",0) with{curves=[Disk("bad",0).curves[0] with{end=new(0,10,0)}]}])]),"CURVE_DOMAIN_INVALID");
  Check("overlap-union",15000,()=>Area(Emit([C("a",[outer]),C("b",[Rect("overlap",50,0,100,100)])])));
  Check("touching-union",20000,()=>Area(Emit([C("a",[outer]),C("b",[Rect("touch",100,0,100,100)])])));
  Check("identical-union",10000,()=>Area(Emit([C("a",[outer]),C("b",[outer])])));
  Check("ordered-intersect",5000,()=>Area(Emit([C("ordered",[],[new("U","Union",XY,[outer],0,100),new("I","Intersect",XY,[Rect("overlap",50,0,100,100)],0,100)])])));
  Check("ordered-empty-intersection-stays-empty",0,()=>Area(Emit([C("ordered",[],[new("U","Union",XY,[outer],0,100),new("I","Intersect",XY,[],0,100)])])));
  Check("donut-vertical-section",6000,()=>Area(Emit([donut],view:XZ,offset:-50)));
  Check("circle-vertical-diameter-section",2000,()=>Area(Emit([C("disk",[Disk("d",0)])],view:XZ,offset:0)));
  Check("unowned-opening-does-not-cut-neighbour",18400,()=>Area(Emit([C("a",[outer]),C("b",[Rect("far",200,0,100,100)])],[new("O","Void",["a"],XY,[hole with{role="Outer"}],[],[],0,100,null,null,["L03-SPEC"])])));
  Check("projection-ordered-subtract",8400,()=>Area(Emit([C("project",[],[new("U","Union",XY,[outer],0,100),new("S","Subtract",XY,[hole with{role="Outer"}],0,100)])],mode:"Projection")));
  var circle=Disk("twoArc",0);var arc=circle.curves[0];
  circle=circle with{curves=[arc with{end=new(-10,0,0),sweep_rad=Math.PI},arc with{id="second",start=new(-10,0,0),sweep_rad=Math.PI}]};
  Check("two-arc-circle",100*Math.PI,()=>Area(Emit([C("two",[circle])])));
  Fault("duplicate-component-ownership",()=>Emit([C("same",[outer]),C("same",[outer])]),"CSG_OWNERSHIP_INVALID");
  Fault("missing-component-source",()=>Emit([C("a",[outer]) with{source_ids=[]}]),"CSG_SOURCE_UNRESOLVED");
  Fault("nonfinite-sweep",()=>Emit([C("bad",[Disk("bad",0) with{curves=[Disk("bad",0).curves[0] with{sweep_rad=double.NaN}]}])]),"CURVE_DOMAIN_INVALID");
  Check("single-operand-overlapping-outers",15000,()=>Area(Emit([C("single",[outer,Rect("overlap",50,0,100,100)])])));
  Check("overlapping-owned-holes",7600,()=>Area(Emit([C("holes",[outer,hole,Rect("hole2",50,30,40,40,"Inner")])])));
  Check("overlapping-analytic-disks",200*Math.PI-(200*Math.Acos(.5)-50*Math.Sqrt(3)),()=>Area(Emit([C("d0",[Disk("d0",0)]),C("d1",[Disk("d1",10)])])));
  Check("signed-negative-source-sweep-midpoint",-10,()=>{var h=half with{curves=[half.curves[0] with{sweep_rad=-Math.PI},half.curves[1]]};var a=Emit([C("neg",[h])]).SelectMany(r=>r.loops).SelectMany(l=>l.curves).Single(a=>a.kind=="Arc");return a.center!.y_mm+a.radius_mm!.Value*Math.Sin(Math.Atan2(a.start.y_mm-a.center.y_mm,a.start.x_mm-a.center.x_mm)+a.sweep_rad!.Value*Math.Sign(a.normal!.z)/2);},1e-8,"mm");
  Check("tangent-full-circles",200*Math.PI,()=>Area(Emit([C("d0",[Disk("d0",0)]),C("d1",[Disk("d1",20)])])));
  Fault("hole-crosses-owned-outer",()=>Emit([C("cross",[outer,Rect("crossingHole",90,10,40,40,"Inner")])]),"CSG_HOLE_OWNER_UNRESOLVED");
  Fault("missing-active-section-offset",()=>Emit([C("a",[outer])],offset:null),"CSG_SECTION_OFFSET_UNRESOLVED");
  Fault("unknown-cut-kind",()=>Emit([C("a",[outer])],mode:"Bogus"),"CSG_CUT_KIND_INVALID");
  Fault("missing-geometry-is-not-explicit-empty",()=>Emit([C("a",[])]),"CSG_GEOMETRY_UNRESOLVED");
  var tilted=new FrameV2("Tilt",new(0,0,0),new(1,0,0),new(0,Math.Sqrt(.5),Math.Sqrt(.5)),new(0,-Math.Sqrt(.5),Math.Sqrt(.5)),1);
  Fault("tilted-arc-projection-blocks-ellipse",()=>Emit([C("a",[half])],view:tilted,mode:"Projection"),"CSG_PROJECTION_UNSUPPORTED");
#if !L03_FROZEN_BASELINE
  var kernel=new AnalyticPlanarOccupiedRegionKernel();
  var kt=new PlanarRegionTolerance(.0001,1e-8,.000001,["L03-SPEC"],1e-6);
  var request=new PlanarOccupiedRegionRequest("AuditRequest","A","M","V",
    [new("owned",[new(0,"donut","Union",[outer,hole],["GeometrySource"]),new(1,"island","Union",[cutter],["IslandSource"])],["ComponentSource"])],kt,["L03-SPEC"]);
  var result=kernel.Execute(request);
  Check("port-total-area",8800,()=>result.TotalOccupiedAreaMm2);
  Check("port-input-edge-audit",12,()=>result.InputEdgeCount,0,"count");
  Check("port-unconsumed-audit",0,()=>result.UnconsumedEdgeIds.Length,0,"count");
  Check("port-owned-outer-hole-island-tree",3,()=>result.Boundaries.Count(b=>b.ParentLoopId is null || result.Boundaries.Any(p=>p.Boundary.id==b.ParentLoopId)),0,"count");
  Check("port-island-nested-in-hole",1,()=>result.Boundaries.Count(b=>b.Boundary.role=="Outer"&&b.ParentLoopId is not null
    &&result.Boundaries.Any(p=>p.Boundary.id==b.ParentLoopId&&p.Boundary.role=="Inner")),0,"count");
  Check("port-provenance-complete",4,()=>result.SourceIds.Length,0,"count");
  var empty=kernel.Execute(request with{Components=[new("owned",[new(0,"outer","Union",[outer],["GeometrySource"]),new(1,"remove","Subtract",[Rect("engulf",-10,-10,120,120)],["GeometrySource"])],["ComponentSource"])]});
  Check("port-explicit-empty-flag",1,()=>empty.IsEmpty?1:0,0,"boolean");
#endif
  foreach(double radius in new[]{10.0,1000000.0}) {
    double y=radius*Math.Sin(.000001);
    var hs=new LoopV2("scaledHalf","Outer",[new("arc","Arc",new(radius,0,0),new(-radius,0,0),new(0,0,0),new(0,0,1),radius,Math.PI,"Boundary"),L("diameter",-radius,0,radius,0)],true,.5*Math.PI*radius*radius);
    Check("radius-"+radius+"-near-arc-endpoint-section",2*Math.Sqrt(radius*radius-y*y)*100,
      ()=>Area(Emit([C("scaled",[hs])],view:XZ,offset:-y)),radius>100?1e-5:1e-7);
  }
#if L03_ORACLE_RUNNER
  File.WriteAllText("results.json",JsonSerializer.Serialize(Rows,new JsonSerializerOptions{WriteIndented=true}));
#endif
  return Rows.Any(r=>!(bool)r.GetType().GetProperty("pass")!.GetValue(r)!)?1:0;
 }
}



#if L03_ORACLE_RUNNER
public static class OracleEntryPoint { public static int Main()=>L03OccupiedRegionOracles.Run(); }
#else
public sealed class L03OccupiedRegionAcceptanceTests
{
 [Xunit.Fact] public void PublicEmitMatchesIndependentAnalyticOracles() =>
     Xunit.Assert.Equal(0,L03OccupiedRegionOracles.Run());
}
#endif
