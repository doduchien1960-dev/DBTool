using System;
using System.Linq;
using DBTool.Core;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class ClippingTests
{
    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void CircleThroughCenter_RetainsTwoSegmentsAndFourteenMillimetres()
    {
        var retained = CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 0, 0, 0, 3);

        Assert.Collection(retained,
            left =>
            {
                AssertInterval(left, -10, -3);
            },
            right =>
            {
                AssertInterval(right, 3, 10);
            });
        Assert.Equal(14, TotalLength(retained), 6);
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void CircleOffsetOutsideOpening_RetainsOriginalTwentyMillimetres()
    {
        var retained = CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 4, 0, 0, 3);

        Assert.Collection(retained, segment => AssertInterval(segment, -10, 10));
        Assert.Equal(20, TotalLength(retained), 6);
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void CircleTangent_DoesNotRemoveLength()
    {
        var retained = CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 3, 0, 0, 3);

        Assert.Collection(retained, segment => AssertInterval(segment, -10, 10));
        Assert.Equal(20, TotalLength(retained), 6);
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void CircleCoveringEntireSegment_ReturnsNoRetainedSegment()
    {
        var retained = CircleOpeningClipper.SubtractCircleFromHorizontal(-2, 2, 0, 0, 0, 3);

        Assert.Empty(retained);
        Assert.Equal(0, TotalLength(retained), 6);
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void RectangleOpening_RetainsSixteenMillimetres()
    {
        var retained = RectangleOpeningClipper.SubtractFromHorizontal(-10, 10, 0, new Rect2D(-2, -2, 2, 2));

        Assert.Collection(retained,
            left => AssertInterval(left, -10, -2),
            right => AssertInterval(right, 2, 10));
        Assert.Equal(16, TotalLength(retained), 6);
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void ReversedEndpoints_AreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CircleOpeningClipper.SubtractCircleFromHorizontal(10, -10, 0, 0, 0, 3));
        Assert.Throws<ArgumentException>(() =>
            RectangleOpeningClipper.SubtractFromHorizontal(10, -10, 0, new Rect2D(-2, -2, 2, 2)));
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void NonFiniteInput_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CircleOpeningClipper.SubtractCircleFromHorizontal(double.NaN, 10, 0, 0, 0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RectangleOpeningClipper.SubtractFromHorizontal(-10, 10, double.NaN, new Rect2D(-2, -2, 2, 2)));
    }

    [Fact]
    [Trait("Migration", "MigratedIndependent")]
    public void NegativeCircleRadius_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 0, 0, 0, -1));
    }

    private static double TotalLength(System.Collections.Generic.IReadOnlyList<Interval1D> segments) =>
        segments.Sum(segment => segment.Length);

    private static void AssertInterval(Interval1D actual, double expectedStart, double expectedEnd)
    {
        Assert.Equal(expectedStart, actual.Start, 6);
        Assert.Equal(expectedEnd, actual.End, 6);
    }
}
