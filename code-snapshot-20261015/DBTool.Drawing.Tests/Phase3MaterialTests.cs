using DBTool.Drawing;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class Phase3MaterialTests
{
    [Theory]
    [InlineData("Concrete", "ANSI32", 120)]
    [InlineData("Bedding", "AR-CONC", 12)]
    public void SectionPatternsHaveLogicalModelScalesAndNativeMaterialRole(string material, string pattern, double scale)
    {
        var style = SectionMaterialPolicy.Resolve(material)!;
        Assert.Equal(pattern, style.Pattern);
        Assert.Equal(scale, style.Scale);
        Assert.Equal("VHG-VL", SemanticRoleRegistry.Resolve(style.Role)!.Layer);
        Assert.Equal(style, SectionMaterialPolicy.ForRole(style.Role));
        if (material == "Concrete") Assert.Null(SectionMaterialPolicy.Resolve(material, steelSection: true));
        else Assert.Equal(style, SectionMaterialPolicy.Resolve(material, steelSection: true));
    }

    [Fact]
    public void UnknownPhysicalMaterialIsNotGuessedAsConcrete()
    {
        Assert.Null(SectionMaterialPolicy.Resolve("BeddingMortar"));
        Assert.Null(SectionMaterialPolicy.Resolve("Steel"));
        Assert.Null(SectionMaterialPolicy.ForRole("Unknown"));
    }

    [Fact]
    public void CurvedOccurrenceBlocksRatherThanDroppingAnActiveStructuralBar()
    {
        var arc = new ArcSegment3Mm(new(0, 0, 100), 50, new(1, 0, 0), new(0, 1, 0), Math.PI / 2);
        var bar = new BarOccurrence("curved", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X, 10,
            new BarPath([arc], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            50 * Math.PI / 2, 50 * Math.PI / 2, LengthStatus.Known, "TEST", [], RebarDirection.Vertical);
        var result = StructuralRebarProjector.Build(new RebarLayout([bar], [], CalculationScope.FullRebar),
            new Dictionary<string, string> { [bar.Id] = "D001" });
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "STRUCTURAL_REBAR_PROJECTION_UNSUPPORTED" &&
            d.Severity == DiagnosticSeverity.Block && d.Actual == bar.Id);
    }
}
