using System.Globalization;

namespace DBTool.Domain;

/// <summary>Shared cell policy for text, flags, rebar tokens and numeric values.</summary>
public static class RawCellReader
{
    public static OperationResult<string> ReadText(RawCell cell, NumericPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(policy);
        if (cell.Kind == RawCellKind.Error) return Fail<string>("CELL_ERROR", cell);
        if (cell.Kind == RawCellKind.Formula)
        {
            if (!policy.AcceptCachedFormula || string.IsNullOrWhiteSpace(cell.CachedText))
                return Fail<string>("FORMULA_VALUE_UNAVAILABLE", cell);
            if (cell.CachedKind == RawCellKind.Error || cell.CachedText.TrimStart().StartsWith('#')) return Fail<string>("CELL_ERROR", cell);
            string text = cell.CachedText.Trim();
            if (cell.CachedKind == RawCellKind.Boolean)
            {
                if (!bool.TryParse(text, out bool flag)) return Fail<string>("CELL_ERROR", cell);
                text = flag ? "Có" : "Không";
            }
            else if (cell.CachedKind == RawCellKind.Number)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
                    return Fail<string>("NUMBER_NOT_FINITE", cell);
                text = number.ToString("R", CultureInfo.GetCultureInfo(policy.CultureName));
            }
            return new(true, text, [new Diagnostic("FORMULA_CACHE_UNVERIFIED",
                DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, cell.Source, null,
                cell.CachedText, "Cached formula value was not recalculated by Excel.")]);
        }
        return new(true, cell.Kind switch
        {
            RawCellKind.Text => cell.Text!.Trim(),
            RawCellKind.Number => cell.Number!.Value.ToString("R", CultureInfo.GetCultureInfo(policy.CultureName)),
            RawCellKind.Boolean => cell.Boolean!.Value ? "Có" : "Không",
            _ => ""
        }, []);
    }

    public static OperationResult<NumericValue> ReadNumber(RawCell cell, NumericPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(policy);
        if (cell.Kind == RawCellKind.Blank) return Fail<NumericValue>("NUMBER_MISSING", cell);
        if (cell.Kind == RawCellKind.Number) return new(true, new(cell.Number!.Value), []);
        var text = ReadText(cell, policy);
        if (!text.Success) return new(false, null, text.Diagnostics);
        if (cell.Kind is not (RawCellKind.Text or RawCellKind.Formula)) return Fail<NumericValue>("NUMBER_FORMAT", cell);
        if (text.Value is "NaN" or "Infinity" or "-Infinity" or "+Infinity")
            return Fail<NumericValue>("NUMBER_NOT_FINITE", cell);
        var styles = cell.Kind == RawCellKind.Formula && cell.CachedKind == RawCellKind.Number
            ? NumberStyles.Float : NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!double.TryParse(text.Value, styles,
                CultureInfo.GetCultureInfo(policy.CultureName), out double value))
            return Fail<NumericValue>("NUMBER_FORMAT", cell);
        if (!double.IsFinite(value)) return Fail<NumericValue>("NUMBER_NOT_FINITE", cell);
        return new(true, new(value), text.Diagnostics);
    }

    private static OperationResult<T> Fail<T>(string code, RawCell cell) where T : class =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, cell.Source, null, cell.CachedText ?? cell.Source.RawText,
            $"Cannot read cell: {code}.")]);
}
