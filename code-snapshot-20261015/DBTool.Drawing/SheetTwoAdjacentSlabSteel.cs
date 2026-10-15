using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Nominal slab steel envelopes adjacent to each wall mesh, with existing family provenance.</summary>
public static class SheetTwoAdjacentSlabSteel
{
    public const string Role="Rebar/SheetTwoAdjacentSlab";
    public static IReadOnlyList<PolylinePrimitive> Build(GeometryModel geometry,IEnumerable<DrawingPrimitive> primitives,
        IReadOnlyDictionary<string,int> diameters)
    {
        var snapshot=primitives.ToArray();var result=new List<PolylinePrimitive>();
        foreach(var group in snapshot.OfType<DimensionIntentPrimitive>().Where(d=>d.SemanticId.Contains("/NominalSpacing/X/",StringComparison.Ordinal)).GroupBy(d=>d.ViewId))
        {
            string view=group.Key;
            if(view is "BaseTop" or "BaseBottom" || SheetMeshPlacement.Title(view) is null || view.Contains("/Roof/",StringComparison.Ordinal)) continue;
            var chain=group.OrderBy(d=>d.SemanticId,StringComparer.Ordinal).ToArray();if(chain.Length!=7) continue;
            double cover=chain[0].LogicalMeasurementMm;bool alongY=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
            foreach(string component in new[]{"Base","Roof"})
            {
                var slab=geometry.Components.SingleOrDefault(c=>c.Id==component);if(slab is null) continue;
                var owner=snapshot.Where(p=>p.ComponentId==component && p.OccurrenceId is not null && p.ScheduleMark is not null &&
                    (component=="Base"?p.ViewId=="BaseTop":p.ViewId.Contains("Roof/Top/",StringComparison.Ordinal)) &&
                    p is PolylinePrimitive line && line.Points.Length==2 &&
                    (Math.Abs(line.Points[1].Y-line.Points[0].Y)>Math.Abs(line.Points[1].X-line.Points[0].X))==alongY)
                    .OrderBy(p=>p.OccurrenceId,StringComparer.Ordinal).FirstOrDefault();
                if(owner?.OccurrenceId is not {} id || !diameters.TryGetValue(id,out int diameter)) continue;
                double a=slab.Footprint.Outer.Vertices.Min(p=>alongY?p.Y:p.X)+cover,
                    b=slab.Footprint.Outer.Vertices.Max(p=>alongY?p.Y:p.X)-cover;
                double low=slab.ZRange.Start+cover,high=slab.ZRange.End-cover;
                if(a>=b || low>=high) continue;
                foreach(double z in component=="Base"?new[]{low,high}:new[]{high-9*diameter,high})
                {
                    if(z<low-1e-7) continue;
                    result.Add(new(view+"/AdjacentSlab/"+component+"/"+result.Count.ToString("000"),Role,view,
                        chain[0].ComponentId,id,owner.ScheduleMark,[new(a,z),new(b,z)],false,
                        materialKind:DrawingMaterialKind.RoundSteel));
                }
            }
        }
        return result;
    }
}
