using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Drawing;

/// <summary>Typed, immutable snapshot passed from Application into the drawing boundary.</summary>
public sealed record DrawingQuantitySnapshot
{
    public DrawingQuantitySnapshot(CalculationStatus concreteStatus, IEnumerable<QuantityLine>? concreteLines,
        CalculationStatus formworkStatus, IEnumerable<QuantityLine>? formworkLines)
    {
        ConcreteStatus = ContractGuard.EnumValue(concreteStatus);
        ConcreteLines = concreteLines is null ? null : ContractGuard.Copy(concreteLines);
        FormworkStatus = ContractGuard.EnumValue(formworkStatus);
        FormworkLines = formworkLines is null ? null : ContractGuard.Copy(formworkLines);
        if ((ConcreteStatus == CalculationStatus.NotCalculated) != (ConcreteLines is null) ||
            (FormworkStatus == CalculationStatus.NotCalculated) != (FormworkLines is null))
            throw new ArgumentException("Quantity status and payload mismatch.");
    }

    public static DrawingQuantitySnapshot NotCalculated =>
        new(CalculationStatus.NotCalculated, null, CalculationStatus.NotCalculated, null);

    public CalculationStatus ConcreteStatus { get; }
    public ImmutableArray<QuantityLine>? ConcreteLines { get; }
    public CalculationStatus FormworkStatus { get; }
    public ImmutableArray<QuantityLine>? FormworkLines { get; }
}

/// <summary>Application-to-Drawing snapshot. It deliberately does not expose CalculationPackage.</summary>
public sealed record DrawingInputs
{
    public DrawingInputs(GeometryModel geometry, RebarLayout rebarLayout, SteelSchedule steelSchedule,
        AngleSchedule? angleSchedule, DrawingQuantitySnapshot quantities,
        WallOrNeckRebarSpec? wallOuter = null, WallOrNeckRebarSpec? wallInner = null,
        MeshSpec? baseTop = null, MeshSpec? baseBottom = null,
        MeshSpec? roofTop = null, MeshSpec? roofBottom = null, double? roofCoverMm = null,
        string? manholeId = null,
        NeckSpec? neckSpec = null, WallOrNeckRebarSpec? neckMesh = null, Profile? profile = null, double? dowelHoleDiameterMm = null,
        PresentationGroupMap? groupRegistry = null, string? groupRegistryHash = null,
        IReadOnlyDictionary<string, string>? sourceRefs = null)
    {
        Geometry = ContractGuard.Required(geometry);
        RebarLayout = ContractGuard.Required(rebarLayout);
        SteelSchedule = ContractGuard.Required(steelSchedule);
        AngleSchedule = angleSchedule;
        Quantities = ContractGuard.Required(quantities);
        WallOuter = wallOuter;
        WallInner = wallInner;
        BaseTop = baseTop;
        BaseBottom = baseBottom;
        RoofTop = roofTop;
        RoofBottom = roofBottom;
        if(roofCoverMm is {} cover && (!double.IsFinite(cover) || cover<0))
            throw new ArgumentOutOfRangeException(nameof(roofCoverMm));
        RoofCoverMm = roofCoverMm;
        ManholeId = manholeId;
        NeckSpec = neckSpec;
        NeckMesh = neckMesh;
        Profile = profile;
        DowelHoleDiameterMm = dowelHoleDiameterMm;
        GroupRegistry = groupRegistry;
        GroupRegistryHash = groupRegistryHash ?? groupRegistry?.RuleVersion;
        SourceRefs = sourceRefs is null ? null : sourceRefs.ToImmutableDictionary();
    }

    public GeometryModel Geometry { get; }
    public RebarLayout RebarLayout { get; }
    public SteelSchedule SteelSchedule { get; }
    public AngleSchedule? AngleSchedule { get; }
    public DrawingQuantitySnapshot Quantities { get; }
    public WallOrNeckRebarSpec? WallOuter { get; }
    public WallOrNeckRebarSpec? WallInner { get; }
    public MeshSpec? BaseTop { get; }
    public MeshSpec? BaseBottom { get; }
    public MeshSpec? RoofTop { get; }
    public MeshSpec? RoofBottom { get; }
    public double? RoofCoverMm { get; }
    public string? ManholeId { get; }
    public NeckSpec? NeckSpec { get; }
    public WallOrNeckRebarSpec? NeckMesh { get; }
    public Profile? Profile { get; }
    public double? DowelHoleDiameterMm { get; }
    public PresentationGroupMap? GroupRegistry { get; }
    public string? GroupRegistryHash { get; }
    public ImmutableDictionary<string, string>? SourceRefs { get; }
}
