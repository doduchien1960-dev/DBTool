using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public sealed class LadderUnfoldingTests
{
    private static BarPath Ladder() => Path(new(0,100,150),new(0,100,0),new(0,-150,0),
        new(300,-150,0),new(300,100,0),new(300,100,150));
    private static BarPath Path(params Point3Mm[] points) => new(points.Zip(points.Skip(1),
        (a,b)=>(PathSegment)new LineSegment3Mm(a,b)),false,HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
    [Fact]
    public void ExplicitLadderUnfoldPreservesFiveLengthsAndFourRightAngles()
    {
        var source=Ladder();
        Assert.False(Bending2DPlanner.Project(source).Success);
        var result=Bending2DPlanner.UnfoldLadder(source);
        Assert.True(result.Success);
        Assert.Equal(5,result.Value!.Count);
        for(var i=0;i<5;i++)
        {
            var s=result.Value[i];
            var physical=source.Segments[i];
            var length=Math.Sqrt(Math.Pow(physical.End.X-physical.Start.X,2)+Math.Pow(physical.End.Y-physical.Start.Y,2)+Math.Pow(physical.End.Z-physical.Start.Z,2));
            Assert.Equal(length,Math.Sqrt(Math.Pow(s.End.X-s.Start.X,2)+Math.Pow(s.End.Y-s.Start.Y,2)),8);
            if(i==0) continue;
            var p=result.Value[i-1];
            Assert.Equal(p.End,s.Start);
            Assert.Equal(0,(p.End.X-p.Start.X)*(s.End.X-s.Start.X)+(p.End.Y-p.Start.Y)*(s.End.Y-s.Start.Y),8);
        }
    }
    [Fact]
    public void NonLadderFamilyKeepsNonplanarGate()
    {
        var bar=new BarOccurrence("x",null,"Wall",ComponentKind.Wall,Layer.Bottom,Axis.X,18,Ladder(),1100,1100,LengthStatus.Known,"TEST",[],familyId:"Wall");
        Assert.False(BendingSchematicPlanner.Build([bar],new Dictionary<string,string>{{"x","T1"}}).Success);
    }
    [Fact]
    public void LadderFamilyUsesContractWithoutChangingScheduleOrSources()
    {
        var bar=new BarOccurrence("x",null,"Ladder",ComponentKind.Wall,Layer.Bottom,Axis.X,18,Ladder(),1100,1100,LengthStatus.Known,"TEST",[],familyId:"Ladder");
        var result=BendingSchematicPlanner.Build([bar],new Dictionary<string,string>{{"x","Th1"}});
        Assert.True(result.Success);
        var shape=Assert.Single(result.Value!);
        Assert.Equal(1100,shape.ScheduleLengthMm);
        Assert.Equal("x",Assert.Single(shape.SourceOccurrenceIds));
        Assert.Equal(5,shape.Segments.Length);
    }
    [Fact]
    public void ArbitraryFiveSegmentNonplanarPathIsRejected()
    {
        var path=Path(new(0,0,0),new(0,0,10),new(0,20,10),new(30,20,10),new(30,0,10),new(40,0,20));
        Assert.False(Bending2DPlanner.UnfoldLadder(path).Success);
    }
}
