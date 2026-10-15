using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>A measured horizontal underside edge of the frame, in native section coordinates.
/// This is contact geometry, not a required bearing width or a structural approval.</summary>
public sealed record CoverContactSegment
{
    public CoverContactSegment(Point2Mm start, Point2Mm end)
    {
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) ||
            !double.IsFinite(end.X) || !double.IsFinite(end.Y) ||
            end.X <= start.X || Math.Abs(start.Y-end.Y)>1e-6)
            throw new ArgumentException("Contact edge requires finite ordered endpoints and a horizontal underside.");
        Start=start;
        End=end;
    }
    public Point2Mm Start { get; }
    public Point2Mm End { get; }
}

public sealed record CoverContactSection
{
    public CoverContactSection(string view, IEnumerable<CoverContactSegment> segments, IEnumerable<SourceRef> sources)
    {
        if (view is not ("AA" or "BB")) throw new ArgumentException("Contact belongs to a native AA or BB section.",nameof(view));
        View=view;
        Segments=ContractGuard.Copy(segments);
        Sources=ContractGuard.Copy(sources);
        if (Segments.IsEmpty || Sources.IsEmpty)
            throw new ArgumentException("Contact section requires actual edges and provenance.");
    }
    public string View { get; }
    public ImmutableArray<CoverContactSegment> Segments { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
