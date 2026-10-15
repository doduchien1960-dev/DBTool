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
            familyId: "MainMesh", shapeRecipeId: "TK.T");

        var marks = new Dictionary<string, string> { { "b1", "T1" } };
        var result = BendingSchematicPlanner.Build([bar], marks);

        Assert.True(result.Success);
        var shape = Assert.Single(result.Value!);
        Assert.Equal("T1", shape.Mark);
        Assert.Equal("TK.T", shape.RecipeId);
        Assert.Equal("TK.T", shape.GlyphAsset);
        Assert.Equal(500, shape.ScheduleLengthMm);
        Assert.False(shape.Closed);
    }

    [Fact]
    public void BendingSchematicPlanner_BuildFromGroups_PreservesS5Registry()
    {
        var path = new BarPath([new LineSegment3Mm(new(0, 0, 0), new(600, 0, 0))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar1 = new BarOccurrence("b1", null, "Wall", ComponentKind.Wall, Layer.Bottom, Axis.X, 12, path, 600, 600, LengthStatus.Known, "rule-1", [Source], shapeRecipeId: "TK.T");
        var bar2 = new BarOccurrence("b2", null, "Wall", ComponentKind.Wall, Layer.Bottom, Axis.X, 12, path, 600, 600, LengthStatus.Known, "rule-1", [Source], shapeRecipeId: "TK.T");

        var record = new PresentationGroupRecord(
            GroupId: "Group_Wall_1",
            Scope: "Wall",
            DisplayMark: "1",
            Function: "MainBar",
            FaceLayer: "Front/Bottom",
            ModelDirection: "X",
            DiameterMm: 12,
            SpacingMode: "Pitch",
            ActualSpacing: 150,
            Quantity: 2,
            ShapeSignatures: ["Straight"],
            SupportRegions: ["Wall"],
            OccurrenceIds: ["b1", "b2"],
            CanonicalMarks: ["1"],
            Views: ["WallElevation"],
            LabelIds: [],
            CollectorIds: [],
            Sources: ["Source1"],
            RuleVersion: "v1"
        );

        var map = new PresentationGroupMap("v1", [record]);
        var result = BendingSchematicPlanner.BuildFromGroups([bar1, bar2], map);

        Assert.True(result.Success);
        var shape = Assert.Single(result.Value!);
        Assert.Equal("1", shape.Mark);
        Assert.Equal("Group_Wall_1", shape.GroupId);
        Assert.Equal(2, shape.Quantity);
        Assert.Equal("TK.T", shape.RecipeId);
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
            familyId: "Ladder", shapeRecipeId: "TK.M.THANG");

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

        // When materialKind is RoundSteel and includeGrid is null, Schedule/Grid must be omitted on VHG native path
        var pagesNative = ScheduleTablePlanner.Plan(rows, new Point2Mm(0, 0), 30, "Steel", ["Mark", "MassKg"], null);
        var pageNative = Assert.Single(pagesNative);
        Assert.DoesNotContain(pageNative.Primitives, p => p.Role == "Schedule/Grid");

        // When explicitly included, Schedule/Grid is generated
        var pagesNormal = ScheduleTablePlanner.Plan(rows, new Point2Mm(0, 0), 30, "Steel", ["Mark", "MassKg"], null, includeGrid: true);
        var pageNormal = Assert.Single(pagesNormal);
        Assert.Contains(pageNormal.Primitives, p => p.Role == "Schedule/Grid");
    }

    [Fact]
    public void ScheduleTablePlanner_RetainsGrid_ForQuantityAndAngleTables()
    {
        var rows = new[]
        {
            new TableRowPrimitive(
                "qrow1", "Quantity/Row", "QuantityTable", "Base", null, "1", "1",
                [new("Item", TableCellKind.Text, "Concrete", null), new("VolumeM3", TableCellKind.Number, null, 2.5)],
                DrawingMaterialKind.None)
        };

        var pages = ScheduleTablePlanner.Plan(rows, new Point2Mm(0, 0), 30, "Quantities", ["Item", "VolumeM3"], null);
        var page = Assert.Single(pages);
        // QuantityTable retains its grid
        Assert.Contains(page.Primitives, p => p.Role == "Schedule/Grid");

        var angleRows = new[]
        {
            new TableRowPrimitive(
                "arow1", "Schedule/Angle/Row", "AngleSchedule", "Base", null, "1", "1",
                [new("Profile", TableCellKind.Text, "L50x50x5", null), new("MassKg", TableCellKind.Number, null, 15.0)],
                DrawingMaterialKind.AngleSteel)
        };

        var anglePages = ScheduleTablePlanner.Plan(angleRows, new Point2Mm(0, 0), 30, "Angles", ["Profile", "MassKg"], null);
        var anglePage = Assert.Single(anglePages);
        // AngleSchedule retains its grid
        Assert.Contains(anglePage.Primitives, p => p.Role == "Schedule/Grid");
    }

    [Fact]
    public void SheetFivePlacement_MatchesBendingViews()
    {
        Assert.NotNull(SheetFivePlacement.Origin(SheetFiveViews.ViewId));
        Assert.Equal(new Point2Mm(0, 0), SheetFivePlacement.Origin(SheetFiveViews.ViewId));

        Assert.Null(SheetFivePlacement.Origin("Bending/Body"));
        Assert.Null(SheetFivePlacement.Origin("Bending/Panel"));
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
    }
}
