using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class SheetTwoNominalAnchorageTests
{
    [Fact]
    public void ConfirmedTemplateUsesTickStationAndPreservesItsFamilyIdentity()
    {
        const string view = "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom";
        var foot = new Footprint(new Polygon2Mm([new(0,0),new(100,0),new(100,100),new(0,100)]),[]);
        var frame = new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var basis = new ComponentGeometry("Base",ComponentKind.Base,"Concrete",foot,new(-150,0),
            new ExtrusionSolid(foot,frame,new(-150,0)),[],[],[]);
        var haunch = new ComponentGeometry("Base/Haunch",ComponentKind.Base,"Concrete",foot,new(0,100),
            new HaunchSolid(1000,1250,100,100,HaunchOwner.Base,new(0,100)),[],[],[]);
        var tick = new BlockPrimitive("tick",SheetTwoGach.AnchorageRole,view,"Wall/T2","Block:Gach",new(-495,-75),.3,[],occurrenceId:"existing",scheduleMark:"D029");
        var cover = new DimensionIntentPrimitive(view+"/Dimension/NominalSpacing/X/000",SheetTwoDimensionPlanner.Role,view,"Wall/T2","existing","D029",new(0,0),new(30,0),30,"<>",0);
        var mesh = new PolylinePrimitive("mesh",SheetTwoNominalWallMesh.Role,view,"Wall/T2","existing","D029",[new(0,0),new(0,1000)],false);
        var input = new DrawingPrimitive[]{tick,cover,mesh};
        var leg = Assert.Single(SheetTwoNominalAnchorage.Build(new GeometryModel([basis,haunch],[]),input));
        Assert.Equal(new Point2Mm(-495,-120),leg.Points[0]);
        Assert.Equal(177.573593128,leg.Points[1].Y,8);
        Assert.Equal(tick.OccurrenceId,leg.OccurrenceId);Assert.Equal(tick.ScheduleMark,leg.ScheduleMark);
        Assert.Empty(SheetTwoNominalAnchorage.Build(new GeometryModel([basis],[]),input));
        Assert.Empty(SheetTwoNominalAnchorage.Build(new GeometryModel([basis,haunch],[]),[tick,cover]));
        var otherBase = new ComponentGeometry("Base",ComponentKind.Base,"Concrete",foot,new(-200,0),new ExtrusionSolid(foot,frame,new(-200,0)),[],[],[]);
        Assert.Empty(SheetTwoNominalAnchorage.Build(new GeometryModel([otherBase,haunch],[]),input));
    }
}
