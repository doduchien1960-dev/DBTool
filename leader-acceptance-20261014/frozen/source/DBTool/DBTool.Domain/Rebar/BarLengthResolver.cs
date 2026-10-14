using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class BarLengthResolver
{
    public static double CalculateStraightRun(BarPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Segments.OfType<LineSegment3Mm>().Sum(l => BendLengthCalculator.Distance(l.Start, l.End));
    }

    public static double CalculatePhysicalFabrication(BarPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        double total = 0;
        foreach (var segment in path.Segments)
        {
            if (segment is LineSegment3Mm line)
                total += BendLengthCalculator.Distance(line.Start, line.End);
            else if (segment is ArcSegment3Mm arc)
                total += arc.CenterlineRadiusMm * Math.Abs(arc.SweepRadians);
        }
        return total + (path.LapAlreadyInPath ? 0 : path.AdditionalLapMm);
    }

    public static OperationResult<LengthResult> Resolve(double spanMm, double startCoverMm, double endCoverMm,
        HookLengths hooks, BarPath? path = null, BendPolicy? bendPolicy = null, double? diameterMm = null,
        SourceRef? source = null, MeasurementBasis? nominalBasis = null)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        if (!double.IsFinite(spanMm) || spanMm <= 0 || !double.IsFinite(startCoverMm) || startCoverMm < 0 ||
            !double.IsFinite(endCoverMm) || endCoverMm < 0 || path is not null && bendPolicy is not null ||
            nominalBasis.HasValue && !Enum.IsDefined(nominalBasis.Value))
            return Failure("BAR_LENGTH_INVALID");
        double straight = spanMm - startCoverMm - endCoverMm;
        double schedule = straight + hooks.StartMm + hooks.EndMm;
        if (straight <= 0 || !double.IsFinite(schedule) || schedule <= 0) return Failure("BAR_LENGTH_INVALID");
        int hookCount = (hooks.StartMm > 0 ? 1 : 0) + (hooks.EndMm > 0 ? 1 : 0);
        var legs = new List<double>();
        if (hooks.StartMm > 0) legs.Add(hooks.StartMm);
        legs.Add(straight);
        if (hooks.EndMm > 0) legs.Add(hooks.EndMm);
        OperationResult<FabricationLength>? actual = null;
        if (path is not null)
        {
            int pathHookCount = path.HookPattern switch
            {
                HookPattern.Hook0M => 0,
                HookPattern.Hook1M or HookPattern.HookStartOnly => 1,
                HookPattern.Hook2M => 2,
                _ => -1
            };
            if (pathHookCount != hookCount ||
                path.HookPattern == HookPattern.HookStartOnly &&
                    (hooks.StartMm <= 0 || hooks.EndMm > 0) ||
                path.HookPattern == HookPattern.Hook1M &&
                    (hooks.EndMm <= 0 || hooks.StartMm > 0))
                return Failure("BAR_HOOK_PATH_MISMATCH");
            if (hookCount > 0 && !HookPathMatches(path, legs, nominalBasis)) return Failure("BAR_PATH_MISMATCH");
            if (hookCount > 0 && !nominalBasis.HasValue) return MissingRule();
            actual = BendLengthCalculator.Calculate(path, 0, source);
        }
        else if (bendPolicy is not null && (bendPolicy.InnerRadiusMm.HasValue || hookCount == 0))
        {
            actual = BendLengthCalculator.CalculateDimensions(legs, diameterMm ?? double.NaN, bendPolicy, source);
        }

        if (actual is not null)
        {
            if (!actual.Success) return new(false, null, actual.Diagnostics);
            if (hookCount == 0 && path is not null && (path.Closed || path.Segments.Any(s => s is ArcSegment3Mm) ||
                Math.Abs(actual.Value!.Value - schedule) > 1e-6 ||
                Math.Abs(BendLengthCalculator.Distance(path.Segments[0].Start, path.Segments[^1].End) - schedule) > 1e-6))
                return Failure("BAR_PATH_MISMATCH");
            return Known(actual.Value!.Value);
        }
        return MissingRule();

        OperationResult<LengthResult> MissingRule()
        {
            var diagnostic = new Diagnostic("FABRICATION_RULE_MISSING", DiagnosticSeverity.Review, DiagnosticScope.Global,
                null, null, source, "real centerline and nominal basis, or explicit bend radius/basis", null,
                "Nominal schedule length retained; fabrication length is unknown.");
            return new(true, new LengthResult(schedule, null, LengthStatus.NeedsRule, [diagnostic]), [diagnostic]);
        }
        OperationResult<LengthResult> Known(double length) => new(true, new LengthResult(schedule, length, LengthStatus.Known, []), []);
        OperationResult<LengthResult> Failure(string code) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source,
                "positive clear span and consistent hook/path inputs", null, "Invalid bar length request.")]);
    }

    private static bool HookPathMatches(BarPath path, IReadOnlyList<double> expectedLegs, MeasurementBasis? basis)
    {
        if (path.Closed || path.Segments.Length != 2 * expectedLegs.Count - 1) return false;
        var measured = new double[expectedLegs.Count];
        for (int i = 0; i < expectedLegs.Count; i++)
        {
            if (path.Segments[2 * i] is not LineSegment3Mm line) return false;
            measured[i] = BendLengthCalculator.Distance(line.Start, line.End);
            if (!double.IsFinite(measured[i]) || measured[i] <= 0) return false;
        }
        for (int i = 0; i < expectedLegs.Count - 1; i++)
        {
            if (path.Segments[2 * i + 1] is not ArcSegment3Mm arc) return false;
            var before = (LineSegment3Mm)path.Segments[2 * i];
            var after = (LineSegment3Mm)path.Segments[2 * i + 2];
            if (!Tangent(before, arc, atStart: true) || !Tangent(after, arc, atStart: false)) return false;
            if (basis == MeasurementBasis.TheoreticalCorner)
            {
                if (Math.Abs(arc.SweepRadians) >= Math.PI) return false;
                double trim = arc.CenterlineRadiusMm * Math.Tan(Math.Abs(arc.SweepRadians) / 2);
                measured[i] += trim;
                measured[i + 1] += trim;
            }
        }
        return !basis.HasValue || measured.Zip(expectedLegs).All(p => double.IsFinite(p.First) && Math.Abs(p.First - p.Second) <= 1e-6);
    }

    private static bool Tangent(LineSegment3Mm line, ArcSegment3Mm arc, bool atStart)
    {
        double length = BendLengthCalculator.Distance(line.Start, line.End);
        var direction = new Vector3((line.End.X - line.Start.X) / length,
            (line.End.Y - line.Start.Y) / length, (line.End.Z - line.Start.Z) / length);
        var point = atStart ? arc.Start : arc.End;
        var radial = new Vector3((point.X - arc.Center.X) / arc.CenterlineRadiusMm,
            (point.Y - arc.Center.Y) / arc.CenterlineRadiusMm, (point.Z - arc.Center.Z) / arc.CenterlineRadiusMm);
        double sign = Math.Sign(arc.SweepRadians);
        var tangent = new Vector3(sign * (arc.Normal.Y * radial.Z - arc.Normal.Z * radial.Y),
            sign * (arc.Normal.Z * radial.X - arc.Normal.X * radial.Z),
            sign * (arc.Normal.X * radial.Y - arc.Normal.Y * radial.X));
        return Math.Abs(FrameInvariant.Dot(direction, tangent) - 1) <= 1e-9;
    }
}
