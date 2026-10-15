using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Presentation clipping to the material cut, never a change to physical bars or schedules.</summary>
public static class SectionRebarClipper
{
    public static IReadOnlyList<ArcPathSegment> ClipArc(ArcPathSegment arc,IEnumerable<Footprint> material)
    {
        var footprints=material.ToArray();var cuts=new SortedSet<double>{0,1};
        foreach(var loop in footprints.SelectMany(f=>new[]{f.Outer}.Concat(f.InnerLoops)))
        for(int i=0;i<loop.Vertices.Length;i++)
        {
            var a=loop.Vertices[i];var b=loop.Vertices[(i+1)%loop.Vertices.Length];
            double dx=b.X-a.X,dy=b.Y-a.Y,x=a.X-arc.Center.X,y=a.Y-arc.Center.Y;
            double aa=dx*dx+dy*dy,bb=2*(x*dx+y*dy),cc=x*x+y*y-arc.RadiusMm*arc.RadiusMm;
            if(aa<1e-16) continue;double discriminant=bb*bb-4*aa*cc;
            if(discriminant<0) continue;
            foreach(double edge in new[]{(-bb-Math.Sqrt(discriminant))/(2*aa),(-bb+Math.Sqrt(discriminant))/(2*aa)})
            {
                if(edge<0 || edge>1) continue;
                double angle=Math.Atan2(y+edge*dy,x+edge*dx);
                double delta=Math.Sign(arc.SweepRadians)*(angle-arc.StartRadians);
                delta=(delta%(2*Math.PI)+2*Math.PI)%(2*Math.PI);
                double t=delta/Math.Abs(arc.SweepRadians);
                if(t>1e-10 && t<1-1e-10) cuts.Add(t);
            }
        }
        var parameters=cuts.ToArray();var result=new List<ArcPathSegment>();
        for(int i=1;i<parameters.Length;i++)
        {
            if(parameters[i]-parameters[i-1]<1e-10) continue;
            double middle=arc.StartRadians+(parameters[i]+parameters[i-1])/2*arc.SweepRadians;
            var point=new Point2Mm(arc.Center.X+arc.RadiusMm*Math.Cos(middle),arc.Center.Y+arc.RadiusMm*Math.Sin(middle));
            if(Clip(point,point,footprints).Count==0) continue;
            result.Add(new ArcPathSegment(arc.Center,arc.RadiusMm,arc.StartRadians+parameters[i-1]*arc.SweepRadians,
                (parameters[i]-parameters[i-1])*arc.SweepRadians));
        }
        return result;
    }

    public static IReadOnlyList<(Point2Mm Start, Point2Mm End)> Clip(Point2Mm start, Point2Mm end, IEnumerable<Footprint> material)
    {
        var footprints = material.ToArray();
        bool Inside(Point2Mm p) => footprints.Any(f => InLoop(p,f.Outer) && !f.InnerLoops.Any(h => InLoop(p,h)));
        if (start == end) return Inside(start) ? [(start,end)] : [];
        double dx=end.X-start.X, dy=end.Y-start.Y;
        var parameters = new SortedSet<double> {0,1};
        foreach (var loop in footprints.SelectMany(f=>new[]{f.Outer}.Concat(f.InnerLoops)))
        for(int i=0;i<loop.Vertices.Length;i++)
        {
            var a=loop.Vertices[i]; var b=loop.Vertices[(i+1)%loop.Vertices.Length];
            double ex=b.X-a.X,ey=b.Y-a.Y,den=dx*ey-dy*ex;
            if(Math.Abs(den)<1e-12) continue;
            double t=((a.X-start.X)*ey-(a.Y-start.Y)*ex)/den;
            double u=((a.X-start.X)*dy-(a.Y-start.Y)*dx)/den;
            if(t>0 && t<1 && u>=0 && u<=1) parameters.Add(t);
        }
        Point2Mm At(double t)=>new(start.X+t*dx,start.Y+t*dy);
        var cuts=parameters.ToArray(); var result=new List<(Point2Mm,Point2Mm)>();
        for(int i=1;i<cuts.Length;i++)
        {
            var pStart = At(cuts[i-1]);
            var pEnd = At(cuts[i]);
            if (Math.Abs(pEnd.X - pStart.X) < 1e-4 && Math.Abs(pEnd.Y - pStart.Y) < 1e-4) continue;
            if(Inside(At((cuts[i-1]+cuts[i])/2))) result.Add((pStart, pEnd));
        }
        return result;
    }
    private static bool InLoop(Point2Mm p, Polygon2Mm loop)
    {
        bool inside=false;
        for(int i=0,j=loop.Vertices.Length-1;i<loop.Vertices.Length;j=i++)
        {
            var a=loop.Vertices[i];var b=loop.Vertices[j];
            if((a.Y>p.Y)!=(b.Y>p.Y) && p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
        }
        return inside;
    }
}
