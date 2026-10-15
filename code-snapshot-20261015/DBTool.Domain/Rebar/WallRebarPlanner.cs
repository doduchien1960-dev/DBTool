using System.Globalization;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class WallRebarPlanner
{
    private static readonly int[] DiameterCatalog = [6, 8, 10, 12, 14, 16];

    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry,
        MeshZoneSet zones, WallOrNeckRebarSpec rebarSpec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(rebarSpec);
        if (rebarSpec.Structure != StructureKind.ReinforcedConcrete)
        {
            var diagnostic = new Diagnostic("WALL_STEEL_NOT_APPLICABLE", DiagnosticSeverity.Review,
                DiagnosticScope.Manhole, spec.Input.Id, null, spec.Sources.FirstOrDefault(),
                "reinforced concrete", rebarSpec.Structure.ToString(),
                "Wall geometry remains available, but wall steel is explicitly absent.");
            return new(true, new RebarLayout([], [diagnostic], CalculationScope.FullRebar, LayoutStatus.NotApplicable), [diagnostic]);
        }
        if (rebarSpec.HorizontalSpacingDeltaMm < 0)
            return Block("WALL_SPACING_INVALID", "Horizontal spacing delta cannot be negative.", spec, null);
        if (!TryHorizontalDiameter(rebarSpec.VerticalDiameterMm, rebarSpec.HorizontalDiameterStepDown,
                out int horizontalDiameter))
            return Block("WALL_DIAMETER_STEP_INVALID", "Vertical diameter and step-down must resolve in the approved catalog.", spec, null);
        double horizontalSpacing = rebarSpec.VerticalSpacingMm + rebarSpec.HorizontalSpacingDeltaMm;
        if (!double.IsFinite(horizontalSpacing) || horizontalSpacing <= 0)
            return Block("WALL_SPACING_INVALID", "Resulting horizontal spacing must be positive.", spec, null);

        if (rebarSpec.InnerLayer && rebarSpec.OuterLayer && spec.Input.WallThicknessMm < 2 * rebarSpec.CoverMm + 2 * rebarSpec.VerticalDiameterMm + 2 * horizontalDiameter)
            return Block("WALL_LAYER_COLLISION", "Both face meshes cannot fit the physical wall thickness.", spec, rebarSpec.Sources.FirstOrDefault());
        var bars = new List<BarOccurrence>();
        var diagnostics = new List<Diagnostic>();
        foreach (var wall in geometry.Components.Where(x => x.Kind == ComponentKind.Wall &&
                     x.MaterialId == "Concrete" && x.Id.StartsWith("Wall/", StringComparison.Ordinal))
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            foreach (var (surface, enabled) in new[] { (RebarSurface.Inner, rebarSpec.InnerLayer), (RebarSurface.Outer, rebarSpec.OuterLayer) })
            {
                if (!enabled) continue;
                var generated = GenerateSurface(spec, geometry, wall, surface, rebarSpec, horizontalDiameter, horizontalSpacing);
                if (!generated.Success) return new(false, null, generated.Diagnostics);
                bars.AddRange(generated.Value!.Occurrences);
                diagnostics.AddRange(generated.Value.Diagnostics);
            }
        }
        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar), diagnostics);
    }

    internal static OperationResult<RebarLayout> PlanNeck(ValidatedManholeSpec spec, GeometryModel geometry,
        WallOrNeckRebarSpec rebarSpec)
    {
        if (rebarSpec.Structure != StructureKind.ReinforcedConcrete)
        {
            var diagnostic = new Diagnostic("NECK_STEEL_NOT_APPLICABLE", DiagnosticSeverity.Review, DiagnosticScope.Manhole,
                spec.Input.Id, null, spec.Sources.FirstOrDefault(), "reinforced concrete", rebarSpec.Structure.ToString(),
                "Neck structure is not reinforced concrete; neck steel is explicitly absent.");
            return new(true, new RebarLayout([], [diagnostic], CalculationScope.FullRebar, LayoutStatus.NotApplicable), [diagnostic]);
        }
        if (spec.NeckSpec is null || spec.NeckSpec.HeightMm <= 0 || !geometry.Components.Any(x => x.Kind == ComponentKind.Neck))
            return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Disabled), []);
        if (rebarSpec.HorizontalSpacingDeltaMm < 0)
            return Block("NECK_SPACING_INVALID", "Horizontal spacing delta cannot be negative.", spec, null);
        if (!TryHorizontalDiameter(rebarSpec.VerticalDiameterMm, rebarSpec.HorizontalDiameterStepDown, out int horizontalDiameter))
            return Block("NECK_DIAMETER_STEP_INVALID", "Neck diameter step-down is outside the catalog.", spec, null);
        double horizontalSpacing = rebarSpec.VerticalSpacingMm + rebarSpec.HorizontalSpacingDeltaMm;
        if (horizontalSpacing <= 0 || !double.IsFinite(horizontalSpacing))
            return Block("NECK_SPACING_INVALID", "Resulting neck horizontal spacing must be positive.", spec, null);

        var component = geometry.Components.Single(x => x.Kind == ComponentKind.Neck);
        var n = spec.NeckSpec!;
        var (cx, cy) = NeckPositionMap.Center(n.Position, spec.Input.L, spec.Input.B, n.InnerWidthMm, n.InnerLengthMm);
        double x = n.InnerWidthMm / 2, y = n.InnerLengthMm / 2, t = n.ThicknessMm;
        var faces = new (string Id, double X0, double Y0, double X1, double Y1)[] {
            ("Neck/T1", cx - x - t, cy + y, cx + x + t, cy + y + t),
            ("Neck/T2", cx + x, cy - y, cx + x + t, cy + y),
            ("Neck/T3", cx - x - t, cy - y - t, cx + x + t, cy - y),
            ("Neck/T4", cx - x - t, cy - y, cx - x, cy + y) };
        var bars = new List<BarOccurrence>();
        foreach (var face in faces)
        {
            var footprint = new Footprint(new Polygon2Mm([new(face.X0, face.Y0), new(face.X1, face.Y0), new(face.X1, face.Y1), new(face.X0, face.Y1)]), []);
            foreach (var (surface, enabled) in new[] { (RebarSurface.Inner, rebarSpec.InnerLayer), (RebarSurface.Outer, rebarSpec.OuterLayer) })
            {
                if (!enabled) continue;
                // Both faces belong to the neck body below the cover seat.
                // GenerateSurface applies the specified cover and half diameter.
                double top = spec.NeckSeatSpec?.Kind == NeckSeatKind.Step
                    ? spec.NeckSeatSpec.SeatElevationMm : component.ZRange.End;
                var range = new Interval1D(component.ZRange.Start, top);
                var strip = new ComponentGeometry(face.Id, ComponentKind.Neck, component.MaterialId, footprint, range, component.Occupied, [], [], component.Sources);
                var result = GenerateSurface(spec, geometry, strip, surface, rebarSpec, horizontalDiameter, horizontalSpacing);
                if (!result.Success) return result;
                foreach (var bar in result.Value!.Occurrences)
                    bars.Add(new BarOccurrence(bar.Id, bar.ParentId, component.Id, component.Kind, bar.Layer, bar.Axis, bar.DiameterMm,
                        bar.Path, bar.ScheduleLengthMm, bar.FabricationLengthMm, bar.LengthStatus, bar.RuleId, bar.Sources, bar.Direction, face.Id, surface, "Neck",
                        MeasurementBasisKind.Centerline, null, bar.StationIndex, bar.StationMm, component.Id, null, "Uncut", null, bar.ScheduleLengthMm,
                        "NECK-REBAR-S2-001", "NeckMesh", bar.StationKey, "Centerline"));
            }
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<RebarLayout> GenerateSurface(ValidatedManholeSpec spec, GeometryModel model,
        ComponentGeometry wall, RebarSurface surface, WallOrNeckRebarSpec rebarSpec, int horizontalDiameter,
        double horizontalSpacing)
    {
        var points = wall.Footprint.Outer.Vertices;
        double minX = points.Min(x => x.X), maxX = points.Max(x => x.X);
        double minY = points.Min(x => x.Y), maxY = points.Max(x => x.Y);
        bool alongX = wall.Id.EndsWith("T1", StringComparison.Ordinal) || wall.Id.EndsWith("T3", StringComparison.Ordinal);
        bool innerAtMin = wall.Id.EndsWith("T1", StringComparison.Ordinal) || wall.Id.EndsWith("T2", StringComparison.Ordinal);
        double tangentMin = alongX ? minX : minY, tangentMax = alongX ? maxX : maxY;
        double normalMin = alongX ? minY : minX, normalMax = alongX ? maxY : maxX;
        bool atMin = surface == RebarSurface.Inner ? innerAtMin : !innerAtMin;
        double face = atMin ? normalMin + rebarSpec.CoverMm + rebarSpec.VerticalDiameterMm / 2d
            : normalMax - rebarSpec.CoverMm - rebarSpec.VerticalDiameterMm / 2d;
        // Crossing horizontal bars sit one diameter behind the vertical face mesh.
        double horizontalFace = face + (atMin ? 1 : -1) * (rebarSpec.VerticalDiameterMm + horizontalDiameter) / 2d;
        if (normalMax - normalMin < 2 * rebarSpec.CoverMm + rebarSpec.VerticalDiameterMm + horizontalDiameter)
            return Block("MEMBER_LAYER_COLLISION", "Two directions cannot fit the physical face thickness.", spec, rebarSpec.Sources.FirstOrDefault());
        var bars = new List<BarOccurrence>();
        var vertical = BarDistributor.DistributeSegment(tangentMax - tangentMin, rebarSpec.CoverMm,
            rebarSpec.VerticalDiameterMm, rebarSpec.VerticalSpacingMm, spec.Input.DistributionMode);
        var horizontal = BarDistributor.DistributeSegment(wall.ZRange.Length, rebarSpec.CoverMm,
            horizontalDiameter, horizontalSpacing, spec.Input.DistributionMode);
        if (!vertical.Success) return new(false, null, vertical.Diagnostics);
        if (!horizontal.Success) return new(false, null, horizontal.Diagnostics);
        double verticalStart = wall.ZRange.Start + rebarSpec.CoverMm + rebarSpec.VerticalDiameterMm / 2d;
        if (spec.Input.MainRebar is not null)
            verticalStart = wall.Kind == ComponentKind.Wall ? -spec.Input.BaseThicknessMm + spec.Input.CoverMm + rebarSpec.VerticalDiameterMm / 2d
                : spec.Input.H + spec.Input.MainRebar.RoofCoverMm + rebarSpec.VerticalDiameterMm / 2d;

        double openingClearance = spec.Input.MainRebar?.Accessories?.PipeOpeningClearanceMm
            ?? (spec.Input.ConfigRawCells.TryGetValue("Protect.H", out var ph1) && double.TryParse(ph1.Text ?? ph1.CachedText, NumberStyles.Any, CultureInfo.InvariantCulture, out var phv1) ? phv1 : (double?)null)
            ?? (spec.Input.UnsupportedRawFields.TryGetValue("Protect.H", out var ph2) && double.TryParse(ph2.Text ?? ph2.CachedText, NumberStyles.Any, CultureInfo.InvariantCulture, out var phv2) ? phv2 : (double?)null)
            ?? spec.Input.MainRebar?.Accessories?.Pipe?.Cover
            ?? rebarSpec.CoverMm;
        int vertIdx = 0;
        foreach (double stationOffset in vertical.Value!.PositionsMm)
        {
            double station = tangentMin + stationOffset;
            Point3Mm start = alongX ? new(station, face, verticalStart) : new(face, station, verticalStart);
            Point3Mm end = alongX ? new(station, face, wall.ZRange.End - rebarSpec.CoverMm - rebarSpec.VerticalDiameterMm / 2d) : new(face, station, wall.ZRange.End - rebarSpec.CoverMm - rebarSpec.VerticalDiameterMm / 2d);
            var bar = Make($"{spec.Input.Id}/{wall.Id}/{surface}/Vertical/{bars.Count:000}", wall,
                surface == RebarSurface.Inner ? Layer.Bottom : Layer.Top, alongX ? Axis.X : Axis.Y,
                RebarDirection.Vertical, rebarSpec.VerticalDiameterMm, start, end, wall.Id, rebarSpec.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault(), rebarSpec.Sources,
                vertIdx++, station);
            var clipped = ClipIfRequired(bar, wall, model, openingClearance);
            if (!clipped.Success) return new(false, null, clipped.Diagnostics);
            bars.AddRange(clipped.Value!.Retained);
        }

        int horizIdx = 0;
        foreach (double zOffset in horizontal.Value!.PositionsMm)
        {
            double z = wall.ZRange.Start + zOffset;
            Point3Mm start = alongX ? new(tangentMin + rebarSpec.CoverMm + horizontalDiameter / 2d, horizontalFace, z) : new(horizontalFace, tangentMin + rebarSpec.CoverMm + horizontalDiameter / 2d, z);
            Point3Mm end = alongX ? new(tangentMax - rebarSpec.CoverMm - horizontalDiameter / 2d, horizontalFace, z) : new(horizontalFace, tangentMax - rebarSpec.CoverMm - horizontalDiameter / 2d, z);
            var bar = Make($"{spec.Input.Id}/{wall.Id}/{surface}/Horizontal/{bars.Count:000}", wall,
                surface == RebarSurface.Inner ? Layer.Bottom : Layer.Top, alongX ? Axis.X : Axis.Y,
                RebarDirection.Horizontal, horizontalDiameter, start, end, wall.Id, rebarSpec.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault(), rebarSpec.Sources,
                horizIdx++, z);
            var clipped = ClipIfRequired(bar, wall, model, openingClearance);
            if (!clipped.Success) return new(false, null, clipped.Diagnostics);
            bars.AddRange(clipped.Value!.Retained);
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    private static OperationResult<ClipResult> ClipIfRequired(BarOccurrence bar, ComponentGeometry wall,
        GeometryModel model, double cover)
    {
        var openings = model.Openings.Where(x => x.OwnerId == wall.Id).ToArray();
        if (openings.Length == 0) return new(true, new ClipResult([bar], 0, bar.ScheduleLengthMm), []);
        return OpeningClipper.Clip(bar, openings, new ClearancePolicy(cover), model);
    }

    private static BarOccurrence Make(string id, ComponentGeometry component, Layer layer, Axis axis,
        RebarDirection direction, int diameter, Point3Mm start, Point3Mm end, string frameId, SourceRef? source,
        IEnumerable<SourceRef>? additionalSources = null, int stationIndex = 0, double stationMm = 0)
    {
        var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        double length = BendLengthCalculator.Distance(start, end);
        return new BarOccurrence(id, null, component.Id, component.Kind, layer, axis, diameter, path, length, length,
            LengthStatus.Known, "WALL-NECK-REBAR-S2-001", (source is null ? Array.Empty<SourceRef>() : new[] { source }).Concat(additionalSources ?? []).Distinct(), direction, frameId,
            layer == Layer.Bottom ? RebarSurface.Inner : RebarSurface.Outer,
            component.Kind == ComponentKind.Neck ? "Neck" : "Wall", MeasurementBasisKind.Centerline, null,
            stationIndex, stationMm, component.Id, null, "Uncut", null, length, "WALL-NECK-REBAR-S2-001",
            component.Kind == ComponentKind.Neck ? "NeckMesh" : "WallMesh", $"{component.Id}:{direction}:{stationIndex}", "Centerline");
    }

    private static bool TryHorizontalDiameter(int verticalDiameter, int stepDown, out int horizontal)
    {
        horizontal = 0;
        int index = Array.IndexOf(DiameterCatalog, verticalDiameter);
        if (index < 0 || stepDown < 0 || index - stepDown < 0) return false;
        horizontal = DiameterCatalog[index - stepDown];
        return true;
    }

    private static OperationResult<RebarLayout> Block(string code, string message, ValidatedManholeSpec spec, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Manhole, spec.Input.Id, null,
            source, null, null, message)]);
}
