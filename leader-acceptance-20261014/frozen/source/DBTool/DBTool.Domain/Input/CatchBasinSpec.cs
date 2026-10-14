using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public enum CatchBasinOwner { Body, Neck }
public enum HorizontalPosition { Left, Center, Right }

public sealed record CatchBasinSpec
{
    public CatchBasinSpec(CatchBasinOwner owner, WallId wall, HorizontalPosition position,
        double openingWidthMm, double openingHeightMm, double chamberClearWidthMm,
        double chamberClearDepthMm, double chamberClearHeightMm, double wallThicknessMm,
        double baseThicknessMm, double offsetMm, IEnumerable<KeyValuePair<string, SourceRef>> sourceRefs)
        : this(owner, wall, position, openingWidthMm, openingHeightMm, chamberClearWidthMm,
               chamberClearDepthMm, chamberClearHeightMm, wallThicknessMm, baseThicknessMm, offsetMm,
               sourceRefs, 0, 0, false, null, null)
    {
    }

    public CatchBasinSpec(CatchBasinOwner owner, WallId wall, HorizontalPosition position,
        double openingWidthMm, double openingHeightMm, double chamberClearWidthMm,
        double chamberClearDepthMm, double chamberClearHeightMm, double wallThicknessMm,
        double baseThicknessMm, double offsetMm, IEnumerable<KeyValuePair<string, SourceRef>> sourceRefs,
        double padThicknessMm, double padExpansionMm, bool reinforced = false,
        string? productCode = null, HorizontalPosition? rawPosition = null)
    {
        Owner = ContractGuard.EnumValue(owner);
        Wall = ContractGuard.EnumValue(wall);
        RawPosition = rawPosition ?? ContractGuard.EnumValue(position);
        Position = owner == CatchBasinOwner.Neck ? HorizontalPosition.Center : RawPosition;
        OpeningWidthMm = ContractGuard.Positive(openingWidthMm, nameof(openingWidthMm));
        OpeningHeightMm = ContractGuard.Positive(openingHeightMm, nameof(openingHeightMm));
        ChamberClearWidthMm = ContractGuard.Positive(chamberClearWidthMm, nameof(chamberClearWidthMm));
        ChamberClearDepthMm = ContractGuard.Positive(chamberClearDepthMm, nameof(chamberClearDepthMm));
        ChamberClearHeightMm = ContractGuard.Positive(chamberClearHeightMm, nameof(chamberClearHeightMm));
        WallThicknessMm = ContractGuard.Positive(wallThicknessMm, nameof(wallThicknessMm));
        BaseThicknessMm = ContractGuard.Positive(baseThicknessMm, nameof(baseThicknessMm));
        OffsetMm = ContractGuard.Finite(offsetMm, nameof(offsetMm));
        PadThicknessMm = ContractGuard.NonNegative(padThicknessMm, nameof(padThicknessMm));
        PadExpansionMm = ContractGuard.NonNegative(padExpansionMm, nameof(padExpansionMm));
        Reinforced = reinforced;
        ProductCode = productCode;
        SourceRefs = ContractGuard.Map(sourceRefs);
    }
    public CatchBasinOwner Owner { get; }
    public WallId Wall { get; }
    public HorizontalPosition Position { get; }
    public HorizontalPosition RawPosition { get; }
    public double OpeningWidthMm { get; }
    public double OpeningHeightMm { get; }
    public double ChamberClearWidthMm { get; }
    public double ChamberClearDepthMm { get; }
    public double ChamberClearHeightMm { get; }
    public double WallThicknessMm { get; }
    public double BaseThicknessMm { get; }
    public double OffsetMm { get; }
    public double PadThicknessMm { get; }
    public double PadExpansionMm { get; }
    public bool Reinforced { get; }
    public string? ProductCode { get; }
    public ImmutableDictionary<string, SourceRef> SourceRefs { get; }
}
