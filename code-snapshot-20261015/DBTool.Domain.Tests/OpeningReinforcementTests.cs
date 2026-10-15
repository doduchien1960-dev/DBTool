using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class OpeningReinforcementTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void RoundBoUsesOneClosedLoopPerActiveLayerAndSingleLap()
    {
        var (opening, component) = RoundFixture(305);
        var spec = new OpeningReinforcementSpec(OpeningReinforcementShape.RoundBo, 2, [Layer.Bottom, Layer.Top],
            10, 0, 30, null, EndAnchoragePolicy.Explicit, 0);

        var result = OpeningReinforcementPlanner.Plan(opening, component, spec, ActiveLayers(), Policy);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences, x => {
            Assert.True(x.Path.Closed);
            Assert.Equal(2436.2830044410593, x.ScheduleLengthMm, 9);
        });
    }

    [Fact]
    public void Diagonal45RequiresSpacingWhenCountIsGreaterThanOne()
    {
        var (opening, component) = RoundFixture(305);
        var spec = new OpeningReinforcementSpec(OpeningReinforcementShape.Diagonal45, 2, [Layer.Bottom, Layer.Top],
            10, 100, 0, 50, EndAnchoragePolicy.Explicit);

        var result = OpeningReinforcementPlanner.Plan(opening, component, spec, ActiveLayers(), Policy);

        Assert.True(result.Success);
        Assert.Equal(16, result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences, x => Assert.Equal(880, x.ScheduleLengthMm, 6));
    }

    [Fact]
    public void SeparateSidesAreFourIndependentBarsPerSetAndLayer()
    {
        var (opening, component) = BoxFixture(600, 400);
        var spec = new OpeningReinforcementSpec(OpeningReinforcementShape.SeparateSides, 2, [Layer.Bottom, Layer.Top],
            10, 100, 0, 50, EndAnchoragePolicy.Explicit);

        var result = OpeningReinforcementPlanner.Plan(opening, component, spec, ActiveLayers(), Policy);

        Assert.True(result.Success);
        Assert.Equal(16, result.Value!.Occurrences.Length);
        Assert.Equal(12320, result.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);
    }

    private static IReadOnlyList<ActiveLayerFrame> ActiveLayers() => [
        new ActiveLayerFrame(Layer.Bottom, "bottom", new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))),
        new ActiveLayerFrame(Layer.Top, "top", new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)))];

    private static (OpeningProfile Opening, ComponentGeometry Component) RoundFixture(double radius)
    {
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var circle = new CircleProfile(new(0, 0), radius);
        var opening = new OpeningProfile("O", "Wall/T1", OpeningKind.Round, circle, null, circle, null,
            frame, new Interval1D(0, 100), new Interval1D(0, 150), []);
        return (opening, Component(-1000, -1000, 1000, 1000));
    }

    private static (OpeningProfile Opening, ComponentGeometry Component) BoxFixture(double width, double height)
    {
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var rect = new Rectangle2Mm(-width / 2, -height / 2, width / 2, height / 2);
        var profile = new RectangleProfile(rect);
        var opening = new OpeningProfile("O", "Wall/T1", OpeningKind.Box, profile, null, profile, null,
            frame, new Interval1D(0, 100), new Interval1D(0, 150), []);
        return (opening, Component(-1000, -1000, 1000, 1000));
    }

    private static ComponentGeometry Component(double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2Mm([new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)]);
        var footprint = new Footprint(polygon, []);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var range = new Interval1D(0, 100);
        return new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", footprint, range,
            new ExtrusionSolid(footprint, frame, range), [], [], []);
    }
}
