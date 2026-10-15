using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class PresentationGroupTests
{
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture, "PresentationTest.inline",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("PresentationTest"))), null, null, null, "PresentationTest");

    [Fact]
    public void SyntheticPackage_PresentationGroupMapper_ProducesValidMapping()
    {
        var path1 = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(1000, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar1 = new BarOccurrence("Bar/1", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 12, path1, 1000, 1000, LengthStatus.Known, "RULE-1", [Source]);

        var path2 = new BarPath([new LineSegment3Mm(new(0, 200, 0), new(1000, 200, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar2 = new BarOccurrence("Bar/2", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 12, path2, 1000, 1000, LengthStatus.Known, "RULE-1", [Source]);

        var collector = new PolylinePrimitive(
            "Plan/Collector/Base",
            SheetTwoGachCollector.CollectorRole,
            "Plan",
            "Base",
            "Bar/1",
            "D1",
            [new Point2Mm(500, 0), new Point2Mm(500, 200)],
            false
        );

        var tick1 = new BlockPrimitive(
            "Plan/Collector/Base/Gach/Bar/1",
            SheetTwoGach.Role,
            "Plan",
            "Base",
            "Block:GACH",
            new Point2Mm(500, 0),
            0.3,
            [],
            0,
            "Bar/1",
            "D1"
        );

        var tick2 = new BlockPrimitive(
            "Plan/Collector/Base/Gach/Bar/2",
            SheetTwoGach.Role,
            "Plan",
            "Base",
            "Block:GACH",
            new Point2Mm(500, 200),
            0.3,
            [],
            0,
            "Bar/2",
            "D1"
        );

        var callout = new BlockPrimitive(
            "Plan/Collector/Base/Callout",
            SheetTwoBarAnnotations.Role,
            "Plan",
            "Base",
            "Block:BLT1",
            new Point2Mm(600, 100),
            0.3,
            [
                new BlockAttributeIntent("SH", "D1"),
                new BlockAttributeIntent("DK", "%%c12"),
                new BlockAttributeIntent("A", "@200")
            ],
            0,
            "Bar/1",
            "D1"
        );

        var leader = new PolylinePrimitive(
            "Plan/Collector/Base/Callout/Leader",
            "Rebar/Leader",
            "Plan",
            "Base",
            "Bar/1",
            "D1",
            [new Point2Mm(500, 100), new Point2Mm(600, 100)],
            false
        );

        var bounds = new Bounds2Mm(-1000, -1000, 1000, 1000);
        var view = new ViewDefinition("Plan", ViewPurpose.Plan, new Point2Mm(0, 0), bounds, 30);
        var pkg = new DrawingPackage(
            new string('a', 64),
            "DBTOOL-FINAL-SPEC-20261012-v1",
            [view],
            [collector, tick1, tick2, callout, leader],
            bounds,
            CalculationScope.FullRebar
        );

        var map = PresentationGroupMapper.Build(pkg, [bar1, bar2]);

        Assert.NotNull(map);
        Assert.Single(map.Groups);
        var group = map.Groups[0];
        Assert.Equal("D1", group.DisplayMark);
        Assert.Equal(12, group.DiameterMm);
        Assert.Equal(200.0, group.ActualSpacing);
        Assert.Contains("Bar/1", group.OccurrenceIds);
        Assert.Contains("Bar/2", group.OccurrenceIds);
        Assert.Contains("D1", group.CanonicalMarks);
        Assert.Equal("Plan", group.Views[0]);

        string json = map.ToJson();
        Assert.NotEmpty(json);
        var restored = PresentationGroupMap.FromJson(json);
        Assert.Equal(map.Groups.Length, restored.Groups.Length);
        Assert.Equal(map.Groups[0].GroupId, restored.Groups[0].GroupId);
    }

    [Fact]
    public void Case1Run_ExportsPresentationGroupMap_WhenPackageExists()
    {
        string? root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "outputs")))
        {
            root = Directory.GetParent(root)?.FullName;
        }
        if (root is null) return;

        string candidateDir = Path.Combine(root, "outputs", "P6-candidate", "case1-calc-final");
        string? runDir = null;
        if (Directory.Exists(candidateDir))
        {
            var subDirs = Directory.GetDirectories(candidateDir, "run-*");
            if (subDirs.Length > 0)
            {
                runDir = subDirs[^1];
            }
        }

        if (runDir is null) return;

        string drawingPath = Path.Combine(runDir, "drawing.json");
        string packagePath = Path.Combine(runDir, "package.json");
        if (!File.Exists(drawingPath) || !File.Exists(packagePath)) return;

        var drawing = DrawingPackageJson.Deserialize(File.ReadAllText(drawingPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(packagePath));
        var packageRoot = doc.RootElement;
        if (!packageRoot.TryGetProperty("Occurrences", out var occArray)) return;

        var occurrences = new List<BarOccurrence>();
        foreach (var el in occArray.EnumerateArray())
        {
            string id = el.GetProperty("Id").GetString()!;
            string compId = el.GetProperty("ComponentId").GetString()!;
            string frameId = el.GetProperty("FrameId").GetString()!;
            int dia = el.GetProperty("DiameterMm").GetInt32();
            var layer = Layer.Bottom;
            if (el.TryGetProperty("Layer", out var l))
            {
                if (l.ValueKind == JsonValueKind.String && Enum.TryParse<Layer>(l.GetString(), out var pl))
                    layer = pl;
                else if (l.ValueKind == JsonValueKind.Number)
                    layer = (Layer)l.GetInt32();
            }

            var surface = RebarSurface.Inner;
            if (el.TryGetProperty("Surface", out var s) && s.GetString() is { } surfStr && Enum.TryParse<RebarSurface>(surfStr, out var ps))
                surface = ps;

            var direction = RebarDirection.Horizontal;
            if (el.TryGetProperty("Direction", out var d) && d.GetString() is { } dirStr && Enum.TryParse<RebarDirection>(dirStr, out var pd))
                direction = pd;

            var hook = HookPattern.Hook0M;
            var pathEl = el.GetProperty("Path");
            if (pathEl.TryGetProperty("HookPattern", out var hp) && hp.GetString() is { } hookStr && Enum.TryParse<HookPattern>(hookStr, out var php))
                hook = php;

            var segments = new List<PathSegment>();
            if (pathEl.TryGetProperty("Segments", out var segArray))
            {
                foreach (var seg in segArray.EnumerateArray())
                {
                    var startEl = seg.GetProperty("Start");
                    var endEl = seg.GetProperty("End");
                    var start = new Point3Mm(startEl.GetProperty("X").GetDouble(), startEl.GetProperty("Y").GetDouble(), startEl.GetProperty("Z").GetDouble());
                    var end = new Point3Mm(endEl.GetProperty("X").GetDouble(), endEl.GetProperty("Y").GetDouble(), endEl.GetProperty("Z").GetDouble());
                    segments.Add(new LineSegment3Mm(start, end));
                }
            }
            if (segments.Count == 0)
            {
                segments.Add(new LineSegment3Mm(new(0, 0, 0), new(100, 0, 0)));
            }

            var path = new BarPath(segments, false, hook, 0, false, MeasurementBasis.TangentPath);
            double schedLen = el.GetProperty("ScheduleLengthMm").GetDouble();
            double? fabLen = el.TryGetProperty("FabricationLengthMm", out var fl) && fl.ValueKind == JsonValueKind.Number ? fl.GetDouble() : null;
            var lenStatus = fabLen.HasValue ? LengthStatus.Known : LengthStatus.NeedsRule;
            string ruleId = el.GetProperty("RuleId").GetString()!;

            var compKind = ComponentKind.Base;
            if (el.TryGetProperty("ComponentKind", out var ckEl) && Enum.TryParse<ComponentKind>(ckEl.GetString(), out var parsedCk))
                compKind = parsedCk;
            else if (compId.Contains("Wall", StringComparison.OrdinalIgnoreCase))
                compKind = ComponentKind.Wall;
            else if (compId.Contains("Roof", StringComparison.OrdinalIgnoreCase) || compId.Contains("Slab", StringComparison.OrdinalIgnoreCase))
                compKind = ComponentKind.Roof;
            else if (compId.Contains("Neck", StringComparison.OrdinalIgnoreCase))
                compKind = ComponentKind.Neck;
            else if (compId.Contains("Haunch", StringComparison.OrdinalIgnoreCase))
                compKind = ComponentKind.Base;

            var axis = Axis.X;
            if (el.TryGetProperty("Axis", out var axEl) && Enum.TryParse<Axis>(axEl.GetString(), out var parsedAx))
                axis = parsedAx;

            occurrences.Add(new BarOccurrence(
                id,
                null,
                compId,
                compKind,
                layer,
                axis,
                dia,
                path,
                schedLen,
                fabLen,
                lenStatus,
                ruleId,
                [],
                direction,
                frameId,
                surface
            ));
        }

        var map = PresentationGroupMapper.Build(drawing, occurrences);
        Assert.NotNull(map);
        Assert.NotEmpty(map.Groups);

        var coverage = map.ValidateCoverage(occurrences);
        Assert.Equal(occurrences.Count, coverage.MappedOccurrences);
        Assert.Equal(0, coverage.OrphanOccurrences);
        Assert.Equal(0, coverage.PhantomOccurrences);

        string mapJson = map.ToJson();

        string outDir = Path.Combine(root, "output", "dbtool-final-fix");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "presentation-group-map.json"), mapJson);

        string candidateGen = Path.Combine(root, "outputs", "P6-candidate", "case1-final-run");
        if (Directory.Exists(candidateGen))
        {
            File.WriteAllText(Path.Combine(candidateGen, "presentation-group-map.json"), mapJson);
        }
    }

    [Fact]
    public void PresentationGroupMapper_CoversUnmappedComponentsWithZeroOrphans()
    {
        var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(500, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var baseBar = new BarOccurrence("Base/1", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 12, path, 500, 500, LengthStatus.Known, "R1", [Source]);
        var haunchBar = new BarOccurrence("Haunch/1", null, "Haunch", ComponentKind.Base, Layer.Top, Axis.Y, 10, path, 500, 500, LengthStatus.Known, "R2", [Source], familyId: "Haunch");
        var roofBar = new BarOccurrence("Roof/1", null, "Roof", ComponentKind.Roof, Layer.Top, Axis.X, 10, path, 500, 500, LengthStatus.Known, "R3", [Source]);
        var neckBar = new BarOccurrence("Neck/1", null, "Neck", ComponentKind.Neck, Layer.Bottom, Axis.Y, 10, path, 500, 500, LengthStatus.Known, "R4", [Source]);

        var bounds = new Bounds2Mm(0, 0, 1000, 1000);
        var pkg = new DrawingPackage(
            new string('a', 64),
            "DBTOOL-FINAL-SPEC-20261012-v1",
            [new("View1", ViewPurpose.Plan, new(0, 0), bounds, 30)],
            [],
            bounds,
            CalculationScope.FullRebar
        );

        var allBars = new[] { baseBar, haunchBar, roofBar, neckBar };
        var map = PresentationGroupMapper.Build(pkg, allBars);

        Assert.NotNull(map);
        var coverage = map.ValidateCoverage(allBars);
        Assert.Equal(4, coverage.TotalOccurrences);
        Assert.Equal(4, coverage.MappedOccurrences);
        Assert.Equal(0, coverage.OrphanOccurrences);
        Assert.Equal(0, coverage.PhantomOccurrences);
    }
}
