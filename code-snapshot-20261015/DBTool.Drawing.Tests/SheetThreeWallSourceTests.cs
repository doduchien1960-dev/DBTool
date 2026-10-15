using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class SheetThreeWallSourceTests
{
    [Theory]
    [InlineData("G400-1")]
    [InlineData("G500-2")]
    [InlineData("CUSTOM")]
    public void WallThreeKeepsItsOwnPhysicalProjectionAndNeverClonesWallOne(string name)
    {
        var one=new PolylinePrimitive("one","Rebar/Main","Reinforcement/Wall/T1/Wall/T1/Outer/Top","Wall/T1","bar-one","D1",[new(0,0),new(100,0)],false);
        var three=new PolylinePrimitive("three","Rebar/Main","Reinforcement/Wall/T3/Wall/T3/Outer/Top","Wall/T3","bar-three","D3",[new(30,0),new(830,0)],false);
        var result=SheetThreeWallViews.Build([one,three],name);
        Assert.Same(three,Assert.Single(result));Assert.Equal("bar-three",result[0].OccurrenceId);
        Assert.Equal("D3",result[0].ScheduleMark);
    }
    [Fact]
    public void MissingWallThreeCannotBeManufacturedFromWallOne()
    {
        var one=new PolylinePrimitive("one","Rebar/Main","Reinforcement/Wall/T1/Wall/T1/Outer/Top","Wall/T1","bar-one","D1",[new(0,0),new(100,0)],false);
        Assert.Empty(SheetThreeWallViews.Build([one],"G400-1"));
    }
}
