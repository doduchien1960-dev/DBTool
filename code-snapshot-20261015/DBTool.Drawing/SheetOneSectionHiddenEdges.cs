using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Concealed roof edge below the neck, derived from the physical footprint.</summary>
public static class SheetOneSectionHiddenEdges
{
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,string view,PresentationRequest presentation)
    {
        if(!SheetOnePresentationPolicy.Enabled(presentation) || view is not ("AA" or "BB" or "SteelAA" or "SteelBB")) return [];
        var roof=geometry.Components.FirstOrDefault(c=>c.Id=="Roof");
        var neck=geometry.Components.FirstOrDefault(c=>c.Id=="Neck");
        if(roof is null || neck is null || Math.Abs(roof.ZRange.End-neck.ZRange.Start)>1e-8) return [];
        var vertices=neck.Footprint.Outer.Vertices;
        if(vertices.Length!=4 || vertices.Select(p=>p.X).Distinct().Count()!=2 || vertices.Select(p=>p.Y).Distinct().Count()!=2) return [];
        bool alongY=view.EndsWith("AA",StringComparison.Ordinal);
        double left=vertices.Min(p=>alongY?p.Y:p.X),right=vertices.Max(p=>alongY?p.Y:p.X);
        // Native reference-visit-v11: the full outer neck width conceals the roof top, not just its opening.
        return [new PolylinePrimitive($"{view}/Roof/HiddenBelowNeck","Geometry/SectionHidden",view,"Roof",null,null,
            [new(left,roof.ZRange.End),new(right,roof.ZRange.End)],false)];
    }
}
