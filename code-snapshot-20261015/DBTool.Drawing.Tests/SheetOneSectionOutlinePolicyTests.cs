using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneSectionOutlinePolicyTests
{
    [Theory]
    [InlineData("AA",700,125,400,80,-275,1550)]
    [InlineData("BB",700,125,450,80,150,1400)]
    [InlineData("SteelAA",800,100,350,90,200,1600)]
    public void NeckSideOutlinesExposeTheSourceUPathsAndKeepTheClosedPlate(
        string view,double opening,double wall,double height,double plateHeight,double x,double bottom)
    {
        double left=x-opening/2-wall,right=x+opening/2+wall,top=bottom+height;
        PolylinePrimitive[] old=[Rect("left",view,"Neck",left,bottom,left+wall,top),
            Rect("right",view,"Neck",right-wall,bottom,right,top),Rect("plate",view,"Neck",left,top,right,top+plateHeight)];
        var result=SheetOneSectionOutlinePolicy.Compose(old,Presentation).Cast<PolylinePrimitive>().ToArray();
        Assert.All(result,p=>Assert.Equal(SheetOneSectionOutlinePolicy.Role,p.Role));
        Assert.False(result[0].Closed);Assert.False(result[1].Closed);Assert.True(result[2].Closed);
        Assert.Equal(new Point2Mm[]{new(left,bottom),new(left,top),new(left+wall,top),new(left+wall,bottom)},result[0].Points.ToArray());
        Assert.Equal(new Point2Mm[]{new(right,bottom),new(right,top),new(right-wall,top),new(right-wall,bottom)},result[1].Points.ToArray());
        Assert.Equal(new Point2Mm[]{new(left,top),new(left,top+plateHeight),new(right,top+plateHeight),new(right,top)},result[2].Points.ToArray());
        for(int i=0;i<old.Length;i++)
        {
            Assert.Equal(old[i].SemanticId,result[i].SemanticId);Assert.Equal(old[i].ComponentId,result[i].ComponentId);
            Assert.Equal(old[i].MaterialKind,result[i].MaterialKind);Assert.Equal(old[i].LogicalLengthMm,result[i].LogicalLengthMm);
            Assert.Equal(old[i].Points.OrderBy(p=>p.X).ThenBy(p=>p.Y),result[i].Points.OrderBy(p=>p.X).ThenBy(p=>p.Y));
        }
    }
    [Fact]
    public void MaterialStyleIntentPreservesOtherRegionGeometryAndLeavesOtherProfilesAndRolesAlone()
    {
        var material=Rect("wall","AA","Wall/T1",0,0,150,1400);
        var result=Assert.IsType<PolylinePrimitive>(Assert.Single(SheetOneSectionOutlinePolicy.Compose([material],Presentation)));
        Assert.Equal(SheetOneSectionOutlinePolicy.Role,result.Role);Assert.Equal(material.Points.ToArray(),result.Points.ToArray());Assert.Equal(material.Closed,result.Closed);
        Assert.Equal(new DrawingPrimitive[]{material},SheetOneSectionOutlinePolicy.Compose([material],new(30,ElevationMode.Symbolic,null,"Default",true)));
        var plan=Rect("plan","Plan","Neck",0,0,100,400);
        var hidden=new PolylinePrimitive("hidden","Geometry/SectionHidden","AA","Roof",null,null,[new(0,0),new(100,0)],false);
        Assert.Equal(new DrawingPrimitive[]{plan,hidden},SheetOneSectionOutlinePolicy.Compose([plan,hidden],Presentation));
    }
    [Fact]
    public void IncompleteOrDisconnectedNeckAssemblyKeepsExistingBoundaryClosureAndCorners()
    {
        var left=Rect("left","AA","Neck",-475,1550,-350,1950);
        var right=Rect("right","AA","Neck",350,1550,475,1950);
        var disconnected=Rect("plate","AA","Neck",-475,1951,475,2031);
        foreach(var old in new[]{new[]{left,right},new[]{left,right,disconnected}})
        {
            var result=SheetOneSectionOutlinePolicy.Compose(old,Presentation).Cast<PolylinePrimitive>().ToArray();
            for(int i=0;i<old.Length;i++){Assert.Equal(old[i].Points.ToArray(),result[i].Points.ToArray());Assert.Equal(old[i].Closed,result[i].Closed);}
        }
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    [Fact]
    public void NativeMaterialOutlinesRetainEveryMaterialLeaderTextAndThicknessLabel()
    {
        var bounds=new Bounds2Mm(-1800,-1500,1800,2500);
        var views=new[]{"AA","BB","SteelAA","SteelBB"}.Select(v=>new ViewDefinition(v,ViewPurpose.Section,new(0,0),bounds,30)).ToArray();
        var original=new List<DrawingPrimitive>();
        foreach(var view in new[]{"AA","BB"})
            original.AddRange(new[]{Rect(view+"/base",view,"Base",-875,-150,875,0),Rect(view+"/pad",view,"Pad",-975,-250,975,-150),
                Rect(view+"/left",view,"Wall/T3",-775,0,-625,1385),Rect(view+"/right",view,"Wall/T1",625,0,775,1385),
                Rect(view+"/mortar",view,"Wall/Mortar",625,1385,775,1400)});
        foreach(var view in new[]{"SteelAA","SteelBB"})
        {
            original.AddRange(new[]{Rect(view+"/left",view,"Neck",-750,1550,-625,1950),Rect(view+"/right",view,"Neck",75,1550,200,1950),
                Rect(view+"/plate",view,"Neck",-750,1950,200,2030)});
            original.Add(new PolylinePrimitive(view+"/Callout/NeckBottom/Leader","Rebar/Leader",view,"Neck",null,null,[new(-715,1800),new(-1035,1815)],false));
        }
        DrawingPackage Package(IEnumerable<DrawingPrimitive> p)=>new(new string('a',64),"test",views,p,bounds,CalculationScope.FullRebar);
        var labels=new Dictionary<string,string>{{"Base","BTCT M250 đá 1x2"},{"Pad","BT lót M100 đá 2x4"},{"Wall/Mortar","Vữa XM M75"},{"Neck","BTCT M250 đá 1x2"}};
        var before=SheetOneMaterialLeaders.Build(Package(original),labels);
        var after=SheetOneMaterialLeaders.Build(Package(SheetOneSectionOutlinePolicy.Compose(original,Presentation)),labels);
        Assert.Equal(16,before.Count);Assert.Equal(before.Count,after.Count);
        Assert.Equal(DrawingPackageJson.Serialize(Package(before)),DrawingPackageJson.Serialize(Package(after)));
    }
    private static PolylinePrimitive Rect(string id,string view,string component,double left,double bottom,double right,double top)=>
        new(id,"Geometry/Material",view,component,null,null,[new(left,bottom),new(right,bottom),new(right,top),new(left,top)],true);
}
