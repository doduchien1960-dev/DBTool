using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Visible rung silhouette outside the concrete; physical embedment and hooks remain in the occurrence.</summary>
public static class SheetOneLadderPresentation
{
    public static IReadOnlyList<DrawingPrimitive> Build(RebarLayout layout,IReadOnlyDictionary<string,string> marks,
        ViewProfile aa,ViewProfile bb, double scale = 30)
    {
        var result=new List<DrawingPrimitive>();
        foreach(var (view,profile) in new[]{("AA",aa),("BB",bb)})
        foreach(var group in layout.Occurrences.Where(b=>b.FamilyId=="Ladder").GroupBy(b=>b.ComponentId))
        {
            var regions=profile.Regions.Where(r=>r.SourceComponentId==group.Key).ToArray();
            var vertices=regions.SelectMany(r=>r.Footprint.Outer.Vertices).ToArray();
            double inner=vertices.Length==0?0:vertices.Average(p=>p.X)<0?vertices.Max(p=>p.X):vertices.Min(p=>p.X);
            Point2Mm Map(Point3Mm p)=>new(view=="AA"?p.Y:p.X,p.Z);
            var leg=group.First().Path.Segments[1];
            bool front=Math.Abs(Map(leg.Start).X-Map(leg.End).X)<1e-6;
            var visible=new List<(BarOccurrence Bar,Point2Mm Face,Point2Mm End)>();
            foreach(var bar in group.OrderByDescending(b=>b.Path.Segments[1].End.Z))
            {
                // This contract is already validated by BendingSchematicPlanner; reject silently inventing a silhouette.
                if(!Bending2DPlanner.UnfoldLadder(bar.Path).Success) throw new InvalidOperationException("Invalid ladder silhouette contract");
                // The other orthographic view sees the free transverse tread.
                // Project that physical segment even when its support wall is behind the section.
                var segment=bar.Path.Segments[front?2:1];
                var a=Map(segment.Start);var b=Map(segment.End);
                if(Math.Abs(a.Y-b.Y)>1e-6 || Math.Abs(a.X-b.X)<1e-6) continue;
                double t=front?0:(inner-a.X)/(b.X-a.X);
                if(t<0 || t>1) throw new InvalidOperationException("Ladder leg does not cross its inner support face");
                var face=front?a:new Point2Mm(inner,a.Y);
                if(face==b) continue;
                visible.Add((bar,face,b));
                foreach(var id in new[]{view,"Steel"+view})
                    result.Add(new PathPrimitive($"{id}/{bar.Id}/VisibleRung","Rebar/Main",id,bar.ComponentId,bar.Id,
                        marks[bar.Id],[new LinePathSegment(face,b)],false,ViewPurpose.Section,bar.ScheduleLengthMm,
                        DrawingMaterialKind.RoundSteel,bar.Id));
            }
            if(visible.Count==0) continue;
            if(group.All(b=>b.RuleId=="LADDER-PUBLISHED-1"))
            {
                var highest=visible[0];
                double rungLeft=Math.Min(highest.Face.X,highest.End.X),rungRight=Math.Max(highest.Face.X,highest.End.X);
                double capTop=profile.Regions.Where(r=>r.SourceComponentId is "NeckSeat" or "Neck" or "Collar")
                    .SelectMany(r=>r.Footprint.Outer.Vertices).Select(p=>p.Y).DefaultIfEmpty(highest.End.Y).Max();
                double face=front?rungLeft:highest.Face.X;
                double dimensionX=front?rungLeft-120:highest.End.X+Math.Sign(highest.End.X-highest.Face.X)*150;
                foreach(var id in new[]{view,"Steel"+view})
                {
                    AddDimension("Width",new(rungLeft,highest.End.Y),new(rungRight,highest.End.Y),new(rungRight,highest.End.Y-180),false);
                    if(capTop>highest.End.Y)AddDimension("TopOffset",new(face,capTop),new(face,highest.End.Y),new(dimensionX,highest.End.Y),true);
                    if(visible.Count>1)AddDimension("Pitch",new(face,highest.End.Y),new(face,visible[1].End.Y),new(dimensionX,visible[1].End.Y),true);
                    void AddDimension(string name,Point2Mm a,Point2Mm b,Point2Mm at,bool vertical)=>
                        result.Add(new DimensionIntentPrimitive($"{id}/Ladder/{group.Key}/{name}","Dimension/Linear",id,group.Key,null,null,
                            a,b,Math.Abs(vertical?b.Y-a.Y:b.X-a.X),"<>",0,vertical?Math.PI/2:0,at));
                }
            }
            var target=visible[front?0:Math.Min(1,visible.Count-1)];
            double left=Math.Min(target.Face.X,target.End.X),right=Math.Max(target.Face.X,target.End.X);
            double direction=Math.Sign(target.End.X-target.Face.X);
            var text=front?new Point2Mm(left-60,target.End.Y+110):new Point2Mm(target.End.X+direction*30,target.End.Y-130);
            var anchor=front?new Point2Mm(left+30,target.End.Y):new Point2Mm(target.End.X-direction*60,target.End.Y);
            var elbow=new Point2Mm(text.X,text.Y-20);
            var end=new Point2Mm(front?right:elbow.X+direction*360,elbow.Y);
            foreach(var id in new[]{view,"Steel"+view})
            {
                result.Add(new PolylinePrimitive($"{id}/Ladder/{group.Key}/Leader","Rebar/Leader",id,group.Key,null,null,
                    [anchor,elbow,end],false));
                double textHeight = scale > 0 ? 2.0 * scale : 60;
                result.Add(new TextPrimitive($"{id}/Ladder/{group.Key}/Label","Rebar/Mark",id,group.Key,null,null,
                    text,$"Thang D{target.Bar.DiameterMm}",textHeight,0));
            }
        }
        return result;
    }
}
