using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class SteelScheduleCalculator
{
    public static OperationResult<SteelSchedule> Calculate(
        IReadOnlyList<BarOccurrence> occurrences, MassPolicy massPolicy, MarkPolicy markPolicy)
    {
        if (occurrences is null || massPolicy is null || markPolicy is null || occurrences.Count == 0)
            return Block("SCHEDULE_INVALID_INPUT", "Occurrences and policies are required.");
        if (occurrences.Any(o => o is null))
            return Block("SCHEDULE_INVALID_INPUT", "Occurrence list contains null.");
        var duplicate = occurrences.GroupBy(o => o.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Block("SCHEDULE_DUPLICATE_OCCURRENCE", $"Duplicate occurrence ID: {duplicate.Key}", duplicate.First().Sources.FirstOrDefault());

        double unitFactor = massPolicy.Id switch {
            MassPolicyId.D2Over162 => 1d / 162,
            MassPolicyId.Density7850 => Math.PI / 4 * massPolicy.DensityKgPerM3!.Value / 1_000_000,
            _ => double.NaN
        };
        if (!double.IsFinite(unitFactor) || unitFactor <= 0)
            return Block("SCHEDULE_MASS_POLICY_INVALID", "Unknown mass policy.");

        var keyed = new List<(ScheduleGroupKey Key, BarOccurrence Bar)>();
        foreach (var bar in occurrences)
        {
            if (!double.IsFinite(bar.ScheduleLengthMm) || bar.ScheduleLengthMm <= 0 ||
                !double.IsFinite(bar.FabricationLengthMm ?? 0) || bar.DiameterMm <= 0)
                return Block("SCHEDULE_LENGTH_INVALID", "Bar length or diameter is invalid.", bar.Sources.FirstOrDefault());
            var key = new ScheduleGroupKey(bar.ComponentKind, bar.Layer, bar.Axis, bar.DiameterMm,
                ShapeSignature.Create(bar.Path, bar.DiameterMm), bar.RuleId, bar.ScheduleLengthMm,
                bar.Direction, bar.FrameId, bar.Surface, bar.FamilyId);
            keyed.Add((key, bar));
        }

        var groups = keyed.GroupBy(x => x.Key).ToArray();
        var marks = BarMarkAllocator.Allocate(groups.Select(g => g.Key).ToArray(), markPolicy.Scope);
        var rows = new List<SteelScheduleRow>();
        foreach (var group in groups.OrderBy(g => marks[g.Key], StringComparer.Ordinal))
        {
            var key = group.Key;
            int quantity = group.Count();
            double totalLength = quantity * key.SingleLengthMm;
            double unitMass = key.DiameterMm * (double)key.DiameterMm * unitFactor;
            double mass = totalLength / 1000 * unitMass;
            if (!double.IsFinite(totalLength) || totalLength <= 0 ||
                !double.IsFinite(unitMass) || unitMass <= 0 || !double.IsFinite(mass) || mass < 0)
                return Block("SCHEDULE_MASS_INVALID", "Schedule length or mass overflows.", group.First().Bar.Sources.FirstOrDefault());
            rows.Add(new SteelScheduleRow(marks[key], key.ShapeSignature, key.ComponentKind, key.Layer,
                key.Axis, key.DiameterMm, quantity, key.SingleLengthMm, totalLength, unitMass,
                new MassKg(mass), group.Select(x => x.Bar.Id).Order(StringComparer.Ordinal), key.ScheduleLengthPolicy,
                key.Direction, key.FrameId, key.Surface, key.FamilyId));
        }
        double grand = rows.Sum(r => r.MassKg.Value);
        if (!double.IsFinite(grand)) return Block("SCHEDULE_MASS_INVALID", "Grand total overflows.");
        var summary = rows.GroupBy(r => r.ComponentKind)
            .Select(g => new KeyValuePair<ComponentKind, MassKg>(g.Key, new MassKg(g.Sum(r => r.MassKg.Value))));
        return new(true, new SteelSchedule(rows, summary, new MassKg(grand),
            massPolicy.Id.ToString(), massPolicy.Version), []);
    }

    private static OperationResult<SteelSchedule> Block(string code, string message, SourceRef? source = null) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, source, null, null, message)]);
}
