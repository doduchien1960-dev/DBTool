using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class PipeOpeningFullTests
{
    [Fact]
    public void DoubleBoxUsesTotalInnerWidthAndKeepsMiddleWall()
    {
        var entry = new PipeCatalogEntry("DB1000", PipeKind.DoubleBox, null, 1000, 600, 50, 100, 50);
        var pipe = new PipeConnectionSpec(WallId.T1, entry.Code, 300, 0, 40, []);
        var result = PipeCrossSectionBuilder.BuildBox(entry, pipe, new DoubleBoxPolicy());
        Assert.True(result.Success);
        var section = result.Value!;
        Assert.Equal(2, section.InnerCells.Length);
        Assert.Equal(-275, section.InnerCells[0].Center.X, 6);
        Assert.Equal(275, section.InnerCells[1].Center.X, 6);
        Assert.All(section.InnerCells, c => Assert.Equal(265000, c.AreaMm2, 6));
        Assert.Equal(new Rectangle2Mm(-550, 250, 550, 950), section.PipeOuter.Rectangle);
        Assert.Equal(new Rectangle2Mm(-590, 210, 590, 990), section.ConcreteCutout.Rectangle);
        Assert.Equal(530000, section.InnerCells.Sum(c => c.AreaMm2), 6);
    }

    [Fact]
    public void MissingDoubleWallIsNeedsRuleRatherThanOuterWallFallback()
    {
        var entry = new PipeCatalogEntry("DB1000", PipeKind.DoubleBox, null, 1000, 600, 50, null, 50);
        var pipe = new PipeConnectionSpec(WallId.T1, entry.Code, 300, 0, 40, []);
        var result = PipeCrossSectionBuilder.BuildBox(entry, pipe, new DoubleBoxPolicy());
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "NEEDS_RULE_DOUBLE_WALL" && d.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void ApprovedExplicitMiddleWallBindingIsTraceableAndValidated()
    {
        var entry = new PipeCatalogEntry("DB1000", PipeKind.DoubleBox, null, 1000, 600, 50, null, 50);
        var source = SyntheticTestFactory.Create().Sources[0];
        var pipe = new PipeConnectionSpec(WallId.T1, entry.Code, 300, 0, 40, []);
        var result = PipeCrossSectionBuilder.BuildBox(entry, pipe, new DoubleBoxPolicy(100, source));
        Assert.True(result.Success);
        Assert.Contains(source, result.Value!.SourceRefs);
        var invalid = PipeCrossSectionBuilder.BuildBox(entry, pipe, new DoubleBoxPolicy(-100, source));
        Assert.False(invalid.Success);
        Assert.Null(invalid.Value);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "PIPE_DIMENSION_INVALID");
    }

    [Fact]
    public void BoxOpeningPreservesTwoInnerCellsAndWallFrame()
    {
        var seed = SyntheticTestFactory.Create();
        var entry = new PipeCatalogEntry("DB1000", PipeKind.DoubleBox, null, 1000, 600, 50, 100, 50);
        var pipe = new PipeConnectionSpec(WallId.T1, entry.Code, 300, 0, 40, []);
        var catalogs = new CatalogSet(seed.Catalogs.Pipes.Append(new KeyValuePair<string, PipeCatalogEntry>(entry.Code, entry)), [], [], []);
        var spec = new ValidatedManholeSpec(seed.Input, catalogs, "S2Geometry-1/v1", seed.Sources, []);
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var result = PipeOpeningBuilder.BuildBox(spec, pipe, catalogs, policy, new DoubleBoxPolicy());
        Assert.True(result.Success);
        var opening = result.Value!;
        Assert.Equal(OpeningKind.Box, opening.Kind);
        Assert.Equal(2, Assert.IsType<MultiCellProfile>(opening.Inner).Cells.Length);
        Assert.Equal(new Interval1D(210, 990), opening.ZRange);
        Assert.Equal(new Point3Mm(0, 625, 600), opening.WallFrame!.ToGlobal(0, 0, 600));
        Assert.Equal("Wall/T1", opening.OwnerId);
    }

    [Fact]
    public void RoundInvertZeroCanCrossBaseAndPartitionWithoutDiscardingNegativeZ()
    {
        var seed = SyntheticTestFactory.Create();
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var spec = new ValidatedManholeSpec(seed.Input, seed.Catalogs, policy.Version, seed.Sources, []);
        var pipe = new PipeConnectionSpec(WallId.T1, "D400", 0, 0, 50, []);
        var openingResult = PipeOpeningBuilder.BuildRoundFull(spec, pipe, spec.Catalogs, policy);
        Assert.True(openingResult.Success);
        var opening = openingResult.Value!;
        Assert.Equal(new Interval1D(-105, 505), opening.ZRange);
        var model = ChamberGeometryBuilder.Build(spec, policy).Value!;
        var owned = OpeningOwnershipPartitioner.Partition(model,
            new OpeningSolid(opening.Id, [new RoundOpeningCut(opening.WallFrame!,
                Assert.IsType<CircleProfile>(opening.ConcreteCutout), opening.ExtrusionRange)], opening.Sources),
            new OpeningOwnershipPolicy());
        Assert.True(owned.Success);
        Assert.Contains(owned.Value!.Children, c => c.OwnerId == "Base" && c.VolumeMm3 > 0);
        Assert.Contains(owned.Value.Children, c => c.OwnerId == "Wall/T1" && c.VolumeMm3 > 0);
    }

    [Fact]
    public void DuplicateExplicitPipeIdsBlockBeforeAnyOpeningIsBuilt()
    {
        var a=new PipeConnectionSpec(WallId.T1,"D400",300,0,50,[],"Pipe/A");
        var b=new PipeConnectionSpec(WallId.T2,"D400",300,0,50,[],"Pipe/A");
        var seed=SyntheticTestFactory.Create([a,b]);
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var spec=new ValidatedManholeSpec(seed.Input,seed.Catalogs,policy.Version,seed.Sources,[]);
        var result=PipeOpeningBuilder.BuildAll(spec,policy,new DoubleBoxPolicy());
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="PIPE_ID_DUPLICATE" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void BuildAllConsumesRoundBoxAndDoubleOnFourWallsWithStableIdentity()
    {
        var pipes=new[]
        {
            new PipeConnectionSpec(WallId.T1,"D400",300,0,50,[],"P1"),
            new PipeConnectionSpec(WallId.T2,"BOX",300,0,20,[],"P2"),
            new PipeConnectionSpec(WallId.T3,"DOUBLE",300,0,20,[],"P3"),
            new PipeConnectionSpec(WallId.T4,"D400",300,0,50,[],"P4")
        };
        var box=new PipeCatalogEntry("BOX",PipeKind.Box,null,400,300,50,null,0);
        var dbl=new PipeCatalogEntry("DOUBLE",PipeKind.DoubleBox,null,600,300,50,100,0);
        var round=new PipeCatalogEntry("D400",PipeKind.Round,400,null,null,55,null,0);
        var catalogs=new CatalogSet(new[]{new KeyValuePair<string,PipeCatalogEntry>(round.Code,round),
            new KeyValuePair<string,PipeCatalogEntry>(box.Code,box),
            new KeyValuePair<string,PipeCatalogEntry>(dbl.Code,dbl)},[],[],[]);
        var seed=SyntheticTestFactory.Create(pipes,catalogs);
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var spec=new ValidatedManholeSpec(seed.Input,catalogs,policy.Version,seed.Sources,[]);
        var result=PipeOpeningBuilder.BuildAll(spec,policy,new DoubleBoxPolicy());
        Assert.True(result.Success);
        Assert.Equal(new[]{"P1","P2","P3","P4"},result.Value!.Openings.Select(o=>o.Id));
        Assert.Equal(new[]{OpeningKind.Round,OpeningKind.Box,OpeningKind.Box,OpeningKind.Round},
            result.Value.Openings.Select(o=>o.Kind));
        Assert.Equal(new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"},
            result.Value.Openings.Select(o=>o.OwnerId));
    }
}
