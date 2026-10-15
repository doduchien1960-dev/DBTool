using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Published G400-1 nominal anchorage strokes, associated with existing mesh ticks.
/// The measured template leg is a display convention, not a fabricated bar length.</summary>
public static class SheetTwoNominalAnchorage
{
    public const string Role = "Rebar/SheetTwoNominalAnchorage";
    // Reference Sheet2: upper endpoint 177.573593128 mm above the slab top.
    // Its construction rule is not established; apply only to the confirmed template geometry.
    private const double PublishedUpperEndMm = 177.573593128;

    public static IReadOnlyList<PolylinePrimitive> Build(GeometryModel geometry,
        IEnumerable<DrawingPrimitive> primitives)
    {
        var snapshot = primitives.ToArray();
        var basis = geometry.Components.SingleOrDefault(c => c.Id == "Base");
        var haunch = geometry.Components.SingleOrDefault(c => c.Id == "Base/Haunch");
        if (basis is null || Math.Abs(basis.ZRange.Length - 150) > 1e-7 ||
            haunch?.Occupied is not HaunchSolid h || h.Owner != HaunchOwner.Base ||
            Math.Abs(h.WidthMm - 100) > 1e-7 || Math.Abs(h.HeightMm - 100) > 1e-7 ||
            Math.Abs(h.ChamberWidthMm - 1000) > 1e-7 || Math.Abs(h.ChamberLengthMm - 1250) > 1e-7 ||
            Math.Abs(h.ZRange.Start - basis.ZRange.End) > 1e-7)
            return [];

        var result = new List<PolylinePrimitive>();
        foreach (var tick in snapshot.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoGach.AnchorageRole))
        {
            if (!tick.ViewId.Contains("/Inner/", StringComparison.Ordinal) ||
                SheetMeshPlacement.Title(tick.ViewId) is null || tick.OccurrenceId is null || tick.ScheduleMark is null ||
                !snapshot.Any(p => p.ViewId == tick.ViewId && p.Role == SheetTwoNominalWallMesh.Role))
                continue;
            var cover = snapshot.OfType<DimensionIntentPrimitive>().SingleOrDefault(d => d.ViewId == tick.ViewId &&
                d.SemanticId.EndsWith("/NominalSpacing/X/000", StringComparison.Ordinal));
            if (cover is null || Math.Abs(cover.LogicalMeasurementMm - 30) > 1e-7) continue;
            double low = basis.ZRange.Start + cover.LogicalMeasurementMm;
            double high = basis.ZRange.End + PublishedUpperEndMm;
            result.Add(new PolylinePrimitive(tick.SemanticId + "/Leg", Role, tick.ViewId, tick.ComponentId,
                tick.OccurrenceId, tick.ScheduleMark, [new(tick.Insertion.X, low), new(tick.Insertion.X, high)],
                false, materialKind: DrawingMaterialKind.RoundSteel));
        }
        return result;
    }
}
