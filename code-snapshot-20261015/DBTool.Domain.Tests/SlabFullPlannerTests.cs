using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class SlabFullPlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void S2BaseMeshSpansToConcreteExpansionBoundary()
    {
        var seed = SyntheticTestFactory.Create();
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, Policy.Version, seed.Sources, []);
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var result = SlabRebarPlanner.PlanS2Base(spec, geometry, Policy);
        Assert.True(result.Success);
        double x = seed.Input.L / 2 + seed.Input.WallThicknessMm + seed.Input.BaseExpansionMm - seed.Input.CoverMm;
        double y = seed.Input.B / 2 + seed.Input.WallThicknessMm + seed.Input.BaseExpansionMm - seed.Input.CoverMm;
        Assert.All(result.Value!.Occurrences, bar => Assert.All(bar.Path.Segments, segment => {
            Assert.InRange(segment.Start.X, -x - 1e-3, x + 1e-3); Assert.InRange(segment.End.X, -x - 1e-3, x + 1e-3);
            Assert.InRange(segment.Start.Y, -y - 1e-3, y + 1e-3); Assert.InRange(segment.End.Y, -y - 1e-3, y + 1e-3);
        }));
        // When BaseExpansionMm > 0, the bar extends beyond the wall outer face
        double wallOuterX = seed.Input.L / 2 + seed.Input.WallThicknessMm;
        Assert.True(result.Value.Occurrences.Any(b => b.Path.Segments.Any(s => Math.Abs(s.End.X) > wallOuterX - seed.Input.CoverMm)));
    }

    [Fact]
    public void RoofClipsEveryRectangularOpening()
    {
        var component = RectangularRoof(-500, -35, 500, 35, new Rectangle2Mm(-250, -20, -150, 20));
        var first = component.Voids.Single();
        var second = new OpeningProfile("Roof/Hole2", "Roof", OpeningKind.Box,
            new RectangleProfile(new Rectangle2Mm(150, -20, 250, 20)), null,
            new RectangleProfile(new Rectangle2Mm(150, -20, 250, 20)), null,
            first.Frame, first.ZRange, first.ExtrusionRange, []);
        var roof = new ComponentGeometry(component.Id, component.Kind, component.MaterialId, component.Footprint,
            component.ZRange, component.Occupied, [first, second], [], []);
        var result = SlabRebarPlanner.Plan(SyntheticTestFactory.Create(), new GeometryModel([roof], [first, second]),
            new([], []), new SlabLayerPolicy(new(10, 200), new(10, 200), 30, false, true, 30,
                activeRoofLayers: [Layer.Bottom], activeRoofAxes: [Axis.X]));
        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences, bar => {
            var line = Assert.IsType<LineSegment3Mm>(Assert.Single(bar.Path.Segments));
            Assert.False(line.Start.X < 285 && line.End.X > 115);
            Assert.False(line.Start.X < -115 && line.End.X > -285);
        });
    }

    [Fact]
    public void UnequalDirectionDiametersStackByPhysicalRadius()
    {
        var roof=RectangularRoof(-500,-600,500,600,new Rectangle2Mm(-100,-150,100,150));
        var result=SlabRebarPlanner.Plan(SyntheticTestFactory.Create(),new GeometryModel([roof],roof.Voids),new([],[]),
            new SlabLayerPolicy(new(16,150),new(8,150),new(12,150),new(10,150),30,false,true,30));
        Assert.True(result.Success);
        var bars=result.Value!.Occurrences;
        Assert.Equal(38,bars.First(b=>b.Layer==Layer.Bottom && b.Axis==Axis.X).Path.Segments[0].Start.Z);
        Assert.Equal(50,bars.First(b=>b.Layer==Layer.Bottom && b.Axis==Axis.Y).Path.Segments[0].Start.Z);
        Assert.Equal(114,bars.First(b=>b.Layer==Layer.Top && b.Axis==Axis.X).Path.Segments[0].Start.Z);
        Assert.Equal(103,bars.First(b=>b.Layer==Layer.Top && b.Axis==Axis.Y).Path.Segments[0].Start.Z);
    }

    [Fact]
    public void RoofCentersStackWithoutIntersectionAndKeepPhysicalEdgeCover()
    {
        var roof=RectangularRoof(-500,-600,500,600,new Rectangle2Mm(-100,-150,100,150));
        var seed=SyntheticTestFactory.Create();
        var result=SlabRebarPlanner.Plan(seed,new GeometryModel([roof],roof.Voids),new([],[]),
            new SlabLayerPolicy(new(10,150),new(10,200),30,false,true,30));
        Assert.True(result.Success);
        var bars=result.Value!.Occurrences;
        Assert.Equal(35,bars.First(b=>b.Layer==Layer.Bottom && b.Axis==Axis.X).Path.Segments[0].Start.Z);
        Assert.Equal(45,bars.First(b=>b.Layer==Layer.Bottom && b.Axis==Axis.Y).Path.Segments[0].Start.Z);
        Assert.Equal(115,bars.First(b=>b.Layer==Layer.Top && b.Axis==Axis.X).Path.Segments[0].Start.Z);
        Assert.Equal(105,bars.First(b=>b.Layer==Layer.Top && b.Axis==Axis.Y).Path.Segments[0].Start.Z);
        Assert.All(bars,b=>{
            Assert.True(double.IsFinite(b.ScheduleLengthMm) && b.ScheduleLengthMm>0);
            foreach(var segment in b.Path.Segments)
            {
                Assert.InRange(segment.Start.X,-465,465);Assert.InRange(segment.End.X,-465,465);
                Assert.InRange(segment.Start.Y,-565,565);Assert.InRange(segment.End.Y,-565,565);
            }
        });
    }

    [Fact]
    public void BasePlanOverloadPreservesTheS1Regression()
    {
        var seed = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(seed, CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var baseComponent = geometry.Components.Single(x => x.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, seed.Input.BaseBottomMesh, seed.Input.CoverMm,
            CalculationPolicy.Select("S1-Analytic-1", true).Value!).Value!;
        var set = MeshZoneSet.FromLegacy(zones);
        var result = SlabRebarPlanner.Plan(seed, geometry, set,
            SlabLayerPolicy.BaseOnly(seed.Input.BaseBottomMesh, seed.Input.BaseTopMesh, seed.Input.CoverMm));

        Assert.True(result.Success);
        Assert.Equal(48, result.Value!.Occurrences.Length);
        Assert.Equal(74620, result.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);
    }

    [Fact]
    public void RoofOpeningCreatesTwoSurvivingChildrenPerActiveLayerWithoutNewHooks()
    {
        var component = RectangularRoof(-470, -35, 470, 35, new Rectangle2Mm(-100, -20, 100, 20));
        var opening = component.Voids.Single();
        var geometry = new GeometryModel([component], [opening]);
        var seed = SyntheticTestFactory.Create();
        var set = new MeshZoneSet([], []);
        var policy = new SlabLayerPolicy(new MeshSpec(10, 200), new MeshSpec(10, 200), 30,
            includeBase: false, includeRoof: true, openingClearanceMm: 35, activeRoofLayers: [Layer.Bottom, Layer.Top],
            activeRoofAxes: [Axis.X]);

        var result = SlabRebarPlanner.Plan(seed, geometry, set, policy);

        Assert.True(result.Success);
        Assert.Equal(4, result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences, bar => {
            Assert.Equal(295, bar.ScheduleLengthMm, 6);
            Assert.Equal(HookPattern.Hook0M, bar.Path.HookPattern);
            Assert.NotNull(bar.ParentId);
        });
        Assert.Equal(1180, result.Value.Occurrences.Sum(x => x.ScheduleLengthMm), 6);
        Assert.Contains(result.Value.Diagnostics, x => x.Code == "ROOF_OPENING_CLIPPED" && x.Actual == "280");
    }

    private static ComponentGeometry RectangularRoof(double minX, double minY, double maxX, double maxY, Rectangle2Mm hole)
    {
        var outer = new Polygon2Mm([new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)]);
        var inner = new Polygon2Mm([new(hole.MinX, hole.MaxY), new(hole.MaxX, hole.MaxY), new(hole.MaxX, hole.MinY), new(hole.MinX, hole.MinY)]);
        var footprint = new Footprint(outer, [inner]);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var opening = new OpeningProfile("Roof/Hole", "Roof", OpeningKind.Box,
            new RectangleProfile(hole), null, new RectangleProfile(hole), null, frame,
            new Interval1D(0, 150), new Interval1D(0, 150), []);
        return new ComponentGeometry("Roof", ComponentKind.Roof, "Concrete", footprint,
            new Interval1D(0, 150), new ExtrusionSolid(new Footprint(outer, []), frame, new Interval1D(0, 150)),
            [opening], [], []);
    }
}
