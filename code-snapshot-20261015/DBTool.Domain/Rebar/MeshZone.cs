using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>A uniform distribution zone for bars running along Axis.</summary>
public sealed record MeshZone
{
    public MeshZone(string id, string componentId, Axis axis, Interval1D distributionInterval,
        Interval1D runInterval, string sourceRuleId, IEnumerable<double> positionsMm, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        Axis = ContractGuard.EnumValue(axis);
        DistributionInterval = distributionInterval;
        RunInterval = runInterval;
        SourceRuleId = ContractGuard.Text(sourceRuleId, nameof(sourceRuleId));
        PositionsMm = ContractGuard.Copy(positionsMm);
        Sources = ContractGuard.Copy(sources);
        if (DistributionInterval.Length <= 0 || RunInterval.Length <= 0 || PositionsMm.IsEmpty)
            throw new ArgumentException("Mesh zone requires positive intervals and placements.");
        for (int i = 0; i < PositionsMm.Length; i++)
        {
            double position = ContractGuard.Finite(PositionsMm[i], nameof(positionsMm));
            if (position < DistributionInterval.Start || position > DistributionInterval.End ||
                i > 0 && position <= PositionsMm[i - 1])
                throw new ArgumentException("Mesh placements must increase within the distribution interval.");
        }
    }

    public string Id { get; }
    public string ComponentId { get; }
    public Axis Axis { get; }
    public Interval1D DistributionInterval { get; }
    public Interval1D RunInterval { get; }
    public string SourceRuleId { get; }
    public ImmutableArray<double> PositionsMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record MeshZonePlan
{
    public MeshZonePlan(IEnumerable<MeshZone> zones)
    {
        Zones = ContractGuard.Copy(zones);
        if (Zones.IsEmpty || Zones.Select(z => z.Id).Distinct(StringComparer.Ordinal).Count() != Zones.Length)
            throw new ArgumentException("Mesh zone IDs must be present and unique.");
    }

    public ImmutableArray<MeshZone> Zones { get; }
}
