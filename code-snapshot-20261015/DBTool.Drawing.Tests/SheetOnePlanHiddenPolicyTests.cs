using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOnePlanHiddenPolicyTests
{
    [Theory]
    [InlineData(1000,1250,0,0)]
    [InlineData(1150,1350,200,-100)]
    [InlineData(1200,1450,-200,250)]
    public void FourConnectedConcealedEdgesShareOneNativePerimeterAndKeepEveryTrace(double width,double length,double x,double y)
    {
        var old=Edges(width,length,x,y);
        var result=SheetOnePlanHiddenPolicy.Compose(old,Presentation).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(old.Select(p=>p.SemanticId),result.Select(p=>p.SemanticId));
        Assert.All(result,p=>{Assert.Equal(SheetOnePlanHiddenPolicy.WallRole,p.Role);Assert.False(p.Closed);});
        var tl=new Point2Mm(x-width/2,y+length/2);var tr=new Point2Mm(x+width/2,y+length/2);
        var br=new Point2Mm(x+width/2,y-length/2);var bl=new Point2Mm(x-width/2,y-length/2);
        Assert.All(result,p=>Assert.Equal(new[]{tl,tr,br,bl,tl},p.Points.ToArray()));
        for(int i=0;i<4;i++){Assert.Equal(old[i].ComponentId,result[i].ComponentId);Assert.Equal(old[i].OccurrenceId,result[i].OccurrenceId);}
        var aliases=SheetOnePlanHiddenPolicy.RenderAliases(result);
        Assert.Equal(3,aliases.Count);Assert.All(aliases,p=>Assert.Equal(old[0].SemanticId,p.Value));
    }
    [Fact]
    public void ConcealedPipeEdgesDoNotPreventWallPerimeterComposition()
    {
        var pipe=new PolylinePrimitive("pipe","Geometry/PlanPipeHidden","Plan","Wall/T1",null,null,
            [new(-200,625),new(-200,1175)],false);
        var result=SheetOnePlanHiddenPolicy.Compose([..Edges(1000,1250,0,0),pipe],Presentation);
        Assert.Equal(pipe,result.Single(p=>p.SemanticId=="pipe"));
        Assert.Equal(3,SheetOnePlanHiddenPolicy.RenderAliases(result).Count);
    }
    [Fact]
    public void HoleKeepsItsCornersIdentityAndClosureButUsesSourceDirection()
    {
        var old=new PolylinePrimitive("hole","Geometry/PlanHidden","Plan","Roof",null,null,
            [new(-200,-625),new(500,-625),new(500,75),new(-200,75)],true);
        var result=Assert.IsType<PolylinePrimitive>(Assert.Single(SheetOnePlanHiddenPolicy.Compose([old],Presentation)));
        Assert.Equal(SheetOnePlanHiddenPolicy.HoleRole,result.Role);Assert.True(result.Closed);
        Assert.Equal(old.SemanticId,result.SemanticId);Assert.Equal(old.ComponentId,result.ComponentId);
        Assert.Equal(new Point2Mm[]{new(-200,75),new(500,75),new(500,-625),new(-200,-625)},result.Points.ToArray());
        Assert.Empty(SheetOnePlanHiddenPolicy.RenderAliases([result]));
    }
    [Fact]
    public void IncompleteDisconnectedAndOtherProfilesRetainOriginalEdges()
    {
        var edges=Edges(1000,1250,0,0);
        Assert.Equal(edges,SheetOnePlanHiddenPolicy.Compose(edges,new(30,ElevationMode.Symbolic,null,"Default",true)));
        Assert.Equal(edges.Take(3),SheetOnePlanHiddenPolicy.Compose(edges.Take(3),Presentation));
        var disconnected=edges.ToArray();
        disconnected[3]=new("broken","Geometry/PlanHidden","Plan","Wall/T4",null,null,[new(-500,-600),new(-500,625)],false);
        Assert.Equal(disconnected,SheetOnePlanHiddenPolicy.Compose(disconnected,Presentation));
    }
    [Fact]
    public void AliasesRequireExactExplicitPerimeterAndDoNotMergeNearEdgesOrOtherRoles()
    {
        var points=new Point2Mm[]{new(-500,625),new(500,625),new(500,-625),new(-500,-625),new(-500,625)};
        PolylinePrimitive P(string id,string role,double offset=0)=>new(id,role,"Plan","Wall/T1",null,null,points.Select(p=>new Point2Mm(p.X+offset,p.Y)),false);
        var result=SheetOnePlanHiddenPolicy.RenderAliases([P("a",SheetOnePlanHiddenPolicy.WallRole),P("b",SheetOnePlanHiddenPolicy.WallRole),
            P("near",SheetOnePlanHiddenPolicy.WallRole,.0001),P("other","Geometry/PlanHidden")]);
        Assert.Equal("a",Assert.Single(result).Value);Assert.Equal("b",Assert.Single(result).Key);
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static PolylinePrimitive[] Edges(double width,double length,double x,double y)
    {
        Point2Mm[] p=[new(x-width/2,y+length/2),new(x+width/2,y+length/2),new(x+width/2,y-length/2),new(x-width/2,y-length/2)];
        return Enumerable.Range(0,4).Select(i=>new PolylinePrimitive($"edge/{i}","Geometry/PlanHidden","Plan",$"Wall/T{i+1}",null,null,[p[i],p[(i+1)%4]],false)).ToArray();
    }
}
