using System.Collections.Immutable;
namespace DBTool.Domain;
public sealed record RawManholeRow
{
    public RawManholeRow(int rowIndex, IEnumerable<KeyValuePair<string,RawCell>> cells)
    {
        RowIndex = ContractGuard.Positive(rowIndex, nameof(rowIndex));
        Cells = ContractGuard.Map(cells);

    }
    public int RowIndex { get; }
    public ImmutableDictionary<string,RawCell> Cells { get; }
}
public sealed record RawProjectInput
{
    public RawProjectInput(string projectId, string workbookHash, Profile profile, string mainSheet, IEnumerable<RawManholeRow> rows, IEnumerable<KeyValuePair<string,RawCell>> configCells, IEnumerable<KeyValuePair<string,IEnumerable<RawManholeRow>>> catalogCells, IEnumerable<Diagnostic> diagnostics)
    {
        ProjectId = ContractGuard.Text(projectId, nameof(projectId));
        WorkbookHash = ContractGuard.Hash(workbookHash);
        Profile = ContractGuard.EnumValue(profile);
        MainSheet = ContractGuard.Text(mainSheet, nameof(mainSheet));
        Rows = ContractGuard.Copy(rows);
        ConfigCells = ContractGuard.Map(configCells);
        CatalogCells = ContractGuard.Map(catalogCells.Select(p => new KeyValuePair<string,ImmutableArray<RawManholeRow>>(p.Key, ContractGuard.Copy(p.Value))));
        Diagnostics = ContractGuard.Copy(diagnostics);

    }
    public string ProjectId { get; }
    public string WorkbookHash { get; }
    public Profile Profile { get; }
    public string MainSheet { get; }
    public ImmutableArray<RawManholeRow> Rows { get; }
    public ImmutableDictionary<string,RawCell> ConfigCells { get; }
    public ImmutableDictionary<string,ImmutableArray<RawManholeRow>> CatalogCells { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

