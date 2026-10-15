using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>One BLTC per projected physical tie placement. Symbols never create steel.</summary>
public static class SheetTwoTieSymbols
{
    public const string BaseRole="Rebar/SheetTwoTie";
    public const string WallRole="Rebar/SheetTwoTieWall";
    public const string RoofRole="Rebar/SheetThreeTieRoof";

    public static IReadOnlyList<BlockPrimitive> Build(IEnumerable<DrawingPrimitive> primitives,
        string? manholeCode = null, IReadOnlySet<string>? tieOccurrenceIds = null, double scale = 30)
    {
        if(tieOccurrenceIds is null || tieOccurrenceIds.Count==0) return [];
        var result=new List<BlockPrimitive>();
        double annotScale = scale > 0 ? scale / 100.0 : 0.3;
        foreach(var group in primitives.Where(p=>p.OccurrenceId is {} id && tieOccurrenceIds.Contains(id) &&
            (p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom") &&
            (p.ViewId=="BaseTop" || p.ViewId.Contains("/Outer/Top",StringComparison.Ordinal) || p.ViewId.Contains("Roof",StringComparison.OrdinalIgnoreCase)))
            .GroupBy(p=>(p.ViewId,p.OccurrenceId)).OrderBy(g=>g.Key.ViewId,StringComparer.Ordinal)
            .ThenBy(g=>g.Key.OccurrenceId,StringComparer.Ordinal))
        {
            var source=group.OrderBy(p=>p.SemanticId,StringComparer.Ordinal).First();
            Point2Mm? point=source switch
            {
                PolylinePrimitive line => line.Points.Length > 0 ? line.Points[0] : null,
                CirclePrimitive circle => circle.Center,
                PathPrimitive path => path.Segments.Length > 0 ? path.Segments[0].Start : null,
                _ => null
            };
            if(point is null) continue;
            string role = source.ViewId == "BaseTop" ? BaseRole :
                          source.ViewId.Contains("Roof", StringComparison.OrdinalIgnoreCase) ? RoofRole : WallRole;
            result.Add(new BlockPrimitive($"{source.ViewId}/BLTC/{source.OccurrenceId}",
                role, source.ViewId, source.ComponentId,
                "Block:BLTC", point.Value, annotScale, [], occurrenceId: source.OccurrenceId, scheduleMark: source.ScheduleMark));
        }
        return result;
    }
}
