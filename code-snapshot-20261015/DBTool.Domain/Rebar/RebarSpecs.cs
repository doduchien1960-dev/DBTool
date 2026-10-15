using System.Collections.Immutable;
namespace DBTool.Domain;
public enum RebarKind { Mesh, Counted, Anchor, Disabled }
public enum ParseContext { MeshRequired, MeshOptional, AnchorOptional, LiftingHook }
public sealed record MeshSpec
{
    public MeshSpec(int diameterMm, double spacingMm)
    {
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        SpacingMm = ContractGuard.Positive(spacingMm, nameof(spacingMm));

    }
    public int DiameterMm { get; }
    public double SpacingMm { get; }
}
public sealed record CountedBarSpec
{
    public CountedBarSpec(int count, int diameterMm)
    {
        Count = count;
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
    }
    public int Count { get; }
    public int DiameterMm { get; }
}
public sealed record AnchorSpec
{
    public AnchorSpec(int diameterMm, double spacingMm, double lengthMm)
    {
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        SpacingMm = ContractGuard.Positive(spacingMm, nameof(spacingMm));
        LengthMm = ContractGuard.Positive(lengthMm, nameof(lengthMm));

    }
    public int DiameterMm { get; }
    public double SpacingMm { get; }
    public double LengthMm { get; }
}
public sealed record ParsedRebar
{
    public ParsedRebar(RebarKind kind, MeshSpec? mesh, CountedBarSpec? counted, AnchorSpec? anchor, SourceRef source)
    {
        Kind = ContractGuard.EnumValue(kind);
        Mesh = mesh;
        Counted = counted;
        Anchor = anchor;
        Source = ContractGuard.Required(source);
        bool valid = kind switch {
            RebarKind.Mesh => mesh != null && counted == null && anchor == null,
            RebarKind.Counted => mesh == null && counted is { Count: > 0 } && anchor == null,
            RebarKind.Anchor => mesh == null && counted == null && anchor != null,
            RebarKind.Disabled => mesh == null && counted == null && anchor == null,
            _ => false };
        if (!valid) throw new ArgumentException("Rebar payload does not match its kind.");
    }
    public RebarKind Kind { get; }
    public MeshSpec? Mesh { get; }
    public CountedBarSpec? Counted { get; }
    public AnchorSpec? Anchor { get; }
    public SourceRef Source { get; }
}

