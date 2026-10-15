using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneDotRenderAliasTests
{
    private static CirclePrimitive Dot(string id,string view,Point2Mm center,double radius=5,string role=SheetOneRebarDotPolicy.Role) =>
        new(id,role,view,"Wall/T1",id,"T1",center,radius);

    [Fact]
    public void CoincidentWallMeshSymbolsShareInkWithoutLosingAnyPrimitiveIdentity()
    {
        DrawingPrimitive[] primitives=[Dot("a","WallMeshPlan",new(20,40)),Dot("b","WallMeshPlan",new(20,40),6),
            Dot("c","WallMeshPlan",new(20,40)),Dot("d","WallMeshPlan",new(30,40))];
        var aliases=SheetOneRebarDotPolicy.RenderAliases(primitives);
        Assert.Equal(2,aliases.Count);Assert.Equal("a",aliases["b"]);Assert.Equal("a",aliases["c"]);
        Assert.Equal(4,primitives.Length);Assert.Equal(4,primitives.Select(p=>p.SemanticId).Distinct().Count());
        Assert.All(primitives,p=>Assert.Equal(p.SemanticId,p.OccurrenceId));
        Assert.Equal(6,Assert.IsType<CirclePrimitive>(primitives[1]).RadiusMm);
    }

    [Fact]
    public void NearbySymbolsOtherViewsAndLegacyRolesAreNeverMerged()
    {
        DrawingPrimitive[] primitives=[Dot("a","WallMeshPlan",new(20,40)),Dot("near","WallMeshPlan",new(20.00000001,40)),
            Dot("bb1","SteelBB",new(20,40)),Dot("bb2","SteelBB",new(20,40)),
            Dot("old1","WallMeshPlan",new(20,40),role:"Rebar/Main"),Dot("old2","WallMeshPlan",new(20,40),role:"Rebar/Main")];
        Assert.Empty(SheetOneRebarDotPolicy.RenderAliases(primitives));
    }
}
