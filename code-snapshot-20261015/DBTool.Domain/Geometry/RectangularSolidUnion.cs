using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record SolidUnionVolume
{
    public SolidUnionVolume(double value) { Value = ContractGuard.NonNegative(value, nameof(value)); }
    public double Value { get; }
}

/// <summary>Exact union volume for finite axis-aligned rectangular extrusions.</summary>
public static class RectangularSolidUnion
{
    public static OperationResult<SolidUnionVolume> Measure(UnionSolid union)
    {
        ArgumentNullException.ThrowIfNull(union);
        OperationResult<SolidUnionVolume> Fail(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                null, "axis-aligned rectangular extrusions", actual, "Union volume is unavailable.")]);
        var solids = new List<ExtrusionSolid>();
        bool Flatten(SolidDescriptor solid)
        {
            if (solid is ExtrusionSolid extrusion) { solids.Add(extrusion); return true; }
            return solid is UnionSolid nested && nested.Solids.All(Flatten);
        }
        if (!Flatten(union)) return Fail("SOLID_UNION_UNSUPPORTED", "nonrectangular solid");
        var parts = new List<(Rectangle2Mm Outer, Rectangle2Mm[] Holes, Interval1D Z)>();
        foreach (var solid in solids)
        {
            if (solid.Frame.Origin != new Point3Mm(0,0,0) ||
                solid.Frame.U != new Vector3(1,0,0) || solid.Frame.V != new Vector3(0,1,0) ||
                solid.Frame.N != new Vector3(0,0,1) || !Rect(solid.Footprint.Outer,out var outer))
                return Fail("SOLID_UNION_UNSUPPORTED", "non-axis rectangular extrusion");
            var holes=new List<Rectangle2Mm>();
            foreach(var hole in solid.Footprint.InnerLoops)
            { if(!Rect(hole,out var rectangle)) return Fail("SOLID_UNION_UNSUPPORTED","nonrectangular hole");
              holes.Add(rectangle); }
            parts.Add((outer,holes.ToArray(),solid.Depth));
        }
        double[] xs=parts.SelectMany(p=>new[]{p.Outer.MinX,p.Outer.MaxX})
            .Concat(parts.SelectMany(p=>p.Holes.SelectMany(h=>new[]{h.MinX,h.MaxX}))).Distinct().Order().ToArray();
        double[] ys=parts.SelectMany(p=>new[]{p.Outer.MinY,p.Outer.MaxY})
            .Concat(parts.SelectMany(p=>p.Holes.SelectMany(h=>new[]{h.MinY,h.MaxY}))).Distinct().Order().ToArray();
        double[] zs=parts.SelectMany(p=>new[]{p.Z.Start,p.Z.End}).Distinct().Order().ToArray();
        double volume=0;
        for(int i=0;i<xs.Length-1;i++)
        for(int j=0;j<ys.Length-1;j++)
        for(int k=0;k<zs.Length-1;k++)
        {
            double x=(xs[i]+xs[i+1])/2,y=(ys[j]+ys[j+1])/2,z=(zs[k]+zs[k+1])/2;
            if(!parts.Any(p=>z>p.Z.Start && z<p.Z.End && Inside(p.Outer,x,y) &&
                !p.Holes.Any(h=>Inside(h,x,y)))) continue;
            double cell=(xs[i+1]-xs[i])*(ys[j+1]-ys[j])*(zs[k+1]-zs[k]);
            if(!double.IsFinite(cell) || !double.IsFinite(volume+cell))
                return Fail("SOLID_UNION_OVERFLOW","nonfinite volume");
            volume+=cell;
        }
        return new(true,new SolidUnionVolume(volume),[]);
    }

    private static bool Inside(Rectangle2Mm r,double x,double y) =>
        x>r.MinX && x<r.MaxX && y>r.MinY && y<r.MaxY;
    private static bool Rect(Polygon2Mm polygon,out Rectangle2Mm rectangle)
    {
        var points=polygon.Vertices;
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X),y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        if(points.Length!=4 || points.Any(p=>(p.X!=x0 && p.X!=x1)||(p.Y!=y0 && p.Y!=y1)))
        { rectangle=null!;return false; }
        rectangle=new Rectangle2Mm(x0,y0,x1,y1);return true;
    }
}
