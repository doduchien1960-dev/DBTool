using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Named VHG three-column/two-row cells. Never reorders, adds pages, or reduces scale.</summary>
public static class VhgNamedSlots
{
    public static OperationResult<SheetLayoutSheet> Arrange(DrawingPackage package,PresentationProfile profile,int count,int page,
        Func<string,Point2Mm?> legacyOrigin)
    {
        var frames=new List<SheetViewFrame>();
        foreach(var view in package.Views)
        {
            if(profile.PrimaryScale!=30 || legacyOrigin(view.Id) is not {} legacy || view.Bounds is not {} b)
                return Fail(view.Id);
            int col=(int)Math.Round((legacy.X-2295)/3390);
            // Structure uses a different published horizontal pitch.
            if(view.Id is "BB" or "WallMeshPlan") col=1;
            if(view.Id is "SteelAA" or "SteelBB") col=2;
            bool upper=view.Id is "Plan" or "BB" or "SteelAA" || (view.Id is not ("AA" or "WallMeshPlan" or "SteelBB") && legacy.Y>4000);
            double minX=300+col*4000,maxX=minX+3790;
            double minY=upper?4500:300,maxY=upper?8310:4260;
            double width=b.MaxX-b.MinX,height=b.MaxY-b.MinY;
            if(col<0 || col>2 || width>maxX-minX || height>maxY-minY) return Fail(view.Id);
            var origin=new Point2Mm((minX+(maxX-minX-width)/2)/30,(maxY-height)/30);
            frames.Add(new(view.Id,view.Purpose,b,new(origin.X,origin.Y,origin.X+width/30,origin.Y+height/30),new(origin,30),"VHG named slots 1:30",
                package.Primitives.Where(p=>p.ViewId==view.Id).Select(p=>p.Role).Distinct()));
        }
        return new(true,new(page,count,profile.PrintableBounds,frames),[]);
    }
    private static OperationResult<SheetLayoutSheet> Fail(string id)=>new(false,null,[new Diagnostic("SHEET_PLAN_PAGE_OVERFLOW",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,
        "Named VHG cell at 1:30",id,"The view does not fit its assigned cell; no crop or scale reduction is allowed.")]);
}
