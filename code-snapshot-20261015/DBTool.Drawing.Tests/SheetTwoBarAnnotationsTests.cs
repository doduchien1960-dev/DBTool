using DBTool.Domain;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoBarAnnotationsTests
{
    [Fact]
    public void NominalDisplayMustNotInventD6At300OnD12MainBar()
    {
        var bar=new PolylinePrimitive("physical","Rebar/Top","BaseTop","Base","occ","D003",
            [new(-200,0),new(200,0)],false);
        var labels=SheetTwoBarAnnotations.Build([bar],new Dictionary<string,(int,double?)>{{"occ",(12,175)}},true)
            .OfType<BlockPrimitive>().ToArray();
        Assert.NotEmpty(labels);
        Assert.All(labels,b=>
        {
            Assert.Equal("occ",b.OccurrenceId);
            Assert.Contains(b.Attributes,a=>a.Key=="DK" && a.Value=="%%c12");
            Assert.Contains(b.Attributes,a=>a.Key=="A" && a.Value=="@175");
        });
    }

    [Theory]
    [InlineData(-150,125)]
    [InlineData(750,-300)]
    public void TieCrossConsumesOneActualProjectionAndKeepsIdentity(double x,double y)
    {
        var tie=new CirclePrimitive("tie/0","Rebar/Main","BaseTop","Base","tie-1","T001",new(x,y),3);
        var main=new CirclePrimitive("main/0","Rebar/Main","BaseTop","Base","main-1","D001",new(0,0),6);
        var cross=Assert.Single(SheetTwoTieSymbols.Build([tie,main],tieOccurrenceIds:new HashSet<string>{"tie-1"}));
        Assert.Equal(new DBTool.Domain.Geometry.Point2Mm(x,y),cross.Insertion);
        Assert.Equal("tie-1",cross.OccurrenceId);Assert.Equal("T001",cross.ScheduleMark);
    }
    [Fact]
    public void NoPhysicalTieMeansNoCrossSymbol()
    {
        var views=new[]{"BaseTop",
            "Reinforcement/Wall/T1/Wall/T1/Outer/Top",
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top"};
        var bars=views.Select(v=>new PolylinePrimitive(v+"/bar","Rebar/Main",v,
            v=="BaseTop"?"Base":v.Contains("/T1/",StringComparison.Ordinal)?"T1":"T2","bar","D001",
            [new(-100,0),new(100,0)],false));
        var symbols=SheetTwoTieSymbols.Build(bars);
        Assert.Empty(symbols);
    }
    [Fact]
    public void InnerAnchorageLabelUsesExistingHookAndKeepsItsCanonicalIdentity()
    {
        const string view="Reinforcement/Wall/T1/Wall/T1/Inner/Bottom";
        var vertical=new PolylinePrimitive("v","Rebar/Main",view,"Wall/T1","v","D072",
            [new(-300,-75),new(100,-75),new(100,1200)],false);
        var horizontal=new PolylinePrimitive("h","Rebar/Main",view,"Wall/T1","h","D071",
            [new(-600,200),new(600,200)],false);
        var outline=new PolylinePrimitive("o",SheetTwoMaterialViews.WallRole,view,"Wall/T1",null,null,
            [new(-650,-150),new(650,-150),new(650,1550),new(-650,1550)],false);
        var result=SheetTwoBarAnnotations.Build([horizontal,vertical,outline],
            new Dictionary<string,(int,double?)>{{"h",(10,150)},{"v",(10,150)}},true);
        var anchor=Assert.Single(result.OfType<BlockPrimitive>(),b=>b.SemanticId.EndsWith("/Anchorage"));
        Assert.Equal("T3",anchor.Attributes.Single(a=>a.Key=="SH").Value);
        Assert.Equal("v",anchor.OccurrenceId);
        Assert.Equal("D072",anchor.ScheduleMark);
        var leader=Assert.Single(result.OfType<PolylinePrimitive>(),b=>b.SemanticId.EndsWith("/Anchorage/Leader"));
        Assert.Equal(-75,leader.Points[0].Y);
        Assert.InRange(leader.Points[0].X,-300,100);
        Assert.Equal(anchor.Insertion,leader.Points[^1]);
    }
    [Fact]
    public void NominalDisplayAliasesPreserveCanonicalIdentityAndActualLeaderTips()
    {
        var horizontal=new PolylinePrimitive("h","Rebar/Top","BaseTop","Base","h","D071",
            [new(-600,200),new(600,200)],false);
        var vertical=new PolylinePrimitive("v","Rebar/Top","BaseTop","Base","v","D072",
            [new(100,-740),new(100,740)],false);
        var result=SheetTwoBarAnnotations.Build([horizontal,vertical],
            new Dictionary<string,(int,double?)>{{"h",(10,150)},{"v",(10,150)}},useNominalLabels:true);
        var blocks=result.OfType<BlockPrimitive>().ToArray();
        Assert.Equal("D1",Assert.Single(blocks,b=>b.ScheduleMark=="D071").Attributes.Single(a=>a.Key=="SH").Value);
        Assert.Equal("D2",Assert.Single(blocks,b=>b.ScheduleMark=="D072").Attributes.Single(a=>a.Key=="SH").Value);
        Assert.All(blocks,b=>Assert.Contains(b.OccurrenceId,new[]{"h","v"}));
        Assert.All(result.OfType<PolylinePrimitive>(),leader=>
        {
            if(leader.OccurrenceId=="h") Assert.Equal(200,leader.Points[0].Y);
            else Assert.Equal(100,leader.Points[0].X);
        });
    }
    [Fact]
    public void SheetTwoCalloutUsesSourceStrokeFontWithoutChangingTheRegularSheetOneStyle()
    {
        Assert.Equal("VHG-Unicode.Regular",SemanticRoleRegistry.Resolve(SheetTwoBarAnnotations.Role)!.TextStyle);
        Assert.Equal("VHG-Unicode.Regular",SemanticRoleRegistry.Resolve("Rebar/Mark")!.TextStyle);
    }
    [Fact]
    public void CanonicalMarkDiameterNominalPitchAndLeaderRemainLinkedToActualBar()
    {
        var bar=new PolylinePrimitive("b","Rebar/Top","BaseTop","Base","physical-child","D071",
            [new(-600,200),new(600,200)],false);
        var outline=new PolylinePrimitive("o",SheetTwoMaterialViews.BaseRole,"BaseTop","Base",null,null,
            [new(-750,-875),new(750,-875),new(750,875),new(-750,875),new(-750,-875)],false);
        var result=SheetTwoBarAnnotations.Build([bar,outline],
            new Dictionary<string,(int Diameter,double? Pitch)>{{"physical-child",(12,175)}});
        var block=Assert.Single(result.OfType<BlockPrimitive>());
        Assert.Equal("Block:BLT1",block.BlockRole);
        Assert.Equal("physical-child",block.OccurrenceId); Assert.Equal("D071",block.ScheduleMark);
        Assert.Equal("D071",block.Attributes.Single(a=>a.Key=="SH").Value);
        Assert.Equal("%%c12",block.Attributes.Single(a=>a.Key=="DK").Value);
        Assert.Equal("@175",block.Attributes.Single(a=>a.Key=="A").Value);
        var leader=Assert.Single(result.OfType<PolylinePrimitive>());
        Assert.Equal(bar.OccurrenceId,leader.OccurrenceId); Assert.Equal(bar.ScheduleMark,leader.ScheduleMark);
        Assert.Equal(200,leader.Points[0].Y); Assert.InRange(leader.Points[0].X,-600,600);
        Assert.Equal(block.Insertion,leader.Points[^1]);
        var package=new DrawingPackage(new string('a',64),"test",
            [new ViewDefinition("BaseTop",ViewPurpose.BaseMesh,new(0,0),new(-750,-1100,750,875),30)],
            result,new(-750,-1100,750,875),CalculationScope.FullRebar);
        var copy=DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(package));
        var roundTrip=Assert.Single(copy.Primitives.OfType<BlockPrimitive>());
        Assert.Equal(block.OccurrenceId,roundTrip.OccurrenceId);
        Assert.Equal(block.ScheduleMark,roundTrip.ScheduleMark);
    }

    [Fact]
    public void SplitMarksAreNotCollapsedAndUnknownPitchIsNotInvented()
    {
        var bars=new[]{"D081","D082","D083"}.Select((mark,i)=>new PolylinePrimitive("b"+i,"Rebar/Main",
            "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom","Wall/T2","child"+i,mark,
            [new(100+i*150,0),new(100+i*150,900)],false)).ToArray();
        var result=SheetTwoBarAnnotations.Build(bars,new Dictionary<string,(int,double?)>{{"child0",(10,150)},{"child1",(10,150)},{"child2",(10,null)}});
        Assert.Equal(2,result.OfType<BlockPrimitive>().Count());
        Assert.Equal(2,result.OfType<PolylinePrimitive>().Count());
        Assert.DoesNotContain(result.OfType<BlockPrimitive>(),b=>b.ScheduleMark=="D083");
        Assert.Empty(SheetTwoBarAnnotations.Build([new PolylinePrimitive("x","Rebar/Main","SteelAA","Wall/T1","child0","D081",
            [new(0,0),new(0,900)],false)],new Dictionary<string,(int,double?)>{{"child0",(10,150)}}));
    }
}
