using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public sealed record ViewTransform
{
    public ViewTransform(Point2Mm origin, double uniformScaleDenominator, bool rotated = false)
    {
        Origin = origin;
        UniformScaleDenominator = ContractGuard.Positive(uniformScaleDenominator, nameof(uniformScaleDenominator));
        Rotated = rotated;
    }
    public Point2Mm Origin { get; }
    public double UniformScaleDenominator { get; }
    public bool Rotated { get; }
    public double Scale => 1d / UniformScaleDenominator;
    public string OrientationLabel => Rotated ? "Rotated 90°" : "Normal";
    /// <summary>Local millimetres to paper. Normalize minima before rotation; the origin is the placed lower left.</summary>
    public Point2Mm Apply(Point2Mm point, Bounds2Mm bounds)
    {
        var x = point.X - bounds.MinX;
        var y = point.Y - bounds.MinY;
        return Rotated
            ? new(Origin.X + (bounds.MaxY - bounds.MinY - y) * Scale, Origin.Y + x * Scale)
            : new(Origin.X + x * Scale, Origin.Y + y * Scale);
    }

    /// <summary>Paper placement lifted to model space without scaling physical geometry twice.</summary>
    public ViewTransform ToModel(double plotDenominator, double sheetOffsetMm = 0) =>
        new(new Point2Mm(Origin.X * plotDenominator + sheetOffsetMm, Origin.Y * plotDenominator),
            UniformScaleDenominator / ContractGuard.Positive(plotDenominator, nameof(plotDenominator)), Rotated);
}

public sealed record SheetViewFrame
{
    public SheetViewFrame(string viewId, ViewPurpose purpose, Bounds2Mm logicalBounds, Bounds2Mm paperBounds,
        ViewTransform transform, string label, IEnumerable<string> requiredRoles)
    {
        ViewId = ContractGuard.Text(viewId, nameof(viewId));
        Purpose = ContractGuard.EnumValue(purpose);
        LogicalBounds = ContractGuard.Required(logicalBounds);
        PaperBounds = ContractGuard.Required(paperBounds);
        Transform = ContractGuard.Required(transform);
        Label = ContractGuard.Text(label, nameof(label));
        RequiredRoles = ContractGuard.Copy(requiredRoles);
    }
    public string ViewId { get; }
    public ViewPurpose Purpose { get; }
    public Bounds2Mm LogicalBounds { get; }
    public Bounds2Mm PaperBounds { get; }
    public ViewTransform Transform { get; }
    public string Label { get; }
    public ImmutableArray<string> RequiredRoles { get; }
}

public sealed record SheetLayoutSheet
{
    public SheetLayoutSheet(int pageIndex, int pageCount, Bounds2Mm printableBounds, IEnumerable<SheetViewFrame> frames,string? sourcePlanPageId=null)
    {
        if (pageIndex <= 0 || pageCount <= 0 || pageIndex > pageCount) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        PageIndex = pageIndex;
        PageCount = pageCount;
        PrintableBounds = ContractGuard.Required(printableBounds);
        Frames = ContractGuard.Copy(frames);
        SourcePlanPageId=sourcePlanPageId;
    }
    public int PageIndex { get; }
    public int PageCount { get; }
    public Bounds2Mm PrintableBounds { get; }
    public ImmutableArray<SheetViewFrame> Frames { get; }
    public string? SourcePlanPageId {get;}
}

public sealed record SheetLayout
{
    public SheetLayout(string themeId, IEnumerable<SheetLayoutSheet> sheets)
    {
        ThemeId = ContractGuard.Text(themeId, nameof(themeId));
        Sheets = ContractGuard.Copy(sheets);
        if (Sheets.IsEmpty) throw new ArgumentException("At least one sheet is required.");
        if (!Sheets.Select(s => s.PageIndex).SequenceEqual(Enumerable.Range(1, Sheets.Length)))
            throw new ArgumentException("Sheet page indexes must be contiguous.");
    }
    public string ThemeId { get; }
    public ImmutableArray<SheetLayoutSheet> Sheets { get; }
}

