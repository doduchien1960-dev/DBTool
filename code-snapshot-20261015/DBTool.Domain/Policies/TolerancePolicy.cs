using System.Collections.Immutable;
namespace DBTool.Domain;
public sealed record TolerancePolicy
{
    public TolerancePolicy(double geometryMm, double lengthMm, double massKg, double? areaMm2, double? volumeMm3)
    {
        GeometryMm = ContractGuard.Positive(geometryMm, nameof(geometryMm));
        LengthMm = ContractGuard.Positive(lengthMm, nameof(lengthMm));
        MassKg = ContractGuard.Positive(massKg, nameof(massKg));
        AreaMm2 = areaMm2;
        VolumeMm3 = volumeMm3;
        if (areaMm2.HasValue) ContractGuard.Positive(areaMm2.Value, nameof(areaMm2));
        if (volumeMm3.HasValue) ContractGuard.Positive(volumeMm3.Value, nameof(volumeMm3));
    }
    public double GeometryMm { get; }
    public double LengthMm { get; }
    public double MassKg { get; }
    public double? AreaMm2 { get; }
    public double? VolumeMm3 { get; }
}
public sealed record RoundingPolicy
{
    public RoundingPolicy(int digits, MidpointRounding mode)
    {
        Digits = digits;
        Mode = ContractGuard.EnumValue(mode);
        if (digits is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(digits));
    }
    public int Digits { get; }
    public MidpointRounding Mode { get; }
}

