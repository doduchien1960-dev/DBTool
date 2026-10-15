using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class HaunchCompositionTests
{
    [Theory]
    [InlineData(0,150)]
    [InlineData(150,0)]
    [InlineData(150,150)]
    public void WorkbookHaunchesComposeOnceWithIndependentProposalAndStayUncertified(int baseSize,int roofSize)
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("BaseHaunch",ManholeValidatorTests.Cell(baseSize,"D5","CF.Ga"))
            .SetItem("TopHaunch",ManholeValidatorTests.Cell(roofSize,"C9","CF.Ga"));
        var cells=raw.Rows[0].Cells.SetItem("PipeCodeT1",ManholeValidatorTests.Cell(null,"O4"))
            .SetItem("InvertT1",ManholeValidatorTests.Cell(null,"P4"));
        raw=new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[new(4,cells)],config,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),raw.Diagnostics);
        var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var request=new CalculationRequest(raw.ProjectId,[ManholeValidatorTests.Id],CalculationScope.FullRebar,policy.PolicyId,true);
        var validated=new ManholeValidator(new NumericCellReader(),new("en-US",false,false))
            .Validate(raw,ManholeValidatorTests.Catalogs(),request,policy);
        Assert.True(validated.Success,string.Join(";",validated.Diagnostics.Select(d=>d.Code)));
        var spec=Assert.Single(validated.Value!.SelectedManholes);
        var geometry=new GeometryStageAdapter().Build(spec,policy);
        Assert.True(geometry.Success,string.Join(";",geometry.Diagnostics.Select(d=>d.Code)));
        var result=new FullRebarStage().Plan(spec,geometry.Value!,policy);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var bars=result.Value!.Occurrences.Where(b=>b.FamilyId==HaunchRebarPlanner.FamilyId).ToArray();
        Assert.NotEmpty(bars);
        Assert.Equal(bars.Length,bars.Select(b=>b.Id).Distinct().Count());
        Assert.Equal(baseSize>0,bars.Any(b=>b.ComponentKind==ComponentKind.Base));
        Assert.Equal(roofSize>0,bars.Any(b=>b.ComponentKind==ComponentKind.Roof));
        Assert.Equal(LayoutStatus.Partial,result.Value.Status);
        Assert.All(bars,b=>
        {
            Assert.Equal(LengthStatus.NeedsRule,b.LengthStatus);
            Assert.Null(b.FabricationLengthMm);
            Assert.Equal(HaunchRebarPlanner.RuleId,b.RuleId);
            Assert.Equal(b.Id.Contains("/Dist/")?8:10,b.DiameterMm);
            Assert.Equal(b.Id.Contains("/Dist/")?1:3,b.Path.Segments.Length);
        });
    }
}
