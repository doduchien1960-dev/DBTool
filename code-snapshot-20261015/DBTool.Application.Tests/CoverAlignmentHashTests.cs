using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class CoverAlignmentHashTests
{
    [Fact]
    public void OwnerOutlineRuleSurvivesValidationAndActualChamberGeometryWithoutCreatingConcrete()
    {
        var validated=Validate(12,outlineExtension:125);
        Assert.True(validated.Success);
        var geometry=ChamberGeometryBuilder.Build(validated.Value!.SelectedManholes[0],CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(geometry.Success);
        var purchase=Assert.Single(geometry.Value!.PurchasedCovers);
        Assert.NotNull(purchase.SectionOutlines);
        // Current request uses 700 clear + 2*150 actual neck thickness;
        // the catalog's historical +125 rule remains available as provenance.
        Assert.Equal(1000,purchase.SectionOutlines.AA.MaxX-purchase.SectionOutlines.AA.MinX);
        Assert.Equal(100,purchase.SectionOutlines.AA.MaxY-purchase.SectionOutlines.AA.MinY);
        Assert.Equal(purchase.Anchor.Z,purchase.SectionOutlines.AA.MaxY);
        Assert.DoesNotContain(geometry.Value.Components,c=>c.Id.Contains("Outline",StringComparison.Ordinal));
    }
    [Fact]
    public void ChangingOnlySectionOutlineDimensionsInvalidatesValidatedHash()
    {
        var first=Validate(12,outlineExtension:125);
        var changed=Validate(12,outlineExtension:126);
        Assert.True(first.Success);Assert.True(changed.Success);
        Assert.NotEqual(first.Value!.InputHash,changed.Value!.InputHash);
    }
    [Fact]
    public void OverflowingRoofSupportDimensionsProduceADiagnosticInsteadOfThrowing()
    {
        var result=Validate(12,null,0,60,double.MaxValue,double.MaxValue);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_SUPPORT_GEOMETRY_OVERFLOW" && d.Severity==DiagnosticSeverity.Block);
    }
    [Fact]
    public void AValidatedRoofMountedCoverSurvivesTheActualChamberGeometryPipeline()
    {
        var result=Validate(12,null,0,60);
        Assert.True(result.Success);
        var geometry=ChamberGeometryBuilder.Build(result.Value!.SelectedManholes[0],CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(geometry.Success,string.Join(";",geometry.Diagnostics.Select(d=>d.Code)));
        var purchase=Assert.Single(geometry.Value!.PurchasedCovers);
        Assert.Equal(result.Value.SelectedManholes[0].Input.PurchasedCoverSpec!.Anchor,purchase.Anchor);
        Assert.NotNull(purchase.NativeViewPlacement);
        Assert.DoesNotContain(geometry.Value.Components,c=>c.Kind==ComponentKind.Neck);
    }
    [Theory]
    [InlineData(60,true)]
    [InlineData(70,false)]
    public void NativeFrameOnRoofUsesActualRoofSupportAndChecksItsContactElevation(double capHeight,bool success)
    {
        var result=Validate(12,null,0,capHeight);
        Assert.Equal(success,result.Success);
        if(!success) Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_CONTACT_ELEVATION_INCOMPATIBLE" && d.Severity==DiagnosticSeverity.Block);
    }
    [Fact]
    public void ChangingMeasuredContactGeometryInvalidatesHashEvenWhenFrameBoundsAreUnchanged()
    {
        var first=Validate(12,50);var changed=Validate(12,100);
        Assert.True(first.Success,string.Join(";",first.Diagnostics.Select(d=>d.Code)));
        Assert.True(changed.Success,string.Join(";",changed.Diagnostics.Select(d=>d.Code)));
        Assert.NotEqual(first.Value!.InputHash,changed.Value!.InputHash);
    }
    [Fact]
    public void ChangingOnlyTheNativeSectionReferenceInvalidatesTheValidatedInputHash()
    {
        var first=Validate(12);var changed=Validate(22);
        Assert.True(first.Success,string.Join(";",first.Diagnostics.Select(d=>d.Code)));
        Assert.True(changed.Success,string.Join(";",changed.Diagnostics.Select(d=>d.Code)));
        Assert.Equal(first.Value!.SelectedManholes[0].Input.PurchasedCoverSpec!.Anchor,
            changed.Value!.SelectedManholes[0].Input.PurchasedCoverSpec!.Anchor);
        Assert.NotEqual(first.Value.InputHash,changed.Value.InputHash);
    }
    private static OperationResult<ValidatedProjectInput> Validate(double sectionReferenceX,double? contactWidth=null,double neckHeight=600,double capHeight=60,
        double length=1000,double wallThickness=150,double? outlineExtension=null)
    {
        var raw=ManholeValidatorTests.Raw("NeckHeight",neckHeight);
        var config=raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Gang","C13","CF.Ga"))
            .SetItem("CoverBlock",ManholeValidatorTests.Cell("FIX","E13","CF.Ga"))
            .SetItem("CoverHeight",ManholeValidatorTests.Cell(capHeight,"D13","CF.Ga"));
        var source=ManholeValidatorTests.Cell("fixture","B4","DBTool.Covers").Source;
        var bounds=new Rectangle2Mm(-500,-500,500,500);
        var blocks=new CoverBlockSet([
            new("FIX.MB","MB",new(0,0,0),bounds,new(0,0),[source]),
            new("FIX.AA","AA",new(0,0,0),bounds,new(sectionReferenceX,-3),[source]),
            new("FIX.BB","BB",new(0,0,0),bounds,new(-7,5),[source])],null,[],[source],
            new[]{"AA","BB"}.Select(view=>new CoverContactSection(view,[
                new(new(-500,(view=="AA"?-3:5)-60),new(-500+(contactWidth ?? 150),(view=="AA"?-3:5)-60)),
                new(new(500-(contactWidth ?? 150),(view=="AA"?-3:5)-60),new(500,(view=="AA"?-3:5)-60))],[source])),
            outlineExtension.HasValue?new CoverSectionOutlineRule(outlineExtension.Value,80,[source]):null);
        var entry=new CoverCatalogEntry("FIX",new(1000,1000),new(0,0,0),0,CoverPlacementDatum.NeckTop,bounds,
            [source],new CoverOpeningContract(new Rectangle2Mm(-350,-350,350,350),contactWidth.HasValue?25:100,[source]),blocks);
        var seed=ManholeValidatorTests.Catalogs();
        var catalogs=new CatalogSet(seed.Pipes,seed.Angles,seed.Hatches,seed.Aliases,
            new Dictionary<string,CoverCatalogEntry>{{entry.Code,entry}});
        return new ManholeValidator(new NumericCellReader(),new("en-US",false,false)).Validate(
            new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,
                raw.Rows.Select(row=>new RawManholeRow(row.RowIndex,row.Cells.SetItem("NeckThickness",ManholeValidatorTests.Cell(150,"X4"))
                    .SetItem("Length",ManholeValidatorTests.Cell(length,"C4"))
                    .SetItem("WallThickness",ManholeValidatorTests.Cell(wallThickness,"I4")))),config,
                new Dictionary<string,IEnumerable<RawManholeRow>>(),[]),catalogs,
            new(raw.ProjectId,[ManholeValidatorTests.Id],CalculationScope.GeometryOnly,"S2Geometry-1",true),
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
    }
}
