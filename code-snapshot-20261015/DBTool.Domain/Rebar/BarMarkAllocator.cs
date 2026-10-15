using System.Collections.Immutable;

namespace DBTool.Domain;

public sealed record MarkPolicy
{
    public MarkPolicy(CalculationScope scope) => Scope = ContractGuard.EnumValue(scope);
    public CalculationScope Scope { get; }
}

public sealed record ScheduleGroupKey
{
    public ScheduleGroupKey(ComponentKind componentKind, Layer layer, Axis axis, int diameterMm,
        string shapeSignature, string scheduleLengthPolicy, double singleLengthMm,
        RebarDirection direction = RebarDirection.Supplementary, string? frameId = null,
        RebarSurface? surface = null, string? familyId = null)
    {
        ComponentKind = ContractGuard.EnumValue(componentKind);
        Layer = ContractGuard.EnumValue(layer);
        Axis = ContractGuard.EnumValue(axis);
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        ShapeSignature = ContractGuard.Text(shapeSignature, nameof(shapeSignature));
        ScheduleLengthPolicy = ContractGuard.Text(scheduleLengthPolicy, nameof(scheduleLengthPolicy));
        SingleLengthMm = ContractGuard.Positive(singleLengthMm, nameof(singleLengthMm));
        Direction = ContractGuard.EnumValue(direction);
        FrameId = frameId;
        Surface = surface;
        FamilyId = familyId;
    }
    public ComponentKind ComponentKind { get; }
    public Layer Layer { get; }
    public Axis Axis { get; }
    public int DiameterMm { get; }
    public string ShapeSignature { get; }
    public string ScheduleLengthPolicy { get; }
    public double SingleLengthMm { get; }
    public RebarDirection Direction { get; }
    public string? FrameId { get; }
    public RebarSurface? Surface { get; }
    public string? FamilyId { get; }
}

public static class BarMarkAllocator
{
    public static ImmutableDictionary<ScheduleGroupKey, string> Allocate(
        IReadOnlyCollection<ScheduleGroupKey> canonicalGroups, CalculationScope scope)
    {
        ArgumentNullException.ThrowIfNull(canonicalGroups);
        ContractGuard.EnumValue(scope);
        var ordered = canonicalGroups.Distinct().OrderBy(k => k.ComponentKind)
            .ThenBy(k => k.Layer).ThenBy(k => k.Axis).ThenBy(k => k.DiameterMm)
            .ThenBy(k => k.Direction).ThenBy(k => k.FrameId, StringComparer.Ordinal)
            .ThenBy(k => k.Surface).ThenBy(k => k.FamilyId, StringComparer.Ordinal)
            .ThenBy(k => k.SingleLengthMm).ThenBy(k => k.ShapeSignature, StringComparer.Ordinal)
            .ThenBy(k => k.ScheduleLengthPolicy, StringComparer.Ordinal).ToArray();
        var builder = ImmutableDictionary.CreateBuilder<ScheduleGroupKey, string>();
        for (int i = 0; i < ordered.Length; i++) builder.Add(ordered[i], $"D{i + 1:000}");
        return builder.ToImmutable();
    }
}
