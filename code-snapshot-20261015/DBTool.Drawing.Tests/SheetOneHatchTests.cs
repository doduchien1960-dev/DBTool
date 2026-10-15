using Xunit;
namespace DBTool.Drawing.Tests;
public class SheetOneHatchTests
{
    [Fact]
    public void CoordinateBoundaryIntentRoundTripsAndLegacyDefaultsToOwnedEntities()
    {
        var hatch = new HatchPrimitive("h", "Material/Concrete", "AA", "Base",
            [new(0,0), new(100,0), new(100,100), new(0,100)], [], DrawingMaterialKind.Concrete);
        var bounds = new DBTool.Domain.Bounds2Mm(0,0,100,100);
        var original = new DrawingPackage(new string('a',64), "test",
            [new("AA",ViewPurpose.Section,new(0,0),bounds,30)], [hatch], bounds, DBTool.Domain.CalculationScope.FullRebar);
        var parsed = System.Text.Json.Nodes.JsonNode.Parse(DrawingPackageJson.Serialize(original))!;
        parsed["Primitives"]![0]!["NativeBoundaryLoops"] = true;
        var json = parsed.ToJsonString();
        var package = DrawingPackageJson.Deserialize(json);
        var written = System.Text.Json.Nodes.JsonNode.Parse(DrawingPackageJson.Serialize(package))!;
        Assert.True(written["Primitives"]![0]!["NativeBoundaryLoops"]?.GetValue<bool>() ?? false);
        parsed["Primitives"]![0]!.AsObject().Remove("NativeBoundaryLoops");
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(DrawingPackageJson.Serialize(DrawingPackageJson.Deserialize(parsed.ToJsonString())))!;
        Assert.False(legacy["Primitives"]![0]!["NativeBoundaryLoops"]?.GetValue<bool>() ?? false);
    }

    [Fact]
    public void ExplicitPatternOriginRoundTripsAndLegacyJsonKeepsDefaultOrigin()
    {
        var origin=new DBTool.Domain.Geometry.Point2Mm(901.49,71006.52);
        var hatch=new HatchPrimitive("h","Material/Concrete","AA","Base",
            [new(0,0),new(100,0),new(100,100),new(0,100)],[],DrawingMaterialKind.Concrete,origin);
        var bounds=new DBTool.Domain.Bounds2Mm(0,0,100,100);
        var package=new DrawingPackage(new string('a',64),"test",
            [new("AA",ViewPurpose.Section,new(0,0),bounds,30)],[hatch],bounds,DBTool.Domain.CalculationScope.FullRebar);
        var json=DrawingPackageJson.Serialize(package);
        Assert.Equal(origin,Assert.IsType<HatchPrimitive>(Assert.Single(DrawingPackageJson.Deserialize(json).Primitives)).PatternOrigin);
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(json)!;
        legacy["Primitives"]![0]!.AsObject().Remove("PatternOrigin");
        Assert.Null(Assert.IsType<HatchPrimitive>(Assert.Single(DrawingPackageJson.Deserialize(legacy.ToJsonString()).Primitives)).PatternOrigin);
    }

    [Fact]
    public void StructuralConcreteUsesBothSourcePatternsWhileSteelSectionKeepsOnlyBeddingAndMortar()
    {
        var concrete=SectionMaterialPolicy.ForSheetOneMaterial("Concrete");
        Assert.Contains(concrete,h=>h.Pattern=="ANSI32" && h.Scale==120);
        Assert.Contains(concrete,h=>h.Pattern=="AR-CONC" && h.Scale==12);
        Assert.Empty(SectionMaterialPolicy.ForSheetOneMaterial("Concrete",true));
        var mortar=Assert.Single(SectionMaterialPolicy.ForSheetOneMaterial("BeddingMortar",true));
        Assert.Equal("ANSI37",mortar.Pattern);
        Assert.Equal(120,mortar.Scale);
        Assert.Equal("VHG-VL",SemanticRoleRegistry.Resolve(mortar.Role)!.Layer);
        Assert.Equal("AR-CONC",Assert.Single(SectionMaterialPolicy.ForSheetOneMaterial("Bedding",true)).Pattern);
    }
}
