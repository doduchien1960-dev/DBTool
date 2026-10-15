using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Domain.Tests;
public sealed class VhgCTiePathTests
{
    [Theory]
    [InlineData(10,6,60,76,16)]
    [InlineData(16,8,100,124,24)]
    [InlineData(22,8,60,90,30)]
    public void CWrapDimensionsFollowActualRodCentersAndDiameters(int mainDiameter,int tieDiameter,double separation,double expectedSpan,double expectedHeight)
    {
        var result=TiePlanner.BuildCWrap(new(200,300,1000),mainDiameter,new(200+separation,300,1000),mainDiameter,tieDiameter,50,Source);
        Assert.True(result.Success);
        var points=result.Value!.Segments.SelectMany(s=>new[]{s.Start,s.End}).ToArray();
        Assert.Equal(expectedSpan,points.Max(p=>p.X)-points.Min(p=>p.X),8);
        Assert.Equal(expectedHeight,points.Max(p=>p.Z)-points.Min(p=>p.Z),8);
        Assert.All(result.Value.Segments.OfType<ArcSegment3Mm>(),a=>Assert.Equal(8,a.CenterlineRadiusMm));
        Assert.Equal(4,result.Value.Segments.OfType<ArcSegment3Mm>().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void CFilletsRemainTangentInOffsetVerticalFrames(int normalSign)
    {
        var frame=new Frame3Mm(new(1000,-300,2000),new(0,1,0),new(0,0,1),new(normalSign,0,0));
        var result=TiePlanner.BuildCPath(90,30,50,frame,Source);
        Assert.True(result.Success);var segments=result.Value!.Segments;
        Assert.Equal(new Point3Mm(1000,-250,2030),segments[0].Start);
        for(int i=1;i<segments.Length;i++) Assert.InRange(BendLengthCalculator.Distance(segments[i-1].End,segments[i].Start),0,1e-8);
        for(int i=1;i<segments.Length;i+=2)
        {
            var arc=Assert.IsType<ArcSegment3Mm>(segments[i]);
            var before=Assert.IsType<LineSegment3Mm>(segments[i-1]);
            var after=Assert.IsType<LineSegment3Mm>(segments[i+1]);
            double Dot(Point3Mm p,Point3Mm q,Point3Mm center,Point3Mm endpoint)=>
                (q.X-p.X)*(endpoint.X-center.X)+(q.Y-p.Y)*(endpoint.Y-center.Y)+(q.Z-p.Z)*(endpoint.Z-center.Z);
            Assert.InRange(Math.Abs(Dot(before.Start,before.End,arc.Center,arc.Start)),0,1e-8);
            Assert.InRange(Math.Abs(Dot(after.Start,after.End,arc.Center,arc.End)),0,1e-8);
        }
    }

    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"owner-C-fillet-R8",new string('a',64),null,null,null,"Owner instruction 2026-10-12: CAD FILLET R8 four corners");
    [Fact]
    public void OwnerCFixtureHasFiveStraightRunsAndFourR8TangentArcs()
    {
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var result=TiePlanner.BuildCPath(90,30,50,frame,Source);
        Assert.True(result.Success);var path=result.Value!;
        Assert.Equal(9,path.Segments.Length);
        Assert.Equal(new Point3Mm(50,30,0),path.Segments[0].Start);
        Assert.Equal(new Point3Mm(40,30,0),path.Segments[^1].End);
        Assert.Equal(new double[]{42,14,74,14,42},path.Segments.OfType<LineSegment3Mm>().Select(l=>BendLengthCalculator.Distance(l.Start,l.End)));
        Assert.All(path.Segments.OfType<ArcSegment3Mm>(),a=> {Assert.Equal(8,a.CenterlineRadiusMm);Assert.Equal(Math.PI/2,a.SweepRadians);});
        var measured=BendLengthCalculator.Calculate(path,0,Source);
        Assert.True(measured.Success);Assert.Equal(250-64+16*Math.PI,measured.Value!.Value,8);
    }
    [Theory]
    [InlineData(16,30,8)]
    [InlineData(90,15,50)]
    [InlineData(90,30,8)]
    public void FilletCannotShortenRequestedDimensionsToFit(double span,double leg,double hook)
    {
        var result=TiePlanner.BuildCPath(span,leg,hook,new(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1)),Source);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Code=="TIE_C_FILLET_SPACE_INVALID");
    }
}
