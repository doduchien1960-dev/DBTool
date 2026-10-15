namespace DBTool.Drawing;

/// <summary>Preserves the actual projected base reinforcement and its callouts.</summary>
public static class SheetTwoNominalBaseMesh
{
    public const string Role = "Rebar/SheetTwoNominalBase";

    public static IReadOnlyList<DrawingPrimitive> Apply(IEnumerable<DrawingPrimitive> primitives)
    {
        // A dimension label cannot establish a physical bar count or station.
        // Retain the projector's paths and occurrence associations unchanged.
        return primitives.ToArray();
    }
}
