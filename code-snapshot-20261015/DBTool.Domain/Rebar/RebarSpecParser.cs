using System.Globalization;
using System.Text.RegularExpressions;

namespace DBTool.Domain;

public static class RebarSpecParser
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly Regex MeshPattern = new(@"\AD([0-9]+)A([0-9]+(?:\.[0-9]+)?)\z",Options);
    private static readonly Regex AnchorPattern = new(@"\AD([0-9]+)A([0-9]+(?:\.[0-9]+)?)L([0-9]+(?:\.[0-9]+)?)\z",Options);
    private static readonly Regex CountedPattern = new(@"\A([0-9]+)D([0-9]+)\z",Options);

    public static OperationResult<ParsedRebar> Parse(string? text, ParseContext context, SourceRef source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string value = text?.Trim() ?? "";
        if (value.Length == 0)
            return context == ParseContext.MeshRequired ? Block("REBAR_REQUIRED","Rebar specification is required.",source)
                : Ok(new ParsedRebar(RebarKind.Disabled,null,null,null,source));
        if (context == ParseContext.AnchorOptional && value == "0")
            return Ok(new ParsedRebar(RebarKind.Disabled,null,null,null,source));
        if (context == ParseContext.MeshRequired && (value == "0" || CountedPattern.IsMatch(value)))
            return Block("REBAR_CONTEXT_INVALID","A mesh specification is required here.",source);
        if (context == ParseContext.LiftingHook && MeshPattern.IsMatch(value))
            return Block("REBAR_CONTEXT_INVALID","A counted lifting bar is required here.",source);

        if (context is ParseContext.MeshRequired or ParseContext.MeshOptional)
        {
            var match = MeshPattern.Match(value);
            if (match.Success && PositiveInt(match.Groups[1].Value,out int diameter) &&
                PositiveDouble(match.Groups[2].Value,out double spacing))
                return Ok(new ParsedRebar(RebarKind.Mesh,new MeshSpec(diameter,spacing),null,null,source));
        }
        else if (context == ParseContext.AnchorOptional)
        {
            var match = AnchorPattern.Match(value);
            if (match.Success && PositiveInt(match.Groups[1].Value,out int diameter) &&
                PositiveDouble(match.Groups[2].Value,out double spacing) &&
                PositiveDouble(match.Groups[3].Value,out double length))
                return Ok(new ParsedRebar(RebarKind.Anchor,null,null,new AnchorSpec(diameter,spacing,length),source));
        }
        else if (context == ParseContext.LiftingHook)
        {
            var match = CountedPattern.Match(value);
            if (match.Success && int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int count) &&
                PositiveInt(match.Groups[2].Value,out int diameter))
                return count == 0 ? Ok(new ParsedRebar(RebarKind.Disabled,null,null,null,source))
                    : Ok(new ParsedRebar(RebarKind.Counted,null,new CountedBarSpec(count,diameter),null,source));
        }
        return Block("REBAR_FORMAT","Invalid rebar grammar or nonpositive/overflow value.",source);
    }

    private static bool PositiveInt(string text,out int value) =>
        int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out value) && value > 0;

    private static bool PositiveDouble(string text,out double value) =>
        double.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value) &&
        double.IsFinite(value) && value > 0;

    private static OperationResult<ParsedRebar> Ok(ParsedRebar value) => new(true,value,Array.Empty<Diagnostic>());
    private static OperationResult<ParsedRebar> Block(string code,string message,SourceRef source) =>
        new(false,null,new[] { new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,
            null,null,source,null,source.RawText,message) });
}
