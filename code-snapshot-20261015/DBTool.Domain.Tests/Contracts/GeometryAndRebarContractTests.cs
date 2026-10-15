using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Domain.Tests.Contracts;
public class GeometryAndRebarContractTests
{
    [Fact] public void OpenTopWallCutDoesNotRequirePipeOuter()
    {
        var wall=WallFrame.Create(WallId.T1,1000,1250);
        var shape=new OpenTopProfile(new Polygon2Mm([
            new(-200,1100),new(200,1100),new(200,1400),new(-200,1400)]));
        var frame=new Frame3Mm(wall.Origin,wall.Left,wall.Up,wall.Outward);
        var opening=new OpeningProfile("Catch","Wall/T1",OpeningKind.OpenTop,
            shape,null,shape,wall,frame,new Interval1D(1100,1400),
            new Interval1D(0,150),[]);
        Assert.Null(opening.PipeOuter);
        Assert.Equal(WallId.T1,opening.WallFrame!.WallId);
        var box=new RectangleProfile(new Rectangle2Mm(-200,1100,200,1400));
        Assert.Throws<ArgumentException>(()=>new OpeningProfile("Pipe","Wall/T1",
            OpeningKind.Box,box,null,box,wall,frame,new Interval1D(1100,1400),
            new Interval1D(0,150),[]));
    }

    [Fact] public void ComponentRejectsDuplicateVoidAndFaceIdentities()
    {
        var polygon=new Polygon2Mm([new(0,0),new(10,0),new(10,10),new(0,10)]);
        var footprint=new Footprint(polygon,[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var solid=new ExtrusionSolid(footprint,frame,new Interval1D(0,10));
        var profile=new CircleProfile(new(5,5),1);
        var opening=new OpeningProfile("O1","C1",OpeningKind.Round,profile,null,profile,null,frame,new Interval1D(0,10),new Interval1D(0,10),[]);
        var face=new FacePatch("F1","C1",FaceRole.Top,profile,frame,new AreaMm2(1),new(0,0,1),null,[]);
        Assert.Throws<ArgumentException>(()=>new ComponentGeometry("C1",ComponentKind.Base,"Concrete",footprint,new Interval1D(0,10),solid,[opening,opening],[],[]));
        Assert.Throws<ArgumentException>(()=>new ComponentGeometry("C1",ComponentKind.Base,"Concrete",footprint,new Interval1D(0,10),solid,[],[face,face],[]));
    }
    [Fact] public void PolygonSnapshotsVerticesAndRejectsCrossing()
    {
        var points=new List<Point2Mm>{new(0,0),new(10,0),new(10,10),new(0,10)};
        var polygon=new Polygon2Mm(points); points.Clear(); Assert.Equal(4,polygon.Vertices.Length);
        Assert.Throws<ArgumentException>(()=>new Polygon2Mm([new(0,0),new(10,10),new(0,10),new(10,0)]));
    }
    [Fact] public void FramesRejectNonOrthonormalDirections()
    {
        Assert.Throws<ArgumentException>(()=>new Frame3Mm(new(0,0,0),new(1,0,0),new(1,0,0),new(0,0,1)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Vector3(double.NaN,0,0));
    }
    [Fact] public void PathRejectsDisconnectedSegments()
    {
        Assert.Throws<ArgumentException>(()=>new BarPath([new LineSegment3Mm(new(0,0,0),new(10,0,0)),new LineSegment3Mm(new(11,0,0),new(20,0,0))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath));
    }
    [Fact] public void NeedsRulePreservesNominalLength()
    {
        var result=new LengthResult(1030,null,LengthStatus.NeedsRule,[new Diagnostic("BEND_RULE_MISSING",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,null,null,null,"Missing bend rule")]);
        Assert.Equal(1030,result.ScheduleLengthMm); Assert.Null(result.FabricationLengthMm);
        Assert.Throws<ArgumentException>(()=>new LengthResult(1030,null,LengthStatus.Known,[]));
    }
    [Fact] public void CatalogSnapshotSurvivesCallerReplacement()
    {
        var pipes=new Dictionary<string,PipeCatalogEntry>{{"D400",new("D400",PipeKind.Round,400,null,null,55,null,0)}};
        var set=new CatalogSet(pipes,new Dictionary<string,SteelAngleEntry>(),new Dictionary<string,HatchEntry>(),new Dictionary<string,string>());
        pipes["D400"]=new("D400",PipeKind.Round,600,null,null,55,null,0); pipes.Clear();
        Assert.Equal(400,set.Pipes["D400"].InnerDiameterMm);
    }
    [Fact] public void ValidatedSpecCannotBePubliclyConstructed()
    {
        Assert.Empty(typeof(ValidatedManholeSpec).GetConstructors());
        Assert.DoesNotContain(typeof(ValidatedManholeSpec).GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public), m => m.ReturnType == typeof(ValidatedManholeSpec));
    }
}
