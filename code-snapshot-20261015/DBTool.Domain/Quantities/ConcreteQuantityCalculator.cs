using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record QuantityTrace
{
    public QuantityTrace(string id, string componentId, string operation, double magnitude, QuantityUnit unit,
        string algorithm, IEnumerable<string> traceIds)
    {
        Id = ContractGuard.Text(id, nameof(id));
        ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        Operation = ContractGuard.Text(operation, nameof(operation));
        Magnitude = ContractGuard.NonNegative(magnitude, nameof(magnitude));
        Unit = ContractGuard.EnumValue(unit);
        Algorithm = ContractGuard.Text(algorithm, nameof(algorithm));
        TraceIds = ContractGuard.Copy(traceIds);
    }

    public string Id { get; }
    public string ComponentId { get; }
    public string Operation { get; }
    public double Magnitude { get; }
    public QuantityUnit Unit { get; }
    public string Algorithm { get; }
    public ImmutableArray<string> TraceIds { get; }
}

public sealed record ConcreteQuantityResult
{
    public ConcreteQuantityResult(IEnumerable<QuantityLine> lines, IEnumerable<QuantityTrace> trace)
    {
        Lines = ContractGuard.Copy(lines);
        Trace = ContractGuard.Copy(trace);
    }

    public ImmutableArray<QuantityLine> Lines { get; }
    public ImmutableArray<QuantityTrace> Trace { get; }
}

/// <summary>
/// Pure concrete quantity calculator. All geometry remains in millimetres internally;
/// only the immutable result is expressed in m³.
/// </summary>
public sealed class ConcreteQuantityCalculator
{
    private const double Mm3PerM3 = 1_000_000_000d;

    public OperationResult<ConcreteQuantityResult> Calculate(GeometryModel geometry,
        QuantityPolicy policy, TolerancePolicy tolerance)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(tolerance);

        var diagnostics = new List<Diagnostic>();
        var trace = new List<QuantityTrace>();
        var states = new Dictionary<(string ComponentId, string MaterialId), LineState>();

