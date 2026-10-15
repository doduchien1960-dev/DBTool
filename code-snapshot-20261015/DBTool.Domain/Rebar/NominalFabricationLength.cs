using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Owner-approved Phase 1B fallback, separate from VHG nominal schedule dimensions.</summary>
public static class NominalFabricationLength
{
    public const string RuleId = "VHG-OWNER-BEND-FALLBACK-1B-v1";

    public static double InnerRadius(double diameterMm, bool smoothOrTie = false, bool lifting = false) =>
        diameterMm * (lifting ? 2 : smoothOrTie ? 1.25 : diameterMm <= 16 ? 2 : 2.5);

    public static double? Calculate(BarPath path, double diameterMm, bool smoothOrTie = false, bool lifting = false)
    {
        if (path.Segments.Any(s => s is not LineSegment3Mm)) return null;
        double radius = InnerRadius(diameterMm, smoothOrTie, lifting) + diameterMm / 2;
        var lengths = path.Segments.Select(s => Distance(s.Start, s.End)).ToArray();
        var trims = new double[lengths.Length];
        double correction = 0;
        for (int i = path.Closed ? 0 : 1; i < lengths.Length; i++)
        {
            int previous = (i + lengths.Length - 1) % lengths.Length;
            var a = path.Segments[previous];
            var b = path.Segments[i];
            double dot = ((a.End.X - a.Start.X) * (b.End.X - b.Start.X)
                + (a.End.Y - a.Start.Y) * (b.End.Y - b.Start.Y) + (a.End.Z - a.Start.Z) * (b.End.Z - b.Start.Z))
                / (lengths[previous] * lengths[i]);
            double angle = Math.Acos(Math.Clamp(dot, -1, 1));
            double trim = radius * Math.Tan(angle / 2);
            if (!double.IsFinite(trim) || angle >= Math.PI - 1e-9) return null;
            trims[previous] += trim;
            trims[i] += trim;
            correction += radius * angle - 2 * trim;
        }
        if (lengths.Where((length, i) => length + 1e-6 < trims[i]).Any()) return null;
        double result = lengths.Sum() + correction + (path.LapAlreadyInPath ? 0 : path.AdditionalLapMm);
        return double.IsFinite(result) && result > 0 ? result : null;
    }

    public static (double? Length, LengthStatus Status) CalculateWithStatus(BarPath path, double diameterMm, bool smoothOrTie = false, bool lifting = false)
    {
        var len = Calculate(path, diameterMm, smoothOrTie, lifting);
        return len.HasValue ? (len.Value, LengthStatus.Known) : (null, LengthStatus.NeedsRule);
    }

    private static double Distance(Point3Mm a, Point3Mm b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
