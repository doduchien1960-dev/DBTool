using DBTool.App;
using DBTool.Domain;
using DBTool.Excel.Tests;
using DBTool.Infrastructure.Excel;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Xunit;

namespace DBTool.UI.Tests;

public sealed class ScheduleEntrypointParityTests
{
    [Fact]
    public async Task ExportConsumesTheCalculatedPackageWithoutRecomputingRowsOrHash()
    {
        string path=TestWorkbookFactory.CreateS2Synthetic(Profile.GaThamLienKhoi,book=>book.Worksheet("GaThamLienKhoi").Cell("B4").Value="G1");
        string output=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try {
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            var calculated=await new PaletteDrawingCalculation().CalculateAsync(path,Profile.GaThamLienKhoi,"GaThamLienKhoi",["G1"],hash,default);
            Assert.True(calculated.Success,string.Join(";",calculated.Diagnostics.Select(d=>d.Code)));
            var package=calculated.Value!.Manholes[0].Package;
            var exported=await SchedulePackageExport.ExportAsync(calculated.Value,output,default);
            Assert.Single(exported);
            using var book=new XLWorkbook(exported[0]);
            var rows=package.SteelSchedule!.Rows;
            Assert.Equal(rows.Length+1,book.Worksheet("Steel").LastRowUsed()!.RowNumber());
            for(int i=0;i<rows.Length;i++){
                Assert.Equal(rows[i].Mark,book.Worksheet("Steel").Cell(i+2,1).GetString());
                Assert.Equal(rows[i].Quantity,book.Worksheet("Steel").Cell(i+2,7).GetDouble());
                var raw=book.Worksheet("Metadata").RowsUsed().Single(r=>r.Cell(1).GetString()=="RawSteel/"+rows[i].Mark).Cell(2).GetString();
                using var payload=System.Text.Json.JsonDocument.Parse(raw);
                Assert.Equal(rows[i].MassKg.Value,payload.RootElement.GetProperty("MassKg").GetDouble());
                Assert.Equal(rows[i].TotalLengthMm,payload.RootElement.GetProperty("TotalLengthMm").GetDouble());
            }
            Assert.Contains(book.Worksheet("Metadata").RowsUsed(),r=>r.Cell(1).GetString()=="InputHash" && r.Cell(2).GetString()==package.InputHash);
        } finally { File.Delete(path); if(Directory.Exists(output))Directory.Delete(output,true); }
    }
}
