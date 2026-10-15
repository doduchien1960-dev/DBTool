using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class Bending2DPlannerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.73)]
    public void VerticalAndRotatedLegsRetainLengths(double angle)
    {
        var x = Math.Cos(angle); var y = Math.Sin(angle);
        var path = Path(new LineSegment3Mm(new(5,6,7),new(5,6,107)),
            new LineSegment3Mm(new(5,6,107),new(5+200*x,6+200*y,107)));
        var result=Bending2DPlanner.Project(path);
        Assert.True(result.Success);
        Assert.Equal(100, Distance(result.Value![0]), 8);
        Assert.Equal(200, Distance(result.Value![1]), 8);
        Assert.Equal(result.Value[0].End,result.Value[1].Start);
    }
    [Fact]
    public void VerticalArcKeepsSignedSweepAndRadius()
    {
        var arc=new ArcSegment3Mm(new(10,20,30),50,new(0,1,0),new(0,0,1),-Math.PI/2);
        var result=Bending2DPlanner.Project(Path(arc));
        Assert.True(result.Success);
        var projected=Assert.IsType<ArcPathSegment>(Assert.Single(result.Value!));
        Assert.Equal(50,projected.RadiusMm);
        Assert.Equal(-Math.PI/2,projected.SweepRadians);
    }
    [Fact]
    public void NonplanarPathIsBlockedInsteadOfFlattened()
    {
        var result=Bending2DPlanner.Project(Path(new LineSegment3Mm(new(0,0,0),new(100,0,0)),
            new LineSegment3Mm(new(100,0,0),new(100,100,0)),new LineSegment3Mm(new(100,100,0),new(100,100,100))));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="BENDING_NONPLANAR_UNSUPPORTED");
    }
    [Fact]
    public void HundredIdenticalBarsProduceOneSchematicWithAllSources()
    {
        var bars=Enumerable.Range(0,100).Select(i=>new BarOccurrence($"bar-{i}",null,"Base",ComponentKind.Base,
            Layer.Bottom,Axis.X,10,Path(new LineSegment3Mm(new(0,i,0),new(1000,i,0))),1000,1000,
            LengthStatus.Known,"TEST",[])).ToArray();
        var result=BendingSchematicPlanner.Build(bars,bars.ToDictionary(b=>b.Id,b=>"D001"));
        Assert.True(result.Success);
        var shape=Assert.Single(result.Value!);
        Assert.Equal(100,shape.Quantity);
        Assert.Equal(100,shape.SourceOccurrenceIds.Length);
        Assert.Equal("D001",shape.Mark);
        Assert.Single(shape.Segments);
    }
    private static double Distance(DrawingPathSegment s)=>Math.Sqrt(Math.Pow(s.End.X-s.Start.X,2)+Math.Pow(s.End.Y-s.Start.Y,2));
    private static BarPath Path(params PathSegment[] segments)=>new(segments,false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
}
