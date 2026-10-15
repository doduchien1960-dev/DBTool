using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record FabricationLength
{
    internal FabricationLength(double value) { Value = ContractGuard.Positive(value, nameof(value)); }
    public double Value { get; }
}

public static class BendLengthCalculator
{
    /// <summary>Measures a real tangent centerline. The argument and path metadata identify one supplemental lap.</summary>
    public static OperationResult<FabricationLength> Calculate(BarPath path, double extraLapMm, SourceRef? source = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if(path.MeasurementBasis==MeasurementBasis.NominalPolyline)
            throw new ArgumentException("Use NominalFabricationLength with an explicit diameter/family for nominal paths.",nameof(path));
        if (!double.IsFinite(extraLapMm) || extraLapMm < 0) return Failure("BAR_LENGTH_INVALID", source);
        if (extraLapMm > 0 && path.AdditionalLapMm > 0 && extraLapMm != path.AdditionalLapMm)
            return Failure("BAR_LAP_CONFLICT", source);
        double total = 0;
        foreach (var segment in path.Segments)
            total += segment switch
            {
                LineSegment3Mm line => Distance(line.Start, line.End),
                ArcSegment3Mm arc => Math.Abs(arc.SweepRadians) * arc.CenterlineRadiusMm,
                _ => throw new InvalidOperationException("Unknown path segment.")
            };
        if (!path.LapAlreadyInPath) total += extraLapMm > 0 ? extraLapMm : path.AdditionalLapMm;
        return Result(total, source);
    }

    /// <summary>Evaluates explicit dimension/bend policy without misrepresenting theoretical corners as a BarPath.</summary>
    public static OperationResult<FabricationLength> CalculateDimensions(IReadOnlyList<double> legsMm, double diameterMm, BendPolicy policy, SourceRef? source = null)
    {
        ArgumentNullException.ThrowIfNull(legsMm);
        ArgumentNullException.ThrowIfNull(policy);
        if (!double.IsFinite(diameterMm) || diameterMm <= 0 || legsMm.Count == 0 ||
            legsMm.Any(x => !double.IsFinite(x) || x <= 0) || policy.AnglesRadians.Length != legsMm.Count - 1)
            return Failure("BAR_BEND_INVALID", source);
        if (policy.AnglesRadians.Length > 0 && policy.InnerRadiusMm is null)
            return Failure("FABRICATION_RULE_MISSING", source);
        double radius = (policy.InnerRadiusMm ?? 0) + diameterMm / 2;
        if (!double.IsFinite(radius) || radius <= 0) return Failure("BAR_BEND_INVALID", source);
        var tangent = legsMm.ToArray();
        double arcTotal = 0;
        for (int i = 0; i < policy.AnglesRadians.Length; i++)
        {
            double angle = Math.Abs(policy.AnglesRadians[i]);
            arcTotal += angle * radius;
            if (policy.MeasurementBasis == MeasurementBasis.TheoreticalCorner)
            {
                double trim = radius * Math.Tan(angle / 2);
                tangent[i] -= trim;
                tangent[i + 1] -= trim;
            }
        }
        if (tangent.Any(x => !double.IsFinite(x) || x <= 0)) return Failure("BAR_BEND_INVALID", source);
        return Result(tangent.Sum() + arcTotal + (policy.ExtraLapAlreadyInPath ? 0 : policy.AdditionalLapMm), source);
    }

    internal static double Distance(Point3Mm a, Point3Mm b)
    {
        double x = Math.Abs(a.X - b.X), y = Math.Abs(a.Y - b.Y), z = Math.Abs(a.Z - b.Z);
        double scale = Math.Max(x, Math.Max(y, z));
        if (scale == 0) return 0;
        if (!double.IsFinite(scale)) return double.PositiveInfinity;
        return scale * Math.Sqrt(Math.Pow(x / scale, 2) + Math.Pow(y / scale, 2) + Math.Pow(z / scale, 2));
    }

    private static OperationResult<FabricationLength> Result(double length, SourceRef? source) =>
        double.IsFinite(length) && length > 0 ? new(true, new FabricationLength(length), []) : Failure("BAR_LENGTH_INVALID", source);

    internal static OperationResult<FabricationLength> Failure(string code, SourceRef? source) => new(false, null,
        [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source,
            "consistent finite lengths, explicit bend rule and single lap", null, "Fabrication length cannot be resolved from these inputs.")]);
}