        foreach (var component in geometry.Components.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (IsPurchasedCastIron(component.MaterialId))
                continue;
            if (!policy.IncludeMortar && string.Equals(component.MaterialId, policy.MortarMaterialId, StringComparison.Ordinal))
                continue;

            string lineComponentId = component.Occupied is HaunchSolid haunch
                ? haunch.Owner == HaunchOwner.Base ? "Base" : "Roof"
                : component.Id;
            var key = (lineComponentId, component.MaterialId);
            if (!states.TryGetValue(key, out var state))
            {
                state = new LineState(lineComponentId, component.MaterialId);
                states.Add(key, state);
            }

            if (component.Occupied is HaunchSolid haunchSolid)
            {
                double additions = haunchSolid.HeightMm *
                    ((haunchSolid.ChamberWidthMm + haunchSolid.ChamberLengthMm) * haunchSolid.WidthMm -
                     4d * haunchSolid.WidthMm * haunchSolid.WidthMm / 3d);
                if (!FiniteNonNegative(additions))
                {
                    diagnostics.Add(Block("QUANTITY_SOLID_INVALID", component.Id, "finite haunch volume", additions.ToString()));
                    continue;
                }
                state.AdditionsMm3 += additions;
                trace.Add(new QuantityTrace($"{component.Id}/additions", lineComponentId, "Additions",
                    additions / Mm3PerM3, QuantityUnit.M3, "haunch-analytic", component.Sources.Select(SourceId)));
                continue;
            }

            if (!TryMeasureSolid(component.Occupied, out double gross, out string? solidError))
            {
                diagnostics.Add(Block("QUANTITY_SOLID_UNSUPPORTED", component.Id, "supported solid descriptor", solidError!));
                continue;
            }
            var openings = component.Voids
                .GroupBy(OpeningSignature, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            bool mortarWithPipeOpening = openings.Length > 0 &&
                string.Equals(component.MaterialId, policy.MortarMaterialId, StringComparison.Ordinal);
            if (!mortarWithPipeOpening)
            {
                state.GrossMm3 += gross;
                trace.Add(new QuantityTrace($"{component.Id}/gross", lineComponentId, "Gross", gross / Mm3PerM3,
                    QuantityUnit.M3, solidError ?? "solid-measure", component.Sources.Select(SourceId)));
            }
            if (openings.Length == 0) continue;
            if (string.Equals(component.MaterialId, policy.MortarMaterialId, StringComparison.Ordinal))
            {
                if (!TryMeasureMortar(component, openings, tolerance, out double mortar, out var mortarAlgorithm, out var mortarError))
                {
                    diagnostics.Add(Block("QUANTITY_OPENING_UNSUPPORTED", component.Id, "pipe outer and concrete cutout", mortarError!));
                    continue;
                }
                state.AdditionsMm3 += mortar;
                trace.Add(new QuantityTrace($"{component.Id}/mortar", lineComponentId, "Additions", mortar / Mm3PerM3,
                    QuantityUnit.M3, mortarAlgorithm!, openings.Select(x => x.Id)));
            }
            else if (!TryMeasureOpenings(geometry, component, openings, tolerance, out double deductions,
                out var algorithm, out var openingError))
            {
                diagnostics.Add(Block("QUANTITY_OPENING_UNSUPPORTED", component.Id, "supported union/intersection topology", openingError!));
                continue;
            }
            else
            {
                state.DeductionsMm3 += deductions;
                trace.Add(new QuantityTrace($"{component.Id}/deductions", lineComponentId, "Deduction",
                    deductions / Mm3PerM3, QuantityUnit.M3, algorithm!, openings.Select(x => x.Id)));
            }
        }

        if (diagnostics.Any(x => x.Severity == DiagnosticSeverity.Block))
            return new(false, null, diagnostics);

        var lines = new List<QuantityLine>();
        foreach (var state in states.Values.OrderBy(x => x.ComponentId, StringComparer.Ordinal).ThenBy(x => x.MaterialId, StringComparer.Ordinal))
        {
            double net = state.GrossMm3 + state.AdditionsMm3 - state.DeductionsMm3;
            if (!Finite(net) || net < 0)
            {
                diagnostics.Add(Block("QUANTITY_NEGATIVE_NET", state.ComponentId, ">= 0", net.ToString()));
                continue;
            }
            lines.Add(new QuantityLine(
                $"Concrete/{state.ComponentId}/{state.MaterialId}", state.ComponentId, state.MaterialId,
                state.GrossMm3 / Mm3PerM3, state.AdditionsMm3 / Mm3PerM3, state.DeductionsMm3 / Mm3PerM3,
                net / Mm3PerM3, QuantityUnit.M3, policy.RuleVersion,
                [], trace.Where(x => x.ComponentId == state.ComponentId).SelectMany(x => x.TraceIds).Distinct(StringComparer.Ordinal)));
        }
        if (diagnostics.Any(x => x.Severity == DiagnosticSeverity.Block))
            return new(false, null, diagnostics);
        return new(true, new ConcreteQuantityResult(lines, trace), diagnostics);

        Diagnostic Block(string code, string componentId, string expected, string actual) =>
            new(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                geometry.Components.FirstOrDefault(x => x.Id == componentId ||
                    (componentId is "Base" or "Roof" && x.Occupied is HaunchSolid haunch &&
                        ((componentId == "Base" && haunch.Owner == HaunchOwner.Base) ||
                         (componentId == "Roof" && haunch.Owner == HaunchOwner.Roof))))?.Sources.FirstOrDefault(),
                expected, componentId + ": " + actual, "Concrete quantity cannot be calculated.");
    }

