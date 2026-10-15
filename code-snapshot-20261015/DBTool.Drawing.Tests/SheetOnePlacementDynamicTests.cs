using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public sealed class SheetOnePlacementDynamicTests
{
    [Fact]
    public void NativeFrameMarginMovesTheWholeGroupWithoutChangingRelativePlacement()
    {
        var views=new[]{"AA","SteelBB"}.Select(id=>new ViewDefinition(id,ViewPurpose.Section,
            new(0,0),new Bounds2Mm(-1700,-500,1200,2000),30)).ToArray();
        var package=new DrawingPackage(new string('a',64),"test",views,[],views[0].Bounds,CalculationScope.FullRebar);
        var profile=new PresentationProfile(420,297,25,10,10,10,0,5,[30],30,[2],"native-frame");
        var result=SheetOnePlacement.Arrange(package,profile,5);
        Assert.True(result.Success);
        var frames=result.Value!.Frames;
        Assert.All(frames,f=>Assert.InRange(f.PaperBounds.MinX,25,410));
        Assert.Equal((10075d-2295)/30,frames[1].Transform.Origin.X-frames[0].Transform.Origin.X,8);
        Assert.All(frames,f=>Assert.Equal(30,f.Transform.UniformScaleDenominator));
        Assert.All(frames,f=>Assert.Equal(views[0].Bounds,f.LogicalBounds));
        double expectedShift=25*30-(2295-1700);
        foreach(var frame in frames)
        {
            var oldTitle=SheetOnePlacement.Title(frame.ViewId,package)!.Value;
            var title=SheetOnePlacement.Title(frame,package)!.Value;
            Assert.Equal(oldTitle.X+expectedShift,title.X,8);
            Assert.Equal(oldTitle.Y,title.Y,8);
        }
    }
    [Fact]
    public void InterruptedBasePerimeterKeepsEverySheetDatum()
    {
        var seed=Package(1650,1850,2130);
        var presentation=new PresentationRequest(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
        PolylinePrimitive Pipe(int sign)=>new($"Plan/p/Stub/VisibleOuter/{sign}","Geometry/PlanPipeVisible","Plan","Wall/T1",null,null,
            [new(sign*255,825),new(sign*255,1225)],false);
        var primitives=SheetOnePlanBaseOutlinePolicy.ClipAtPipes(seed.Primitives
            .Select(p=>SheetOnePlanBaseOutlinePolicy.Apply(p,presentation)).Concat([Pipe(-1),Pipe(1)]),presentation);
        var split=new DrawingPackage(seed.InputHash,seed.RuleVersion,seed.Views,primitives,seed.Bounds,seed.Scope);
        foreach(string view in new[]{"Plan","AA","BB","SteelAA","SteelBB","WallMeshPlan"})
        {
            Assert.Equal(SheetOnePlacement.Origin(view,seed),SheetOnePlacement.Origin(view,split));
            Assert.Equal(SheetOnePlacement.Title(view,seed),SheetOnePlacement.Title(view,split));
        }
    }

    [Fact]
    public void MaterialLeaderDoesNotMoveThePhysicalPadDatum()
    {
        var seed=Package(1650,1850,2130);
        var leader=new PolylinePrimitive("BB/MaterialLeader/Pad","Material/Leader","BB","Pad",null,null,
            [new(0,-200),new(0,-850),new(400,-850)],false);
        var annotated=new DrawingPackage(seed.InputHash,seed.RuleVersion,seed.Views,seed.Primitives.Append(leader),seed.Bounds,seed.Scope);
        Assert.Equal(SheetOnePlacement.Origin("AA",seed),SheetOnePlacement.Origin("AA",annotated));
        Assert.Equal(new Point2Mm(2345,1530),SheetOnePlacement.Origin("AA",annotated));
    }
    [Theory]
    [InlineData(1500,1750,2030,2295,6185,10075,4550,1730)]
    [InlineData(1650,1850,2130,2345,6335,10325,4450,1530)]
    [InlineData(1700,1950,2080,2395,6485,10575,4500,1630)]
    public void TitlesAndSectionDatumsFollowExtentsRatherThanManholeCode(double width,double length,double top,
        double x0,double x1,double x2,double titleBottom,double floorBottom)
    {
        var shape=Package(width,length,top);
        Assert.Equal(new Point2Mm(x0,8610),SheetOnePlacement.Title("Plan",shape));
        Assert.Equal(new Point2Mm(x1,titleBottom),SheetOnePlacement.Title("WallMeshPlan",shape));
        Assert.Equal(new Point2Mm(x2,7830-top),SheetOnePlacement.Origin("SteelAA",shape));
        Assert.Equal(new Point2Mm(x0,floorBottom),SheetOnePlacement.Origin("AA",shape));
        Assert.Equal(new Point2Mm(x0,7530-length/2),SheetOnePlacement.Origin("Plan",shape));
    }
    private static DrawingPackage Package(double width,double length,double top)
    {
        var baseOutline=new PolylinePrimitive("Plan/Base/Outer","Geometry/Material","Plan","Base",null,null,
            [new(-width/2,-length/2),new(width/2,-length/2),new(width/2,length/2),new(-width/2,length/2)],true);
        var pad=new PolylinePrimitive("BB/Pad/Outer","Geometry/Material","BB","Pad",null,null,[new(-width/2,-250),new(width/2,-250),new(width/2,-150),new(-width/2,-150)],true);
        var cap=new BlockPrimitive("BB/Elevation/NeckSeat","Elevation/SheetOneCap","BB","Neck","Block:CT",new(0,top),.3,[new("CT","Z2")]);
        var bounds=new Bounds2Mm(-width/2,-250,width/2,top);
        return new(new string('a',64),"test",[new ViewDefinition("Plan",ViewPurpose.Plan,new(0,0),bounds,30),new ViewDefinition("BB",ViewPurpose.Section,new(0,0),bounds,30)],
            [baseOutline,pad,cap],bounds,CalculationScope.FullRebar);
    }
}
