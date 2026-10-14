using System.Collections.Immutable;
using DBTool.Domain;
namespace DBTool.Drawing;

public sealed record BendingShapeIntent
{
    public BendingShapeIntent(string mark,string componentId,ComponentKind componentKind,int quantity,double scheduleLengthMm,
        bool closed,ImmutableArray<DrawingPathSegment> segments,ImmutableArray<string> sourceOccurrenceIds,
        string? recipeId = null, string? glyphAsset = null)
    {
        Mark=mark;ComponentId=componentId;ComponentKind=componentKind;Quantity=quantity;ScheduleLengthMm=scheduleLengthMm;
        Closed=closed;Segments=segments;SourceOccurrenceIds=sourceOccurrenceIds;
        RecipeId=recipeId;GlyphAsset=glyphAsset;
    }
    public string Mark { get; }
    public string ComponentId { get; }
    public ComponentKind ComponentKind { get; }
    public int Quantity { get; }
    public double ScheduleLengthMm { get; }
    public bool Closed { get; }
    public ImmutableArray<DrawingPathSegment> Segments { get; }
    public ImmutableArray<string> SourceOccurrenceIds { get; }
    public string? RecipeId { get; }
    public string? GlyphAsset { get; }
}
public static class BendingSchematicPlanner
{
    public static OperationResult<IReadOnlyList<BendingShapeIntent>> Build(IEnumerable<BarOccurrence> bars,
        IReadOnlyDictionary<string,string> marks,
        IReadOnlyDictionary<string, string>? recipeOverrides = null)
    {
        var shapes=new List<BendingShapeIntent>();
        var snapshot=bars.ToArray();
        foreach(var bar in snapshot)
            if(!marks.ContainsKey(bar.Id)) return Block("BENDING_MARK_UNRESOLVED",bar.Id);
        foreach(var group in snapshot.GroupBy(b=>marks[b.Id],StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var first=group.OrderBy(b=>b.Id,StringComparer.Ordinal).First();
            var projection=Project(first);
            if(!projection.Success) return new(false,null,projection.Diagnostics);
            foreach(var bar in group)
            {
                var other=Project(bar);
                if(!other.Success) return new(false,null,other.Diagnostics);
                if(bar.DiameterMm!=first.DiameterMm || bar.ScheduleLengthMm!=first.ScheduleLengthMm ||
                    bar.Path.Closed!=first.Path.Closed || !Same(projection.Value!,other.Value!))
                    return Block("BENDING_MARK_SHAPE_CONFLICT",group.Key);
            }
            string? recipeId = (recipeOverrides != null && recipeOverrides.TryGetValue(group.Key, out var rId))
                ? rId
                : first.ShapeRecipeId;
            string? glyphAsset = ResolveDefaultGlyph(first, recipeId);
            shapes.Add(new(group.Key,first.ComponentId,first.ComponentKind,group.Count(),first.ScheduleLengthMm,
                first.Path.Closed,projection.Value!.ToImmutableArray(),group.Select(b=>b.Id).Order(StringComparer.Ordinal).ToImmutableArray(),
                recipeId, glyphAsset));
        }
        return new(true,shapes.AsReadOnly(),[]);
    }

    private static string? ResolveDefaultGlyph(BarOccurrence bar, string? recipeId)
    {
        if (bar.FamilyId == "Ladder") return "TK.M.THANG";
        if (!string.IsNullOrEmpty(recipeId))
        {
            return recipeId switch
            {
                "Shape.C" => "TK.C",
                "Shape.L" => "TK.L",
                "Shape.Straight" => "TK.T",
                "Shape.U" => "TK.U",
                _ => null
            };
        }
        return null;
    }
    private static OperationResult<IReadOnlyList<DrawingPathSegment>> Project(BarOccurrence bar)=>
        bar.FamilyId=="Ladder" ? Bending2DPlanner.UnfoldLadder(bar.Path) : Bending2DPlanner.Project(bar.Path);
    private static bool Same(IReadOnlyList<DrawingPathSegment> a,IReadOnlyList<DrawingPathSegment> b)
    {
        bool Near(double x,double y)=>Math.Abs(x-y)<=1e-6;
        bool Point(DBTool.Domain.Geometry.Point2Mm x,DBTool.Domain.Geometry.Point2Mm y)=>Near(x.X,y.X)&&Near(x.Y,y.Y);
        return a.Count==b.Count && a.Zip(b).All(pair=>pair.First.GetType()==pair.Second.GetType() &&
            Point(pair.First.Start,pair.Second.Start)&&Point(pair.First.End,pair.Second.End)&&
            (pair.First is not ArcPathSegment arc || pair.Second is ArcPathSegment other &&
                Point(arc.Center,other.Center)&&Near(arc.RadiusMm,other.RadiusMm)&&Near(arc.SweepRadians,other.SweepRadians)));
    }
    private static OperationResult<IReadOnlyList<BendingShapeIntent>> Block(string code,string actual)=>new(false,null,
        [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,"one consistent shape per canonical mark",actual,
            "Bending schematic cannot be resolved.")]);
}
