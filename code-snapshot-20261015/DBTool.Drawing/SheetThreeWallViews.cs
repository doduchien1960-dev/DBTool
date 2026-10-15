namespace DBTool.Drawing;

/// <summary>Retains the actual T3/T4 projections, ownership and annotation associations.</summary>
public static class SheetThreeWallViews
{
    public static IReadOnlyList<DrawingPrimitive> Build(IEnumerable<DrawingPrimitive> sourcePrimitives,string? manholeCode=null)
        =>sourcePrimitives.Where(p=>p.ViewId.Contains("/Wall/T3/",StringComparison.Ordinal) ||
            p.ViewId.Contains("/Wall/T4/",StringComparison.Ordinal)).ToArray();
}
