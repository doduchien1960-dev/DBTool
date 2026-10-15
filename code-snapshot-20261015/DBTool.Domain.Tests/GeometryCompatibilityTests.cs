using DBTool.Core;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class GeometryCompatibilityTests
{
    [Fact]
    public void Point2Mm_PreservesCoordinates()
    {
        var point = new Point2Mm(3, 4);
        Assert.Equal(3, point.X);
        Assert.Equal(4, point.Y);
    }

    [Fact]
    public void Interval1D_HasDirectedLength()
    {
        Assert.Equal(5, new DBTool.Domain.Geometry.Interval1D(2, 7).Length);
    }

    [Fact]
    public void CircleClipper_RemovesCentralDiameter()
    {
        var segments = CircleOpeningClipper.SubtractCircleFromHorizontal(-10, 10, 0, 0, 0, 3);
        Assert.Collection(segments,
            first => { Assert.Equal(-10, first.Start); Assert.Equal(-3, first.End); },
            second => { Assert.Equal(3, second.Start); Assert.Equal(10, second.End); });
    }

    [Theory]
    [InlineData(WallDirection.T1, -100)]
    [InlineData(WallDirection.T2, 100)]
    [InlineData(WallDirection.T3, 100)]
    [InlineData(WallDirection.T4, -100)]
    public void LegacyWallOffset_MatchesExistingConvention(WallDirection direction, double expected)
    {
        Assert.Equal(expected, WallViewTransform.ToLocalOffset(direction, 100));
    }
}
