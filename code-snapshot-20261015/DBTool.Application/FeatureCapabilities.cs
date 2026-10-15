using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Application;

public enum CapabilityStatus { Supported, Unsupported, NeedsRule, NotRequested }

/// <summary>Explicit consumer inventory supplied by the caller. T9 registers no production set.</summary>
public sealed class CapabilitySet
{
    public CapabilitySet(IEnumerable<string> featureIds)
    {
        ArgumentNullException.ThrowIfNull(featureIds);
        var ids = featureIds.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Feature IDs must be unique and nonblank.", nameof(featureIds));
        FeatureIds = ids.ToImmutableHashSet(StringComparer.Ordinal);
    }

    public ImmutableHashSet<string> FeatureIds { get; }
    public bool HasConsumer(string featureId) => FeatureIds.Contains(featureId);
}

public sealed record CapabilityResult
{
    public CapabilityResult(string featureId, bool requested, CapabilityStatus status, Diagnostic? diagnostic)
    {
        FeatureId = ContractGuard.Text(featureId, nameof(featureId));
        Status = ContractGuard.EnumValue(status);
        if (requested == (status == CapabilityStatus.NotRequested))
            throw new ArgumentException("Requested and status disagree.");
        if ((status is CapabilityStatus.Unsupported or CapabilityStatus.NeedsRule) != (diagnostic is not null))
            throw new ArgumentException("Unsupported features need one diagnostic.");
        Requested = requested;
        Diagnostic = diagnostic;
    }

    public string FeatureId { get; }
    public bool Requested { get; }
    public CapabilityStatus Status { get; }
    public Diagnostic? Diagnostic { get; }
}

public sealed record FeatureSupportReport
{
    public FeatureSupportReport(CalculationScope scope, IEnumerable<CapabilityResult> results)
    {
        Scope = ContractGuard.EnumValue(scope);
        Results = ContractGuard.Copy(results);
        if (Results.Select(x => x.FeatureId).Distinct(StringComparer.Ordinal).Count() != Results.Length)
            throw new ArgumentException("Duplicate feature ID.", nameof(results));
        ConcreteStatus = CalculationStatus.NotCalculated;
        FormworkStatus = CalculationStatus.NotCalculated;
    }

    public CalculationScope Scope { get; }
    public ImmutableArray<CapabilityResult> Results { get; }
    public CalculationStatus ConcreteStatus { get; }
    public CalculationStatus FormworkStatus { get; }
}
