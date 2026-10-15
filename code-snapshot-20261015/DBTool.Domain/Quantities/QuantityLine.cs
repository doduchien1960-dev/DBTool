using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public enum QuantityUnit { M2, M3, Kg }
public sealed record QuantityLine
{
    public QuantityLine(string id, string componentId, string materialId, double gross, double additions, double deductions, double net, QuantityUnit unit, string ruleId, IEnumerable<SourceRef> sources, IEnumerable<string> traceIds)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        MaterialId = ContractGuard.Text(materialId, nameof(materialId));
        Gross = ContractGuard.NonNegative(gross, nameof(gross));
        Additions = ContractGuard.NonNegative(additions, nameof(additions));
        Deductions = ContractGuard.NonNegative(deductions, nameof(deductions));
        Net = ContractGuard.NonNegative(net, nameof(net));
        Unit = ContractGuard.EnumValue(unit);
        RuleId = ContractGuard.Text(ruleId, nameof(ruleId));
        Sources = ContractGuard.Copy(sources);
        TraceIds = ContractGuard.Copy(traceIds);
        if(Math.Abs(gross+additions-deductions-net)>1e-9) throw new ArgumentException("Quantity balance mismatch.");
    }
    public string Id { get; }
    public string ComponentId { get; }
    public string MaterialId { get; }
    public double Gross { get; }
    public double Additions { get; }
    public double Deductions { get; }
    public double Net { get; }
    public QuantityUnit Unit { get; }
    public string RuleId { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public ImmutableArray<string> TraceIds { get; }
}