    private static bool TryMeasureSolid(SolidDescriptor solid, out double volume, out string? error)
    {
        error = null;
        switch (solid)
        {
            case ExtrusionSolid extrusion:
                volume = FootprintArea(extrusion.Footprint) * extrusion.Depth.Length;
                error = "solid-extrusion";
                return FiniteNonNegative(volume);
            case UnionSolid union:
            {
                var result = RectangularSolidUnion.Measure(union);
                if (result.Success)
                {
                    volume = result.Value!.Value;
                    error = "solid-rectangular-union";
                    return true;
                }
                volume = 0;
                error = result.Diagnostics.FirstOrDefault()?.Actual ?? "union unsupported";
                return false;
            }
            case DifferenceSolid difference:
                if (!TryMeasureSolid(difference.Occupied, out double occupied, out _))
                {
                    volume = 0; error = "difference occupied solid unsupported"; return false;
                }
                double removed = 0;
                foreach (var cut in difference.Cuts)
                {
                    if (!TryMeasureCutSolid(cut, out double cutVolume))
                    {
                        volume = 0; error = "difference cut unsupported"; return false;
                    }
                    removed += cutVolume;
                }
                volume = occupied - removed;
                error = "solid-difference";
                return FiniteNonNegative(volume);
            case NeckSeatCutSolid:
                volume = 0; error = "seat cut is only valid inside DifferenceSolid"; return false;
            default:
                volume = 0; error = "solid descriptor is outside quantity roadmap"; return false;
        }
    }

    private static bool TryMeasureCutSolid(SolidDescriptor cut, out double volume)
    {
        if (cut is NeckSeatCutSolid seat)
        {
            double width = seat.Opening.MaxX - seat.Opening.MinX;
            double length = seat.Opening.MaxY - seat.Opening.MinY;
            double area = seat.Kind switch
            {
                NeckSeatKind.Step => (width + 2 * seat.ExpansionMm) * (length + 2 * seat.ExpansionMm) - width * length,
                NeckSeatKind.Slope => (width + length) * seat.ExpansionMm + 4 * seat.ExpansionMm * seat.ExpansionMm / 3,
                _ => 0
            };
            volume = area * seat.ZRange.Length;
            return FiniteNonNegative(volume);
        }
        return TryMeasureSolid(cut, out volume, out _);
    }

    private static bool TryMeasureOpenings(GeometryModel geometry, ComponentGeometry component,
        IReadOnlyList<OpeningProfile> openings, TolerancePolicy tolerance, out double volume,
        out string? algorithm, out string? error)
    {
        volume = 0; algorithm = null; error = null;
        var framed = openings.Where(x => x.WallFrame is not null).ToArray();
        var planar = openings.Where(x => x.WallFrame is null).ToArray();
        if (framed.Length > 0 && planar.Length > 0)
        { error = "mixed wall-frame and plan cuts"; return false; }
        if (framed.Length > 0)
        {
            var frames = framed.Select(x => x.WallFrame!).Distinct().ToArray();
            if (frames.Length == 1 && TryLocalBounds(component, frames[0], out var bounds))
            {
                volume = MeasureFramedUnion(framed, bounds, component.ZRange, tolerance);
                algorithm = "circle-union-event-integration";
                return FiniteNonNegative(volume);
            }
            if (framed.All(x => x.ConcreteCutout is CircleProfile) && TryCrossFrameUnion(geometry, component, framed, out volume))
            {
                algorithm = "cross-frame-analytic-union";
                return true;
            }
            error = "different wall frames or nonrectangular owner outside exact kernel";
            return false;
        }
        if (!planar.All(x => x.ConcreteCutout is RectangleProfile or PolygonProfile or MultiCellProfile or OpenTopProfile or CircleProfile))
        { error = "planar profile outside supported arrangement"; return false; }
        volume = MeasurePlanUnion(component, planar, tolerance);
        algorithm = planar.Any(x => x.ConcreteCutout is CircleProfile) ? "planar-arrangement-circle-integration" : "planar-arrangement";
        return FiniteNonNegative(volume);
    }

