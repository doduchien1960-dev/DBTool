using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Collectors and native ticks at intersections with visible physical bar segments.</summary>
public static class SheetTwoGachCollector
{
    public const string CollectorRole = "Rebar/Collector";
    private sealed record Segment(PolylinePrimitive Bar, Point2Mm A, Point2Mm B,
        double Ux, double Uy, double Length);

    public static OperationResult<IReadOnlyList<DrawingPrimitive>> BuildForGeometry(
        IEnumerable<DrawingPrimitive> primitives,IReadOnlySet<string> mainBarIds,GeometryModel geometry)
    {
        var source=primitives.ToArray();
        var diagnostics=new List<Diagnostic>();
        var result=Build(source,mainBarIds,tick=>
        {
            var clipped=WallOpeningSymbolClipper.Clip([tick],geometry,tick.ComponentId!);
            if(!clipped.Success) {diagnostics.AddRange(clipped.Diagnostics);return false;}
            return clipped.Value!.Count==1;
        });
        if(diagnostics.Count>0) return new(false,null,diagnostics.Distinct().ToArray());
        var expected=source.OfType<PolylinePrimitive>().Where(p=>p.OccurrenceId is {} id && mainBarIds.Contains(id) &&
            p.ScheduleMark is not null && p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom" &&
            (p.ViewId is "BaseTop" or "BaseBottom" || p.ViewId.Contains("/Wall/",StringComparison.Ordinal) || p.ViewId.StartsWith("Wall/",StringComparison.Ordinal)))
            .Select(p=>(p.ViewId,p.OccurrenceId)).Distinct().ToArray();
        var covered=result.OfType<BlockPrimitive>().Select(t=>(t.ViewId,t.OccurrenceId)).ToHashSet();
        var missing=expected.Where(p=>!covered.Contains(p)).ToArray();
        if(missing.Length>0) return new(false,null,[new Diagnostic("GACH_COLLECTOR_NO_VALID_ROUTE",
            DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
            "each visible main bar has an admissible native tick at its collector intersection",
            string.Join(",",missing.Select(p=>p.ViewId+":"+p.OccurrenceId)),
            "No tick was moved off its physical bar or placed in a cutout; another route or supported curve projection is required.")]);
        return new(true,result,[]);
    }

