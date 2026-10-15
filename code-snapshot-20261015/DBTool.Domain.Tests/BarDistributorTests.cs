using Xunit;

namespace DBTool.Domain.Tests;

public sealed class BarDistributorTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void Equalized_UsesBothUsableEdgesAndSevenBoundedGaps()
    {
        var result = BarDistributor.DistributeSegment(1000, 30, 10, 150, DistributionMode.Equalized);
        Assert.True(result.Success);
        Assert.Equal(8, result.Value!.PositionsMm.Length);
        Assert.Equal(7, result.Value.ActualGapsMm.Length);
        AssertClose(35, result.Value.PositionsMm[0]);
        AssertClose(965, result.Value.PositionsMm[^1]);
        foreach (double gap in result.Value.ActualGapsMm) AssertClose(930d / 7, gap);
        Assert.All(result.Value.ActualGapsMm, gap => Assert.True(gap <= 150 + Tol));
    }

    [Fact]
    public void DensityCentered_UsesSevenSpecifiedStations()
    {
        var result = BarDistributor.DistributeSegment(1000, 30, 10, 150, DistributionMode.DensityCentered);
        Assert.True(result.Success);
        AssertPositions(new[] { 50d, 200, 350, 500, 650, 800, 950 }, result.Value!.PositionsMm);
        AssertPositions(new[] { 150d, 150, 150, 150, 150, 150 }, result.Value.ActualGapsMm);
    }

    [Theory]
    [InlineData(DistributionMode.Equalized)]
    [InlineData(DistributionMode.DensityCentered)]
    public void ZeroClearSpan_HasOneBarAndNoGap(DistributionMode mode)
    {
        var result = BarDistributor.DistributeSegment(70, 30, 10, 150, mode);
        Assert.True(result.Success);
        AssertPositions(new[] { 35d }, result.Value!.PositionsMm);
        Assert.Empty(result.Value.ActualGapsMm);
    }

    [Fact]
    public void ShortPositiveClearSpan_UsesRequestedMode()
    {
        var equal = BarDistributor.DistributeSegment(100, 30, 10, 150, DistributionMode.Equalized);
        var density = BarDistributor.DistributeSegment(100, 30, 10, 150, DistributionMode.DensityCentered);
        AssertPositions(new[] { 35d, 65 }, equal.Value!.PositionsMm);
        AssertPositions(new[] { 50d }, density.Value!.PositionsMm);
        Assert.Empty(density.Value.ActualGapsMm);
    }

    [Fact]
    public void NegativeClearSpan_IsBlockWithNullValue()
    {
        var result = BarDistributor.DistributeSegment(69, 30, 10, 150, DistributionMode.Equalized);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "DISTRIBUTION_NO_CLEAR_SPAN" && d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global);
    }

    [Theory]
    [InlineData(100, -1, 10, 150)]
    [InlineData(100, 30, 10, 0)]
    [InlineData(double.NaN, 30, 10, 150)]
    [InlineData(100, 30, double.PositiveInfinity, 150)]
    public void InvalidInput_IsBlockWithNullValue(double span, double cover, double diameter, double spacing)
    {
        var result = BarDistributor.DistributeSegment(span, cover, diameter, spacing, DistributionMode.Equalized);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Global);
    }

    [Fact]
    public void RatioCloseToInteger_SnapsOnlyWithinLengthTolerance()
    {
        var near = BarDistributor.DistributeSegment(370 + 0.5e-6, 30, 10, 100, DistributionMode.Equalized);
        var beyond = BarDistributor.DistributeSegment(370 + 2e-6, 30, 10, 100, DistributionMode.Equalized);
        Assert.Equal(4, near.Value!.PositionsMm.Length);
        Assert.Equal(5, beyond.Value!.PositionsMm.Length);
    }

    [Fact]
    public void PositiveClearSpanNearZero_StillHasTwoEqualizedEdgeBars()
    {
        var result = BarDistributor.DistributeSegment(70 + 0.5e-6, 30, 10, 150, DistributionMode.Equalized);
        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.PositionsMm.Length);
        Assert.True(result.Value.PositionsMm[1] > result.Value.PositionsMm[0]);
    }

    private static void AssertPositions(double[] expected, System.Collections.Immutable.ImmutableArray<double> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++) AssertClose(expected[i], actual[i]);
    }

    private static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, Tol);
}
