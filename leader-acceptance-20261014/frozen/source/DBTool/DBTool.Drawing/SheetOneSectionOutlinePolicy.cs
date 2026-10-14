using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>
/// First-sheet section material outlines emit outer and inner loops from section material regions
/// using shared boundary emitter; closure and void deduplication preserve topology identity.
/// </summary>
public static class SheetOneSectionOutlinePolicy
{
    public const string Role="Geometry/SheetOneSectionOutline";
    public static IReadOnlyList<DrawingPrimitive> Compose(IEnumerable<DrawingPrimitive> primitives,PresentationRequest presentation)
    {
        var original=primitives.ToArray();
        if(!SheetOnePresentationPolicy.Enabled(presentation)) return original;
        var eligible=original.OfType<PolylinePrimitive>().Where(p=>Section(p.ViewId) && p.Role=="Geometry/Material" && p.Closed && p.OccurrenceId is null).ToArray();
        var neckPaths=new Dictionary<string,(Point2Mm[] Points,bool Closed)>(StringComparer.Ordinal);
        foreach(var group in eligible.Where(p=>p.ComponentId=="Neck").GroupBy(p=>p.ViewId))
        {
            var parts=group.ToArray();
            if(parts.Length!=3 || parts.Any(p=>!Rectangle(p))) continue;
            foreach(var plate in parts)
            {
                var sides=parts.Where(p=>p!=plate).OrderBy(p=>p.Points.Min(v=>v.X)).ToArray();
                var a=Bounds(sides[0]);var b=Bounds(sides[1]);var cap=Bounds(plate);
                if(!Near(a.MinY,b.MinY) || !Near(a.MaxY,b.MaxY) || !Near(a.MaxY,cap.MinY) ||
                    !Near(a.MinX,cap.MinX) || !Near(b.MaxX,cap.MaxX) || a.MaxX>=b.MinX ||
                    !Near(a.MaxX-a.MinX,b.MaxX-b.MinX)) continue;
                neckPaths.Add(sides[0].SemanticId,([new(a.MinX,a.MinY),new(a.MinX,a.MaxY),new(a.MaxX,a.MaxY),new(a.MaxX,a.MinY)],false));
                neckPaths.Add(sides[1].SemanticId,([new(b.MaxX,b.MinY),new(b.MaxX,b.MaxY),new(b.MinX,b.MaxY),new(b.MinX,b.MinY)],false));
                neckPaths.Add(plate.SemanticId,([new(cap.MinX,cap.MinY),new(cap.MinX,cap.MaxY),new(cap.MaxX,cap.MaxY),new(cap.MaxX,cap.MinY)],true));
                break;
            }
        }
        return original.Select(Transform).ToArray();
        DrawingPrimitive Transform(DrawingPrimitive p)
        {
            if(p is not PolylinePrimitive poly || !eligible.Contains(poly)) return p;
            var path=neckPaths.TryGetValue(poly.SemanticId,out var sourcePath)?sourcePath:(poly.Points.ToArray(),poly.Closed);
            return new PolylinePrimitive(poly.SemanticId,Role,poly.ViewId,poly.ComponentId,poly.OccurrenceId,poly.ScheduleMark,
                path.Item1,path.Item2,poly.LogicalLengthMm,poly.MaterialKind);
        }
    }
    private static bool Section(string view)=>view is "AA" or "BB" or "SteelAA" or "SteelBB";
    private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-8;
    private static bool Rectangle(PolylinePrimitive p)=>p.Points.Length==4 && p.Points.Distinct().Count()==4 &&
        p.Points.Select(v=>v.X).Distinct().Count()==2 && p.Points.Select(v=>v.Y).Distinct().Count()==2;
    private static Bounds2Mm Bounds(PolylinePrimitive p)=>new(p.Points.Min(v=>v.X),p.Points.Min(v=>v.Y),p.Points.Max(v=>v.X),p.Points.Max(v=>v.Y));
}
