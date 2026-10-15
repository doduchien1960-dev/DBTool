#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Drawing.Tests;

public class W04PlannerAndProjectorTests
{
    private static GlyphV2[] CreateTestGlyphs(string styleId)
    {
        var list = new List<GlyphV2>();
        // ASCII printable
        for (int cp = 32; cp <= 126; cp++)
        {
            list.Add(new GlyphV2(styleId, cp, 60.0, new Bounds2V2(0, 0, 60.0, 100.0), "font_hash", 100.0));
        }
        // Unicode escapes and Vietnamese characters
        int[] extras = [
            'Ø', '±', '°', 'À', 'Á', 'Â', 'Ã', 'È', 'É', 'Ê', 'Ì', 'Í', 'Ò', 'Ó', 'Ô', 'Õ', 'Ù', 'Ú', 'Ý',
            'à', 'á', 'â', 'ã', 'è', 'é', 'ê', 'ì', 'í', 'ò', 'ó', 'ô', 'õ', 'ù', 'ú', 'ý',
            'Ă', 'ă', 'Đ', 'đ', 'Ĩ', 'ĩ', 'Ũ', 'ũ', 'Ơ', 'ơ', 'Ư', 'ư', 'Ạ', 'ạ', 'Ả', 'ả',
            'Ấ', 'ấ', 'Ầ', 'ầ', 'Ẩ', 'ẩ', 'Ẫ', 'ẫ', 'Ậ', 'ậ', 'Ắ', 'ắ', 'Ằ', 'ằ', 'Ẳ', 'ẳ',
            'Ẵ', 'ẵ', 'Ặ', 'ặ', 'Ẹ', 'ẹ', 'Ẻ', 'ẻ', 'Ẽ', 'ẽ', 'Ế', 'ế', 'Ề', 'ề', 'Ể', 'ể',
            'Ễ', 'ễ', 'Ệ', 'ệ', 'Ỉ', 'ỉ', 'Ị', 'ị', 'Ọ', 'ọ', 'Ỏ', 'ỏ', 'Ố', 'ố', 'Ồ', 'ồ',
            'Ổ', 'ổ', 'Ỗ', 'ỗ', 'Ộ', 'ộ', 'Ớ', 'ớ', 'Ờ', 'ờ', 'Ở', 'ở', 'Ỡ', 'ỡ', 'Ợ', 'ợ',
            'Ụ', 'ụ', 'Ủ', 'ủ', 'Ứ', 'ứ', 'Ừ', 'ừ', 'Ử', 'ử', 'Ữ', 'ữ', 'Ự', 'ự', 'Ỳ', 'ỳ',
            'Ỵ', 'ỵ', 'Ỷ', 'ỷ', 'Ỹ', 'ỹ'
        ];
        foreach (int cp in extras)
        {
            list.Add(new GlyphV2(styleId, cp, 60.0, new Bounds2V2(0, 0, 60.0, 100.0), "font_hash", 100.0));
        }
        return list.ToArray();
    }

    private static EffectiveV2 CreateTestEffective(bool catchActive = true)
    {
        return new EffectiveV2(
            id: "M01",
            display_name: "Hố ga M01",
            profile: "Standard",
            clear_x_mm: 1500.0,
            clear_y_mm: 1200.0,
            clear_h_mm: 2000.0,
            base_mm: 250.0,
            wall_mm: 200.0,
            roof_mm: 200.0,
            base_expansion_mm: 100.0,
            pad_mm: 100.0,
            pad_expansion_mm: 100.0,
            base_haunch_mm: 150.0,
            top_haunch_mm: 150.0,
            bedding_mortar_mm: 20.0,
            neck_active: true,
            neck_clear_x_mm: 800.0,
            neck_clear_y_mm: 800.0,
            neck_raw_clear_x_mm: 800.0,
            neck_raw_clear_y_mm: 800.0,
            neck_wall_mm: 150.0,
            neck_height_mm: 500.0,
            neck_position: "Center",
            neck_center: new Point2V2(0, 0),
            neck_reinforced: true,
            cover_kind: "Standard",
            cover_code: "COV-01",
            product_rotation_rad: 0.0,
            frame_outer_x_mm: 1000.0,
            frame_outer_y_mm: 1000.0,
            frame_height_mm: 100.0,
            base_elevation_m: 10.5,
            elevation_mode: "Absolute",
            body_scale: 30.0,
            panel_scale: 30.0,
            note_style: "NoteStyle1",
            pitch_symbol: "@",
            datums:
            [
                new DatumV2("BaseTop", 0.0, "BT", ["src1"]),
                new DatumV2("FinishedCoverTop", 2600.0, "FCT", ["src2"])
            ],
            members: [],
            pipes: [],
            @catch: catchActive
                ? new CatchSpecV2(
                    active: true,
                    owner: "Body",
                    wall: "T1",
                    position: "Center",
                    raw_position: "Center",
                    offset_u_mm: 0.0,
                    opening_width_mm: 600.0,
                    opening_height_mm: 400.0,
                    clear_depth_mm: 500.0,
                    clear_height_mm: 800.0,
                    wall_mm: 150.0,
                    base_mm: 150.0,
                    pad_mm: 50.0,
                    pad_expansion_mm: 50.0,
                    reinforced: true,
                    product_code: "SCR960x530",
                    source_ids: ["srcCatch"]
                )
                : null,
            panel: null,
            source_ids: ["srcM01"],
            parsed_fields: [],
            contact_equations: []
        );
    }

