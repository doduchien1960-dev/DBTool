using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoMaterialViewsTests
{
    [Fact]
    public void MortarHatchesRequirePhysicalConnectedMortarAndTranslatePublishedPatternOrigin()
    {
        var geometry=Geometry();var wall=geometry.Components.Single(c=>c.Id=="Wall/T1");
        var mortar=new ComponentGeometry("Wall/Mortar",ComponentKind.Wall,"BeddingMortar",wall.Footprint,
            new(1390,1400),wall.Occupied,[],[],[]);
        var components=geometry.Components.Select(c=>c.Id.StartsWith("Wall/")?
            new ComponentGeometry(c.Id,c.Kind,c.MaterialId,c.Footprint,new(0,1390),c.Occupied,[],[],[]):c);
        var g=new GeometryModel(components.Append(mortar),[]);
        string[] views=["Reinforcement/Wall/T1/Wall/T1/Outer/Top","Reinforcement/Wall/T1/Wall/T1/Inner/Bottom",
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top","Reinforcement/Wall/T2/Wall/T2/Inner/Bottom",
            SheetThreeRoofViews.TopViewId,SheetThreeRoofViews.BottomViewId];
        var result=SheetTwoMortar.Build(g,views);
        Assert.Equal(4,result.Count);
        Assert.Equal(new Point2Mm[]{new(-650,1400),new(650,1400),new(650,1390),new(-650,1390)},result[0].OuterLoop.ToArray());
        Assert.All(result,h=>
        {
            var slot=SheetTwoPlacement.Origin(h.ViewId)!.Value;
            var origin=h.PatternOrigin!.Value;
            Assert.Equal(-14403.50952778792,origin.X+slot.X,7);
            Assert.Equal(72736.52203702747,origin.Y+slot.Y,7);
        });
        Assert.All(result,h=>{Assert.True(h.NativeBoundaryLoops);Assert.Equal(SheetTwoMortar.Role,h.Role);});
        var boundaries=SheetTwoMortar.Boundaries(result);
        Assert.Equal(4,boundaries.Count);
        Assert.Equal(new Point2Mm[]{new(-650,1390),new(650,1390)},boundaries[0].Points.ToArray());
        Assert.Equal("VHG-Dim",SemanticRoleRegistry.Resolve(boundaries[0].Role)!.Layer);
        Assert.Empty(SheetTwoMortar.Build(geometry,views));
        Assert.Empty(SheetTwoMortar.Build(g,["BaseTop","AA","Reinforcement/Wall/T1/Wall/T1/Default/Bottom"]));
    }
    [Fact]
    public void HiddenMeshOutlinesFollowRoofChamberAndPhysicalHaunch()
    {
        var geometry=Geometry();var foot=geometry.Components.Single(c=>c.Id=="Base").Footprint;
        var haunch=new ComponentGeometry("Base/Haunch",ComponentKind.Base,"Concrete",foot,new(0,80),
            new HaunchSolid(1000,1250,60,80,HaunchOwner.Base,new(0,80)),[],[],[]);
        var withHaunch=new GeometryModel(geometry.Components.Append(haunch),[]);
        string[] views=["BaseTop","BaseBottom","Reinforcement/Wall/T1/Wall/T1/Outer/Top",
            "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom","Reinforcement/Wall/T2/Wall/T2/Outer/Top",
            "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom",SheetThreeRoofViews.TopViewId,SheetThreeRoofViews.BottomViewId];
        var result=SheetTwoHiddenEdges.Build(withHaunch,views).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(8,result.Length);
        var wall=Assert.Single(result,p=>p.ViewId==views[2]);
        Assert.Equal(new Point2Mm[]{new(-500,1400),new(-500,80),new(-440,0),new(440,0),new(500,80),new(500,1400)},wall.Points.ToArray());
        var baseInner=Assert.Single(result,p=>p.ViewId=="BaseTop" && p.SemanticId.EndsWith("Chamber"));
        Assert.Equal(new Point2Mm(-500,625),baseInner.Points[0]);
        Assert.Equal(baseInner.Points[0],baseInner.Points[^1]);
        Assert.All(result,p=>{Assert.False(p.Closed);Assert.Null(p.OccurrenceId);});
        Assert.Empty(SheetTwoHiddenEdges.Build(withHaunch,["AA","Plan","Reinforcement/Wall/T1/Wall/T1/Default/Bottom"]));
        Assert.Empty(SheetTwoHiddenEdges.Build(new GeometryModel(geometry.Components.Where(c=>c.Id!="Wall/T2"),[]),views));
        Assert.Equal(4,SheetTwoHiddenEdges.Build(geometry,views).Count);
    }
    [Theory]
    [InlineData("Wall/T1",1300,1500)]
    [InlineData("Wall/T2",1550,1750)]
    public void MaterialContextUsesPhysicalSlabExtentsAndHeightsWithoutChangingBarModel(string wall,double roofWidth,double baseWidth)
    {
        var geometry=Geometry();
        string view=$"Reinforcement/{wall}/{wall}/Outer/Top";
        var result=SheetTwoMaterialViews.Build(geometry,[view]).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(2,result.Length);
        var outline=Assert.Single(result,p=>p.Role==SheetTwoMaterialViews.WallRole);
        Assert.True(outline.Closed);Assert.Equal(8,outline.Points.Length);
        Assert.Equal(new Point2Mm(-roofWidth/2,1550),outline.Points[0]);
        Assert.Contains(new Point2Mm(-baseWidth/2,-150),outline.Points);
        Assert.Contains(new Point2Mm(baseWidth/2,-150),outline.Points);
        var junction=Assert.Single(result,p=>p.Role==SheetTwoMaterialViews.RoofRole);
        Assert.Equal(new Point2Mm[]{new(-roofWidth/2,1400),new(roofWidth/2,1400)},junction.Points.ToArray());
        Assert.All(result,p=>{Assert.Equal(view,p.ViewId);Assert.Equal(wall,p.ComponentId);Assert.Null(p.OccurrenceId);});
        Assert.Equal(-150,geometry.Components.Single(c=>c.Id=="Base").ZRange.Start);
    }
    [Fact]
    public void MissingWallUnknownAndAccessoryViewsDoNotCreateInventedContexts()
    {
        Assert.Empty(SheetTwoMaterialViews.Build(Geometry(),["Plan","BaseTop","Reinforcement/Wall/T1/Wall/T1/Default/Bottom"]));
        var g=Geometry();var missing=new GeometryModel(g.Components.Where(c=>c.Id!="Wall/T1"),[]);
        Assert.Empty(SheetTwoMaterialViews.Build(missing,["Reinforcement/Wall/T1/Wall/T1/Outer/Top"]));
    }
    [Fact]
    public void BaseOutlineRewindingKeepsEveryCornerAndOnlyAppliesToTheDeclaredFamily()
    {
        var p=new PolylinePrimitive("base","Geometry/BaseOutline","BaseTop","Base",null,null,
            [new(-750,-875),new(750,-875),new(750,875),new(-750,875)],true);
        var presentation=new PresentationRequest(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
        var result=Assert.IsType<PolylinePrimitive>(SheetTwoMaterialViews.Apply(p,presentation));
        Assert.Equal(p.SemanticId,result.SemanticId);Assert.False(result.Closed);
        Assert.Equal(new Point2Mm(-750,875),result.Points[0]);Assert.Equal(result.Points[0],result.Points[^1]);
        Assert.True(p.Points.ToHashSet().SetEquals(result.Points));
        Assert.Same(p,SheetTwoMaterialViews.Apply(p,new(30,ElevationMode.Symbolic,null,"Default",true)));
    }
    private static GeometryModel Geometry()
    {
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        ComponentGeometry Rect(string id,ComponentKind kind,double x0,double y0,double x1,double y1,double z0,double z1)
        {
            var foot=new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
            return new(id,kind,"Concrete",foot,new(z0,z1),new ExtrusionSolid(foot,frame,new(z0,z1)),[],[],[]);
        }
        return new([Rect("Base",ComponentKind.Base,-750,-875,750,875,-150,0),
            Rect("Roof",ComponentKind.Roof,-650,-775,650,775,1400,1550),
            Rect("Wall/T1",ComponentKind.Wall,-650,625,650,775,0,1400),
            Rect("Wall/T2",ComponentKind.Wall,500,-625,650,625,0,1400),
            Rect("Wall/T3",ComponentKind.Wall,-650,-775,650,-625,0,1400),
            Rect("Wall/T4",ComponentKind.Wall,-650,-625,-500,625,0,1400)],[]);
    }
}
