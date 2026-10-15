using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public sealed record WorkbookReadRequest
{
    public WorkbookReadRequest(string projectId, string filePath, Profile profile, string mainSheet)
    {
        ProjectId = ContractGuard.Text(projectId, nameof(projectId));
        FilePath = ContractGuard.Text(filePath, nameof(filePath));
        Profile = ContractGuard.EnumValue(profile);
        MainSheet = ContractGuard.Text(mainSheet, nameof(mainSheet));

    }
    public string ProjectId { get; }
    public string FilePath { get; }
    public Profile Profile { get; }
    public string MainSheet { get; }
}
public sealed record ValidatedProjectInput
{
    public ValidatedProjectInput(IEnumerable<ValidatedManholeSpec> selectedManholes, string inputHash, string ruleVersion, IEnumerable<Diagnostic> diagnostics, string? sourceWorkbookHash = null)
    {
        SelectedManholes = ContractGuard.Copy(selectedManholes);
        InputHash = ContractGuard.Hash(inputHash);
        SourceWorkbookHash = sourceWorkbookHash is null ? null : ContractGuard.Hash(sourceWorkbookHash);
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        Diagnostics = ContractGuard.Copy(diagnostics);
        if(SelectedManholes.IsEmpty || SelectedManholes.Select(m=>m.Input.Id).Distinct().Count()!=SelectedManholes.Length) throw new ArgumentException("Invalid validated selection.");
        if(Diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Block) || SelectedManholes.Any(m=>m.RuleVersion!=ruleVersion)) throw new ArgumentException("Validated project mismatch.");
    }
    public ImmutableArray<ValidatedManholeSpec> SelectedManholes { get; }
    public string InputHash { get; }
    public string? SourceWorkbookHash { get; }
    public string RuleVersion { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}
