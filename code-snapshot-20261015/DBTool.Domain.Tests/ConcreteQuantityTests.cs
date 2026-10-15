using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class ConcreteQuantityTests
{
    private static readonly TolerancePolicy Tolerance = new(1e-6, 1e-6, 1e-9, 1e-6, 1);

    [Fact]
    public void BasicChamberComponentsProduceIndependentConcreteLines()
    {
        var model = Model(
            Box("Pad", ComponentKind.Pad, "Bedding", -850, -975, 850, 975, -250, -150),
            Box("Base", ComponentKind.Base, "Concrete", -750, -875, 750, 875, -150, 0),
            Ring("Wall", ComponentKind.Wall, "Concrete", -650, -775, 650, 775, -500, -625, 500, 625, 0, 1400),
            Ring("Roof", ComponentKind.Roof, "Concrete", -650, -775, 650, 775, -350, -350, 350, 350, 1400, 1550));

        var result = new ConcreteQuantityCalculator().Calculate(model, new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        AssertClose(0.3315, Line(result.Value!, "Pad").Net);
        AssertClose(0.39375, Line(result.Value!, "Base").Net);
        AssertClose(1.071, Line(result.Value!, "Wall").Net);
        AssertClose(0.22875, Line(result.Value!, "Roof").Net);
    }

    [Fact]
    public void OverlappingAndDuplicateBoxCutsDeductOnce()
    {
        var first = BoxOpening("Cut-A", 0, 0, 100, 100, 0, 100);
        var second = BoxOpening("Cut-B", 50, 0, 150, 100, 0, 100);
        var duplicate = BoxOpening("Cut-Duplicate", 0, 0, 100, 100, 0, 100);
        var component = Box("Wall/T1", ComponentKind.Wall, "Concrete", 0, 0, 200, 100, 0, 100, first, second, duplicate);

        var result = new ConcreteQuantityCalculator().Calculate(Model(component), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        var line = Line(result.Value!, "Wall/T1");
        AssertClose(0.002, line.Gross);
        AssertClose(0.0015, line.Deductions);
        AssertClose(0.0005, line.Net);
        Assert.Single(result.Value!.Trace.Where(t => t.Operation == "Deduction" && t.ComponentId == "Wall/T1"));
    }

    [Fact]
    public void CircleSplitBetweenOwnersUsesExactHalfAreaAndTrace()
    {
        var frame = WallFrame.Create(WallId.T1, 200, 200);
        var left = Box("Wall/T1", ComponentKind.Wall, "Concrete", -100, 100, 0, 200, 0, 200,
            RoundOpening("Pipe/T1", "Wall/T1", frame, new(0, 100), 100, 0, 100));
        var right = Box("Wall/T2", ComponentKind.Wall, "Concrete", 0, 100, 100, 200, 0, 200,
            RoundOpening("Pipe/T2", "Wall/T2", frame, new(0, 100), 100, 0, 100));

        var result = new ConcreteQuantityCalculator().Calculate(Model(left, right), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        const double half = 0.0015707963267948967;
        AssertClose(half, Line(result.Value!, "Wall/T1").Deductions);
        AssertClose(half, Line(result.Value!, "Wall/T2").Deductions);
        Assert.Equal(2, result.Value!.Trace.Count(t => t.Operation == "Deduction"));
    }

    [Fact]
    public void TwoIntersectingCirclesUseIndependentLensAreaWithoutDoubleCount()
    {
        var frame = WallFrame.Create(WallId.T1, 400, 200);
        var one = RoundOpening("Round-A", "Wall/T1", frame, new(0, 100), 100, 0, 100);
        var two = RoundOpening("Round-B", "Wall/T1", frame, new(-100, 100), 100, 0, 100);
        var component = Box("Wall/T1", ComponentKind.Wall, "Concrete", -200, 100, 200, 200, 0, 200, one, two);

        var result = new ConcreteQuantityCalculator().Calculate(Model(component), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        const double expectedMm3 = 50548.156085708295 * 100;
        AssertClose(expectedMm3 / 1e9, Line(result.Value!, "Wall/T1").Deductions);
        Assert.Contains(result.Value!.Trace, t => t.Algorithm.Contains("circle-union", StringComparison.Ordinal));
    }

    [Fact]
    public void HaunchIsAssignedToOwnerExactlyOnce()
    {
        var baseComponent = Box("Base", ComponentKind.Base, "Concrete", -500, -625, 500, 625, 0, 150,
            solid: new HaunchSolid(1000, 1250, 100, 100, HaunchOwner.Base, new Interval1D(0, 100)));

        var result = new ConcreteQuantityCalculator().Calculate(Model(baseComponent), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        AssertClose(0.021166666666666668, Line(result.Value!, "Base").Additions);
        AssertClose(Line(result.Value!, "Base").Gross + Line(result.Value!, "Base").Additions, result.Value!.Lines.Sum(x => x.Net));
    }

    [Fact]
    public void MortarUsesCutMinusPipeOuterAndKeepsMaterialSeparate()
    {
        var frame = WallFrame.Create(WallId.T1, 800, 600);
        var opening = RoundOpening("Pipe", "Wall/Mortar", frame, new(0, 305), 305, 0, 100, pipeRadius: 255);
        var component = Box("Wall/Mortar", ComponentKind.Wall, "BeddingMortar", -400, 300, 400, 400, 0, 610, opening);

        var result = new ConcreteQuantityCalculator().Calculate(Model(component), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        var expected = Math.PI * (305 * 305 - 255 * 255) * 100 / 1e9;
        AssertClose(expected, Line(result.Value!, "Wall/Mortar").Net);
        Assert.Equal("BeddingMortar", Line(result.Value!, "Wall/Mortar").MaterialId);
    }

    [Fact]
    public void NeckCollarAndPanelsRemainSeparateTypedConcreteComponents()
    {
        var neck = Ring("Neck", ComponentKind.Neck, "Concrete", -450, -450, 450, 450, -350, -350, 350, 350, 1550, 2150);
        var collar = Ring("Collar", ComponentKind.Collar, "Concrete", -500, -500, 500, 500, -400, -400, 400, 400, 2150, 2250);
        var panel1 = Box("Panel/1", ComponentKind.Panel, "Concrete", -422.5, -420, -5, 420, 2250, 2400);
        var panel2 = Box("Panel/2", ComponentKind.Panel, "Concrete", 5, -420, 422.5, 420, 2250, 2400);

        var result = new ConcreteQuantityCalculator().Calculate(Model(neck, collar, panel1, panel2), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        AssertClose(0.192, Line(result.Value!, "Neck").Net);
        AssertClose(0.036, Line(result.Value!, "Collar").Net);
        AssertClose(0.052605, Line(result.Value!, "Panel/1").Net);
        AssertClose(0.052605, Line(result.Value!, "Panel/2").Net);
    }

    [Fact]
    public void CircleUnionHandlesDisjointTangentAndCoincidentCases()
    {
        var disjoint = Box("Disjoint", ComponentKind.Wall, "Concrete", -300, -100, 300, 100, 0, 100,
            PlanCircleOpening("D1", "Disjoint", -100, 0, 50, 0, 100),
            PlanCircleOpening("D2", "Disjoint", 100, 0, 50, 0, 100));
        var tangent = Box("Tangent", ComponentKind.Wall, "Concrete", -100, -50, 200, 50, 0, 100,
            PlanCircleOpening("T1", "Tangent", 0, 0, 50, 0, 100),
            PlanCircleOpening("T2", "Tangent", 100, 0, 50, 0, 100));
        var coincident = Box("Coincident", ComponentKind.Wall, "Concrete", -100, -100, 100, 100, 0, 100,
            PlanCircleOpening("C1", "Coincident", 0, 0, 50, 0, 100),
            PlanCircleOpening("C2", "Coincident", 0, 0, 50, 0, 100));

        var result = new ConcreteQuantityCalculator().Calculate(Model(disjoint, tangent, coincident), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        AssertClose(2 * Math.PI * 50 * 50 * 100 / 1e9, Line(result.Value!, "Disjoint").Deductions);
        AssertClose(2 * Math.PI * 50 * 50 * 100 / 1e9, Line(result.Value!, "Tangent").Deductions);
        AssertClose(Math.PI * 50 * 50 * 100 / 1e9, Line(result.Value!, "Coincident").Deductions);
    }

    [Fact]
    public void CircleBoundaryClipsToOwnerAndMixedCircleBoxUnionIsExactWithinAreaTolerance()
    {
        var half = Box("Half", ComponentKind.Wall, "Concrete", 0, -100, 100, 100, 0, 100,
            PlanCircleOpening("HalfCircle", "Half", 0, 0, 100, 0, 100));
        var mixed = Box("Mixed", ComponentKind.Wall, "Concrete", 0, 0, 300, 200, 0, 100,
            PlanCircleOpening("MixedCircle", "Mixed", 100, 100, 100, 0, 100),
            BoxOpeningForOwner("MixedBox", "Mixed", 100, 0, 200, 100, 0, 100));
        var outside = Box("Outside", ComponentKind.Wall, "Concrete", 0, 0, 100, 100, 0, 100,
            PlanCircleOpening("OutsideCircle", "Outside", 300, 300, 10, 0, 100));

        var result = new ConcreteQuantityCalculator().Calculate(Model(half, mixed, outside), new QuantityPolicy("S2-Quantity-1"), Tolerance);

        Assert.True(result.Success);
        AssertClose(Math.PI * 100 * 100 / 2 * 100 / 1e9, Line(result.Value!, "Half").Deductions);
        var mixedArea = Math.PI * 100 * 100 + 100 * 100 - Math.PI * 100 * 100 / 4;
        AssertClose(mixedArea * 100 / 1e9, Line(result.Value!, "Mixed").Deductions);
        AssertClose(0, Line(result.Value!, "Outside").Deductions);
    }

    private static QuantityLine Line(ConcreteQuantityResult result, string componentId) =>
        Assert.Single(result.Lines.Where(x => x.ComponentId == componentId));

    private static GeometryModel Model(params ComponentGeometry[] components) =>
        new(components, components.SelectMany(c => c.Voids).DistinctBy(x => x.Id));

    private static ComponentGeometry Box(string id, ComponentKind kind, string material, double x0, double y0, double x1, double y1, double z0, double z1,
        params OpeningProfile[] openings) => Box(id, kind, material, x0, y0, x1, y1, z0, z1, null, openings);

    private static ComponentGeometry Box(string id, ComponentKind kind, string material, double x0, double y0, double x1, double y1, double z0, double z1,
        SolidDescriptor? solid, params OpeningProfile[] openings)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]), []);
        var range = new Interval1D(z0, z1);
        return new ComponentGeometry(id, kind, material, footprint, range,
            solid ?? new ExtrusionSolid(footprint, IdentityFrame(), range), openings, [], []);
    }

    private static ComponentGeometry Ring(string id, ComponentKind kind, string material, double x0, double y0, double x1, double y1,
        double hx0, double hy0, double hx1, double hy1, double z0, double z1)
    {
        var outer = new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]);
        var hole = new Polygon2Mm([new(hx0, hy1), new(hx1, hy1), new(hx1, hy0), new(hx0, hy0)]);
        var footprint = new Footprint(outer, [hole]);
        var range = new Interval1D(z0, z1);
        return new ComponentGeometry(id, kind, material, footprint, range,
            new ExtrusionSolid(footprint, IdentityFrame(), range), [], [], []);
    }

    private static OpeningProfile BoxOpening(string id, double x0, double y0, double x1, double y1, double z0, double z1)
        => BoxOpeningForOwner(id, "Wall/T1", x0, y0, x1, y1, z0, z1);

    private static OpeningProfile BoxOpeningForOwner(string id, string ownerId, double x0, double y0, double x1, double y1, double z0, double z1)
    {
        var profile = new RectangleProfile(new Rectangle2Mm(x0, y0, x1, y1));
        return new OpeningProfile(id, ownerId, OpeningKind.Box, profile, null, profile, null,
            IdentityFrame(), new Interval1D(z0, z1), new Interval1D(0, 1), []);
    }

    private static OpeningProfile PlanCircleOpening(string id, string ownerId, double x, double y, double radius, double z0, double z1)
    {
        var profile = new CircleProfile(new Point2Mm(x, y), radius);
        return new OpeningProfile(id, ownerId, OpeningKind.Round, profile, null, profile, null,
            IdentityFrame(), new Interval1D(z0, z1), new Interval1D(0, 1), []);
    }

    private static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-9);

    private static OpeningProfile RoundOpening(string id, string ownerId, WallFrame frame, Point2Mm center, double cutRadius, double v0, double v1, double? pipeRadius = null)
    {
        var cut = new CircleProfile(center, cutRadius);
        var pipe = new CircleProfile(center, pipeRadius ?? cutRadius);
        return new OpeningProfile(id, ownerId, OpeningKind.Round, pipe, pipe, cut, frame,
            IdentityFrame(), new Interval1D(center.Y - cutRadius, center.Y + cutRadius), new Interval1D(v0, v1), []);
    }

    private static Frame3Mm IdentityFrame() => new(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
}
