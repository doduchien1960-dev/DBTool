using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class HaunchSectionProjectionTests
{
    [Fact]
    public void RoofHaunchRetainsSlabSlopeAndWallLegInTheirActualMaterialUnion()
    {
        var points=new Point3Mm[]{new(0,65,1035),new(0,365,1035),new(0,535,865),new(0,535,565)};
        var segments=Enumerable.Range(0,3).Select(i=>(PathSegment)new LineSegment3Mm(points[i],points[i+1])).ToArray();
        double length=segments.Sum(s=>Math.Sqrt(Math.Pow(s.End.X-s.Start.X,2)+Math.Pow(s.End.Y-s.Start.Y,2)+Math.Pow(s.End.Z-s.Start.Z,2)));
        var bar=new BarOccurrence("roof-haunch",null,"Roof/Haunch",ComponentKind.Roof,Layer.Top,Axis.Y,10,
            new BarPath(segments,false,HookPattern.Hook2M,0,false,MeasurementBasis.NominalPolyline),length,null,
            LengthStatus.NeedsRule,HaunchRebarPlanner.RuleId,[],RebarDirection.Supplementary,"Wall/T1",RebarSurface.Inner,"Haunch");
        MaterialRegion Region(string owner,params Point2Mm[] vertices)=>new(owner,owner,"Concrete",new Footprint(new Polygon2Mm(vertices),[]),[]);
        var section=new ViewProfile("AA",ViewPurpose.Section,
            [Region("Roof",new(-650,1000),new(650,1000),new(650,1150),new(-650,1150)),
             Region("Roof/Haunch",new(350,1000),new(500,850),new(500,1000)),
             Region("Wall/T1",new(500,0),new(650,0),new(650,1000),new(500,1000))],[],[]);
        var review=new Diagnostic("TEST_UNCONFIRMED",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,null,null,null,"Candidate only.");
        var result=StructuralRebarProjector.Build(new RebarLayout([bar],[review],CalculationScope.FullRebar,LayoutStatus.Partial),
            new Dictionary<string,string>{{bar.Id,"H1"}},section,new ViewProfile("BB",ViewPurpose.Section,[],[],[]));
        Assert.True(result.Success);
        var lines=result.Value!.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="SteelAA").ToArray();
        Assert.NotEmpty(lines);
        double projected=lines.Sum(p=>Math.Sqrt(Math.Pow(p.Points[^1].X-p.Points[0].X,2)+Math.Pow(p.Points[^1].Y-p.Points[0].Y,2)));
        Assert.Equal(length,projected,6);
        Assert.All(lines,p=>{Assert.Equal(bar.Id,p.OccurrenceId);Assert.Equal("H1",p.ScheduleMark);});
    }
}
