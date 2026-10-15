using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoPlacementTests
{
    [Fact]
    public void TemplateSlotPlannerPreservesSharedPanelOwnershipAndBlocksOffPaperOrUnknownViews()
    {
        var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test");
        var main="Reinforcement/Wall/T1/Wall/T1/Outer/Top";
        var accessory="Reinforcement/Wall/T1/Wall/T1/Default/Bottom";
        var views=new[]{new ViewDefinition(main,ViewPurpose.Section,new(0,0),new(-1100,-1000,1000,2000),30),
            new ViewDefinition(accessory,ViewPurpose.Section,new(0,0),new(-250,1050,250,1550),30)};
        DrawingPackage Package(IEnumerable<ViewDefinition> v)=>new(new string('a',64),"test",v,[],null,CalculationScope.FullRebar);
        var result=SheetTwoPlacement.Arrange(Package(views),profile,5);
        Assert.True(result.Success); Assert.Equal(2,result.Value!.Frames.Length);
        Assert.All(result.Value.Frames,f=>Assert.Equal(SheetTwoPlacement.Origin(main)!.Value,
            f.Transform.ToModel(30).Apply(new(0,0),f.LogicalBounds)));
        Assert.False(SheetTwoPlacement.Arrange(Package([new(main,ViewPurpose.Section,new(0,0),new(-6000,0,1000,2000),30)]),profile,5).Success);
        Assert.False(SheetTwoPlacement.Arrange(Package([new("Unknown",ViewPurpose.Section,new(0,0),new(0,0,100,100),30)]),profile,5).Success);
    }
    [Fact]
    public void SixPanelsHaveSourceColumnRowOrderAndCorrectLocalOriginWithoutLosingPhysicalViews()
    {
        string[] ids=["BaseTop","BaseBottom","Reinforcement/Wall/T1/Wall/T1/Outer/Top",
            "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom","Reinforcement/Wall/T2/Wall/T2/Outer/Top",
            "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom","Reinforcement/Wall/T1/Wall/T1/Default/Bottom"];
        var frames=ids.Select(id=>new SheetViewFrame(id,ViewPurpose.BaseMesh,new(-750,-875,750,875),
            new(10,10,60,70),new(new(10,10),30),id,[])).ToArray();
        var sheet=new SheetLayoutSheet(2,5,new(0,0,420,297),frames);
        var result=SheetTwoPlacement.Apply(sheet);
        Assert.Equal(ids,result.Frames.Select(f=>f.ViewId));
        foreach(var f in result.Frames)
        {
            var point=SheetTwoPlacement.Origin(f.ViewId)!.Value;
            Assert.Equal(point,f.Transform.ToModel(30).Apply(new(0,0),f.LogicalBounds));
            Assert.Equal(frames.Single(q=>q.ViewId==f.ViewId).LogicalBounds,f.LogicalBounds);
        }
        Assert.Equal(SheetTwoPlacement.Origin(ids[2]),SheetTwoPlacement.Origin(ids[6]));
        Assert.Null(SheetTwoPlacement.Title(ids[6]));
        Assert.Equal(6,ids.Count(id=>SheetTwoPlacement.Title(id) is not null));
        Assert.True(SheetTwoPlacement.Title(ids[0])!.Value.Y>SheetTwoPlacement.Title(ids[1])!.Value.Y);
        Assert.True(SheetTwoPlacement.Title(ids[2])!.Value.X<SheetTwoPlacement.Title(ids[4])!.Value.X);
    }
    [Fact]
    public void OtherPagesAndUnknownViewsKeepTheirPlacement()
    {
        var f=new SheetViewFrame("unrelated",ViewPurpose.Plan,new(0,0,100,100),new(1,1,10,10),new(new(1,1),30),"Unrelated",[]);
        var first=new SheetLayoutSheet(1,5,new(0,0,420,297),[f]);
        Assert.Same(first,SheetTwoPlacement.Apply(first));
        Assert.Same(f,Assert.Single(SheetTwoPlacement.Apply(new(2,5,first.PrintableBounds,[f])).Frames));
    }
}
