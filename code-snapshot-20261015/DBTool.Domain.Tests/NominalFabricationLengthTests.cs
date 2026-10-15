using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Domain.Tests;

public sealed class NominalFabricationLengthTests
{
    [Theory]
    [InlineData(10, false, false, 20)]
    [InlineData(18, false, false, 45)]
    [InlineData(18, true, false, 22.5)]
    [InlineData(18, false, true, 36)]
    public void OwnerFallbackUsesFamilyAndDiameter(double d,bool smooth,bool lifting,double expected) =>
        Assert.Equal(expected,NominalFabricationLength.InnerRadius(d,smooth,lifting));

    [Fact]
    public void LadderFabricationIsSeparateFromNominalLegs()
    {
        Point3Mm[] points=[new(0,0,150),new(0,0,0),new(0,250,0),new(300,250,0),new(300,0,0),new(300,0,150)];
        var path=new BarPath(points.Zip(points.Skip(1),(a,b)=>(PathSegment)new LineSegment3Mm(a,b)),false,
            HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
        Assert.Equal(1100-4*(63-Math.PI*31.5/2),NominalFabricationLength.Calculate(path,18,true)!.Value,6);
        Assert.Equal(150,path.Segments[0].Start.Z);
        Assert.Equal(300,path.Segments[2].End.X);
    }

    [Fact]
    public void InsufficientTangentLengthRemainsUnresolved()
    {
        var path=new BarPath([new LineSegment3Mm(new(0,0,0),new(10,0,0)),new LineSegment3Mm(new(10,0,0),new(10,100,0))],
            false,HookPattern.Hook1M,0,false,MeasurementBasis.NominalPolyline);
        Assert.Null(NominalFabricationLength.Calculate(path,10));
        Assert.Throws<ArgumentException>(()=>BendLengthCalculator.Calculate(path,0));
    }
}
