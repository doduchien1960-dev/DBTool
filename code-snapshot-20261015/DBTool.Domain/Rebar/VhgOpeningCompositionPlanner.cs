using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>HDSD 2.3.13/14: one Bo per face and four side bars per selected set.
/// Multiple parallel sets require placement evidence; no spacing is inferred from diameter.</summary>
public static class VhgOpeningCompositionPlanner
{
    public const string RuleId="VHG-OPENING-COMPOSITION-1C-v1";
    public static bool CanConsume(VhgOpeningInput? input) => input is not null &&
        (input.Composition==VhgOpeningComposition.Bo || input.Count<=1);

    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec,GeometryModel geometry,bool roof)
    {
        var main=spec.Input.MainRebar!;
        var input=roof?main.Accessories?.Roof:main.Accessories?.Pipe;
        if(input is null) return new(true,new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.Disabled),[]);
        OperationResult<RebarLayout> Block(string code,string message)=>new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Manhole,spec.Input.Id,null,input.Sources.FirstOrDefault(),
                "resolved physical opening placement",input.Composition.ToString(),message)]);
        if(!CanConsume(input)) return Block("OPENING_PARALLEL_PLACEMENT_UNRESOLVED","Parallel-set spacing is absent from the VHG input contract.");
        var openings=geometry.Openings.Where(o=>roof?o.Id=="Roof/Hole":o.WallFrame is not null && o.OwnerId.StartsWith("Wall/",StringComparison.Ordinal)).ToArray();
        if(openings.Length==0) return Block("ACCESSORY_OPENING_REQUIRED","Active reinforcement needs its registered physical opening.");
        var bars=new List<BarOccurrence>();
        foreach(var opening in openings)
        {
            var owner=geometry.Components.Single(c=>c.Id==opening.OwnerId);
            if(geometry.Openings.Any(o=>o.OwnerId==owner.Id && o.Id!=opening.Id))
                return Block("OPENING_NEIGHBOR_CLEARANCE_UNRESOLVED","Additional owner cutouts require joint perimeter/extension placement; no neighboring void may be ignored.");
            Rectangle2Mm host;
            var frames=new List<(Layer Layer,string Id,Frame3Mm Frame)>();
            double q=input.Cover+input.Diameter/2d;
            if(roof)
            {
                var points=owner.Footprint.Outer.Vertices;
                int faceCount=(main.RoofBottom is null?0:1)+(main.RoofTop is null?0:1);
                if(owner.ZRange.Length<2*main.RoofCoverMm+Math.Max(1,faceCount)*input.Diameter)
                    return Block("OPENING_FACE_CLEARANCE_INVALID","Accessory diameter and face covers cannot fit the roof thickness.");
                if(points.Max(p=>p.X)-points.Min(p=>p.X)<=2*q || points.Max(p=>p.Y)-points.Min(p=>p.Y)<=2*q)
                    return Block("OPENING_REBAR_OUTSIDE_CONCRETE","Accessory edge cover excludes the roof domain.");
                host=new(points.Min(p=>p.X)+q,points.Min(p=>p.Y)+q,points.Max(p=>p.X)-q,points.Max(p=>p.Y)-q);
                void Face(Layer layer,double z)=>frames.Add((layer,layer.ToString(),new(new(0,0,z),new(1,0,0),new(0,1,0),new(0,0,1))));
                if(main.RoofBottom is not null)Face(Layer.Bottom,owner.ZRange.Start+main.RoofCoverMm+input.Diameter/2d);
                if(main.RoofTop is not null)Face(Layer.Top,owner.ZRange.End-main.RoofCoverMm-input.Diameter/2d);
            }
            else
            {
                var wall=opening.WallFrame!;
                var stations=owner.Footprint.Outer.Vertices.Select(p=>wall.ToLocal(new(p.X,p.Y,0)).U).ToArray();
                var faces=new[]{main.WallOuter,main.WallInner}.Where(f=>f is not null).Select(f=>f!).ToArray();
                double depth=faces.Length==1?2*faces[0].CoverMm+input.Diameter:faces.Sum(f=>f.CoverMm+input.Diameter);
                if(spec.Input.WallThicknessMm<depth)
                    return Block("OPENING_FACE_CLEARANCE_INVALID","Accessory diameter and supported faces cannot fit the wall thickness.");
                if(stations.Max()-stations.Min()<=2*q || owner.ZRange.Length<=2*q)
                    return Block("OPENING_REBAR_OUTSIDE_CONCRETE","Accessory edge cover excludes the wall domain.");
                host=new(stations.Min()+q,owner.ZRange.Start+q,stations.Max()-q,owner.ZRange.End-q);
                void Face(WallOrNeckRebarSpec face,bool outer)
                {
                    double v=outer?spec.Input.WallThicknessMm-face.CoverMm-input.Diameter/2d:face.CoverMm+input.Diameter/2d;
                    frames.Add((outer?Layer.Top:Layer.Bottom,outer?"Outer":"Inner",new(wall.ToGlobal(0,v,0),wall.Left,wall.Up,wall.Outward)));
                }
                if(main.WallOuter is not null)Face(main.WallOuter,true);
                if(main.WallInner is not null)Face(main.WallInner,false);
            }
            if(frames.Count==0)return Block("OPENING_LAYER_REQUIRED","Active opening reinforcement has no supported main face.");
            Rectangle2Mm boundary;
            CircleProfile? circle=opening.ConcreteCutout as CircleProfile;
            if(circle is not null)boundary=new(circle.Center.X-circle.RadiusMm-q,circle.Center.Y-circle.RadiusMm-q,circle.Center.X+circle.RadiusMm+q,circle.Center.Y+circle.RadiusMm+q);
            else if(opening.ConcreteCutout is RectangleProfile rectangle)
                boundary=new(rectangle.Rectangle.MinX-q,rectangle.Rectangle.MinY-q,rectangle.Rectangle.MaxX+q,rectangle.Rectangle.MaxY+q);
            else return Block("OPENING_PROFILE_UNSUPPORTED","Composition requires a circle or rectangular physical cutout.");
            if(boundary.MinX<host.MinX || boundary.MaxX>host.MaxX || boundary.MinY<host.MinY || boundary.MaxY>host.MaxY)
                return Block("OPENING_REBAR_OUTSIDE_CONCRETE","Opening perimeter does not fit the concrete cover domain.");
            if(circle is null && input.Composition is VhgOpeningComposition.Diagonal or VhgOpeningComposition.BoDiagonal)
                return Block("OPENING_COMPOSITION_PROFILE_MISMATCH","Diagonal composition requires a round opening.");
            foreach(var face in frames)
            {
                Point3Mm World(double x,double y)=>new(face.Frame.Origin.X+x*face.Frame.U.X+y*face.Frame.V.X,
                    face.Frame.Origin.Y+x*face.Frame.U.Y+y*face.Frame.V.Y,face.Frame.Origin.Z+x*face.Frame.U.Z+y*face.Frame.V.Z);
                void Add(string family,BarPath path,double length,double? fabrication)
                {
                    bars.Add(new($"{opening.Id}/Accessory/{face.Id}/{family}",null,owner.Id,owner.Kind,face.Layer,Axis.X,input.Diameter,
                        path,length,fabrication,fabrication.HasValue?LengthStatus.Known:LengthStatus.NeedsRule,RuleId,
                        input.Sources.Concat(opening.Sources).Concat(owner.Sources),RebarDirection.Supplementary,face.Id,
                        roof?null:face.Id=="Inner"?RebarSurface.Inner:RebarSurface.Outer,"OpeningReinforcement",MeasurementBasisKind.Nominal));
                }
                if(input.Composition is VhgOpeningComposition.Bo or VhgOpeningComposition.BoSquare or VhgOpeningComposition.BoDiagonal)
                {
                    double lap=input.LapMultiple*input.Diameter;
                    if(circle is not null)
                    {
                        double radius=circle.RadiusMm+q,length=2*Math.PI*radius+lap;
                        Add("Bo",new([new ArcSegment3Mm(World(circle.Center.X,circle.Center.Y),radius,face.Frame.N,face.Frame.U,2*Math.PI)],true,HookPattern.Hook0M,lap,false,MeasurementBasis.TangentPath),length,length);
                    }
                    else
                    {
                        Point3Mm[] points=[World(boundary.MinX,boundary.MinY),World(boundary.MaxX,boundary.MinY),World(boundary.MaxX,boundary.MaxY),World(boundary.MinX,boundary.MaxY)];
                        var segments=Enumerable.Range(0,4).Select(i=>(PathSegment)new LineSegment3Mm(points[i],points[(i+1)%4]));
                        double length=2*(boundary.MaxX-boundary.MinX+boundary.MaxY-boundary.MinY)+lap;
                        var path=new BarPath(segments,true,HookPattern.Hook0M,lap,false,MeasurementBasis.NominalPolyline);
                        Add("Bo",path,length,NominalFabricationLength.Calculate(path,input.Diameter));
                    }
                }
                if(input.Composition!=VhgOpeningComposition.Bo && input.Count>0)
                {
                    double cx=(boundary.MinX+boundary.MaxX)/2,cy=(boundary.MinY+boundary.MaxY)/2;
                    double rx=(boundary.MaxX-boundary.MinX)/2,ry=(boundary.MaxY-boundary.MinY)/2;
                    bool diagonal=input.Composition is VhgOpeningComposition.Diagonal or VhgOpeningComposition.BoDiagonal;
                    for(int side=0;side<4;side++)
                    {
                        double angle=(side+(diagonal?0.5:0))*Math.PI/2,nx=Math.Cos(angle),ny=Math.Sin(angle),tx=-ny,ty=nx;
                        double radial=diagonal?rx:side%2==0?rx:ry;
                        double x=cx+nx*radial,y=cy+ny*radial;
                        double half=(side%2==0?ry:rx)+input.Extension;
                        double lo=-half,hi=half;
                        void Clip(double c,double t,double min,double max)
                        {if(Math.Abs(t)<1e-12)return;double a=(min-c)/t,b=(max-c)/t;lo=Math.Max(lo,Math.Min(a,b));hi=Math.Min(hi,Math.Max(a,b));}
                        Clip(x,tx,host.MinX,host.MaxX);Clip(y,ty,host.MinY,host.MaxY);
                        if(hi<=lo)return Block("OPENING_EXTENSION_EXCLUDES_BAR","Clipped extension leaves no physical side bar.");
                        var path=new BarPath([new LineSegment3Mm(World(x+tx*lo,y+ty*lo),World(x+tx*hi,y+ty*hi))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
                        Add($"{(diagonal?"Diagonal":"Square")}/{side}",path,hi-lo,hi-lo);
                    }
                }
            }
        }
        bool unresolved=bars.Any(b=>b.LengthStatus==LengthStatus.NeedsRule);
        var diagnostics=bars.Any(b=>b.Path.MeasurementBasis==MeasurementBasis.NominalPolyline)?new[]{new Diagnostic(
            unresolved?"OPENING_FABRICATION_NEEDS_RULE":"OPENING_FABRICATION_FALLBACK",DiagnosticSeverity.Review,
            DiagnosticScope.Manhole,spec.Input.Id,null,input.Sources.FirstOrDefault(),"nominal perimeter / separate fabrication",null,
            "Nominal rectangular Bo includes one lap; internal fabrication uses the Phase1B owner fallback and remains for review.")}:Array.Empty<Diagnostic>();
        return new(true,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,unresolved?LayoutStatus.Partial:LayoutStatus.Complete),diagnostics);
    }
}
