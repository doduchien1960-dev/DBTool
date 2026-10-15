using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Whole C-placement collision with extruded wall cutouts; never truncates the wrap.</summary>
public sealed record CWrapClash
{
    public CWrapClash(bool intersects) { Intersects=intersects; }
    public bool Intersects { get; }
}

public static class CWrapOpeningCheck
{
    public static OperationResult<CWrapClash> Intersects(BarOccurrence bar,ComponentGeometry wall)
    {
        OperationResult<CWrapClash> Fail(string actual)=>new(false,null,[new Diagnostic("C_TIE_OPENING_CHECK_UNSUPPORTED",
            DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,bar.Sources.FirstOrDefault(),
            "straight legs and edge-on C arcs against physical wall contours",actual,
            "No approximate or truncated C was retained.")]);
        foreach(var opening in wall.Voids.Where(o=>o.OwnerId==wall.Id))
        foreach(var segment in bar.Path.Segments)
        {
            if(segment is LineSegment3Mm line)
            {
                double length=BendLengthCalculator.Distance(line.Start,line.End);
                var temporary=new BarOccurrence(bar.Id+"/check",bar.Id,bar.ComponentId,bar.ComponentKind,bar.Layer,bar.Axis,
                    bar.DiameterMm,new BarPath([line],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
                    length,length,LengthStatus.Known,bar.RuleId,bar.Sources);
                // OpeningClipper adds the physical half diameter itself.
                var clipped=OpeningClipper.Clip(temporary,[opening],new ClearancePolicy(0),new GeometryModel([wall],[opening]));
                if(!clipped.Success) return new(false,null,clipped.Diagnostics);
                if(clipped.Value!.RemovedLengthMm>1e-7) return new(true,new(true),[]);
                continue;
            }
            if(segment is not ArcSegment3Mm arc || opening.WallFrame is not {} frame) return Fail(opening.Id);
            double Dot(Vector3 a,Vector3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            if(Math.Abs(Math.Abs(Dot(arc.Normal,frame.Left))-1)>1e-7) return Fail(opening.Id+":arc frame");
            var cross=new Vector3(arc.Normal.Y*arc.StartDirection.Z-arc.Normal.Z*arc.StartDirection.Y,
                arc.Normal.Z*arc.StartDirection.X-arc.Normal.X*arc.StartDirection.Z,
                arc.Normal.X*arc.StartDirection.Y-arc.Normal.Y*arc.StartDirection.X);
            var center=frame.ToLocal(arc.Center);
            double radius=arc.CenterlineRadiusMm,q=bar.DiameterMm/2d;
            double zc=radius*Dot(arc.StartDirection,frame.Up),zs=radius*Dot(cross,frame.Up);
            double vc=radius*Dot(arc.StartDirection,frame.Outward),vs=radius*Dot(cross,frame.Outward);
            foreach(var profile in Profiles(opening.ConcreteCutout))
            {
                var events=new SortedSet<double>{0,1};
                void Roots(double a,double b,double target)
                {
                    double amplitude=Math.Sqrt(a*a+b*b);
                    if(amplitude<1e-12 || Math.Abs(target)>amplitude+1e-9) return;
                    double phase=Math.Atan2(b,a),offset=Math.Acos(Math.Clamp(target/amplitude,-1,1));
                    foreach(double root in new[]{phase-offset,phase+offset})
                    for(int turn=-2;turn<=2;turn++)
                    {
                        double t=(root+turn*2*Math.PI)/arc.SweepRadians;
                        if(t>0 && t<1) events.Add(t);
                    }
                }
                void CircleEvents(Point2Mm point,double r)
                {
                    double du=center.U-point.X,remaining=r*r-du*du;
                    if(remaining<0) return;
                    double dz=Math.Sqrt(remaining);
                    Roots(zc,zs,point.Y-dz-center.Z);Roots(zc,zs,point.Y+dz-center.Z);
                }
                Point2Mm[] polygon=profile switch
                {
                    RectangleProfile r=>[new(r.Rectangle.MinX,r.Rectangle.MinY),new(r.Rectangle.MaxX,r.Rectangle.MinY),
                        new(r.Rectangle.MaxX,r.Rectangle.MaxY),new(r.Rectangle.MinX,r.Rectangle.MaxY)],
                    PolygonProfile p=>p.Polygon.Vertices.ToArray(),OpenTopProfile p=>p.Polygon.Vertices.ToArray(),_=>[]
                };
                if(profile is CircleProfile circle) CircleEvents(circle.Center,circle.RadiusMm+q);
                else if(polygon.Length>0)
                {
                    for(int i=0;i<polygon.Length;i++)
                    {
                        var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
                        double dx=b.X-a.X,dz=b.Y-a.Y,length=Math.Sqrt(dx*dx+dz*dz);
                        double constant=(dx*(center.Z-a.Y)-dz*(center.U-a.X))/length;
                        Roots(dx*zc/length,dx*zs/length,q-constant);
                        Roots(dx*zc/length,dx*zs/length,-q-constant);
                        CircleEvents(a,q);
                    }
                }
                else return Fail(opening.Id+":"+profile.GetType().Name);
                Roots(vc,vs,opening.ExtrusionRange.Start-q-center.V);
                Roots(vc,vs,opening.ExtrusionRange.End+q-center.V);
                bool Hit(double t)
                {
                    double angle=t*arc.SweepRadians,z=center.Z+zc*Math.Cos(angle)+zs*Math.Sin(angle);
                    double v=center.V+vc*Math.Cos(angle)+vs*Math.Sin(angle);
                    if(v<opening.ExtrusionRange.Start-q-1e-8 || v>opening.ExtrusionRange.End+q+1e-8) return false;
                    if(profile is CircleProfile c) return Math.Pow(center.U-c.Center.X,2)+Math.Pow(z-c.Center.Y,2)<=Math.Pow(c.RadiusMm+q,2)+1e-8;
                    bool inside=false;
                    for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                    {
                        var a=polygon[j];var b=polygon[i];
                        if((a.Y>z)!=(b.Y>z) && center.U<(b.X-a.X)*(z-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
                        double dx=b.X-a.X,dy=b.Y-a.Y;
                        double s=Math.Clamp(((center.U-a.X)*dx+(z-a.Y)*dy)/(dx*dx+dy*dy),0,1);
                        if(Math.Pow(center.U-a.X-s*dx,2)+Math.Pow(z-a.Y-s*dy,2)<=q*q+1e-8) return true;
                    }
                    return inside;
                }
                var ordered=events.ToArray();
                if(ordered.Any(Hit) || Enumerable.Range(1,ordered.Length-1).Any(i=>Hit((ordered[i-1]+ordered[i])/2)))
                    return new(true,new(true),[]);
            }
        }
        return new(true,new(false),[]);
    }

    private static IEnumerable<Profile2Mm> Profiles(Profile2Mm profile)=>profile is MultiCellProfile cells
        ?cells.Cells.SelectMany(Profiles):[profile];
}

