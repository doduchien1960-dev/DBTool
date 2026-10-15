using ClosedXML.Excel;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;
using System.IO.Compression;
using System.Xml.Linq;

namespace DBTool.Excel.Tests;

public sealed class TypedFormulaCacheTests
{
    [Theory]
    [InlineData("1E+20", 1e20)]
    [InlineData("1E-08", 1e-8)]
    public void TypedNumericCacheIsNotRejectedAsFormattedUserText(string cache, double expected)
    {
        var source = new SourceRef(SourceKind.SyntheticFixture, "typed-cache", new string('a',64), null, null, null, cache);
        var cell = new RawCell(RawCellKind.Formula, null, null, null, "test()", cache, source, RawCellKind.Number);
        var result = new NumericCellReader().ReadNumber(cell, new NumericPolicy("vi-VN", false, true));
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value!.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_CACHE_UNVERIFIED");
    }

    [Theory]
    [InlineData("TRUE", "Có")]
    [InlineData("FALSE", "Không")]
    public void TypedBooleanCacheRetainsFlagMeaning(string cache, string expected)
    {
        var source = new SourceRef(SourceKind.SyntheticFixture, "typed-cache", new string('a',64), null, null, null, cache);
        var cell = new RawCell(RawCellKind.Formula, null, null, null, "test()", cache, source, RawCellKind.Boolean);
        var result = RawCellReader.ReadText(cell, new NumericPolicy("vi-VN", false, true));
        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_CACHE_UNVERIFIED");
    }

    [Fact]
    public void ImportedNumericFormulaRetainsValueAcrossInputCulture()
    {
        var path = TestWorkbookFactory.CopySource(2, book =>
        {
            book.Worksheet("CF.Ga").Cell("E9").FormulaA1 = "1001/2";
        });
        try
        {
            // Store the actual XLSX cached numeric payload independently of ClosedXML recalculation.
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                XDocument Load(string name) { using var stream = zip.GetEntry(name)!.Open(); return XDocument.Load(stream); }
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var sheet = Load("xl/workbook.xml").Descendants(ns + "sheet").Single(x => (string?)x.Attribute("name") == "CF.Ga");
                var link = Load("xl/_rels/workbook.xml.rels").Root!.Elements().Single(x => (string?)x.Attribute("Id") == (string?)sheet.Attribute(rel + "id"));
                string target = (string)link.Attribute("Target")!;
                string entryName = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                var xml = Load(entryName);
                var xmlCell = xml.Descendants(ns + "c").Single(x => (string?)x.Attribute("r") == "E9");
                xmlCell.SetAttributeValue("t", null);
                xmlCell.Element(ns + "v")?.Remove();
                xmlCell.Add(new XElement(ns + "v", "500.5"));
                zip.GetEntry(entryName)!.Delete();
                using var output = zip.CreateEntry(entryName).Open();
                xml.Save(output);
            }
            var raw = new WorkbookImporter().Read(new("Typed", path, Profile.GaThamNapRoi, "GaThamNapRoi"));
            Assert.True(raw.Success);
            var cell = raw.Value!.ConfigCells["DowelLength"];
            Assert.Equal(RawCellKind.Formula, cell.Kind);
            var result = new NumericCellReader().ReadNumber(cell, new NumericPolicy("vi-VN", false, true));
            Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
            Assert.Equal(500.5, result.Value!.Value);
            Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_CACHE_UNVERIFIED");
        }
        finally { File.Delete(path); }
    }
}
