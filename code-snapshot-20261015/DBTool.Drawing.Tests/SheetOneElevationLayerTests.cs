using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneElevationLayerTests
{
    [Fact]
    public void SourceSheetOneFloorUsesTextLayerAndCapUsesConcreteLayerWithoutChangingElevationData()
    {
        var geometry=Fixture();
        var ordinary=new PresentationRequest(30,ElevationMode.Absolute,new ElevationM(55.24),"Default",true);
        var native=new PresentationRequest(30,ElevationMode.Absolute,new ElevationM(55.24),SheetOnePresentationPolicy.Family21Theme,true);
        var before=AnnotationPlanner.NativeSectionElevations(geometry,ordinary,2030)!.Cast<BlockPrimitive>().ToArray();
        var after=AnnotationPlanner.NativeSectionElevations(geometry,native,2030)!.Cast<BlockPrimitive>().ToArray();
        Assert.Equal(8,after.Length);
        for(int i=0;i<after.Length;i++)
        {
            Assert.Equal(before[i].SemanticId,after[i].SemanticId);
            Assert.Equal(before[i].ComponentId,after[i].ComponentId);
            Assert.Equal(before[i].Insertion,after[i].Insertion);
            Assert.Equal(before[i].AnnotationScale,after[i].AnnotationScale);
            Assert.Equal(before[i].BlockRole,after[i].BlockRole);
            Assert.Equal(before[i].Attributes.ToArray(),after[i].Attributes.ToArray());
            Assert.Equal("VHG-150",SemanticRoleRegistry.Resolve(before[i].Role)!.Layer);
            bool floor=after[i].ComponentId=="Base";
            Assert.Equal(floor?"VHG-Text":"VHG-BT",SemanticRoleRegistry.Resolve(after[i].Role)!.Layer);
            Assert.Equal(floor?"55.24":"57.27",Assert.Single(after[i].Attributes).Value);
        }
    }

    private static GeometryModel Fixture()
    {
        var footprint=new Footprint(new Polygon2Mm([new(-100,-100),new(100,-100),new(100,100),new(-100,100)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        ComponentGeometry Component(string id,ComponentKind kind,double bottom,double top)=>
            new(id,kind,"Concrete",footprint,new(bottom,top),new ExtrusionSolid(footprint,frame,new(bottom,top)),[],[],[]);
        return new GeometryModel([Component("Base",ComponentKind.Base,-150,0),Component("Neck",ComponentKind.Neck,1550,2000),
            Component("Wall/T1",ComponentKind.Wall,0,1400),Component("Wall/T2",ComponentKind.Wall,0,1400),
            Component("Wall/T3",ComponentKind.Wall,0,1400),Component("Wall/T4",ComponentKind.Wall,0,1400)],[]);
    }
}
