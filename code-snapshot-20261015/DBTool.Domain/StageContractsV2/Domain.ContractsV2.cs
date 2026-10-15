#nullable enable
namespace DBTool.Domain.StageContractsV2;

public sealed record Point3V2(
    double x_mm /* mm */,
    double y_mm /* mm */,
    double z_mm /* mm */
);

public sealed record Point2V2(
    double x_mm /* mm */,
    double y_mm /* mm */
);

public sealed record Vector3V2(
    double x /* dimensionless */,
    double y /* dimensionless */,
    double z /* dimensionless */
);

public sealed record Bounds2V2(
    double min_x_mm /* mm */,
    double min_y_mm /* mm */,
    double max_x_mm /* mm */,
    double max_y_mm /* mm */
);

public sealed record FrameV2(
    string id /* — */,
    Point3V2 origin /* mm */,
    Vector3V2 axis_u /* dimensionless */,
    Vector3V2 axis_v /* dimensionless */,
    Vector3V2 normal /* dimensionless */,
    int handedness /* — */
);

public sealed record SourceV2(
    string id /* — */,
    string kind /* — */,
    string? relative_path /* — */,
    string sha256 /* — */,
    string? sheet /* — */,
    string? cell /* — */,
    int? page /* page */,
    string? handle /* — */,
    string raw_text /* — */,
    string[] parent_ids /* — */,
    [property: System.Text.Json.Serialization.JsonPropertyOrder(10)] string? formula_id /* — */
)
{
    // A source snapshot must not change when a caller mutates the array it supplied or received.
    private readonly string[] _parentIds = parent_ids?.ToArray()
        ?? throw new ArgumentNullException(nameof(parent_ids));

    [System.Text.Json.Serialization.JsonPropertyOrder(9)]
    public string[] parent_ids
    {
        get => _parentIds.ToArray();
        init => _parentIds = value?.ToArray()
            ?? throw new ArgumentNullException(nameof(parent_ids));
    }
}

public sealed record DiagnosticV2(
    string code /* — */,
    string stage /* — */,
    string? manhole_id /* — */,
    string? subject_id /* — */,
    string[] field_paths /* — */,
    string expected_text /* — */,
    string actual_text /* — */,
    string[] source_ids /* — */,
    string[] needsrule_ids /* — */,
    string severity /* — */
);

public sealed record ReceiptV2(
    string stage /* — */,
    string run_id /* — */,
    string manhole_id /* — */,
    string status /* — */,
    string input_hash /* — */,
    string? output_hash /* — */,
    string started_utc /* UTC */,
    string finished_utc /* UTC */
);

public sealed record AttributeDefV2(
    string tag /* — */,
    string default_text /* — */,
    string style_id /* — */,
    double height_mm /* native mm */,
    double width_factor /* dimensionless */,
    Point3V2 insertion /* native mm */,
    Point3V2 alignment /* native mm */,
    double rotation_rad /* rad */,
    string horizontal_mode /* — */,
    string vertical_mode /* — */
);

public sealed record AssetV2(
    string name /* — */,
    string definition_handle /* — */,
    string content_hash /* — */,
    Point3V2 base_point /* native mm */,
    Bounds2V2 bounds /* native mm */,
    string role /* — */,
    string[] child_names /* — */,
    AttributeDefV2[] attributes /* — */,
    string layer_id /* — */,
    string? style_id /* — */,
    string[] source_ids /* — */
);

public sealed record GlyphV2(
    string style_id /* — */,
    int codepoint /* Unicode */,
    double advance_native_mm /* native mm */,
    Bounds2V2 ink /* native mm */,
    string font_file_hash /* — */,
    double reference_height_mm /* native mm */
);

public sealed record AssetsV2(
    string bundle_hash /* — */,
    AssetV2[] definitions /* — */,
    GlyphV2[] glyphs /* — */,
    string font_policy_id /* — */,
    string units /* — */,
    ProductContractV2[] products /* — */,
    KerningV2[] kerning /* — */,
    LayerMetricV2[] layers /* — */,
    FontMetricStyleV2[] font_styles /* — */
);