    public static IReadOnlyList<DrawingPrimitive> Build(IEnumerable<DrawingPrimitive> primitives,
        IReadOnlySet<string> mainBarIds, Func<BlockPrimitive,bool>? isValidTick = null)
    {
        var result=new List<DrawingPrimitive>();
        var bars=primitives.OfType<PolylinePrimitive>().Where(p=>p.OccurrenceId is {} id &&
            mainBarIds.Contains(id) && p.ScheduleMark is not null &&
            p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom" &&
            (p.ViewId is "BaseTop" or "BaseBottom" || p.ViewId.Contains("/Wall/",StringComparison.Ordinal) || p.ViewId.StartsWith("Wall/",StringComparison.Ordinal)) &&
            !p.Bulges.Any(b=>Math.Abs(b)>1e-9));
        foreach(var family in bars.GroupBy(p=>(p.ViewId,p.ComponentId))
            .OrderBy(g=>g.Key.ViewId,StringComparer.Ordinal).ThenBy(g=>g.Key.ComponentId,StringComparer.Ordinal))
        {
            var segments=new List<Segment>();
            foreach(var occurrence in family.GroupBy(p=>p.OccurrenceId))
            {
                var pieces=occurrence.SelectMany(p=>Enumerable.Range(0,p.Points.Length-1).Select(i=>
                {
                    var a=p.Points[i]; var b=p.Points[i+1];
                    double dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);
                    if(dx< -1e-9 || Math.Abs(dx)<=1e-9 && dy<0) { dx=-dx; dy=-dy; }
                    return new Segment(p,a,b,length>1e-9?dx/length:0,length>1e-9?dy/length:0,length);
                })).Where(s=>s.Length>1e-7).OrderByDescending(s=>s.Length)
                    .ThenBy(s=>s.Bar.SemanticId,StringComparer.Ordinal).ToArray();
                if(pieces.Length==0) continue;
                var primary=pieces[0];
                // Hook legs do not create another tick for the same occurrence.
                segments.AddRange(pieces.Where(s=>Math.Abs(s.Ux-primary.Ux)<1e-7 && Math.Abs(s.Uy-primary.Uy)<1e-7));
            }
            foreach(var direction in segments.GroupBy(s=>(Math.Round(s.Ux,7),Math.Round(s.Uy,7))))
            {
                double ux=direction.First().Ux,uy=direction.First().Uy,nx=uy,ny=-ux;
                var remaining=direction.ToList();
                while(remaining.Count>0)
                {
                    double Along(Point2Mm p)=>p.X*ux+p.Y*uy;
                    double Across(Point2Mm p)=>p.X*nx+p.Y*ny;
                    var candidates=remaining.SelectMany(s=>
                    {
                        double low=Math.Min(Along(s.A),Along(s.B)),high=Math.Max(Along(s.A),Along(s.B));
                        double inset=75*.3+1e-6;
                        return high-low>2*inset?new[]{(low+high)/2,low+inset,high-inset}:new[]{(low+high)/2};
                    }).Distinct().ToArray();
                    double center=candidates.Average();
                    Point2Mm Intersection(Segment s,double c)=>new(
                        s.A.X+(s.B.X-s.A.X)*(c-Along(s.A))/(Along(s.B)-Along(s.A)),
                        s.A.Y+(s.B.Y-s.A.Y)*(c-Along(s.A))/(Along(s.B)-Along(s.A)));
                    BlockPrimitive Tick(Segment s,double c,string id)=>new(id,SheetTwoGach.Role,
                        family.Key.ViewId,family.Key.ComponentId,"Block:Gach",Intersection(s,c),.3,[],
                        Math.Atan2(ny,nx),s.Bar.OccurrenceId,s.Bar.ScheduleMark);
                    var chosen=candidates.Select(c=>(C:c,Hits:remaining.Where(s=>c>=Math.Min(Along(s.A),Along(s.B))-1e-7 &&
                        c<=Math.Max(Along(s.A),Along(s.B))+1e-7 &&
                        (isValidTick is null || isValidTick(Tick(s,c,"Gach/Candidate")))).GroupBy(s=>s.Bar.OccurrenceId)
                        .Select(g=>g.OrderBy(s=>s.Bar.SemanticId,StringComparer.Ordinal).First()).ToArray()))
                        .OrderByDescending(c=>c.Hits.Length).ThenBy(c=>Math.Abs(c.C-center)).ThenBy(c=>c.C).First();
                    if(chosen.Hits.Length==0) break;
                    var tips=chosen.Hits.Select(s=>(Segment:s,Point:Intersection(s,chosen.C)))
                        .OrderBy(t=>Across(t.Point)).ToArray();
                    string id=$"{family.Key.ViewId}/Collector/{result.Count:000}";
                    var first=tips[0]; var last=tips[^1];
                    // Extend a singleton collector by the native Gach half-width.
                    double extension=Math.Abs(Across(last.Point)-Across(first.Point))<1e-7?75*.3:0;
                    result.Add(new PolylinePrimitive(id,CollectorRole,family.Key.ViewId,family.Key.ComponentId,
                        first.Segment.Bar.OccurrenceId,first.Segment.Bar.ScheduleMark,
                        [new(first.Point.X-nx*extension,first.Point.Y-ny*extension),
                         new(last.Point.X+nx*extension,last.Point.Y+ny*extension)],false));
                    foreach(var tip in tips)
                        result.Add(Tick(tip.Segment,chosen.C,id+"/Gach/"+tip.Segment.Bar.OccurrenceId));
                    var covered=tips.Select(t=>t.Segment.Bar.OccurrenceId).ToHashSet(StringComparer.Ordinal);
                    remaining.RemoveAll(s=>covered.Contains(s.Bar.OccurrenceId));
                }
            }
        }
        return result;
    }
}
