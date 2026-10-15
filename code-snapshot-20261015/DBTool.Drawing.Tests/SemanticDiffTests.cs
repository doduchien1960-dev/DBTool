using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SemanticDiffTests
{
    private static SemanticEntity Entity(string id, string type, string layer = "steel", string role = "GC.Round", string tag = "TONG", double x = 1000, string handle = "1") =>
        new(id, type, "Base", null, new Dictionary<string,string> { ["Layer"] = layer, ["BlockRole"] = role, ["ATT."+tag] = "46.062" }, new Dictionary<string,double> { ["End.X"] = x }, handle);
    private static SemanticSnapshot Fixture() => new("drawing-copy", "generation-a", [Entity("l1","Line"),Entity("l2","Line"),Entity("a1","Arc"),Entity("b1","BlockReference")]);
    private static SemanticSnapshot Actual(IEnumerable<SemanticEntity> entities) => new("drawing-copy", "generation-b", entities);

    [Fact] public void RemovedLineIsExactlyOneMissing() => Assert.Equal("l1", Assert.Single(SemanticDiff.CompareArchived(Fixture(), Actual(Fixture().Entities.Skip(1)), new()).Missing).SemanticId);

    [Theory]
    [InlineData("endpoint")]
    [InlineData("layer")]
    [InlineData("role")]
    [InlineData("tag")]
    [InlineData("dimension")]
    public void SingleIndependentMutationIsExactlyOneChangedEntity(string mutation)
    {
        var actual = Fixture().Entities.ToArray();
        actual[0] = mutation switch
        {
            "endpoint" => Entity("l1","Line",x:1000.01),
            "layer" => Entity("l1","Line",layer:"wrong"),
            "role" => Entity("l1","Line",role:"GC.Rectangle"),
            "tag" => Entity("l1","Line",tag:"TongKL"),
            _ => Entity("l1","Line",x:999)
        };
        var diff = SemanticDiff.CompareArchived(Fixture(), Actual(actual), new());
        Assert.Empty(diff.Missing); Assert.Empty(diff.Unexpected);
        Assert.Equal("l1", Assert.Single(diff.Changed).SemanticId);
        Assert.False(diff.NoDifference);
    }

    [Fact] public void EntityOrderAndAllocatedHandlesDoNotChangeSemantics()
    {
        var actual = Fixture().Entities.Reverse().Select(e => Entity(e.SemanticId,e.Type,handle:"regenerated"));
        Assert.True(SemanticDiff.CompareArchived(Fixture(), Actual(actual), new()).NoDifference);
    }

    [Fact] public void RawMassToleranceAndRenderedTextAreIndependent()
    {
        var expected = new SemanticEntity("summary","BlockReference","Steel",null,new Dictionary<string,string> { ["ATT.TONG"]="46.062" },new Dictionary<string,double> { ["MassKg"]=46.06172839506173 });
        var actual = new SemanticEntity("summary","BlockReference","Steel",null,new Dictionary<string,string> { ["ATT.TONG"]="46.061" },new Dictionary<string,double> { ["MassKg"]=46.06172839506173+1e-10 });
        var diff = SemanticDiff.CompareArchived(Actual([expected]),Actual([actual]),new());
        Assert.Equal("ATT.TONG",Assert.Single(Assert.Single(diff.Changed).Fields).Field);
    }

    [Fact] public void DimensionMeasurement1000Versus999ChangesExactlyOne()
    {
        SemanticEntity Dimension(double measurement)=>new("dim","RotatedDimension","Wall/T1",null,new Dictionary<string,string> { ["Style"]="ISO" },new Dictionary<string,double> { ["MeasurementMm"]=measurement });
        var diff=SemanticDiff.CompareArchived(Actual([Dimension(1000)]),Actual([Dimension(999)]),new());
        Assert.Equal("MeasurementMm",Assert.Single(Assert.Single(diff.Changed).Fields).Field);
    }

    [Fact] public void SnapshotJsonRoundtripPreservesSemanticComparison()
    {
        var json=System.Text.Json.JsonSerializer.Serialize(Fixture());
        var restored=System.Text.Json.JsonSerializer.Deserialize<SemanticSnapshot>(json)!;
        Assert.True(SemanticDiff.CompareArchived(Fixture(),restored,new()).NoDifference);
    }
}

