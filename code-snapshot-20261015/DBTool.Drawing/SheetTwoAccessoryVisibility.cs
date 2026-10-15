namespace DBTool.Drawing;

/// <summary>The nominal mesh panels omit ladder/dowel overlays; calculated occurrences remain untouched.</summary>
public static class SheetTwoAccessoryVisibility
{
    public static IReadOnlyList<DrawingPrimitive> Apply(IEnumerable<DrawingPrimitive> primitives,
        IReadOnlyDictionary<string,string?> families)
    {
        var snapshot=primitives.ToArray();
        var nominalWalls=snapshot.Where(p=>p.Role==SheetTwoNominalWallMesh.Role)
            .Select(p=>p.ComponentId).ToHashSet(StringComparer.Ordinal);
        return snapshot.Where(p=>!(p.Role=="Rebar/Main" && p.OccurrenceId is {} id &&
            families.TryGetValue(id,out var family) && family is "Ladder" or "Dowel" &&
            nominalWalls.Contains(p.ComponentId) && SheetMeshPlacement.Origin(p.ViewId) is not null)).ToArray();
    }
}
