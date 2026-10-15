using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>End-on first-sheet symbols for one opening-clipped parent, retaining every physical fragment link.</summary>
public static class SheetOneContinuousWallDots
{
    public const string Role="Rebar/SheetOneContinuousWallDot";
    public static IReadOnlyList<DrawingPrimitive> Build(RebarLayout layout,IReadOnlyDictionary<string,string> marks,ViewProfile? section)
    {
        if(section is null || (section.Id!="AA" && section.Id!="BB")) return [];
        bool isAA = section.Id == "AA";
        string viewId = isAA ? "SteelAA" : "SteelBB";
        var result=new List<DrawingPrimitive>();
        var parents=layout.Occurrences.Where(b=>b.ComponentKind==ComponentKind.Wall && b.Direction==RebarDirection.Horizontal && b.ParentId is not null)
            .GroupBy(b=>(b.ParentId,b.ComponentId,b.Layer,b.Axis,b.DiameterMm,b.FrameId,b.Surface,b.FamilyId));
        foreach(var parent in parents)
        {
            var fragments=parent.OrderBy(b=>b.Id,StringComparer.Ordinal).ToArray();
            if(fragments.Length<2) continue;
            var wires=fragments.SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>().Select(s=>(Bar:b,Line:s)))
                .Where(x=>isAA
                    ? Math.Abs(x.Line.Start.X-x.Line.End.X)>1e-8 && Near(x.Line.Start.Y,x.Line.End.Y) && Near(x.Line.Start.Z,x.Line.End.Z)
                    : Math.Abs(x.Line.Start.Y-x.Line.End.Y)>1e-8 && Near(x.Line.Start.X,x.Line.End.X) && Near(x.Line.Start.Z,x.Line.End.Z)).ToArray();
            if(wires.Select(x=>x.Bar.Id).Distinct().Count()!=fragments.Length) continue;
            var anchor=isAA
                ? new Point2Mm(wires[0].Line.Start.Y,wires[0].Line.Start.Z)
                : new Point2Mm(wires[0].Line.Start.X,wires[0].Line.Start.Z);
            if(isAA
                ? wires.Any(x=>!Near(x.Line.Start.Y,anchor.X) || !Near(x.Line.Start.Z,anchor.Y))
                : wires.Any(x=>!Near(x.Line.Start.X,anchor.X) || !Near(x.Line.Start.Z,anchor.Y))) continue;
            var intervals=wires.Select(x=>isAA
                ? (Min:Math.Min(x.Line.Start.X,x.Line.End.X),Max:Math.Max(x.Line.Start.X,x.Line.End.X))
                : (Min:Math.Min(x.Line.Start.Y,x.Line.End.Y),Max:Math.Max(x.Line.Start.Y,x.Line.End.Y))).ToArray();
            // An actual crossing already has a dot. Only bridge a clipped parent
            // with physical fragments on both sides of the first-sheet section plane.
            if(intervals.Any(i=>i.Min<=0 && i.Max>=0) || !intervals.Any(i=>i.Max<0) || !intervals.Any(i=>i.Min>0)) continue;
            var material=section.Regions.Where(r=>r.SourceComponentId==parent.Key.ComponentId).Select(r=>r.Footprint);
            if(SectionRebarClipper.Clip(anchor,anchor,material).Count==0) continue;
            // No physical fragment crosses the plane: this is an opening gap,
            // never a reason to synthesize a reinforcing bar inside that gap.
            continue;
        }
        return result;
    }
    public static IReadOnlyDictionary<string,string> RenderAliases(IEnumerable<DrawingPrimitive> primitives)
    {
        var first=new Dictionary<(string,string?,Point2Mm),string>();
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var p in primitives.OfType<CirclePrimitive>().Where(p=>p.Role==Role && (p.ViewId=="SteelAA" || p.ViewId=="SteelBB")))
        {
            var key=(p.ViewId,p.ComponentId,p.Center);
            if(first.TryGetValue(key,out var representative)) aliases.Add(p.SemanticId,representative);
            else first.Add(key,p.SemanticId);
        }
        return aliases;
    }
    private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-8;
}
