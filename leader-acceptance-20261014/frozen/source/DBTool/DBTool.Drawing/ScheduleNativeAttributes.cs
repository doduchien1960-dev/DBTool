using DBTool.Domain;
using System.Globalization;

namespace DBTool.Drawing;

public static class ScheduleNativeAttributes
{
    public const string SummaryBlockName="TK.THKL";
    public const string AngleBlockName="TK.TG";
    private static readonly string[] SummaryTags=["DAY1","DAY2","TUONG1","TUONG2","TRAN1","TRAN2","CO1","CO2","TONG"];
    private static string Format(double value) {
        if(!double.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));
        return value.ToString("R",CultureInfo.InvariantCulture);
    }
    // Native summary columns use diameter <=10 / >10 (not Bottom/Top layers).
    public static IReadOnlyList<BlockAttributeIntent> Summary(SteelSchedule schedule,AngleSchedule? angles)
    {
        var groups=new Dictionary<string,double>();
        foreach(var row in schedule.Rows)
        {
            string? prefix=row.ComponentKind switch {
                ComponentKind.Base=>"DAY",ComponentKind.Wall=>"TUONG",ComponentKind.Roof=>"TRAN",ComponentKind.Neck=>"CO",_=>null };
            if(prefix is null)continue; // Catch/panel/accessories remain separately scheduled; grand total includes them.
            string tag=prefix+(row.DiameterMm<=10?"1":"2");
            groups[tag]=groups.GetValueOrDefault(tag)+row.MassKg.Value;
        }
        return Summary(groups,schedule.TotalMassKg.Value+(angles?.TotalMassKg.Value??0));
    }
    public static IReadOnlyList<BlockAttributeIntent> Summary(IReadOnlyDictionary<string,double> groups,double total)
    {
        if(groups.Keys.Any(k=>!SummaryTags.Take(8).Contains(k)))throw new ArgumentException("Unknown summary group.");
        return SummaryTags.Select(k=>new BlockAttributeIntent(k,Format(k=="TONG"?total:groups.GetValueOrDefault(k)))).ToArray();
    }
    public static IReadOnlyList<BlockAttributeIntent> Angle(AngleScheduleRow row)=>Angle(row.Mark,row.CatalogCode,row.Quantity,row.SingleLengthMm,row.TotalLengthMm,row.UnitMassKgPerM,row.MassKg.Value);
    public static IReadOnlyList<BlockAttributeIntent> Angle(string mark,string catalog,double quantity,double length,double totalLength,double unitMass,double mass)=>
        [new("SH",mark),new("L",catalog),new("D",Format(unitMass)+"kg/m"),new("DAI",Format(length)),new("SL",Format(quantity)),new("TDAI",Format(totalLength/1000)),new("KL",Format(mass))];
    public const string HeaderBlockName="TK.Tieude";
    public const string GroupTitleBlockName="TK.Tenluoi";
    public const string CShapeBlockName="TK.M.C";
    public const string CircularReinforcementBlockName="TK.GC.T";
    public const string VerifiedShapeBlockName="TK.VG";
    public const string RectangularLapBlockName="TK.GC.H";
    public const string LiftingHookBlockName="TK.MC";
    public const string TwoHooksBlockName="TK.L2M";
    public const string OneHookBlockName="TK.L1M";
    public const string StraightBlockName="TK.L0M";
    public const string ZTieBlockName="TK.M.Z";
    public const string UShapeBlockName="TK.M.U";
    public const string AnchorBlockName="TK.NG";
    public const string PanelMesh1BlockName="TK.NA1";
    public const string PanelMesh2BlockName="TK.NA2";
    public const string PanelSummaryBlockName="TK.THKLNAP";
    public const string CatchMeshNBlockName="TK.CT.N";
    public const string CatchMeshTBlockName="TK.CT.T";
    public const string CatchSummaryBlockName="TK.THKL.CT";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ExactBlockSchemas =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [HeaderBlockName] = [],
            [GroupTitleBlockName] = ["TENLUOI"],
            [CShapeBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "LP", "LT", "L", "SH"],
            [CircularReinforcementBlockName] = ["SH", "L", "LT", "D", "DAI", "SL", "TDAI", "KL"],
            [VerifiedShapeBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "LP", "LT", "L", "SH"],
            [RectangularLapBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "LT", "L", "SH"],
            [SummaryBlockName] = ["TONG", "CO2", "CO1", "TRAN2", "TRAN1", "TUONG2", "TUONG1", "DAY2", "DAY1"],
            [LiftingHookBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "SH", "L2T", "L1T", "L3", "L1P", "L2P"],
            [TwoHooksBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "LP", "LT", "L", "SH"],
            [OneHookBlockName] = ["SH", "L", "LT", "D", "DAI", "SL", "TDAI", "KL"],
            [StraightBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "L", "SH"],
            [ZTieBlockName] = ["KL", "TDAI", "SL", "DAI", "D", "LP", "LT", "L", "SH"],
            [UShapeBlockName] = ["L2P", "L1P", "L3", "L1T", "L2T", "SH", "D", "DAI", "SL", "TDAI", "KL"],
            [AnchorBlockName] = ["SH", "L", "D", "DAI", "SL", "TDAI", "KL"],
            [AngleBlockName] = ["D", "KL", "TDAI", "SL", "DAI", "SH", "L"],
            [PanelMesh1BlockName] = ["SH", "L", "LT", "LP", "D", "DAI", "SL", "TDAI", "KL"],
            [PanelMesh2BlockName] = ["SH", "L", "LT", "LP", "D", "DAI", "SL", "TDAI", "KL"],
            [PanelSummaryBlockName] = ["NAP2", "NAP1", "THEPHINH", "TONG"],
            [CatchMeshNBlockName] = ["SH", "L", "LT", "D", "DAI", "SL", "TDAI", "KL"],
            [CatchMeshTBlockName] = ["SH", "L", "LT", "D", "DAI", "SL", "TDAI", "KL"],
            [CatchSummaryBlockName] = ["CUATHU1", "CUATHU2"]
        };

    public static string FormatApproved(double value, string role = "DAI")
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return role switch
        {
            "DAI" => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture),
            "TDAI" or "KL" => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture),
            _ => Format(value)
        };
    }

    public static IReadOnlyList<BlockAttributeIntent> BindScheduleRow(
        string blockName,
        string mark,
        double diameterMm,
        double singleLengthMm,
        double quantity,
        double totalLengthMm,
        double massKg,
        IReadOnlyDictionary<string, double>? roleLegs = null)
    {
        if (!ExactBlockSchemas.TryGetValue(blockName, out var tags))
            throw new ArgumentException($"Unknown schedule block schema: {blockName}", nameof(blockName));

        var result = new List<BlockAttributeIntent>(tags.Count);
        double totalLengthMetres = totalLengthMm / 1000.0;

        foreach (var tag in tags)
        {
            string val = tag switch
            {
                "SH" => mark,
                "D" => blockName == AngleBlockName ? Format(diameterMm) + "kg/m" : Format(diameterMm),
                "DAI" => Format(singleLengthMm),
                "SL" => Format(quantity),
                "TDAI" => Format(totalLengthMetres),
                "KL" => Format(massKg),
                "L" => roleLegs != null && roleLegs.TryGetValue("L", out var lVal) ? Format(lVal) : Format(singleLengthMm),
                "LT" => roleLegs != null && roleLegs.TryGetValue("LT", out var ltVal) ? Format(ltVal) : "0",
                "LP" => roleLegs != null && roleLegs.TryGetValue("LP", out var lpVal) ? Format(lpVal) : "0",
                "L1T" => roleLegs != null && roleLegs.TryGetValue("L1T", out var l1tVal) ? Format(l1tVal) : "0",
                "L2T" => roleLegs != null && roleLegs.TryGetValue("L2T", out var l2tVal) ? Format(l2tVal) : "0",
                "L3" => roleLegs != null && roleLegs.TryGetValue("L3", out var l3Val) ? Format(l3Val) : "0",
                "L1P" => roleLegs != null && roleLegs.TryGetValue("L1P", out var l1pVal) ? Format(l1pVal) : "0",
                "L2P" => roleLegs != null && roleLegs.TryGetValue("L2P", out var l2pVal) ? Format(l2pVal) : "0",
                _ => roleLegs != null && roleLegs.TryGetValue(tag, out var custVal) ? Format(custVal) : ""
            };
            result.Add(new BlockAttributeIntent(tag, val));
        }

        return result;
    }

    public static IReadOnlyList<BlockAttributeIntent> PanelSummary(double nap1, double nap2, double thepHinh, double tong) =>
        [new("NAP2", Format(nap2)), new("NAP1", Format(nap1)), new("THEPHINH", Format(thepHinh)), new("TONG", Format(tong))];

    public static IReadOnlyList<BlockAttributeIntent> CatchSummary(double cuaThu1, double cuaThu2) =>
        [new("CUATHU1", Format(cuaThu1)), new("CUATHU2", Format(cuaThu2))];

    public static IReadOnlyList<BlockAttributeIntent> GroupTitle(string tenLuoi) =>
        [new("TENLUOI", tenLuoi)];

    public static IReadOnlyList<BlockAttributeIntent> Header() => [];
}
