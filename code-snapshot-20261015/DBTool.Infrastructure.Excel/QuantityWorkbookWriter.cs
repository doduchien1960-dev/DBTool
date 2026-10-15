using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed class QuantityWorkbookWriter : IQuantityWorkbookWriter
{
    private static readonly string[] QuantityHeaders =
        ["ComponentId", "MaterialId", "Unit", "Gross", "Additions", "Deductions", "Net", "RuleId", "TraceIds"];

    public OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(request);
            var validation = Validate(package);
            if (!validation.Success) return new(false, null, validation.Diagnostics);

            var reserved = OutputFileNamer.ReserveNewFile(new OutputNameRequest(request.OutputDirectory,
                request.DisplayName, ".xlsx", request.RunId));
            if (!reserved.Success) return new(false, null, reserved.Diagnostics);
            var reservation = reserved.Value!;
            string staging = Path.Combine(Path.GetDirectoryName(reservation.Path)!,
                $".{Path.GetFileNameWithoutExtension(reservation.Path)}.{Guid.NewGuid():N}.staging.xlsx");
            try
            {
                token.ThrowIfCancellationRequested();
                using (var book = BuildWorkbook(package, validation.Value!)) book.SaveAs(staging);
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

    private static OperationResult<QuantityLine[]> Validate(CalculationPackage package)
    {
        if (package.Scope is not (CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly))
            return Block<QuantityLine[]>("EXPORT_QUANTITY_SCOPE_INVALID", "Quantity workbook requires ConcreteOnly or FormworkOnly.");
        IEnumerable<QuantityLine>? lines = package.Scope == CalculationScope.ConcreteOnly ? package.ConcreteLines : package.FormworkLines;
        if (lines is null || (package.Scope == CalculationScope.ConcreteOnly && package.ConcreteStatus != CalculationStatus.Calculated) ||
            (package.Scope == CalculationScope.FormworkOnly && package.FormworkStatus != CalculationStatus.Calculated))
            return Block<QuantityLine[]>("EXPORT_QUANTITY_NOT_CALCULATED", "Package does not contain calculated quantity lines for its scope.");
        return new(true, lines.ToArray(), []);
    }

    private static XLWorkbook BuildWorkbook(CalculationPackage package, QuantityLine[] lines)
    {
        var book = new XLWorkbook();
        var quantity = book.AddWorksheet("Quantity");
        var summary = book.AddWorksheet("Summary");
        var trace = book.AddWorksheet("Trace");
        var metadata = book.AddWorksheet("Metadata");

        for (int column = 0; column < QuantityHeaders.Length; column++)
            quantity.Cell(1, column + 1).Value = QuantityHeaders[column];

        int row = 2;
        foreach (var line in lines)
        {
            quantity.Cell(row, 1).Value = line.ComponentId;
            quantity.Cell(row, 2).Value = line.MaterialId;
            quantity.Cell(row, 3).Value = line.Unit.ToString();
            quantity.Cell(row, 4).Value = line.Gross;
            quantity.Cell(row, 5).Value = line.Additions;
            quantity.Cell(row, 6).Value = line.Deductions;
            quantity.Cell(row, 7).Value = line.Net;
            quantity.Cell(row, 8).Value = line.RuleId;
            quantity.Cell(row, 9).Value = string.Join(";", line.TraceIds);
            row++;
        }

        string[] summaryHeaders = ["ComponentId", "MaterialId", "Unit", "Gross", "Additions", "Deductions", "Net"];
        for (int column = 0; column < summaryHeaders.Length; column++) summary.Cell(1, column + 1).Value = summaryHeaders[column];
        int summaryRow = 2;
        foreach (var group in lines.GroupBy(x => (x.ComponentId, x.MaterialId, x.Unit))
                     .OrderBy(x => x.Key.ComponentId, StringComparer.Ordinal)
                     .ThenBy(x => x.Key.MaterialId, StringComparer.Ordinal)
                     .ThenBy(x => x.Key.Unit))
        {
            summary.Cell(summaryRow, 1).Value = group.Key.ComponentId;
            summary.Cell(summaryRow, 2).Value = group.Key.MaterialId;
            summary.Cell(summaryRow, 3).Value = group.Key.Unit.ToString();
            summary.Cell(summaryRow, 4).Value = group.Sum(x => x.Gross);
            summary.Cell(summaryRow, 5).Value = group.Sum(x => x.Additions);
            summary.Cell(summaryRow, 6).Value = group.Sum(x => x.Deductions);
            summary.Cell(summaryRow, 7).Value = group.Sum(x => x.Net);
            summaryRow++;
        }

        string[] traceHeaders = ["QuantityId", "TraceIds", "SourceRefs"];
        for (int column = 0; column < traceHeaders.Length; column++) trace.Cell(1, column + 1).Value = traceHeaders[column];
        int traceRow = 2;
        foreach (var line in lines)
        {
            trace.Cell(traceRow, 1).Value = line.Id;
            trace.Cell(traceRow, 2).Value = string.Join(";", line.TraceIds);
            trace.Cell(traceRow, 3).Value = string.Join(";", line.Sources.Select(x =>
                $"{x.RelativePath}!{x.SheetName}!{x.CellAddress}#{x.Sha256}"));
            traceRow++;
        }

        var metadataRows = new (string Key, string Value)[]
        {
            ("Scope", package.Scope.ToString()),
            ("SourceHash", package.SourceWorkbookHash),
            ("InputHash", package.InputHash),
            ("RuleVersion", package.RuleVersion),
            ("EngineVersion", package.EngineVersion),
            ("ContractVersion", package.ContractVersion),
            ("PolicyId", package.PolicyId),
            ("RecalculationStatus", RecalculationStatus.NotRun.ToString()),
            ("ExcelHostStatus", "ExcelHostRecalcPending")
        };
        for (int index = 0; index < metadataRows.Length; index++)
        {
            metadata.Cell(index + 1, 1).Value = metadataRows[index].Key;
            metadata.Cell(index + 1, 2).Value = metadataRows[index].Value;
        }

        quantity.Row(1).Style.Font.Bold = true;
        summary.Row(1).Style.Font.Bold = true;
        trace.Row(1).Style.Font.Bold = true;
        quantity.SheetView.FreezeRows(1);
        trace.SheetView.FreezeRows(1);
        return book;
    }

    private static OperationResult<T> Block<T>(string code, string message) where T : class =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);

    private static OperationResult<ExportArtifact> Block(string code, string message) =>
        Block<ExportArtifact>(code, message);
}
