using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Historical style fixtures only. Production bending shapes use BendingSchematicPlanner.</summary>
public static class SheetFiveViews
{
    public const string ViewId = "Bending/Details";
    public static readonly Bounds2Mm Bounds = new(0, 0, 12600, 8910);

    public static IReadOnlyList<DrawingPrimitive> BuildBending(IEnumerable<BendingShapeIntent> intents, string viewId = ViewId) =>
        BuildFromIntents(intents, viewId);

    public static IReadOnlyList<DrawingPrimitive> BuildFromIntents(IEnumerable<BendingShapeIntent> intents, string viewId = ViewId)
    {
        var primitives = new List<DrawingPrimitive>();
        double x = 0;
        foreach (var shape in intents)
        {
            string id = $"{viewId}/{shape.Mark}";
            primitives.Add(new PathPrimitive($"{id}/Shape", "Rebar/Schematic", viewId, shape.ComponentId, null, shape.Mark,
                shape.Segments, shape.Closed, ViewPurpose.Schematic, shape.ScheduleLengthMm, DrawingMaterialKind.RoundSteel,
                shape.SourceOccurrenceIds.FirstOrDefault() ?? shape.Mark));
            primitives.Add(new TextPrimitive($"{id}/Name", "Rebar/Mark", viewId, shape.ComponentId, null, shape.Mark,
                new Point2Mm(x, 0), $"{shape.Mark} SL={shape.Quantity} L={Math.Round(shape.ScheduleLengthMm, 0):0}", 60, 0));
            x += 2500;
        }
        return primitives;
    }

    public static IReadOnlyList<DrawingPrimitive> BuildFromCaptions(IEnumerable<DBTool.Drawing.StageContractsV2.CaptionV2> captions, string viewId = ViewId)
    {
        var primitives = new List<DrawingPrimitive>();
        foreach (var cap in captions)
        {
            string id = $"{viewId}/{cap.group_id}";
            primitives.Add(new TextPrimitive($"{id}/Caption", "Rebar/BendingMark", viewId, null, null, cap.group_id,
                new Point2Mm(cap.insertion.x_mm, cap.insertion.y_mm), cap.text, 60, 0));
            primitives.Add(new PolylinePrimitive($"{id}/Underline", "Rebar/BendingUnderline", viewId, null, null, cap.group_id,
                [new(cap.underline_start.x_mm, cap.underline_start.y_mm), new(cap.underline_end.x_mm, cap.underline_end.y_mm)], false));
        }
        return primitives;
    }
}
