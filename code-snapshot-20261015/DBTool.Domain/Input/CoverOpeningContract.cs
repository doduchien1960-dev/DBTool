using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Catalog assembly dimensions in native cover coordinates, independently sourced from its outer boundary.</summary>
public sealed record CoverOpeningContract
{
    public CoverOpeningContract(Rectangle2Mm clearOpening, double minimumBearingMm, IEnumerable<SourceRef> sources)
        : this(new RectangleProfile(clearOpening), minimumBearingMm, sources) { }

    public CoverOpeningContract(Profile2Mm clearProfile, double minimumBearingMm, IEnumerable<SourceRef> sources)
    {
        ClearProfile = ContractGuard.Required(clearProfile);
        ClearOpening = clearProfile switch
        {
            RectangleProfile rectangle => rectangle.Rectangle,
            RoundedRectangleProfile rounded => rounded.AxisBounds,
            CircleProfile circle => new(circle.Center.X-circle.RadiusMm, circle.Center.Y-circle.RadiusMm,
                circle.Center.X+circle.RadiusMm, circle.Center.Y+circle.RadiusMm),
            _ => throw new ArgumentException("Cover aperture requires a supported explicit Circle, Rectangle or RoundedRectangle profile.", nameof(clearProfile))
        };
        MinimumBearingMm = ContractGuard.Positive(minimumBearingMm, nameof(minimumBearingMm));
        Sources = ContractGuard.Copy(sources);
        if (Sources.IsEmpty) throw new ArgumentException("Cover assembly requires provenance.");
    }
    public Rectangle2Mm ClearOpening { get; }
    /// <summary>Physical aperture. ClearOpening is its directional extent, never a substitute contour.</summary>
    public Profile2Mm ClearProfile { get; }
    public double MinimumBearingMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public static class CoverAssemblyValidator
{
    public static OperationResult<CoverOpeningContract> Validate(CoverCatalogEntry entry,
        Rectangle2Mm neckOpening, double? neckThicknessMm, double toleranceMm,
        SourceRef coverSource, SourceRef neckSource,
        CoverSupportSurface? supportSurface=null,double? coverTopElevationMm=null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(neckOpening);
        var rule = entry.OpeningContract;
        OperationResult<CoverOpeningContract> Block(string code, string message, string? actual = null) =>
            new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, coverSource, $"Compatible opening at {neckSource.SheetName}!{neckSource.CellAddress}", actual, message)]);
        if (!double.IsFinite(toleranceMm) || toleranceMm < 0)
            return Block("COVER_ASSEMBLY_TOLERANCE_INVALID", "Finite nonnegative geometry tolerance required.");
        if (rule is null || entry.LocalBounds is null || entry.InsertionOffset is null || entry.RotationDegrees is null)
            return Block("COVER_ASSEMBLY_METADATA_REQUIRED", "Catalog needs independently sourced clear opening and minimum bearing; outer dimensions and product names are insufficient.");
        double rotation = entry.RotationDegrees.Value % 360;
        double quarterTurns = rotation / 90;
        if (Math.Abs(quarterTurns - Math.Round(quarterTurns)) > 1e-9)
            return Block("COVER_ASSEMBLY_ROTATION_UNSUPPORTED", "Rectangular neck contract supports quarter-turn cover placement only; no bounding-box approximation.");
        if(entry.BlockSet is { } nativeBlocks)
        {
            var placement=CoverBlockPlacementResolver.Resolve(nativeBlocks,new(0,0),0,rotation);
            if(!placement.Success) return new(false,null,placement.Diagnostics);
            var insertion=placement.Value!.PlanInsertion;
            if(Math.Abs(insertion.X-entry.InsertionOffset.Value.X)>toleranceMm ||
                Math.Abs(insertion.Y-entry.InsertionOffset.Value.Y)>toleranceMm)
                return Block("COVER_VIEW_ANCHOR_INCOMPATIBLE","Declared insertion offset contradicts measured aperture alignment; no silent correction.");
        }
        var clear = rule.ClearOpening;
        double bearing;
        if(entry.BlockSet is { } measured)
        {
            if(measured.ContactSections.Length!=2)
                return Block("COVER_CONTACT_GEOMETRY_REQUIRED","Native assembly requires independently sourced frame underside edges in both AA and BB; inspection bounds are not contact geometry.");
            if(supportSurface is null && (!neckThicknessMm.HasValue || !double.IsFinite(neckThicknessMm.Value) || neckThicknessMm.Value<=0))
                return Block("COVER_CONTACT_WALL_REQUIRED","Native contact requires a finite positive neck wall thickness.");
            var levels=measured.ContactSections.SelectMany(section=>section.Segments.SelectMany(edge=>
                new[]{edge.Start.Y,edge.End.Y}.Select(y=>y-measured.Views.Single(v=>v.View==section.View).AlignmentPoint!.Value.Y))).ToArray();
            if(levels.Max()-levels.Min()>Math.Max(toleranceMm,1e-6))
                return Block("COVER_CONTACT_PLANE_INCOMPATIBLE","Frame feet do not share one support plane relative to their independent native top references; no merging across elevations.");
            if(supportSurface is not null && (!coverTopElevationMm.HasValue || !double.IsFinite(coverTopElevationMm.Value)))
                return Block("COVER_CONTACT_ELEVATION_REQUIRED","Physical support requires the resolved cover top elevation.");
            if(supportSurface is not null && levels.Any(z=>Math.Abs(coverTopElevationMm!.Value+z-supportSurface.ElevationMm)>Math.Max(toleranceMm,1e-6)))
                return Block("COVER_CONTACT_ELEVATION_INCOMPATIBLE","Measured frame feet do not meet the concrete support plane at the requested cover elevation; no scaling or height correction.");
            var supported=new List<double>();
            foreach(var section in measured.ContactSections)
            {
                // Edges use this section's own native origin, not the MB origin.
                double centre=measured.Views.Single(v=>v.View==section.View).AlignmentPoint!.Value.X;
                double halfSpan=section.View=="AA"?(clear.MaxY-clear.MinY)/2:(clear.MaxX-clear.MinX)/2;
                if(supportSurface is null)
                {
                    double left=centre-halfSpan,right=centre+halfSpan,thickness=neckThicknessMm!.Value;
                    supported.Add(ContinuousSupportedWidth(section,left-thickness,left));
                    supported.Add(ContinuousSupportedWidth(section,right,right+thickness));
                }
                else
                {
                    // A native AA horizontal direction is plan +Y; BB is plan +X.
                    // Quarter turns map it to one signed world axis, without bounding-box projection.
                    double contactAngle=rotation*Math.PI/180;
                    int axisX=(int)Math.Round(section.View=="AA"?-Math.Sin(contactAngle):Math.Cos(contactAngle));
                    int axisY=(int)Math.Round(section.View=="AA"?Math.Cos(contactAngle):Math.Sin(contactAngle));
                    double supportCentreX=(neckOpening.MinX+neckOpening.MaxX)/2,supportCentreY=(neckOpening.MinY+neckOpening.MaxY)/2;
                    (double Min,double Max) Project(Rectangle2Mm rectangle)
                    {
                        double a=axisX!=0?(rectangle.MinX-supportCentreX)*axisX:(rectangle.MinY-supportCentreY)*axisY;
                        double b=axisX!=0?(rectangle.MaxX-supportCentreX)*axisX:(rectangle.MaxY-supportCentreY)*axisY;
                        return (centre+Math.Min(a,b),centre+Math.Max(a,b));
                    }
                    var outer=Project(supportSurface.Outer);var hole=Project(supportSurface.Opening);
                    supported.Add(ContinuousSupportedWidth(section,outer.Min,hole.Min));
                    supported.Add(ContinuousSupportedWidth(section,hole.Max,outer.Max));
                }
            }
            bearing=supported.Min();
        }
        else
        {
            // Legacy explicit rectangular catalog contract; native assets never enter this branch.
            var outer=entry.LocalBounds;
            bearing=new[] {clear.MinX-outer.MinX,outer.MaxX-clear.MaxX,
                clear.MinY-outer.MinY,outer.MaxY-clear.MaxY}.Min();
        }
        if (bearing + toleranceMm < rule.MinimumBearingMm || supportSurface is null && neckThicknessMm.HasValue && neckThicknessMm.Value + toleranceMm < rule.MinimumBearingMm)
            return Block("COVER_BEARING_INCOMPATIBLE", "Declared bearing exceeds the continuous supported contact width or the neck wall.", bearing.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        double radians = rotation * Math.PI / 180;
        double cos = Math.Cos(radians), sin = Math.Sin(radians);
        var offset = entry.InsertionOffset.Value;
        var corners = new[] { (clear.MinX, clear.MinY), (clear.MinX, clear.MaxY),
            (clear.MaxX, clear.MinY), (clear.MaxX, clear.MaxY) }
            .Select(p => (X: cos * p.Item1 - sin * p.Item2 + offset.X,
                Y: sin * p.Item1 + cos * p.Item2 + offset.Y)).ToArray();
        double cx = (neckOpening.MinX + neckOpening.MaxX) / 2, cy = (neckOpening.MinY + neckOpening.MaxY) / 2;
        double[] expected = [neckOpening.MinX - cx, neckOpening.MinY - cy, neckOpening.MaxX - cx, neckOpening.MaxY - cy];
        double[] actual = [corners.Min(p => p.X), corners.Min(p => p.Y), corners.Max(p => p.X), corners.Max(p => p.Y)];
        if (expected.Zip(actual).Any(p => Math.Abs(p.First - p.Second) > toleranceMm))
            return Block("COVER_OPENING_INCOMPATIBLE",
                $"Cover {entry.Code} clear opening after placement conflicts with {neckSource.SheetName}!{neckSource.CellAddress}; no implicit transition or workbook rewrite.",
                string.Join(",", actual.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        return new(true, rule, []);
    }

    private static double ContinuousSupportedWidth(CoverContactSection section,double wallMin,double wallMax)
    {
        var intervals=section.Segments.Select(e=>(Start:Math.Max(e.Start.X,wallMin),End:Math.Min(e.End.X,wallMax)))
            .Where(e=>e.End>e.Start).OrderBy(e=>e.Start).ToArray();
        double width=0,start=0,end=0;
        bool active=false;
        foreach(var interval in intervals)
        {
            if(!active || interval.Start>end+1e-9)
            {
                if(active) width=Math.Max(width,end-start);
                start=interval.Start;end=interval.End;active=true;
            }
            else end=Math.Max(end,interval.End);
        }
        return active?Math.Max(width,end-start):0;
    }
}