public sealed record ParamV2(
    string key /* — */,
    string kind /* — */,
    double? number /* unit field */,
    string? text /* — */,
    bool? flag /* — */,
    string unit /* — */,
    string[] source_ids /* — */
);

public sealed record ExprNodeV2(
    string id /* — */,
    string op /* — */,
    string[] arg_ids /* — */,
    string? ref_path /* — */,
    double? constant /* unit field */,
    string unit /* — */,
    string[] source_ids /* — */,
    string? rounding_mode /* — */
);

public sealed record ShapeCornerV2(
    string role /* — */,
    string x_expr /* — */,
    string y_expr /* — */,
    string z_expr /* — */,
    double? bend_radius_mm /* mm */,
    string? radius_basis /* — */
);

public sealed record TagBindingV2(
    string tag /* — */,
    string operation /* — */,
    string? role /* — */,
    string? literal /* — */,
    string unit /* — */
);

public sealed record ShapeRecipeV2(
    string id /* — */,
    string component_kind /* — */,
    string family /* — */,
    string shape_kind /* — */,
    ShapeCornerV2[] corners /* — */,
    string length_expr_id /* — */,
    string measurement_basis /* — */,
    string native_asset /* — */,
    string fixed_glyph_hash /* — */,
    TagBindingV2[] tag_bindings /* — */,
    string[] source_ids /* — */,
    string fabrication_basis /* — */,
    string presentation_length_basis /* — */
);

public sealed record RuleV2(
    string id /* — */,
    string version /* — */,
    string status /* — */,
    string? approver /* — */,
    string? approved_at_utc /* UTC */,
    string[] profiles /* — */,
    string[] components /* — */,
    string[] shape_kinds /* — */,
    ParamV2[] parameters /* — */,
    ExprNodeV2[] expressions /* — */,
    ShapeRecipeV2[] shapes /* — */,
    string[] source_ids /* — */
);

public sealed record RulesV2(
    string snapshot_hash /* — */,
    RuleV2[] entries /* — */,
    double? geometry_tolerance_mm /* mm */,
    double? length_tolerance_mm /* mm */,
    double? paper_tolerance_mm /* paper mm */,
    double? mass_tolerance_kg /* kg */,
    double? basis_tolerance /* dimensionless */,
    double? angle_tolerance_rad /* rad */,
    double? area_tolerance_mm2 /* mm2 */,
    double? pdf_tolerance_points /* pdf point */
);

public sealed record RawCellV2(
    string field_key /* — */,
    string sheet /* — */,
    string address /* — */,
    string kind /* — */,
    string? text /* — */,
    string? raw_number_text /* — */,
    bool? boolean /* — */,
    string? formula /* — */,
    string? cache_kind /* — */,
    string? cache_text /* — */,
    bool cache_verified /* — */,
    bool active /* — */,
    string source_id /* — */,
    string[] note_source_ids /* — */
);

public sealed record RawRowV2(
    int row_index /* row */,
    string name_raw /* — */,
    RawCellV2[] cells /* — */
);

public sealed record RawCatalogRowV2(
    string sheet /* — */,
    int row_index /* row */,
    RawCellV2[] cells /* — */
);

public sealed record RawV2(
    string workbook_hash /* — */,
    string profile /* — */,
    string main_sheet /* — */,
    int selected_row_index /* row */,
    RawRowV2[] rows /* — */,
    RawCellV2[] config /* — */,
    RawCatalogRowV2[] catalog_rows /* — */,
    string version_marker /* — */
);

public sealed record DatumV2(
    string surface_id /* — */,
    double z_mm /* mm */,
    string? symbol /* — */,
    string[] source_ids /* — */
);

public sealed record MemberSpecV2(
    string id /* — */,
    string host_id /* — */,
    string component_kind /* — */,
    string face /* — */,
    string layer /* — */,
    string direction /* — */,
    string family /* — */,
    bool active /* — */,
    string? suppression_reason /* — */,
    int? diameter_mm /* mm */,
    double? requested_pitch_mm /* mm */,
    string spacing_mode /* — */,
    double cover_mm /* mm */,
    double pitch_delta_mm /* mm */,
    int diameter_step_down /* catalog steps */,
    string? hook_pattern /* — */,
    string shape_recipe_id /* — */,
    string[] required_rule_ids /* — */,
    int? count /* count */,
    string[] source_ids /* — */
);