    private static GeometryV2 CreateTestGeometry()
    {
        var frame = new FrameV2("F0", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);
        var loop = new LoopV2("L0", "Outer",
        [
            new CurveV2("C0", "Line", new Point3V2(-400, -200, 0), new Point3V2(400, -200, 0), null, null, null, null, "Bottom"),
            new CurveV2("C1", "Line", new Point3V2(400, -200, 0), new Point3V2(400, 200, 0), null, null, null, null, "Right"),
            new CurveV2("C2", "Line", new Point3V2(400, 200, 0), new Point3V2(-400, 200, 0), null, null, null, null, "Top"),
            new CurveV2("C3", "Line", new Point3V2(-400, 200, 0), new Point3V2(-400, -200, 0), null, null, null, null, "Left")
        ], true, 320000.0);

        var comp = new ComponentV2(
            id: "Catch/Body",
            kind: "Catch",
            assembly_id: "CatchAssembly",
            material_id: "Concrete",
            frame: frame,
            loops: [loop],
            z_min_mm: 0.0,
            z_max_mm: 500.0,
            thickness_mm: 150.0,
            patches: [],
            void_ids: [],
            source_ids: ["srcGeom"],
            solid_parts: [],
            local_key: "CatchBody",
            material_role: "Concrete"
        );

        var prod = new ProductPlacementV2(
            id: "Prod01",
            contract_id: "Contract01",
            anchor: new Point3V2(0, 0, 100),
            rotation_rad: 0.0,
            physical_scale: 1.0,
            plan_asset: "Block:SCR960x530",
            section_aa_asset: "Block:SCR960x530",
            section_bb_asset: "Block:SCR960x530",
            source_ids: ["srcProd"]
        );

        var openLoop = new LoopV2("OL0", "Outer",
        [
            new CurveV2("OC0", "Line", new Point3V2(-100, -100, 0), new Point3V2(100, -100, 0), null, null, null, null, "Cut"),
            new CurveV2("OC1", "Line", new Point3V2(100, -100, 0), new Point3V2(100, 100, 0), null, null, null, null, "Cut"),
            new CurveV2("OC2", "Line", new Point3V2(100, 100, 0), new Point3V2(-100, 100, 0), null, null, null, null, "Cut"),
            new CurveV2("OC3", "Line", new Point3V2(-100, 100, 0), new Point3V2(-100, -100, 0), null, null, null, null, "Cut")
        ], true, 40000.0);

        var opening = new OpeningV2(
            id: "Open01",
            kind: "Pipe",
            host_ids: ["Catch/Body"],
            frame: frame,
            inner: [openLoop],
            pipe_outer: [openLoop],
            concrete_cutout: [openLoop],
            depth_min_mm: 0.0,
            depth_max_mm: 500.0,
            invert_mm: 0.0,
            clearance_rule_id: null,
            source_ids: ["srcOpen"]
        );

        return new GeometryV2([comp], [opening], [], "geom_hash", [prod]);
    }

    private static RebarV2 CreateTestRebar()
    {
        var path = new[]
        {
            new CurveV2("BarC0", "Line", new Point3V2(-300, 0, 0), new Point3V2(300, 0, 0), null, null, null, null, "Main")
        };

        var occ = new OccurrenceV2(
            id: "Occ01",
            parent_id: "Parent01",
            host_id: "Catch/Body",
            assembly_id: "CatchAssembly",
            panel_instance_id: null,
            face: "Outer",
            layer: "L1",
            direction: "Horizontal",
            family: "MainMesh",
            station_index: 0,
            station_mm: 100.0,
            diameter_mm: 10,
            requested_pitch_mm: 150.0,
            actual_gap_mm: 150.0,
            opening_ids: [],
            region_role: "Catch",
            path: path,
            role_values: [new RoleValueV2("DAI", 600.0, ["srcR"])],
            straight_run_mm: 600.0,
            fabrication_length_mm: 600.0,
            schedule_length_mm: 600.0,
            measurement_basis: "Centerline",
            length_status: "Approved",
            shape_recipe_id: "Recipe01",
            source_ids: ["srcOcc"],
            spec_id: "Spec01",
            station_key: "K0",
            fabrication_basis: "TrueLength"
        );

        return new RebarV2([occ], [], [], [], [], "rebar_hash", [], "fab_hash");
    }

    private static GroupsV2 CreateTestGroups()
    {
        var g = new GroupV2(
            id: "G01",
            host_id: "Catch/Body",
            face: "Outer",
            layer: "L1",
            direction: "Horizontal",
            family: "MainMesh",
            region_role: "Catch",
            opening_ids: [],
            shape_recipe_id: "Recipe01",
            diameter_mm: 10,
            member_ids: ["Occ01"],
            canonical_ids: ["Can01"],
            member_lengths_mm: [600.0],
            count: 1,
            raw_sum_mm: 600.0,
            mean_mm: 600.0,
            min_mm: 600.0,
            max_mm: 600.0,
            mean_roles: [new RoleValueV2("DAI", 600.0, ["srcR"])],
            display_mark: "1",
            pitch_mm: 150.0,
            mass_kg: 0.37,
            measurement_basis: "Centerline",
            source_ids: ["srcGrp"],
            material_kind: "RoundBar",
            angle_code: null,
            unit_mass_kg_per_m: 0.617,
            member_length_basis: "Centerline",
            length_policy_id: "Policy01"
        );

        return new GroupsV2([g], "group_hash");
    }

