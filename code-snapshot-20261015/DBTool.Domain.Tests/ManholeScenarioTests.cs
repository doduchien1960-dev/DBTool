using System.Text.Json;
using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class ManholeScenarioTests
{
    public static IEnumerable<object[]> ProfilePositions() =>
        from profile in Enum.GetValues<Profile>()
        from position in Enum.GetValues<NeckPosition>()
        select new object[] { profile, position };

    [Theory]
    [MemberData(nameof(ProfilePositions))]
    public void ThreeProfilesByNinePositionsKeepIndependentNeckOracle(Profile profile, NeckPosition position)
    {
        var seed = SyntheticTestFactory.Create();
        var i = seed.Input;
        var input = new ManholeInput(i.Id, i.Name, profile, i.L, i.B, i.H, i.BaseThicknessMm, i.WallThicknessMm, i.RoofThicknessMm,
            i.PadThicknessMm, i.BaseExpansionMm, i.PadExpansionMm, i.BeddingMortarMm, i.BaseHaunch, i.TopHaunch, i.NeckHeight,
            i.RoofOpening, i.PipeConnections, i.BaseBottomMesh, i.BaseTopMesh, i.CoverMm, i.ElevationMode, i.BaseElevationM,
            i.PipeInvertReference, i.HookBaseBottom, i.HookBaseTop, i.DistributionMode, i.MeshJunctionPolicy, i.LayerStackPolicy,
            i.FeatureRequests, i.SourceRefs, i.UnsupportedRawFields);
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var spec = new ValidatedManholeSpec(input, seed.Catalogs, policy.Version, seed.Sources, []);
        var result = NeckGeometryBuilder.Build(spec, new(700, 600, 100, 600, position, seed.Sources),
            new(NeckSeatKind.None, 0, 0, 1550, seed.Sources), policy);
        var centers = new Dictionary<NeckPosition, Point2Mm>
        {
            [NeckPosition.Center] = new(0,0), [NeckPosition.North] = new(0,325), [NeckPosition.East] = new(150,0),
            [NeckPosition.South] = new(0,-325), [NeckPosition.West] = new(-150,0), [NeckPosition.NorthEast] = new(150,325),
            [NeckPosition.SouthEast] = new(150,-325), [NeckPosition.SouthWest] = new(-150,-325), [NeckPosition.NorthWest] = new(-150,325)
        };
        Assert.True(result.Success);
        Assert.Equal(centers[position], result.Value!.Center);
        Assert.Equal(420000, result.Value.HoleAreaMm2);
    }

    [Fact]
    public void RequiredScenarioMatrixExistsAndRetainsInvalidNeckCatchCase()
    {
        var path = FixtureManifest.ResolveWorkspacePath("DBTool/Tests/Fixtures/s2-s5/scenario-manifest.json");
        Assert.True(File.Exists(path), "T42 scenario matrix has not been materialized.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Contains(manifest.RootElement.GetProperty("Records").EnumerateArray(), r =>
            r.GetProperty("CaseId").GetString() == "mandatory-neck0-catchNeck" && !r.GetProperty("ExpectedValid").GetBoolean());
    }

    [Theory]
    [InlineData(WallId.T1,-100,100,625)]
    [InlineData(WallId.T1,100,-100,625)]
    [InlineData(WallId.T2,-100,500,-100)]
    [InlineData(WallId.T2,100,500,100)]
    [InlineData(WallId.T3,-100,-100,-625)]
    [InlineData(WallId.T3,100,100,-625)]
    [InlineData(WallId.T4,-100,-500,100)]
    [InlineData(WallId.T4,100,-500,-100)]
    public void FourWallsBySignedOffsetMatchLiteralOracle(WallId wall,double offset,double x,double y)
    {
        Assert.Equal(new Point3Mm(x,y,500),WallFrame.Create(wall,1000,1250).ToGlobal(offset,0,500));
    }

    [Fact]
    public void NeckZeroWithCatchOwnerNeckIsRequiredBlockWithoutPartialPayload()
    {
        var seed=SyntheticTestFactory.Create();
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var spec=new ValidatedManholeSpec(seed.Input,seed.Catalogs,policy.Version,seed.Sources,[]);
        var geometry=ChamberGeometryBuilder.Build(spec,policy).Value!;
        var result=CatchBasinBuilder.Build(spec,geometry,new(CatchBasinOwner.Neck,WallId.T1,HorizontalPosition.Center,400,300,600,500,600,100,100,0,[]),policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Equal("CATCH_HOST_MISSING",Assert.Single(result.Diagnostics).Code);
        Assert.Equal(DiagnosticSeverity.Block,result.Diagnostics[0].Severity);
    }
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;

    [Fact]
    public void FrozenS1ScenarioMatchesPureGeometryLayoutAndSchedule()
    {
        using var expected = LoadExpected();
        using var input = LoadInput();
        var expectedRoot = expected.RootElement;
        var inputRoot = input.RootElement;
        var spec = SyntheticTestFactory.Create();

        var dimensions = inputRoot.GetProperty("input").GetProperty("dimensionsMm");
        Assert.Equal(dimensions.GetProperty("length").GetDouble(), spec.Input.L, 6);
        Assert.Equal(dimensions.GetProperty("width").GetDouble(), spec.Input.B, 6);
        Assert.Equal(dimensions.GetProperty("height").GetDouble(), spec.Input.H, 6);

        var geometry = Succeed(ChamberGeometryBuilder.Build(spec, Policy));
        AssertBounds(geometry.Components.Where(c => c.Kind == ComponentKind.Wall && c.Id.StartsWith("Wall/", StringComparison.Ordinal)),
            expectedRoot.GetProperty("geometry").GetProperty("outerWallMm"));
        AssertBounds([geometry.Components.Single(c => c.Id == "Base")], expectedRoot.GetProperty("geometry").GetProperty("baseMm"));
        AssertBounds([geometry.Components.Single(c => c.Id == "Pad")], expectedRoot.GetProperty("geometry").GetProperty("padMm"));

        var pipe = spec.Input.PipeConnections.Single();
        var opening = Succeed(PipeOpeningBuilder.BuildRound(spec, pipe, spec.Catalogs, Policy));
        var expectedOpening = expectedRoot.GetProperty("geometry").GetProperty("pipeOpening");
        var cutout = Assert.IsType<CircleProfile>(opening.ConcreteCutout);
        Assert.Equal(expectedOpening.GetProperty("cutDiameterMm").GetDouble() / 2, cutout.RadiusMm, 6);
        Assert.Equal(expectedOpening.GetProperty("centerZMm").GetDouble(), cutout.Center.Y, 6);
        Assert.Equal(expectedOpening.GetProperty("wallId").GetString(), opening.WallFrame!.WallId.ToString());
        Assert.Equal(new Point3Mm(0, 625, 500), opening.WallFrame.ToGlobal(cutout.Center.X, 0, cutout.Center.Y));
        Assert.Equal(expectedOpening.GetProperty("zBoundsMm").GetProperty("min").GetDouble(),
            cutout.Center.Y - cutout.RadiusMm, 6);
        Assert.Equal(expectedOpening.GetProperty("zBoundsMm").GetProperty("max").GetDouble(),
            cutout.Center.Y + cutout.RadiusMm, 6);

        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = Succeed(MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy));
        var layout = Succeed(SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy));
        var expectedBars = expectedRoot.GetProperty("bars").EnumerateArray().ToArray();
        Assert.Equal(expectedBars.Length, layout.Occurrences.Length);
        Assert.Equal(layout.Occurrences.Length, layout.Occurrences.Select(bar => bar.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var group in expectedBars.GroupBy(bar =>
                     (Layer: bar.GetProperty("layer").GetString()!, Axis: bar.GetProperty("axis").GetString()!)))
        {
            var actual = layout.Occurrences
                .Where(bar => bar.Layer.ToString() == group.Key.Layer && bar.Axis.ToString() == group.Key.Axis)
                .OrderBy(bar => StartStation(bar, group.Key.Axis))
                .ToArray();
            var frozen = group.OrderBy(bar => StartStation(bar, group.Key.Axis)).ToArray();
            Assert.Equal(frozen.Length, actual.Length);
            for (var index = 0; index < frozen.Length; index++)
            {
                var expectedBar = frozen[index];
                var actualLine = Assert.IsType<LineSegment3Mm>(actual[index].Path.Segments.Single());
                Assert.Equal(expectedBar.GetProperty("scheduleLengthMm").GetDouble(), actual[index].ScheduleLengthMm, 6);
                AssertPoint(expectedBar.GetProperty("start"), actualLine.Start);
                AssertPoint(expectedBar.GetProperty("end"), actualLine.End);
            }
        }

        var schedule = Succeed(SteelScheduleCalculator.Calculate(layout.Occurrences, Policy.MassPolicy,
            new MarkPolicy(CalculationScope.BaseRebarOnly)));
        var expectedSchedule = expectedRoot.GetProperty("steelSchedule");
        Assert.Equal(expectedSchedule.GetProperty("rows").GetArrayLength(), schedule.Rows.Length);
        Assert.Equal(expectedSchedule.GetProperty("totalLengthMm").GetDouble(),
            schedule.Rows.Sum(row => row.TotalLengthMm), 6);
        Assert.Equal(expectedSchedule.GetProperty("totalMassKg").GetDouble(), schedule.TotalMassKg.Value, 12);
        Assert.Equal(expectedSchedule.GetProperty("displayMassKg").GetDouble(),
            Math.Round(schedule.TotalMassKg.Value, 3, MidpointRounding.AwayFromZero), 3);
        Assert.Equal(new[] { 13, 11, 13, 11 }, schedule.Rows.Select(row => row.Quantity));
        Assert.Equal(new[] { 1440d, 1690d, 1440d, 1690d }, schedule.Rows.Select(row => row.SingleLengthMm));
    }

    [Fact]
    public void RebarIdsAndPathsRemainStableWhenGeometryComponentOrderChanges()
    {
        var spec = SyntheticTestFactory.Create();
        var geometry = Succeed(ChamberGeometryBuilder.Build(spec, Policy));
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = Succeed(MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy));
        var first = Succeed(SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy));
        var reorderedGeometry = new GeometryModel(geometry.Components.Reverse(), geometry.Openings);
        var second = Succeed(SlabRebarPlanner.PlanBase(spec, reorderedGeometry, zones, Policy));

        Assert.Equal(first.Occurrences.Select(bar => bar.Id), second.Occurrences.Select(bar => bar.Id));
        Assert.Equal(first.Occurrences.Select(bar => bar.Path.Segments.Single()),
            second.Occurrences.Select(bar => bar.Path.Segments.Single()));
    }

    [Fact]
    public void InvalidP10A150IsBlockedAsGrammarErrorWithNullValue()
    {
        var source = new SourceRef(SourceKind.SyntheticFixture, "T42a/P10A150", new string('a', 64),
            null, null, null, "P10A150");
        var result = RebarSpecParser.Parse("P10A150", ParseContext.MeshRequired, source);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "REBAR_FORMAT" &&
            diagnostic.Severity == DiagnosticSeverity.Block && diagnostic.Scope == DiagnosticScope.Global);
    }

    private static JsonDocument LoadExpected() =>
        JsonDocument.Parse(File.ReadAllText(FixtureManifest.ResolveWorkspacePath(
            "DBTool/Tests/Fixtures/s1-mono-001.expected.json")));

    private static JsonDocument LoadInput() =>
        JsonDocument.Parse(File.ReadAllText(FixtureManifest.ResolveWorkspacePath(
            "DBTool/Tests/Fixtures/s1-mono-001.json")));

    private static double StartStation(BarOccurrence bar, string axis) =>
        axis == "X" ? ((LineSegment3Mm)bar.Path.Segments.Single()).Start.Y
        : ((LineSegment3Mm)bar.Path.Segments.Single()).Start.X;

    private static double StartStation(JsonElement bar, string axis) =>
        axis == "X" ? bar.GetProperty("start").GetProperty("y").GetDouble()
        : bar.GetProperty("start").GetProperty("x").GetDouble();

    private static void AssertBounds(IEnumerable<ComponentGeometry> components, JsonElement expected)
    {
        var componentArray = components.ToArray();
        var vertices = componentArray.SelectMany(component => component.Footprint.Outer.Vertices).ToArray();
        Assert.Equal(expected.GetProperty("width").GetDouble(), vertices.Max(point => point.X) - vertices.Min(point => point.X), 6);
        Assert.Equal(expected.GetProperty("height").GetDouble(), vertices.Max(point => point.Y) - vertices.Min(point => point.Y), 6);
        if (expected.TryGetProperty("zRangeMm", out var zRange))
        {
            Assert.Equal(zRange.GetProperty("min").GetDouble(), componentArray.Min(component => component.ZRange.Start), 6);
            Assert.Equal(zRange.GetProperty("max").GetDouble(), componentArray.Max(component => component.ZRange.End), 6);
        }
    }

    private static void AssertPoint(JsonElement expected, Point3Mm actual)
    {
        Assert.Equal(expected.GetProperty("x").GetDouble(), actual.X, 6);
        Assert.Equal(expected.GetProperty("y").GetDouble(), actual.Y, 6);
        Assert.Equal(expected.GetProperty("z").GetDouble(), actual.Z, 6);
    }

    private static T Succeed<T>(OperationResult<T> result) where T : class
    {
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Code)));
        return result.Value!;
    }
}
