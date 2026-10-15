using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class WallOpeningConsistencyTests
{
    [Fact]
    public void BoxCulvertsOnT2AndT4DoNotAppearOnT1OrT3()
    {
        // Set up WallFrames
        var frameT1 = WallFrame.Create(WallId.T1, 1000, 1200);
        var frameT2 = WallFrame.Create(WallId.T2, 1000, 1200);
        var frameT3 = WallFrame.Create(WallId.T3, 1000, 1200);
        var frameT4 = WallFrame.Create(WallId.T4, 1000, 1200);

        var opT2 = new OpeningProfile(
            id: "Wall/T2/Pipe/Box800",
            ownerId: "Wall/T2",
            kind: OpeningKind.Box,
            inner: new RectangleProfile(new(-400, 200, 400, 1000)),
            pipeOuter: new RectangleProfile(new(-450, 150, 450, 1050)),
            concreteCutout: new RectangleProfile(new(-500, 100, 500, 1100)),
            wallFrame: frameT2,
            frame: new Frame3Mm(frameT2.Origin, frameT2.Left, frameT2.Up, frameT2.Outward),
            zRange: new(100, 1100),
            extrusionRange: new(0, 200),
            sources: []);

        var opT4 = new OpeningProfile(
            id: "Wall/T4/Pipe/Box1000",
            ownerId: "Wall/T4",
            kind: OpeningKind.Box,
            inner: new RectangleProfile(new(-500, 300, 500, 1100)),
            pipeOuter: new RectangleProfile(new(-550, 250, 550, 1150)),
            concreteCutout: new RectangleProfile(new(-600, 200, 600, 1200)),
            wallFrame: frameT4,
            frame: new Frame3Mm(frameT4.Origin, frameT4.Left, frameT4.Up, frameT4.Outward),
            zRange: new(200, 1200),
            extrusionRange: new(0, 200),
            sources: []);

        var fp = new Footprint(new Polygon2Mm([new(-1000, -1000), new(1000, -1000), new(1000, 1000), new(-1000, 1000)]), []);
        var z = new Interval1D(0, 2000);
        var f3 = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));

        var compT1 = new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", fp, z, new ExtrusionSolid(fp, f3, z), [], [], []);
        var compT2 = new ComponentGeometry("Wall/T2", ComponentKind.Wall, "Concrete", fp, z, new ExtrusionSolid(fp, f3, z), [opT2], [], []);
        var compT3 = new ComponentGeometry("Wall/T3", ComponentKind.Wall, "Concrete", fp, z, new ExtrusionSolid(fp, f3, z), [], [], []);
        var compT4 = new ComponentGeometry("Wall/T4", ComponentKind.Wall, "Concrete", fp, z, new ExtrusionSolid(fp, f3, z), [opT4], [], []);

        var geom = new GeometryModel([compT1, compT2, compT3, compT4], [opT2, opT4]);

        // Build views for all 4 walls
        var primsT1 = SheetWallPipeDetailer.BuildPipePrimitives(geom, new(30, ElevationMode.Symbolic, null, "S2", true), "Wall/T1", "Wall/T1", new(-1000, 0, 1000, 2000));
        var primsT2 = SheetWallPipeDetailer.BuildPipePrimitives(geom, new(30, ElevationMode.Symbolic, null, "S2", true), "Wall/T2", "Wall/T2", new(-1000, 0, 1000, 2000));
        var primsT3 = SheetWallPipeDetailer.BuildPipePrimitives(geom, new(30, ElevationMode.Symbolic, null, "S2", true), "Wall/T3", "Wall/T3", new(-1000, 0, 1000, 2000));
        var primsT4 = SheetWallPipeDetailer.BuildPipePrimitives(geom, new(30, ElevationMode.Symbolic, null, "S2", true), "Wall/T4", "Wall/T4", new(-1000, 0, 1000, 2000));

        // T1 and T3 must have NO pipe primitives
        Assert.Empty(primsT1);
        Assert.Empty(primsT3);

        // T2 and T4 must have primitives corresponding to their own OpeningId
        Assert.NotEmpty(primsT2);
        Assert.All(primsT2, p => Assert.Equal(opT2.Id, p.OccurrenceId));

        Assert.NotEmpty(primsT4);
        Assert.All(primsT4, p => Assert.Equal(opT4.Id, p.OccurrenceId));

        // Check OpeningViewMapExporter
        var pkg = new DrawingPackage("a".PadLeft(64, '0'), "DBTOOL-FINAL-SPEC-20261012-v1",
            [new("Wall/T1", ViewPurpose.Section, new(0, 0), new Bounds2Mm(0, 0, 2000, 2000), 30),
             new("Wall/T2", ViewPurpose.Section, new(0, 0), new Bounds2Mm(0, 0, 2000, 2000), 30),
             new("Wall/T3", ViewPurpose.Section, new(0, 0), new Bounds2Mm(0, 0, 2000, 2000), 30),
             new("Wall/T4", ViewPurpose.Section, new(0, 0), new Bounds2Mm(0, 0, 2000, 2000), 30)],
            [.. primsT2, .. primsT4], new Bounds2Mm(0, 0, 2000, 2000), CalculationScope.FullRebar);

        var map = OpeningViewMapExporter.Build(geom, pkg);
        Assert.Equal(2, map.Openings.Length);

        var mapT2 = Assert.Single(map.Openings, o => o.OpeningId == opT2.Id);
        Assert.Equal("Wall/T2", mapT2.OwnerId);
        Assert.Equal("Box", mapT2.Kind);

        var viewT2Entry = Assert.Single(mapT2.Views, v => v.ViewId == "Wall/T2");
        Assert.Equal("Visible", viewT2Entry.VisibleMode);
        Assert.NotEmpty(viewT2Entry.LocalContourIds);

        var viewT1Entry = Assert.Single(mapT2.Views, v => v.ViewId == "Wall/T1");
        Assert.Equal("NotCut", viewT1Entry.VisibleMode);

        string json = map.ToJson();
        Assert.Contains(opT2.Id, json);
        Assert.Contains(opT4.Id, json);
        Assert.Contains("Visible", json);
        Assert.Contains("NotCut", json);
    }
}
