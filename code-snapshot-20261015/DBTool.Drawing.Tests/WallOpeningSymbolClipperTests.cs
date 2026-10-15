using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class WallOpeningSymbolClipperTests
{
    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void ActualWallFramesClipAnOffsetOpeningOnTheCorrectWorldSide(WallId wall)
    {
        var basis=WallFrame.Create(wall,1600,1200);
        string owner="Wall/"+wall;
        var circle=new CircleProfile(new(150,600),50);
        var opening=new OpeningProfile("offset",owner,OpeningKind.Round,circle,circle,circle,basis,
            new(basis.Origin,basis.Left,basis.Up,basis.Outward),new(550,650),new(0,150),[]);
        var footprint=new Footprint(new Polygon2Mm([new(-1000,-1000),new(1000,-1000),new(1000,1000),new(-1000,1000)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var component=new ComponentGeometry(owner,ComponentKind.Wall,"Concrete",footprint,new(0,2000),
            new ExtrusionSolid(footprint,frame,new(0,2000)),[opening],[],[]);
        var model=new GeometryModel([component],[opening]);
        Point2Mm Project(double u)
        { var p=basis.ToGlobal(u,0,600);return new(wall is WallId.T2 or WallId.T4?p.Y:p.X,p.Z); }
        var bar=new PolylinePrimitive("mesh","Rebar/Main","WallView",owner,"bar","D1",[Project(0),Project(300)],false);
        var result=WallOpeningSymbolClipper.ClipMesh([bar],model,owner);
        Assert.True(result.Success);Assert.Equal(2,result.Value!.Count);
        Assert.Equal(Project(100),result.Value[0].Points[1]);
        Assert.Equal(Project(200),result.Value[1].Points[0]);
        var hits=new BlockPrimitive("X","Rebar/SheetTwoTieWall","WallView",owner,"Block:BLTC",Project(150),.3,[],occurrenceId:"tie");
        var clipped=WallOpeningSymbolClipper.Clip([hits],model,owner);
        Assert.True(clipped.Success);Assert.Empty(clipped.Value!);
    }

    [Fact]
    public void MeshClipsEveryOpeningExactlyAndRetainsSourceAndDirection()
    {
        var model=Model(Opening("one",new RectangleProfile(new(-300,100,-200,200))),
            Opening("two",new CircleProfile(new(200,150),50)));
        var bar=new PolylinePrimitive("physical","Rebar/Main","WallView","Wall/T1","bar-1","D1",[new(500,150),new(-500,150)],false);
        var result=WallOpeningSymbolClipper.ClipMesh([bar],model,"Wall/T1");
        Assert.True(result.Success);Assert.Equal(3,result.Value!.Count);
        Assert.Equal(new Point2Mm(250,150),result.Value[0].Points[1]);
        Assert.Equal(new Point2Mm(150,150),result.Value[1].Points[0]);
        Assert.Equal(new Point2Mm(-200,150),result.Value[1].Points[1]);
        Assert.Equal(new Point2Mm(-300,150),result.Value[2].Points[0]);
        Assert.All(result.Value,p=> {Assert.Equal("bar-1",p.OccurrenceId);Assert.Equal("D1",p.ScheduleMark);Assert.True(p.Points[0].X>p.Points[1].X);});
        Assert.Equal(1000,bar.Points[0].X-bar.Points[1].X);
    }
    [Fact]
    public void MeshUsesChamferContourRatherThanItsBoundingBox()
    {
        var model=Model(Opening("diamond",new PolygonProfile(new Polygon2Mm([new(-100,0),new(0,-100),new(100,0),new(0,100)]))));
        var bar=new PolylinePrimitive("physical","Rebar/Main","WallView","Wall/T1","bar-1","D1",[new(-200,75),new(200,75)],false);
        var result=WallOpeningSymbolClipper.ClipMesh([bar],model,"Wall/T1");
        Assert.True(result.Success);Assert.Equal(2,result.Value!.Count);
        Assert.Equal(new Point2Mm(-25,75),result.Value[0].Points[1]);
        Assert.Equal(new Point2Mm(25,75),result.Value[1].Points[0]);
    }

    private static GeometryModel Model(params OpeningProfile[] openings)
    {
        var footprint=new Footprint(new Polygon2Mm([new(-2000,-100),new(2000,-100),new(2000,100),new(-2000,100)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var component=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,new(0,2000),
            new ExtrusionSolid(footprint,frame,new(0,2000)),openings,[],[]);
        return new([component],openings);
    }
    private static OpeningProfile Opening(string id,Profile2Mm cutout,Frame3Mm? frame=null)=>new(id,"Wall/T1",OpeningKind.Polygon,
        cutout,null,cutout,null,frame ?? new(new(0,0,0),new(1,0,0),new(0,0,1),new(0,-1,0)),new(0,1000),new(0,150),[]);
    private static BlockPrimitive Symbol(string id,double x,double z,string block="BLTC",double rotation=0)=>
        new(id,"Rebar/SheetTwoTieWall","WallView","Wall/T1","Block:"+block,new(x,z),.3,[],rotation,occurrenceId:id);

    [Fact]
    public void LongBoxDoesNotEraseSymbolOutsideItsActualHeight()
    {
        var model=Model(Opening("box",new RectangleProfile(new(-500,-50,500,50))));
        var symbol=Symbol("keep",0,120);
        var result=WallOpeningSymbolClipper.Clip([symbol],model,"Wall/T1");
        Assert.True(result.Success);Assert.Same(symbol,Assert.Single(result.Value!));
    }
    [Fact]
    public void EveryOpeningAndWholeNativeStrokeAreChecked()
    {
        var model=Model(Opening("first",new CircleProfile(new(0,100),20)),
            Opening("second",new RectangleProfile(new(400,80,440,120))));
        var result=WallOpeningSymbolClipper.Clip([Symbol("first-hit",25,100),Symbol("second-hit",420,100),Symbol("keep",700,100)],model,"Wall/T1");
        Assert.True(result.Success);Assert.Equal("keep",Assert.Single(result.Value!).SemanticId);
    }
    [Fact]
    public void ChamferDoesNotBecomeBoundingRectangle()
    {
        var polygon=new PolygonProfile(new Polygon2Mm([new(-100,0),new(0,-100),new(100,0),new(0,100)]));
        var model=Model(Opening("diamond",polygon));
        var symbol=Symbol("outside-chamfer",90,90,"Gach");
        var result=WallOpeningSymbolClipper.Clip([symbol],model,"Wall/T1");
        Assert.True(result.Success);Assert.Single(result.Value!);
    }
    [Fact]
    public void MirroredWallFrameMapsOffsetToCorrectWorldSide()
    {
        var frame=new Frame3Mm(new(1000,0,200),new(-1,0,0),new(0,0,1),new(0,1,0));
        var model=Model(Opening("offset",new RectangleProfile(new(100,300,200,400)),frame));
        var result=WallOpeningSymbolClipper.Clip([Symbol("hit",850,550),Symbol("keep",1150,550)],model,"Wall/T1");
        Assert.True(result.Success);Assert.Equal("keep",Assert.Single(result.Value!).SemanticId);
    }
}
