using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class HaunchProfileBuilderTests
{
    private static readonly CalculationPolicy Policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
    private static ValidatedManholeSpec Spec()
    {
        var seed=SyntheticTestFactory.Create();
        return new ValidatedManholeSpec(seed.Input,seed.Catalogs,Policy.Version,seed.Sources,[]);
    }
    private static HaunchSpec Haunch(double width,double height,HaunchOwner owner=HaunchOwner.Base) =>
        new(width,height,owner,SyntheticTestFactory.Create().Sources);

    [Theory]
    [InlineData(100,100,21166666.666666668)]
    [InlineData(100,200,42333333.333333336)]
    public void TaperedCornerUnionHasHandIntegratedVolume(double width,double height,double expected)
    {
        var result=HaunchProfileBuilder.Build(Spec(),Haunch(width,height),Policy);
        Assert.True(result.Success);
        Assert.Equal(expected,result.Value!.VolumeMm3,6);
        Assert.Single(result.Value.Components);
        Assert.Equal((800d,1050d),result.Value.Sections[0].ClearSize);
        Assert.Equal((1000d,1250d),result.Value.Sections[1].ClearSize);
        Assert.All(result.Value.FacePatches,p=>
        {
            Assert.Equal(p.Frame.N,p.Normal);
            Assert.IsType<PolygonProfile>(p.Boundary);
        });
    }

    [Fact]
    public void TriangleAreaAndInclinedEdgeUseActualProfile()
    {
        var triangle=new Polygon2Mm([new(0,0),new(100,0),new(0,100)]);
        Assert.Equal(5000,Math.Abs(triangle.SignedArea),6);
        var result=HaunchProfileBuilder.Build(Spec(),Haunch(100,100),Policy).Value!;
        Assert.Equal(5000,result.TriangleAreaMm2,6);
        Assert.Equal(141.4213562373095,result.InclinedEdgeMm,9);
    }

    [Fact]
    public void ZeroHaunchAddsNoSolidOrFace()
    {
        var result=HaunchProfileBuilder.Build(Spec(),Haunch(0,0),Policy).Value!;
        Assert.Empty(result.Components);
        Assert.Empty(result.FacePatches);
        Assert.Equal(0,result.VolumeMm3);
    }

    [Fact]
    public void RoofMirrorChangesZAndOwnerWithoutChangingVolume()
    {
        var baseResult=HaunchProfileBuilder.Build(Spec(),Haunch(100,100),Policy).Value!;
        var roofResult=HaunchProfileBuilder.Build(Spec(),Haunch(100,100,HaunchOwner.Roof),Policy).Value!;
        Assert.Equal(baseResult.VolumeMm3,roofResult.VolumeMm3);
        Assert.Equal(new Interval1D(1300,1400),roofResult.Components[0].ZRange);
        Assert.Equal(ComponentKind.Roof,roofResult.Components[0].Kind);
    }

    [Fact]
    public void ExcessWidthFailsWithoutPayload()
    {
        var result=HaunchProfileBuilder.Build(Spec(),Haunch(501,501),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="HAUNCH_WIDTH_INVALID" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void HalfClearWidthIsValidBoundaryWithoutDegenerateHole()
    {
        var result=HaunchProfileBuilder.Build(Spec(),Haunch(500,100),Policy);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.Empty(result.Value!.Components[0].Footprint.InnerLoops);
    }
}
