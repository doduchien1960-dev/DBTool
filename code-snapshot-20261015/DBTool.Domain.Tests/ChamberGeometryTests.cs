using DBTool.Domain.Tests.Fixtures;
using DBTool.Domain.Geometry;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class ChamberGeometryTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;

    [Fact]
    public void MonoFixtureHasExactExtentsElevationOpeningAndDisjointWalls()
    {
        var result = ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(), Policy);
        Assert.True(result.Success);
        var model = Assert.IsType<GeometryModel>(result.Value);
        Assert.Equal(7, model.Components.Length);
        Check(model, "Pad", ComponentKind.Pad, -850, -975, 850, 975, -250, -150);
        Check(model, "Base", ComponentKind.Base, -750, -875, 750, 875, -150, 0);
        Check(model, "Wall/T1", ComponentKind.Wall, -650, 625, 650, 775, 0, 1400);
        Check(model, "Wall/T2", ComponentKind.Wall, 500, -625, 650, 625, 0, 1400);
        Check(model, "Wall/T3", ComponentKind.Wall, -650, -775, 650, -625, 0, 1400);
        Check(model, "Wall/T4", ComponentKind.Wall, -650, -625, -500, 625, 0, 1400);
        Check(model, "Roof", ComponentKind.Roof, -650, -775, 650, 775, 1400, 1550);
        Assert.Equal(765000, model.Components.Where(c => c.Kind == ComponentKind.Wall).Sum(c => Area(c.Footprint)), 6);
        var roof = model.Components.Single(c => c.Id == "Roof");
        Assert.Empty(Assert.IsType<ExtrusionSolid>(roof.Occupied).Footprint.InnerLoops);
        Assert.Single(roof.Footprint.InnerLoops);
        Assert.True(roof.Footprint.Outer.Closed);
        Assert.All(roof.Footprint.InnerLoops, loop => Assert.True(loop.Closed));
        Assert.Equal(new Point2Mm(-350, 350), roof.Footprint.InnerLoops[0].Vertices[0]);
        Assert.DoesNotContain(model.Components, c => c.Kind == ComponentKind.Neck);
        var hole = Assert.Single(model.Openings);
        Assert.Equal("Roof/Hole", hole.Id);
        Assert.Equal("Roof", hole.OwnerId);
        Assert.Equal(new Rectangle2Mm(-350,-350,350,350), Assert.IsType<RectangleProfile>(hole.Inner).Rectangle);
        Assert.Equal(new Interval1D(1400,1550), hole.ZRange);
        Assert.NotEmpty(hole.Sources);
        Assert.All(model.Components,c => Assert.NotEmpty(c.Sources));
        Assert.Contains(result.Diagnostics,d => d.Code == "GEOMETRY_PIPE_DEFERRED" && d.Severity == DiagnosticSeverity.Review &&
            d.Scope == DiagnosticScope.Component && d.ComponentId == "Wall/T1" && d.Source is not null);
        Assert.All(model.Components, c => Assert.NotEmpty(c.FacePatches));
        Assert.All(model.Components.Where(c => c.Kind == ComponentKind.Wall), c =>
            Assert.Equal([FaceRole.Inner, FaceRole.Outer, FaceRole.Top, FaceRole.Bottom], c.FacePatches.Select(f => f.Role)));
        Assert.Equal(4,roof.FacePatches.Count(p => p.Role == FaceRole.Top));
        Assert.Equal(1525000,roof.FacePatches.Where(p => p.Role == FaceRole.Top).Sum(p => p.AreaMm2.Value),6);
        Assert.Equal(new Point3Mm(-650,775,0),model.Components.Single(c => c.Id == "Wall/T1").FacePatches.Single(p => p.Role == FaceRole.Outer).Frame.Origin);
        Assert.Equal(new Point3Mm(-650,-625,0),model.Components.Single(c => c.Id == "Wall/T3").FacePatches.Single(p => p.Role == FaceRole.Inner).Frame.Origin);
    }

    [Fact]
    public void ZeroExpansionsKeepBaseAndPadAtPrecedingBounds()
    {
        var model = ChamberGeometryBuilder.Build(Variant(baseExpansion:0, padExpansion:0), Policy).Value!;
        Check(model,"Base",ComponentKind.Base,-650,-775,650,775,-150,0);
        Check(model,"Pad",ComponentKind.Pad,-650,-775,650,775,-250,-150);
    }

    [Fact]
    public void PrecastHasShorterWallsAndSeparateMortarBandButRoofStaysAtH()
    {
        var model = ChamberGeometryBuilder.Build(Variant(profile:Profile.GaThamNapRoi, beddingMortar:15), Policy).Value!;
        foreach (var id in new[] {"Wall/T1","Wall/T2","Wall/T3","Wall/T4"})
            Assert.Equal(new Interval1D(0,1385), model.Components.Single(c => c.Id == id).ZRange);
        Assert.Equal(new Interval1D(1385,1400), model.Components.Single(c => c.Id == "Wall/Mortar").ZRange);
        Assert.Equal("BeddingMortar", model.Components.Single(c => c.Id == "Wall/Mortar").MaterialId);
        Assert.Equal(new Interval1D(1400,1550), model.Components.Single(c => c.Id == "Roof").ZRange);
    }

    [Fact]
    public void InvalidOpeningAndMortarReturnBlockWithoutValue()
    {
        var opening = ChamberGeometryBuilder.Build(Variant(roofOpening:new Rectangle2Mm(-550,-350,550,350)),Policy);
        Assert.False(opening.Success);
        Assert.Null(opening.Value);
        Assert.Contains(opening.Diagnostics,d => d.Code == "GEOMETRY_CONTAINMENT" && d.Severity == DiagnosticSeverity.Block &&
            d.Scope == DiagnosticScope.Component && d.ComponentId == "Roof" && d.Source is not null);

        var mortar = ChamberGeometryBuilder.Build(Variant(profile:Profile.GaThamNapRoi,beddingMortar:1400),Policy);
        Assert.False(mortar.Success);
        Assert.Null(mortar.Value);
        Assert.Contains(mortar.Diagnostics,d => d.Code == "GEOMETRY_HEIGHT_INVALID" && d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Manhole);
    }

    [Fact]
    public void PresentationScaleIsAbsentFromTheGeometryApi()
    {
        var parameters=typeof(ChamberGeometryBuilder).GetMethod(nameof(ChamberGeometryBuilder.Build))!.GetParameters();
        Assert.Equal([typeof(ValidatedManholeSpec),typeof(CalculationPolicy)],parameters.Select(p => p.ParameterType));
    }

    [Fact]
    public void MonoGeometryMatchesIndependentJsonSnapshot()
    {
        var model=ChamberGeometryBuilder.Build(SyntheticTestFactory.Create(),Policy).Value!;
        var actual=new
        {
            policyId=Policy.PolicyId,
            components=model.Components.Select(c => new
            {
                id=c.Id, material=c.MaterialId,
                x0=c.Footprint.Outer.Vertices.Min(v=>v.X),y0=c.Footprint.Outer.Vertices.Min(v=>v.Y),
                x1=c.Footprint.Outer.Vertices.Max(v=>v.X),y1=c.Footprint.Outer.Vertices.Max(v=>v.Y),
                z0=c.ZRange.Start,z1=c.ZRange.End,
                holeCount=c.Footprint.InnerLoops.Length
            }).ToArray(),
            openings=model.Openings.Select(o => new {id=o.Id,owner=o.OwnerId,x0=Assert.IsType<RectangleProfile>(o.Inner).Rectangle.MinX,
                y0=Assert.IsType<RectangleProfile>(o.Inner).Rectangle.MinY,x1=Assert.IsType<RectangleProfile>(o.Inner).Rectangle.MaxX,
                y1=Assert.IsType<RectangleProfile>(o.Inner).Rectangle.MaxY,z0=o.ZRange.Start,z1=o.ZRange.End}).ToArray()
        };
        var expected=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Tests","Fixtures","t11-geometry.expected.json")));
        var observed=JsonNode.Parse(JsonSerializer.Serialize(actual));
        Assert.True(JsonNode.DeepEquals(expected,observed),$"Expected {expected}; observed {observed}");
    }

    private static void Check(GeometryModel model,string id,ComponentKind kind,double minX,double minY,double maxX,double maxY,double zMin,double zMax)
    {
        var c = model.Components.Single(c => c.Id == id);
        Assert.Equal(kind,c.Kind);
        Assert.Equal(new Interval1D(zMin,zMax),c.ZRange);
        Assert.Equal(minX,c.Footprint.Outer.Vertices.Min(p => p.X),6);
        Assert.Equal(minY,c.Footprint.Outer.Vertices.Min(p => p.Y),6);
        Assert.Equal(maxX,c.Footprint.Outer.Vertices.Max(p => p.X),6);
        Assert.Equal(maxY,c.Footprint.Outer.Vertices.Max(p => p.Y),6);
        Assert.Equal(4,c.Footprint.Outer.Vertices.Length);
    }

    private static double Area(Footprint footprint) =>
        Math.Abs(Shoelace(footprint.Outer)) - footprint.InnerLoops.Sum(x => Math.Abs(Shoelace(x)));

    private static double Shoelace(Polygon2Mm polygon)
    {
        var points = polygon.Vertices;
        double sum=0;
        for (int i=0;i<points.Length;i++) sum += points[i].X*points[(i+1)%points.Length].Y-points[(i+1)%points.Length].X*points[i].Y;
        return sum/2;
    }

    private static ValidatedManholeSpec Variant(Profile? profile=null,double? baseExpansion=null,double? padExpansion=null,double? beddingMortar=null,Rectangle2Mm? roofOpening=null)
    {
        var seed = SyntheticTestFactory.Create();
        var i = seed.Input;
        var input = new ManholeInput(i.Id,i.Name,profile ?? i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,i.PadThicknessMm,
            baseExpansion ?? i.BaseExpansionMm,padExpansion ?? i.PadExpansionMm,beddingMortar ?? i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            roofOpening ?? i.RoofOpening,i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields);
        return new ValidatedManholeSpec(input,seed.Catalogs,seed.RuleVersion,seed.Sources,seed.FeatureDiagnostics);
    }

}
