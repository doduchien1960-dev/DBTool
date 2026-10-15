using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class PanelCatchRebarTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void TwoPanelsProduceSevenPlusFourBarsPerBottomLayer()
    {
        var panels = new PanelLayout([
            Panel(417.5, 840, 0), Panel(417.5, 840, 430)], 0);
        var result = PanelRebarPlanner.PlanPanels(panels, new PanelRebarSpec(8, 150, 30, false, DistributionMode.Equalized), Policy);

        Assert.True(result.Success);
        Assert.Equal(22, result.Value!.Occurrences.Length);
        Assert.Equal(11245, result.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);
        Assert.Equal(357.5, result.Value.Occurrences.Where(x => x.Axis == Axis.X).First().ScheduleLengthMm, 6);
        Assert.Equal(780, result.Value.Occurrences.Where(x => x.Axis == Axis.Y).First().ScheduleLengthMm, 6);
    }

    [Fact]
    public void DisabledTopDoesNotCreateD0BarsAndEnabledTopChecksLayerClash()
    {
        var panels = new PanelLayout([Panel(400, 600, 0)], 0);
        var one = PanelRebarPlanner.PlanPanels(panels, new PanelRebarSpec(8, 150, 25, false, DistributionMode.Equalized), Policy);
        Assert.True(one.Success);
        Assert.Equal(9, one.Value!.Occurrences.Length);
        Assert.All(one.Value.Occurrences, x => Assert.True(x.DiameterMm > 0));

        var clash = PanelRebarPlanner.PlanPanels(panels, new PanelRebarSpec(10, 150, 250, true, DistributionMode.Equalized, 10), Policy);
        Assert.False(clash.Success);
        Assert.Null(clash.Value);
        Assert.Contains(clash.Diagnostics, x => x.Code == "PANEL_LAYER_COLLISION");
    }

    [Fact]
    public void CatchFloorUsesSeparateXAndYFamiliesAndStepChangeIsMeasured()
    {
        var geometry = new CatchBasinGeometry(new UnionSolid([Solid(800, 800)]), [], [], [], new(0, 100), 0, 0, 0, 0);
        var spec = new CatchRebarSpec(400, 600, 8, 150, 25);
        var result = CatchBasinRebarPlanner.PlanCatch(geometry, spec, Policy);
        Assert.True(result.Success);
        Assert.Equal(5, result.Value!.Occurrences.Count(x => x.FamilyId == "CatchFloorX"));
        Assert.Equal(4, result.Value.Occurrences.Count(x => x.FamilyId == "CatchFloorY"));
        Assert.Equal(3950, result.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);

        var widerStep = CatchBasinRebarPlanner.PlanCatch(geometry, new CatchRebarSpec(400, 600, 8, 300, 25), Policy);
        Assert.True(widerStep.Success);
        Assert.Equal(3, widerStep.Value!.Occurrences.Count(x => x.FamilyId == "CatchFloorX"));
        Assert.Equal(3, widerStep.Value.Occurrences.Count(x => x.FamilyId == "CatchFloorY"));
        Assert.Equal(2700, widerStep.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);
    }

    private static CoverPanel Panel(double width, double height, double x) =>
        new($"Panel/{x}", new Rectangle2Mm(x, 0, x + width, height),
            new Frame3Mm(new(x + width / 2, height / 2, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), new(0, 100), "support", []);

    private static ExtrusionSolid Solid(double width, double depth)
    {
        var outer = new Polygon2Mm([new(0, 0), new(width, 0), new(width, depth), new(0, depth)]);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        return new ExtrusionSolid(new Footprint(outer, []), frame, new(0, 100));
    }
}
