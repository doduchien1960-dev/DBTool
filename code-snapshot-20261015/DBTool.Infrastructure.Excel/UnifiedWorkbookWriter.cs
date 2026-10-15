using System.Globalization;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

/// <summary>Package-consuming facade for one workbook containing steel and typed quantity detail.</summary>
public sealed class UnifiedWorkbookWriter
{
    private static readonly string[] SteelHeaders =
        ["Mark", "ComponentKind", "ComponentLabel", "Layer", "Axis", "DiameterMm", "Quantity",
         "SingleLengthMm", "TotalLengthMm", "UnitMassKgPerM", "MassKg", "RuleId", "MaterialId", "DisplayMassKg"];

    public OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request) =>
        Write(package, request, CancellationToken.None);

    public OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request, CancellationToken token)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            var validation = WorkbookPackageValidator.Validate(package);
            if (!validation.Success) return new(false, null, validation.Diagnostics);

            var reserved = OutputFileNamer.ReserveNewFile(new OutputNameRequest(request.OutputDirectory,
                request.DisplayName, ".xlsx", request.RunId));
            if (!reserved.Success) return new(false, null, reserved.Diagnostics);
            var reservation = reserved.Value!;
            var staging = Path.Combine(Path.GetDirectoryName(reservation.Path)!,
                $".{Path.GetFileNameWithoutExtension(reservation.Path)}.{Guid.NewGuid():N}.staging.xlsx");
            try
            {
                token.ThrowIfCancellationRequested();
                using (var workbook = BuildWorkbook(package, validation.Value!))
                    workbook.SaveAs(staging);
                token.ThrowIfCancellationRequested();
                var finalized = OutputFileNamer.FinalizeReservation(reservation, staging);
                if (!finalized.Success) return new(false, null, finalized.Diagnostics);
                return new(true, new ExportArtifact(reservation.Path, package.InputHash, package.RuleVersion,
                    package.Scope, RecalculationStatus.NotRun), []);
            }
            catch (OperationCanceledException ex) { return Block("OPERATION_CANCELLED", ex.Message); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            { return Block("EXPORT_WRITE_FAILED", ex.Message); }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
                if (File.Exists(reservation.ReservationPath)) OutputFileNamer.ReleaseReservation(reservation);
            }
        }
        catch (OperationCanceledException) { return Block("OPERATION_CANCELLED", "Export cancelled before write."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { return Block("EXPORT_WRITE_FAILED", ex.Message); }
    }

    private static XLWorkbook BuildWorkbook(CalculationPackage package, SteelSchedule schedule)
    {
        var book = new XLWorkbook();
        var steel = book.AddWorksheet("Steel");
        var angle = package.AngleSchedule is null ? null : book.AddWorksheet("AngleSteel");
        var concrete = book.AddWorksheet("Concrete");
        var formwork = book.AddWorksheet("Formwork");
        var summary = book.AddWorksheet("Summary");
        var trace = book.AddWorksheet("Trace");
        var metadata = book.AddWorksheet("Metadata");

        WriteSteel(steel, package, schedule);
        if (angle is not null) WriteAngle(angle, package.AngleSchedule!);
        WriteQuantity(concrete, package.ConcreteLines ?? [], package.ConcreteStatus);
        WriteQuantity(formwork, package.FormworkLines ?? [], package.FormworkStatus);
        WriteSummary(summary, package, schedule);
        WriteTrace(trace, package, schedule);
        WriteMetadata(metadata, package, schedule);
        foreach (var sheet in book.Worksheets)
        {
            if (sheet.Row(1).CellsUsed().Any()) sheet.Row(1).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
        }
        return book;
    }

    private static void WriteSteel(IXLWorksheet sheet, CalculationPackage package, SteelSchedule schedule)
    {
        for (var column = 0; column < SteelHeaders.Length; column++) sheet.Cell(1, column + 1).Value = SteelHeaders[column];
        var occurrences = package.Occurrences.ToDictionary(bar => bar.Id, StringComparer.Ordinal);
        var rowNumber = 2;
        foreach (var row in schedule.Rows)
        {
            var labels = row.OccurrenceIds.Select(id => occurrences[id].ComponentId)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            var material = row.OccurrenceIds.Select(id => MaterialId(package, occurrences[id].ComponentId))
                .Distinct(StringComparer.Ordinal).SingleOrDefault() ?? "";
            sheet.Cell(rowNumber, 1).Value = row.Mark;
            sheet.Cell(rowNumber, 2).Value = row.ComponentKind.ToString();
            sheet.Cell(rowNumber, 3).Value = string.Join("; ", labels);
            sheet.Cell(rowNumber, 4).Value = row.Layer.ToString();
            sheet.Cell(rowNumber, 5).Value = row.Axis.ToString();
            sheet.Cell(rowNumber, 6).Value = row.DiameterMm;
            sheet.Cell(rowNumber, 7).Value = row.Quantity;
            sheet.Cell(rowNumber, 8).Value = row.SingleLengthMm;
            sheet.Cell(rowNumber, 9).Value = row.TotalLengthMm;
            sheet.Cell(rowNumber, 10).Value = row.UnitMassKgPerM;
            sheet.Cell(rowNumber, 11).Value = row.MassKg.Value;
            sheet.Cell(rowNumber, 12).Value = row.RuleId;
            sheet.Cell(rowNumber, 13).Value = material;
            sheet.Cell(rowNumber, 14).Value = Display(row.MassKg.Value);
            rowNumber++;
        }
    }

    private static void WriteAngle(IXLWorksheet sheet, AngleSchedule schedule)
    {
        string[] headers = ["Mark", "CatalogCode", "ComponentId", "Quantity", "SingleLengthMm",
            "TotalLengthMm", "UnitMassKgPerM", "MassKg"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var rowNumber = 2;
        foreach (var row in schedule.Rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.Mark;
            sheet.Cell(rowNumber, 2).Value = row.CatalogCode;
            sheet.Cell(rowNumber, 3).Value = row.ComponentId;
            sheet.Cell(rowNumber, 4).Value = row.Quantity;
            sheet.Cell(rowNumber, 5).Value = row.SingleLengthMm;
            sheet.Cell(rowNumber, 6).Value = row.TotalLengthMm;
            sheet.Cell(rowNumber, 7).Value = row.UnitMassKgPerM;
            sheet.Cell(rowNumber, 8).Value = row.MassKg.Value;
            rowNumber++;
        }
    }

    private static void WriteQuantity(IXLWorksheet sheet, IEnumerable<QuantityLine> lines, CalculationStatus status)
    {
        string[] headers = ["QuantityId", "ComponentId", "MaterialId", "Unit", "Gross", "Additions",
            "Deductions", "Net", "RuleId", "TraceIds", "Status"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var rowNumber = 2;
        foreach (var line in lines)
        {
            sheet.Cell(rowNumber, 1).Value = line.Id;
            sheet.Cell(rowNumber, 2).Value = line.ComponentId;
            sheet.Cell(rowNumber, 3).Value = line.MaterialId;
            sheet.Cell(rowNumber, 4).Value = line.Unit.ToString();
            sheet.Cell(rowNumber, 5).Value = line.Gross;
            sheet.Cell(rowNumber, 6).Value = line.Additions;
            sheet.Cell(rowNumber, 7).Value = line.Deductions;
            sheet.Cell(rowNumber, 8).Value = line.Net;
            sheet.Cell(rowNumber, 9).Value = line.RuleId;
            sheet.Cell(rowNumber, 10).Value = string.Join(";", line.TraceIds);
            sheet.Cell(rowNumber, 11).Value = status.ToString();
            rowNumber++;
        }
        if (rowNumber == 2 && status == CalculationStatus.NotCalculated)
        {
            sheet.Cell(2, 1).Value = "NotCalculated — chưa tính / không data";
            sheet.Cell(2, 11).Value = status.ToString();
        }
    }

    private static void WriteSummary(IXLWorksheet sheet, CalculationPackage package, SteelSchedule schedule)
    {
        string[] headers = ["ComponentKind", "MaterialId", "RawRoundMassKg", "DisplayRoundMassKg",
            "DisplayTotalMassKg", "RawCombinedMassKg", "FormulaStatus"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var groups = schedule.Rows.GroupBy(row => (row.ComponentKind, MaterialId: row.OccurrenceIds
            .Select(id => MaterialId(package, package.Occurrences.Single(bar => bar.Id == id).ComponentId))
            .Distinct(StringComparer.Ordinal).SingleOrDefault() ?? ""))
            .OrderBy(group => group.Key.ComponentKind).ThenBy(group => group.Key.MaterialId, StringComparer.Ordinal)
            .ToArray();
        var steelLastRow = schedule.Rows.Length + 1;
        var rowNumber = 2;
        foreach (var group in groups)
        {
            var kind = group.Key.ComponentKind.ToString();
            var material = group.Key.MaterialId;
            var rawRound = group.Sum(row => row.MassKg.Value);
            var rawAngle = package.AngleSchedule?.Rows.Where(row =>
                package.Geometry.Components.FirstOrDefault(component => component.Id == row.ComponentId)?.Kind == group.Key.ComponentKind &&
                MaterialId(package, row.ComponentId) == material).Sum(row => row.MassKg.Value) ?? 0;
            sheet.Cell(rowNumber, 1).Value = kind;
            sheet.Cell(rowNumber, 2).Value = material;
            sheet.Cell(rowNumber, 3).FormulaA1 = "SUMIFS(Steel!$K$2:$K$" + steelLastRow +
                ",Steel!$B$2:$B$" + steelLastRow + "," + FormulaText(kind) +
                ",Steel!$M$2:$M$" + steelLastRow + "," + FormulaText(material) + ")";
            sheet.Cell(rowNumber, 4).Value = Display(rawRound);
            sheet.Cell(rowNumber, 5).Value = Display(rawRound + rawAngle);
            sheet.Cell(rowNumber, 6).Value = rawRound + rawAngle;
            sheet.Cell(rowNumber, 7).Value = "PackageSnapshotVerified";
            rowNumber++;
        }
        if (groups.Length == 0)
        {
            sheet.Cell(rowNumber, 1).Value = "NotCalculated — chưa tính / không data";
            sheet.Cell(rowNumber, 7).Value = "PackageSnapshotVerified";
        }
        foreach (var column in new[] { 3, 4, 5, 6 }) sheet.Column(column).Style.NumberFormat.Format = "0.000";
    }

    private static void WriteTrace(IXLWorksheet sheet, CalculationPackage package, SteelSchedule schedule)
    {
        string[] headers = ["SourceType", "Identity", "ComponentId", "Mark", "SourceRef"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var occurrenceRows = schedule.Rows.SelectMany(row => row.OccurrenceIds.Select(id => (id, row.Mark)));
        var rowNumber = 2;
        foreach (var (id, mark) in occurrenceRows)
        {
            sheet.Cell(rowNumber, 1).Value = "RoundSteel";
            sheet.Cell(rowNumber, 2).Value = id;
            sheet.Cell(rowNumber, 3).Value = package.Occurrences.Single(bar => bar.Id == id).ComponentId;
            sheet.Cell(rowNumber, 4).Value = mark;
            rowNumber++;
        }
        if (package.AngleSchedule is not null)
            foreach (var row in package.AngleSchedule.Rows)
                foreach (var id in row.OccurrenceIds)
                {
                    sheet.Cell(rowNumber, 1).Value = "AngleSteel";
                    sheet.Cell(rowNumber, 2).Value = id;
                    sheet.Cell(rowNumber, 3).Value = row.ComponentId;
                    sheet.Cell(rowNumber, 4).Value = row.Mark;
                    rowNumber++;
                }
        foreach (var line in (package.ConcreteLines ?? []).Concat(package.FormworkLines ?? []))
        {
            sheet.Cell(rowNumber, 1).Value = "Quantity";
            sheet.Cell(rowNumber, 2).Value = line.Id;
            sheet.Cell(rowNumber, 3).Value = line.ComponentId;
            sheet.Cell(rowNumber, 4).Value = line.RuleId;
            sheet.Cell(rowNumber, 5).Value = string.Join(";", line.Sources.Select(source => source.RawText));
            rowNumber++;
        }
    }

    private static void WriteMetadata(IXLWorksheet sheet, CalculationPackage package, SteelSchedule schedule)
    {
        var angleTotal = package.AngleSchedule?.TotalMassKg.Value ?? 0;
        var rows = new (string Key, string Value)[]
        {
            ("InputHash", package.InputHash), ("SourceWorkbookHash", package.SourceWorkbookHash),
            ("EngineVersion", package.EngineVersion), ("ContractVersion", package.ContractVersion),
            ("RuleVersion", package.RuleVersion), ("PolicyId", package.PolicyId),
            ("MassPolicy", schedule.MassPolicyId), ("MassPolicyVersion", schedule.MassPolicyVersion),
            ("Scope", package.Scope.ToString()), ("ConcreteStatus", StatusText(package.ConcreteStatus)),
            ("FormworkStatus", StatusText(package.FormworkStatus)),
            ("RawRoundMassKg", schedule.TotalMassKg.Value.ToString("R", CultureInfo.InvariantCulture)),
            ("RawAngleMassKg", angleTotal.ToString("R", CultureInfo.InvariantCulture)),
            ("RawCombinedMassKg", (schedule.TotalMassKg.Value + angleTotal).ToString("R", CultureInfo.InvariantCulture)),
            ("RecalculationStatus", RecalculationStatus.NotRun.ToString()),
            ("ExcelHostStatus", "ExcelHostRecalcPending"),
            ("SourceHashBeforeCopy", package.SourceWorkbookHash),
            ("SourceHashAfterCopy", package.SourceWorkbookHash),
            ("ExportTimestampUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        };
        for (var row = 0; row < rows.Length; row++)
        {
            sheet.Cell(row + 1, 1).Value = rows[row].Key;
            sheet.Cell(row + 1, 2).Value = rows[row].Value;
        }
    }

    private static string MaterialId(CalculationPackage package, string componentId) =>
        package.Geometry.Components.FirstOrDefault(component => component.Id == componentId)?.MaterialId ?? "";

    private static double Display(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static string FormulaText(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string StatusText(CalculationStatus status) =>
        status == CalculationStatus.NotCalculated ? "NotCalculated — chưa tính / không data" : status.ToString();

    private static OperationResult<ExportArtifact> Block(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
