using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed record SheetNameRequest
{
    public SheetNameRequest(string manholeId, string displayName)
    { ManholeId=manholeId; DisplayName=displayName; }
    public string ManholeId { get; }
    public string DisplayName { get; }
}

public sealed record SheetNameMap
{
    public SheetNameMap(IEnumerable<KeyValuePair<string,string>> names) =>
        Names = names.ToImmutableDictionary(StringComparer.Ordinal);
    public ImmutableDictionary<string,string> Names { get; }
}

public static class SheetNameAllocator
{
    private static readonly string[] BuiltInReserved = ["TH","Temp","Steel","Summary","Metadata","Trace","History"];

    public static OperationResult<SheetNameMap> AllocateSheetNames(
        IEnumerable<SheetNameRequest> requests, IEnumerable<string> reserved)
    {
        if (requests is null || reserved is null)
            return Block("SHEET_NAME_INVALID", "Requests and reserved names are required.");
        var items=requests.ToArray();
        if (items.Any(x => x is null || string.IsNullOrWhiteSpace(x.ManholeId)) ||
            items.Select(x => x.ManholeId).Distinct(StringComparer.Ordinal).Count()!=items.Length)
            return Block("SHEET_ID_DUPLICATE", "Manhole IDs must be unique.");
        var used=new HashSet<string>(BuiltInReserved.Concat(reserved),StringComparer.OrdinalIgnoreCase);
        var map=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var item in items.OrderBy(x => x.ManholeId,StringComparer.Ordinal))
        {
            string stem = Sanitize(item.DisplayName);
            for (int n=1; ; n++)
            {
                string suffix = n==1 ? "" : $"__{n}";
                string candidate = stem[..Math.Min(stem.Length,31-suffix.Length)] + suffix;
                if (!used.Add(candidate)) continue;
                map.Add(item.ManholeId,candidate);
                break;
            }
        }
        return new(true,new SheetNameMap(map),[]);
    }

    public static string FormulaReference(string sheetName,string cellAddress)
    {
        if (string.IsNullOrWhiteSpace(sheetName) || string.IsNullOrWhiteSpace(cellAddress) ||
            cellAddress.Any(ch => !char.IsLetterOrDigit(ch) && ch != '$'))
            throw new ArgumentException("Invalid formula reference.");
        return $"'{sheetName.Replace("'","''")}'!{cellAddress}";
    }

    private static string Sanitize(string? value)
    {
        string text=new string((value ?? "").Select(ch => ch is '[' or ']' or ':' or '*' or '?' or '/' or '\\' || char.IsControl(ch) ? '_' : ch).ToArray())
            .Trim(' ','\'');
        return text.Length==0 ? "Ga" : text;
    }

    private static OperationResult<SheetNameMap> Block(string code,string message) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
