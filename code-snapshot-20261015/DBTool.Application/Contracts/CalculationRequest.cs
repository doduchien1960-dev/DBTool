using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public sealed record CalculationRequest
{
    public CalculationRequest(string projectId, IEnumerable<string> selectedIds, CalculationScope scope, string policyId, bool allowExperimental)
    {
        ProjectId = ContractGuard.Text(projectId, nameof(projectId));
        SelectedIds = ContractGuard.Copy(selectedIds);
        Scope = ContractGuard.EnumValue(scope);
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        AllowExperimental = allowExperimental;
        if(SelectedIds.Any(string.IsNullOrWhiteSpace) || SelectedIds.Distinct().Count()!=SelectedIds.Length) throw new ArgumentException("Invalid selection.");
    }

    /// <summary>Contract capability gate only; no validation or calculation engine is run.</summary>
    public OperationResult<CalculationRequest> CheckS1()
    {
        if(PolicyId!="S1-Analytic-1") return new(false,null,[new Diagnostic("S1_POLICY_UNSUPPORTED",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,"S1-Analytic-1",PolicyId,"S1 pipeline requires S1 policy.")]);
        var policy=CalculationPolicy.Select(PolicyId,AllowExperimental);
        if(!policy.Success) return new(false,null,policy.Diagnostics);
        string? code=Scope!=CalculationScope.BaseRebarOnly ? "S1_SCOPE_UNSUPPORTED" : SelectedIds.Length!=1 ? "S1_SINGLE_MANHOLE_ONLY" : null;
        return code==null ? new(true,this,Array.Empty<Diagnostic>()) :
            new(false,null,new[] { new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,code) });
    }
    /// <summary>Input normalization gate for S2 pure geometry; no calculation consumer is registered here.</summary>
    public OperationResult<CalculationRequest> CheckS2GeometryInput()
    {
        if(PolicyId!="S2Geometry-1" || !AllowExperimental || Scope!=CalculationScope.GeometryOnly || SelectedIds.Length!=1)
            return new(false,null,[new Diagnostic("S2_INPUT_SCOPE_INVALID",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
                "S2Geometry-1, GeometryOnly, one selected manhole, experimental opt-in",PolicyId,"Invalid S2 input request.")]);
        return new(true,this,[]);
    }
    /// <summary>Explicit S2 quantity gate. It is intentionally separate from the locked S1 gate.</summary>
    public OperationResult<CalculationRequest> CheckS2QuantityInput()
    {
        bool validScope = Scope is CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly or CalculationScope.GeometryOnly;
        if (PolicyId != "S2Geometry-1" || !AllowExperimental || !validScope || SelectedIds.Length != 1)
            return new(false, null, [new Diagnostic("S2_QUANTITY_INPUT_INVALID", DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, null, "S2Geometry-1, ConcreteOnly/FormworkOnly/GeometryOnly, one selected manhole, experimental opt-in",
                PolicyId, "Invalid S2 quantity input request.")]);
        return new(true, this, []);
    }
    /// <summary>Explicit S3 full-rebar gate; it never downgrades to a quantity-only scope.</summary>
    public OperationResult<CalculationRequest> CheckS2RebarInput()
    {
        bool validScope = Scope is CalculationScope.FullRebar or CalculationScope.Full;
        if (PolicyId != "S2Rebar-1" || !AllowExperimental || !validScope || SelectedIds.Length != 1)
            return new(false, null, [new Diagnostic("S3_REBAR_INPUT_INVALID", DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, null, "S2Rebar-1, FullRebar/Full, one selected manhole, experimental opt-in",
                PolicyId, "Invalid S3 rebar input request.")]);
        return new(true, this, []);
    }
    public string ProjectId { get; }
    public ImmutableArray<string> SelectedIds { get; }
    public CalculationScope Scope { get; }
    public string PolicyId { get; }
    public bool AllowExperimental { get; }
}
