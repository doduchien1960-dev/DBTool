using DBTool.Domain.Geometry;
using DBTool.Domain;

namespace DBTool.Drawing;

/// <summary>Family2.1 six mesh positions measured in the published first-case Sheet2.
/// Separate physical accessory projections remain visible in the owning outer-wall panel.</summary>
public static class SheetTwoPlacement
{
    /// <summary>Explicit template slots, including accessory views owned by the same physical panel.
    /// Generic shelf packing cannot express those intentional overlays. Every frame must still fit.</summary>
    public static OperationResult<SheetLayoutSheet> Arrange(DrawingPackage package,PresentationProfile profile,int pageCount,DrawingPackage? placementPackage=null)
    {
        var frames=new List<SheetViewFrame>();
        foreach(var view in package.Views)
        {
            if(profile.PrimaryScale!=30 || (placementPackage is null?Origin(view.Id):Origin(view.Id,placementPackage)) is not {} point || view.Bounds is not {} b)
                return placementPackage is null ? Failure(view.Id) : VhgNamedSlots.Arrange(package,profile,pageCount,2,id=>Origin(id));
            var origin=new Point2Mm((point.X+b.MinX)/30,(point.Y+b.MinY)/30);
            var paper=new Bounds2Mm(origin.X,origin.Y,(point.X+b.MaxX)/30,(point.Y+b.MaxY)/30);
            var printable=profile.PrintableBounds;
            if(paper.MinX<printable.MinX || paper.MinY<printable.MinY || paper.MaxX>printable.MaxX || paper.MaxY>printable.MaxY)
                return placementPackage is null ? Failure(view.Id) : VhgNamedSlots.Arrange(package,profile,pageCount,2,id=>Origin(id));
            frames.Add(new(view.Id,view.Purpose,b,paper,new(origin,30),"1:30",
                package.Primitives.Where(p=>p.ViewId==view.Id).Select(p=>p.Role).Distinct(StringComparer.Ordinal)));
        }
        return new(true,new(2,pageCount,profile.PrintableBounds,frames),[]);
    }
    private static OperationResult<SheetLayoutSheet> Failure(string view)=>new(false,null,
        [new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
            "Known Sheet2 template slot wholly inside printable bounds at 1:30",view,"View cannot be placed without crop or clamp.")]);
    public static Point2Mm? Origin(string view) => view switch
    {
        "BaseTop" => new(2295,6835), "BaseBottom" => new(2295,2775),
        "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom" => new(5685,6160),
        "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom" => new(5685,2100),
        "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom" => new(9075,6160),
        "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom" => new(9075,2100),
        _ => null
    };
    public static Point2Mm? Title(string view) => view switch
    {
        "BaseTop" => new(2295,8610), "BaseBottom" => new(2295,4550),
        "Reinforcement/Wall/T1/Wall/T1/Outer/Top" => new(5685,8610),
        "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom" => new(5685,4550),
        "Reinforcement/Wall/T2/Wall/T2/Outer/Top" => new(9075,8610),
        "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom" => new(9075,4550),
        _ => null
    };
    public static Point2Mm? Origin(string view, DrawingPackage? package, string? manholeCode = null)
    {
        var code = Family21ManholeHelper.Normalize(manholeCode ?? "") ??
            (package is null ? null : Family21ManholeHelper.ResolveFromPackage(package));
        if (code is not null) return Family21ManholeHelper.GetSheetTwoOrigin(view, code);
        return package is null ? Origin(view) : SheetMeshDynamicPlacement.Origin(view, Origin(view), package);
    }

    public static Point2Mm? Title(string view, DrawingPackage? package, string? manholeCode = null)
    {
        var code = Family21ManholeHelper.Normalize(manholeCode ?? "") ??
            (package is null ? null : Family21ManholeHelper.ResolveFromPackage(package));
        if (code is not null) return Family21ManholeHelper.GetSheetTwoTitle(view, code);
        return package is null ? Title(view) : SheetMeshDynamicPlacement.Title(Title(view), package);
    }
    public static SheetLayoutSheet Apply(SheetLayoutSheet sheet,DrawingPackage? package=null,string? manholeCode=null)
    {
        if(sheet.PageIndex!=2) return sheet;
        var frames=sheet.Frames.Select(f=>
        {
            if((Origin(f.ViewId,package,manholeCode)) is not {} point) return f;
            var b=f.LogicalBounds;
            var origin=new Point2Mm((point.X+b.MinX)/30,(point.Y+b.MinY)/30);
            return new SheetViewFrame(f.ViewId,f.Purpose,b,
                new(origin.X,origin.Y,origin.X+(b.MaxX-b.MinX)/30,origin.Y+(b.MaxY-b.MinY)/30),
                new(origin,30),f.Label,f.RequiredRoles);
        });
        return new(sheet.PageIndex,sheet.PageCount,sheet.PrintableBounds,frames);
    }
}
