using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoCollectorLabelsTests
{
    [Fact]
    public void SplitCollectorsHaveSeparateActualLabelsAndLeaderTips()
    {
        var a=new PolylinePrimitive("a","Rebar/Top","BaseTop","Base","a","D9",[new(0,0),new(0,20)],false);
        var b=new PolylinePrimitive("b","Rebar/Top","BaseTop","Base","b","D9",[new(100,80),new(100,100)],false);
        var ticks=SheetTwoGachCollector.Build([a,b],new HashSet<string>{"a","b"});
        var original=new BlockPrimitive("old",SheetTwoBarAnnotations.Role,"BaseTop","Base","Block:BLT2",new(-300,-150),.3,
            [new("SH","D1"),new("DK","%%c10"),new("A","@150")],occurrenceId:"a",scheduleMark:"D9");
        var oldLeader=new PolylinePrimitive("old/Leader","Rebar/Leader","BaseTop","Base","a","D9",[new(0,0),original.Insertion],false);
        var result=SheetTwoCollectorLabels.Apply(new DrawingPrimitive[]{a,b,original,oldLeader}.Concat(ticks),
            new Dictionary<string,(int,double?)>{{"a",(10,150)},{"b",(12,175)}});
        var labels=result.OfType<BlockPrimitive>().Where(p=>p.Role==SheetTwoBarAnnotations.Role).ToArray();
        Assert.Equal(2,labels.Length);
        Assert.DoesNotContain(original,result);
        Assert.DoesNotContain(oldLeader,result);
        Assert.Equal(2,labels.Select(p=>p.Insertion).Distinct().Count());
        Assert.True(DrawingPackageValidator.ValidateCollectorAssociations(result).Success);
        var withoutLeaders=result.Where(p=>p.Role!="Rebar/Leader").ToArray();
        Assert.Contains(DrawingPackageValidator.ValidateCollectorAssociations(withoutLeaders).Diagnostics,
            d=>d.Code=="DRAWING_COLLECTOR_LABEL_INVALID");
        var wrongTicks=result.Select(p=>p is BlockPrimitive tick && tick.Role==SheetTwoGach.Role
            ?new BlockPrimitive(tick.SemanticId,tick.Role,tick.ViewId,tick.ComponentId,tick.BlockRole,
                new(tick.Insertion.X+5,tick.Insertion.Y),tick.AnnotationScale,tick.Attributes,tick.RotationRadians,
                tick.OccurrenceId,tick.ScheduleMark):p).ToArray();
        Assert.False(DrawingPackageValidator.ValidateCollectorAssociations(wrongTicks).Success);
        foreach(var label in labels)
        {
            var leader=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId==label.SemanticId+"/Leader");
            var tick=Assert.Single(ticks.OfType<BlockPrimitive>(),p=>p.OccurrenceId==label.OccurrenceId);
            Assert.Equal(tick.Insertion,leader.Points[0]);
            Assert.Equal(label.Insertion,leader.Points[^1]);
            Assert.Equal(label.OccurrenceId,leader.OccurrenceId);
            Assert.Contains(label.Attributes,p=>p.Key=="DK" && p.Value==(label.OccurrenceId=="a"?"%%c10":"%%c12"));
        }
    }

    [Fact]
    public void HistoricalLabelsReplay_NeverGeneratesDuplicateInsertionPoints()
    {
        // Replays a multi-wall scenario with 6 collectors across 3 wall components
        var primitives = new List<DrawingPrimitive>();
        var metadata = new Dictionary<string, (int, double?)>();
        string[] walls = ["Wall/T1", "Wall/T2", "Wall/T3"];

        for (int w = 0; w < walls.Length; w++)
        {
            for (int dir = 0; dir < 2; dir++)
            {
                string occ = $"bar_{w}_{dir}";
                string mark = dir == 0 ? "T1" : "T2";
                double x = w * 500.0;
                double y = dir * 400.0;
                var bar = new PolylinePrimitive($"bar_{w}_{dir}", "Rebar/Main", walls[w], walls[w], occ, mark,
                    dir == 0 ? [new(x, y), new(x, y + 200)] : [new(x, y), new(x + 200, y)], false);
                primitives.Add(bar);
                metadata[occ] = (10, 150);
            }
        }

        var occIds = metadata.Keys.ToHashSet();
        var ticksAndCollectors = SheetTwoGachCollector.Build(primitives, occIds);
        primitives.AddRange(ticksAndCollectors);

        var result = SheetTwoCollectorLabels.Apply(primitives, metadata);
        var labels = result.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoBarAnnotations.Role).ToArray();

        Assert.NotEmpty(labels);
        // Assert zero duplicate insertions across all generated labels (historical 64-stack bug fix)
        Assert.Equal(labels.Length, labels.Select(l => (Math.Round(l.Insertion.X, 3), Math.Round(l.Insertion.Y, 3))).Distinct().Count());
        
        // Assert no label is stacked at the historical collision points (-1065, 1350)
        Assert.DoesNotContain(labels, l => Math.Abs(l.Insertion.X - (-1065)) < 1e-3 && Math.Abs(l.Insertion.Y - 1350) < 1e-3);
    }

    [Fact]
    public void MultipleFamiliesAcrossView_AllocateDistinctSharedSlots()
    {
        // 3 distinct wall mesh families sharing the same view "Wall/Elevation"
        var primitives = new List<DrawingPrimitive>();
        var metadata = new Dictionary<string, (int, double?)>();

        for (int f = 0; f < 3; f++)
        {
            string comp = $"Comp_{f}";
            for (int c = 0; c < 2; c++)
            {
                string occ = $"occ_{f}_{c}";
                string mark = $"D{f}{c}";
                double x = f * 300.0;
                double y = c * 250.0;
                var bar = new PolylinePrimitive($"bar_{f}_{c}", "Rebar/Main", "Wall/Elevation", comp, occ, mark,
                    [new(x, y), new(x, y + 100)], false);
                primitives.Add(bar);
                metadata[occ] = (12, 200);
            }
        }

        var ticksAndCollectors = SheetTwoGachCollector.Build(primitives, metadata.Keys.ToHashSet());
        primitives.AddRange(ticksAndCollectors);

        var result = SheetTwoCollectorLabels.Apply(primitives, metadata);
        var labels = result.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoBarAnnotations.Role).ToArray();

        // 6 distinct labels generated, each in a unique shared slot
        Assert.Equal(6, labels.Length);
        Assert.Equal(6, labels.Select(l => (Math.Round(l.Insertion.X, 2), Math.Round(l.Insertion.Y, 2))).Distinct().Count());
    }

    [Fact]
    public void RepeatedEquivalentBars_ProduceSingleCollectorAndCallout_WithFullCoverageInMap()
    {
        // 15 repeated equivalent vertical bars in base mesh
        var bars = Enumerable.Range(0, 15).Select(i =>
            new PolylinePrimitive($"bar_{i}", "Rebar/Main", "BaseTop", "Base", $"occ_{i}", "D1",
                [new(i * 150.0, 0), new(i * 150.0, 1000)], false)).ToArray();

        var occIds = bars.Select(b => b.OccurrenceId!).ToHashSet();
        var ticks = SheetTwoGachCollector.Build(bars, occIds);
        var metadata = occIds.ToDictionary(id => id, _ => (10, (double?)150));

        var result = SheetTwoCollectorLabels.Apply(bars.Concat(ticks), metadata);

        // Assert 1 collector polyline and 1 callout label
        var collectors = result.OfType<PolylinePrimitive>().Where(p => p.Role == SheetTwoGachCollector.CollectorRole).ToArray();
        Assert.Single(collectors);

        var labels = result.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoBarAnnotations.Role).ToArray();
        Assert.Single(labels);

        // Assert 15 native ticks on the single collector
        var calloutTicks = result.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoGach.Role).ToArray();
        Assert.Equal(15, calloutTicks.Length);

        // Assert PresentationGroupMap contains 1 group with all 15 occurrences mapped
        var physicalOccurrences = Enumerable.Range(0, 15).Select(i =>
            new DBTool.Domain.BarOccurrence($"occ_{i}", null, "Base", DBTool.Domain.ComponentKind.Base,
                DBTool.Domain.Layer.Bottom, DBTool.Domain.Axis.X, 10,
                new DBTool.Domain.BarPath([new LineSegment3Mm(new(i * 150.0, 0, 0), new(i * 150.0, 1000, 0))],
                    false, DBTool.Domain.HookPattern.Hook0M, 0, false, DBTool.Domain.MeasurementBasis.TangentPath),
                1000, 1000, DBTool.Domain.LengthStatus.Known, "D1", [])).ToArray();

        var bounds = new Bounds2Mm(0, -500, 2200, 1100);
        var views = new[] { new ViewDefinition("BaseTop", ViewPurpose.BaseMesh, new(0, 0), bounds, 30) };
        var package = new DrawingPackage(new string('a', 64), "DBTOOL-FINAL-SPEC-20261012-v1",
            views, result, bounds, DBTool.Domain.CalculationScope.FullRebar);

        var groupMap = PresentationGroupMapper.Build(package, physicalOccurrences);
        Assert.Single(groupMap.Groups);
        var group = groupMap.Groups[0];
        Assert.Equal(15, group.OccurrenceIds.Length);
        Assert.Equal("D2", group.DisplayMark);
        Assert.Single(group.CollectorIds);
    }
}
