#nullable enable
using System.Collections.Generic;
using System.Linq;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class W10ProjectionAndWiringTests
{
    [Fact]
    public void SheetThreeRoofViews_RetainsTopAndBottomViewIds_WithoutStaticFixtures()
    {
        Assert.Equal("Reinforcement/Roof/Roof/Top/Outer/Top", SheetThreeRoofViews.TopViewId);
        Assert.Equal("Reinforcement/Roof/Roof/Bottom/Inner/Bottom", SheetThreeRoofViews.BottomViewId);
    }

    [Fact]
    public void RoofJointDowelProjection_PreservesHoleOneToOne_AndScalesPaperText()
    {
        var line = new LineSegment3Mm(new Point3Mm(100, 200, 0), new Point3Mm(100, 200, 500));
        var path = new BarPath([line], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var dowel = new BarOccurrence(
            id: "dowel-1",
            parentId: null,
            componentId: "Roof",
            componentKind: ComponentKind.Roof,
            layer: Layer.Top,
            axis: Axis.X,
            diameterMm: 12,
            path: path,
            scheduleLengthMm: 500,
            fabricationLengthMm: 500,
            lengthStatus: LengthStatus.Known,
            ruleId: "rule-1",
            sources: [],
            familyId: "Dowel"
        );

        var marks = new Dictionary<string, string> { ["dowel-1"] = "D1" };
        var result = RoofJointDowelProjection.Build([dowel], marks, holeDiameterMm: 20, scaleDenominator: 30);

        Assert.True(result.Success);
        var hole = Assert.Single(result.Value!.OfType<CirclePrimitive>());
        Assert.Equal("Geometry/DowelHole", hole.Role);
        Assert.Equal(SheetThreeRoofViews.TopViewId, hole.ViewId);
        Assert.Equal(10, hole.RadiusMm); // 1:1 model mm: diameter 20 / 2 = 10, not multiplied by 30
        Assert.Equal(new Point2Mm(100, 200), hole.Center);

        var note = Assert.Single(result.Value!.OfType<TextPrimitive>());
        Assert.Equal("Note/RoofDowel", note.Role);
        Assert.Equal(60, note.HeightMm); // 2mm paper * 30 scale denominator
        Assert.Contains("Chốt D12 - lỗ D20", note.Content);
    }

    [Fact]
    public void RoofJointDowelProjection_ScaleDenominator_AdaptsTextHeight()
    {
        var line = new LineSegment3Mm(new Point3Mm(50, 60, 0), new Point3Mm(50, 60, 300));
        var path = new BarPath([line], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var dowel = new BarOccurrence(
            id: "dowel-2",
            parentId: null,
            componentId: "Roof",
            componentKind: ComponentKind.Roof,
            layer: Layer.Top,
            axis: Axis.X,
            diameterMm: 10,
            path: path,
            scheduleLengthMm: 300,
            fabricationLengthMm: 300,
            lengthStatus: LengthStatus.Known,
            ruleId: "rule-2",
            sources: [],
            familyId: "Dowel"
        );

        var marks = new Dictionary<string, string> { ["dowel-2"] = "D2" };
        var result = RoofJointDowelProjection.Build([dowel], marks, holeDiameterMm: 18, scaleDenominator: 50);

        Assert.True(result.Success);
        var note = Assert.Single(result.Value!.OfType<TextPrimitive>());
        Assert.Equal(100, note.HeightMm); // 2mm * 50 = 100
    }
}
