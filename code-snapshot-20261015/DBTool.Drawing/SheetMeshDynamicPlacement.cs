using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Mesh-panel datums derived from the same physical extents as the structural sheet.</summary>
public static class SheetMeshDynamicPlacement
{
    public static Point2Mm? Title(Point2Mm? legacy,DrawingPackage package)
    {
        if(legacy is not {} point || Datums(package) is not {} d) return legacy;
        int column=(int)Math.Round((point.X-2295)/3390);
        return new(d.FirstX+column*d.Pitch,point.Y>6000?8610:d.BottomTitle);
    }
    public static Point2Mm? Origin(string view,Point2Mm? legacy,DrawingPackage package)
    {
        if(legacy is not {} point || Datums(package) is not {} d) return legacy;
        int column=(int)Math.Round((point.X-2295)/3390);
        double title=point.Y>4000?8610:d.BottomTitle;
        double y;
        if(view is "BaseTop" or "BaseBottom") y=title-900-d.HalfBaseLength;
        else if(view.Contains("/Roof/",StringComparison.Ordinal))
        {
            var outline=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId==view && p.ComponentId=="Roof" &&
                p.Role is "Geometry/Material" or "Geometry/BaseOutline").SelectMany(p=>p.Points).ToArray();
            if(outline.Length<4) return legacy;
            y=title-900-outline.Max(p=>p.Y);
        }
        else y=title-900-d.RoofTop;
        return new(d.FirstX+column*d.Pitch,y);
    }
    private sealed record PlacementDatums(double FirstX,double Pitch,double BottomTitle,double HalfBaseLength,double RoofTop);
    private static PlacementDatums? Datums(DrawingPackage package)
    {
        var basis=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.ComponentId=="Base" &&
            (p.Closed || p.Role==SheetOnePlanBaseOutlinePolicy.Role)).SelectMany(p=>p.Points).ToArray();
        var roof=package.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="BB" && p.ComponentId=="Roof" &&
            p.Role is "Geometry/Material" or SheetOneSectionOutlinePolicy.Role).SelectMany(p=>p.Points).ToArray();
        var code = Family21ManholeHelper.ResolveFromPackage(package);
        var layout = Family21ManholeHelper.GetSheetTwoThreeLayout(code);
        return new(layout.FirstX, layout.Pitch, layout.BottomTitleY,
            (basis.Max(p=>p.Y)-basis.Min(p=>p.Y))/2,roof.Max(p=>p.Y));
    }
}
