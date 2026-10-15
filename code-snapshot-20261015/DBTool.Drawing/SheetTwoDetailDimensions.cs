using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public static class SheetTwoDetailDimensions
{
    private sealed record Anchor(Point2Mm Point,string? OccurrenceId,string? Mark);

    public static IReadOnlyList<DimensionIntentPrimitive> Build(GeometryModel geometry,
        IEnumerable<DrawingPrimitive> primitives,IReadOnlySet<string> mainBarIds,
        IReadOnlyDictionary<string,(int Diameter,double? Pitch)>? nominalMetadata=null)
    {
        var snapshot=primitives.ToArray();
        var initial=snapshot.OfType<DimensionIntentPrimitive>().Where(d=>d.Role==SheetTwoDimensionPlanner.Role
            && d.SemanticId.Contains("/Dimension/Outer/Width",StringComparison.Ordinal)).ToArray();
        var basis=geometry.Components.SingleOrDefault(c=>c.Id=="Base");
        if(basis is null) return [];
        var result=new List<DimensionIntentPrimitive>();
        foreach(var outerWidth in initial)
        {
            string view=outerWidth.ViewId;
            var dimensions=snapshot.OfType<DimensionIntentPrimitive>().Where(d=>d.ViewId==view).ToArray();
            var outerHeight=dimensions.Single(d=>d.SemanticId==view+"/Dimension/Outer/Height");
            var innerWidth=dimensions.Single(d=>d.SemanticId==view+"/Dimension/Inner/Width");
            var innerHeight=dimensions.Single(d=>d.SemanticId==view+"/Dimension/Inner/Height");
            var outer=new Bounds2Mm(outerWidth.Start.X,outerHeight.End.Y,outerWidth.End.X,outerHeight.Start.Y);
            var inner=new Bounds2Mm(innerWidth.Start.X,innerHeight.End.Y,innerWidth.End.X,innerHeight.Start.Y);
            double top=innerWidth.DimensionLinePoint!.Value.Y,right=innerHeight.DimensionLinePoint!.Value.X;
            Add("Detail/Width/Left",new(outer.MinX,outer.MaxY),new(inner.MinX,inner.MaxY),new(inner.MinX,top),false);
            Add("Detail/Width/Right",new(inner.MaxX,inner.MaxY),new(outer.MaxX,outer.MaxY),new(outer.MaxX,top),false);
            Add("Detail/Height/Top",new(outer.MaxX,outer.MaxY),new(inner.MaxX,inner.MaxY),new(right,inner.MaxY),true);
            Add("Detail/Height/Bottom",new(inner.MaxX,inner.MinY),new(outer.MaxX,outer.MinY),new(right,outer.MinY),true);
            if(view is not ("BaseTop" or "BaseBottom"))
            {
                bool y=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
                double low=basis.Footprint.Outer.Vertices.Min(p=>y?p.Y:p.X);
                double high=basis.Footprint.Outer.Vertices.Max(p=>y?p.Y:p.X);
                if(Math.Abs(outer.MinX - low) > 1e-3)
                    Add("Detail/BaseExtension/Left",new(outer.MinX,outer.MaxY),new(low,basis.ZRange.End),new(low,top),false);
                if(Math.Abs(high - outer.MaxX) > 1e-3)
                    Add("Detail/BaseExtension/Right",new(outer.MaxX,outer.MaxY),new(high,basis.ZRange.End),new(high,top),false);
            }
            var horizontal=new List<Anchor>();var vertical=new List<Anchor>();
            foreach(var bar in snapshot.OfType<PolylinePrimitive>().Where(p=>p.ViewId==view &&
                p.OccurrenceId is not null && mainBarIds.Contains(p.OccurrenceId) &&
                p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom"))
            {
                var run=bar.Points.Zip(bar.Points.Skip(1),(a,b)=>(A:a,B:b))
                    .OrderByDescending(r=>Math.Pow(r.B.X-r.A.X,2)+Math.Pow(r.B.Y-r.A.Y,2)).FirstOrDefault();
                if(Math.Abs(run.A.Y-run.B.Y)<1e-7 && Math.Abs(run.A.X-run.B.X)>1e-7)
                    horizontal.Add(new(new(Math.Min(run.A.X,run.B.X),run.A.Y),bar.OccurrenceId,bar.ScheduleMark));
                else if(Math.Abs(run.A.X-run.B.X)<1e-7 && Math.Abs(run.A.Y-run.B.Y)>1e-7)
                    vertical.Add(new(new(run.A.X,Math.Min(run.A.Y,run.B.Y)),bar.OccurrenceId,bar.ScheduleMark));
            }
            if(!NominalChain(vertical,false)) Chain(vertical,false);
            if(!NominalChain(horizontal,true)) Chain(horizontal,true);

            bool NominalChain(List<Anchor> runs,bool height)
            {
                // The published mesh sheet is a nominal detailing grid, distinct from
                // the equalized fabrication centerlines. This opt-in never edits bars,
                // schedules or quantities. Split/opening families keep measured chains.
                bool slab=view is "BaseTop" or "BaseBottom";
                if(nominalMetadata is null || runs.Count<2 || horizontal.Count<2 || vertical.Count<2) return false;
                if(slab && horizontal.Concat(vertical).Select(a=>a.Mark).Distinct().Count()!=2) return false;
                var values=runs.Select(a=>nominalMetadata.TryGetValue(a.OccurrenceId!,out var v)?v:default).Distinct().ToArray();
                if(values.Length!=1 || values[0].Diameter<=0 || values[0].Pitch is not >0) return false;
                double pitch=values[0].Pitch!.Value,diameter=values[0].Diameter;
                bool alongY=view.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Contains("/Wall/T4/",StringComparison.Ordinal);
                var wall=slab?null:geometry.Components.SingleOrDefault(c=>c.Id==outerWidth.ComponentId);
                if(!slab && wall is null) return false;
                double physicalLeft=slab?inner.MinX:wall!.Footprint.Outer.Vertices.Min(p=>alongY?p.Y:p.X);
                double cover=vertical.Min(a=>a.Point.X)-physicalLeft-diameter/2;
                if(cover<0) return false;
                // Template convention: a 10d hook has a 9d clear leg between its
                // outside nominal lines. It is a detailing band, not fabricated length.
                double hook=9*diameter;
                double extension=slab?(height?outer.MaxY-inner.MaxY:inner.MinX-outer.MinX):0;
                double span=height?outer.MaxY-outer.MinY:outer.MaxX-outer.MinX;
                double topInset=height&&!slab?inner.MaxY-horizontal.Max(a=>a.Point.Y)-diameter/2:cover;
                double available=height&&!slab?inner.MaxY-topInset-(outer.MinY+cover+hook):span-2*(cover+extension+hook);
                int count=(int)Math.Floor((available+1e-7)/pitch)-1;
                if(count<1 || extension<0 || topInset<0) return false;
                double core=count*pitch,remainder=(available-core)/2;
                double[] bands=height&&!slab?[topInset,remainder,core,remainder,hook,cover]:
                    slab?[cover,extension,hook,remainder,core,remainder,hook,extension,cover]:
                    [cover,hook,remainder,core,remainder,hook,cover];
                int coreIndex=height&&!slab?2:slab?4:3;
                double cursor=height?(slab?outer.MaxY:inner.MaxY):outer.MinX;
                double fixedCoordinate=height?outer.MinX+cover:outer.MinY+cover;
                var owner=runs.OrderBy(a=>a.OccurrenceId,StringComparer.Ordinal).First();
                for(int i=0;i<bands.Length;i++)
                {
                    double next=cursor+(height?-bands[i]:bands[i]);
                    var a=height?new Point2Mm(fixedCoordinate,cursor):new Point2Mm(cursor,fixedCoordinate);
                    var b=height?new Point2Mm(fixedCoordinate,next):new Point2Mm(next,fixedCoordinate);
                    var line=height?new Point2Mm(outer.MinX-510,next):new Point2Mm(next,outer.MinY-390);
                    string text=i==coreIndex?count.ToString(System.Globalization.CultureInfo.InvariantCulture)+"x"+
                        pitch.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"=<>":"<>";
                    Add("NominalSpacing/"+(height?"Y":"X")+"/"+i.ToString("000"),a,b,line,height,
                        owner.OccurrenceId,owner.Mark,text);
                    cursor=next;
                }
                return true;
            }

            void Chain(IEnumerable<Anchor> runs,bool height)
            {
                // These are actual projected centers, including redistributed pitch and
                // surviving split bars. Never label them as nominal n*150 when not true.
                var anchors=runs.GroupBy(a=>Math.Round(height?a.Point.Y:a.Point.X,7))
                    .Select(g=>g.OrderBy(a=>a.OccurrenceId,StringComparer.Ordinal).First())
                    .OrderBy(a=>height?-a.Point.Y:a.Point.X).ToList();
                if(anchors.Count<2) return;
                double first=height?anchors[0].Point.Y:anchors[0].Point.X;
                double last=height?anchors[^1].Point.Y:anchors[^1].Point.X;
                double upper=height && view is not ("BaseTop" or "BaseBottom")?inner.MaxY:outer.MaxY;
                if(height ? first>upper || last<outer.MinY : first<outer.MinX || last>outer.MaxX) return;
                // Source drawing convention retains edge bands and aggregates a uniform
                // central run. Group only physically equal intervals, never nominal pitch.
                int edgeBands=view is "BaseTop" or "BaseBottom"?3:height?1:2;
                int coreStart=edgeBands,coreEnd=anchors.Count-1-edgeBands;
                double step=anchors.Count>1?Math.Abs((height?anchors[1].Point.Y:anchors[1].Point.X)-first):0;
                bool uniform=anchors.Zip(anchors.Skip(1),(a,b)=>Math.Abs((height?b.Point.Y:b.Point.X)-(height?a.Point.Y:a.Point.X)))
                    .All(gap=>Math.Abs(gap-step)<1e-7);
                Point2Mm? coreA=null,coreB=null;string? coreLabel=null;
                if(uniform && coreEnd-coreStart>1)
                {
                    coreA=anchors[coreStart].Point;coreB=anchors[coreEnd].Point;
                    string expression=(coreEnd-coreStart).ToString(System.Globalization.CultureInfo.InvariantCulture)+"x"+
                        step.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
                    double span=Math.Abs((height?coreB.Value.Y:coreB.Value.X)-(height?coreA.Value.Y:coreA.Value.X));
                    // Retain every measured value. Long fractional pitch expressions
                    // use the native below-line text break instead of crossing adjacent
                    // chain labels. Estimate uses the template's 60 mm model text height.
                    double estimatedWidth=(expression.Length+4)*30*.8;
                    coreLabel=expression+(estimatedWidth+120>span?"\\X":"")+"=<>";
                    anchors.RemoveRange(coreStart+1,coreEnd-coreStart-1);
                }
                if(height && view is not ("BaseTop" or "BaseBottom") && vertical.Count>0)
                {
                    var embed=vertical.OrderBy(a=>a.Point.Y).First();
                    if(embed.Point.Y<last-1e-7 && embed.Point.Y>outer.MinY+1e-7)
                        anchors.Add(embed);
                }
                var before=height?new Point2Mm(anchors[0].Point.X,upper):new Point2Mm(outer.MinX,anchors[0].Point.Y);
                var after=height?new Point2Mm(anchors[^1].Point.X,outer.MinY):new Point2Mm(outer.MaxX,anchors[^1].Point.Y);
                if(before!=anchors[0].Point) anchors.Insert(0,new(before,null,null));
                if(after!=anchors[^1].Point) anchors.Add(new(after,null,null));
                for(int i=1;i<anchors.Count;i++)
                {
                    var a=anchors[i-1];var b=anchors[i];
                    var line=height?new Point2Mm(outer.MinX-510,b.Point.Y):new Point2Mm(b.Point.X,outer.MinY-390);
                    Add("Spacing/"+(height?"Y":"X")+"/"+(i-1).ToString("000",System.Globalization.CultureInfo.InvariantCulture),
                        a.Point,b.Point,line,height,a.OccurrenceId??b.OccurrenceId,a.Mark??b.Mark,
                        a.Point==coreA && b.Point==coreB?coreLabel!:"<>");
                }
            }
            void Add(string suffix,Point2Mm a,Point2Mm b,Point2Mm line,bool height,string? occurrence=null,string? mark=null,string label="<>")
            {
                double value=height?Math.Abs(b.Y-a.Y):Math.Abs(b.X-a.X);
                if(value<=1e-7) return;
                result.Add(new(view+"/Dimension/"+suffix,SheetTwoDimensionPlanner.Role,view,outerWidth.ComponentId,
                    occurrence,mark,a,b,value,label,0,height?Math.PI/2:0,line));
            }
        }
        return result;
    }
}
