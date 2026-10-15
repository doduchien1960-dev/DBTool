using System.Collections.Immutable;

namespace DBTool.Domain;

public sealed record BarPositions
{
    public BarPositions(IEnumerable<double> positionsMm, IEnumerable<double> actualGapsMm)
    {
        PositionsMm = ContractGuard.Copy(positionsMm);
        ActualGapsMm = ContractGuard.Copy(actualGapsMm);
        if (PositionsMm.IsEmpty || ActualGapsMm.Length != PositionsMm.Length - 1)
            throw new ArgumentException("Segment position/gap count mismatch.");
        for (int i = 0; i < PositionsMm.Length; i++)
        {
            ContractGuard.Finite(PositionsMm[i], nameof(positionsMm));
            if (i > 0 && PositionsMm[i] <= PositionsMm[i - 1])
                throw new ArgumentException("Segment positions must increase strictly.");
        }
        foreach (double gap in ActualGapsMm) ContractGuard.Positive(gap, nameof(actualGapsMm));
    }

    public ImmutableArray<double> PositionsMm { get; }
    public ImmutableArray<double> ActualGapsMm { get; }
}

public static class BarDistributor
{
    private const double LengthToleranceMm = 1e-6;

    public static OperationResult<BarPositions> DistributeSegment(
        double spanMm, double coverMm, double diameterMm, double spacingMm, DistributionMode mode)
    {
        if (!double.IsFinite(spanMm) || spanMm <= 0 ||
            !double.IsFinite(coverMm) || coverMm < 0 ||
            !double.IsFinite(diameterMm) || diameterMm <= 0 ||
            !double.IsFinite(spacingMm) || spacingMm <= 0 ||
            !Enum.IsDefined(mode))
            return Block("DISTRIBUTION_INVALID_INPUT", "Segment dimensions, spacing or mode are invalid.");

        double q = coverMm + diameterMm / 2;
        double usableMm = spanMm - 2 * q;
        if (!double.IsFinite(q) || !double.IsFinite(usableMm))
            return Block("DISTRIBUTION_INVALID_INPUT", "Segment arithmetic is not finite.");
        if (usableMm < 0)
            return Block("DISTRIBUTION_NO_CLEAR_SPAN", "Cover and diameter leave no clear span.");
        if (usableMm == 0)
            return Success(new[] { q }, Array.Empty<double>());

        double ratio = usableMm / spacingMm;
        if (!double.IsFinite(ratio))
            return Block("DISTRIBUTION_INVALID_INPUT", "Segment count is not finite.");

        double nearest = Math.Round(ratio);
        if (nearest >= 1 && double.IsFinite(nearest) &&
            Math.Abs(usableMm - nearest * spacingMm) <= LengthToleranceMm)
            ratio = nearest;

        double gapCount = mode == DistributionMode.Equalized ? Math.Ceiling(ratio) : Math.Floor(ratio);
        if (gapCount >= int.MaxValue)
            return Block("DISTRIBUTION_TOO_MANY_BARS", "Segment count exceeds supported collection size.");
        int k = (int)gapCount;
        if (k == 0)
            return Success(new[] { spanMm / 2 }, Array.Empty<double>());

        double step = mode == DistributionMode.Equalized ? usableMm / k : spacingMm;
        double first = mode == DistributionMode.Equalized ? q : q + (usableMm - k * spacingMm) / 2;
        var positions = new double[k + 1];
        var gaps = new double[k];
        for (int i = 0; i <= k; i++)
        {
            positions[i] = i == k && mode == DistributionMode.Equalized ? spanMm - q : first + i * step;
            if (i > 0)
            {
                gaps[i - 1] = positions[i] - positions[i - 1];
                if (!double.IsFinite(gaps[i - 1]) || gaps[i - 1] <= 0 ||
                    (mode == DistributionMode.Equalized && gaps[i - 1] > spacingMm + LengthToleranceMm))
                    return Block("DISTRIBUTION_PRECISION_LOSS", "Segment stations cannot be represented strictly at this scale.");
            }
        }
        return Success(positions, gaps);
    }

    private static OperationResult<BarPositions> Success(double[] positions, double[] gaps) =>
        new(true, new BarPositions(positions, gaps), Array.Empty<Diagnostic>());

    private static OperationResult<BarPositions> Block(string code, string message) =>
        new(false, null, new[] { new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message) });
}