    private static bool TryMeasureMortar(ComponentGeometry component, IReadOnlyList<OpeningProfile> openings,
        TolerancePolicy tolerance, out double volume, out string? algorithm, out string? error)
    {
        volume = 0; algorithm = "mortar-annulus"; error = null;
        foreach (var opening in openings)
        {
            if (opening.PipeOuter is null)
            { error = "mortar requires pipe outer profile"; return false; }
            var outer = ReplaceCutout(opening, opening.ConcreteCutout);
            var pipe = ReplaceCutout(opening, opening.PipeOuter);
            if (!TryMeasureOpenings(new GeometryModel([WithoutVoids(component)], []), component, [outer], tolerance,
                out double cut, out _, out error) ||
                !TryMeasureOpenings(new GeometryModel([WithoutVoids(component)], []), component, [pipe], tolerance,
                out double pipeVolume, out _, out error))
                return false;
            volume += cut - pipeVolume;
        }
        return FiniteNonNegative(volume);
    }

    private static double MeasurePlanUnion(ComponentGeometry component, IReadOnlyList<OpeningProfile> openings,
        TolerancePolicy tolerance)
    {
        var zEvents = new List<double> { component.ZRange.Start, component.ZRange.End };
        zEvents.AddRange(openings.SelectMany(x => new[] { x.ZRange.Start, x.ZRange.End })
            .Where(z => z > component.ZRange.Start && z < component.ZRange.End));
        var ordered = zEvents.Distinct().Order().ToArray();
        double result = 0;
        for (int i = 0; i < ordered.Length - 1; i++)
        {
            double z0 = ordered[i], z1 = ordered[i + 1];
            var active = openings.Where(x => x.ZRange.Start < z1 && x.ZRange.End > z0)
                .Select(x => x.ConcreteCutout).ToArray();
            if (active.Length == 0) continue;
            result += MeasureAreaUnionWithinOwner(active, component.Footprint, tolerance) * (z1 - z0);
        }
        return result;
    }

    private static double MeasureFramedUnion(IReadOnlyList<OpeningProfile> openings, LocalBounds bounds,
        Interval1D zRange, TolerancePolicy tolerance)
    {
        var depthEvents = new List<double> { bounds.V0, bounds.V1 };
        depthEvents.AddRange(openings.SelectMany(x => new[] { x.ExtrusionRange.Start, x.ExtrusionRange.End })
            .Where(v => v > bounds.V0 && v < bounds.V1));
        var ordered = depthEvents.Distinct().Order().ToArray();
        double result = 0;
        for (int i = 0; i < ordered.Length - 1; i++)
        {
            double v0 = ordered[i], v1 = ordered[i + 1];
            var active = openings.Where(x => x.ExtrusionRange.Start < v1 && x.ExtrusionRange.End > v0)
                .Select(x => x.ConcreteCutout).ToArray();
            if (active.Length == 0) continue;
            result += MeasureAreaUnion(active, null, bounds.U0, bounds.U1, zRange.Start, zRange.End, tolerance) * (v1 - v0);
        }
        return result;
    }

    private static bool TryCrossFrameUnion(GeometryModel geometry, ComponentGeometry component,
        IReadOnlyList<OpeningProfile> openings, out double volume)
    {
        volume = 0;
        if (openings.Count < 2) return false;
        var cuts = openings.Select(x => new RoundOpeningCut(x.WallFrame!, (CircleProfile)x.ConcreteCutout, x.ExtrusionRange));
        var solid = new OpeningSolid("quantity/" + component.Id, cuts, openings.SelectMany(x => x.Sources));
        var localModel = new GeometryModel([component], openings);
        var result = OpeningOwnershipPartitioner.Partition(localModel, solid, new OpeningOwnershipPolicy([component.Id]));
        var child = result.Success ? result.Value!.Children.FirstOrDefault(x => x.OwnerId == component.Id) : null;
        if (child is null) return false;
        volume = child.VolumeMm3;
        return FiniteNonNegative(volume);
    }

    private static double MeasureAreaUnionWithinOwner(IEnumerable<Profile2Mm> shapes, Footprint owner, TolerancePolicy tolerance)
    {
        var outer = owner.Outer.Vertices.ToArray();
        double x0 = outer.Min(x => x.X), x1 = outer.Max(x => x.X);
        return MeasureAreaUnion(shapes, owner, x0, x1, outer.Min(x => x.Y), outer.Max(x => x.Y), tolerance);
    }

