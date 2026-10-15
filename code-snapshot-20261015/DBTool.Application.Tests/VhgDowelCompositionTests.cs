using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class VhgDowelCompositionTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;
    private static ValidatedManholeSpec Spec(Profile profile = Profile.GaThamNapRoi, string feature = "Dowels")
    {
        var raw=ManholeValidatorTests.Raw();
        var cells=raw.Rows[0].Cells.SetItem("PipeCodeT1",ManholeValidatorTests.Cell(null,"O4"))
            .SetItem("InvertT1",ManholeValidatorTests.Cell(null,"P4"))
            .SetItem("WallOuterMesh",ManholeValidatorTests.Cell("D10A150","J4"));
        var config=raw.ConfigCells;
        foreach(var (key,value) in new (string,object)[]{("HasDowel","Có"),("DowelSpec","D18A500"),
            ("DowelLength",500),("DowelHoleDia",25),("CoverWall",25),("WallSpacingDelta",0),
            ("WallDiaStepDown",0),("HookPattern.E","0M"),("BeddingMortar",15)})
            config=config.SetItem(key,ManholeValidatorTests.Cell(value,"synthetic","CF.Ga"));
        var source=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,profile,profile.ToString(),[new RawManholeRow(4,cells)],
            config,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        var request=new CalculationRequest(raw.ProjectId,[$"S1/{profile}/S1-MONO-001"],CalculationScope.FullRebar,Policy.PolicyId,true);
        var validated=new ManholeValidator(new NumericCellReader(),new("en-US",false,false))
            .Validate(source,ManholeValidatorTests.Catalogs(),request,Policy);
        Assert.True(validated.Success,string.Join(";",validated.Diagnostics.Select(d=>d.Code)));
        return Assert.Single(validated.Value!.SelectedManholes);
    }

    [Fact]
    public void SupportedDowelsAreComposedOnceAndScheduledAsTenStraightBars()
    {
        var spec=Spec();
        var capability=new FeatureSupportValidator().ValidateS2Rebar(spec,CalculationScope.FullRebar,Policy);
        Assert.True(capability.Success,string.Join(";",capability.Diagnostics.Select(d=>d.Code)));
        var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var result=new FullRebarStage().Plan(spec,geometry,Policy);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var dowels=result.Value!.Occurrences.Where(b=>b.FamilyId=="Dowel").ToArray();
        Assert.Equal(10,dowels.Length);
        Assert.All(dowels,b=>{
            var line=Assert.IsType<LineSegment3Mm>(Assert.Single(b.Path.Segments));
            Assert.Equal(1550,line.Start.Z); Assert.Equal(1050,line.End.Z);
            Assert.Equal(500,b.ScheduleLengthMm); Assert.Equal(500,b.FabricationLengthMm);
        });
        var schedule=SteelScheduleCalculator.Calculate(dowels,Policy.MassPolicy,new MarkPolicy(CalculationScope.FullRebar));
        Assert.True(schedule.Success);
        Assert.Equal(5000,dowels.Sum(b=>b.ScheduleLengthMm));
    }

    [Theory]
    [InlineData(Profile.GaThamLienKhoi,"Dowels")]
    public void UnverifiedConsumersRemainBlocked(Profile profile,string feature)
    {
        var result=new FeatureSupportValidator().ValidateS2Rebar(Spec(profile,feature),CalculationScope.FullRebar,Policy);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="FEATURE_UNSUPPORTED" && d.Actual==feature);
    }
}