    private static AssetsV2 CreateTestAssets()
    {
        var scheduleAttrs = new[]
        {
            new AttributeDefV2("SH", "1", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("DK", "%%c10", "Standard", 150, 0.8, new(100, 0, 0), new(100, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("DAI", "600", "Standard", 150, 0.8, new(200, 0, 0), new(200, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TDAI", "0.60", "Standard", 150, 0.8, new(300, 0, 0), new(300, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("KL", "0.37", "Standard", 150, 0.8, new(400, 0, 0), new(400, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("SL", "1", "Standard", 150, 0.8, new(500, 0, 0), new(500, 0, 0), 0, "Left", "Baseline")
        };

        var summaryAttrs = new[]
        {
            new AttributeDefV2("DAY1", "0", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("DAY2", "0", "Standard", 150, 0.8, new(100, 0, 0), new(100, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TUONG1", "0", "Standard", 150, 0.8, new(200, 0, 0), new(200, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TUONG2", "0", "Standard", 150, 0.8, new(300, 0, 0), new(300, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TRAN1", "0", "Standard", 150, 0.8, new(400, 0, 0), new(400, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TRAN2", "0", "Standard", 150, 0.8, new(500, 0, 0), new(500, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("CO1", "0", "Standard", 150, 0.8, new(600, 0, 0), new(600, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("CO2", "0", "Standard", 150, 0.8, new(700, 0, 0), new(700, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("NAP1", "0", "Standard", 150, 0.8, new(800, 0, 0), new(800, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("NAP2", "0", "Standard", 150, 0.8, new(900, 0, 0), new(900, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("CUATHU1", "0.37", "Standard", 150, 0.8, new(1000, 0, 0), new(1000, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("CUATHU2", "0", "Standard", 150, 0.8, new(1100, 0, 0), new(1100, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("THEPHINH", "0", "Standard", 150, 0.8, new(1200, 0, 0), new(1200, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("TONG", "0.37", "Standard", 150, 0.8, new(1300, 0, 0), new(1300, 0, 0), 0, "Left", "Baseline")
        };

        var labelAttrs = new[]
        {
            new AttributeDefV2("SH", "1", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("DK", "%%c10", "Standard", 150, 0.8, new(100, 0, 0), new(100, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("A", "@150", "Standard", 150, 0.8, new(200, 0, 0), new(200, 0, 0), 0, "Left", "Baseline"),
            new AttributeDefV2("NOTE", "1-%%c10", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline")
        };

        var titleAttrs = new[]
        {
            new AttributeDefV2("TIEUDE", "TITLE", "Standard", 250, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline")
        };

        return new AssetsV2(
            bundle_hash: "asset_hash",
            definitions:
            [
                new AssetV2("TK.T", "H1", "hash1", new(0, 0, 0), new(0, -500, 12500, 0), "Schedule", [], scheduleAttrs, "0", "Standard", []),
                new AssetV2("TK.THKL.CT", "H2", "hash2", new(0, 0, 0), new(0, -1000, 12500, 0), "Schedule", [], summaryAttrs, "0", "Standard", []),
                new AssetV2("TK.THKL", "H2_THKL", "hash_thkl", new(0, 0, 0), new(0, -1000, 12500, 0), "Schedule", [], summaryAttrs, "0", "Standard", []),
                new AssetV2("TK.THKLNAP", "H2_NAP", "hash_nap", new(0, 0, 0), new(0, -1000, 12500, 0), "Schedule", [], summaryAttrs, "0", "Standard", []),
                new AssetV2("TK.Tieude", "H_TD", "hash_td", new(0, 0, 0), new(0, 0, 5000, 500), "Title", [], [], "0", "Standard", []),
                new AssetV2("TK.Tenluoi", "H_TL", "hash_tl", new(0, 0, 0), new(0, 0, 3000, 300), "Title", [], [
                    new AttributeDefV2("TENLUOI", "TENLUOI", "Standard", 200, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline")
                ], "0", "Standard", []),
                new AssetV2("TieudeMC", "H3", "hash3", new(0, 0, 0), new(-500, -100, 500, 100), "Title", [], titleAttrs, "0", "Standard", []),
                new AssetV2("BLT1", "H4", "hash4", new(0, 0, 0), new(0, 0, 300, 150), "Callout", [], labelAttrs, "0", "Standard", []),
                new AssetV2("BLT2", "H5", "hash5", new(0, 0, 0), new(-300, 0, 0, 150), "Callout", [], labelAttrs, "0", "Standard", []),
                new AssetV2("Gach", "H6", "hash6", new(0, 0, 0), new(-5, -5, 5, 5), "Tick", [], [], "0", "Standard", []),
                new AssetV2("BLTC", "H7", "hash7", new(0, 0, 0), new(-10, -10, 10, 10), "Tick", [], [], "0", "Standard", []),
                new AssetV2("Block:SCR960x530", "H8", "hash8", new(0, 0, 0), new(-480, -265, 480, 265), "Product", [], [], "0", "Standard", [])
            ],
            glyphs: CreateTestGlyphs("Standard"),
            font_policy_id: "Policy01",
            units: "mm",
            products: [],
            kerning: [],
            layers: [],
            font_styles:
            [
                new FontMetricStyleV2("Standard", "Arial.ttf", "font_hash", "Unicode", "Pair", "1.0", "NFC")
            ]
        );
    }

    private static ParamV2 TextParam(string key, string text) =>
        new ParamV2(key, "Text", null, text, null, "none", []);

    private static ParamV2 NumParam(string key, double number, string unit = "mm") =>
        new ParamV2(key, "Number", number, null, null, unit, []);

    private static RulesV2 CreateTestRules()
    {
        var shapeBindings = new[]
        {
            new TagBindingV2("SH", "Mark", null, null, "none"),
            new TagBindingV2("DK", "Diameter", null, null, "mm"),
            new TagBindingV2("DAI", "MeanLength", null, null, "mm"),
            new TagBindingV2("SL", "Count", null, null, "count"),
            new TagBindingV2("TDAI", "RawSumMetres", null, null, "m"),
            new TagBindingV2("KL", "Mass", null, null, "kg")
        };

        var recipe = new ShapeRecipeV2(
            id: "Recipe01",
            component_kind: "Catch",
            family: "MainMesh",
            shape_kind: "Straight",
            corners: [],
            length_expr_id: "Formula",
            measurement_basis: "Centerline",
            native_asset: "TK.T",
            fixed_glyph_hash: "hash1",
            tag_bindings: shapeBindings,
            source_ids: ["srcRule"],
            fabrication_basis: "TrueLength",
            presentation_length_basis: "Centerline"
        );

        var ruleNR22 = new RuleV2(
            id: "NR-22",
            version: "1.0",
            status: "Approved",
            approver: "DucBao",
            approved_at_utc: "2026-10-13",
            profiles: ["Standard"],
            components: ["Catch", "MainMesh", "Body", "Neck", "Panel"],
            shape_kinds: ["Straight"],
            parameters:
            [
                TextParam("glyph_asset", "TK.T"),
                NumParam("first_row_y_native_mm", 0.0),
                NumParam("min_ink_gap_paper_mm", 2.0),
                NumParam("heading_x_native_mm.BendingTitle", 0.0),
                NumParam("heading_y_native_mm.BendingTitle", 500.0)
            ],
            expressions: [],
            shapes: [recipe],
            source_ids: ["srcRule"]
        );

        var ruleNR14 = new RuleV2(
            id: "NR-14",
            version: "1.0",
            status: "Approved",
            approver: "DucBao",
            approved_at_utc: "2026-10-13",
            profiles: ["Standard"],
            components: [],
            shape_kinds: [],
            parameters:
            [
                TextParam("summary_asset", "TK.THKL.CT")
            ],
            expressions: [],
            shapes: [],
            source_ids: ["srcRule"]
        );

        return new RulesV2(
            snapshot_hash: "rule_hash",
            // Synthetic authority for this fixture's existing tolerance values;
            // unrelated NR14/22 approvals cannot authorize CSG tolerances.
            entries: [ruleNR22, ruleNR14, new RuleV2("NR-18", "synthetic-test-only", "Approved",
                "SyntheticTestFixture", "2026-10-15T00:00:00Z", ["*"], ["*"], ["*"], [], [], [], ["W04-Test-Tolerance-Source"])],
            geometry_tolerance_mm: 1e-4,
            length_tolerance_mm: 1.0,
            paper_tolerance_mm: 0.5,
            mass_tolerance_kg: 0.01,
            basis_tolerance: 1e-6,
            angle_tolerance_rad: 1e-4,
            area_tolerance_mm2: 1.0,
            pdf_tolerance_points: 0.1
        );
    }

    [Fact]
    public void CatchAssemblyProjector_Active_ProjectsThreeViews()
    {
        var projector = new CatchAssemblyProjector();
        var effective = CreateTestEffective(catchActive: true);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var projection = projector.Project(effective, geometry, rebar, groups, assets, rules);

        Assert.NotNull(projection);
        Assert.Equal(3, projection.views.Length);

        var roles = projection.views.Select(v => v.role).ToArray();
        Assert.Contains("CatchPlan", roles);
        Assert.Contains("Catch11", roles);
        Assert.Contains("Catch22", roles);

        Assert.All(projection.views, v => Assert.True(v.required));
        Assert.NotEmpty(projection.curves);
        Assert.NotEmpty(projection.targets);
        Assert.NotEmpty(projection.opening_map);
        Assert.NotEmpty(projection.native_references);
    }

    [Fact]
    public void CatchAssemblyProjector_Inactive_ReturnsUnrequiredViews()
    {
        var projector = new CatchAssemblyProjector();
        var effective = CreateTestEffective(catchActive: false);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var projection = projector.Project(effective, geometry, rebar, groups, assets, rules);

        Assert.NotNull(projection);
        Assert.Equal(3, projection.views.Length);
        Assert.All(projection.views, v =>
        {
            Assert.False(v.required);
            Assert.Equal("CatchBasinNotConfigured", v.na_reason);
        });
    }

    [Fact]
    public void SchematicPresentationPlanner_PlansCaptions()
    {
        var planner = new SchematicPresentationPlanner();
        var effective = CreateTestEffective();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var captions = planner.PlanCaptions(effective, groups, assets, rules);

        Assert.NotNull(captions);
        Assert.Single(captions);

        var c = captions[0];
        Assert.Equal("G01", c.group_id);
        Assert.Contains("1-%%c10", c.text);
        Assert.Contains("L=600", c.text);
        Assert.NotNull(c.underline_start);
        Assert.NotNull(c.underline_end);
        Assert.True(c.ink.max_x_mm > c.ink.min_x_mm);
    }

    [Fact]
    public void NativeSteelSchedulePlanner_PlansRowsAndSummaries()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var geometry = CreateTestGeometry();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var schedules = planner.PlanRowsAndSummaries(effective, geometry, groups, assets, rules);

        Assert.NotNull(schedules);
        Assert.Single(schedules.rows);
        Assert.Equal("G01", schedules.rows[0].group_id);
        Assert.NotEmpty(schedules.headings);
        Assert.Single(schedules.captions);
        Assert.NotEmpty(schedules.summaries);
        Assert.NotEmpty(schedules.ink);
    }

    [Fact]
    public void NativeSteelSchedulePlanner_InvalidCount_ThrowsFault()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var geometry = CreateTestGeometry();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var badGroup = new GroupV2(
            id: "Bad01",
            host_id: "Catch/Body",
            face: "Outer",
            layer: "L1",
            direction: "Horizontal",
            family: "MainMesh",
            region_role: "Catch",
            opening_ids: [],
            shape_recipe_id: "Recipe01",
            diameter_mm: 10,
            member_ids: ["Occ01"],
            canonical_ids: ["Can01"],
            member_lengths_mm: [600.0],
            count: 0, // Invalid!
            raw_sum_mm: 600.0,
            mean_mm: 600.0,
            min_mm: 600.0,
            max_mm: 600.0,
            mean_roles: [],
            display_mark: "1",
            pitch_mm: 150.0,
            mass_kg: 0.37,
            measurement_basis: "Centerline",
            source_ids: [],
            material_kind: "RoundBar",
            angle_code: null,
            unit_mass_kg_per_m: null,
            member_length_basis: "Centerline",
            length_policy_id: "Policy01"
        );

        var badGroups = new GroupsV2([badGroup], "hash");

        var ex = Assert.ThrowsAny<Exception>(() =>
            planner.PlanRowsAndSummaries(effective, geometry, badGroups, assets, rules));
        Assert.Equal("DrawingStageFaultException", ex.GetType().Name);
    }

    [Fact]
    public void AnnotationLaneAllocator_PlansAnnotations()
    {
        var allocator = new AnnotationLaneAllocator();
        var projector = new CatchAssemblyProjector();
        var effective = CreateTestEffective(catchActive: true);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var projection = projector.Project(effective, geometry, rebar, groups, assets, rules);
        var annotations = allocator.Plan(effective, projection, groups, assets, rules);

        Assert.NotNull(annotations);
        Assert.NotEmpty(annotations.labels);
        Assert.NotEmpty(annotations.routes);
        Assert.NotEmpty(annotations.dimensions);
        Assert.NotEmpty(annotations.ticks);
        Assert.NotEmpty(annotations.hatches);
        Assert.NotEmpty(annotations.view_ink);
    }

    // === Targeted Finding Verification Tests ===

    [Fact]
    public void D03_ClosedTagOperations_ValidOperationsBindCorrectly()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var geometry = CreateTestGeometry();
        var assets = CreateTestAssets();

        // Test each operation in the closed set:
        // Mark, Diameter, Pitch, MeanLength, RawSumMetres, Count, Mass, MeanRole, FixedRole, Literal, SizePair
        var operations = new[]
        {
            new TagBindingV2("TAG_MARK", "Mark", null, null, "none"),
            new TagBindingV2("TAG_DIA", "Diameter", null, null, "mm"),
            new TagBindingV2("TAG_PITCH", "Pitch", null, null, "mm"),
            new TagBindingV2("TAG_MEAN", "MeanLength", null, null, "mm"),
            new TagBindingV2("TAG_RAWSUM", "RawSumMetres", null, null, "m"),
            new TagBindingV2("TAG_COUNT", "Count", null, null, "count"),
            new TagBindingV2("TAG_MASS", "Mass", null, null, "kg"),
            new TagBindingV2("TAG_ROLE", "MeanRole", "DAI", null, "mm"),
            new TagBindingV2("TAG_FIXED", "FixedRole", "CORNER", null, "mm"),
            new TagBindingV2("TAG_LIT", "Literal", null, "CustomLiteral", "none"),
            new TagBindingV2("TAG_PAIR", "SizePair", "A,B", null, "mm")
        };

        var corners = new[] { new ShapeCornerV2("CORNER", "100.0", "0", "0", 20.0, "Inside") };
        var d03Attrs = operations.Select(op => new AttributeDefV2(op.tag, "", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline")).ToArray();
        var assetD03 = new AssetV2("TK.D03", "H_D03", "hash_d03", new(0, 0, 0), new(0, -500, 12500, 0), "Schedule", [], d03Attrs, "0", "Standard", []);
        var assetsExtended = assets with { definitions = assets.definitions.Append(assetD03).ToArray() };

        var shapeRecipe = new ShapeRecipeV2("RecipeD03", "Catch", "MainMesh", "Straight", corners, "Formula", "Centerline", "TK.D03", "hash_d03", operations, ["src"], "TrueLength", "Centerline");

        var ruleNR22 = new RuleV2("NR-22", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], ["Catch", "MainMesh"], ["Straight"],
            [TextParam("glyph_asset", "TK.D03"), NumParam("first_row_y_native_mm", 0.0)], [], [shapeRecipe], ["src"]);
        var ruleNR14 = new RuleV2("NR-14", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], [], [],
            [TextParam("summary_asset", "TK.THKL.CT")], [], [], ["src"]);

        var rules = new RulesV2("h", [ruleNR22, ruleNR14], 1e-4, 1.0, 0.5, 0.01, 1e-6, 1e-4, 1.0, 0.1);

        var group = new GroupV2(
            id: "G_D03",
            host_id: "Catch/Body",
            face: "Outer",
            layer: "L1",
            direction: "Horizontal",
            family: "MainMesh",
            region_role: "Catch",
            opening_ids: [],
            shape_recipe_id: "RecipeD03",
            diameter_mm: 12,
            member_ids: ["Occ01", "Occ02"],
            canonical_ids: ["Can01", "Can02"],
            member_lengths_mm: [1200.0, 1200.0],
            count: 2,
            raw_sum_mm: 2400.0,
            mean_mm: 1200.0,
            min_mm: 1200.0,
            max_mm: 1200.0,
            mean_roles: [new RoleValueV2("DAI", 1200.0, ["src"]), new RoleValueV2("A", 400.0, ["src"]), new RoleValueV2("B", 800.0, ["src"])],
            display_mark: "2",
            pitch_mm: 200.0,
            mass_kg: 2.13,
            measurement_basis: "Centerline",
            source_ids: ["src"],
            material_kind: "RoundBar",
            angle_code: null,
            unit_mass_kg_per_m: 0.888,
            member_length_basis: "Centerline",
            length_policy_id: "Policy01"
        );

        var schedules = planner.PlanRowsAndSummaries(effective, geometry, new GroupsV2([group], "hash"), assetsExtended, rules);
        Assert.Single(schedules.rows);

        var attrs = schedules.rows[0].attributes;
        Assert.Equal("2", attrs.First(a => a.tag == "TAG_MARK").text);
        Assert.Equal("12", attrs.First(a => a.tag == "TAG_DIA").text);
        Assert.Equal("200", attrs.First(a => a.tag == "TAG_PITCH").text);
        Assert.Equal("1200", attrs.First(a => a.tag == "TAG_MEAN").text);
        Assert.Equal("2.40", attrs.First(a => a.tag == "TAG_RAWSUM").text);
        Assert.Equal("2", attrs.First(a => a.tag == "TAG_COUNT").text);
        Assert.Equal("2.13", attrs.First(a => a.tag == "TAG_MASS").text);
        Assert.Equal("1200", attrs.First(a => a.tag == "TAG_ROLE").text);
        Assert.Equal("100", attrs.First(a => a.tag == "TAG_FIXED").text);
        Assert.Equal("CustomLiteral", attrs.First(a => a.tag == "TAG_LIT").text);
        Assert.Equal("400x800", attrs.First(a => a.tag == "TAG_PAIR").text);
    }

    [Fact]
    public void D03_UnknownTagOperation_ThrowsFault()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var geometry = CreateTestGeometry();
        var assets = CreateTestAssets();

        var badBinding = new[] { new TagBindingV2("BAD_TAG", "UnknownOperation", null, null, "none") };
        var shapeRecipe = new ShapeRecipeV2("RecipeBad", "Catch", "MainMesh", "Straight", [], "Formula", "Centerline", "TK.T", "hash1", badBinding, ["src"], "TrueLength", "Centerline");
        var ruleNR22 = new RuleV2("NR-22", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], ["Catch", "MainMesh"], ["Straight"],
            [TextParam("glyph_asset", "TK.T"), NumParam("first_row_y_native_mm", 0.0)], [], [shapeRecipe], ["src"]);
        var ruleNR14 = new RuleV2("NR-14", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], [], [],
            [TextParam("summary_asset", "TK.THKL.CT")], [], [], ["src"]);
        var rules = new RulesV2("h", [ruleNR22, ruleNR14], 1e-4, 1.0, 0.5, 0.01, 1e-6, 1e-4, 1.0, 0.1);

        var group = CreateTestGroups().items[0] with { shape_recipe_id = "RecipeBad" };

        var ex = Assert.ThrowsAny<Exception>(() =>
            planner.PlanRowsAndSummaries(effective, geometry, new GroupsV2([group], "hash"), assets, rules));
        Assert.Equal("DrawingStageFaultException", ex.GetType().Name);
        Assert.Contains("TK_TAG_OPERATION_UNSUPPORTED", ex.Message);
    }

    [Fact]
    public void D04_MissingExactAsset_ThrowsFault()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var geometry = CreateTestGeometry();
        var groups = CreateTestGroups();
        var rules = CreateTestRules();

        // Assets without TK.T definition
        var emptyAssets = new AssetsV2(
            bundle_hash: "hash",
            definitions: [],
            glyphs: [],
            font_policy_id: "P",
            units: "mm",
            products: [],
            kerning: [],
            layers: [],
            font_styles: []
        );

        var ex = Assert.ThrowsAny<Exception>(() =>
            planner.PlanRowsAndSummaries(effective, geometry, groups, emptyAssets, rules));
        Assert.Equal("DrawingStageFaultException", ex.GetType().Name);
        Assert.Contains("ASSET_EXACT_NAME_MISSING", ex.Message);
    }

    [Fact]
    public void D05_NativeInkMeasurement_ComputesAccurateBoundsAndFaultsOnMissingGlyph()
    {
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var assetDef = assets.definitions.First(a => a.name == "BLT1");
        var attrs = new[]
        {
            new BoundAttributeV2("NOTE", "Đoạn thép 1-%%c12", "Text", null, "none", [])
        };

        var ink = AnnotationLaneAllocator.MeasureAssetInk(assetDef, attrs, assets, rules);
        Assert.True(ink.max_x_mm > ink.min_x_mm);
        Assert.True(ink.max_y_mm > ink.min_y_mm);

        // Test missing glyph metric fault
        var assetsWithoutGlyphs = assets with { glyphs = [] };
        var ex = Assert.ThrowsAny<Exception>(() =>
            AnnotationLaneAllocator.MeasureAssetInk(assetDef, attrs, assetsWithoutGlyphs, rules));
        Assert.Equal("DrawingStageFaultException", ex.GetType().Name);
        Assert.Contains("GLYPH_METRIC_MISSING", ex.Message);
    }

    [Fact]
    public void D06_FiniteLaneAllocation_ExhaustedCandidates_ThrowsLayoutNoFit()
    {
        var allocator = new AnnotationLaneAllocator();
        var effective = CreateTestEffective(catchActive: false);
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var view = new ViewV2("V01", "StructurePlan", "M01", new FrameV2("F", new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), 1), 0.0, 30.0, false, true, null, []);

        // Huge obstacle completely blocking all candidate lanes on Right side
        var hugeObstacle = new ProjectedCurveV2(
            id: "BlockAll",
            view_id: "V01",
            source_id: "Obs",
            group_id: null,
            kind: "Line",
            start: new Point2V2(-1000, -2000),
            end: new Point2V2(5000, 2000),
            center: null,
            radius_mm: null,
            sweep_rad: null,
            visibility: "Visible",
            role: "Geometry",
            projection_frame_id: "F"
        );

        var target = new Target2V2("T01", "V01", "Occ01", "G01", "Bar", new Point2V2(100, 100), ["BlockAll"], [], true);
        var proj = new ProjectionV2([view], [hugeObstacle], [target], [], [], []);
        var groups = CreateTestGroups();

        var ex = Assert.ThrowsAny<Exception>(() => allocator.Plan(effective, proj, groups, assets, rules));
        Assert.Equal("DrawingStageFaultException", ex.GetType().Name);
        Assert.Contains("LAYOUT_ANNOTATION_NO_FIT", ex.Message);
    }

    [Fact]
    public void D07_PhysicalAssociation_TicksHavePhysicalOccurrenceIdsAndFullEnvelope()
    {
        var allocator = new AnnotationLaneAllocator();
        var projector = new CatchAssemblyProjector();
        var effective = CreateTestEffective(catchActive: true);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();

        var projection = projector.Project(effective, geometry, rebar, groups, assets, rules);
        var annotations = allocator.Plan(effective, projection, groups, assets, rules);

        // Every MainMesh tick has occurrence_id equal to real member occurrence ID (Occ01)
        var mainTicks = annotations.ticks.Where(t => t.asset_name == "Gach").ToList();
        Assert.NotEmpty(mainTicks);
        Assert.All(mainTicks, t => Assert.Equal("Occ01", t.occurrence_id));

        // View ink envelope encompasses all curves and annotations
        foreach (var vi in annotations.view_ink)
        {
            var viewCurves = projection.curves.Where(c => c.view_id == vi.view_id).ToList();
            foreach (var c in viewCurves)
            {
                Assert.True(vi.bounds.min_x_mm <= Math.Min(c.start.x_mm, c.end.x_mm) + 1e-3);
                Assert.True(vi.bounds.max_x_mm >= Math.Max(c.start.x_mm, c.end.x_mm) - 1e-3);
            }
        }
    }

    [Fact]
    public void D09_PrefixedComponentOwnership_DAY_TUONG_TRAN_CO_MatchRawQuantities()
    {
        var planner = new NativeSteelSchedulePlanner();
        var effective = CreateTestEffective();
        var assets = CreateTestAssets();

        // Components with prefixed IDs: MH1:Base, MH1:Wall, MH1:Roof, MH1:Neck
        var frame = new FrameV2("F0", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);
        var loop = new LoopV2("L0", "Outer", [new CurveV2("C", "Line", new(0, 0, 0), new(1, 1, 0), null, null, null, null, "Bottom")], true, 1.0);

        var components = new[]
        {
            new ComponentV2("MH1:Base", "Base", "A", "Mat", frame, [loop], 0, 200, 200, [], [], [], [], "Base", "Concrete"),
            new ComponentV2("MH1:Wall", "Wall", "A", "Mat", frame, [loop], 0, 200, 200, [], [], [], [], "Wall", "Concrete"),
            new ComponentV2("MH1:Roof", "Roof", "A", "Mat", frame, [loop], 0, 200, 200, [], [], [], [], "Roof", "Concrete"),
            new ComponentV2("MH1:Neck", "Neck", "A", "Mat", frame, [loop], 0, 200, 200, [], [], [], [], "Neck", "Concrete")
        };

        var geometry = new GeometryV2(components, [], [], "geom_hash", []);

        // Create groups owned by each prefixed component
        var gBase = new GroupV2("G_Base", "MH1:Base", "Outer", "L1", "H", "MainMesh", "Body", [], "Recipe01", 10, ["O1"], ["C1"], [1000.0], 1, 1000.0, 1000.0, 1000.0, 1000.0, [new("DAI", 1000.0, [])], "1", 150.0, 10.0, "Centerline", [], "RoundBar", null, 0.617, "Centerline", "P1");
        var gWall = new GroupV2("G_Wall", "MH1:Wall", "Outer", "L1", "H", "MainMesh", "Body", [], "Recipe01", 12, ["O2"], ["C2"], [1000.0], 1, 1000.0, 1000.0, 1000.0, 1000.0, [new("DAI", 1000.0, [])], "2", 150.0, 20.0, "Centerline", [], "RoundBar", null, 0.888, "Centerline", "P1");
        var gRoof = new GroupV2("G_Roof", "MH1:Roof", "Outer", "L1", "H", "MainMesh", "Body", [], "Recipe01", 10, ["O3"], ["C3"], [1000.0], 1, 1000.0, 1000.0, 1000.0, 1000.0, [new("DAI", 1000.0, [])], "3", 150.0, 30.0, "Centerline", [], "RoundBar", null, 0.617, "Centerline", "P1");
        var gNeck = new GroupV2("G_Neck", "MH1:Neck", "Outer", "L1", "H", "MainMesh", "Neck", [], "Recipe01", 14, ["O4"], ["C4"], [1000.0], 1, 1000.0, 1000.0, 1000.0, 1000.0, [new("DAI", 1000.0, [])], "4", 150.0, 40.0, "Centerline", [], "RoundBar", null, 1.208, "Centerline", "P1");

        // Scope recipe for Wall/Base/Roof/Neck
        var d09Bindings = new[]
        {
            new TagBindingV2("SH", "Mark", null, null, "none"),
            new TagBindingV2("DK", "Diameter", null, null, "mm"),
            new TagBindingV2("DAI", "MeanLength", null, null, "mm"),
            new TagBindingV2("SL", "Count", null, null, "count"),
            new TagBindingV2("TDAI", "RawSumMetres", null, null, "m"),
            new TagBindingV2("KL", "Mass", null, null, "kg")
        };
        var baseRecipe = new ShapeRecipeV2("Recipe01", "Base", "MainMesh", "Straight", [], "Formula", "Centerline", "TK.T", "hash1", d09Bindings, [], "TrueLength", "Centerline");
        var wallRecipe = new ShapeRecipeV2("Recipe01", "Wall", "MainMesh", "Straight", [], "Formula", "Centerline", "TK.T", "hash1", d09Bindings, [], "TrueLength", "Centerline");
        var roofRecipe = new ShapeRecipeV2("Recipe01", "Roof", "MainMesh", "Straight", [], "Formula", "Centerline", "TK.T", "hash1", d09Bindings, [], "TrueLength", "Centerline");
        var neckRecipe = new ShapeRecipeV2("Recipe01", "Neck", "MainMesh", "Straight", [], "Formula", "Centerline", "TK.T", "hash1", d09Bindings, [], "TrueLength", "Centerline");

        var ruleNR22 = new RuleV2("NR-22", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], ["Base", "Wall", "Roof", "Neck", "MainMesh"], ["Straight"],
            [TextParam("glyph_asset", "TK.T"), NumParam("first_row_y_native_mm", 0.0)], [],
            [baseRecipe, wallRecipe, roofRecipe, neckRecipe], []);
        var ruleNR14 = new RuleV2("NR-14", "1.0", "Approved", "DucBao", "2026-10-13", ["Standard"], [], [],
            [TextParam("summary_asset", "TK.THKL.CT")], [], [], []);
        var rulesExtended = new RulesV2("h", [ruleNR22, ruleNR14], 1e-4, 1.0, 0.5, 0.01, 1e-6, 1e-4, 1.0, 0.1);

        var groupsV2 = new GroupsV2([gBase, gWall, gRoof, gNeck], "hash");
        var schedules = planner.PlanRowsAndSummaries(effective, geometry, groupsV2, assets, rulesExtended);

        var summary = schedules.summaries.First();
        var attrs = summary.attributes.ToDictionary(a => a.tag, a => double.Parse(a.text, System.Globalization.CultureInfo.InvariantCulture));

        // DAY1 (dia <= 10): 10.0 kg; DAY2 (dia > 10): 0.0 kg
        Assert.Equal(10.0, attrs["DAY1"]);
        Assert.Equal(0.0, attrs["DAY2"]);

        // TUONG1 (dia <= 10): 0.0 kg; TUONG2 (dia > 10): 20.0 kg
        Assert.Equal(0.0, attrs["TUONG1"]);
        Assert.Equal(20.0, attrs["TUONG2"]);

        // TRAN1 (dia <= 10): 30.0 kg; TRAN2 (dia > 10): 0.0 kg
        Assert.Equal(30.0, attrs["TRAN1"]);
        Assert.Equal(0.0, attrs["TRAN2"]);

        // CO1 (dia <= 10): 0.0 kg; CO2 (dia > 10): 40.0 kg
        Assert.Equal(0.0, attrs["CO1"]);
        Assert.Equal(40.0, attrs["CO2"]);

        // TONG: 10 + 20 + 30 + 40 = 100.0 kg
        Assert.Equal(100.0, attrs["TONG"]);
    }
}
