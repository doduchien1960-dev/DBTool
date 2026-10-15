using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Wall-plan guides and face labels derived from wall locations and unchanged bar occurrences.</summary>
public static class WallMeshPresentation
{
    // Six millimetres of paper-space clearance between the wall diagram and its lower annotations.
    public const double GraphicOffsetY = 180;
    public static Point2Mm GraphicPoint(Point2Mm point) => new(point.X,point.Y+GraphicOffsetY);
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry, RebarLayout layout)
    {
        var walls=geometry.Components.Where(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar").ToArray();
        if(walls.Length!=4) return [];
        var result=new List<DrawingPrimitive>();
        var all=walls.SelectMany(w=>w.Footprint.Outer.Vertices).ToArray();
        double left=all.Min(p=>p.X),right=all.Max(p=>p.X),bottom=all.Min(p=>p.Y),top=all.Max(p=>p.Y);
        var basePoints=geometry.Components.FirstOrDefault(c=>c.Id=="Base")?.Footprint.Outer.Vertices;
        double guideLeft=basePoints?.Min(p=>p.X)+30 ?? left,
            guideRight=basePoints?.Max(p=>p.X)-30 ?? right,
            guideBottom=basePoints?.Min(p=>p.Y)+30 ?? bottom,
            guideTop=basePoints?.Max(p=>p.Y)-30 ?? top;
        var guides=new HashSet<(bool,double)>();
        foreach(var wall in walls)
        {
            var p=wall.Footprint.Outer.Vertices;
            double minX=p.Min(x=>x.X),maxX=p.Max(x=>x.X),minY=p.Min(x=>x.Y),maxY=p.Max(x=>x.Y);
            double cx=(minX+maxX)/2,cy=(minY+maxY)/2;
            bool horizontal=maxX-minX>maxY-minY;
            foreach(var bar in layout.Occurrences.Where(b=>b.ComponentId==wall.Id && b.Direction==RebarDirection.Vertical))
            {
                var leg=bar.Path.Segments.OfType<LineSegment3Mm>().MaxBy(s=>Math.Abs(s.End.Z-s.Start.Z));
                if(leg is null) continue;
                double station=horizontal?leg.Start.X:leg.Start.Y;
                if(!guides.Add((horizontal,station))) continue;
                Point2Mm a=GraphicPoint(horizontal?new(station,guideBottom):new(guideLeft,station)),
                    b=GraphicPoint(horizontal?new(station,guideTop):new(guideRight,station));
                result.Add(new PolylinePrimitive($"WallMeshPlan/Guide/{bar.Id}","Rebar/HiddenSecondary","WallMeshPlan",wall.Id,bar.Id,null,[a,b],false));
            }
            foreach(bool outside in new[]{false,true})
            {
                double direction=horizontal?Math.Sign(cy-(bottom+top)/2):Math.Sign(cx-(left+right)/2);
                Point2Mm target=horizontal?new(cx,outside?(direction>0?maxY-30:minY+30):(direction>0?minY+30:maxY-30))
                    :new(outside?(direction>0?maxX-30:minX+30):(direction>0?minX+30:maxX-30),cy);
                target=GraphicPoint(target);
                // Native first-sheet labels use baseline anchors, including three right-justified labels.
                // Offsets are presentation distances from the actual wall edges, never bar/quantity changes.
                bool rightAligned=!horizontal && (direction>0 || outside);
                Point2Mm text=horizontal
                    ? new(cx-240,direction>0?maxY+(outside?348:-58):minY+(outside?-8:398))
                    : direction<0?new(outside?minX-148:maxX+48,cy+260)
                    : new(minX-(outside?60:48),cy+(outside?-10:410));
                Point2Mm elbow=new(text.X,text.Y-20);
                Point2Mm landing=new(text.X+(rightAligned?-360:360),text.Y-20);
                string id=$"WallMeshPlan/Label/{wall.Id}/{outside}";
                result.Add(new PolylinePrimitive(id+"/Leader","Rebar/Leader","WallMeshPlan",wall.Id,null,null,[target,elbow,landing],false));
                result.Add(new TextPrimitive(id,rightAligned?"Rebar/MarkRight":"Rebar/Mark","WallMeshPlan",wall.Id,null,null,text,
                    wall.Id.Replace("Wall/","")+ (outside?" - ngoài":" - trong"),60,0));
            }
        }
        return result;
    }
}
