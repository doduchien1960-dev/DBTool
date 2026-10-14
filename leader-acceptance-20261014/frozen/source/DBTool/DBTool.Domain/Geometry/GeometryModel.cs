using System.Collections.Immutable;
using DBTool.Domain.Geometry;
using DBTool.Domain.StageContractsV2;

namespace DBTool.Domain;

public sealed record GeometryModel
{
    public GeometryModel(
        IEnumerable<ComponentGeometry> components,
        IEnumerable<OpeningProfile> openings,
        IEnumerable<PurchasedCoverPlacement>? purchasedCovers = null)
        : this(components, openings, purchasedCovers, null, null, null, null, null)
    {
    }

    public GeometryModel(
        IEnumerable<ComponentGeometry> components,
        IEnumerable<OpeningProfile> openings,
        IEnumerable<PurchasedCoverPlacement>? purchasedCovers,
        IEnumerable<JointV2>? joints,
        CatchBasinGeometry? catchBasin,
        IEnumerable<DatumV2>? datums,
        IEnumerable<FrameV2>? frames,
        IEnumerable<KeyValuePair<string, string>>? topologyTrace)
    {
        Components = ContractGuard.Copy(components);
        Openings = ContractGuard.Copy(openings);
        PurchasedCovers = purchasedCovers is null ? [] : ContractGuard.Copy(purchasedCovers);
        Joints = joints is null ? [] : joints.ToImmutableArray();
        CatchBasin = catchBasin;
        Datums = datums is null ? [] : datums.ToImmutableArray();
        Frames = frames is null ? [] : frames.ToImmutableArray();
        TopologyTrace = topologyTrace is null ? ImmutableDictionary<string, string>.Empty : topologyTrace.ToImmutableDictionary();

        if (Components.Select(c=>c.Id).Distinct().Count()!=Components.Length || Openings.Select(o=>o.Id).Distinct().Count()!=Openings.Length) throw new ArgumentException("Duplicate geometry identity.");
        var ids=Components.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        if (Openings.Any(o=>!ids.Contains(o.OwnerId))) throw new ArgumentException("Unresolved opening owner.");
        foreach(var component in Components)
            foreach(var opening in component.Voids)
                if(!Openings.Contains(opening)) throw new ArgumentException("Component void absent from opening registry.");
    }

    public ImmutableArray<ComponentGeometry> Components { get; }
    public ImmutableArray<OpeningProfile> Openings { get; }
    public ImmutableArray<PurchasedCoverPlacement> PurchasedCovers { get; }
    public ImmutableArray<JointV2> Joints { get; }
    public CatchBasinGeometry? CatchBasin { get; }
    public ImmutableArray<DatumV2> Datums { get; }
    public ImmutableArray<FrameV2> Frames { get; }
    public ImmutableDictionary<string, string> TopologyTrace { get; }
}
