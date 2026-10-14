using System.Collections.Immutable;

namespace DBTool.Domain;
public enum DiagnosticSeverity { Info, Review, Block }
public enum DiagnosticScope { Global, Manhole, Component }
public sealed record Diagnostic
{
    public Diagnostic(string code, DiagnosticSeverity severity, DiagnosticScope scope, string? manholeId, string? componentId, SourceRef? source, string? expected, string? actual, string message)
        : this(code, severity, scope, manholeId, componentId, source, expected, actual, message, null, null, null)
    {
    }

    public Diagnostic(string code, DiagnosticSeverity severity, DiagnosticScope scope, string? manholeId, string? componentId, SourceRef? source, string? expected, string? actual, string message, string? subjectId, string? unit = null, IEnumerable<string>? needsRuleIds = null)
    {
        Code = ContractGuard.Text(code, nameof(code)); Severity = ContractGuard.EnumValue(severity); Scope = ContractGuard.EnumValue(scope);
        if (scope != DiagnosticScope.Global) ContractGuard.Text(manholeId!, nameof(manholeId));
        if (scope == DiagnosticScope.Component) ContractGuard.Text(componentId!, nameof(componentId));
        if (scope == DiagnosticScope.Global && (manholeId is not null || componentId is not null)) throw new ArgumentException("Global diagnostic cannot target a manhole.");
        if (scope == DiagnosticScope.Manhole && componentId is not null) throw new ArgumentException("Component requires Component scope.");
        ManholeId = manholeId; ComponentId = componentId; Source = source; Expected = expected; Actual = actual; Message = ContractGuard.Text(message, nameof(message));
        SubjectId = subjectId;
        Unit = unit;
        NeedsRuleIds = needsRuleIds is null ? ImmutableArray<string>.Empty : ContractGuard.Copy(needsRuleIds);
    }

    public string Code { get; }
    public DiagnosticSeverity Severity { get; }
    public DiagnosticScope Scope { get; }
    public string? ManholeId { get; }
    public string? ComponentId { get; }
    public SourceRef? Source { get; }
    public string? Expected { get; }
    public string? Actual { get; }
    public string Message { get; }
    public string? SubjectId { get; }
    public string? Unit { get; }
    public ImmutableArray<string> NeedsRuleIds { get; }
}
