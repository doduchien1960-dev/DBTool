using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class Phase1CContractTests
{
    [Fact]
    public void OpeningDiameterMustFitBothSupportedFaces()
    {
        var spec=RoofSpec(60);var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var geometry=ChamberGeometryBuilder.Build(spec,policy).Value!;
        var result=VhgOpeningCompositionPlanner.Plan(spec,geometry,true);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="OPENING_FACE_CLEARANCE_INVALID");
    }

    [Fact]
    public void NeighborVoidCannotBeIgnoredByOpeningExtensions()
    {
        var spec=RoofSpec(10);var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var geometry=ChamberGeometryBuilder.Build(spec,policy).Value!;
        var profile=new CircleProfile(new(500,0),30);
        var cut=new OpeningProfile("Roof/Neighbor","Roof",OpeningKind.Round,profile,null,profile,null,
            new(new(0,0,spec.Input.H),new(1,0,0),new(0,1,0),new(0,0,1)),
            new(spec.Input.H,spec.Input.H+150),new(0,150),spec.Sources);
        geometry=new(geometry.Components,geometry.Openings.Add(cut));
        var result=VhgOpeningCompositionPlanner.Plan(spec,geometry,true);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="OPENING_NEIGHBOR_CLEARANCE_UNRESOLVED");
    }

    private static ValidatedManholeSpec RoofSpec(int diameter)
    {
        var seed=SyntheticTestFactory.Create(pipes:[]);var i=seed.Input;
        var opening=new VhgOpeningInput(diameter,1,250,30,30,VhgOpeningComposition.BoSquare,seed.Sources);
        var accessories=new AccessoryRebarInput(null,null,null,null,null,opening,seed.Sources);
        var main=new MainRebarInput(null,null,new(10,150),new(10,150),30,null,null,StructureKind.Other,null,[],accessories:accessories);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            i.RoofOpening,i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,
            i.PipeInvertReference,i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,
            i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields,mainRebar:main);
        return new(input,seed.Catalogs,CalculationPolicy.Select("S2Rebar-1",true).Value!.Version,seed.Sources,[]);
    }

    [Fact]
    public void OpeningInputRejectsUnknownCompositionAndCopiesSources()
    {
        var sources=SyntheticTestFactory.Create().Sources.ToList();
        Assert.Throws<ArgumentOutOfRangeException>(()=>new VhgOpeningInput(10,1,250,30,10,(VhgOpeningComposition)100,sources));
        var input=new VhgOpeningInput(10,0,250,30,10,VhgOpeningComposition.Bo,sources);
        sources.Clear();
        Assert.NotEmpty(input.Sources);
        Assert.True(VhgOpeningCompositionPlanner.CanConsume(input));
        Assert.False(VhgOpeningCompositionPlanner.CanConsume(new(10,2,250,30,10,VhgOpeningComposition.BoSquare,input.Sources)));
    }

    [Fact]
    public void GeometryCopiesPurchasedPlacementsWithoutFabricatingComponentsOrQuantities()
    {
        var source=SyntheticTestFactory.Create().Sources;
        var placements=new List<PurchasedCoverPlacement>{new("C",new(10,20,30),new(100,200),1,37,new(-50,-100,50,100),source)};
        var model=new GeometryModel([],[],placements);
        placements.Clear();
        var placement=Assert.Single(model.PurchasedCovers);
        Assert.Equal(37,placement.RotationDegrees);
        Assert.Equal(new Point3Mm(10,20,30),placement.Anchor);
        Assert.Empty(model.Components);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new PurchasedCoverPlacement("C",new(0,0,0),new(100,200),0,0,new(-50,-100,50,100),source));
    }
}
