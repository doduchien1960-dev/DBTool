using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Typed direction metadata for S2/S3 rebar families. Existing S1 Axis/Layer values remain unchanged.</summary>
public enum RebarDirection { Horizontal, Vertical, Perimeter, Supplementary }
public enum RebarSurface { Inner, Outer }
public enum StructureKind { ReinforcedConcrete, Other }
public enum LayoutStatus { Complete, Partial, Disabled, NotApplicable }

public sealed record MemberRebarLayerSpec
{
    public MemberRebarLayerSpec(string id, Layer layer, Axis axis, RebarDirection direction,
        int diameterMm, double spacingMm, string? frameId = null, RebarSurface? surface = null)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Layer = ContractGuard.EnumValue(layer);
        Axis = ContractGuard.EnumValue(axis);
        Direction = ContractGuard.EnumValue(direction);
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        SpacingMm = ContractGuard.Positive(spacingMm, nameof(spacingMm));
        FrameId = frameId;
        Surface = surface;
    }

    public string Id { get; }
    public Layer Layer { get; }
    public Axis Axis { get; }
    public RebarDirection Direction { get; }
    public int DiameterMm { get; }
    public double SpacingMm { get; }
    public string? FrameId { get; }
    public RebarSurface? Surface { get; }
}

/// <summary>Validated, immutable member rebar input. RawCell parsing belongs to the Application validator.</summary>
public sealed record MemberRebarSpec
{
    public MemberRebarSpec(IEnumerable<MemberRebarLayerSpec> layers, double coverMm = 30, string componentId = "Base")
    {
        Layers = ContractGuard.Copy(layers);
        if (Layers.IsEmpty) throw new ArgumentException("At least one member rebar layer is required.", nameof(layers));
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
    }

    public ImmutableArray<MemberRebarLayerSpec> Layers { get; }
    public double CoverMm { get; }
    public string ComponentId { get; }
}

