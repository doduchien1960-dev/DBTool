using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class OpeningReinforcementPlanner
{
    public static OperationResult<RebarLayout> Plan(OpeningProfile opening, ComponentGeometry component,
        OpeningReinforcementSpec spec, IReadOnlyList<ActiveLayerFrame> activeLayerFrames, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(opening); ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(activeLayerFrames);
        ArgumentNullException.ThrowIfNull(policy);
        if (spec.Shape == OpeningReinforcementShape.None || spec.CountPerSideK == 0)
            return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Disabled), []);
        if (!string.Equals(opening.OwnerId, component.Id, StringComparison.Ordinal))
            return Block("OPENING_OWNER_MISMATCH", "Opening owner does not match the concrete component.", component, opening);
        if (spec.LayerSet.IsEmpty || !activeLayerFrames.Any(x => spec.LayerSet.Contains(x.Layer)))
            return Block("OPENING_LAYER_REQUIRED", "At least one active requested layer is required.", component, opening);
        if (spec.EndAnchoragePolicy == EndAnchoragePolicy.NeedsRule)
            return Block("OPENING_ANCHORAGE_NEEDS_RULE", "Anchorage policy is unresolved.", component, opening);
        if (spec.CountPerSideK > 1 && spec.Shape == OpeningReinforcementShape.Diagonal45 && !spec.ParallelSpacingMm.HasValue)
            return Block("OPENING_PARALLEL_SPACING_REQUIRED", "Parallel spacing is required when diagonal count is greater than one.", component, opening);

        var layers = activeLayerFrames.Where(x => spec.LayerSet.Contains(x.Layer)).ToArray();
        var bars = new List<BarOccurrence>();
        foreach (var layer in layers)
        {
            for (int set = 0; set < Math.Max(1, spec.CountPerSideK); set++)
            {
                var generated = spec.Shape switch
                {
                    OpeningReinforcementShape.RoundBo when set == 0 => Round(opening, component, spec, layer, policy),
                    OpeningReinforcementShape.RoundBo => new OperationResult<RebarLayout>(true,
                        new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Complete), []),
                    OpeningReinforcementShape.RectangularBo when set == 0 => Rectangular(opening, component, spec, layer, policy),
                    OpeningReinforcementShape.RectangularBo => new OperationResult<RebarLayout>(true,
                        new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Complete), []),
                    OpeningReinforcementShape.Diagonal45 => Diagonal(opening, component, spec, layer, set, policy),
                    OpeningReinforcementShape.SeparateSides => Separate(opening, component, spec, layer, set, policy),
                    _ => Block("OPENING_SHAPE_UNSUPPORTED", "Opening reinforcement shape is unsupported.", component, opening)
                };
                if (!generated.Success) return generated;
                bars.AddRange(generated.Value!.Occurrences);
            }
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> Round(OpeningProfile opening, ComponentGeometry component,
        OpeningReinforcementSpec spec, ActiveLayerFrame layer, CalculationPolicy policy)
    {
        if (opening.ConcreteCutout is not CircleProfile circle)
            return Block("OPENING_PROFILE_MISMATCH", "RoundBo requires a round concrete cutout.", component, opening);
        double radius = circle.RadiusMm + spec.CoverMm + spec.DiameterMm / 2d;
        if (!ContainsCircle(component, circle.Center, radius))
            return Block("OPENING_REBAR_OUTSIDE_CONCRETE", "Round reinforcement does not fit inside concrete cover domain.", component, opening);
        var center = ToWorld(opening.Frame, circle.Center);
        var arc = new ArcSegment3Mm(center, radius, opening.Frame.N, opening.Frame.U, 2 * Math.PI);
        double lap = spec.LapDiameterMultiple * spec.DiameterMm;
        var path = new BarPath([arc], true, HookPattern.Hook0M, lap, false, MeasurementBasis.TangentPath);
        double length = 2 * Math.PI * radius + lap;
        var bar = Create($"{component.Id}/Opening/{layer.Id}/RoundBo/000", component, layer, Axis.X,
            RebarDirection.Perimeter, spec.DiameterMm, path, length, opening, radius);
        return new(true, new RebarLayout([bar], [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> Rectangular(OpeningProfile opening, ComponentGeometry component,
        OpeningReinforcementSpec spec, ActiveLayerFrame layer, CalculationPolicy policy)
    {
        if (opening.ConcreteCutout is not RectangleProfile rectangle)
            return Block("OPENING_PROFILE_MISMATCH", "RectangularBo requires a rectangular concrete cutout.", component, opening);
        double radius = spec.InnerRadiusMm ?? 0;
        var r = rectangle.Rectangle;
        if (radius <= 0 || radius * 2 >= Math.Min(r.MaxX - r.MinX, r.MaxY - r.MinY))
            return Block("OPENING_BEND_RADIUS_INVALID", "RectangularBo needs a positive feasible bend radius.", component, opening);
        double q = spec.CoverMm + spec.DiameterMm / 2d;
        var centerline = new Rectangle2Mm(r.MinX - q, r.MinY - q, r.MaxX + q, r.MaxY + q);
        if (!ContainsRectangle(component, r, q))
            return Block("OPENING_REBAR_OUTSIDE_CONCRETE", "Rectangular reinforcement does not fit inside concrete cover domain.", component, opening);
        var pathSegments = RoundedRectanglePath(opening.Frame, centerline, radius);
        double lap = spec.LapDiameterMultiple * spec.DiameterMm;
        var path = new BarPath(pathSegments, true, HookPattern.Hook0M, lap, false, MeasurementBasis.TangentPath);
        double length = 2 * ((centerline.MaxX - centerline.MinX) + (centerline.MaxY - centerline.MinY)) - 8 * radius + 2 * Math.PI * radius + lap;
        var bar = Create($"{component.Id}/Opening/{layer.Id}/RectangularBo/000", component, layer, Axis.X,
            RebarDirection.Perimeter, spec.DiameterMm, path, length, opening, radius);
        return new(true, new RebarLayout([bar], [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> Diagonal(OpeningProfile opening, ComponentGeometry component,
        OpeningReinforcementSpec spec, ActiveLayerFrame layer, int set, CalculationPolicy policy)
    {
        if (opening.ConcreteCutout is not CircleProfile circle)
            return Block("OPENING_PROFILE_MISMATCH", "Diagonal45 requires a circular opening in this fixture.", component, opening);
        double radius = circle.RadiusMm + spec.CoverMm + spec.DiameterMm / 2d;
        if (!ContainsCircle(component, circle.Center, radius + spec.ExtensionMm))
            return Block("OPENING_REBAR_OUTSIDE_CONCRETE", "Diagonal reinforcement does not fit inside concrete cover domain.", component, opening);
        double parallelOffset = set * (spec.ParallelSpacingMm ?? 0);
        var bars = new List<BarOccurrence>();
        var center = ToWorld(opening.Frame, circle.Center);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            double sx = quadrant is 0 or 3 ? 1 : -1;
            double sy = quadrant is 0 or 1 ? 1 : -1;
            double inv = 1 / Math.Sqrt(2);
            var normal = new Vector3(sx * inv, sy * inv, 0);
            var tangent = new Vector3(-sy * inv, sx * inv, 0);
            var shifted = new Point3Mm(center.X + normal.X * radius + tangent.X * parallelOffset,
                center.Y + normal.Y * radius + tangent.Y * parallelOffset, center.Z);
            var start = new Point3Mm(shifted.X - tangent.X * (radius + spec.ExtensionMm),
                shifted.Y - tangent.Y * (radius + spec.ExtensionMm), shifted.Z);
            var end = new Point3Mm(shifted.X + tangent.X * (radius + spec.ExtensionMm),
                shifted.Y + tangent.Y * (radius + spec.ExtensionMm), shifted.Z);
            var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            bars.Add(Create($"{component.Id}/Opening/{layer.Id}/Diagonal45/{set:000}/{quadrant:000}",
                component, layer, Axis.X, RebarDirection.Supplementary, spec.DiameterMm, path, 2 * (radius + spec.ExtensionMm), opening, null));
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> Separate(OpeningProfile opening, ComponentGeometry component,
        OpeningReinforcementSpec spec, ActiveLayerFrame layer, int set, CalculationPolicy policy)
    {
        if (opening.ConcreteCutout is not RectangleProfile rectangle)
            return Block("OPENING_PROFILE_MISMATCH", "SeparateSides requires a rectangular opening.", component, opening);
        double q = spec.CoverMm + spec.DiameterMm / 2d;
        var r = rectangle.Rectangle;
        double width = r.MaxX - r.MinX + 2 * q, height = r.MaxY - r.MinY + 2 * q;
        if (!ContainsRectangle(component, r, q + spec.ExtensionMm))
            return Block("OPENING_REBAR_OUTSIDE_CONCRETE", "Separate-side reinforcement does not fit inside concrete cover domain.", component, opening);
        var center = ToWorld(opening.Frame, new Point2Mm((r.MinX + r.MaxX) / 2, (r.MinY + r.MaxY) / 2));
        var bars = new List<BarOccurrence>();
        double offset = set * (spec.ParallelSpacingMm ?? 0);
        var runs = new[] {
            (Axis.X, width + 2 * spec.ExtensionMm, center.Y - height / 2 + offset),
            (Axis.X, width + 2 * spec.ExtensionMm, center.Y + height / 2 + offset),
            (Axis.Y, height + 2 * spec.ExtensionMm, center.X - width / 2 + offset),
            (Axis.Y, height + 2 * spec.ExtensionMm, center.X + width / 2 + offset) };
        for (int i = 0; i < runs.Length; i++)
        {
            var (axis, length, fixedCoordinate) = runs[i];
            Point3Mm start = axis == Axis.X ? new(center.X - length / 2, fixedCoordinate, center.Z)
                : new(fixedCoordinate, center.Y - length / 2, center.Z);
            Point3Mm end = axis == Axis.X ? new(center.X + length / 2, fixedCoordinate, center.Z)
                : new(fixedCoordinate, center.Y + length / 2, center.Z);
            var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            bars.Add(Create($"{component.Id}/Opening/{layer.Id}/SeparateSides/{set:000}/{i:000}",
                component, layer, axis, RebarDirection.Supplementary, spec.DiameterMm, path, length, opening, null));
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static ImmutableArray<PathSegment> RoundedRectanglePath(Frame3Mm frame, Rectangle2Mm r, double radius)
    {
        Point3Mm P(double x, double y) => ToWorld(frame, new Point2Mm(x, y));
        var z = frame.Origin.Z;
        var segments = new List<PathSegment> {
            new LineSegment3Mm(P(r.MinX + radius, r.MinY), P(r.MaxX - radius, r.MinY)),
            new ArcSegment3Mm(P(r.MaxX - radius, r.MinY + radius), radius, frame.N, new Vector3(0, -1, 0), Math.PI / 2),
            new LineSegment3Mm(P(r.MaxX, r.MinY + radius), P(r.MaxX, r.MaxY - radius)),
            new ArcSegment3Mm(P(r.MaxX - radius, r.MaxY - radius), radius, frame.N, new Vector3(1, 0, 0), Math.PI / 2),
            new LineSegment3Mm(P(r.MaxX - radius, r.MaxY), P(r.MinX + radius, r.MaxY)),
            new ArcSegment3Mm(P(r.MinX + radius, r.MaxY - radius), radius, frame.N, new Vector3(0, 1, 0), Math.PI / 2),
            new LineSegment3Mm(P(r.MinX, r.MaxY - radius), P(r.MinX, r.MinY + radius)),
            new ArcSegment3Mm(P(r.MinX + radius, r.MinY + radius), radius, frame.N, new Vector3(-1, 0, 0), Math.PI / 2)
        };
        return segments.ToImmutableArray();
    }

    private static BarOccurrence Create(string id, ComponentGeometry component, ActiveLayerFrame layer,
        Axis axis, RebarDirection direction, int diameter, BarPath path, double length, OpeningProfile opening,
        double? innerRadius)
    {
        return new BarOccurrence(id, null, component.Id, component.Kind, layer.Layer, axis, diameter, path, length, length,
            LengthStatus.Known, "OPENING-REBAR-S2-001", opening.Sources.Concat(component.Sources), direction,
            layer.Id, null, "OpeningReinforcement", MeasurementBasisKind.Centerline, innerRadius);
    }

    private static Point3Mm ToWorld(Frame3Mm frame, Point2Mm point) => new(
        frame.Origin.X + frame.U.X * point.X + frame.V.X * point.Y,
        frame.Origin.Y + frame.U.Y * point.X + frame.V.Y * point.Y,
        frame.Origin.Z + frame.U.Z * point.X + frame.V.Z * point.Y);

    private static bool ContainsCircle(ComponentGeometry component, Point2Mm center, double radius)
    {
        var points = component.Footprint.Outer.Vertices;
        double minX = points.Min(x => x.X), maxX = points.Max(x => x.X), minY = points.Min(x => x.Y), maxY = points.Max(x => x.Y);
        return center.X - radius >= minX - 1e-6 && center.X + radius <= maxX + 1e-6 &&
               center.Y - radius >= minY - 1e-6 && center.Y + radius <= maxY + 1e-6;
    }

    private static bool ContainsRectangle(ComponentGeometry component, Rectangle2Mm rectangle, double extra)
    {
        var points = component.Footprint.Outer.Vertices;
        return rectangle.MinX - extra >= points.Min(x => x.X) - 1e-6 && rectangle.MaxX + extra <= points.Max(x => x.X) + 1e-6 &&
               rectangle.MinY - extra >= points.Min(x => x.Y) - 1e-6 && rectangle.MaxY + extra <= points.Max(x => x.Y) + 1e-6;
    }

    private static OperationResult<RebarLayout> Block(string code, string message, ComponentGeometry component, OpeningProfile opening) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
            opening.Sources.FirstOrDefault() ?? component.Sources.FirstOrDefault(), null, null, message)]);
}
