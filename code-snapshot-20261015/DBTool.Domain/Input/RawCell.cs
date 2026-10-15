using System.Collections.Immutable;
namespace DBTool.Domain;
public enum RawCellKind { Blank, Text, Number, Boolean, Formula, Error }
public sealed record RawCell
{
    public RawCell(RawCellKind kind, string? text, double? number, bool? boolean, string? formula, string? cachedText, SourceRef source,
        RawCellKind? cachedKind = null)
    {
        Kind = ContractGuard.EnumValue(kind);
        Text = text;
        Number = number;
        Boolean = boolean;
        Formula = formula;
        CachedText = cachedText;
        CachedKind = cachedKind;
        if (cachedKind.HasValue && (kind != RawCellKind.Formula || cachedKind is RawCellKind.Formula || !Enum.IsDefined(cachedKind.Value)))
            throw new ArgumentException("Cached kind is only valid for a formula value.");
        Source = ContractGuard.Required(source);
        if (number is double n) ContractGuard.Finite(n, nameof(number));
        bool valid = kind switch {
            RawCellKind.Blank => text == null && number == null && boolean == null && formula == null && cachedText == null,
            RawCellKind.Text or RawCellKind.Error => text != null && number == null && boolean == null && formula == null && cachedText == null,
            RawCellKind.Number => number != null && text == null && boolean == null && formula == null && cachedText == null,
            RawCellKind.Boolean => boolean != null && text == null && number == null && formula == null && cachedText == null,
            RawCellKind.Formula => !string.IsNullOrWhiteSpace(formula) && text == null && number == null && boolean == null,
            _ => false };
        if (!valid) throw new ArgumentException("Raw cell payload mismatch.");
    }
    public RawCellKind Kind { get; }
    public string? Text { get; }
    public double? Number { get; }
    public bool? Boolean { get; }
    public string? Formula { get; }
    public string? CachedText { get; }
    public RawCellKind? CachedKind { get; }
    public SourceRef Source { get; }
}
