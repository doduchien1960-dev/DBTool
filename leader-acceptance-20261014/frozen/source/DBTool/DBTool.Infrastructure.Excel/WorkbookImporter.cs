using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed class WorkbookImporter : IWorkbookImporter
{
    public OperationResult<RawProjectInput> Read(WorkbookReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<Diagnostic>();
        if (!File.Exists(request.FilePath))
            return Failure("WORKBOOK_MISSING", $"Workbook not found: {request.FilePath}");
        string hash;
        byte[] snapshot;
        try { snapshot=File.ReadAllBytes(request.FilePath); hash = Convert.ToHexString(SHA256.HashData(snapshot)).ToLowerInvariant(); }
        catch (Exception ex) when(ex is IOException or UnauthorizedAccessException) { return Failure("WORKBOOK_READ_ERROR", ex.Message); }
        string relative = RelativePath(request.FilePath);
        try
        {
            using var stream = new MemoryStream(snapshot,false);
            using var book = new XLWorkbook(stream);
            foreach (string name in new[] { request.MainSheet, "CF.Ga", "CF.Cong", "CF.Data" })
                if (!book.TryGetWorksheet(name, out _))
                    diagnostics.Add(new Diagnostic("SHEET_MISSING", DiagnosticSeverity.Block, DiagnosticScope.Global,
                        null, null, null, name, null, $"Required sheet '{name}' is missing."));
            if (diagnostics.Count > 0) return new(false, null, diagnostics);

            if (book.TryGetWorksheet("VHGpro", out var vhgSheet))
            {
                var markerCell = vhgSheet.Cell("L2");
                string markerText = markerCell.GetString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(markerText) && markerText != "VHGpro 3.2")
                {
                    diagnostics.Add(new Diagnostic("VERSION_UNSUPPORTED", DiagnosticSeverity.Block, DiagnosticScope.Global,
                        null, null, ConvertCell(markerCell, relative, hash).Source, "VHGpro 3.2", markerText, "Unsupported VHGpro version."));
                }
            }
            if (diagnostics.Count > 0) return new(false, null, diagnostics);

            bool hasCoverSheet=book.TryGetWorksheet("DBTool.Covers",out _);
            if(book.TryGetWorksheet("DBTool.Schema",out var schema))
            {
                var version=ConvertCell(schema.Cell("B1"),relative,hash);
                if(version.Kind!=RawCellKind.Text || version.Text!=Vhg32Schema.DbToolInputVersion)
                    diagnostics.Add(new("WORKBOOK_SCHEMA_UNSUPPORTED",DiagnosticSeverity.Block,DiagnosticScope.Global,
                        null,null,version.Source,Vhg32Schema.DbToolInputVersion,version.Text,"Unknown DBTool input schema."));
            }
            else if(hasCoverSheet)
                diagnostics.Add(new("WORKBOOK_SCHEMA_REQUIRED",DiagnosticSeverity.Block,DiagnosticScope.Global,
                    null,null,ConvertCell(book.Worksheet("DBTool.Covers").Cell("A4"),relative,hash).Source,
                    "DBTool.Schema!B1="+Vhg32Schema.DbToolInputVersion,null,"Physical cover catalog requires a versioned schema."));
            if(diagnostics.Count>0) return new(false,null,diagnostics);

            var main = book.Worksheet(request.MainSheet);
            var rows = new List<RawManholeRow>();
            int last = main.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 3;
            for (int r=4;r<=last;r++)
            {
                bool populated = Enumerable.Range(2,26).Any(c => !main.Cell(r,c).IsEmpty());
                if (!populated) continue;
                var cells = Vhg32Schema.MainColumns.ToDictionary(p => p.Key,
                    p => ConvertCell(main.Cell($"{p.Value}{r}"), relative, hash));
                if (string.IsNullOrWhiteSpace(CellText(cells["Name"])))
                    diagnostics.Add(new Diagnostic("ROW_NAME_MISSING", DiagnosticSeverity.Block, DiagnosticScope.Global,
                        null,null,cells["Name"].Source,"Nonblank name",null,$"Row {r} has data but no name."));
                rows.Add(new RawManholeRow(r,cells));
            }

            var config = book.Worksheet("CF.Ga");
            var configCells = new Dictionary<string,RawCell>(StringComparer.Ordinal);
            foreach (var cell in config.CellsUsed(XLCellsUsedOptions.Contents))
                configCells[$"CF.Ga!{cell.Address}"] = ConvertCell(cell,relative,hash);
            foreach (var pair in Vhg32Schema.ConfigAddresses(request.Profile))
                configCells[pair.Key] = ConvertCell(config.Cell(pair.Value),relative,hash);
            if(schema is not null)
            {
                configCells["DBTool.Schema!B1"]=ConvertCell(schema.Cell("B1"),relative,hash);
                foreach(var pair in Vhg32Schema.DbToolConfigAddresses)
                    configCells[pair.Key]=ConvertCell(config.Cell(pair.Value),relative,hash);
            }

            var catalogs = new Dictionary<string,IEnumerable<RawManholeRow>>();
            foreach (string sheet in hasCoverSheet ? new[] { "CF.Cong", "CF.Data", "DBTool.Covers" } : new[] { "CF.Cong", "CF.Data" })
            {
                var ws = book.Worksheet(sheet);
                var rawRows = new List<RawManholeRow>();
                int lastCatalog = ws.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 3;
                int lastColumn = Math.Max(sheet == "CF.Cong" ? 8 : sheet=="DBTool.Covers" ? 18 : 5,
                    ws.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 1);
                for (int r=4;r<=lastCatalog;r++)
                {
                    if (!Enumerable.Range(1,lastColumn).Any(c => !ws.Cell(r,c).IsEmpty())) continue;
                    var cells = new Dictionary<string,RawCell>();
                    for (int c=1;c<=lastColumn;c++)
                    {
                        var cell = ws.Cell(r,c);
                        string column = cell.Address.ColumnLetter;
                        cells[column] = ConvertCell(cell,relative,hash);
                    }
                    rawRows.Add(new RawManholeRow(r,cells));
                }
                catalogs[sheet] = rawRows;
            }
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block)) return new(false,null,diagnostics);
            return new(true,new RawProjectInput(request.ProjectId,hash,request.Profile,request.MainSheet,
                rows,configCells,catalogs,diagnostics),diagnostics);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FileFormatException)
        {
            return Failure("WORKBOOK_READ_ERROR",ex.Message);
        }
    }

    private static string CellText(RawCell cell) => cell.Kind switch
    {
        RawCellKind.Text => cell.Text ?? "",
        RawCellKind.Number => cell.Number?.ToString(CultureInfo.InvariantCulture) ?? "",
        _ => ""
    };

    private static RawCell ConvertCell(IXLCell cell,string relative,string hash)
    {
        string formula = cell.FormulaA1;
        string raw = !string.IsNullOrEmpty(formula) ? "="+formula : cell.IsEmpty() ? "" : cell.CachedValue.ToString(CultureInfo.InvariantCulture);
        var source = new SourceRef(SourceKind.WorkbookCell,relative,hash,cell.Worksheet.Name,
            cell.Address.ToString(),null,raw);
        if (!string.IsNullOrEmpty(formula))
        {
            string? cache = cell.CachedValue.IsBlank ? null : cell.CachedValue.ToString(CultureInfo.InvariantCulture);
            RawCellKind cacheKind = cell.CachedValue.Type switch
            {
                XLDataType.Number => RawCellKind.Number,
                XLDataType.Boolean => RawCellKind.Boolean,
                XLDataType.Error => RawCellKind.Error,
                XLDataType.Blank => RawCellKind.Blank,
                _ => RawCellKind.Text
            };
            return new(RawCellKind.Formula,null,null,null,formula,cache,source,cacheKind);
        }
        if (cell.IsEmpty()) return new(RawCellKind.Blank,null,null,null,null,null,source);
        var value = cell.CachedValue;
        return value.Type switch
        {
            XLDataType.Number => new(RawCellKind.Number,null,value.GetNumber(),null,null,null,source),
            XLDataType.Boolean => new(RawCellKind.Boolean,null,null,value.GetBoolean(),null,null,source),
            XLDataType.Error => new(RawCellKind.Error,value.ToString(CultureInfo.InvariantCulture),null,null,null,null,source),
            _ => new(RawCellKind.Text,value.ToString(CultureInfo.InvariantCulture),null,null,null,null,source)
        };
    }

    private static string RelativePath(string path)
    {
        string full = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(Environment.CurrentDirectory,full).Replace('\\','/');
        return relative.StartsWith("../",StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? Path.GetFileName(full) : relative;
    }

    private static OperationResult<RawProjectInput> Failure(string code,string message) =>
        new(false,null,new[] { new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,
            null,null,null,null,null,message) });
}
