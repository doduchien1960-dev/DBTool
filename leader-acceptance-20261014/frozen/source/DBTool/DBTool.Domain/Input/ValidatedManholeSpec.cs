using System.Collections.Immutable;
using DBTool.Domain.Geometry;
using DBTool.Domain.StageContractsV2;

namespace DBTool.Domain;
public sealed record ValidatedManholeSpec
{
    internal ValidatedManholeSpec(ManholeInput input, CatalogSet catalogs, string ruleVersion, IEnumerable<SourceRef> sources, IEnumerable<Diagnostic> featureDiagnostics)
        : this(input, catalogs, ruleVersion, sources, featureDiagnostics, null, null, null, null, null)
    {
    }

    internal ValidatedManholeSpec(ManholeInput input, CatalogSet catalogs, string ruleVersion, IEnumerable<SourceRef> sources, IEnumerable<Diagnostic> featureDiagnostics, IEnumerable<KeyValuePair<string, double>>? assemblyDatums, IEnumerable<DatumV2>? datums, IEnumerable<ProductContractV2>? resolvedProducts, IEnumerable<FeatureStateV2>? featureStates, Rectangle2Mm? resolvedAperture)
    {
        Input = ContractGuard.Required(input);
        Catalogs = ContractGuard.Required(catalogs);
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        Sources = ContractGuard.Copy(sources);
        FeatureDiagnostics = ContractGuard.Copy(featureDiagnostics);
        if (FeatureDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Block)) throw new ArgumentException("Validated data cannot contain Block.");
        if (Input.PipeConnections.Any(p => !Catalogs.Pipes.ContainsKey(p.CatalogCode))) throw new ArgumentException("Unresolved pipe catalog.");
        AssemblyDatums = assemblyDatums is null ? ImmutableDictionary<string, double>.Empty : assemblyDatums.ToImmutableDictionary(StringComparer.Ordinal);
        Datums = datums is null ? ImmutableArray<DatumV2>.Empty : ContractGuard.Copy(datums);
        ResolvedProducts = resolvedProducts is null ? ImmutableArray<ProductContractV2>.Empty : ContractGuard.Copy(resolvedProducts);
        FeatureStates = featureStates is null ? ImmutableArray<FeatureStateV2>.Empty : ContractGuard.Copy(featureStates);
        ResolvedAperture = resolvedAperture;
    }

    public ManholeInput Input { get; }
    public HaunchSpec? BaseHaunchSpec => Input.BaseHaunchSpec;
    public HaunchSpec? TopHaunchSpec => Input.TopHaunchSpec;
    public NeckSpec? NeckSpec => Input.NeckSpec;
    public NeckSeatSpec? NeckSeatSpec => Input.NeckSeatSpec;
    public CollarSpec? CollarSpec => Input.CollarSpec;
    public CoverPanelSpec? CoverPanelSpec => Input.CoverPanelSpec;
    public PurchasedCoverSpec? PurchasedCoverSpec => Input.PurchasedCoverSpec;
    public CatchBasinSpec? CatchBasinSpec => Input.CatchBasinSpec;
    public MemberRebarSpec? MemberRebarSpec => Input.MemberRebarSpec;
    public ImmutableArray<OpeningReinforcementSpec> OpeningReinforcementSpecs => Input.OpeningReinforcementSpecs;
    public CatalogSet Catalogs { get; }
    public string RuleVersion { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public ImmutableArray<Diagnostic> FeatureDiagnostics { get; }

    public ImmutableDictionary<string, double> AssemblyDatums { get; }
    public ImmutableArray<DatumV2> Datums { get; }
    public ImmutableArray<ProductContractV2> ResolvedProducts { get; }
    public ImmutableArray<FeatureStateV2> FeatureStates { get; }
    public Rectangle2Mm? ResolvedAperture { get; }
}
