using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record ClearancePolicy
{
    public ClearancePolicy(double coverToHoleMm, double toleranceMm = 1e-6)
    {
        CoverToHoleMm = ContractGuard.NonNegative(coverToHoleMm, nameof(coverToHoleMm));
        ToleranceMm = ContractGuard.Positive(toleranceMm, nameof(toleranceMm));
    }
    public double CoverToHoleMm { get; }
    public double ToleranceMm { get; }
}

public enum ClipMode { SingleRound, Box, Multiple }
public enum ClipPlane { WorldXY, WallLocalUZ }

public sealed record ClippingPolicy
{
    public ClippingPolicy(ClipMode mode, double toleranceMm)
    { Mode=mode; ToleranceMm=toleranceMm; }
    public ClipMode Mode { get; }
    public double ToleranceMm { get; }
}

/// <summary>One physical concrete opening and its explicit bar-plane coordinates.</summary>
public sealed record RoundExclusion
{
    private RoundExclusion(string ownerComponentId, Point2Mm centerMm, double concreteRadiusMm,
        double coverToHoleMm, int barDiameterMm, ClipPlane plane, WallFrame? wallFrame,
        Interval1D? wallDepthMm, double? worldPlaneZmm, SourceRef? source, string? manholeId)
    {
        OwnerComponentId=ownerComponentId; CenterMm=centerMm; ConcreteRadiusMm=concreteRadiusMm;
        CoverToHoleMm=coverToHoleMm; BarDiameterMm=barDiameterMm; Plane=plane;
        WallFrame=wallFrame; WallDepthMm=wallDepthMm; WorldPlaneZmm=worldPlaneZmm;
        Source=source; ManholeId=manholeId;
    }
    public static RoundExclusion ForWall(string ownerComponentId, WallFrame frame, Interval1D depthMm,
        Point2Mm centerUzMm, double concreteRadiusMm, double coverToHoleMm, int barDiameterMm,
        SourceRef? source=null, string? manholeId=null) =>
        new(ownerComponentId,centerUzMm,concreteRadiusMm,coverToHoleMm,barDiameterMm,
            ClipPlane.WallLocalUZ,frame,depthMm,null,source,manholeId);
    public static RoundExclusion ForWorldXY(string ownerComponentId, double planeZmm,
        Point2Mm centerXyMm, double concreteRadiusMm, double coverToHoleMm, int barDiameterMm,
        SourceRef? source=null, string? manholeId=null) =>
        new(ownerComponentId,centerXyMm,concreteRadiusMm,coverToHoleMm,barDiameterMm,
            ClipPlane.WorldXY,null,null,planeZmm,source,manholeId);
    public string OwnerComponentId { get; }
    public Point2Mm CenterMm { get; }
    public double ConcreteRadiusMm { get; }
    public double CoverToHoleMm { get; }
    public int BarDiameterMm { get; }
    public ClipPlane Plane { get; }
    public WallFrame? WallFrame { get; }
    public Interval1D? WallDepthMm { get; }
    public double? WorldPlaneZmm { get; }
    public SourceRef? Source { get; }
    public string? ManholeId { get; }
}

public sealed record ClipResult
{
    public ClipResult(IEnumerable<BarOccurrence> retained, double removedLengthMm, double originalLengthMm)
    {
        Retained=ContractGuard.Copy(retained);
        RemovedLengthMm=ContractGuard.NonNegative(removedLengthMm,nameof(removedLengthMm));
        OriginalLengthMm=ContractGuard.Positive(originalLengthMm,nameof(originalLengthMm));
        if (RemovedLengthMm > OriginalLengthMm + 1e-6 ||
            Math.Abs(Retained.Sum(b=>b.ScheduleLengthMm)+RemovedLengthMm-OriginalLengthMm)>1e-6)
            throw new ArgumentException("Clip length is not conserved.");
    }
    public ImmutableArray<BarOccurrence> Retained { get; }
    public double RemovedLengthMm { get; }
    public double OriginalLengthMm { get; }
}
