using System.Collections.Immutable;

namespace DBTool.Domain;

public sealed record AngleScheduleRow
{
    public AngleScheduleRow(string mark, string catalogCode, string componentId, int quantity, double singleLengthMm,
        double totalLengthMm, double unitMassKgPerM, MassKg massKg, IEnumerable<string> occurrenceIds)
    {
        Mark = ContractGuard.Text(mark, nameof(mark)); CatalogCode = ContractGuard.Text(catalogCode, nameof(catalogCode));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId)); Quantity = quantity;
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        SingleLengthMm = ContractGuard.Positive(singleLengthMm, nameof(singleLengthMm)); TotalLengthMm = ContractGuard.Positive(totalLengthMm, nameof(totalLengthMm));
        UnitMassKgPerM = ContractGuard.Positive(unitMassKgPerM, nameof(unitMassKgPerM)); MassKg = massKg;
        OccurrenceIds = ContractGuard.Copy(occurrenceIds);
        if (OccurrenceIds.Length != quantity || Math.Abs(totalLengthMm - quantity * singleLengthMm) > 1e-6 ||
            Math.Abs(massKg.Value - totalLengthMm / 1000d * unitMassKgPerM) > 1e-9)
            throw new ArgumentException("Angle schedule row arithmetic mismatch.");
    }
    public string Mark { get; }
    public string CatalogCode { get; }
    public string ComponentId { get; }
    public int Quantity { get; }
    public double SingleLengthMm { get; }
    public double TotalLengthMm { get; }
    public double UnitMassKgPerM { get; }
    public MassKg MassKg { get; }
    public ImmutableArray<string> OccurrenceIds { get; }
}

public sealed record AngleSchedule
{
    public AngleSchedule(IEnumerable<AngleScheduleRow> rows, MassKg totalMassKg, string version = "ANGLE-SCHEDULE-1")
    {
        Rows = ContractGuard.Copy(rows); TotalMassKg = totalMassKg; Version = ContractGuard.Text(version, nameof(version));
        if (Rows.Select(x => x.Mark).Distinct().Count() != Rows.Length || Math.Abs(Rows.Sum(x => x.MassKg.Value) - totalMassKg.Value) > 1e-9)
            throw new ArgumentException("Angle schedule total mismatch.");
    }
    public ImmutableArray<AngleScheduleRow> Rows { get; }
    public MassKg TotalMassKg { get; }
    public string Version { get; }
}

public static class AngleScheduleCalculator
{
    public static OperationResult<AngleSchedule> Calculate(IReadOnlyList<AngleOccurrence> occurrences)
    {
        if (occurrences is null || occurrences.Count == 0)
            return new(true, new AngleSchedule([], new MassKg(0)), []);
        if (occurrences.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            return new(false, null, [new Diagnostic("ANGLE_SCHEDULE_DUPLICATE_OCCURRENCE", DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, null, null, null, "Angle occurrence IDs must be unique.")]);
        var rows = occurrences.GroupBy(x => (x.CatalogCode, x.ComponentId, x.LengthMm, x.UnitMassKgPerM))
            .OrderBy(x => x.Key.CatalogCode, StringComparer.Ordinal).ThenBy(x => x.Key.ComponentId, StringComparer.Ordinal).ThenBy(x => x.Key.LengthMm)
            .Select((group, index) => new AngleScheduleRow($"A{index + 1:000}", group.Key.CatalogCode, group.Key.ComponentId, group.Count(),
                group.Key.LengthMm, group.Count() * group.Key.LengthMm, group.Key.UnitMassKgPerM,
                new MassKg(group.Count() * group.Key.LengthMm / 1000d * group.Key.UnitMassKgPerM), group.Select(x => x.Id).Order(StringComparer.Ordinal)))
            .ToArray();
        return new(true, new AngleSchedule(rows, new MassKg(rows.Sum(x => x.MassKg.Value))), []);
    }
}
