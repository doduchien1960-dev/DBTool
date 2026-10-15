using System.Collections.Immutable;

namespace DBTool.Application.Templates;

public static class VhgTemplateRoles
{
    public static ImmutableArray<TemplateRequirement> Presentation { get; } =
    [
        new("SteelSummary", AssetKind.Block, ["TONG"]),
        new("PanelSummary", AssetKind.Block, ["TONG"]),
        new("Block:Khung", AssetKind.Block, [], new Dictionary<string, string> { ["NativeWidthMm"] = "42000", ["NativeHeightMm"] = "29700" }),
        Layer("VHG-Thep3", "6", "DASHED"), Layer("VHG-Thep4", "8", "DASHED"),
        Layer("VHG-BT", "4", "Continuous"), Layer("VHG-VL", "251", "Continuous"),
        Layer("VHG-Dim", "140", "Continuous"), Layer("VHG-Text", "7", "Continuous"),
        Layer("VHG-Thep", "1", "Continuous"), Layer("VHG-Thep2", "2", "Continuous"),
        Layer("VHG-140", "140", "Continuous"), Layer("VHG-150", "150", "Continuous"),
        Layer("VHG-Nap2", "125", "Continuous"), Layer("VHG-Nap3", "251", "Continuous"), Layer("VHG-Nap4", "7", "Continuous"),
        new("TextStyle:VHG-Unicode.Regular", AssetKind.TextStyle, [],
            new Dictionary<string, string> { ["FileName"] = "viet buddy.shx", ["XScale"] = "0.8" }),
        new("TextStyle:VHG-Unicode.Bold", AssetKind.TextStyle, [],
            new Dictionary<string, string> { ["FileName"] = "arial.ttf", ["XScale"] = "0.8" }),
        new("DimStyle:VHG1-30", AssetKind.DimStyle, [], new Dictionary<string, string>
        {
            ["Dimscale"] = "300", ["Dimtxt"] = "0.2", ["Dimasz"] = "0.1", ["Dimgap"] = "0.05",
            ["Dimlfac"] = "1", ["Dimdec"] = "0", ["Dimtxsty"] = "VHG-Unicode.Regular",
            ["Dimclrd/ColorIndex"] = "140", ["Dimclre/ColorIndex"] = "140", ["Dimclrt/ColorIndex"] = "7"
        }),
        new("Linetype:DASHED", AssetKind.Linetype, [])
    ];

    public static ImmutableArray<TemplateRequirement> CatchBasinRequirements { get; } =
    [
        new("CatchSummary", AssetKind.Block, ["CUATHU1", "CUATHU2"]),
        Layer("VHG-HoThu", "3", "Continuous")
    ];

    public static ImmutableArray<TemplateRequirement> GeometryRequirements { get; } =
    [
        new("Geometry/DowelHole", AssetKind.Block, []),
        Layer("VHG-BT", "4", "Continuous"),
        Layer("VHG-VL", "251", "Continuous")
    ];

    private static TemplateRequirement Layer(string name, string color, string linetype) =>
        new($"Layer:{name}", AssetKind.Layer, [], new Dictionary<string, string>
        { ["ColorIndex"] = color, ["Linetype"] = linetype });
}
