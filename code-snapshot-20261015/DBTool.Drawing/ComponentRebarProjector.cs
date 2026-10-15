using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Component/face-local silhouettes; physical quantities and canonical marks are retained.</summary>
public static class ComponentRebarProjector
{
    public static OperationResult<IReadOnlyList<DrawingPrimitive>> Build(IEnumerable<BarOccurrence> occurrences,
        IReadOnlyDictionary<string, string> marks, string viewId, string componentId, string? frameId,
        RebarSurface? surface, Layer layer)
    {
        var output = new List<DrawingPrimitive>();
        foreach (var bar in occurrences.Where(b => b.ComponentId == componentId && b.FrameId == frameId &&
                     b.Surface == surface && b.Layer == layer).OrderBy(b => b.Id, StringComparer.Ordinal))
        {
            bool face = bar.ComponentKind is ComponentKind.Wall or ComponentKind.Neck;
            bool alongY = (frameId ?? componentId).EndsWith("T2", StringComparison.Ordinal) ||
                (frameId ?? componentId).EndsWith("T4", StringComparison.Ordinal);
            Point2Mm Project(Point3Mm p) => face ? new(alongY ? p.Y : p.X, p.Z) : new(p.X, p.Y);
            for (int index = 0; index < bar.Path.Segments.Length; index++)
            {
                var segment = bar.Path.Segments[index];
                string id = $"{viewId}/{bar.Id}/{index}";
                if (segment is LineSegment3Mm line)
                {
                    var a = Project(line.Start); var b = Project(line.End);
                    if (a == b)
                        output.Add(new CirclePrimitive(id, "Rebar/Main", viewId, componentId, bar.Id,
                            marks[bar.Id], a, bar.DiameterMm / 2d));
                    else
                        output.Add(new PolylinePrimitive(id, "Rebar/Main", viewId, componentId, bar.Id,
                            marks[bar.Id], [a, b], false, materialKind: DrawingMaterialKind.RoundSteel));
                }
                else if (segment is ArcSegment3Mm arc &&
                    Math.Abs(face ? (alongY ? arc.Normal.X : arc.Normal.Y) : arc.Normal.Z) > 1 - 1e-9)
                {
                    var center = Project(arc.Center);
                    var start = Project(arc.Start);
                    double orientation = face ? (alongY ? arc.Normal.X : -arc.Normal.Y) : arc.Normal.Z;
                    output.Add(new PathPrimitive(id, "Rebar/Main", viewId, componentId, bar.Id, marks[bar.Id],
                        [new ArcPathSegment(center, arc.CenterlineRadiusMm, Math.Atan2(start.Y-center.Y,start.X-center.X),
                            orientation * arc.SweepRadians)], bar.Path.Closed,
                        face ? ViewPurpose.Section : ViewPurpose.Plan, bar.ScheduleLengthMm, DrawingMaterialKind.RoundSteel));
                }
                else if(segment is ArcSegment3Mm edge &&
                    Math.Abs(face?(alongY?edge.Normal.X:edge.Normal.Y):edge.Normal.Z)<1e-9)
                {
                    var u=edge.StartDirection;var n=edge.Normal;
                    var v=new Vector3(n.Y*u.Z-n.Z*u.Y,n.Z*u.X-n.X*u.Z,n.X*u.Y-n.Y*u.X);
                    double ux=face?(alongY?u.Y:u.X):u.X,uy=face?u.Z:u.Y;
                    double vx=face?(alongY?v.Y:v.X):v.X,vy=face?v.Z:v.Y;
                    bool xAxis=Math.Abs(ux)+Math.Abs(vx)>Math.Abs(uy)+Math.Abs(vy);
                    double phase=Math.Atan2(xAxis?vx:vy,xAxis?ux:uy);
                    var angles=new List<double>{0,edge.SweepRadians};
                    for(int k=-3;k<=3;k++)
                    {
                        double angle=phase+k*Math.PI;
                        if(angle>=Math.Min(0,edge.SweepRadians)-1e-9 && angle<=Math.Max(0,edge.SweepRadians)+1e-9)
                            angles.Add(angle);
                    }
                    var center=Project(edge.Center);
                    var points=angles.Select(a=>new Point2Mm(center.X+edge.CenterlineRadiusMm*(ux*Math.Cos(a)+vx*Math.Sin(a)),
                        center.Y+edge.CenterlineRadiusMm*(uy*Math.Cos(a)+vy*Math.Sin(a))))
                        .OrderBy(p=>xAxis?p.X:p.Y).ToArray();
                    var a=points[0];var b=points[^1];
                    output.Add(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<1e-8
                        ?new CirclePrimitive(id,"Rebar/Main",viewId,componentId,bar.Id,marks[bar.Id],a,bar.DiameterMm/2d)
                        :new PolylinePrimitive(id,"Rebar/Main",viewId,componentId,bar.Id,marks[bar.Id],[a,b],false,
                            materialKind:DrawingMaterialKind.RoundSteel));
                }
                else
                    return new(false, null, [new Diagnostic("COMPONENT_REBAR_PROJECTION_UNSUPPORTED",
                        DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, bar.Sources.FirstOrDefault(),
                        "verified line face projection", bar.Id, "Curved face projection remains unresolved; active bar was not omitted.")]);
            }
        }
        return new(true, output, []);
    }
}
