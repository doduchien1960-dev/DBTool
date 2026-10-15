using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class UniformBaseMeshZoneTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;
    private static readonly MeshSpec Mesh = new(10, 150);

    [Fact]
    public void S1BaseCreatesTwoZonesWithIndependentRunAndDistributionIntervals()
    {
        var result = MeshZonePlanner.PlanUniformBase(Base(), Mesh, 30, Policy);
        Assert.True(result.Success);
        var plan = Assert.IsType<MeshZonePlan>(result.Value);
        Assert.Equal(2, plan.Zones.Length);
        var x = plan.Zones.Single(z => z.Axis == Axis.X);
        var y = plan.Zones.Single(z => z.Axis == Axis.Y);
        Assert.Equal("Base/X", x.Id);
        Assert.Equal("Base/Y", y.Id);
        Assert.Equal("Base", x.ComponentId);
        Assert.Equal("MESH-UNIFORM-TEST-001", x.SourceRuleId);
        Assert.Equal(new Interval1D(-875, 875), x.DistributionInterval);
        Assert.Equal(new Interval1D(-720, 720), x.RunInterval);
        Assert.Equal(new Interval1D(-750, 750), y.DistributionInterval);
        Assert.Equal(new Interval1D(-845, 845), y.RunInterval);
        Assert.NotEmpty(x.Sources);
        Assert.NotEmpty(y.Sources);
    }

    [Fact]
    public void S1BasePlacementsMatchIndependentFixtureAndContainCenter()
    {
        var plan = MeshZonePlanner.PlanUniformBase(Base(), Mesh, 30, Policy).Value!;
        AssertNear(new[] { -840d, -700, -560, -420, -280, -140, 0, 140, 280, 420, 560, 700, 840 },
            plan.Zones.Single(z => z.Axis == Axis.X).PositionsMm);
        AssertNear(new[] { -715d, -572, -429, -286, -143, 0, 143, 286, 429, 572, 715 },
            plan.Zones.Single(z => z.Axis == Axis.Y).PositionsMm);
    }

    [Fact]
    public void ZeroBaseExpansionChangesBothCountsAndRunLengths()
    {
        var baseComponent = Base(baseExpansion: 0);
        var plan = MeshZonePlanner.PlanUniformBase(baseComponent, Mesh, 30, Policy).Value!;
        var x = plan.Zones.Single(z => z.Axis == Axis.X);
        var y = plan.Zones.Single(z => z.Axis == Axis.Y);
        Assert.Equal(11, x.PositionsMm.Length);
        Assert.Equal(10, y.PositionsMm.Length);
        AssertNear(1240, x.RunInterval.Length);
        AssertNear(1490, y.RunInterval.Length);
        Assert.Contains(x.PositionsMm, p => Math.Abs(p) <= 1e-6);
    }

    [Fact]
    public void RotatedInputVertexOrderKeepsZoneIdentityAndPlacements()
    {
        var original = Base();
        var vertices = original.Footprint.Outer.Vertices;
        var rotated = new Polygon2Mm(vertices.Skip(1).Concat(vertices.Take(1)));
        var changed = WithFootprint(original, new Footprint(rotated, []));
        var first = MeshZonePlanner.PlanUniformBase(original, Mesh, 30, Policy).Value!;
        var second = MeshZonePlanner.PlanUniformBase(changed, Mesh, 30, Policy).Value!;
        Assert.Equal(first.Zones.Select(z => z.Id), second.Zones.Select(z => z.Id));
        for (int i = 0; i < first.Zones.Length; i++) AssertNear(first.Zones[i].PositionsMm, second.Zones[i].PositionsMm);
    }

    [Fact]
    public void NonRectangularBaseRequiresJunctionPolicy()
    {
        var original = Base();
        var polygon = new Polygon2Mm(new[] {
            new Point2Mm(-750,-875), new Point2Mm(750,-875), new Point2Mm(750,775),
            new Point2Mm(650,875), new Point2Mm(-750,875) });
        AssertBlock(WithFootprint(original, new Footprint(polygon, [])), "MESH_JUNCTION_POLICY_REQUIRED");
    }

    [Fact]
    public void BaseOpeningIsUnsupported()
    {
        var original = Base();
        var hole = new Polygon2Mm(new[] {
            new Point2Mm(-50,50), new Point2Mm(50,50), new Point2Mm(50,-50), new Point2Mm(-50,-50) });
        AssertBlock(WithFootprint(original, new Footprint(original.Footprint.Outer, [hole])), "MESH_OPENING_UNSUPPORTED");
    }

    [Fact]
    public void OccupiedGeometryDifferentFromReferenceFootprintRequiresJunctionPolicy()
    {
        var original = Base();
        var smaller = new Footprint(new Polygon2Mm(new[] {
            new Point2Mm(-700,-825), new Point2Mm(700,-825),
            new Point2Mm(700,825), new Point2Mm(-700,825) }), []);
        var changed = new ComponentGeometry(original.Id, original.Kind, original.MaterialId, original.Footprint,
            original.ZRange, new ExtrusionSolid(smaller, Assert.IsType<ExtrusionSolid>(original.Occupied).Frame,
                original.ZRange), original.Voids, original.FacePatches, original.Sources);
        AssertBlock(changed, "MESH_JUNCTION_POLICY_REQUIRED");
    }

    [Fact]
    public void ActiveHaunchIsBlockedUpstreamBeforeAComponentCanBePlanned()
    {
        var seed = SyntheticTestFactory.Create();
        var i = seed.Input;
        var changed = new ManholeInput(i.Id, i.Name, i.Profile, i.L, i.B, i.H, i.BaseThicknessMm, i.WallThicknessMm,
            i.RoofThicknessMm, i.PadThicknessMm, i.BaseExpansionMm, i.PadExpansionMm, i.BeddingMortarMm, 20,
            i.TopHaunch, i.NeckHeight, i.RoofOpening, i.PipeConnections, i.BaseBottomMesh, i.BaseTopMesh, i.CoverMm,
            i.ElevationMode, i.BaseElevationM, i.PipeInvertReference, i.HookBaseBottom, i.HookBaseTop, i.DistributionMode,
            i.MeshJunctionPolicy, i.LayerStackPolicy, i.FeatureRequests, i.SourceRefs, i.UnsupportedRawFields);
        var spec = new ValidatedManholeSpec(changed, seed.Catalogs, seed.RuleVersion, seed.Sources, seed.FeatureDiagnostics);
        var geometry = ChamberGeometryBuilder.Build(spec, Policy);
        Assert.False(geometry.Success);
        Assert.Null(geometry.Value);
        Assert.Contains(geometry.Diagnostics, d => d.Code == "GEOMETRY_UNSUPPORTED" && d.Severity == DiagnosticSeverity.Block);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void InvalidCoverReturnsBlockAndNullValue(double cover)
    {
        var result = MeshZonePlanner.PlanUniformBase(Base(), Mesh, cover, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global && d.Source is not null);
    }

    private static void AssertBlock(ComponentGeometry component, string code)
    {
        var result = MeshZonePlanner.PlanUniformBase(component, Mesh, 30, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global && d.Source is not null);
    }

    private static ComponentGeometry Base(double? baseExpansion = null)
    {
        var seed = SyntheticTestFactory.Create();
        if (baseExpansion is null)
            return ChamberGeometryBuilder.Build(seed, Policy).Value!.Components.Single(c => c.Id == "Base");
        var i = seed.Input;
        var changed = new ManholeInput(i.Id, i.Name, i.Profile, i.L, i.B, i.H, i.BaseThicknessMm, i.WallThicknessMm,
            i.RoofThicknessMm, i.PadThicknessMm, baseExpansion.Value, i.PadExpansionMm, i.BeddingMortarMm, i.BaseHaunch,
            i.TopHaunch, i.NeckHeight, i.RoofOpening, i.PipeConnections, i.BaseBottomMesh, i.BaseTopMesh, i.CoverMm,
            i.ElevationMode, i.BaseElevationM, i.PipeInvertReference, i.HookBaseBottom, i.HookBaseTop, i.DistributionMode,
            i.MeshJunctionPolicy, i.LayerStackPolicy, i.FeatureRequests, i.SourceRefs, i.UnsupportedRawFields);
        var spec = new ValidatedManholeSpec(changed, seed.Catalogs, seed.RuleVersion, seed.Sources, seed.FeatureDiagnostics);
        return ChamberGeometryBuilder.Build(spec, Policy).Value!.Components.Single(c => c.Id == "Base");
    }

    private static ComponentGeometry WithFootprint(ComponentGeometry original, Footprint footprint) =>
        new(original.Id, original.Kind, original.MaterialId, footprint, original.ZRange,
            new ExtrusionSolid(footprint, Assert.IsType<ExtrusionSolid>(original.Occupied).Frame, original.ZRange),
            original.Voids, original.FacePatches, original.Sources);

    private static void AssertNear(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-6);
    private static void AssertNear(IEnumerable<double> expected, IEnumerable<double> actual)
    {
        var e = expected.ToArray(); var a = actual.ToArray();
        Assert.Equal(e.Length, a.Length);
        for (int i = 0; i < e.Length; i++) AssertNear(e[i], a[i]);
    }
}
