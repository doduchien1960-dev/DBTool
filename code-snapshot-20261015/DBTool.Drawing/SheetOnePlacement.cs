using DBTool.Domain.Geometry;
using DBTool.Domain;

namespace DBTool.Drawing;

/// <summary>Family 2.1 and 3.1 six structural views measured in the published Sheet1.
/// Explicit template slots place Plan, AA, BB, WallMeshPlan, SteelAA, SteelBB.</summary>
public static class SheetOnePlacement
{
    public static OperationResult<SheetLayoutSheet> Arrange(DrawingPackage package, PresentationProfile profile, int pageCount)
    {
        var frames = new List<SheetViewFrame>();
        foreach (var view in package.Views)
        {
            if (profile.PrimaryScale != 30 || Origin(view.Id,package) is not { } point || view.Bounds is not { } b)
                return VhgNamedSlots.Arrange(package,profile,pageCount,1,id=>Origin(id));
            var origin = new Point2Mm((point.X + b.MinX) / 30, (point.Y + b.MinY) / 30);
            var paper = new Bounds2Mm(origin.X, origin.Y, (point.X + b.MaxX) / 30, (point.Y + b.MaxY) / 30);
            if (paper.MinX < 0 || paper.MinY < 0 || paper.MaxX > profile.PaperWidthMm || paper.MaxY > profile.PaperHeightMm)
                return VhgNamedSlots.Arrange(package,profile,pageCount,1,id=>Origin(id));
            frames.Add(new(view.Id, view.Purpose, b, paper, new(origin, 30), "1:30",
                package.Primitives.Where(p => p.ViewId == view.Id).Select(p => p.Role).Distinct(StringComparer.Ordinal)));
        }
        if(frames.Count>0)
        {
            var printable=profile.PrintableBounds;
            double minX=frames.Min(f=>f.PaperBounds.MinX),maxX=frames.Max(f=>f.PaperBounds.MaxX);
            double minY=frames.Min(f=>f.PaperBounds.MinY),maxY=frames.Max(f=>f.PaperBounds.MaxY);
            if(maxX-minX>profile.PrintableWidthMm+1e-9 || maxY-minY>profile.PrintableHeightMm+1e-9)
                return VhgNamedSlots.Arrange(package,profile,pageCount,1,id=>Origin(id));
            double dx=Math.Clamp(0,printable.MinX-minX,printable.MaxX-maxX);
            double dy=Math.Clamp(0,printable.MinY-minY,printable.MaxY-maxY);
            frames=frames.Select(f=>new SheetViewFrame(f.ViewId,f.Purpose,f.LogicalBounds,
                new(f.PaperBounds.MinX+dx,f.PaperBounds.MinY+dy,f.PaperBounds.MaxX+dx,f.PaperBounds.MaxY+dy),
                new(new(f.Transform.Origin.X+dx,f.Transform.Origin.Y+dy),f.Transform.UniformScaleDenominator),
                f.Label,f.RequiredRoles)).ToList();
        }
        return new(true, new(1, pageCount, profile.PrintableBounds, frames), []);
    }

    public static OperationResult<SheetLayoutSheet> ArrangeSheet1A(DrawingPackage package,PresentationProfile profile,int totalPages) =>
        ArrangeLarge(package,profile,totalPages,false);
    public static OperationResult<SheetLayoutSheet> ArrangeSheet1B(DrawingPackage package,PresentationProfile profile,int totalPages) =>
        ArrangeLarge(package,profile,totalPages,true);
    private static OperationResult<SheetLayoutSheet> ArrangeLarge(DrawingPackage package,PresentationProfile profile,int totalPages,bool steel)
    {
        if(profile.PrimaryScale!=30 || profile.AllowedScales.Any(s=>s!=30)) return Failure("1:30 required");
        var frames=new List<SheetViewFrame>();
        foreach(var view in package.Views)
        {
            if(view.Bounds is not {} b || LargeCell(view.Id,steel) is not {} cell) return Failure(view.Id);
            double width=b.MaxX-b.MinX,height=b.MaxY-b.MinY;
            if(width>cell.MaxX-cell.MinX+300 || height>cell.MaxY-cell.MinY+300) return Failure(view.Id);
            var origin=new Point2Mm((cell.MinX+(cell.MaxX-cell.MinX-width)/2)/30,(cell.MinY+(cell.MaxY-cell.MinY-height)/2)/30);
            frames.Add(new(view.Id,view.Purpose,b,new(origin.X,origin.Y,origin.X+width/30,origin.Y+height/30),new(origin,30),"1:30",
                package.Primitives.Where(p=>p.ViewId==view.Id).Select(p=>p.Role).Distinct()));
        }
        return new(true,new(steel?2:1,totalPages,profile.PrintableBounds,frames),[]);
    }
    private static Bounds2Mm? LargeCell(string id,bool steel) => (steel,id) switch
    {
        (false,"Plan") => new(300,4300,6200,8550),
        (false,"AA") => new(300,50,6200,4300),
        (false,"BB") => new(6400,50,12300,4300),
        (false,"WallMeshPlan") => new(6400,4300,12300,8550),
        (true,"SteelAA") => new(300,100,6200,8750),
        (true,"SteelBB") => new(6400,100,12300,8750),
        (true,"AngleSchedule") => new(6400,3300,12300,8550),
        (true,"AccessoryDetail/Collar") => new(6400,50,9200,3100),
        (true,"NeckDetail/Neck") => new(9400,50,12300,3100),
        _ => null
    };

