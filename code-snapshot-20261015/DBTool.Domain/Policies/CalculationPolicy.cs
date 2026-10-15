using System.Collections.Immutable;
using DBTool.Domain.StageContractsV2;

namespace DBTool.Domain;

public sealed record CalculationPolicy
{
    private CalculationPolicy(string policyId)
    {
        PolicyId = policyId; Version = policyId+"/v1";
        EvidenceStatus = EvidenceStatus.ExperimentalSoftwareFixture;
        PipeInvertReference = PipeInvertReference.InternalInvert; DistributionMode = DistributionMode.Equalized;
        MeshJunctionPolicy = MeshJunctionPolicy.UniformBaseTestOnly; LayerStackPolicy = LayerStackPolicy.TwoDirectionsTouchingTestOnly;
        HookPolicy = new(HookMode.ClearThickness, null);
        MassPolicy = new(MassPolicyId.D2Over162, "MASS-162-001/v1", null);
        RoundingPolicy = new(3, MidpointRounding.AwayFromZero);
        TolerancePolicy = policyId is "S2Geometry-1" or "S2Rebar-1"
            ? new(1e-6,1e-6,1e-9,1,1)
            : new(1e-6, 1e-6, 1e-9, null, null);
        var ruleIds = new[] { "UNIT-001", "FRAME-001", "PIPE-ROUND-001", "DIST-SEGMENT-001", "DIST-LOOP-TEST-001",
            "MESH-UNIFORM-TEST-001", "BAR-LENGTH-001", "LAYER-STACK-TEST-001", "MASS-162-001", "ROUND-DISPLAY-001" }
            .AsEnumerable();
        if (policyId=="S2Geometry-1") ruleIds=ruleIds.Concat([
            "HAUNCH-ANALYTIC-001","NECK-EXPLICIT-001","PANEL-LAYOUT-001",
            "OPENING-CLEARANCE-S2-001","SECTION-STEPPED-S2-001","HOOK-RETAIN-S2-001"]);
        if (policyId=="S2Rebar-1") ruleIds=ruleIds.Concat([
            "MESH-ZONE-S2-001","SLAB-REBAR-S2-001","WALL-NECK-REBAR-S2-001","OPENING-REBAR-S2-001",
            "TIE-REBAR-S3-001","ANGLE-FRAME-S3-001","ANCHOR-S3-001","DOWEL-S3-001",
            "LADDER-S3-001","LIFTING-HOOK-S3-001","PANEL-REBAR-S3-001","CATCH-REBAR-S3-001"]);
        Rules = ruleIds.Select(id => new RuleBinding(id, Version, EvidenceKind.S, EvidenceStatus.ExperimentalSoftwareFixture,
                Array.Empty<SourceRef>(), new[] { "VHG-equivalence-unresolved" })).ToImmutableArray();
    }

    public CalculationPolicy(string policyId, string version, EvidenceStatus status, PipeInvertReference invertRef, DistributionMode distMode, MeshJunctionPolicy meshPolicy, HookPolicy hookPolicy, BendPolicy? bendPolicy, LayerStackPolicy layerPolicy, MassPolicy massPolicy, RoundingPolicy roundingPolicy, TolerancePolicy tolPolicy, IEnumerable<RuleBinding> rules)
    {
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        Version = ContractGuard.Text(version, nameof(version));
        EvidenceStatus = ContractGuard.EnumValue(status);
        PipeInvertReference = ContractGuard.EnumValue(invertRef);
        DistributionMode = ContractGuard.EnumValue(distMode);
        MeshJunctionPolicy = ContractGuard.EnumValue(meshPolicy);
        HookPolicy = ContractGuard.Required(hookPolicy);
        BendPolicy = bendPolicy;
        LayerStackPolicy = ContractGuard.EnumValue(layerPolicy);
        MassPolicy = ContractGuard.Required(massPolicy);
        RoundingPolicy = ContractGuard.Required(roundingPolicy);
        TolerancePolicy = ContractGuard.Required(tolPolicy);
        Rules = ContractGuard.Copy(rules);
    }

    public static OperationResult<CalculationPolicy> Select(string policyId, bool allowExperimental)
    {
        string? error = policyId is not ("S1-Analytic-1" or "S2Geometry-1" or "S2Rebar-1") ? "POLICY_UNKNOWN" : !allowExperimental ? "POLICY_EXPERIMENTAL_DISABLED" : null;
        return error == null ? new(true, new CalculationPolicy(policyId), Array.Empty<Diagnostic>())
            : new(false, null, new[] { new Diagnostic(error, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null, null, policyId, error) });
    }

    public bool HasApprovedRules => Rules.All(r => r.EvidenceStatus != EvidenceStatus.ExperimentalSoftwareFixture && !r.Sources.IsEmpty);

    public string PolicyId { get; }
    /// <summary>Authorization snapshot: experimental instances exist only after explicit opt-in; excluded from calculation hashing.</summary>
    public bool AllowExperimental => EvidenceStatus == EvidenceStatus.ExperimentalSoftwareFixture;
    public string Version { get; }
    public EvidenceStatus EvidenceStatus { get; }
    public PipeInvertReference PipeInvertReference { get; }
    public DistributionMode DistributionMode { get; }
    public MeshJunctionPolicy MeshJunctionPolicy { get; }
    public HookPolicy HookPolicy { get; }
    public BendPolicy? BendPolicy { get; }
    public LayerStackPolicy LayerStackPolicy { get; }
    public MassPolicy MassPolicy { get; }
    public RoundingPolicy RoundingPolicy { get; }
    public TolerancePolicy TolerancePolicy { get; }
    public ImmutableArray<RuleBinding> Rules { get; }
}
