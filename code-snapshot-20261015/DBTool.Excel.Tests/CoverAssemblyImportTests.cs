using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class CoverAssemblyImportTests
{
    [Fact]
    public void AllowedCachedCatalogDimensionRetainsExplicitEntryAndReview()
    {
        var path = TestWorkbookFactory.CopySource(2, b => TestWorkbookFactory.DeclareSyntheticCover(b));
        try
        {
            var raw = new WorkbookImporter().Read(new("Assembly", path, Profile.GaThamNapRoi, "GaThamNapRoi")).Value!;
            var rows = raw.CatalogCells["DBTool.Covers"].ToArray();
            var source = rows[0].Cells["B"].Source;
            rows[0] = new(rows[0].RowIndex, rows[0].Cells.SetItem("B",
                new(RawCellKind.Formula, null, null, null, "500+550", "1050", source, RawCellKind.Number)));
            var catalogs = raw.CatalogCells.ToDictionary(p => p.Key, p => (IEnumerable<RawManholeRow>)p.Value);
            catalogs["DBTool.Covers"] = rows;
            raw = new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows, raw.ConfigCells, catalogs, []);
            var result = new CatalogReader().Read(raw, new NumericPolicy("en-US", false, true));
            Assert.True(result.Success);
            Assert.True(result.Value!.Covers.ContainsKey("TEST.COVER"));
            Assert.NotNull(result.Value.Covers["TEST.COVER"].OpeningContract);
            Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_CACHE_UNVERIFIED" && d.Source?.CellAddress == "B4");
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitCatalogApertureFlowsThroughImporterOrBlocksPartialMetadata(bool partial)
    {
        var path = TestWorkbookFactory.CopySource(2, book =>
        {
            TestWorkbookFactory.DeclareSyntheticCover(book);
            if (partial) book.Worksheet("DBTool.Covers").Cell("R4").Clear();
        });
        try
        {
            var raw = new WorkbookImporter().Read(new("Assembly", path, Profile.GaThamNapRoi, "GaThamNapRoi"));
            Assert.True(raw.Success);
            var policy = new NumericPolicy("en-US", false, false);
            var catalog = new CatalogReader().Read(raw.Value!, policy);
            if (partial)
            {
                Assert.False(catalog.Success);
                Assert.Contains(catalog.Diagnostics, d => d.Code == "COVER_ASSEMBLY_FIELD_REQUIRED" &&
                    d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "R4");
                return;
            }
            Assert.True(catalog.Success);
            var entry = catalog.Value!.Covers["TEST.COVER"];
            Assert.Equal(820, entry.OpeningContract!.ClearOpening.MaxX - entry.OpeningContract.ClearOpening.MinX);
            Assert.Equal(570, entry.OpeningContract.ClearOpening.MaxY - entry.OpeningContract.ClearOpening.MinY);
            Assert.Equal(1050, entry.PhysicalSize.Width);
            Assert.Contains(entry.OpeningContract.Sources, s => s.SheetName == "DBTool.Covers" && s.CellAddress == "N4");
            var result = new ManholeValidator(new NumericCellReader(), policy).Validate(raw.Value!, catalog.Value,
                new("Assembly", [ManholeIdentity.Create("Assembly", raw.Value!.Profile, "G400-1")], CalculationScope.FullRebar, "S2Rebar-1", true),
                CalculationPolicy.Select("S2Rebar-1", true).Value!);
            Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
            Assert.Contains(result.Value!.SelectedManholes[0].Input.PurchasedCoverSpec!.Sources, s => s.CellAddress == "N4");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WrongNeckSizeBlocksAtCoverAndIdentifiesNeckCell()
    {
        var path = TestWorkbookFactory.CopySource(2, book =>
        {
            TestWorkbookFactory.DeclareSyntheticCover(book);
            book.Worksheet("CF.Ga").Cell("E11").Value = "800x800";
        });
        try
        {
            var raw = new WorkbookImporter().Read(new("Assembly", path, Profile.GaThamNapRoi, "GaThamNapRoi")).Value!;
            var policy = new NumericPolicy("en-US", false, false);
            var catalog = new CatalogReader().Read(raw, policy).Value!;
            var result = new ManholeValidator(new NumericCellReader(), policy).Validate(raw, catalog,
                new("Assembly", [ManholeIdentity.Create("Assembly", raw.Profile, "G400-1")], CalculationScope.FullRebar, "S2Rebar-1", true),
                CalculationPolicy.Select("S2Rebar-1", true).Value!);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "COVER_OPENING_INCOMPATIBLE" &&
                d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "E13" && d.Message.Contains("E11"));
        }
        finally { File.Delete(path); }
    }
}
