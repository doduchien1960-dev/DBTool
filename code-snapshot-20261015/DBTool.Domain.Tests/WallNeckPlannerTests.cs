using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class WallNeckPlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    [Fact]
    public void BothNeckFacesStopBelowTheSeatWithTheirSpecifiedCover()
    {
        var seed=SyntheticTestFactory.Create(pipes:[]); var i=seed.Input;
        var neck=new NeckSpec(700,700,125,500,NeckPosition.Center,seed.Sources);
        var seat=new NeckSeatSpec(NeckSeatKind.Step,75,50,2000,seed.Sources);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,0,0,450,i.RoofOpening,
            i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,
            i.SourceRefs,i.UnsupportedRawFields,neckSpec:neck,neckSeatSpec:seat);
        var spec=new ValidatedManholeSpec(input,seed.Catalogs,Policy.Version,seed.Sources,[]);
        var geometry=ChamberGeometryBuilder.Build(spec,Policy);
        Assert.True(geometry.Success);
        var result=NeckRebarPlanner.Plan(spec,geometry.Value!,new([],[]),new(10,150,0,0,coverMm:25));
        Assert.True(result.Success);
        Assert.NotEmpty(result.Value!.Occurrences);
        Assert.Contains(result.Value.Occurrences,b=>b.Surface==RebarSurface.Outer);
        Assert.All(result.Value.Occurrences.SelectMany(b=>b.Path.Segments),s=>
        {
            Assert.True(s.Start.Z<=1970,s.Start.Z.ToString());
            Assert.True(s.End.Z<=1970,s.End.Z.ToString());
        });
    }

    [Theory]
    [InlineData("Wall/T1", 660, 740)]
    [InlineData("Wall/T2", 535, 615)]
    [InlineData("Wall/T3", -660, -740)]
    [InlineData("Wall/T4", -535, -615)]
    public void AsymmetricWallFacesHaveIndependentPhysicalCover(string frame,double inner,double outer)
    {
        var seed=SyntheticTestFactory.Create(pipes:[]);
        var spec=new ValidatedManholeSpec(seed.Input,seed.Catalogs,Policy.Version,seed.Sources,[]);
        var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var result=WallRebarPlanner.Plan(spec,geometry,new MeshZoneSet([],[]),new(10,150,0,50));
        Assert.True(result.Success);
        bool x=frame is "Wall/T1" or "Wall/T3";
        var bars=result.Value!.Occurrences.Where(b=>b.FrameId==frame && b.Direction==RebarDirection.Vertical).ToArray();
        Assert.All(bars,b=>Assert.Equal(b.Surface==RebarSurface.Inner?inner:outer,
            x?b.Path.Segments[0].Start.Y:b.Path.Segments[0].Start.X,6));
        Assert.All(bars,b=>Assert.Equal(35,b.Path.Segments[0].Start.Z,6));
        Assert.All(bars,b=>Assert.Equal(1365,b.Path.Segments[^1].End.Z,6));
    }

    [Theory]
    [InlineData("Wall/T1",658,739)]
    [InlineData("Wall/T2",533,614)]
    [InlineData("Wall/T3",-658,-739)]
    [InlineData("Wall/T4",-533,-614)]
    public void IndependentInnerOuterDiametersKeepFaceCover(string frame,double inner,double outer)
    {
        var seed=SyntheticTestFactory.Create(pipes:[]);
        var spec=new ValidatedManholeSpec(seed.Input,seed.Catalogs,Policy.Version,seed.Sources,[]);
        var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var i=WallRebarPlanner.Plan(spec,geometry,new([],[]),new(6,125,0,0,coverMm:30,innerLayer:true,outerLayer:false));
        var o=WallRebarPlanner.Plan(spec,geometry,new([],[]),new(12,175,0,0,coverMm:30,innerLayer:false,outerLayer:true));
        Assert.True(i.Success);Assert.True(o.Success);
        bool x=frame is "Wall/T1" or "Wall/T3";
        Assert.All(i.Value!.Occurrences.Where(b=>b.FrameId==frame && b.Direction==RebarDirection.Vertical),
            b=>Assert.Equal(inner,x?b.Path.Segments[0].Start.Y:b.Path.Segments[0].Start.X,6));
        Assert.All(o.Value!.Occurrences.Where(b=>b.FrameId==frame && b.Direction==RebarDirection.Vertical),
            b=>Assert.Equal(outer,x?b.Path.Segments[0].Start.Y:b.Path.Segments[0].Start.X,6));
    }

    [Fact]
    public void WallPlannerUsesNappedRoiBodyHeightAndFourDistinctWallFrames()
    {
        var seed = SyntheticTestFactory.Create();
        var input = seed.Input;
        var changed = new ManholeInput(input.Id, input.Name, Profile.GaThamNapRoi, input.L, input.B, input.H,
            input.BaseThicknessMm, input.WallThicknessMm, input.RoofThicknessMm, input.PadThicknessMm,
            input.BaseExpansionMm, input.PadExpansionMm, 20, input.BaseHaunch, input.TopHaunch, input.NeckHeight,
            input.RoofOpening, input.PipeConnections, input.BaseBottomMesh, input.BaseTopMesh, input.CoverMm,
            input.ElevationMode, input.BaseElevationM, input.PipeInvertReference, input.HookBaseBottom,
            input.HookBaseTop, input.DistributionMode, input.MeshJunctionPolicy, input.LayerStackPolicy,
            input.FeatureRequests, input.SourceRefs, input.UnsupportedRawFields);
        var spec = new ValidatedManholeSpec(changed, seed.Catalogs, "S2Rebar-1/v1", seed.Sources, []);
        var geometry = ChamberGeometryBuilder.Build(spec, Policy).Value!;

        var result = WallRebarPlanner.Plan(spec, geometry, new MeshZoneSet([], []),
            new WallOrNeckRebarSpec(12, 150, 1, 50));

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(x => $"{x.Code}:{x.Message}")));
        Assert.Equal(4, result.Value!.Occurrences.Select(x => x.FrameId).Distinct().Count());
        Assert.All(result.Value.Occurrences.Where(x => x.Direction == RebarDirection.Vertical),
            bar => Assert.Equal(1344, bar.Path.Segments.Max(x => x.End.Z), 6));
        Assert.Contains(result.Value.Occurrences, x => x.Direction == RebarDirection.Horizontal && x.DiameterMm == 10);
    }

    [Fact]
    public void StepDownIsCatalogBoundAndNegativeSpacingDeltaBlocks()
    {
        var seed = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(new ValidatedManholeSpec(seed.Input, seed.Catalogs,
            "S2Rebar-1/v1", seed.Sources, []), Policy).Value!;
        var invalidStep = WallRebarPlanner.Plan(new ValidatedManholeSpec(seed.Input, seed.Catalogs,
            "S2Rebar-1/v1", seed.Sources, []), geometry, new MeshZoneSet([], []),
            new WallOrNeckRebarSpec(12, 150, 99, 50));
        var negativeDelta = WallRebarPlanner.Plan(new ValidatedManholeSpec(seed.Input, seed.Catalogs,
            "S2Rebar-1/v1", seed.Sources, []), geometry, new MeshZoneSet([], []),
            new WallOrNeckRebarSpec(12, 150, 1, -1));

        Assert.False(invalidStep.Success);
        Assert.Null(invalidStep.Value);
        Assert.False(negativeDelta.Success);
        Assert.Null(negativeDelta.Value);
    }

    [Fact]
    public void NeckOtherStructureReturnsExplicitNotApplicableWithoutNeckSteel()
    {
        var seed = SyntheticTestFactory.Create();
        var geometry = ChamberGeometryBuilder.Build(new ValidatedManholeSpec(seed.Input, seed.Catalogs,
            "S2Rebar-1/v1", seed.Sources, []), Policy).Value!;
        var result = NeckRebarPlanner.Plan(new ValidatedManholeSpec(seed.Input, seed.Catalogs,
            "S2Rebar-1/v1", seed.Sources, []), geometry, new MeshZoneSet([], []),
            new WallOrNeckRebarSpec(12, 150, 1, 50, StructureKind.Other));

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Occurrences);
        Assert.Equal(LayoutStatus.NotApplicable, result.Value.Status);
        Assert.Contains(result.Diagnostics, x => x.Code == "NECK_STEEL_NOT_APPLICABLE");
    }
}
