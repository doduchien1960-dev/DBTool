namespace DBTool.Drawing;

/// <summary>Paper metrics and native model-space insertion; geometry remains in millimetres at 1:1.</summary>
public static class VhgPresentationStandards
{
    public const double PlotDenominator = 30;
    public const double ModelGeometryScale = 1;
    public const double FrameInsertionScale = 0.3;
    public const double ModelWidthMm = 420 * PlotDenominator;
    public const double ModelHeightMm = 297 * PlotDenominator;
    public const double DimensionPaperTextHeightMm = 2;
    public const string RegularTextStyle = "VHG-Unicode.Regular";
    public const string BoldTextStyle = "VHG-Unicode.Bold";
    public const string DimensionStyle = "VHG1-30";
    // Published first-sheet steel dots are 9x9mm graphic symbols, independent of the physical bar radius.
    public static double RebarDotInsertionScale(string view,double physicalRadiusMm) =>
        (view is "WallMeshPlan" or "SteelAA" or "SteelBB") ? .09 : physicalRadiusMm/50;
    public static double PaperToModel(double paperMm) => PresentationProfile.ModelTextHeight(paperMm, PlotDenominator);
}

public sealed record SemanticCadStyle
{
    public SemanticCadStyle(string layer, string? textStyle = null, string? dimStyle = null)
    { Layer = layer; TextStyle = textStyle; DimStyle = dimStyle; }
    public string Layer { get; }
    public string? TextStyle { get; }
    public string? DimStyle { get; }
}

