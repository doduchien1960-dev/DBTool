using System.Globalization;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Native Sheet2 callouts; nominal display aliases never replace canonical provenance.</summary>
public static class SheetTwoBarAnnotations
{
    public const string Role="Rebar/SheetTwoCallout";
    public static IReadOnlyList<DrawingPrimitive> Build(IEnumerable<DrawingPrimitive> primitives,
        IReadOnlyDictionary<string,(int Diameter,double? Pitch)> metadata,bool useNominalLabels=false)
    {
        var snapshot=primitives.ToArray();
        var result=new List<DrawingPrimitive>();
        foreach(var view in snapshot.OfType<PolylinePrimitive>().Where(p=>
            SheetTwoPlacement.Title(p.ViewId) is not null && !p.ViewId.Contains("/Roof/",StringComparison.Ordinal) &&
            p.OccurrenceId is not null && p.ScheduleMark is not null &&
            p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom").GroupBy(p=>p.ViewId))
        {
            var context=snapshot.OfType<PolylinePrimitive>().Where(p=>p.ViewId==view.Key &&
                p.Role is "Geometry/BaseOutline" or SheetTwoMaterialViews.BaseRole or SheetTwoMaterialViews.WallRole).SelectMany(p=>p.Points).ToArray();
            if(context.Length==0) context=view.SelectMany(p=>p.Points).ToArray();
            double left=context.Min(p=>p.X),bottom=context.Min(p=>p.Y),top=context.Max(p=>p.Y);
            if(view.Key is not ("BaseTop" or "BaseBottom")) left=context.Where(p=>p.Y==top).Min(p=>p.X);
            int horizontalSlot=0,verticalSlot=0;
            bool nominal=useNominalLabels;
            var groups=nominal
                ? (IEnumerable<IGrouping<string, PolylinePrimitive>>)view.Where(p=>metadata.TryGetValue(p.OccurrenceId!,out var d) && d.Pitch is >0)
                      .GroupBy(p=>{
                          var runs=Enumerable.Range(1,p.Points.Length-1).Select(i=>
                              (Length:Distance(p.Points[i-1],p.Points[i]),
                               Horiz:Math.Abs(p.Points[i].X-p.Points[i-1].X)>=Math.Abs(p.Points[i].Y-p.Points[i-1].Y)));
                          var longest=runs.OrderByDescending(r=>r.Length).FirstOrDefault();
                          return longest.Horiz?"Horiz":"Vert";
                      })
                      .OrderByDescending(g=>g.Key=="Horiz")
                : view.GroupBy(p=>p.ScheduleMark!).OrderBy(g=>g.Key,StringComparer.Ordinal);
            foreach(var family in groups)
            {
                var eligible=family.Where(p=>metadata.TryGetValue(p.OccurrenceId!,out var data) && data.Pitch is >0).ToArray();
                if(eligible.Length==0) continue;
                // The longest segment is the main run, not an end hook. Each split row retains its own label.
                var runs=eligible.SelectMany(p=>Enumerable.Range(1,p.Points.Length-1).Select(i=>
                    (Bar:p,A:p.Points[i-1],B:p.Points[i],Length:Distance(p.Points[i-1],p.Points[i])))).ToArray();
                var run=runs.OrderByDescending(r=>r.Length).ThenBy(r=>r.Bar.SemanticId,StringComparer.Ordinal).First();
                bool horizontal=Math.Abs(run.B.X-run.A.X)>=Math.Abs(run.B.Y-run.A.Y);
                bool slab=view.Key is "BaseTop" or "BaseBottom";
                // Source first-case callout lanes: 5mm below slab, 5mm below wall base,
                // and 1mm outside left edge; later split rows use separate lanes.
                bool bottomLane=slab?horizontal:!horizontal;
                bool inner=view.Key.Contains("/Inner/",StringComparison.Ordinal);
                var insertion=bottomLane
                    ? new Point2Mm(left+(slab?340:nominal&&inner?340:315)+420*(verticalSlot%3),bottom-150-180*(verticalSlot++/3))
                    : new Point2Mm(left-30,top-(slab?280:345)-180*horizontalSlot++);
                var closest=runs.Select(r=>(r.Bar,Point:Closest(r.A,r.B,insertion)))
                    .OrderBy(r=>Distance(r.Point,insertion)).ThenBy(r=>r.Bar.SemanticId,StringComparer.Ordinal).First();
                var data=metadata[closest.Bar.OccurrenceId!];
                string id=view.Key+"/Callout/"+closest.Bar.ScheduleMark;
                string block=slab && horizontal?"Block:BLT1":"Block:BLT2";
                string displayMark=nominal?(slab?(horizontal?"D1":"D2"):(horizontal?"T2":"T1")):family.Key!;
                result.Add(new BlockPrimitive(id,Role,view.Key,closest.Bar.ComponentId,block,insertion,.3,
                    [new("SH",displayMark),new("DK","%%c"+data.Diameter.ToString(CultureInfo.InvariantCulture)),
                        new("A",data.Pitch is >0 ? "@"+data.Pitch.Value.ToString("0.###",CultureInfo.InvariantCulture):" ")],
                    occurrenceId:closest.Bar.OccurrenceId,scheduleMark:closest.Bar.ScheduleMark));
                if(closest.Point!=insertion)
                    result.Add(new PolylinePrimitive(id+"/Leader","Rebar/Leader",view.Key,closest.Bar.ComponentId,
                        closest.Bar.OccurrenceId,closest.Bar.ScheduleMark,[closest.Point,insertion],false));
                if(nominal && inner && !horizontal)
                {
                    // Detail the existing bent foot, without creating another physical bar.
                    var hooks=runs.Where(r=>Math.Abs(r.A.Y-r.B.Y)<1e-7 && r.A.Y<0 && r.Length>1e-7).ToArray();
                    if(hooks.Length==0) continue;
                    double hookY=hooks.Min(r=>r.A.Y);
                    var hookInsertion=new Point2Mm(left-30,hookY);
                    var hook=hooks.Select(r=>(r.Bar,Point:Closest(r.A,r.B,hookInsertion)))
                        .OrderBy(r=>Distance(r.Point,hookInsertion)).ThenBy(r=>r.Bar.SemanticId,StringComparer.Ordinal).First();
                    var hookData=metadata[hook.Bar.OccurrenceId!];
                    result.Add(new BlockPrimitive(id+"/Anchorage",Role,view.Key,hook.Bar.ComponentId,"Block:BLT2",hookInsertion,.3,
                        [new("SH","T3"),new("DK","%%c"+hookData.Diameter.ToString(CultureInfo.InvariantCulture)),
                            new("A","@"+hookData.Pitch!.Value.ToString("0.###",CultureInfo.InvariantCulture))],
                        occurrenceId:hook.Bar.OccurrenceId,scheduleMark:hook.Bar.ScheduleMark));
                    if(hook.Point!=hookInsertion)
                        result.Add(new PolylinePrimitive(id+"/Anchorage/Leader","Rebar/Leader",view.Key,hook.Bar.ComponentId,
                            hook.Bar.OccurrenceId,hook.Bar.ScheduleMark,[hook.Point,hookInsertion],false));
                }
            }

            // Provide full callout and leader coverage for any additional bar families/marks (e.g. split bars, haunch bars, supplementary bars) with known pitch
            var coveredMarks = result.OfType<BlockPrimitive>().Where(b => b.ViewId == view.Key).Select(b => b.ScheduleMark).ToHashSet(StringComparer.Ordinal);
            var remainingFamilies = view.Where(p => p.ScheduleMark is not null && !coveredMarks.Contains(p.ScheduleMark)
                                                 && metadata.TryGetValue(p.OccurrenceId!, out var d) && d.Pitch is > 0)
                                        .GroupBy(p => p.ScheduleMark!)
                                        .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var remFamily in remainingFamilies)
            {
                var runs = remFamily.SelectMany(p => Enumerable.Range(1, p.Points.Length - 1).Select(i =>
                    (Bar: p, A: p.Points[i - 1], B: p.Points[i], Length: Distance(p.Points[i - 1], p.Points[i])))).ToArray();
                if (runs.Length == 0) continue;
                var run = runs.OrderByDescending(r => r.Length).ThenBy(r => r.Bar.SemanticId, StringComparer.Ordinal).First();
                bool horizontal = Math.Abs(run.B.X - run.A.X) >= Math.Abs(run.B.Y - run.A.Y);
                bool slab = view.Key is "BaseTop" or "BaseBottom";
                bool bottomLane = slab ? horizontal : !horizontal;
                var insertion = bottomLane
                    ? new Point2Mm(left + (slab ? 340 : 315) + 420 * (verticalSlot % 3), bottom - 150 - 180 * (verticalSlot++ / 3))
                    : new Point2Mm(left - 30, top - (slab ? 280 : 345) - 180 * horizontalSlot++);
                var closest = runs.Select(r => (r.Bar, Point: Closest(r.A, r.B, insertion)))
                    .OrderBy(r => Distance(r.Point, insertion)).ThenBy(r => r.Bar.SemanticId, StringComparer.Ordinal).First();
                metadata.TryGetValue(closest.Bar.OccurrenceId!, out var data);
                int diam = data.Diameter > 0 ? data.Diameter : 10;
                string pitchStr = data.Pitch is > 0 ? "@" + data.Pitch.Value.ToString("0.###", CultureInfo.InvariantCulture) : " ";
                string id = view.Key + "/Callout/" + remFamily.Key;
                string block = slab && horizontal ? "Block:BLT1" : "Block:BLT2";
                result.Add(new BlockPrimitive(id, Role, view.Key, closest.Bar.ComponentId, block, insertion, 0.3,
                    [new("SH", remFamily.Key), new("DK", "%%c" + diam), new("A", pitchStr)],
                    occurrenceId: closest.Bar.OccurrenceId, scheduleMark: closest.Bar.ScheduleMark));
                if (closest.Point != insertion)
                    result.Add(new PolylinePrimitive(id + "/Leader", "Rebar/Leader", view.Key, closest.Bar.ComponentId,
                        closest.Bar.OccurrenceId, closest.Bar.ScheduleMark, [closest.Point, insertion], false));
            }
        }
        return result;
    }

    private static double Distance(Point2Mm a,Point2Mm b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    /// <summary>Conservative native BLT1/BLT2 envelope at scale .3, including canonical mark text.
    /// The host still checks actual entity extents; this does not bypass its cropping gate.</summary>
    public static IEnumerable<Point2Mm> Envelope(IEnumerable<DrawingPrimitive> annotations)
    {
        foreach(var p in annotations)
            if(p is BlockPrimitive b && b.Role==Role)
            {
                yield return new(b.Insertion.X-420,b.Insertion.Y-120);
                yield return new(b.Insertion.X+420,b.Insertion.Y+120);
            }
            else if(p is PolylinePrimitive line)
                foreach(var point in line.Points) yield return point;
    }
    private static Point2Mm Closest(Point2Mm a,Point2Mm b,Point2Mm p)
    {
        double x=b.X-a.X,y=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y),0,1);
        return new(a.X+t*x,a.Y+t*y);
    }
}