public static class SheetLayoutPlanner
{
    private sealed record PlannedView(ViewDefinition View, Bounds2Mm LogicalBounds, double Scale, double Width, double Height,
        bool Rotated, ImmutableArray<string> RequiredRoles);

    public static OperationResult<SheetLayout> Arrange(DrawingPackage package, PresentationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(profile);
        var planned = new List<PlannedView>();
        foreach (var view in package.Views.OrderBy(v => v.Purpose).ThenBy(v => v.Bounds?.MaxX ?? double.MaxValue).ThenBy(v => v.Id, StringComparer.Ordinal))
        {
            if (view.Bounds is null) return Block("LAYOUT_VIEW_BOUNDS_MISSING", view.Id);
            var picked = PickScale(view, package, profile);
            if (picked is null) return Block("LAYOUT_VIEW_TOO_LARGE", view.Id);
            planned.Add(picked);
        }

        var sheets = new List<List<SheetViewFrame>>();
        var printable = profile.PrintableBounds;
        var availableWidth = profile.PrintableWidthMm;
        var availableHeight = profile.PrintableHeightMm;
        var cursorY = 0d;
        var cursorX = 0d;
        var rowHeight = 0d;
        var current = new List<SheetViewFrame>();
        sheets.Add(current);

        foreach (var view in planned)
        {
            if (current.Count == 0)
            {
                cursorX = 0;
                cursorY = 0;
                rowHeight = 0;
            }
            else if (cursorX + view.Width > availableWidth + 1e-12)
            {
                cursorX = 0;
                cursorY += rowHeight + profile.InterViewGapMm;
                rowHeight = 0;
            }
            if (cursorY + view.Height > availableHeight + 1e-12)
            {
                current = [];
                sheets.Add(current);
                cursorX = 0;
                cursorY = 0;
                rowHeight = 0;
            }
            var origin = new Point2Mm(printable.MinX + cursorX, printable.MinY + cursorY);
            var paperBounds = new Bounds2Mm(origin.X, origin.Y, origin.X + view.Width, origin.Y + view.Height);
            current.Add(new SheetViewFrame(view.View.Id, view.View.Purpose, view.LogicalBounds, paperBounds,
                new ViewTransform(origin, view.Scale, view.Rotated), PresentationProfile.LabelFor(view.Scale), view.RequiredRoles));
            cursorX += view.Width + profile.InterViewGapMm;
            rowHeight = Math.Max(rowHeight, view.Height);
        }

        var output = sheets.Select((frames, index) => new SheetLayoutSheet(index + 1, sheets.Count, printable, frames)).ToArray();
        return new(true, new SheetLayout(profile.ThemeId, output), []);
    }

