using DBTool.Core;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>One round opening against one straight centerline; no CAD or multi-opening boolean.</summary>
public static class OpeningClipper
{
    public static OperationResult<ClipResult> Clip(BarOccurrence bar,
        IReadOnlyList<OpeningProfile> openings, ClearancePolicy policy, GeometryModel model)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(openings);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(model);
        OperationResult<ClipResult> Block(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                bar.Sources.FirstOrDefault(), "supported physical path/openings", actual,
                "Multi-opening clip failed without changing the bar.")]);
        if (!model.Components.Any(c => c.Id == bar.ComponentId))
            return Block("CLIP_OWNER_MISSING", bar.ComponentId);
        if (!bar.Path.Closed && bar.Path.Segments.Length > 1)
            return ClipMultiPath(bar,openings,policy,model);
        if (bar.Path.Segments.Length == 1 && bar.Path.Segments[0] is ArcSegment3Mm arc)
            return ClipArc(bar, arc, openings, policy);
        if (bar.Path.Closed || bar.Path.Segments.Length != 1 ||
            bar.Path.Segments[0] is not LineSegment3Mm line ||
            bar.Path.HookPattern != HookPattern.Hook0M || bar.Path.AdditionalLapMm != 0 ||
            bar.LengthStatus != LengthStatus.Known)
            return Block("CLIP_PATH_UNSUPPORTED", "only one physical unhooked line is implemented");
        double dx = line.End.X - line.Start.X, dy = line.End.Y - line.Start.Y, dz = line.End.Z - line.Start.Z;
        double original = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (!double.IsFinite(original) || Math.Abs(original - bar.ScheduleLengthMm) > policy.ToleranceMm ||
            !bar.FabricationLengthMm.HasValue || Math.Abs(original - bar.FabricationLengthMm.Value) > policy.ToleranceMm)
            return Block("CLIP_LENGTH_INVALID", "path length mismatch");
        var forbidden = new List<(double Start, double End)>();
        foreach (var opening in openings.Where(o => o.OwnerId == bar.ComponentId)
                     .DistinctBy(o => o.Id).OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            if (opening.WallFrame is null)
                return Block("CLIP_PLANE_UNSUPPORTED", opening.Id);
            var frame = opening.WallFrame;
            var a = frame.ToLocal(line.Start);
            var b = frame.ToLocal(line.End);
            double q = policy.CoverToHoleMm + bar.DiameterMm / 2d;
            if (Math.Abs(a.V-b.V)>policy.ToleranceMm)
            {
                if(opening.ConcreteCutout is not CircleProfile depthCircle ||
                    Math.Abs(a.U-b.U)>policy.ToleranceMm ||
                    Math.Abs(a.Z-b.Z)>policy.ToleranceMm)
                {
                    if(!IsConvexFace(opening.ConcreteCutout))
                        return Block("CLIP_PLANE_UNSUPPORTED",opening.Id+" intersects a nonconvex depth-varying opening");
                    var coupled=ConvexVolumeInterval(a,b,opening.ConcreteCutout,
                        opening.ExtrusionRange,q,original,policy.ToleranceMm);
                    if(coupled is { } unresolved &&
                        (!double.IsFinite(unresolved.Start) ||
                         !double.IsFinite(unresolved.End)))
                        return Block("CLIP_ROOT_UNRESOLVED",opening.Id);
                    if(coupled is { } interval &&
                        (interval.End-interval.Start)*original>policy.ToleranceMm)
                        forbidden.Add(interval);
                    continue;
                }
                double radial=Math.Sqrt(Math.Pow(a.U-depthCircle.Center.X,2)+
                    Math.Pow(a.Z-depthCircle.Center.Y,2));
                double faceDistance=Math.Max(0,radial-depthCircle.RadiusMm);
                if(faceDistance>=q && faceDistance>0) continue;
                double capMargin=Math.Sqrt(Math.Max(0,(q-faceDistance)*(q+faceDistance)));
                double t0=(opening.ExtrusionRange.Start-capMargin-a.V)/(b.V-a.V);
                double t1=(opening.ExtrusionRange.End+capMargin-a.V)/(b.V-a.V);
                if(t0>t1)(t0,t1)=(t1,t0);
                t0=Math.Max(0,t0);t1=Math.Min(1,t1);
                if((t1-t0)*original>policy.ToleranceMm) forbidden.Add((t0,t1));
                continue;
            }
            double depthDistance=a.V<opening.ExtrusionRange.Start
                ? opening.ExtrusionRange.Start-a.V
                : a.V>opening.ExtrusionRange.End
                    ? a.V-opening.ExtrusionRange.End : 0;
            if(depthDistance>=q && depthDistance>0) continue;
            double effectiveQ=Math.Sqrt(Math.Max(0,(q-depthDistance)*(q+depthDistance)));
            double du = b.U - a.U, dv = b.Z - a.Z;
            if (opening.ConcreteCutout is CircleProfile circle)
            {
                double r = circle.RadiusMm + effectiveQ;
                double ox = a.U - circle.Center.X, oy = a.Z - circle.Center.Y;
                double aa = du * du + dv * dv, bb = 2 * (ox * du + oy * dv), cc = ox * ox + oy * oy - r * r;
                double discriminant = bb * bb - 4 * aa * cc;
                if (discriminant <= 0) continue;
                double root = Math.Sqrt(discriminant);
                double t0 = Math.Max(0, (-bb - root) / (2 * aa));
                double t1 = Math.Min(1, (-bb + root) / (2 * aa));
                if (t1 - t0 > policy.ToleranceMm / original) forbidden.Add((t0, t1));
            }
            else if (opening.ConcreteCutout is RectangleProfile rectangle)
                forbidden.AddRange(PolygonIntervals(RectanglePolygon(rectangle.Rectangle), a.U, a.Z, du, dv, effectiveQ));
            else if (opening.ConcreteCutout is PolygonProfile polygon)
                forbidden.AddRange(PolygonIntervals(polygon.Polygon, a.U, a.Z, du, dv, effectiveQ));
            else if (opening.ConcreteCutout is OpenTopProfile openTop)
                forbidden.AddRange(PolygonIntervals(openTop.Polygon, a.U, a.Z, du, dv, effectiveQ));
            else return Block("CLIP_OPENING_UNSUPPORTED", opening.Id);
        }
        if (forbidden.Count == 0) return new(true, new ClipResult([bar], 0, original), []);
        var merged = new List<(double Start, double End)>();
        foreach (var interval in forbidden.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End + policy.ToleranceMm / original)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, interval.End));
            else merged.Add(interval);
        }
        var kept = new List<(double Start, double End)>();
        double cursor = 0;
        foreach (var interval in merged)
        {
            if (interval.Start - cursor > policy.ToleranceMm / original) kept.Add((cursor, interval.Start));
            cursor = Math.Max(cursor, interval.End);
        }
        if (1 - cursor > policy.ToleranceMm / original) kept.Add((cursor, 1));
        var retained = new List<BarOccurrence>();
        var sources = bar.Sources.Concat(openings.SelectMany(o => o.Sources)).Distinct().ToArray();
        foreach (var interval in kept)
        {
            Point3Mm At(double t) => new(line.Start.X + dx * t, line.Start.Y + dy * t, line.Start.Z + dz * t);
            double length = original * (interval.End - interval.Start);
            var path = new BarPath([new LineSegment3Mm(At(interval.Start), At(interval.End))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            var cutOpeningIds = openings.Where(o => o.OwnerId == bar.ComponentId).Select(o => o.Id).Distinct();
            retained.Add(new BarOccurrence($"{bar.Id}/clip/{retained.Count}", bar.Id, bar.ComponentId,
                bar.ComponentKind, bar.Layer, bar.Axis, bar.DiameterMm, path, length, length,
                LengthStatus.Known, bar.RuleId, sources, bar.Direction, bar.FrameId, bar.Surface,
                bar.FamilyId, bar.MeasurementBasis, bar.InnerRadiusMm,
                bar.StationIndex, bar.StationMm, bar.AssemblyId, bar.PanelInstanceId,
                bar.RegionRole ?? (retained.Count == 0 ? "Left" : "Right"), cutOpeningIds, length,
                bar.ShapeRecipeId, bar.SpecId, bar.StationKey, bar.FabricationBasis));
        }
        double removed = original - retained.Sum(c => c.ScheduleLengthMm);
        return new(true, new ClipResult(retained, removed, original), []);

        static IEnumerable<(double Start,double End)> PolygonIntervals(Polygon2Mm polygon,
            double u0,double z0,double du,double dz,double q)
        {
            var points=polygon.Vertices;
            var events=new List<double>{0,1};
            double pathSquare=du*du+dz*dz;
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i]; var n=points[(i+1)%points.Length];
                double ex=n.X-p.X,ez=n.Y-p.Y;
                double edgeLength=Math.Sqrt(ex*ex+ez*ez);
                double signedStart=(ex*(z0-p.Y)-ez*(u0-p.X))/edgeLength;
                double signedDelta=(ex*dz-ez*du)/edgeLength;
                if(signedDelta!=0)
                    foreach(double target in new[]{-q,q})
                    { double t=(target-signedStart)/signedDelta; if(t>0 && t<1) events.Add(t); }
                double ox=u0-p.X,oy=z0-p.Y;
                double b=2*(ox*du+oy*dz),c=ox*ox+oy*oy-q*q;
                double disc=b*b-4*pathSquare*c;
                if(disc<0) continue;
                double root=Math.Sqrt(disc);
                foreach(double t in new[]{(-b-root)/(2*pathSquare),(-b+root)/(2*pathSquare)})
                    if(t>0 && t<1) events.Add(t);
            }
            var ordered=events.Distinct().Order().ToArray();
            for(int i=0;i<ordered.Length-1;i++)
            {
                double t0=ordered[i],t1=ordered[i+1],t=(t0+t1)/2;
                double x=u0+du*t,y=z0+dz*t;
                bool inside=false;
                for(int j=0,k=points.Length-1;j<points.Length;k=j++)
                {
                    var a=points[j];var b=points[k];
                    if((a.Y>y)!=(b.Y>y) && x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X)
                        inside=!inside;
                }
                double best=double.PositiveInfinity;
                for(int j=0;j<points.Length;j++)
                {
                    var a=points[j];var b=points[(j+1)%points.Length];
                    double vx=b.X-a.X,vy=b.Y-a.Y;
                    double s=Math.Clamp(((x-a.X)*vx+(y-a.Y)*vy)/(vx*vx+vy*vy),0,1);
                    double ex=x-(a.X+s*vx),ey=y-(a.Y+s*vy);
                    best=Math.Min(best,ex*ex+ey*ey);
                }
                if(inside || best<q*q) yield return(t0,t1);
            }
        }
    }

    private static (double Start,double End)? ConvexVolumeInterval(LocalWallPointMm a,
        LocalWallPointMm b,Profile2Mm faceProfile,DBTool.Domain.Geometry.Interval1D depth,
        double clearanceMm,double pathLengthMm,double toleranceMm)
    {
        double FaceDistance(double u,double z) => faceProfile switch
        {
            CircleProfile circle=>Math.Max(0,Math.Sqrt(
                Math.Pow(u-circle.Center.X,2)+Math.Pow(z-circle.Center.Y,2))-
                circle.RadiusMm),
            RectangleProfile box=>Math.Sqrt(Math.Pow(Math.Max(0,
                Math.Max(box.Rectangle.MinX-u,u-box.Rectangle.MaxX)),2)+
                Math.Pow(Math.Max(0,Math.Max(box.Rectangle.MinY-z,z-box.Rectangle.MaxY)),2)),
            PolygonProfile polygon=>PolygonDistance(polygon.Polygon,u,z),
            OpenTopProfile openTop=>PolygonDistance(openTop.Polygon,u,z),
            _=>double.NaN
        };
        double Distance(double t)
        {
            double u=a.U+(b.U-a.U)*t,z=a.Z+(b.Z-a.Z)*t,
                v=a.V+(b.V-a.V)*t;
            double face=FaceDistance(u,z);
            double cap=v<depth.Start?depth.Start-v:v>depth.End?v-depth.End:0;
            return Math.Sqrt(face*face+cap*cap);
        }
        // Distance to a closed convex extrusion is convex along a line. The inside
        // parameters therefore form one interval, whose two endpoints are bracketed.
        double lo=0,hi=1;
        for(int i=0;i<120;i++)
        {
            double left=lo+(hi-lo)/3,right=hi-(hi-lo)/3;
            if(Distance(left)<Distance(right)) hi=right;
            else lo=left;
        }
        double minimum=(lo+hi)/2;
        if((hi-lo)*pathLengthMm>toleranceMm/4 ||
            !double.IsFinite(Distance(minimum))) return (double.NaN,double.NaN);
        if(Distance(minimum)>clearanceMm) return null;
        double start=0,end=1;
        if(Distance(0)>clearanceMm)
        {
            lo=0;hi=minimum;
            for(int i=0;i<80;i++)
            {
                double mid=(lo+hi)/2;
                if(Distance(mid)>clearanceMm) lo=mid;
                else hi=mid;
            }
            if((hi-lo)*pathLengthMm>toleranceMm/4)
                return (double.NaN,double.NaN);
            start=(lo+hi)/2;
        }
        if(Distance(1)>clearanceMm)
        {
            lo=minimum;hi=1;
            for(int i=0;i<80;i++)
            {
                double mid=(lo+hi)/2;
                if(Distance(mid)>clearanceMm) hi=mid;
                else lo=mid;
            }
            if((hi-lo)*pathLengthMm>toleranceMm/4)
                return (double.NaN,double.NaN);
            end=(lo+hi)/2;
        }
        return end>start?(start,end):null;
    }

    private static bool IsConvexFace(Profile2Mm profile)
    {
        if(profile is CircleProfile or RectangleProfile) return true;
        var polygon=profile switch
        {
            PolygonProfile p=>p.Polygon,
            OpenTopProfile p=>p.Polygon,
            _=>null
        };
        if(polygon is null) return false;
        int sign=0;
        var points=polygon.Vertices;
        for(int i=0;i<points.Length;i++)
        {
            double cross=Polygon2Mm.Cross(points[i],points[(i+1)%points.Length],
                points[(i+2)%points.Length]);
            if(Math.Abs(cross)<=1e-9) continue;
            int next=Math.Sign(cross);
            if(sign!=0 && sign!=next) return false;
            sign=next;
        }
        return sign!=0;
    }

    private static double PolygonDistance(Polygon2Mm polygon,double u,double z)
    {
        if(polygon.Contains(new Point2Mm(u,z))) return 0;
        double best=double.PositiveInfinity;
        var points=polygon.Vertices;
        for(int i=0;i<points.Length;i++)
        {
            var p=points[i];var n=points[(i+1)%points.Length];
            double dx=n.X-p.X,dz=n.Y-p.Y;
            double t=Math.Clamp(((u-p.X)*dx+(z-p.Y)*dz)/(dx*dx+dz*dz),0,1);
            best=Math.Min(best,Math.Sqrt(Math.Pow(u-p.X-t*dx,2)+
                Math.Pow(z-p.Y-t*dz,2)));
        }
        return best;
    }

    private static OperationResult<ClipResult> ClipMultiPath(BarOccurrence bar,
        IReadOnlyList<OpeningProfile> openings,ClearancePolicy policy,GeometryModel model)
    {
        OperationResult<ClipResult> Block(string code,string actual) => new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,
                bar.Sources.FirstOrDefault(),"known physical multi-segment centerline",actual,
                "Multi-segment clip failed without changing the bar.")]);
        if(bar.Path.AdditionalLapMm!=0 ||
            bar.LengthStatus!=LengthStatus.Known || !bar.FabricationLengthMm.HasValue)
            return Block("CLIP_PATH_UNSUPPORTED","hooked, lapped or unknown-length path");
        double SegmentLength(PathSegment segment) => segment switch
        {
            LineSegment3Mm line=>Math.Sqrt(Math.Pow(line.End.X-line.Start.X,2)+
                Math.Pow(line.End.Y-line.Start.Y,2)+Math.Pow(line.End.Z-line.Start.Z,2)),
            ArcSegment3Mm arc=>arc.CenterlineRadiusMm*Math.Abs(arc.SweepRadians),
            _=>double.NaN
        };
        double original=bar.Path.Segments.Sum(SegmentLength);
        if(!double.IsFinite(original) || Math.Abs(original-bar.ScheduleLengthMm)>policy.ToleranceMm ||
            Math.Abs(original-bar.FabricationLengthMm.Value)>policy.ToleranceMm)
            return Block("CLIP_LENGTH_INVALID","physical and scheduled lengths differ");
        var pieces=new List<(double Start,double End,List<PathSegment> Segments)>();
        double station=0;
        foreach(var segment in bar.Path.Segments)
        {
            double length=SegmentLength(segment);
            var path=new BarPath([segment],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            var temporary=new BarOccurrence($"{bar.Id}/segment/{pieces.Count}/{station:R}",bar.Id,
                bar.ComponentId,bar.ComponentKind,bar.Layer,bar.Axis,bar.DiameterMm,path,
                length,length,LengthStatus.Known,bar.RuleId,bar.Sources,bar.Direction,bar.FrameId,
                bar.Surface,bar.FamilyId,bar.MeasurementBasis,bar.InnerRadiusMm);
            var clipped=Clip(temporary,openings,policy,model);
            if(!clipped.Success) return new(false,null,clipped.Diagnostics);
            foreach(var retained in clipped.Value!.Retained)
            {
                var child=retained.Path.Segments[0];
                double offset=Offset(segment,child.Start,length);
                if(!double.IsFinite(offset) || offset< -policy.ToleranceMm ||
                    offset+retained.ScheduleLengthMm>length+policy.ToleranceMm)
                    return Block("CLIP_LENGTH_INVALID","child outside original segment");
                pieces.Add((station+offset,station+offset+retained.ScheduleLengthMm,[child]));
            }
            station+=length;
        }
        var joined=new List<(double Start,double End,List<PathSegment> Segments)>();
        foreach(var piece in pieces.OrderBy(p=>p.Start))
        {
            if(joined.Count>0 &&
                Math.Abs(piece.Start-joined[^1].End)<=policy.ToleranceMm &&
                Distance(joined[^1].Segments[^1].End,piece.Segments[0].Start)<=policy.ToleranceMm)
            {
                var last=joined[^1];last.Segments.AddRange(piece.Segments);
                joined[^1]=(last.Start,piece.End,last.Segments);
            }
            else joined.Add(piece);
        }
        if(joined.Count==1 && Math.Abs(joined[0].Start)<=policy.ToleranceMm &&
            Math.Abs(joined[0].End-original)<=policy.ToleranceMm &&
            joined[0].Segments.Count==bar.Path.Segments.Length)
            return new(true,new ClipResult([bar],0,original),[]);
        var sources=bar.Sources.Concat(openings.SelectMany(o=>o.Sources)).Distinct().ToArray();
        var children=new List<BarOccurrence>();
        foreach(var part in joined)
        {
            double length=part.End-part.Start;
            if(length<=policy.ToleranceMm) continue;
            bool retainsStart=Math.Abs(part.Start)<=policy.ToleranceMm &&
                Distance(part.Segments[0].Start,bar.Path.Segments[0].Start)<=policy.ToleranceMm;
            bool retainsEnd=Math.Abs(part.End-original)<=policy.ToleranceMm &&
                Distance(part.Segments[^1].End,bar.Path.Segments[^1].End)<=policy.ToleranceMm;
            bool startHook=retainsStart && bar.Path.HookPattern is
                (HookPattern.Hook2M or HookPattern.HookStartOnly);
            bool endHook=retainsEnd && bar.Path.HookPattern is
                (HookPattern.Hook2M or HookPattern.Hook1M);
            if(startHook && (part.Segments.Count<2 ||
                part.Segments[0]!=bar.Path.Segments[0] ||
                part.Segments[1]!=bar.Path.Segments[1]) ||
                endHook && (part.Segments.Count<2 ||
                part.Segments[^2]!=bar.Path.Segments[^2] ||
                part.Segments[^1]!=bar.Path.Segments[^1]))
                return Block("CLIP_HOOK_PARTIAL_UNSUPPORTED","an original hook bend was truncated");
            var hook=startHook ? (endHook?HookPattern.Hook2M:HookPattern.HookStartOnly)
                : endHook?HookPattern.Hook1M:HookPattern.Hook0M;
            var path=new BarPath(part.Segments,false,hook,0,false,MeasurementBasis.TangentPath);
            var cutOpeningIds = openings.Where(o => o.OwnerId == bar.ComponentId).Select(o => o.Id).Distinct();
            children.Add(new BarOccurrence($"{bar.Id}/clip/{children.Count}",bar.Id,
                bar.ComponentId,bar.ComponentKind,bar.Layer,bar.Axis,bar.DiameterMm,path,
                length,length,LengthStatus.Known,bar.RuleId,sources,bar.Direction,bar.FrameId,
                bar.Surface,bar.FamilyId,bar.MeasurementBasis,bar.InnerRadiusMm,
                bar.StationIndex, bar.StationMm, bar.AssemblyId, bar.PanelInstanceId,
                bar.RegionRole ?? (children.Count == 0 ? "Left" : "Right"), cutOpeningIds, length,
                bar.ShapeRecipeId, bar.SpecId, bar.StationKey, bar.FabricationBasis));
        }
        double removed=original-children.Sum(c=>c.ScheduleLengthMm);
        if(removed<0 && Math.Abs(removed)<=policy.ToleranceMm) removed=0;
        if(!double.IsFinite(removed) || removed<0)
            return Block("CLIP_LENGTH_INVALID","length conservation failed");
        return new(true,new ClipResult(children,removed,original),[]);

        static double Distance(Point3Mm a,Point3Mm b) =>
            Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2));
        static double Offset(PathSegment segment,Point3Mm point,double length)
        {
            if(segment is LineSegment3Mm line) return Distance(line.Start,point);
            if(segment is not ArcSegment3Mm arc) return double.NaN;
            var start=arc.StartDirection;
            var cross=new Vector3(arc.Normal.Y*start.Z-arc.Normal.Z*start.Y,
                arc.Normal.Z*start.X-arc.Normal.X*start.Z,
                arc.Normal.X*start.Y-arc.Normal.Y*start.X);
            var radial=new Vector3((point.X-arc.Center.X)/arc.CenterlineRadiusMm,
                (point.Y-arc.Center.Y)/arc.CenterlineRadiusMm,
                (point.Z-arc.Center.Z)/arc.CenterlineRadiusMm);
            double cosine=radial.X*start.X+radial.Y*start.Y+radial.Z*start.Z;
            double sine=radial.X*cross.X+radial.Y*cross.Y+radial.Z*cross.Z;
            double angle=Math.Atan2(sine,cosine);
            if(arc.SweepRadians>0 && angle<0) angle+=2*Math.PI;
            if(arc.SweepRadians<0 && angle>0) angle-=2*Math.PI;
            return length*angle/arc.SweepRadians;
        }
    }

    private static OperationResult<ClipResult> ClipArc(BarOccurrence bar, ArcSegment3Mm arc,
        IReadOnlyList<OpeningProfile> openings, ClearancePolicy policy)
    {
        OperationResult<ClipResult> Block(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                bar.Sources.FirstOrDefault(), "physical planar arc and supported opening", actual,
                "Analytic arc clip failed.")]);
        if (bar.Path.Closed || bar.Path.Segments.Length != 1 ||
            bar.Path.HookPattern != HookPattern.Hook0M || bar.Path.AdditionalLapMm != 0 ||
            bar.LengthStatus != LengthStatus.Known)
            return Block("CLIP_PATH_UNSUPPORTED", "hooked, lapped or multi-segment arc");
        double original = arc.CenterlineRadiusMm * Math.Abs(arc.SweepRadians);
        if (!double.IsFinite(original) || Math.Abs(original - bar.ScheduleLengthMm) > policy.ToleranceMm ||
            !bar.FabricationLengthMm.HasValue || Math.Abs(original - bar.FabricationLengthMm.Value) > policy.ToleranceMm)
            return Block("CLIP_LENGTH_INVALID", "arc length mismatch");
        var active = openings.Where(o => o.OwnerId == bar.ComponentId).DistinctBy(o => o.Id)
            .OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
        if (active.Length == 0) return new(true, new ClipResult([bar], 0, original), []);
        var curves = new List<(OpeningProfile Opening, double U, double Z, double Su, double Sz,
            double Cu, double Cz, CircleProfile? Circle, Polygon2Mm? Polygon)>();
        var cross = new Vector3(arc.Normal.Y * arc.StartDirection.Z - arc.Normal.Z * arc.StartDirection.Y,
            arc.Normal.Z * arc.StartDirection.X - arc.Normal.X * arc.StartDirection.Z,
            arc.Normal.X * arc.StartDirection.Y - arc.Normal.Y * arc.StartDirection.X);
        static double Dot(Vector3 a, Vector3 b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        foreach (var opening in active)
        {
            if (opening.WallFrame is null)
                return Block("CLIP_PLANE_UNSUPPORTED", opening.Id);
            var frame = opening.WallFrame;
            var center = frame.ToLocal(arc.Center);
            if (Math.Abs(Dot(arc.Normal, frame.Outward)) < 1 - 1e-9 ||
                center.V < opening.ExtrusionRange.Start - policy.ToleranceMm ||
                center.V > opening.ExtrusionRange.End + policy.ToleranceMm)
                return Block("CLIP_PLANE_MISMATCH", opening.Id);
            var circle = opening.ConcreteCutout as CircleProfile;
            var polygon = opening.ConcreteCutout switch
            {
                RectangleProfile r => RectanglePolygon(r.Rectangle),
                PolygonProfile p => p.Polygon,
                OpenTopProfile p => p.Polygon,
                _ => null
            };
            if (circle is null && polygon is null)
                return Block("CLIP_OPENING_UNSUPPORTED", opening.Id);
            curves.Add((opening, center.U, center.Z,
                Dot(arc.StartDirection, frame.Left), Dot(arc.StartDirection, frame.Up),
                Dot(cross, frame.Left), Dot(cross, frame.Up), circle, polygon));
        }
        var events = new List<double> { 0, 1 };
        double radius = arc.CenterlineRadiusMm, sweep = arc.SweepRadians;
        double q = policy.CoverToHoleMm + bar.DiameterMm / 2d;
        foreach (var c in curves)
        {
            if (c.Circle is { } circle)
            {
                double du = c.U - circle.Center.X, dz = c.Z - circle.Center.Y;
                double effective = circle.RadiusMm + q;
                Roots(2*radius*(du*c.Su+dz*c.Sz), 2*radius*(du*c.Cu+dz*c.Cz),
                    effective*effective-du*du-dz*dz-radius*radius);
            }
            else if (c.Polygon is { } polygon)
            {
                var points = polygon.Vertices;
                for (int i = 0; i < points.Length; i++)
                {
                    var p = points[i];
                    var n = points[(i + 1) % points.Length];
                    double ex = n.X - p.X, ez = n.Y - p.Y;
                    double edgeLength = Math.Sqrt(ex*ex + ez*ez);
                    double constant = (ex*(c.Z-p.Y)-ez*(c.U-p.X))/edgeLength;
                    double cosine = radius*(ex*c.Sz-ez*c.Su)/edgeLength;
                    double sine = radius*(ex*c.Cz-ez*c.Cu)/edgeLength;
                    Roots(cosine, sine, q-constant);
                    Roots(cosine, sine, -q-constant);

                    double du = c.U-p.X, dz = c.Z-p.Y;
                    Roots(2*radius*(du*c.Su+dz*c.Sz), 2*radius*(du*c.Cu+dz*c.Cz),
                        q*q-du*du-dz*dz-radius*radius);
                }
            }
        }
        var ordered = events.Distinct().Order().ToArray();
        var kept = new List<(double Start, double End)>();
        for (int i=0;i<ordered.Length-1;i++)
        {
            double t0=ordered[i], t1=ordered[i+1];
            if ((t1-t0)*original <= policy.ToleranceMm) continue;
            double angle = sweep * (t0+t1)/2;
            bool forbidden = curves.Any(c =>
            {
                double u=c.U+radius*(c.Su*Math.Cos(angle)+c.Cu*Math.Sin(angle));
                double z=c.Z+radius*(c.Sz*Math.Cos(angle)+c.Cz*Math.Sin(angle));
                if(c.Circle is { } circle)
                {
                    double du=u-circle.Center.X, dz=z-circle.Center.Y;
                    return du*du+dz*dz < Math.Pow(circle.RadiusMm+q,2);
                }
                var points = c.Polygon!.Vertices;
                bool inside = false;
                double best = double.PositiveInfinity;
                for (int j = 0, k = points.Length-1; j < points.Length; k=j++)
                {
                    var a=points[j]; var b=points[k];
                    if ((a.Y>z)!=(b.Y>z) && u<(b.X-a.X)*(z-a.Y)/(b.Y-a.Y)+a.X)
                        inside=!inside;
                    double vx=b.X-a.X, vy=b.Y-a.Y;
                    double t=Math.Clamp(((u-a.X)*vx+(z-a.Y)*vy)/(vx*vx+vy*vy),0,1);
                    double dx=u-(a.X+t*vx), dy=z-(a.Y+t*vy);
                    best=Math.Min(best,dx*dx+dy*dy);
                }
                return inside || best<q*q;
            });
            if (!forbidden) kept.Add((t0,t1));
        }
        if (kept.Count == 1 && kept[0].Start == 0 && kept[0].End == 1)
            return new(true, new ClipResult([bar], 0, original), []);
        var retained = new List<BarOccurrence>();
        var sources = bar.Sources.Concat(active.SelectMany(o => o.Sources)).Distinct().ToArray();
        foreach (var (start,end) in kept)
        {
            double angle=sweep*start;
            var direction=new Vector3(
                Math.Cos(angle)*arc.StartDirection.X+Math.Sin(angle)*cross.X,
                Math.Cos(angle)*arc.StartDirection.Y+Math.Sin(angle)*cross.Y,
                Math.Cos(angle)*arc.StartDirection.Z+Math.Sin(angle)*cross.Z);
            var childArc=new ArcSegment3Mm(arc.Center,radius,arc.Normal,direction,sweep*(end-start));
            double length=original*(end-start);
            var path=new BarPath([childArc],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            var cutOpeningIds = openings.Where(o => o.OwnerId == bar.ComponentId).Select(o => o.Id).Distinct();
            retained.Add(new BarOccurrence($"{bar.Id}/clip/{retained.Count}",bar.Id,bar.ComponentId,
                bar.ComponentKind,bar.Layer,bar.Axis,bar.DiameterMm,path,length,length,
                LengthStatus.Known,bar.RuleId,sources,bar.Direction,bar.FrameId,bar.Surface,
                bar.FamilyId,bar.MeasurementBasis,bar.InnerRadiusMm,
                bar.StationIndex, bar.StationMm, bar.AssemblyId, bar.PanelInstanceId,
                bar.RegionRole ?? (retained.Count == 0 ? "Left" : "Right"), cutOpeningIds, length,
                bar.ShapeRecipeId, bar.SpecId, bar.StationKey, bar.FabricationBasis));
        }
        return new(true,new ClipResult(retained,original-retained.Sum(x=>x.ScheduleLengthMm),original),[]);

        void Roots(double a,double b,double target)
        {
            double amplitude=Math.Sqrt(a*a+b*b);
            if(amplitude==0 || Math.Abs(target)>amplitude) return;
            double phase=Math.Atan2(b,a), offset=Math.Acos(Math.Clamp(target/amplitude,-1,1));
            foreach(double root in new[] { phase+offset, phase-offset })
            for(int k=-2;k<=2;k++)
            {
                double t=(root+2*Math.PI*k)/sweep;
                if(t>0 && t<1) events.Add(t);
            }
        }

    }

    private static Polygon2Mm RectanglePolygon(Rectangle2Mm r) =>
        new([new Point2Mm(r.MinX, r.MinY), new Point2Mm(r.MaxX, r.MinY),
            new Point2Mm(r.MaxX, r.MaxY), new Point2Mm(r.MinX, r.MaxY)]);

    public static OperationResult<ClipResult> ClipStraightSegment(
        BarOccurrence bar, RoundExclusion exclusion, ClippingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(exclusion);
        ArgumentNullException.ThrowIfNull(policy);
        OperationResult<ClipResult> Block(string code, string message) =>
            new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,
                string.IsNullOrWhiteSpace(exclusion.ManholeId) ? DiagnosticScope.Global : DiagnosticScope.Component,
                exclusion.ManholeId, string.IsNullOrWhiteSpace(exclusion.ManholeId) ? null : exclusion.OwnerComponentId,
                exclusion.Source ?? bar.Sources.FirstOrDefault(),null,null,message)]);

        if (!Enum.IsDefined(policy.Mode) || policy.Mode != ClipMode.SingleRound)
            return Block("CLIP_MODE_UNSUPPORTED","Only one round opening is supported.");
        double tolerance=policy.ToleranceMm;
        double radius=exclusion.ConcreteRadiusMm+exclusion.CoverToHoleMm+exclusion.BarDiameterMm/2d;
        if (!double.IsFinite(tolerance) || tolerance <= 0 ||
            !double.IsFinite(exclusion.ConcreteRadiusMm) || exclusion.ConcreteRadiusMm <= 0 ||
            !double.IsFinite(exclusion.CoverToHoleMm) || exclusion.CoverToHoleMm < 0 ||
            exclusion.BarDiameterMm <= 0 || exclusion.BarDiameterMm != bar.DiameterMm ||
            !double.IsFinite(radius) || radius <= 0)
            return Block("CLIP_DIMENSION_INVALID","Circle radius, cover, bar diameter or tolerance is invalid.");
        if (bar.ComponentId != exclusion.OwnerComponentId)
        {
            double unchanged=bar.ScheduleLengthMm;
            return new(true,new ClipResult([bar],0,unchanged),[]);
        }
        if (bar.Path.Closed || bar.Path.Segments.Length != 1 ||
            bar.Path.Segments[0] is not LineSegment3Mm line || bar.Path.HookPattern != HookPattern.Hook0M ||
            bar.Path.AdditionalLapMm != 0)
            return Block("CLIP_PATH_UNSUPPORTED","Only an open, physical straight 0M segment is supported.");
        if (!Enum.IsDefined(exclusion.Plane))
            return Block("CLIP_PLANE_MISMATCH","Opening plane is invalid.");

        double start, end, fixedCoordinate, circleAlong, circleFixed;
        bool horizontal;
        Func<double,Point3Mm> world;
        if (exclusion.Plane == ClipPlane.WallLocalUZ)
        {
            if (exclusion.WallFrame is null || exclusion.WallDepthMm is null)
                return Block("CLIP_PLANE_MISMATCH","Wall opening needs an explicit frame and depth.");
            var frame=exclusion.WallFrame;
            if (exclusion.OwnerComponentId != $"Wall/{frame.WallId}" ||
                bar.ComponentKind != ComponentKind.Wall || exclusion.WallDepthMm.Value.Length <= 0)
                return Block("CLIP_PLANE_MISMATCH","Wall owner, frame or depth is inconsistent.");
            var a=frame.ToLocal(line.Start); var b=frame.ToLocal(line.End);
            if (Math.Abs(a.V-b.V)>tolerance || a.V<exclusion.WallDepthMm.Value.Start-tolerance ||
                a.V>exclusion.WallDepthMm.Value.End+tolerance)
                return Block("CLIP_PLANE_MISMATCH","Bar is outside the wall opening depth plane.");
            if (Math.Abs(a.Z-b.Z)<=tolerance && b.U>a.U+tolerance)
            {
                horizontal=true; start=a.U; end=b.U; fixedCoordinate=(a.Z+b.Z)/2;
                circleAlong=exclusion.CenterMm.X; circleFixed=exclusion.CenterMm.Y;
                world=value=>frame.ToGlobal(value,a.V,fixedCoordinate);
            }
            else if (Math.Abs(a.U-b.U)<=tolerance && b.Z>a.Z+tolerance)
            {
                horizontal=false; start=a.Z; end=b.Z; fixedCoordinate=(a.U+b.U)/2;
                circleAlong=exclusion.CenterMm.Y; circleFixed=exclusion.CenterMm.X;
                world=value=>frame.ToGlobal(fixedCoordinate,a.V,value);
            }
            else if (Math.Abs(a.Z-b.Z)<=tolerance && b.U<a.U-tolerance ||
                     Math.Abs(a.U-b.U)<=tolerance && b.Z<a.Z-tolerance)
                return Block("CLIP_INTERVAL_INVALID","Bar interval is reversed in opening coordinates.");
            else return Block("CLIP_PATH_UNSUPPORTED","Straight bar is not horizontal or vertical in wall local coordinates.");
        }
        else
        {
            if (!exclusion.WorldPlaneZmm.HasValue || !double.IsFinite(exclusion.WorldPlaneZmm.Value) ||
                Math.Abs(line.Start.Z-exclusion.WorldPlaneZmm.Value)>tolerance ||
                Math.Abs(line.End.Z-exclusion.WorldPlaneZmm.Value)>tolerance)
                return Block("CLIP_PLANE_MISMATCH","Bar and world XY opening lie in different planes.");
            if (Math.Abs(line.Start.Y-line.End.Y)<=tolerance && line.End.X>line.Start.X+tolerance)
            {
                horizontal=true; start=line.Start.X; end=line.End.X; fixedCoordinate=(line.Start.Y+line.End.Y)/2;
                circleAlong=exclusion.CenterMm.X; circleFixed=exclusion.CenterMm.Y;
                world=value=>new Point3Mm(value,fixedCoordinate,exclusion.WorldPlaneZmm.Value);
            }
            else if (Math.Abs(line.Start.X-line.End.X)<=tolerance && line.End.Y>line.Start.Y+tolerance)
            {
                horizontal=false; start=line.Start.Y; end=line.End.Y; fixedCoordinate=(line.Start.X+line.End.X)/2;
                circleAlong=exclusion.CenterMm.Y; circleFixed=exclusion.CenterMm.X;
                world=value=>new Point3Mm(fixedCoordinate,value,exclusion.WorldPlaneZmm.Value);
            }
            else if (Math.Abs(line.Start.Y-line.End.Y)<=tolerance && line.End.X<line.Start.X-tolerance ||
                     Math.Abs(line.Start.X-line.End.X)<=tolerance && line.End.Y<line.Start.Y-tolerance)
                return Block("CLIP_INTERVAL_INVALID","Bar interval is reversed in opening coordinates.");
            else return Block("CLIP_PATH_UNSUPPORTED","Straight bar is not horizontal or vertical in world XY.");
        }

        double original=end-start;
        if (!double.IsFinite(original) || original <= tolerance ||
            Math.Abs(original-bar.ScheduleLengthMm)>tolerance ||
            bar.LengthStatus != LengthStatus.Known ||
            !bar.FabricationLengthMm.HasValue || Math.Abs(original-bar.FabricationLengthMm.Value)>tolerance)
            return Block("CLIP_PATH_UNSUPPORTED","Bar length must describe this physical straight path.");
        IReadOnlyList<DBTool.Core.Interval1D> intervals;
        try
        {
            intervals=horizontal
                ? CircleOpeningClipper.SubtractCircleFromHorizontal(start,end,fixedCoordinate,circleAlong,circleFixed,radius,tolerance)
                : CircleOpeningClipper.SubtractCircleFromVertical(start,end,fixedCoordinate,circleFixed,circleAlong,radius,tolerance);
        }
        catch (ArgumentException ex) { return Block("CLIP_DIMENSION_INVALID",ex.Message); }
        if (intervals.Count==1 && Math.Abs(intervals[0].Start-start)<=tolerance &&
            Math.Abs(intervals[0].End-end)<=tolerance)
            return new(true,new ClipResult([bar],0,original),[]);

        var retained=new List<BarOccurrence>();
        var sources=bar.Sources.Concat(exclusion.Source is null ? [] : [exclusion.Source]).Distinct().ToArray();
        foreach(var interval in intervals.OrderBy(x=>x.Start))
        {
            if (interval.Length<=tolerance) continue;
            var path=new BarPath([new LineSegment3Mm(world(interval.Start),world(interval.End))],false,
                HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            double length=interval.Length;
            retained.Add(new BarOccurrence($"{bar.Id}/clip/{retained.Count}",bar.Id,bar.ComponentId,
                bar.ComponentKind,bar.Layer,bar.Axis,bar.DiameterMm,path,length,length,
                LengthStatus.Known,bar.RuleId,sources,bar.Direction,bar.FrameId,bar.Surface,
                bar.FamilyId,bar.MeasurementBasis,bar.InnerRadiusMm,
                bar.StationIndex, bar.StationMm, bar.AssemblyId, bar.PanelInstanceId,
                bar.RegionRole ?? (retained.Count == 0 ? "Left" : "Right"), [], length,
                bar.ShapeRecipeId, bar.SpecId, bar.StationKey, bar.FabricationBasis));
        }
        double removed=original-retained.Sum(x=>x.ScheduleLengthMm);
        if (removed<0 && Math.Abs(removed)<=tolerance) removed=0;
        if (!double.IsFinite(removed) || removed<0)
            return Block("CLIP_LENGTH_INVALID","Clipping did not conserve length.");
        return new(true,new ClipResult(retained,removed,original),[]);
    }
}
