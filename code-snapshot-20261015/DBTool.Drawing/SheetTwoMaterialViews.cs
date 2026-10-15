using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>First-case family2.1 mesh context; dimensions come from physical slab geometry.</summary>
public static class SheetTwoMaterialViews
{
    public const string BaseRole="Geometry/SheetTwoBaseOutline";
    public const string WallRole="Geometry/SheetTwoWallEnvelope";
    public const string RoofRole="Geometry/SheetTwoRoofJunction";
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,IEnumerable<string> viewIds)
    {
        var slab=geometry.Components.SingleOrDefault(c=>c.Id=="Base");
        var roof=geometry.Components.SingleOrDefault(c=>c.Id=="Roof");
        if(slab is null || roof is null || !Rectangle(slab) || !Rectangle(roof) ||
            slab.MaterialId!="Concrete" || roof.MaterialId!="Concrete" || slab.ZRange.End>=roof.ZRange.Start) return [];
        // This envelope is only valid for the complete connected four-wall assembly.
        // A missing wall or an unexplained vertical gap must not become invented concrete.
        foreach(string id in new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"})
        {
            var wall=geometry.Components.SingleOrDefault(c=>c.Id==id);
            if(wall is null || !Rectangle(wall) || wall.MaterialId!="Concrete" ||
                Math.Abs(wall.ZRange.Start-slab.ZRange.End)>1e-8) return [];
            if(Math.Abs(wall.ZRange.End-roof.ZRange.Start)>1e-8 &&
                !geometry.Components.Any(c=>c.Id=="Wall/Mortar" &&
                    Math.Abs(c.ZRange.Start-wall.ZRange.End)<1e-8 && Math.Abs(c.ZRange.End-roof.ZRange.Start)<1e-8)) return [];
        }
        var result=new List<DrawingPrimitive>();
        foreach(var view in viewIds)
        foreach(string wall in new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"})
        {
            if(view!=$"Reinforcement/{wall}/{wall}/Outer/Top" && view!=$"Reinforcement/{wall}/{wall}/Inner/Bottom") continue;
            if(!geometry.Components.Any(c=>c.Id==wall && c.Kind==ComponentKind.Wall && c.MaterialId=="Concrete")) continue;
            bool y=wall is "Wall/T2" or "Wall/T4";
            double Coordinate(Point2Mm p)=>y?p.Y:p.X;
            double a=slab.Footprint.Outer.Vertices.Min(Coordinate),b=slab.Footprint.Outer.Vertices.Max(Coordinate),
                l=roof.Footprint.Outer.Vertices.Min(Coordinate),r=roof.Footprint.Outer.Vertices.Max(Coordinate);
            if(a>l || b<r) continue;
            double bottom=slab.ZRange.Start,floor=slab.ZRange.End,top=roof.ZRange.End;
            Point2Mm[] assemblyPoints = Math.Abs(a - l) > 1e-4 || Math.Abs(b - r) > 1e-4
                ? [new(l,top),new(r,top),new(r,floor),new(b,floor),new(b,bottom),new(a,bottom),new(a,floor),new(l,floor)]
                : [new(l,top),new(r,top),new(r,bottom),new(l,bottom)];
            result.Add(new PolylinePrimitive(view+"/Material/Assembly",WallRole,view,wall,null,null,
                assemblyPoints,true));
            result.Add(new PolylinePrimitive(view+"/Material/RoofBottom",RoofRole,view,wall,null,null,
                [new(l,roof.ZRange.Start),new(r,roof.ZRange.Start)],false));
        }
        return result;
    }
    public static DrawingPrimitive Apply(DrawingPrimitive primitive,PresentationRequest presentation)
    {
        if(!SheetOnePresentationPolicy.Enabled(presentation) || primitive is not PolylinePrimitive p ||
            p.ViewId is not ("BaseTop" or "BaseBottom") || p.Role!="Geometry/BaseOutline" ||
            !p.Closed || p.Points.Length!=4 || p.Points.Select(v=>v.X).Distinct().Count()!=2 ||
            p.Points.Select(v=>v.Y).Distinct().Count()!=2) return primitive;
        double l=p.Points.Min(v=>v.X),r=p.Points.Max(v=>v.X),b=p.Points.Min(v=>v.Y),t=p.Points.Max(v=>v.Y);
        return new PolylinePrimitive(p.SemanticId,BaseRole,p.ViewId,p.ComponentId,p.OccurrenceId,p.ScheduleMark,
            [new(l,t),new(r,t),new(r,b),new(l,b),new(l,t)],false,p.LogicalLengthMm,p.MaterialKind);
    }
    private static bool Rectangle(ComponentGeometry c)=>c.Footprint.Outer.Vertices.Length==4 &&
        c.Footprint.Outer.Vertices.Select(p=>p.X).Distinct().Count()==2 &&
        c.Footprint.Outer.Vertices.Select(p=>p.Y).Distinct().Count()==2;
}