    private static double MeasureAreaUnion(IEnumerable<Profile2Mm> shapes, Footprint? owner,
        double x0, double x1, double y0, double y1, TolerancePolicy tolerance)
    {
        var shapeList = shapes.ToArray();
        var events = new List<double> { x0, x1 };
        foreach (var shape in shapeList) AddXEvents(shape, events, x0, x1, y0, y1);
        var ordered = events.Where(x => x >= x0 && x <= x1).Distinct().Order().ToArray();
        double area = 0;
        for (int i = 0; i < ordered.Length - 1; i++)
        {
            double a = ordered[i], b = ordered[i + 1];
            if (b <= a) continue;
            area += AdaptiveSimpson(x => UnionHeightAtX(shapeList, owner, x, y0, y1), a, b,
                Math.Max(1e-8, tolerance.VolumeMm3.GetValueOrDefault(1) / 100));
        }
        return Math.Max(0, area);
    }

    private static double UnionHeightAtX(IReadOnlyList<Profile2Mm> shapes, Footprint? owner,
        double x, double y0, double y1)
    {
        var cutIntervals = new List<(double Low, double High)>();
        foreach (var shape in shapes) cutIntervals.AddRange(ProfileIntervals(shape, x));
        if (cutIntervals.Count == 0) return 0;
        var union = Merge(cutIntervals);
        if (owner is null) return union.Sum(x => Math.Max(0, Math.Min(y1, x.High) - Math.Max(y0, x.Low)));
        var ownerIntervals = FootprintIntervals(owner, x);
        return ownerIntervals.Sum(ownerPart => Merge(union.Select(cut =>
            (Low: Math.Max(ownerPart.Low, Math.Max(y0, cut.Low)),
             High: Math.Min(ownerPart.High, Math.Min(y1, cut.High))))
            .Where(x => x.High > x.Low)).Sum(x => x.High - x.Low));
    }

    private static IEnumerable<(double Low, double High)> ProfileIntervals(Profile2Mm profile, double x)
    {
        switch (profile)
        {
            case CircleProfile circle:
                double dx = x - circle.Center.X;
                if (Math.Abs(dx) < circle.RadiusMm)
                {
                    double half = Math.Sqrt(Math.Max(0, circle.RadiusMm * circle.RadiusMm - dx * dx));
                    yield return (circle.Center.Y - half, circle.Center.Y + half);
                }
                yield break;
            case RectangleProfile rectangle:
                if (x > rectangle.Rectangle.MinX && x < rectangle.Rectangle.MaxX)
                    yield return (rectangle.Rectangle.MinY, rectangle.Rectangle.MaxY);
                yield break;
            case PolygonProfile polygon:
                foreach (var interval in PolygonIntervals(polygon.Polygon, x)) yield return interval;
                yield break;
            case OpenTopProfile openTop:
                foreach (var interval in PolygonIntervals(openTop.Polygon, x)) yield return interval;
                yield break;
            case MultiCellProfile cells:
                foreach (var cell in cells.Cells)
                    foreach (var interval in PolygonIntervals(cell.Polygon, x)) yield return interval;
                yield break;
            default:
                yield break;
        }
    }

    private static IEnumerable<(double Low, double High)> FootprintIntervals(Footprint footprint, double x)
    {
        var result = Merge(PolygonIntervals(footprint.Outer, x));
        foreach (var hole in footprint.InnerLoops)
            result = Subtract(result, Merge(PolygonIntervals(hole, x))).ToList();
        return result;
    }

    private static IEnumerable<(double Low, double High)> PolygonIntervals(Polygon2Mm polygon, double x)
    {
        var intersections = new List<double>();
        for (int i = 0; i < polygon.Vertices.Length; i++)
        {
            var a = polygon.Vertices[i];
            var b = polygon.Vertices[(i + 1) % polygon.Vertices.Length];
            if ((a.X <= x && x < b.X) || (b.X <= x && x < a.X))
            {
                double t = (x - a.X) / (b.X - a.X);
                intersections.Add(a.Y + t * (b.Y - a.Y));
            }
        }
        intersections.Sort();
        for (int i = 0; i + 1 < intersections.Count; i += 2)
            if (intersections[i + 1] > intersections[i]) yield return (intersections[i], intersections[i + 1]);
    }