public sealed record PipeSpecV2(
    string id /* — */,
    string wall /* — */,
    string catalog_code /* — */,
    string kind /* — */,
    double invert_mm /* mm */,
    double offset_u_mm /* mm */,
    double inner_width_mm /* mm */,
    double inner_height_mm /* mm */,
    double wall_thickness_mm /* mm */,
    double mortar_mm /* mm */,
    double chamfer_x_mm /* mm */,
    double chamfer_y_mm /* mm */,
    double? web_mm /* mm */,
    string[] source_ids /* — */
);

public sealed record CatchSpecV2(
    bool active /* — */,
    string? owner /* — */,
    string? wall /* — */,
    string? position /* — */,
    string? raw_position /* — */,
    double offset_u_mm /* mm */,
    double opening_width_mm /* mm */,
    double opening_height_mm /* mm */,
    double clear_depth_mm /* mm */,
    double clear_height_mm /* mm */,
    double wall_mm /* mm */,
    double base_mm /* mm */,
    double pad_mm /* mm */,
    double pad_expansion_mm /* mm */,
    bool reinforced /* — */,
    string? product_code /* — */,
    string[] source_ids /* — */
);

public sealed record PanelSpecV2(
    bool active /* — */,
    string? contract_id /* — */,
    int count /* count */,
    string? split_axis /* — */,
    double gap_mm /* mm */,
    double thickness_mm /* mm */,
    double seat_expansion_mm /* mm */,
    double seat_height_mm /* mm */,
    string[] source_ids /* — */,
    string? support_mode /* — */
);

public sealed record EffectiveV2(
    string id /* — */,
    string display_name /* — */,
    string profile /* — */,
    double clear_x_mm /* mm */,
    double clear_y_mm /* mm */,
    double clear_h_mm /* mm */,
    double base_mm /* mm */,
    double wall_mm /* mm */,
    double roof_mm /* mm */,
    double base_expansion_mm /* mm */,
    double pad_mm /* mm */,
    double pad_expansion_mm /* mm */,
    double base_haunch_mm /* mm */,
    double top_haunch_mm /* mm */,
    double bedding_mortar_mm /* mm */,
    bool neck_active /* — */,
    double neck_clear_x_mm /* mm */,
    double neck_clear_y_mm /* mm */,
    double? neck_raw_clear_x_mm /* mm */,
    double? neck_raw_clear_y_mm /* mm */,
    double neck_wall_mm /* mm */,
    double neck_height_mm /* mm */,
    string neck_position /* — */,
    Point2V2 neck_center /* mm */,
    bool neck_reinforced /* — */,
    string cover_kind /* — */,
    string? cover_code /* — */,
    double product_rotation_rad /* rad */,
    double frame_outer_x_mm /* mm */,
    double frame_outer_y_mm /* mm */,
    double frame_height_mm /* mm */,
    double? base_elevation_m /* m */,
    string elevation_mode /* — */,
    double body_scale /* 1:S */,
    double? panel_scale /* 1:S */,
    string note_style /* — */,
    string pitch_symbol /* — */,
    DatumV2[] datums /* — */,
    MemberSpecV2[] members /* — */,
    PipeSpecV2[] pipes /* — */,
    CatchSpecV2? @catch /* — */,
    PanelSpecV2? panel /* — */,
    string[] source_ids /* — */,
    ParsedFieldV2[] parsed_fields /* — */,
    SurfaceConstraintV2[] contact_equations /* mm */
);

public sealed record CurveV2(
    string id /* — */,
    string kind /* — */,
    Point3V2 start /* mm */,
    Point3V2 end /* mm */,
    Point3V2? center /* mm */,
    Vector3V2? normal /* dimensionless */,
    double? radius_mm /* mm */,
    double? sweep_rad /* rad */,
    string role /* — */
);

