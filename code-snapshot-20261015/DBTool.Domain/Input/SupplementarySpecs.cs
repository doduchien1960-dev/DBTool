using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record EnabledPositionsSet
{
    public EnabledPositionsSet(IEnumerable<int> positions)
    {
        Positions = ContractGuard.Copy(positions.Select(x =>
        {
            if (x is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(positions));
            return x;
        }).Distinct().Order());
    }

    public EnabledPositionsSet(bool position1, bool position2, bool position3)
        : this(new[] { position1 ? 1 : 0, position2 ? 2 : 0, position3 ? 3 : 0 }.Where(x => x != 0)) { }

    public static EnabledPositionsSet All { get; } = new([1, 2, 3]);
    public ImmutableArray<int> Positions { get; }
    public bool Contains(int position) => Positions.Contains(position);
}

public sealed record AngleFrameSpec
{
    public AngleFrameSpec(int position, Rectangle2Mm boundary, double planeMm, IEnumerable<SourceRef>? sources = null)
    {
        if (position is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(position));
        Position = position; Boundary = ContractGuard.Required(boundary); PlaneMm = ContractGuard.Finite(planeMm, nameof(planeMm));
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public int Position { get; }
    public Rectangle2Mm Boundary { get; }
    public double PlaneMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record AnchorRun
{
    public AnchorRun(string runId, Point3Mm start, Point3Mm end, string frameId,
        string componentId = "Neck", ComponentKind componentKind = ComponentKind.Neck,
        Layer layer = Layer.Bottom, IEnumerable<SourceRef>? sources = null)
        : this(runId, [start, end], false, frameId, componentId, componentKind, layer, sources) { }

    public AnchorRun(string runId, IEnumerable<Point3Mm> points, bool closed, string frameId,
        string componentId = "Neck", ComponentKind componentKind = ComponentKind.Neck,
        Layer layer = Layer.Bottom, IEnumerable<SourceRef>? sources = null)
    {
        RunId = ContractGuard.Text(runId, nameof(runId));
        Points = ContractGuard.Copy(points);
        if (Points.Length < 2) throw new ArgumentException("An anchor run needs at least two points.", nameof(points));
        FrameId = ContractGuard.Text(frameId, nameof(frameId));
        Closed = closed;
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        ComponentKind = ContractGuard.EnumValue(componentKind);
        Layer = ContractGuard.EnumValue(layer);
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }

    public string RunId { get; }
    public ImmutableArray<Point3Mm> Points { get; }
    public bool Closed { get; }
    public string FrameId { get; }
    public string ComponentId { get; }
    public ComponentKind ComponentKind { get; }
    public Layer Layer { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record DowelSpec
{
    public DowelSpec(double stepMm, int diameterMm, double lengthMm, Round50Mode? round50Mode = null,
        IEnumerable<SourceRef>? sources = null)
    {
        StepMm = ContractGuard.Positive(stepMm, nameof(stepMm)); DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm));
        LengthMm = ContractGuard.Positive(lengthMm, nameof(lengthMm));
        if (round50Mode.HasValue) ContractGuard.EnumValue(round50Mode.Value);
        Round50Mode = round50Mode;
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public double StepMm { get; }
    public int DiameterMm { get; }
    public double LengthMm { get; }
    public Round50Mode? Round50Mode { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public enum Round50Mode { ExplicitNearestAway, Floor, Ceiling, ResolvedVhgRule }

public sealed record LadderSpec
{
    public LadderSpec(double topOffsetMm, double pitchMm, double minZMm, double widthMm, double legHeightMm,
        double hookMm, int diameterMm = 18, LadderOpeningBehavior openingBehavior = LadderOpeningBehavior.Reject,
        IEnumerable<SourceRef>? sources = null)
    {
        TopOffsetMm = ContractGuard.NonNegative(topOffsetMm, nameof(topOffsetMm)); PitchMm = ContractGuard.Finite(pitchMm, nameof(pitchMm));
        MinZMm = ContractGuard.Finite(minZMm, nameof(minZMm)); WidthMm = ContractGuard.Positive(widthMm, nameof(widthMm));
        LegHeightMm = ContractGuard.Positive(legHeightMm, nameof(legHeightMm)); HookMm = ContractGuard.NonNegative(hookMm, nameof(hookMm));
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm)); OpeningBehavior = ContractGuard.EnumValue(openingBehavior);
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public double TopOffsetMm { get; }
    public double PitchMm { get; }
    public double MinZMm { get; }
    public double WidthMm { get; }
    public double LegHeightMm { get; }
    public double HookMm { get; }
    public int DiameterMm { get; }
    public LadderOpeningBehavior OpeningBehavior { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record LiftingHookSpec
{
    public LiftingHookSpec(int count, int diameterMm, double hookLengthMm, double spacingMm = 0,
        IEnumerable<SourceRef>? sources = null)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        Count = count; DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm)); HookLengthMm = ContractGuard.Positive(hookLengthMm, nameof(hookLengthMm));
        SpacingMm = ContractGuard.NonNegative(spacingMm, nameof(spacingMm)); Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public int Count { get; }
    public int DiameterMm { get; }
    public double HookLengthMm { get; }
    public double SpacingMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public enum LadderOpeningBehavior { Relocate, Skip, Reject }

public sealed record SupplementaryLayout
{
    public SupplementaryLayout(IEnumerable<BarOccurrence> occurrences, IEnumerable<Diagnostic> diagnostics,
        LayoutStatus status = LayoutStatus.Complete)
    {
        Occurrences = ContractGuard.Copy(occurrences); Diagnostics = ContractGuard.Copy(diagnostics); Status = ContractGuard.EnumValue(status);
    }
    public ImmutableArray<BarOccurrence> Occurrences { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public LayoutStatus Status { get; }
}

public sealed record PanelRebarSpec
{
    public PanelRebarSpec(int diameterMm, double spacingMm, double coverMm, bool includeTop,
        DistributionMode distributionMode = DistributionMode.Equalized, int topDiameterMm = 0,
        IEnumerable<SourceRef>? sources = null)
    {
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm)); SpacingMm = ContractGuard.Positive(spacingMm, nameof(spacingMm));
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm)); IncludeTop = includeTop;
        TopDiameterMm = includeTop ? ContractGuard.Positive(topDiameterMm == 0 ? diameterMm : topDiameterMm, nameof(topDiameterMm)) : 0;
        DistributionMode = ContractGuard.EnumValue(distributionMode); Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public int DiameterMm { get; }
    public double SpacingMm { get; }
    public double CoverMm { get; }
    public bool IncludeTop { get; }
    public int TopDiameterMm { get; }
    public DistributionMode DistributionMode { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record CatchRebarSpec
{
    public CatchRebarSpec(double floorWidthMm, double floorDepthMm, int diameterMm, double spacingMm, double coverMm,
        MeshSpec? sideHorizontal = null, MeshSpec? sideVertical = null, MeshSpec? front = null,
        OpeningReinforcementSpec? openingReinforcement = null, bool structuralEvidenceApproved = false,
        IEnumerable<SourceRef>? sources = null)
    {
        FloorWidthMm = ContractGuard.Positive(floorWidthMm, nameof(floorWidthMm)); FloorDepthMm = ContractGuard.Positive(floorDepthMm, nameof(floorDepthMm));
        DiameterMm = ContractGuard.Positive(diameterMm, nameof(diameterMm)); SpacingMm = ContractGuard.Positive(spacingMm, nameof(spacingMm));
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm)); SideHorizontal = sideHorizontal; SideVertical = sideVertical;
        Front = front; OpeningReinforcement = openingReinforcement; StructuralEvidenceApproved = structuralEvidenceApproved;
        Sources = sources is null ? [] : ContractGuard.Copy(sources);
    }
    public double FloorWidthMm { get; }
    public double FloorDepthMm { get; }
    public int DiameterMm { get; }
    public double SpacingMm { get; }
    public double CoverMm { get; }
    public MeshSpec? SideHorizontal { get; }
    public MeshSpec? SideVertical { get; }
    public MeshSpec? Front { get; }
    public OpeningReinforcementSpec? OpeningReinforcement { get; }
    public bool StructuralEvidenceApproved { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
