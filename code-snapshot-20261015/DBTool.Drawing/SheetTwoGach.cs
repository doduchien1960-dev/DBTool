using System.Globalization;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Template steel ticks derived from nominal detailing stations.</summary>
public static class SheetTwoGach
{
    public const string Role="Rebar/SheetTwoGach";
    public const string AnchorageRole="Rebar/SheetTwoGachAnchorage";
    public static IReadOnlyList<BlockPrimitive> Build(IEnumerable<DimensionIntentPrimitive> dimensions,
        IReadOnlyDictionary<string,int> diameters,GeometryModel? geometry=null)
    {
        var result=new List<BlockPrimitive>();
        foreach(var view in dimensions.GroupBy(d=>d.ViewId))
        {
            bool slab=view.Key is "BaseTop" or "BaseBottom";
            var x=Chain("X");var y=Chain("Y");
            int coreIndex=slab?4:3;
            if(x.Length!=(slab?9:7) || y.Length!=(slab?9:6) || x[coreIndex].OccurrenceId is not {} owner ||
                !diameters.TryGetValue(owner,out int diameter)) continue;
            if(slab)
            {
                double tickX=x[4].Start.X+diameter;
                double cover=y[0].LogicalMeasurementMm;
                double tickY=y[2].End.Y-2*cover;
                foreach(var station in Stations(x,false)) Add(new(station,tickY),"X");
                foreach(var station in Stations(y,true)) Add(new(tickX,station),"Y");
            }
            else
            {
                var wall=geometry?.Components.SingleOrDefault(c=>c.Id==x[0].ComponentId);
                var basis=geometry?.Components.SingleOrDefault(c=>c.Id=="Base");
                if(wall is null || basis is null) continue;
                bool alongY=view.Key.Contains("/Wall/T2/",StringComparison.Ordinal) || view.Key.Contains("/Wall/T4/",StringComparison.Ordinal);
                bool inner=view.Key.Contains("/Inner/",StringComparison.Ordinal);
                double physicalWidth=wall.Footprint.Outer.Vertices.Max(p=>alongY?p.Y:p.X)-
                    wall.Footprint.Outer.Vertices.Min(p=>alongY?p.Y:p.X);
                double outerWidth=x[^1].End.X-x[0].Start.X;
                bool cornerOverlap=physicalWidth<outerWidth-1e-7;
                var stations=Stations(x,false).ToArray();
                // Cover-line corner stations belong to the overlapping perpendicular
                // wall. Inner faces show the straight central run, with its anchorage
                // leg detailed separately below the wall.
                int trim=(inner?1:0)+(cornerOverlap?1:0);
                stations=stations.Skip(trim).Take(stations.Length-2*trim).ToArray();
                double pitch=y[2].LogicalMeasurementMm/CoreCount(y[2]);
                double tickY=y[0].End.Y-pitch;
                double tickX=x[0].Start.X+(inner?340:315)+30;
                foreach(var station in stations) Add(new(station,tickY),"X");
                foreach(var station in Stations(y,true).Where(s=>s>=basis.ZRange.End)) Add(new(tickX,station),"Y");
                if(inner)
                {
                    double cover=x[0].LogicalMeasurementMm;
                    double middle=(basis.ZRange.Start+basis.ZRange.End)/2;
                    int count=CoreCount(x[3]);
                    for(int i=0;i<=count;i++)
                        Add(new(x[3].Start.X+(x[3].End.X-x[3].Start.X)*i/count+cover,middle),"Anchorage");
                }
            }

            DimensionIntentPrimitive[] Chain(string axis)=>view.Where(d=>d.SemanticId.Contains("/NominalSpacing/"+axis+"/",StringComparison.Ordinal))
                .OrderBy(d=>d.SemanticId,StringComparer.Ordinal).ToArray();
            void Add(Point2Mm point,string axis)
            {
                var core=axis=="Y"?y[slab?4:2]:x[coreIndex];
                if(core.OccurrenceId is not {} association || !diameters.ContainsKey(association)) return;
                result.Add(new(view.Key+"/Gach/"+axis+"/"+result.Count.ToString("000",CultureInfo.InvariantCulture),
                    axis=="Anchorage"?AnchorageRole:Role,view.Key,x[0].ComponentId,"Block:Gach",point,.3,[],
                    occurrenceId:association,scheduleMark:core.ScheduleMark));
            }
        }
        return result;
    }
    private static int CoreCount(DimensionIntentPrimitive dimension)
    {
        int split=dimension.Label.IndexOf('x');
        return split>0 && int.TryParse(dimension.Label.AsSpan(0,split),out int count) && count>0?count:1;
    }
    private static IEnumerable<double> Stations(DimensionIntentPrimitive[] chain,bool height)
    {
        var positions=new SortedSet<double>();
        foreach(var dimension in chain)
        {
            double start=height?dimension.Start.Y:dimension.Start.X;
            double end=height?dimension.End.Y:dimension.End.X;
            int count=CoreCount(dimension);
            for(int i=0;i<=count;i++) positions.Add(Math.Round(start+(end-start)*i/count,7));
        }
        positions.Remove(Math.Round(height?chain[0].Start.Y:chain[0].Start.X,7));
        positions.Remove(Math.Round(height?chain[^1].End.Y:chain[^1].End.X,7));
        return positions;
    }
}
