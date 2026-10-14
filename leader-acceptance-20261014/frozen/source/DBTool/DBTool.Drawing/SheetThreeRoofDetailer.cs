using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Roof plan context from physical geometry, retaining every projected bar and canonical mark.</summary>
public static class SheetThreeRoofDetailer
{
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,IEnumerable<DrawingPrimitive> projected,string view,
        MeshSpec? mesh=null,double? coverMm=null,IReadOnlySet<string>? mainBarIds=null,string? manholeCode=null,
        IReadOnlyDictionary<string,(int Diameter,double? Pitch)>? metadata=null,double scaleDenominator=30)
    {
        var roof=geometry.Components.Single(c=>c.Id=="Roof");
        var result=projected.Where(p=>p.ViewId==view).ToList();
        var outer=roof.Footprint.Outer.Vertices;
        double left=outer.Min(p=>p.X),right=outer.Max(p=>p.X),bottom=outer.Min(p=>p.Y),top=outer.Max(p=>p.Y);
        result.Add(new PolylinePrimitive(view+"/Physical/Outline","Geometry/BaseOutline",view,"Roof",null,null,outer,true));
        Dimension("Width",new(left,top),new(right,top),new(right,top+360),0);
        bool topLayer=view==SheetThreeRoofViews.TopViewId;
        double rightChain=right+(topLayer?480:180);
        Dimension("Height",new(right,top),new(right,bottom),new(rightChain+180,bottom),Math.PI/2);
        var walls=new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"}
            .Select(id=>geometry.Components.SingleOrDefault(c=>c.Id==id && c.Kind==ComponentKind.Wall)).ToArray();
        if(walls.All(w=>w is not null) && walls.All(w=>Rectangle(w!.Footprint.Outer)))
        {
            double l=walls[3]!.Footprint.Outer.Vertices.Max(p=>p.X),r=walls[1]!.Footprint.Outer.Vertices.Min(p=>p.X),
                b=walls[2]!.Footprint.Outer.Vertices.Max(p=>p.Y),t=walls[0]!.Footprint.Outer.Vertices.Min(p=>p.Y);
            if(left<l && l<r && r<right && bottom<b && b<t && t<top)
            {
                result.Add(new PolylinePrimitive(view+"/Physical/Hidden/Chamber","Geometry/PlanHidden",view,"Roof",null,null,
                    [new(l,t),new(r,t),new(r,b),new(l,b),new(l,t)],false));
                Dimension("Chamber/X/Left",new(left,top),new(l,t),new(l,top+180),0);
                Dimension("Chamber/X/Width",new(l,t),new(r,t),new(r,top+180),0);
                Dimension("Chamber/X/Right",new(r,t),new(right,top),new(right,top+180),0);
                Dimension("Chamber/Y/Top",new(right,top),new(r,t),new(rightChain,t),Math.PI/2);
                Dimension("Chamber/Y/Height",new(r,t),new(r,b),new(rightChain,b),Math.PI/2);
                Dimension("Chamber/Y/Bottom",new(r,b),new(right,bottom),new(rightChain,bottom),Math.PI/2);
            }
        }
        var neck=geometry.Components.SingleOrDefault(c=>c.Id=="Neck" && c.Kind==ComponentKind.Neck);
        if(neck is not null && Rectangle(neck.Footprint.Outer))
            result.Add(new PolylinePrimitive(view+"/Physical/Hidden/Neck","Geometry/PlanHidden",view,"Neck",null,null,
                neck.Footprint.Outer.Vertices,true));
        int holeIndex=0;
        foreach(var hole in roof.Footprint.InnerLoops)
        {
            var p=hole.Vertices;string id="Hole/"+holeIndex++;
            result.Add(new PolylinePrimitive(view+"/Physical/"+id,"Geometry/RoofOpening",view,"Roof",null,null,p,true));
            double a=p.Min(q=>q.X),b=p.Max(q=>q.X),c=p.Min(q=>q.Y),d=p.Max(q=>q.Y);
            Dimension(id+"/Width",new(a,c),new(b,c),new(b,c+120),0);
            Dimension(id+"/Height",new(a,c),new(a,d),new(a+120,d),Math.PI/2);

        }

        if(mesh is not null && coverMm is {} cover && double.IsFinite(cover) && cover>=0 && mainBarIds is not null)
        {
            // Published nominal dimension stations are separate from redistributed fabrication
            // centerlines. These dimensions never alter projected bars, openings or schedules.
            var runs=result.OfType<PolylinePrimitive>().Where(p=>p.Role=="Rebar/Main" && p.OccurrenceId is {} id &&
                mainBarIds.Contains(id) && p.ScheduleMark is not null).Select(p=>
                (Bar:p,Run:p.Points.Zip(p.Points.Skip(1)).OrderByDescending(r=>
                    Math.Pow(r.Second.X-r.First.X,2)+Math.Pow(r.Second.Y-r.First.Y,2)).FirstOrDefault())).ToArray();
            Chain(false);Chain(true);
            void Chain(bool height)
            {
                var candidates=runs.Where(r=>height
                    ?Math.Abs(r.Run.First.Y-r.Run.Second.Y)<1e-7 && Math.Abs(r.Run.First.X-r.Run.Second.X)>1e-7
                    :Math.Abs(r.Run.First.X-r.Run.Second.X)<1e-7 && Math.Abs(r.Run.First.Y-r.Run.Second.Y)>1e-7)
                    .OrderBy(r=>r.Bar.SemanticId,StringComparer.Ordinal).ToArray();
                if(candidates.Length==0)return;
                double span=height?top-bottom:right-left,available=span-2*cover;
                int count=(int)Math.Floor((available+1e-7)/mesh.SpacingMm)-1;
                if(count<1 || available<=0)return;
                double core=count*mesh.SpacingMm,remainder=(available-core)/2;
                var owner=candidates[0].Bar;
                double[] bands=[cover,remainder,core,remainder,cover];
                double cursor=height?top:left;
                for(int i=0;i<bands.Length;i++)
                {
                    double next=cursor+(height?-bands[i]:bands[i]);
                    var a=height?new Point2Mm(left+cover,cursor):new Point2Mm(cursor,bottom+cover);
                    var b=height?new Point2Mm(left+cover,next):new Point2Mm(next,bottom+cover);
                    var line=height?new Point2Mm(left-510,next):new Point2Mm(next,bottom-390);
                    string label=i==2?count.ToString(System.Globalization.CultureInfo.InvariantCulture)+"x"+
                        mesh.SpacingMm.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"=<>":"<>";
                    Dimension("NominalSpacing/"+(height?"Y":"X")+"/"+i,a,b,line,height?Math.PI/2:0,
                        owner.OccurrenceId,owner.ScheduleMark,label);
                    cursor=next;
                }
            }
        }
        var placedHoriz = new List<Point2Mm>();
        var placedVert = new List<Point2Mm>();
        foreach(var family in result.OfType<PolylinePrimitive>().Where(p=>p.Role=="Rebar/Main" &&
            p.OccurrenceId is not null && p.ScheduleMark is not null).GroupBy(p=>p.ScheduleMark).ToArray())
        {
            var line = family.FirstOrDefault(p => mainBarIds?.Contains(p.OccurrenceId!) == true)
                ?? family.OrderByDescending(p=>p.Points.Zip(p.Points.Skip(1)).Sum(e=>
                    Math.Sqrt(Math.Pow(e.First.X-e.Second.X,2)+Math.Pow(e.First.Y-e.Second.Y,2)))).First();
            (int Diameter,double? Pitch) data;
            if(metadata is not null && metadata.TryGetValue(line.OccurrenceId!,out var actual)) data=actual;
            else if(mesh is not null && mainBarIds?.Contains(line.OccurrenceId!)==true) data=(mesh.DiameterMm,mesh.SpacingMm);
            else continue;
            double dx = Math.Abs(line.Points[^1].X - line.Points[0].X);
            double dy = Math.Abs(line.Points[^1].Y - line.Points[0].Y);
            bool isHoriz = dx >= dy;
            Point2Mm tip, end;
            if (isHoriz)
            {
                tip = line.Points.OrderByDescending(p => p.X).First();
                double baseRight = right + (topLayer ? 480 : 360);
                int lane = 0;
                while (placedHoriz.Any(p => Math.Abs(p.X - (baseRight + lane * 150)) < 10 && Math.Abs(p.Y - tip.Y) < 120))
                {
                    lane++;
                }
                end = new Point2Mm(baseRight + lane * 150, tip.Y);
                placedHoriz.Add(end);
            }
            else
            {
                tip = line.Points.OrderBy(p => p.Y).First();
                double baseBottom = bottom - 300;
                int lane = 0;
                while (placedVert.Any(p => Math.Abs(p.Y - (baseBottom - lane * 150)) < 10 && Math.Abs(p.X - tip.X) < 400))
                {
                    lane++;
                }
                end = new Point2Mm(tip.X, baseBottom - lane * 150);
                placedVert.Add(end);
            }
            result.Add(new PolylinePrimitive(view+"/Physical/Callout/"+family.Key+"/Leader","Rebar/Leader",view,"Roof",
                line.OccurrenceId,line.ScheduleMark,[tip,end],false));
            string blockRole = isHoriz ? "Block:BLT1" : "Block:BLT2";
            string diaText = $"%%c{data.Diameter}";
            string pitchText = data.Pitch is > 0 ? FormattableString.Invariant($"@{data.Pitch:0.###}") : " ";
            result.Add(new BlockPrimitive(view+"/Physical/Callout/"+family.Key, SheetTwoBarAnnotations.Role, view, "Roof",
                blockRole, end, scaleDenominator / 100.0,
                [new("SH", family.Key!), new("DK", diaText), new("A", pitchText)],
                occurrenceId: line.OccurrenceId, scheduleMark: line.ScheduleMark));
        }
        return result;
        void Dimension(string suffix,Point2Mm a,Point2Mm b,Point2Mm at,double angle,
            string? occurrence=null,string? mark=null,string label="<>")
        {
            double length=angle==0?Math.Abs(b.X-a.X):Math.Abs(b.Y-a.Y);
            if(length<=0)return;
            if(result.OfType<DimensionIntentPrimitive>().Any(d=>
                Math.Abs((d.RotationRadians ?? 0) - angle)<1e-4 &&
                ((Math.Abs(d.Start.X-a.X)<1e-4 && Math.Abs(d.Start.Y-a.Y)<1e-4 && Math.Abs(d.End.X-b.X)<1e-4 && Math.Abs(d.End.Y-b.Y)<1e-4) ||
                 (Math.Abs(d.Start.X-b.X)<1e-4 && Math.Abs(d.Start.Y-b.Y)<1e-4 && Math.Abs(d.End.X-a.X)<1e-4 && Math.Abs(d.End.Y-a.Y)<1e-4)) &&
                d.DimensionLinePoint.HasValue && Math.Abs(d.DimensionLinePoint.Value.X-at.X)<10 && Math.Abs(d.DimensionLinePoint.Value.Y-at.Y)<10))
                return;
            result.Add(new DimensionIntentPrimitive(view+"/Physical/Dimension/"+suffix,
                suffix.StartsWith("NominalSpacing/",StringComparison.Ordinal)?"Dimension/Roof":"Dimension/RoofOpening",
                view,"Roof",occurrence,mark,a,b,length,label,0,angle,at));
        }
    }
    private static bool Rectangle(Polygon2Mm p)=>p.Vertices.Length==4 &&
        p.Vertices.Select(v=>v.X).Distinct().Count()==2 && p.Vertices.Select(v=>v.Y).Distinct().Count()==2;
}