public sealed record LoopV2(
    string id /* — */,
    string role /* — */,
    CurveV2[] curves /* — */,
    bool closed /* — */,
    double signed_area_mm2 /* mm2 */
);

public sealed record PatchV2(
    string id /* — */,
    string component_id /* — */,
    string face_role /* — */,
    FrameV2 frame /* — */,
    LoopV2[] loops /* mm */,
    double area_mm2 /* mm2 */,
    string? adjacent_component_id /* — */,
    string[] source_ids /* — */
);

public sealed record ComponentV2(
    string id /* — */,
    string kind /* — */,
    string assembly_id /* — */,
    string material_id /* — */,
    FrameV2 frame /* — */,
    LoopV2[] loops /* mm */,
    double z_min_mm /* mm */,
    double z_max_mm /* mm */,
    double thickness_mm /* mm */,
    PatchV2[] patches /* — */,
    string[] void_ids /* — */,
    string[] source_ids /* — */,
    SolidPartV2[] solid_parts /* — */,
    string local_key /* — */,
    string material_role /* — */
);

public sealed record OpeningV2(
    string id /* — */,
    string kind /* — */,
    string[] host_ids /* — */,
    FrameV2 frame /* — */,
    LoopV2[] inner /* mm */,
    LoopV2[] pipe_outer /* mm */,
    LoopV2[] concrete_cutout /* mm */,
    double depth_min_mm /* mm */,
    double depth_max_mm /* mm */,
    double? invert_mm /* mm */,
    string? clearance_rule_id /* — */,
    string[] source_ids /* — */
);

public sealed record JointV2(
    string id /* — */,
    string lower_assembly_id /* — */,
    string upper_assembly_id /* — */,
    double z_mm /* mm */,
    string? mortar_component_id /* — */,
    bool dowel_required /* — */,
    string[] source_ids /* — */
);

public sealed record GeometryV2(
    ComponentV2[] components /* — */,
    OpeningV2[] openings /* — */,
    JointV2[] joints /* — */,
    string physical_hash /* — */,
    ProductPlacementV2[] products /* — */
);

public sealed record RoleValueV2(
    string role /* — */,
    double value_mm /* mm */,
    string[] source_ids /* — */
);

public sealed record OccurrenceV2(
    string id /* — */,
    string parent_id /* — */,
    string host_id /* — */,
    string assembly_id /* — */,
    string? panel_instance_id /* — */,
    string face /* — */,
    string layer /* — */,
    string direction /* — */,
    string family /* — */,
    int station_index /* count */,
    double station_mm /* mm */,
    int diameter_mm /* mm */,
    double? requested_pitch_mm /* mm */,
    double? actual_gap_mm /* mm */,
    string[] opening_ids /* — */,
    string region_role /* — */,
    CurveV2[] path /* mm */,
    RoleValueV2[] role_values /* mm */,
    double straight_run_mm /* mm */,
    double? fabrication_length_mm /* mm */,
    double? schedule_length_mm /* mm */,
    string measurement_basis /* — */,
    string length_status /* — */,
    string shape_recipe_id /* — */,
    string[] source_ids /* — */,
    string spec_id /* — */,
    string station_key /* — */,
    string fabrication_basis /* — */
);

public sealed record ClipLedgerV2(
    string parent_id /* — */,
    string[] opening_ids /* — */,
    double original_length_mm /* mm */,
    double[] removed_intervals_mm /* mm */,
    double[] retained_intervals_mm /* mm */,
    string[] retained_ids /* — */,
    double conservation_error_mm /* mm */
);

public sealed record FeatureStateV2(
    string spec_id /* — */,
    string state /* — */,
    string reason /* — */,
    string[] occurrence_ids /* — */,
    string[] needsrule_ids /* — */
);

public sealed record DowelPairV2(
    string id /* — */,
    string joint_id /* — */,
    string dowel_occurrence_id /* — */,
    string hole_id /* — */,
    Point3V2 center /* mm */,
    double hole_diameter_mm /* mm */,
    double hole_depth_mm /* mm */,
    Vector3V2 hole_axis /* dimensionless */,
    double hole_z_min_mm /* mm */,
    double hole_z_max_mm /* mm */,
    string[] source_ids /* — */
);

