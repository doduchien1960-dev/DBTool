using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetMeshDynamicPlacementTests
{
    [Theory]
    [InlineData(1500,1750,2030,1550,2295,3390,4550,6835,6160)]
    [InlineData(1650,1850,2130,1600,2345,3490,4450,6785,6110)]
    [InlineData(1700,1950,2080,1550,2395,3590,4500,6735,6160)]
    public void MeshSlotsFollowPhysicalDatumsAcrossDifferentSizes(double width,double length,double cap,double roof,
        double first,double pitch,double bottomTitle,double baseY,double wallY)
    {
        PolylinePrimitive Rectangle(string id,string view,string owner,double x0,double y0,double x1,double y1)=>
            new(id,"Geometry/Material",view,owner,null,null,[new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)],true);
        var bounds=new Bounds2Mm(-2000,-2000,2000,3000);
        var package=new DrawingPackage(new string('a',64),"test",
            [new("Plan",ViewPurpose.Plan,new(0,0),bounds,30),new("BB",ViewPurpose.Section,new(0,0),bounds,30),
             new(SheetThreeRoofViews.TopViewId,ViewPurpose.Plan,new(0,0),new(-1200,-1300,1425,1685),30)],[
            Rectangle("base","Plan","Base",-width/2,-length/2,width/2,length/2),
            Rectangle("roof","BB","Roof",-width/2,roof-150,width/2,roof),
            Rectangle("roof-plan",SheetThreeRoofViews.TopViewId,"Roof",-(width-200)/2,-(length-200)/2,(width-200)/2,(length-200)/2),
            new BlockPrimitive("BB/Elevation/NeckSeat","Elevation/SheetOneFloor","BB","Neck","Block:CT",new(0,cap),.3,[])],
            bounds,CalculationScope.FullRebar);
        const string wall="Reinforcement/Wall/T1/Wall/T1/Outer/Top";
        const string lower="Reinforcement/Wall/T1/Wall/T1/Inner/Bottom";
        Assert.Equal(new Point2Mm(first,baseY),SheetMeshDynamicPlacement.Origin("BaseTop",SheetTwoPlacement.Origin("BaseTop"),package));
        Assert.Equal(new Point2Mm(first+pitch,wallY),SheetMeshDynamicPlacement.Origin(wall,SheetTwoPlacement.Origin(wall),package));
        Assert.Equal(new Point2Mm(first+pitch,wallY-(8610-bottomTitle)),SheetMeshDynamicPlacement.Origin(lower,SheetTwoPlacement.Origin(lower),package));
        Assert.Equal(new Point2Mm(first,bottomTitle),SheetMeshDynamicPlacement.Title(SheetTwoPlacement.Title("BaseBottom"),package));
        Assert.Equal(new Point2Mm(first,wallY),SheetMeshDynamicPlacement.Origin("Reinforcement/Wall/T3/Wall/T3/Outer/Top",SheetThreePlacement.Origin("Reinforcement/Wall/T3/Wall/T3/Outer/Top"),package));
        Assert.Equal(new Point2Mm(first+pitch,8610),SheetMeshDynamicPlacement.Title(SheetThreePlacement.Title("Reinforcement/Wall/T4/Wall/T4/Outer/Top"),package));
        if(width==1650)
        {
            var subset=new DrawingPackage(package.InputHash,package.RuleVersion,
                package.Views.Where(v=>v.Id==SheetThreeRoofViews.TopViewId),
                package.Primitives.Where(p=>p.ViewId==SheetThreeRoofViews.TopViewId),bounds,CalculationScope.FullRebar);
            var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test");
            Assert.False(SheetThreePlacement.Arrange(subset,profile,5).Success);
            var arranged=SheetThreePlacement.Arrange(subset,profile,5,package);
            Assert.True(arranged.Success);
            var frame=Assert.Single(arranged.Value!.Frames);
            Assert.Equal(new Point2Mm(first+2*pitch,6885),frame.Transform.ToModel(30).Apply(new(0,0),frame.LogicalBounds));
        }
    }
}
