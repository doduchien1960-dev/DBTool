using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>
/// First-sheet plan outlines emit outer/inner loops according to material/void ownership,
/// preserving true boundaries without introducing artificial seams or deleting roof access holes.
/// </summary>
public static class SheetOnePlanOutlinePolicy
{
    public const string Role = "Geometry/SheetOnePlanOutline";
    public static DrawingPrimitive Apply(DrawingPrimitive primitive, PresentationRequest presentation)
    {
        if (!SheetOnePresentationPolicy.Enabled(presentation) || primitive is not PolylinePrimitive
            { ViewId:"Plan", ComponentId:"Roof" or "Neck", Role:"Geometry/Material", Closed:true } p ||
            p.Points.Length!=4 || p.Points.Distinct().Count()!=4 ||
            p.Points.Select(v=>v.X).Distinct().Count()!=2 || p.Points.Select(v=>v.Y).Distinct().Count()!=2)
            return primitive;
        double left=p.Points.Min(v=>v.X),right=p.Points.Max(v=>v.X),bottom=p.Points.Min(v=>v.Y),top=p.Points.Max(v=>v.Y);
        var tl=new Point2Mm(left,top);var tr=new Point2Mm(right,top);
        var br=new Point2Mm(right,bottom);var bl=new Point2Mm(left,bottom);
        bool closed=p.ComponentId=="Neck";
        // Roof uses an open five-vertex path returning to its first vertex;
        // neck uses the same perimeter with four vertices and Closed=true.
        return new PolylinePrimitive(p.SemanticId,Role,p.ViewId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,
            closed?[tl,tr,br,bl]:[tl,tr,br,bl,tl],closed,p.LogicalLengthMm,p.MaterialKind);
    }
}
