using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;
namespace DBTool.Excel.Tests;
public class ValidatorIntegrationTests
{
    [Fact] public void ActualImporterCatalogParserAndValidatorAcceptS1Workbook()
    {
        string path=TestWorkbookFactory.CreateS1FromJson();
        try
        {
            var numeric=new NumericPolicy("en-US",false,false);
            var raw=new WorkbookImporter().Read(new("S1",path,Profile.GaThamLienKhoi,"GaThamLienKhoi"));
            Assert.True(raw.Success);
            var catalogs=new CatalogReader().Read(raw.Value!,numeric); Assert.True(catalogs.Success);
            var result=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw.Value!,catalogs.Value!,
                new("S1",["S1/GaThamLienKhoi/S1-MONO-001"],CalculationScope.BaseRebarOnly,"S1-Analytic-1",true),CalculationPolicy.Select("S1-Analytic-1",true).Value!);
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code+":"+d.Source?.CellAddress)));
            var input=Assert.Single(result.Value!.SelectedManholes).Input;
            Assert.Equal(1000,input.L); Assert.Equal(1250,input.B); Assert.Equal(1400,input.H);
            Assert.Equal(10,input.BaseBottomMesh.DiameterMm); Assert.Equal(150,input.BaseBottomMesh.SpacingMm);
            Assert.Equal(300,Assert.Single(input.PipeConnections).InvertMm); Assert.Equal(10,input.BaseElevationM!.Value.Value);
        }
        finally { File.Delete(path); }
    }
}
