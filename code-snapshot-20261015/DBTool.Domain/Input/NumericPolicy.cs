using System.Collections.Immutable;
namespace DBTool.Domain;
public sealed record NumericValue
{
    public NumericValue(double value)
    {
        Value = ContractGuard.Finite(value, nameof(value));

    }
    public double Value { get; }
}
public sealed record NumericPolicy
{
    public NumericPolicy(string cultureName, bool allowThousands, bool acceptCachedFormula)
    {
        CultureName = ContractGuard.Text(cultureName, nameof(cultureName));
        DecimalSeparator = System.Globalization.CultureInfo.GetCultureInfo(cultureName).NumberFormat.NumberDecimalSeparator;
        if (allowThousands) throw new ArgumentException("Thousands separators are disabled in contract-v1.");
        AllowThousands = false; AcceptCachedFormula = acceptCachedFormula;
    }
    public string CultureName { get; }
    public string DecimalSeparator { get; }
    public bool AllowThousands { get; }
    public bool AcceptCachedFormula { get; }
}

