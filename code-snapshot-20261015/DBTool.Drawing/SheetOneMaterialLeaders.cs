using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Sheet-one material leaders from explicit source labels, not inferred concrete grades.</summary>
public static class SheetOneMaterialLeaders
{
    public static IReadOnlyList<DrawingPrimitive> Build(DrawingPackage package, IReadOnlyDictionary<string,string> labels)
    {
        var result=new List<DrawingPrimitive>();
        foreach(string view in new[]{"AA","BB","SteelAA","SteelBB"})
        foreach(var entry in labels)
        {
            if(entry.Key=="Pipe/Grout")
            {
                foreach(var grout in package.Primitives.OfType<HatchPrimitive>().Where(p=>p.ViewId==view &&
                    p.Role=="Material/Mortar" && p.SemanticId.EndsWith("/Grout/1",StringComparison.Ordinal)))
                {
                    double min=grout.OuterLoop.Min(p=>p.X),max=grout.OuterLoop.Max(p=>p.X);
                    double lower=grout.OuterLoop.Min(p=>p.Y),upper=grout.OuterLoop.Max(p=>p.Y);
                    int direction=Math.Sign((min+max)/2);
                    var anchor=new Point2Mm((min+max)/2,(lower+upper)/2);
                    var groutElbow=new Point2Mm((direction<0?min:max)+direction*30,lower+170);
                    var groutEnd=new Point2Mm(groutElbow.X+direction*entry.Value.Length*36,groutElbow.Y);
                    string groutId=$"{view}/MaterialLeader/PipeGrout/{grout.SemanticId}";
                    result.Add(new PolylinePrimitive(groutId,"Material/Leader",view,grout.ComponentId,null,null,[anchor,groutElbow,groutEnd],false));
                    result.Add(new TextPrimitive(groutId+"/Text","DimensionText",view,grout.ComponentId,null,null,
                        new(direction<0?groutEnd.X:groutElbow.X,groutElbow.Y+20),entry.Value,60,0));
                }
                continue;
            }
            bool steel=view.StartsWith("Steel",StringComparison.Ordinal);
            if(steel && entry.Key!="Neck" || !steel && entry.Key=="Neck") continue;
            var outlines=package.Primitives.OfType<PolylinePrimitive>()
                .Where(p=>p.ViewId==view && p.ComponentId==entry.Key && MaterialOutline(p)).ToArray();
            if(steel)
            {
                if(outlines.Length==0) continue;
                // Explicit grade shares the reinforced-neck callout; never infer a grade from a material kind.
                if(!package.Primitives.OfType<PolylinePrimitive>().Any(p=>p.ViewId==view &&
                    p.SemanticId.StartsWith(view+"/Callout/Neck",StringComparison.Ordinal) && p.SemanticId.EndsWith("/Leader",StringComparison.Ordinal))) continue;
                double neckLeft=outlines.SelectMany(p=>p.Points).Min(p=>p.X);
                double neckTop=outlines.SelectMany(p=>p.Points).Max(p=>p.Y);
                var neckCallout=package.Primitives.OfType<TextPrimitive>().FirstOrDefault(p=>p.ViewId==view &&
                    p.SemanticId.StartsWith(view+"/Callout/Neck",StringComparison.Ordinal));
                result.Add(new TextPrimitive($"{view}/MaterialLeader/Neck/Text","Rebar/MarkRight",view,"Neck",null,null,
                    new(neckCallout?.Position.X ?? neckLeft-95,neckCallout is null?neckTop-350:neckCallout.Position.Y-100),entry.Value,60,0));
                continue;
            }
            var outline=package.Primitives.OfType<PolylinePrimitive>()
                .Where(p=>p.ViewId==view && p.ComponentId==entry.Key && MaterialOutline(p))
                .OrderByDescending(p=>p.Points.Max(point=>point.X)).FirstOrDefault();
            if(outline is null) continue;
            var points=outline.Points;
            double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
            bool mortar=entry.Key=="Wall/Mortar";
            var walls=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId==view && MaterialOutline(p) &&
                p.ComponentId is not null && p.ComponentId.StartsWith("Wall/") && p.ComponentId!="Wall/Mortar").ToArray();
            double left=walls.Where(p=>p.Points.Average(x=>x.X)<0).Select(p=>p.Points.Max(x=>x.X)).DefaultIfEmpty(0).Max();
            double right=walls.Where(p=>p.Points.Average(x=>x.X)>0).Select(p=>p.Points.Min(x=>x.X)).DefaultIfEmpty(0).Min();
            double noteX=right>left?left+(right-left)/4:(minX+maxX)/2;
            Point2Mm target=mortar?new((minX+maxX)/2,(minY+maxY)/2):new(noteX,(minY+maxY)/2);
            target=MaterialAnchor(package,view,entry.Key,outline,target);
            // Keep the mortar label inside the chamber, and material notes just below the overall dimension.
            Point2Mm elbow=mortar?new(minX-30,minY-120):new(noteX,minY-(entry.Key=="Base"?580:600));
            Point2Mm end=mortar?new(elbow.X-450,elbow.Y):new(elbow.X+entry.Value.Length*36,elbow.Y);
            string id=$"{view}/MaterialLeader/{entry.Key}";
            result.Add(new PolylinePrimitive(id,"Material/Leader",view,entry.Key,null,null,[target,elbow,end],false));
            result.Add(new TextPrimitive(id+"/Text","DimensionText",view,entry.Key,null,null,
                new(mortar?end.X:elbow.X+20,elbow.Y+20),entry.Value,60,0));
            if(mortar)
                result.Add(new TextPrimitive(id+"/Thickness","DimensionText",view,entry.Key,null,null,
                    new(end.X,elbow.Y-90),$"Dày {maxY-minY:0.###}mm",60,0));
        }
        return result;
    }
    private static bool MaterialOutline(PolylinePrimitive p)=>p.Role is "Geometry/Material" or SheetOneSectionOutlinePolicy.Role;

    private static Point2Mm MaterialAnchor(DrawingPackage package,string view,string owner,PolylinePrimitive outline,Point2Mm preferred)
    {
        var hatches=package.Primitives.OfType<HatchPrimitive>().Where(p=>p.ViewId==view && p.ComponentId==owner).ToArray();
        var material=hatches.Length>0
            ? hatches.Select(h=>new Footprint(new Polygon2Mm(h.OuterLoop),h.InnerLoops.Select(loop=>new Polygon2Mm(loop)))).ToArray()
            : [new Footprint(new Polygon2Mm(outline.Points),[])];
        if(SectionRebarClipper.Clip(preferred,preferred,material).Count>0) return preferred;
        var vertices=material.SelectMany(f=>new[]{f.Outer}.Concat(f.InnerLoops)).SelectMany(p=>p.Vertices).ToArray();
        double lower=vertices.Min(p=>p.Y),upper=vertices.Max(p=>p.Y);
        var xs=vertices.Select(p=>p.X).Distinct().Order().ToArray();
        var stations=xs.Zip(xs.Skip(1),(a,b)=>(a+b)/2).Append(preferred.X).Distinct();
        var candidates=stations.SelectMany(x=>SectionRebarClipper.Clip(new(x,lower),new(x,upper),material))
            .Select(segment=>new Point2Mm((segment.Start.X+segment.End.X)/2,(segment.Start.Y+segment.End.Y)/2));
        var ordered=candidates.OrderBy(p=>Math.Pow(p.X-preferred.X,2)+Math.Pow(p.Y-preferred.Y,2))
            .ThenBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
        if(ordered.Length==0) throw new InvalidOperationException($"MATERIAL_LEADER_TARGET_UNRESOLVED: {view}/{owner}");
        return ordered[0];
    }
}
