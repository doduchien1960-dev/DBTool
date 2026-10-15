using DBTool.Domain.Tests.Fixtures;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CatchBasinBuilderTests
{
    [Theory]
    [InlineData(WallId.T1,HorizontalPosition.Left)]
    [InlineData(WallId.T1,HorizontalPosition.Right)]
    [InlineData(WallId.T2,HorizontalPosition.Left)]
    [InlineData(WallId.T2,HorizontalPosition.Right)]
    [InlineData(WallId.T3,HorizontalPosition.Left)]
    [InlineData(WallId.T3,HorizontalPosition.Right)]
    [InlineData(WallId.T4,HorizontalPosition.Left)]
    [InlineData(WallId.T4,HorizontalPosition.Right)]
    public void BodyEdgePositionUsesTheSameInverseWallFrame(WallId wall,HorizontalPosition position)
    {
        var seed=SyntheticTestFactory.Create();
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var spec=new ValidatedManholeSpec(seed.Input,seed.Catalogs,policy.Version,seed.Sources,[]);
        var model=ChamberGeometryBuilder.Build(spec,policy).Value!;
        var basin=new CatchBasinSpec(CatchBasinOwner.Body,wall,position,
            400,300,600,500,600,100,100,0,[]);
        var result=CatchBasinBuilder.Build(spec,model,basin,policy);
        Assert.True(result.Success);
        var frame=WallFrame.Create(wall,seed.Input.L,seed.Input.B);
        var corners=result.Value!.OpeningOwnedChildren.SelectMany(c=>c.Cells)
            .SelectMany(c=>new[]{
                frame.ToLocal(new Point3Mm(c.XY.MinX,c.XY.MinY,0)).U,
                frame.ToLocal(new Point3Mm(c.XY.MaxX,c.XY.MaxY,0)).U}).ToArray();
        double half=(wall is WallId.T1 or WallId.T3?seed.Input.L:seed.Input.B)/2;
        double expectedMin=position==HorizontalPosition.Left?-half:half-400;
        Assert.InRange(Math.Abs(corners.Min()-expectedMin),0,1e-6);
        Assert.InRange(Math.Abs(corners.Max()-(expectedMin+400)),0,1e-6);
    }

    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void OpeningHeightAndHostThicknessAreIndependentOfChamberDimensions(WallId wall)
    {
        var seed = SyntheticTestFactory.Create();
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, policy.Version, seed.Sources, []);
        var model = ChamberGeometryBuilder.Build(spec, policy).Value!;
        var basin = new CatchBasinSpec(CatchBasinOwner.Body, wall, HorizontalPosition.Center,
            400, 300, 600, 500, 600, 100, 100, 0, []);
        var result = CatchBasinBuilder.Build(spec, model, basin, policy);
        Assert.True(result.Success);
        Assert.Equal(new Interval1D(1100, 1400), result.Value!.OpeningZRange);
        Assert.Equal(800, result.Value.ChamberBottomZmm);
        Assert.Equal(150, result.Value.ChamberStartVMm);
        Assert.Equal(18_000_000, result.Value.OpeningOwnedChildren.Sum(c => c.VolumeMm3), 6);
        Assert.Equal(150_000, result.Value.OpeningRevealAreaMm2, 6);
        Assert.Equal(3,result.Value.OpeningRevealPatches.Length);
        Assert.Equal(result.Value.OpeningRevealAreaMm2,
            result.Value.OpeningRevealPatches.Sum(p=>p.AreaMm2.Value),6);
        Assert.DoesNotContain(result.Value.OpeningRevealPatches,p=>p.Id.EndsWith("/Top",StringComparison.Ordinal));
        Assert.All(result.Value.OpeningRevealPatches,p=>Assert.Equal(FaceRole.OpeningReveal,p.Role));
        Assert.Equal(138_000_000, result.Value.TotalOccupiedVolumeMm3, 6);
        Assert.All(result.Value.ExteriorFaces, face => Assert.True(face.AreaMm2 > 0));
        Assert.All(result.Value.ContactPatches, face => Assert.True(face.AreaMm2 > 0));
        Assert.All(result.Value.ExteriorFaces, face => Assert.NotNull(face.Patch));
        Assert.All(result.Value.ContactPatches, face =>
        {
            Assert.NotNull(face.Patch);
            Assert.Equal(FaceRole.Contact,face.Patch!.Role);
            Assert.Equal($"Wall/{wall}",face.Patch.AdjacentComponentId);
        });
        Assert.Equal(180_000,result.Value.ContactPatches.Sum(face=>face.AreaMm2),6);
        Assert.Equal(2_960_000,result.Value.ExteriorFaces.Sum(face=>face.AreaMm2),6);
        Assert.All(result.Value.ExteriorFaces,face=>
        {
            Assert.Equal(face.AreaMm2,face.Patch!.AreaMm2.Value,6);
            Assert.Null(face.Patch.AdjacentComponentId);
        });
    }

    [Fact]
    public void ChamberShorterThanOpeningBlocks()
    {
        var seed = SyntheticTestFactory.Create();
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, policy.Version, seed.Sources, []);
        var model = ChamberGeometryBuilder.Build(spec, policy).Value!;
        var basin = new CatchBasinSpec(CatchBasinOwner.Body, WallId.T1, HorizontalPosition.Center,
            400, 300, 600, 500, 200, 100, 100, 0, []);
        var result = CatchBasinBuilder.Build(spec, model, basin, policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void NeckOpeningUsesNeckWallAndTopElevation()
    {
        var seed = SyntheticTestFactory.Create();
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, policy.Version, seed.Sources, []);
        var outer = new Polygon2Mm([new(-450,-450),new(450,-450),new(450,450),new(-450,450)]);
        var inner = new Polygon2Mm([new(-350,350),new(350,350),new(350,-350),new(-350,-350)]);
        var footprint = new Footprint(outer,[inner]);
        var z = new Interval1D(1550,2150);
        var world = new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var neck = new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        var basin = new CatchBasinSpec(CatchBasinOwner.Neck,WallId.T1,HorizontalPosition.Center,
            400,250,600,500,600,100,100,0,[]);
        var result = CatchBasinBuilder.Build(spec,new GeometryModel([neck],[]),basin,policy);
        Assert.True(result.Success);
        Assert.Equal(new Interval1D(1900,2150),result.Value!.OpeningZRange);
        Assert.Equal(100,result.Value.ChamberStartVMm);
        Assert.Equal(10_000_000,result.Value.OpeningOwnedChildren.Sum(c=>c.VolumeMm3),6);
    }

    [Fact]
    public void RectangularUnionCountsSharedVolumeOnce()
    {
        var world = new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        ExtrusionSolid Box(double x0,double x1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,0),new(x1,0),
                new(x1,100),new(x0,100)]),[]);
            return new ExtrusionSolid(footprint,world,new Interval1D(0,100));
        }
        var union=new UnionSolid([Box(0,100),Box(50,150)]);
        var result=RectangularSolidUnion.Measure(union);
        Assert.True(result.Success);
        Assert.Equal(1_500_000,result.Value!.Value,6);
    }
}
