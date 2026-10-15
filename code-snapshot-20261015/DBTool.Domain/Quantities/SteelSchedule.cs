using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public sealed record SteelScheduleRow
{
    public SteelScheduleRow(string mark, string shapeSignature, ComponentKind componentKind, Layer layer, Axis axis, int diameterMm, int quantity, double singleLengthMm, double totalLengthMm, double unitMassKgPerM, MassKg massKg, IEnumerable<string> occurrenceIds, string ruleId,
        RebarDirection direction = RebarDirection.Supplementary, string? frameId = null,
        RebarSurface? surface = null, string? familyId = null)
    {
        Mark = ContractGuard.Text(mark, nameof(mark));
        ShapeSignature = ContractGuard.Text(shapeSignature, nameof(shapeSignature));
        ComponentKind = ContractGuard.EnumValue(componentKind);
        Layer = ContractGuard.EnumValue(layer);
        Axis = ContractGuard.EnumValue(axis);
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        Quantity = ContractGuard.Positive(quantity, nameof(quantity));
        SingleLengthMm = ContractGuard.Positive(singleLengthMm, nameof(singleLengthMm));
        TotalLengthMm = ContractGuard.Positive(totalLengthMm, nameof(totalLengthMm));
        UnitMassKgPerM = ContractGuard.Positive(unitMassKgPerM, nameof(unitMassKgPerM));
        MassKg = massKg;
        OccurrenceIds = ContractGuard.Copy(occurrenceIds);
        RuleId = ContractGuard.Text(ruleId, nameof(ruleId));
        Direction = ContractGuard.EnumValue(direction);
        FrameId = frameId;
        Surface = surface;
        FamilyId = familyId;
        if(OccurrenceIds.Length!=quantity || OccurrenceIds.Distinct().Count()!=quantity) throw new ArgumentException("Row count mismatch.");
        if(Math.Abs(totalLengthMm-quantity*singleLengthMm)>1e-6) throw new ArgumentException("Row length mismatch.");
        if(Math.Abs(massKg.Value-totalLengthMm/1000*unitMassKgPerM)>1e-9) throw new ArgumentException("Row mass mismatch.");
    }
    public string Mark { get; }
    public string ShapeSignature { get; }
    public ComponentKind ComponentKind { get; }
    public Layer Layer { get; }
    public Axis Axis { get; }
    public int DiameterMm { get; }
    public int Quantity { get; }
    public double SingleLengthMm { get; }
    public double TotalLengthMm { get; }
    public double UnitMassKgPerM { get; }
    public MassKg MassKg { get; }
    public ImmutableArray<string> OccurrenceIds { get; }
    public string RuleId { get; }
    public RebarDirection Direction { get; }
    public string? FrameId { get; }
    public RebarSurface? Surface { get; }
    public string? FamilyId { get; }
}
public sealed record SteelSchedule
{
    public SteelSchedule(IEnumerable<SteelScheduleRow> rows, IEnumerable<KeyValuePair<ComponentKind,MassKg>> summaryByComponent, MassKg totalMassKg, string massPolicyId, string massPolicyVersion)
    {
        Rows = ContractGuard.Copy(rows);
        SummaryByComponent = summaryByComponent.ToImmutableDictionary();
        TotalMassKg = totalMassKg;
        MassPolicyId = ContractGuard.Text(massPolicyId, nameof(massPolicyId));
        MassPolicyVersion = ContractGuard.Text(massPolicyVersion, nameof(massPolicyVersion));
        var ids=Rows.SelectMany(r=>r.OccurrenceIds).ToArray();
        if(ids.Distinct().Count()!=ids.Length || Rows.Select(r=>r.Mark).Distinct().Count()!=Rows.Length) throw new ArgumentException("Duplicate schedule identity.");
        if(Math.Abs(Rows.Sum(r=>r.MassKg.Value)-totalMassKg.Value)>1e-9) throw new ArgumentException("Schedule total mismatch.");
        var groups=Rows.GroupBy(r=>r.ComponentKind).ToArray();
        if(groups.Length!=SummaryByComponent.Count || groups.Any(g=>!SummaryByComponent.TryGetValue(g.Key,out var mass) || Math.Abs(g.Sum(r=>r.MassKg.Value)-mass.Value)>1e-9)) throw new ArgumentException("Component summary mismatch.");
    }
    public ImmutableArray<SteelScheduleRow> Rows { get; }
    public ImmutableDictionary<ComponentKind,MassKg> SummaryByComponent { get; }
    public MassKg TotalMassKg { get; }
    public string MassPolicyId { get; }
    public string MassPolicyVersion { get; }
}
