using System.Collections.Immutable;
namespace DBTool.Domain;
public enum PipeInvertReference { InternalInvert }
public enum DistributionMode { Equalized, DensityCentered }
public enum MeshJunctionPolicy { UniformBaseTestOnly }
public enum HookPattern { Hook0M, Hook1M, Hook2M, HookStartOnly }
public enum HookMode { ClearThickness, DiameterMultiple }
public enum MeasurementBasis { TangentPath, TheoreticalCorner, NominalPolyline }
public enum LayerStackPolicy { TwoDirectionsTouchingTestOnly }
public enum MassPolicyId { D2Over162, Density7850 }
public sealed record HookPolicy
{
    public HookPolicy(HookMode mode, int? diameterMultiple)
    {
        Mode = ContractGuard.EnumValue(mode);
        DiameterMultiple = diameterMultiple;
        if (mode == HookMode.DiameterMultiple ? diameterMultiple is not > 0 : diameterMultiple != null) throw new ArgumentException("Hook policy mismatch.");
    }
    public HookMode Mode { get; }
    public int? DiameterMultiple { get; }
}
public sealed record BendPolicy
{
    public BendPolicy(MeasurementBasis measurementBasis, double? innerRadiusMm, IEnumerable<double> anglesRadians, double additionalLapMm, bool extraLapAlreadyInPath)
    {
        MeasurementBasis = ContractGuard.EnumValue(measurementBasis);
        InnerRadiusMm = innerRadiusMm;
        AnglesRadians = ContractGuard.Copy(anglesRadians);
        AdditionalLapMm = ContractGuard.NonNegative(additionalLapMm, nameof(additionalLapMm));
        ExtraLapAlreadyInPath = extraLapAlreadyInPath;
        if (innerRadiusMm.HasValue) ContractGuard.NonNegative(innerRadiusMm.Value, nameof(innerRadiusMm));
        foreach (double a in AnglesRadians) { ContractGuard.Finite(a, nameof(anglesRadians)); if (a == 0 || Math.Abs(a) >= Math.PI) throw new ArgumentOutOfRangeException(nameof(anglesRadians)); }
    }
    public MeasurementBasis MeasurementBasis { get; }
    public double? InnerRadiusMm { get; }
    public ImmutableArray<double> AnglesRadians { get; }
    public double AdditionalLapMm { get; }
    public bool ExtraLapAlreadyInPath { get; }
}
public sealed record MassPolicy
{
    public MassPolicy(MassPolicyId id, string version, double? densityKgPerM3)
    {
        Id = ContractGuard.EnumValue(id);
        Version = ContractGuard.Text(version, nameof(version));
        DensityKgPerM3 = densityKgPerM3;
        if (id == MassPolicyId.Density7850 ? densityKgPerM3 != 7850 : densityKgPerM3 != null) throw new ArgumentException("Mass policy mismatch.");
    }
    public MassPolicyId Id { get; }
    public string Version { get; }
    public double? DensityKgPerM3 { get; }
}
public sealed record RuleBinding
{
    public RuleBinding(string ruleId, string ruleVersion, EvidenceKind evidenceKind, EvidenceStatus evidenceStatus, IEnumerable<SourceRef> sources, IEnumerable<string> openIssueIds)
    {
        RuleId = ContractGuard.Text(ruleId, nameof(ruleId));
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        EvidenceKind = ContractGuard.EnumValue(evidenceKind);
        EvidenceStatus = ContractGuard.EnumValue(evidenceStatus);
        Sources = ContractGuard.Copy(sources);
        OpenIssueIds = ContractGuard.Copy(openIssueIds);

    }
    public string RuleId { get; }
    public string RuleVersion { get; }
    public EvidenceKind EvidenceKind { get; }
    public EvidenceStatus EvidenceStatus { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public ImmutableArray<string> OpenIssueIds { get; }
}
