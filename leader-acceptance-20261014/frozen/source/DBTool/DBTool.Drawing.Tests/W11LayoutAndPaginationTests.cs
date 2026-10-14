#nullable enable
using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class W11LayoutAndPaginationTests
{
    [Fact]
    public void FrameRegistry_TracksRegisteredPlacementsAndVerifiesCoverage()
    {
        var registry = new FrameRegistry();

        var page = new SheetPlanPage(
            id: "Page1",
            content: SheetContent.Structure,
            manifestId: null,
            viewIds: ImmutableArray.Create("Plan", "AA"),
            manholeId: "MH01",
            pageRole: "Structure",
            pageIndex: 1,
            scaleDenominator: 30.0,
            inkBounds: new Bounds2Mm(0, 0, 1000, 1000),
            ownedViewBundles: new[] { "Plan", "AA" }
        );

        registry.Register(page);

        Assert.True(registry.IsViewPlaced("Plan"));
        Assert.True(registry.IsViewPlaced("AA"));
        Assert.False(registry.IsViewPlaced("BB"));
        Assert.Equal(page, registry.GetPage("Page1"));

        var passCoverage = registry.VerifyCoverage(["Plan", "AA"]);
        Assert.True(passCoverage.Success);

        var failCoverage = registry.VerifyCoverage(["Plan", "AA", "BB"]);
        Assert.False(failCoverage.Success);
        Assert.Equal("LAYOUT_REQUIRED_VIEW_COVERAGE", failCoverage.Diagnostics[0].Code);
    }

    [Fact]
    public void RecipeContext_EncapsulatesRoleOrderAndContentFrame()
    {
        var content = new Bounds2Mm(10, 10, 410, 287);
        var recipe = new RecipeContext(
            recipeId: "NR-22",
            manholeType: "GaThamLienKhoi",
            visitProfile: 1,
            roleOrder: ["Structure", "BaseWalls12", "Walls34Roof", "NeckAndSchedule", "CoverConstruction", "Bending"],
            preferredScaleDenominator: 30.0,
            contentFrame: content
        );

        Assert.Equal("NR-22", recipe.RecipeId);
        Assert.Equal("GaThamLienKhoi", recipe.ManholeType);
        Assert.Equal(6, recipe.RoleOrder.Count);
        Assert.Equal(30.0, recipe.PreferredScaleDenominator);
        Assert.Equal(content, recipe.ContentFrame);
    }

    [Fact]
    public void DrawingPackageValidator_ValidatePackageCompleteness_DetectsMissingGroupOrViews()
    {
        var views = new[]
        {
            new ViewDefinition("Plan", ViewPurpose.Plan, new Point2Mm(0, 0), new Bounds2Mm(0, 0, 1000, 1000), 30),
            new ViewDefinition("AA", ViewPurpose.Section, new Point2Mm(0, 0), new Bounds2Mm(0, 0, 1000, 1000), 30)
        };

        // Primitives only reference "Plan", omitting "AA"
        var prim = new PolylinePrimitive("p1", "Geometry/BaseOutline", "Plan", "Base", "Occ01", "1", [new(0, 0), new(100, 0)], false);

        var package = new DrawingPackage(
            new string('a', 64),
            "v1",
            views,
            [prim],
            new Bounds2Mm(0, 0, 1000, 1000),
            CalculationScope.FullRebar,
            groups: ["Occ01", "Occ02_Missing"]
        );

        var report = DrawingPackageValidator.ValidatePackageCompleteness(package);
        Assert.False(report.Success);
        // Either view primitive missing or declared group missing
        Assert.Contains(report.Diagnostics, d => d.Code is "DRAWING_VIEW_PRIMITIVES_MISSING" or "DRAWING_GROUP_COVERAGE_MISSING");
    }

    [Fact]
    public void SheetPlanPage_PreservesExpandedProperties()
    {
        var page = new SheetPlanPage(
            id: "Page/Cover",
            content: SheetContent.Auxiliary,
            manifestId: "Manifest/Cover",
            viewIds: ImmutableArray.Create("Cover/Plan", "Cover/Section"),
            manholeId: "MH_Test",
            pageRole: "CoverConstruction",
            pageIndex: 5,
            scaleDenominator: 20.0,
            inkBounds: new Bounds2Mm(10, 10, 200, 200),
            ownedViewBundles: new[] { "Cover/Plan", "Cover/Section" }
        );

        Assert.Equal("Page/Cover", page.Id);
        Assert.Equal("CoverConstruction", page.PageRole);
        Assert.Equal(5, page.PageIndex);
        Assert.Equal(20.0, page.ScaleDenominator);
        Assert.NotNull(page.InkBounds);
        Assert.Equal(2, page.OwnedViewBundles.Length);
    }
}
