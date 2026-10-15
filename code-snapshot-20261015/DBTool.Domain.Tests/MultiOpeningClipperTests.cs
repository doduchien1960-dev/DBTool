using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class MultiOpeningClipperTests
{
    [Fact]
    public void MultiSegmentBarRejoinsKeptPiecesAcrossOriginalJoint()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var a=frame.ToGlobal(0,0,500);
        var b=frame.ToGlobal(500,0,500);
        var c=frame.ToGlobal(500,0,1000);
        var bar=new BarOccurrence("P",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([new LineSegment3Mm(a,b),new LineSegment3Mm(b,c)],false,
                HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            1000,1000,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(250,500),100);
        var hole=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[hole],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(210,result.Value!.RemovedLengthMm,6);
        Assert.Equal(new[]{"P/clip/0","P/clip/1"},result.Value.Retained.Select(x=>x.Id));
        Assert.Equal(145,result.Value.Retained[0].ScheduleLengthMm,6);
        Assert.Equal(645,result.Value.Retained[1].ScheduleLengthMm,6);
        Assert.Equal(2,result.Value.Retained[1].Path.Segments.Length);
    }

    [Fact]
    public void ArcAndLinePathConservesTrueArcLengthAcrossJoint()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var arc=new ArcSegment3Mm(frame.ToGlobal(0,0,500),100,
            frame.Outward,frame.Left,Math.PI/2);
        var line=new LineSegment3Mm(arc.End,frame.ToGlobal(0,0,800));
        double original=50*Math.PI+200;
        var bar=new BarOccurrence("ArcLine",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([arc,line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            original,original,LengthStatus.Known,"fixture",[]);
        var polygon=new OpenTopProfile(new Polygon2Mm([new(55,400),new(200,400),
            new(200,700),new(55,700)]));
        var opening=new OpeningProfile("Half","Wall/T1",OpeningKind.OpenTop,
            polygon,polygon,polygon,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,700),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(104.71975511965977,result.Value!.RemovedLengthMm,6);
        var child=Assert.Single(result.Value.Retained);
        Assert.Equal("ArcLine/clip/0",child.Id);
        Assert.Equal(2,child.Path.Segments.Length);
        Assert.Equal(52.35987755982989+200,child.ScheduleLengthMm,6);
    }

    [Fact]
    public void EndHookStaysOnlyWithChildContainingOriginalEnd()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var main=new LineSegment3Mm(frame.ToGlobal(0,0,500),frame.ToGlobal(900,0,500));
        var bend=new ArcSegment3Mm(frame.ToGlobal(900,0,600),100,
            frame.Outward,new Vector3(0,0,-1),Math.PI/2);
        var hook=new LineSegment3Mm(bend.End,frame.ToGlobal(1000,0,700));
        double original=1000+50*Math.PI;
        var bar=new BarOccurrence("Hooked",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([main,bend,hook],false,HookPattern.Hook1M,0,false,
                MeasurementBasis.TangentPath),original,original,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(500,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(210,result.Value!.RemovedLengthMm,6);
        Assert.Equal(new[]{HookPattern.Hook0M,HookPattern.Hook1M},
            result.Value.Retained.Select(c=>c.Path.HookPattern));
        Assert.Single(result.Value.Retained[0].Path.Segments);
        Assert.Equal(3,result.Value.Retained[1].Path.Segments.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingOneOriginalHookKeepsOnlyTheOtherHook(bool cutStart)
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var startLine=new LineSegment3Mm(frame.ToGlobal(0,0,700),frame.ToGlobal(0,0,600));
        var startArc=new ArcSegment3Mm(frame.ToGlobal(100,0,600),100,frame.Outward,
            new Vector3(-frame.Left.X,-frame.Left.Y,-frame.Left.Z),Math.PI/2);
        var main=new LineSegment3Mm(startArc.End,frame.ToGlobal(900,0,500));
        var endArc=new ArcSegment3Mm(frame.ToGlobal(900,0,600),100,frame.Outward,
            new Vector3(0,0,-1),Math.PI/2);
        var endLine=new LineSegment3Mm(endArc.End,frame.ToGlobal(1000,0,700));
        double original=1000+100*Math.PI;
        var bar=new BarOccurrence("TwoHooks",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([startLine,startArc,main,endArc,endLine],false,HookPattern.Hook2M,
                0,false,MeasurementBasis.TangentPath),original,original,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(cutStart?0:1000,600),250);
        var opening=new OpeningProfile("EndCut","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(350,850),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        var child=Assert.Single(result.Value!.Retained);
        Assert.Equal(cutStart?HookPattern.Hook1M:HookPattern.HookStartOnly,
            child.Path.HookPattern);
        if(cutStart)
        {
            Assert.NotEqual(bar.Path.Segments[0].Start,child.Path.Segments[0].Start);
            Assert.Equal(bar.Path.Segments[^1].End,child.Path.Segments[^1].End);
        }
        else
        {
            Assert.Equal(bar.Path.Segments[0].Start,child.Path.Segments[0].Start);
            Assert.NotEqual(bar.Path.Segments[^1].End,child.Path.Segments[^1].End);
        }
        Assert.Equal(original,result.Value.RemovedLengthMm+child.ScheduleLengthMm,6);
    }

    [Fact]
    public void CuttingThroughHookBendBlocksInsteadOfRelabelingTruncatedHook()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var main=new LineSegment3Mm(frame.ToGlobal(0,0,500),frame.ToGlobal(900,0,500));
        var bend=new ArcSegment3Mm(frame.ToGlobal(900,0,600),100,
            frame.Outward,new Vector3(0,0,-1),Math.PI/2);
        var hook=new LineSegment3Mm(bend.End,frame.ToGlobal(1000,0,700));
        double original=1000+50*Math.PI;
        var bar=new BarOccurrence("Bend",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([main,bend,hook],false,HookPattern.Hook1M,0,false,
                MeasurementBasis.TangentPath),original,original,LengthStatus.Known,"fixture",[]);
        double mid=100/Math.Sqrt(2);
        var circle=new CircleProfile(new Point2Mm(900+mid,600-mid),20);
        var opening=new OpeningProfile("BendCut","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(500,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CLIP_HOOK_PARTIAL_UNSUPPORTED" &&
            d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void OpenTopCatchCutsBarEndWithoutShorteningWallGeometry()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(0,0,0),frame.ToGlobal(0,0,1400));
        var bar=new BarOccurrence("Vertical",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.Y,
            10,new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            1400,1400,LengthStatus.Known,"fixture",[]);
        var profile=new OpenTopProfile(new Polygon2Mm([new(-200,1100),new(200,1100),
            new(200,1400),new(-200,1400)]));
        var opening=new OpeningProfile("Catch","Wall/T1",OpeningKind.OpenTop,
            profile,profile,profile,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(1100,1400),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(305,result.Value!.RemovedLengthMm,6);
        var child=Assert.Single(result.Value.Retained);
        Assert.Equal(1095,child.ScheduleLengthMm,6);
        Assert.Equal(1400,model.Components.Single(c=>c.Id=="Wall/T1").ZRange.End);
    }

    [Fact]
    public void BarCrossingOpeningDepthDoesNotReturnFalseUnchangedSuccess()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-50,-20,500),frame.ToGlobal(50,170,500));
        double length=Math.Sqrt(100*100+190*190);
        var bar=new BarOccurrence("Depth",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,
            10,new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var concave=new PolygonProfile(new Polygon2Mm([
            new(-100,400),new(100,400),new(100,450),new(0,450),
            new(0,550),new(100,550),new(100,600),new(-100,600)]));
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Polygon,
            concave,concave,concave,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CLIP_PLANE_UNSUPPORTED" &&
            d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void ConstantFacePositionDepthBarUsesExactRoundVolumeAndCapClearance()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(0,-20,500),frame.ToGlobal(0,170,500));
        var bar=new BarOccurrence("DepthExact",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            190,190,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(160,result.Value!.RemovedLengthMm,6);
        Assert.Equal(190,result.Value.OriginalLengthMm,6);
        Assert.Equal(2,result.Value.Retained.Length);
        Assert.Equal(15,result.Value.Retained[0].ScheduleLengthMm,6);
        Assert.Equal(15,result.Value.Retained[1].ScheduleLengthMm,6);
        Assert.Equal(-5,frame.ToLocal(result.Value.Retained[0].Path.Segments[0].End).V,6);
        Assert.Equal(155,frame.ToLocal(result.Value.Retained[1].Path.Segments[0].Start).V,6);
    }

    [Fact]
    public void FacePlaneJustOutsideRoundCapUsesEuclideanClearance()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-150,-3,500),
            frame.ToGlobal(150,-3,500));
        var bar=new BarOccurrence("CapNear",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            300,300,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.Equal(208,result.Value!.RemovedLengthMm,6);
        Assert.Equal(92,result.Value.Retained.Sum(c=>c.ScheduleLengthMm),6);
    }

    [Fact]
    public void CoupledFaceAndDepthLineClipsRoundCylinderAtAnalyticFaceRoots()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-200,-50,500),
            frame.ToGlobal(200,200,500));
        double length=Math.Sqrt(400*400+250*250);
        var bar=new BarOccurrence("Coupled",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.InRange(Math.Abs(result.Value!.RemovedLengthMm-length*0.525),0,1e-6);
        Assert.Equal(2,result.Value.Retained.Length);
        Assert.InRange(Math.Abs(frame.ToLocal(result.Value.Retained[0].Path.Segments[0].End).U+105),0,1e-6);
        Assert.InRange(Math.Abs(frame.ToLocal(result.Value.Retained[1].Path.Segments[0].Start).U-105),0,1e-6);
    }

    [Fact]
    public void CoupledFaceAndDepthLineClipsBoxPrismAtAnalyticFaceRoots()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-200,-50,500),
            frame.ToGlobal(200,200,500));
        double length=Math.Sqrt(400*400+250*250);
        var bar=new BarOccurrence("CoupledBox",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var box=new RectangleProfile(new Rectangle2Mm(-100,400,100,600));
        var opening=new OpeningProfile("Box","Wall/T1",OpeningKind.Box,box,box,box,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.InRange(Math.Abs(result.Value!.RemovedLengthMm-length*0.525),0,1e-6);
        Assert.Equal(2,result.Value.Retained.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoupledDepthLineClipsConvexPolygonOrOpenTop(bool openTop)
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-200,-50,500),
            frame.ToGlobal(200,200,500));
        double length=Math.Sqrt(400*400+250*250);
        var bar=new BarOccurrence("Convex",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var polygon=new Polygon2Mm([new(-100,500),new(0,400),
            new(100,500),new(0,600)]);
        Profile2Mm profile=openTop ? new OpenTopProfile(polygon) : new PolygonProfile(polygon);
        var opening=new OpeningProfile("Diamond","Wall/T1",
            openTop ? OpeningKind.OpenTop : OpeningKind.Polygon,
            profile,openTop ? null : profile,profile,frame,
            new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.InRange(Math.Abs(result.Value!.RemovedLengthMm-length*0.525),0,1e-6);
    }

    [Fact]
    public void CoupledRoundLineClipsAtExpandedDepthCapsWhileInsideFace()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(-50,-20,500),
            frame.ToGlobal(50,170,500));
        double length=Math.Sqrt(100*100+190*190);
        var bar=new BarOccurrence("CoupledCaps",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.InRange(Math.Abs(result.Value!.RemovedLengthMm-length*160/190),0,1e-6);
        Assert.Equal(-5,frame.ToLocal(result.Value.Retained[0].Path.Segments[0].End).V,6);
        Assert.Equal(155,frame.ToLocal(result.Value.Retained[1].Path.Segments[0].Start).V,6);
    }

    [Fact]
    public void CoupledRoundLineUsesEuclideanRoundedCapCorner()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var line=new LineSegment3Mm(frame.ToGlobal(113,-14,500),
            frame.ToGlobal(93,6,500));
        double length=Math.Sqrt(20*20+20*20);
        var bar=new BarOccurrence("Corner",null,"Wall/T1",ComponentKind.Wall,
            Layer.Bottom,Axis.X,10,
            new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            length,length,LengthStatus.Known,"fixture",[]);
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Hole","Wall/T1",OpeningKind.Round,circle,circle,circle,
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var result=OpeningClipper.Clip(bar,[opening],new ClearancePolicy(0),model);
        Assert.True(result.Success);
        Assert.InRange(Math.Abs(result.Value!.RemovedLengthMm-length/2),0,1e-6);
        var kept=Assert.Single(result.Value.Retained);
        Assert.InRange(Math.Abs(frame.ToLocal(kept.Path.Segments[0].End).U-103),0,1e-6);
        Assert.InRange(Math.Abs(frame.ToLocal(kept.Path.Segments[0].End).V+4),0,1e-6);
    }

    [Fact]
    public void BoxCornerClearanceUsesRoundedOffsetRatherThanSquareExpansion()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var line = new LineSegment3Mm(frame.ToGlobal(0, 0, 446), frame.ToGlobal(600, 0, 446));
        var bar = new BarOccurrence("Corner", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X,
            10, new BarPath([line], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            600, 600, LengthStatus.Known, "fixture", []);
        var rectangle = new RectangleProfile(new Rectangle2Mm(200, 450, 400, 550));
        var opening = new OpeningProfile("Box", "Wall/T1", OpeningKind.Box, rectangle, rectangle, rectangle,
            frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(450, 550), new Interval1D(0, 150), []);
        var model = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var result = OpeningClipper.Clip(bar, [opening], new ClearancePolicy(0), model);
        Assert.True(result.Success);
        Assert.Equal(206, result.Value!.RemovedLengthMm, 6);
        Assert.Equal(394, result.Value.Retained.Sum(x => x.ScheduleLengthMm), 6);
    }

    [Fact]
    public void OverlappingAndDuplicateRoundCutsAreUnionedBeforeSubtraction()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var path = new BarPath([new LineSegment3Mm(frame.ToGlobal(0, 0, 500), frame.ToGlobal(1000, 0, 500))],
            false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar = new BarOccurrence("P", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X,
            10, path, 1000, 1000, LengthStatus.Known, "fixture", []);
        OpeningProfile Hole(string id, double u, double radius)
        {
            var circle = new CircleProfile(new Point2Mm(u, 500), radius);
            return new OpeningProfile(id, "Wall/T1", OpeningKind.Round, circle, circle, circle, frame,
                new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
                new Interval1D(500-radius, 500+radius), new Interval1D(0, 150), []);
        }
        var a = Hole("A", 350, 145);
        var b = Hole("B", 600, 195);
        var seed = SyntheticTestFactory.Create();
        var policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;
        var model = ChamberGeometryBuilder.Build(seed, policy).Value!;
        var result = OpeningClipper.Clip(bar, [a, b, a], new ClearancePolicy(0), model);
        Assert.True(result.Success);
        Assert.Equal(new[] { "P/clip/0", "P/clip/1" }, result.Value!.Retained.Select(x => x.Id));
        Assert.All(result.Value.Retained, x => Assert.InRange(Math.Abs(x.ScheduleLengthMm - 200), 0, 1e-6));
        Assert.Equal(600, result.Value.RemovedLengthMm, 6);
        var reordered = OpeningClipper.Clip(bar, [b, a], new ClearancePolicy(0), model);
        Assert.True(reordered.Success);
        Assert.Equal(result.Value.Retained.Select(x => x.Id), reordered.Value!.Retained.Select(x => x.Id));
        Assert.Equal(result.Value.RemovedLengthMm, reordered.Value.RemovedLengthMm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuarterArcIsClippedAtAnalyticAngleWithoutLineApproximation(bool extraCollinearVertex)
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var arc = new ArcSegment3Mm(frame.ToGlobal(0, 0, 500), 100,
            frame.Outward, frame.Left, Math.PI / 2);
        double original = 50 * Math.PI;
        var bar = new BarOccurrence("Arc", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X,
            10, new BarPath([arc], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            original, original, LengthStatus.Known, "fixture", []);
        var rectangle = extraCollinearVertex
            ? new Polygon2Mm([new(55, 400), new(200, 400), new(200, 700), new(55, 700), new(55, 650)])
            : new Polygon2Mm([new(55, 400), new(200, 400), new(200, 700), new(55, 700)]);
        var opening = new OpeningProfile("OpenTop", "Wall/T1", OpeningKind.OpenTop,
            new OpenTopProfile(rectangle), new OpenTopProfile(rectangle), new OpenTopProfile(rectangle),
            frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(400, 700), new Interval1D(0, 150), []);
        var seed = SyntheticTestFactory.Create();
        var model = ChamberGeometryBuilder.Build(seed, CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var result = OpeningClipper.Clip(bar, [opening], new ClearancePolicy(0), model);
        Assert.True(result.Success);
        Assert.Equal(104.71975511965977, result.Value!.RemovedLengthMm, 6);
        var kept = Assert.Single(result.Value.Retained);
        Assert.IsType<ArcSegment3Mm>(Assert.Single(kept.Path.Segments));
        Assert.Equal(52.35987755982989, kept.ScheduleLengthMm, 6);
    }

    [Fact]
    public void AdjacentForbiddenBoxesFormOneUnionAndFullCoverageReturnsNoZeroLengthBar()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var line = new LineSegment3Mm(frame.ToGlobal(0, 0, 500), frame.ToGlobal(1000, 0, 500));
        var bar = new BarOccurrence("P", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X,
            10, new BarPath([line], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            1000, 1000, LengthStatus.Known, "fixture", []);
        OpeningProfile Box(string id, double u0, double u1)
        {
            var rectangle = new RectangleProfile(new Rectangle2Mm(u0, 450, u1, 550));
            return new OpeningProfile(id, "Wall/T1", OpeningKind.Box, rectangle, rectangle, rectangle,
                frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
                new Interval1D(450, 550), new Interval1D(0, 150), []);
        }
        var model = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var adjacent = OpeningClipper.Clip(bar, [Box("A",205,395), Box("B",405,595)],
            new ClearancePolicy(0), model);
        Assert.True(adjacent.Success);
        Assert.Equal(400, adjacent.Value!.RemovedLengthMm, 6);
        Assert.Equal(2, adjacent.Value.Retained.Length);
        var full = OpeningClipper.Clip(bar, [Box("All",-5,1005)], new ClearancePolicy(0), model);
        Assert.True(full.Success);
        Assert.Empty(full.Value!.Retained);
        Assert.Equal(1000, full.Value.RemovedLengthMm, 6);
    }

    [Fact]
    public void PolygonHoleClipsStraightBarWithCornerClearance()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var bar = new BarOccurrence("P", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X,
            10, new BarPath([new LineSegment3Mm(frame.ToGlobal(0, 0, 500), frame.ToGlobal(600, 0, 500))],
                false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            600, 600, LengthStatus.Known, "fixture", []);
        var profile = new PolygonProfile(new Polygon2Mm([new(200, 400), new(400, 500), new(200, 600)]));
        var opening = new OpeningProfile("Polygon", "Wall/T1", OpeningKind.Polygon, profile, profile,
            profile, frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(400, 600), new Interval1D(0, 150), []);
        var model = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),
            CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var result = OpeningClipper.Clip(bar, [opening], new ClearancePolicy(0), model);
        Assert.True(result.Success);
        Assert.Equal(210, result.Value!.RemovedLengthMm, 6);
        Assert.Equal(390, result.Value.Retained.Sum(x => x.ScheduleLengthMm), 6);
    }
}
