using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneWallMeshHiddenEdgesTests
{
    [Theory]
    [InlineData(0, 0, 1000, 1250)]
    [InlineData(250, -100, 1200, 1450)]
    public void BothConcealedContoursFollowPhysicalWallFaces(double x, double y, double width, double length)
    {
        var geometry = Fixture(x, y, width, length);
        var contours = SheetOneWallMeshHiddenEdges.Build(geometry, Presentation).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(2, contours.Length);
        for (int i = 0; i < 2; i++)
        {
            double extension = i == 0 ? 150 : 0;
            var contour = contours[i];
            Assert.Equal("WallMeshPlan", contour.ViewId);
            Assert.Equal("Geometry/SectionHidden", contour.Role);
            Assert.Equal("VHG-Dashed", SemanticRoleRegistry.Resolve(contour.Role)!.Layer);
            Assert.False(contour.Closed); // Source uses a repeated first vertex, not the Closed flag.
            Assert.Equal(5, contour.Points.Length);
            Assert.Equal(new Point2Mm(x-width/2-extension, y+length/2+extension+180), contour.Points[0]);
            Assert.Equal(new Point2Mm(x+width/2+extension, y+length/2+extension+180), contour.Points[1]);
            Assert.Equal(new Point2Mm(x+width/2+extension, y-length/2-extension+180), contour.Points[2]);
            Assert.Equal(contour.Points[0], contour.Points[4]);
            Assert.Null(contour.OccurrenceId);
        }
    }

    [Fact]
    public void ConventionalProfileAndIncompleteWallsDoNotInventContours()
    {
        Assert.Empty(SheetOneWallMeshHiddenEdges.Build(Fixture(0,0,1000,1250), new(30,ElevationMode.Symbolic,null,"Default",true)));
        var geometry = Fixture(0,0,1000,1250);
        Assert.Empty(SheetOneWallMeshHiddenEdges.Build(new(geometry.Components.Take(3), []), Presentation));
    }

    private static readonly PresentationRequest Presentation = new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static GeometryModel Fixture(double x,double y,double width,double length)
    {
        ComponentGeometry Rect(string id,double x0,double y0,double x1,double y1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0+x,y0+y),new(x1+x,y0+y),new(x1+x,y1+y),new(x0+x,y1+y)]),[]);
            var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
            return new(id,ComponentKind.Wall,"Concrete",footprint,new(0,1400),new ExtrusionSolid(footprint,frame,new(0,1400)),[],[],[]);
        }
        double a=width/2,b=length/2;
        return new([Rect("Wall/T1",-a-150,b,a+150,b+150), Rect("Wall/T2",a,-b,a+150,b),
            Rect("Wall/T3",-a-150,-b-150,a+150,-b), Rect("Wall/T4",-a-150,-b,-a,b)],[]);
    }
}
