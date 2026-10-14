using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>
/// Exposed outlines of a connected first-sheet concrete mass (base, walls, haunches).
/// Preserves concrete composition, emits outer and inner boundaries from the same pour/material,
/// preserves type 2/3 joints and voids, and never unions mortar into reinforced concrete.
/// </summary>
public sealed record SheetOneConcreteAssembly
{
    private SheetOneConcreteAssembly(IReadOnlyDictionary<string,IReadOnlyList<Point2Mm[]>> paths,
        IReadOnlyDictionary<string,string> aliases) { Paths=paths;Aliases=aliases; }
    public IReadOnlyDictionary<string,IReadOnlyList<Point2Mm[]>> Paths { get; }
    public IReadOnlyDictionary<string,string> Aliases { get; }
    public static SheetOneConcreteAssembly Build(IEnumerable<DrawingPrimitive> primitives)
    {
        var paths = new Dictionary<string, IReadOnlyList<Point2Mm[]>>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var eligible = primitives.OfType<PolylinePrimitive>().Where(p =>
            p.Role == SheetOneSectionOutlinePolicy.Role && p.Closed && p.OccurrenceId is null &&
            p.ViewId is "AA" or "BB" or "SteelAA" or "SteelBB");
        foreach (var group in eligible.GroupBy(p => p.ViewId))
        {
            var bases = group.Where(p => p.ComponentId == "Base").ToArray();
            var walls = group.Where(p => p.ComponentId is "Wall/T1" or "Wall/T2" or "Wall/T3" or "Wall/T4")
                .Where(p => bases.Length==1 && Near(Bounds(p).MinY,Bounds(bases[0]).MaxY))
                .OrderBy(p => Bounds(p).MinX).ToArray();
            var haunches = group.Where(p => p.ComponentId == "Base/Haunch").ToArray();
            if (bases.Length != 1 || walls.Length != 2 || haunches.Length != 2 ||
                !Rectangle(bases[0]) || walls.Any(p => !Rectangle(p)) || haunches.Any(p => p.Points.Length != 3)) continue;
            var slab = Bounds(bases[0]); var left = Bounds(walls[0]); var right = Bounds(walls[1]);
            var corners = haunches.OrderBy(p => Bounds(p).MinX).ToArray();
            var a = Bounds(corners[0]); var b = Bounds(corners[1]);
            if (!Near(left.MinY, slab.MaxY) || !Near(right.MinY, slab.MaxY) ||
                left.MaxX >= right.MinX || slab.MinX >= left.MinX || slab.MaxX <= right.MaxX ||
                !Near(a.MinX, left.MaxX) || !Near(b.MaxX, right.MinX) ||
                !Near(a.MinY, slab.MaxY) || !Near(b.MinY, slab.MaxY) || a.MaxX >= b.MinX ||
                a.MaxY >= left.MaxY || b.MaxY >= right.MaxY ||
                !Triangle(corners[0], [new(a.MinX,a.MinY),new(a.MaxX,a.MinY),new(a.MinX,a.MaxY)]) ||
                !Triangle(corners[1], [new(b.MinX,b.MinY),new(b.MaxX,b.MinY),new(b.MaxX,b.MaxY)])) continue;

            // Only interfaces inside this connected concrete mass disappear.
            // Exposed base wings, soffit, wall faces, sloping haunches and floor remain.
            paths.Add(bases[0].SemanticId, [
                [new(left.MinX,slab.MaxY),new(slab.MinX,slab.MaxY),new(slab.MinX,slab.MinY),
                 new(slab.MaxX,slab.MinY),new(slab.MaxX,slab.MaxY),new(right.MaxX,slab.MaxY)]]);
            paths.Add(walls[0].SemanticId, [
                [new(left.MinX,left.MinY),new(left.MinX,left.MaxY),new(left.MaxX,left.MaxY),new(left.MaxX,a.MaxY)]]);
            paths.Add(walls[1].SemanticId, [
                [new(right.MinX,b.MaxY),new(right.MinX,right.MaxY),new(right.MaxX,right.MaxY),new(right.MaxX,right.MinY)]]);
            // One connected inner boundary owns the ink; both haunch IDs keep its handle.
            paths.Add(haunches[0].SemanticId, [
                [new(a.MinX,a.MaxY),new(a.MaxX,a.MinY),new(b.MinX,b.MinY),new(b.MaxX,b.MaxY)]]);
            aliases.Add(haunches[1].SemanticId, haunches[0].SemanticId);
        }
        return new(paths, aliases);
    }
    private static bool Near(double a,double b) => Math.Abs(a-b)<1e-8;
    private static bool Rectangle(PolylinePrimitive p) => p.Points.Length==4 && p.Points.Distinct().Count()==4 &&
        p.Points.Select(v=>v.X).Distinct().Count()==2 && p.Points.Select(v=>v.Y).Distinct().Count()==2;
    private static bool Triangle(PolylinePrimitive p,Point2Mm[] expected) => p.Points.ToHashSet().SetEquals(expected);
    private static Bounds2Mm Bounds(PolylinePrimitive p) => new(p.Points.Min(v=>v.X),p.Points.Min(v=>v.Y),
        p.Points.Max(v=>v.X),p.Points.Max(v=>v.Y));
}
