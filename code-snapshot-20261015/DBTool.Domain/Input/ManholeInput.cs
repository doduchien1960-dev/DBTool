using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public sealed record ManholeInput
{
    public ManholeInput(string id, string name, Profile profile, double l, double b, double h, double baseThicknessMm, double wallThicknessMm, double roofThicknessMm, double padThicknessMm, double baseExpansionMm, double padExpansionMm, double beddingMortarMm, double baseHaunch, double topHaunch, double neckHeight, Rectangle2Mm roofOpening, IEnumerable<PipeConnectionSpec> pipeConnections, MeshSpec baseBottomMesh, MeshSpec baseTopMesh, double coverMm, ElevationMode elevationMode, ElevationM? baseElevationM, PipeInvertReference pipeInvertReference, HookPattern hookBaseBottom, HookPattern hookBaseTop, DistributionMode distributionMode, MeshJunctionPolicy meshJunctionPolicy, LayerStackPolicy layerStackPolicy, IEnumerable<FeatureRequest> featureRequests, IEnumerable<KeyValuePair<string,SourceRef>> sourceRefs, IEnumerable<KeyValuePair<string,RawCell>> unsupportedRawFields, HaunchSpec? baseHaunchSpec = null, HaunchSpec? topHaunchSpec = null, NeckSpec? neckSpec = null, NeckSeatSpec? neckSeatSpec = null, CollarSpec? collarSpec = null, CoverPanelSpec? coverPanelSpec = null, PurchasedCoverSpec? purchasedCoverSpec = null, CatchBasinSpec? catchBasinSpec = null, MemberRebarSpec? memberRebarSpec = null, IEnumerable<OpeningReinforcementSpec>? openingReinforcementSpecs = null, MainRebarInput? mainRebar = null, string? noteStyle = null, string? pitchSymbol = null, string? wallDimPrefix = null, string? neckPrefix = null, int? panelDetailScaleDenominator = null, string? materialG = null, IEnumerable<KeyValuePair<string, string>>? annotationPrefixes = null, IEnumerable<KeyValuePair<string, RawCell>>? configRawCells = null)
    {
        MainRebar = mainRebar;
        Id = ContractGuard.Text(id, nameof(id));
        Name = ContractGuard.Text(name, nameof(name));
        Profile = ContractGuard.EnumValue(profile);
        L = ContractGuard.Positive(l, nameof(l));
        B = ContractGuard.Positive(b, nameof(b));
        H = ContractGuard.Positive(h, nameof(h));
        BaseThicknessMm = ContractGuard.Positive(baseThicknessMm, nameof(baseThicknessMm));
        WallThicknessMm = ContractGuard.Positive(wallThicknessMm, nameof(wallThicknessMm));
        RoofThicknessMm = ContractGuard.Positive(roofThicknessMm, nameof(roofThicknessMm));
        PadThicknessMm = ContractGuard.NonNegative(padThicknessMm, nameof(padThicknessMm));
        BaseExpansionMm = ContractGuard.NonNegative(baseExpansionMm, nameof(baseExpansionMm));
        PadExpansionMm = ContractGuard.NonNegative(padExpansionMm, nameof(padExpansionMm));
        BeddingMortarMm = ContractGuard.NonNegative(beddingMortarMm, nameof(beddingMortarMm));
        BaseHaunch = ContractGuard.NonNegative(baseHaunch, nameof(baseHaunch));
        TopHaunch = ContractGuard.NonNegative(topHaunch, nameof(topHaunch));
        NeckHeight = ContractGuard.NonNegative(neckHeight, nameof(neckHeight));
        RoofOpening = ContractGuard.Required(roofOpening);
        PipeConnections = ContractGuard.Copy(pipeConnections);
        BaseBottomMesh = ContractGuard.Required(baseBottomMesh);
        BaseTopMesh = ContractGuard.Required(baseTopMesh);
        CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
        ElevationMode = ContractGuard.EnumValue(elevationMode);
        BaseElevationM = baseElevationM;
        PipeInvertReference = ContractGuard.EnumValue(pipeInvertReference);
        HookBaseBottom = ContractGuard.EnumValue(hookBaseBottom);
        HookBaseTop = ContractGuard.EnumValue(hookBaseTop);
        DistributionMode = ContractGuard.EnumValue(distributionMode);
        MeshJunctionPolicy = ContractGuard.EnumValue(meshJunctionPolicy);
        LayerStackPolicy = ContractGuard.EnumValue(layerStackPolicy);
        FeatureRequests = ContractGuard.Copy(featureRequests);
        SourceRefs = ContractGuard.Map(sourceRefs);
        UnsupportedRawFields = ContractGuard.Map(unsupportedRawFields);
        BaseHaunchSpec = baseHaunchSpec;
        TopHaunchSpec = topHaunchSpec;
        NeckSpec = neckSpec;
        NeckSeatSpec = neckSeatSpec;
        CollarSpec = collarSpec;
        CoverPanelSpec = coverPanelSpec;
        PurchasedCoverSpec = purchasedCoverSpec;
        CatchBasinSpec = catchBasinSpec;
        MemberRebarSpec = memberRebarSpec;
        OpeningReinforcementSpecs = openingReinforcementSpecs is null ? ImmutableArray<OpeningReinforcementSpec>.Empty : ContractGuard.Copy(openingReinforcementSpecs);
        if (elevationMode == ElevationMode.Absolute && baseElevationM == null) throw new ArgumentException("Absolute elevation requires a datum.");
        AdditionalFeatures = FeatureRequests.ToImmutableDictionary(x => x.FeatureId, StringComparer.Ordinal);
        NoteStyle = noteStyle;
        PitchSymbol = pitchSymbol;
        WallDimPrefix = wallDimPrefix;
        NeckPrefix = neckPrefix;
        PanelDetailScaleDenominator = panelDetailScaleDenominator;
        MaterialG = materialG;
        AnnotationPrefixes = annotationPrefixes is null ? ImmutableDictionary<string, string>.Empty : annotationPrefixes.ToImmutableDictionary(StringComparer.Ordinal);
        ConfigRawCells = configRawCells is null ? ImmutableDictionary<string, RawCell>.Empty : configRawCells.ToImmutableDictionary(StringComparer.Ordinal);
    }
    public ImmutableDictionary<string,FeatureRequest> AdditionalFeatures { get; }
    public MainRebarInput? MainRebar { get; }
    public string Id { get; }
    public string Name { get; }
    public Profile Profile { get; }
    public double L { get; }
    public double B { get; }
    public double H { get; }
    public double BaseThicknessMm { get; }
    public double WallThicknessMm { get; }
    public double RoofThicknessMm { get; }
    public double PadThicknessMm { get; }
    public double BaseExpansionMm { get; }
    public double PadExpansionMm { get; }
    public double BeddingMortarMm { get; }
    public double BaseHaunch { get; }
    public double TopHaunch { get; }
    public double NeckHeight { get; }
    public Rectangle2Mm RoofOpening { get; }
    public ImmutableArray<PipeConnectionSpec> PipeConnections { get; }
    public MeshSpec BaseBottomMesh { get; }
    public MeshSpec BaseTopMesh { get; }
    public double CoverMm { get; }
    public ElevationMode ElevationMode { get; }
    public ElevationM? BaseElevationM { get; }
    public PipeInvertReference PipeInvertReference { get; }
    public HookPattern HookBaseBottom { get; }
    public HookPattern HookBaseTop { get; }
    public DistributionMode DistributionMode { get; }
    public MeshJunctionPolicy MeshJunctionPolicy { get; }
    public LayerStackPolicy LayerStackPolicy { get; }
    public ImmutableArray<FeatureRequest> FeatureRequests { get; }
    public ImmutableDictionary<string,SourceRef> SourceRefs { get; }
    public ImmutableDictionary<string,RawCell> UnsupportedRawFields { get; }
    public HaunchSpec? BaseHaunchSpec { get; }
    public HaunchSpec? TopHaunchSpec { get; }
    public NeckSpec? NeckSpec { get; }
    public NeckSeatSpec? NeckSeatSpec { get; }
    public CollarSpec? CollarSpec { get; }
    public CoverPanelSpec? CoverPanelSpec { get; }
    public PurchasedCoverSpec? PurchasedCoverSpec { get; }
    public CatchBasinSpec? CatchBasinSpec { get; }
    public MemberRebarSpec? MemberRebarSpec { get; }
    public ImmutableArray<OpeningReinforcementSpec> OpeningReinforcementSpecs { get; }

    public string? NoteStyle { get; }
    public string? PitchSymbol { get; }
    public string? WallDimPrefix { get; }
    public string? NeckPrefix { get; }
    public int? PanelDetailScaleDenominator { get; }
    public string? MaterialG { get; }
    public ImmutableDictionary<string, string> AnnotationPrefixes { get; }
    public ImmutableDictionary<string, RawCell> ConfigRawCells { get; }
}
