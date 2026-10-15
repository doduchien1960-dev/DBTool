using System.Collections.Immutable;
namespace DBTool.Domain;

/// <summary>Boundary checks and eager immutable copies shared by contract assemblies.</summary>
public static class ContractGuard
{
    public static double Finite(double value, string name) => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(name);
    public static double NonNegative(double value, string name) => Finite(value, name) >= 0 ? value : throw new ArgumentOutOfRangeException(name);
    public static double Positive(double value, string name) => Finite(value, name) > 0 ? value : throw new ArgumentOutOfRangeException(name);
    public static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
    public static string Text(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Required text.", name);
    public static string Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit) ? value.ToUpperInvariant() : throw new ArgumentException("SHA256 must contain 64 hex characters.");
    public static T EnumValue<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    public static T Required<T>(T? value) where T : class => value ?? throw new ArgumentNullException(nameof(value));
    public static ImmutableArray<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = values.ToImmutableArray();
        if (copy.Any(x => x is null)) throw new ArgumentException("Null collection member.");
        return copy;
    }
    public static ImmutableDictionary<TKey, TValue> Map<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values)
        where TKey : struct, Enum
        where TValue : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = ImmutableDictionary.CreateBuilder<TKey, TValue>();
        foreach (var pair in values)
        {
            var key = EnumValue(pair.Key);
            var value = EnumValue(pair.Value);
            if (result.ContainsKey(key)) throw new ArgumentException("Duplicate map key.", nameof(values));
            result.Add(key, value);
        }
        return result.ToImmutable();
    }

    public static ImmutableDictionary<string, T> Map<T>(IEnumerable<KeyValuePair<string,T>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = ImmutableDictionary.CreateBuilder<string,T>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            Text(pair.Key, nameof(values));
            if (pair.Value is null) throw new ArgumentException("Null map value.");
            result.Add(pair.Key, pair.Value);
        }
        return result.ToImmutable();
    }
}
