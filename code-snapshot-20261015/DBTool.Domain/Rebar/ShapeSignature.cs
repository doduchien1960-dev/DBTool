using System.Globalization;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class ShapeSignature
{
    /// <summary>
    /// Intrinsic, versioned shape key; excludes placement, CAD names, axis, layer and allocated marks.
    /// Geometry is canonicalized to 1e-6 mm and direction invariants to 1e-12. Raw lengths are not rounded.
    /// </summary>
    public static string Create(BarPath path, double diameterMm) =>
        Create(path, diameterMm, null, null, null);

    public static string Create(BarPath path, double diameterMm, string? hostScope,
        MeasurementBasisKind? basis, IEnumerable<string>? orderedRoles)
    {
        ArgumentNullException.ThrowIfNull(path);
        ContractGuard.Positive(diameterMm, nameof(diameterMm));
        var tokens = new List<string>
        {
            "shape-v2",
            hostScope ?? "*",
            (basis ?? (path.MeasurementBasis == MeasurementBasis.NominalPolyline ? MeasurementBasisKind.Nominal : MeasurementBasisKind.Centerline)).ToString(),
            path.Closed.ToString(),
            path.HookPattern.ToString(),
            path.MeasurementBasis.ToString(),
            Number(diameterMm, 6),
            Number(path.AdditionalLapMm, 6),
            path.LapAlreadyInPath.ToString()
        };

        if (orderedRoles is not null)
        {
            tokens.Add("roles:" + string.Join(",", orderedRoles));
        }

        var points = new List<Point3Mm>();
        var directions = new List<Vector3>();
        foreach (var segment in path.Segments)
        {
            points.Add(segment.Start);
            points.Add(segment.End);
            if (segment is ArcSegment3Mm arc)
            {
                tokens.Add($"Arc:{Number(arc.CenterlineRadiusMm, 6)}:{Number(Math.Abs(arc.SweepRadians), 12)}");
                points.Add(arc.Center);
                directions.Add(arc.StartDirection);
                double sign = Math.Sign(arc.SweepRadians);
                directions.Add(new Vector3(sign * arc.Normal.X, sign * arc.Normal.Y, sign * arc.Normal.Z));
            }
            else tokens.Add("Line");
        }

        // Pair distances preserve dimension and relative placement without world coordinates.
        for (int i = 0; i < points.Count; i++)
        {
            double fromOrigin = BendLengthCalculator.Distance(points[0], points[i]);
            if (fromOrigin > 1e-6 && double.IsFinite(fromOrigin))
                directions.Add(new Vector3((points[i].X - points[0].X) / fromOrigin,
                    (points[i].Y - points[0].Y) / fromOrigin, (points[i].Z - points[0].Z) / fromOrigin));
            for (int j = 0; j < i; j++) tokens.Add(Number(BendLengthCalculator.Distance(points[i], points[j]), 6));
        }
        for (int i = 0; i < directions.Count; i++)
            for (int j = 0; j < i; j++) tokens.Add(Number(FrameInvariant.Dot(directions[i], directions[j]), 12));

        // Signed triple products distinguish spatial chirality while remaining invariant under proper rotation.
        if (directions.Count > 1)
        {
            Vector3? normal = null;
            foreach (var direction in directions.Skip(1))
            {
                var a = directions[0];
                var cross = new Vector3(a.Y * direction.Z - a.Z * direction.Y,
                    a.Z * direction.X - a.X * direction.Z, a.X * direction.Y - a.Y * direction.X);
                if (FrameInvariant.Dot(cross, cross) > 1e-12) { normal = cross; break; }
            }
            if (normal is not null)
                foreach (var direction in directions) tokens.Add(Number(FrameInvariant.Dot(normal, direction), 12));
        }
        return string.Join("|", tokens);
    }

    private static string Number(double value, int digits)
    {
        ContractGuard.Finite(value, nameof(value));
        double canonical = Math.Round(value, digits, MidpointRounding.AwayFromZero);
        return (canonical == 0 ? 0 : canonical).ToString("R", CultureInfo.InvariantCulture);
    }
}
