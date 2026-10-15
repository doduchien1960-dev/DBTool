using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOnePlanOutlinePolicyTests
{
    [Theory]
    [InlineData("Roof",1300,1550,0,0,false)]
    [InlineData("Roof",1450,1650,200,-100,false)]
    [InlineData("Neck",950,950,150,-275,true)]
    [InlineData("Neck",1000,1100,-200,300,true)]
    public void OuterRectangleUsesSourceVertexDirectionWithoutChangingFootprintOrTrace(
        string component,double width,double length,double x,double y,bool closed)
    {
        var original=Outline(component,width,length,x,y);
        var actual=Assert.IsType<PolylinePrimitive>(SheetOnePlanOutlinePolicy.Apply(original,Presentation));
        Assert.Equal(SheetOnePlanOutlinePolicy.Role,actual.Role);
        Assert.Equal(closed,actual.Closed);
        var tl=new Point2Mm(x-width/2,y+length/2);var tr=new Point2Mm(x+width/2,y+length/2);
        var br=new Point2Mm(x+width/2,y-length/2);var bl=new Point2Mm(x-width/2,y-length/2);
        Assert.Equal(closed?new[]{tl,tr,br,bl}:new[]{tl,tr,br,bl,tl},actual.Points.ToArray());
        Assert.Equal(original.SemanticId,actual.SemanticId);Assert.Equal(original.ComponentId,actual.ComponentId);
        Assert.Equal(original.OccurrenceId,actual.OccurrenceId);Assert.Equal(original.ScheduleMark,actual.ScheduleMark);
        Assert.Equal(original.LogicalLengthMm,actual.LogicalLengthMm);Assert.Equal(original.MaterialKind,actual.MaterialKind);
        Assert.Equal(original.Points.Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y),actual.Points.Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y));
    }
    [Fact]
    public void OtherProfilesViewsInnerLoopsAndUnsupportedShapesStayUnchanged()
    {
        var original=Outline("Roof",1300,1550,0,0);
        Assert.Same(original,SheetOnePlanOutlinePolicy.Apply(original,new(30,ElevationMode.Symbolic,null,"Default",true)));
        foreach(var p in new[]{Outline("Base",1500,1750,0,0),
            new PolylinePrimitive("inner","Geometry/InnerLoop","Plan","Neck",null,null,original.Points,true),
            new PolylinePrimitive("section","Geometry/Material","AA","Roof",null,null,original.Points,true),
            new PolylinePrimitive("triangle","Geometry/Material","Plan","Roof",null,null,[new(0,0),new(100,0),new(50,100)],true),
            new PolylinePrimitive("open","Geometry/Material","Plan","Roof",null,null,original.Points,false)})
            Assert.Same(p,SheetOnePlanOutlinePolicy.Apply(p,Presentation));
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static PolylinePrimitive Outline(string component,double width,double length,double x,double y)=>new($"Plan/{component}/Outer","Geometry/Material","Plan",component,null,null,
        [new(x-width/2,y-length/2),new(x+width/2,y-length/2),new(x+width/2,y+length/2),new(x-width/2,y+length/2)],true);
}
