using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public static class Vhg32Schema
{
    public const string DbToolInputVersion="DBTool-Input-1A-v1";
    public static readonly IReadOnlyDictionary<string,string> DbToolConfigAddresses=new Dictionary<string,string>
    { ["PanelSupportMode"]="Z13" };
    // Explicit physical input extension. Legacy CF.Data cover names are not dimensions.
    public static readonly IReadOnlyDictionary<string,string> CoverCatalogColumns=new Dictionary<string,string>
    {
        ["Code"]="A",["WidthMm"]="B",["LengthMm"]="C",["OffsetX"]="D",["OffsetY"]="E",["OffsetZ"]="F",
        ["RotationDegrees"]="G",["PlacementDatum"]="H",["Units"]="I",["MinX"]="J",["MinY"]="K",["MaxX"]="L",["MaxY"]="M",
        ["ClearMinX"]="N",["ClearMinY"]="O",["ClearMaxX"]="P",["ClearMaxY"]="Q",["MinimumBearingMm"]="R"
    };
    /// <summary>Only confirmed S2 geometry cells. F11/G11 and F13/G13 retain their raw meanings pending U-NECK.</summary>
    public static IReadOnlyDictionary<string,string> S2GeometryConfigAddresses(Profile profile)
    {
        var all=ConfigAddresses(profile);
        var names=profile==Profile.GaThamLienKhoi
            ? new[]{"BaseHaunch","TopHaunch","NeckStructure","NeckPosition","NeckSize","CoverType","CoverHeight","PanelCount","PanelGap","PanelDirection"}
            : new[]{"BaseHaunch","NeckStructure","NeckPosition","NeckSize","CoverType","CoverHeight","PanelCount","PanelGap","PanelDirection"};
        return names.ToDictionary(name=>name,name=>all[name],StringComparer.Ordinal);
    }
    public static readonly IReadOnlyDictionary<string,string> PipeCatalogColumns = new Dictionary<string,string>
    {
        ["RoundDiameter"]="B", ["RoundThickness"]="C", ["BoxWidth"]="D", ["BoxHeight"]="E",
        ["BoxThickness"]="F", ["BoxChamfer"]="G", ["DoubleFlag"]="H"
    };

    public static readonly IReadOnlyDictionary<string,string> DataCatalogColumns = new Dictionary<string,string>
    {
        ["AngleCode"]="B", ["KgPerMetre"]="C", ["HatchName"]="D", ["HatchScale"]="E"
    };

    public static readonly IReadOnlyDictionary<string, string> MainColumns = new Dictionary<string, string>
    {
        ["Name"]="B", ["Length"]="C", ["Width"]="D", ["Height"]="E", ["BaseThickness"]="F",
        ["BaseBottomMesh"]="G", ["BaseTopMesh"]="H", ["WallThickness"]="I", ["WallOuterMesh"]="J",
        ["WallInnerMesh"]="K", ["RoofThickness"]="L", ["RoofTopMesh"]="M", ["RoofBottomMesh"]="N",
        ["PipeCodeT1"]="O", ["InvertT1"]="P", ["PipeCodeT2"]="Q", ["InvertT2"]="R",
        ["PipeCodeT3"]="S", ["InvertT3"]="T", ["PipeCodeT4"]="U", ["InvertT4"]="V",
        ["NeckHeight"]="W", ["NeckThickness"]="X", ["NeckOuterMesh"]="Y", ["NeckInnerMesh"]="Z",
        ["BaseElevationM"]="AA"
    };

    public static IReadOnlyDictionary<string, string> ConfigAddresses(Profile profile)
    {
        int shift = profile == Profile.GaThamThuNapRoi ? 2 : 0;
        var map = new Dictionary<string, string>
        {
            ["PadExpansion"]="C3", ["PadThickness"]="D3", ["BaseExpansion"]="C5", ["BaseHaunch"]="D5",
            ["WallSpacingDelta"]="C7", ["WallDiaStepDown"]="D7", ["WallDimPrefix"]="E7",
            ["NeckStructure"]="C11", ["NeckPosition"]="D11", ["NeckSize"]="E11", ["NeckHaunchFlag"]="F11",
            ["NeckHaunchSize"]="G11", ["AngleCode"]="H11", ["AnglePosition"]="I11", ["Anchor"]="J11",
            ["NeckPrefix"]="K11", ["NeckSpacingDelta"]="L11", ["NeckDiaStepDown"]="M11",
            ["CoverType"]="C13", ["CoverHeight"]="D13", ["CoverBlock"]="E13", ["CoverHaunchFlag"]="F13",
            ["CoverHaunchSize"]="G13", ["PanelCount"]="I13", ["PanelGap"]="J13", ["PanelDirection"]="K13",
            ["CoverBottom"]=$"C{17+shift}", ["CoverWall"]=$"D{17+shift}", ["CoverTop"]=$"E{17+shift}",
            ["CoverCatch"]=$"I{17+shift}", ["Distribution"]=$"K{19+shift}", ["HookMode"]=$"L{19+shift}",
            ["HookMultiple"]=$"L{21+shift}", ["PipeMortar"]=$"C{28+shift}",
            ["PipeOffsetT1"]=$"I{28+shift}", ["PipeOffsetT2"]=$"J{28+shift}",
            ["PipeOffsetT3"]=$"K{28+shift}", ["PipeOffsetT4"]=$"L{28+shift}",
            ["Annotation.PitchSymbol"]=$"M{19+shift}",
            ["Annotation.NoteStyle"]=$"K{21+shift}"
        };
        for (char c = 'C'; c <= 'J'; c++)
            map[$"Annotation.Prefix.{c}"] = $"{c}{21+shift}";

        if (profile == Profile.GaThamLienKhoi) map["TopHaunch"] = "C9";
        else
        {
            string[] names = {"HasDowel","DowelSpec","DowelLength","DowelHoleDia","BeddingMortar","LiftingSpec","LiftingSize"};
            for (int i=0;i<names.Length;i++) map[names[i]] = $"{(char)('C'+i)}9";
            // NR-IO-002: F-206 Material.G vữa đệm CF.Ga!G34 loại 2, !G36 loại 3
            map["Material.G"] = $"G{34+shift}";
        }
        string[] panel = {"BottomLayerBarDiameter","TopLayerBarDiameter","BarSpacingMm","TieRebar","TieShape",
            "AngleSteelCode","AngleSteelQuantity","AngleAnchorRebar","LiftingHookSpec","LiftingHookSize","CoverRatioPercent"};
        for (int i=0;i<panel.Length;i++) map["Panel."+panel[i]] = $"{(char)('C'+i)}15";
        // Semantic rename for Panel scale while preserving alias
        map["Panel.DetailScaleDenominator"] = "M15";

        for (char c='C';c<='I';c++) map[$"Protect.{c}"] = $"{c}{17+shift}";
        if (profile == Profile.GaThamThuNapRoi)
            for (int i=0;i<11;i++) map[$"Catch.{(char)('C'+i)}"] = $"{(char)('C'+i)}17";
        foreach (var (prefix, row, first, last) in new[]
        {
            ("HookPattern",20,'C','J'), ("Ties",23,'C','L'), ("Ladder",25,'C','G'),
            ("Pipe",28,'C','L'), ("RoofRebar",30,'C','G'), ("Elevation",32,'C','I'),
            ("Material",34,'C','F'), ("Hatch",36,'C','M')
        })
            for (char c=first;c<=last;c++) map[$"{prefix}.{c}"] = $"{c}{row+shift}";
        return map;
    }
}
