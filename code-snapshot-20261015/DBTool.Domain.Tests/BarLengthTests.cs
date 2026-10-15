using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class BarLengthTests
{
    [Theory]
    [InlineData(HookPattern.Hook0M, HookMode.ClearThickness, 0, 0, 940)]
    [InlineData(HookPattern.Hook1M, HookMode.ClearThickness, 0, 90, 1030)]
    [InlineData(HookPattern.Hook2M, HookMode.ClearThickness, 90, 90, 1120)]
    [InlineData(HookPattern.HookStartOnly, HookMode.ClearThickness, 90, 0, 1030)]
    [InlineData(HookPattern.Hook1M, HookMode.DiameterMultiple, 0, 100, 1040)]
    [InlineData(HookPattern.Hook2M, HookMode.DiameterMultiple, 100, 100, 1140)]
    public void NominalHooksHaveExplicitEndsAndDoNotClaimFabrication(HookPattern pattern, HookMode mode,
        double start, double end, double expected)
    {
        var hooks = HookResolver.Resolve(pattern, mode, 150, 30, 30, 10, mode == HookMode.DiameterMultiple ? 10 : null);
        Assert.True(hooks.Success);
        Assert.Equal(start, hooks.Value!.StartMm); Assert.Equal(end, hooks.Value.EndMm);
        var result = BarLengthResolver.Resolve(1000, 30, 30, hooks.Value);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value!.ScheduleLengthMm);
        Assert.Null(result.Value.FabricationLengthMm);
        Assert.Equal(LengthStatus.NeedsRule, result.Value.LengthStatus);
        Assert.Contains(result.Diagnostics, d => d.Code == "FABRICATION_RULE_MISSING" && d.Severity == DiagnosticSeverity.Review);
    }

    [Theory]
    [InlineData(50, 30, 30, 10)]
    [InlineData(60, 30, 30, 10)]
    [InlineData(150, -1, 30, 10)]
    [InlineData(150, 30, 30, double.NaN)]
    public void InvalidHookGeometryDoesNotClamp(double t, double c1, double c2, double d) =>
        Block(HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, t, c1, c2, d, null), "HOOK_INVALID");

    [Fact]
    public void InvalidMultipleAndEnumAreBlocked()
    {
        Block(HookResolver.Resolve(HookPattern.Hook2M, HookMode.DiameterMultiple, 150, 30, 30, 10, 0), "HOOK_INVALID");
        Block(HookResolver.Resolve((HookPattern)99, HookMode.ClearThickness, 150, 30, 30, 10, null), "HOOK_INVALID");
    }

    [Fact]
    public void RealTangentPathMeasuresLinesAndArcWithoutSecondBendDeduction()
    {
        var path = Bent();
        Assert.Equal(10, Assert.IsType<ArcSegment3Mm>(path.Segments[1]).CenterlineRadiusMm);
        Near(195.70796326794897, BendLengthCalculator.Calculate(path, 0).Value!.Value);
        Near(235.70796326794897, BendLengthCalculator.Calculate(path, 40).Value!.Value);
        Near(195.70796326794897, BendLengthCalculator.Calculate(Bent(sweepNegative: true), 0).Value!.Value);
    }

    [Theory]
    [InlineData(MeasurementBasis.TangentPath, 195.70796326794897)]
    [InlineData(MeasurementBasis.TheoreticalCorner, 175.70796326794897)]
    public void ExplicitDimensionBasisControlsOneBendDeduction(MeasurementBasis basis, double expected)
    {
        var policy = new BendPolicy(basis, 5, [Math.PI / 2], 0, false);
        Near(expected, BendLengthCalculator.CalculateDimensions([100, 80], 10, policy).Value!.Value);
    }

    [Fact]
    public void LapMetadataAndArgumentDescribeOneLapAndAlreadyIncludedIsNotAddedAgain()
    {
        Near(235.70796326794897, BendLengthCalculator.Calculate(Bent(lap: 40), 40).Value!.Value);
        Near(235.70796326794897, BendLengthCalculator.Calculate(Bent(lap: 40), 0).Value!.Value);
        Near(235.70796326794897, BendLengthCalculator.Calculate(Bent(lap: 40, included: true), 40).Value!.Value);
        Block(BendLengthCalculator.Calculate(Bent(lap: 40), 50), "BAR_LAP_CONFLICT");
        Block(BendLengthCalculator.Calculate(Bent(), -1), "BAR_LENGTH_INVALID");
    }

    [Fact]
    public void Straight1440HasEqualScheduleAndFabricationAndEuclidean3DWorks()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook0M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(1440, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var result = BarLengthResolver.Resolve(1500, 30, 30, hooks, path);
        Assert.Equal(1440, result.Value!.ScheduleLengthMm);
        Assert.Equal(1440d, result.Value.FabricationLengthMm);
        Assert.Equal(LengthStatus.Known, result.Value.LengthStatus);
        var diagonal = new BarPath([new LineSegment3Mm(new(1, 2, 3), new(4, 6, 15))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        Near(13, BendLengthCalculator.Calculate(diagonal, 0).Value!.Value);
    }

    [Fact]
    public void KnownBendRuleCanResolveNominalHookAndMissingRadiusStaysNeedsRule()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var rule = new BendPolicy(MeasurementBasis.TheoreticalCorner, 5, [Math.PI / 2], 0, false);
        var result = BarLengthResolver.Resolve(1000, 30, 30, hooks, bendPolicy: rule, diameterMm: 10);
        Assert.Equal(1030, result.Value!.ScheduleLengthMm);
        Near(1025.707963267949, result.Value.FabricationLengthMm!.Value);
        var missing = BarLengthResolver.Resolve(1000, 30, 30, hooks,
            bendPolicy: new BendPolicy(MeasurementBasis.TheoreticalCorner, null, [Math.PI / 2], 0, false), diameterMm: 10);
        Assert.True(missing.Success); Assert.Equal(LengthStatus.NeedsRule, missing.Value!.LengthStatus);
        Block(BarLengthResolver.Resolve(50, 30, 30, hooks), "BAR_LENGTH_INVALID");
        Block(BendLengthCalculator.CalculateDimensions([5, 80], 10, rule), "BAR_BEND_INVALID");
    }

    [Fact]
    public void InvalidArcGuardsRemainInExistingContract()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArcSegment3Mm.FromInnerRadius(new(0, 0, 0), -1, 10, new(0, 0, 1), new(1, 0, 0), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArcSegment3Mm(new(0, 0, 0), double.NaN, new(0, 0, 1), new(1, 0, 0), 1));
        Assert.Throws<ArgumentException>(() => new ArcSegment3Mm(new(0, 0, 0), 10, new(0, 0, 1), new(1, 0, 0), 0));
        Assert.Throws<ArgumentException>(() => new BarPath([new LineSegment3Mm(new(0, 0, 0), new(1, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TheoreticalCorner));
    }

    [Fact]
    public void ShapeSignatureIsRigidTransformInvariantButDistinguishesGeometryAndDiameter()
    {
        var path = Bent();
        string key = ShapeSignature.Create(path, 10);
        Assert.Equal(key, ShapeSignature.Create(Transform(path), 10));
        Assert.NotEqual(key, ShapeSignature.Create(path, 12));
        Assert.NotEqual(key, ShapeSignature.Create(Bent(lap: 40), 10));
        Assert.NotEqual(key, ShapeSignature.Create(Bent(included: true), 10));
    }

    [Fact]
    public void StraightPathCannotContradictTheDeclaredClearSpan()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook0M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var wrong = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(100, 0, 0))], false,
            HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        Block(BarLengthResolver.Resolve(1500, 30, 30, hooks, wrong), "BAR_PATH_MISMATCH");
    }

    [Fact]
    public void NominalOnlyStraightLengthNeedsEvidenceOfFabrication()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook0M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var result = BarLengthResolver.Resolve(1500, 30, 30, hooks);
        Assert.True(result.Success);
        Assert.Equal(1440, result.Value!.ScheduleLengthMm);
        Assert.Null(result.Value.FabricationLengthMm);
        Assert.Equal(LengthStatus.NeedsRule, result.Value.LengthStatus);
    }

    [Fact]
    public void HookLabelCannotTurnAnUnrelatedLineIntoKnownFabrication()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var falseHook = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(100, 0, 0))], false,
            HookPattern.Hook1M, 0, false, MeasurementBasis.TangentPath);
        Block(BarLengthResolver.Resolve(1000, 30, 30, hooks, falseHook), "BAR_PATH_MISMATCH");
    }

    [Fact]
    public void HookedRealPathRequiresExplicitNominalBasisAndMatchingDimensions()
    {
        var hooks = HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var unknown = BarLengthResolver.Resolve(170, 30, 30, hooks, Bent());
        Assert.Equal(LengthStatus.NeedsRule, unknown.Value!.LengthStatus);
        var known = BarLengthResolver.Resolve(170, 30, 30, hooks, Bent(), nominalBasis: MeasurementBasis.TheoreticalCorner);
        Assert.True(known.Success);
        Assert.Equal(200, known.Value!.ScheduleLengthMm);
        Near(195.70796326794897, known.Value.FabricationLengthMm!.Value);
        Block(BarLengthResolver.Resolve(1000, 30, 30, hooks, Bent(), nominalBasis: MeasurementBasis.TheoreticalCorner), "BAR_PATH_MISMATCH");
        var tangentHooks = HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, 140, 30, 30, 10, null).Value!;
        var tangent = BarLengthResolver.Resolve(160, 30, 30, tangentHooks, Bent(), nominalBasis: MeasurementBasis.TangentPath);
        Assert.Equal(180, tangent.Value!.ScheduleLengthMm);
        Near(195.70796326794897, tangent.Value.FabricationLengthMm!.Value);
    }

    [Fact]
    public void TwoHookPathMatchesBothHookLegsAndTheBodyUnderRigidTransform()
    {
        var first = new ArcSegment3Mm(new(10, 10, 0), 10, new(0, 0, 1), new(-1, 0, 0), Math.PI / 2);
        var last = new ArcSegment3Mm(new(930, 10, 0), 10, new(0, 0, 1), new(0, -1, 0), Math.PI / 2);
        var path = new BarPath([new LineSegment3Mm(new(0, 90, 0), first.Start), first,
            new LineSegment3Mm(first.End, last.Start), last, new LineSegment3Mm(last.End, new(940, 90, 0))],
            false, HookPattern.Hook2M, 0, false, MeasurementBasis.TangentPath);
        var hooks = HookResolver.Resolve(HookPattern.Hook2M, HookMode.ClearThickness, 150, 30, 30, 10, null).Value!;
        var result = BarLengthResolver.Resolve(1000, 30, 30, hooks, Transform(path), nominalBasis: MeasurementBasis.TheoreticalCorner);
        Assert.True(result.Success);
        Assert.Equal(1120, result.Value!.ScheduleLengthMm);
        Near(1111.415926535898, result.Value.FabricationLengthMm!.Value);
    }

    [Fact]
    public void LengthFailureRetainsSourceAndRejectsDerivedOverflow()
    {
        var source = Fixtures.SyntheticTestFactory.Create().Sources[0];
        var result = HookResolver.Resolve(HookPattern.Hook1M, HookMode.ClearThickness, 50, 30, 30, 10, null, source);
        Assert.Same(source, Assert.Single(result.Diagnostics).Source);
        var huge = new BarPath([new LineSegment3Mm(new(-double.MaxValue, 0, 0), new(double.MaxValue, 0, 0))],
            false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        Block(BendLengthCalculator.Calculate(huge, 0), "BAR_LENGTH_INVALID");
        Block(BendLengthCalculator.CalculateDimensions([100, 80], double.NaN,
            new BendPolicy(MeasurementBasis.TangentPath, 5, [Math.PI / 2], 0, false)), "BAR_BEND_INVALID");
    }

    [Fact]
    public void MultipleCornersTrimSharedLegAtBothEndsAndDoNotReuseNominalAsTangent()
    {
        var policy = new BendPolicy(MeasurementBasis.TheoreticalCorner, 5, [Math.PI / 2, Math.PI / 2], 0, false);
        // Nominal 100+80+100; four trims of 10; two quarter arcs total 10*pi.
        Near(271.41592653589794, BendLengthCalculator.CalculateDimensions([100, 80, 100], 10, policy).Value!.Value);
        Block(BendLengthCalculator.CalculateDimensions([100, 15, 100], 10, policy), "BAR_BEND_INVALID");
    }

    [Fact]
    public void ShapeIncludesRelativeBendGeometryNotOnlySegmentLengths()
    {
        BarPath Poly(Point3Mm last) => new([new LineSegment3Mm(new(0, 0, 0), new(100, 0, 0)),
            new LineSegment3Mm(new(100, 0, 0), last)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        Assert.NotEqual(ShapeSignature.Create(Poly(new(100, 100, 0)), 10),
            ShapeSignature.Create(Poly(new(160, 80, 0)), 10));
    }

    [Fact]
    public void ClosedCircleSignatureIgnoresFloatingClosureResidueAfterRigidTransform()
    {
        var circle = new BarPath([new ArcSegment3Mm(new(0, 0, 0), 10, new(0, 0, 1), new(1, 0, 0), 2 * Math.PI)],
            true, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        Assert.Equal(ShapeSignature.Create(circle, 10), ShapeSignature.Create(Transform(circle), 10));
    }

    private static BarPath Bent(double lap = 0, bool included = false, bool sweepNegative = false)
    {
        double sign = sweepNegative ? -1 : 1;
        var arc = ArcSegment3Mm.FromInnerRadius(new(100, sign * 10, 0), 5, 10, new(0, 0, 1), new(0, -sign, 0), sign * Math.PI / 2);
        return new BarPath([new LineSegment3Mm(new(0, 0, 0), arc.Start), arc,
            new LineSegment3Mm(arc.End, new(110, sign * (90 + (included ? 40 : 0)), 0))], false, HookPattern.Hook1M, lap, included, MeasurementBasis.TangentPath);
    }

    private static BarPath Transform(BarPath path)
    {
        Vector3 V(Vector3 p) => new(p.Z, 0.6 * p.X - 0.8 * p.Y, 0.8 * p.X + 0.6 * p.Y);
        Point3Mm P(Point3Mm p) => new(50 + p.Z, 700 + 0.6 * p.X - 0.8 * p.Y, -200 + 0.8 * p.X + 0.6 * p.Y);
        return new BarPath(path.Segments.Select<PathSegment, PathSegment>(s => s is LineSegment3Mm l ? new LineSegment3Mm(P(l.Start), P(l.End)) :
            new ArcSegment3Mm(P(((ArcSegment3Mm)s).Center), ((ArcSegment3Mm)s).CenterlineRadiusMm,
                V(((ArcSegment3Mm)s).Normal), V(((ArcSegment3Mm)s).StartDirection), ((ArcSegment3Mm)s).SweepRadians)),
            path.Closed, path.HookPattern, path.AdditionalLapMm, path.LapAlreadyInPath, path.MeasurementBasis);
    }
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-6, expected + 1e-6);
    private static void Block<T>(OperationResult<T> result, string code) where T : class
    {
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Block);
    }
}
