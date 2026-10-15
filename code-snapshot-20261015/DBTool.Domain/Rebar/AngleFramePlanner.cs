using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record AngleOccurrence
{
    public AngleOccurrence(string id, string componentId, string catalogCode, BarPath path, double lengthMm,
        double unitMassKgPerM, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id)); ComponentId = ContractGuard.Text(componentId, nameof(componentId));
        CatalogCode = ContractGuard.Text(catalogCode, nameof(catalogCode)); Path = ContractGuard.Required(path);
        LengthMm = ContractGuard.Positive(lengthMm, nameof(lengthMm)); UnitMassKgPerM = ContractGuard.Positive(unitMassKgPerM, nameof(unitMassKgPerM));
        Sources = ContractGuard.Copy(sources);
    }
    public string Id { get; }
    public string ComponentId { get; }
    public string CatalogCode { get; }
    public BarPath Path { get; }
    public double LengthMm { get; }
    public double UnitMassKgPerM { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record AngleLayout
{
    public AngleLayout(IEnumerable<AngleOccurrence> occurrences, IEnumerable<Diagnostic> diagnostics,
        CalculationScope scope = CalculationScope.FullRebar, LayoutStatus status = LayoutStatus.Complete)
    {
        Occurrences = ContractGuard.Copy(occurrences); Diagnostics = ContractGuard.Copy(diagnostics);
        Scope = ContractGuard.EnumValue(scope); Status = ContractGuard.EnumValue(status);
        if (Occurrences.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Occurrences.Length) throw new ArgumentException("Duplicate angle occurrence.");
        TotalMassKg = new MassKg(Occurrences.Sum(x => x.LengthMm / 1000d * x.UnitMassKgPerM));
    }
    public ImmutableArray<AngleOccurrence> Occurrences { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public CalculationScope Scope { get; }
    public LayoutStatus Status { get; }
    public MassKg TotalMassKg { get; }
}

public static class AngleFramePlanner
{
    public static OperationResult<AngleLayout> PlanAngles(NeckSeatProfile seat, EnabledPositionsSet enabledPositions,
        SteelAngleEntry catalog, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(seat); ArgumentNullException.ThrowIfNull(enabledPositions);
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(policy);
        var occurrences = new List<AngleOccurrence>();
        foreach (int position in enabledPositions.Positions)
        {
            var frame = position switch
            {
                1 => new AngleFrameSpec(1, seat.InnerBoundary, seat.SupportPlaneMm, seat.Sources),
                2 => new AngleFrameSpec(2, seat.OuterBoundary, seat.SupportPlaneMm + seat.SeatHeightMm, seat.Sources),
                3 => new AngleFrameSpec(3, seat.OuterBoundary, seat.TopPlaneMm, seat.Sources),
                _ => throw new ArgumentOutOfRangeException()
            };
            var path = RectanglePath(frame.Boundary, frame.PlaneMm);
            double perimeter = 2 * ((frame.Boundary.MaxX - frame.Boundary.MinX) + (frame.Boundary.MaxY - frame.Boundary.MinY));
            occurrences.Add(new AngleOccurrence($"Neck/Angle/{position}", "Neck", catalog.Code, path, perimeter,
                catalog.KgPerMetre, seat.Sources));
        }
        return new(true, new AngleLayout(occurrences, []), []);
    }

    /// <summary>HDSD page 10: 1 outer top convex, 2 recessed concave, 3 original inner convex.</summary>
    public static OperationResult<AngleLayout> PlanVhgAngles(NeckSeatProfile seat,Rectangle2Mm originalInner,
        AngleFrameInput input,SteelAngleEntry catalog)
    {
        var bars=new List<AngleOccurrence>();
        foreach(int position in input.Positions.Positions)
        {
            var boundary=position==1?seat.OuterBoundary:position==2?seat.InnerBoundary:originalInner;
            double z=position==1?seat.TopPlaneMm:seat.SupportPlaneMm;
            var path=RectanglePath(boundary,z);
            bars.Add(new($"Neck/Angle/{position}","Neck",catalog.Code,path,
                2*(boundary.MaxX-boundary.MinX+boundary.MaxY-boundary.MinY),catalog.KgPerMetre,seat.Sources.Concat(input.Sources)));
        }
        return new(true,new AngleLayout(bars,[]),[]);
    }

    private static BarPath RectanglePath(Rectangle2Mm rectangle, double z)
    {
        var a = new Point3Mm(rectangle.MinX, rectangle.MinY, z);
        var b = new Point3Mm(rectangle.MaxX, rectangle.MinY, z);
        var c = new Point3Mm(rectangle.MaxX, rectangle.MaxY, z);
        var d = new Point3Mm(rectangle.MinX, rectangle.MaxY, z);
        return new BarPath([new LineSegment3Mm(a, b), new LineSegment3Mm(b, c), new LineSegment3Mm(c, d), new LineSegment3Mm(d, a)],
            true, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
    }
}
