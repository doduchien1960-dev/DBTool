using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Measured representative bar/callout routes in the published G400-1 mesh template.
/// These nominal drafting paths retain canonical bar provenance and never edit fabrication paths.</summary>
public static class SheetTwoRepresentativePaths
{
    public const string Role = "Rebar/SheetTwoRepresentative";

    public static IReadOnlyList<DrawingPrimitive> Apply(IEnumerable<DrawingPrimitive> primitives)
    {
        var result = primitives.ToList();
        foreach (var label in result.OfType<BlockPrimitive>().Where(b => b.Role == SheetTwoBarAnnotations.Role).ToArray())
        {
            string? mark = label.Attributes.SingleOrDefault(a => a.Key == "SH")?.Value;
            var points = Route(label.ViewId, mark);
            if (points is null || points[^1] != label.Insertion || label.OccurrenceId is null || label.ScheduleMark is null)
                continue;
            var x = Chain("X"); var y = Chain("Y");
            bool slab = label.ViewId is "BaseTop" or "BaseBottom";
            bool t2 = label.ViewId.Contains("/Wall/T2/", StringComparison.Ordinal) || label.ViewId.Contains("/Wall/T4/", StringComparison.Ordinal);
            double[] expectedX = slab ? [30,100,90,80,900,80,90,100,30] :
                t2 ? [30,90,130,1050,130,90,30] : [30,90,80,900,80,90,30];
            double[] expectedY = slab ? [30,100,90,130,1050,130,90,100,30] : [45,92.5,1200,92.5,90,30];
            // The routes are confirmed only for this template's nominal band geometry.
            if (x.Length != (slab ? 9 : 7) || y.Length != (slab ? 9 : 6) ||
                x.Where((d,i) => Math.Abs(d.LogicalMeasurementMm - expectedX[i]) > 1e-7).Any() ||
                y.Where((d,i) => Math.Abs(d.LogicalMeasurementMm - expectedY[i]) > 1e-7).Any() ||
                Math.Abs(x[0].Start.X - (slab ? -750 : t2 ? -775 : -650)) > 1e-7 ||
                Math.Abs(x[^1].End.X - (slab ? 750 : t2 ? 775 : 650)) > 1e-7 ||
                Math.Abs(y[0].Start.Y - (slab ? 875 : 1400)) > 1e-7 ||
                Math.Abs(y[^1].End.Y - (slab ? -875 : -150)) > 1e-7 ||
                !result.Any(p => p.ViewId == label.ViewId && p.Role == (slab ? SheetTwoNominalBaseMesh.Role : SheetTwoNominalWallMesh.Role)))
                continue;
            var leader = result.OfType<PolylinePrimitive>().SingleOrDefault(p => p.SemanticId == label.SemanticId + "/Leader" && p.Role == "Rebar/Leader");
            if (leader is null) continue;
            result[result.IndexOf(leader)] = new PolylinePrimitive(leader.SemanticId, Role, label.ViewId,
                label.ComponentId, label.OccurrenceId, label.ScheduleMark, points, false);

            DimensionIntentPrimitive[] Chain(string axis) => result.OfType<DimensionIntentPrimitive>()
                .Where(d => d.ViewId == label.ViewId && d.SemanticId.Contains("/NominalSpacing/" + axis + "/", StringComparison.Ordinal))
                .OrderBy(d => d.SemanticId, StringComparer.Ordinal).ToArray();
        }
        return result;
    }

    private static Point2Mm[]? Route(string view, string? mark)
    {
        if (view is "BaseTop" or "BaseBottom") return mark switch
        {
            "D1" => [new(-440,845),new(-440,-845),new(-440,-1025),new(-410,-1025)],
            "D2" => [new(720,595),new(-720,595),new(-750,595),new(-780,595)],
            "D3" when view == "BaseTop" => [new(300,-525),new(375,-600),new(375,-1025),new(405,-1025)],
            _ => null
        };
        bool t2 = view.Contains("/Wall/T2/", StringComparison.Ordinal) || view.Contains("/Wall/T4/", StringComparison.Ordinal);
        bool inner = view.Contains("/Inner/", StringComparison.Ordinal);
        if (SheetMeshPlacement.Title(view) is null) return null;
        if (mark == "T1")
        {
            double x = t2 ? inner ? -405 : -430 : inner ? -280 : -305;
            return [new(x,1355),new(x,62.5),new(x,-300),new(x-30,-300)];
        }
        if (mark == "T2")
        {
            double edge = t2 ? inner ? 525 : 655 : inner ? 530 : 620;
            double bend = t2 ? -775 : -650;
            return [new(edge,1205),new(-edge,1205),new(bend,1205),new(bend-30,1205)];
        }
        if (mark != "T3") return null;
        if (inner)
        {
            double right = t2 ? 555 : 480, left = t2 ? -495 : -420, bend = t2 ? -775 : -650;
            return [new(right,-75),new(left,-75),new(bend,-75),new(bend-30,-75)];
        }
        double start = t2 ? 525 : 300;
        return [new(start,62.5),new(start+75,-12.5),new(start+75,-300),new(start+45,-300)];
    }
}
