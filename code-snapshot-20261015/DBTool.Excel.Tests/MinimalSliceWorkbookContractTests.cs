using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Headless;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class MinimalSliceWorkbookContractTests
{
    [Fact]
    public async Task RealWorkbookRoundTripMatchesFrozenExpectedAndPublishesExplicitStatuses()
    {
        string input = TestWorkbookFactory.CreateS1FromJson();
        string outputRoot = Path.Combine(Path.GetTempPath(), "dbtool-t42a-" + Guid.NewGuid().ToString("N"));
        try
        {
            var console = new StringWriter();
            var exit = await HeadlessRunner.RunAsync(Arguments(input, outputRoot), console, default);
            Assert.Equal(0, exit);
            var run = Assert.Single(Directory.GetDirectories(outputRoot, "run-*"));

            using var expected = LoadExpected();
            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "package.json")));
            var expectedRoot = expected.RootElement;
            var expectedSchedule = expectedRoot.GetProperty("steelSchedule");
            var occurrences = package.RootElement.GetProperty("Occurrences");
            Assert.Equal(expectedRoot.GetProperty("bars").GetArrayLength(), occurrences.GetArrayLength());
            Assert.Equal(expectedSchedule.GetProperty("totalLengthMm").GetDouble(),
                occurrences.EnumerateArray().Sum(bar => bar.GetProperty("ScheduleLengthMm").GetDouble()), 6);
            var rawMass = package.RootElement.GetProperty("SteelSchedule")
                .GetProperty("TotalMassKg").GetProperty("Value").GetDouble();
            Assert.Equal(expectedSchedule.GetProperty("totalMassKg").GetDouble(), rawMass, 12);
            Assert.Equal("BaseRebarOnly", package.RootElement.GetProperty("Scope").GetString());
            Assert.Equal("NotCalculated", package.RootElement.GetProperty("ConcreteStatus").GetString());
            Assert.Equal("NotCalculated", package.RootElement.GetProperty("FormworkStatus").GetString());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))),
                package.RootElement.GetProperty("SourceWorkbookHash").GetString());

            var drawing = DrawingPackageJson.Deserialize(File.ReadAllText(Path.Combine(run, "drawing.json")));
            Assert.Equal(6, drawing.Views.Length);
            Assert.Equal(48, drawing.Primitives.Count(primitive => primitive.OccurrenceId is not null));
            Assert.Equal(48, drawing.Primitives.Where(primitive => primitive.OccurrenceId is not null)
                .Select(primitive => primitive.OccurrenceId).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(4, drawing.Primitives.Where(primitive => primitive.ScheduleMark is not null)
                .Select(primitive => primitive.ScheduleMark).Distinct(StringComparer.Ordinal).Count());

            using var workbook = new XLWorkbook(Path.Combine(run, "steel.xlsx"));
            var steel = workbook.Worksheet("Steel");
            Assert.Equal(4, steel.RangeUsed()!.RowCount() - 1);
            Assert.Equal(48, Enumerable.Range(2, 4).Sum(row => steel.Cell(row, 7).GetDouble()));
            Assert.Equal(expectedSchedule.GetProperty("totalLengthMm").GetDouble(),
                Enumerable.Range(2, 4).Sum(row => steel.Cell(row, 9).GetDouble()), 6);
            Assert.Equal(expectedSchedule.GetProperty("totalMassKg").GetDouble(),
                Enumerable.Range(2, 4).Sum(row => steel.Cell(row, 11).GetDouble()), 12);
            Assert.Equal(48, workbook.Worksheet("Trace").RangeUsed()!.RowCount() - 1);

            var metadata = ReadMetadata(workbook.Worksheet("Metadata"));
            Assert.Equal("BaseRebarOnly", metadata["Scope"]);
            Assert.Equal("NotCalculated", metadata["ConcreteStatus"]);
            Assert.Equal("NotCalculated", metadata["FormworkStatus"]);
            Assert.Equal("NotRun", metadata["RecalculationStatus"]);
            Assert.Equal("ExcelHostRecalcPending", metadata["ExcelHostStatus"]);

            using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "status.json")));
            Assert.Equal("Complete", status.RootElement.GetProperty("Package").GetString());
            Assert.Equal("Complete", status.RootElement.GetProperty("Drawing").GetString());
            Assert.Equal("Complete", status.RootElement.GetProperty("Excel").GetString());
            Assert.Equal("NotRun", status.RootElement.GetProperty("Recalculation").GetString());
            Assert.Contains("ExcelHostRecalcPending", console.ToString());
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
        }
    }

    [Fact]
    public async Task OriginalMonoP4BlankIsBlockedWithNullResultAndNoPublishedRun()
    {
        string input = TestWorkbookFactory.CreateS1FromJson();
        string outputRoot = Path.Combine(Path.GetTempPath(), "dbtool-t42a-p4-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var workbook = new XLWorkbook(input))
            {
                workbook.Worksheet("GaThamLienKhoi").Cell("P4").Clear();
                workbook.Save();
            }

            var console = new StringWriter();
            var exit = await HeadlessRunner.RunAsync(Arguments(input, outputRoot), console, default);

            Assert.NotEqual(0, exit);
            Assert.Contains("PIPE_INVERT_REQUIRED", console.ToString());
            Assert.False(Directory.Exists(outputRoot));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
        }
    }

    [Fact]
    public void MissingRequestedSheetDoesNotFallbackAndReturnsNullPayload()
    {
        string input = TestWorkbookFactory.CreateS1FromJson();
        try
        {
            using (var workbook = new XLWorkbook(input))
            {
                workbook.Worksheet("GaThamLienKhoi").Name = "OtherSheet";
                workbook.Save();
            }

            var result = new WorkbookImporter().Read(new WorkbookReadRequest(
                "S1", input, Profile.GaThamLienKhoi, "GaThamLienKhoi"));

            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SHEET_MISSING" &&
                diagnostic.Scope == DiagnosticScope.Global && diagnostic.Severity == DiagnosticSeverity.Block);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
        }
    }

    [Fact]
    public void BlankMiddleRowDoesNotDropFollowingManhole()
    {
        string input = TestWorkbookFactory.Create(edit: workbook =>
        {
            var sheet = workbook.Worksheet("GaThamLienKhoi");
            sheet.Cell("B6").Value = "G400-2";
            sheet.Cell("B8").Value = "G400-3";
        });
        try
        {
            var result = new WorkbookImporter().Read(new WorkbookReadRequest(
                "S1", input, Profile.GaThamLienKhoi, "GaThamLienKhoi"));

            Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Code)));
            Assert.Equal(new[] { 4, 6, 8 }, result.Value!.Rows.Select(row => row.RowIndex));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
        }
    }

    [Fact]
    public void OutputCollisionNeverOverwritesExistingFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dbtool-t42a-collision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var request = new OutputNameRequest(directory, "Result", ".xlsx", "run1");
            var first = OutputFileNamer.ReserveNewFile(request).Value!;
            File.WriteAllText(first.Path, "original");
            var staging = Path.Combine(directory, "staging.xlsx");
            File.WriteAllText(staging, "replacement");

            var collision = OutputFileNamer.FinalizeReservation(first, staging);

            Assert.False(collision.Success);
            Assert.Null(collision.Value);
            Assert.Equal("original", File.ReadAllText(first.Path));
            Assert.Contains(collision.Diagnostics, diagnostic => diagnostic.Code == "OUTPUT_COLLISION");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static string[] Arguments(string input, string outputRoot) =>
    [
        "--project", "S1", "--input", input, "--profile", "GaThamLienKhoi", "--sheet", "GaThamLienKhoi",
        "--id", "S1-MONO-001", "--scope", "BaseRebarOnly", "--policy", "S1-Analytic-1",
        "--allow-experimental", "true", "--output", outputRoot
    ];

    private static JsonDocument LoadExpected() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(TestWorkbookFactory.WorkspaceRoot(),
            "DBTool", "Tests", "Fixtures", "s1-mono-001.expected.json")));

    private static Dictionary<string, string> ReadMetadata(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in sheet.RangeUsed()!.RowsUsed())
            map[row.Cell(1).GetString()] = row.Cell(2).GetString();
        return map;
    }
}
