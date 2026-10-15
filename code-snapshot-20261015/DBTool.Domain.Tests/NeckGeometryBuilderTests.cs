using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class NeckGeometryBuilderTests
{
    private static readonly CalculationPolicy Policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
    private static ValidatedManholeSpec Spec()
    {
        var seed=SyntheticTestFactory.Create();
        return new ValidatedManholeSpec(seed.Input,seed.Catalogs,Policy.Version,seed.Sources,[]);
    }
    private static NeckSpec Neck(double width=700,double length=700,double thickness=100,double height=600,NeckPosition position=NeckPosition.Center) =>
        new(width,length,thickness,height,position,SyntheticTestFactory.Create().Sources);
    private static NeckSeatSpec Seat(NeckSeatKind kind=NeckSeatKind.None,double expansion=0,double height=0) =>
        new(kind,expansion,height,kind==NeckSeatKind.None?1550:2150-height,SyntheticTestFactory.Create().Sources);

    [Fact]
    public void NinePositionsUseBothClearDimensions()
    {
        foreach(var (position,x,y) in new (NeckPosition,double,double)[]
        {
            (NeckPosition.Center,0,0),(NeckPosition.North,0,325),(NeckPosition.East,150,0),
            (NeckPosition.South,0,-325),(NeckPosition.West,-150,0),
            (NeckPosition.NorthEast,150,325),(NeckPosition.SouthEast,150,-325),
            (NeckPosition.SouthWest,-150,-325),(NeckPosition.NorthWest,-150,325)
        })
        {
            var result=NeckGeometryBuilder.Build(Spec(),Neck(700,600,100,600,position),Seat(),Policy);
            Assert.True(result.Success);
            Assert.Equal(new Point2Mm(x,y),result.Value!.Center);
            Assert.Equal(420000,result.Value.HoleAreaMm2);
        }
    }

    [Fact]
    public void NonSquareHoleUsesItsOwnWidthAndLength()
    {
        var result=NeckGeometryBuilder.Build(Spec(),Neck(820,570,50,600),Seat(),Policy);
        Assert.True(result.Success); Assert.Equal(467400,result.Value!.HoleAreaMm2);
    }

    [Fact]
    public void GrossNeckAndStepAndSlopeDeductionsAreSeparate()
    {
        var plain=NeckGeometryBuilder.Build(Spec(),Neck(),Seat(),Policy).Value!;
        var step=NeckGeometryBuilder.Build(Spec(),Neck(),Seat(NeckSeatKind.Step,50,50),Policy).Value!;
        var slope=NeckGeometryBuilder.Build(Spec(),Neck(),Seat(NeckSeatKind.Slope,50,50),Policy).Value!;
        Assert.Equal(192000000,plain.GrossVolumeMm3,6);
        Assert.Equal(0,plain.SeatRemovedMm3);
        Assert.Equal(7500000,step.SeatRemovedMm3,6);
        Assert.Equal(3666666.666666667,slope.SeatRemovedMm3,6);
        Assert.Equal(2100,step.SeatProfile.SupportPlaneMm);
        Assert.Equal(2150,step.SeatProfile.TopPlaneMm);
        var component=Assert.Single(step.Components);
        Assert.Equal(4,component.FacePatches.Count(p=>p.Role==FaceRole.Top));
        Assert.Equal(170000,component.FacePatches.Where(p=>p.Role==FaceRole.Top).Sum(p=>p.AreaMm2.Value),6);
    }

    [Fact]
    public void ZeroHeightHasRoofOpeningWithoutNeckComponent()
    {
        var result=NeckGeometryBuilder.Build(Spec(),Neck(height:0),Seat(),Policy).Value!;
        Assert.Empty(result.Components);
        Assert.NotNull(result.RoofOpening);
        Assert.Equal(490000,result.HoleAreaMm2);
    }

    [Fact]
    public void OversizedNeckAndImpossibleStepBlockWithNull()
    {
        var wide=NeckGeometryBuilder.Build(Spec(),Neck(width:1001),Seat(),Policy);
        Assert.False(wide.Success); Assert.Null(wide.Value);
        Assert.Contains(wide.Diagnostics,d=>d.Code=="NECK_OPENING_OUTSIDE" && d.Severity==DiagnosticSeverity.Block);
        var wall=NeckGeometryBuilder.Build(Spec(),Neck(thickness:50),Seat(NeckSeatKind.Step,50,50),Policy);
        Assert.False(wall.Success); Assert.Null(wall.Value);
        Assert.Contains(wall.Diagnostics,d=>d.Code=="NECK_SEAT_WALL_INVALID");
    }

    [Fact]
    public void SeatDatumMustMatchCutLocation()
    {
        var result=NeckGeometryBuilder.Build(Spec(),Neck(),new NeckSeatSpec(NeckSeatKind.Step,50,50,1550,SyntheticTestFactory.Create().Sources),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="NECK_SEAT_DATUM_MISMATCH");
    }

    [Fact]
    public void CollarOuterMinusOpeningHasHandComputedVolume()
    {
        var source=SyntheticTestFactory.Create().Sources;
        var collar=new CollarSpec(new RectangleProfile(new Rectangle2Mm(-500,-500,500,500)),
            new RectangleProfile(new Rectangle2Mm(-400,-400,400,400)),new Interval1D(1550,1650),"Concrete",source);
        var result=CollarGeometryBuilder.Build(Spec(),Seat(),collar,Policy);
        Assert.True(result.Success);
        Assert.Equal(36000000,(result.Value!.Footprint.Outer.SignedArea+result.Value.Footprint.InnerLoops.Sum(x=>x.SignedArea))*result.Value.ZRange.Length,6);
        Assert.Equal(4,result.Value.FacePatches.Count(p=>p.Role==FaceRole.Top));
        Assert.Equal(360000,result.Value.FacePatches.Sum(p=>p.AreaMm2.Value),6);
    }

    [Fact]
    public void PurchasedCoverRequiresCatalogAndKeepsPhysicalAnchorAtUnitScale()
    {
        var seed=SyntheticTestFactory.Create(); var i=seed.Input; var source=seed.Sources;
        var physical=new Dimensions2Mm(850,850);
        var purchase=new PurchasedCoverSpec("COVER-850",new Point3Mm(12,34,2160),physical,source);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            i.RoofOpening,i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,
            i.PipeInvertReference,i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,
            i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields,purchasedCoverSpec:purchase);
        var missing=new ValidatedManholeSpec(input,seed.Catalogs,Policy.Version,source,[]);
        var blocked=NeckGeometryBuilder.Build(missing,Neck(),Seat(),Policy);
        Assert.False(blocked.Success); Assert.Null(blocked.Value);
        Assert.Contains(blocked.Diagnostics,d=>d.Code=="COVER_CATALOG_MISSING" && d.Source is not null);

        var catalogs=new CatalogSet(seed.Catalogs.Pipes,seed.Catalogs.Angles,seed.Catalogs.Hatches,seed.Catalogs.Aliases,
            new Dictionary<string,CoverCatalogEntry>{{"COVER-850",new CoverCatalogEntry("COVER-850",physical)}});
        var spec=new ValidatedManholeSpec(input,catalogs,Policy.Version,source,[]);
        var unresolved=NeckGeometryBuilder.Build(spec,Neck(height:600),Seat(),Policy);
        Assert.False(unresolved.Success);
        Assert.Contains(unresolved.Diagnostics,d=>d.Code=="U_NECK_COVER_CATALOG");
        catalogs=new CatalogSet(seed.Catalogs.Pipes,seed.Catalogs.Angles,seed.Catalogs.Hatches,seed.Catalogs.Aliases,
            new Dictionary<string,CoverCatalogEntry>{{"COVER-850",new CoverCatalogEntry("COVER-850",physical,new(0,0,0),0,
                CoverPlacementDatum.NeckTop,new(-425,-425,425,425),source)}});
        spec=new ValidatedManholeSpec(input,catalogs,Policy.Version,source,[]);
        var result=NeckGeometryBuilder.Build(spec,Neck(height:600),Seat(),Policy);
        Assert.True(result.Success);
        var placement=result.Value!.PurchasedCoverPlacement!;
        Assert.Equal("COVER-850",placement.CatalogId);
        Assert.Equal(new Point3Mm(12,34,2160),placement.Anchor);
        Assert.Equal(physical,placement.PhysicalSize);
        Assert.Equal(1,placement.Scale);
        var narrower=NeckGeometryBuilder.Build(spec,Neck(width:600,height:600),Seat(),Policy);
        Assert.True(narrower.Success);
        Assert.Equal(placement.Anchor,narrower.Value!.PurchasedCoverPlacement!.Anchor);
        Assert.Equal(placement.PhysicalSize,narrower.Value.PurchasedCoverPlacement.PhysicalSize);
    }

    [Fact]
    public void DerivedOuterOverflowReturnsBlockInsteadOfThrowing()
    {
        var result=NeckGeometryBuilder.Build(Spec(),Neck(thickness:double.MaxValue),Seat(),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="NECK_OVERFLOW" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void CollarMustIntersectExplicitSupportPlane()
    {
        var source=SyntheticTestFactory.Create().Sources;
        var collar=new CollarSpec(new RectangleProfile(new Rectangle2Mm(-500,-500,500,500)),
            new RectangleProfile(new Rectangle2Mm(-400,-400,400,400)),new Interval1D(1400,1500),"Concrete",source);
        var result=CollarGeometryBuilder.Build(Spec(),Seat(),collar,Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COLLAR_SUPPORT_INVALID");
    }
}
