using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Physical bedding mortar at the roof junction of each nominal wall mesh.</summary>
public static class SheetTwoMortar
{
    public const string Role="Material/SheetTwoMortar";
    public const string BoundaryRole="Geometry/SheetTwoMortarBoundary";
    // The source pattern is anchored at WCS (0,0), while its sheet frame starts here.
    // Translate the pattern anchor with the sheet; resetting it to the new WCS changes phase.
    private static readonly Point2Mm PublishedSheetOrigin=new(14403.50952778792,-72736.52203702747);
    public static IReadOnlyList<HatchPrimitive> Build(GeometryModel geometry,IEnumerable<string> viewIds)
    {
        var mortar=geometry.Components.SingleOrDefault(c=>c.Id=="Wall/Mortar");
        var roof=geometry.Components.SingleOrDefault(c=>c.Id=="Roof");
        if(mortar is null || roof is null || mortar.MaterialId!="BeddingMortar" ||
            Math.Abs(mortar.ZRange.End-roof.ZRange.Start)>1e-8) return [];
        var result=new List<HatchPrimitive>();
        foreach(string view in viewIds.Distinct(StringComparer.Ordinal))
        {
            if(!view.StartsWith("Reinforcement/Wall/",StringComparison.Ordinal) || SheetMeshPlacement.Title(view) is null ||
                SheetMeshPlacement.Origin(view) is not {} origin) continue;
            bool alongY=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
            string wallId=view.Contains("/Wall/T4/",StringComparison.Ordinal) ? "Wall/T4"
                : view.Contains("/Wall/T3/",StringComparison.Ordinal) ? "Wall/T3"
                : alongY ? "Wall/T2" : "Wall/T1";
            var wall=geometry.Components.SingleOrDefault(c=>c.Id==wallId);
            if(wall is null || Math.Abs(wall.ZRange.End-mortar.ZRange.Start)>1e-8) continue;
            double left=roof.Footprint.Outer.Vertices.Min(p=>alongY?p.Y:p.X),
                right=roof.Footprint.Outer.Vertices.Max(p=>alongY?p.Y:p.X);
            result.Add(new(view+"/Material/Mortar",Role,view,wall.Id,
                [new(left,mortar.ZRange.End),new(right,mortar.ZRange.End),
                    new(right,mortar.ZRange.Start),new(left,mortar.ZRange.Start)],[],DrawingMaterialKind.Concrete,
                new(-PublishedSheetOrigin.X-origin.X,-PublishedSheetOrigin.Y-origin.Y),nativeBoundaryLoops:true));
        }
        return result;
    }
    public static IReadOnlyList<PolylinePrimitive> Boundaries(IEnumerable<HatchPrimitive> hatches) =>
        hatches.Where(h=>h.Role==Role && h.OuterLoop.Length==4).Select(h=>
            new PolylinePrimitive(h.SemanticId+"/BottomBoundary",BoundaryRole,h.ViewId,h.ComponentId,null,null,
                [h.OuterLoop[3],h.OuterLoop[2]],false)).ToArray();
}
