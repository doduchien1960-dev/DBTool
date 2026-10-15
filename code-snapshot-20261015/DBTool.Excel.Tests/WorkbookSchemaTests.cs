using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public class WorkbookSchemaTests
{
    private static readonly WorkbookImporter Importer = new();
    private static OperationResult<RawProjectInput> Read(string path, Profile profile = Profile.GaThamLienKhoi, string? sheet = null) =>
        Importer.Read(new WorkbookReadRequest("S1", path, profile, sheet ?? profile.ToString()));

    [Fact] public void MissingRequestedSheetHasNoFallbackOrPayload()
    {
        string path = TestWorkbookFactory.Create();
        var result = Read(path, sheet: "OtherSheet");
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "SHEET_MISSING" && d.Severity == DiagnosticSeverity.Block);
    }

    [Fact] public void BlankMiddleRowDoesNotEndScanAndRow2001IsRead()
    {
        string path = TestWorkbookFactory.Create(edit: w => { w.Worksheet(1).Cell("B6").Value = "G400-2"; w.Worksheet(1).Cell("B2001").Value = "G400-3"; });
        var result = Read(path);
        Assert.True(result.Success);
        Assert.Equal(new[] { 4, 6, 2001 }, result.Value!.Rows.Select(r => r.RowIndex));
    }

    [Fact] public void MissingConfigSheetBlocks()
    {
        string path = TestWorkbookFactory.Create(edit: w => w.Worksheet("CF.Ga").Delete());
        var result = Read(path);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "SHEET_MISSING");
    }

    [Fact] public void FormulaBlankAndNumericZeroStayDistinct()
    {
        string path = TestWorkbookFactory.Create(edit: w => w.Worksheet(1).Cell("C4").FormulaA1 = "500+500");
        var row = Assert.Single(Read(path).Value!.Rows);
        Assert.Equal(RawCellKind.Formula, row.Cells["Length"].Kind);
        Assert.Equal("500+500", row.Cells["Length"].Formula);
        Assert.Equal(RawCellKind.Blank, row.Cells["InvertT2"].Kind);
        Assert.Equal(RawCellKind.Number, row.Cells["NeckHeight"].Kind);
        Assert.Equal(0, row.Cells["NeckHeight"].Number);
        Assert.Equal(RawCellKind.Number, row.Cells["InvertT1"].Kind);
        Assert.Equal(300, row.Cells["InvertT1"].Number);
    }

    [Theory]
    [InlineData(Profile.GaThamLienKhoi, 30)]
    [InlineData(Profile.GaThamThuNapRoi, 47)]
    public void ProtectRowShiftOnlyForType3(Profile profile, double expected)
    {
        string path = TestWorkbookFactory.Create(profile);
        var result = Read(path, profile);
        Assert.Equal(expected, result.Value!.ConfigCells["CoverBottom"].Number);
        int row = profile == Profile.GaThamThuNapRoi ? 19 : 17;
        Assert.Equal($"F{row}",result.Value.ConfigCells["Protect.F"].Source.CellAddress);
    }

    [Theory]
    [InlineData(1,Profile.GaThamLienKhoi,30)]
    [InlineData(3,Profile.GaThamThuNapRoi,47)]
    public void SourceCopySentinelDoesNotShiftType3Twice(int sourceNumber,Profile profile,double expected)
    {
        string path=TestWorkbookFactory.CopySource(sourceNumber,w=>
        {
            w.Worksheet("CF.Ga").Cell("C17").Value=30;
            w.Worksheet("CF.Ga").Cell("C19").Value=47;
        });
        Assert.Equal(expected,Read(path,profile).Value!.ConfigCells["CoverBottom"].Number);
    }

    [Fact] public void BlankInvertDiffersFromNumericZero()
    {
        string blankPath=TestWorkbookFactory.Create(edit:w=>w.Worksheet(1).Cell("P4").Clear());
        Assert.Equal(RawCellKind.Blank,Assert.Single(Read(blankPath).Value!.Rows).Cells["InvertT1"].Kind);
        string zeroPath=TestWorkbookFactory.Create(edit:w=>w.Worksheet(1).Cell("P4").Value=0);
        var zero=Assert.Single(Read(zeroPath).Value!.Rows).Cells["InvertT1"];
        Assert.Equal(RawCellKind.Number,zero.Kind); Assert.Equal(0,zero.Number);
    }

    [Fact] public void NameMissingOnPopulatedRowBlocks()
    {
        string path = TestWorkbookFactory.Create(edit: w => w.Worksheet(1).Cell("C6").Value = 1000);
        var result = Read(path);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "ROW_NAME_MISSING" && d.Source?.CellAddress == "B6");
    }

    [Fact] public void CorruptWorkbookReturnsBlockResult()
    {
        string path=Path.Combine(Path.GetTempPath(),$"dbtool-corrupt-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path,"not an XLSX archive");
        var result=Read(path);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="WORKBOOK_READ_ERROR" && d.Severity==DiagnosticSeverity.Block);
    }

    [Theory]
    [InlineData(1, Profile.GaThamLienKhoi, 1000, 1000, 1200, 0,"50a4cbf80521995c0ac8ed4a8fcf870d3b545aa13eeabf4334d3abba8bc30ead")]
    [InlineData(2, Profile.GaThamNapRoi, 1000, 1100, 1200, 400,"d0917949603848209f5b14d3cabd3204ecaa72b9358d16fffb0a437d9a7843f1")]
    [InlineData(3, Profile.GaThamThuNapRoi, 1000, 1100, 1200, 500,"1dbc939979546c55eb0981e00fb90fc471ecc28cde931b50d41d1b1e37fc502b")]
    public void ReadsVerifiedSourceWorkbookCells(int sourceNumber, Profile profile,
        double length, double width, double height, double neck,string expectedHash)
    {
        string source = TestWorkbookFactory.CopySource(sourceNumber);
        string actualHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        Assert.Equal(expectedHash,actualHash);
        var result = Read(source,profile);
        Assert.True(result.Success);
        var row = result.Value!.Rows.Single(r => r.RowIndex == 4);
        Assert.Equal(length,row.Cells["Length"].Number);
        Assert.Equal(width,row.Cells["Width"].Number);
        Assert.Equal(height,row.Cells["Height"].Number);
        Assert.Equal(neck,row.Cells["NeckHeight"].Number);
        Assert.Equal($"{profile}!",row.Cells["Width"].Source.SheetName+"!");
    }

    [Fact] public void SyntheticS1WorkbookUsesJsonAndExplicitDisabledCells()
    {
        string path = TestWorkbookFactory.CreateS1FromJson();
        var result = Read(path);
        Assert.True(result.Success);
        var row = Assert.Single(result.Value!.Rows);
        Assert.Equal(1250,row.Cells["Width"].Number);
        Assert.Equal(10,row.Cells["BaseElevationM"].Number);
        Assert.Equal("D400",row.Cells["PipeCodeT1"].Text);
        Assert.Equal(RawCellKind.Blank,row.Cells["PipeCodeT2"].Kind);
        Assert.Equal(0,result.Value.ConfigCells["BaseHaunch"].Number);
        Assert.Equal("Chia đều",result.Value.ConfigCells["Distribution"].Text);
        var catalogs = new CatalogReader().Read(result.Value,new NumericPolicy("en-US",false,false));
        Assert.True(catalogs.Success);
        Assert.Equal(400,catalogs.Value!.Pipes["D400"].InnerDiameterMm);
        Assert.Equal(55,catalogs.Value.Pipes["D400"].WallThicknessMm);
        string artifact = Path.Combine(TestWorkbookFactory.WorkspaceRoot(),"DBTool","TestOutput","T5","s1-mono-001.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
        File.Copy(path,artifact,true);
    }
}
