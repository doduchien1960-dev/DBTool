using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.App;
using DBTool.Domain;
using DBTool.Excel.Tests;
using Xunit;

namespace DBTool.UI.Tests;

public sealed class PaletteDrawingCalculationTests
{
    [Fact]
    public void FailurePresentationSeparatesReviewAndReportsSeverityScopeGaAndCell()
    {
        var source=new SourceRef(SourceKind.WorkbookCell,"copy.xlsx",new string('A',64),"CF.Ga","F11",null,"Có");
        var diagnostics=new[]
        {
            new Diagnostic("U_NECK_SOURCE_MAPPING",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,source,null,null,"Unresolved seat"),
            new Diagnostic("S2_FEATURE_DEFERRED",DiagnosticSeverity.Review,DiagnosticScope.Manhole,"Palette/GaThamNapRoi/G400-1",null,source,null,null,"Pending FullRebar")
        };
        var report=PaletteDrawingCalculation.FormatDiagnostics(diagnostics);
        Assert.Contains("[Block][Global]",report.Blocking);
        Assert.Contains("CF.Ga!F11",report.Blocking);
        Assert.DoesNotContain("S2_FEATURE_DEFERRED",report.Blocking);
        Assert.Contains("[Review][Manhole]",report.Review);
        Assert.Contains("G400-1",report.Review);
        Assert.Contains("CF.Ga!F11",report.Review);
        Assert.Contains("FullRebar",report.Review);
    }

    [Fact]
    public async Task Each_checked_row_gets_its_own_immutable_package_from_the_same_snapshot()
    {
        string path = TwoRows();
        try
        {
            var result = await new PaletteDrawingCalculation().CalculateAsync(path, Profile.GaThamLienKhoi,
                "GaThamLienKhoi", ["G1", "G2"], Hash(path), default);
            Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            Assert.Equal(2, result.Value!.Manholes.Count);
            Assert.All(result.Value.Manholes, m => Assert.Equal(Hash(path), m.Package.SourceWorkbookHash, ignoreCase: true));
            Assert.All(result.Value.Manholes, m => Assert.NotNull(m.Package.DrawingPackage));
            Assert.NotEqual(result.Value.Manholes[0].Package.InputHash, result.Value.Manholes[1].Package.InputHash);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Missing_selected_row_returns_no_batch_for_the_writer()
    {
        string path = TwoRows();
        try
        {
            var result = await new PaletteDrawingCalculation().CalculateAsync(path, Profile.GaThamLienKhoi,
                "GaThamLienKhoi", ["G1", "Missing"], Hash(path), default);
            Assert.False(result.Success); Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, d => d.Code == "MANHOLE_NOT_FOUND");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Changed_workbook_bytes_require_reloading_before_drawing()
    {
        string path = TwoRows();
        try
        {
            var result = await new PaletteDrawingCalculation().CalculateAsync(path, Profile.GaThamLienKhoi,
                "GaThamLienKhoi", ["G1"], new string('0', 64), default);
            Assert.False(result.Success); Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, d => d.Code == "SOURCE_SNAPSHOT_MISMATCH");
        }
        finally { File.Delete(path); }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string TwoRows() => TestWorkbookFactory.CreateS2Synthetic(Profile.GaThamLienKhoi, book =>
    {
        var sheet = book.Worksheet("GaThamLienKhoi");
        sheet.Cell("B4").Value = "G1";
        sheet.Range("A4:AB4").CopyTo(sheet.Cell("A5"));
        sheet.Cell("B5").Value = "G2";
    });
}
