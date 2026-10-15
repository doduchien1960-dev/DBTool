using DBTool.Domain.Geometry;
using DBTool.Domain;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class Phase2BTests
{
    [Fact]
    public void MinimaRotationAndModelLiftAreOneTransform()
    {
        var bounds = new Bounds2Mm(-10,-20,90,180);
        var transform = new ViewTransform(new(15,25),30,true);
        Assert.Equal(new Point2Mm(15,25), transform.Apply(new(-10,180),bounds));
        Assert.Equal(new Point2Mm(650,750), transform.ToModel(30,200).Apply(new(-10,180),bounds));
        Assert.Equal(1,transform.ToModel(30).Scale);
    }

    [Fact]
    public void TableCoordinatesSurviveJson()
    {
        var view = new ViewDefinition("table",ViewPurpose.SteelTable,new(0,0),new Bounds2Mm(0,0,300,300),30);
        var row = new TableRowPrimitive("r","Schedule/Row",view.Id,null,null,null,"1",
            [new TableCell("qty",TableCellKind.Number,null,12)], placements:[new("qty",new(10,90),60)]);
        var package = new DrawingPackage(new string('a',64),"test",[view],[row],view.Bounds,CalculationScope.Full);
        var read = DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(package));
        Assert.Equal(row.Placements.ToArray(), Assert.IsType<TableRowPrimitive>(Assert.Single(read.Primitives)).Placements.ToArray());
    }
    [Fact]
    public void PrintablePlacementIncludesMargins()
    {
        var view = new ViewDefinition("v", ViewPurpose.Plan, new(0,0), new Bounds2Mm(-100,-200,100,200),30);
        var package = new DrawingPackage(new string('a',64), "test", [view], [], view.Bounds, CalculationScope.Full);
        var profile = new PresentationProfile(420,297,10,15,10,10,0,0,[30],30,[2],"test");
        var frame = SheetLayoutPlanner.Arrange(package,profile).Value!.Sheets[0].Frames[0];
        Assert.Equal(10,frame.PaperBounds.MinX);
        Assert.Equal(15,frame.PaperBounds.MinY);
    }

    [Fact]
    public void BaseMeshRejectsForeignComponent()
    {
        var view = new ViewDefinition("BaseTop",ViewPurpose.BaseMesh,new(0,0),new Bounds2Mm(0,0,100,100),30);
        var primitive = new PolylinePrimitive("wall","Rebar/Top",view.Id,"Wall1",null,null,[new(0,0),new(50,50)],false);
        Assert.False(DrawingPackageValidator.ValidatePrimitives([view],[primitive]).Success);
    }
}
