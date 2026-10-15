using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Nominal face-mesh guide axes, separate from the physical equalized bar stations.</summary>
public static class WallMeshGuidePolicy
{
    public const string Role = "Rebar/SheetOneWallGuide";
    public static IReadOnlyList<DrawingPrimitive> Compose(IEnumerable<DrawingPrimitive> primitives, DrawingInputs inputs,
        PresentationRequest presentation)
    {
        var original=primitives.ToArray();
        if(!SheetOnePresentationPolicy.Enabled(presentation) || inputs.WallOuter is null || inputs.WallInner is null ||
            inputs.WallOuter.VerticalSpacingMm!=inputs.WallInner.VerticalSpacingMm) return original;
        var guides=original.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="WallMeshPlan" && p.Role=="Rebar/HiddenSecondary" && p.Points.Length==2).ToArray();
        if(guides.Length==0) return original;
        var walls=inputs.Geometry.Components.Where(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar").ToArray();
        var baseComponent=inputs.Geometry.Components.FirstOrDefault(c=>c.Id=="Base");
        if(walls.Length!=4 || baseComponent is null || walls.Any(w=>w.Footprint.Outer.Vertices.Length!=4 ||
            w.Footprint.Outer.Vertices.Select(p=>p.X).Distinct().Count()!=2 ||
            w.Footprint.Outer.Vertices.Select(p=>p.Y).Distinct().Count()!=2)) return original;
        var vertices=walls.SelectMany(w=>w.Footprint.Outer.Vertices).ToArray();
        var xs=vertices.Select(p=>p.X).Distinct().Order().ToArray();
        var ys=vertices.Select(p=>p.Y).Distinct().Order().ToArray();
        if(xs.Length!=4 || ys.Length!=4) return original;
        var bp=baseComponent.Footprint.Outer.Vertices;
        if(bp.Length!=4 || bp.Select(p=>p.X).Distinct().Count()!=2 || bp.Select(p=>p.Y).Distinct().Count()!=2) return original;
        double cover=inputs.WallOuter.CoverMm;
        double left=bp.Min(p=>p.X)+cover,right=bp.Max(p=>p.X)-cover,
            bottom=bp.Min(p=>p.Y)+cover,top=bp.Max(p=>p.Y)-cover;
        if(left>=right || bottom>=top) return original;
        var xStations=Stations(xs,left,right); var yStations=Stations(ys,bottom,top);
        var result=original.Select(ReplaceGuide).ToList();
        foreach(var x in xStations) result.Add(Guide($"WallMeshPlan/DesignGuide/X/{Array.IndexOf(xStations,x)}",null,null,null,true,x));
        foreach(var y in yStations) result.Add(Guide($"WallMeshPlan/DesignGuide/Y/{Array.IndexOf(yStations,y)}",null,null,null,false,y));
        return result;

        double[] Stations(double[] faces,double min,double max)
        {
            double spacing=inputs.WallInner.VerticalSpacingMm,center=(faces[1]+faces[2])/2;
            int count=(int)Math.Round((faces[2]-faces[1])/spacing,MidpointRounding.AwayFromZero);
            var values=new HashSet<double>{min,max,faces[0]+inputs.WallOuter.CoverMm,faces[3]-inputs.WallOuter.CoverMm,
                faces[1]-inputs.WallInner.CoverMm,faces[2]+inputs.WallInner.CoverMm};
            for(int i=0;i<count;i++) values.Add(center+(i-(count-1)/2d)*spacing);
            return values.Order().ToArray();
        }
        PolylinePrimitive Guide(string id,string? component,string? occurrence,string? mark,bool vertical,double station)
        {
            Point2Mm Point(double x,double y)=>WallMeshPresentation.GraphicPoint(new(x,y));
            return new(id,Role,"WallMeshPlan",component,occurrence,mark,
                vertical?[Point(station,top),Point(station,bottom)]:[Point(left,station),Point(right,station)],false);
        }
        DrawingPrimitive ReplaceGuide(DrawingPrimitive primitive)
        {
            if(primitive is not PolylinePrimitive p || !guides.Contains(p)) return primitive;
            bool vertical=p.Points[0].X==p.Points[1].X;
            if(!vertical && p.Points[0].Y!=p.Points[1].Y) return primitive;
            double station=vertical?p.Points[0].X:p.Points[0].Y-WallMeshPresentation.GraphicOffsetY;
            var axis=vertical?xStations:yStations;
            // Preserve the physical guide's ID/link while associating it with the nearest
            // nominal diagram axis. No physical bar position or schedule is changed.
            double nominal=axis.OrderBy(v=>Math.Abs(v-station)).ThenBy(v=>v).First();
            return Guide(p.SemanticId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,vertical,nominal);
        }
    }
    public static IReadOnlyDictionary<string,string> RenderAliases(IEnumerable<DrawingPrimitive> primitives)
    {
        var first=new Dictionary<(Point2Mm,Point2Mm),string>();
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var p in primitives.OfType<PolylinePrimitive>().Where(p=>p.Role==Role && p.ViewId=="WallMeshPlan" && !p.Closed && p.Points.Length==2))
        {
            var key=(p.Points[0],p.Points[1]);
            if(first.TryGetValue(key,out var representative)) aliases.Add(p.SemanticId,representative);
            else first.Add(key,p.SemanticId);
        }
        return aliases;
    }
}
