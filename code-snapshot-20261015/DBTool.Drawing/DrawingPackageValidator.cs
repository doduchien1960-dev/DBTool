using DBTool.Domain;

namespace DBTool.Drawing;

public sealed record DrawingValidationReport
{
    public DrawingValidationReport(int viewCount, int primitiveCount)
    {
        ViewCount = viewCount;
        PrimitiveCount = primitiveCount;
    }

    public int ViewCount { get; }
    public int PrimitiveCount { get; }
}

public static class DrawingPackageValidator
{
    private static readonly string[] KnownRequiredRoles = ["Required/Geometry", "Required/Annotation", "Required/Accessory"];

    public static OperationResult<DrawingValidationReport> ValidateCollectorAssociations(IEnumerable<DrawingPrimitive> primitives)
    {
        var snapshot=primitives.ToArray();
        bool On(DBTool.Domain.Geometry.Point2Mm p,PolylinePrimitive line)
        {
            for(int i=1;i<line.Points.Length;i++)
            {
                var a=line.Points[i-1];var b=line.Points[i];double dx=b.X-a.X,dy=b.Y-a.Y;
                double length2=dx*dx+dy*dy;
                if(length2<1e-12) continue;
                double t=((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length2;
                if(t< -1e-9 || t>1+1e-9) continue;
                if(Math.Abs(p.X-a.X-t*dx)<1e-6 && Math.Abs(p.Y-a.Y-t*dy)<1e-6) return true;
            }
            return false;
        }
        foreach(var tick in snapshot.OfType<BlockPrimitive>().Where(p=>p.Role==SheetTwoGach.Role))
        {
            var collector=snapshot.OfType<PolylinePrimitive>().SingleOrDefault(p=>p.Role==SheetTwoGachCollector.CollectorRole &&
                tick.SemanticId.StartsWith(p.SemanticId+"/Gach/",StringComparison.Ordinal));
            if(collector is null || collector.ViewId!=tick.ViewId || collector.ComponentId!=tick.ComponentId ||
                !On(tick.Insertion,collector))
                return Block("DRAWING_GACH_COLLECTOR_INVALID",$"Tick {tick.SemanticId} has no matching collector intersection.");
            if(!snapshot.OfType<PolylinePrimitive>().Any(p=>p.ViewId==tick.ViewId && p.ComponentId==tick.ComponentId &&
                p.OccurrenceId==tick.OccurrenceId && p.ScheduleMark==tick.ScheduleMark &&
                p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom" && On(tick.Insertion,p)))
                return Block("DRAWING_GACH_BAR_INTERSECTION_INVALID",$"Tick {tick.SemanticId} does not intersect its own visible physical bar.");
        }
        foreach(var collector in snapshot.OfType<PolylinePrimitive>().Where(p=>p.Role==SheetTwoGachCollector.CollectorRole))
        {
            var label=snapshot.OfType<BlockPrimitive>().SingleOrDefault(p=>p.SemanticId==collector.SemanticId+"/Callout" && p.Role==SheetTwoBarAnnotations.Role);
            var leader=snapshot.OfType<PolylinePrimitive>().SingleOrDefault(p=>p.SemanticId==collector.SemanticId+"/Callout/Leader" && p.Role=="Rebar/Leader");
            if(label is null || leader is null || label.ViewId!=collector.ViewId || label.ComponentId!=collector.ComponentId ||
                leader.OccurrenceId!=label.OccurrenceId ||
                leader.Points[^1]!=label.Insertion || !On(leader.Points[0],collector) ||
                !snapshot.OfType<BlockPrimitive>().Any(p=>p.SemanticId.StartsWith(collector.SemanticId+"/Gach/",StringComparison.Ordinal) &&
                    p.OccurrenceId==label.OccurrenceId && (p.Insertion==leader.Points[0] || Math.Abs(p.Insertion.X-leader.Points[0].X)<1e-6 && Math.Abs(p.Insertion.Y-leader.Points[0].Y)<1e-6)))
                return Block("DRAWING_COLLECTOR_LABEL_INVALID",$"Collector {collector.SemanticId} has no associated label/leader through its own bar tick.");
        }
        return new(true,new DrawingValidationReport(0,snapshot.Length),[]);
    }

    public static OperationResult<DrawingValidationReport> ValidateModelCoverage(IEnumerable<BarOccurrence> bars, DrawingPackage package)
    {
        var collectors=ValidateCollectorAssociations(package.Primitives);
        if(!collectors.Success) return collectors;
        var occurrences=bars.ToDictionary(b=>b.Id,StringComparer.Ordinal);
        if(occurrences.Values.Any(b=>b.LengthStatus!=LengthStatus.Known))
            return Block("DRAWING_FABRICATION_NEEDS_RULE","A requested physical bar still has an unresolved length/shape rule.");
        foreach(var symbol in package.Primitives.OfType<BlockPrimitive>().Where(b=>b.BlockRole=="Block:BLTC"))
            if(symbol.OccurrenceId is not {} id || !occurrences.TryGetValue(id,out var tie) || tie.FamilyId!="Tie" || tie.ComponentId!=symbol.ComponentId)
                return Block("DRAWING_TIE_ASSOCIATION_INVALID","BLTC must refer to its own physical tie occurrence and owner.");
        foreach(var label in package.Primitives.OfType<BlockPrimitive>().Where(b=>b.Role==SheetTwoBarAnnotations.Role))
        {
            if(label.OccurrenceId is not {} id || !occurrences.TryGetValue(id,out var bar) || bar.ComponentId!=label.ComponentId)
                return Block("DRAWING_LABEL_ASSOCIATION_INVALID","Bar label must refer to its actual occurrence and owner.");
            var diameter=label.Attributes.FirstOrDefault(a=>a.Key=="DK")?.Value;
            if(diameter!="%%c"+bar.DiameterMm.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return Block("DRAWING_LABEL_DIAMETER_MISMATCH",$"Label {label.SemanticId} does not match physical diameter D{bar.DiameterMm}.");
        }
        foreach(var bar in occurrences.Values.Where(b=>b.FamilyId=="Dowel"))
        {
            var holes=package.Primitives.OfType<CirclePrimitive>().Where(p=>p.Role=="Geometry/DowelHole" && p.OccurrenceId==bar.Id).ToArray();
            if(holes.Length!=1)
                return Block("DRAWING_DOWEL_HOLE_MISSING",$"Dowel {bar.Id} requires exactly one associated roof-plan hole.");
            var start=bar.Path.Segments[0].Start;
            if(holes[0].RadiusMm*2<=bar.DiameterMm || Math.Abs(holes[0].Center.X-start.X)>1e-6 || Math.Abs(holes[0].Center.Y-start.Y)>1e-6)
                return Block("DRAWING_DOWEL_HOLE_MISMATCH",$"Hole position/diameter does not match dowel {bar.Id}.");
        }
        return new(true,new DrawingValidationReport(package.Views.Length,package.Primitives.Length),[]);
    }

    public static OperationResult<DrawingValidationReport> ValidatePrimitives(
        IEnumerable<ViewDefinition> views, IEnumerable<DrawingPrimitive> primitives)
    {
        var viewSnapshot = views.ToArray();
        var primitiveSnapshot = primitives.ToArray();
        if (viewSnapshot.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != viewSnapshot.Length)
            return Block("DRAWING_VIEW_DUPLICATE", "View identity is duplicated.");
        var duplicate = primitiveSnapshot.GroupBy(p => p.SemanticId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Block("DRAWING_SEMANTIC_ID_DUPLICATE", $"SemanticId is duplicated: {duplicate.Key}");
        if (primitiveSnapshot.Any(p => !viewSnapshot.Any(v => v.Id == p.ViewId)))
            return Block("DRAWING_VIEW_UNRESOLVED", "Primitive references an unknown view.");
        if (primitiveSnapshot.Any(p => viewSnapshot.Single(v => v.Id == p.ViewId).Purpose == ViewPurpose.BaseMesh &&
            p.ComponentId is not null && p.ComponentId != "Base"))
            return Block("DRAWING_BASE_COMPONENT_MISMATCH", "Base views may contain only Base components.");
        var unknownRole = primitiveSnapshot.FirstOrDefault(p => p.Role.StartsWith("Required/", StringComparison.Ordinal) &&
            !KnownRequiredRoles.Contains(p.Role, StringComparer.Ordinal));
        if (unknownRole is not null)
            return Block("DRAWING_REQUIRED_ROLE_UNKNOWN", $"Required role is not in the drawing contract: {unknownRole.Role}");
        return new(true, new DrawingValidationReport(viewSnapshot.Length, primitiveSnapshot.Length), []);
    }

    public static OperationResult<DrawingValidationReport> ValidatePackageCompleteness(DrawingPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var primResult = ValidatePrimitives(package.Views, package.Primitives);
        if (!primResult.Success) return primResult;

        if (!package.Primitives.IsEmpty)
        {
            foreach (var view in package.Views)
            {
                if (!package.Primitives.Any(p => p.ViewId == view.Id))
                    return Block("DRAWING_VIEW_PRIMITIVES_MISSING", $"View {view.Id} has no drawing primitives.");
            }
        }

        var colResult = ValidateCollectorAssociations(package.Primitives);
        if (!colResult.Success) return colResult;

        if (!package.Groups.IsEmpty)
        {
            var presentGroups = package.Primitives.Select(p => p.OccurrenceId).Where(id => id != null).ToHashSet(StringComparer.Ordinal);
            foreach (var group in package.Groups)
            {
                if (!presentGroups.Contains(group))
                    return Block("DRAWING_GROUP_COVERAGE_MISSING", $"Declared group {group} is not covered by any primitive.");
            }
        }

        return new(true, new DrawingValidationReport(package.Views.Length, package.Primitives.Length), []);
    }

    private static OperationResult<DrawingValidationReport> Block(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
