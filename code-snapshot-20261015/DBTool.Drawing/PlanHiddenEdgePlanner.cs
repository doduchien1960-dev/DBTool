using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Concealed wall-union boundaries; shared material seams and visible roof edges are not duplicate outlines.</summary>
public static class PlanHiddenEdgePlanner
{
    private const double Epsilon=1e-8;

    public static IReadOnlyList<DrawingPrimitive> Build(ViewProfile plan)
    {
        var walls=plan.Regions.Where(r=>r.SourceComponentId.StartsWith("Wall/",StringComparison.Ordinal) &&
            r.SourceComponentId!="Wall/Mortar").ToArray();
        var roofs=plan.Regions.Where(r=>r.SourceComponentId=="Roof").ToArray();
        var result=new List<DrawingPrimitive>();
        foreach(var wall in walls)
        {
            int index=0;
            foreach(var edge in Edges(wall.Footprint.Outer))
            {
                var splits=new List<double>{0,1};
                foreach(var point in walls.Concat(roofs).SelectMany(w=>w.Footprint.Outer.Vertices))
                    if(OnSegment(point,edge.A,edge.B)) splits.Add(Parameter(point,edge.A,edge.B));
                var ordered=splits.Order().DistinctBy(t=>Math.Round(t,9)).ToArray();
                for(int i=1;i<ordered.Length;i++)
                {
                    var a=Lerp(edge.A,edge.B,ordered[i-1]);var b=Lerp(edge.A,edge.B,ordered[i]);
                    var midpoint=Lerp(a,b,.5);
                    if(walls.Any(other=>other!=wall && ContainsOrBoundary(other.Footprint.Outer,midpoint))) continue;
                    if(roofs.Any(roof=>Edges(roof.Footprint.Outer).Any(e=>OnSegment(midpoint,e.A,e.B)))) continue;
                    result.Add(new PolylinePrimitive($"Plan/{wall.Id}/Hidden/{index++}","Geometry/PlanHidden",plan.Id,
                        wall.SourceComponentId,null,null,[a,b],false));
                }
            }
        }
        return result;
    }

    private static IEnumerable<(Point2Mm A,Point2Mm B)> Edges(Polygon2Mm polygon)
    {
        for(int i=0;i<polygon.Vertices.Length;i++) yield return (polygon.Vertices[i],polygon.Vertices[(i+1)%polygon.Vertices.Length]);
    }
    private static double Parameter(Point2Mm p,Point2Mm a,Point2Mm b)=>
        ((p.X-a.X)*(b.X-a.X)+(p.Y-a.Y)*(b.Y-a.Y))/(Math.Pow(b.X-a.X,2)+Math.Pow(b.Y-a.Y,2));
    private static bool OnSegment(Point2Mm p,Point2Mm a,Point2Mm b)
    {
        double length=Math.Sqrt(Math.Pow(b.X-a.X,2)+Math.Pow(b.Y-a.Y,2));
        if(length<Epsilon) return Math.Abs(p.X-a.X)<Epsilon && Math.Abs(p.Y-a.Y)<Epsilon;
        double cross=(p.X-a.X)*(b.Y-a.Y)-(p.Y-a.Y)*(b.X-a.X),t=Parameter(p,a,b);
        return Math.Abs(cross)/length<Epsilon && t>=-Epsilon && t<=1+Epsilon;
    }
    private static Point2Mm Lerp(Point2Mm a,Point2Mm b,double t)=>new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y));
    private static bool ContainsOrBoundary(Polygon2Mm polygon,Point2Mm p)
    {
        bool inside=false;
        foreach(var edge in Edges(polygon))
        {
            if(OnSegment(p,edge.A,edge.B)) return true;
            if((edge.A.Y>p.Y)!=(edge.B.Y>p.Y) && p.X<(edge.B.X-edge.A.X)*(p.Y-edge.A.Y)/(edge.B.Y-edge.A.Y)+edge.A.X) inside=!inside;
        }
        return inside;
    }
}
