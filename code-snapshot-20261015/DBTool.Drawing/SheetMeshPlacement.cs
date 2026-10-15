using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Unified placement lookup for Family 2.1 nominal mesh sheets (Sheet 2 and Sheet 3).</summary>
public static class SheetMeshPlacement
{
    public static Point2Mm? Origin(string view) =>
        SheetTwoPlacement.Origin(view) ?? SheetThreePlacement.Origin(view);

    public static Point2Mm? Title(string view) =>
        SheetTwoPlacement.Title(view) ?? SheetThreePlacement.Title(view);
}
