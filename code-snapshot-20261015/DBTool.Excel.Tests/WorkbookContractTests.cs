using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class WorkbookContractTests
{
    [Theory]
    [InlineData(Profile.GaThamLienKhoi)]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public void EachProfilePreservesBlankRowsLateRowAndSourceIdentity(Profile profile)
    {
        var path = TestWorkbookFactory.Create(profile, book =>
        {
            book.Worksheet(1).Cell("B6").Value = "Ga-é";
            book.Worksheet(1).Cell("B2005").Value = "Ga-e\u0301";
        });
        try
        {
            var result = new WorkbookImporter().Read(new WorkbookReadRequest("S9", path, profile, profile.ToString()));
            Assert.True(result.Success);
            Assert.Equal(new[] { 4, 6, 2005 }, result.Value!.Rows.Select(r => r.RowIndex));
            var cell = result.Value.Rows[2].Cells["Name"];
            Assert.Equal("B2005", cell.Source.CellAddress);
            Assert.Equal(result.Value.WorkbookHash, cell.Source.Sha256);
            Assert.Equal(profile == Profile.GaThamThuNapRoi ? 47 : 30, result.Value.ConfigCells["CoverBottom"].Number);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(Profile.GaThamLienKhoi)]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public void MissingRequestedSheetBlocksExactlyWithoutFallback(Profile profile)
    {
        var path = TestWorkbookFactory.Create(profile);
        try
        {
            var result = new WorkbookImporter().Read(new WorkbookReadRequest("S9", path, profile, "absent"));
            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Equal("SHEET_MISSING", Assert.Single(result.Diagnostics).Code);
            Assert.Equal(DiagnosticSeverity.Block, result.Diagnostics[0].Severity);
        }
        finally { File.Delete(path); }
    }
}
