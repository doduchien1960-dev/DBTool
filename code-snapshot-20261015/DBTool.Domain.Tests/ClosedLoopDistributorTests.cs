using Xunit;

namespace DBTool.Domain.Tests;

public sealed class ClosedLoopDistributorTests
{
    [Fact]
    public void Equalized_UsesFourStationsWithoutRepeatingPerimeter()
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(1000, 300, DistributionMode.Equalized, 0);
        Assert.True(result.Success);
        AssertStations(new[] { 0d, 250, 500, 750 }, result.Value!.StationsMm);
        AssertStations(new[] { 250d, 250, 250, 250 }, result.Value.CyclicGapsMm);
    }

    [Fact]
    public void DensityCentered_UsesThreeStationsAndSeamGap()
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(1000, 300, DistributionMode.DensityCentered, 0);
        Assert.True(result.Success);
        AssertStations(new[] { 200d, 500, 800 }, result.Value!.StationsMm);
        AssertStations(new[] { 300d, 300, 400 }, result.Value.CyclicGapsMm);
    }

    [Fact]
    public void ExactMultipleDensity_KeepsCenteredSeamGap()
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(900, 300, DistributionMode.DensityCentered, 0);
        AssertStations(new[] { 150d, 450, 750 }, result.Value!.StationsMm);
        AssertStations(new[] { 300d, 300, 300 }, result.Value.CyclicGapsMm);
    }

    [Theory]
    [InlineData(DistributionMode.Equalized)]
    [InlineData(DistributionMode.DensityCentered)]
    public void PerimeterShorterThanSpacing_HasOneStationAndOneCyclicGap(DistributionMode mode)
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(200, 300, mode, 0);
        AssertStations(new[] { 0d }, result.Value!.StationsMm);
        AssertStations(new[] { 200d }, result.Value.CyclicGapsMm);
    }

    [Theory]
    [InlineData(DistributionMode.Equalized)]
    [InlineData(DistributionMode.DensityCentered)]
    public void SeamOffset_ShiftsStationsModuloPerimeter(DistributionMode mode)
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(1000, 300, mode, 50);
        AssertStations(mode == DistributionMode.Equalized ? new[] { 50d, 300, 550, 800 } : new[] { 250d, 550, 850 }, result.Value!.StationsMm);
        Assert.All(result.Value.StationsMm, station => Assert.InRange(station, 0, 999.999999));
        Assert.Equal(result.Value.StationsMm.Length, result.Value.StationsMm.Distinct().Count());
    }

    [Fact]
    public void SeamOffsetPastEnd_WrapsWithoutDuplicate()
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(1000, 300, DistributionMode.Equalized, 900);
        AssertStations(new[] { 900d, 150, 400, 650 }, result.Value!.StationsMm);
        AssertStations(new[] { 250d, 250, 250, 250 }, result.Value.CyclicGapsMm);
    }

    [Theory]
    [InlineData(0, 300, 0)]
    [InlineData(1000, 0, 0)]
    [InlineData(double.NaN, 300, 0)]
    [InlineData(1000, 300, double.PositiveInfinity)]
    public void InvalidInput_IsBlockWithNullValue(double perimeter, double spacing, double seam)
    {
        var result = ClosedLoopDistributor.DistributeClosedLoop(perimeter, spacing, DistributionMode.Equalized, seam);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global);
    }

    private static void AssertStations(double[] expected, System.Collections.Immutable.ImmutableArray<double> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++) Assert.InRange(Math.Abs(expected[i] - actual[i]), 0, 1e-6);
    }
}
