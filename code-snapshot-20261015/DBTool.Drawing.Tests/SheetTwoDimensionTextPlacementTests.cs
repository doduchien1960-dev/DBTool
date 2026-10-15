using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class SheetTwoDimensionTextPlacementTests
{
    [Theory]
    [InlineData("X/000",30,1461.142857142857,1510)]
    [InlineData("X/008",30,1658.857142857143,1510)]
    [InlineData("Y/000",30,1575,2013.857142857143)]
    [InlineData("Y/008",30,1575,1846.142857142857)]
    public void SourceCoverTextIsPlacedOutsideWithoutChangingMeasurement(string band,double measurement,double x,double y)
    {
        var d=new DimensionIntentPrimitive("BaseTop/Dimension/NominalSpacing/"+band,SheetTwoDimensionPlanner.Role,"BaseTop","Base","bar","D1",new(1545,1930),new(1575,1930),measurement,"<>",0,dimensionLinePoint:new(1575,1510));
        var p=SheetTwoDimensionTextPlacement.Position(d)!.Value;
        Assert.Equal(x,p.X,7);Assert.Equal(y,p.Y,7);
        Assert.Equal(30,d.LogicalMeasurementMm);
        var ordinary=new DimensionIntentPrimitive("dimension","Dimension/Linear","SteelAA","Base",null,null,new(0,0),new(30,0),30,"<>",0);
        Assert.Null(SheetTwoDimensionTextPlacement.Position(ordinary));
    }
}
