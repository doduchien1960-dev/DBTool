using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class AnchorPlanner
{
    public static OperationResult<RebarLayout> PlanAnchors(IReadOnlyList<AnchorRun> runs, AnchorSpec spec, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(runs); ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(policy);
        if (runs.Count == 0) return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Disabled), []);
        var bars = new List<BarOccurrence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            var segments = Segments(run);
            double perimeter = segments.Sum(x => Distance(x.Start, x.End));
            if (!double.IsFinite(perimeter) || perimeter <= 0) return Block("ANCHOR_RUN_INVALID", "Anchor run has no positive length.", run);
            int count = (int)Math.Ceiling(perimeter / spec.SpacingMm);
            for (int i = 0; i < count; i++)
            {
                double station = i * perimeter / count;
                var location = PointAt(segments, station);
                string key = $"{run.FrameId}|{location.X:R}|{location.Y:R}|{location.Z:R}";
                if (!seen.Add(key)) continue;
                var end = new Point3Mm(location.X, location.Y, location.Z + spec.LengthMm);
                var path = new BarPath([new LineSegment3Mm(location, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
                bars.Add(new BarOccurrence($"{run.RunId}/Anchor/{bars.Count:000}", null, run.ComponentId, run.ComponentKind,
                    run.Layer, Axis.X, spec.DiameterMm, path, spec.LengthMm, spec.LengthMm, LengthStatus.Known,
                    "ANCHOR-RUN-S2-001", run.Sources, RebarDirection.Supplementary, run.FrameId,
                    null, "Anchor", MeasurementBasisKind.Centerline));
            }
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static IReadOnlyList<PathSegment> Segments(AnchorRun run)
    {
        var points = run.Points.ToArray();
        var segments = new List<PathSegment>();
        for (int i = 1; i < points.Length; i++) segments.Add(new LineSegment3Mm(points[i - 1], points[i]));
        if (run.Closed) segments.Add(new LineSegment3Mm(points[^1], points[0]));
        return segments;
    }

    private static Point3Mm PointAt(IReadOnlyList<PathSegment> segments, double station)
    {
        double cursor = 0;
        foreach (var segment in segments)
        {
            double length = Distance(segment.Start, segment.End);
            if (station <= cursor + length + 1e-9)
            {
                double t = length == 0 ? 0 : (station - cursor) / length;
                return new Point3Mm(segment.Start.X + (segment.End.X - segment.Start.X) * t,
                    segment.Start.Y + (segment.End.Y - segment.Start.Y) * t,
                    segment.Start.Z + (segment.End.Z - segment.Start.Z) * t);
            }
            cursor += length;
        }
        return segments[^1].End;
    }

    private static double Distance(Point3Mm a, Point3Mm b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

    private static OperationResult<RebarLayout> Block(string code, string message, AnchorRun run) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Component, run.ComponentId, run.ComponentId,
            run.Sources.FirstOrDefault(), null, null, message)]);
}
