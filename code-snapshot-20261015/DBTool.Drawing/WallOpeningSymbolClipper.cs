using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Display clipping against every physical cutout. Native strokes are measured in native-leader-symbols.json.</summary>
public static class WallOpeningSymbolClipper
{
    public static OperationResult<IReadOnlyList<PolylinePrimitive>> ClipMesh(IEnumerable<PolylinePrimitive> bars,
        GeometryModel geometry,string wallId)
    {
        var openings=geometry.Openings.Where(o=>o.OwnerId==wallId || o.OwnerId=="Wall/"+wallId || "Wall/"+o.OwnerId==wallId).ToArray();
        var result=new List<PolylinePrimitive>();
        foreach(var bar in bars)
        {
            if(openings.Length==0) {result.Add(bar);continue;}
            if(bar.Bulges.Any(b=>Math.Abs(b)>1e-9)) return Fail("OPENING_MESH_CLIP_CURVE_UNSUPPORTED",bar.SemanticId);
            var pieces=new List<(Point2Mm A,Point2Mm B)>();
            int count=bar.Closed?bar.Points.Length:bar.Points.Length-1;
            for(int i=0;i<count;i++)
            {
                var a=bar.Points[i];var b=bar.Points[(i+1)%bar.Points.Length];
                var cuts=new SortedSet<double>{0,1};
                var profiles=new List<(Point2Mm A,Point2Mm B,Profile2Mm Profile)>();
                foreach(var opening in openings)
                {
                    if(opening.ConcreteCutout is not (CircleProfile or RectangleProfile or PolygonProfile or MultiCellProfile or OpenTopProfile))
                        return Fail("OPENING_MESH_CLIP_PROFILE_UNSUPPORTED",opening.Id);
                    var frame=opening.Frame;
                    bool alongY=wallId.EndsWith("T2",StringComparison.Ordinal)||wallId.EndsWith("T4",StringComparison.Ordinal);
                    double originX=alongY?frame.Origin.Y:frame.Origin.X;
                    double ux=alongY?frame.U.Y:frame.U.X,vx=alongY?frame.V.Y:frame.V.X;
                    double determinant=ux*frame.V.Z-vx*frame.U.Z;
                    if(Math.Abs(determinant)<1e-9) return Fail("OPENING_MESH_CLIP_FRAME_UNSUPPORTED",opening.Id);
                    Point2Mm Local(Point2Mm p)=>new(((p.X-originX)*frame.V.Z-vx*(p.Y-frame.Origin.Z))/determinant,
                        (ux*(p.Y-frame.Origin.Z)-(p.X-originX)*frame.U.Z)/determinant);
                    var localA=Local(a);var localB=Local(b);
                    profiles.Add((localA,localB,opening.ConcreteCutout));
                    foreach(double t in Crossings(localA,localB,opening.ConcreteCutout)) if(t>0 && t<1) cuts.Add(t);
                }
                Point2Mm At(double t)=>new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y));
                var parameters=cuts.ToArray();
                for(int j=1;j<parameters.Length;j++)
                {
                    double t=(parameters[j-1]+parameters[j])/2;
                    if(profiles.Any(p=> {var point=new Point2Mm(p.A.X+t*(p.B.X-p.A.X),p.A.Y+t*(p.B.Y-p.A.Y));return Intersects(point,point,p.Profile);})) continue;
                    var start=At(parameters[j-1]);var end=At(parameters[j]);
                    if(Math.Abs(end.X-start.X)+Math.Abs(end.Y-start.Y)>1e-8) pieces.Add((start,end));
                }
            }
            if(pieces.Count==count && pieces.Select((p,i)=>p.A==bar.Points[i] && p.B==bar.Points[(i+1)%bar.Points.Length]).All(x=>x))
            {result.Add(bar);continue;}
            for(int i=0;i<pieces.Count;i++) result.Add(new PolylinePrimitive(bar.SemanticId+"/Visible/"+i,bar.Role,bar.ViewId,
                bar.ComponentId,bar.OccurrenceId,bar.ScheduleMark,[pieces[i].A,pieces[i].B],false,materialKind:bar.MaterialKind));
        }
        return new(true,result,[]);
        OperationResult<IReadOnlyList<PolylinePrimitive>> Fail(string code,string actual)=>new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
                "supported contour, projected frame and straight display segments",wallId+":"+actual,"Physical bars and schedule are unchanged; no contour approximation was used.")]);
    }

    private static IEnumerable<double> Crossings(Point2Mm a,Point2Mm b,Profile2Mm profile)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y;
        if(profile is CircleProfile circle)
        {
            double x=a.X-circle.Center.X,y=a.Y-circle.Center.Y,aa=dx*dx+dy*dy;
            if(aa<1e-16) yield break;
            double bb=2*(x*dx+y*dy),cc=x*x+y*y-circle.RadiusMm*circle.RadiusMm,discriminant=bb*bb-4*aa*cc;
            if(discriminant>=0) {yield return (-bb-Math.Sqrt(discriminant))/(2*aa);yield return (-bb+Math.Sqrt(discriminant))/(2*aa);}
            yield break;
        }
        if(profile is MultiCellProfile cells)
        {foreach(var cell in cells.Cells) foreach(var t in Crossings(a,b,cell)) yield return t;yield break;}
        var points=Vertices(profile);
        for(int i=0;i<points.Length;i++)
        {
            var c=points[i];var d=points[(i+1)%points.Length];double ex=d.X-c.X,ey=d.Y-c.Y,det=dx*ey-dy*ex;
            if(Math.Abs(det)<1e-12) continue;
            double t=((c.X-a.X)*ey-(c.Y-a.Y)*ex)/det,u=((c.X-a.X)*dy-(c.Y-a.Y)*dx)/det;
            if(u>=0 && u<=1) yield return t;
        }
    }

    private static Point2Mm[] Vertices(Profile2Mm profile)=>profile switch
    {
        RectangleProfile r=>[new(r.Rectangle.MinX,r.Rectangle.MinY),new(r.Rectangle.MaxX,r.Rectangle.MinY),
            new(r.Rectangle.MaxX,r.Rectangle.MaxY),new(r.Rectangle.MinX,r.Rectangle.MaxY)],
        PolygonProfile p=>p.Polygon.Vertices.ToArray(),OpenTopProfile p=>p.Polygon.Vertices.ToArray(),_=>[]
    };

    public static OperationResult<IReadOnlyList<BlockPrimitive>> Clip(IEnumerable<BlockPrimitive> symbols,
        GeometryModel geometry,string wallId)
    {
        var openings=geometry.Openings.Where(o=>o.OwnerId==wallId || o.OwnerId=="Wall/"+wallId ||
            "Wall/"+o.OwnerId==wallId).ToArray();
        var retained=new List<BlockPrimitive>();
        foreach(var symbol in symbols)
        {
            if(symbol.BlockRole is not ("Block:BLTC" or "Block:Gach")) {retained.Add(symbol);continue;}
            bool overlaps=false;
            foreach(var opening in openings)
            {
                if(opening.ConcreteCutout is not (CircleProfile or RectangleProfile or PolygonProfile or MultiCellProfile or OpenTopProfile))
                    return Fail("OPENING_SYMBOL_CLIP_PROFILE_UNSUPPORTED",opening);
                var frame=opening.Frame;
                bool alongY=wallId.EndsWith("T2",StringComparison.Ordinal) || wallId.EndsWith("T4",StringComparison.Ordinal);
                bool plan=wallId is "Base" or "Roof";
                double originX=alongY?frame.Origin.Y:frame.Origin.X;
                double ux=alongY?frame.U.Y:frame.U.X,vx=alongY?frame.V.Y:frame.V.X;
                double uy=plan?frame.U.Y:frame.U.Z,vy=plan?frame.V.Y:frame.V.Z;
                double originY=plan?frame.Origin.Y:frame.Origin.Z;
                double determinant=ux*vy-vx*uy;
                if(Math.Abs(determinant)<1e-9) return Fail("OPENING_SYMBOL_CLIP_FRAME_UNSUPPORTED",opening);
                Point2Mm Local(Point2Mm p)
                {
                    double x=p.X-originX,y=p.Y-originY;
                    return new((x*vy-vx*y)/determinant,(ux*y-x*uy)/determinant);
                }
                double half=symbol.BlockRole=="Block:BLTC"?100:75;
                var strokes=new List<(Point2Mm A,Point2Mm B)>{(new(-half,-half),new(half,half))};
                if(symbol.BlockRole=="Block:BLTC") strokes.Add((new(half,-half),new(-half,half)));
                foreach(var stroke in strokes)
                {
                    Point2Mm Place(Point2Mm p)
                    {
                        double angle=symbol.RotationRadians;
                        double x=symbol.MirrorHorizontal?-p.X:p.X;
                        return new(symbol.Insertion.X+symbol.AnnotationScale*(x*Math.Cos(angle)-p.Y*Math.Sin(angle)),
                            symbol.Insertion.Y+symbol.AnnotationScale*(x*Math.Sin(angle)+p.Y*Math.Cos(angle)));
                    }
                    if(Intersects(Local(Place(stroke.A)),Local(Place(stroke.B)),opening.ConcreteCutout))
                    {overlaps=true;break;}
                }
                if(overlaps) break;
            }
            if(!overlaps) retained.Add(symbol);
        }
        return new(true,retained,[]);
        OperationResult<IReadOnlyList<BlockPrimitive>> Fail(string code,OpeningProfile opening)=>new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,opening.Sources.FirstOrDefault(),
                "supported physical contour and wall projection frame",wallId+":"+opening.Id,"Symbol clipping cannot substitute a bounding circle.")]);
    }

    private static bool Intersects(Point2Mm a,Point2Mm b,Profile2Mm profile)
    {
        if(profile is CircleProfile circle)
        {
            double dx=b.X-a.X,dy=b.Y-a.Y,length2=dx*dx+dy*dy;
            double t=length2==0?0:Math.Clamp(((circle.Center.X-a.X)*dx+(circle.Center.Y-a.Y)*dy)/length2,0,1);
            double x=a.X+t*dx-circle.Center.X,y=a.Y+t*dy-circle.Center.Y;
            return x*x+y*y<=circle.RadiusMm*circle.RadiusMm+1e-8;
        }
        if(profile is MultiCellProfile cells) return cells.Cells.Any(c=>Intersects(a,b,c));
        var vertices=Vertices(profile);
        bool Inside(Point2Mm p)
        {
            bool inside=false;
            for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
            { var u=vertices[i];var v=vertices[j];
                if((u.Y>p.Y)!=(v.Y>p.Y) && p.X<(v.X-u.X)*(p.Y-u.Y)/(v.Y-u.Y)+u.X) inside=!inside; }
            return inside;
        }
        if(Inside(a)||Inside(b)) return true;
        for(int i=0;i<vertices.Length;i++) if(Crosses(a,b,vertices[i],vertices[(i+1)%vertices.Length])) return true;
        return false;
    }

    private static bool Crosses(Point2Mm a,Point2Mm b,Point2Mm c,Point2Mm d)
    {
        double Cross(Point2Mm p,Point2Mm q,Point2Mm r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X);
        double x=Cross(a,b,c),y=Cross(a,b,d),z=Cross(c,d,a),w=Cross(c,d,b);
        if(Math.Max(a.X,b.X)<Math.Min(c.X,d.X)-1e-8 || Math.Max(c.X,d.X)<Math.Min(a.X,b.X)-1e-8 ||
            Math.Max(a.Y,b.Y)<Math.Min(c.Y,d.Y)-1e-8 || Math.Max(c.Y,d.Y)<Math.Min(a.Y,b.Y)-1e-8) return false;
        return x*y<=1e-8 && z*w<=1e-8;
    }
}
