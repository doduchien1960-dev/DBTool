using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Native template presentation intents; contains no host API or invented title-block fields.</summary>
public static class VhgSheetDecoration
{
    public static IReadOnlyList<BlockPrimitive> CutMarkers(GeometryModel geometry,IEnumerable<DrawingPrimitive> primitives)
    {
        var neck=geometry.Components.FirstOrDefault(c=>c.Id=="Neck");
        var basis=geometry.Components.Single(c=>c.Id=="Base").Footprint.Outer.Vertices;
        var bounds=new Bounds2Mm(basis.Min(p=>p.X),basis.Min(p=>p.Y),basis.Max(p=>p.X),basis.Max(p=>p.Y));
        if(neck is null || neck.Footprint.InnerLoops.Length!=1)return CutMarkers(bounds);
        var hole=neck.Footprint.InnerLoops[0].Vertices;
        double cx=(hole.Min(p=>p.X)+hole.Max(p=>p.X))/2,cy=(hole.Min(p=>p.Y)+hole.Max(p=>p.Y))/2;
        double axisInset=(hole.Max(p=>p.X)-hole.Min(p=>p.X))/4;
        double axisA=cx-Math.Sign(cx)*axisInset,axisB=cy-Math.Sign(cy)*axisInset;
        var plan=primitives.Where(p=>p.ViewId=="Plan").ToArray();
        var pipes=plan.OfType<PolylinePrimitive>().Where(p=>p.SemanticId.Contains("/Stub/",StringComparison.Ordinal))
            .SelectMany(p=>p.Points).ToArray();
        var horizontal=plan.OfType<DimensionIntentPrimitive>().Where(p=>p.RotationRadians==0 && p.DimensionLinePoint.HasValue)
            .Select(p=>p.DimensionLinePoint!.Value).ToArray();
        var vertical=plan.OfType<DimensionIntentPrimitive>().Where(p=>p.RotationRadians==Math.PI/2 && p.DimensionLinePoint.HasValue)
            .Select(p=>p.DimensionLinePoint!.Value).ToArray();
        double top=horizontal.Concat(pipes).Select(p=>p.Y).Append(bounds.MaxY).Max()+180;
        double bottom=horizontal.Concat(pipes).Select(p=>p.Y).Append(bounds.MinY).Min()-120;
        double left=vertical.Concat(pipes).Select(p=>p.X).Append(bounds.MinX).Min()-180;
        double right=vertical.Concat(pipes).Select(p=>p.X).Append(bounds.MaxX).Max()+120;
        return [new("Plan/Cut/A/Top","Section/CutMarker","Plan",null,"Block:MC1",new(axisA,top),.3,[]),
            new("Plan/Cut/A/Bottom","Section/CutMarker","Plan",null,"Block:MC1",new(axisA,bottom),.3,[]),
            new("Plan/Cut/B/Left","Section/CutMarker","Plan",null,"Block:MC2",new(left,axisB),.3,[]),
            new("Plan/Cut/B/Right","Section/CutMarker","Plan",null,"Block:MC2",new(right,axisB),.3,[])];
    }
    public static IReadOnlyList<BlockPrimitive> CutMarkers(Bounds2Mm pad) =>
    [
        new("Plan/Cut/A/Top", "Section/CutMarker", "Plan", null, "Block:MC1", new(-25,pad.MaxY+180),0.3,[]),
        new("Plan/Cut/A/Bottom", "Section/CutMarker", "Plan", null, "Block:MC1", new(-25,pad.MinY-470),0.3,[]),
        new("Plan/Cut/B/Left", "Section/CutMarker", "Plan", null, "Block:MC2", new(pad.MinX-180,-100),0.3,[]),
        new("Plan/Cut/B/Right", "Section/CutMarker", "Plan", null, "Block:MC2", new(pad.MaxX+480,-100),0.3,[])
    ];

    public static BlockPrimitive Frame(string viewId) => new(
        $"{viewId}/Frame", "Frame", viewId, null, "Block:Khung",
        new(0, VhgPresentationStandards.ModelHeightMm), VhgPresentationStandards.FrameInsertionScale, []);

    public static BlockPrimitive Title(string viewId, string sourceViewId, string title, Point2Mm insertion,double viewScaleDenominator=30) => new(
        $"{viewId}/Title/{sourceViewId}", "ViewTitle", viewId, null, "Block:TenMC", insertion,
        VhgPresentationStandards.FrameInsertionScale,
        [new("TIEUDE", title), new("TYLE", "Tỷ lệ 1:"+ContractGuard.Positive(viewScaleDenominator,nameof(viewScaleDenominator)).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture))]);
}
