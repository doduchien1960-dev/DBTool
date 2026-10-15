using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Family 2.1 Sheet 4 placement: Cổ ga detailing (Plan, AA, BB) and Rebar Schedule.</summary>
public static class SheetFourPlacement
{
    /// <summary>Title position in sheet model coordinates, derived from the packed paper frame.</summary>
    public static Point2Mm Title(SheetViewFrame frame,double plotDenominator,double gapPaperMm=8)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ContractGuard.Positive(plotDenominator,nameof(plotDenominator));
        ContractGuard.NonNegative(gapPaperMm,nameof(gapPaperMm));
        return new((frame.PaperBounds.MinX/2+frame.PaperBounds.MaxX/2)*plotDenominator,
            (frame.PaperBounds.MaxY+gapPaperMm)*plotDenominator);
    }

    public static OperationResult<SheetLayoutSheet> Arrange(DrawingPackage package, PresentationProfile profile, int pageCount, int pageIndex = 4)
    {
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (profile.PrimaryScale != 30 || Origin(view.Id) is not {} point || view.Bounds is not {} b)
                return Failure(view.Id);
            var origin = new Point2Mm((point.X + b.MinX) / 30, (point.Y + b.MinY) / 30);
            var paper = new Bounds2Mm(origin.X, origin.Y, (point.X + b.MaxX) / 30, (point.Y + b.MaxY) / 30);
            var printable = profile.PrintableBounds;
            double minY = view.Id == "SteelSchedule" ? 5 : printable.MinY;
            double maxX = view.Id == "SteelSchedule" ? 412 : printable.MaxX;
            if (paper.MinX < printable.MinX || paper.MinY < minY || paper.MaxX > maxX || paper.MaxY > printable.MaxY)
                return Failure(view.Id);
            frames.Add(new(view.Id, view.Purpose, b, paper, new(origin, 30), "1:30",
                package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct(StringComparer.Ordinal)));
        }
        return new(true, new(pageIndex, pageCount, profile.PrintableBounds, frames), []);
    }

    private static OperationResult<SheetLayoutSheet> Failure(string view) => new(false, null,
        [new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null,
            "Known Sheet4 template slot wholly inside printable bounds at 1:30", view, "View cannot be placed without crop or clamp.")]);

    public static Point2Mm? Origin(string view) => view switch
    {
        "Reinforcement/Neck/Plan" => new(2295, 7235),
        "Reinforcement/Neck/AA" => new(2295, 5220),
        "Reinforcement/Neck/BB" => new(2295, 3050),
        "Reinforcement/Catch/Plan" => new(5685, 7235),
        "Reinforcement/Catch/1-1" => new(5685, 4755),
        "Reinforcement/Catch/2-2" => new(5685, 2805),
        "SteelSchedule" => new(4500, 8610),
        _ => null
    };

    public static Point2Mm? Title(string view) => view switch
    {
        "Reinforcement/Neck/Plan" => new(2295, 8610),
        "Reinforcement/Neck/AA" => new(2295, 6040),
        "Reinforcement/Neck/BB" => new(2295, 3870),
        "Reinforcement/Catch/Plan" => new(5685, 8610),
        "Reinforcement/Catch/1-1" => new(5685, 5650),
        "Reinforcement/Catch/2-2" => new(5685, 3735),
        _ => null
    };

    public static Point2Mm? Title(string view, string? manholeCode)
    {
        var code = Family21ManholeHelper.Normalize(manholeCode ?? "");
        if (code is null || !view.StartsWith("Reinforcement/Neck/", StringComparison.Ordinal)) return Title(view);
        var x = Family21ManholeHelper.GetSheetTwoThreeLayout(code).FirstX;
        var (aa, bb) = code switch
        {
            "G500-2" => (6040d, 3820d),
            "G600-3" => (6040d, 3920d),
            "G800-1" => (6190d, 4120d),
            "G800-2" => (5990d, 3820d),
            _ => (6040d, 3870d)
        };
        return view switch
        {
            SheetFourViews.PlanViewId => new(x, 8610),
            SheetFourViews.AAViewId => new(x, aa),
            SheetFourViews.BBViewId => new(x, bb),
            _ => null
        };
    }

    public static SheetLayoutSheet Apply(SheetLayoutSheet sheet)
    {
        if (sheet.PageIndex != 4 && sheet.PageIndex != 5) return sheet;
        var frames = sheet.Frames.Select(f =>
        {
            if (Origin(f.ViewId) is not {} point) return f;
            var b = f.LogicalBounds;
            var origin = new Point2Mm((point.X + b.MinX) / 30, (point.Y + b.MinY) / 30);
            return new SheetViewFrame(f.ViewId, f.Purpose, b,
                new(origin.X, origin.Y, origin.X + (b.MaxX - b.MinX) / 30, origin.Y + (b.MaxY - b.MinY) / 30),
                new(origin, 30), f.Label, f.RequiredRoles);
        });
        return new(sheet.PageIndex, sheet.PageCount, sheet.PrintableBounds, frames);
    }
}
