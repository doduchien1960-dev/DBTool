using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>First-sheet face-mesh symbols, retaining every occurrence and segment identity.</summary>
public static class WallMeshBandPolicy
{
    public const double BandWidthMm = 36;
    public const string Role = "Rebar/SheetOneWallBand";
    public static IReadOnlyList<DrawingPrimitive> Compose(IEnumerable<DrawingPrimitive> primitives, DrawingInputs inputs,
        PresentationRequest presentation)
    {
        var original = primitives.ToArray();
        if (!SheetOnePresentationPolicy.Enabled(presentation) || inputs.WallOuter is null || inputs.WallInner is null) return original;
        var walls=inputs.Geometry.Components.Where(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar").ToArray();
        if(walls.Length!=4 || walls.Any(w=>w.Footprint.Outer.Vertices.Length!=4 ||
            w.Footprint.Outer.Vertices.Select(p=>p.X).Distinct().Count()!=2 ||
            w.Footprint.Outer.Vertices.Select(p=>p.Y).Distinct().Count()!=2)) return original;
        var vertices=walls.SelectMany(w=>w.Footprint.Outer.Vertices).ToArray();
        var xs=vertices.Select(p=>p.X).Distinct().Order().ToArray();
        var ys=vertices.Select(p=>p.Y).Distinct().Order().ToArray();
        if(xs.Length!=4 || ys.Length!=4) return original;
        double cx=(xs[0]+xs[3])/2, cy=(ys[0]+ys[3])/2, half=BandWidthMm/2;
        var bars=inputs.RebarLayout.Occurrences.ToDictionary(b=>b.Id,StringComparer.Ordinal);
        var byId=walls.ToDictionary(w=>w.Id,StringComparer.Ordinal);
        return original.Select(ComposePrimitive).ToArray();

        DrawingPrimitive ComposePrimitive(DrawingPrimitive primitive)
        {
            if(primitive is not PolylinePrimitive { ViewId:"WallMeshPlan", Role:"Rebar/Hidden" } p ||
                p.OccurrenceId is not { } occurrenceId || !bars.TryGetValue(occurrenceId,out var bar) || bar.Direction!=RebarDirection.Horizontal ||
                bar.Surface is null || !byId.TryGetValue(bar.ComponentId,out var wall)) return primitive;
            bool outer=bar.Surface==RebarSurface.Outer;
            var spec=outer?inputs.WallOuter:inputs.WallInner;
            var points=wall.Footprint.Outer.Vertices;
            double x0=points.Min(v=>v.X),x1=points.Max(v=>v.X),y0=points.Min(v=>v.Y),y1=points.Max(v=>v.Y);
            bool horizontal=x1-x0>y1-y0;
            double left,right,bottom,top;
            if(horizontal)
            {
                double extent=outer?(xs[3]-xs[0])/2-spec.CoverMm:(xs[2]-xs[1])/2+spec.CoverMm;
                bool positive=(y0+y1)/2>cy;
                double face=outer?(positive?y1-spec.CoverMm:y0+spec.CoverMm)
                    :(positive?y0+spec.CoverMm:y1-spec.CoverMm);
                left=cx-extent-half;right=cx+extent+half;bottom=face-half;top=face+half;
            }
            else
            {
                bool positive=(x0+x1)/2>cx;
                double face=outer?(positive?x1-spec.CoverMm:x0+spec.CoverMm)
                    :(positive?x0+spec.CoverMm:x1-spec.CoverMm);
                // Source G400/G500/G600: outer returns reach the inner horizontal face;
                // inner returns end at the first/last stations of the nominal clear-span grid.
                double count=Math.Round((ys[2]-ys[1])/spec.VerticalSpacingMm,MidpointRounding.AwayFromZero);
                double extent=outer?(ys[2]-ys[1])/2+inputs.WallInner.CoverMm:(count-1)*spec.VerticalSpacingMm/2;
                if(extent<=0) return primitive;
                left=face-half;right=face+half;bottom=cy-extent-half;top=cy+extent+half;
            }
            Point2Mm Point(double x,double y)=>WallMeshPresentation.GraphicPoint(new(x,y));
            return new PolylinePrimitive(p.SemanticId,Role,p.ViewId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,
                [Point(left,top),Point(right,top),Point(right,bottom),Point(left,bottom)],true,
                materialKind:p.MaterialKind);
        }
    }

    public static IReadOnlyDictionary<string,string> RenderAliases(IEnumerable<DrawingPrimitive> primitives)
    {
        var first=new Dictionary<(string?,Point2Mm,Point2Mm,Point2Mm,Point2Mm),string>();
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var p in primitives.OfType<PolylinePrimitive>().Where(p=>p.Role==Role && p.ViewId=="WallMeshPlan" && p.Closed && p.Points.Length==4))
        {
            var key=(p.ComponentId,p.Points[0],p.Points[1],p.Points[2],p.Points[3]);
            if(first.TryGetValue(key,out var representative)) aliases.Add(p.SemanticId,representative);
            else first.Add(key,p.SemanticId);
        }
        return aliases;
    }
}
