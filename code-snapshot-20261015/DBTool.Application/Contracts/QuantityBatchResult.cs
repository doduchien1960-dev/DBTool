using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Application;

/// <summary>Independent quantity packages; it never masquerades as a steel Full package.</summary>
public sealed record QuantityBatchResult
{
    public QuantityBatchResult(IEnumerable<CalculationPackage> packages)
    {
        Packages = ContractGuard.Copy(packages);
        if (Packages.IsEmpty || Packages.Any(x => x.Scope is not (CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly)) ||
            Packages.Select(x => x.Scope).Distinct().Count() != Packages.Length)
            throw new ArgumentException("Quantity batch requires one independent package per quantity scope.", nameof(packages));
    }

    public ImmutableArray<CalculationPackage> Packages { get; }
}
