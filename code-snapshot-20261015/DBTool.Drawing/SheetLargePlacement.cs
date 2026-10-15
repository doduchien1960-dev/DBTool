using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Dedicated placements for large manholes where standard multi-view pages exceed A3 paper limits.</summary>
public static class SheetLargePlacement
{
    public static OperationResult<SheetLayoutSheet> ArrangeBaseSlab(DrawingPackage package, PresentationProfile profile, int page, int pageCount)
    {
        if (profile.PrimaryScale != 30) return Failure("1:30 required");
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (view.Bounds is not { } b) return Failure(view.Id);
            double width = b.MaxX - b.MinX, height = b.MaxY - b.MinY;
            var cell = view.Id switch
            {
                "BaseTop" => new Bounds2Mm(300, 100, 6200, 8750),
                "BaseBottom" => new Bounds2Mm(6400, 100, 12300, 8750),
                _ => new Bounds2Mm(300, 100, 12300, 8750)
            };
            if (width > cell.MaxX - cell.MinX + 150 || height > cell.MaxY - cell.MinY + 150) return Failure(view.Id);
            var origin = new Point2Mm((cell.MinX + (cell.MaxX - cell.MinX - width) / 2) / 30, (cell.MinY + (cell.MaxY - cell.MinY - height) / 2) / 30);
            frames.Add(new(view.Id, view.Purpose, b, new(origin.X, origin.Y, origin.X + width / 30, origin.Y + height / 30),
                new(origin, 30), "1:30", package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct()));
        }
        return new(true, new(page, pageCount, profile.PrintableBounds, frames), []);
    }

    public static OperationResult<SheetLayoutSheet> ArrangeWalls(DrawingPackage package, PresentationProfile profile, int page, int pageCount, bool walls12)
    {
        if (profile.PrimaryScale != 30) return Failure("1:30 required");
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (view.Bounds is not { } b) return Failure(view.Id);
            double width = b.MaxX - b.MinX, height = b.MaxY - b.MinY;
            bool isFirstWall = walls12 ? view.Id.Contains("T1", StringComparison.Ordinal) : view.Id.Contains("T3", StringComparison.Ordinal);
            bool isUpper = view.Id.Contains("Outer", StringComparison.Ordinal) || view.Id.Contains("Top", StringComparison.Ordinal);
            double minX = isFirstWall ? 300 : 6400, maxX = minX + 5900;
            double minY = isUpper ? 4400 : 50, maxY = isUpper ? 8800 : 4400;
            if (width > maxX - minX + 150 || height > maxY - minY + 150) return Failure(view.Id);
            var origin = new Point2Mm((minX + (maxX - minX - width) / 2) / 30, (minY + (maxY - minY - height) / 2) / 30);
            frames.Add(new(view.Id, view.Purpose, b, new(origin.X, origin.Y, origin.X + width / 30, origin.Y + height / 30),
                new(origin, 30), "1:30", package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct()));
        }
        return new(true, new(page, pageCount, profile.PrintableBounds, frames), []);
    }

    public static OperationResult<SheetLayoutSheet> ArrangeRoof(DrawingPackage package, PresentationProfile profile, int page, int pageCount)
    {
        if (profile.PrimaryScale != 30) return Failure("1:30 required");
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (view.Bounds is not { } b) return Failure(view.Id);
            double width = b.MaxX - b.MinX, height = b.MaxY - b.MinY;
            var cell = view.Id switch
            {
                var id when id.Contains("Top", StringComparison.Ordinal) || id.Contains("Outer", StringComparison.Ordinal) => new Bounds2Mm(300, 100, 6200, 8750),
                var id when id.Contains("Bottom", StringComparison.Ordinal) || id.Contains("Inner", StringComparison.Ordinal) => new Bounds2Mm(6400, 100, 12300, 8750),
                "Reinforcement/Neck/Plan" => new Bounds2Mm(6400, 5500, 12300, 8800),
                "Reinforcement/Neck/AA" => new Bounds2Mm(6400, 50, 9300, 5200),
                "Reinforcement/Neck/BB" => new Bounds2Mm(9400, 50, 12300, 5200),
                _ => new Bounds2Mm(300, 100, 6200, 8750)
            };
            if (width > cell.MaxX - cell.MinX + 150 || height > cell.MaxY - cell.MinY + 150) return Failure(view.Id);
            var origin = new Point2Mm((cell.MinX + (cell.MaxX - cell.MinX - width) / 2) / 30, (cell.MinY + (cell.MaxY - cell.MinY - height) / 2) / 30);
            frames.Add(new(view.Id, view.Purpose, b, new(origin.X, origin.Y, origin.X + width / 30, origin.Y + height / 30),
                new(origin, 30), "1:30", package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct()));
        }
        return new(true, new(page, pageCount, profile.PrintableBounds, frames), []);
    }

    private static OperationResult<SheetLayoutSheet> Failure(string view) => new(false, null,
        [new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null,
            "Large manhole dedicated slot wholly inside printable bounds at 1:30", view, "View cannot be placed without crop or clamp.")]);
}
