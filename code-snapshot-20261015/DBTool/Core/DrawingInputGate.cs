using System;
using System.Collections.Generic;
using System.Linq;
using Domain=DBTool.Domain;
namespace DBTool.Core;

/// <summary>Translate legacy ownership to stable IDs, then invoke the shared Application gate.</summary>
public static class DrawingInputGate
{
    public static List<Diagnostic> ForUi(IEnumerable<NormalizedManholeSpec> selected,IEnumerable<Diagnostic> diagnostics)=>GetBlockingDiagnostics(selected,diagnostics);
    public static List<Diagnostic> ForBatch(IEnumerable<NormalizedManholeSpec> selected,IEnumerable<Diagnostic> diagnostics)=>GetBlockingDiagnostics(selected,diagnostics);
    public static List<Diagnostic> ForExport(IEnumerable<NormalizedManholeSpec> selected,IEnumerable<Diagnostic> diagnostics)=>GetBlockingDiagnostics(selected,diagnostics);
    public static List<Diagnostic> GetBlockingDiagnostics(IEnumerable<NormalizedManholeSpec> selectedManholes,IEnumerable<Diagnostic> diagnostics)
    {
        var selected=selectedManholes.ToArray();
        string Id(NormalizedManholeSpec m)=>Application.ManholeIdentity.Create("Legacy",(Domain.Profile)m.Type,m.Name);
        var selectedIds=selected.Select(Id).ToArray();
        var mapped=diagnostics.Select(d=>
        {
            string? owner=null;
            string? sheet=d.Source?.SheetName;
            bool global=string.IsNullOrWhiteSpace(d.ManholeName) || new[]{"CF.Ga","CF.Cong","CF.Data"}.Contains(sheet,StringComparer.OrdinalIgnoreCase);
            if(!global)
            {
                if(Enum.TryParse<Domain.Profile>(sheet,true,out var profile) && Enum.IsDefined(profile))
                    owner=Application.ManholeIdentity.Create("Legacy",profile,d.ManholeName);
                else
                {
                    var candidates=selected.Where(m=>Application.ManholeIdentity.CanonicalText(m.Name)==Application.ManholeIdentity.CanonicalText(d.ManholeName)).Select(Id).Distinct().ToArray();
                    if(candidates.Length==1) owner=candidates[0];
                }
            }
            // Legacy has no source hash; retain the original diagnostic for display instead of inventing provenance.
            var normalized=new Domain.Diagnostic("LEGACY_INPUT_DIAGNOSTIC",(Domain.DiagnosticSeverity)d.Severity,
                owner==null?Domain.DiagnosticScope.Global:Domain.DiagnosticScope.Manhole,owner,null,null,null,d.Source?.ToString(),d.Message);
            return (Original:d,Normalized:normalized);
        }).ToArray();
        var blocked=Application.DrawingInputGate.GetBlockingDiagnostics(selectedIds,mapped.Select(m=>m.Normalized)).ToHashSet();
        return mapped.Where(m=>blocked.Contains(m.Normalized)).Select(m=>m.Original).ToList();
    }
}
