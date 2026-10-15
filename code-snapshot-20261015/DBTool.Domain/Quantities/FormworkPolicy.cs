namespace DBTool.Domain;

public enum RoofCastMode { InSitu, PrecastBed }

public sealed record FormworkPolicy
{
    public FormworkPolicy(string policyId, bool castAgainstGround = true, bool baseBottomRequired = false,
        bool? wallInnerRequired = null, bool? wallOuterRequired = null,
        RoofCastMode roofCastMode = RoofCastMode.InSitu, bool? panelBedRequired = null,
        bool? openingRevealRequired = null, bool contactExcluded = true, TolerancePolicy? tolerancePolicy = null)
    {
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        CastAgainstGround = castAgainstGround;
        BaseBottomRequired = baseBottomRequired;
        WallInnerRequired = wallInnerRequired;
        WallOuterRequired = wallOuterRequired;
        RoofCastMode = ContractGuard.EnumValue(roofCastMode);
        PanelBedRequired = panelBedRequired;
        OpeningRevealRequired = openingRevealRequired;
        ContactExcluded = contactExcluded;
        TolerancePolicy = tolerancePolicy ?? new TolerancePolicy(1e-6, 1e-6, 1e-9, 1e-6, 1);
    }

    public string PolicyId { get; }
    public bool CastAgainstGround { get; }
    public bool BaseBottomRequired { get; }
    public bool? WallInnerRequired { get; }
    public bool? WallOuterRequired { get; }
    public RoofCastMode RoofCastMode { get; }
    public bool? PanelBedRequired { get; }
    public bool? OpeningRevealRequired { get; }
    public bool ContactExcluded { get; }
    public TolerancePolicy TolerancePolicy { get; }
}
