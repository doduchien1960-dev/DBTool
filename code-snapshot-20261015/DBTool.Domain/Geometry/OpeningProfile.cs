using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public enum OpeningKind { Round, Box, Polygon, OpenTop }
public sealed record OpeningProfile
{
    public OpeningProfile(string id, string ownerId, OpeningKind kind, Profile2Mm inner, Profile2Mm? pipeOuter, Profile2Mm concreteCutout, WallFrame? wallFrame, Frame3Mm frame, Interval1D zRange, Interval1D extrusionRange, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        OwnerId = ContractGuard.Text(ownerId, nameof(ownerId));
        Kind = ContractGuard.EnumValue(kind);
        Inner = ContractGuard.Required(inner);
        PipeOuter = pipeOuter;
        ConcreteCutout = ContractGuard.Required(concreteCutout);
        WallFrame = wallFrame;
        Frame = ContractGuard.Required(frame);
        ZRange = zRange;
        ExtrusionRange = extrusionRange;
        Sources = ContractGuard.Copy(sources);
        if ((pipeOuter is not null && wallFrame is null) ||
            (wallFrame is not null && pipeOuter is null && kind is OpeningKind.Round or OpeningKind.Box))
            throw new ArgumentException("Pipe outer and wall frame must be present together for pipe openings.");
        if (extrusionRange.Length <= 0) throw new ArgumentException("Opening depth must be positive.");
    }
    public string Id { get; }
    public string OwnerId { get; }
    public OpeningKind Kind { get; }
    public Profile2Mm Inner { get; }
    public Profile2Mm? PipeOuter { get; }
    public Profile2Mm ConcreteCutout { get; }
    public WallFrame? WallFrame { get; }
    public Frame3Mm Frame { get; }
    public Interval1D ZRange { get; }
    public Interval1D ExtrusionRange { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
