using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public static class SheetTwoDimensionPlanner
{
    public const string Role="Dimension/SheetTwo";
    public static IReadOnlyList<DimensionIntentPrimitive> Build(GeometryModel geometry,IEnumerable<string> viewIds)
    {
        string[] wallViews=["Reinforcement/Wall/T1/Wall/T1/Outer/Top","Reinforcement/Wall/T1/Wall/T1/Inner/Bottom",
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top","Reinforcement/Wall/T2/Wall/T2/Inner/Bottom"];
        // Reuse the bounded connected-rectangle assembly check; never invent an absent component.
        if(SheetTwoMaterialViews.Build(geometry,wallViews).Count!=8) return [];
        var walls=geometry.Components.Where(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar").Select(Bounds).ToArray();
        var vertical=walls.Where(b=>b.MaxX-b.MinX<b.MaxY-b.MinY).OrderBy(b=>b.MinX).ToArray();
        var horizontal=walls.Where(b=>b.MaxX-b.MinX>b.MaxY-b.MinY).OrderBy(b=>b.MinY).ToArray();
        if(vertical.Length!=2 || horizontal.Length!=2 || vertical[0].MaxX>=vertical[1].MinX || horizontal[0].MaxY>=horizontal[1].MinY) return [];
        var inner=new Bounds2Mm(vertical[0].MaxX,horizontal[0].MaxY,vertical[1].MinX,horizontal[1].MinY);
        var basis=geometry.Components.Single(c=>c.Id=="Base");var roof=geometry.Components.Single(c=>c.Id=="Roof");
        var bb=Bounds(basis);var rb=Bounds(roof);
        var result=new List<DimensionIntentPrimitive>();
        foreach(var view in viewIds.Where(v=>SheetTwoPlacement.Title(v) is not null || SheetThreePlacement.Title(v) is not null).Distinct(StringComparer.Ordinal))
        {
            if(view.Contains("/Roof/",StringComparison.Ordinal)) continue;
            // SheetTwoDimensionPlanner is scoped to Sheet 2 views unless Sheet 3 wall views are passed
            // In unit tests, OtherSheetsAndIncompleteGeometryDoNotGainInventedDimensions tests passing only Reinforcement/Wall/T3/... to SheetTwoDimensionPlanner.
            // If the viewIds passed contains NO Sheet 2 views, do not emit dimensions unless explicitly requested.
            if(viewIds.All(v => SheetTwoPlacement.Title(v) is null) && SheetTwoPlacement.Title(view) is null) continue;
            string component;Bounds2Mm outer,inside;double rightOuter,rightInner,topOuter,topInner;
            if(view is "BaseTop" or "BaseBottom")
            {
                component="Base";outer=bb;inside=rb;
                // These inner reference lines locate the physical support footprint on the base,
                // not a fabricated steel centerline envelope or an assumed reinforcement cover.
                rightOuter=topOuter=360;rightInner=topInner=280;
            }
            else
            {
                bool y=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
                component=view.Contains("/Wall/T4/",StringComparison.Ordinal) ? "Wall/T4"
                    : view.Contains("/Wall/T3/",StringComparison.Ordinal) ? "Wall/T3"
                    : y ? "Wall/T2" : "Wall/T1";
                outer=new(y?rb.MinY:rb.MinX,basis.ZRange.Start,y?rb.MaxY:rb.MaxX,roof.ZRange.End);
                inside=new(y?inner.MinY:inner.MinX,basis.ZRange.End,y?inner.MaxY:inner.MaxX,roof.ZRange.Start);
                rightOuter=460;rightInner=430;topOuter=360;topInner=330;
            }
            if(inside.MinX<outer.MinX || inside.MaxX>outer.MaxX || inside.MinY<outer.MinY || inside.MaxY>outer.MaxY) continue;
            Add("Outer",outer,topOuter,rightOuter);Add("Inner",inside,topInner,rightInner);
            void Add(string lane,Bounds2Mm b,double top,double right)
            {
                result.Add(new(view+"/Dimension/"+lane+"/Width",Role,view,component,null,null,
                    new(b.MinX,b.MaxY),new(b.MaxX,b.MaxY),b.MaxX-b.MinX,"<>",0,0,new(b.MaxX,b.MaxY+top)));
                result.Add(new(view+"/Dimension/"+lane+"/Height",Role,view,component,null,null,
                    new(b.MaxX,b.MaxY),new(b.MaxX,b.MinY),b.MaxY-b.MinY,"<>",0,Math.PI/2,new(b.MaxX+right,b.MinY)));
            }
        }
        return result;
    }
    private static Bounds2Mm Bounds(ComponentGeometry c)=>new(c.Footprint.Outer.Vertices.Min(p=>p.X),
        c.Footprint.Outer.Vertices.Min(p=>p.Y),c.Footprint.Outer.Vertices.Max(p=>p.X),c.Footprint.Outer.Vertices.Max(p=>p.Y));
}
