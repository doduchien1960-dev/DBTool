using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class SheetOneLadderPresentationTests
{
    [Fact]
    public void PublishedLadderMeasuresActualTopOffsetPitchAndVisibleWidth()
    {
        BarOccurrence Rung(string id,double z)
        {
            Point3Mm[] points=[new(-675,475,z+150),new(-675,475,z),new(-425,475,z),
                new(-425,175,z),new(-675,175,z),new(-675,175,z+150)];
            var path=new BarPath(points.Zip(points.Skip(1),(a,b)=>(PathSegment)new LineSegment3Mm(a,b)),false,HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
            return new(id,null,"Wall/T4",ComponentKind.Wall,Layer.Bottom,Axis.Y,18,path,1100,1100,LengthStatus.Known,"LADDER-PUBLISHED-1",[],familyId:"Ladder");
        }
        MaterialRegion Region(string owner,double x0,double x1,double y0,double y1)=>new(owner,owner,"Concrete",
            new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]),[]);
        var aa=new ViewProfile("AA",ViewPurpose.Section,[Region("NeckSeat",-150,800,2050,2130)],[],[]);
        var bb=new ViewProfile("BB",ViewPurpose.Section,[Region("Wall/T4",-725,-575,0,1435),Region("NeckSeat",-700,50,2050,2130)],[],[]);
        var bars=new[]{Rung("first",1630),Rung("second",1330)};
        var result=SheetOneLadderPresentation.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),
            bars.ToDictionary(b=>b.Id,_=>"Th1"),aa,bb);
        var dimensions=result.OfType<DimensionIntentPrimitive>().ToArray();
        Assert.Equal(12,dimensions.Length);
        Assert.Equal(300,dimensions.Single(d=>d.SemanticId=="AA/Ladder/Wall/T4/Width").LogicalMeasurementMm);
        Assert.Equal(150,dimensions.Single(d=>d.SemanticId=="BB/Ladder/Wall/T4/Width").LogicalMeasurementMm);
        Assert.Equal(500,dimensions.Single(d=>d.SemanticId=="AA/Ladder/Wall/T4/TopOffset").LogicalMeasurementMm);
        Assert.Equal(new Point2Mm(55,1330),dimensions.Single(d=>d.SemanticId=="AA/Ladder/Wall/T4/Pitch").DimensionLinePoint);
        Assert.Equal(new Point2Mm(115,1740),result.OfType<TextPrimitive>().Single(p=>p.ViewId=="AA").Position);
        Assert.Equal(new Point2Mm(-395,1200),result.OfType<TextPrimitive>().Single(p=>p.ViewId=="BB").Position);
        Assert.Equal(new Point2Mm(205,1630),result.OfType<PolylinePrimitive>().Single(p=>p.ViewId=="AA").Points[0]);
    }
    [Fact]
    public void ShowsVisibleRungInItsSupportingSectionWithoutDiscardingPhysicalEmbedment()
    {
        Point3Mm[] points=[new(600,-300,1250),new(600,-300,1100),new(350,-300,1100),
            new(350,0,1100),new(600,0,1100),new(600,0,1250)];
        var path=new BarPath(points.Zip(points.Skip(1),(a,b)=>(PathSegment)new LineSegment3Mm(a,b)),false,HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
        var bar=new BarOccurrence("rung",null,"Wall/T2",ComponentKind.Wall,Layer.Bottom,Axis.Y,18,path,1100,1100,LengthStatus.Known,"TEST",[],familyId:"Ladder");
        var footprint=new Footprint(new Polygon2Mm([new(500,0),new(650,0),new(650,1400),new(500,1400)]),[]);
        var aa=new ViewProfile("AA",ViewPurpose.Section,[],[],[]);
        var bb=new ViewProfile("BB",ViewPurpose.Section,[new MaterialRegion("wall","Wall/T2","Concrete",footprint,[])],[],[]);
        var result=SheetOneLadderPresentation.Build(new RebarLayout([bar],[],CalculationScope.FullRebar),new Dictionary<string,string>{{"rung","Th1"}},aa,bb);
        var lines=result.OfType<PathPrimitive>().ToArray();
        Assert.Equal(4,lines.Length);
        Assert.Equal(new[]{"AA","SteelAA","BB","SteelBB"},lines.Select(l=>l.ViewId));
        Assert.All(lines.Where(l=>l.ViewId is "BB" or "SteelBB"),l=>{
            var s=Assert.IsType<LinePathSegment>(Assert.Single(l.Segments));
            Assert.Equal(new Point2Mm(500,1100),s.Start);
            Assert.Equal(new Point2Mm(350,1100),s.End);
            Assert.Equal("rung",l.OccurrenceId);
        });
        Assert.All(lines.Where(l=>l.ViewId is "AA" or "SteelAA"),l=>{
            var s=Assert.IsType<LinePathSegment>(Assert.Single(l.Segments));
            Assert.Equal(new Point2Mm(-300,1100),s.Start);
            Assert.Equal(new Point2Mm(0,1100),s.End);
            Assert.Equal("rung",l.OccurrenceId);
        });
        Assert.Contains(result.OfType<TextPrimitive>(),t=>t.Content=="Thang D18");
        Assert.Equal(5,path.Segments.Length);
        Assert.Equal(600,path.Segments[1].Start.X);
        Assert.Equal(1100,bar.ScheduleLengthMm);
    }
}
