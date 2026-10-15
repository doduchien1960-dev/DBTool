using DBTool.Core;
using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;
using Interval1D = DBTool.Domain.Geometry.Interval1D;

namespace DBTool.Domain.Tests;

public sealed class OpeningClipperTests
{
    private static readonly ClippingPolicy Policy = new(ClipMode.SingleRound, 1e-6);
    private static readonly WallFrame T1 = WallFrame.Create(WallId.T1, 1000, 1250);

    [Theory]
    [InlineData(0, 10, -5, 5)]
    [InlineData(3, 12, -4, 4)]
    public void ExistingCircleHelperRetainsIndependentR5Vectors(double y, double retainedMm, double leftEnd, double rightStart)
    {
        var intervals = CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, y, 0, 0, 5, 1e-6);
        Assert.Equal(2, intervals.Count);
        Near(-10, intervals[0].Start);
        Near(leftEnd, intervals[0].End);
        Near(rightStart, intervals[1].Start);
        Near(10, intervals[1].End);
        Near(retainedMm, intervals.Sum(x => x.Length));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void ExistingCircleHelperLeavesTangentAndOutsideBarUnchanged(double y)
    {
        var interval = Assert.Single(CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, y, 0, 0, 5, 1e-6));
        Near(-10, interval.Start); Near(10, interval.End);
    }

    [Fact]
    public void ExistingCircleHelperCanRemoveAllOrMissFarCircle()
    {
        Assert.Empty(CircleOpeningClipper.SubtractCircleFromHorizontal(-2, 2, 0, 0, 0, 5, 1e-6));
        var missed = Assert.Single(CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 0, 100, 0, 5, 1e-6));
        Near(20, missed.Length);
    }

    [Fact]
    public void WallLocalS1CircleCreatesTwoPhysicalChildrenAndConservesLength()
    {
        var parent = WallBar("P", -500, 500, 500);
        var source = SyntheticTestFactory.Create().Sources[0];
        var exclusion = RoundExclusion.ForWall("Wall/T1", T1, new Interval1D(0,150),
            new Point2Mm(0,500), 305, 15, 10, source);
        var result = OpeningClipper.ClipStraightSegment(parent, exclusion, Policy);
        Assert.True(result.Success);
        var clipped = result.Value!;
        Assert.Equal(new[] { "P/clip/0", "P/clip/1" }, clipped.Retained.Select(b => b.Id));
        Assert.Equal(new[] { 175d, 175d }, clipped.Retained.Select(b => b.ScheduleLengthMm));
        Near(1000, clipped.OriginalLengthMm);
        Near(650, clipped.RemovedLengthMm);
        Near(350, clipped.Retained.Sum(b => b.ScheduleLengthMm));
        Near(clipped.OriginalLengthMm, clipped.RemovedLengthMm + clipped.Retained.Sum(b => b.ScheduleLengthMm));
        var left = clipped.Retained[0].Path.Segments[0];
        var right = clipped.Retained[1].Path.Segments[0];
        Near(-500, T1.ToLocal(left.Start).U); Near(-325, T1.ToLocal(left.End).U);
        Near(325, T1.ToLocal(right.Start).U); Near(500, T1.ToLocal(right.End).U);
        Assert.All(clipped.Retained, b => {
            Assert.Equal("P", b.ParentId);
            Assert.Equal("Wall/T1", b.ComponentId);
            Assert.Equal(ComponentKind.Wall, b.ComponentKind);
            Assert.Equal(LengthStatus.Known, b.LengthStatus);
            Assert.Equal(HookPattern.Hook0M, b.Path.HookPattern);
            Assert.Contains(source, b.Sources);
        });
    }

    [Fact]
    public void DifferentOwnerBaseBarIsNotClippedByWallOpening()
    {
        var source = SyntheticTestFactory.Create().Sources[0];
        var basePath = new BarPath([new LineSegment3Mm(new Point3Mm(-500,0,-115),new Point3Mm(500,0,-115))],
            false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var baseBar = new BarOccurrence("BaseBar",null,"Base",ComponentKind.Base,Layer.Bottom,Axis.X,10,
            basePath,1000,1000,LengthStatus.Known,"MESH-UNIFORM-TEST-001",[source]);
        var exclusion = RoundExclusion.ForWall("Wall/T1", T1, new Interval1D(0,150),
            new Point2Mm(0,500),305,15,10,source);
        var result = OpeningClipper.ClipStraightSegment(baseBar,exclusion,Policy);
        Assert.True(result.Success);
        Assert.Same(baseBar,Assert.Single(result.Value!.Retained));
        Near(0,result.Value.RemovedLengthMm);
    }

    [Fact]
    public void ReversedLocalIntervalAndNegativeRadiusAreBlocked()
    {
        var reverse = WallBar("reverse",10,-10,500);
        var good = RoundExclusion.ForWall("Wall/T1",T1,new Interval1D(0,150),new Point2Mm(0,500),5,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(reverse,good,Policy),"CLIP_INTERVAL_INVALID");
        var negative = RoundExclusion.ForWall("Wall/T1",T1,new Interval1D(0,150),new Point2Mm(0,500),-1,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(WallBar("normal",-10,10,500),negative,Policy),"CLIP_DIMENSION_INVALID");
    }

    [Fact]
    public void UnsupportedModeBentPathAndWrongPlaneAreBlocked()
    {
        var bar = WallBar("P",-10,10,500);
        var circle = RoundExclusion.ForWall("Wall/T1",T1,new Interval1D(0,150),new Point2Mm(0,500),5,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(bar,circle,new ClippingPolicy(ClipMode.Box,1e-6)),"CLIP_MODE_UNSUPPORTED");
        AssertBlock(OpeningClipper.ClipStraightSegment(bar,circle,new ClippingPolicy(ClipMode.Multiple,1e-6)),"CLIP_MODE_UNSUPPORTED");
        var bentPath = new BarPath([
            new LineSegment3Mm(T1.ToGlobal(-10,0,500),T1.ToGlobal(0,0,500)),
            new LineSegment3Mm(T1.ToGlobal(0,0,500),T1.ToGlobal(0,0,510))],
            false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var bent = new BarOccurrence("bent",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            bentPath,20,20,LengthStatus.Known,"fixture",[]);
        AssertBlock(OpeningClipper.ClipStraightSegment(bent,circle,Policy),"CLIP_PATH_UNSUPPORTED");
        var wrongDepth = RoundExclusion.ForWall("Wall/T1",T1,new Interval1D(50,150),new Point2Mm(0,500),5,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(bar,wrongDepth,Policy),"CLIP_PLANE_MISMATCH");
        var wrongFrame = RoundExclusion.ForWall("Wall/T1",WallFrame.Create(WallId.T2,1000,1250),
            new Interval1D(0,150),new Point2Mm(0,500),5,0,10);
        var t2Line=new LineSegment3Mm(wrongFrame.WallFrame!.ToGlobal(-10,0,500),
            wrongFrame.WallFrame.ToGlobal(10,0,500));
        var mislabeled=new BarOccurrence("mislabeled",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            new BarPath([t2Line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            20,20,LengthStatus.Known,"fixture",[]);
        AssertBlock(OpeningClipper.ClipStraightSegment(mislabeled,wrongFrame,Policy),"CLIP_PLANE_MISMATCH");
    }

    [Fact]
    public void WorldXyPlaneClipsOnlyBarAtOpeningElevation()
    {
        var source=SyntheticTestFactory.Create().Sources[0];
        BarOccurrence BarAt(double z)
        {
            var path=new BarPath([new LineSegment3Mm(new Point3Mm(-10,6,z),new Point3Mm(10,6,z))],
                false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            return new BarOccurrence("xy",null,"Base",ComponentKind.Base,Layer.Bottom,Axis.X,10,
                path,20,20,LengthStatus.Known,"fixture",[source]);
        }
        var exclusion=RoundExclusion.ForWorldXY("Base",-115,new Point2Mm(0,0),0.1,0,9);
        AssertBlock(OpeningClipper.ClipStraightSegment(BarAt(-115),exclusion,Policy),"CLIP_DIMENSION_INVALID");
        exclusion=RoundExclusion.ForWorldXY("Base",-115,new Point2Mm(0,0),0,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(BarAt(-115),exclusion,Policy),"CLIP_DIMENSION_INVALID");
        exclusion=RoundExclusion.ForWorldXY("Base",-115,new Point2Mm(0,0),5,0,10);
        AssertBlock(OpeningClipper.ClipStraightSegment(BarAt(-116),exclusion,Policy),"CLIP_PLANE_MISMATCH");
        var result=OpeningClipper.ClipStraightSegment(BarAt(-115),exclusion,Policy);
        Assert.True(result.Success);
        Assert.Equal(new[]{2d,2d},result.Value!.Retained.Select(b=>b.ScheduleLengthMm));
        Near(16,result.Value.RemovedLengthMm);
    }

    private static BarOccurrence WallBar(string id,double startU,double endU,double z)
    {
        var source=SyntheticTestFactory.Create().Sources[0];
        var path=new BarPath([new LineSegment3Mm(T1.ToGlobal(startU,0,z),T1.ToGlobal(endU,0,z))],
            false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        double length=Math.Abs(endU-startU);
        return new BarOccurrence(id,null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,10,
            path,length,length,LengthStatus.Known,"fixture",[source]);
    }

    private static void AssertBlock(OperationResult<ClipResult> result,string code)
    {
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code==code && d.Severity==DiagnosticSeverity.Block && d.Scope==DiagnosticScope.Global);
    }

    private static void Near(double expected,double actual) => Assert.InRange(Math.Abs(expected-actual),0,1e-6);
}
