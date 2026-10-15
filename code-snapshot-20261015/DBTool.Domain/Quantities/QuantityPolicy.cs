namespace DBTool.Domain;

public sealed record QuantityPolicy
{
    public QuantityPolicy(string policyId, string? ruleVersion = null, bool includeMortar = true,
        string mortarMaterialId = "BeddingMortar", TolerancePolicy? tolerancePolicy = null)
    {
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        RuleVersion = ContractGuard.Text(ruleVersion ?? policyId + "/v1", nameof(ruleVersion));
        IncludeMortar = includeMortar;
        MortarMaterialId = ContractGuard.Text(mortarMaterialId, nameof(mortarMaterialId));
        TolerancePolicy = tolerancePolicy ?? new TolerancePolicy(1e-6, 1e-6, 1e-9, 1e-6, 1);
    }

    public string PolicyId { get; }
    public string RuleVersion { get; }
    public bool IncludeMortar { get; }
    public string MortarMaterialId { get; }
    public TolerancePolicy TolerancePolicy { get; }
}
