using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Preserves the physical wall mesh supplied by the component projector.</summary>
public static class SheetTwoNominalWallMesh
{
    public const string Role = "Rebar/SheetTwoNominalWall";
    public const string EdgeRole = "Rebar/SheetTwoNominalWallEdge";

    public static IReadOnlyList<DrawingPrimitive> Apply(
        GeometryModel geometry, IEnumerable<DrawingPrimitive> primitives)
    {
        // Dimension chains describe bars; they cannot create new physical occurrences
        // or move a bar while borrowing the identity of a nearby occurrence.
        return primitives.ToArray();
    }
}

