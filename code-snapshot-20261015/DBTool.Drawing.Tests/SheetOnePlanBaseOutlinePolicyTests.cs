using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOnePlanBaseOutlinePolicyTests
{
    [Theory]
    [InlineData(1500,1750,0,0)]
    [InlineData(1650,1850,200,-100)]
    [InlineData(1700,1950,-150,250)]
    public void NativeBaseEdgesUsePhysicalCornersAndRetainTheWholeOutlineIdentity(double width,double length,double x,double y)
    {
        var original=Outline(width,length,x,y);
        var intent=Assert.IsType<PolylinePrimitive>(SheetOnePlanBaseOutlinePolicy.Apply(original,Presentation));
        Assert.Equal(SheetOnePlanBaseOutlinePolicy.Role,intent.Role);
        Assert.Equal("VHG-BT",SemanticRoleRegistry.Resolve(intent.Role)!.Layer);
        Assert.Equal(original.SemanticId,intent.SemanticId);Assert.Equal(original.ComponentId,intent.ComponentId);
        Assert.Equal(original.Points.ToArray(),intent.Points.ToArray());Assert.Equal(original.Closed,intent.Closed);
        var edges=SheetOnePlanBaseOutlinePolicy.NativeEdges(intent);
        Assert.Equal(4,edges.Count);
        var topLeft=new Point2Mm(x-width/2,y+length/2);var topRight=new Point2Mm(x+width/2,y+length/2);
        var bottomRight=new Point2Mm(x+width/2,y-length/2);var bottomLeft=new Point2Mm(x-width/2,y-length/2);
        Assert.Equal(new[]{topLeft,topRight,bottomRight,topLeft},edges.Select(e=>e.Start).ToArray());
        Assert.Equal(new[]{topRight,bottomRight,bottomLeft,bottomLeft},edges.Select(e=>e.End).ToArray());
    }
    [Fact]
    public void PipeVisibilityInterruptsOnlyTheCrossedBaseEdgeAndKeepsCalculationOutline()
    {
        var original=Outline(1650,1850,0,0);
        var intent=Assert.IsType<PolylinePrimitive>(SheetOnePlanBaseOutlinePolicy.Apply(original,Presentation));
        PolylinePrimitive Pipe(int sign)=>new($"Plan/p/Stub/VisibleOuter/{sign}","Geometry/PlanPipeVisible","Plan","Wall/T1",null,null,
            [new(sign*255,825),new(sign*255,1225)],false);
        var result=SheetOnePlanBaseOutlinePolicy.ClipAtPipes([intent,Pipe(-1),Pipe(1)],Presentation);
        var hidden=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.ComponentId=="Base" && p.Role=="Geometry/PlanPipeHidden");
        Assert.Equal(new[]{new Point2Mm(-255,925),new Point2Mm(255,925)},hidden.Points.ToArray());
        var edges=result.OfType<PolylinePrimitive>().Where(p=>p.ComponentId=="Base" && p.Role==SheetOnePlanBaseOutlinePolicy.Role).ToArray();
        Assert.Equal(5,edges.Length);
        Assert.All(edges,p=>Assert.Single(SheetOnePlanBaseOutlinePolicy.NativeEdges(p)));
        Assert.Contains(edges,p=>p.Points[0]==new Point2Mm(-825,925) && p.Points[1]==new Point2Mm(-255,925));
        Assert.Contains(edges,p=>p.Points[0]==new Point2Mm(255,925) && p.Points[1]==new Point2Mm(825,925));
        Assert.Equal(4,original.Points.Length);Assert.True(original.Closed);
        Assert.Same(intent,Assert.Single(SheetOnePlanBaseOutlinePolicy.ClipAtPipes([intent],Presentation)));
    }
    [Fact]
    public void OtherProfilesComponentsAndNonRectangularBoundariesKeepTheirRepresentation()
    {
        var original=Outline(1500,1750,0,0);
        Assert.Same(original,SheetOnePlanBaseOutlinePolicy.Apply(original,new(30,ElevationMode.Symbolic,null,"Default",true)));
        var roof=new PolylinePrimitive("roof","Geometry/Material","Plan","Roof",null,null,original.Points,true);
        Assert.Same(roof,SheetOnePlanBaseOutlinePolicy.Apply(roof,Presentation));
        var triangle=new PolylinePrimitive("base","Geometry/Material","Plan","Base",null,null,[new(0,0),new(100,0),new(50,100)],true);
        Assert.Same(triangle,SheetOnePlanBaseOutlinePolicy.Apply(triangle,Presentation));
        Assert.Empty(SheetOnePlanBaseOutlinePolicy.NativeEdges(original));
        var malformed=new PolylinePrimitive("base",SheetOnePlanBaseOutlinePolicy.Role,"Plan","Base",null,null,triangle.Points,true);
        Assert.Empty(SheetOnePlanBaseOutlinePolicy.NativeEdges(malformed));
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static PolylinePrimitive Outline(double width,double length,double x,double y)=>new("Plan/Base/Outer","Geometry/Material","Plan","Base",null,null,
        [new(x-width/2,y-length/2),new(x+width/2,y-length/2),new(x+width/2,y+length/2),new(x-width/2,y+length/2)],true);
}
