using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class WallMeshBandPolicyTests
{
    [Theory]
    [InlineData(1000,1250,638,548,745,655,543)]
    [InlineData(1150,1350,713,623,795,705,618)]
    [InlineData(1200,1450,738,648,845,755,693)]
    public void EightNativeBandsMatchThreeSourceCasesWithoutDiscardingOccurrenceLinks(
        double width,double length,double outerX,double innerX,double outerY,double innerY,double innerEnd)
    {
        var (inputs, primitives) = Fixture(width,length);
        var composed = WallMeshBandPolicy.Compose(primitives, inputs, Presentation).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(primitives.Length,composed.Length);
        Assert.Equal(primitives.Select(p=>p.SemanticId),composed.Select(p=>p.SemanticId));
        Assert.Equal(primitives.Select(p=>p.OccurrenceId),composed.Select(p=>p.OccurrenceId));
        Assert.All(composed,p=>{ Assert.True(p.Closed); Assert.Equal(WallMeshBandPolicy.Role,p.Role); Assert.Equal(4,p.Points.Length); });
        var aliases=WallMeshBandPolicy.RenderAliases(composed);
        Assert.Equal(primitives.Length-8,aliases.Count);
        var native=composed.Where(p=>!aliases.ContainsKey(p.SemanticId)).ToArray();
        Assert.Equal(8,native.Length);
        double[] Bounds(PolylinePrimitive p) => [p.Points.Min(v=>v.X),p.Points.Min(v=>v.Y)-180,p.Points.Max(v=>v.X),p.Points.Max(v=>v.Y)-180];
        Assert.Equal(new[]{-outerX,outerY-18,outerX,outerY+18},Bounds(native.Single(p=>p.ComponentId=="Wall/T1" && p.ScheduleMark=="Outer")));
        Assert.Equal(new[]{-innerX,innerY-18,innerX,innerY+18},Bounds(native.Single(p=>p.ComponentId=="Wall/T1" && p.ScheduleMark=="Inner")));
        Assert.Equal(new[]{-outerX,-innerY-18,-outerX+36,innerY+18},Bounds(native.Single(p=>p.ComponentId=="Wall/T4" && p.ScheduleMark=="Outer")));
        Assert.Equal(new[]{-innerX,-innerEnd,-innerX+36,innerEnd},Bounds(native.Single(p=>p.ComponentId=="Wall/T4" && p.ScheduleMark=="Inner")));
        foreach(var alias in aliases)
        {
            var a=composed.Single(p=>p.SemanticId==alias.Key); var b=composed.Single(p=>p.SemanticId==alias.Value);
            Assert.Equal(a.ComponentId,b.ComponentId); Assert.Equal(a.ScheduleMark,b.ScheduleMark); Assert.Equal(a.Points.ToArray(),b.Points.ToArray());
        }
    }

    [Fact]
    public void ConventionalProfileRetainsIndividualPhysicalProjections()
    {
        var (inputs, primitives)=Fixture(1000,1250);
        var result=WallMeshBandPolicy.Compose(primitives,inputs,new(30,ElevationMode.Symbolic,null,"Default",true));
        Assert.Equal(primitives,result);
        Assert.Empty(WallMeshBandPolicy.RenderAliases(result));
    }

    [Fact]
    public void MissingDesignInputRetainsProjectionsAndAliasesRequireExplicitBandRole()
    {
        var (inputs,primitives)=Fixture(1000,1250);
        var missing=new DrawingInputs(inputs.Geometry,inputs.RebarLayout,inputs.SteelSchedule,null,inputs.Quantities);
        Assert.Equal(primitives,WallMeshBandPolicy.Compose(primitives,missing,Presentation));
        var band=(PolylinePrimitive)WallMeshBandPolicy.Compose(primitives,inputs,Presentation)[0];
        var near=new PolylinePrimitive("near",band.Role,band.ViewId,band.ComponentId,band.OccurrenceId,band.ScheduleMark,
            band.Points.Select(p=>new Point2Mm(p.X+0.001,p.Y)),true);
        var ordinary=new PolylinePrimitive("ordinary","Rebar/Hidden",band.ViewId,band.ComponentId,band.OccurrenceId,band.ScheduleMark,band.Points,true);
        Assert.Empty(WallMeshBandPolicy.RenderAliases([band,near,ordinary]));
    }

    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static (DrawingInputs Inputs,PolylinePrimitive[] Primitives) Fixture(double width,double length)
    {
        ComponentGeometry Rect(string id,double x0,double y0,double x1,double y1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
            var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
            return new(id,ComponentKind.Wall,"Concrete",footprint,new(0,1400),new ExtrusionSolid(footprint,frame,new(0,1400)),[],[],[]);
        }
        double a=width/2,b=length/2;
        var geometry=new GeometryModel([Rect("Wall/T1",-a-150,b,a+150,b+150),Rect("Wall/T2",a,-b,a+150,b),
            Rect("Wall/T3",-a-150,-b-150,a+150,-b),Rect("Wall/T4",-a-150,-b,-a,b)],[]);
        var bars=new List<BarOccurrence>(); var primitives=new List<PolylinePrimitive>();
        foreach(var wall in geometry.Components)
        foreach(var face in new[]{RebarSurface.Outer,RebarSurface.Inner})
        foreach(int z in new[]{300,600})
        {
            string id=$"{wall.Id}/{face}/{z}";
            var bar=new BarOccurrence(id,null,wall.Id,ComponentKind.Wall,Layer.Top,Axis.X,10,
                new BarPath([new LineSegment3Mm(new(0,0,z),new(100,0,z))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
                100,100,LengthStatus.Known,"TEST",[],RebarDirection.Horizontal,wall.Id,face);
            bars.Add(bar);
            for(int i=0;i<3;i++) primitives.Add(new($"WallMeshPlan/{id}/{i}","Rebar/Hidden","WallMeshPlan",wall.Id,id,face.ToString(),
                [new(0,0),new(100,0)],false));
        }
        var spec=new WallOrNeckRebarSpec(10,150,0,0,coverMm:30);
        return(new(geometry,new RebarLayout(bars,[],CalculationScope.FullRebar),new SteelSchedule([],[],new MassKg(0),"D2Over162","v1"),
            null,DrawingQuantitySnapshot.NotCalculated,spec,spec),primitives.ToArray());
    }
}
