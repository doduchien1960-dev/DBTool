using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class ComponentRebarProjectionTests
{
    [Fact]
    public void RoofPerimeterKeepsAnalyticCircleAndCanonicalMark()
    {
        var arc = new ArcSegment3Mm(new(10, 20, 1500), 100, new(0, 0, 1), new(1, 0, 0), 2 * Math.PI);
        var length = 200 * Math.PI;
        var bar = new BarOccurrence("ring", null, "Roof", ComponentKind.Roof, Layer.Top, Axis.X, 10,
            new BarPath([arc], true, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            length, length, LengthStatus.Known, "TEST", [], frameId: "Roof/Top", surface: RebarSurface.Outer);
        var result = ComponentRebarProjector.Build([bar], new Dictionary<string,string> { [bar.Id]="N1" },
            "RoofTop", "Roof", bar.FrameId, bar.Surface, bar.Layer);
        Assert.True(result.Success);
        var path = Assert.IsType<PathPrimitive>(Assert.Single(result.Value!));
        Assert.Equal("N1", path.ScheduleMark);
        var projected = Assert.IsType<ArcPathSegment>(Assert.Single(path.Segments));
        Assert.Equal(new Point2Mm(10,20), projected.Center);
        Assert.Equal(100, projected.RadiusMm);
        Assert.Equal(2*Math.PI, projected.SweepRadians);
    }
    [Fact]
    public void SelectionIsolatesComponentFrameAndSurfaceAndPreservesVerticalLeg()
    {
        var selected = Bar("selected", "Wall/T3", "Wall/T3", RebarSurface.Inner);
        var bars = new[] { selected, Bar("outer", "Wall/T3", "Wall/T3", RebarSurface.Outer),
            Bar("other", "Wall/T4", "Wall/T4", RebarSurface.Inner) };
        var result = ComponentRebarProjector.Build(bars, bars.ToDictionary(b => b.Id, b => "D001"),
            "T3Inner", "Wall/T3", "Wall/T3", RebarSurface.Inner, Layer.Bottom);
        Assert.True(result.Success);
        var path = Assert.IsType<PolylinePrimitive>(Assert.Single(result.Value!));
        Assert.Equal(selected.Id, path.OccurrenceId);
        Assert.Equal(new Point2Mm(25, 100), path.Points[0]);
        Assert.Equal(new Point2Mm(25, 1100), path.Points[1]);
    }

    [Fact]
    public void FaceProjectionShowsEndOnLegAsDiameterDot()
    {
        var bar = new BarOccurrence("end", null, "Wall/T4", ComponentKind.Wall, Layer.Bottom, Axis.Y, 12,
            new BarPath([new LineSegment3Mm(new(20, 25, 100), new(80, 25, 100))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath), 60, 60, LengthStatus.Known,
            "TEST", [], frameId: "Wall/T4", surface: RebarSurface.Inner);
        var result = ComponentRebarProjector.Build([bar], new Dictionary<string, string> { [bar.Id] = "D001" },
            "T4Inner", bar.ComponentId, bar.FrameId, bar.Surface, bar.Layer);
        Assert.True(result.Success);
        Assert.Equal(6, Assert.IsType<CirclePrimitive>(Assert.Single(result.Value!)).RadiusMm);
    }

    private static BarOccurrence Bar(string id, string owner, string frame, RebarSurface surface) =>
        new(id, null, owner, ComponentKind.Wall, Layer.Bottom, Axis.X, 10,
            new BarPath([new LineSegment3Mm(new(25, 30, 100), new(25, 30, 1100))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath), 1000, 1000, LengthStatus.Known,
            "TEST", [], RebarDirection.Vertical, frame, surface);
}
