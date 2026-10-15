using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class AngleAnchorTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void ClosedPerimeterUsesCeilingCountAndOmitsPointP()
    {
        var seat = Seat(1000, 400, 0, 100, 0);
        var result = AngleFramePlanner.PlanAngles(seat, new EnabledPositionsSet([1]), new SteelAngleEntry("L50", 2.5), Policy);

        Assert.True(result.Success);
        var occurrence = Assert.Single(result.Value!.Occurrences);
        Assert.Equal(2800, occurrence.LengthMm, 9);
        Assert.True(occurrence.Path.Closed);
        Assert.Equal(4, occurrence.Path.Segments.Length);
    }

    [Fact]
    public void EnabledPositionsProduceSeparateAngleOccurrencesAndAnchorsKeepPlanesSeparate()
    {
        var seat = Seat(1000, 400, 0, 100, 0);
        var angles = AngleFramePlanner.PlanAngles(seat, new EnabledPositionsSet([1, 2, 3]), new SteelAngleEntry("L50", 2.5), Policy);
        Assert.True(angles.Success);
        Assert.Equal(3, angles.Value!.Occurrences.Length);
        Assert.Equal(21, angles.Value.Occurrences.Sum(x => x.LengthMm / 1000d * x.UnitMassKgPerM), 9);

        var runs = new[] {
            new AnchorRun("edge", new Point3Mm(0, 0, 0), new Point3Mm(2800, 0, 0), "frame"),
            new AnchorRun("edge-top", new Point3Mm(0, 0, 300), new Point3Mm(2800, 0, 300), "frame") };
        var anchors = AnchorPlanner.PlanAnchors(runs, new AnchorSpec(6, 150, 100), Policy);
        Assert.True(anchors.Success);
        Assert.Equal(38, anchors.Value!.Occurrences.Length);
        Assert.Equal(3800, anchors.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 9);
    }

    [Fact]
    public void AngleMassAndAnchorMassRemainSeparateScheduleFamilies()
    {
        var seat = Seat(1000, 400, 0, 100, 0);
        var angle = AngleFramePlanner.PlanAngles(seat, new EnabledPositionsSet([1]), new SteelAngleEntry("L50", 2.5), Policy).Value!;
        var anchors = AnchorPlanner.PlanAnchors([new AnchorRun("edge", new Point3Mm(0, 0, 0), new Point3Mm(2800, 0, 0), "frame")],
            new AnchorSpec(6, 150, 100), Policy).Value!;
        var schedule = SteelScheduleCalculator.Calculate(anchors.Occurrences, Policy.MassPolicy, new MarkPolicy(CalculationScope.FullRebar));

        Assert.True(schedule.Success);
        Assert.Equal(0.4222222222222222, schedule.Value!.TotalMassKg.Value, 12);
        Assert.Equal(7, angle.TotalMassKg.Value, 12);
    }

    private static NeckSeatProfile Seat(double width, double height, double support, double seatHeight, double top) =>
        new(new Rectangle2Mm(-width / 2, -height / 2, width / 2, height / 2),
            new Rectangle2Mm(-width / 2, -height / 2, width / 2, height / 2), support, top == 0 ? support + seatHeight : top,
            seatHeight, []);
}
