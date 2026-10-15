using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Display material boundaries only; never modifies the calculation geometry.</summary>
public static class SheetOneHatchRegions
{
    private const double Epsilon = 1e-8;

    /// <summary>One topology for both ink and hatch, grouped by the actual pour joint.
    /// Cancels shared edges of the partitioned section; never fills an inner loop.</summary>
    public static IReadOnlyList<MaterialRegion> Build(ViewProfile profile, bool monolithic)
    {
        var activeRegions = profile.Regions.Where(r => !(r.SourceComponentId == "Pad" && Area(r.Footprint.Outer) <= Epsilon)).ToArray();
        var result = new List<MaterialRegion>();
        foreach (var group in activeRegions.GroupBy(r => Group(r)))
        {
            var regions = group.ToArray();
            if (regions.Length < 2 || !group.Key.StartsWith("Cast/", StringComparison.Ordinal))
            { result.AddRange(regions); continue; }
            var polygons = regions.SelectMany(r => new[] { r.Footprint.Outer }.Concat(r.Footprint.InnerLoops)).ToArray();
            var points = polygons.SelectMany(p => p.Vertices).Distinct().ToArray();
            var edges = new HashSet<(Point2Mm A, Point2Mm B)>();
            foreach (var region in regions)
            foreach (var loop in new[] { region.Footprint.Outer }.Concat(region.Footprint.InnerLoops))
            {
                bool hole = loop != region.Footprint.Outer;
                var vertices = (SignedArea(loop) > 0 != hole ? loop.Vertices.AsEnumerable() : loop.Vertices.Reverse()).ToArray();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var a = vertices[i]; var b = vertices[(i + 1) % vertices.Length];
                    double dx = b.X-a.X, dy = b.Y-a.Y, length = dx*dx+dy*dy;
                    if (length < Epsilon) continue;
                    double Parameter(Point2Mm p) => ((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length;
                    var split = points.Where(p => Math.Abs((p.X-a.X)*dy-(p.Y-a.Y)*dx)<Epsilon &&
                        Parameter(p)>=-Epsilon && Parameter(p)<=1+Epsilon).OrderBy(Parameter).ToArray();
                    for (int j=1;j<split.Length;j++)
                        if (!edges.Remove((split[j],split[j-1]))) edges.Add((split[j-1],split[j]));
                }
            }
            var loops = new List<Polygon2Mm>();
            bool valid = true;
            while (edges.Count>0 && valid)
            {
                var start=edges.Select(e=>e.A).OrderBy(p=>p.X).ThenBy(p=>p.Y).First();
                var path=new List<Point2Mm>(); var current=start;
                do
                {
                    path.Add(current);
                    var next=edges.Where(e=>e.A==current).ToArray();
                    if(next.Length!=1) { valid=false;break; }
                    edges.Remove(next[0]);current=next[0].B;
                } while(current!=start && edges.Count>0);
                if(!valid || current!=start || path.Count<3) { valid=false;break; }
                loops.Add(new Polygon2Mm(path));
            }
            var outers=loops.Where(p=>SignedArea(p)>0).ToArray();
            var holes=loops.Where(p=>SignedArea(p)<0).ToArray();
            double expected=regions.Sum(r=>Area(r.Footprint.Outer)-r.Footprint.InnerLoops.Sum(Area));
            double actual=outers.Sum(Area)-holes.Sum(Area);
            if(!valid || outers.Length==0 || Math.Abs(expected-actual)>1e-6)
            { result.AddRange(regions);continue; }
            for(int i=0;i<outers.Length;i++)
            {
                var outer=outers[i];
                var owner=regions.FirstOrDefault(r=>r.Footprint.Outer.Vertices.ToHashSet().SetEquals(outer.Vertices))
                    ?? regions.FirstOrDefault(r=>r.SourceComponentId=="Base") ?? regions[0];
                result.Add(new MaterialRegion(owner.Id+"/Cast/"+i,owner.SourceComponentId,owner.MaterialId,
                    new Footprint(outer,holes.Where(h=>Contains(outer,h.Vertices[0]))),regions.SelectMany(r=>r.Sources).Distinct()));
            }
        }
        return result;

        string Group(MaterialRegion r)
        {
            if(r.MaterialId!="Concrete") return "Material/"+r.Id;
            string id=r.SourceComponentId;
            if(id=="Base" || id.StartsWith("Base/",StringComparison.Ordinal) || id.StartsWith("Wall/",StringComparison.Ordinal))
                return "Cast/Lower";
            if(id=="Roof" || id.StartsWith("Roof/",StringComparison.Ordinal) || id=="Neck" || id.StartsWith("Neck/",StringComparison.Ordinal))
                return monolithic ? "Cast/Lower" : "Cast/Upper";
            return "Material/"+r.Id;
        }
    }

    private static double SignedArea(Polygon2Mm p)=>p.Vertices.Select((v,i)=>
        v.X*p.Vertices[(i+1)%p.Vertices.Length].Y-v.Y*p.Vertices[(i+1)%p.Vertices.Length].X).Sum()/2;

    private static bool Contains(Polygon2Mm polygon,Point2Mm p)
    {
        bool inside=false;var vertices=polygon.Vertices;
        for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
            if((vertices[i].Y>p.Y)!=(vertices[j].Y>p.Y) &&
                p.X<(vertices[j].X-vertices[i].X)*(p.Y-vertices[i].Y)/(vertices[j].Y-vertices[i].Y)+vertices[i].X)
                inside=!inside;
        return inside;
    }

    public static IReadOnlyList<MaterialRegion> Build(ViewProfile profile)
    {
        var activeRegions = profile.Regions.Where(r => !(r.SourceComponentId == "Pad" && Area(r.Footprint.Outer) <= Epsilon)).ToArray();
        var bases = activeRegions.Where(r=>r.SourceComponentId=="Base").ToArray();
        if(bases.Length!=1) return activeRegions;
        var floor=bases[0].Footprint.Outer.Vertices.Max(p=>p.Y);
        var haunches = activeRegions.Where(r=>r.SourceComponentId=="Base/Haunch").ToArray();
        var walls = activeRegions.Where(r=>r.SourceComponentId.StartsWith("Wall/",StringComparison.Ordinal)
            && r.MaterialId=="Concrete" &&
            Math.Abs(r.Footprint.Outer.Vertices.Min(p=>p.Y)-floor)<Epsilon).ToArray();
        // Join only the two physical feet. Separate material above a pipe opening
        // remains untouched, so the union cannot fill the opening gap.
        if (bases.Length!=1 || haunches.Length!=2 || walls.Length!=2 ||
            bases.Concat(haunches).Concat(walls).Any(r=>r.MaterialId!="Concrete" || r.Footprint.InnerLoops.Length!=0)
            || !Rectangle(bases[0]) || walls.Any(r=>!Rectangle(r))) return profile.Regions;
        var top=haunches.SelectMany(r=>r.Footprint.Outer.Vertices).Max(p=>p.Y);
        if (top<=floor || haunches.Any(r=>Math.Abs(r.Footprint.Outer.Vertices.Min(p=>p.Y)-floor)>Epsilon)
            || walls.Any(r=>Math.Abs(r.Footprint.Outer.Vertices.Min(p=>p.Y)-floor)>Epsilon
                || r.Footprint.Outer.Vertices.Max(p=>p.Y)<=top)) return profile.Regions;
        var feet=walls.Select(r=>Box(r,floor,top)).ToArray();
        var pieces=bases.Concat(haunches).Concat(feet).ToArray();
        var polygon=Join(pieces.Select(r=>r.Footprint.Outer).ToArray());
        if (polygon is null || Math.Abs(Area(polygon)-pieces.Sum(r=>Area(r.Footprint.Outer)))>1e-6)
            return activeRegions;
        var merged=new MaterialRegion(bases[0].Id+"/JoinedFeet",bases[0].SourceComponentId,"Concrete",
            new Footprint(polygon,[]),pieces.SelectMany(r=>r.Sources).Distinct());
        return activeRegions.Where(r=>!haunches.Contains(r)).Select(r=>
            r==bases[0] ? merged : walls.Contains(r) ? Box(r,top,r.Footprint.Outer.Vertices.Max(p=>p.Y)) : r).ToArray();
    }

    private static MaterialRegion Box(MaterialRegion source,double bottom,double top)
    {
        var left=source.Footprint.Outer.Vertices.Min(p=>p.X);
        var right=source.Footprint.Outer.Vertices.Max(p=>p.X);
        return new MaterialRegion(source.Id,source.SourceComponentId,source.MaterialId,
            new Footprint(new Polygon2Mm([new(left,bottom),new(right,bottom),new(right,top),new(left,top)]),[]),source.Sources);
    }

    private static bool Rectangle(MaterialRegion r)
    {
        var p=r.Footprint.Outer.Vertices;
        return p.Length==4 && p.Select(v=>v.X).Distinct().Count()==2 && p.Select(v=>v.Y).Distinct().Count()==2;
    }

    private static double Area(Polygon2Mm polygon) => Math.Abs(polygon.Vertices.Select((p,i)=>
        p.X*polygon.Vertices[(i+1)%polygon.Vertices.Length].Y-
        p.Y*polygon.Vertices[(i+1)%polygon.Vertices.Length].X).Sum())/2;

    private static Polygon2Mm? Join(IReadOnlyList<Polygon2Mm> polygons)
    {
        var points=polygons.SelectMany(p=>p.Vertices).Distinct().ToArray();
        var edges=new HashSet<(Point2Mm A,Point2Mm B)>();
        foreach (var polygon in polygons)
        for (var i=0;i<polygon.Vertices.Length;i++)
        {
            var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Length];
            var dx=b.X-a.X;var dy=b.Y-a.Y;var length=dx*dx+dy*dy;
            var split=points.Where(p=>Math.Abs((p.X-a.X)*dy-(p.Y-a.Y)*dx)<Epsilon
                && Parameter(p)>=-Epsilon && Parameter(p)<=1+Epsilon).OrderBy(Parameter).ToArray();
            for (var j=1;j<split.Length;j++)
                if (!edges.Remove((split[j],split[j-1]))) edges.Add((split[j-1],split[j]));
            double Parameter(Point2Mm p)=>((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length;
        }
        if (edges.Count==0) return null;
        var start=edges.Select(e=>e.A).OrderBy(p=>p.X).ThenBy(p=>p.Y).First();
        var loop=new List<Point2Mm>();var current=start;
        while (edges.Count>0)
        {
            loop.Add(current);
            var next=edges.Where(e=>e.A==current).ToArray();
            if (next.Length!=1) return null;
            edges.Remove(next[0]);current=next[0].B;
            if (current==start) break;
        }
        if (edges.Count!=0 || current!=start) return null;
        for (var i=loop.Count-1;i>=0 && loop.Count>3;i--)
        {
            var a=loop[(i+loop.Count-1)%loop.Count];var b=loop[i];var c=loop[(i+1)%loop.Count];
            if (Math.Abs((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))<Epsilon) loop.RemoveAt(i);
        }
        return new Polygon2Mm(loop);
    }
}
