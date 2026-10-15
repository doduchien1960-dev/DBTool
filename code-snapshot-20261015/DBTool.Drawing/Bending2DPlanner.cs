using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Rigid local-plane unfolding. Physical lengths and analytic arcs are preserved.</summary>
public static class Bending2DPlanner
{
    /// <summary>Manufacturing unfold for the five straight legs of an approved hooked ladder rung.</summary>
    public static OperationResult<IReadOnlyList<DrawingPathSegment>> UnfoldLadder(BarPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if(path.Closed || path.Segments.Length!=5 || path.Segments.Any(s=>s is not LineSegment3Mm)) return Block();
        var directions=path.Segments.Select(s=>new Vector3(s.End.X-s.Start.X,s.End.Y-s.Start.Y,s.End.Z-s.Start.Z)).ToArray();
        double Dot(Vector3 a,Vector3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        var lengths=directions.Select(v=>Math.Sqrt(Dot(v,v))).ToArray();
        if(lengths.Any(l=>!double.IsFinite(l)||l<=0)) return Block();
        var units=directions.Select((v,i)=>new Vector3(v.X/lengths[i],v.Y/lengths[i],v.Z/lengths[i])).ToArray();
        for(var i=1;i<5;i++)
            if(path.Segments[i-1].End!=path.Segments[i].Start || Math.Abs(Dot(units[i-1],units[i]))>1e-9) return Block();
        // Two opposite hooks and two opposite embedment legs around one crossbar.
        if(Math.Abs(Dot(units[0],units[4])+1)>1e-9 || Math.Abs(Dot(units[1],units[3])+1)>1e-9 ||
            Math.Abs(Dot(units[0],units[2]))>1e-9 || Math.Abs(Dot(units[1],units[2]))>1e-9) return Block();
        Point2Mm[] points=[new(0,0),new(lengths[0],0),new(lengths[0],-lengths[1]),
            new(lengths[0]+lengths[2],-lengths[1]),new(lengths[0]+lengths[2],-lengths[1]+lengths[3]),
            new(lengths[0]+lengths[2]-lengths[4],-lengths[1]+lengths[3])];
        return new(true,points.Zip(points.Skip(1),(a,b)=>(DrawingPathSegment)new LinePathSegment(a,b)).ToArray(),[]);
        OperationResult<IReadOnlyList<DrawingPathSegment>> Block()=>new(false,null,
            [new Diagnostic("BENDING_LADDER_CONTRACT_INVALID",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
                "five orthogonal straight legs with opposite hooks and embedments","unsupported ladder path",
                "Ladder unfolding must preserve every segment length and bend angle.")]);
    }
    public static OperationResult<IReadOnlyList<DrawingPathSegment>> Project(BarPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var origin=path.Segments[0].Start;
        Vector3 Sub(Point3Mm a,Point3Mm b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        double Dot(Vector3 a,Vector3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        Vector3 Cross(Vector3 a,Vector3 b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        Vector3 Unit(Vector3 v) { var length=Math.Sqrt(Dot(v,v)); return new(v.X/length,v.Y/length,v.Z/length); }
        var first=path.Segments[0];
        var u=first is ArcSegment3Mm firstArc ? firstArc.StartDirection : Unit(Sub(first.End,first.Start));
        Vector3? normal=path.Segments.OfType<ArcSegment3Mm>().FirstOrDefault()?.Normal;
        if(normal is null)
            foreach(var segment in path.Segments)
            {
                var candidate=Cross(u,Sub(segment.End,origin));
                if(Dot(candidate,candidate)>1e-12) { normal=Unit(candidate); break; }
            }
        normal ??= Unit(Cross(u,Math.Abs(u.Z)<0.9 ? new Vector3(0,0,1) : new Vector3(0,1,0)));
        var v=Unit(Cross(normal,u));
        Point2Mm Map(Point3Mm p) { var d=Sub(p,origin); return new(Dot(d,u),Dot(d,v)); }
        bool InPlane(Point3Mm p)=>Math.Abs(Dot(Sub(p,origin),normal))<=1e-6;
        var segments=new List<DrawingPathSegment>();
        foreach(var segment in path.Segments)
        {
            if(!InPlane(segment.Start)||!InPlane(segment.End)) return Block();
            if(segment is LineSegment3Mm line) segments.Add(new LinePathSegment(Map(line.Start),Map(line.End)));
            else if(segment is ArcSegment3Mm arc)
            {
                if(!InPlane(arc.Center)||Math.Abs(Math.Abs(Dot(arc.Normal,normal))-1)>1e-9) return Block();
                segments.Add(new ArcPathSegment(Map(arc.Center),arc.CenterlineRadiusMm,
                    Math.Atan2(Dot(arc.StartDirection,v),Dot(arc.StartDirection,u)),arc.SweepRadians*Math.Sign(Dot(arc.Normal,normal))));
            }
            else return Block();
        }
        return new(true,segments.AsReadOnly(),[]);
        OperationResult<IReadOnlyList<DrawingPathSegment>> Block()=>new(false,null,
            [new Diagnostic("BENDING_NONPLANAR_UNSUPPORTED",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
                "planar line/arc bending path","nonplanar or unsupported path","A manufacturing unfolding contract is required; XY flattening is forbidden.")]);
    }
}
