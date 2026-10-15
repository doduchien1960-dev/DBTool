using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public static class RoofJointDowelProjection
{
    public static OperationResult<IReadOnlyList<DrawingPrimitive>> Build(IEnumerable<BarOccurrence> bars,
        IReadOnlyDictionary<string,string> marks,double? holeDiameterMm,double scaleDenominator = 30)
    {
        var dowels=bars.Where(b=>b.FamilyId=="Dowel").OrderBy(b=>b.Id,StringComparer.Ordinal).ToArray();
        if(dowels.Length==0) return new(true,[],[]);
        var result=new List<DrawingPrimitive>();
        foreach(var bar in dowels)
        {
            if(holeDiameterMm is not {} diameter || !double.IsFinite(diameter) || diameter<=bar.DiameterMm ||
                !marks.TryGetValue(bar.Id,out var mark) || bar.Path.Segments.Length!=1 || bar.Path.Segments[0] is not LineSegment3Mm line ||
                Math.Abs(line.Start.X-line.End.X)>1e-6 || Math.Abs(line.Start.Y-line.End.Y)>1e-6)
                return new(false,null,[new Diagnostic("ROOF_DOWEL_PROJECTION_NEEDS_RULE",DiagnosticSeverity.Block,
                    DiagnosticScope.Component,null,"Roof",bar.Sources.FirstOrDefault(),"straight joint dowel and configured larger hole",bar.Id,
                    "Cannot invent hole diameter, station, or a bent dowel silhouette.")]);
            var point=new Point2Mm(line.Start.X,line.Start.Y);
            string id="Joint/WallRoof/"+bar.Id;
            result.Add(new CirclePrimitive(id+"/Hole","Geometry/DowelHole",SheetThreeRoofViews.TopViewId,"Roof",bar.Id,mark,point,diameter/2));
        }
        double textHeight = 2 * scaleDenominator;
        foreach(var group in dowels.GroupBy(b=>b.ComponentId))
        {
            var bar=group.First();var point=bar.Path.Segments[0].Start;
            var tip=new Point2Mm(point.X,point.Y);
            var end=new Point2Mm(point.X+180,point.Y+180);
            string id="Joint/WallRoof/"+bar.Id+"/Callout";
            result.Add(new PolylinePrimitive(id+"/Leader","Rebar/Leader",SheetThreeRoofViews.TopViewId,"Roof",bar.Id,marks[bar.Id],[tip,end],false));
            result.Add(new TextPrimitive(id,"Note/RoofDowel",SheetThreeRoofViews.TopViewId,"Roof",bar.Id,marks[bar.Id],
                end,FormattableString.Invariant($"Chốt D{bar.DiameterMm} - lỗ D{holeDiameterMm:0.###}"),textHeight,0));
        }
        return new(true,result,[]);
    }
}
