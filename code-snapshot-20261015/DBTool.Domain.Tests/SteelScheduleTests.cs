using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using System.Text.Json;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class SteelScheduleTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;
    private static readonly MarkPolicy Marks = new(CalculationScope.BaseRebarOnly);

    [Fact]
    public void S1GroupsIntoFourStableRowsAndPreservesRawMass()
    {
        var bars = S1Bars();
        var schedule = SteelScheduleCalculator.Calculate(bars, Policy.MassPolicy, Marks).Value!;
        Assert.Equal(4, schedule.Rows.Length);
        Assert.Equal(new[] { "D001", "D002", "D003", "D004" }, schedule.Rows.Select(r => r.Mark));
        Assert.Equal(new[] { 13, 11, 13, 11 }, schedule.Rows.Select(r => r.Quantity));
        Assert.Equal(new[] { Layer.Bottom, Layer.Bottom, Layer.Top, Layer.Top }, schedule.Rows.Select(r => r.Layer));
        Assert.Equal(new[] { Axis.X, Axis.Y, Axis.X, Axis.Y }, schedule.Rows.Select(r => r.Axis));
        Assert.Equal(48, schedule.Rows.Sum(r => r.Quantity));
        Assert.Equal(74620, schedule.Rows.Sum(r => r.TotalLengthMm), 6);
        Assert.InRange(Math.Abs(schedule.TotalMassKg.Value - 46.06172839506173), 0, 1e-9);
        Assert.Equal(46.062, Math.Round(schedule.TotalMassKg.Value, 3, MidpointRounding.AwayFromZero), 3);
        Assert.All(schedule.Rows, r => Assert.Equal(100d / 162, r.UnitMassKgPerM, 12));
        Assert.All(schedule.Rows.Where(r => r.Axis == Axis.X), r => {
            Assert.Equal(18720, r.TotalLengthMm, 6);
            Assert.InRange(Math.Abs(r.MassKg.Value - 11.55555555555556), 0, 1e-9);
        });
        Assert.All(schedule.Rows.Where(r => r.Axis == Axis.Y), r => {
            Assert.Equal(18590, r.TotalLengthMm, 6);
            Assert.InRange(Math.Abs(r.MassKg.Value - 11.47530864197531), 0, 1e-9);
        });
        Assert.Equal(48, schedule.Rows.SelectMany(r => r.OccurrenceIds).Distinct().Count());
        Assert.Equal(schedule.TotalMassKg, schedule.SummaryByComponent[ComponentKind.Base]);
    }

    [Fact]
    public void ShufflingOccurrencesDoesNotChangeMarksOrMembership()
    {
        var bars = S1Bars();
        var a = SteelScheduleCalculator.Calculate(bars, Policy.MassPolicy, Marks).Value!;
        var b = SteelScheduleCalculator.Calculate(bars.Reverse().ToArray(), Policy.MassPolicy, Marks).Value!;
        Assert.Equal(a.Rows.Select(r => (r.Mark,r.ComponentKind,r.Layer,r.Axis,r.Quantity)),
            b.Rows.Select(r => (r.Mark,r.ComponentKind,r.Layer,r.Axis,r.Quantity)));
        for (int i = 0; i < a.Rows.Length; i++)
            Assert.Equal(a.Rows[i].OccurrenceIds.ToArray(), b.Rows[i].OccurrenceIds.ToArray());
    }

    [Fact]
    public void DuplicateOccurrenceIdReturnsBlockAndNull()
    {
        var bar = S1Bars()[0];
        var result = SteelScheduleCalculator.Calculate(new[] { bar, bar }, Policy.MassPolicy, Marks);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "SCHEDULE_DUPLICATE_OCCURRENCE" &&
            d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global);
    }

    [Fact]
    public void RawLengthsAndComponentKindRemainDistinctEvenWhenDisplayLabelsAgree()
    {
        var a = Straight("A", "Bản trần", ComponentKind.Base, 1440, LengthStatus.Known);
        var b = Straight("B", "Bản trần", ComponentKind.Base, 1440.01, LengthStatus.Known);
        var c = Straight("C", "Bản trần", ComponentKind.Roof, 1440, LengthStatus.NeedsRule);
        var schedule = SteelScheduleCalculator.Calculate(new[] { a, b, c }, Policy.MassPolicy, Marks).Value!;
        Assert.Equal(3, schedule.Rows.Length);
        Assert.Equal(2, schedule.SummaryByComponent.Count);
        Assert.Contains(schedule.Rows, r => r.ComponentKind == ComponentKind.Roof && r.OccurrenceIds.Single() == "C");
    }

    [Fact]
    public void MassPolicyIsVersionedAndPhysical7850RemainsDifferent()
    {
        var bars = S1Bars();
        var physical = new MassPolicy(MassPolicyId.Density7850, "physical7850/v1", 7850);
        var a = SteelScheduleCalculator.Calculate(bars, Policy.MassPolicy, Marks).Value!;
        var b = SteelScheduleCalculator.Calculate(bars, physical, Marks).Value!;
        Assert.Equal("D2Over162", a.MassPolicyId);
        Assert.Equal("Density7850", b.MassPolicyId);
        Assert.Equal("physical7850/v1", b.MassPolicyVersion);
        Assert.InRange(Math.Abs(b.Rows[0].UnitMassKgPerM - 0.6165375582669969), 0, 1e-12);
        Assert.NotEqual(a.TotalMassKg, b.TotalMassKg);
    }

    [Fact]
    public void GrandTotalRoundsAfterRawSummation()
    {
        var first = Straight("small-base", "Base", ComponentKind.Base, 0.648, LengthStatus.NeedsRule);
        var second = Straight("small-roof", "Roof", ComponentKind.Roof, 0.648, LengthStatus.NeedsRule);
        var schedule = SteelScheduleCalculator.Calculate(new[] { first, second }, Policy.MassPolicy, Marks).Value!;
        Assert.Equal(2, schedule.Rows.Length);
        Assert.All(schedule.Rows, r => Assert.Equal(0, Math.Round(r.MassKg.Value, 3, MidpointRounding.AwayFromZero)));
        Assert.InRange(Math.Abs(schedule.TotalMassKg.Value - 0.0008), 0, 1e-12);
        Assert.Equal(0.001, Math.Round(schedule.TotalMassKg.Value, 3, MidpointRounding.AwayFromZero), 3);
    }

    [Fact]
    public void WritesS1ScheduleArtifactWhenOutputDirectoryIsProvided()
    {
        var schedule = SteelScheduleCalculator.Calculate(S1Bars(), Policy.MassPolicy, Marks).Value!;
        string? directory = Environment.GetEnvironmentVariable("DBTOOL_T29_OUTPUT");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var artifact = new { schedule.MassPolicyId,schedule.MassPolicyVersion,
            TotalMassKg=schedule.TotalMassKg.Value,
            Rows=schedule.Rows.Select(r => new { r.Mark,r.ShapeSignature,ComponentKind=r.ComponentKind.ToString(),
                Layer=r.Layer.ToString(),Axis=r.Axis.ToString(),r.DiameterMm,r.Quantity,r.SingleLengthMm,
                r.TotalLengthMm,r.UnitMassKgPerM,MassKg=r.MassKg.Value,r.OccurrenceIds }) };
        File.WriteAllText(Path.Combine(directory,"schedule.json"),
            JsonSerializer.Serialize(artifact,new JsonSerializerOptions { WriteIndented=true }));
    }

    private static BarOccurrence[] S1Bars()
    {
        var spec = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy).Value!;
        return SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy).Value!.Occurrences.ToArray();
    }

    private static BarOccurrence Straight(string id, string label, ComponentKind kind, double length, LengthStatus status)
    {
        var path = new BarPath([new LineSegment3Mm(new Point3Mm(0,0,0), new Point3Mm(length,0,0))],
            false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        return new BarOccurrence(id,null,label,kind,Layer.Bottom,Axis.X,10,path,length,
            status == LengthStatus.Known ? length : null,status,"MESH-UNIFORM-TEST-001",[]);
    }
}
