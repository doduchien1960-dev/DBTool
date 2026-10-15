using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetLayoutTests
{
    [Fact]
    public void A3PrintableBoundsAndShelfPackingUseAllFrames()
    {
        var package = Package(View("V1", ViewPurpose.Plan, 6000, 4000), View("V2", ViewPurpose.Section, 6000, 4000));
        var profile = A3(20, 10);

        var result = SheetLayoutPlanner.Arrange(package, profile);

        Assert.True(result.Success);
        Assert.Equal(new Bounds2Mm(10, 10, 410, 287), result.Value!.Sheets[0].PrintableBounds);
        Assert.Equal(2, result.Value.Sheets.Length);
        Assert.Equal((1, 2), (result.Value.Sheets[0].PageIndex, result.Value.Sheets[0].PageCount));
        Assert.Equal((2, 2), (result.Value.Sheets[1].PageIndex, result.Value.Sheets[1].PageCount));
        Assert.Equal(1, result.Value.Sheets[0].Frames.Length);
        Assert.Equal(300, result.Value.Sheets[0].Frames[0].PaperBounds.MaxX - result.Value.Sheets[0].Frames[0].PaperBounds.MinX, 12);
        Assert.Equal(200, result.Value.Sheets[0].Frames[0].PaperBounds.MaxY - result.Value.Sheets[0].Frames[0].PaperBounds.MinY, 12);
    }

    [Fact]
    public void PrimaryScaleThirtyFitsSideBySideAtZeroGapAndUsesActualLabels()
    {
        var package = Package(View("V1", ViewPurpose.Plan, 6000, 4000), View("V2", ViewPurpose.Section, 6000, 4000));

        var noGap = SheetLayoutPlanner.Arrange(package, A3(30, 0));
        var gap = SheetLayoutPlanner.Arrange(package, A3(30, 10));

        Assert.True(noGap.Success);
        Assert.Single(noGap.Value!.Sheets);
        Assert.Equal("1:30", noGap.Value.Sheets[0].Frames[1].Label);
        Assert.Equal(200, noGap.Value.Sheets[0].Frames[0].PaperBounds.MaxX - noGap.Value.Sheets[0].Frames[0].PaperBounds.MinX, 12);
        Assert.Equal(133.33333333333334, noGap.Value.Sheets[0].Frames[0].PaperBounds.MaxY - noGap.Value.Sheets[0].Frames[0].PaperBounds.MinY, 12);
        Assert.True(gap.Success);
        Assert.Single(gap.Value!.Sheets);
        Assert.Equal(153.33333333333334, gap.Value.Sheets[0].Frames[1].Transform.Origin.Y, 12);
        Assert.Equal(286.6666666666667, gap.Value.Sheets[0].Frames[1].PaperBounds.MaxY, 12);
    }

    [Fact]
    public void OversizedViewBlocksWithoutCropOrClamp()
    {
        var package = Package(View("Huge", ViewPurpose.Plan, 15000, 10000));

        var result = SheetLayoutPlanner.Arrange(package, A3(20, 10));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LAYOUT_VIEW_TOO_LARGE" && diagnostic.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void ThemeAndScaleArePresentationOnlyAndTextHeightUsesModelScale()
    {
        var package = Package(View("Aux", ViewPurpose.AccessoryDetail, 6000, 4000));
        var first = SheetLayoutPlanner.Arrange(package, A3(20, 0, "light"));
        var second = SheetLayoutPlanner.Arrange(package, A3(30, 0, "dark"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(new string('E', 64), package.InputHash);
        Assert.Equal(50, PresentationProfile.ModelTextHeight(2.5, 20), 12);
        Assert.Equal(75, PresentationProfile.ModelTextHeight(2.5, 30), 12);
        Assert.Equal(20, first.Value!.Sheets[0].Frames[0].Transform.UniformScaleDenominator);
        Assert.Equal(30, second.Value!.Sheets[0].Frames[0].Transform.UniformScaleDenominator);
        Assert.Equal("1:20", first.Value.Sheets[0].Frames[0].Label);
        Assert.Equal("1:30", second.Value.Sheets[0].Frames[0].Label);
    }

    private static PresentationProfile A3(double primaryScale, double gap, string theme = "test") =>
        new(420, 297, 10, 10, 10, 10, 0, gap, [20, 30], primaryScale, [2.5], theme);

    private static ViewDefinition View(string id, ViewPurpose purpose, double width, double height) =>
        new(id, purpose, new Point2Mm(0, 0), new Bounds2Mm(0, 0, width, height), 20);

    private static DrawingPackage Package(params ViewDefinition[] views) =>
        new(new string('e', 64), "Session-07/Drawing-v2", views, [], new Bounds2Mm(0, 0, 1, 1), CalculationScope.FullRebar);
}