public sealed record MeshZoneInput
{
    public MeshZoneInput(string id, string componentId, Axis axis, Interval1D distributionInterval,
        Interval1D runInterval, IEnumerable<double>? positionsMm = null, string? physicalIdentity = null,
        IEnumerable<SourceRef>? sources = null)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        Axis = ContractGuard.EnumValue(axis);
        DistributionInterval = distributionInterval;
        RunInterval = runInterval;
        PositionsMm = positionsMm is null ? ImmutableArray<double>.Empty : ContractGuard.Copy(positionsMm);
        PhysicalIdentity = physicalIdentity ?? $"{componentId}/{axis}";
        Sources = sources is null ? ImmutableArray<SourceRef>.Empty : ContractGuard.Copy(sources);
    }

    public string Id { get; }
    public string ComponentId { get; }
    public Axis Axis { get; }
    public Interval1D DistributionInterval { get; }
    public Interval1D RunInterval { get; }
    public ImmutableArray<double> PositionsMm { get; }
    public string PhysicalIdentity { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record ConnectionGraph
{
    public ConnectionGraph(IEnumerable<MeshZoneInput> zones)
    {
        Zones = ContractGuard.Copy(zones);
        if (Zones.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Zones.Length)
            throw new ArgumentException("Connection graph zone IDs must be unique.");
    }

    public ImmutableArray<MeshZoneInput> Zones { get; }
}

public sealed record JunctionPolicy
{
    public JunctionPolicy(string policyId, bool mergeSeamDuplicates, double toleranceMm, bool approvedProduction)
    {
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        MergeSeamDuplicates = mergeSeamDuplicates;
        ToleranceMm = ContractGuard.Positive(toleranceMm, nameof(toleranceMm));
        ApprovedProduction = approvedProduction;
    }

    public static JunctionPolicy UniformFixture { get; } = new("MESH-JUNCTION-FIXTURE-001", true, 1e-6, false);
    public string PolicyId { get; }
    public bool MergeSeamDuplicates { get; }
    public double ToleranceMm { get; }
    public bool ApprovedProduction { get; }
}

public sealed record MergedMeshCenter
{
    public MergedMeshCenter(string zoneId, string physicalIdentity, Layer layer, Axis axis,
        RebarDirection direction, double positionMm, IEnumerable<SourceRef> provenance)
    {
        ZoneId = ContractGuard.Text(zoneId, nameof(zoneId));
        PhysicalIdentity = ContractGuard.Text(physicalIdentity, nameof(physicalIdentity));
        Layer = ContractGuard.EnumValue(layer);
        Axis = ContractGuard.EnumValue(axis);
        Direction = ContractGuard.EnumValue(direction);
        PositionMm = ContractGuard.Finite(positionMm, nameof(positionMm));
        Provenance = ContractGuard.Copy(provenance);
    }

    public string ZoneId { get; }
    public string PhysicalIdentity { get; }
    public Layer Layer { get; }
    public Axis Axis { get; }
    public RebarDirection Direction { get; }
    public double PositionMm { get; }
    public ImmutableArray<SourceRef> Provenance { get; }
}

public sealed record MeshZoneSet
{
    public MeshZoneSet(IEnumerable<MeshZone> zones, IEnumerable<MergedMeshCenter> mergedCenters)
    {
        Zones = ContractGuard.Copy(zones);
        MergedCenters = ContractGuard.Copy(mergedCenters);
        if (Zones.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Zones.Length)
            throw new ArgumentException("Zone IDs must be unique.");
        PhysicalBars = MergedCenters;
    }

    public static MeshZoneSet FromLegacy(MeshZonePlan plan) => new(plan.Zones,
        plan.Zones.SelectMany(z => z.PositionsMm.Select(p => new MergedMeshCenter(z.Id, z.Id,
            Layer.Bottom, z.Axis, z.Axis == Axis.X ? RebarDirection.Horizontal : RebarDirection.Vertical, p, z.Sources))));

    public ImmutableArray<MeshZone> Zones { get; }
    public ImmutableArray<MergedMeshCenter> MergedCenters { get; }
    public ImmutableArray<MergedMeshCenter> PhysicalBars { get; }
}

public sealed record SlabLayerPolicy
{
    public SlabLayerPolicy(MeshSpec bottom, MeshSpec top, double coverMm, bool includeBase = true,
        bool includeRoof = false, double? openingClearanceMm = null, IEnumerable<Layer>? activeRoofLayers = null,
        IEnumerable<Axis>? activeRoofAxes = null)
        : this(bottom, bottom, top, top, coverMm, includeBase, includeRoof, openingClearanceMm, activeRoofLayers, activeRoofAxes) { }

    public SlabLayerPolicy(MeshSpec bottomX, MeshSpec bottomY, MeshSpec topX, MeshSpec topY, double coverMm,
        bool includeBase = true, bool includeRoof = false, double? openingClearanceMm = null,
        IEnumerable<Layer>? activeRoofLayers = null, IEnumerable<Axis>? activeRoofAxes = null)
    {
        BottomX = ContractGuard.Required(bottomX); BottomY = ContractGuard.Required(bottomY);
        TopX = ContractGuard.Required(topX); TopY = ContractGuard.Required(topY);
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
        IncludeBase = includeBase; IncludeRoof = includeRoof;
        OpeningClearanceMm = openingClearanceMm ?? double.NaN;
        if (!double.IsNaN(OpeningClearanceMm)) ContractGuard.NonNegative(OpeningClearanceMm, nameof(openingClearanceMm));
        ActiveRoofLayers = activeRoofLayers is null ? ImmutableArray.Create(Layer.Bottom, Layer.Top) : ContractGuard.Copy(activeRoofLayers);
        if (ActiveRoofLayers.Distinct().Count() != ActiveRoofLayers.Length) throw new ArgumentException("Roof layers must be unique.");
        ActiveRoofAxes = activeRoofAxes is null ? ImmutableArray.Create(Axis.X, Axis.Y) : ContractGuard.Copy(activeRoofAxes);
        if (ActiveRoofAxes.Distinct().Count() != ActiveRoofAxes.Length) throw new ArgumentException("Roof axes must be unique.");
    }

    public static SlabLayerPolicy BaseOnly(MeshSpec bottom, MeshSpec top, double coverMm) =>
        new(bottom, top, coverMm, true, false);
    public MeshSpec BottomX { get; }
    public MeshSpec BottomY { get; }
    public MeshSpec TopX { get; }
    public MeshSpec TopY { get; }
    public double CoverMm { get; }
    public bool IncludeBase { get; }
    public bool IncludeRoof { get; }
    public double OpeningClearanceMm { get; }
    public ImmutableArray<Layer> ActiveRoofLayers { get; }
    public ImmutableArray<Axis> ActiveRoofAxes { get; }
}

public sealed record WallOrNeckRebarSpec
{
    public WallOrNeckRebarSpec(int verticalDiameterMm, double verticalSpacingMm, int horizontalDiameterStepDown,
        double horizontalSpacingDeltaMm, StructureKind structure = StructureKind.ReinforcedConcrete,
        double coverMm = 30, bool innerLayer = true, bool outerLayer = true, IEnumerable<SourceRef>? sources = null)
    {
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
        VerticalDiameterMm = ContractGuard.Positive(verticalDiameterMm, nameof(verticalDiameterMm));
        VerticalSpacingMm = ContractGuard.Positive(verticalSpacingMm, nameof(verticalSpacingMm));
        HorizontalDiameterStepDown = horizontalDiameterStepDown;
        HorizontalSpacingDeltaMm = ContractGuard.Finite(horizontalSpacingDeltaMm, nameof(horizontalSpacingDeltaMm));
        Structure = ContractGuard.EnumValue(structure);
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
        InnerLayer = innerLayer; OuterLayer = outerLayer;
    }

    public ImmutableArray<SourceRef> Sources { get; }
    public int VerticalDiameterMm { get; }
    public double VerticalSpacingMm { get; }
    public int HorizontalDiameterStepDown { get; }
    public double HorizontalSpacingDeltaMm { get; }
    public StructureKind Structure { get; }
    public double CoverMm { get; }
    public bool InnerLayer { get; }
    public bool OuterLayer { get; }
}

public enum OpeningReinforcementShape { None, RoundBo, RectangularBo, Diagonal45, SeparateSides }
public enum EndAnchoragePolicy { None, Explicit, NeedsRule }
public enum MeasurementBasisKind { Centerline, Nominal }

public sealed record OpeningReinforcementSpec
{
    public OpeningReinforcementSpec(OpeningReinforcementShape shape, int countPerSideK, IEnumerable<Layer> layerSet,
        int diameterMm, double extensionMm, double lapDiameterMultiple, double? parallelSpacingMm,
        EndAnchoragePolicy endAnchoragePolicy, double? innerRadiusMm = null, IEnumerable<SourceRef>? sources = null,
        double coverMm = 30)
    {
        Shape = ContractGuard.EnumValue(shape);
        if (countPerSideK < 0) throw new ArgumentOutOfRangeException(nameof(countPerSideK));
        CountPerSideK = countPerSideK;
        LayerSet = ContractGuard.Copy(layerSet);
        if (LayerSet.Distinct().Count() != LayerSet.Length) throw new ArgumentException("Layer set must be unique.");
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        ExtensionMm = ContractGuard.NonNegative(extensionMm, nameof(extensionMm));
        LapDiameterMultiple = ContractGuard.NonNegative(lapDiameterMultiple, nameof(lapDiameterMultiple));
        ParallelSpacingMm = parallelSpacingMm;
        if (parallelSpacingMm.HasValue) ContractGuard.Positive(parallelSpacingMm.Value, nameof(parallelSpacingMm));
        EndAnchoragePolicy = ContractGuard.EnumValue(endAnchoragePolicy);
        InnerRadiusMm = innerRadiusMm;
        if (innerRadiusMm.HasValue) ContractGuard.NonNegative(innerRadiusMm.Value, nameof(innerRadiusMm));
        Sources = sources is null ? ImmutableArray<SourceRef>.Empty : ContractGuard.Copy(sources);
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
    }

    public OpeningReinforcementShape Shape { get; }
    public int CountPerSideK { get; }
    public ImmutableArray<Layer> LayerSet { get; }
    public int DiameterMm { get; }
    public double ExtensionMm { get; }
    public double LapDiameterMultiple { get; }
    public double? ParallelSpacingMm { get; }
    public EndAnchoragePolicy EndAnchoragePolicy { get; }
    public double? InnerRadiusMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public double CoverMm { get; }
}

public sealed record ActiveLayerFrame
{
    public ActiveLayerFrame(Layer layer, string id, Frame3Mm frame)
    {
        Layer = ContractGuard.EnumValue(layer); Id = ContractGuard.Text(id, nameof(id)); Frame = ContractGuard.Required(frame);
    }
    public Layer Layer { get; }
    public string Id { get; }
    public Frame3Mm Frame { get; }
}

/// <summary>Independent outer/inner meshes and source-backed configuration; no collapse to one mesh.</summary>
public sealed record MainRebarInput
{
    public MainRebarInput(WallOrNeckRebarSpec? wallOuter,WallOrNeckRebarSpec? wallInner,MeshSpec? roofTop,MeshSpec? roofBottom,
        double roofCoverMm,WallOrNeckRebarSpec? neckOuter,WallOrNeckRebarSpec? neckInner,StructureKind neckStructure,
        LadderSpec? ladder,IEnumerable<KeyValuePair<string,SourceRef>> sources, IEnumerable<KeyValuePair<string,HookPattern>>? hooks = null, HookMode hookMode = HookMode.ClearThickness, int? hookMultiple = null, AngleFrameInput? angleFrame = null, AccessoryRebarInput? accessories = null, double? wallSupportCoverMm = null)
    {
        AngleFrame=angleFrame;Accessories=accessories;
        WallSupportCoverMm=wallSupportCoverMm ?? wallInner?.CoverMm ?? wallOuter?.CoverMm;
        if(WallSupportCoverMm.HasValue) ContractGuard.NonNegative(WallSupportCoverMm.Value,nameof(wallSupportCoverMm));
        Hooks=hooks is null?ImmutableDictionary<string,HookPattern>.Empty:ContractGuard.Map(hooks);HookMode=ContractGuard.EnumValue(hookMode);HookMultiple=hookMultiple;
        WallOuter=wallOuter;WallInner=wallInner;RoofTop=roofTop;RoofBottom=roofBottom;
        RoofCoverMm=ContractGuard.NonNegative(roofCoverMm,nameof(roofCoverMm));NeckOuter=neckOuter;NeckInner=neckInner;
        NeckStructure=ContractGuard.EnumValue(neckStructure);Ladder=ladder;Sources=ContractGuard.Map(sources);
    }
    public AccessoryRebarInput? Accessories {get;}
    public double? WallSupportCoverMm {get;}
    public AngleFrameInput? AngleFrame {get;}
    public ImmutableDictionary<string,HookPattern> Hooks {get;}
    public HookMode HookMode {get;}
    public int? HookMultiple {get;}
    public WallOrNeckRebarSpec? WallOuter {get;}
    public WallOrNeckRebarSpec? WallInner {get;}
    public MeshSpec? RoofTop {get;}
    public MeshSpec? RoofBottom {get;}
    public double RoofCoverMm {get;}
    public WallOrNeckRebarSpec? NeckOuter {get;}
    public WallOrNeckRebarSpec? NeckInner {get;}
    public StructureKind NeckStructure {get;}
    public LadderSpec? Ladder {get;}
    public ImmutableDictionary<string,SourceRef> Sources {get;}
}

public sealed record AngleFrameInput
{
    public AngleFrameInput(string catalogCode,EnabledPositionsSet positions,IEnumerable<SourceRef> sources)
    {CatalogCode=ContractGuard.Text(catalogCode,nameof(catalogCode));Positions=ContractGuard.Required(positions);Sources=ContractGuard.Copy(sources);}
    public string CatalogCode {get;}
    public EnabledPositionsSet Positions {get;}
    public ImmutableArray<SourceRef> Sources {get;}
}
