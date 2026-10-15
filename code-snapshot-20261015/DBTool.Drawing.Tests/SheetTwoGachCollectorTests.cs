using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoGachCollectorTests
{
    [Fact]
    public void ActualBaseOpeningRejectsMidpointGlyphAndConsumerReroutes()
    {
        var model=BaseOpening(new CircleProfile(new(20,60),10));
        var bar=Bar("a",0,0,100);
        var midpoint=new BlockPrimitive("midpoint",SheetTwoGach.Role,"BaseTop","Base","Block:Gach",new(0,50),.3,[]);
        Assert.Empty(WallOpeningSymbolClipper.Clip([midpoint],model,"Base").Value!);
        var result=SheetTwoGachCollector.BuildForGeometry([bar],new HashSet<string>{"a"},model);
        Assert.True(result.Success);
        var tick=Assert.Single(result.Value!.OfType<BlockPrimitive>());
        Assert.NotEqual(50,tick.Insertion.Y);
        Assert.Equal(0,tick.Insertion.X);
        Assert.Single(WallOpeningSymbolClipper.Clip([tick],model,"Base").Value!);
    }

    [Fact]
    public void MissingValidNativeGlyphProducesCoverageDiagnostic()
    {
        var model=BaseOpening(new CircleProfile(new(0,50),100));
        var result=SheetTwoGachCollector.BuildForGeometry([Bar("a",0,0,100)],new HashSet<string>{"a"},model);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="GACH_COLLECTOR_NO_VALID_ROUTE");
    }

    private static GeometryModel BaseOpening(Profile2Mm profile)
    {
        var basis=WallFrame.Create(WallId.T1,1000,1000);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var opening=new OpeningProfile("base-hole","Base",OpeningKind.Round,profile,profile,profile,basis,frame,
            new(-150,0),new(-150,0),[]);
        var footprint=new Footprint(new Polygon2Mm([new(-500,-500),new(500,-500),new(500,500),new(-500,500)]),[]);
        var component=new ComponentGeometry("Base",ComponentKind.Base,"Concrete",footprint,new(-150,0),
            new ExtrusionSolid(footprint,frame,new(-150,0)),[opening],[],[]);
        return new GeometryModel([component],[opening]);
    }

    [Fact]
    public void GlyphRejectionSelectsAnotherIntersectionOnTheSameVisibleBar()
    {
        var bar=Bar("a",0,0,100);
        var result=SheetTwoGachCollector.Build([bar],new HashSet<string>{"a"},
            tick=>tick.Insertion.Y<30 || tick.Insertion.Y>70);
        var tick=Assert.Single(result.OfType<BlockPrimitive>());
        Assert.Equal("a",tick.OccurrenceId);
        Assert.True(tick.Insertion.Y<30 || tick.Insertion.Y>70);
        Assert.InRange(tick.Insertion.Y,0,100);
        Assert.Single(result.OfType<PolylinePrimitive>());
    }

    [Fact]
    public void NoAdmissibleIntersectionDoesNotInventATickOrEmptyCollector()
    {
        var result=SheetTwoGachCollector.Build([Bar("a",0,0,20)],new HashSet<string>{"a"},_=>false);
        Assert.Empty(result);
    }

    [Fact]
    public void EachTickIntersectsItsOwnActualBarWithoutDimensionChains()
    {
        var bars = new[] { Bar("a",0,0,100), Bar("b",137,20,80), Bar("c",290,40,120) };
        var result = SheetTwoGachCollector.Build(bars, bars.Select(b=>b.OccurrenceId!).ToHashSet());
        var ticks = result.OfType<BlockPrimitive>().ToArray();
        Assert.Equal(3,ticks.Length);
        Assert.Single(result.OfType<PolylinePrimitive>());
        foreach(var tick in ticks)
        {
            var bar=bars.Single(b=>b.OccurrenceId==tick.OccurrenceId);
            Assert.Equal(bar.Points[0].X,tick.Insertion.X);
            Assert.InRange(tick.Insertion.Y,bar.Points[0].Y,bar.Points[1].Y);
            Assert.Equal("D1",tick.ScheduleMark);
            Assert.Equal(.3,tick.AnnotationScale);
        }
    }

    [Fact]
    public void DisjointVisibleFragmentsSplitCollectorsAndNeverTickRemovedMiddle()
    {
        var bars=new[]{Bar("a",0,0,20),Bar("b",100,80,100),Bar("a",0,80,100)};
        var result=SheetTwoGachCollector.Build(bars,new HashSet<string>{"a","b"});
        var ticks=result.OfType<BlockPrimitive>().ToArray();
        Assert.Equal(2,ticks.Length);
        Assert.All(ticks,t=>Assert.True(t.Insertion.Y<=20 || t.Insertion.Y>=80));
        Assert.Equal(2,ticks.Select(t=>t.OccurrenceId).Distinct().Count());
        var separate=SheetTwoGachCollector.Build(bars.Take(2),new HashSet<string>{"a","b"});
        Assert.Equal(2,separate.OfType<PolylinePrimitive>().Count());
    }

    [Fact]
    public void OtherFamiliesAndViewsCannotBorrowMainBarAssociations()
    {
        var a=Bar("a",0,0,100);
        var other=Bar("accessory",100,0,100);
        var result=SheetTwoGachCollector.Build(new[]{a,other},new HashSet<string>{"a"});
        Assert.Equal("a",Assert.Single(result.OfType<BlockPrimitive>()).OccurrenceId);
        Assert.Empty(SheetTwoGachCollector.Build(new[]{a},new HashSet<string>()));
    }

    private static PolylinePrimitive Bar(string id,double x,double low,double high)=>
        new(id+"/"+low,"Rebar/Top","BaseTop","Base",id,"D1",[new(x,low),new(x,high)],false);
}
