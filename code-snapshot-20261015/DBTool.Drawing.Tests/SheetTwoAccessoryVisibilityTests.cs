using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class SheetTwoAccessoryVisibilityTests
{
    [Fact]
    public void OnlyTypedLadderAndDowelsInSupportedNominalPanelsAreOmitted()
    {
        const string view="Reinforcement/Wall/T2/Wall/T2/Inner/Bottom";
        PolylinePrimitive Line(string id,string role,string v,string component)=>new(id,role,v,component,id,"D1",[new(0,0),new(100,0)],false);
        var grid=Line("grid",SheetTwoNominalWallMesh.Role,view,"Wall/T2");
        var ladder=Line("ladder","Rebar/Main",view,"Wall/T2");
        var dowel=Line("dowel","Rebar/Main","Reinforcement/Wall/T2/Wall/T2/Default/Bottom","Wall/T2");
        var section=Line("section","Rebar/Main","SteelAA","Wall/T2");
        var other=Line("other","Rebar/Main",view,"Wall/T2");
        var unknown=Line("unknown","Rebar/Main","Reinforcement/Wall/T3/Wall/T3/Inner/Bottom","Wall/T3");
        var families=new Dictionary<string,string?>{{"ladder","Ladder"},{"dowel","Dowel"},{"section","Ladder"},{"other","Wall"},{"unknown","Ladder"}};
        var input=new DrawingPrimitive[]{grid,ladder,dowel,section,other,unknown};
        Assert.Equal(new DrawingPrimitive[]{grid,section,other,unknown},SheetTwoAccessoryVisibility.Apply(input,families));
        Assert.Equal(input,SheetTwoAccessoryVisibility.Apply(input,new Dictionary<string,string?>()));
        Assert.Equal(new DrawingPrimitive[]{ladder,dowel},SheetTwoAccessoryVisibility.Apply([ladder,dowel],families));
    }
}
