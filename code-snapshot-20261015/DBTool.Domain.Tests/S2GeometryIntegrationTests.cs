using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class S2GeometryIntegrationTests
{
    private static readonly CalculationPolicy Policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;

    [Fact]
    public void ExplicitHaunchNeckAndPanelAppearInOneCanonicalGeometryModel()
    {
        var seed=SyntheticTestFactory.Create(); var i=seed.Input; var sources=seed.Sources;
        var baseHaunch=new HaunchSpec(100,100,HaunchOwner.Base,sources);
        var neck=new NeckSpec(700,700,100,600,NeckPosition.Center,sources);
        var seat=new NeckSeatSpec(NeckSeatKind.Step,50,50,2100,sources);
        var panel=new CoverPanelSpec(2,5,SplitAxis.X,PanelSupportMode.Recess,100,sources);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,100,0,600,i.RoofOpening,
            i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,
            i.SourceRefs,i.UnsupportedRawFields,baseHaunchSpec:baseHaunch,neckSpec:neck,neckSeatSpec:seat,coverPanelSpec:panel);
        var spec=new ValidatedManholeSpec(input,seed.Catalogs,Policy.Version,sources,[]);
        var result=ChamberGeometryBuilder.Build(spec,Policy);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var model=result.Value!;
        Assert.Contains(model.Components,c=>c.Id=="Base/Haunch" && c.Kind==ComponentKind.Base);
        Assert.Contains(model.Components,c=>c.Id=="Neck" && c.Kind==ComponentKind.Neck);
        Assert.Equal(2,model.Components.Count(c=>c.Kind==ComponentKind.Panel));
        Assert.Single(model.Openings);
        Assert.Equal(new Rectangle2Mm(-350,-350,350,350),((RectangleProfile)model.Openings[0].Inner).Rectangle);
    }

    [Fact]
    public void ActiveNeckWithoutExplicitSeatRuleBlocksProductionGeometry()
    {
        var seed=SyntheticTestFactory.Create(); var i=seed.Input; var sources=seed.Sources;
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,0,0,600,i.RoofOpening,
            i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,
            i.SourceRefs,i.UnsupportedRawFields,neckSpec:new NeckSpec(700,700,100,600,NeckPosition.Center,sources));
        var result=ChamberGeometryBuilder.Build(new ValidatedManholeSpec(input,seed.Catalogs,Policy.Version,sources,[]),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="U_NECK_SEAT" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void TypedBaseAndRoofHaunchCannotOverlapEvenWhenLegacyScalarsAreZero()
    {
        var seed=SyntheticTestFactory.Create(); var i=seed.Input; var sources=seed.Sources;
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,0,0,0,i.RoofOpening,
            i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,
            i.SourceRefs,i.UnsupportedRawFields,
            baseHaunchSpec:new HaunchSpec(100,100,HaunchOwner.Base,sources),
            topHaunchSpec:new HaunchSpec(100,1350,HaunchOwner.Roof,sources));
        var result=ChamberGeometryBuilder.Build(new ValidatedManholeSpec(input,seed.Catalogs,Policy.Version,sources,[]),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="HAUNCH_TOPOLOGY_OVERLAP");
    }
}
