using System.Collections.Immutable;

namespace DBTool.Domain;

public sealed record LoopStations
{
    public LoopStations(IEnumerable<double> stationsMm, IEnumerable<double> cyclicGapsMm)
    {
        StationsMm = ContractGuard.Copy(stationsMm);
        CyclicGapsMm = ContractGuard.Copy(cyclicGapsMm);
        if (StationsMm.IsEmpty || CyclicGapsMm.Length != StationsMm.Length)
            throw new ArgumentException("Loop station/gap count mismatch.");
        foreach (double station in StationsMm) ContractGuard.Finite(station, nameof(stationsMm));
        foreach (double gap in CyclicGapsMm) ContractGuard.Positive(gap, nameof(cyclicGapsMm));
    }

    public ImmutableArray<double> StationsMm { get; }
    public ImmutableArray<double> CyclicGapsMm { get; }
}

public static class ClosedLoopDistributor
{
    public static OperationResult<LoopStations> DistributeClosedLoop(
        double perimeterMm, double spacingMm, DistributionMode mode, double seamMm)
    {
        if (!double.IsFinite(perimeterMm) || perimeterMm <= 0 ||
            !double.IsFinite(spacingMm) || spacingMm <= 0 ||
            !double.IsFinite(seamMm) || !Enum.IsDefined(mode))
            return Block("DISTRIBUTION_INVALID_INPUT", "Loop perimeter, spacing, seam or mode are invalid.");

        double ratio = perimeterMm / spacingMm;
        if (!double.IsFinite(ratio))
            return Block("DISTRIBUTION_INVALID_INPUT", "Loop count is not finite.");
        double count = Math.Max(1, mode == DistributionMode.Equalized ? Math.Ceiling(ratio) : Math.Floor(ratio));
        if (count >= int.MaxValue)
            return Block("DISTRIBUTION_TOO_MANY_BARS", "Loop count exceeds supported collection size.");
        int n = (int)count;
        double seam = Mod(seamMm, perimeterMm);
        if (n == 1)
            return Success(new[] { seam }, new[] { perimeterMm });

        double first = mode == DistributionMode.Equalized ? 0 : (perimeterMm - (n - 1) * spacingMm) / 2;
        double step = mode == DistributionMode.Equalized ? perimeterMm / n : spacingMm;
        var stations = new double[n];
        var gaps = new double[n];
        for (int i = 0; i < n; i++)
        {
            stations[i] = Mod(Mod(first + i * step, perimeterMm) + seam, perimeterMm);
            gaps[i] = i == n - 1 ? perimeterMm - (n - 1) * step : step;
            if (!double.IsFinite(stations[i]) || !double.IsFinite(gaps[i]) || gaps[i] <= 0 ||
                (i > 0 && stations[i] == stations[i - 1]))
                return Block("DISTRIBUTION_PRECISION_LOSS", "Loop stations cannot be represented distinctly at this scale.");
        }
        return Success(stations, gaps);
    }

    private static double Mod(double value, double modulus)
    {
        double remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static OperationResult<LoopStations> Success(double[] stations, double[] gaps) =>
        new(true, new LoopStations(stations, gaps), Array.Empty<Diagnostic>());

    private static OperationResult<LoopStations> Block(string code, string message) =>
        new(false, null, new[] { new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message) });
}
