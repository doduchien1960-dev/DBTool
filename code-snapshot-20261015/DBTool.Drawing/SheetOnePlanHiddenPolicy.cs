using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Connected concealed wall edges form one native first-sheet perimeter, with every edge ID retained.</summary>
public static class SheetOnePlanHiddenPolicy
{
    public const string WallRole="Geometry/SheetOnePlanWallHidden";
    public const string HoleRole="Geometry/SheetOnePlanHoleHidden";
    public static IReadOnlyList<DrawingPrimitive> Compose(IEnumerable<DrawingPrimitive> primitives,PresentationRequest presentation)
    {
        var original=primitives.ToArray();
        if(!SheetOnePresentationPolicy.Enabled(presentation)) return original;
        var edges=original.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.Role=="Geometry/PlanHidden" &&
            p.ComponentId is "Wall/T1" or "Wall/T2" or "Wall/T3" or "Wall/T4").ToArray();
        Point2Mm[]? ring=null;
        if(edges.Length==4 && edges.Select(p=>p.ComponentId).Distinct().Count()==4 &&
            edges.All(p=>!p.Closed && p.Points.Length==2 && p.OccurrenceId is null && p.ScheduleMark is null))
        {
            var corners=RectangleCorners(edges.SelectMany(p=>p.Points).Distinct().ToArray());
            if(corners is not null && Enumerable.Range(0,4).All(i=>edges.Count(e=>
                e.Points[0]==corners[i] && e.Points[1]==corners[(i+1)%4] ||
                e.Points[1]==corners[i] && e.Points[0]==corners[(i+1)%4])==1))
                ring=[..corners,corners[0]];
        }
        return original.Select(Replace).ToArray();
        DrawingPrimitive Replace(DrawingPrimitive primitive)
        {
            if(primitive is not PolylinePrimitive p) return primitive;
            if(ring is not null && edges.Contains(p)) return Copy(p,WallRole,ring,false);
            if(p is { ViewId:"Plan",Role:"Geometry/PlanHidden",ComponentId:"Roof",Closed:true,OccurrenceId:null } &&
                RectangleCorners(p.Points.ToArray()) is { } hole) return Copy(p,HoleRole,hole,true);
            return primitive;
        }
    }
    public static IReadOnlyDictionary<string,string> RenderAliases(IEnumerable<DrawingPrimitive> primitives)
    {
        var first=new Dictionary<(Point2Mm,Point2Mm,Point2Mm,Point2Mm),string>();
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var p in primitives.OfType<PolylinePrimitive>().Where(p=>p is
            { ViewId:"Plan",Role:WallRole,Closed:false,OccurrenceId:null,ScheduleMark:null } && p.Points.Length==5 && p.Points[0]==p.Points[4]))
        {
            var corners=RectangleCorners(p.Points.Take(4).ToArray());
            if(corners is null || !corners.SequenceEqual(p.Points.Take(4))) continue;
            var key=(corners[0],corners[1],corners[2],corners[3]);
            if(first.TryGetValue(key,out var representative)) aliases.Add(p.SemanticId,representative);
            else first.Add(key,p.SemanticId);
        }
        return aliases;
    }
    private static PolylinePrimitive Copy(PolylinePrimitive p,string role,Point2Mm[] points,bool closed)=>
        new(p.SemanticId,role,p.ViewId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,points,closed,p.LogicalLengthMm,p.MaterialKind);
    private static Point2Mm[]? RectangleCorners(Point2Mm[] p)
    {
        if(p.Length!=4 || p.Distinct().Count()!=4 || p.Select(v=>v.X).Distinct().Count()!=2 || p.Select(v=>v.Y).Distinct().Count()!=2) return null;
        double left=p.Min(v=>v.X),right=p.Max(v=>v.X),bottom=p.Min(v=>v.Y),top=p.Max(v=>v.Y);
        return [new(left,top),new(right,top),new(right,bottom),new(left,bottom)];
    }
}
