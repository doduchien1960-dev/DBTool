using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Concealed assembly outlines on the nominal mesh sheet, from physical footprints.</summary>
public static class SheetTwoHiddenEdges
{
    public const string Role="Geometry/SheetTwoHidden";
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,IEnumerable<string> viewIds)
    {
        var roof=geometry.Components.SingleOrDefault(c=>c.Id=="Roof");
        var haunch=geometry.Components.SingleOrDefault(c=>c.Id=="Base/Haunch");
        var walls=new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"}
            .Select(id=>geometry.Components.SingleOrDefault(c=>c.Id==id)).ToArray();
        if(roof is null || walls.Any(c=>c is null) || !Rectangle(roof.Footprint.Outer) ||
            walls.Any(c=>!Rectangle(c!.Footprint.Outer))) return [];
        // The four inner wall faces define the chamber, including an asymmetric footprint.
        double left=walls[3]!.Footprint.Outer.Vertices.Max(p=>p.X),
            right=walls[1]!.Footprint.Outer.Vertices.Min(p=>p.X),
            bottom=walls[2]!.Footprint.Outer.Vertices.Max(p=>p.Y),
            top=walls[0]!.Footprint.Outer.Vertices.Min(p=>p.Y);
        if(left>=right || bottom>=top) return [];
        var result=new List<DrawingPrimitive>();
        foreach(string view in viewIds.Distinct(StringComparer.Ordinal))
        {
            if(view is "BaseTop" or "BaseBottom")
            {
                var v=roof.Footprint.Outer.Vertices;
                Add("Roof", "Base", [new(v.Min(p=>p.X),v.Max(p=>p.Y)),new(v.Max(p=>p.X),v.Max(p=>p.Y)),
                    new(v.Max(p=>p.X),v.Min(p=>p.Y)),new(v.Min(p=>p.X),v.Min(p=>p.Y)),new(v.Min(p=>p.X),v.Max(p=>p.Y))]);
                Add("Chamber","Base",[new(left,top),new(right,top),new(right,bottom),new(left,bottom),new(left,top)]);
            }
            else if(view.StartsWith("Reinforcement/Wall/",StringComparison.Ordinal) &&
                SheetMeshPlacement.Title(view) is not null && haunch?.Occupied is HaunchSolid h &&
                h.Owner==HaunchOwner.Base && h.WidthMm>0 && h.HeightMm>0 &&
                Math.Abs(haunch.ZRange.Start-walls[0]!.ZRange.Start)<1e-8)
            {
                bool alongY=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
                double low=alongY?bottom:left,high=alongY?top:right;
                if(2*h.WidthMm>=high-low) continue;
                double floor=haunch.ZRange.Start,knee=floor+h.HeightMm,ceiling=roof.ZRange.Start;
                if(knee>=ceiling) continue;
                string comp=view.Contains("/Wall/T4/",StringComparison.Ordinal) ? "Wall/T4"
                    : view.Contains("/Wall/T3/",StringComparison.Ordinal) ? "Wall/T3"
                    : alongY ? "Wall/T2" : "Wall/T1";
                Add("ChamberHaunch",comp,
                    [new(low,ceiling),new(low,knee),new(low+h.WidthMm,floor),
                        new(high-h.WidthMm,floor),new(high,knee),new(high,ceiling)]);
            }
            void Add(string suffix,string component,Point2Mm[] points)=>result.Add(
                new PolylinePrimitive(view+"/Hidden/"+suffix,Role,view,component,null,null,points,false));
        }
        return result;
    }
    private static bool Rectangle(Polygon2Mm polygon)=>polygon.Vertices.Length==4 &&
        polygon.Vertices.Select(p=>p.X).Distinct().Count()==2 && polygon.Vertices.Select(p=>p.Y).Distinct().Count()==2;
}
