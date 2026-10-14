#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class W12ModuleRemovalTests
{
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture, "W12.inline",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("W12 synthetic fixture"))), null, null, null,
        "W12 synthetic fixture");
    private static readonly PackageIdentity Identity = new(new string('c', 64), "W12/ModuleRemoval", CalculationScope.FullRebar);

    [Fact]
    public void SixStaticModules_AreRemovedFromDrawingAssembly()
    {
        var drawingAssembly = typeof(SheetFourViews).Assembly;
        var types = drawingAssembly.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Family21SheetFiveStore", types);
        Assert.DoesNotContain("Family21SheetFourStore", types);
        Assert.DoesNotContain("Family21SheetThreeStore", types);
        Assert.DoesNotContain("Family21SheetTwoStore", types);
        Assert.DoesNotContain("SheetFourCatchBasinViews", types);
        Assert.DoesNotContain("SheetTwoViews", types);
    }

    [Fact]
    public void SheetFourViews_RetainsSemanticIds_AndMigratedCatchViewIds()
    {
        Assert.Equal("Reinforcement/Neck/Plan", SheetFourViews.PlanViewId);
        Assert.Equal("Reinforcement/Neck/AA", SheetFourViews.AAViewId);
        Assert.Equal("Reinforcement/Neck/BB", SheetFourViews.BBViewId);
        Assert.Equal("SteelSchedule", SheetFourViews.ScheduleViewId);

        Assert.Equal("Reinforcement/Catch/Plan", SheetFourViews.CatchPlanViewId);
        Assert.Equal("Reinforcement/Catch/1-1", SheetFourViews.CatchSec11ViewId);
        Assert.Equal("Reinforcement/Catch/2-2", SheetFourViews.CatchSec22ViewId);
    }

    [Fact]
    public void SheetFiveViews_RetainsViewIdAndDynamicBuilders_WithoutStaticBuildFixture()
    {
        Assert.Equal("Bending/Details", SheetFiveViews.ViewId);
        Assert.NotNull(SheetFiveViews.Bounds);

        // Verify BuildFixture method no longer exists on SheetFiveViews
        var methods = typeof(SheetFiveViews).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.DoesNotContain(methods, m => m.Name == "BuildFixture");

        // Verify dynamic builder methods exist
        Assert.Contains(methods, m => m.Name == "BuildBending");
        Assert.Contains(methods, m => m.Name == "BuildFromIntents");
        Assert.Contains(methods, m => m.Name == "BuildFromCaptions");
    }

    [Fact]
    public void ViewBuilder_CatchFamily_ProjectsDynamicCatchViews()
    {
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var components = new List<ComponentGeometry>
        {
            Component("Pad", ComponentKind.Pad, "Bedding", -600, -600, 600, 600, -250, -150, frame),
            Component("Base", ComponentKind.Base, "Concrete", -500, -500, 500, 500, -150, 0, frame),
            Component("Roof", ComponentKind.Roof, "Concrete", -500, -500, 500, 500, 1400, 1550, frame),
            Component("Neck", ComponentKind.Neck, "Concrete", -250, -250, 250, 250, 1550, 1750, frame),
            Component("CatchBasin1", ComponentKind.CatchBasin, "Concrete", -300, -200, 300, 200, 0, 400, frame)
        };

        var neckPath = new BarPath([new LineSegment3Mm(new(-200, 0, 1560), new(-200, 0, 1740))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var neckBar = new BarOccurrence("neck-bar-1", null, "Neck", ComponentKind.Neck, Layer.Bottom, Axis.Y,
            10, neckPath, 180, 180, LengthStatus.Known, "TEST", [Source], RebarDirection.Vertical, "Neck/T1", RebarSurface.Inner, "Neck");

        var catchPath = new BarPath([new LineSegment3Mm(new(-200, -100, 50), new(200, -100, 50))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var catchBar = new BarOccurrence("catch-bar-1", null, "CatchBasin1", ComponentKind.CatchBasin, Layer.Bottom, Axis.X,
            10, catchPath, 400, 400, LengthStatus.Known, "TEST", [Source], RebarDirection.Supplementary);

        var geometry = new GeometryModel(components, []);
        var layout = new RebarLayout([neckBar, catchBar], [], CalculationScope.FullRebar);

        var neckRow = new SteelScheduleRow("N001", "neck", ComponentKind.Neck, Layer.Bottom, Axis.Y, 10, 1,
            180, 180, 0.6172839506172839, new MassKg(0.1111111111111111), [neckBar.Id], "TEST", RebarDirection.Vertical);
        var catchRow = new SteelScheduleRow("CT01", "catch", ComponentKind.CatchBasin, Layer.Bottom, Axis.X, 10, 1,
            400, 400, 0.6172839506172839, new MassKg(0.24691358024691357), [catchBar.Id], "TEST", RebarDirection.Supplementary);
        var totalMass = new MassKg(neckRow.MassKg.Value + catchRow.MassKg.Value);
        var schedule = new SteelSchedule([neckRow, catchRow],
            [new KeyValuePair<ComponentKind, MassKg>(ComponentKind.Neck, neckRow.MassKg), new KeyValuePair<ComponentKind, MassKg>(ComponentKind.CatchBasin, catchRow.MassKg)],
            totalMass, "D2Over162", "v1");

        var inputs = new DrawingInputs(geometry, layout, schedule, null, DrawingQuantitySnapshot.NotCalculated);

        var presentation = new PresentationRequest(30, ElevationMode.Absolute, new ElevationM(10), SheetOnePresentationPolicy.Family21Theme, true);
        var result = ViewBuilder.Build(inputs, presentation, Identity);

        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
        var package = result.Value!;

        // Must contain the 3 catch views dynamically
        Assert.Contains(package.Views, v => v.Id == SheetFourViews.CatchPlanViewId);
        Assert.Contains(package.Views, v => v.Id == SheetFourViews.CatchSec11ViewId);
        Assert.Contains(package.Views, v => v.Id == SheetFourViews.CatchSec22ViewId);

        // Primitives must come from CatchBasin1
        Assert.Contains(package.Primitives, p => p.ViewId == SheetFourViews.CatchPlanViewId && p.ComponentId == "CatchBasin1");
        Assert.Contains(package.Primitives, p => p.ViewId == SheetFourViews.CatchSec11ViewId && p.ComponentId == "CatchBasin1");
        Assert.Contains(package.Primitives, p => p.ViewId == SheetFourViews.CatchSec22ViewId && p.ComponentId == "CatchBasin1");
    }

    private static ComponentGeometry Component(string id, ComponentKind kind, string material, double x0, double y0,
        double x1, double y1, double z0, double z1, Frame3Mm frame)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]), []);
        var z = new Interval1D(z0, z1);
        return new ComponentGeometry(id, kind, material, footprint, z, new ExtrusionSolid(footprint, frame, z), [], [], [Source]);
    }
}
