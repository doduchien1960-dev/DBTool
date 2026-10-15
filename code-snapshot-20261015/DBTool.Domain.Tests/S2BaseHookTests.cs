using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class S2BaseHookTests
{
    [Theory]
    [InlineData(HookPattern.Hook0M, 0)]
    [InlineData(HookPattern.Hook1M, 1)]
    [InlineData(HookPattern.HookStartOnly, 1)]
    [InlineData(HookPattern.Hook2M, 2)]
    public void ExpandedConcreteDoesNotSuppressRequestedDesignMeshHooks(HookPattern pattern, int ends)
    {
        var seed = SyntheticTestFactory.Create(pipes: []);
        var i = seed.Input;
        var main = new MainRebarInput(null, null, null, null, 30, null, null, StructureKind.Other, null, []);
        var input = new ManholeInput(i.Id, i.Name, i.Profile, i.L, i.B, i.H, i.BaseThicknessMm,
            i.WallThicknessMm, i.RoofThicknessMm, i.PadThicknessMm, i.BaseExpansionMm, i.PadExpansionMm,
            i.BeddingMortarMm, i.BaseHaunch, i.TopHaunch, i.NeckHeight, i.RoofOpening, i.PipeConnections,
            i.BaseBottomMesh, i.BaseTopMesh, i.CoverMm, i.ElevationMode, i.BaseElevationM,
            i.PipeInvertReference, pattern, pattern, i.DistributionMode, i.MeshJunctionPolicy,
            i.LayerStackPolicy, i.FeatureRequests, i.SourceRefs, i.UnsupportedRawFields, mainRebar: main);
        var policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;
        var spec = new ValidatedManholeSpec(input, seed.Catalogs, policy.Version, seed.Sources, []);
        var geometry = ChamberGeometryBuilder.Build(spec, policy).Value!;
        var straight = SlabRebarPlanner.PlanS2Base(spec, geometry, policy).Value!;
        var result = NominalHookPlanner.Apply(spec, geometry, straight.Occurrences, main);
        Assert.True(result.Success);
        Assert.Equal(straight.Occurrences.Length, result.Value!.Occurrences.Length);
        foreach (var (before, after) in straight.Occurrences.Zip(result.Value.Occurrences))
        {
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.DiameterMm, after.DiameterMm);
            Assert.Equal(pattern, after.Path.HookPattern);
            Assert.Equal(1 + ends, after.Path.Segments.Length);
            Assert.Equal(before.ScheduleLengthMm + ends * 90, after.ScheduleLengthMm, 6);
            Assert.Contains(before.Path.Segments.Single(), after.Path.Segments);
            if (ends > 0)
                Assert.Equal(MeasurementBasis.NominalPolyline, after.Path.MeasurementBasis);
        }
    }
}
