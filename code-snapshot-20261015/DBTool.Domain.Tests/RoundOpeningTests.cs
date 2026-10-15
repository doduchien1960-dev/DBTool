using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class RoundOpeningTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;

    [Fact]
    public void S1HasThreeConcentricBoundariesAndInternalInvertDatum()
    {
        var spec = SyntheticTestFactory.Create();
        var result = PipeOpeningBuilder.BuildRound(spec, spec.Input.PipeConnections[0], spec.Catalogs, Policy);
        Assert.True(result.Success);
        var hole = Assert.IsType<OpeningProfile>(result.Value);
        Circle(hole.Inner, 200, 300, 700);
        Circle(hole.PipeOuter!, 255, 245, 755);
        Circle(hole.ConcreteCutout, 305, 195, 805);
        Assert.Equal(610, 2 * Assert.IsType<CircleProfile>(hole.ConcreteCutout).RadiusMm);
        Assert.Equal("Wall/T1", hole.OwnerId);
        Assert.Equal(new Interval1D(195, 805), hole.ZRange);
        Assert.Equal(new Interval1D(0, 150), hole.ExtrusionRange);
        Assert.Equal(new Point3Mm(0, 625, 500), hole.WallFrame!.ToGlobal(0, 0, 500));
        Assert.Equal(new Point3Mm(0, 775, 500), hole.WallFrame.ToGlobal(0, 150, 500));
        Assert.Equal(hole.WallFrame.Left, hole.Frame.U);
        Assert.Equal(hole.WallFrame.Up, hole.Frame.V);
        Assert.Equal(hole.WallFrame.Outward, hole.Frame.N);
        Assert.Equal(hole.WallFrame.Origin, hole.Frame.Origin);
        Assert.Contains(spec.Sources[0], hole.Sources);
    }

    [Fact]
    public void ZeroMortarHasCoincidentOuterAndCutoutWithoutAnAnnulus()
    {
        var section = PipeCrossSectionBuilder.BuildRound(400, 55, 0, 300, 0).Value!;
        Assert.True(section.MortarEmpty);
        Assert.Equal(section.PipeOuterCircle, section.ConcreteCutoutCircle);
        Assert.Equal(255, section.ConcreteCutoutCircle.RadiusMm);
        var hole = Build(mortar: 0).Value!;
        Assert.Equal(hole.PipeOuter, hole.ConcreteCutout);
        Assert.Equal(new Interval1D(245, 755), hole.ZRange);
        Assert.False(PipeCrossSectionBuilder.BuildRound(400, 55, 50, 300, 0).Value!.MortarEmpty);
    }

    [Theory]
    [InlineData(WallId.T1, -100, 625)]
    [InlineData(WallId.T2, 500, 100)]
    [InlineData(WallId.T3, 100, -625)]
    [InlineData(WallId.T4, -500, -100)]
    public void OffsetGeometryUsesWallFrameWithoutExpandingPipelineCapabilities(WallId wall, double x, double y)
    {
        var result = Build(offset: 100, wall: wall);
        Assert.True(result.Success);
        var hole = result.Value!;
        var circle = Assert.IsType<CircleProfile>(hole.Inner);
        Assert.Equal(new Point2Mm(100, 500), circle.Center);
        Assert.Equal(new Point3Mm(x, y, 500), hole.WallFrame!.ToGlobal(circle.Center.X, 0, circle.Center.Y));
        Assert.Equal(new Point2Mm(-100, 500), hole.WallFrame.ToFaceScreen(new LocalWallPointMm(100, 0, 500)));
        Assert.Equal($"Wall/{wall}", hole.OwnerId);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(300, 300)]
    [InlineData(300, -300)]
    [InlineData(1000, 0)]
    public void CutoutBeyondWallIsBlockedWithoutClamping(double invert, double offset)
    {
        var result = Build(invert: invert, offset: offset);
        Block(result, "OPENING_CROSS_COMPONENT_UNSUPPORTED");
        var d = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticScope.Component, d.Scope);
        Assert.Equal("Wall/T1", d.ComponentId);
        Assert.NotNull(d.Source);
    }

    [Theory]
    [InlineData(105, 195)]
    [InlineData(895, -195)]
    public void CutoutExactlyTangentToFaceBoundaryIsAllowed(double invert, double offset) =>
        Assert.True(Build(invert: invert, offset: offset).Success);

    [Theory]
    [InlineData(PipeKind.Box)]
    [InlineData(PipeKind.DoubleBox)]
    public void NonRoundCatalogHasExplicitUnsupportedResult(PipeKind kind)
    {
        var spec = SyntheticTestFactory.Create();
        var entry = new PipeCatalogEntry("D400", kind, null, 400, 400, 55,
            kind == PipeKind.DoubleBox ? 50 : null, 0);
        var catalogs = new CatalogSet(new Dictionary<string, PipeCatalogEntry> { [entry.Code] = entry }, [], [], []);
        Block(PipeOpeningBuilder.BuildRound(spec, spec.Input.PipeConnections[0], catalogs, Policy), "PIPE_SHAPE_UNSUPPORTED");
    }

    [Fact]
    public void MissingCatalogIsBlockInsteadOfFallbackToSpecCatalog()
    {
        var spec = SyntheticTestFactory.Create();
        Block(PipeOpeningBuilder.BuildRound(spec, spec.Input.PipeConnections[0], new CatalogSet([], [], [], []), Policy), "PIPE_CATALOG_MISSING");
    }

    [Theory]
    [InlineData(0, 55, 50, 300, 0)]
    [InlineData(-1, 55, 50, 300, 0)]
    [InlineData(400, -1, 50, 300, 0)]
    [InlineData(400, 55, -1, 300, 0)]
    [InlineData(400, 55, 50, -1, 0)]
    [InlineData(double.NaN, 55, 50, 300, 0)]
    [InlineData(400, double.PositiveInfinity, 50, 300, 0)]
    [InlineData(400, 55, double.NaN, 300, 0)]
    [InlineData(400, 55, 50, double.PositiveInfinity, 0)]
    [InlineData(400, 55, 50, 300, double.NegativeInfinity)]
    [InlineData(double.MaxValue, double.MaxValue, 50, 300, 0)]
    public void InvalidScalarOrOverflowReturnsBlock(double d, double t, double m, double h, double e) =>
        Block(PipeCrossSectionBuilder.BuildRound(d, t, m, h, e), "PIPE_DIMENSION_INVALID");

    [Fact]
    public void AttachCreatesSnapshotAndRegistersCutOnceInOwnerAndRegistry()
    {
        var spec = SyntheticTestFactory.Create();
        var original = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var hole = Build().Value!;
        var result = PipeOpeningBuilder.AttachOpening(original, hole);
        Assert.True(result.Success);
        var attached = result.Value!;
        Assert.NotSame(original, attached);
        Assert.Single(original.Openings);
        Assert.Empty(original.Components.Single(c => c.Id == "Wall/T1").Voids);
        Assert.Equal(2, attached.Openings.Length);
        var owner = attached.Components.Single(c => c.Id == "Wall/T1");
        Assert.Same(hole, Assert.Single(owner.Voids));
        Assert.Same(hole, attached.Openings.Single(o => o.Id == hole.Id));
        Assert.Same(original.Components.Single(c => c.Id == owner.Id).Occupied, owner.Occupied);
        Assert.Same(original.Components.Single(c => c.Id == "Roof"), attached.Components.Single(c => c.Id == "Roof"));
        Assert.Contains(result.Diagnostics, d => d.Code == "OPENING_FACE_PATCHES_DEFERRED" && d.Severity == DiagnosticSeverity.Review);
        Block(PipeOpeningBuilder.AttachOpening(attached, hole), "OPENING_DUPLICATE");
        Assert.Equal(2, attached.Openings.Length);
    }

    [Fact]
    public void AttachMissingOwnerReturnsBlockAndNoPayload() =>
        Block(PipeOpeningBuilder.AttachOpening(new GeometryModel([], []), Build().Value!), "OPENING_OWNER_MISSING");

    [Theory]
    [InlineData(600, false)]
    [InlineData(1400, true)]
    public void AttachRejectsAnotherWallSizeInsteadOfRegisteringAnOutsideCut(double wallHeight, bool shiftWall)
    {
        var model = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(), Policy).Value!;
        var wall = model.Components.Single(c => c.Id == "Wall/T1");
        var footprint = shiftWall ? new Footprint(new Polygon2Mm([
            new(1000, 625), new(2300, 625), new(2300, 775), new(1000, 775)]), []) : wall.Footprint;
        var range = new Interval1D(0, wallHeight);
        var solid = new ExtrusionSolid(footprint, Assert.IsType<ExtrusionSolid>(wall.Occupied).Frame, range);
        var otherWall = new ComponentGeometry(wall.Id, wall.Kind, wall.MaterialId, footprint, range, solid, [], [], wall.Sources);
        var other = new GeometryModel([otherWall], []);
        Block(PipeOpeningBuilder.AttachOpening(other, Build().Value!), "OPENING_CROSS_COMPONENT_UNSUPPORTED");
        Assert.Empty(other.Openings);
    }

    [Fact]
    public void DifferentRuleVersionDoesNotSilentlyUseCurrentInvertRule()
    {
        var seed = SyntheticTestFactory.Create();
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, "future/v2", seed.Sources, []);
        Block(PipeOpeningBuilder.BuildRound(spec, spec.Input.PipeConnections[0], spec.Catalogs, Policy), "PIPE_POLICY_UNSUPPORTED");
    }

    [Theory]
    [InlineData(WallId.T1, 250)]
    [InlineData(WallId.T1, -250)]
    [InlineData(WallId.T3, 250)]
    [InlineData(WallId.T3, -250)]
    public void AttachUsesClearFaceInsteadOfCornerOwnedGrossStrip(WallId wall, double offset)
    {
        var model = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(), Policy).Value!;
        // Independent descriptor valid for a wider L1200 chamber: u reaches 555 < 600.
        // In target L1000 the clear face ends at 500, despite the gross strip ending at 650.
        var frame = WallFrame.Create(wall, 1200, 1250);
        var center = new Point2Mm(offset, 500);
        var hole = new OpeningProfile($"Wall/{wall}/Pipe", $"Wall/{wall}", OpeningKind.Round,
            new CircleProfile(center, 200), new CircleProfile(center, 255), new CircleProfile(center, 305),
            frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(195, 805), new Interval1D(0, 150), SyntheticTestFactory.Create().Sources);
        Block(PipeOpeningBuilder.AttachOpening(model, hole), "OPENING_CROSS_COMPONENT_UNSUPPORTED");
        Assert.Single(model.Openings);
    }

    private static OperationResult<OpeningProfile> Build(double invert = 300, double offset = 0, double mortar = 50, WallId wall = WallId.T1)
    {
        var seed = SyntheticTestFactory.Create();
        var pipe = new PipeConnectionSpec(wall, "D400", invert, offset, mortar, seed.Input.PipeConnections[0].SourceRefs);
        var spec = SyntheticTestFactory.Create([pipe]);
        return PipeOpeningBuilder.BuildRound(spec, pipe, spec.Catalogs, Policy);
    }

    private static void Circle(Profile2Mm profile, double radius, double lowerZ, double upperZ)
    {
        var circle = Assert.IsType<CircleProfile>(profile);
        Assert.Equal(new Point2Mm(0, 500), circle.Center);
        Assert.Equal(radius, circle.RadiusMm, 6);
        Assert.Equal(lowerZ, circle.Center.Y - circle.RadiusMm, 6);
        Assert.Equal(upperZ, circle.Center.Y + circle.RadiusMm, 6);
    }

    private static void Block<T>(OperationResult<T> result, string code) where T : class
    {
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Block);
    }
}
