using System.Globalization;
using System.Text.RegularExpressions;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Infrastructure.Excel;

public sealed class NumericCellReader : IRawValueParser
{
    private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public OperationResult<NumericValue> ReadNumber(RawCell cell, NumericPolicy policy)
    {
        return RawCellReader.ReadNumber(cell, policy);
    }

    public OperationResult<Dimensions2Mm> ReadDimensions(string text,NumericPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        string[] parts = Regex.Split(text?.Trim() ?? "", "[xX×]");
        if (parts.Length != 2) return Fail<Dimensions2Mm>("DIMENSIONS_FORMAT",null);
        var width = Parse(parts[0],policy,null);
        var height = Parse(parts[1],policy,null);
        if (!width.Success || !height.Success || width.Value!.Value <= 0 || height.Value!.Value <= 0)
            return Fail<Dimensions2Mm>("DIMENSIONS_FORMAT",null);
        return new(true,new Dimensions2Mm(width.Value.Value,height.Value.Value),Array.Empty<Diagnostic>());
    }

    private static OperationResult<NumericValue> Parse(string text,NumericPolicy policy,SourceRef? source)
    {
        string trimmed = text.Trim();
        if (trimmed is "NaN" or "Infinity" or "-Infinity" or "+Infinity")
            return Fail<NumericValue>("NUMBER_NOT_FINITE",source);
        CultureInfo culture = CultureInfo.GetCultureInfo(policy.CultureName);
        if (!double.TryParse(trimmed,Styles,culture,out double value))
            return Fail<NumericValue>("NUMBER_FORMAT",source);
        if (!double.IsFinite(value)) return Fail<NumericValue>("NUMBER_NOT_FINITE",source);
        return new(true,new NumericValue(value),Array.Empty<Diagnostic>());
    }

    private static OperationResult<T> Fail<T>(string code,SourceRef? source) where T:class =>
        new(false,null,new[] { new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,
            null,null,source,null,source?.RawText,$"Cannot read numeric value: {code}.") });
}
