using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;

public sealed record Polygon2Mm
{
    public Polygon2Mm(IEnumerable<Point2Mm> vertices)
    {
        Vertices = ContractGuard.Copy(vertices);
        if (Vertices.Length < 3 || Vertices.Distinct().Count() != Vertices.Length) throw new ArgumentException("Polygon needs distinct vertices.");
        for (int i = 0; i < Vertices.Length; i++)
            for (int j = i+1; j < Vertices.Length; j++)
            {
                if (j == i+1 || i == 0 && j == Vertices.Length-1) continue;
                if (Intersects(Vertices[i],Vertices[(i+1)%Vertices.Length],Vertices[j],Vertices[(j+1)%Vertices.Length]))
                    throw new ArgumentException("Self intersecting polygon.");
            }
        SignedArea = Enumerable.Range(0,Vertices.Length).Sum(i => Vertices[i].X*Vertices[(i+1)%Vertices.Length].Y-Vertices[(i+1)%Vertices.Length].X*Vertices[i].Y)/2;
        if (!double.IsFinite(SignedArea) || SignedArea == 0) throw new ArgumentException("Degenerate polygon.");
    }
    public ImmutableArray<Point2Mm> Vertices { get; }
    public bool Closed => true;
    internal double SignedArea { get; }
    internal static double Cross(Point2Mm a, Point2Mm b, Point2Mm c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    internal static bool Intersects(Point2Mm a, Point2Mm b, Point2Mm c, Point2Mm d)
    {
        if (Math.Max(a.X,b.X)<Math.Min(c.X,d.X) || Math.Max(c.X,d.X)<Math.Min(a.X,b.X) ||
            Math.Max(a.Y,b.Y)<Math.Min(c.Y,d.Y) || Math.Max(c.Y,d.Y)<Math.Min(a.Y,b.Y)) return false;
        return Cross(a,b,c)*Cross(a,b,d)<=0 && Cross(c,d,a)*Cross(c,d,b)<=0;
    }
    internal bool Contains(Point2Mm p)
    {
        bool inside=false;
        for (int i=0,j=Vertices.Length-1;i<Vertices.Length;j=i++)
        {
            var a=Vertices[i]; var b=Vertices[j];
            if ((a.Y>p.Y)!=(b.Y>p.Y) && p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
        }
        return inside;
    }
    internal bool Crosses(Polygon2Mm other) =>
        Enumerable.Range(0,Vertices.Length).Any(i => Enumerable.Range(0,other.Vertices.Length).Any(j =>
            Intersects(Vertices[i],Vertices[(i+1)%Vertices.Length],other.Vertices[j],other.Vertices[(j+1)%other.Vertices.Length])));
}
public sealed record Footprint
{
    public Footprint(Polygon2Mm outer, IEnumerable<Polygon2Mm> innerLoops)
    {
        Outer = ContractGuard.Required(outer);
        InnerLoops = ContractGuard.Copy(innerLoops);
        if (Outer.SignedArea <= 0 || InnerLoops.Any(h=>h.SignedArea >= 0 || Outer.Crosses(h) || !Outer.Contains(h.Vertices[0]))) throw new ArgumentException("Invalid footprint winding or hole containment.");
        for(int i=0;i<InnerLoops.Length;i++) for(int j=i+1;j<InnerLoops.Length;j++)
            if(InnerLoops[i].Crosses(InnerLoops[j]) || InnerLoops[i].Contains(InnerLoops[j].Vertices[0]) || InnerLoops[j].Contains(InnerLoops[i].Vertices[0])) throw new ArgumentException("Overlapping holes.");
    }
    public Polygon2Mm Outer { get; }
    public ImmutableArray<Polygon2Mm> InnerLoops { get; }
}

