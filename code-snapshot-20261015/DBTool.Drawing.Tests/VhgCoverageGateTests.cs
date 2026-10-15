using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class VhgCoverageGateTests
{
    [Fact]
    public void CrossWithoutTieOccurrenceCannotPassFinalGate()
    {
        var package=Package(new BlockPrimitive("orphan","Rebar/SheetTwoTie","BaseTop","Base","Block:BLTC",new(0,0),.3,[]));
        var result=DrawingPackageValidator.ValidateModelCoverage([],package);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="DRAWING_TIE_ASSOCIATION_INVALID");
    }

    [Fact]
    public void DowelWithoutMatchingHoleCannotPassFinalGate()
    {
        var path=new BarPath([new LineSegment3Mm(new(0,0,1500),new(0,0,1000))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var dowel=new BarOccurrence("dowel",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,18,path,500,500,
            LengthStatus.Known,"joint-rule",[],familyId:"Dowel");
        var result=DrawingPackageValidator.ValidateModelCoverage([dowel],Package());
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="DRAWING_DOWEL_HOLE_MISSING");
    }

    [Theory]
    [InlineData(25)]
    [InlineData(32)]
    public void HoleConsumesActualDowelStationAndConfiguredDiameter(double diameter)
    {
        var path=new BarPath([new LineSegment3Mm(new(123,-456,1500),new(123,-456,1087))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var dowel=new BarOccurrence("dowel",null,"Wall/T1",ComponentKind.Wall,Layer.Bottom,Axis.X,18,path,413,413,
            LengthStatus.Known,"joint-rule",[],familyId:"Dowel");
        var result=RoofJointDowelProjection.Build([dowel],new Dictionary<string,string>{{"dowel","D01"}},diameter);
        Assert.True(result.Success);
        var hole=Assert.Single(result.Value!.OfType<CirclePrimitive>());
        Assert.Equal(new Point2Mm(123,-456),hole.Center);Assert.Equal(diameter/2,hole.RadiusMm);
        Assert.Equal("dowel",hole.OccurrenceId);Assert.Equal("D01",hole.ScheduleMark);
    }

    private static DrawingPackage Package(params DrawingPrimitive[] primitives)=>new(new string('a',64),"test",
        [new ViewDefinition("BaseTop",ViewPurpose.BaseMesh,new(0,0),new(-500,-500,500,500),30)],primitives,
        new(-500,-500,500,500),CalculationScope.FullRebar);
}
