using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneSectionHiddenEdgesTests
{
    [Theory]
    [InlineData("AA",-750,200)]
    [InlineData("SteelAA",-750,200)]
    [InlineData("BB",-325,625)]
    [InlineData("SteelBB",-325,625)]
    public void HiddenRoofEdgeMeasuresNeckOuterFootprintAtRoofTop(string view,double left,double right)
    {
        var geometry=Fixture();
        var line=Assert.IsType<PolylinePrimitive>(Assert.Single(SheetOneSectionHiddenEdges.Build(geometry,view,Presentation)));
        Assert.Equal("Geometry/SectionHidden",line.Role);
        Assert.Equal("Roof",line.ComponentId);
        Assert.False(line.Closed);
        Assert.Equal(new Point2Mm(left,1550),line.Points[0]);
        Assert.Equal(new Point2Mm(right,1550),line.Points[1]);
        Assert.Equal("VHG-Dashed",SemanticRoleRegistry.Resolve(line.Role)!.Layer);
        Assert.Equal(1550,geometry.Components[0].ZRange.End);
    }
    [Fact]
    public void OtherThemesViewsAndDisconnectedNeckDoNotInventHiddenEdge()
    {
        Assert.Empty(SheetOneSectionHiddenEdges.Build(Fixture(),"AA",new(30,ElevationMode.Symbolic,null,"Default",true)));
        Assert.Empty(SheetOneSectionHiddenEdges.Build(Fixture(),"Plan",Presentation));
        Assert.Empty(SheetOneSectionHiddenEdges.Build(Fixture(1600),"AA",Presentation));
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static GeometryModel Fixture(double neckBottom=1550)
    {
        ComponentGeometry Rect(string id,ComponentKind kind,double x0,double y0,double x1,double y1,double z0,double z1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
            var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
            return new(id,kind,"Concrete",footprint,new(z0,z1),new ExtrusionSolid(footprint,frame,new(z0,z1)),[],[],[]);
        }
        return new([Rect("Roof",ComponentKind.Roof,-650,-775,650,775,1400,1550),
            Rect("Neck",ComponentKind.Neck,-325,-750,625,200,neckBottom,2000)],[]);
    }
}
