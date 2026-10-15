using System.Collections.Immutable;

namespace DBTool.Domain;
public enum SourceKind { WorkbookCell, PdfPage, SourceCode, SyntheticFixture, RuleDocument, Dxf, Dwg, Decision, Derived, CadHandle, NoteRef }
public sealed record SourceRef
{
    public SourceRef(SourceKind sourceKind, string relativePath, string sha256, string? sheetName, string? cellAddress, int? pdfPage, string rawText)
        : this(sourceKind, relativePath, sha256, sheetName, cellAddress, pdfPage, rawText, null, null, null, null)
    {
    }

    public SourceRef(SourceKind sourceKind, string relativePath, string sha256, string? sheetName, string? cellAddress, int? pdfPage, string rawText, string? handle, string? formulaId = null, IEnumerable<string>? parentIds = null, SourceRef? noteSource = null)
    {
        SourceKind = ContractGuard.EnumValue(sourceKind);
        RelativePath = ContractGuard.Text(relativePath, nameof(relativePath)).Replace('\\', '/');
        if (RelativePath.StartsWith('/') || RelativePath.Contains(':') || RelativePath.Split('/').Any(s => s is ".." or "." or ""))
            throw new ArgumentException("Source path must be artifact-relative.");
        Sha256 = ContractGuard.Hash(sha256);
        if (sourceKind == SourceKind.WorkbookCell && (string.IsNullOrWhiteSpace(sheetName) || string.IsNullOrWhiteSpace(cellAddress)))
            throw new ArgumentException("Workbook sources require sheet and cell.");
        if (sourceKind == SourceKind.PdfPage && pdfPage is not > 0) throw new ArgumentException("PDF page is one-based.");
        if (sourceKind != SourceKind.PdfPage && pdfPage is not null) throw new ArgumentException("Page is not applicable.");
        if (sourceKind != SourceKind.WorkbookCell && (sheetName is not null || cellAddress is not null)) throw new ArgumentException("Cell is not applicable.");
        if (sourceKind == SourceKind.PdfPage && !string.IsNullOrEmpty(handle))
            throw new ArgumentException("CAD handle cannot be assigned to a PDF entity.");

        SheetName = sheetName; CellAddress = cellAddress; PdfPage = pdfPage; RawText = rawText ?? throw new ArgumentNullException(nameof(rawText));
        Handle = handle;
        FormulaId = formulaId;
        ParentIds = parentIds is null ? ImmutableArray<string>.Empty : ContractGuard.Copy(parentIds);
        NoteSource = noteSource;
    }

    public SourceKind SourceKind { get; }
    public string RelativePath { get; }
    public string Sha256 { get; }
    public string? SheetName { get; }
    public string? CellAddress { get; }
    public int? PdfPage { get; }
    public string RawText { get; }
    public string? Handle { get; }
    public string? FormulaId { get; }
    public ImmutableArray<string> ParentIds { get; }
    public SourceRef? NoteSource { get; }
}
