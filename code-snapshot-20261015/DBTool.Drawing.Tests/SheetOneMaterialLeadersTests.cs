using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class SheetOneMaterialLeadersTests
{
    [Fact]
    public void MergedConcreteLeaderCannotAnchorInsideChamberVoid()
    {
        Point2Mm[] outer=[new(-800,-150),new(800,-150),new(800,1400),new(-800,1400)];
        Point2Mm[] hole=[new(-650,0),new(-650,1300),new(650,1300),new(650,0)];
        var outline=new PolylinePrimitive("cast","Geometry/Material","AA","Base",null,null,outer,true);
        var hatch=new HatchPrimitive("cast/hatch","Material/Concrete","AA","Base",outer,[hole],DrawingMaterialKind.Concrete);
        var bounds=new Bounds2Mm(-1500,-1500,1500,2000);
        var package=new DrawingPackage(new string('a',64),"test",[new("AA",ViewPurpose.Section,new(0,0),bounds,30)],
            [outline,hatch],bounds,CalculationScope.FullRebar);
        var notes=SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>{{"Base","BTCT M250"}});
        var target=Assert.Single(notes.OfType<PolylinePrimitive>()).Points[0];
        Assert.True(target.Y<0 || target.Y>1300 || Math.Abs(target.X)>650,$"Leader target is inside chamber void: {target}");
        Assert.InRange(target.X,-800,800);Assert.InRange(target.Y,-150,1400);
    }

    [Theory]
    [InlineData("AA",1)]
    [InlineData("SteelBB",-1)]
    public void PipeGroutNoteUsesSuppliedGradeAndActualUpperBand(string view,int direction)
    {
        double x0=direction>0?675:-825,x1=direction>0?825:-675;
        var grout=new HatchPrimitive(view+"/Pipe/T1/Grout/1","Material/Mortar",view,"Wall/T1",
            [new(x0,1155),new(x1,1155),new(x1,1205),new(x0,1205)],[],DrawingMaterialKind.Concrete);
        var bounds=new Bounds2Mm(-2000,-500,2000,2500);
        var package=new DrawingPackage(new string('a',64),"test",
            [new(view,ViewPurpose.Section,new(0,0),bounds,30)],[grout],bounds,CalculationScope.FullRebar);
        Assert.Empty(SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>()));
        var notes=SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>{{"Pipe/Grout","Vữa XM M100"}});
        var leader=Assert.Single(notes.OfType<PolylinePrimitive>());
        Assert.Equal(new Point2Mm(direction*750,1180),leader.Points[0]);
        Assert.Equal(new Point2Mm(direction*855,1325),leader.Points[1]);
        var text=Assert.Single(notes.OfType<TextPrimitive>());
        Assert.Equal("Vữa XM M100",text.Content);
        Assert.Equal(new Point2Mm(direction>0?855:leader.Points[2].X,1345),text.Position);
        Assert.Equal("Wall/T1",text.ComponentId);
    }

    [Fact]
    public void MaterialLeaderAnchorsMatchPublishedSectionAndRetainNativeArrowIntent()
    {
        PolylinePrimitive Outline(string id,string owner,double x0,double y0,double x1,double y1)=>
            new(id,"Geometry/Material","AA",owner,null,null,[new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)],true);
        var bounds=new Bounds2Mm(-1800,-1500,1800,2500);
        var package=new DrawingPackage(new string('a',64),"test",[new("AA",ViewPurpose.Section,new(0,0),bounds,30)],
            [Outline("base","Base",-875,-150,875,0),Outline("pad","Pad",-975,-250,975,-150),
             Outline("left","Wall/T3",-775,0,-625,1385),Outline("right","Wall/T1",625,0,775,1385),
             Outline("mortar","Wall/Mortar",625,1385,775,1400)],bounds,CalculationScope.FullRebar);
        var notes=SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>{
            {"Base","BTCT M250 đá 1x2"},{"Pad","BT lót M100 đá 2x4"},{"Wall/Mortar","Vữa XM M75"}});
        var expected=new[]{("Base",new Point2Mm(-312.5,-75),new Point2Mm(-312.5,-730),new Point2Mm(263.5,-730),new Point2Mm(-292.5,-710)),
            ("Pad",new Point2Mm(-312.5,-200),new Point2Mm(-312.5,-850),new Point2Mm(335.5,-850),new Point2Mm(-292.5,-830)),
            ("Wall/Mortar",new Point2Mm(700,1392.5),new Point2Mm(595,1265),new Point2Mm(145,1265),new Point2Mm(145,1285))};
        foreach(var (owner,target,elbow,end,text) in expected)
        {
            var leader=Assert.Single(notes.OfType<PolylinePrimitive>(),p=>p.ComponentId==owner);
            Assert.Equal("Material/Leader",leader.Role);
            Assert.Equal(new[]{target,elbow,end},leader.Points);
            Assert.Equal(text,Assert.Single(notes.OfType<TextPrimitive>(),p=>p.SemanticId==$"AA/MaterialLeader/{owner}/Text").Position);
        }
        Assert.Equal(new Point2Mm(145,1175),Assert.Single(notes.OfType<TextPrimitive>(),p=>p.Content=="Dày 15mm").Position);
    }

    [Fact]
    public void NeckGradeRequiresExplicitLabelAndExistingNeckCallout()
    {
        var outline=new PolylinePrimitive("neck","Geometry/Material","SteelAA","Neck",null,null,
            [new(-750,1550),new(-625,1550),new(-625,2080),new(-750,2080)],true);
        var leader=new PolylinePrimitive("SteelAA/Callout/NeckBottom/Leader","Dimension/Linear","SteelAA","Neck",null,null,
            [new(-715,1800),new(-1035,1815)],false);
        var bounds=new Bounds2Mm(-1800,0,1200,2500);
        DrawingPackage Package(params DrawingPrimitive[] primitives)=>new(new string('a',64),"test",
            [new("SteelAA",ViewPurpose.Section,new(0,0),bounds,30)],primitives,bounds,CalculationScope.FullRebar);
        var label=new Dictionary<string,string>{{"Neck","BTCT M250 đá 1x2"}};
        Assert.Empty(SheetOneMaterialLeaders.Build(Package(outline),label));
        Assert.Empty(SheetOneMaterialLeaders.Build(Package(outline,leader),new Dictionary<string,string>()));
        var text=Assert.IsType<TextPrimitive>(Assert.Single(SheetOneMaterialLeaders.Build(Package(outline,leader),label)));
        Assert.Equal("BTCT M250 đá 1x2",text.Content);
        Assert.Equal(new Point2Mm(-845,1730),text.Position);
        Assert.Equal("Rebar/MarkRight",text.Role);
        var callout=new TextPrimitive("SteelAA/Callout/NeckBottom","Rebar/Mark","SteelAA","Neck",null,null,
            new(-1035,1905),"Cổ ga",60,0);
        var aligned=Assert.Single(SheetOneMaterialLeaders.Build(Package(outline,leader,callout),label).OfType<TextPrimitive>());
        Assert.Equal(new Point2Mm(-1035,1805),aligned.Position);
    }

    [Fact]
    public void MortarLeaderUsesRightCutAndKeepsLabelInsideChamber()
    {
        PolylinePrimitive Mortar(string id,double x)=>new(id,"Geometry/Material","AA","Wall/Mortar",null,null,
            [new(x,1385),new(x+150,1385),new(x+150,1400),new(x,1400)],true);
        var bounds=new Bounds2Mm(-1000,-1500,1000,2000);
        var package=new DrawingPackage(new string('a',64),"test",[new("AA",ViewPurpose.Section,new(0,0),bounds,30)],
            [Mortar("left",-775),Mortar("right",625)],bounds,CalculationScope.FullRebar);
        var result=SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>{{"Wall/Mortar","Vữa XM M75"}});
        var leader=Assert.Single(result.OfType<PolylinePrimitive>(),p=>!p.SemanticId.EndsWith("/Arrow"));
        Assert.InRange(leader.Points[0].X,625,775);
        var text=Assert.Single(result.OfType<TextPrimitive>(),t=>t.Content=="Vữa XM M75");
        Assert.InRange(text.Position.X,-625,625);
    }

    [Fact]
    public void LeaderUsesSuppliedMaterialAndMeasuredMortarThickness()
    {
        var bounds=new Bounds2Mm(-1000,-1500,1000,2000);
        var outline=new PolylinePrimitive("m","Geometry/Material","AA","Wall/Mortar",null,null,
            [new(-775,1385),new(-625,1385),new(-625,1400),new(-775,1400)],true);
        var package=new DrawingPackage(new string('a',64),"test",[new("AA",ViewPurpose.Section,new(0,0),bounds,30)],
            [outline],bounds,CalculationScope.FullRebar);
        var result=SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>{{"Wall/Mortar","Vữa XM M75"}});
        Assert.Contains(result.OfType<TextPrimitive>(),t=>t.Content=="Vữa XM M75");
        Assert.Contains(result.OfType<TextPrimitive>(),t=>t.Content=="Dày 15mm");
        var leader=Assert.Single(result.OfType<PolylinePrimitive>(),p=>!p.SemanticId.EndsWith("/Arrow"));
        Assert.InRange(leader.Points[0].X,-775,-625);
        Assert.InRange(leader.Points[0].Y,1385,1400);
        Assert.Empty(SheetOneMaterialLeaders.Build(package,new Dictionary<string,string>()));
    }
}
