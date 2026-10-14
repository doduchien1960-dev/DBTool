using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class LiftingHookPlanner
{
    public static OperationResult<RebarLayout> Plan(LiftingHookSpec spec, ComponentGeometry geometry, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);

        if (spec.Count == 0) return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Disabled), []);

        bool isPanel = geometry.Kind == ComponentKind.Panel;
        if (isPanel)
        {
            if (spec.Count is not (1 or 2 or 4))
                return Block("LIFTING_HOOK_COUNT_INVALID", "Panel lifting hook count must be 0, 1, 2 or 4.", spec.Sources.FirstOrDefault());
        }
        else
        {
            if (spec.Count is not (2 or 4))
                return Block("LIFTING_HOOK_COUNT_INVALID", "Roof lifting hook count must be 0, 2 or 4.", spec.Sources.FirstOrDefault());
        }

        if (spec.Count > 1 && spec.SpacingMm <= 0)
            return Block("LIFTING_HOOK_SPACING_REQUIRED", "Hook distribution spacing must be explicit.", spec.Sources.FirstOrDefault());

        double centerX = geometry.Footprint.Outer.Vertices.Average(x => x.X);
        double centerY = geometry.Footprint.Outer.Vertices.Average(x => x.Y);

        Point2Mm[] points = spec.Count switch
        {
            1 => [new Point2Mm(centerX, centerY)],
            2 => [new Point2Mm(centerX - spec.SpacingMm / 2, centerY), new Point2Mm(centerX + spec.SpacingMm / 2, centerY)],
            _ => [
                new Point2Mm(centerX - spec.SpacingMm / 2, centerY - spec.SpacingMm / 2),
                new Point2Mm(centerX + spec.SpacingMm / 2, centerY - spec.SpacingMm / 2),
                new Point2Mm(centerX + spec.SpacingMm / 2, centerY + spec.SpacingMm / 2),
                new Point2Mm(centerX - spec.SpacingMm / 2, centerY + spec.SpacingMm / 2)
            ]
        };

        var bars = new List<BarOccurrence>();
        int idx = 0;
        foreach (var point in points)
        {
            if (!Inside(geometry.Footprint.Outer, point))
                return Block("LIFTING_HOOK_OUTSIDE_SOLID", "Lifting hook support is outside the actual solid.", spec.Sources.FirstOrDefault());

            var start = new Point3Mm(point.X, point.Y, geometry.ZRange.Start);
            var end = new Point3Mm(point.X, point.Y, start.Z + spec.HookLengthMm);
            if (end.Z > geometry.ZRange.End + policy.TolerancePolicy.GeometryMm)
                return Block("LIFTING_HOOK_COLLISION", "Lifting hook exceeds the actual solid support range.", spec.Sources.FirstOrDefault());

            var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            bars.Add(new BarOccurrence($"{geometry.Id}/LiftingHook/{bars.Count:000}", null, geometry.Id, geometry.Kind, Layer.Bottom,
                Axis.X, spec.DiameterMm, path, spec.HookLengthMm, spec.HookLengthMm, LengthStatus.Known, "LIFTING-HOOK-S2-001",
                geometry.Sources.Concat(spec.Sources), RebarDirection.Supplementary, geometry.Id, null, "LiftingHook",
                MeasurementBasisKind.Centerline, null, idx++, 0, geometry.Id, isPanel ? geometry.Id : null,
                "Lifting", null, spec.HookLengthMm, "LIFTING-HOOK-S2-001", "LiftingHook", $"{geometry.Id}:Hook:{idx}", "Centerline"));
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    public static OperationResult<RebarLayout> Plan(LiftingHookSpec spec, GeometryModel geometry, CalculationPolicy policy) =>
        Plan(spec, geometry.Components.FirstOrDefault() ?? throw new ArgumentException("Geometry has no component."), policy);

    private static bool Inside(Polygon2Mm polygon, Point2Mm p) =>
        p.X >= polygon.Vertices.Min(x => x.X) && p.X <= polygon.Vertices.Max(x => x.X) &&
        p.Y >= polygon.Vertices.Min(x => x.Y) && p.Y <= polygon.Vertices.Max(x => x.Y);

    private static OperationResult<RebarLayout> Block(string code, string message, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, null, null, message)]);
}
