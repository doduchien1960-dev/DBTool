using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class PlanHiddenEdgePlannerTests
{
    private static MaterialRegion Rect(string id,double x0,double y0,double x1,double y1)=>new(id,id,"Concrete",
        new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]),[]);

    [Fact]
    public void ConnectedWallsKeepChamberBoundaryWithoutSharedSeamsOrDuplicateRoofOutline()
    {
        var regions=new[]{Rect("Wall/T1",-650,625,650,775),Rect("Wall/T2",500,-625,650,625),
            Rect("Wall/T3",-650,-775,650,-625),Rect("Wall/T4",-650,-625,-500,625),
            Rect("Roof",-650,-775,650,775)};
        var plan=new ViewProfile("Plan",ViewPurpose.Plan,regions,[],[]);
        var lines=PlanHiddenEdgePlanner.Build(plan).OfType<PolylinePrimitive>().ToArray();
        Assert.Equal(4,lines.Length);
        Assert.All(lines,p=>Assert.All(p.Points,q=>Assert.True(Math.Abs(q.X)==500 && Math.Abs(q.Y)==625)));
        Assert.Equal(4500,lines.Sum(p=>Math.Abs(p.Points[1].X-p.Points[0].X)+Math.Abs(p.Points[1].Y-p.Points[0].Y)));
        Assert.Equal(5,plan.Regions.Length);
        Assert.Equal(650,plan.Regions[0].Footprint.Outer.Vertices[1].X);
    }

    [Fact]
    public void ExposedOuterWallEdgesRemainWhenThereIsNoCoincidentRoofOutline()
    {
        var plan=new ViewProfile("Plan",ViewPurpose.Plan,[Rect("Wall/T1",0,0,100,20)],[],[]);
        Assert.Equal(4,PlanHiddenEdgePlanner.Build(plan).Count);
    }

    [Fact]
    public void PartlyCoincidentRoofKeepsUncoveredWallEdgeFragments()
    {
        var plan=new ViewProfile("Plan",ViewPurpose.Plan,[Rect("Wall/T1",0,0,100,20),Rect("Roof",20,20,80,40)],[],[]);
        var top=PlanHiddenEdgePlanner.Build(plan).OfType<PolylinePrimitive>().Where(p=>p.Points.All(q=>q.Y==20)).ToArray();
        Assert.Equal(2,top.Length);
        Assert.Equal(40,top.Sum(p=>Math.Abs(p.Points[1].X-p.Points[0].X)));
    }
}
