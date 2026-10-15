using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneConcreteAssemblyTests
{
    [Fact]
    public void PipeSplittingUpperWallDoesNotRestoreInterfacesAtTheFloor()
    {
        var parts=Parts("BB",0,1250);
        var lower=Rect("right","BB","Wall/T1",625,0,775,170);
        var upper=Rect("upper","BB","Wall/T1",625,890,775,1385);
        var input=parts.Select(p=>p.SemanticId=="right"?lower:p).Append(upper).ToArray();
        var result=SheetOneConcreteAssembly.Build(input);
        Assert.Equal(4,result.Paths.Count);
        Assert.False(result.Paths.ContainsKey("upper"));
        Assert.Equal(new Point2Mm(625,100),result.Paths["right"][0][0]);
        Assert.Equal(new Point2Mm(625,170),result.Paths["right"][0][1]);
        Assert.Single(result.Aliases);
    }
    [Theory]
    [InlineData("AA",0,1250)]
    [InlineData("BB",250,1000)]
    [InlineData("SteelAA",-500,1400)]
    [InlineData("SteelBB",100,1600)]
    public void ConnectedConcreteKeepsExposedBoundaryAndRemovesOnlyInternalInterfaces(string view,double x,double width)
    {
        var p=Parts(view,x,width); var before=p.ToArray();
        var result=SheetOneConcreteAssembly.Build(p);
        Assert.Equal(4,result.Paths.Count); Assert.Single(result.Aliases);
        Assert.Equal("a",result.Aliases["b"]);
        var h=width/2;
        Assert.Equal(new Point2Mm[]{new(x-h-150,0),new(x-h-250,0),new(x-h-250,-150),
            new(x+h+250,-150),new(x+h+250,0),new(x+h+150,0)},Assert.Single(result.Paths["base"]));
        Assert.Equal(new Point2Mm[]{new(x-h,100),new(x-h+100,0),new(x+h-100,0),new(x+h,100)},
            Assert.Single(result.Paths["a"]));
        var segments=result.Paths.Values.SelectMany(paths=>paths).SelectMany(path=>path.Zip(path.Skip(1))).ToArray();
        Assert.DoesNotContain(segments,s=>s.First.Y==0 && s.Second.Y==0 &&
            Math.Min(s.First.X,s.Second.X)<=x-h && Math.Max(s.First.X,s.Second.X)>=x-h+100);
        Assert.DoesNotContain(segments,s=>s.First.X==x-h && s.Second.X==x-h &&
            Math.Min(s.First.Y,s.Second.Y)<100);
        Assert.All(segments,s=>Assert.NotEqual(s.First,s.Second));
        Assert.Equal(before,p); // Original material extents and annotation inputs do not change.
    }
    [Fact]
    public void IncompleteDetachedWrongViewAndNonPresentationRegionsKeepTheirOriginalOutlines()
    {
        var p=Parts("AA",0,1250);
        Assert.Empty(SheetOneConcreteAssembly.Build(p.Take(4)).Paths);
        var detached=p.Select(q=>q.SemanticId=="left"?Rect("left","AA","Wall/T3",-775,1,-625,1400):q);
        Assert.Empty(SheetOneConcreteAssembly.Build(detached).Paths);
        Assert.Empty(SheetOneConcreteAssembly.Build(Parts("Plan",0,1250)).Paths);
        var ordinary=p.Select(q=>new PolylinePrimitive(q.SemanticId,"Geometry/Material",q.ViewId,q.ComponentId,null,null,q.Points,true));
        Assert.Empty(SheetOneConcreteAssembly.Build(ordinary).Paths);
    }
    [Fact]
    public void ReversedHaunchOrderStillMapsToAnAlreadyRenderedRepresentative()
    {
        var p=Parts("AA",0,1250); var reversed=p.Take(3).Concat(p.Skip(3).Reverse()).ToArray();
        var result=SheetOneConcreteAssembly.Build(reversed);
        Assert.Equal("b",result.Aliases["a"]);Assert.True(result.Paths.ContainsKey("b"));
    }
    private static PolylinePrimitive[] Parts(string view,double x,double width)
    {
        var h=width/2;
        return [Rect("base",view,"Base",x-h-250,-150,x+h+250,0),
            Rect("left",view,"Wall/T3",x-h-150,0,x-h,1385),Rect("right",view,"Wall/T1",x+h,0,x+h+150,1385),
            new("a",SheetOneSectionOutlinePolicy.Role,view,"Base/Haunch",null,null,
                [new(x-h,0),new(x-h+100,0),new(x-h,100)],true),
            new("b",SheetOneSectionOutlinePolicy.Role,view,"Base/Haunch",null,null,
                [new(x+h-100,0),new(x+h,0),new(x+h,100)],true)];
    }
    private static PolylinePrimitive Rect(string id,string view,string component,double x0,double y0,double x1,double y1)=>
        new(id,SheetOneSectionOutlinePolicy.Role,view,component,null,null,[new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)],true);
}
