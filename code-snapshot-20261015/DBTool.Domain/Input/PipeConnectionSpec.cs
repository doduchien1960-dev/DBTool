using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public sealed record PipeConnectionSpec
{
    public PipeConnectionSpec(WallId wallId, string catalogCode, double invertMm, double offsetMm, double mortarMm, IEnumerable<KeyValuePair<string,SourceRef>> sourceRefs, string? id = null)
    {
        WallId = ContractGuard.EnumValue(wallId);
        CatalogCode = ContractGuard.Text(catalogCode, nameof(catalogCode));
        InvertMm = ContractGuard.NonNegative(invertMm, nameof(invertMm));
        OffsetMm = ContractGuard.Finite(offsetMm, nameof(offsetMm));
        MortarMm = ContractGuard.NonNegative(mortarMm, nameof(mortarMm));
        SourceRefs = ContractGuard.Map(sourceRefs);
        Id = id is null ? null : ContractGuard.Text(id, nameof(id));

    }
    public WallId WallId { get; }
    public string CatalogCode { get; }
    public double InvertMm { get; }
    public double OffsetMm { get; }
    public double MortarMm { get; }
    public ImmutableDictionary<string,SourceRef> SourceRefs { get; }
    public string? Id { get; }
}
public sealed record FeatureRequest
{
    public FeatureRequest(string featureId, bool requested, IEnumerable<KeyValuePair<string,RawCell>> parameters)
    {
        FeatureId = ContractGuard.Text(featureId, nameof(featureId));
        Requested = requested;
        Parameters = ContractGuard.Map(parameters);

    }
    public string FeatureId { get; }
    public bool Requested { get; }
    public ImmutableDictionary<string,RawCell> Parameters { get; }
}
