using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CircularCoverContractTests
{
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture, "circle-contract", new string('c',64), null,null,null,"explicit circle and support fixture");

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void CircularApertureRetainsItsShapeThroughCompatibilityCheck(double angle)
    {
        var profile = new CircleProfile(new(0,0),325);
        var rule = new CoverOpeningContract(profile,125,[Source]);
        var entry = new CoverCatalogEntry("ROUND-FIXTURE",new(900,900),new(0,0,0),angle,
            CoverPlacementDatum.NeckTop,new(-450,-450,450,450),[Source],rule);
        var result = CoverAssemblyValidator.Validate(entry,new(-325,-325,325,325),150,.001,Source,Source);
        Assert.True(result.Success);
        var circle = Assert.IsType<CircleProfile>(result.Value!.ClearProfile);
        Assert.Equal(325,circle.RadiusMm);
        Assert.Equal(new Point2Mm(0,0),circle.Center);
    }
}
