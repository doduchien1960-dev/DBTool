#nullable enable
using DBTool.Domain.StageContractsV2;
namespace DBTool.Drawing.StageContractsV2;

public sealed record GroupV2(
    string id /* — */,
    string host_id /* — */,
    string face /* — */,
    string layer /* — */,
    string direction /* — */,
    string family /* — */,
    string region_role /* — */,
    string[] opening_ids /* — */,
    string shape_recipe_id /* — */,
    int? diameter_mm /* mm */,
    string[] member_ids /* — */,
    string[] canonical_ids /* — */,
    double[] member_lengths_mm /* mm */,
    int count /* count */,
    double raw_sum_mm /* mm */,
    double mean_mm /* mm */,
    double min_mm /* mm */,
    double max_mm /* mm */,
    RoleValueV2[] mean_roles /* mm */,
    string display_mark /* — */,
    double? pitch_mm /* mm */,
    double? mass_kg /* kg */,
    string measurement_basis /* — */,
    string[] source_ids /* — */,
    string material_kind /* — */,
    string? angle_code /* — */,
    double? unit_mass_kg_per_m /* kg/m */,
    string member_length_basis /* — */,
    string length_policy_id /* — */
);

public sealed record GroupsV2(
    GroupV2[] items /* — */,
    string registry_hash /* — */
);

public sealed record ViewV2(
    string id /* — */,
    string role /* — */,
    string owner_id /* — */,
    FrameV2 frame /* — */,
    double? section_offset_mm /* mm */,
    double scale /* 1:S */,
    bool mirror /* — */,
    bool required /* — */,
    string? na_reason /* — */,
    ViewPartCutV2[] component_cuts /* — */
);

public sealed record ProjectedCurveV2(
    string id /* — */,
    string view_id /* — */,
    string source_id /* — */,
    string? group_id /* — */,
    string kind /* — */,
    Point2V2 start /* model mm */,
    Point2V2 end /* model mm */,
    Point2V2? center /* model mm */,
    double? radius_mm /* model mm */,
    double? sweep_rad /* rad */,
    string visibility /* — */,
    string role /* — */,
    string projection_frame_id /* — */
);

public sealed record Target2V2(
    string id /* — */,
    string view_id /* — */,
    string source_id /* — */,
    string? group_id /* — */,
    string kind /* — */,
    Point2V2? point /* model mm */,
    string[] curve_ids /* — */,
    string[] region_loop_ids /* — */,
    bool visible /* — */
);

public sealed record ProjectedOpeningV2(
    string opening_id /* — */,
    string view_id /* — */,
    string host_id /* — */,
    string representation /* — */,
    string[] curve_ids /* — */,
    string projection_frame_id /* — */
);

public sealed record ProjectionV2(
    ViewV2[] views /* — */,
    ProjectedCurveV2[] curves /* — */,
    Target2V2[] targets /* — */,
    ProjectedOpeningV2[] opening_map /* — */,
    Region2V2[] regions /* — */,
    ProjectedNativeV2[] native_references /* — */
);

public sealed record BoundAttributeV2(
    string tag /* — */,
    string text /* — */,
    string value_kind /* — */,
    double? raw_number /* unit field */,
    string unit /* — */,
    string[] source_ids /* — */
);

public sealed record MemberIntersectionV2(
    string occurrence_id /* — */,
    Point2V2 point /* model mm */,
    string collector_curve_id /* — */
);

public sealed record LabelV2(
    string id /* — */,
    string view_id /* — */,
    string? group_id /* — */,
    string[] target_ids /* — */,
    string asset_name /* — */,
    BoundAttributeV2[] attributes /* — */,
    Point2V2 insertion /* model mm */,
    Point2V2 port_native /* native mm */,
    double asset_scale /* dimensionless */,
    double rotation_rad /* rad */,
    Bounds2V2 ink /* model mm */,
    string[] route_ids /* — */,
    string[] source_ids /* — */
);

public sealed record RouteV2(
    string id /* — */,
    string view_id /* — */,
    string? group_id /* — */,
    Point2V2[] points /* model mm */,
    MemberIntersectionV2[] intersections /* — */,
    string[] label_ids /* — */,
    string reason /* — */
);

public sealed record DimensionV2(
    string id /* — */,
    string view_id /* — */,
    string subject_id /* — */,
    Point2V2 start /* model mm */,
    Point2V2 end /* model mm */,
    string axis /* — */,
    double measured_mm /* mm */,
    string prefix /* — */,
    string display_text /* — */,
    double lane_mm /* model mm */,
    Bounds2V2 ink /* model mm */,
    string[] source_ids /* — */
);

public sealed record TickV2(
    string id /* — */,
    string view_id /* — */,
    string asset_name /* — */,
    string occurrence_id /* — */,
    Point2V2 point /* model mm */,
    double scale /* dimensionless */
);

public sealed record AnnotationsV2(
    LabelV2[] labels /* — */,
    RouteV2[] routes /* — */,
    DimensionV2[] dimensions /* — */,
    TickV2[] ticks /* — */,
    ViewInkV2[] view_ink /* — */,
    HatchV2[] hatches /* — */
);

public sealed record ViewInkV2(
    string view_id /* — */,
    Bounds2V2 bounds /* model mm */
);

public sealed record ScheduleRowV2(
    string id /* — */,
    string group_id /* — */,
    string native_asset /* — */,
    BoundAttributeV2[] attributes /* — */,
    string glyph_hash /* — */,
    Point2V2 insertion /* model mm */,
    double scale /* dimensionless */,
    Bounds2V2 ink /* model mm */,
    string view_id /* — */
);

