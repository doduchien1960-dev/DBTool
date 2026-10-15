using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class MeshZoneFullTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void SeamCentersMergeOnlyWhenThePhysicalIdentityMatches()
    {
        var graph = new ConnectionGraph([
            new MeshZoneInput("A", "Base", Axis.X, new Interval1D(0, 300), new Interval1D(0, 1000), [0, 150, 300]),
            new MeshZoneInput("B", "Base", Axis.X, new Interval1D(300, 600), new Interval1D(0, 1000), [300, 450, 600])]);
        var spec = new MemberRebarSpec([
            new MemberRebarLayerSpec("base-bottom", Layer.Bottom, Axis.X, RebarDirection.Horizontal, 10, 150)]);

        var result = MeshZonePlanner.Plan(BaseComponent(), graph, spec, JunctionPolicy.UniformFixture);

        Assert.True(result.Success);
        Assert.Equal(new[] { 0d, 150, 300, 450, 600 }, result.Value!.MergedCenters.Select(x => x.PositionMm));
        Assert.Equal(5, result.Value.PhysicalBars.Length);
        Assert.All(result.Value.MergedCenters, x => Assert.NotEmpty(x.Provenance));
    }

    [Fact]
    public void SeamDoesNotMergeDifferentLayersEvenWhenCentersCoincide()
    {
        var graph = new ConnectionGraph([
            new MeshZoneInput("A", "Base", Axis.X, new Interval1D(0, 300), new Interval1D(0, 1000), [0, 150, 300]),
            new MeshZoneInput("B", "Base", Axis.X, new Interval1D(300, 600), new Interval1D(0, 1000), [300, 450, 600])]);
        var spec = new MemberRebarSpec([
            new MemberRebarLayerSpec("bottom", Layer.Bottom, Axis.X, RebarDirection.Horizontal, 10, 150),
            new MemberRebarLayerSpec("top", Layer.Top, Axis.X, RebarDirection.Horizontal, 10, 150)]);

        var result = MeshZonePlanner.Plan(BaseComponent(), graph, spec, JunctionPolicy.UniformFixture);

        Assert.True(result.Success);
        Assert.Equal(10, result.Value!.PhysicalBars.Length);
        Assert.Equal(10, result.Value.MergedCenters.Length);
    }

    [Fact]
    public void SeamKeepsNonMatchingCentersFromTheSecondZone()
    {
        var graph = new ConnectionGraph([
            new MeshZoneInput("A", "Base", Axis.X, new Interval1D(0, 300), new Interval1D(0, 1000), [0, 150, 300]),
            new MeshZoneInput("B", "Base", Axis.X, new Interval1D(300, 700), new Interval1D(0, 1000), [300, 500, 700])]);
        var spec = new MemberRebarSpec([
            new MemberRebarLayerSpec("base-bottom", Layer.Bottom, Axis.X, RebarDirection.Horizontal, 10, 150)]);

        var result = MeshZonePlanner.Plan(BaseComponent(), graph, spec, JunctionPolicy.UniformFixture);

        Assert.True(result.Success);
        Assert.Equal(new[] { 0d, 150, 300, 500, 700 }, result.Value!.PhysicalBars.Select(x => x.PositionMm));
    }

    [Fact]
    public void EqualizedAndDensityCenteredUseTheLockedSegmentOracles()
    {
        var equalized = BarDistributor.DistributeSegment(1000, 30, 10, 150, DistributionMode.Equalized).Value!;
        var density = BarDistributor.DistributeSegment(1000, 30, 10, 150, DistributionMode.DensityCentered).Value!;

        Assert.Equal(8, equalized.PositionsMm.Length);
        Assert.Equal(132.85714285714286, equalized.ActualGapsMm[0], 12);
        Assert.Equal(new[] { 50d, 200, 350, 500, 650, 800, 950 }, density.PositionsMm);
        Assert.Equal(35, BarDistributor.DistributeSegment(70, 30, 10, 150, DistributionMode.Equalized).Value!.PositionsMm.Single());
        Assert.False(BarDistributor.DistributeSegment(69, 30, 10, 150, DistributionMode.Equalized).Success);
    }

    private static ComponentGeometry BaseComponent()
    {
        var spec = SyntheticTestFactory.Create();
        return ChamberGeometryBuilder.Build(spec, CalculationPolicy.Select("S1-Analytic-1", true).Value!)
            .Value!.Components.Single(x => x.Id == "Base");
    }
}
