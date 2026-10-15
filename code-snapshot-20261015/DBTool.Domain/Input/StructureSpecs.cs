using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public enum HaunchOwner { Base, Roof }
public enum NeckPosition { Center, North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest }
public enum NeckSeatKind { None, Step, Slope }
public enum SplitAxis { X, Y }
public enum PanelSupportMode { Recess, OverOuter }
public enum CoverKind { None, Purchased, Panel }
public enum CoverPlacementDatum { NeckTop, SeatPlane }

public static class CoverKindMap
{
    public static bool TryParse(string? label,out CoverKind kind)
    {
        kind=label?.Trim().Normalize().ToUpperInvariant() switch
        {
            null or "" or "KHÔNG" or "NONE"=>CoverKind.None,
            "GANG" or "PURCHASED"=>CoverKind.Purchased,
            "BTCT" or "PANEL"=>CoverKind.Panel,
            _=>(CoverKind)(-1)
        };
        return Enum.IsDefined(kind);
    }
}

public static class NeckPositionMap
{
    public static string Name(NeckPosition position) => position switch
    {
        NeckPosition.Center=>"Center",NeckPosition.North=>"North",NeckPosition.East=>"East",
        NeckPosition.South=>"South",NeckPosition.West=>"West",NeckPosition.NorthEast=>"NorthEast",
        NeckPosition.SouthEast=>"SouthEast",NeckPosition.SouthWest=>"SouthWest",NeckPosition.NorthWest=>"NorthWest",
        _=>throw new ArgumentOutOfRangeException(nameof(position))
    };
    public static bool TryParse(string? name, out NeckPosition position)
    {
        position=name?.Trim() switch
        {
            "Center" or "Giữa ga" => NeckPosition.Center,
            "North" or "Giữa T1" => NeckPosition.North,
            "East" or "Giữa T2" => NeckPosition.East,
            "South" or "Giữa T3" => NeckPosition.South,
            "West" or "Giữa T4" => NeckPosition.West,
            "NorthEast" or "Góc T1-T2" => NeckPosition.NorthEast,
            "SouthEast" or "Góc T2-T3" => NeckPosition.SouthEast,
            "SouthWest" or "Góc T3-T4" => NeckPosition.SouthWest,
            "NorthWest" or "Góc T4-T1" => NeckPosition.NorthWest,
            _ => (NeckPosition)(-1)
        };
        return Enum.IsDefined(position);
    }
    public static (double X,double Y) Center(NeckPosition position,double chamberLengthMm,double chamberWidthMm,double innerWidthMm,double innerLengthMm)
    {
        ContractGuard.EnumValue(position);
        var ax=(ContractGuard.Positive(chamberLengthMm,nameof(chamberLengthMm))-ContractGuard.Positive(innerWidthMm,nameof(innerWidthMm)))/2;
        var ay=(ContractGuard.Positive(chamberWidthMm,nameof(chamberWidthMm))-ContractGuard.Positive(innerLengthMm,nameof(innerLengthMm)))/2;
        if(ax<0 || ay<0) throw new ArgumentException("Neck opening exceeds chamber clear size.");
        return position switch
        {
            NeckPosition.Center => (0,0), NeckPosition.North => (0,ay), NeckPosition.East => (ax,0),
            NeckPosition.South => (0,-ay), NeckPosition.West => (-ax,0),
            NeckPosition.NorthEast => (ax,ay), NeckPosition.SouthEast => (ax,-ay),
            NeckPosition.SouthWest => (-ax,-ay), NeckPosition.NorthWest => (-ax,ay),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
    }
}

public static class PanelDirectionMap
{
    public static bool TryParse(string? label,out SplitAxis axis)
    {
        axis=label?.Trim() switch { "X" or "Dọc"=>SplitAxis.X,"Y" or "Ngang"=>SplitAxis.Y,_=>(SplitAxis)(-1) };
        return Enum.IsDefined(axis);
    }
}

/// <summary>Independent candidate detailing. Anchorage remains provisional until its rule is confirmed.</summary>
public sealed record HaunchRebarSpec
{
    public HaunchRebarSpec(int inclinedDiameterMm=10,double inclinedSpacingMm=150,int distributionDiameterMm=8,
        double slabAnchorMm=300,double wallAnchorMm=300)
    {
        InclinedMesh=new MeshSpec(inclinedDiameterMm,inclinedSpacingMm);
        DistributionDiameterMm=ContractGuard.Positive(distributionDiameterMm,nameof(distributionDiameterMm));
        SlabAnchorMm=ContractGuard.Positive(slabAnchorMm,nameof(slabAnchorMm));
        WallAnchorMm=ContractGuard.Positive(wallAnchorMm,nameof(wallAnchorMm));
    }
    public MeshSpec InclinedMesh {get;}
    public int DistributionDiameterMm {get;}
    public double SlabAnchorMm {get;}
    public double WallAnchorMm {get;}
    public string RuleVersion => "HAUNCH-PROPOSAL-20261012-v1";
}

public sealed record HaunchSpec
{
    public HaunchSpec(double widthMm,double heightMm,HaunchOwner owner,IEnumerable<SourceRef> sources,HaunchRebarSpec? rebar=null)
    {
        WidthMm=ContractGuard.NonNegative(widthMm,nameof(widthMm)); HeightMm=ContractGuard.NonNegative(heightMm,nameof(heightMm));
        Owner=ContractGuard.EnumValue(owner); Sources=ContractGuard.Copy(sources); Rebar=rebar;
        if ((widthMm==0)!=(heightMm==0)) throw new ArgumentException("Haunch width and height must both be zero or positive.");
    }
    public double WidthMm { get; }
    public double HeightMm { get; }
    public HaunchOwner Owner { get; }
    public HaunchRebarSpec? Rebar {get;}
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record NeckSpec
{
    public NeckSpec(double innerWidthMm,double innerLengthMm,double thicknessMm,double heightMm,NeckPosition position,IEnumerable<SourceRef> sources)
    {
        InnerWidthMm=ContractGuard.Positive(innerWidthMm,nameof(innerWidthMm)); InnerLengthMm=ContractGuard.Positive(innerLengthMm,nameof(innerLengthMm));
        ThicknessMm=ContractGuard.Positive(thicknessMm,nameof(thicknessMm)); HeightMm=ContractGuard.NonNegative(heightMm,nameof(heightMm));
        Position=ContractGuard.EnumValue(position); Sources=ContractGuard.Copy(sources);
    }
    public double InnerWidthMm { get; }
    public double InnerLengthMm { get; }
    public double ThicknessMm { get; }
    public double HeightMm { get; }
    public NeckPosition Position { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record NeckSeatSpec
{
    public NeckSeatSpec(NeckSeatKind kind,double expansionMm,double heightMm,double seatElevationMm,IEnumerable<SourceRef> sources)
    {
        Kind=ContractGuard.EnumValue(kind); ExpansionMm=ContractGuard.NonNegative(expansionMm,nameof(expansionMm));
        HeightMm=ContractGuard.NonNegative(heightMm,nameof(heightMm)); SeatElevationMm=ContractGuard.Finite(seatElevationMm,nameof(seatElevationMm));
        Sources=ContractGuard.Copy(sources);
        if (kind==NeckSeatKind.None && (expansionMm!=0 || heightMm!=0) || kind!=NeckSeatKind.None && heightMm<=0)
            throw new ArgumentException("Seat parameters disagree with kind.");
    }
    public NeckSeatKind Kind { get; }
    public double ExpansionMm { get; }
    public double HeightMm { get; }
    public double SeatElevationMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record CollarSpec
{
    public CollarSpec(Profile2Mm outerProfile,Profile2Mm openingProfile,Interval1D zRange,string materialId,IEnumerable<SourceRef> sources)
    {
        OuterProfile=ContractGuard.Required(outerProfile); OpeningProfile=ContractGuard.Required(openingProfile);
        if(zRange.Length<=0) throw new ArgumentException("Collar height must be positive.");
        ZRange=zRange; MaterialId=ContractGuard.Text(materialId,nameof(materialId)); Sources=ContractGuard.Copy(sources);
    }
    public Profile2Mm OuterProfile { get; }
    public Profile2Mm OpeningProfile { get; }
    public Interval1D ZRange { get; }
    public string MaterialId { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record CoverPanelSpec
{
    public CoverPanelSpec(int count,double gapMm,SplitAxis splitAxis,PanelSupportMode supportMode,double capHeightMm,IEnumerable<SourceRef> sources)
    {
        if(count<1) throw new ArgumentOutOfRangeException(nameof(count));
        Count=count; GapMm=ContractGuard.NonNegative(gapMm,nameof(gapMm)); SplitAxis=ContractGuard.EnumValue(splitAxis);
        SupportMode=ContractGuard.EnumValue(supportMode); CapHeightMm=ContractGuard.Positive(capHeightMm,nameof(capHeightMm));
        Sources=ContractGuard.Copy(sources);
    }
    public int Count { get; }
    public double GapMm { get; }
    public SplitAxis SplitAxis { get; }
    public PanelSupportMode SupportMode { get; }
    public double CapHeightMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record PurchasedCoverSpec
{
    public PurchasedCoverSpec(string catalogId,Point3Mm anchor,Dimensions2Mm physicalSize,IEnumerable<SourceRef> sources,double rotationDegrees=0)
    { CatalogId=ContractGuard.Text(catalogId,nameof(catalogId)); Anchor=anchor; PhysicalSize=ContractGuard.Required(physicalSize); Sources=ContractGuard.Copy(sources); RotationDegrees=ContractGuard.Finite(rotationDegrees,nameof(rotationDegrees)); }
    public string CatalogId { get; }
    public Point3Mm Anchor { get; }
    public Dimensions2Mm PhysicalSize { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public double RotationDegrees { get; }
}
