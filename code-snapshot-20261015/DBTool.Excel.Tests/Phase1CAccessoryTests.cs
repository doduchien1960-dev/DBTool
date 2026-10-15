using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;
namespace DBTool.Excel.Tests;
public sealed class Phase1CAccessoryTests
{
    [Fact]
    public async Task SourceAnglesContributeAllThreeVerifiedPositionsToPackage()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>{
            TestWorkbookFactory.DeclareSyntheticCover(b);
            var c=b.Worksheet("CF.Ga");
            foreach(var cell in new[]{"C9","C25"}) c.Cell(cell).Value="Không";
            c.Cell("H9").Value="0D12"; c.Cell("J11").Value="0";c.Cell("H23").Value="0";
            c.Cell("D28").Value=0;c.Cell("F28").Value=0;c.Cell("C30").Value=0;c.Cell("E30").Value=0;
        });
        try {
            var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
            var use=new CalculateManholeUseCase(new WorkbookImporter(),new CatalogReader(),new ManholeValidator(new NumericCellReader(),new("en-US",false,false)),new("en-US",false,false),
                new CalculationPipeline(new GeometryStageAdapter(),new FullRebarStage(),new SteelScheduleStageAdapter(),new DrawingStageAdapter()));
            var result=await use.CalculateAsync(new("1C",path,Profile.GaThamNapRoi,"GaThamNapRoi"),
                new("1C",[ManholeIdentity.Create("1C",Profile.GaThamNapRoi,"G400-1")],CalculationScope.FullRebar,policy.PolicyId,true),new(30,ElevationMode.Symbolic,null,"Default",false),default);
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code+":"+d.Actual)));
            Assert.NotNull(result.Value!.AngleSchedule);
            Assert.Equal(new[]{2780d,3380d,3780d},result.Value.AngleSchedule!.Rows.Select(r=>r.SingleLengthMm).Order());
            Assert.Equal(3,result.Value.AngleSchedule.Rows.Sum(r=>r.Quantity));
            Assert.Equal(9940,result.Value.AngleSchedule.Rows.Sum(r=>r.TotalLengthMm),6);
        } finally {File.Delete(path);}
    }
}
