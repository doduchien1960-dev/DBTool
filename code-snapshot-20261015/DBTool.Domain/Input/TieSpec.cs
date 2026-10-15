using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public enum TieLatticeMode { Aligned, Staggered }
public enum LargeSpacingBehavior { Disable, KeepBoundaryPoints, Block }

/// <summary>Construction-independent tie shape information. A null bend radius is unresolved evidence.</summary>
public sealed record TieShapeSpec
{
    public TieShapeSpec(int diameterMm, double? bendRadiusMm = null,
        HookPattern hookPattern = HookPattern.Hook0M, double? hookLengthMm = null)
    {
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        BendRadiusMm = bendRadiusMm;
        if (bendRadiusMm.HasValue) ContractGuard.NonNegative(bendRadiusMm.Value, nameof(bendRadiusMm));
        HookPattern = ContractGuard.EnumValue(hookPattern);
        HookLengthMm = hookLengthMm;
        if (hookLengthMm.HasValue) ContractGuard.Positive(hookLengthMm.Value, nameof(hookLengthMm));
    }

    public int DiameterMm { get; }
    public double? BendRadiusMm { get; }
    public HookPattern HookPattern { get; }
    public double? HookLengthMm { get; }
}

public sealed record TieSpec
{
    public TieSpec(double spacingUMm, double spacingVMm, Point2Mm origin, TieLatticeMode mode,
        TieShapeSpec? shape, double edgeMarginMm, LargeSpacingBehavior largeSpacingBehavior,
        double openingClearanceMm, IEnumerable<SourceRef>? sources = null)
    {
        SpacingUMm = ContractGuard.Positive(spacingUMm, nameof(spacingUMm));
        SpacingVMm = ContractGuard.Positive(spacingVMm, nameof(spacingVMm));
        Origin = origin;
        Mode = ContractGuard.EnumValue(mode);
        Shape = shape;
        EdgeMarginMm = ContractGuard.NonNegative(edgeMarginMm, nameof(edgeMarginMm));
        LargeSpacingBehavior = ContractGuard.EnumValue(largeSpacingBehavior);
        OpeningClearanceMm = ContractGuard.NonNegative(openingClearanceMm, nameof(openingClearanceMm));
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }

    public TieSpec(double spacingUMm, double spacingVMm, Point2Mm origin, TieLatticeMode mode,
        TieShapeSpec? shape, double edgeMarginMm, LargeSpacingBehavior largeSpacingBehavior)
        : this(spacingUMm, spacingVMm, origin, mode, shape, edgeMarginMm, largeSpacingBehavior, 0) { }

    public double SpacingUMm { get; }
    public double SpacingVMm { get; }
    public Point2Mm Origin { get; }
    public TieLatticeMode Mode { get; }
    public TieShapeSpec? Shape { get; }
    public double EdgeMarginMm { get; }
    public LargeSpacingBehavior LargeSpacingBehavior { get; }
    public double OpeningClearanceMm { get; }
    public System.Collections.Immutable.ImmutableArray<SourceRef> Sources { get; }
}
