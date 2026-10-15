using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class FormworkTests
{
    [Fact]
    public void SurfacePatchBuilderCreatesNetHorizontalPatchesForABox()
    {
        var component = Box("Base", ComponentKind.Base, "Concrete", 0, 0, 100, 200, 0, 50);

        var result = SurfacePatchBuilder.Build(component);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Length);
        Assert.Equal(40_000, result.Value.Sum(x => x.AreaMm2.Value));
    }

    [Fact]
    public void BaseSideFormworkExcludesBottomUntilPolicyRequiresIt()
    {
        var baseComponent = Box("Base", ComponentKind.Base, "Concrete", -750, -875, 750, 875, -150, 0);

        var excluded = new FormworkCalculator().Calculate(Model(baseComponent),
            new FormworkPolicy("FW-1", castAgainstGround: true, baseBottomRequired: false), Tolerance());
        var included = new FormworkCalculator().Calculate(Model(baseComponent),
            new FormworkPolicy("FW-1", castAgainstGround: false, baseBottomRequired: true), Tolerance());

        Assert.True(excluded.Success);
        Assert.True(included.Success);
        AssertClose(0.975, Line(excluded.Value!, "Base").Net);
        AssertClose(3.6, Line(included.Value!, "Base").Net);
        Assert.Contains(excluded.Value!.FaceTrace, x => !x.Included && x.FaceId == "Base/Bottom");
    }

    [Fact]
    public void WallFacesSubtractOneOpeningPerFaceAndAddClosedRoundReveal()
    {
        var wall = WallWithOpening("Wall/T1", OpeningKind.Round, new CircleProfile(new(0, 500), 305), 150);
        var result = new FormworkCalculator().Calculate(Model(wall),
            new FormworkPolicy("FW-1", wallInnerRequired: true, wallOuterRequired: true, openingRevealRequired: true), Tolerance());

        Assert.True(result.Success);
        AssertClose(13.982962414603087, Line(result.Value!, "Wall/T1").Net);
        Assert.Contains(result.Value!.FaceTrace, x => x.Included && x.FaceId.Contains("Reveal", StringComparison.Ordinal));
        Assert.Equal(2, result.Value!.FaceTrace.Count(x => !x.Included && x.Reason == "Opening cut"));
    }

    [Fact]
    public void OpeningRevealUsesThreeEdgesForOpenTopAndFourForClosedBox()
    {
        var closed = WallWithOpening("Closed", OpeningKind.Box, new RectangleProfile(new(-200, 200, 200, 500)), 150);
        var open = WallWithOpening("Open", OpeningKind.OpenTop, new RectangleProfile(new(-200, 200, 200, 500)), 150);

        var result = new FormworkCalculator().Calculate(Model(closed, open),
            new FormworkPolicy("FW-1", wallInnerRequired: false, wallOuterRequired: false, openingRevealRequired: true), Tolerance());

        Assert.True(result.Success);
        AssertClose(0.21, Line(result.Value!, "Closed").Net);
        AssertClose(0.15, Line(result.Value!, "Open").Net);
    }

    [Fact]
    public void RoofCastModeSelectsSoffitAreaWithoutTopFaceAccident()
    {
        var roof = BoxWithOpening("Roof", ComponentKind.Roof, -650, -775, 650, 775, 1400, 1550,
            new RectangleProfile(new(-350, -350, 350, 350)));
        var inSitu = new FormworkCalculator().Calculate(Model(roof),
            new FormworkPolicy("FW-1", roofCastMode: RoofCastMode.InSitu, openingRevealRequired: true), Tolerance());
        var precast = new FormworkCalculator().Calculate(Model(roof),
            new FormworkPolicy("FW-1", roofCastMode: RoofCastMode.PrecastBed, openingRevealRequired: true), Tolerance());

        Assert.True(inSitu.Success);
        Assert.True(precast.Success);
        AssertClose(2.035, Line(inSitu.Value!, "Roof").Net);
        AssertClose(2.8, Line(precast.Value!, "Roof").Net);
        Assert.DoesNotContain(inSitu.Value!.FaceTrace, x => x.Included && x.FaceId.Contains("Top", StringComparison.Ordinal));
    }

    [Fact]
    public void ContactFaceIsExcludedAndInclinedPatchUsesItsOwnArea()
    {
        var first = BoxWithFaces("A", ComponentKind.Wall, 0, 0, 100, 100, 0, 100,
            [new FacePatch("A/Contact", "A", FaceRole.Contact, RectProfile(100, 100), Frame(), new AreaMm2(10000), new(1, 0, 0), "B", [])]);
        var second = BoxWithFaces("B", ComponentKind.Wall, 100, 0, 200, 100, 0, 100,
            [new FacePatch("B/Contact", "B", FaceRole.Contact, RectProfile(100, 100), Frame(), new AreaMm2(10000), new(-1, 0, 0), "A", [])]);
        var slope = BoxWithFaces("Slope", ComponentKind.Wall, 0, 0, 1000, 100, 0, 100,
            [new FacePatch("Slope/Inclined", "Slope", FaceRole.Inner, RectProfile(100, 100), Frame(),
                new AreaMm2(141421.3562373095), new(0, 1, 0), null, [])]);

        var result = new FormworkCalculator().Calculate(Model(first, second, slope),
            new FormworkPolicy("FW-1", wallInnerRequired: true, wallOuterRequired: false, contactExcluded: true), Tolerance());

        Assert.True(result.Success);
        AssertClose(0, Line(result.Value!, "A").Net);
        AssertClose(0, Line(result.Value!, "B").Net);
        AssertClose(0.1414213562373095, Line(result.Value!, "Slope").Net);
        Assert.All(result.Value!.FaceTrace.Where(x => x.FaceId.Contains("Contact", StringComparison.Ordinal)), x => Assert.False(x.Included));
    }

    private static double SurfaceFaces(ComponentGeometry component) => component.FacePatches.Sum(x => x.AreaMm2.Value / 1e6);
    private static QuantityLine Line(FormworkResult result, string id) => Assert.Single(result.Lines.Where(x => x.ComponentId == id));
    private static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-9);
    private static TolerancePolicy Tolerance() => new(1e-6, 1e-6, 1e-9, 1e-6, 1);

    private static GeometryModel Model(params ComponentGeometry[] components) =>
        new(components, components.SelectMany(c => c.Voids).DistinctBy(x => x.Id));

    private static ComponentGeometry Box(string id, ComponentKind kind, string material, double x0, double y0, double x1, double y1, double z0, double z1,
        params OpeningProfile[] openings) => BoxWithFaces(id, kind, x0, y0, x1, y1, z0, z1, [], material, openings);

    private static ComponentGeometry BoxWithOpening(string id, ComponentKind kind, double x0, double y0, double x1, double y1, double z0, double z1, Profile2Mm profile) =>
        kind == ComponentKind.Roof && profile is RectangleProfile rectangle
            ? RingWithOpening(id, x0, y0, x1, y1, rectangle.Rectangle, z0, z1)
            : BoxWithFaces(id, kind, x0, y0, x1, y1, z0, z1, [], "Concrete",
                new OpeningProfile($"{id}/Opening", id, OpeningKind.Box, profile, null, profile, null, Frame(),
                    new Interval1D(z0, z1), new Interval1D(0, z1 - z0), []));

    private static ComponentGeometry RingWithOpening(string id, double x0, double y0, double x1, double y1,
        Rectangle2Mm hole, double z0, double z1)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]),
            [new Polygon2Mm([new(hole.MinX, hole.MaxY), new(hole.MaxX, hole.MaxY), new(hole.MaxX, hole.MinY), new(hole.MinX, hole.MinY)])]);
        var range = new Interval1D(z0, z1);
        var opening = new OpeningProfile($"{id}/Opening", id, OpeningKind.Box, new RectangleProfile(hole), null,
            new RectangleProfile(hole), null, Frame(), range, new Interval1D(0, z1 - z0), []);
        return new ComponentGeometry(id, ComponentKind.Roof, "Concrete", footprint, range,
            new ExtrusionSolid(footprint, Frame(), range), [opening], [], []);
    }

    private static ComponentGeometry WallWithOpening(string id, OpeningKind kind, Profile2Mm profile, double thickness)
    {
        var opening = new OpeningProfile($"{id}/Opening", id, kind, profile, null, profile, null, Frame(),
            new Interval1D(0, 1400), new Interval1D(0, thickness), []);
        var inner = new FacePatch($"{id}/Inner", id, FaceRole.Inner, RectProfile(1300, 1400), Frame(), new AreaMm2(6_300_000), new(0, -1, 0), null, []);
        var outer = new FacePatch($"{id}/Outer", id, FaceRole.Outer, RectProfile(1300, 1400), Frame(), new AreaMm2(7_980_000), new(0, 1, 0), null, []);
        return BoxWithFaces(id, ComponentKind.Wall, -650, 625, 650, 775, 0, 1400, [inner, outer], "Concrete", opening);
    }

    private static ComponentGeometry BoxWithFaces(string id, ComponentKind kind, double x0, double y0, double x1, double y1, double z0, double z1,
        IEnumerable<FacePatch> faces, string material = "Concrete", params OpeningProfile[] openings)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]), []);
        var range = new Interval1D(z0, z1);
        return new ComponentGeometry(id, kind, material, footprint, range,
            new ExtrusionSolid(footprint, Frame(), range), openings, faces, []);
    }

    private static RectangleProfile RectProfile(double width, double height) => new(new Rectangle2Mm(0, 0, width, height));
    private static Frame3Mm Frame() => new(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
}
