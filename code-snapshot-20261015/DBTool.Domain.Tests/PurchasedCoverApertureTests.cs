using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class PurchasedCoverApertureTests
{
    [Theory]
    [InlineData(false,150)]
    [InlineData(false,200)]
    [InlineData(false,250)]
    [InlineData(true,150)]
    public void GeometryCarriesResolvedNativeSectionReferencesAndRejectsAnInconsistentRequestedAnchor(bool displace,double thickness)
    {
        var seed=SyntheticTestFactory.Create();var i=seed.Input;var source=seed.Sources;
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var physical=new Dimensions2Mm(1000,1000);
        var requested=new PurchasedCoverSpec("NATIVE-FIX",new(displace?0:-10,-20,2160),physical,source,90);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            i.RoofOpening,i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,
            i.PipeInvertReference,i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,
            i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields,purchasedCoverSpec:requested);
        var blocks=new CoverBlockSet([
            new("NATIVE-FIX.MB","MB",new(0,0,0),new(-500,-500,500,500),new(20,-10),source),
            new("NATIVE-FIX.AA","AA",new(0,0,0),new(-500,-100,500,0),new(12,-3),source),
            new("NATIVE-FIX.BB","BB",new(0,0,0),new(-500,-100,500,0),new(-7,5),source)],
            new CircleProfile(new(20,-10),325),[],source,sectionOutlineRule:new(125,80,source));
        var entry=new CoverCatalogEntry("NATIVE-FIX",physical,new(-10,-20,0),90,CoverPlacementDatum.NeckTop,
            new(-500,-500,500,500),source,new CoverOpeningContract(new CircleProfile(new(20,-10),325),100,source),blocks);
        var catalogs=new CatalogSet(seed.Catalogs.Pipes,seed.Catalogs.Angles,seed.Catalogs.Hatches,seed.Catalogs.Aliases,
            new Dictionary<string,CoverCatalogEntry>{{entry.Code,entry}});
        var result=NeckGeometryBuilder.Build(new(input,catalogs,policy.Version,source,[]),
            new(650,650,thickness,600,NeckPosition.Center,source),new(NeckSeatKind.None,0,0,1550,source),policy);
        if(displace)
        {
            Assert.False(result.Success);Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_NATIVE_ANCHOR_MISMATCH" && d.Severity==DiagnosticSeverity.Block);
        }
        else
        {
            Assert.True(result.Success);
            var purchase=result.Value!.PurchasedCoverPlacement!;
            var views=Assert.IsType<CoverBlockPlacements>(purchase.NativeViewPlacement);
            Assert.Equal(new Point2Mm(-10,-20),views.PlanInsertion);
            Assert.Equal("BB",views.SectionAA.NativeView);
            Assert.Equal(new Point2Mm(7,2155),views.SectionAA.Insertion);
            Assert.Equal(new Point2Mm(12,2163),views.SectionBB.Insertion);
            Assert.Equal(-1,views.SectionBB.HorizontalDirection);
            var outline=Assert.IsType<CoverSectionOutlines>(purchase.SectionOutlines);
            Assert.Equal(650+2*thickness,outline.AA.MaxX-outline.AA.MinX);
            Assert.Equal(650+2*thickness,outline.BB.MaxX-outline.BB.MinX);
            Assert.Equal(100,outline.AA.MaxY-outline.AA.MinY);
            Assert.Equal("OWNER-COVER-20261012-v2",outline.Rule.RuleVersion);
            Assert.Equal(125,blocks.SectionOutlineRule!.SideExtensionMm); // Catalog history remains immutable.
        }
    }

    [Theory]
    [InlineData(0, 32, 24, true)]
    [InlineData(90, 22, 54, true)]
    [InlineData(180, -8, 44, true)]
    [InlineData(270, 2, 14, true)]
    [InlineData(0, 32, 24, false)]
    [InlineData(90, 22, 54, false)]
    [InlineData(180, -8, 44, false)]
    [InlineData(270, 2, 14, false)]
    public void GeometryRetainsActualApertureAndTransformsItsOffCentreOrigin(double angle, double x, double y, bool round)
    {
        var seed = SyntheticTestFactory.Create();
        var i = seed.Input;
        var source = seed.Sources;
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var physical = new Dimensions2Mm(900, 900);
        var requested = new PurchasedCoverSpec("CIRCLE", new(12, 34, 2160), physical, source, angle);
        var input = new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            i.RoofOpening,i.PipeConnections,i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,
            i.PipeInvertReference,i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,
            i.FeatureRequests,i.SourceRefs,i.UnsupportedRawFields,purchasedCoverSpec:requested);
        Profile2Mm local = round ? new CircleProfile(new(20,-10),325)
            : new RectangleProfile(new Rectangle2Mm(-305,-295,345,275));
        var entry = new CoverCatalogEntry("CIRCLE",physical,new(0,0,0),angle,CoverPlacementDatum.NeckTop,
            new(-450,-450,450,450),source,new CoverOpeningContract(local,100,source));
        var catalogs = new CatalogSet(seed.Catalogs.Pipes,seed.Catalogs.Angles,seed.Catalogs.Hatches,seed.Catalogs.Aliases,
            new Dictionary<string,CoverCatalogEntry>{{entry.Code,entry}});
        var spec = new ValidatedManholeSpec(input,catalogs,policy.Version,source,[]);
        var result = NeckGeometryBuilder.Build(spec,new(650,650,150,600,NeckPosition.Center,source),
            new(NeckSeatKind.None,0,0,1550,source),policy);
        Assert.True(result.Success);
        var placement = result.Value!.PurchasedCoverPlacement!;
        var world = placement.WorldClearProfile;
        if(round)
        {
            var circle = Assert.IsType<CircleProfile>(world);
            Assert.Equal(x,circle.Center.X,6);
            Assert.Equal(y,circle.Center.Y,6);
            Assert.Equal(325,circle.RadiusMm);
        }
        else
        {
            var polygon = Assert.IsType<PolygonProfile>(world).Polygon;
            Assert.Equal(4,polygon.Vertices.Length);
            Assert.Equal(x,polygon.Vertices.Average(p=>p.X),6);
            Assert.Equal(y,polygon.Vertices.Average(p=>p.Y),6);
            Assert.Equal(650*570,polygon.SignedArea,6);
            Assert.Equal(angle is 90 or 270 ? 570 : 650,
                polygon.Vertices.Max(p=>p.X)-polygon.Vertices.Min(p=>p.X),6);
        }
    }
}