    /// <summary>Pack within explicit pages. Overflow blocks rather than changing primary page order/count.</summary>
    public static OperationResult<SheetLayout> Arrange(DrawingPackage package, PresentationProfile profile, SheetPlan plan,
        bool useVhgSheetTwoSlots=false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var assigned=plan.Pages.SelectMany(p=>p.ViewIds).ToArray();
        if(!assigned.Order(StringComparer.Ordinal).SequenceEqual(package.Views.Select(v=>v.Id).Order(StringComparer.Ordinal)))
            return Block("SHEET_PLAN_VIEW_COVERAGE", "All package views must be assigned once.");
        var pages=plan.Pages.Where(p=>p.Content!=SheetContent.Auxiliary).ToArray();
        bool large=pages.Any(p=>p.Content==SheetContent.StructureSteel);
        var sheets=new List<SheetLayoutSheet>();
        foreach(var page in pages)
        {
            if(page.ViewIds.IsEmpty) { sheets.Add(new(sheets.Count+1,Math.Max(pages.Length,sheets.Count+1),profile.PrintableBounds,[],page.Id)); continue; }
            var ids=page.ViewIds.ToHashSet(StringComparer.Ordinal);
            var subset=new DrawingPackage(package.InputHash,package.RuleVersion,package.Views.Where(v=>ids.Contains(v.Id)),
                package.Primitives.Where(p=>ids.Contains(p.ViewId)),package.Bounds,package.Scope);
            OperationResult<SheetLayoutSheet>? placed = useVhgSheetTwoSlots ? page.Content switch
            {
                SheetContent.Structure => large ? SheetOnePlacement.ArrangeSheet1A(subset,profile,pages.Length) : SheetOnePlacement.Arrange(subset,profile,pages.Length),
                SheetContent.StructureSteel => SheetOnePlacement.ArrangeSheet1B(subset,profile,pages.Length),
                SheetContent.BaseSlab => SheetLargePlacement.ArrangeBaseSlab(subset,profile,sheets.Count+1,pages.Length),
                SheetContent.Walls12 => SheetLargePlacement.ArrangeWalls(subset,profile,sheets.Count+1,pages.Length,true),
                SheetContent.Walls34 => SheetLargePlacement.ArrangeWalls(subset,profile,sheets.Count+1,pages.Length,false),
                SheetContent.Roof => SheetLargePlacement.ArrangeRoof(subset,profile,sheets.Count+1,pages.Length),
                SheetContent.BaseAndWalls12 => SheetTwoPlacement.Arrange(subset,profile,pages.Length,package),
                SheetContent.Walls34AndRoof => SheetThreePlacement.Arrange(subset,profile,pages.Length,package),
                // Physical neck/catch projections and schedules have model-derived extents.
                // Normalize those extents with the existing packer, including the requested scale.
                SheetContent.NeckAndCatch or SheetContent.Tables => null,
                // Physical bending schematics have variable extents; use the existing envelope packer.
                SheetContent.Bending => null,
                _ => null
            } : null;
            if(placed is not null)
            {
                if(!placed.Success) return new(false,null,placed.Diagnostics);
                sheets.Add(new(sheets.Count+1,Math.Max(pages.Length,sheets.Count+1),placed.Value!.PrintableBounds,placed.Value.Frames,page.Id));
                continue;
            }
            var arranged=page.Content is SheetContent.Tables or SheetContent.NeckAndCatch
                ? ArrangeCompactDetails(subset,profile):Arrange(subset,profile);
            if(!arranged.Success) return arranged;
            if(arranged.Value!.Sheets.Length!=1 && page.Content is not (SheetContent.Tables or SheetContent.NeckAndCatch or SheetContent.Bending))
                return Block("SHEET_PLAN_PAGE_OVERFLOW",page.Id);
            foreach(var child in arranged.Value.Sheets)
                sheets.Add(new(sheets.Count+1,Math.Max(pages.Length,sheets.Count+1),profile.PrintableBounds,child.Frames,page.Id));
        }
        return new(true,new SheetLayout(profile.ThemeId,sheets.Select((s,i)=>new SheetLayoutSheet(i+1,sheets.Count,
            s.PrintableBounds,s.Frames,s.SourcePlanPageId))),[]);
    }

