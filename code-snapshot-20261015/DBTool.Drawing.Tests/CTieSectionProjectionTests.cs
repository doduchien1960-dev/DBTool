using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class CTieSectionProjectionTests
{
    [Theory]
    [InlineData("Wall/T1",false,"SteelAA")]
    [InlineData("Wall/T3",false,"SteelAA")]
    [InlineData("Wall/T2",true,"SteelBB")]
    [InlineData("Wall/T4",true,"SteelBB")]
    public void PhysicalCProjectsItsFourArcsIntoSupportingSection(string wall,bool alongY,string view)
    {
        var source=new SourceRef(SourceKind.SyntheticFixture,"C",new string('a',64),null,null,null,"C wrap fixture");
        var path=TiePlanner.BuildCWrap(alongY?new(50,0,500):new(0,50,500),22,
            alongY?new(110,0,500):new(0,110,500),22,8,50,source).Value!;
        var length=BendLengthCalculator.Calculate(path,0).Value!.Value;
        var bar=new BarOccurrence("tie",null,wall,ComponentKind.Wall,Layer.Top,alongY?Axis.Y:Axis.X,8,path,length,length,
            LengthStatus.Known,"VHG-C-WRAP-R8-20261012",[source],RebarDirection.Supplementary,wall,RebarSurface.Outer,"Tie");
        var result=StructuralRebarProjector.Build(new([bar],[],CalculationScope.FullRebar),new Dictionary<string,string>{{"tie","T1"}});
        Assert.True(result.Success);
        var arcs=result.Value!.OfType<PathPrimitive>().Where(p=>p.ViewId==view).ToArray();
        Assert.Equal(4,arcs.Length);Assert.All(arcs,p=> {Assert.Equal("tie",p.OccurrenceId);Assert.Equal("T1",p.ScheduleMark);
            Assert.Equal(8,Assert.IsType<ArcPathSegment>(Assert.Single(p.Segments)).RadiusMm);});
        Assert.DoesNotContain(result.Value,p=>p.ViewId==(view=="SteelAA"?"SteelBB":"SteelAA"));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ArcClipperRemovesInnerVoidAnalyticallyForBothSweepDirections(int sign)
    {
        var outer=new Polygon2Mm([new(-20,-20),new(20,-20),new(20,20),new(-20,20)]);
        var hole=new Polygon2Mm([new(-2,-15),new(-2,15),new(2,15),new(2,-15)]);
        var cuts=SectionRebarClipper.ClipArc(new(new(0,0),10,0,sign*2*Math.PI),[new Footprint(outer,[hole])]);
        Assert.Equal(2*Math.PI-4*Math.Asin(.2),cuts.Sum(a=>Math.Abs(a.SweepRadians)),8);
        Assert.All(cuts,a=> {Assert.Equal(10,a.RadiusMm);Assert.Equal(sign,Math.Sign(a.SweepRadians));
            double middle=a.StartRadians+a.SweepRadians/2;Assert.True(Math.Abs(10*Math.Cos(middle))>2);});
    }
}
