using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class SupplementaryPlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Theory]
    [InlineData(245, Round50Mode.ExplicitNearestAway, 250)]
    [InlineData(245, Round50Mode.Floor, 200)]
    [InlineData(245, Round50Mode.Ceiling, 250)]
    [InlineData(225, Round50Mode.ExplicitNearestAway, 250)]
    public void DowelLengthUsesExplicitRound50Mode(double input, Round50Mode mode, double expected)
    {
        var result = DowelPlanner.Plan(new DowelSpec(200, 10, input, mode), Component(0, 1000), Policy);
        Assert.True(result.Success);
        Assert.All(result.Value!.Occurrences, x => Assert.Equal(expected, x.ScheduleLengthMm, 9));
    }

    [Fact]
    public void DowelQuantizationWithoutModeNeedsRule()
    {
        var result = DowelPlanner.Plan(new DowelSpec(200, 10, 245), Component(0, 1000), Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, x => x.Code == "NEEDS_RULE_DOWEL");
    }

    [Fact]
    public void LadderUsesActualCoordinatesAndLeavesFabricationUnresolved()
    {
        var result = LadderPlanner.Plan(new LadderSpec(200, 300, 300, 300, 150, 100), Component(0, 1750), Policy);
        Assert.True(result.Success);
        Assert.Equal(new[] { 1550d, 1250d, 950d, 650d, 350d }, result.Value!.Occurrences.Select(x => ((LineSegment3Mm)x.Path.Segments[0]).Start.Z));
        Assert.All(result.Value.Occurrences, x => { Assert.Equal(800, x.ScheduleLengthMm, 9); Assert.Null(x.FabricationLengthMm); });
        Assert.Contains(result.Diagnostics, x => x.Code == "LADDER_FABRICATION_NEEDS_RULE");
    }

    [Fact]
    public void LiftingHooksAllowOnlyZeroTwoOrFourAndUseInputSpacing()
    {
        var component = Component(0, 1000);
        Assert.Empty(LiftingHookPlanner.Plan(new LiftingHookSpec(0, 10, 400), component, Policy).Value!.Occurrences);
        var two = LiftingHookPlanner.Plan(new LiftingHookSpec(2, 10, 400, 300), component, Policy);
        Assert.True(two.Success);
        Assert.Equal(2, two.Value!.Occurrences.Length);
        Assert.Equal(800, two.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 9);
        var three = LiftingHookPlanner.Plan(new LiftingHookSpec(3, 10, 400, 300), component, Policy);
        Assert.False(three.Success);
        Assert.Null(three.Value);
    }

    private static ComponentGeometry Component(double z0, double z1)
    {
        var outer = new Polygon2Mm([new(-500, -500), new(500, -500), new(500, 500), new(-500, 500)]);
        var footprint = new Footprint(outer, []);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        return new ComponentGeometry("Panel", ComponentKind.Panel, "Concrete", footprint, new(z0, z1),
            new ExtrusionSolid(footprint, frame, new(z0, z1)), [], [], []);
    }
}
