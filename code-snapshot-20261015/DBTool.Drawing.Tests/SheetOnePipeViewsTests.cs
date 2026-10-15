using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public sealed class SheetOnePipeViewsTests
{
    [Fact]
    public void FarWallCircleAndElevationFollowTheActualPipeOffsetAndInvert()
    {
        var geometry=Fixture();
        var result=SheetOnePipeViews.Build(geometry,new(30,ElevationMode.Absolute,new ElevationM(54),SheetOnePresentationPolicy.Family21Theme,true));
        foreach(var view in new[]{"BB","SteelBB"})
        {
            var inner=Assert.Single(result.OfType<CirclePrimitive>(),p=>p.ViewId==view && p.Role=="Geometry/PipeInner");
            Assert.Equal(new Point2Mm(-70,650),inner.Center);
            Assert.Equal(200,inner.RadiusMm);
            var level=Assert.Single(result.OfType<BlockPrimitive>(),p=>p.ViewId==view);
            Assert.Equal("54.45",Assert.Single(level.Attributes).Value);
            Assert.Equal(new Point2Mm(-70,450),level.Insertion);
            var diameter=Assert.Single(result.OfType<DiameterDimensionPrimitive>(),p=>p.ViewId==view);
            Assert.Equal(inner.Center,diameter.Center);
            Assert.Equal(400,diameter.DiameterMm);
        }
        Assert.DoesNotContain(result.OfType<CirclePrimitive>(),p=>p.ViewId is "AA" or "SteelAA");
        Assert.DoesNotContain(result,p=>p.SemanticId.EndsWith("/Face/CenterH") || p.SemanticId.EndsWith("/Face/CenterV"));
        var centerline=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId=="AA/Pipe/T1/Stub/Center");
        Assert.Equal(new Point2Mm(625,650),centerline.Points[0]);
        Assert.Equal(new Point2Mm(1075,650),centerline.Points[1]);
        var edge=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId=="Plan/Pipe/T1/Stub/Inner/1");
        Assert.Equal(new Point2Mm(-270,625),edge.Points[0]);
        Assert.Equal(new Point2Mm(-270,1175),edge.Points[1]);
        Assert.Equal("Geometry/PlanPipeHidden",edge.Role);
        var visible=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId=="Plan/Pipe/T1/Stub/VisibleOuter/1");
        Assert.Equal(new Point2Mm(-325,775),visible.Points[0]);
        Assert.Equal(new Point2Mm(-325,1175),visible.Points[1]);
        Assert.DoesNotContain(result,p=>p.SemanticId=="Plan/Pipe/T1/Stub/End");
        var planBreak=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId=="Plan/Pipe/T1/Stub/Break");
        Assert.Equal(new Point2Mm(-385,1175),planBreak.Points[0]);
        Assert.Equal(new Point2Mm(-100,1235),planBreak.Points[2]);
        Assert.Equal(new Point2Mm(-40,1115),planBreak.Points[3]);
        var sectionBreak=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId=="AA/Pipe/T1/Stub/End");
        Assert.Equal(6,sectionBreak.Points.Length);
        Assert.Equal(new Point2Mm(1135,680),sectionBreak.Points[2]);
        Assert.Equal(new Point2Mm(1015,620),sectionBreak.Points[3]);
        var material=Assert.Single(result.OfType<HatchPrimitive>(),p=>p.SemanticId=="AA/Pipe/T1/Stub/PipeMaterial/1");
        Assert.Equal("ANSI31",SectionMaterialPolicy.ForRole(material.Role)!.Pattern);
        Assert.Equal(new Point2Mm(625,850),material.OuterLoop[0]);
        Assert.Equal(new Point2Mm(1075,905),material.OuterLoop[2]);
        var grout=Assert.Single(result.OfType<HatchPrimitive>(),p=>p.SemanticId=="AA/Pipe/T1/Stub/Grout/1");
        Assert.Equal("ANSI37",SectionMaterialPolicy.ForRole(grout.Role)!.Pattern);
        Assert.Equal(new Point2Mm(775,955),grout.OuterLoop[2]);
        var plain=geometry.Components.Select(c=>new ComponentGeometry(c.Id,c.Kind,c.MaterialId,c.Footprint,c.ZRange,c.Occupied,[],c.FacePatches,c.Sources));
        Assert.Empty(SheetOnePipeViews.Build(new GeometryModel(plain,[]),new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true)));
    }

    [Fact]
    public void BoxPipeProducesPlanAndSectionStubsWithDimensions()
    {
        var wall=WallFrame.Create(WallId.T2,1000,1250);
        var inner=new RectangleProfile(new(-400,200,400,1000));
        var outer=new RectangleProfile(new(-480,120,480,1080));
        var cutout=new RectangleProfile(new(-530,70,530,1130));
        var op=new OpeningProfile("Pipe/T2","Wall/T2",OpeningKind.Box,inner,outer,cutout,wall,
            new Frame3Mm(wall.Origin,wall.Left,wall.Up,wall.Outward),new(70,1130),new(0,200),[]);
        var fp=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var comp=new ComponentGeometry("Wall/T2",ComponentKind.Wall,"Concrete",fp,z,new ExtrusionSolid(fp,frame,z),[op],[],[]);
        var geom=new GeometryModel([comp], [op]);
        var res=SheetOnePipeViews.Build(geom,new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true));
        Assert.NotEmpty(res.OfType<PolylinePrimitive>().Where(p=>p.Role=="Geometry/PlanPipeVisible"));
        Assert.NotEmpty(res.OfType<PolylinePrimitive>().Where(p=>p.Role=="Geometry/PlanPipeHidden"));
        Assert.NotEmpty(res.OfType<PolylinePrimitive>().Where(p=>p.Role=="Geometry/PlanPipeCenter"));
        var txt=Assert.Single(res.OfType<TextPrimitive>(),p=>p.ViewId=="BB" && p.Role=="DimensionText");
        Assert.Equal("800x800",txt.Content);
    }



    private static GeometryModel Fixture()
    {
        var wall=WallFrame.Create(WallId.T1,1000,1250);
        var op=new OpeningProfile("Pipe/T1","Wall/T1",OpeningKind.Round,new CircleProfile(new(70,650),200),
            new CircleProfile(new(70,650),255),new CircleProfile(new(70,650),305),wall,
            new Frame3Mm(wall.Origin,wall.Left,wall.Up,wall.Outward),new(345,955),new(0,150),[]);
        var fp=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        return new([new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",fp,z,new ExtrusionSolid(fp,frame,z),[op],[],[])],[op]);
    }
}
