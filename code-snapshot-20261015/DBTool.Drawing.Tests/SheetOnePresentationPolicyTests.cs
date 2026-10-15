using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOnePresentationPolicyTests
{
    [Fact]
    public void ExplicitFamily21ProfileKeepsPhysicalRegistryAndProjectsDeclaredOpenings()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            new CircleProfile(new(0,700),80),new CircleProfile(new(0,700),90),new CircleProfile(new(0,700),100),
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),new(600,800),new(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var physical=new GeometryModel([wall],[opening]);
        var display=SheetOnePresentationPolicy.ProjectionGeometry(physical,
            new PresentationRequest(30,ElevationMode.Symbolic,null,"VHG-2.1-SheetOne",true));
        Assert.Single(display.Openings);
        Assert.Single(display.Components[0].Voids);
        Assert.Same(wall.Occupied,display.Components[0].Occupied);
        var old=SheetOnePresentationPolicy.ProjectionGeometry(physical,
            new PresentationRequest(30,ElevationMode.Symbolic,null,"S2",true));
        Assert.Same(physical,old);
        Assert.Contains(PlanProjector.BuildPlan(old).Value!.Voids,v=>v.Role==ProjectedVoidRole.PipePlanSilhouette);
    }
}
