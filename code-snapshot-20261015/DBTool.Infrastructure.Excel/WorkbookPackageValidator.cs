using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed record TraceLinkValidation(int OccurrenceCount);

public static class WorkbookPackageValidator
{
    public static OperationResult<SteelSchedule> Validate(CalculationPackage? package)
    {
        if (package is null || package.SteelSchedule is null ||
            package.Scope is not (CalculationScope.BaseRebarOnly or CalculationScope.FullRebar or CalculationScope.Full))
            return Block<SteelSchedule>("EXPORT_STEEL_NOT_CALCULATED","Package has no steel schedule for its scope.");
        if (package.Scope == CalculationScope.BaseRebarOnly &&
            package.SteelSchedule.Rows.Any(row=>row.ComponentKind!=ComponentKind.Base))
            return Block<SteelSchedule>("EXPORT_SCOPE_MISMATCH","BaseRebarOnly package contains non-base steel.");

        var links=ValidateTraceIds(package.Occurrences,
            package.SteelSchedule.Rows.SelectMany(row=>row.OccurrenceIds));
        if (!links.Success) return new(false,null,links.Diagnostics);
        var occurrences=package.Occurrences.ToDictionary(bar=>bar.Id,StringComparer.Ordinal);
        foreach (var row in package.SteelSchedule.Rows)
            foreach (string id in row.OccurrenceIds)
            {
                var bar=occurrences[id];
                if (bar.ComponentKind!=row.ComponentKind || bar.Layer!=row.Layer || bar.Axis!=row.Axis ||
                    bar.DiameterMm!=row.DiameterMm || Math.Abs(bar.ScheduleLengthMm-row.SingleLengthMm)>1e-6)
                    return Block<SteelSchedule>("EXPORT_TRACE_MISMATCH",$"Schedule row {row.Mark} contradicts occurrence {id}.");
            }
        return new(true,package.SteelSchedule,[]);
    }

    public static OperationResult<TraceLinkValidation> ValidateTraceIds(
        IEnumerable<BarOccurrence>? occurrences,IEnumerable<string>? scheduledIds)
    {
        if (occurrences is null || scheduledIds is null)
            return Block<TraceLinkValidation>("EXPORT_TRACE_INVALID","Occurrence and trace sets are required.");
        var actual=occurrences.Select(bar=>bar.Id).ToArray();
        var scheduled=scheduledIds.ToArray();
        if (actual.Distinct(StringComparer.Ordinal).Count()!=actual.Length ||
            scheduled.Distinct(StringComparer.Ordinal).Count()!=scheduled.Length)
            return Block<TraceLinkValidation>("EXPORT_DUPLICATE_OCCURRENCE","Trace IDs must be unique.");
        var actualSet=actual.ToHashSet(StringComparer.Ordinal);
        if (scheduled.Any(id=>!actualSet.Contains(id)))
            return Block<TraceLinkValidation>("EXPORT_OCCURRENCE_UNKNOWN","Schedule references an unknown occurrence ID.");
        if (actual.Length!=scheduled.Length)
            return Block<TraceLinkValidation>("EXPORT_OCCURRENCE_UNSCHEDULED","Some occurrences are absent from the schedule.");
        return new(true,new TraceLinkValidation(actual.Length),[]);
    }

    private static OperationResult<T> Block<T>(string code,string message) where T:class =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
