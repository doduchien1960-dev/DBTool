using System.Collections.Immutable;
namespace DBTool.Domain;
public sealed record OperationResult<T> where T : class
{
    public OperationResult(bool success, T? value, IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostics = ContractGuard.Copy(diagnostics);
        bool blocked = Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Block);
        if (success ? value is null || blocked : value is not null || !blocked)
            throw new ArgumentException("Result truth table violated.");
        Success = success; Value = value;
    }
    public bool Success { get; }
    public T? Value { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