public sealed record CanonicalRowV2(
    string id /* — */,
    string[] member_ids /* — */,
    int diameter_mm /* mm */,
    double single_length_mm /* mm */,
    int quantity /* count */,
    double total_length_mm /* mm */,
    double? mass_kg /* kg */,
    string measurement_basis /* — */,
    double accounted_length_mm /* mm */,
    string mass_length_basis /* — */
);

public sealed record RebarV2(
    OccurrenceV2[] occurrences /* — */,
    ClipLedgerV2[] clip_ledger /* — */,
    FeatureStateV2[] features /* — */,
    DowelPairV2[] dowel_pairs /* — */,
    CanonicalRowV2[] canonical /* — */,
    string physical_hash /* — */,
    AngleOccurrenceV2[] angles /* — */,
    string fabrication_geometry_hash /* — */
);

public sealed record ParsedFieldV2(
    string field_key /* — */,
    string sheet /* — */,
    string address /* — */,
    int? row_index /* row */,
    bool active /* — */,
    string? inactive_reason /* — */,
    string kind /* — */,
    double? number /* unit field */,
    int? integer /* unit field */,
    string? text /* — */,
    bool? flag /* — */,
    double[] tuple_mm /* mm */,
    int? diameter_mm /* mm */,
    double? pitch_mm /* mm */,
    double? length_mm /* mm */,
    int? count /* count */,
    string[] tokens /* — */,
    string unit /* — */,
    string[] source_ids /* — */
);

public sealed record SurfaceConstraintV2(
    string surface_a /* — */,
    string surface_b /* — */,
    double delta_mm /* mm */,
    string[] source_ids /* — */
);

public sealed record ProductContractV2(
    string id /* — */,
    string code /* — */,
    string plan_asset /* — */,
    string section_aa_asset /* — */,
    string section_bb_asset /* — */,
    double aperture_x_mm /* mm */,
    double aperture_y_mm /* mm */,
    double rotation_rad /* rad */,
    LoopV2[] aperture /* mm */,
    SurfaceConstraintV2[] contact_equations /* mm */,
    bool approved /* — */,
    string[] source_ids /* — */,
    string placement_surface_id /* — */,
    Point3V2 registration_offset /* mm */
);

public sealed record KerningV2(
    string style_id /* — */,
    int left_codepoint /* Unicode */,
    int right_codepoint /* Unicode */,
    double adjustment_native_mm /* native mm */
);

public sealed record LayerMetricV2(
    string layer_id /* — */,
    string semantic_role /* — */,
    double stroke_paper_mm /* paper mm */,
    string[] source_ids /* — */
);

public sealed record SolidPartV2(
    string id /* — */,
    string operation /* — */,
    FrameV2 frame /* — */,
    LoopV2[] loops /* mm */,
    double depth_min_mm /* mm */,
    double depth_max_mm /* mm */
);

public sealed record ProductPlacementV2(
    string id /* — */,
    string contract_id /* — */,
    Point3V2 anchor /* mm */,
    double rotation_rad /* rad */,
    double physical_scale /* dimensionless */,
    string plan_asset /* — */,
    string section_aa_asset /* — */,
    string section_bb_asset /* — */,
    string[] source_ids /* — */
);

public sealed record AngleOccurrenceV2(
    string id /* — */,
    string host_id /* — */,
    string? panel_instance_id /* — */,
    string catalog_code /* — */,
    double unit_mass_kg_per_m /* kg/m */,
    CurveV2[] path /* mm */,
    double total_length_mm /* mm */,
    double mass_kg /* kg */,
    string shape_recipe_id /* — */,
    string[] source_ids /* — */,
    string spec_id /* — */,
    RoleValueV2[] role_values /* mm */
);

public sealed record FontMetricStyleV2(
    string style_id /* — */,
    string font_path /* — */,
    string font_hash /* — */,
    string encoding /* — */,
    string kerning_mode /* — */,
    string provider_version /* — */,
    string normalization /* — */
);