/// <summary>Explicit roles only. Unknown roles are blocked by the CAD adapter, never guessed from colour.</summary>
public static class SemanticRoleRegistry
{
    public static SemanticCadStyle? Resolve(string role) => role switch
    {
        "Rebar/Bottom" or "Rebar/Top" or "Rebar/Schematic" or "Rebar/Main" or "Rebar/Hoop" or SheetOneRebarDotPolicy.Role or SheetOneContinuousWallDots.Role => new("VHG-Thep"),
        "Rebar/Secondary" or "Accessory/AngleFrame" => new("VHG-Thep2"),
        "Rebar/Tie" or "Rebar/Hidden" or WallMeshBandPolicy.Role or "VHG-Thep3" => new("VHG-Thep3"),
        "Rebar/HiddenSecondary" or WallMeshGuidePolicy.Role or "VHG-Thep4" => new("VHG-Thep4"),
        "Geometry/Hatch" or "Material/Concrete" or "Material/Bedding" or "Material/Pipe" or SectionMaterialPolicy.SheetOnePipeRole or "Material/ConcreteAggregate" or "Material/Mortar" or SheetTwoMortar.Role => new("VHG-VL"),
        "Geometry/BaseOutline" or SheetTwoMaterialViews.BaseRole or SheetTwoMaterialViews.WallRole or SheetTwoMaterialViews.RoofRole or SheetOnePlanBaseOutlinePolicy.Role or SheetOnePlanOutlinePolicy.Role or SheetOneSectionOutlinePolicy.Role or "Geometry/Material" or "Geometry/InnerLoop" or
        "Geometry/RoofOpening" or "Geometry/ConcreteCutout" => new("VHG-BT"),
        "Geometry/PipePlanSilhouette" or "Geometry/PipeOuter" or "Geometry/PipeInner" => new("VHG-BT"),
        "Geometry/PlanPipeVisible" => new("VHG-Cong"),
        "Geometry/PlanPipeCenter" => new("VHG-Center"),
        "Geometry/PlanPipeHidden" => new("VHG-Dashed"),
        "Dimension/PipeDiameter" => new("VHG-150",dimStyle:VhgPresentationStandards.DimensionStyle),
        "Geometry/PurchasedCover" => new("VHG-Nap1"),
        "Geometry/LegacyCoverPlan" => new("VHG-VL"),
        "Geometry/LegacyCoverSection" => new("VHG-BT"),
        SheetTwoBarAnnotations.Role => new("VHG-Dim",textStyle:VhgPresentationStandards.RegularTextStyle,dimStyle:VhgPresentationStandards.DimensionStyle),
        SheetTwoGach.Role => new("VHG-Thep"),
        SheetTwoGachCollector.CollectorRole => new("VHG-Dim"),
        SheetTwoGach.AnchorageRole => new("VHG-Thep2"),
        SheetTwoNominalAnchorage.Role => new("VHG-Thep2"),
        SheetTwoRepresentativePaths.Role => new("VHG-Dim"),
        SheetTwoMortar.BoundaryRole => new("VHG-Dim"),
        SheetTwoTieSymbols.BaseRole => new("VHG-Thep"),
        SheetTwoNominalBaseMesh.Role => new("VHG-Thep"),
        SheetTwoNominalWallMesh.Role => new("VHG-Thep"),
        SheetTwoNominalWallMesh.EdgeRole => new("VHG-Thep3"),
        SheetTwoAdjacentSlabSteel.Role => new("VHG-Thep3"),
        SheetTwoTieSymbols.WallRole => new("VHG-Thep3"),
        SheetTwoDimensionPlanner.Role => new("VHG-Dim",textStyle:VhgPresentationStandards.RegularTextStyle,dimStyle:VhgPresentationStandards.DimensionStyle),
        "Dimension/RoofOpening" or "Dimension/BaseOpening" => new("VHG-BT", dimStyle: VhgPresentationStandards.DimensionStyle),
        "Dimension/RoofDowel" or "Dimension/BaseDowel" => new("VHG-Dashed", dimStyle: VhgPresentationStandards.DimensionStyle),
        "Dimension/Roof" or "Dimension/Neck" or "Dimension/Base" => new("VHG-Dim", dimStyle: VhgPresentationStandards.DimensionStyle),
        "Dimension/Gach" or "Schedule/BlockDim" or "RectangularOpeningReinforcement" => new("VHG-Dim"),
        "Schedule/BlockDashed" => new("VHG-Dashed"),
        "Schedule/BlockBT" => new("VHG-BT"),
        "Schedule/BlockText" or "SteelSummary" => new("VHG-Text", VhgPresentationStandards.RegularTextStyle),
        "Geometry/NeckBreak" => new("VHG-150"),
        "Note/RoofDowel" or "Detail/RoofText" => new("VHG-Text", VhgPresentationStandards.RegularTextStyle),
        "Detail/RoofBubble" => new("VHG-Dim"),
        "Frame" or "Dimension/Linear" or "Rebar/Leader" or "Material/Leader" or "Schedule/Grid" => new("VHG-Dim", dimStyle: VhgPresentationStandards.DimensionStyle),
        "ViewTitle" => new("VHG-Text", VhgPresentationStandards.BoldTextStyle),
        "Elevation/Native" => new("VHG-150", VhgPresentationStandards.RegularTextStyle),
        "Elevation/SheetOneFloor" => new("VHG-Text", VhgPresentationStandards.RegularTextStyle),
        "Elevation/SheetOneCap" => new("VHG-BT", VhgPresentationStandards.RegularTextStyle),
        "Section/CutMarker" => new("VHG-Dashed"),
        "Geometry/PlanHidden" or "Geometry/SectionHidden" or SheetTwoHiddenEdges.Role or SheetOnePlanHiddenPolicy.WallRole or SheetOnePlanHiddenPolicy.HoleRole => new("VHG-Dashed"),
        "Dimension" or "Rebar/Mark" or "Rebar/MarkRight" or "Rebar/BendingMark" or "Dimension/Bending" or "DimensionText" or "Elevation/Absolute" or "Elevation/Symbolic" or
        "Schedule/Row" or "Schedule/AngleRow" or "Schedule/TotalMass" or "Schedule/Header" or "Schedule/Group" or "Quantity/Row" or
        "Accessory/PurchasedCoverDatum" or "Accessory/Tag" => new("VHG-Text", VhgPresentationStandards.RegularTextStyle),
        "Schedule/NeckMark" => new("Defpoints", VhgPresentationStandards.RegularTextStyle),
        _ => null
    };
}
