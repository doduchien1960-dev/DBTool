using DBTool.Domain;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CoverAssemblyValidatorTests
{
    private static readonly SourceRef CoverSource = new(SourceKind.SyntheticFixture, "cover-fixture", new string('a', 64), null, null, null, "independent aperture fixture");
    private static readonly SourceRef NeckSource = new(SourceKind.WorkbookCell, "fixture.xlsx", new string('b', 64), "CF.Ga", "E11", null, "explicit neck size");
    private static CoverCatalogEntry Entry(double rotation = 0, double bearing = 20, double offsetX = 0) =>
        new("TEST-COVER", new(1050, 745), new(offsetX, 0, 0), rotation, CoverPlacementDatum.NeckTop,
            new(-525, -372.5, 525, 372.5), [CoverSource],
            new(new Rectangle2Mm(-410, -285, 410, 285), bearing, [CoverSource]));

    [Theory]
    [InlineData(0, 820, 570)]
    [InlineData(90, 570, 820)]
    [InlineData(180, 820, 570)]
    [InlineData(270, 570, 820)]
    [InlineData(-90, 570, 820)]
    public void ApertureFollowsCoverRotationRatherThanFixedSectionLabels(double angle, double x, double y)
    {
        var result = CoverAssemblyValidator.Validate(Entry(angle), new(100-x/2, 200-y/2, 100+x/2, 200+y/2), 150, .001, CoverSource, NeckSource);
        Assert.True(result.Success);
        Assert.Equal(820, result.Value!.ClearOpening.MaxX - result.Value.ClearOpening.MinX);
        Assert.Equal(570, result.Value.ClearOpening.MaxY - result.Value.ClearOpening.MinY);
    }

    [Theory]
    [InlineData(0, 800, 800, "COVER_OPENING_INCOMPATIBLE")]
    [InlineData(90, 820, 570, "COVER_OPENING_INCOMPATIBLE")]
    [InlineData(45, 820, 570, "COVER_ASSEMBLY_ROTATION_UNSUPPORTED")]
    public void MismatchOrUnsupportedRotationCannotBeHiddenByBoundingBox(double angle, double x, double y, string code)
    {
        var result = CoverAssemblyValidator.Validate(Entry(angle), new(-x/2, -y/2, x/2, y/2), 150, .001, CoverSource, NeckSource);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Block);
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(20, 10)]
    public void RequiredBearingMustFitBothProductAndNeck(double bearing, double thickness)
    {
        var result = CoverAssemblyValidator.Validate(Entry(bearing: bearing), new(-410, -285, 410, 285), thickness, .001, CoverSource, NeckSource);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "COVER_BEARING_INCOMPATIBLE");
    }

    [Fact]
    public void InsertionOffsetCannotSilentlyDisplaceApertureFromNeck()
    {
        var result = CoverAssemblyValidator.Validate(Entry(offsetX: 25), new(-410, -285, 410, 285), 150, .001, CoverSource, NeckSource);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "COVER_OPENING_INCOMPATIBLE");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonfiniteToleranceCannotAcceptIncompatibleOpening(double tolerance)
    {
        var result = CoverAssemblyValidator.Validate(Entry(), new(-400, -400, 400, 400), 150, tolerance, CoverSource, NeckSource);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "COVER_ASSEMBLY_TOLERANCE_INVALID" && d.Severity == DiagnosticSeverity.Block);
    }
}
