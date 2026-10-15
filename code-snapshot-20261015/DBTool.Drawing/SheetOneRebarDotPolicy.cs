using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Explicit native presentation intent; physical radius and occurrence remain unchanged.</summary>
public static class SheetOneRebarDotPolicy
{
    public const string Role = "Rebar/SheetOneDot";
    public const double GraphicDiameterMm = 9;
    /// <summary>Share only exactly coincident WallMeshPlan ink; retain all physical and semantic identities.</summary>
    public static IReadOnlyDictionary<string,string> RenderAliases(IEnumerable<DrawingPrimitive> primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        var first=new Dictionary<Point2Mm,string>();
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var dot in primitives.OfType<CirclePrimitive>().Where(p=>p.Role==Role && p.ViewId=="WallMeshPlan"))
            if(first.TryGetValue(dot.Center,out var representative)) aliases.Add(dot.SemanticId,representative);
            else first.Add(dot.Center,dot.SemanticId);
        return aliases;
    }
    public static DrawingPrimitive Apply(DrawingPrimitive primitive, PresentationRequest presentation) =>
        SheetOnePresentationPolicy.Enabled(presentation) && primitive is CirclePrimitive
        { Role: "Rebar/Main", ViewId: "SteelAA" or "SteelBB" or "WallMeshPlan" } dot
            ? new CirclePrimitive(dot.SemanticId, Role, dot.ViewId, dot.ComponentId, dot.OccurrenceId,
                dot.ScheduleMark, dot.Center, dot.RadiusMm)
            : primitive;
}
