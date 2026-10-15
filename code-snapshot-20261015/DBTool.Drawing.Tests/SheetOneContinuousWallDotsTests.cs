using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneContinuousWallDotsTests
{
    [Theory]
    [InlineData("Wall/T3",-730,35)]
    [InlineData("Wall/T3",-670,181.111111111111)]
    [InlineData("Wall/T1",730,327.222222222222)]
    public void ClippedParentAcrossThePlaneDoesNotInventSteelInsideTheOpening(string wall,double face,double z)
    {
        var bars=new[]{Bar("left","parent",wall,-615,-100,face,z),Bar("right","parent",wall,100,615,face,z)};
        var layout=new RebarLayout(bars,[],CalculationScope.FullRebar);
        var original=layout.Occurrences.ToArray();
        var dots=SheetOneContinuousWallDots.Build(layout,bars.ToDictionary(b=>b.Id,b=>"T1"),Section(wall,face)).Cast<CirclePrimitive>().ToArray();
        Assert.Empty(dots);
        var aliases=SheetOneContinuousWallDots.RenderAliases(dots);
        Assert.Empty(aliases);
        Assert.Equal(original,layout.Occurrences.ToArray());
    }
    [Fact]
    public void MissingParentOneSidedDifferentStationsAndExistingCrossingDoNotInventDots()
    {
        BarOccurrence[] Pair(string? a,string? b,double y1=-730,double y2=-730)=>
            [Bar("left",a,"Wall/T3",-615,-100,y1,35),Bar("right",b,"Wall/T3",100,615,y2,35)];
        var cases=new[]{Pair(null,null),Pair("a","b"),Pair("p","p",-730,-670),
            new[]{Bar("left","p","Wall/T3",-615,-100,-730,35)},
            new[]{Bar("cross","p","Wall/T3",-615,100,-730,35),Bar("right","p","Wall/T3",100,615,-730,35)}};
        foreach(var bars in cases)
            Assert.Empty(SheetOneContinuousWallDots.Build(new(bars,[],CalculationScope.FullRebar),bars.ToDictionary(b=>b.Id,b=>"T"),Section("Wall/T3",-730)));
        var pair=Pair("p","p");var layout=new RebarLayout(pair,[],CalculationScope.FullRebar);
        Assert.Empty(SheetOneContinuousWallDots.Build(layout,pair.ToDictionary(b=>b.Id,b=>"T"),null));
        Assert.Empty(SheetOneContinuousWallDots.Build(layout,pair.ToDictionary(b=>b.Id,b=>"T"),Section("Wall/T1",730)));
    }
    [Fact]
    public void LegacyOtherViewsAndNearSymbolsDoNotShareNativeInk()
    {
        CirclePrimitive Dot(string id,string role,string view,double x)=>new(id,role,view,"Wall/T3",id,"T",new(x,35),5);
        DrawingPrimitive[] p=[Dot("a",SheetOneContinuousWallDots.Role,"SteelAA",-730),Dot("near",SheetOneContinuousWallDots.Role,"SteelAA",-730.000000001),
            Dot("b",SheetOneRebarDotPolicy.Role,"SteelAA",-730),Dot("c",SheetOneContinuousWallDots.Role,"SteelBB",-730)];
        Assert.Empty(SheetOneContinuousWallDots.RenderAliases(p));
    }
    private static ViewProfile Section(string wall,double face)
    {
        var footprint=new Footprint(new Polygon2Mm([new(face-35,0),new(face+35,0),new(face+35,1400),new(face-35,1400)]),[]);
        return new("AA",ViewPurpose.Section,[new MaterialRegion("wall",wall,"Concrete",footprint,[])],[],[]);
    }
    private static BarOccurrence Bar(string id,string? parent,string wall,double x0,double x1,double face,double z)=>
        new(id,parent,wall,ComponentKind.Wall,Layer.Top,Axis.X,10,
            new BarPath([new LineSegment3Mm(new(x0,face,z),new(x1,face,z))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
            x1-x0,x1-x0,LengthStatus.Known,"TEST",[],RebarDirection.Horizontal);
}
