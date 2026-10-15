#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.PlanarRegions;

namespace DBTool.Drawing;

/// <summary>
/// Explicit analytic provider. Boolean classification uses complete occupied sets on both
/// sides of every subdivided analytic edge; holes are never treated as filled operands.
/// </summary>
public sealed class AnalyticPlanarOccupiedRegionKernel : IPlanarOccupiedRegionKernel
{
    public PlanarOccupiedRegionResult Execute(PlanarOccupiedRegionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tol = request.Tolerance;
        if (tol is null || !Positive(tol.LinearMm) || !Positive(tol.AngularRad) || !Positive(tol.AreaMm2) || !Positive(tol.BasisDimensionless)
            || tol.SourceIds is null || tol.SourceIds.Length == 0 || tol.SourceIds.Any(string.IsNullOrWhiteSpace))
            throw Fault("CSG_TOLERANCE_UNRESOLVED", request.RequestId, "positive finite sourced linear/angular/area tolerances", request.SourceIds ?? []);
        if (new[] {request.RequestId,request.AssemblyId,request.MaterialId,request.ViewId}.Any(string.IsNullOrWhiteSpace)
            || request.SourceIds is null || request.SourceIds.Length == 0 || request.SourceIds.Any(string.IsNullOrWhiteSpace)
            || request.Components is null || request.Components.Any(c=>c is null)
            || request.Components.Select(c=>c.ComponentId).Distinct().Count()!=request.Components.Length)
            throw Fault("CSG_OWNERSHIP_INVALID", request.RequestId, "unique exact component/request ownership and sources", request.SourceIds ?? []);
        int edges=0;
        // Validate every operand before evaluating any operation, including operands of empty intersections.
        foreach (var c in request.Components) {
            if (string.IsNullOrWhiteSpace(c.ComponentId) || c.SourceIds is null || c.SourceIds.Length==0
                || c.SourceIds.Any(string.IsNullOrWhiteSpace))
                throw Fault("CSG_SOURCE_UNRESOLVED", c.ComponentId, "component source IDs", request.SourceIds);
            if(c.Operations is null || c.Operations.Any(o=>o is null))
                throw Fault("CSG_GEOMETRY_UNRESOLVED",c.ComponentId,"explicit ordered operations",c.SourceIds);
            int? previous=null;
            foreach (var op in c.Operations) {
                if (previous.HasValue && op.Sequence<=previous.Value)
                    throw Fault("CSG_OPERATION_ORDER_INVALID", op.Id, "strictly increasing source sequence", request.SourceIds);
                previous=op.Sequence;
                if (op.Kind is not ("Union" or "Subtract" or "Intersect") || op.SourceIds is null
                    || op.SourceIds.Length==0 || op.SourceIds.Any(string.IsNullOrWhiteSpace))
                    throw Fault("CSG_OPERATION_INVALID", op.Id, "sourced Union/Subtract/Intersect", request.SourceIds);
                ValidateLoops(op.Loops,tol with {SourceIds=tol.SourceIds.Concat(op.SourceIds).Distinct().ToArray()});
                edges+=op.Loops.Sum(l=>l.curves.Length);
            }
        }
        var all=new List<LoopV2>();
        foreach (var c in request.Components) {
            var occupied=new List<LoopV2>();
            foreach (var op in c.Operations) {
                var operationTolerance=tol with {SourceIds=tol.SourceIds.Concat(c.SourceIds).Concat(op.SourceIds).Distinct().ToArray()};
                occupied=Combine(occupied,Normalize(op.Loops,operationTolerance),op.Kind,operationTolerance);
            }
            all=Combine(all,occupied,"Union",tol with {SourceIds=tol.SourceIds.Concat(c.SourceIds).Distinct().ToArray()});
        }
        var sources=request.SourceIds.Concat(request.Components.SelectMany(c=>c.SourceIds))
            .Concat(request.Components.SelectMany(c=>c.Operations).SelectMany(o=>o.SourceIds)).Distinct().Order().ToArray();
        all=all.Select((l,i)=>l with {id=request.RequestId+"/Boundary/"+i}).ToList();
        var boundaries=all.Select(l=>new PlanarRegionLoop(l,Parent(l,all,tol.LinearMm)?.id,sources)).ToArray();
        string CurveKey(CurveV2 c)=>string.Join(",",c.kind,Num(c.start.x_mm),Num(c.start.y_mm),
            Num(c.end.x_mm),Num(c.end.y_mm),c.center is null?"":Num(c.center.x_mm),
            c.center is null?"":Num(c.center.y_mm),c.radius_mm is null?"":Num(c.radius_mm.Value),
            c.sweep_rad is null?"":Num(c.sweep_rad.Value),c.normal is null?"":Num(c.normal.z));
        string LoopKey(LoopV2 l) {
            var keys=l.curves.Select(CurveKey).ToArray();
            // Cyclic start-edge allocation and input iteration order are not physical identity.
            return l.role+":"+Enumerable.Range(0,keys.Length)
                .Select(i=>string.Join(";",keys.Skip(i).Concat(keys.Take(i)))).Order(StringComparer.Ordinal).First();
        }
        var canonical=string.Join("|",all.Select(LoopKey).Order(StringComparer.Ordinal));
        return new(request.RequestId,request.AssemblyId,request.MaterialId,request.ViewId,all.Count==0,boundaries,
            all.Sum(l=>l.signed_area_mm2),edges,all.Sum(l=>l.curves.Length),[],
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),sources);
    }
    private static string Num(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    private static bool Positive(double n)=>double.IsFinite(n)&&n>0;
    private static DrawingStageFaultException Fault(string code,string subject,string expected,string[] sources,
        string actual="invalid or unsupported analytic geometry",string field="analytic_region")=>
        new(code,subject,[field],expected,actual,sources);
    private static string CurveActual(CurveV2 c)=>string.Join(";",
        "kind="+c.kind,"start="+c.start,"end="+c.end,"center="+c.center,"normal="+c.normal,
        "radius_mm="+(c.radius_mm is null?"missing":Num(c.radius_mm.Value)),
        "sweep_rad="+(c.sweep_rad is null?"missing":Num(c.sweep_rad.Value)));

    public static void ValidateLoops(LoopV2[] loops,PlanarRegionTolerance tol)
    {
        if(loops is null || loops.Any(l=>l is null))
            throw Fault("CSG_GEOMETRY_UNRESOLVED","loops","explicit analytic loops array; [] is explicit Empty",tol.SourceIds);
        if(loops.Any(l=>string.IsNullOrWhiteSpace(l.id)) || loops.Select(l=>l.id).Distinct(StringComparer.Ordinal).Count()!=loops.Length)
            throw Fault("CSG_ID_INVALID","loops","nonempty unique loop IDs within an operand",tol.SourceIds);
        foreach(var l in loops) {
            if (!l.closed || l.curves is null || l.curves.Length==0)
                throw Fault("CSG_LOOP_NOT_CLOSED",l.id,"complete declared closed loop", tol.SourceIds, "loop="+l.id+"; closed="+l.closed+"; edges="+(l.curves?.Length??0), "loops["+l.id+"]");
            if(l.role is not ("Outer" or "Inner")) throw Fault("CSG_LOOP_ROLE_INVALID",l.id,"Outer or Inner", tol.SourceIds);
            if(l.curves.Any(curve=>curve is null || string.IsNullOrWhiteSpace(curve.id)) || l.curves.Select(curve=>curve.id).Distinct(StringComparer.Ordinal).Count()!=l.curves.Length)
                throw Fault("CSG_ID_INVALID",l.id,"non-null curves with unique nonempty IDs within the loop",tol.SourceIds);
            foreach(var c in l.curves) {
                if(!Finite(c.start)||!Finite(c.end)) throw Fault("CURVE_DOMAIN_INVALID",c.id,"finite endpoints", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
                if(Math.Abs(c.start.z_mm)>tol.LinearMm||Math.Abs(c.end.z_mm)>tol.LinearMm)
                    throw Fault("CSG_NONPLANAR_CURVE",c.id,"view-local planar curves", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
                if(c.kind=="Line") {
                    if(Distance(P(c.start),P(c.end))<=tol.LinearMm) throw Fault("CURVE_DOMAIN_INVALID",c.id,"line longer than linear tolerance", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
                } else if(c.kind=="Arc") {
                    if(c.center is null||!Finite(c.center)||c.normal is null||!Positive(c.radius_mm??double.NaN)
                        || !double.IsFinite(c.sweep_rad??double.NaN)||c.sweep_rad==0||Math.Abs(c.sweep_rad!.Value)>2*Math.PI
                        ||!double.IsFinite(c.normal.x)||!double.IsFinite(c.normal.y)||!double.IsFinite(c.normal.z)
                        ||Math.Abs(c.normal.x)>tol.BasisDimensionless||Math.Abs(c.normal.y)>tol.BasisDimensionless||Math.Abs(Math.Abs(c.normal.z)-1)>tol.BasisDimensionless)
                        throw Fault("CURVE_DOMAIN_INVALID",c.id,"finite radius>0, 0<abs(sweep)<=2pi, planar unit normal", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
                    var center=P(c.center);double r=c.radius_mm!.Value;
                    double start=Math.Atan2(c.start.y_mm-center.Y,c.start.x_mm-center.X);
                    double sweep=SignedSweep(c);
                    var expected=new Point2D(center.X+r*Math.Cos(start+sweep),center.Y+r*Math.Sin(start+sweep));
                    if(Math.Abs(Distance(center,P(c.start))-r)>tol.LinearMm
                        ||Distance(expected,P(c.end))>tol.LinearMm||Math.Abs(c.center.z_mm)>tol.LinearMm)
                        throw Fault("CURVE_DOMAIN_INVALID",c.id,"radius/sweep/endpoints consistent", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
                } else throw Fault("CURVE_DOMAIN_INVALID",c.id,"analytic Line or Arc", tol.SourceIds, CurveActual(c), "curves["+c.id+"]");
            }
            for(int i=0;i<l.curves.Length;i++)
                if(Distance(P(l.curves[i].end),P(l.curves[(i+1)%l.curves.Length].start))>tol.LinearMm)
                    throw Fault("CSG_LOOP_NOT_CLOSED",l.id,"every edge consumed in ordered closed loop", tol.SourceIds, "loop="+l.id+"; closed="+l.closed+"; edges="+(l.curves?.Length??0), "loops["+l.id+"]");
        }
    }
    private static bool Finite(Point3V2? p)=>p is not null&&double.IsFinite(p.x_mm)&&double.IsFinite(p.y_mm)&&double.IsFinite(p.z_mm);
    private static Point2D P(Point3V2 p)=>new(p.x_mm,p.y_mm);
    internal static double SignedSweep(CurveV2 c)=>(c.normal?.z<0?-1:1)*c.sweep_rad!.Value;
    private static double ArcParameter(double angle,double start,double sweep) {
        double d=(angle-start)%(2*Math.PI);
        if(sweep>0&&d<0)d+=2*Math.PI;
        if(sweep<0&&d>0)d-=2*Math.PI;
        return d/sweep;
    }
    private static List<LoopV2> Normalize(IEnumerable<LoopV2> loops,PlanarRegionTolerance tol) {
        var result=new List<LoopV2>();
        foreach(var loop in loops) {
            var segments=FlattenLoopsToSegments([loop]);
            double area=segments.Sum(s=>s.SignedAreaContribution());
            if(!double.IsFinite(area))
                throw Fault("CSG_NUMERICAL_RANGE_INVALID",loop.id,"finite analytic occupied area",tol.SourceIds,
                    "signed_area_mm2="+Num(area),"loops["+loop.id+"].signed_area_mm2");
            if(Math.Abs(area)<=tol.AreaMm2) throw Fault("CSG_DEGENERATE_LOOP",loop.id,"area above sourced area tolerance", tol.SourceIds);
            bool outer=loop.role=="Outer";
            if((area>0)!=outer) segments=segments.AsEnumerable().Reverse().Select(s=>s.Reversed()).ToList();
            result.Add(loop with {curves=segments.Select(s=>s.ToCurveV2()).ToArray(),signed_area_mm2=outer?Math.Abs(area):-Math.Abs(area)});
        }
        var outers=result.Where(l=>l.role=="Outer").ToArray();
        var owners=new Dictionary<LoopV2,LoopV2>();
        foreach(var hole in result.Where(l=>l.role=="Inner")) {
            var owner=outers.Where(o=>Math.Abs(o.signed_area_mm2)>Math.Abs(hole.signed_area_mm2)
                && IsPointInsideLoop(P(hole.curves[0].start),o,0))
                .OrderBy(o=>Math.Abs(o.signed_area_mm2)).FirstOrDefault();
            if(owner is null)
                throw Fault("CSG_HOLE_OWNER_UNRESOLVED",hole.id,"containing occupied outer in this operand", tol.SourceIds);
            var holeEdges=FlattenLoopsToSegments([hole]);
            var ownerEdges=FlattenLoopsToSegments([owner]);
            // Any crossing/tangency to the owned outer is invalid input topology. Test whole
            // analytic curves, not just the midpoint, so excursions cannot be missed.
            if(holeEdges.Any(edge=>!IsPointInsideLoop(edge.Midpoint,owner,0)
                || ownerEdges.Any(boundary=>FindSegmentIntersections(edge,boundary,tol.LinearMm).Count>0)))
                throw Fault("CSG_HOLE_OWNER_UNRESOLVED",hole.id,"entire hole contained in its owned outer", tol.SourceIds);
            owners.Add(hole,owner);
        }
        // Compose each owned outer-minus-holes before unioning other outers or islands.
        // In particular, overlapping holes remove material once and another outer can fill a void.
        var complete=new List<LoopV2>();
        foreach(var outer in outers.OrderByDescending(o=>Math.Abs(o.signed_area_mm2))) {
            var region=new List<LoopV2>{outer};
            foreach(var hole in owners.Where(pair=>pair.Value==outer).Select(pair=>pair.Key))
                region=Combine(region,[ReverseLoop(hole) with {role="Outer"}],"Subtract",tol);
            complete=Combine(complete,region,"Union",tol);
        }
        return complete;
    }
    private static LoopV2? Parent(LoopV2 l,IEnumerable<LoopV2> all,double tol)=>
        all.Where(o=>o!=l&&Math.Abs(o.signed_area_mm2)>Math.Abs(l.signed_area_mm2)
            &&IsPointInsideLoop(P(l.curves[0].start),o,0)).OrderBy(o=>Math.Abs(o.signed_area_mm2)).FirstOrDefault();
    private static bool Occupied(Point2D p,List<LoopV2> loops)=>
        loops.Where(l=>IsPointInsideLoop(p,l,0)).Sum(l=>l.role=="Outer"?1:-1)>0;
    internal static bool Contains(LoopV2[] loops,double x,double y)=>
        loops.Where(l=>IsPointInsideLoop(new(x,y),l,0)).Sum(l=>l.role=="Outer"?1:-1)>0;
    private static List<LoopV2> Combine(List<LoopV2> a,List<LoopV2> b,string kind,PlanarRegionTolerance tol) {
        if(a.Count==0)return kind=="Union"?b.ToList():[];
        if(b.Count==0)return kind=="Intersect"?[]:a.ToList();
        var sa=FlattenLoopsToSegments(a);var sb=FlattenLoopsToSegments(b);
        SubdivideIntersectingSegments(sa,sb,tol.LinearMm);
        bool Eval(Point2D p) {
            bool ia=Occupied(p,a),ib=Occupied(p,b);
            return kind=="Union"?ia||ib:kind=="Subtract"?ia&&!ib:ia&&ib;
        }
        var kept=new List<Segment2D>();
        foreach(var seg in sa.Concat(sb)) {
            var mid=seg.Midpoint;
            double tx,ty;
            if(seg is ArcSegment2D arc) {
                tx=-(mid.Y-arc.Center.Y)*Math.Sign(arc.SweepRad);
                ty=(mid.X-arc.Center.X)*Math.Sign(arc.SweepRad);
            } else {tx=seg.End.X-seg.Start.X;ty=seg.End.Y-seg.Start.Y;}
            double len=Math.Sqrt(tx*tx+ty*ty);
            double offset=Math.Min(tol.LinearMm*.25,len*.1);
            bool left=Eval(new(mid.X-ty/len*offset,mid.Y+tx/len*offset));
            bool right=Eval(new(mid.X+ty/len*offset,mid.Y-tx/len*offset));
            if(left==right)continue;
            var directed=left?seg:seg.Reversed();
            if(!kept.Any(k=>AreSegmentsCoincident(k,directed,tol.LinearMm)))kept.Add(directed);
        }
        return StitchClosedLoops(kept,tol.LinearMm,tol.AreaMm2,tol.SourceIds);
    }
    private static List<Segment2D> FlattenLoopsToSegments(IEnumerable<LoopV2> loops)
    {
        var segments = new List<Segment2D>();
        foreach (var loop in loops)
        {
            foreach (var curve in loop.curves ?? [])
            {
                var pStart = new Point2D(curve.start.x_mm, curve.start.y_mm);
                var pEnd = new Point2D(curve.end.x_mm, curve.end.y_mm);

                if (string.Equals(curve.kind, "Arc", StringComparison.OrdinalIgnoreCase))
                {
                    var pCenter = new Point2D(curve.center!.x_mm, curve.center.y_mm);
                    double radius = curve.radius_mm!.Value;
                    double sweep = SignedSweep(curve);

                    segments.Add(new ArcSegment2D(curve.id, pStart, pEnd, pCenter, radius, sweep, curve.role ?? "Boundary"));
                }
                else
                {
                    segments.Add(new LineSegment2D(curve.id, pStart, pEnd, curve.role ?? "Boundary"));
                }
            }
        }
        return segments;
    }

    private static void SubdivideIntersectingSegments(
        List<Segment2D> listA,
        List<Segment2D> listB,
        double tol)
    {
        var splitsA = new Dictionary<Segment2D, List<Point2D>>();
        var splitsB = new Dictionary<Segment2D, List<Point2D>>();

        foreach (var sA in listA)
        {
            foreach (var sB in listB)
            {
                var hitPoints = FindSegmentIntersections(sA, sB, tol);
                foreach (var pt in hitPoints)
                {
                    if (IsPointStrictlyInternalToSegment(sA, pt, tol))
                    {
                        if (!splitsA.TryGetValue(sA, out var pts))
                        {
                            pts = [];
                            splitsA[sA] = pts;
                        }
                        if (!pts.Any(p => Distance(p, pt) <= tol)) pts.Add(pt);
                    }

                    if (IsPointStrictlyInternalToSegment(sB, pt, tol))
                    {
                        if (!splitsB.TryGetValue(sB, out var pts))
                        {
                            pts = [];
                            splitsB[sB] = pts;
                        }
                        if (!pts.Any(p => Distance(p, pt) <= tol)) pts.Add(pt);
                    }
                }
            }
        }

        // Apply splits to listA
        ApplySplitsToList(listA, splitsA, tol);
        // Apply splits to listB
        ApplySplitsToList(listB, splitsB, tol);
    }

    private static void ApplySplitsToList(
        List<Segment2D> list,
        Dictionary<Segment2D, List<Point2D>> splits,
        double tol)
    {
        if (splits.Count == 0) return;

        var replacements = new List<Segment2D>();
        foreach (var seg in list)
        {
            if (splits.TryGetValue(seg, out var pts) && pts.Count > 0)
            {
                var orderedPts = OrderPointsAlongSegment(seg, pts);
                var subSegments = SplitSegmentAtPoints(seg, orderedPts);
                replacements.AddRange(subSegments);
            }
            else
            {
                replacements.Add(seg);
            }
        }

        list.Clear();
        list.AddRange(replacements);
    }

    private static List<Point2D> OrderPointsAlongSegment(Segment2D seg, List<Point2D> pts)
    {
        if (seg is ArcSegment2D arc)
        {
            return pts.OrderBy(p =>
            {
                double angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
                double diff = (angle - arc.StartAngle) % (2 * Math.PI);
                if (arc.SweepRad > 0 && diff < 0) diff += 2 * Math.PI;
                if (arc.SweepRad < 0 && diff > 0) diff -= 2 * Math.PI;
                return Math.Abs(diff);
            }).ToList();
        }

        var start = seg.Start;
        var end = seg.End;
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;

        return pts.OrderBy(p => (p.X - start.X) * dx + (p.Y - start.Y) * dy).ToList();
    }

    private static List<Segment2D> SplitSegmentAtPoints(Segment2D seg, List<Point2D> pts)
    {
        var result = new List<Segment2D>();
        Segment2D current = seg;

        foreach (var pt in pts)
        {
            var (first, second) = current.SplitAt(pt);
            result.Add(first);
            current = second;
        }
        result.Add(current);

        return result;
    }

    private static List<Point2D> FindSegmentIntersections(Segment2D s1, Segment2D s2, double tol)
    {
        var hits = new List<Point2D>();

        if (s1 is LineSegment2D l1 && s2 is LineSegment2D l2)
        {
            var hit = IntersectLineLine(l1, l2, tol);
            if (hit.HasValue)
            {
                hits.Add(hit.Value);
            }
            else
            {
                if (IsPointStrictlyInternalToSegment(l1, l2.Start, tol)) hits.Add(l2.Start);
                if (IsPointStrictlyInternalToSegment(l1, l2.End, tol)) hits.Add(l2.End);
                if (IsPointStrictlyInternalToSegment(l2, l1.Start, tol)) hits.Add(l1.Start);
                if (IsPointStrictlyInternalToSegment(l2, l1.End, tol)) hits.Add(l1.End);
            }
        }
        else if (s1 is LineSegment2D line && s2 is ArcSegment2D arc)
        {
            hits.AddRange(IntersectLineArc(line, arc, tol));
        }
        else if (s1 is ArcSegment2D a1 && s2 is LineSegment2D l)
        {
            hits.AddRange(IntersectLineArc(l, a1, tol));
        }
        else if (s1 is ArcSegment2D arc1 && s2 is ArcSegment2D arc2)
        {
            hits.AddRange(IntersectArcArc(arc1, arc2, tol));
        }

        return hits;
    }

    private static Point2D? IntersectLineLine(LineSegment2D l1, LineSegment2D l2, double tol)
    {
        double x1 = l1.Start.X, y1 = l1.Start.Y;
        double x2 = l1.End.X, y2 = l1.End.Y;
        double x3 = l2.Start.X, y3 = l2.Start.Y;
        double x4 = l2.End.X, y4 = l2.End.Y;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (denom == 0) return null;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

        double tTol = tol / Distance(l1.Start,l1.End);
        double uTol = tol / Distance(l2.Start,l2.End);
        if (t >= -tTol && t <= 1 + tTol && u >= -uTol && u <= 1 + uTol)
        {
            return new Point2D(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        }

        return null;
    }

    private static List<Point2D> IntersectLineArc(LineSegment2D line, ArcSegment2D arc, double tol)
    {
        var hits = new List<Point2D>();
        double dx = line.End.X - line.Start.X;
        double dy = line.End.Y - line.Start.Y;
        double fx = line.Start.X - arc.Center.X;
        double fy = line.Start.Y - arc.Center.Y;

        double a = dx * dx + dy * dy;
        double b = 2 * (fx * dx + fy * dy);
        double c = fx * fx + fy * fy - arc.Radius * arc.Radius;

        if (a == 0) return hits;

        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return hits;

        double sqrtD = Math.Sqrt(discriminant);
        double t1 = (-b - sqrtD) / (2 * a);
        double t2 = (-b + sqrtD) / (2 * a);

        foreach (double t in new[] { t1, t2 })
        {
            double parameterTol=tol/Math.Sqrt(a);
            if (t >= -parameterTol && t <= 1 + parameterTol)
            {
                var pt = new Point2D(line.Start.X + t * dx, line.Start.Y + t * dy);
                double angle = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
                if (IsAngleOnArc(angle, arc.StartAngle, arc.SweepRad, tol/arc.Radius))
                {
                    hits.Add(pt);
                }
            }
        }

        return hits;
    }

    private static List<Point2D> IntersectArcArc(ArcSegment2D a1, ArcSegment2D a2, double tol)
    {
        var hits = new List<Point2D>();
        double d = Distance(a1.Center, a2.Center);
        // Coincident circles need the common angular partition before duplicate-edge removal.
        // Keeping an unsplit full circle beside two semicircles doubles the occupied area.
        if(d<=tol && Math.Abs(a1.Radius-a2.Radius)<=tol) {
            foreach(var point in new[]{a1.Start,a1.End,a2.Start,a2.End}) {
                var angle1=Math.Atan2(point.Y-a1.Center.Y,point.X-a1.Center.X);
                var angle2=Math.Atan2(point.Y-a2.Center.Y,point.X-a2.Center.X);
                if(IsAngleOnArc(angle1,a1.StartAngle,a1.SweepRad,tol/a1.Radius)
                    && IsAngleOnArc(angle2,a2.StartAngle,a2.SweepRad,tol/a2.Radius)
                    && !hits.Any(existing=>Distance(existing,point)<=tol))hits.Add(point);
            }
            return hits;
        }
        if (d <= tol || d > a1.Radius + a2.Radius + tol || d < Math.Abs(a1.Radius - a2.Radius) - tol)
        {
            return hits;
        }

        double a = (a1.Radius * a1.Radius - a2.Radius * a2.Radius + d * d) / (2 * d);
        double hSq = a1.Radius * a1.Radius - a * a;
        if (hSq < 0) return hits;
        double h = Math.Sqrt(Math.Max(0, hSq));

        double px = a1.Center.X + a * (a2.Center.X - a1.Center.X) / d;
        double py = a1.Center.Y + a * (a2.Center.Y - a1.Center.Y) / d;

        double rx = -(a2.Center.Y - a1.Center.Y) * (h / d);
        double ry = (a2.Center.X - a1.Center.X) * (h / d);

        var pts = new[] { new Point2D(px + rx, py + ry), new Point2D(px - rx, py - ry) };
        foreach (var pt in pts)
        {
            double ang1 = Math.Atan2(pt.Y - a1.Center.Y, pt.X - a1.Center.X);
            double ang2 = Math.Atan2(pt.Y - a2.Center.Y, pt.X - a2.Center.X);

            if (IsAngleOnArc(ang1, a1.StartAngle, a1.SweepRad, tol/a1.Radius) &&
                IsAngleOnArc(ang2, a2.StartAngle, a2.SweepRad, tol/a2.Radius))
            {
                hits.Add(pt);
            }
        }

        return hits;
    }

    private static bool IsAngleOnArc(double angle, double startAngle, double sweepRad, double tol)
    {
        if (Math.Abs(sweepRad) < tol) return false;
        if (sweepRad > 0)
        {
            double diff = (angle - startAngle) % (2 * Math.PI);
            if (diff < -tol) diff += 2 * Math.PI;
            return diff >= -tol && diff <= sweepRad + tol;
        }
        else
        {
            double diff = (angle - startAngle) % (2 * Math.PI);
            if (diff > tol) diff -= 2 * Math.PI;
            return diff <= tol && diff >= sweepRad - tol;
        }
    }

    private static bool IsPointStrictlyInternalToSegment(Segment2D seg, Point2D pt, double tol)
    {
        if (Distance(seg.Start, pt) <= tol || Distance(seg.End, pt) <= tol) return false;

        if (seg is LineSegment2D line)
        {
            double dx = line.End.X - line.Start.X;
            double dy = line.End.Y - line.Start.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq <= tol * tol) return false;

            double cross = Math.Abs((pt.X - line.Start.X) * dy - (pt.Y - line.Start.Y) * dx);
            if (cross * cross > tol * tol * lenSq) return false;

            double t = ((pt.X - line.Start.X) * dx + (pt.Y - line.Start.Y) * dy) / lenSq;
            double endpointTol = tol / Math.Sqrt(lenSq);
            return t > endpointTol && t < 1.0 - endpointTol;
        }
        else if (seg is ArcSegment2D arc)
        {
            double d = Math.Abs(Distance(arc.Center, pt) - arc.Radius);
            if (d > tol) return false;
            double angle = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
            return IsAngleOnArc(angle, arc.StartAngle, arc.SweepRad, tol/arc.Radius);
        }

        return false;
    }

    private static bool AreSegmentsCoincident(Segment2D s1, Segment2D s2, double tol)
    {
        if (Distance(s1.Midpoint, s2.Midpoint) > tol) return false;
        bool sameDir = Distance(s1.Start, s2.Start) <= tol && Distance(s1.End, s2.End) <= tol;
        bool oppDir = Distance(s1.Start, s2.End) <= tol && Distance(s1.End, s2.Start) <= tol;
        return sameDir || oppDir;
    }

    private static bool IsPointInsideLoop(Point2D pt, LoopV2 loop, double tol)
    {
        int windingNumber = 0;
        foreach (var curve in loop.curves ?? [])
        {
            if (string.Equals(curve.kind, "Arc", StringComparison.OrdinalIgnoreCase))
            {
                var pStart = new Point2D(curve.start.x_mm, curve.start.y_mm);
                var pEnd = new Point2D(curve.end.x_mm, curve.end.y_mm);
                var pCenter = new Point2D(curve.center!.x_mm, curve.center.y_mm);
                double radius = curve.radius_mm!.Value;
                double sweep = SignedSweep(curve);

                double startAngle = Math.Atan2(pStart.Y - pCenter.Y, pStart.X - pCenter.X);

                double dy = pt.Y - pCenter.Y;
                if (Math.Abs(dy) <= radius + tol)
                {
                    double dx = Math.Sqrt(Math.Max(0.0, radius * radius - dy * dy));
                    double[] xCandidates = dx > tol ? [pCenter.X - dx, pCenter.X + dx] : [pCenter.X];

                    foreach (double xInt in xCandidates)
                    {
                        if (xInt >= pt.X - tol)
                        {
                            double ang = Math.Atan2(pt.Y - pCenter.Y, xInt - pCenter.X);
                            double param = ArcParameter(ang, startAngle, sweep);
                            if (param >= 0 && param < 1)
                            {
                                double ty = (sweep > 0) ? (xInt - pCenter.X) : -(xInt - pCenter.X);
                                if (ty > tol) windingNumber++;
                                else if (ty < -tol) windingNumber--;
                            }
                        }
                    }
                }
            }
            else
            {
                var p1 = new Point2D(curve.start.x_mm, curve.start.y_mm);
                var p2 = new Point2D(curve.end.x_mm, curve.end.y_mm);

                if (p1.Y <= pt.Y)
                {
                    if (p2.Y > pt.Y)
                    {
                        if (IsLeft(p1, p2, pt) > 0) windingNumber++;
                    }
                }
                else
                {
                    if (p2.Y <= pt.Y)
                    {
                        if (IsLeft(p1, p2, pt) < 0) windingNumber--;
                    }
                }
            }
        }

        return windingNumber != 0;
    }

    private static double IsLeft(Point2D p0, Point2D p1, Point2D p2)
    {
        return (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
    }

    private static List<LoopV2> StitchClosedLoops(
        List<Segment2D> segments,
        double geomTol,
        double areaTol,
        string[] sources)
    {
        var result = new List<LoopV2>();
        var remaining = new List<Segment2D>(segments);
        double connectTol = geomTol;

        while (remaining.Count > 0)
        {
            var currentLoopSegments = new List<Segment2D>();
            var startSeg = remaining[0];
            remaining.RemoveAt(0);
            currentLoopSegments.Add(startSeg);

            var currentPt = startSeg.End;
            var originPt = startSeg.Start;
            bool closed = false;

            while (true)
            {
                if (Distance(currentPt, originPt) <= connectTol)
                {
                    closed = true;
                    break;
                }

                // Find next segment starting near currentPt
                int nextIdx = remaining.FindIndex(s => Distance(s.Start, currentPt) <= connectTol);
                if (nextIdx >= 0)
                {
                    var nextSeg = remaining[nextIdx];
                    remaining.RemoveAt(nextIdx);
                    currentLoopSegments.Add(nextSeg);
                    currentPt = nextSeg.End;
                }
                else
                {
                    // Cannot find connecting segment
                    break;
                }
            }

            if (!closed) throw Fault("CSG_UNCONSUMED_EDGES", string.Join(",", currentLoopSegments.Select(s => s.Id)), "closed output loop", sources);
            if (closed && currentLoopSegments.Count >= 1)
            {
                double totalArea = currentLoopSegments.Sum(s => s.SignedAreaContribution());
                if (Math.Abs(totalArea) > areaTol)
                {
                    string loopId = $"Stitched/{result.Count}";
                    var loopCurves = currentLoopSegments.Select(s => s.ToCurveV2()).ToArray();
                    result.Add(new LoopV2(
                        id: loopId,
                        role: totalArea >= 0 ? "Outer" : "Inner",
                        curves: loopCurves,
                        closed: true,
                        signed_area_mm2: totalArea));
                }
            }
        }

        return result;
    }

    private static LoopV2 ReverseLoop(LoopV2 loop)
    {
        var reversedCurves = new List<CurveV2>();
        foreach (var c in (loop.curves ?? []).Reverse())
        {
            if (string.Equals(c.kind, "Arc", StringComparison.OrdinalIgnoreCase))
            {
                double sweep = -SignedSweep(c);
                reversedCurves.Add(new CurveV2(
                    id: $"{c.id}/rev",
                    kind: "Arc",
                    start: c.end,
                    end: c.start,
                    center: c.center,
                    normal: new Vector3V2(0, 0, sweep >= 0 ? 1 : -1),
                    radius_mm: c.radius_mm,
                    sweep_rad: Math.Abs(sweep),
                    role: c.role
                ));
            }
            else
            {
                reversedCurves.Add(new CurveV2(
                    id: $"{c.id}/rev",
                    kind: "Line",
                    start: c.end,
                    end: c.start,
                    center: null,
                    normal: null,
                    radius_mm: null,
                    sweep_rad: null,
                    role: c.role
                ));
            }
        }

        double newArea = -loop.signed_area_mm2;
        return new LoopV2(
            id: $"{loop.id}/Inverted",
            role: newArea >= 0 ? "Outer" : "Inner",
            curves: reversedCurves.ToArray(),
            closed: loop.closed,
            signed_area_mm2: newArea
        );
    }

    private static double Distance(Point2D a, Point2D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static CurveV2 CreateLineCurve(string id, Point2D start, Point2D end, string role)
    {
        return new CurveV2(
            id: id,
            kind: "Line",
            start: new Point3V2(start.X, start.Y, 0),
            end: new Point3V2(end.X, end.Y, 0),
            center: null,
            normal: null,
            radius_mm: null,
            sweep_rad: null,
            role: role
        );
    }

    private static double SignedPolygonArea(IReadOnlyList<Point2D> points)
    {
        if (points.Count < 3) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];
            sum += (p1.X * p2.Y - p2.X * p1.Y);
        }
        return sum * 0.5;
    }

    private static string Encode(params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            int byteCount = Encoding.UTF8.GetByteCount(p);
            sb.Append(byteCount).Append(':').Append(p);
        }
        return sb.ToString();
    }

    private readonly record struct Point2D(double X, double Y);

    private abstract record Segment2D(string Id, Point2D Start, Point2D End, string Role)
    {
        public abstract Point2D Midpoint { get; }
        public abstract Segment2D Reversed();
        public abstract (Segment2D First, Segment2D Second) SplitAt(Point2D pt);
        public abstract CurveV2 ToCurveV2();
        public abstract double SignedAreaContribution();
    }

    private sealed record LineSegment2D(string Id, Point2D Start, Point2D End, string Role) : Segment2D(Id, Start, End, Role)
    {
        public override Point2D Midpoint => new((Start.X + End.X) * 0.5, (Start.Y + End.Y) * 0.5);

        public override Segment2D Reversed() => new LineSegment2D(Id, End, Start, Role);

        public override (Segment2D First, Segment2D Second) SplitAt(Point2D pt)
        {
            return (
                new LineSegment2D($"{Id}/a", Start, pt, Role),
                new LineSegment2D($"{Id}/b", pt, End, Role)
            );
        }

        public override CurveV2 ToCurveV2() => CreateLineCurve(Id, Start, End, Role);

        public override double SignedAreaContribution() => (Start.X * End.Y - End.X * Start.Y) * 0.5;
    }

    private sealed record ArcSegment2D(
        string Id,
        Point2D Start,
        Point2D End,
        Point2D Center,
        double Radius,
        double SweepRad,
        string Role) : Segment2D(Id, Start, End, Role)
    {
        public double StartAngle => Math.Atan2(Start.Y - Center.Y, Start.X - Center.X);

        public override Point2D Midpoint
        {
            get
            {
                double midAngle = StartAngle + SweepRad * 0.5;
                return new(Center.X + Radius * Math.Cos(midAngle), Center.Y + Radius * Math.Sin(midAngle));
            }
        }

        public override Segment2D Reversed()
        {
            return new ArcSegment2D(Id, End, Start, Center, Radius, -SweepRad, Role);
        }

        public override (Segment2D First, Segment2D Second) SplitAt(Point2D pt)
        {
            double ptAngle = Math.Atan2(pt.Y - Center.Y, pt.X - Center.X);
            double sweep1 = (ptAngle - StartAngle) % (2 * Math.PI);
            if (SweepRad > 0 && sweep1 < 0) sweep1 += 2 * Math.PI;
            if (SweepRad < 0 && sweep1 > 0) sweep1 -= 2 * Math.PI;
            double sweep2 = SweepRad - sweep1;

            return (
                new ArcSegment2D($"{Id}/a", Start, pt, Center, Radius, sweep1, Role),
                new ArcSegment2D($"{Id}/b", pt, End, Center, Radius, sweep2, Role)
            );
        }

        public override CurveV2 ToCurveV2()
        {
            return new CurveV2(
                id: Id,
                kind: "Arc",
                start: new Point3V2(Start.X, Start.Y, 0),
                end: new Point3V2(End.X, End.Y, 0),
                center: new Point3V2(Center.X, Center.Y, 0),
                normal: new Vector3V2(0, 0, SweepRad >= 0 ? 1 : -1),
                radius_mm: Radius,
                sweep_rad: Math.Abs(SweepRad),
                role: Role
            );
        }

        public override double SignedAreaContribution()
        {
            double chordArea = (Start.X * End.Y - End.X * Start.Y) * 0.5;
            double segArea = 0.5 * Radius * Radius * (SweepRad - Math.Sin(SweepRad));
            return chordArea + segArea;
        }
    }
}
