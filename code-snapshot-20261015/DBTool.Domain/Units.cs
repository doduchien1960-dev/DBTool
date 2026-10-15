namespace DBTool.Domain;

public readonly record struct LengthMm
{
    public LengthMm(double value) => Value = ContractGuard.Finite(value, nameof(value));
    public double Value { get; }
}

public readonly record struct AreaMm2
{
    public AreaMm2(double value) => Value = ContractGuard.NonNegative(value, nameof(value));
    public double Value { get; }
}

public readonly record struct VolumeMm3
{
    public VolumeMm3(double value) => Value = ContractGuard.NonNegative(value, nameof(value));
    public double Value { get; }
}

public readonly record struct ElevationM
{
    public ElevationM(double value) => Value = ContractGuard.Finite(value, nameof(value));
    public double Value { get; }
}

public readonly record struct MassKg
{
    public MassKg(double value) => Value = ContractGuard.NonNegative(value, nameof(value));
    public double Value { get; }
}

public static class Units
{
    public static double ToMetres(LengthMm value) => value.Value / 1000d;
    public static double ToSquareMetres(AreaMm2 value) => value.Value / 1_000_000d;
    public static double ToCubicMetres(VolumeMm3 value) => value.Value / 1_000_000_000d;
    public static ElevationM ToAbsoluteElevation(ElevationM origin, LengthMm offset) => new(origin.Value + offset.Value / 1000d);
}