    private static IEnumerable<(double Low, double High)> Subtract(
        IEnumerable<(double Low, double High)> source, IEnumerable<(double Low, double High)> cuts)
    {
        var current = source.ToList();
        foreach (var cut in cuts)
        {
            var next = new List<(double Low, double High)>();
            foreach (var part in current)
            {
                if (cut.High <= part.Low || cut.Low >= part.High) { next.Add(part); continue; }
                if (cut.Low > part.Low) next.Add((part.Low, Math.Min(cut.Low, part.High)));
                if (cut.High < part.High) next.Add((Math.Max(cut.High, part.Low), part.High));
            }
            current = next.Where(x => x.High > x.Low).ToList();
        }
        return current;
    }

    private static List<(double Low, double High)> Merge(IEnumerable<(double Low, double High)> intervals)
    {
        var ordered = intervals.Where(x => x.High > x.Low).OrderBy(x => x.Low).ThenBy(x => x.High).ToArray();
        var result = new List<(double Low, double High)>();
        foreach (var interval in ordered)
        {
            if (result.Count == 0 || interval.Low > result[^1].High)
                result.Add(interval);
            else if (interval.High > result[^1].High)
                result[^1] = (result[^1].Low, interval.High);
        }
        return result;
    }

    private static void AddXEvents(Profile2Mm shape, ICollection<double> events, double x0, double x1, double y0, double y1)
    {
        switch (shape)
        {
            case CircleProfile circle:
                events.Add(circle.Center.X - circle.RadiusMm); events.Add(circle.Center.X + circle.RadiusMm);
                foreach (double y in new[] { y0, y1 })
                {
                    double d = y - circle.Center.Y;
                    if (Math.Abs(d) < circle.RadiusMm)
                    {
                        var dx = Math.Sqrt(circle.RadiusMm * circle.RadiusMm - d * d);
                        events.Add(circle.Center.X - dx); events.Add(circle.Center.X + dx);
                    }
                }
                break;
            case RectangleProfile rectangle:
                events.Add(rectangle.Rectangle.MinX); events.Add(rectangle.Rectangle.MaxX); break;
            case PolygonProfile polygon:
                foreach (var point in polygon.Polygon.Vertices) events.Add(point.X); break;
            case OpenTopProfile openTop:
                foreach (var point in openTop.Polygon.Vertices) events.Add(point.X); break;
            case MultiCellProfile cells:
                foreach (var cell in cells.Cells) foreach (var point in cell.Polygon.Vertices) events.Add(point.X); break;
        }
    }

    private static bool TryLocalBounds(ComponentGeometry component, WallFrame frame, out LocalBounds bounds)
    {
        bounds = default;
        if (component.Footprint.InnerLoops.Length != 0) return false;
        var local = component.Footprint.Outer.Vertices.Select(point => frame.ToLocal(new Point3Mm(point.X, point.Y, 0))).ToArray();
        bounds = new LocalBounds(local.Min(x => x.U), local.Max(x => x.U), local.Min(x => x.V), local.Max(x => x.V));
        return bounds.U1 > bounds.U0 && bounds.V1 > bounds.V0;
    }

    private static double FootprintArea(Footprint footprint) =>
        Math.Abs(SignedArea(footprint.Outer)) - footprint.InnerLoops.Sum(x => Math.Abs(SignedArea(x)));

    private static double SignedArea(Polygon2Mm polygon) =>
        Enumerable.Range(0, polygon.Vertices.Length).Sum(i =>
            polygon.Vertices[i].X * polygon.Vertices[(i + 1) % polygon.Vertices.Length].Y -
            polygon.Vertices[(i + 1) % polygon.Vertices.Length].X * polygon.Vertices[i].Y) / 2;

