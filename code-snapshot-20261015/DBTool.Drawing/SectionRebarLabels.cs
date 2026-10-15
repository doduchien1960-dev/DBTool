using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Section callouts name physical owner faces and supplied bar diameters; schedules are never recalculated.</summary>
public static class SectionRebarLabels
{
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,RebarLayout layout,ViewProfile aa,ViewProfile bb,
        IReadOnlyList<DrawingPrimitive> projected, double scaleDenominator = 30)
    {
        var result=new List<DrawingPrimitive>();
        double scaleRatio = scaleDenominator / 30.0;
        double textHeight = 2.0 * scaleDenominator;

        foreach(var pair in new[]{("SteelAA",aa),("SteelBB",bb)})
        {
            string view=pair.Item1;var profile=pair.Item2;
            var wallCuts=profile.Regions.Where(r=>r.SourceComponentId.StartsWith("Wall/") && r.SourceComponentId!="Wall/Mortar")
                .Select(r=>r.Footprint.Outer.Vertices.ToArray()).ToArray();
            double leftInside=wallCuts.Where(p=>p.Average(x=>x.X)<0).Select(p=>p.Max(x=>x.X)).DefaultIfEmpty(0).Max();
            double rightInside=wallCuts.Where(p=>p.Average(x=>x.X)>0).Select(p=>p.Min(x=>x.X)).DefaultIfEmpty(0).Min();
            Point2Mm? Target(string owner,Layer? layer,Point2Mm preferred)
            {
                var ids=layout.Occurrences.Where(b=>b.ComponentId==owner && b.FamilyId!="Dowel" && (!layer.HasValue || b.Layer==layer)).Select(b=>b.Id).ToHashSet();
                var points=projected.Where(p=>p.ViewId==view && p.ComponentId==owner && p.OccurrenceId is not null && ids.Contains(p.OccurrenceId))
                    .SelectMany(p=>p switch { CirclePrimitive c=>new[]{c.Center}, PolylinePrimitive l=>
                        l.Points.Zip(l.Points.Skip(1),(a,b)=>Closest(a,b,preferred)).Concat(l.Points).ToArray(), _=>Array.Empty<Point2Mm>() });
                return points.OrderBy(p=>Math.Pow(p.X-preferred.X,2)+Math.Pow(p.Y-preferred.Y,2)).Select(p=>(Point2Mm?)p).FirstOrDefault();
            }
            void Label(string id,string owner,Point2Mm target,Point2Mm text,string content,bool right=false)
            {
                result.Add(new PolylinePrimitive($"{view}/Callout/{id}/Leader","Rebar/Leader",view,owner,null,null,
                    [target,new(text.X,text.Y-20*scaleRatio),new(text.X+(right?-300*scaleRatio:300*scaleRatio),text.Y-20*scaleRatio)],false));
                result.Add(new TextPrimitive($"{view}/Callout/{id}",right?"Rebar/MarkRight":"Rebar/Mark",view,owner,null,null,text,content,textHeight,0));
            }
            // A callout is permitted only when its physical dowel is visible in
            // this section; diameter is consumed from that occurrence, not a label constant.
            var dowels=layout.Occurrences.Where(b=>b.FamilyId=="Dowel").ToDictionary(b=>b.Id);
            var visibleDowel=projected.OfType<PolylinePrimitive>().Where(p=>p.ViewId==view &&
                p.OccurrenceId is not null && dowels.ContainsKey(p.OccurrenceId) && p.Points.Length>=2)
                .OrderBy(p=>p.Points.Average(q=>q.X)).ThenBy(p=>p.SemanticId,StringComparer.Ordinal).FirstOrDefault();
            if(visibleDowel is not null)
            {
                var a=visibleDowel.Points[0];var b=visibleDowel.Points[^1];
                var target=new Point2Mm((a.X+b.X)/2,(a.Y+b.Y)/2);
                Label("Dowel",visibleDowel.ComponentId!,target,new(target.X-390*scaleRatio,target.Y+50*scaleRatio),
                    $"Chốt D{dowels[visibleDowel.OccurrenceId!].DiameterMm}");
            }
            foreach(var owner in profile.Regions.Select(r=>r.SourceComponentId).Distinct().Where(id=>id.StartsWith("Wall/") && id!="Wall/Mortar"))
            {
                var p=profile.Regions.Where(r=>r.SourceComponentId==owner).SelectMany(r=>r.Footprint.Outer.Vertices).ToArray();
                double min=p.Min(x=>x.X),max=p.Max(x=>x.X);bool left=(min+max)<0;
                foreach(bool outside in new[]{false,true})
                {
                    double target=outside?(left?min+30:max-30):(left?max-30:min+30);
                    double x=outside?(left?min-90*scaleRatio:max+90*scaleRatio):(left?max+90*scaleRatio:min-90*scaleRatio);
                    var point=Target(owner,null,new(target,!outside && left?50:60));
                    if(point.HasValue) Label(owner+outside,owner,point.Value,new(x,!outside && left?190:140),
                        owner.Replace("Wall/","")+(outside?" ngoài":" trong"),outside?left:!left);
                }
            }
            foreach(string owner in new[]{"Base","Roof","Neck"})
            {
                var regions=profile.Regions.Where(r=>r.SourceComponentId==owner).ToArray();
                if(regions.Length==0) continue;
                var component=geometry.Components.Single(c=>c.Id==owner);
                foreach(var group in layout.Occurrences.Where(b=>b.ComponentId==owner).GroupBy(b=>owner=="Neck"?Layer.Bottom:b.Layer))
                {
                    bool top=group.Key==Layer.Top;
                    string name=owner=="Base"?"Đáy":owner=="Roof"?"Trần":"Cổ ga";
                    var vertices=regions.SelectMany(r=>r.Footprint.Outer.Vertices).ToArray();
                    double min=vertices.Min(p=>p.X),max=vertices.Max(p=>p.X);
                    double neckBodyTop=geometry.Components.FirstOrDefault(c=>c.Id=="Collar")?.ZRange.Start ?? component.ZRange.End;
                    var preferred=new Point2Mm(owner=="Neck"?min+30:0,owner=="Neck"?
                        (component.ZRange.Start+neckBodyTop)/2:top?component.ZRange.End-35:component.ZRange.Start+35);
                    var point=Target(owner,owner=="Neck"?null:group.Key,preferred);
                    if(!point.HasValue) continue;
                    double neckTop=Math.Max(vertices.Max(p=>p.Y),
                        geometry.PurchasedCovers.Select(c=>c.Anchor.Z).DefaultIfEmpty(vertices.Max(p=>p.Y)).Max());
                    double textY=owner=="Base"?(top?80:-380):owner=="Roof"?component.ZRange.Start+(top?260:-130):neckTop-225;
                    double clearWidth=rightInside>leftInside?rightInside-leftInside:max-min;
                    double textX=owner=="Base"?-clearWidth/(top?4:3)-60:owner=="Neck"?min-285*scaleRatio:
                        top?(view=="SteelAA"?min+180:max-180):(view=="SteelAA"?40:-230);
                    if(owner=="Neck")
                    {
                        var anchor=new Point2Mm(min-95*scaleRatio,textY);
                        result.Add(new PolylinePrimitive($"{view}/Callout/{owner}{group.Key}/Leader","Rebar/Leader",view,owner,null,null,
                            [point.Value,new(anchor.X,anchor.Y-20*scaleRatio),new(anchor.X-210*scaleRatio,anchor.Y-20*scaleRatio)],false));
                        result.Add(new TextPrimitive($"{view}/Callout/{owner}{group.Key}","Rebar/MarkRight",view,owner,null,null,anchor,name,textHeight,0));
                    }
                    else Label(owner+group.Key,owner,point.Value,new(textX,textY),$"{name} {(top?"trên":"dưới")}",
                        owner=="Roof" && top && view=="SteelAA");
                }
            }
        }
        return result;
    }
    private static Point2Mm Closest(Point2Mm a,Point2Mm b,Point2Mm p)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,length=dx*dx+dy*dy;
        double t=length==0?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length,0,1);
        return new(a.X+t*dx,a.Y+t*dy);
    }
}
