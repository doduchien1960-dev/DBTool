using Xunit;

namespace DBTool.Drawing.Tests;

public class Phase2APresentationTests
{
    [Fact]
    public void SheetOneDotUsesNativeGraphicSizeWithoutChangingPhysicalRadius()
    {
        var dot=new CirclePrimitive("bar","Rebar/Main","SteelAA","Wall/T1","physical-bar","T1",new(0,0),6);
        Assert.Equal(.09,VhgPresentationStandards.RebarDotInsertionScale(dot.ViewId,dot.RadiusMm));
        Assert.Equal(6,dot.RadiusMm);
        Assert.Equal(.09,VhgPresentationStandards.RebarDotInsertionScale("WallMeshPlan",5));
        Assert.Equal(.12,VhgPresentationStandards.RebarDotInsertionScale("BaseTop",6));
    }

    [Fact]
    public void NativeA3AndDimensionMetricsUseOnePlotConversion()
    {
        var profile = new PresentationProfile(420, 297, 10, 10, 10, 10, 0, 10, [30], 30, [2], "VHG");
        Assert.Equal(12600, profile.ModelWidthMm);
        Assert.Equal(8910, profile.ModelHeightMm);
        Assert.Equal(1, VhgPresentationStandards.ModelGeometryScale);
        Assert.Equal(60, VhgPresentationStandards.PaperToModel(VhgPresentationStandards.DimensionPaperTextHeightMm));
        Assert.Equal(0.3, VhgPresentationStandards.FrameInsertionScale);
    }

    [Theory]
    [InlineData("Rebar/Tie", "VHG-Thep3")]
    [InlineData("Geometry/Hatch", "VHG-VL")]
    [InlineData("Dimension/Linear", "VHG-Dim")]
    [InlineData("Rebar/Bottom", "VHG-Thep")]
    [InlineData("Geometry/PipeOuter", "VHG-BT")]
    [InlineData("Dimension", "VHG-Text")]
    [InlineData("Rebar/Mark", "VHG-Text")]
    public void SemanticRolesResolveNativeLayers(string role, string layer) =>
        Assert.Equal(layer, SemanticRoleRegistry.Resolve(role)!.Layer);

    [Fact]
    public void NativeTextAndDimensionStylesAreExplicitAndUnknownRolesAreUnresolved()
    {
        Assert.Equal("VHG-Unicode.Regular", SemanticRoleRegistry.Resolve("Schedule/Row")!.TextStyle);
        Assert.Equal("VHG-Unicode.Bold", SemanticRoleRegistry.Resolve("ViewTitle")!.TextStyle);
        Assert.Equal("VHG1-30", SemanticRoleRegistry.Resolve("Dimension/Linear")!.DimStyle);
        Assert.Null(SemanticRoleRegistry.Resolve("unknown"));
    }
}
