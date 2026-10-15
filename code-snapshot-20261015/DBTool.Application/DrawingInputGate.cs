using DBTool.Domain;
using System.Collections.Immutable;
namespace DBTool.Application;
public static class DrawingInputGate
{
    public static ImmutableArray<Diagnostic> GetBlockingDiagnostics(IEnumerable<string> selectedIds,IEnumerable<Diagnostic> diagnostics)
    {
        var selected=selectedIds.ToHashSet(StringComparer.Ordinal);
        return diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Block &&
            (d.Scope==DiagnosticScope.Global || d.ManholeId is not null && selected.Contains(d.ManholeId))).ToImmutableArray();
    }
}
