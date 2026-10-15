#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DBTool.Application;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class W10StageAdapterTests
{
    [Fact]
    public void DrawingStageAdapter_ImplementsIS6StageV2_AndIS7StageV2()
    {
        var adapter = new DrawingStageAdapter();
        Assert.IsAssignableFrom<IS6StageV2>(adapter);
        Assert.IsAssignableFrom<IS7StageV2>(adapter);
    }

    [Fact]
    public void DrawingStageAdapter_ExecuteS6_WithActiveCatch_ProducesProjectionV2()
    {
        var adapter = new DrawingStageAdapter();
        var context = CreateTestContext();
        var request = CreateTestRequest();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();
        var raw = CreateTestRaw();
        var effective = CreateTestEffective(catchActive: true);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();

        var s5 = new S5StateV2(context, request, assets, rules, raw, effective, geometry, rebar, groups);
        var s6 = adapter.Execute(s5);

        Assert.NotNull(s6);
        Assert.NotNull(s6.projection);
        Assert.Equal(3, s6.projection.views.Length);
        Assert.All(s6.projection.views, v => Assert.True(v.required));
        Assert.NotEmpty(s6.projection.curves);
        Assert.NotEmpty(s6.projection.targets);
        Assert.NotEmpty(s6.projection.opening_map);
    }

    [Fact]
    public void DrawingStageAdapter_ExecuteS6_WithInactiveCatch_ProducesUnrequiredViews()
    {
        var adapter = new DrawingStageAdapter();
        var context = CreateTestContext();
        var request = CreateTestRequest();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();
        var raw = CreateTestRaw();
        var effective = CreateTestEffective(catchActive: false);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();

        var s5 = new S5StateV2(context, request, assets, rules, raw, effective, geometry, rebar, groups);
        var s6 = adapter.Execute(s5);

        Assert.NotNull(s6);
        Assert.NotNull(s6.projection);
        Assert.Equal(3, s6.projection.views.Length);
        Assert.All(s6.projection.views, v =>
        {
            Assert.False(v.required);
            Assert.Equal("CatchBasinNotConfigured", v.na_reason);
        });
    }

    [Fact]
    public void DrawingStageAdapter_ExecuteS7_ProducesAnnotationsV2()
    {
        var adapter = new DrawingStageAdapter();
        var context = CreateTestContext();
        var request = CreateTestRequest();
        var assets = CreateTestAssets();
        var rules = CreateTestRules();
        var raw = CreateTestRaw();
        var effective = CreateTestEffective(catchActive: true);
        var geometry = CreateTestGeometry();
        var rebar = CreateTestRebar();
        var groups = CreateTestGroups();

        var s5 = new S5StateV2(context, request, assets, rules, raw, effective, geometry, rebar, groups);
        var s6 = adapter.Execute(s5);
        var s7 = adapter.Execute(s6);

        Assert.NotNull(s7);
        Assert.NotNull(s7.annotations);
        Assert.NotEmpty(s7.annotations.labels);
        Assert.NotEmpty(s7.annotations.routes);
        Assert.NotEmpty(s7.annotations.dimensions);
        Assert.NotEmpty(s7.annotations.ticks);
        Assert.NotEmpty(s7.annotations.hatches);
        Assert.NotEmpty(s7.annotations.view_ink);
    }

    private static ContextV2 CreateTestContext() => new(
        run_id: "run-w10",
        job_id: "job-w10",
        manhole_id: "MH1",
        row_index: 0,
        build_hash: "b-hash",
        template_hash: "t-hash",
        rule_set_hash: "r-hash",
        cancel_requested: false,
        sources: [],
        diagnostics: [],
        receipts: [],
        artifact_directory: "artifacts",
        prior_row_results: [],
        prior_plot_pages: [],
        plot_session_id: null
    );

    private static RequestV2 CreateTestRequest() => new(
        jobs: [],
        target_dwg_path: "out.dwg",
        generation_mode: "Clean",
        anchor: new Point3V2(0, 0, 0),
        body_scale: 30,
        panel_scale: null,
        scale_mode: "Fixed",
        elevation_mode_override: null,
        draw_scope: "Full",
        max_numeric_count: 1000,
        plot_profile_id: "default",
        template_manifest_path: "manifest.json",
        template_manifest_sha256: "thash",
        rule_snapshot_path: "rules.json",
        rule_snapshot_sha256: "rhash",
        reference_bundle_ids: []
    );

    private static AssetsV2 CreateTestAssets()
    {
        return new AssetsV2(
            bundle_hash: "asset_hash",
            definitions:
            [
                new AssetV2("TK.T", "H1", "hash1", new(0, 0, 0), new(0, -500, 12500, 0), "Schedule", [], [], "0", "Standard", []),
                new AssetV2("TK.THKL.CT", "H2", "hash2", new(0, 0, 0), new(0, -1000, 12500, 0), "Schedule", [], [], "0", "Standard", []),
                new AssetV2("TieudeMC", "H3", "hash3", new(0, 0, 0), new(-500, -100, 500, 100), "Title", [], [], "0", "Standard", []),
                new AssetV2("BLT1", "H4", "hash4", new(0, 0, 0), new(0, 0, 300, 150), "Callout", [], [], "0", "Standard", [])
            ],
            glyphs: [],
            font_policy_id: "Policy01",
            units: "mm",
            products: [],
            kerning: [],
            layers: [],
            font_styles: []
        );
    }

    private static RulesV2 CreateTestRules()
    {
        return new RulesV2(
            snapshot_hash: "rule_hash",
            // Synthetic fixture authority for the existing explicit tolerances below.
            // Production still blocks missing tolerance sources.
            entries: [new RuleV2("NR-18","synthetic-test-only","Approved",
                "W10 Test Fixture","2026-10-15",["*"],["*"],["*"],[],[],[],["W10-SYNTHETIC-TOLERANCE"])],
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

    private static RawV2 CreateTestRaw() => new("wb-hash", "GaThamThuNapRoi", "Sheet1", 0, [], [], [], "v1");

    private static EffectiveV2 CreateTestEffective(bool catchActive)
    {
        var catchSpec = new CatchSpecV2(
            active: catchActive,
            owner: "Neck",
            wall: "Wall/T1",
            position: "Left",
            raw_position: "Left",
            offset_u_mm: 0,
            opening_width_mm: 500,
            opening_height_mm: 300,
            clear_depth_mm: 400,
            clear_height_mm: 600,
            wall_mm: 150,
            base_mm: 150,
            pad_mm: 100,
            pad_expansion_mm: 100,
            reinforced: true,
            product_code: "SCR",
            source_ids: []
        );

        return new EffectiveV2(
            id: "MH1",
            display_name: "MH1",
            profile: "GaThamThuNapRoi",
            clear_x_mm: 1000,
            clear_y_mm: 1000,
            clear_h_mm: 1500,
            base_mm: 200,
            wall_mm: 150,
            roof_mm: 150,
            base_expansion_mm: 100,
            pad_mm: 100,
            pad_expansion_mm: 100,
            base_haunch_mm: 100,
            top_haunch_mm: 100,
            bedding_mortar_mm: 20,
            neck_active: true,
            neck_clear_x_mm: 700,
            neck_clear_y_mm: 700,
            neck_raw_clear_x_mm: null,
            neck_raw_clear_y_mm: null,
            neck_wall_mm: 125,
            neck_height_mm: 500,
            neck_position: "Center",
            neck_center: new Point2V2(0, 0),
            neck_reinforced: true,
            cover_kind: "None",
            cover_code: null,
            product_rotation_rad: 0,
            frame_outer_x_mm: 950,
            frame_outer_y_mm: 950,
            frame_height_mm: 100,
            base_elevation_m: null,
            elevation_mode: "Symbolic",
            body_scale: 30,
            panel_scale: null,
            note_style: "NoteStyle1",
            pitch_symbol: "@",
            datums: [],
            members: [],
            pipes: [],
            @catch: catchSpec,
            panel: null,
            source_ids: [],
            parsed_fields: [],
            contact_equations: []
        );
    }

    private static GeometryV2 CreateTestGeometry()
    {
        var frame = new FrameV2("World", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);
        var loop = new LoopV2("L0", "Outer",
        [
            new CurveV2("C0", "Line", new Point3V2(-400, -300, 0), new Point3V2(400, -300, 0), null, null, null, null, "Outer"),
            new CurveV2("C1", "Line", new Point3V2(400, -300, 0), new Point3V2(400, 300, 0), null, null, null, null, "Outer"),
            new CurveV2("C2", "Line", new Point3V2(400, 300, 0), new Point3V2(-400, 300, 0), null, null, null, null, "Outer"),
            new CurveV2("C3", "Line", new Point3V2(-400, 300, 0), new Point3V2(-400, -300, 0), null, null, null, null, "Outer")
        ], true, 800.0 * 600.0);

        var comp = new ComponentV2(
            id: "Catch/Body",
            kind: "Catch",
            assembly_id: "CatchAssembly",
            material_id: "Concrete",
            frame: frame,
            loops: [loop],
            z_min_mm: -200.0,
            z_max_mm: 600.0,
            thickness_mm: 150.0,
            patches: [],
            void_ids: [],
            source_ids: ["srcComp"],
            solid_parts: [],
            local_key: "Catch/Body",
            material_role: "Concrete"
        );

        var opening = new OpeningV2(
            id: "Catch/Opening",
            kind: "Catch",
            host_ids: ["Catch/Body"],
            frame: frame,
            inner: [loop],
            pipe_outer: [],
            concrete_cutout: [loop],
            depth_min_mm: 0.0,
            depth_max_mm: 150.0,
            invert_mm: 0.0,
            clearance_rule_id: null,
            source_ids: ["srcOpen"]
        );

        var product = new ProductPlacementV2(
            id: "Prod01",
            contract_id: "SCR960x530",
            anchor: new Point3V2(0, 0, 600.0),
            rotation_rad: 0.0,
            physical_scale: 1.0,
            plan_asset: "Block:SCR960x530",
            section_aa_asset: "Block:NAPCUATHU1",
            section_bb_asset: "Block:NAPCUATHU2",
            source_ids: ["srcProd"]
        );

        return new GeometryV2([comp], [opening], [], "geo_hash", [product]);
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
}