    private static OperationResult<SheetLayoutSheet> Failure(string view) => new(false, null,
        [new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null,
            "Known Sheet1 template slot wholly inside printable bounds at 1:30", view, "View cannot be placed without crop or clamp.")]);

    public static Point2Mm? Origin(string view) => view switch
    {
        "Plan" => new(2295, 6655), "AA" => new(2295, 1730),
        "BB" => new(6185, 5800), "WallMeshPlan" => new(6185, 2595),
        "SteelAA" => new(10075, 5800), "SteelBB" => new(10075, 1730),
        _ => null
    };

    public static Point2Mm? Title(string view) => view switch
    {
        "Plan" => new(2295, 8610), "AA" => new(2295, 4550),
        "BB" => new(6185, 8610), "WallMeshPlan" => new(6185, 4550),
        "SteelAA" => new(10075, 8610), "SteelBB" => new(10075, 4550),
        _ => null
    };

    /// <summary>Geometry-driven slots; the published cap datum aligns the upper sections.</summary>
    public static Point2Mm? Origin(string view,DrawingPackage package)
    {
        if(Datums(package) is not {} d) return Origin(view);
        var old=Origin(view); if(old is null) return null;
        double x=view is "Plan" or "AA"?d.FirstX:view is "BB" or "WallMeshPlan"?d.FirstX+d.Pitch:d.FirstX+2*d.Pitch;
        double y=view switch
        {
            "Plan"=>7530-d.HalfLength,
            "WallMeshPlan"=>d.BottomTitle-1080-d.HalfLength,
            "BB" or "SteelAA"=>7830-d.CapTop,
            _=>Math.Max(d.BottomTitle-d.CapTop-1040-d.PadBottom, -d.PadBottom + 300)
        };
        return new(x,y);
    }
    public static Point2Mm? Title(string view,DrawingPackage package)
    {
        if(Datums(package) is not {} d) return Title(view);
        if(Title(view) is null) return null;
        double x=view is "Plan" or "AA"?d.FirstX:view is "BB" or "WallMeshPlan"?d.FirstX+d.Pitch:d.FirstX+2*d.Pitch;
        return new(x,view is "AA" or "WallMeshPlan" or "SteelBB"?d.BottomTitle:8610);
    }
    public static Point2Mm? Title(SheetViewFrame frame,DrawingPackage package)
    {
        if(Title(frame.ViewId,package) is not {} title || Origin(frame.ViewId,package) is not {} origin)return null;
        double scale=frame.Transform.UniformScaleDenominator;
        return new(title.X+frame.Transform.Origin.X*scale-origin.X-frame.LogicalBounds.MinX,
            title.Y+frame.Transform.Origin.Y*scale-origin.Y-frame.LogicalBounds.MinY);
    }
    private sealed record PlacementDatums(double FirstX,double Pitch,double HalfLength,double CapTop,double BottomTitle,double PadBottom);
    private static PlacementDatums? Datums(DrawingPackage package)
    {
        var basis=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.ComponentId=="Base" &&
            (p.Closed || p.Role==SheetOnePlanBaseOutlinePolicy.Role)).SelectMany(p=>p.Points).ToArray();
        var cap=package.Primitives.OfType<BlockPrimitive>().FirstOrDefault(p=>p.SemanticId=="BB/Elevation/NeckSeat");
        if(basis.Length<4 || cap is null) return null;
        double width=basis.Max(p=>p.X)-basis.Min(p=>p.X);
        double halfLength=(basis.Max(p=>p.Y)-basis.Min(p=>p.Y))/2;
        double offset=width<=1500?0:width<=1650?50:100;
        double bottom=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="BB" && p.ComponentId=="Pad" && p.Closed &&
            p.Role is "Geometry/Material" or SheetOneSectionOutlinePolicy.Role).SelectMany(p=>p.Points).Select(p=>p.Y).DefaultIfEmpty(-250).Min();
        return new(2295+offset,3890+2*offset,halfLength,cap.Insertion.Y,6580-cap.Insertion.Y,bottom);
    }

    public static SheetLayoutSheet Apply(SheetLayoutSheet sheet,DrawingPackage? package=null)
    {
        if (sheet.PageIndex != 1) return sheet;
        var frames = sheet.Frames.Select(f =>
        {
            if ((package is null?Origin(f.ViewId):Origin(f.ViewId,package)) is not { } point) return f;
            var b = f.LogicalBounds;
            var origin = new Point2Mm((point.X + b.MinX) / 30, (point.Y + b.MinY) / 30);
            return new SheetViewFrame(f.ViewId, f.Purpose, b,
                new(origin.X, origin.Y, origin.X + (b.MaxX - b.MinX) / 30, origin.Y + (b.MaxY - b.MinY) / 30),
                new(origin, 30), f.Label, f.RequiredRoles);
        });
        return new(sheet.PageIndex, sheet.PageCount, sheet.PrintableBounds, frames);
    }
}
