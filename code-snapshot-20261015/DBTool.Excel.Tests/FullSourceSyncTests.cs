using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class FullSourceSyncTests
{
    [Fact]
    public void FullCopyUpdatesSelectedIdentityLeavesOtherRowsAndVerifiesAggregateWhenKnown()
    {
        using var fixture = new SourceFixture();
        var request = fixture.Request(SourceSyncMode.RecalculateAggregate, ["S1/GaThamLienKhoi/B"], fixture.Package("B", 25));

        var result = SourceWorkbookSynchronizer.SyncToCopy(request);

        Assert.True(result.Success, Details(result.Diagnostics));
        Assert.Single(result.Value!.UpdatedIds);
        Assert.Equal("S1/GaThamLienKhoi/B", result.Value.UpdatedIds[0]);
        using var book = new XLWorkbook(result.Value.Artifact.Path);
        var sheet = book.Worksheet("Thep");
        Assert.Equal(10, sheet.Cell("C5").GetDouble());
        Assert.Equal(25, sheet.Cell("C6").GetDouble());
        Assert.Equal(30, sheet.Cell("C7").GetDouble());
        Assert.Equal(25, sheet.Cell("P6").GetDouble());
        Assert.Equal("AggregateVerified", Metadata(book.Worksheet("DBTool.SyncStatus"), "AggregateStatus"));
    }

    [Fact]
    public void StaleUnselectedRowsCannotProduceVerifiedAggregate()
    {
        using var fixture = new SourceFixture();
        fixture.AddStaleStatus("A");
        var request = fixture.Request(SourceSyncMode.RecalculateAggregate, ["S1/GaThamLienKhoi/B"], fixture.Package("B", 25));

        var result = SourceWorkbookSynchronizer.SyncToCopy(request);

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Artifact.Path);
        Assert.NotEqual("AggregateVerified", Metadata(book.Worksheet("DBTool.SyncStatus"), "AggregateStatus"));
        Assert.Contains(Metadata(book.Worksheet("DBTool.SyncStatus"), "AggregateStatus"),
            new[] { "AggregateUnavailable", "Review" });
        Assert.NotEqual(65, book.Worksheet("Thep").Cell("P6").GetDouble());
    }

    [Fact]
    public void ExpectedSourceHashMismatchAndDuplicateCanonicalSelectionBlockBeforeCopy()
    {
        using var fixture = new SourceFixture();
        var wrongHash = new string('0', 64);
        var mismatch = fixture.Request(SourceSyncMode.PreserveWithExplicitStatus, ["S1/GaThamLienKhoi/B"], fixture.Package("B", 25), wrongHash);
        var duplicate = fixture.Request(SourceSyncMode.PreserveWithExplicitStatus, ["S1/GaThamLienKhoi/B", "S1/GaThamLienKhoi/B"], fixture.Package("B", 25));

        var hashResult = SourceWorkbookSynchronizer.SyncToCopy(mismatch);
        var duplicateResult = SourceWorkbookSynchronizer.SyncToCopy(duplicate);

        Assert.False(hashResult.Success);
        Assert.Null(hashResult.Value);
        Assert.Contains(hashResult.Diagnostics, diagnostic => diagnostic.Code == "SOURCE_HASH_MISMATCH");
        Assert.False(duplicateResult.Success);
        Assert.Null(duplicateResult.Value);
        Assert.Contains(duplicateResult.Diagnostics, diagnostic => diagnostic.Code == "SYNC_ID_AMBIGUOUS");
        Assert.Empty(Directory.GetFiles(fixture.OutputDirectory, "source-sync*.xlsx"));
    }

    [Fact]
    public void BlankRowsDoNotHideLaterIdentityAndSecondRunGetsReservedOutput()
    {
        using var fixture = new SourceFixture();
        fixture.InsertBlankRow(6);
        var request = fixture.Request(SourceSyncMode.PreserveWithExplicitStatus, ["S1/GaThamLienKhoi/C"], fixture.Package("C", 35));

        var first = SourceWorkbookSynchronizer.SyncToCopy(request);
        var secondRequest = fixture.Request(SourceSyncMode.PreserveWithExplicitStatus, ["S1/GaThamLienKhoi/C"], fixture.Package("C", 36));
        var second = SourceWorkbookSynchronizer.SyncToCopy(secondRequest);

        Assert.True(first.Success, Details(first.Diagnostics));
        Assert.True(second.Success, Details(second.Diagnostics));
        Assert.NotEqual(first.Value!.Artifact.Path, second.Value!.Artifact.Path);
        using var book = new XLWorkbook(second.Value.Artifact.Path);
        Assert.Equal(36, book.Worksheet("Thep").Cell("C8").GetDouble());
    }

    [Fact]
    public void TwoHundredthSourceRowCanBeSelectedAndUpdated()
    {
        using var fixture = new SourceFixture();
        fixture.SetRows(200);
        var request = fixture.Request(SourceSyncMode.RecalculateAggregate,
            ["S1/GaThamLienKhoi/Row200"], fixture.Package("Row200", 25));

        var result = SourceWorkbookSynchronizer.SyncToCopy(request);

        Assert.True(result.Success, Details(result.Diagnostics));
        using var book = new XLWorkbook(result.Value!.Artifact.Path);
        Assert.Equal(25, book.Worksheet("Thep").Cell(204, 3).GetDouble());
    }

    [Fact]
    public void CancellationDoesNotPublishOrOverwriteOutput()
    {
        using var fixture = new SourceFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = SourceWorkbookSynchronizer.SyncToCopy(
            fixture.Request(SourceSyncMode.PreserveWithExplicitStatus, ["S1/GaThamLienKhoi/B"], fixture.Package("B", 25)), cancellation.Token);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OPERATION_CANCELLED");
        Assert.Empty(Directory.GetFiles(fixture.OutputDirectory, "source-sync*.xlsx"));
    }

    private static string Metadata(IXLWorksheet sheet, string key)
    {
        for (var row = 1; row <= (sheet.LastRowUsed()?.RowNumber() ?? 0); row++)
            if (sheet.Cell(row, 1).GetString() == key) return sheet.Cell(row, 2).GetString();
        throw new Xunit.Sdk.XunitException($"Metadata key missing: {key}");
    }

    private static string Details(IEnumerable<Diagnostic> diagnostics) => string.Join("; ", diagnostics.Select(d => $"{d.Code}: {d.Message}"));

    private sealed class SourceFixture : IDisposable
    {
        private readonly string sourcePath;
        public string OutputDirectory { get; } = Path.Combine(Path.GetTempPath(), "dbtool-source-sync-" + Guid.NewGuid().ToString("N"));

        public SourceFixture()
        {
            Directory.CreateDirectory(OutputDirectory);
            sourcePath = Path.Combine(OutputDirectory, "source.xlsx");
            using var book = new XLWorkbook();
            var sheet = book.AddWorksheet("Thep");
            sheet.Cell("A2").Value = "TT"; sheet.Cell("B2").Value = "Hố ga";
            sheet.Cell("C2").Value = "Đáy"; sheet.Cell("E2").Value = "Tường"; sheet.Cell("G2").Value = "Trần";
            sheet.Cell("I2").Value = "Cổ"; sheet.Cell("K2").Value = "Cửa thu"; sheet.Cell("M2").Value = "Nắp";
            sheet.Cell("O2").Value = "Thép hình"; sheet.Cell("P2").Value = "Tổng";
            for (var column = 3; column <= 14; column += 2) { sheet.Cell(3, column).Value = "D ≤ 10"; sheet.Cell(3, column + 1).Value = "D > 10"; }
            AddRow(sheet, 5, "A", 10);
            AddRow(sheet, 6, "B", 20);
            AddRow(sheet, 7, "C", 30);
            book.SaveAs(sourcePath);
        }

        public void InsertBlankRow(int row)
        {
            using var book = new XLWorkbook(sourcePath);
            book.Worksheet("Thep").Row(row).InsertRowsAbove(1);
            book.SaveAs(sourcePath);
        }

        public void SetRows(int count)
        {
            using var book = new XLWorkbook(sourcePath);
            var sheet = book.Worksheet("Thep");
            for (var index = 0; index < count; index++) AddRow(sheet, 5 + index, $"Row{index + 1}", index + 1);
            book.SaveAs(sourcePath);
        }

        public void AddStaleStatus(string name)
        {
            using var book = new XLWorkbook(sourcePath);
            var status = book.AddWorksheet("DBTool.SyncStatus");
            status.Cell("A1").Value = "OverallTotalsNotValidated";
            status.Cell("A2").Value = "SourceRow"; status.Cell("B2").Value = "ManholeId";
            status.Cell("C2").Value = "Profile"; status.Cell("D2").Value = "Name"; status.Cell("E2").Value = "Status";
            status.Cell("A3").Value = 5; status.Cell("B3").Value = "S1/GaThamLienKhoi/A";
            status.Cell("C3").Value = "GaThamLienKhoi"; status.Cell("D3").Value = name; status.Cell("E3").Value = "StaleNotRecalculated";
            book.SaveAs(sourcePath);
        }

        public SourceWorkbookCopyRequest Request(SourceSyncMode mode, IEnumerable<string> selected,
            params SourceWorkbookSyncPackage[] packages) => Request(mode, selected, packages, null);

        public SourceWorkbookCopyRequest Request(SourceSyncMode mode, IEnumerable<string> selected,
            SourceWorkbookSyncPackage package, string expectedHash) => Request(mode, selected, [package], expectedHash);

        public SourceWorkbookCopyRequest Request(SourceSyncMode mode, IEnumerable<string> selected,
            IEnumerable<SourceWorkbookSyncPackage> packages, string? expectedHash = null)
        {
            var hash = expectedHash ?? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
            return new SourceWorkbookCopyRequest(sourcePath, hash, selected, OutputDirectory, "run-1", mode, packages);
        }

        public SourceWorkbookSyncPackage Package(string name, double mass) =>
            PackageBuilder.Create($"S1/GaThamLienKhoi/{name}", name, mass);

        public void Dispose() => Directory.Delete(OutputDirectory, true);

        private static void AddRow(IXLWorksheet sheet, int row, string name, double value)
        {
            sheet.Cell(row, 2).Value = name;
            sheet.Cell(row, 3).Value = value;
            sheet.Cell(row, 16).Value = value;
        }
    }

    private static class PackageBuilder
    {
        public static SourceWorkbookSyncPackage Create(string id, string name, double mass)
        {
            var footprint = new Footprint(new Polygon2Mm([new(-500, -500), new(500, -500), new(500, 500), new(-500, 500)]), []);
            var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
            var component = new ComponentGeometry("Base", ComponentKind.Base, "Concrete", footprint, new Interval1D(-150, 0),
                new ExtrusionSolid(footprint, frame, new Interval1D(-150, 0)), [], [], []);
            var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(1000, 0, 0))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            var bar = new BarOccurrence("bar", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 10, path,
                1000, 1000, LengthStatus.Known, "T", []);
            var row = new SteelScheduleRow("D001", "line", ComponentKind.Base, Layer.Bottom, Axis.X, 10, 1,
                1000, 1000, mass / 1, new MassKg(mass), ["bar"], "T");
            var schedule = new SteelSchedule([row], [new KeyValuePair<ComponentKind, MassKg>(ComponentKind.Base, new MassKg(mass))],
                new MassKg(mass), "test", "v1");
            var line = new QuantityLine("Q", "Base", "Concrete", 1, 0, 0, 1, QuantityUnit.M3, "Q", [], []);
            var package = new CalculationPackage(new string('1', 64), new string('2', 64), "engine", "contract", "rule", "policy",
                CalculationScope.Full, new GeometryModel([component], []), [bar], schedule,
                CalculationStatus.Calculated, [line], CalculationStatus.Calculated, [line], null, []);
            var values = Enum.GetValues<SyncBucket>().Select(bucket =>
                new SyncSummaryValue(bucket, bucket == SyncBucket.BaseD10 ? mass : 0, CalculationStatus.Calculated));
            return new SourceWorkbookSyncPackage(id, Profile.GaThamLienKhoi, name, package, values);
        }
    }
}
