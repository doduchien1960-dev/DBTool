using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class QuantityWorkbookWriterTests
{
    [Fact]
    public void ConcreteQuantityWorkbookPreservesHeadersMetadataTypedSummaryAndTwoHundredLines()
    {
        using var output = new TemporaryOutput();
        var package = Package(CalculationScope.ConcreteOnly, Enumerable.Range(1, 200).Select(i =>
            new QuantityLine($"Q-{i:000}", i % 2 == 0 ? "Base" : "Wall", i % 2 == 0 ? "Concrete" : "BeddingMortar",
                i + 0.1, 0.2, 0.05, i + 0.25, QuantityUnit.M3, "S2-Q-1", [], [$"trace-{i:000}"])));

        var result = new QuantityWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "quantity", "run1"), CancellationToken.None);

        Assert.True(result.Success, Details(result.Diagnostics));
        Assert.Equal(RecalculationStatus.NotRun, result.Value!.RecalculationStatus);
        using var book = new XLWorkbook(result.Value.Path);
        var quantity = book.Worksheet("Quantity");
        Assert.Equal(new[] { "ComponentId", "MaterialId", "Unit", "Gross", "Additions", "Deductions", "Net", "RuleId", "TraceIds" },
            Enumerable.Range(1, 9).Select(column => quantity.Cell(1, column).GetString()));
        Assert.Equal(201, quantity.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(200.25, quantity.Cell(201, 7).GetDouble(), 10);
        Assert.Equal("S2-Q-1", quantity.Cell(2, 8).GetString());
        Assert.Equal("trace-001", quantity.Cell(2, 9).GetString());
        Assert.Equal(3, book.Worksheet("Summary").LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal("Base", book.Worksheet("Summary").Cell(2, 1).GetString());
        Assert.Equal("Concrete", book.Worksheet("Summary").Cell(2, 2).GetString());
        Assert.Equal("M3", book.Worksheet("Summary").Cell(2, 3).GetString());
        Assert.Equal(package.InputHash, Metadata(book.Worksheet("Metadata"), "InputHash"));
        Assert.Equal(package.SourceWorkbookHash, Metadata(book.Worksheet("Metadata"), "SourceHash"));
        Assert.Equal(package.RuleVersion, Metadata(book.Worksheet("Metadata"), "RuleVersion"));
        Assert.Equal(package.Scope.ToString(), Metadata(book.Worksheet("Metadata"), "Scope"));
    }

    [Fact]
    public void CancellationBeforeWriteLeavesNoFinalWorkbookOrReservation()
    {
        using var output = new TemporaryOutput();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = new QuantityWorkbookWriter().Write(Package(CalculationScope.FormworkOnly, [
                new QuantityLine("F-1", "Wall", "Formwork", 0, 1, 0, 1, QuantityUnit.M2, "S2-FW-1", [], [])]),
            new WorkbookWriteRequest(output.Directory, null, "quantity", "run-cancel"), cancellation.Token);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, x => x.Code == "OPERATION_CANCELLED" && x.Severity == DiagnosticSeverity.Block);
        Assert.Empty(Directory.GetFiles(output.Directory));
    }

    [Fact]
    public void FormworkOnlyPackageWritesFormworkLinesWithoutConcreteOrSteelSheets()
    {
        using var output = new TemporaryOutput();
        var package = Package(CalculationScope.FormworkOnly, [
            new QuantityLine("F-1", "Wall", "Formwork", 0, 1.25, 0, 1.25, QuantityUnit.M2, "S2-FW-1", [], [])]);

        var result = new QuantityWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory, null, "formwork", "run1"), CancellationToken.None);

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Path);
        Assert.DoesNotContain("Steel", book.Worksheets.Select(x => x.Name));
        Assert.Equal(1.25, book.Worksheet("Quantity").Cell(2, 7).GetDouble(), 10);
        Assert.Equal("FormworkOnly", Metadata(book.Worksheet("Metadata"), "Scope"));
    }

    private static CalculationPackage Package(CalculationScope scope, IEnumerable<QuantityLine> lines)
    {
        var concrete = scope == CalculationScope.ConcreteOnly ? lines.ToArray() : null;
        var formwork = scope == CalculationScope.FormworkOnly ? lines.ToArray() : null;
        return new CalculationPackage(new string('a', 64), new string('b', 64), "s2-engine", "contract-v2-quantity",
            "S2Geometry-1/v1", "S2Geometry-1", scope, new GeometryModel([], []), [], null,
            concrete is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated, concrete,
            formwork is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated, formwork, null, []);
    }

    private static string Metadata(IXLWorksheet sheet, string key)
    {
        for (int row = 1; row <= (sheet.LastRowUsed()?.RowNumber() ?? 0); row++)
            if (sheet.Cell(row, 1).GetString() == key) return sheet.Cell(row, 2).GetString();
        throw new Xunit.Sdk.XunitException($"Metadata key missing: {key}");
    }

    private static string Details(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(x => $"{x.Code}: {x.Message}"));

    private sealed class TemporaryOutput : IDisposable
    {
        public TemporaryOutput()
        {
            Directory = Path.Combine(Path.GetTempPath(), "dbtool-quantity-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
