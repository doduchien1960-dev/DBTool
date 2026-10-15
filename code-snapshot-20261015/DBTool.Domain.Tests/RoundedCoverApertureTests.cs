using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class RoundedCoverApertureTests
{
    [Fact]
    public void RoundedApertureKeepsItsCornerRadiusAndOffCentreOriginAfterRotation()
    {
        var profile=new RoundedRectangleProfile(new(0,21.8224943950918),new(820,570),20);
        var placement=new PurchasedCoverPlacement("ROUND-CORNERS",new(12,34,100),new(1050,745),1,90,
            new(-525,-398.1775056049082,525,346.8224943950918),[],profile);
        var world=Assert.IsType<RoundedRectangleProfile>(placement.WorldClearProfile);
        var center=world.Center;
        Assert.Equal(-9.8224943950918,center.X,6);Assert.Equal(34,center.Y,6);
        Assert.Equal(20,world.CornerRadiusMm);
        var bounds=world.AxisBounds;
        Assert.Equal(570,bounds.MaxX-bounds.MinX,6);
        Assert.Equal(820,bounds.MaxY-bounds.MinY,6);
    }
}
