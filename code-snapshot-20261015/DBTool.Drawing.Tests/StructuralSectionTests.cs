using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class StructuralSectionTests
{
    [Theory]
    [InlineData(325)]
    [InlineData(-450)]
    public void OffsetNeckSectionCutsHorizontalBarsThroughItsOwnCenter(double center)
    {
        var bars=new[]{new BarOccurrence("neck/h",null,"Neck",ComponentKind.Neck,Layer.Bottom,Axis.Y,10,
            new BarPath([new LineSegment3Mm(new(-650,center-300,1800),new(-650,center+300,1800))],false,
                HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),600,600,LengthStatus.Known,"TEST",[],
            RebarDirection.Horizontal,"Neck/T4")};
        var footprint=new Footprint(new Polygon2Mm([new(-700,1600),new(-575,1600),new(-575,2050),new(-700,2050)]),[]);
        var section=new ViewProfile("BB",ViewPurpose.Section,[new MaterialRegion("neck","Neck","Concrete",footprint,[])],[],[]);
        var result=StructuralRebarProjector.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),
            bars.ToDictionary(b=>b.Id,b=>"CG"),null,section,true);
        Assert.True(result.Success);
        var dot=Assert.Single(result.Value!.OfType<CirclePrimitive>(),p=>p.ViewId=="SteelBB");
        Assert.Equal(new Point2Mm(-650,1800),dot.Center);
        Assert.Equal("neck/h",dot.OccurrenceId);
        Assert.Equal(600,bars[0].ScheduleLengthMm);
    }
    [Fact]
    public void ParallelRepresentativeKeepsBothFragmentsOfOpeningClippedLane()
    {
        BarOccurrence Bar(string id,double x,double z0,double z1)=>new(id,null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            new BarPath([new LineSegment3Mm(new(x,650,z0),new(x,650,z1))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            z1-z0,z1-z0,LengthStatus.Known,"TEST",[],RebarDirection.Vertical);
        var bars=new[]{Bar("lane/lower",20,0,200),Bar("lane/upper",20,600,1000),Bar("background",120,0,1000)};
        var footprint=new Footprint(new Polygon2Mm([new(600,0),new(750,0),new(750,1200),new(600,1200)]),[]);
        var section=new ViewProfile("AA",ViewPurpose.Section,[new MaterialRegion("wall","Wall/T1","Concrete",footprint,[])],[],[]);
        var result=StructuralRebarProjector.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),bars.ToDictionary(b=>b.Id,b=>"T1"),section);
        Assert.True(result.Success);
        var lines=result.Value!.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="SteelAA").ToArray();
        Assert.Equal(2,lines.Length);
        Assert.Contains(lines,p=>p.OccurrenceId=="lane/lower" && p.Points[1].Y==200);
        Assert.Contains(lines,p=>p.OccurrenceId=="lane/upper" && p.Points[0].Y==600);
        Assert.DoesNotContain(lines,p=>p.OccurrenceId=="background");
        Assert.Equal(3,bars.Length);
        Assert.Equal(1000,bars[2].ScheduleLengthMm);
        var symbolic=StructuralRebarProjector.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),
            bars.ToDictionary(b=>b.Id,b=>b.Id),section,continuousSheetOneWalls:true);
        var lanes=symbolic.Value!.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="SteelAA")
            .OrderBy(p=>p.Points[0].Y).ToArray();
        Assert.Equal(2,lanes.Length);
        Assert.Equal(new Point2Mm(650,0),lanes[0].Points[0]);
        // The Sheet1 presentation must preserve the physical opening gap too.
        Assert.Equal(new Point2Mm(650,200),lanes[0].Points[1]);
        Assert.Equal(new Point2Mm(650,600),lanes[1].Points[0]);
        Assert.Equal(new Point2Mm(650,1000),lanes[1].Points[1]);
        Assert.Equal("lane/lower",lanes[0].OccurrenceId);
        Assert.Equal("lane/upper",lanes[1].OccurrenceId);
        Assert.Equal(200,bars[0].ScheduleLengthMm);
        Assert.Equal(400,bars[1].ScheduleLengthMm);
    }

    [Fact]
    public void CutWallKeepsAnchorageInsideBaseButBackgroundWallDoesNotAppear()
    {
        BarOccurrence Bar(string owner)=>new(owner,null,owner,ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            new BarPath([new LineSegment3Mm(new(0,650,-100),new(0,740,-100)),
                new LineSegment3Mm(new(0,740,-100),new(0,740,1000))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),1190,1190,LengthStatus.Known,"TEST",[],RebarDirection.Vertical);
        Footprint Rect(double x0,double y0,double x1,double y1)=>new(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
        var section=new ViewProfile("AA",ViewPurpose.Section,[
            new MaterialRegion("wall","Wall/T1","Concrete",Rect(600,0,775,1200),[]),
            new MaterialRegion("base","Base","Concrete",Rect(-875,-150,875,0),[])],[],[]);
        var bars=new[]{Bar("Wall/T1"),Bar("Wall/T2")};
        var result=StructuralRebarProjector.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),bars.ToDictionary(b=>b.Id,b=>"T"),section);
        Assert.True(result.Success);
        Assert.Contains(result.Value!.OfType<PolylinePrimitive>(),p=>p.ViewId=="SteelAA" && p.Points[0]==new Point2Mm(650,-100) && p.Points[1]==new Point2Mm(740,-100));
        Assert.DoesNotContain(result.Value,p=>p.ViewId=="SteelAA" && p.ComponentId=="Wall/T2");
    }

    [Fact]
    public void SectionUsesOneParallelRepresentativeAndCutsTransverseBarWithoutChangingLayout()
    {
        BarOccurrence Bar(string id,Point3Mm a,Point3Mm b,RebarDirection direction)=>new(id,null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            new BarPath([new LineSegment3Mm(a,b)],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),1000,1000,LengthStatus.Known,"TEST",[],direction);
        var bars=new[]{Bar("v1",new(-50,650,0),new(-50,650,1000),RebarDirection.Vertical),
            Bar("v2",new(50,650,0),new(50,650,1000),RebarDirection.Vertical),
            Bar("h",new(-500,650,300),new(500,650,300),RebarDirection.Horizontal)};
        var layout=new RebarLayout(bars,[],CalculationScope.FullRebar);
        var footprint=new Footprint(new Polygon2Mm([new(600,0),new(750,0),new(750,1200),new(600,1200)]),[]);
        var section=new ViewProfile("AA",ViewPurpose.Section,[new MaterialRegion("wall","Wall/T1","Concrete",footprint,[])],[],[]);
        var result=StructuralRebarProjector.Build(layout,bars.ToDictionary(b=>b.Id,b=>"T1"),section);
        Assert.True(result.Success);
        Assert.Single(result.Value!.OfType<PolylinePrimitive>(),p=>p.ViewId=="SteelAA");
        var dot=Assert.Single(result.Value.OfType<CirclePrimitive>(),p=>p.ViewId=="SteelAA");
        Assert.Equal(new Point2Mm(650,300),dot.Center);
        Assert.Equal(3,layout.Occurrences.Length);
        Assert.All(layout.Occurrences,b=>Assert.Equal(1000,b.ScheduleLengthMm));
    }
}
