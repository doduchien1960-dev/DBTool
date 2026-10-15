using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Concealed inside and outside wall boundaries in the first-sheet wall mesh plan.</summary>
public static class SheetOneWallMeshHiddenEdges
{
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry, PresentationRequest presentation)
    {
        if (!SheetOnePresentationPolicy.Enabled(presentation)) return [];
        var walls = geometry.Components.Where(c => c.Kind == ComponentKind.Wall && c.Id != "Wall/Mortar").ToArray();
        if (walls.Length != 4 || walls.Any(w => w.Footprint.Outer.Vertices.Length != 4 ||
            w.Footprint.Outer.Vertices.Select(p => p.X).Distinct().Count() != 2 ||
            w.Footprint.Outer.Vertices.Select(p => p.Y).Distinct().Count() != 2)) return [];
        var vertices = walls.SelectMany(w => w.Footprint.Outer.Vertices).ToArray();
        var xs = vertices.Select(p => p.X).Distinct().Order().ToArray();
        var ys = vertices.Select(p => p.Y).Distinct().Order().ToArray();
        if (xs.Length != 4 || ys.Length != 4) return [];
        PolylinePrimitive Contour(string name, int lo, int hi)
        {
            // Native source contours start at the upper-left and repeat that vertex.
            Point2Mm Point(double x, double y) => WallMeshPresentation.GraphicPoint(new(x, y));
            var topLeft = Point(xs[lo], ys[hi]);
            return new($"WallMeshPlan/Walls/Hidden/{name}", "Geometry/SectionHidden", "WallMeshPlan", null, null, null,
                [topLeft, Point(xs[hi], ys[hi]), Point(xs[hi], ys[lo]), Point(xs[lo], ys[lo]), topLeft], false);
        }
        return [Contour("Outer", 0, 3), Contour("Inner", 1, 2)];
    }
}
