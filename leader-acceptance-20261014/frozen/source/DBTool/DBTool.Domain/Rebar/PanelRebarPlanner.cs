using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class PanelRebarPlanner
{
    public static OperationResult<RebarLayout> PlanPanels(PanelLayout panels, PanelRebarSpec spec, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);

        var bars = new List<BarOccurrence>();
        foreach (var panel in panels.Panels)
        {
            if (spec.IncludeTop && panel.ZRange.Length < 2 * spec.CoverMm + spec.DiameterMm / 2d + spec.TopDiameterMm / 2d)
                return Block("PANEL_LAYER_COLLISION", "Panel thickness cannot contain the requested layers.", spec.Sources.FirstOrDefault());

            var bottom = PlanLayer(panel, Layer.Bottom, spec.DiameterMm, spec, bars.Count);
            if (!bottom.Success) return bottom;
            bars.AddRange(bottom.Value!.Occurrences);

            if (spec.IncludeTop)
            {
                var top = PlanLayer(panel, Layer.Top, spec.TopDiameterMm, spec, bars.Count);
                if (!top.Success) return top;
                bars.AddRange(top.Value!.Occurrences);
            }
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> PlanLayer(CoverPanel panel, Layer layer, int diameter, PanelRebarSpec spec, int offset)
    {
        var bars = new List<BarOccurrence>();
        double z = layer == Layer.Bottom ? panel.ZRange.Start + spec.CoverMm + diameter / 2d : panel.ZRange.End - spec.CoverMm - diameter / 2d;
        double width = panel.Bounds.MaxX - panel.Bounds.MinX, height = panel.Bounds.MaxY - panel.Bounds.MinY;
        var xPositions = BarDistributor.DistributeSegment(height, spec.CoverMm, diameter, spec.SpacingMm, spec.DistributionMode);
        var yPositions = BarDistributor.DistributeSegment(width, spec.CoverMm, diameter, spec.SpacingMm, spec.DistributionMode);
        if (!xPositions.Success) return new(false, null, xPositions.Diagnostics);
        if (!yPositions.Success) return new(false, null, yPositions.Diagnostics);

        int index = 0;
        foreach (double yOffset in xPositions.Value!.PositionsMm)
        {
            double y = panel.Bounds.MinY + yOffset;
            var start = new Point3Mm(panel.Bounds.MinX + spec.CoverMm, y, z);
            var end = new Point3Mm(panel.Bounds.MaxX - spec.CoverMm, y, z);
            bars.Add(Create($"{panel.Id}/{layer}/X/{index++:000}", panel, layer, Axis.X, diameter, start, end, spec.Sources, index - 1, y));
        }

        index = 0;
        foreach (double xOffset in yPositions.Value!.PositionsMm)
        {
            double x = panel.Bounds.MinX + xOffset;
            var start = new Point3Mm(x, panel.Bounds.MinY + spec.CoverMm, z);
            var end = new Point3Mm(x, panel.Bounds.MaxY - spec.CoverMm, z);
            bars.Add(Create($"{panel.Id}/{layer}/Y/{index++:000}", panel, layer, Axis.Y, diameter, start, end, spec.Sources, index - 1, x));
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static BarOccurrence Create(string id, CoverPanel panel, Layer layer, Axis axis, int diameter,
        Point3Mm start, Point3Mm end, IEnumerable<SourceRef> sources, int stationIndex, double stationMm)
    {
        var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        double length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2) + Math.Pow(end.Z - start.Z, 2));
        return new BarOccurrence(id, null, panel.Id, ComponentKind.Panel, layer, axis, diameter, path, length, length,
            LengthStatus.Known, "PANEL-REBAR-S2-001", panel.Sources.Concat(sources), axis == Axis.X ? RebarDirection.Horizontal : RebarDirection.Vertical,
            panel.Id, layer == Layer.Bottom ? RebarSurface.Inner : RebarSurface.Outer, "Panel", MeasurementBasisKind.Centerline, null,
            stationIndex, stationMm, "CoverPanel", panel.Id, "Uncut", null, length, "PANEL-REBAR-S2-001", "PanelMesh",
            $"{panel.Id}:{layer}:{axis}:{stationIndex}", "Centerline");
    }

    private static OperationResult<RebarLayout> Block(string code, string message, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, null, null, message)]);
}
