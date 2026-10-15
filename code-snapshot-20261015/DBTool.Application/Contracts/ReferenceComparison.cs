using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Application;

public enum ComparisonClassification
{
    Same,
    InputMismatch,
    PolicyDifference,
    SourceFormulaDefect,
    ImplementationDefect,
    Unresolved
}

public sealed record ReferenceValue
{
    public ReferenceValue(string id, string componentId, string materialId, double value, QuantityUnit unit,
        string referenceRuleId, SourceRef source)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        MaterialId = ContractGuard.Text(materialId, nameof(materialId));
        Value = ContractGuard.Finite(value, nameof(value));
        Unit = ContractGuard.EnumValue(unit);
        ReferenceRuleId = ContractGuard.Text(referenceRuleId, nameof(referenceRuleId));
        Source = ContractGuard.Required(source);
    }

    public string Id { get; }
    public string ComponentId { get; }
    public string MaterialId { get; }
    public double Value { get; }
    public QuantityUnit Unit { get; }
    public string ReferenceRuleId { get; }
    public SourceRef Source { get; }
}

public sealed record ReferenceFixture
{
    public ReferenceFixture(string fixtureId, string canonicalInputHash, IEnumerable<ReferenceValue> values)
    {
        FixtureId = ContractGuard.Text(fixtureId, nameof(fixtureId));
        CanonicalInputHash = ContractGuard.Hash(canonicalInputHash);
        Values = ContractGuard.Copy(values);
        if (Values.IsEmpty) throw new ArgumentException("Reference fixture requires values.", nameof(values));
        if (Values.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Values.Length)
            throw new ArgumentException("Reference fixture contains duplicate values.", nameof(values));
    }

    public string FixtureId { get; }
    public string CanonicalInputHash { get; }
    public ImmutableArray<ReferenceValue> Values { get; }
}

public sealed record ComparisonPolicy
{
    public ComparisonPolicy(string policyId, string ruleVersion, double absoluteTolerance = 1e-9, double relativeTolerance = 1e-9)
    {
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        AbsoluteTolerance = ContractGuard.Positive(absoluteTolerance, nameof(absoluteTolerance));
        RelativeTolerance = ContractGuard.Positive(relativeTolerance, nameof(relativeTolerance));
    }

    public string PolicyId { get; }
    public string RuleVersion { get; }
    public double AbsoluteTolerance { get; }
    public double RelativeTolerance { get; }
}

public sealed record ComparisonLine
{
    public ComparisonLine(string id, string componentId, string materialId, QuantityUnit unit, double expectedReference,
        double? actualGeometry, double? delta, string referenceRuleId, SourceRef referenceSource,
        ComparisonClassification classification, IEnumerable<string> traceIds)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        MaterialId = ContractGuard.Text(materialId, nameof(materialId));
        Unit = ContractGuard.EnumValue(unit);
        ExpectedReference = ContractGuard.Finite(expectedReference, nameof(expectedReference));
        ActualGeometry = actualGeometry;
        Delta = delta;
        ReferenceRuleId = ContractGuard.Text(referenceRuleId, nameof(referenceRuleId));
        ReferenceSource = ContractGuard.Required(referenceSource);
        Classification = ContractGuard.EnumValue(classification);
        TraceIds = ContractGuard.Copy(traceIds);
    }

    public string Id { get; }
    public string ComponentId { get; }
    public string MaterialId { get; }
    public QuantityUnit Unit { get; }
    public double ExpectedReference { get; }
    public double? ActualGeometry { get; }
    public double? Delta { get; }
    public string ReferenceRuleId { get; }
    public SourceRef ReferenceSource { get; }
    public ComparisonClassification Classification { get; }
    public ImmutableArray<string> TraceIds { get; }
}

public sealed record ComparisonReport
{
    public ComparisonReport(string fixtureId, string policyId, IEnumerable<ComparisonLine> lines, IEnumerable<Diagnostic> diagnostics)
    {
        FixtureId = ContractGuard.Text(fixtureId, nameof(fixtureId));
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        Lines = ContractGuard.Copy(lines);
        Diagnostics = ContractGuard.Copy(diagnostics);
    }

    public string FixtureId { get; }
    public string PolicyId { get; }
    public ImmutableArray<ComparisonLine> Lines { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}
