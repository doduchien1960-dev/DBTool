using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public enum RecalculationStatus { NotRun, ClosedXmlChecked, ExcelHostVerified }
public sealed record WorkbookWriteRequest
{
    public WorkbookWriteRequest(string outputDirectory, string? templatePath, string displayName, string runId)
    {
        OutputDirectory = ContractGuard.Text(outputDirectory, nameof(outputDirectory));
        TemplatePath = templatePath;
        DisplayName = ContractGuard.Text(displayName, nameof(displayName));
        RunId = ContractGuard.Text(runId, nameof(runId));

    }
    public string OutputDirectory { get; }
    public string? TemplatePath { get; }
    public string DisplayName { get; }
    public string RunId { get; }
}
public sealed record ExportArtifact
{
    public ExportArtifact(string path, string inputHash, string ruleVersion, CalculationScope scope, RecalculationStatus recalculationStatus)
    {
        Path = ContractGuard.Text(path, nameof(path));
        InputHash = ContractGuard.Hash(inputHash);
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        Scope = ContractGuard.EnumValue(scope);
        RecalculationStatus = ContractGuard.EnumValue(recalculationStatus);

    }
    public string Path { get; }
    public string InputHash { get; }
    public string RuleVersion { get; }
    public CalculationScope Scope { get; }
    public RecalculationStatus RecalculationStatus { get; }
}