    private static double AdaptiveSimpson(Func<double, double> f, double a, double b, double eps)
    {
        double c = (a + b) / 2, fa = f(a), fb = f(b), fc = f(c);
        double whole = Simpson(fa, fb, fc, a, b);
        return Adaptive(f, a, b, eps, whole, fa, fb, fc, 0);
    }

    private static double Adaptive(Func<double, double> f, double a, double b, double eps,
        double whole, double fa, double fb, double fc, int depth)
    {
        double c = (a + b) / 2, leftMid = (a + c) / 2, rightMid = (c + b) / 2;
        double fLeft = f(leftMid), fRight = f(rightMid);
        double left = Simpson(fa, fc, fLeft, a, c), right = Simpson(fc, fb, fRight, c, b);
        double delta = left + right - whole;
        if (depth >= 24 || Math.Abs(delta) <= 15 * eps) return left + right + delta / 15;
        return Adaptive(f, a, c, eps / 2, left, fa, fc, fLeft, depth + 1) +
            Adaptive(f, c, b, eps / 2, right, fc, fb, fRight, depth + 1);
    }

    private static double Simpson(double fa, double fb, double fc, double a, double b) =>
        (b - a) * (fa + 4 * fc + fb) / 6;

    private static string OpeningSignature(OpeningProfile opening) =>
        $"{opening.Kind}|{opening.ExtrusionRange.Start:R}|{opening.ExtrusionRange.End:R}|{opening.ZRange.Start:R}|{opening.ZRange.End:R}|{ProfileSignature(opening.ConcreteCutout)}|{opening.WallFrame}";

    private static string ProfileSignature(Profile2Mm profile) => profile switch
    {
        CircleProfile circle => $"C:{circle.Center.X:R},{circle.Center.Y:R},{circle.RadiusMm:R}",
        RectangleProfile rectangle => $"R:{rectangle.Rectangle.MinX:R},{rectangle.Rectangle.MinY:R},{rectangle.Rectangle.MaxX:R},{rectangle.Rectangle.MaxY:R}",
        PolygonProfile polygon => "P:" + string.Join(';', polygon.Polygon.Vertices.Select(x => $"{x.X:R},{x.Y:R}")),
        OpenTopProfile openTop => "O:" + string.Join(';', openTop.Polygon.Vertices.Select(x => $"{x.X:R},{x.Y:R}")),
        MultiCellProfile cells => "M:" + string.Join('|', cells.Cells.Select(x => ProfileSignature(x))),
        _ => profile.ToString() ?? string.Empty
    };

    private static bool IsPurchasedCastIron(string material) =>
        material.Contains("castiron", StringComparison.OrdinalIgnoreCase) ||
        material.Contains("gang", StringComparison.OrdinalIgnoreCase);

    private static string SourceId(SourceRef source) => source.RelativePath + ":" + source.RawText;
    private static bool Finite(double value) => double.IsFinite(value);
    private static bool FiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static OpeningProfile ReplaceCutout(OpeningProfile opening, Profile2Mm cutout) =>
        new(opening.Id, opening.OwnerId, opening.Kind, opening.Inner, opening.PipeOuter, cutout,
            opening.WallFrame, opening.Frame, opening.ZRange, opening.ExtrusionRange, opening.Sources);

    private static ComponentGeometry WithoutVoids(ComponentGeometry component) =>
        new(component.Id, component.Kind, component.MaterialId, component.Footprint, component.ZRange,
            component.Occupied, [], component.FacePatches, component.Sources);

    private readonly record struct LocalBounds(double U0, double U1, double V0, double V1);

    private sealed class LineState
    {
        public LineState(string componentId, string materialId) { ComponentId = componentId; MaterialId = materialId; }
        public string ComponentId { get; }
        public string MaterialId { get; }
        public double GrossMm3 { get; set; }
        public double AdditionsMm3 { get; set; }
        public double DeductionsMm3 { get; set; }
    }
}
