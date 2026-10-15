#nullable enable
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
namespace DBTool.Application.StageContractsV2;

public sealed record ContextV2(
    string run_id /* — */,
    string job_id /* — */,
    string manhole_id /* — */,
    int row_index /* row */,
    string build_hash /* — */,
    string template_hash /* — */,
    string rule_set_hash /* — */,
    bool cancel_requested /* — */,
    SourceV2[] sources /* — */,
    DiagnosticV2[] diagnostics /* — */,
    ReceiptV2[] receipts /* — */,
    string artifact_directory /* — */,
    RowVerdictV2[] prior_row_results /* — */,
    PlotPageV2[] prior_plot_pages /* — */,
    string? plot_session_id /* — */
);

public sealed record TargetV2(
    int row_index /* row */,
    string manhole_id /* — */,
    string? expected_generation_id /* — */,
    string? expected_input_hash /* — */,
    string? expected_document_token /* — */,
    string? requested_generation_id /* — */
);

public sealed record JobV2(
    string job_id /* — */,
    string logical_source_id /* — */,
    string workbook_path /* — */,
    byte[] workbook_bytes /* — */,
    string workbook_sha256 /* — */,
    string profile /* — */,
    string main_sheet /* — */,
    string numeric_culture /* — */,
    TargetV2[] targets /* — */,
    string? recalculation_receipt_path /* — */,
    string? recalculation_receipt_sha256 /* — */
);

public sealed record RequestV2(
    JobV2[] jobs /* — */,
    string target_dwg_path /* — */,
    string generation_mode /* — */,
    Point3V2 anchor /* mm */,
    double body_scale /* 1:S */,
    double? panel_scale /* 1:S */,
    string scale_mode /* — */,
    string? elevation_mode_override /* — */,
    string draw_scope /* — */,
    int max_numeric_count /* count */,
    string plot_profile_id /* — */,
    string template_manifest_path /* — */,
    string template_manifest_sha256 /* — */,
    string rule_snapshot_path /* — */,
    string rule_snapshot_sha256 /* — */,
    string[] reference_bundle_ids /* — */
);

public sealed record FailureV2(
    DiagnosticV2 diagnostic /* — */,
    ReceiptV2 receipt /* — */
);

public sealed record EntryStateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */
);

public sealed record S0StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */
);

public sealed record S1StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */
);

public sealed record S2StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */
);

public sealed record S3StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */
);

public sealed record S4StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */
);

public sealed record S5StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */
);

public sealed record S6StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */
);

public sealed record S7StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */,
    AnnotationsV2 annotations /* — */
);

public sealed record S8StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */,
    AnnotationsV2 annotations /* — */,
    SchedulesV2 schedules /* — */
);

public sealed record S9StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */,
    AnnotationsV2 annotations /* — */,
    SchedulesV2 schedules /* — */,
    SheetV2 sheet /* — */
);

public sealed record S10StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */,
    AnnotationsV2 annotations /* — */,
    SchedulesV2 schedules /* — */,
    SheetV2 sheet /* — */,
    NativeV2 native /* — */
);

public sealed record S11StateV2(
    ContextV2 context /* — */,
    RequestV2 request /* — */,
    AssetsV2 assets /* — */,
    RulesV2 rules /* — */,
    RawV2 raw /* — */,
    EffectiveV2 effective /* — */,
    GeometryV2 geometry /* — */,
    RebarV2 rebar /* — */,
    GroupsV2 groups /* — */,
    ProjectionV2 projection /* — */,
    AnnotationsV2 annotations /* — */,
    SchedulesV2 schedules /* — */,
    SheetV2 sheet /* — */,
    NativeV2 native /* — */,
    BatchV2 batch /* — */
);

public interface IS0StageV2 { S0StateV2 Execute(EntryStateV2 input); }

public interface IS1StageV2 { S1StateV2 Execute(S0StateV2 input); }

public interface IS2StageV2 { S2StateV2 Execute(S1StateV2 input); }

public interface IS3StageV2 { S3StateV2 Execute(S2StateV2 input); }

public interface IS4StageV2 { S4StateV2 Execute(S3StateV2 input); }

public interface IS5StageV2 { S5StateV2 Execute(S4StateV2 input); }

public interface IS6StageV2 { S6StateV2 Execute(S5StateV2 input); }

public interface IS7StageV2 { S7StateV2 Execute(S6StateV2 input); }

public interface IS8StageV2 { S8StateV2 Execute(S7StateV2 input); }

public interface IS9StageV2 { S9StateV2 Execute(S8StateV2 input); }

public interface IS10StageV2 { S10StateV2 Execute(S9StateV2 input); }

public interface IS11StageV2 { S11StateV2 Execute(S10StateV2 input); }

public interface ITemplateMetricsReader {
    AssetsV2 Read(RequestV2 request, ContextV2 context);
    AssetV2 ObserveActualNativeDefinition(AssetV2 definition, SourceV2[] sources);
}
public interface IRuleSnapshotReader { RulesV2 Load(string path, string sha256, SourceV2[] sources); }
public interface INativeGenerationPort { NativeV2 Write(S9StateV2 input, System.Threading.CancellationToken token); }
public interface IPlotEvidencePort { PlotPageV2 Plot(string frozenDwgPath, SheetFrameV2 frame, RequestV2 request, ContextV2 context, System.Threading.CancellationToken token); }
public interface IVisualReviewReader { PlotPageV2? ReadReview(PlotPageV2 actualPage, string[] referenceBundleIds); }
public interface IArtifactStore { byte[] Read(string path); string Write(string path, byte[] bytes); }
public interface IRunContextFactory { ContextV2 Create(RequestV2 request, JobV2 job, TargetV2 target, RowVerdictV2[] priorRows, PlotPageV2[] priorPages, string? plotSessionId); }
public interface IPlotSessionPort { string OpenHealthySession(string profileId); bool IsHealthy(string sessionId); void Close(string sessionId); }
public interface IStagePipelineV2 { S11StateV2 RunRow(EntryStateV2 input, System.Threading.CancellationToken token); }
