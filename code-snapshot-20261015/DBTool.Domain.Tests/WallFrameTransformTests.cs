using DBTool.Core;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class WallFrameTransformTests
{
    private const double ToleranceMm = 1e-6;

    [Theory]
    [InlineData(WallId.T1, 0, -100, 625, 500)]
    [InlineData(WallId.T2, 0, 500, 100, 500)]
    [InlineData(WallId.T3, 0, 100, -625, 500)]
    [InlineData(WallId.T4, 0, -500, -100, 500)]
    [InlineData(WallId.T1, 150, -100, 775, 500)]
    [InlineData(WallId.T2, 150, 650, 100, 500)]
    [InlineData(WallId.T3, 150, 100, -775, 500)]
    [InlineData(WallId.T4, 150, -650, -100, 500)]
    public void PositiveLeftOffset_MapsToLiteralInsideAndOutsidePoints(
        WallId wall, double v, double x, double y, double z)
    {
        var frame = WallFrame.Create(wall, 1000, 1250);
        var global = frame.ToGlobal(100, v, 500);
        Near(x, global.X); Near(y, global.Y); Near(z, global.Z);

        // Independent global oracle also tests inverse, not only inverse(forward(p)).
        var local = frame.ToLocal(new Point3Mm(x, y, z));
        Near(100, local.U); Near(v, local.V); Near(500, local.Z);
        var screen = frame.ToFaceScreen(local);
        Near(-100, screen.X); Near(500, screen.Y);
    }

    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void SignedLocalCoordinates_RoundTripAndProjectConsistently(WallId wall)
    {
        var frame = WallFrame.Create(wall, 1000, 1250);
        var local = frame.ToLocal(frame.ToGlobal(-125, 75, -30));
        Near(-125, local.U); Near(75, local.V); Near(-30, local.Z);
        Assert.Equal(new Point2Mm(125, -30), frame.ToFaceScreen(local));
    }

    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void FactoryBasis_IsUnitAndMutuallyOrthogonal(WallId wall)
    {
        var frame = WallFrame.Create(wall, 1000, 1250);
        var axes = new[] { frame.Left, frame.Outward, frame.Up };
        foreach (var axis in axes)
            Near(1, Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z));
        Near(0, Dot(frame.Left, frame.Outward));
        Near(0, Dot(frame.Left, frame.Up));
        Near(0, Dot(frame.Outward, frame.Up));
        Assert.Equal(new Vector3(0, 0, 1), frame.Up);
    }

    [Fact]
    public void ClockwiseQuarterTurn_SwapsDimensionsAndPreservesLocalOffset()
    {
        var t1 = WallFrame.Create(WallId.T1, 1000, 1250);
        var p = t1.ToGlobal(100, 150, 500);
        var rotated = new Point3Mm(p.Y, -p.X, p.Z);
        Assert.Equal(new Point3Mm(775, 100, 500), rotated);
        var t2 = WallFrame.Create(WallId.T2, 1250, 1000);
        Assert.Equal(rotated, t2.ToGlobal(100, 150, 500));
        Assert.Equal(new LocalWallPointMm(100, 150, 500), t2.ToLocal(rotated));
    }

    [Fact]
    public void ExistingConstructor_UsesOriginAndAllThreeBasisVectors()
    {
        var frame = new WallFrame(WallId.T1, new Point3Mm(10, 20, 30),
            new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
        Assert.Equal(new Point3Mm(13, 24, 32), frame.ToGlobal(2, 3, 4));
        Assert.Equal(new LocalWallPointMm(2, 3, 4), frame.ToLocal(new Point3Mm(13, 24, 32)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidInnerDimension_IsRejectedOnEitherAxis(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallFrame.Create(WallId.T1, value, 1250));
        Assert.Throws<ArgumentOutOfRangeException>(() => WallFrame.Create(WallId.T1, 1000, value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void UndefinedWallId_IsRejected(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WallFrame.Create((WallId)value, 1000, 1250));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteLocalCoordinate_IsRejectedOnEveryAxis(double value)
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        Assert.Throws<ArgumentOutOfRangeException>(() => frame.ToGlobal(value, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => frame.ToGlobal(0, value, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => frame.ToGlobal(0, 0, value));
    }

    [Fact]
    public void NullFacePoint_IsRejected() =>
        Assert.Throws<ArgumentNullException>(() => WallFrame.Create(WallId.T1, 1000, 1250).ToFaceScreen(null!));

    [Theory]
    [InlineData(WallDirection.T1, WallId.T1)]
    [InlineData(WallDirection.T2, WallId.T2)]
    [InlineData(WallDirection.T3, WallId.T3)]
    [InlineData(WallDirection.T4, WallId.T4)]
    public void LegacyEnumAdapter_MapsByWallMeaningNotNumericCast(WallDirection legacy, WallId expected) =>
        Assert.Equal(expected, WallViewTransform.ToWallId(legacy));

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void UndefinedLegacyDirection_IsRejected(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WallViewTransform.ToWallId((WallDirection)value));

    private static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static void Near(double expected, double actual) =>
        Assert.InRange(actual, expected - ToleranceMm, expected + ToleranceMm);
}
