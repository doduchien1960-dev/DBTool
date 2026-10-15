using System;
using System.Linq;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class DistributionTests
{
    public static TheoryData<string, double, double, double, double, int, double, double> EqualizedCases =>
        new()
        {
            { "S1000-c30-d10-a150", 1000, 30, 10, 150, 8, 35, 965 }
        };

    public static TheoryData<string, double, double[]> DensityCenteredCases =>
        new()
        {
            { "S1000-c30-d10-a150", 1000, new[] { 50d, 200d, 350d, 500d, 650d, 800d, 950d } }
        };

    [Theory]
    [MemberData(nameof(EqualizedCases))]
    [Trait("Migration", "MigratedIndependent")]
    public void Equalized_UsesIndependentExpectedArithmetic(
        string caseName,
        double span,
        double cover,
        double diameter,
        double requestedSpacing,
        int expectedCount,
        double expectedFirst,
        double expectedLast)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));

        var edgeToBarCenter = cover + diameter / 2;
        var usableSpan = span - 2 * edgeToBarCenter;
        var count = (int)Math.Ceiling(usableSpan / requestedSpacing) + 1;
        var actualSpacing = usableSpan / (count - 1);
        var positions = Enumerable.Range(0, count)
            .Select(index => edgeToBarCenter + index * actualSpacing)
            .ToArray();

        Assert.Equal(930, usableSpan, 6);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedFirst, positions[0], 6);
        Assert.Equal(expectedLast, positions[^1], 6);
        Assert.True(actualSpacing <= requestedSpacing + 1e-6);
    }

    [Theory]
    [MemberData(nameof(DensityCenteredCases))]
    [Trait("Migration", "MigratedIndependent")]
    public void DensityCentered_UsesIndependentExpectedPositions(
        string caseName,
        double span,
        double[] expectedPositions)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));

        const double cover = 30;
        const double diameter = 10;
        const double requestedSpacing = 150;
        var edgeToBarCenter = cover + diameter / 2;
        var usableSpan = span - 2 * edgeToBarCenter;
        var count = (int)Math.Floor(usableSpan / requestedSpacing) + 1;
        var residual = usableSpan - (count - 1) * requestedSpacing;
        var positions = Enumerable.Range(0, count)
            .Select(index => edgeToBarCenter + residual / 2 + index * requestedSpacing)
            .ToArray();

        Assert.Equal(930, usableSpan, 6);
        Assert.Equal(expectedPositions.Length, count);
        Assert.Equal(expectedPositions.Length, positions.Length);
        for (var index = 0; index < positions.Length; index++)
        {
            Assert.Equal(expectedPositions[index], positions[index], 6);
        }
    }
}
