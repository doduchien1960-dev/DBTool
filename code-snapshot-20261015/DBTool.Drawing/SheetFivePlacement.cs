using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Family 2.1 Sheet 5 placement: Rebar Bending Details (15 columns of bending shapes).</summary>
public static class SheetFivePlacement
{
    public static OperationResult<SheetLayoutSheet> Arrange(DrawingPackage package, PresentationProfile profile, int pageCount, int pageIndex = 5)
    {
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (profile.PrimaryScale != 30 || Origin(view.Id) is not {} point || view.Bounds is not {} b)
                return Failure(view.Id);
            var origin = new Point2Mm(point.X / 30, point.Y / 30);
            var paper = new Bounds2Mm(0, 0, 420, 297);
            frames.Add(new(view.Id, view.Purpose, b, paper, new(origin, 30), "1:30",
                package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct(StringComparer.Ordinal)));
        }
        return new(true, new(pageIndex, pageCount, profile.PrintableBounds, frames), []);
    }

    private static OperationResult<SheetLayoutSheet> Failure(string view) => new(false, null,
        [new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null,
            "Known Sheet5 template slot wholly inside printable bounds at 1:30", view, "View cannot be placed without crop or clamp.")]);

    public static Point2Mm? Origin(string view) => view switch
    {
        SheetFiveViews.ViewId => new(0, 0),
        _ => null
    };

    public static Point2Mm? Title(string view) => null;

    public static SheetLayoutSheet Apply(SheetLayoutSheet sheet)
    {
        if (sheet.PageIndex != 5 && sheet.PageIndex != 6) return sheet;
        var frames = sheet.Frames.Select(f =>
        {
            if (Origin(f.ViewId) is not {} point) return f;
            var b = f.LogicalBounds;
            var origin = new Point2Mm(point.X / 30, point.Y / 30);
            return new SheetViewFrame(f.ViewId, f.Purpose, b, new Bounds2Mm(0, 0, 420, 297),
                new(origin, 30), f.Label, f.RequiredRoles);
        });
        return new(sheet.PageIndex, sheet.PageCount, sheet.PrintableBounds, frames);
    }
}
