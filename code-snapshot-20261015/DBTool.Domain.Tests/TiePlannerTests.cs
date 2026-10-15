using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class TiePlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void AlignedLatticeUsesInclusiveCoordinatesAndKeepsEightAfterCircularOpening()
    {
        var component = Component(new CircleProfile(new(0, 0), 50));
        var active = ActiveLayers(component);
        var spec = new TieSpec(200, 200, new Point2Mm(-200, -200), TieLatticeMode.Aligned,
            new TieShapeSpec(8, 0), 0, LargeSpacingBehavior.Disable, 0);

        var result = TiePlanner.Plan(component, active, spec, Policy);

        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(x => x.Code)));
        Assert.Equal(8, result.Value!.Occurrences.Length);
        Assert.Equal(8, result.Value.Occurrences.Select(x => ((LineSegment3Mm)x.Path.Segments.Single()).Start).Distinct().Count());
    }

    [Fact]
    public void StaggeredLatticeShiftsTheMiddleRowAndProducesEightPoints()
    {
        var result = TiePlanner.Plan(Component(null), ActiveLayers(Component(null)),
            new TieSpec(200, 200, new Point2Mm(-200, -200), TieLatticeMode.Staggered,
                new TieShapeSpec(8, 0), 0, LargeSpacingBehavior.Disable, 0), Policy);

        Assert.True(result.Success);
        Assert.Equal(8, result.Value!.Occurrences.Length);
        var middle = result.Value.Occurrences.Count(x => Math.Abs(((LineSegment3Mm)x.Path.Segments.Single()).Start.Y) < 1e-6);
        Assert.Equal(2, middle);
    }

    [Fact]
    public void OneActiveLayerReturnsDisabledLayout()
    {
        var component = Component(null);
        var onlyBottom = new RebarLayout(ActiveLayers(component).Occurrences.Where(x => x.Layer == Layer.Bottom), [], CalculationScope.FullRebar);
        var result = TiePlanner.Plan(component, onlyBottom,
            new TieSpec(200, 200, new Point2Mm(-200, -200), TieLatticeMode.Aligned,
                new TieShapeSpec(8, 0), 0, LargeSpacingBehavior.Disable, 0), Policy);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Occurrences);
        Assert.Equal(LayoutStatus.Disabled, result.Value.Status);
        Assert.Contains(result.Diagnostics, x => x.Code == "TIE_ONE_ACTIVE_LAYER");
    }

    [Fact]
    public void LargeSpacingDisableDoesNotForceMinimumFourTies()
    {
        var component = Component(null);
        var result = TiePlanner.Plan(component, ActiveLayers(component),
            new TieSpec(1000, 1000, new Point2Mm(-200, -200), TieLatticeMode.Aligned,
                new TieShapeSpec(8, 0), 0, LargeSpacingBehavior.Disable, 0), Policy);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Occurrences);
        Assert.Equal(LayoutStatus.Disabled, result.Value.Status);
    }

    private static RebarLayout ActiveLayers(ComponentGeometry component) => new([
        Bar(component, Layer.Bottom, 0), Bar(component, Layer.Top, 1)], [], CalculationScope.FullRebar);

    private static BarOccurrence Bar(ComponentGeometry component, Layer layer, int index)
    {
        var path = new BarPath([new LineSegment3Mm(new(-200, -200, layer == Layer.Bottom ? 10 : 90), new(200, 200, layer == Layer.Bottom ? 10 : 90))], false,
            HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        return new BarOccurrence($"main-{index}", null, component.Id, component.Kind, layer, Axis.X, 10, path, path.Segments[0].End.X - path.Segments[0].Start.X, path.Segments[0].End.X - path.Segments[0].Start.X,
            LengthStatus.Known, "TEST", []);
    }

    private static ComponentGeometry Component(CircleProfile? hole)
    {
        var outer = new Polygon2Mm([new(-200, -200), new(200, -200), new(200, 200), new(-200, 200)]);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var openings = hole is null ? Array.Empty<OpeningProfile>() : [new OpeningProfile("hole", "Base", OpeningKind.Round, hole, null, hole, null, frame, new(0, 100), new(0, 100), [])];
        return new ComponentGeometry("Base", ComponentKind.Base, "Concrete", new Footprint(outer, []), new(0, 100),
            new ExtrusionSolid(new Footprint(outer, []), frame, new(0, 100)), openings, [], []);
    }
}
