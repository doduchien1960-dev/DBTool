using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Physical XY projection of straight bar legs, including end-on diameter symbols.</summary>
public static class PlanBarProjector
{
    public static OperationResult<IReadOnlyList<DrawingPrimitive>> Build(BarOccurrence bar, string viewId, string role, string mark)
    {
        var result = new List<DrawingPrimitive>();
        for (int i = 0; i < bar.Path.Segments.Length; i++)
        {
            string id = $"{viewId}/Bar/{bar.Id}/Leg/{i}";
            if(bar.Path.Segments[i] is ArcSegment3Mm arc)
            {
                if(Math.Abs(arc.Normal.Z)>1-1e-9)
                {
                    result.Add(new PathPrimitive(id,role,viewId,bar.ComponentId,bar.Id,mark,
                        [new ArcPathSegment(new(arc.Center.X,arc.Center.Y),arc.CenterlineRadiusMm,
                            Math.Atan2(arc.Start.Y-arc.Center.Y,arc.Start.X-arc.Center.X),arc.SweepRadians*Math.Sign(arc.Normal.Z))],
                        false,ViewPurpose.Plan,bar.ScheduleLengthMm,DrawingMaterialKind.RoundSteel));
                    continue;
                }
                if(Math.Abs(arc.Normal.Z)<1e-9)
                {
                    var u=arc.StartDirection;var n=arc.Normal;
                    var v=new Vector3(n.Y*u.Z-n.Z*u.Y,n.Z*u.X-n.X*u.Z,n.X*u.Y-n.Y*u.X);
                    bool xAxis=Math.Abs(u.X)+Math.Abs(v.X)>Math.Abs(u.Y)+Math.Abs(v.Y);
                    double phase=Math.Atan2(xAxis?v.X:v.Y,xAxis?u.X:u.Y);
                    var angles=new List<double>{0,arc.SweepRadians};
                    for(int k=-3;k<=3;k++)
                    {
                        double angle=phase+k*Math.PI;
                        if(angle>=Math.Min(0,arc.SweepRadians)-1e-9 && angle<=Math.Max(0,arc.SweepRadians)+1e-9) angles.Add(angle);
                    }
                    var points=angles.Select(a=>new Point2Mm(arc.Center.X+arc.CenterlineRadiusMm*(u.X*Math.Cos(a)+v.X*Math.Sin(a)),
                        arc.Center.Y+arc.CenterlineRadiusMm*(u.Y*Math.Cos(a)+v.Y*Math.Sin(a))))
                        .OrderBy(p=>xAxis?p.X:p.Y).ToArray();
                    var a=points[0];var b=points[^1];
                    result.Add(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<1e-8
                        ? new CirclePrimitive(id,role,viewId,bar.ComponentId,bar.Id,mark,a,bar.DiameterMm/2d)
                        : new PolylinePrimitive(id,role,viewId,bar.ComponentId,bar.Id,mark,[a,b],false,materialKind:DrawingMaterialKind.RoundSteel));
                    continue;
                }
            }
            if (bar.Path.Segments[i] is not LineSegment3Mm line)
                return new(false, null, [new Diagnostic("BASE_BAR_PROJECTION_UNSUPPORTED", DiagnosticSeverity.Block,
                    DiagnosticScope.Global, null, null, bar.Sources.FirstOrDefault(),
                    "straight, plan-plane or edge-on arc XY projection", bar.Id, "Tilted circular bar requires an exact elliptic projection.")]);
            var start = new Point2Mm(line.Start.X, line.Start.Y);
            var end = new Point2Mm(line.End.X, line.End.Y);
            result.Add(start == end
                ? new CirclePrimitive(id, role, viewId, bar.ComponentId, bar.Id, mark, start, bar.DiameterMm / 2)
                : new PolylinePrimitive(id, role, viewId, bar.ComponentId, bar.Id, mark, [start, end], false,
                    materialKind: DrawingMaterialKind.RoundSteel));
        }
        return new(true, result.AsReadOnly(), []);
    }
}
