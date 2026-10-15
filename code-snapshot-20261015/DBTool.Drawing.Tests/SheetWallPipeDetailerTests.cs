using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetWallPipeDetailerTests
{
    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void EveryRoundOpeningUsesItsPhysicalFrameWithoutInventedSteel(WallId wallId)
    {
        var frame=WallFrame.Create(wallId,1000,1200);
        string owner="Wall/"+wallId;
        OpeningProfile Opening(string id,double u,double z)=>new(id,owner,OpeningKind.Round,
            new CircleProfile(new(u,z),10),new CircleProfile(new(u,z),15),new CircleProfile(new(u,z),20),
            frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),new(z-20,z+20),new(0,150),[]);
        var first=Opening("pipe-one",120,300);var second=Opening("pipe-two",-80,700);
        var model=new GeometryModel([MakeWallComp(owner,first)],[second,first]);
        var primitives=SheetWallPipeDetailer.BuildPipePrimitives(model,new(30,ElevationMode.Symbolic,null,"S2",true),
            "wall-view",owner,new(-1000,0,1000,2000));
        Assert.Equal(primitives.Count,primitives.Select(p=>p.SemanticId).Distinct().Count());
        Assert.All(primitives,p=>Assert.Equal("wall-view",p.ViewId));
        Assert.DoesNotContain(primitives,p=>p.Role.StartsWith("Rebar/") || p.Role==SheetTwoBarAnnotations.Role);
        var contours=primitives.OfType<CirclePrimitive>().Where(p=>p.Role=="Geometry/ConcreteCutout").ToArray();
        Assert.Equal(2,contours.Length);
        foreach(var op in new[]{first,second})
        {
            var circle=(CircleProfile)op.ConcreteCutout;
            var point=frame.ToGlobal(circle.Center.X,0,circle.Center.Y);
            var drawn=Assert.Single(contours,p=>p.SemanticId.Contains("/"+op.Id+"/"));
            Assert.Equal(new Point2Mm(wallId is WallId.T2 or WallId.T4?point.Y:point.X,point.Z),drawn.Center);
            Assert.Equal(circle.RadiusMm,drawn.RadiusMm);
        }
    }

    private static ComponentGeometry MakeWallComp(string wallId, OpeningProfile op)
    {
        var fp = new Footprint(new Polygon2Mm([new(-1000, -1000), new(1000, -1000), new(1000, 1000), new(-1000, 1000)]), []);
        var z = new Interval1D(0, 2000);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        return new ComponentGeometry(wallId, ComponentKind.Wall, "Concrete", fp, z, new ExtrusionSolid(fp, frame, z), [op], [], []);
    }

    [Fact]
    public void NonIntersectingBarIsNotStretchedByHoleCutout()
    {
        // Hole at (0, 100) with rCut = 20 (cutout interval along Y: [80, 120])
        var wall = WallFrame.Create(WallId.T1, 1000, 1200);
        var op = new OpeningProfile("Pipe/T1", "Wall/T1", OpeningKind.Round,
            new CircleProfile(new(0, 100), 10),
            new CircleProfile(new(0, 100), 15),
            new CircleProfile(new(0, 100), 20),
            wall, new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward),
            new(80, 120), new(0, 200), []);
        var comp = MakeWallComp("Wall/T1", op);
        var geom = new GeometryModel([comp], [op]);

        // Bar at x = 0, y from 0 to 10. Does NOT touch [80, 120].
        var bar = new PolylinePrimitive("bar1", "Rebar/Main", "View", "Wall/T1", null, null,
            [new Point2Mm(0, 0), new Point2Mm(0, 10)], false);

        var clipped = SheetWallPipeDetailer.ClipWallMesh([bar], geom, "Wall/T1");

        // The bar must remain unchanged with length = 10, not extended to 80
        var single = Assert.Single(clipped);
        Assert.Equal(new Point2Mm(0, 0), single.Points[0]);
        Assert.Equal(new Point2Mm(0, 10), single.Points[1]);
    }

    [Fact]
    public void IntersectingBarIsSplitAndTotalLengthDoesNotExceedOriginal()
    {
        // Hole at (0, 100) with rCut = 20 (cutout interval along Y: [70, 130])
        var wall = WallFrame.Create(WallId.T1, 1000, 1200);
        var op = new OpeningProfile("Pipe/T1", "Wall/T1", OpeningKind.Round,
            new CircleProfile(new(0, 100), 10),
            new CircleProfile(new(0, 100), 15),
            new CircleProfile(new(0, 100), 20),
            wall, new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward),
            new(80, 120), new(0, 200), []);
        var comp = MakeWallComp("Wall/T1", op);
        var geom = new GeometryModel([comp], [op]);

        // Bar at x = 0, y from 0 to 200. Crosses [70, 130].
        var bar = new PolylinePrimitive("bar1", "Rebar/Main", "View", "Wall/T1", null, null,
            [new Point2Mm(0, 0), new Point2Mm(0, 200)], false);

        var clipped = SheetWallPipeDetailer.ClipWallMesh([bar], geom, "Wall/T1");

        Assert.Equal(2, clipped.Count);
        double totalLength = clipped.Sum(b => Math.Abs(b.Points[1].Y - b.Points[0].Y));
        Assert.True(totalLength < 200.0);
    }

    [Fact]
    public void BoxPipeOnWallIsRecognizedAndGeneratesBoxPrimitives()
    {
        var wall = WallFrame.Create(WallId.T2, 2000, 2000);
        var rectInner = new RectangleProfile(new Rectangle2Mm(-500, -500, 500, 500));
        var rectOuter = new RectangleProfile(new Rectangle2Mm(-550, -550, 550, 550));
        var rectCutout = new RectangleProfile(new Rectangle2Mm(-600, -600, 600, 600));

        var op = new OpeningProfile("Pipe/T2", "Wall/T2", OpeningKind.Box,
            rectInner, rectOuter, rectCutout,
            wall, new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward),
            new(400, 1600), new(0, 200), []);
        var comp = MakeWallComp("Wall/T2", op);
        var geom = new GeometryModel([comp], [op]);

        Assert.True(SheetWallPipeDetailer.HasPipe(geom, "Wall/T2"));

        var prims = SheetWallPipeDetailer.BuildPipePrimitives(
            geom, new(30, ElevationMode.Symbolic, null, "S2", true),
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top", "Wall/T2", new(-1000, 0, 1000, 2000));

        Assert.NotEmpty(prims);
        Assert.Contains(prims, p => p.Role=="Geometry/ConcreteCutout");
        Assert.DoesNotContain(prims,p=>p.Role.StartsWith("Rebar/"));
    }

    [Fact]
    public void Par50_BoxPipeWithChamferPreservesEightVerticesAndDoesNotCollapseToBox()
    {
        var wall = WallFrame.Create(WallId.T2, 2000, 2000);
        double a = 50.0;
        double w = 500.0, h = 500.0;
        var chamferVertices = new Point2Mm[]
        {
            new(-w + a, -h), new(w - a, -h), new(w, -h + a), new(w, h - a),
            new(w - a, h), new(-w + a, h), new(-w, h - a), new(-w, -h + a)
        };
        var polyInner = new PolygonProfile(new Polygon2Mm(chamferVertices));
        var rectOuter = new RectangleProfile(new Rectangle2Mm(-550, -550, 550, 550));
        var rectCutout = new RectangleProfile(new Rectangle2Mm(-600, -600, 600, 600));

        var op = new OpeningProfile("Pipe/T2/Chamfer", "Wall/T2", OpeningKind.Box,
            polyInner, rectOuter, rectCutout,
            wall, new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward),
            new(400, 1600), new(0, 200), []);
        var comp = MakeWallComp("Wall/T2", op);
        var geom = new GeometryModel([comp], [op]);

        var prims = SheetWallPipeDetailer.BuildPipePrimitives(
            geom, new(30, ElevationMode.Symbolic, null, "S2", true),
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top", "Wall/T2", new(-1000, 0, 1000, 2000));

        var innerPoly = prims.OfType<PolylinePrimitive>().Single(p => p.SemanticId.EndsWith("/Pipe/Inner"));
        Assert.Equal(8, innerPoly.Points.Length);
        Assert.True(innerPoly.Closed);
    }

    [Fact]
    public void Par50_DoubleBoxPipePreservesBothInnerCellsSeparatelyWithDivider()
    {
        var wall = WallFrame.Create(WallId.T2, 2000, 2000);
        var cell1 = new PolygonProfile(new Polygon2Mm([new(-500, -400), new(-50, -400), new(-50, 400), new(-500, 400)]));
        var cell2 = new PolygonProfile(new Polygon2Mm([new(50, -400), new(500, -400), new(500, 400), new(50, 400)]));
        var multiInner = new MultiCellProfile([cell1, cell2]);
        var rectOuter = new RectangleProfile(new Rectangle2Mm(-550, -450, 550, 450));
        var rectCutout = new RectangleProfile(new Rectangle2Mm(-600, -500, 600, 500));

        var op = new OpeningProfile("Pipe/T2/DoubleBox", "Wall/T2", OpeningKind.Box,
            multiInner, rectOuter, rectCutout,
            wall, new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward),
            new(500, 1500), new(0, 200), []);
        var comp = MakeWallComp("Wall/T2", op);
        var geom = new GeometryModel([comp], [op]);

        var prims = SheetWallPipeDetailer.BuildPipePrimitives(
            geom, new(30, ElevationMode.Symbolic, null, "S2", true),
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top", "Wall/T2", new(-1000, 0, 1000, 2000));

        var inner1 = prims.OfType<PolylinePrimitive>().Single(p => p.SemanticId.EndsWith("/Pipe/Inner/1"));
        var inner2 = prims.OfType<PolylinePrimitive>().Single(p => p.SemanticId.EndsWith("/Pipe/Inner/2"));
        Assert.Equal(4, inner1.Points.Length);
        Assert.Equal(4, inner2.Points.Length);
        Assert.True(inner1.Points.Max(p => p.X) < inner2.Points.Min(p => p.X));
    }
}
