using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using System.Text.Json;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class BaseSlabRebarTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S1-Analytic-1", true).Value!;

    [Fact]
    public void S1Has48PhysicalStraightBarsAtFourDistinctHeights()
    {
        var layout = Plan(SyntheticTestFactory.Create());
        Assert.Equal(CalculationScope.BaseRebarOnly, layout.Scope);
        Assert.Equal(48, layout.Occurrences.Length);
        Assert.Equal(48, layout.Occurrences.Select(b => b.Id).Distinct().Count());
        Assert.Equal(74620, layout.Occurrences.Sum(b => b.ScheduleLengthMm), 6);
        foreach (var (layer, axis, count, length, z) in new[] {
            (Layer.Bottom,Axis.X,13,1440d,-115d),(Layer.Bottom,Axis.Y,11,1690d,-105d),
            (Layer.Top,Axis.X,13,1440d,-35d),(Layer.Top,Axis.Y,11,1690d,-45d) })
        {
            var bars = layout.Occurrences.Where(b => b.Layer == layer && b.Axis == axis).ToArray();
            Assert.Equal(count, bars.Length);
            Assert.All(bars, b => {
                Assert.Equal(length, b.ScheduleLengthMm, 6);
                Assert.Equal(length, b.FabricationLengthMm!.Value, 6);
                Assert.Equal(LengthStatus.Known, b.LengthStatus);
                Assert.Equal(HookPattern.Hook0M, b.Path.HookPattern);
                Assert.Equal(10, b.DiameterMm);
                Assert.Null(b.ParentId);
                Assert.Equal(ComponentKind.Base, b.ComponentKind);
                Assert.NotEmpty(b.Sources);
                Assert.Equal(z, b.Path.Segments[0].Start.Z, 6);
            });
        }
        var firstX = layout.Occurrences.Single(b => b.Layer == Layer.Bottom && b.Axis == Axis.X && b.Id.EndsWith("/001"));
        Assert.Equal(new Point3Mm(-720,-840,-115), firstX.Path.Segments[0].Start);
        Assert.Equal(new Point3Mm(720,-840,-115), firstX.Path.Segments[0].End);
        var firstY = layout.Occurrences.Single(b => b.Layer == Layer.Bottom && b.Axis == Axis.Y && b.Id.EndsWith("/001"));
        Assert.Equal(new Point3Mm(-715,-845,-105), firstY.Path.Segments[0].Start);
        Assert.Equal(new Point3Mm(-715,845,-105), firstY.Path.Segments[0].End);
    }

    [Fact]
    public void TopA200ChangesOnlyTopCountsAndTotalLength()
    {
        var layout = Plan(Variant(topSpacing: 200));
        Assert.Equal(43, layout.Occurrences.Length);
        Assert.Equal(66920, layout.Occurrences.Sum(b => b.ScheduleLengthMm), 6);
        Assert.Equal(24, layout.Occurrences.Count(b => b.Layer == Layer.Bottom));
        Assert.Equal(10, layout.Occurrences.Count(b => b.Layer == Layer.Top && b.Axis == Axis.X));
        Assert.Equal(9, layout.Occurrences.Count(b => b.Layer == Layer.Top && b.Axis == Axis.Y));
        Assert.Equal(29610, layout.Occurrences.Where(b => b.Layer == Layer.Top).Sum(b => b.ScheduleLengthMm), 6);
    }

    [Fact]
    public void ThinBaseBlocksLayerCollisionWithNullValue()
    {
        var spec = Variant(thickness: 90);
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy).Value!;
        var result = SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "BASE_LAYER_COLLISION" && d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Manhole);
    }

    [Fact]
    public void GeometryComponentOrderDoesNotChangePhysicalBarIdentityOrPath()
    {
        var spec = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy).Value!;
        var original = SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy).Value!;
        var reversed = SlabRebarPlanner.PlanBase(spec,
            new GeometryModel(geometry.Components.Reverse(), geometry.Openings), zones, Policy).Value!;
        Assert.Equal(original.Occurrences.Select(b => b.Id), reversed.Occurrences.Select(b => b.Id));
        Assert.Equal(original.Occurrences.Select(b => b.Path.Segments[0].Start),
            reversed.Occurrences.Select(b => b.Path.Segments[0].Start));
    }

    [Fact]
    public void BaseOpeningReturnsBlockWithoutPartialLayout()
    {
        var spec = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy).Value!;
        var hole = new Polygon2Mm(new[] {
            new Point2Mm(-50,50),new Point2Mm(50,50),new Point2Mm(50,-50),new Point2Mm(-50,-50) });
        var footprint = new Footprint(baseComponent.Footprint.Outer, [hole]);
        var changedBase = new ComponentGeometry(baseComponent.Id,baseComponent.Kind,baseComponent.MaterialId,footprint,
            baseComponent.ZRange,baseComponent.Occupied,baseComponent.Voids,baseComponent.FacePatches,baseComponent.Sources);
        var changed = new GeometryModel(geometry.Components.Select(c => c.Id == "Base" ? changedBase : c), geometry.Openings);
        var result = SlabRebarPlanner.PlanBase(spec, changed, zones, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "BASE_OPENING_UNSUPPORTED" && d.Severity == DiagnosticSeverity.Block && d.Scope == DiagnosticScope.Component);
    }

    [Fact]
    public void WritesS1LayoutArtifactWhenOutputDirectoryIsProvided()
    {
        var layout = Plan(SyntheticTestFactory.Create());
        string? directory = Environment.GetEnvironmentVariable("DBTOOL_T22A_OUTPUT");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var rows = layout.Occurrences.Select(b => new {
            b.Id,b.ComponentId,Layer=b.Layer.ToString(),Axis=b.Axis.ToString(),b.DiameterMm,
            b.ScheduleLengthMm,b.FabricationLengthMm,
            Start=b.Path.Segments[0].Start,End=b.Path.Segments[0].End
        });
        File.WriteAllText(Path.Combine(directory,"layout.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions { WriteIndented=true }));
    }

    private static RebarLayout Plan(ValidatedManholeSpec spec)
    {
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;
        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var zones = MeshZonePlanner.PlanUniformBase(baseComponent, spec.Input.BaseBottomMesh, spec.Input.CoverMm, Policy).Value!;
        var result = SlabRebarPlanner.PlanBase(spec, geometry, zones, Policy);
        Assert.True(result.Success);
        return result.Value!;
    }

    private static ValidatedManholeSpec Variant(double? topSpacing = null, double? thickness = null)
    {
        var seed = SyntheticTestFactory.Create(); var i = seed.Input;
        var changed = new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,thickness ?? i.BaseThicknessMm,
            i.WallThicknessMm,i.RoofThicknessMm,i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,
            i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,i.RoofOpening,i.PipeConnections,
            i.BaseBottomMesh,new MeshSpec(10,topSpacing ?? i.BaseTopMesh.SpacingMm),i.CoverMm,i.ElevationMode,
            i.BaseElevationM,i.PipeInvertReference,i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,
            i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields);
        return new ValidatedManholeSpec(changed,seed.Catalogs,seed.RuleVersion,seed.Sources,seed.FeatureDiagnostics);
    }
}
