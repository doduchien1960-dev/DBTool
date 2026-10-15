using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class Family21PlacementRegressionTests
{
    [Fact]
    public void ExplicitCodeOverridesGeometryAndPlacesFramesWithoutPackage()
    {
        const string wall = "Reinforcement/Wall/T1/Wall/T1/Outer/Top";
        var frame = new SheetViewFrame(wall, ViewPurpose.Section, new(-100, -100, 100, 100),
            new(1, 1, 10, 10), new(new(1, 1), 30), "wall", []);
        var sheet = new SheetLayoutSheet(2, 5, new(0, 0, 420, 297), [frame]);
        var placed = Assert.Single(SheetTwoPlacement.Apply(sheet, manholeCode: "G400-1").Frames);
        Assert.Equal(new Point2Mm(5685, 6860), placed.Transform.ToModel(30).Apply(new(0, 0), placed.LogicalBounds));
        Assert.Equal(new Point2Mm(2395, 2625), SheetTwoPlacement.Origin("BaseBottom", null, "G600-3"));
        Assert.Equal(new Point2Mm(9575, 2505), SheetThreePlacement.Origin(SheetThreeRoofViews.BottomViewId, null, "G800-2"));
        Assert.Equal(new Point2Mm(2345, 3820), SheetFourPlacement.Title(SheetFourViews.BBViewId, "G500-2"));
    }

    [Theory]
    [InlineData("G600-3")]
    [InlineData("G800-1")]
    [InlineData("G800-2")]
    public void WallPipeDimensionsHaveLocalNondegenerateGeometry(string code)
    {
        var walls = SheetThreeWallViews.Build([], code);
        Assert.All(walls.OfType<DimensionIntentPrimitive>(), d =>
        {
            Assert.NotEqual(d.Start, d.End);
            Assert.InRange(Math.Abs(d.Start.X), 0, 3000);
            Assert.InRange(Math.Abs(d.Start.Y), 0, 3000);
        });
    }
    [Theory]
    [InlineData(1500, "G400-1")]
    [InlineData(1650, "G500-2")]
    [InlineData(1700, "G600-3")]
    [InlineData(1750, "G800-1")]
    [InlineData(1800, "G800-2")]
    public void OpenBaseOutlineResolvesBeforeTitlesAreAdded(double width, string code)
    {
        var outline = new PolylinePrimitive("base", "Geometry/BaseOutline", "Plan", "Base", null, null,
            [new(0, 0), new(width, 0), new(width, 1700)], false);
        var package = new DrawingPackage(new string('a', 64), "test",
            [new ViewDefinition("Plan", ViewPurpose.Plan, new(0, 0), new(0, 0, width, 1700), 30)],
            [outline], new(0, 0, width, 1700), CalculationScope.FullRebar);
        Assert.Equal(code, Family21ManholeHelper.ResolveFromPackage(package));
    }
}
