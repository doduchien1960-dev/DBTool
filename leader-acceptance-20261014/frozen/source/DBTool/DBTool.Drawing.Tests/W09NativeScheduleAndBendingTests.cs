#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class W09NativeScheduleAndBendingTests
{
    private static readonly SourceRef Source = new(
        SourceKind.SyntheticFixture,
        "test.xlsx",
        "0000000000000000000000000000000000000000000000000000000000000000",
        null,
        null,
        null,
        "fixture");

    [Fact]
    public void BendingSchematicPlanner_PopulatesRecipeAndGlyph()
    {
        var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(500, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar = new BarOccurrence(
            "b1", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X, 12, path,
            500, 500, LengthStatus.Known, "rule-1", [Source],
            familyId: "MainMesh", shapeRecipeId: "Shape.Straight");

        var marks = new Dictionary<string, string> { { "b1", "T1" } };
        var result = BendingSchematicPlanner.Build([bar], marks);

        Assert.True(result.Success);
        var shape = Assert.Single(result.Value!);
        Assert.Equal("T1", shape.Mark);
        Assert.Equal("Shape.Straight", shape.RecipeId);
        Assert.Equal("TK.T", shape.GlyphAsset);
        Assert.Equal(500, shape.ScheduleLengthMm);
        Assert.False(shape.Closed);
    }

    [Fact]
    public void BendingSchematicPlanner_UnresolvedMark_Blocks()
    {
        var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(500, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar = new BarOccurrence("b1", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X, 12, path, 500, 500, LengthStatus.Known, "rule-1", [Source]);

        var result = BendingSchematicPlanner.Build([bar], new Dictionary<string, string>());
        Assert.False(result.Success);
        Assert.Equal("BENDING_MARK_UNRESOLVED", result.Diagnostics[0].Code);
    }

    [Fact]
    public void BendingSchematicPlanner_LadderBar_ResolvesLadderGlyph()
    {
        Point3Mm[] pts = [new(0, 100, 150), new(0, 100, 0), new(0, -150, 0), new(300, -150, 0), new(300, 100, 0), new(300, 100, 150)];
        var segments = pts.Zip(pts.Skip(1), (a, b) => (PathSegment)new LineSegment3Mm(a, b));
        var path = new BarPath(segments, false, HookPattern.Hook0M, 0, false, MeasurementBasis.NominalPolyline);

        var bar = new BarOccurrence(
            "ladder-1", null, "Ladder", ComponentKind.Wall, Layer.Bottom, Axis.X, 18, path,
            1100, 1100, LengthStatus.Known, "rule-1", [Source],
            familyId: "Ladder", shapeRecipeId: "Shape.Ladder");

        var marks = new Dictionary<string, string> { { "ladder-1", "Th1" } };
        var result = BendingSchematicPlanner.Build([bar], marks);

        Assert.True(result.Success);
        var shape = Assert.Single(result.Value!);
        Assert.Equal("TK.M.THANG", shape.GlyphAsset);
    }

    [Fact]
    public void ScheduleTablePlanner_OmitRoundGrid_ForRoundSteelSchedule()
    {
        var rows = new[]
        {
            new TableRowPrimitive(
                "row1", "Schedule/Row", "SteelSchedule", "Base", null, "1", "1",
                [new("Mark", TableCellKind.Text, "1", null), new("MassKg", TableCellKind.Number, null, 10.5)],
                DrawingMaterialKind.RoundSteel)
        };

        var pages = ScheduleTablePlanner.Plan(rows, new(0, 0), 30, "Steel", ["Mark", "Mass"], null);
        var page = Assert.Single(pages);

        // Grid lines are omitted for round steel schedule on VHG native path
        Assert.DoesNotContain(page.Primitives, p => p.Role == "Schedule/Grid");
        Assert.Contains(page.Primitives, p => p.Role == "Schedule/Header");
        Assert.Contains(page.Primitives.OfType<TableRowPrimitive>(), r => r.SemanticId == "row1");
    }

    [Fact]
    public void ScheduleTablePlanner_RetainsGrid_ForQuantityTable()
    {
        var rows = new[]
        {
            new TableRowPrimitive(
                "qrow1", "Quantity/Row", "QuantityTable", "Base", null, null, "Base",
                [new("Component", TableCellKind.Text, "Base", null), new("Net", TableCellKind.Number, null, 1.25)],
                DrawingMaterialKind.Concrete)
        };

        var pages = ScheduleTablePlanner.Plan(rows, new(0, 0), 30, "Quantities", ["Component", "Net"], null);
        var page = Assert.Single(pages);

        // QuantityTable retains its grid
        Assert.Contains(page.Primitives, p => p.Role == "Schedule/Grid");
    }

    [Fact]
    public void ScheduleTablePlanner_RetainsGrid_ForAngleSchedule()
    {
        var rows = new[]
        {
            new TableRowPrimitive(
                "arow1", "Schedule/AngleRow", "AngleSchedule", "Panel", null, "TG1", "TG1",
                [new("Mark", TableCellKind.Text, "TG1", null), new("MassKg", TableCellKind.Number, null, 15.0)],
                DrawingMaterialKind.AngleSteel)
        };

        var pages = ScheduleTablePlanner.Plan(rows, new(0, 0), 30, "Angles", ["Mark", "Mass"], null);
        var page = Assert.Single(pages);

        // AngleSchedule retains its grid
        Assert.Contains(page.Primitives, p => p.Role == "Schedule/Grid");
    }

    [Fact]
    public void SheetFivePlacement_MatchesBendingViews()
    {
        Assert.NotNull(SheetFivePlacement.Origin(SheetFiveViews.ViewId));
        Assert.Equal(new Point2Mm(0, 0), SheetFivePlacement.Origin(SheetFiveViews.ViewId));

        Assert.NotNull(SheetFivePlacement.Origin("Bending/Body"));
        Assert.Equal(new Point2Mm(0, 0), SheetFivePlacement.Origin("Bending/Body"));

        Assert.NotNull(SheetFivePlacement.Origin("Bending/Panel"));
        Assert.Equal(new Point2Mm(0, 0), SheetFivePlacement.Origin("Bending/Panel"));

        Assert.Null(SheetFivePlacement.Origin("Plan"));
        Assert.Null(SheetFivePlacement.Origin("WallMeshPlan"));
    }

    [Fact]
    public void SheetFiveViews_BuildFromIntentsAndCaptions()
    {
        var segments = ImmutableArray.Create<DrawingPathSegment>(new LinePathSegment(new(0, 0), new(500, 0)));
        var intent = new BendingShapeIntent("T1", "Wall", ComponentKind.Wall, 10, 500, false, segments, ["b1"], "Shape.Straight", "TK.T");
        var primitives = SheetFiveViews.BuildFromIntents([intent]);

        Assert.NotEmpty(primitives);
        Assert.Contains(primitives.OfType<PathPrimitive>(), p => p.Purpose == ViewPurpose.Schematic);
        Assert.Contains(primitives.OfType<TextPrimitive>(), p => p.Role == "Rebar/Mark");

        var caption = new CaptionV2(
            group_id: "G1",
            glyph_asset: "TK.T",
            text: "1-%%c12 L=500",
            role_values: [],
            underline_start: new Point2V2(0, -10),
            underline_end: new Point2V2(100, -10),
            ink: new Bounds2V2(0, -15, 100, 20),
            view_id: SheetFiveViews.ViewId,
            insertion: new Point2V2(0, 0),
            asset_scale: 0.3
        );

        var capPrimitives = SheetFiveViews.BuildFromCaptions([caption]);
        Assert.Equal(2, capPrimitives.Count);
        Assert.Contains(capPrimitives.OfType<TextPrimitive>(), t => t.Role == "Rebar/BendingMark" && t.Content == "1-%%c12 L=500");
        Assert.Contains(capPrimitives.OfType<PolylinePrimitive>(), p => p.Role == "Rebar/BendingUnderline");
    }
}
