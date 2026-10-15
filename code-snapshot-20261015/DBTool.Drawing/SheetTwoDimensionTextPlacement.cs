using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Published manual text placement for the outer short nominal cover bands.</summary>
public static class SheetTwoDimensionTextPlacement
{
    // Measured on all 24 source dimensions at 1:30; not a change to measured geometry.
    private const double OutsideTextOffsetMm = 83.857142857143;

    public static Point2Mm? Position(DimensionIntentPrimitive dimension)
    {
        if (dimension.Role != SheetTwoDimensionPlanner.Role || dimension.DimensionLinePoint is not {} line ||
            SheetMeshPlacement.Title(dimension.ViewId) is null)
            return null;
        bool slab = dimension.ViewId is "BaseTop" or "BaseBottom";
        string id = dimension.SemanticId;
        if (Math.Abs(dimension.LogicalMeasurementMm - 30) < 1e-7)
        {
            if (id.EndsWith("/NominalSpacing/X/000", StringComparison.Ordinal))
                return new(dimension.Start.X - OutsideTextOffsetMm, line.Y);
            if (id.EndsWith("/NominalSpacing/X/" + (slab ? "008" : "006"), StringComparison.Ordinal))
                return new(dimension.End.X + OutsideTextOffsetMm, line.Y);
            if (id.EndsWith("/NominalSpacing/Y/" + (slab ? "008" : "005"), StringComparison.Ordinal))
                return new(line.X, dimension.End.Y - OutsideTextOffsetMm);
        }
        if (Math.Abs(dimension.LogicalMeasurementMm - (slab ? 30 : 45)) < 1e-7 &&
            id.EndsWith("/NominalSpacing/Y/000", StringComparison.Ordinal))
            return new(line.X, dimension.Start.Y + OutsideTextOffsetMm);
        return null;
    }
}