    // Place the largest ink envelope first and reuse space beside and above it.
    // The shelf packer wastes the full height of a schedule alongside short neck views.
    private static OperationResult<SheetLayout> ArrangeCompactDetails(DrawingPackage package,PresentationProfile profile)
    {
        var planned=new List<PlannedView>();
        foreach(var view in package.Views)
        {
            if(view.Bounds is null) return Block("LAYOUT_VIEW_BOUNDS_MISSING",view.Id);
            var picked=PickScale(view,package,profile);
            if(picked is null)return Block("LAYOUT_VIEW_TOO_LARGE",view.Id);
            planned.Add(picked);
        }
        var free=new List<Bounds2Mm>{profile.PrintableBounds};
        var frames=new List<SheetViewFrame>();
        var completed=new List<List<SheetViewFrame>>();
        foreach(var view in planned.OrderByDescending(v=>v.Width*v.Height).ThenBy(v=>v.View.Id,StringComparer.Ordinal))
        {
            // Paper space for the title above measured ink, without changing physical bounds.
            double titleBand=view.View.Id.StartsWith("Reinforcement/Neck/",StringComparison.Ordinal) ||
                view.View.Id.StartsWith("Reinforcement/Catch/",StringComparison.Ordinal)?12:0;
            double height=view.Height+titleBand;
            Bounds2Mm? FindSlot()=>free.Where(b=>b.MaxX-b.MinX>=view.Width-1e-9 && b.MaxY-b.MinY>=height-1e-9)
                .OrderBy(b=>b.MinY).ThenBy(b=>b.MinX).ThenBy(b=>b.MaxX).FirstOrDefault();
            var slot=FindSlot();
            if(slot is null && frames.Count>0)
            {
                completed.Add(frames);frames=[];free=[profile.PrintableBounds];slot=FindSlot();
            }
            if(slot is null)return Block("LAYOUT_DETAIL_PACKING_OVERFLOW",view.View.Id);
            free.Remove(slot);
            double right=slot.MinX+view.Width+profile.InterViewGapMm;
            double top=slot.MinY+height+profile.InterViewGapMm;
            if(right<slot.MaxX-1e-9)free.Add(new(right,slot.MinY,slot.MaxX,slot.MaxY));
            if(top<slot.MaxY-1e-9)free.Add(new(slot.MinX,top,slot.MinX+view.Width,slot.MaxY));
            var origin=new Point2Mm(slot.MinX,slot.MinY);
            frames.Add(new(view.View.Id,view.View.Purpose,view.LogicalBounds,
                new(origin.X,origin.Y,origin.X+view.Width,origin.Y+view.Height),
                new(origin,view.Scale,view.Rotated),PresentationProfile.LabelFor(view.Scale),view.RequiredRoles));
        }
        completed.Add(frames);
        return new(true,new SheetLayout(profile.ThemeId,completed.Select((f,i)=>new SheetLayoutSheet(i+1,completed.Count,
            profile.PrintableBounds,f))),[]);
    }
    /// <summary>Only the published base width triggers the large structure sheet; length is not the threshold.</summary>
    public static bool RequiresAdditionalSheets(DrawingPackage package)
    {
        var points=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.ComponentId=="Base").SelectMany(p=>p.Points).ToArray();
        return points.Length>0 && points.Max(p=>p.X)-points.Min(p=>p.X)>2200;
    }

    private static PlannedView? PickScale(ViewDefinition view, DrawingPackage package, PresentationProfile profile)
    {
        var scales = new[] { profile.PrimaryScale }.Concat(profile.AllowedScales.Where(s => s != profile.PrimaryScale)).ToArray();
        foreach (var scale in scales)
        {
            double width = (view.Bounds!.MaxX - view.Bounds.MinX) / scale;
            double height = (view.Bounds.MaxY - view.Bounds.MinY) / scale;
            bool rotated = false;
            if (width > profile.PrintableWidthMm + 1e-12 || height > profile.PrintableHeightMm + 1e-12)
            {
                if (!profile.AllowRotation || height > profile.PrintableWidthMm + 1e-12 || width > profile.PrintableHeightMm + 1e-12)
                    continue;
                (width, height) = (height, width);
                rotated = true;
            }
            var roles = package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
            return new PlannedView(view, view.Bounds, scale, width, height, rotated, roles);
        }
        return null;
    }

    private static OperationResult<SheetLayout> Block(string code, string viewId) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null,
            "Every view must fit at one allowed scale", viewId, "View cannot be placed without crop or clamp.")]);
}
