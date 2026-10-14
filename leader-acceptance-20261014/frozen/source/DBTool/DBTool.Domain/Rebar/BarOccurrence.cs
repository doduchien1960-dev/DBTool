using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public enum LengthStatus { Known, NeedsRule }

public sealed record LengthResult
{
    public LengthResult(double scheduleLengthMm, double? fabricationLengthMm, LengthStatus lengthStatus, IEnumerable<Diagnostic> diagnostics)
    {
        ScheduleLengthMm = ContractGuard.Positive(scheduleLengthMm, nameof(scheduleLengthMm));
        FabricationLengthMm = fabricationLengthMm;
        LengthStatus = ContractGuard.EnumValue(lengthStatus);
        Diagnostics = ContractGuard.Copy(diagnostics);
        if (fabricationLengthMm.HasValue) ContractGuard.Positive(fabricationLengthMm.Value, nameof(fabricationLengthMm));
        if ((lengthStatus == LengthStatus.Known) != fabricationLengthMm.HasValue) throw new ArgumentException("Length status mismatch.");
        if (lengthStatus == LengthStatus.NeedsRule && Diagnostics.IsEmpty) throw new ArgumentException("Missing rule needs diagnostic.");
    }

    public double ScheduleLengthMm { get; }
    public double? FabricationLengthMm { get; }
    public LengthStatus LengthStatus { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

public sealed record BarOccurrence
{
    public BarOccurrence(
        string id,
        string? parentId,
        string componentId,
        ComponentKind componentKind,
        Layer layer,
        Axis axis,
        int diameterMm,
        BarPath path,
        double scheduleLengthMm,
        double? fabricationLengthMm,
        LengthStatus lengthStatus,
        string ruleId,
        IEnumerable<SourceRef> sources,
        RebarDirection direction = RebarDirection.Supplementary,
        string? frameId = null,
        RebarSurface? surface = null,
        string? familyId = null,
        MeasurementBasisKind? measurementBasis = null,
        double? innerRadiusMm = null,
        int? stationIndex = null,
        double? stationMm = null,
        string? assemblyId = null,
        string? panelInstanceId = null,
        string? regionRole = null,
        IEnumerable<string>? openingIds = null,
        double? straightRunMm = null,
        string? shapeRecipeId = null,
        string? specId = null,
        string? stationKey = null,
        string? fabricationBasis = null)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ParentId = parentId;
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        ComponentKind = ContractGuard.EnumValue(componentKind);
        Layer = ContractGuard.EnumValue(layer);
        Axis = ContractGuard.EnumValue(axis);
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        Path = ContractGuard.Required(path);
        ScheduleLengthMm = ContractGuard.Positive(scheduleLengthMm, nameof(scheduleLengthMm));
        FabricationLengthMm = fabricationLengthMm;
        LengthStatus = ContractGuard.EnumValue(lengthStatus);
        RuleId = ContractGuard.Text(ruleId, nameof(ruleId));
        Sources = ContractGuard.Copy(sources);
        Direction = ContractGuard.EnumValue(direction);
        FrameId = frameId;
        Surface = surface;
        FamilyId = familyId;
        MeasurementBasis = measurementBasis;
        InnerRadiusMm = innerRadiusMm;
        StationIndex = stationIndex;
        StationMm = stationMm;
        AssemblyId = assemblyId;
        PanelInstanceId = panelInstanceId;
        RegionRole = regionRole;
        OpeningIds = openingIds is null ? ImmutableArray<string>.Empty : openingIds.ToImmutableArray();
        StraightRunMm = straightRunMm;
        ShapeRecipeId = shapeRecipeId;
        SpecId = specId;
        StationKey = stationKey;
        FabricationBasis = fabricationBasis;

        if (innerRadiusMm.HasValue) ContractGuard.NonNegative(innerRadiusMm.Value, nameof(innerRadiusMm));
        if (fabricationLengthMm.HasValue) ContractGuard.Positive(fabricationLengthMm.Value, nameof(fabricationLengthMm));
        if ((lengthStatus == LengthStatus.Known) != fabricationLengthMm.HasValue) throw new ArgumentException("Length status mismatch.");
        if (parentId == id) throw new ArgumentException("Occurrence cannot parent itself.");
    }

    public string Id { get; }
    public string? ParentId { get; }
    public string ComponentId { get; }
    public string HostId => ComponentId;
    public ComponentKind ComponentKind { get; }
    public Layer Layer { get; }
    public Axis Axis { get; }
    public int DiameterMm { get; }
    public BarPath Path { get; }
    public double ScheduleLengthMm { get; }
    public double? FabricationLengthMm { get; }
    public LengthStatus LengthStatus { get; }
    public string RuleId { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public RebarDirection Direction { get; }
    public string? FrameId { get; }
    public RebarSurface? Surface { get; }
    public string? FamilyId { get; }
    public MeasurementBasisKind? MeasurementBasis { get; }
    public double? InnerRadiusMm { get; }
    public int? StationIndex { get; }
    public double? StationMm { get; }
    public string? AssemblyId { get; }
    public string? PanelInstanceId { get; }
    public string? RegionRole { get; }
    public ImmutableArray<string> OpeningIds { get; }
    public double? StraightRunMm { get; }
    public string? ShapeRecipeId { get; }
    public string? SpecId { get; }
    public string? StationKey { get; }
    public string? FabricationBasis { get; }
}
