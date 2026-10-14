using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>The source first-sheet base perimeter consists of four native Lines.</summary>
public static class SheetOnePlanBaseOutlinePolicy
{
    public const string Role = "Geometry/SheetOnePlanBaseOutline";
    public static DrawingPrimitive Apply(DrawingPrimitive primitive, PresentationRequest presentation) =>
        SheetOnePresentationPolicy.Enabled(presentation) && primitive is PolylinePrimitive
        { ViewId:"Plan", ComponentId:"Base" or "Pad", Role:"Geometry/Material" } p && IsRectangle(p)
            ? new PolylinePrimitive(p.SemanticId,Role,p.ViewId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,
                p.Points,p.Closed,p.LogicalLengthMm,p.MaterialKind)
            : primitive;
    public static IReadOnlyList<LinePathSegment> NativeEdges(PolylinePrimitive primitive)
    {
        if(primitive.Role!=Role || primitive.ViewId!="Plan" || (primitive.ComponentId!="Base" && primitive.ComponentId!="Pad")) return [];
        if(!primitive.Closed && primitive.Points.Length==2) return [new(primitive.Points[0],primitive.Points[1])];
        if(!IsRectangle(primitive)) return [];
        double left=primitive.Points.Min(p=>p.X),right=primitive.Points.Max(p=>p.X),
            bottom=primitive.Points.Min(p=>p.Y),top=primitive.Points.Max(p=>p.Y);
        var topLeft=new Point2Mm(left,top);var topRight=new Point2Mm(right,top);
        var bottomRight=new Point2Mm(right,bottom);var bottomLeft=new Point2Mm(left,bottom);
        // Reference native endpoint directions: top rightwards, right downwards,
        // bottom leftwards, left downwards. Preserve one outline ID with four handles.
        return [new(topLeft,topRight),new(topRight,bottomRight),new(bottomRight,bottomLeft),new(topLeft,bottomLeft)];
    }
    public static IReadOnlyList<DrawingPrimitive> ClipAtPipes(IEnumerable<DrawingPrimitive> primitives,PresentationRequest presentation)
    {
        var original=primitives.ToArray();
        if(!SheetOnePresentationPolicy.Enabled(presentation)) return original;
        var pipes=original.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.Role=="Geometry/PlanPipeVisible" &&
            p.SemanticId.Contains("/VisibleOuter/",StringComparison.Ordinal) && !p.Closed && p.Points.Length==2)
            .GroupBy(p=>p.SemanticId[..p.SemanticId.LastIndexOf("/VisibleOuter/",StringComparison.Ordinal)])
            .Where(g=>g.Count()==2).Select(g=>g.SelectMany(p=>p.Points).ToArray())
            .Select(p=>new Bounds2Mm(p.Min(v=>v.X),p.Min(v=>v.Y),p.Max(v=>v.X),p.Max(v=>v.Y))).ToArray();
        var result=new List<DrawingPrimitive>();
        foreach(var primitive in original)
        {
            if(primitive is not PolylinePrimitive outline || outline.Role!=Role || !IsRectangle(outline))
            { result.Add(primitive);continue; }
            var visible=new List<LinePathSegment>();var hidden=new List<LinePathSegment>();bool changed=false;
            foreach(var edge in NativeEdges(outline))
            {
                double dx=edge.End.X-edge.Start.X,dy=edge.End.Y-edge.Start.Y;
                var cuts=new List<(double Start,double End)>();
                foreach(var box in pipes)
                {
                    double a,b;
                    if(dy==0 && edge.Start.Y>=box.MinY && edge.Start.Y<=box.MaxY)
                    {a=(box.MinX-edge.Start.X)/dx;b=(box.MaxX-edge.Start.X)/dx;}
                    else if(dx==0 && edge.Start.X>=box.MinX && edge.Start.X<=box.MaxX)
                    {a=(box.MinY-edge.Start.Y)/dy;b=(box.MaxY-edge.Start.Y)/dy;}
                    else continue;
                    double low=Math.Max(0,Math.Min(a,b)),high=Math.Min(1,Math.Max(a,b));
                    if(high>low+1e-8) cuts.Add((low,high));
                }
                double cursor=0;
                Point2Mm At(double t)=>new(edge.Start.X+t*dx,edge.Start.Y+t*dy);
                double hiddenStart=0,hiddenEnd=0;bool hasHidden=false;
                foreach(var cut in cuts.OrderBy(c=>c.Start))
                {
                    if(cut.Start>cursor+1e-8) visible.Add(new(At(cursor),At(cut.Start)));
                    if(!hasHidden) {hiddenStart=cut.Start;hiddenEnd=cut.End;hasHidden=true;}
                    else if(cut.Start<=hiddenEnd+1e-8) hiddenEnd=Math.Max(hiddenEnd,cut.End);
                    else {hidden.Add(new(At(hiddenStart),At(hiddenEnd)));hiddenStart=cut.Start;hiddenEnd=cut.End;}
                    cursor=Math.Max(cursor,cut.End);changed=true;
                }
                if(hasHidden) hidden.Add(new(At(hiddenStart),At(hiddenEnd)));
                if(cursor<1-1e-8) visible.Add(new(At(cursor),edge.End));
            }
            if(!changed) {result.Add(outline);continue;}
            for(int i=0;i<visible.Count;i++) result.Add(new PolylinePrimitive(outline.SemanticId+"/Visible/"+i,
                Role,outline.ViewId,outline.ComponentId,outline.OccurrenceId,outline.ScheduleMark,
                [visible[i].Start,visible[i].End],false,materialKind:outline.MaterialKind));
            for(int i=0;i<hidden.Count;i++) result.Add(new PolylinePrimitive(outline.SemanticId+"/HiddenByPipe/"+i,
                "Geometry/PlanPipeHidden",outline.ViewId,outline.ComponentId,outline.OccurrenceId,outline.ScheduleMark,
                [hidden[i].Start,hidden[i].End],false,materialKind:outline.MaterialKind));
        }
        return result;
    }
    private static bool IsRectangle(PolylinePrimitive p)=>p.Closed && p.Points.Length==4 && p.Points.Distinct().Count()==4 &&
        p.Points.Select(v=>v.X).Distinct().Count()==2 && p.Points.Select(v=>v.Y).Distinct().Count()==2;
}
