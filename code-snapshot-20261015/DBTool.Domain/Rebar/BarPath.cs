using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;

public abstract record PathSegment
{
    private protected PathSegment() {}
    public abstract Point3Mm Start { get; }
    public abstract Point3Mm End { get; }
}
public sealed record LineSegment3Mm : PathSegment
{
    public LineSegment3Mm(Point3Mm start, Point3Mm end)
    {
        if(start==end) throw new ArgumentException("Zero length segment.");
        Start=start; End=end;
    }
    public override Point3Mm Start { get; }
    public override Point3Mm End { get; }
}
public sealed record ArcSegment3Mm : PathSegment
{
    public static ArcSegment3Mm FromInnerRadius(Point3Mm center, double innerRadiusMm, double diameterMm,
        Vector3 normal, Vector3 startDirection, double sweepRadians)
    {
        ContractGuard.NonNegative(innerRadiusMm, nameof(innerRadiusMm));
        ContractGuard.Positive(diameterMm, nameof(diameterMm));
        return new ArcSegment3Mm(center, innerRadiusMm + diameterMm / 2, normal, startDirection, sweepRadians);
    }

    public ArcSegment3Mm(Point3Mm center,double centerlineRadiusMm,Vector3 normal,Vector3 startDirection,double sweepRadians)
    {
        Center=center; CenterlineRadiusMm=ContractGuard.Positive(centerlineRadiusMm,nameof(centerlineRadiusMm));
        Normal=ContractGuard.Required(normal); StartDirection=ContractGuard.Required(startDirection);
        SweepRadians=ContractGuard.Finite(sweepRadians,nameof(sweepRadians));
        if(sweepRadians==0 || Math.Abs(sweepRadians)>2*Math.PI) throw new ArgumentException("Arc sweep must be nonzero and at most one turn.");
        if(Math.Abs(FrameInvariant.Dot(normal,normal)-1)>1e-9 || Math.Abs(FrameInvariant.Dot(startDirection,startDirection)-1)>1e-9 || Math.Abs(FrameInvariant.Dot(normal,startDirection))>1e-9) throw new ArgumentException("Invalid arc basis.");
        Start = new(center.X+centerlineRadiusMm*startDirection.X,center.Y+centerlineRadiusMm*startDirection.Y,center.Z+centerlineRadiusMm*startDirection.Z);
        var cross=new Vector3(normal.Y*startDirection.Z-normal.Z*startDirection.Y,normal.Z*startDirection.X-normal.X*startDirection.Z,normal.X*startDirection.Y-normal.Y*startDirection.X);
        double c=Math.Cos(sweepRadians),s=Math.Sin(sweepRadians);
        End=new(center.X+centerlineRadiusMm*(c*startDirection.X+s*cross.X),center.Y+centerlineRadiusMm*(c*startDirection.Y+s*cross.Y),center.Z+centerlineRadiusMm*(c*startDirection.Z+s*cross.Z));
    }
    public Point3Mm Center { get; }
    public double CenterlineRadiusMm { get; }
    public Vector3 Normal { get; }
    public Vector3 StartDirection { get; }
    public double SweepRadians { get; }
    public override Point3Mm Start { get; }
    public override Point3Mm End { get; }
}
public sealed record BarPath
{
    public BarPath(IEnumerable<PathSegment> segments, bool closed, HookPattern hookPattern, double additionalLapMm, bool lapAlreadyInPath, MeasurementBasis measurementBasis)
    {
        Segments = ContractGuard.Copy(segments);
        Closed = closed;
        HookPattern = ContractGuard.EnumValue(hookPattern);
        AdditionalLapMm = ContractGuard.NonNegative(additionalLapMm, nameof(additionalLapMm));
        LapAlreadyInPath = lapAlreadyInPath;
        MeasurementBasis = ContractGuard.EnumValue(measurementBasis);
        if(Segments.IsEmpty) throw new ArgumentException("Empty bar path.");
        if(measurementBasis is not (MeasurementBasis.TangentPath or MeasurementBasis.NominalPolyline)) throw new ArgumentException("BarPath stores a real tangent centerline, not theoretical corners.");
        bool Near(Point3Mm a,Point3Mm b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2))<=1e-6;
        for(int i=1;i<Segments.Length;i++) if(!Near(Segments[i-1].End,Segments[i].Start)) throw new ArgumentException("Disconnected bar path.");
        if(closed && !Near(Segments[^1].End,Segments[0].Start)) throw new ArgumentException("Path is not closed.");
    }
    public ImmutableArray<PathSegment> Segments { get; }
    public bool Closed { get; }
    public HookPattern HookPattern { get; }
    public double AdditionalLapMm { get; }
    public bool LapAlreadyInPath { get; }
    public MeasurementBasis MeasurementBasis { get; }
}
