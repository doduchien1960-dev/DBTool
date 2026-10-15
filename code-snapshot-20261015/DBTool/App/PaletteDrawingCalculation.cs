using DBTool.Application;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using System.IO;

namespace DBTool.App;

public sealed record PaletteCalculatedManhole(string Name, CalculationPackage Package);
public sealed record PaletteRowReceipt(string Name, string Status, CalculationPackage? Package, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record PaletteDrawingBatch(IReadOnlyList<PaletteCalculatedManhole> Manholes, IReadOnlyList<PaletteRowReceipt>? RowReceipts = null);
public sealed record PaletteDiagnosticReport(string Blocking,string Review,string Information);

/// <summary>Calculate every selected row before allowing the palette to write any CAD entities.</summary>
public sealed class PaletteDrawingCalculation
{
    private readonly IWorkbookImporter importer;
    public PaletteDrawingCalculation(IWorkbookImporter? importer=null)=>this.importer=importer??new WorkbookImporter();
    
    public Task<OperationResult<PaletteDrawingBatch>> CalculateAsync(string filePath, Profile profile,
        string sheet, IEnumerable<string> names, string expectedSourceHash, CancellationToken token)
        => CalculateAsync(filePath, profile, sheet, names, expectedSourceHash, null, token);

    public Task<OperationResult<PaletteDrawingBatch>> CalculateAsync(string filePath, Profile profile,
        string sheet, IEnumerable<string> names, string expectedSourceHash, PresentationRequest? presentation, CancellationToken token)
        => Task.Run(()=>CalculateCore(filePath, profile, sheet, names.ToArray(), expectedSourceHash, presentation, token, importer));

    private static OperationResult<PaletteDrawingBatch> CalculateCore(string filePath, Profile profile,
        string sheet, IEnumerable<string> names, string expectedSourceHash, PresentationRequest? presentation,
        CancellationToken token, IWorkbookImporter importer)
    {
        const string project = "Palette";
        var diagnostics = new List<Diagnostic>();
        var packages = new List<PaletteCalculatedManhole>();
        var rowReceipts = new List<PaletteRowReceipt>();
        var numeric = new NumericPolicy("en-US", false, false);
        var pipeline = new CalculationPipeline(new GeometryStageAdapter(), new FullRebarStage(),
                new SteelScheduleStageAdapter(), new DrawingStageAdapter());
        var validator=new ManholeValidator(new NumericCellReader(),numeric);
        var pres = presentation ?? Presentation;
        try
        {
            var selected = names.ToArray();
            if (selected.Length == 0 || selected.Select(ManholeIdentity.CanonicalText).Distinct().Count() != selected.Length)
                return Fail("PALETTE_SELECTION_INVALID", "Chọn ít nhất một mã ga duy nhất.");
            token.ThrowIfCancellationRequested();
            var raw=importer.Read(new(project,filePath,profile,sheet));
            if(!raw.Success)return new(false,null,raw.Diagnostics);
            if(!string.Equals(raw.Value!.WorkbookHash,expectedSourceHash,StringComparison.OrdinalIgnoreCase))
                return Fail("SOURCE_SNAPSHOT_MISMATCH","Excel đã thay đổi. Đọc lại dữ liệu trước khi vẽ.");
            var catalogs=new CatalogReader().Read(raw.Value,numeric);
            if(!catalogs.Success)return new(false,null,catalogs.Diagnostics);
            var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
            foreach (var name in selected)
            {
                token.ThrowIfCancellationRequested();
                var rowDiags = new List<Diagnostic>();
                var request = new CalculationRequest(project, [ManholeIdentity.Create(project, profile, name)],
                    CalculationScope.FullRebar, "S2Rebar-1", true);
                var validation=validator.Validate(raw.Value,catalogs.Value!,request,policy);
                rowDiags.AddRange(validation.Diagnostics);
                diagnostics.AddRange(validation.Diagnostics);
                if(!validation.Success)
                {
                    rowReceipts.Add(new PaletteRowReceipt(name, "Blocked", null, rowDiags));
                    continue;
                }
                var snapshot=new ValidatedProjectInput(validation.Value!.SelectedManholes,validation.Value.InputHash,
                    validation.Value.RuleVersion,raw.Diagnostics.Concat(catalogs.Diagnostics).Concat(validation.Diagnostics).Distinct(),raw.Value.WorkbookHash);
                var result=pipeline.Calculate(snapshot,policy,request,pres,token);
                rowDiags.AddRange(result.Diagnostics);
                diagnostics.AddRange(result.Diagnostics);
                if (!result.Success)
                {
                    rowReceipts.Add(new PaletteRowReceipt(name, "Failed", null, rowDiags));
                    continue;
                }
                if (!string.Equals(result.Value!.SourceWorkbookHash, expectedSourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    var diag = new Diagnostic("SOURCE_SNAPSHOT_MISMATCH", DiagnosticSeverity.Block, DiagnosticScope.Global,
                        name, null, null, expectedSourceHash, result.Value!.SourceWorkbookHash, "Excel đã thay đổi. Đọc lại dữ liệu trước khi vẽ.");
                    rowDiags.Add(diag);
                    diagnostics.Add(diag);
                    rowReceipts.Add(new PaletteRowReceipt(name, "Failed", null, rowDiags));
                    continue;
                }
                packages.Add(new(name, result.Value));
                rowReceipts.Add(new PaletteRowReceipt(name, "Complete", result.Value, rowDiags));
            }
            bool allSuccess = packages.Count == selected.Length && !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block);
            return new(allSuccess, allSuccess ? new PaletteDrawingBatch(packages.AsReadOnly(), rowReceipts.AsReadOnly()) : null, diagnostics.Distinct());
        }
        catch (OperationCanceledException) { return Fail("OPERATION_CANCELLED", "Đã hủy tính toán."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { return Fail("PALETTE_CALCULATION_FAILED", ex.Message); }
    }

    public static PresentationRequest Presentation => new(30, ElevationMode.Symbolic, null, "VHG-Dark", true);

    public static PaletteDiagnosticReport FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var items=diagnostics.ToArray();
        string Group(DiagnosticSeverity severity)=>string.Join("\n",items.Where(d=>d.Severity==severity).Select(d=>
        {
            string location=d.Source is null ? "" : d.Source.SheetName is null ? d.Source.RelativePath :
                d.Source.RelativePath+" / "+d.Source.SheetName+"!"+d.Source.CellAddress;
            return $"[{d.Severity}][{d.Scope}] {d.ManholeId ?? "Tất cả ga"} {location} {d.Code}: {d.Message}";
        }));
        return new(Group(DiagnosticSeverity.Block),Group(DiagnosticSeverity.Review),Group(DiagnosticSeverity.Info));
    }

    private static OperationResult<PaletteDrawingBatch> Fail(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
