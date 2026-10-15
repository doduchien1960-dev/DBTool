using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class UnifiedWorkbookTests
{
    [Fact]
    public void UnifiedWorkbookKeepsTwoHundredRowsAndGroupsByTypedIdentity()
    {
        using var output = new TemporaryOutput();
        var package = PackageWithSteel(200);

        var result = new UnifiedWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "unified", "run1"));

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Path);
        var steel = book.Worksheet("Steel");
        Assert.Equal(201, steel.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(0.5, steel.Cell(201, 11).GetDouble(), 12);
        Assert.Equal(100, Enumerable.Range(2, 200).Sum(row => steel.Cell(row, 11).GetDouble()), 12);
        Assert.Contains("$K$2:$K$201", book.Worksheet("Summary").Cell("C2").FormulaA1, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, book.Worksheet("Summary").RowsUsed().Count() - 1);
        Assert.Equal("Base", book.Worksheet("Summary").Cell("A2").GetString());
        Assert.Equal("Concrete", book.Worksheet("Summary").Cell("B2").GetString());
    }

    [Fact]
    public void UnifiedDisplayRoundingIsAwayFromZeroAndRawValuesRemainExact()
    {
        using var output = new TemporaryOutput();
        var package = PackageWithSteel(2, lengthMm: 1, unitMassKgPerM: 0.4);

        var result = new UnifiedWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "display", "run1"));

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Path);
        var steel = book.Worksheet("Steel");
        Assert.Equal(0.0004, steel.Cell(2, 11).GetDouble(), 12);
        Assert.Equal(0.0004, steel.Cell(3, 11).GetDouble(), 12);
        Assert.Equal(0, steel.Cell(2, 14).GetDouble(), 12);
        Assert.Equal(0, steel.Cell(3, 14).GetDouble(), 12);
        Assert.Equal(0.001, book.Worksheet("Summary").Cell(2, 5).GetDouble(), 12);
    }

    [Fact]
    public void AngleScheduleIsSeparateAndCombinedTotalUsesRawAngleMass()
    {
        using var output = new TemporaryOutput();
        var package = PackageWithSteel(1, lengthMm: 844.4444444444444, angleMassKg: 7);

        var result = new UnifiedWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "angle", "run1"));

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Path);
        Assert.Equal("CatalogCode", book.Worksheet("AngleSteel").Cell(1, 2).GetString());
        Assert.DoesNotContain("DiameterMm", book.Worksheet("AngleSteel").Row(1).Cells().Select(c => c.GetString()));
        Assert.Equal(7.4222222222222225, book.Worksheet("Summary").Cell(2, 6).GetDouble(), 12);
        Assert.DoesNotContain(book.Worksheet("Steel").RowsUsed().SelectMany(row => row.Cells()).Select(cell => cell.GetString()), value => value == "DUMMY");
    }

    [Fact]
    public void NotCalculatedStatusesAreExplicitAndNotSerializedAsZeroData()
    {
        using var output = new TemporaryOutput();
        var package = PackageWithSteel(1, notCalculated: true);

        var result = new UnifiedWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "status", "run1"));

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Path);
        Assert.Equal("NotCalculated — chưa tính / không data", Metadata(book.Worksheet("Metadata"), "ConcreteStatus"));
        Assert.Equal("NotCalculated — chưa tính / không data", Metadata(book.Worksheet("Metadata"), "FormworkStatus"));
        Assert.DoesNotContain(book.Worksheet("Summary").RowsUsed().SelectMany(row => row.Cells()).Select(cell => cell.GetString()), value => value == "0");
    }

    [Fact]
    public void ExportReadsOnlyTheSelectedPackageSnapshot()
    {
        using var output = new TemporaryOutput();
        var package = PackageWithSteel(1);
        var writer = new UnifiedWorkbookWriter();
        var first = writer.Write(package, new WorkbookWriteRequest(output.Directory, null, "snapshot", "run1"));
        var second = writer.Write(package, new WorkbookWriteRequest(output.Directory, null, "snapshot", "run1"));

        Assert.True(first.Success, Details(first.Diagnostics));
        Assert.True(second.Success, Details(second.Diagnostics));
        Assert.Equal(package.InputHash, second.Value!.InputHash);
        Assert.Equal(RecalculationStatus.NotRun, second.Value.RecalculationStatus);
        Assert.NotEqual(first.Value!.Path, second.Value.Path);
    }

    private static CalculationPackage PackageWithSteel(int count, double lengthMm = 1000, double unitMassKgPerM = 0.5,
        double angleMassKg = 0, bool notCalculated = false)
    {
        var footprint = new Footprint(new Polygon2Mm([new(-1000, -1000), new(1000, -1000), new(1000, 1000), new(-1000, 1000)]), []);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var baseComponent = new ComponentGeometry("Base", ComponentKind.Base, "Concrete", footprint, new Interval1D(-150, 0),
            new ExtrusionSolid(footprint, frame, new Interval1D(-150, 0)), [], [], []);
        var geometry = new GeometryModel([baseComponent], []);
        var bars = new List<BarOccurrence>();
        var rows = new List<SteelScheduleRow>();
        for (var index = 1; index <= count; index++)
        {
            var id = $"bar-{index:000}";
            var path = new BarPath([new LineSegment3Mm(new(0, index, 0), new(lengthMm, index, 0))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            bars.Add(new BarOccurrence(id, null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 10, path,
                lengthMm, lengthMm, LengthStatus.Known, "TEST", []));
            rows.Add(new SteelScheduleRow($"D{index:000}", "line", ComponentKind.Base, Layer.Bottom, Axis.X, 10, 1,
                lengthMm, lengthMm, unitMassKgPerM, new MassKg(lengthMm / 1000 * unitMassKgPerM), [id], "TEST"));
        }
        var total = rows.Sum(row => row.MassKg.Value);
        var schedule = new SteelSchedule(rows, [new KeyValuePair<ComponentKind, MassKg>(ComponentKind.Base, new MassKg(total))],
            new MassKg(total), "test", "v1");
        AngleSchedule? angles = angleMassKg == 0 ? null : new AngleSchedule([
            new AngleScheduleRow("A001", "L40", "Base", 1, 1000, 1000, angleMassKg, new MassKg(angleMassKg), ["angle-1"])
        ], new MassKg(angleMassKg));
        var lines = notCalculated ? null : new[] { new QuantityLine("Q1", "Base", "Concrete", 1, 0, 0, 1, QuantityUnit.M3, "Q", [], []) };
        var status = notCalculated ? CalculationStatus.NotCalculated : CalculationStatus.Calculated;
        var scope = notCalculated ? CalculationScope.BaseRebarOnly : CalculationScope.Full;
        return new CalculationPackage(new string('f', 64), new string('a', 64), "test-engine", "contract-v3", "rule-v3", "policy-v3",
            scope, geometry, bars, schedule, status, lines, status, lines, null, [], angles);
    }

    private static string Metadata(IXLWorksheet sheet, string key)
    {
        for (var row = 1; row <= (sheet.LastRowUsed()?.RowNumber() ?? 0); row++)
            if (sheet.Cell(row, 1).GetString() == key) return sheet.Cell(row, 2).GetString();
        throw new Xunit.Sdk.XunitException($"Metadata key missing: {key}");
    }

    private static string Details(IEnumerable<Diagnostic> diagnostics) => string.Join("; ", diagnostics.Select(d => $"{d.Code}: {d.Message}"));

    private sealed class TemporaryOutput : IDisposable
    {
        public TemporaryOutput()
        {
            Directory = Path.Combine(Path.GetTempPath(), "dbtool-unified-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
        }
        public string Directory { get; }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
