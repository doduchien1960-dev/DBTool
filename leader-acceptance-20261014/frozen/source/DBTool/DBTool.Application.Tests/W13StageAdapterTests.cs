#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBTool.Application;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class W13StageAdapterTests
{
    [Fact]
    public void S10StageAdapter_ImplementsIS10StageV2()
    {
        var adapter = new S10StageAdapter();
        Assert.IsAssignableFrom<IS10StageV2>(adapter);
    }

    [Fact]
    public void S10StageAdapter_DefaultPort_ProducesSimulatedCommittedState()
    {
        var adapter = new S10StageAdapter();
        var s9 = CreateMockS9State();

        var s10 = adapter.Execute(s9);

        Assert.NotNull(s10);
        Assert.Equal("Committed", s10.native.write_status);
        Assert.Equal("Passed", s10.native.readback_status);
        Assert.Equal(s9.geometry.physical_hash, s10.native.normalized_geometry_hash);
    }

    [Fact]
    public void S10StageAdapter_CustomPort_ProducesPortNativeState()
    {
        var fakePort = new FakeNativeGenerationPort();
        var adapter = new S10StageAdapter(fakePort);
        var s9 = CreateMockS9State();

        var s10 = adapter.Execute(s9);

        Assert.NotNull(s10);
        Assert.Equal("CustomCommitted", s10.native.write_status);
        Assert.Equal("Passed", s10.native.readback_status);
        Assert.Equal("custom_saved_hash", s10.native.saved_dwg_sha256);
    }

    [Fact]
    public void S11StageAdapter_ImplementsIS11StageV2()
    {
        var adapter = new S11StageAdapter();
        Assert.IsAssignableFrom<IS11StageV2>(adapter);
    }

    [Fact]
    public void S11StageAdapter_Execute_ProducesBatchV2WithStatus()
    {
        var adapter = new S11StageAdapter();
        var s9 = CreateMockS9State();
        var s10 = new S10StageAdapter().Execute(s9);

        var s11 = adapter.Execute(s10);

        Assert.NotNull(s11);
        Assert.NotNull(s11.batch);
        Assert.NotEmpty(s11.batch.rows);
        Assert.Equal("Partial", s11.batch.status);
    }

    [Fact]
    public void ProductionDispatcher_ImplementsIStagePipelineV2()
    {
        var dispatcher = new ProductionDispatcher();
        Assert.IsAssignableFrom<IStagePipelineV2>(dispatcher);
    }

    private static S9StateV2 CreateMockS9State()
    {
        var context = new ContextV2(
            run_id: "Run01",
            job_id: "Job01",
            manhole_id: "MH01",
            row_index: 1,
            build_hash: "build_hash",
            template_hash: "template_hash",
            rule_set_hash: "rule_hash",
            cancel_requested: false,
            sources: [],
            diagnostics: [],
            receipts: [],
            artifact_directory: Path.GetTempPath(),
            prior_row_results: [],
            prior_plot_pages: [],
            plot_session_id: "SESSION-01"
        );

        var request = new RequestV2(
            jobs: [],
            target_dwg_path: "output.dwg",
            generation_mode: "Production",
            anchor: new Point3V2(0, 0, 0),
            body_scale: 30.0,
            panel_scale: 20.0,
            scale_mode: "Fixed",
            elevation_mode_override: null,
            draw_scope: "FullRebar",
            max_numeric_count: 100,
            plot_profile_id: "Default",
            template_manifest_path: "manifest.json",
            template_manifest_sha256: "manifest_hash",
            rule_snapshot_path: "rules.json",
            rule_snapshot_sha256: "rules_hash",
            reference_bundle_ids: ["BUNDLE-01"]
        );

        var assets = new AssetsV2(
            bundle_hash: "asset_hash",
            definitions:
            [
                new AssetV2("A3_Border", "H01", "block_hash", new(0, 0, 0), new Bounds2V2(300, 300, 12300, 8610), "Border", [], [], "0", null, [])
            ],
            glyphs: [],
            font_policy_id: "Policy01",
            units: "mm",
            products: [],
            kerning: [],
            layers: [],
            font_styles: []
        );

        var rules = new RulesV2(
            snapshot_hash: "rules_hash",
            entries:
            [
                new RuleV2("NR-22", "1.0", "Approved", "DucBao", "2026-10-13", ["GaThamLienKhoi"], [], [], [
                    new ParamV2("view_gap_x_paper_mm", "Number", 10.0, null, null, "paper_mm", []),
                    new ParamV2("view_gap_y_paper_mm", "Number", 10.0, null, null, "paper_mm", []),
                    new ParamV2("frame_asset.Structure", "Text", null, "A3_Border", null, "none", [])
                ], [], [], [])
            ],
            geometry_tolerance_mm: 1e-4,
            length_tolerance_mm: 1.0,
            paper_tolerance_mm: 5.0,
            mass_tolerance_kg: 0.01,
            basis_tolerance: 1e-6,
            angle_tolerance_rad: 1e-4,
            area_tolerance_mm2: 1.0,
            pdf_tolerance_points: 0.1
        );

        var raw = new RawV2("wb-hash", "GaThamLienKhoi", "Sheet1", 0, [], [], [], "v1");

        var effective = new EffectiveV2(
            id: "MH01",
            display_name: "Ga G500-2",
            profile: "GaThamLienKhoi",
            clear_x_mm: 1150,
            clear_y_mm: 1350,
            clear_h_mm: 1500,
            base_mm: 200,
            wall_mm: 150,
            roof_mm: 150,
            base_expansion_mm: 100,
            pad_mm: 100,
            pad_expansion_mm: 100,
            base_haunch_mm: 0,
            top_haunch_mm: 0,
            bedding_mortar_mm: 20,
            neck_active: false,
            neck_clear_x_mm: 700,
            neck_clear_y_mm: 700,
            neck_raw_clear_x_mm: null,
            neck_raw_clear_y_mm: null,
            neck_wall_mm: 125,
            neck_height_mm: 0,
            neck_position: "Center",
            neck_center: new Point2V2(0, 0),
            neck_reinforced: false,
            cover_kind: "None",
            cover_code: null,
            product_rotation_rad: 0,
            frame_outer_x_mm: 0,
            frame_outer_y_mm: 0,
            frame_height_mm: 0,
            base_elevation_m: null,
            elevation_mode: "Symbolic",
            body_scale: 30,
            panel_scale: null,
            note_style: "Standard",
            pitch_symbol: "@",
            datums: [],
            members: [],
            pipes: [],
            @catch: null,
            panel: null,
            source_ids: [],
            parsed_fields: [],
            contact_equations: []
        );

        var geometry = new GeometryV2([], [], [], "geo_phys_hash", []);
        var rebar = new RebarV2([], [], [], [], [], "rebar_phys_hash", [], "rebar_fab_hash");
        var groups = new GroupsV2([], "grp_hash");

        var worldFrame = new FrameV2("World", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);

        var viewsList = new List<ViewV2>
        {
            new("Plan", "Structure", "MH01", worldFrame, null, 30.0, false, true, null, []),
            new("AA", "Structure", "MH01", worldFrame, null, 30.0, false, true, null, [])
        };

        var projection = new ProjectionV2(
            views: viewsList.ToArray(),
            curves: [],
            targets: [],
            opening_map: [],
            regions: [],
            native_references: []
        );

        var annotations = new AnnotationsV2(
            labels: [],
            routes: [],
            dimensions: [],
            ticks: [],
            view_ink: [
                new("Plan", new Bounds2V2(0, 0, 1000, 1000)),
                new("AA", new Bounds2V2(0, 0, 1000, 1000))
            ],
            hatches: []
        );

        var schedules = new SchedulesV2(
            rows: [],
            captions: [],
            summaries: [],
            ink: [],
            headings: []
        );

        var s8 = new S8StateV2(
            context,
            request,
            assets,
            rules,
            raw,
            effective,
            geometry,
            rebar,
            groups,
            projection,
            annotations,
            schedules
        );

        return new DrawingStageAdapter().Execute(s8);
    }

    private sealed class FakeNativeGenerationPort : INativeGenerationPort
    {
        public NativeV2 Write(S9StateV2 input, System.Threading.CancellationToken token)
        {
            var h = new HandleV2("S_01", "10A", "BLOCK_A", [], [], "MH01");
            return new NativeV2(
                saved_dwg_path: input.request.target_dwg_path,
                saved_dwg_sha256: "custom_saved_hash",
                handles: [h],
                normalized_geometry_hash: input.geometry.physical_hash,
                normalized_rebar_hash: input.rebar.physical_hash,
                repository_version: "generation-xdata/v1",
                write_status: "CustomCommitted",
                readback_status: "Passed",
                unchanged_manhole_ids: [],
                unchanged_before_hashes: [],
                unchanged_after_hashes: [],
                domain_geometry_hash: input.rebar.fabrication_geometry_hash,
                expected_projection_hash: input.sheet.sheet_hash,
                commit_state: "Reopened"
            );
        }
    }
}
