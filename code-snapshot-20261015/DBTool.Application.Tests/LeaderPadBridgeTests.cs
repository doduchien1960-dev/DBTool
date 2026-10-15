using DBTool.Domain;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class LeaderPadBridgeTests
{
    [Fact]
    public void PadOffTraversesLegacyGeometryAndAnnotationsWithoutMovingConcrete()
    {
        var positive = ManholeValidatorTests.Validate(ManholeValidatorTests.Raw());
        var zero = ManholeValidatorTests.Validate(ManholeValidatorTests.Raw("PadThickness", 0, true));
        Assert.True(positive.Success);
        Assert.True(zero.Success);
        var adapter = new GeometryStageAdapter();
        var policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;
        var on = adapter.Build(positive.Value!.SelectedManholes.Single(), policy);
        var off = adapter.Build(zero.Value!.SelectedManholes.Single(), policy);
        Assert.True(on.Success);
        Assert.True(off.Success);
        Assert.DoesNotContain(off.Value!.Components, c => c.Kind == ComponentKind.Pad || c.Id == "Pad");
        Assert.DoesNotContain(off.Value.Datums, d => d.surface_id.Contains("Pad", StringComparison.Ordinal));
        var baseOn = on.Value!.Components.Single(c => c.Id == "Base");
        var baseOff = off.Value.Components.Single(c => c.Id == "Base");
        Assert.Equal(baseOn.ZRange, baseOff.ZRange);
        Assert.Equal(baseOn.Footprint.Outer.Vertices.ToArray(), baseOff.Footprint.Outer.Vertices.ToArray());
        var spec = zero.Value.SelectedManholes.Single();
        var layout = new BaseRebarStageAdapter().Plan(spec, off.Value, policy);
        Assert.True(layout.Success);
        var schedule = SteelScheduleCalculator.Calculate(layout.Value!.Occurrences, policy.MassPolicy, new MarkPolicy(CalculationScope.BaseRebarOnly));
        Assert.True(schedule.Success);
        var package = ViewBuilder.Build(off.Value, layout.Value, schedule.Value!,
            new PresentationRequest(20, ElevationMode.Symbolic, null, "default", true),
            new PackageIdentity(new string('b',64), spec.RuleVersion, CalculationScope.BaseRebarOnly));
        Assert.True(package.Success, string.Join(";", package.Diagnostics.Select(d => d.Code)));
        Assert.DoesNotContain(package.Value!.Primitives, p => p.ComponentId == "Pad" || p.SemanticId.Contains("PadBottom", StringComparison.Ordinal));
        foreach (var view in new[] { "Plan", "AA", "BB", "SteelAA", "SteelBB", "WallMeshPlan" })
        {
            var dimensions = SheetOneDimensionPlanner.Build(off.Value, view);
            Assert.NotNull(dimensions);
            Assert.NotEmpty(dimensions);
        }
        foreach (var mode in new[] { ElevationMode.Absolute, ElevationMode.Symbolic })
        {
            var request = new PresentationRequest(20, mode, mode == ElevationMode.Absolute ? new ElevationM(10) : null, "default", true);
            var labels = AnnotationPlanner.Elevations(off.Value, request).ToArray();
            Assert.DoesNotContain(labels, p => p.ComponentId == "Pad" || p.SemanticId.Contains("PadBottom", StringComparison.Ordinal));
            Assert.Contains(labels, p => p.SemanticId == "Elevation/Floor");
            Assert.Contains(labels, p => p.SemanticId == "Elevation/RoofTop");
        }
    }

    [Theory]
    [InlineData(0.0004)]
    [InlineData(73.125)]
    [InlineData(100)]
    public void PositivePadUsesExactInputAndKeepsConcreteDatum(double thickness)
    {
        var validated = ManholeValidatorTests.Validate(ManholeValidatorTests.Raw("PadThickness", thickness, true));
        Assert.True(validated.Success);
        var model = new GeometryStageAdapter().Build(validated.Value!.SelectedManholes.Single(), CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var pad = model.Components.Single(c => c.Kind == ComponentKind.Pad);
        var concreteBase = model.Components.Single(c => c.Id == "Base");
        Assert.Equal(concreteBase.ZRange.Start, pad.ZRange.End);
        Assert.Equal(concreteBase.ZRange.Start - thickness, pad.ZRange.Start);
        Assert.Equal(0, concreteBase.ZRange.End);
        Assert.Contains(AnnotationPlanner.Elevations(model, new PresentationRequest(20, ElevationMode.Symbolic, null, "default", true)), p => p.SemanticId == "Elevation/PadBottom");
    }

    [Fact]
    public void NegativePadIsBlockedAtSourceBeforeAnyGeometry()
    {
        var result = ManholeValidatorTests.Validate(ManholeValidatorTests.Raw("PadThickness", -1, true));
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block && d.Source!.SheetName == "CF.Ga" && d.Source.CellAddress == "D3");
    }
}
