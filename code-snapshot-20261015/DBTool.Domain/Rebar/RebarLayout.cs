using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public sealed record RebarLayout
{
    public RebarLayout(IEnumerable<BarOccurrence> occurrences, IEnumerable<Diagnostic> diagnostics, CalculationScope scope, LayoutStatus status = LayoutStatus.Complete, IEnumerable<AngleOccurrence>? angles = null)
    {
        Angles=ContractGuard.Copy(angles ?? []);
        Occurrences = ContractGuard.Copy(occurrences);
        Diagnostics = ContractGuard.Copy(diagnostics);
        Scope = ContractGuard.EnumValue(scope);
        Status = ContractGuard.EnumValue(status);
        if(Occurrences.Select(o=>o.Id).Distinct().Count()!=Occurrences.Length) throw new ArgumentException("Duplicate occurrence.");
        if(Occurrences.Any(o=>o.LengthStatus==LengthStatus.NeedsRule) && !Diagnostics.Any(d=>d.Severity is DiagnosticSeverity.Review or DiagnosticSeverity.Block)) throw new ArgumentException("Unresolved length requires diagnostic.");
    }
    public ImmutableArray<AngleOccurrence> Angles {get;}
    public ImmutableArray<BarOccurrence> Occurrences { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public CalculationScope Scope { get; }
    public LayoutStatus Status { get; }
}