public sealed record CaptionV2(
    string group_id /* — */,
    string glyph_asset /* — */,
    string text /* — */,
    RoleValueV2[] role_values /* mm */,
    Point2V2 underline_start /* model mm */,
    Point2V2 underline_end /* model mm */,
    Bounds2V2 ink /* model mm */,
    string view_id /* — */,
    Point2V2 insertion /* model mm */,
    double asset_scale /* dimensionless */
);

public sealed record SummaryV2(
    string id /* — */,
    string scope /* — */,
    string native_asset /* — */,
    BoundAttributeV2[] attributes /* — */,
    double mass_small_kg /* kg */,
    double mass_large_kg /* kg */,
    string view_id /* — */,
    Point2V2 insertion /* model mm */,
    double asset_scale /* dimensionless */,
    Bounds2V2 ink /* model mm */
);

public sealed record SchedulesV2(
    ScheduleRowV2[] rows /* — */,
    CaptionV2[] captions /* — */,
    SummaryV2[] summaries /* — */,
    ViewInkV2[] ink /* — */,
    ScheduleHeadingV2[] headings /* — */
);

public sealed record PlacementV2(
    string view_id /* — */,
    string frame_id /* — */,
    Point2V2 translation /* model mm */,
    double rotation_rad /* rad */,
    double scale_denominator /* 1:S */,
    Bounds2V2 ink_after /* model mm */
);

public sealed record SheetFrameV2(
    string id /* — */,
    string manhole_id /* — */,
    string sheet_role /* — */,
    int page_index /* page */,
    double scale_denominator /* 1:S */,
    string frame_asset /* — */,
    Bounds2V2 window /* model mm */,
    Bounds2V2 content /* model mm */,
    PlacementV2[] placements /* — */,
    Point2V2 origin /* sheet-local mm */
);

public sealed record SheetV2(
    SheetFrameV2[] frames /* — */,
    string geometry_hash_before /* — */,
    string rebar_hash_before /* — */,
    string sheet_hash /* — */
);

public sealed record HandleV2(
    string semantic_id /* — */,
    string entity_handle /* — */,
    string? block_name /* — */,
    string[] attribute_tags /* — */,
    string[] attribute_values /* — */,
    string owner_manhole_id /* — */
);

public sealed record NativeV2(
    string saved_dwg_path /* — */,
    string saved_dwg_sha256 /* — */,
    HandleV2[] handles /* — */,
    string normalized_geometry_hash /* — */,
    string normalized_rebar_hash /* — */,
    string repository_version /* — */,
    string write_status /* — */,
    string readback_status /* — */,
    string[] unchanged_manhole_ids /* — */,
    string[] unchanged_before_hashes /* — */,
    string[] unchanged_after_hashes /* — */,
    string domain_geometry_hash /* — */,
    string expected_projection_hash /* — */,
    string commit_state /* — */
);

public sealed record AcceptanceCheckV2(
    string name /* — */,
    string subject_id /* — */,
    double? expected /* unit field */,
    double? actual /* unit field */,
    string? expected_text /* — */,
    string? actual_text /* — */,
    string unit /* — */,
    string? tolerance_rule_id /* — */,
    string[] source_ids /* — */,
    string[] native_handles /* — */,
    string[] evidence_paths /* — */,
    string verdict /* — */,
    string? reason /* — */
);

public sealed record RowVerdictV2(
    string job_id /* — */,
    int row_index /* row */,
    string manhole_id /* — */,
    string status /* — */,
    string[] frame_ids /* — */,
    AcceptanceCheckV2[] checks /* — */,
    DiagnosticV2[] diagnostics /* — */
);

public sealed record PlotPageV2(
    string frame_id /* — */,
    string pdf_path /* — */,
    string pdf_sha256 /* — */,
    int page_index /* page */,
    string host_session_id /* — */,
    int pstylemode /* — */,
    string plot_profile_id /* — */,
    double nominal_scale /* 1:S */,
    double actual_scale /* 1:S */,
    string transcript_path /* — */,
    string visual_status /* — */,
    string? reviewer /* — */,
    string[] reviewed_regions /* — */
);

public sealed record BatchV2(
    RowVerdictV2[] rows /* — */,
    PlotPageV2[] pages /* — */,
    string status /* — */,
    string delivery_manifest_path /* — */,
    string? final_dwg_path /* — */,
    string? final_dwg_sha256 /* — */
);

public sealed record ViewPartCutV2(
    string component_id /* — */,
    FrameV2 frame /* — */,
    string mode /* — */
);

public sealed record Region2V2(
    string id /* — */,
    string view_id /* — */,
    string assembly_id /* — */,
    string material_id /* — */,
    string[] component_ids /* — */,
    LoopV2[] loops /* view-local mm */,
    string projection_frame_id /* — */,
    double? surface_depth_mm /* mm */,
    string material_role /* — */,
    string representation_kind /* — */,
    string? visible_surface_id /* — */
);

public sealed record HatchV2(
    string id /* — */,
    string view_id /* — */,
    string region_id /* — */,
    string pattern_name /* — */,
    double pattern_scale /* dimensionless */,
    double angle_rad /* rad */,
    string layer_id /* — */,
    string[] source_ids /* — */
);

public sealed record ProjectedNativeV2(
    string id /* — */,
    string view_id /* — */,
    string source_id /* — */,
    string asset_name /* — */,
    Point2V2 insertion /* view-local mm */,
    double rotation_rad /* rad */,
    double scale_x /* dimensionless */,
    double scale_y /* dimensionless */,
    string[] source_ids /* — */
);

public sealed record ScheduleHeadingV2(
    string id /* — */,
    string view_id /* — */,
    string? group_id /* — */,
    string kind /* — */,
    string native_asset /* — */,
    BoundAttributeV2[] attributes /* — */,
    Point2V2 insertion /* model mm */,
    double asset_scale /* dimensionless */,
    Bounds2V2 ink /* model mm */
);

