#nullable enable
using System;
using System.Collections.Generic;
using DBTool.Application;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class W09StageAdapterTests
{
    [Fact]
    public void SteelScheduleStageAdapter_ImplementsIS8StageV2_AndHandlesGeometryOnlyScope()
    {
        var adapter = new SteelScheduleStageAdapter();
        Assert.IsAssignableFrom<IS8StageV2>(adapter);

        var context = new ContextV2(
            run_id: "run-1",
            job_id: "job-1",
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

        var request = new RequestV2(
            jobs: [],
            target_dwg_path: "out.dwg",
            generation_mode: "Clean",
            anchor: new Point3V2(0, 0, 0),
            body_scale: 30,
            panel_scale: null,
            scale_mode: "Fixed",
            elevation_mode_override: null,
            draw_scope: "GeometryOnly",
            max_numeric_count: 1000,
            plot_profile_id: "default",
            template_manifest_path: "manifest.json",
            template_manifest_sha256: "thash",
            rule_snapshot_path: "rules.json",
            rule_snapshot_sha256: "rhash",
            reference_bundle_ids: []
        );

        var assets = new AssetsV2("ahash", [], [], "policy", "mm", [], [], [], []);
        var rules = new RulesV2("rhash", [], null, null, null, null, null, null, null, null);
        var raw = new RawV2("wbhash", "GaThamLienKhoi", "Sheet1", 0, [], [], [], "v1");
        var effective = new EffectiveV2("MH1", "MH1", "GaThamLienKhoi", 1000, 1000, 1500, 200, 150, 150, 100, 0, 0, 0, 0, 0, false, 700, 700, null, null, 125, 0, "Center", new Point2V2(0, 0), false, "None", null, 0, 950, 950, 100, null, "Symbolic", 30, null, "NoteStyle1", "@", [], [], [], null, null, [], [], []);
        var geometry = new GeometryV2([], [], [], "geomhash", []);
        var rebar = new RebarV2([], [], [], [], [], "rebarhash", [], "fabhash");
        var groups = new GroupsV2([], "grouphash");
        var projection = new ProjectionV2([], [], [], [], [], []);
        var annotations = new AnnotationsV2([], [], [], [], [], []);

        var s7State = new S7StateV2(context, request, assets, rules, raw, effective, geometry, rebar, groups, projection, annotations);
        var s8State = adapter.Execute(s7State);

        Assert.NotNull(s8State);
        Assert.NotNull(s8State.schedules);
        Assert.Empty(s8State.schedules.rows);
        Assert.Empty(s8State.schedules.captions);
        Assert.Empty(s8State.schedules.headings);
        Assert.Empty(s8State.schedules.summaries);
        Assert.Empty(s8State.schedules.ink);
    }
}
