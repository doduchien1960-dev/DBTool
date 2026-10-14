# 05C — Struct, interfaces và migration đồng bộ

Phiên bản leader-field-contracts-20261013-r2. Đầu ra lần này là đặc tả triển khai và prompt, chưa phải bản phát hành DBTool đã nghiệm thu DWG/PDF.

23 đề xuất NR-01..23 đã được DucBao chấp thuận trực tiếp ngày 13/10/2026 (OWNER-APPROVAL-23-20261013). Không yêu cầu duyệt lại các đề xuất này. ApprovedProposal xác nhận lựa chọn đã mô tả; không tạo ra giá trị nguồn chưa có trong đề xuất. Binding sản phẩm/shape/đơn vị/tolerance thiếu dữ liệu trả `RULE_BINDING_DATA_MISSING` có field/source/debug; không hỏi giữa chừng và không tự bịa.

Pipeline có một chuỗi S0–S11. DTO envelope tích lũy giữ roots đã tính immutable; OUT của stage N bằng IN của N+1. Đây là thay đổi hợp đồng phần mềm được ghi trong NR-IO-001. Drawing chỉ nhận Domain/Drawing payload, Application adapter sở hữu envelope; không tạo dependency Drawing→Application. S8 dùng registry nhóm, không đo glyph để tính chiều dài; thứ tự thực thi chuẩn vẫn đặt S8 sau S7 để kiểm tính liên tục I/O.

Registry field duy nhất sinh ra cả DTO C#,05,05D. Không tạo thêm field trong implementation ngoài registry; thay đổi phải đi cùng schema version/migration.

| Module mới / owner | Signature bắt buộc |
|---|---|
| PresentationGroupPlanner.cs | GroupsV2 Plan(EffectiveV2 effective, GeometryV2 geometry, RebarV2 rebar, RulesV2 rules) |
| RuleApprovalRegistry.cs | RulesV2 Load(string snapshotPath, string snapshotSha256, SourceV2[] sources) |
| NativeSteelSchedulePlanner.cs | SchedulesV2 PlanRowsAndSummaries(EffectiveV2 effective, GeometryV2 geometry, GroupsV2 groups, AssetsV2 assets, RulesV2 rules) |
| SchematicPresentationPlanner.cs | CaptionV2[] PlanCaptions(EffectiveV2 effective, GroupsV2 groups, AssetsV2 assets, RulesV2 rules) |
| AnnotationLaneAllocator.cs | AnnotationsV2 Plan(EffectiveV2 effective, ProjectionV2 projection, GroupsV2 groups, AssetsV2 assets, RulesV2 rules) |
| CatchAssemblyProjector.cs | ProjectionV2 Project(EffectiveV2 effective, GeometryV2 geometry, RebarV2 rebar, GroupsV2 groups, AssetsV2 assets, RulesV2 rules) |
| AssemblyBoundaryEmitter.cs | Region2V2[] Emit(GeometryV2 geometry, OpeningV2[] manufacturingHoles, ViewV2[] views, RulesV2 rules) |
| BatchGenerationOrchestrator.cs | BatchV2 Generate(RequestV2 request, IRunContextFactory contextFactory, CancellationToken token) |
| NativeReadbackVerifier.cs | NativeV2 Verify(S9StateV2 expected, NativeV2 committedWrite, CancellationToken token) |
| AcceptanceEvidenceAssembler.cs | BatchV2 Assemble(S10StateV2 input, CancellationToken token) |

| Port | IN | OUT | Adapter / cách thực hiện | Lỗi |
|---|---|---|---|---|
| IWorkbookBytesReader | JobV2.workbook_bytes/hash/receipt | OOXML cells including formula and cached type; receipt certificate | WorkbookImporter existing decoder;read-only byte stream | WORKBOOK_SNAPSHOT_HASH_MISMATCH/FORMULA_CACHE_UNVERIFIED |
| ITemplateMetricsReader | Request template manifest path/hash | AssetsV2 exact field schema | TemplateManifestReader,NativeAssetSignatures;glyph-only extents and actual font metric provider | TEMPLATE_NATIVE_SIGNATURE_MISMATCH/FONT_GLYPH_METRIC_MISSING |
| IRuleSnapshotReader | Request rule path/hash + owner approval record | RulesV2 exact typed scope/header/parameter/expression/recipe fields | RuleApprovalRegistry;never generate geometry from proposal prose | RULE_BINDING_DATA_MISSING/RULE_SCOPE_MISMATCH |
| INativeGenerationPort | S9StateV2 + intent list + exact generation expectations | NativeV2 or FailureV2 with commit-state recovery artifact | CadGenerationWriter/CadWriteSession/GenerationRepository;one document lock/transaction | NATIVE_FAILURE_AFTER_COMMIT/NATIVE_WRITE_ROLLED_BACK |
| IPlotEvidencePort | savedDWG,SheetFrameV2,approved plot profile,shared session ID | actual PlotPageV2 fields and measured PDF mapping | Headless/AutoCAD host adapter;PSTYLEMODE matched CTB/STB;full transcript | PLOT_SESSION_MISSING/PLOT_PROFILE_INCOMPATIBLE/HOST_UNHEALTHY |
| IVisualReviewReader | actualPDF hash,ReferenceBundle ID,frame ID | reviewer,reviewed_regions,per-region verdict + paths | Read actual saved review receipt;absent→NotRun,not auto Passed | VISUAL_REVIEW_NOT_RUN/REFERENCE_BUNDLE_MISMATCH |
| IArtifactStore | context.artifact_directory,actual bytes/records | path/hash of real artifact | Create output only in named run folder;temp file+atomic rename for JSON;DWG commit semantics remain separate | ARTIFACT_WRITE_FAILED/OUTPUT_PATH_SOURCE_CONFLICT |

## Typed rule binding: giá trị có sẵn và dữ liệu bắt buộc

Các header NR01..23 dưới đây đã Approved. Seed chỉ điền những giá trị xuất hiện trong đề xuất/nguồn đã duyệt; các tolerance, radius basis, contact/stack/shape expressions, product measurements và font/plot hashes chưa có giá trị nguồn giữ null/absent. Junior implement đầy đủ nhánh validate;không hỏi để điền và không coi seed là một case Final sẵn chạy. Wildcard `*` trong scope chỉ khớp explicit approved policy header;parameter/recipe vẫn kiểm exact active component/profile.
| Key | Kind | Unit | Consumer / required when |
|---|---|---|---|
| allowed_sides | Text | none | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| angle_label_asset | Text | none | S7; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| angle_rad | Number | rad | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_assembly_id | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_inner_x_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_inner_y_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_outer_x_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_outer_y_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_z_max_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collar_z_min_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collector_axis | Text | none | H11-COLLECTORS-AND-LANES; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| collision_action | Text | none | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| convention | Text | none | S4; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| count_label_prefix | Text | none | H23-GROUP-TAG-AND-INK-OPERATIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| count_u_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| count_v_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| culture | Text | none | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| default_pattern_exact_name | Text | none | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| density_kg_per_m3 | Number | kg_per_m3 | H10-CANONICAL-AND-MASS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| diameter_mm | Number | mm | H14-PARSE-ACTIVITY-DATUM-MEMBER; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| diameter_squared_divisor | Number | dimensionless | H10-CANONICAL-AND-MASS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| exact_product_code | Text | none | H14-PARSE-ACTIVITY-DATUM-MEMBER; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| fallback_curve_parameter | Number | dimensionless | H11-COLLECTORS-AND-LANES; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| first_row_y_native_mm | Number | mm | S8; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| glyph_asset | Text | none | S8; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| height_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| hole_depth_mm | Number | mm | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| input_material_field | Text | none | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| keep_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| ladder_min_z_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| ladder_top_surface_id | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| layer_id | Text | none | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| lower_embed_mm | Number | mm | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| mass_length_basis | Text | none | H10-CANONICAL-AND-MASS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| min_ink_gap_paper_mm | Number | paper_mm | H28-FINITE-NATIVE-LABEL-PLACEMENT,S7; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| native_baseline_denominator | Number | dimensionless | H28-FINITE-NATIVE-LABEL-PLACEMENT,S7; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| native_view_rotation_rad | Number | rad | H15-PROJECTION-FRAMES-AND-NATIVE-REFERENCE; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| next_gap_paper_mm | Number | paper_mm | S8; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| offset_u_mm | Number | mm | H14-PARSE-ACTIVITY-DATUM-MEMBER; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| page_gap_paper_mm | Number | paper_mm | H18-OFFSET-DISTANCE-AND-CLOSED-OPERATIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| pitch_midpoint_mode | Text | none | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| pitch_quantum_mm | Number | mm | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| rotation_rad | Number | rad | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| round_mass_mode | Text | none | H10-CANONICAL-AND-MASS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| route_enabled | Bool | none | H28-FINITE-NATIVE-LABEL-PLACEMENT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| route_mode | Text | none | H11-COLLECTORS-AND-LANES; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| scale_expr | Text | none | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| seat_kind | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| selected_contract_id | Text | none | S2; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| show_angle | Bool | none | H22-VIEW-ROLE-AND-TARGET-TABLE; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| side | Text | none | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| side_basis | Text | none | H05-CATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| station_count_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS,H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| station_distance_expr | Text | none | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| station_u_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| station_v_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| stub_paper_mm | Number | paper_mm | H11-COLLECTORS-AND-LANES; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| summary_gap_paper_mm | Number | paper_mm | S8; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| support_mode | Text | none | H14-PARSE-ACTIVITY-DATUM-MEMBER; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| target_parameter | Number | dimensionless | H22-VIEW-ROLE-AND-TARGET-TABLE,H23-GROUP-TAG-AND-INK-OPERATIONS,H30-EXACT-PROJECTED-GROUP-TARGET; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| target_role | Text | none | H19-SOURCE-BINDING-AND-SEMANTIC-SUPPORT,H22-VIEW-ROLE-AND-TARGET-TABLE,H23-GROUP-TAG-AND-INK-OPERATIONS,H30-EXACT-PROJECTED-GROUP-TARGET; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| target_selection | Text | none | H30-EXACT-PROJECTED-GROUP-TARGET; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| title_anchor_x_expr | Text | none | H29-REGISTERED-TITLE-AND-ELEVATION-SUBJECTS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| title_anchor_y_expr | Text | none | H29-REGISTERED-TITLE-AND-ELEVATION-SUBJECTS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| title_literal | Text | none | H13-DIM-CT-MATERIAL-HATCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| u_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| underline_gap_paper_mm | Number | paper_mm | S8; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| upper_embed_mm | Number | mm | H09-DOWELS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| v_expr | Text | none | H07-SUPPORT-STATIONS-REGIONS; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| view_gap_x_paper_mm | Number | paper_mm | S9; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| view_gap_y_paper_mm | Number | paper_mm | S9; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| width_expr | Text | none | H03-SEAT-FRAME-PANEL-HAUNCH; Only when named operation is invoked for active feature;missing→RULE_BINDING_DATA_MISSING |
| recipe.{MemberBindingKey} | Text | none | S2 exact component/face/layer/family recipe ID |
| offset.{Component}.{Face}.{Layer}.{Direction} | Text | none | S4 expression ID returning mm centreline offset |
| face.{MemberBindingKey}/layer.{MemberBindingKey}/direction.{MemberBindingKey} | Text | none | S2 special family support/direction |
| support_patch_id.{MemberSpecId} | Text | none | S4 special-family exact patch ID |
| dimension_subject.{Ordinal} | Text | none | S7 subject table key |
| dimension_lane_paper_mm.{Subject} | Number | paper_mm | S7 dimension lane |
| asset.{LabelRole} | Text | none | S7 exact block name |
| field_for_material.{MaterialId} | Text | none | S7 requested hatch field |
| lane_offset_paper_mm.{Side}/lane_extent_paper_mm.{Side} | Number | paper_mm | S7 finite lane bounds |
| decimals.{Role}/{Unit} | Number | dimensionless | S7/S8 exact nonnegative integer precision |
| midpoint.{Role}/{Unit} | Text | none | S7/S8 source rounding mode |
| trailing_zeros.{Role}/{Unit} | Bool | none | S7/S8 formatting trailing zeros |
| frame_asset.{SheetRole} | Text | none | S9 exact frame block |
| content_min_x_paper_mm.{Role}/content_min_y_paper_mm.{Role}/content_max_x_paper_mm.{Role}/content_max_y_paper_mm.{Role} | Number | paper_mm | S9 frame printable/content bounds |
| show.{LabelRole} | Bool | none | S6 scoped AnnotationTargets recipe: explicit visible title/elevation roles |
| anchor_world_x_expr.{ElevationRole}/anchor_world_y_expr.{ElevationRole} | Text | none | S6 expression IDs returning source world mm;Z from exact datum/pipe |
| title_anchor_x_expr/title_anchor_y_expr | Text | none | S6 expression IDs returning view-local mm |
| route_enabled | Bool | none | S7 per-label-role explicit route intent;title may have no leader |

Seed RulesV2 chính xác, không phải ví dụ fixture:

```json
{
  "snapshot_hash": "07a6ba4aab682dbd3c416a4b3ebc597eeff3674a52778dc133228dc75c45221c",
  "entries": [
    {
      "id": "NR-01",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "convention",
          "kind": "Text",
          "number": null,
          "text": "Surface",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-02",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-03",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-04",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-05",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-06",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "pitch_quantum_mm",
          "kind": "Number",
          "number": 50,
          "text": null,
          "flag": null,
          "unit": "mm",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-07",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-08",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-09",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-10",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "alias_requires_exact_member_set",
          "kind": "Bool",
          "number": null,
          "text": null,
          "flag": true,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-11",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "regenerate_from_members",
          "kind": "Bool",
          "number": null,
          "text": null,
          "flag": true,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-12",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-13",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "decimals.DAI/mm",
          "kind": "Number",
          "number": 0,
          "text": null,
          "flag": null,
          "unit": "dimensionless",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "decimals.Leg/mm",
          "kind": "Number",
          "number": 0,
          "text": null,
          "flag": null,
          "unit": "dimensionless",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "decimals.TDAI/m",
          "kind": "Number",
          "number": 2,
          "text": null,
          "flag": null,
          "unit": "dimensionless",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "decimals.KL/kg",
          "kind": "Number",
          "number": 2,
          "text": null,
          "flag": null,
          "unit": "dimensionless",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "midpoint.DAI/mm",
          "kind": "Text",
          "number": null,
          "text": "AwayFromZero",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "midpoint.Leg/mm",
          "kind": "Text",
          "number": null,
          "text": "AwayFromZero",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "midpoint.TDAI/m",
          "kind": "Text",
          "number": null,
          "text": "AwayFromZero",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "midpoint.KL/kg",
          "kind": "Text",
          "number": null,
          "text": "AwayFromZero",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-14",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "bucket_mode",
          "kind": "Text",
          "number": null,
          "text": "PhysicalOwner",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "summary_diameter_boundary_mm",
          "kind": "Number",
          "number": 10,
          "text": null,
          "flag": null,
          "unit": "mm",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-15",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "canonical_bundle",
          "kind": "Text",
          "number": null,
          "text": "acad-original",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-16",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-17",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "missing_named_default_action",
          "kind": "Text",
          "number": null,
          "text": "Block",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-18",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-19",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-20",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-21",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "collision_action",
          "kind": "Text",
          "number": null,
          "text": "Reject",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-22",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [
        {
          "key": "fixed_single_view_overflow_action",
          "kind": "Text",
          "number": null,
          "text": "LAYOUT_SINGLE_VIEW_NO_FIT",
          "flag": null,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        },
        {
          "key": "auto_requires_request",
          "kind": "Bool",
          "number": null,
          "text": null,
          "flag": true,
          "unit": "none",
          "source_ids": [
            "OWNER-APPROVAL-23-20261013"
          ]
        }
      ],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    },
    {
      "id": "NR-23",
      "version": "owner-20261013-r2",
      "status": "Approved",
      "approver": "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại",
      "approved_at_utc": "2026-10-13T13:15:06.467490+00:00",
      "profiles": [
        "GaThamLienKhoi",
        "GaThamNapRoi",
        "GaThamThuNapRoi"
      ],
      "components": [
        "*"
      ],
      "shape_kinds": [
        "*"
      ],
      "parameters": [],
      "expressions": [],
      "shapes": [],
      "source_ids": [
        "OWNER-APPROVAL-23-20261013"
      ]
    }
  ],
  "geometry_tolerance_mm": null,
  "length_tolerance_mm": null,
  "paper_tolerance_mm": null,
  "mass_tolerance_kg": null,
  "basis_tolerance": null,
  "angle_tolerance_rad": null,
  "area_tolerance_mm2": null,
  "pdf_tolerance_points": null
}
```

## Schema native assets nhúng trong gói

Các tên/tag dưới đây lấy nguyên từ acad-original. ATTDEF specimen text không phải output;được thay toàn bộ bằng typed values. TK.M.C và TK.MC là hai topology khác nhau;TK.M.U có thể phục vụ tie hoặc thang nhưng role binding phải khác. Glyph giữ native definition;không đo glyph để suy chiều dài chế tạo.

| Exact block | Source handle | Vai trò | Exact ATTDEF fields |
|---|---|---|---|
| TenMC | 2B | Tiêu đề hình và tỷ lệ | [{"tag": "TIEUDE", "style": "VHG-Unicode.Bold", "height": 300.0, "width": null, "insert": [-768.4174624829467, -150.0, 0.0], "align_point": [0.0, 0.0, 0.0], "halign": 1, "valign": 2, "rotation": null}, {"tag": "TYLE", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [-259.0476190476191, -600.0, 0.0], "align_point": [0.0, -500.0, 0.0], "halign": 1, "valign": 2, "rotation": null}] |
| BLT1 | 3E | Nhãn thép hướng 1 | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [840.1769491965891, -88.08024993352592, 0.0], "align_point": [969.7007587203987, -88.08024993352592, 0.0], "halign": 1, "valign": null, "rotation": null}, {"tag": "DK", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [250.1509744156308, 38.01914052432403, 0.0], "align_point": [383.4843077489641, 38.01914052432403, 0.0], "halign": 1, "valign": null, "rotation": null}, {"tag": "A", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [322.5319267965831, -245.0242525772192, 0.0], "align_point": [383.4843077489641, -245.0242525772192, 0.0], "halign": 1, "valign": null, "rotation": null}] |
| BLT2 | 4E | Nhãn thép hướng 2 | [{"tag": "A", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [-518.1076102765066, -245.0242525772192, 0.0], "align_point": [-457.1552293241256, -245.0242525772192, 0.0], "halign": 1, "valign": null, "rotation": null}, {"tag": "DK", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [-604.20657793545, 38.01914052432403, 0.0], "align_point": [-470.8732446021167, 38.01914052432403, 0.0], "halign": 1, "valign": null, "rotation": null}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [-1112.315965052367, -88.08024993352592, 0.0], "align_point": [-982.7921555285575, -88.08024993352592, 0.0], "halign": 1, "valign": null, "rotation": null}] |
| Gach | 5D | Đánh dấu giao collector/thanh | [] |
| BLTC | 65 | Dấu vị trí tie | [] |
| CT | 88 | Cao độ surface/datum | [{"tag": "CT", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [36.49779136939014, 207.7309051528573, 0.0], "align_point": null, "halign": null, "valign": null, "rotation": null}] |
| TK.Tieude | 2BA | ScheduleHeader | [] |
| TK.Tenluoi | 2DB | VietnameseGroupName | [{"tag": "TENLUOI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [292.8570461297817, -349.9999147594385, 0.0], "align_point": [750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.M.C | 3D0 | CShape | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4950.0, -350.0000262313698, 0.0], "align_point": [4950.000022212047, -250.0000099311449, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3138.571339295598, -349.9999249135171, 0.0], "align_point": [3550.0, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.GC.T | 385 | CircularReinforcementWithLaps | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1870.476147151471, -349.9999875303354, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3872.734803530099, -349.9999959920677, 0.0], "align_point": [3964.16341207378, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4557.200723846628, -350.0000163002249, 0.0], "align_point": [4557.200746058675, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6446.666635761167, -350.0000044537999, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7586.190427277109, -349.9999799147765, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8878.09519601244, -349.9999892226819, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10017.61898752838, -349.9999646836585, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11624.28567158195, -349.9999883765087, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.VG | 269 | NeedsVerifiedShapeRecipe | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11624.28567158195, -349.9999883765087, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10017.61898752838, -349.9999646836585, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8878.09519601244, -349.9999892226819, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7586.190427277109, -349.9999799147765, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6446.666635761167, -350.0000044537999, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5081.904720442925, -349.9999900688552, 0.0], "align_point": [5200.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3181.904720442926, -349.9999900688552, 0.0], "align_point": [3300.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4204.285684622135, -488.9848251529905, 0.0], "align_point": [4250.0, -388.9848190068442, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1870.476147151471, -349.9999875303354, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.GC.H | 3AA | ClosedRectangularWithLaps | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11624.28567158195, -349.9999883765087, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10017.61898752838, -349.9999646836585, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8878.09519601244, -349.9999892226819, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7586.190427277109, -349.9999799147765, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6446.666635761167, -350.0000044537999, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4550.498763612002, -350.0000163002249, 0.0], "align_point": [4550.498785824049, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3859.393875391545, -349.9999959920677, 0.0], "align_point": [3950.822483935226, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1870.476147151471, -349.9999875303354, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.THKL | 2E5 | ComponentDiameterSummary | [{"tag": "TONG", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [456.6665966406263, -1349.999951144887, 0.0], "align_point": [750.0, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "CO2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11595.35708525032, -1349.999968068351, 0.0], "align_point": [11812.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "CO1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10254.64280512468, -1349.99997568391, 0.0], "align_point": [10437.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TRAN2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8692.97610803093, -1349.999934221422, 0.0], "align_point": [9062.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TRAN1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7352.261827905294, -1349.999941836981, 0.0], "align_point": [7687.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TUONG2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5859.166570560266, -1349.999915605612, 0.0], "align_point": [6312.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TUONG1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4518.45229043463, -1349.999923221171, 0.0], "align_point": [4937.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAY2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3269.166596640626, -1349.999951144887, 0.0], "align_point": [3562.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAY1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1928.452316514989, -1349.999958760446, 0.0], "align_point": [2187.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.MC | 291 | PanelLiftingHook | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L2T", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3580.327674733126, -381.0669533719419, 0.0], "align_point": [3786.042016191351, -281.0669827650709, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L1T", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3095.565772455065, -449.9999940767867, 0.0], "align_point": [3286.04201619135, -350.0000200852228, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L3", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4087.232439121809, -431.0669567566348, 0.0], "align_point": [4277.708682858094, -331.0669827650709, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L1P", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5078.899105788553, -449.9999940767867, 0.0], "align_point": [5269.375349524839, -350.0000200852228, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L2P", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4572.362923870852, -381.0669533719419, 0.0], "align_point": [4778.077265329076, -281.0669827650709, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| BLT4 | 22F | Ghi chú NOTE | [{"tag": "NOTE", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [-1199.678052468018, 38.01914052432403, 0.0], "align_point": null, "halign": null, "valign": null, "rotation": null}] |
| BLT3 | 238 | Ghi chú NOTE | [{"tag": "NOTE", "style": "VHG-Unicode.Regular", "height": 200.0, "width": 0.8, "insert": [148.2396020822662, 38.01914052432403, 0.0], "align_point": [1192.04912589179, 38.01914052432403, 0.0], "halign": 2, "valign": null, "rotation": null}] |
| TK.L2M | 31D | TwoEndHooks | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5494.285658541776, -349.999970606871, 0.0], "align_point": [5700.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [2594.285658541776, -349.999970606871, 0.0], "align_point": [2800.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.L1M | 342 | NeedsVerifiedShapeRecipe | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [2594.285658541776, -349.999970606871, 0.0], "align_point": [2800.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.L0M | 364 | Straight | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.M.Z | 242 | PanelTieZ | [{"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11624.28567158195, -349.9999883765087, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10017.61898752838, -349.9999646836585, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8878.09519601244, -349.9999892226819, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7586.190427277109, -349.9999799147765, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6446.666635761167, -350.0000044537999, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4849.999977787953, -350.0000163002249, 0.0], "align_point": [4850.0, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3413.809463097898, -349.9999638374852, 0.0], "align_point": [3650.0, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4349.999977787953, -350.0000163002249, 0.0], "align_point": [4350.0, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1870.476147151471, -349.9999875303354, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.M.U | 3F5 | UShape_RoleMustDistinguishTieAndLadder | [{"tag": "L2P", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4699.999977787952, -331.0669990652958, 0.0], "align_point": [4700.0, -231.0669827650709, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L1P", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5250.0, -450.0000363854477, 0.0], "align_point": [5250.000022212047, -350.0000200852228, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L3", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4059.523756263715, -431.0669567566348, 0.0], "align_point": [4250.0, -331.0669827650709, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L1T", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [2769.047534739477, -449.9999517681256, 0.0], "align_point": [3150.0, -350.0000200852228, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L2T", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3388.571339295598, -331.066907678588, 0.0], "align_point": [3800.0, -231.0669827650709, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.NG | 42C | MouthOrPanelAnchor | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.TG | 494 | AngleSteel | [{"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6296.666635761166, -350.0000044537999, 0.0], "align_point": [6350.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11624.28567158195, -349.9999883765087, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10017.61898752838, -349.9999646836585, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8878.09519601244, -349.9999892226819, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7586.190427277109, -349.9999799147765, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1870.476147151471, -349.9999875303354, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4054.285684622136, -350.0000061461463, 0.0], "align_point": [4100.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.NA1 | 44D | PanelMeshType1 | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [2488.571339295599, -349.9999249135171, 0.0], "align_point": [2900.0, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5600.0, -350.0000262313687, 0.0], "align_point": [5600.000022212046, -250.0000099311437, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.NA2 | 471 | PanelMeshType2 | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [3983.33326765402, -349.9999570680995, 0.0], "align_point": [4250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [2594.285658541776, -349.999970606871, 0.0], "align_point": [2800.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LP", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5494.285658541776, -349.999970606871, 0.0], "align_point": [5700.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.THKLNAP | 4B9 | PanelMouthSummary | [{"tag": "NAP2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10418.45235563553, -1350.000012069359, 0.0], "align_point": [10437.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "NAP1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [9043.452355635529, -1350.000012069359, 0.0], "align_point": [9062.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "THEPHINH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11793.45235563553, -1350.000012069359, 0.0], "align_point": [11812.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TONG", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7668.452355635529, -1350.000012069359, 0.0], "align_point": [7687.5, -1250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TieudeMC | 4D9 | BendingGroupTitle | [{"tag": "TIEUDE", "style": "VHG-Unicode.Bold", "height": 300.0, "width": null, "insert": [-9.2e-15, -150.0, 0.0], "align_point": [0.0, 0.0, 0.0], "halign": 1, "valign": 2, "rotation": null}] |
| TK.CT.N | 4E2 | NeedsVerifiedShapeRecipe | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4117.489041455313, -349.999897835974, 0.0], "align_point": [4650.822483935225, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5250.498763612002, -350.0000163002249, 0.0], "align_point": [5250.498785824049, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.CT.T | 505 | NeedsVerifiedShapeRecipe | [{"tag": "SH", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [1980.952355635529, -350.0000120693589, 0.0], "align_point": [2000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "L", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [4117.489041455313, -349.999897835974, 0.0], "align_point": [4650.822483935225, -250.0, 0.0], "halign": 2, "valign": 2, "rotation": 359.9999872734366}, {"tag": "LT", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [5250.498763612002, -350.0000163002249, 0.0], "align_point": [5250.498785824049, -250.0, 0.0], "halign": null, "valign": 2, "rotation": 359.9999872734366}, {"tag": "D", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [6385.71424487341, -349.9999909150283, 0.0], "align_point": [6500.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "DAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [7483.333267654019, -349.9999570680995, 0.0], "align_point": [7750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "SL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [8885.71424487341, -349.9999909150283, 0.0], "align_point": [9000.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "TDAI", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10135.71424487341, -349.9999909150283, 0.0], "align_point": [10250.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "KL", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11445.23802334917, -349.9999486063673, 0.0], "align_point": [11750.0, -250.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |
| TK.THKL.CT | 544 | NeedsVerifiedShapeRecipe | [{"tag": "CUATHU1", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [10418.45235563553, -2850.000012069359, 0.0], "align_point": [10437.5, -2750.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}, {"tag": "CUATHU2", "style": "VHG-Unicode.Regular", "height": 200.0000326004549, "width": 0.8, "insert": [11793.45235563553, -2850.000012069359, 0.0], "align_point": [11812.5, -2750.0, 0.0], "halign": 1, "valign": 2, "rotation": 359.9999872734366}] |

## Migration từ bản cũ

| ID | Mâu thuẫn phát hiện | Cách xử lý cụ thể | Status / nguồn |
|---|---|---|---|
| NR-IO-001 | Stage payload/05C chưa cùng schema;hai nhánh S7/S8 thiếu carrier rõ | Dùng cumulative immutable stage envelopes; Application adapters wrap leaf payload, Drawing planners không tham chiếu Application. Output N==Input N+1 by generated schema. | ResolvedArchitecture; Source05C/StageAdapters;leader software decision |
| NR-IO-002 | 205 input fields thiếu Material.G vữa đệm | Thêm F-206:CF.Ga!G34 loại2,!G36 loại3;only active BeddingMortar;không lấy Material.F hoặc M75 mẫu. | ResolvedSourceCorrection; Original CF.Ga material C:G headers |
| NR-IO-003 | W/bậc cổ/khung100/FinishedTop chưa khép kín cao độ | Giữ FrameBottom=NeckTop,FrameHeight100;contact equations của selected NR23 contract phải xác định NeckTop/PanelBottom/FinishedTop;thiếu field→DATUM_GRAPH_DISCONNECTED. | ApprovedPolicyBindingRequired; CONTRACT C01;NR23 accepted selected contact contract |
| NR-IO-004 | Legacy cửa thu dùng clearHeight như externalHeight | floorTop=hostTop-clearHeight;floorBottom=floorTop-base;source clearHeight note NOTE0411,PDF040 clear250/base125 chỉ evidence. | ResolvedSourceCorrection; CF.Ga G16/G17 type3;PDF040 |
| NR-IO-005 | Panel formula cũ luôn n+1 gaps;Cover=None chưa có aperture contract | Tách Recess/OverOuter theo CoverPanelBuilder;NoCover chỉ dùng explicit selected no-cover aperture/contact,không tự rawE11 authoritative. | ResolvedContractWithBinding; CoverPanelBuilder;NR23 approved proposal |
| NR-IO-006 | LadderTop legacy nhiều cao độ;note chỉ đỉnh ga | NR19 binding ladder_top_surface_id+min_z_expr bắt buộc;missing→RULE_BINDING_DATA_MISSING;không tự RoofTop/FinishedTop. | ApprovedPolicyBindingRequired; EX083;LadderPlanner;NR19 |
| NR-IO-007 | Haunch scalar chưa chứng minh b=h;C/R8 chưa chứng minh radius basis | Bắt buộc width/height expressions NR05;recipe fabrication centreline and nominal schedule separately NR02;missing payload blocks active feature. | ApprovedPolicyBindingRequired; EX020/026;NR02/05 approved proposals |
| NR-IO-008 | Catch Left/Right legacy sign so FRAME-001 khác observer | Center invariant;Neck forced Center;Body Left/Right requires named side_basis in NR19 placement binding;both signs explicitly implemented. | ApprovedPolicyBindingRequired; WallFrame FRAME001;CatchBasinBuilder;EX177 |
| NR-IO-009 | Draft box outer contour dùng offset Euclidean của inner chamfer | Outer là rectangle W+2t,H+2t;cut rectangle thêm2m;chamfer chỉ inner cells;surface cover clipping vẫn Euclidean offset riêng. | ResolvedSourceCorrection; PipeCrossSectionBuilder.BuildBox |
| NR-IO-010 | Draft activity F13/G13 gắn nhầm BTCT panel | EX042 chỉ Gang/Purchased;G13 là collar phía trên cổ. Active collar phải có selected source-backed profile/contact expressions;không thay Frame100 hoặc bỏ feature. | ResolvedSourceWithBinding; EX042/043;ManholeValidator collar branch;NR23 contact contract |
| NR-IO-011 | S7 cần anchor title/cao độ nhưng registry S6 chưa khai báo; lane thiếu side và nhầm port với insertion | S6 đăng ký Target2V2 ViewTitle/Elevation có ID/frame/point từ biểu thức nguồn. S7 join đúng target, local lane có side; tính vùng cấm theo ink tương đối với port sau scale/rotation. Nhánh accessory không sinh Gach của lưới; helper finite allocator có thân đầy đủ. | ResolvedArchitectureWithSourceBinding; 05C Target2V2;CONTRACT C06;NR16/22 đã duyệt;leader software correction |

## Typed DTO, namespace và dependency
### Domain DTO source

```csharp
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
    string? formula_id /* — */
);

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
```

### Drawing DTO source

```csharp
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
```

### Application DTO source

```csharp
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
```

## Toàn bộ field lá theo struct

### Point3V2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Point3V2.x_mm | double | mm | Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| Point3V2.y_mm | double | mm | Tọa độ Y của point argument, 05B-F26/F27. |
| Point3V2.z_mm | double | mm | Tọa độ Z của point argument, 05B-F26/F27. |

### Point2V2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Point2V2.x_mm | double | mm | Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| Point2V2.y_mm | double | mm | Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |

### Vector3V2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Vector3V2.x | double | dimensionless | Thành phần X của unit basis/normal; 05B-F25/F26. |
| Vector3V2.y | double | dimensionless | Thành phần Y của unit basis/normal; 05B-F25/F26. |
| Vector3V2.z | double | dimensionless | Thành phần Z của unit basis/normal; 05B-F25/F26. |

### Bounds2V2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Bounds2V2.min_x_mm | double | mm | min của ink/curve X sau transform; 05B-F42. |
| Bounds2V2.min_y_mm | double | mm | min của ink/curve Y sau transform; 05B-F42. |
| Bounds2V2.max_x_mm | double | mm | max của ink/curve X sau transform; 05B-F42. |
| Bounds2V2.max_y_mm | double | mm | max của ink/curve Y sau transform; 05B-F42. |

### FrameV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| FrameV2.id | string | — | ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| FrameV2.origin.x_mm | double | mm | Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| FrameV2.origin.y_mm | double | mm | Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| FrameV2.origin.z_mm | double | mm | Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| FrameV2.axis_u.x | double | dimensionless | Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| FrameV2.axis_u.y | double | dimensionless | Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| FrameV2.axis_u.z | double | dimensionless | Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| FrameV2.axis_v.x | double | dimensionless | Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| FrameV2.axis_v.y | double | dimensionless | Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| FrameV2.axis_v.z | double | dimensionless | Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| FrameV2.normal.x | double | dimensionless | cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| FrameV2.normal.y | double | dimensionless | cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| FrameV2.normal.z | double | dimensionless | cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| FrameV2.handedness | int | — | sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |

### SourceV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SourceV2.id | string | — | SHA256(canonical source descriptor); 05B-F46. |
| SourceV2.kind | string | — | WorkbookCell/PdfPage/SourceCode/RuleDocument/Dxf/Dwg/OwnerDecision/Request/Derived; giữ enum cũ khi migrate. |
| SourceV2.relative_path | string? | — | Pass-through path của bytes nguồn; Derived dùng derived/<RunId>/<id>. |
| SourceV2.sha256 | string | — | SHA256(bytes nguồn), Derived hash formula_id+parent_ids+effective value; 05B-F46. |
| SourceV2.sheet | string? | — | Tên sheet literal từ WorkbookCell; loại nguồn khác null. |
| SourceV2.cell | string? | — | Địa chỉ một ô; note và value khác ô phải tạo hai SourceV2. |
| SourceV2.page | int? | page | PdfPage one-based; nguồn khác null, 05B-F25. |
| SourceV2.handle | string? | — | Native Dxf/Dwg entity handle; không gán cho PDF page. |
| SourceV2.raw_text | string | — | RawText bất biến hoặc literal decision/request. |
| SourceV2.parent_ids[] | string[] | — | Derived giữ IDs nguồn đầu vào, không cycle; nguồn gốc có list rỗng. |
| SourceV2.formula_id | string? | — | ID 05B của phép biến đổi; pass-through/source literal null. |

### DiagnosticV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| DiagnosticV2.code | string | — | Mã RAISE được ghi trực tiếp trong pseudocode. |
| DiagnosticV2.stage | string | — | S0..S11 hoặc PRECHECK. |
| DiagnosticV2.manhole_id | string? | — | Pass-through context.manhole_id nếu đã có. |
| DiagnosticV2.subject_id | string? | — | Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| DiagnosticV2.field_paths[] | string[] | — | Paths cụ thể đã kiểm, không một thông báo chung. |
| DiagnosticV2.expected_text | string | — | Literal invariant/expected field value từ phép kiểm. |
| DiagnosticV2.actual_text | string | — | Serialize actual values bị lỗi, culture invariant. |
| DiagnosticV2.source_ids[] | string[] | — | IDs của các input gây lỗi, tra context.sources[]. |
| DiagnosticV2.needsrule_ids[] | string[] | — | NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| DiagnosticV2.severity | string | — | Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |

### ReceiptV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ReceiptV2.stage | string | — | Tên stage vừa thực hiện. |
| ReceiptV2.run_id | string | — | Pass-through context.run_id. |
| ReceiptV2.manhole_id | string | — | Pass-through context.manhole_id. |
| ReceiptV2.status | string | — | Ready/Partial/Blocked/Cancelled theo diagnostic và cancellation. |
| ReceiptV2.input_hash | string | — | Hash canonical input state trước stage, 05B-F46. |
| ReceiptV2.output_hash | string? | — | Hash ready payload; lỗi trước payload = null. |
| ReceiptV2.started_utc | string | UTC | Clock UTC lúc bắt đầu; không dùng làm freshness workbook. |
| ReceiptV2.finished_utc | string | UTC | Clock UTC lúc kết thúc stage, ghi kể cả lỗi. |

### ContextV2 — DBTool.Application

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ContextV2.run_id | string | — | Request RunId đã tạo trước S0; immutable cả batch. |
| ContextV2.job_id | string | — | ID job workbook hiện tại từ request.jobs[].job_id. |
| ContextV2.manhole_id | string | — | ID target hiện tại, không dựa G400/G500/name similarity. |
| ContextV2.row_index | int | row | Target row_index hiện tại; >=4 theo Vhg32Schema. |
| ContextV2.build_hash | string | — | Hash assembly/source build được host thực load, pass-through request. |
| ContextV2.template_hash | string | — | Hash bundle S0 đã kiểm, pass-through request.templates bundle hash. |
| ContextV2.rule_set_hash | string | — | Hash approved+proposal snapshots, 05B-F46; không sửa giữa batch. |
| ContextV2.cancel_requested | bool | — | Snapshot cancellation state tại entry/exit stage. |
| ContextV2.sources[].id | string | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / SHA256(canonical source descriptor); 05B-F46. |
| ContextV2.sources[].kind | string | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / WorkbookCell/PdfPage/SourceCode/RuleDocument/Dxf/Dwg/OwnerDecision/Request/Derived; giữ enum cũ khi migrate. |
| ContextV2.sources[].relative_path | string? | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / Pass-through path của bytes nguồn; Derived dùng derived/<RunId>/<id>. |
| ContextV2.sources[].sha256 | string | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / SHA256(bytes nguồn), Derived hash formula_id+parent_ids+effective value; 05B-F46. |
| ContextV2.sources[].sheet | string? | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / Tên sheet literal từ WorkbookCell; loại nguồn khác null. |
| ContextV2.sources[].cell | string? | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / Địa chỉ một ô; note và value khác ô phải tạo hai SourceV2. |
| ContextV2.sources[].page | int? | page | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / PdfPage one-based; nguồn khác null, 05B-F25. |
| ContextV2.sources[].handle | string? | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / Native Dxf/Dwg entity handle; không gán cho PDF page. |
| ContextV2.sources[].raw_text | string | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / RawText bất biến hoặc literal decision/request. |
| ContextV2.sources[].parent_ids[] | string[] | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / Derived giữ IDs nguồn đầu vào, không cycle; nguồn gốc có list rỗng. |
| ContextV2.sources[].formula_id | string? | — | Nguồn gốc+Derived append-only, ID không trùng khác nội dung. / ID 05B của phép biến đổi; pass-through/source literal null. |
| ContextV2.diagnostics[].code | string | — | Diagnostic của các stage đã chạy, append-only. / Mã RAISE được ghi trực tiếp trong pseudocode. |
| ContextV2.diagnostics[].stage | string | — | Diagnostic của các stage đã chạy, append-only. / S0..S11 hoặc PRECHECK. |
| ContextV2.diagnostics[].manhole_id | string? | — | Diagnostic của các stage đã chạy, append-only. / Pass-through context.manhole_id nếu đã có. |
| ContextV2.diagnostics[].subject_id | string? | — | Diagnostic của các stage đã chạy, append-only. / Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| ContextV2.diagnostics[].field_paths[] | string[] | — | Diagnostic của các stage đã chạy, append-only. / Paths cụ thể đã kiểm, không một thông báo chung. |
| ContextV2.diagnostics[].expected_text | string | — | Diagnostic của các stage đã chạy, append-only. / Literal invariant/expected field value từ phép kiểm. |
| ContextV2.diagnostics[].actual_text | string | — | Diagnostic của các stage đã chạy, append-only. / Serialize actual values bị lỗi, culture invariant. |
| ContextV2.diagnostics[].source_ids[] | string[] | — | Diagnostic của các stage đã chạy, append-only. / IDs của các input gây lỗi, tra context.sources[]. |
| ContextV2.diagnostics[].needsrule_ids[] | string[] | — | Diagnostic của các stage đã chạy, append-only. / NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| ContextV2.diagnostics[].severity | string | — | Diagnostic của các stage đã chạy, append-only. / Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |
| ContextV2.receipts[].stage | string | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Tên stage vừa thực hiện. |
| ContextV2.receipts[].run_id | string | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Pass-through context.run_id. |
| ContextV2.receipts[].manhole_id | string | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Pass-through context.manhole_id. |
| ContextV2.receipts[].status | string | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Ready/Partial/Blocked/Cancelled theo diagnostic và cancellation. |
| ContextV2.receipts[].input_hash | string | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Hash canonical input state trước stage, 05B-F46. |
| ContextV2.receipts[].output_hash | string? | — | Receipts append-only; mỗi lần chạy stage có record riêng. / Hash ready payload; lỗi trước payload = null. |
| ContextV2.receipts[].started_utc | string | UTC | Receipts append-only; mỗi lần chạy stage có record riêng. / Clock UTC lúc bắt đầu; không dùng làm freshness workbook. |
| ContextV2.receipts[].finished_utc | string | UTC | Receipts append-only; mỗi lần chạy stage có record riêng. / Clock UTC lúc kết thúc stage, ghi kể cả lỗi. |
| ContextV2.artifact_directory | string | — | Caller creates run output directory; every persisted file path stays inside this exact directory. |
| ContextV2.prior_row_results[].job_id | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Pass-through selected job identity. |
| ContextV2.prior_row_results[].row_index | int | row | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Exact selected row. |
| ContextV2.prior_row_results[].manhole_id | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Exact target ID. |
| ContextV2.prior_row_results[].status | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Complete/Partial/Blocked/Failed/Cancelled/NotRun, no silent missing row. |
| ContextV2.prior_row_results[].frame_ids[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Actual generated FrameRegistry IDs for row. |
| ContextV2.prior_row_results[].checks[].name | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Concrete numerical/semantic/visual check name. |
| ContextV2.prior_row_results[].checks[].subject_id | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Actual field/member/handle/frame/region being checked. |
| ContextV2.prior_row_results[].checks[].expected | double? | unit field | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Numerical expected from EffectiveInput/rules; visual/text=null. |
| ContextV2.prior_row_results[].checks[].actual | double? | unit field | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Actual native/PDF measurement; cannot be guessed from requested value. |
| ContextV2.prior_row_results[].checks[].expected_text | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Expected asset/tag/role/text for exact checks. |
| ContextV2.prior_row_results[].checks[].actual_text | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Native/PDF actual exact value. |
| ContextV2.prior_row_results[].checks[].unit | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / mm,m,kg,paper_mm,pdf_point,count,none. |
| ContextV2.prior_row_results[].checks[].tolerance_rule_id | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Approved NR18 numeric/visual tolerance; exact categorical checks=null. |
| ContextV2.prior_row_results[].checks[].source_ids[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Input/rule/reference sources supporting expected. |
| ContextV2.prior_row_results[].checks[].native_handles[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Actual subjects in saved output DWG. |
| ContextV2.prior_row_results[].checks[].evidence_paths[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Actual crop/PDF/readback/ledger paths with artifact hashes in run manifest. |
| ContextV2.prior_row_results[].checks[].verdict | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Passed/Failed/Blocked/NotRun/NA; NA requires reason. |
| ContextV2.prior_row_results[].checks[].reason | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / All required checks, actual evidence; no test counts as acceptance. / Missing approval/evidence/NA or numerical mismatch, Passed may null. |
| ContextV2.prior_row_results[].diagnostics[].code | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Mã RAISE được ghi trực tiếp trong pseudocode. |
| ContextV2.prior_row_results[].diagnostics[].stage | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / S0..S11 hoặc PRECHECK. |
| ContextV2.prior_row_results[].diagnostics[].manhole_id | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Pass-through context.manhole_id nếu đã có. |
| ContextV2.prior_row_results[].diagnostics[].subject_id | string? | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| ContextV2.prior_row_results[].diagnostics[].field_paths[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Paths cụ thể đã kiểm, không một thông báo chung. |
| ContextV2.prior_row_results[].diagnostics[].expected_text | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Literal invariant/expected field value từ phép kiểm. |
| ContextV2.prior_row_results[].diagnostics[].actual_text | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Serialize actual values bị lỗi, culture invariant. |
| ContextV2.prior_row_results[].diagnostics[].source_ids[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / IDs của các input gây lỗi, tra context.sources[]. |
| ContextV2.prior_row_results[].diagnostics[].needsrule_ids[] | string[] | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| ContextV2.prior_row_results[].diagnostics[].severity | string | — | Immutable accumulator from earlier requested rows; root orchestrator adds explicit failed/cancelled receipts before next row. S11 appends current result to batch.rows, not to input. / Row-specific diagnostics including unsuccessful stage. / Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |
| ContextV2.prior_plot_pages[].frame_id | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Registered source frame window ID. |
| ContextV2.prior_plot_pages[].pdf_path | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual generated vector PDF file path. |
| ContextV2.prior_plot_pages[].pdf_sha256 | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Hash actual PDF bytes, 05B-F46. |
| ContextV2.prior_plot_pages[].page_index | int | page | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual PDF page index one-based. |
| ContextV2.prior_plot_pages[].host_session_id | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual one healthy plot host session identity. |
| ContextV2.prior_plot_pages[].pstylemode | int | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Read actual native PSTYLEMODE, choose compatible CTB/STB explicitly. |
| ContextV2.prior_plot_pages[].plot_profile_id | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Approved selected profile/hash, no default acad.ctb unless approved. |
| ContextV2.prior_plot_pages[].nominal_scale | double | 1:S | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Frame requested scale. |
| ContextV2.prior_plot_pages[].actual_scale | double | 1:S | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Scale measured from native window→PDF mapping, 05B-F48. |
| ContextV2.prior_plot_pages[].transcript_path | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual host/plot transcript saved, not fabricated success text. |
| ContextV2.prior_plot_pages[].visual_status | string | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Passed/Failed/NotRun, actual reviewed regions required for Passed. |
| ContextV2.prior_plot_pages[].reviewer | string? | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual reviewer identity; null for NotRun. |
| ContextV2.prior_plot_pages[].reviewed_regions[] | string[] | — | Immutable prior actual plots from same healthy host session; S11 batch.pages appends current row. / Actual reviewed named regions; complete required coverage for visual Passed. |
| ContextV2.plot_session_id | string? | — | One shared healthy host session opened by root orchestrator; null blocks required plot with PLOT_SESSION_MISSING. |

### TargetV2 — DBTool.Application

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| TargetV2.row_index | int | row | Request selection; main rows >=4. |
| TargetV2.manhole_id | string | — | Stable request target ID, required; host tạo/persist khi user chọn row. |
| TargetV2.expected_generation_id | string? | — | Required per target for Replace; exact repository generation, not ManholeId inferred from text. |
| TargetV2.expected_input_hash | string? | — | Expected previous generation input hash for Replace, distinct from new computed input hash. |
| TargetV2.expected_document_token | string? | — | Expected current target CAD document identity token. |
| TargetV2.requested_generation_id | string? | — | Explicit new generation ID for Create; null allows host GUID allocation stored in receipt. |

### JobV2 — DBTool.Application

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| JobV2.job_id | string | — | Unique request ID của workbook job. |
| JobV2.logical_source_id | string | — | Stable project workbook identity từ request, không là full-content SHA. |
| JobV2.workbook_path | string | — | Path workbook cần snapshot, không ghi vào nguồn. |
| JobV2.workbook_bytes[] | byte[] | — | ReadAllBytes đúng một lần trước S0; giữ immutable cả batch. |
| JobV2.workbook_sha256 | string | — | SHA256(workbook_bytes), 05B-F46. |
| JobV2.profile | string | — | GaThamLienKhoi/GaThamNapRoi/GaThamThuNapRoi; explicit request. |
| JobV2.main_sheet | string | — | Exact sheet theo profile, không suy từ tên ga. |
| JobV2.numeric_culture | string | — | Explicit culture của parse, không đoán 1,234. |
| JobV2.targets[].row_index | int | row | Selected rows/ManholeIds, unique trong batch. / Request selection; main rows >=4. |
| JobV2.targets[].manhole_id | string | — | Selected rows/ManholeIds, unique trong batch. / Stable request target ID, required; host tạo/persist khi user chọn row. |
| JobV2.targets[].expected_generation_id | string? | — | Selected rows/ManholeIds, unique trong batch. / Required per target for Replace; exact repository generation, not ManholeId inferred from text. |
| JobV2.targets[].expected_input_hash | string? | — | Selected rows/ManholeIds, unique trong batch. / Expected previous generation input hash for Replace, distinct from new computed input hash. |
| JobV2.targets[].expected_document_token | string? | — | Selected rows/ManholeIds, unique trong batch. / Expected current target CAD document identity token. |
| JobV2.targets[].requested_generation_id | string? | — | Selected rows/ManholeIds, unique trong batch. / Explicit new generation ID for Create; null allows host GUID allocation stored in receipt. |
| JobV2.recalculation_receipt_path | string? | — | Optional actual recalculation certificate for this byte snapshot; active Formula without matching certificate is blocked. |
| JobV2.recalculation_receipt_sha256 | string? | — | Hash actual recalc receipt bytes; null iff path null. |

### RequestV2 — DBTool.Application

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RequestV2.jobs[].job_id | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Unique request ID của workbook job. |
| RequestV2.jobs[].logical_source_id | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Stable project workbook identity từ request, không là full-content SHA. |
| RequestV2.jobs[].workbook_path | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Path workbook cần snapshot, không ghi vào nguồn. |
| RequestV2.jobs[].workbook_bytes[] | byte[] | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / ReadAllBytes đúng một lần trước S0; giữ immutable cả batch. |
| RequestV2.jobs[].workbook_sha256 | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / SHA256(workbook_bytes), 05B-F46. |
| RequestV2.jobs[].profile | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / GaThamLienKhoi/GaThamNapRoi/GaThamThuNapRoi; explicit request. |
| RequestV2.jobs[].main_sheet | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Exact sheet theo profile, không suy từ tên ga. |
| RequestV2.jobs[].numeric_culture | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Explicit culture của parse, không đoán 1,234. |
| RequestV2.jobs[].targets[].row_index | int | row | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Request selection; main rows >=4. |
| RequestV2.jobs[].targets[].manhole_id | string | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Stable request target ID, required; host tạo/persist khi user chọn row. |
| RequestV2.jobs[].targets[].expected_generation_id | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Required per target for Replace; exact repository generation, not ManholeId inferred from text. |
| RequestV2.jobs[].targets[].expected_input_hash | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Expected previous generation input hash for Replace, distinct from new computed input hash. |
| RequestV2.jobs[].targets[].expected_document_token | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Expected current target CAD document identity token. |
| RequestV2.jobs[].targets[].requested_generation_id | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Selected rows/ManholeIds, unique trong batch. / Explicit new generation ID for Create; null allows host GUID allocation stored in receipt. |
| RequestV2.jobs[].recalculation_receipt_path | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Optional actual recalculation certificate for this byte snapshot; active Formula without matching certificate is blocked. |
| RequestV2.jobs[].recalculation_receipt_sha256 | string? | — | Frozen workbook jobs do entrypoint UI/Headless truyền. / Hash actual recalc receipt bytes; null iff path null. |
| RequestV2.target_dwg_path | string | — | Output copy hoặc drawing target do request chọn. |
| RequestV2.generation_mode | string | — | Create/Update; unknown RAISE REQUEST_MODE_INVALID. |
| RequestV2.anchor.x_mm | double | mm | Explicit insertion point của request; không preset theo fixture. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RequestV2.anchor.y_mm | double | mm | Explicit insertion point của request; không preset theo fixture. / Tọa độ Y của point argument, 05B-F26/F27. |
| RequestV2.anchor.z_mm | double | mm | Explicit insertion point của request; không preset theo fixture. / Tọa độ Z của point argument, 05B-F26/F27. |
| RequestV2.body_scale | double | 1:S | Explicit body denominator >0, HDSD p24–25. |
| RequestV2.panel_scale | double? | 1:S | Override request nếu có; nếu null lấy raw Panel.CoverRatioPercent=M15, không phần trăm. |
| RequestV2.scale_mode | string | — | Fixed/Auto; Auto candidates lấy approved NR22, không list số tự đặt. |
| RequestV2.elevation_mode_override | string? | — | null dùng Excel Elevation.C; nếu override phải có SourceV2 Request. |
| RequestV2.draw_scope | string | — | Full/GeometryOnly; GeometryOnly không công bố FullRebarFinal. |
| RequestV2.max_numeric_count | int | count | Resource limit explicit request; quá limit RAISE RESOURCE_LIMIT, không clamp engineering input. |
| RequestV2.plot_profile_id | string | — | Exact approved profile ID từ request/rules NR18. |
| RequestV2.template_manifest_path | string | — | Exact source manifest được chọn; S0 kiểm SHA. |
| RequestV2.template_manifest_sha256 | string | — | Hash bytes manifest, không hash tên file. |
| RequestV2.rule_snapshot_path | string | — | Exact versioned rule snapshot input. |
| RequestV2.rule_snapshot_sha256 | string | — | Hash rule bytes; Proposal vẫn giữ nguyên Proposal. |
| RequestV2.reference_bundle_ids[] | string[] | — | Explicit source bundles để so recipe; không ghép Modelordinal. |

### AttributeDefV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AttributeDefV2.tag | string | — | Pass-through ATTDEF.Tag literal, exact case theo schema nguồn. |
| AttributeDefV2.default_text | string | — | ATTDEF specimen, tuyệt đối không là quantity output. |
| AttributeDefV2.style_id | string | — | ATTDEF text style exact key trong selected bundle. |
| AttributeDefV2.height_mm | double | native mm | ATTDEF.Height từ native definition, không universal200/300. |
| AttributeDefV2.width_factor | double | dimensionless | ATTDEF.WidthFactor từ native source. |
| AttributeDefV2.insertion.x_mm | double | mm | Native ATTDEF insertion point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AttributeDefV2.insertion.y_mm | double | mm | Native ATTDEF insertion point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AttributeDefV2.insertion.z_mm | double | mm | Native ATTDEF insertion point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AttributeDefV2.alignment.x_mm | double | mm | Native ATTDEF alignment point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AttributeDefV2.alignment.y_mm | double | mm | Native ATTDEF alignment point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AttributeDefV2.alignment.z_mm | double | mm | Native ATTDEF alignment point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AttributeDefV2.rotation_rad | double | rad | Native rotation converted đúng một lần, 05B-F25. |
| AttributeDefV2.horizontal_mode | string | — | Native justification enum name, pass-through. |
| AttributeDefV2.vertical_mode | string | — | Native vertical alignment enum name, pass-through. |

### AssetV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AssetV2.name | string | — | Exact block name từ selected native bundle, không fuzzy. |
| AssetV2.definition_handle | string | — | Native definition handle dùng lookup/readback, không entity output handle. |
| AssetV2.content_hash | string | — | Hash native definition signature+dependencies, 05B-F46. |
| AssetV2.base_point.x_mm | double | mm | Native definition origin, không bbox-center. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetV2.base_point.y_mm | double | mm | Native definition origin, không bbox-center. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetV2.base_point.z_mm | double | mm | Native definition origin, không bbox-center. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetV2.bounds.min_x_mm | double | mm | Native source extents của immutable glyph, không dùng tính Li. / min của ink/curve X sau transform; 05B-F42. |
| AssetV2.bounds.min_y_mm | double | mm | Native source extents của immutable glyph, không dùng tính Li. / min của ink/curve Y sau transform; 05B-F42. |
| AssetV2.bounds.max_x_mm | double | mm | Native source extents của immutable glyph, không dùng tính Li. / max của ink/curve X sau transform; 05B-F42. |
| AssetV2.bounds.max_y_mm | double | mm | Native source extents của immutable glyph, không dùng tính Li. / max của ink/curve Y sau transform; 05B-F42. |
| AssetV2.role | string | — | Exact role binding từ asset manifest; missing role Block. |
| AssetV2.child_names[] | string[] | — | Native INSERT dependencies exact names. |
| AssetV2.attributes[].tag | string | — | Đủ ATTDEF schema của definition, không thêm tag bịa. / Pass-through ATTDEF.Tag literal, exact case theo schema nguồn. |
| AssetV2.attributes[].default_text | string | — | Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF specimen, tuyệt đối không là quantity output. |
| AssetV2.attributes[].style_id | string | — | Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF text style exact key trong selected bundle. |
| AssetV2.attributes[].height_mm | double | native mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF.Height từ native definition, không universal200/300. |
| AssetV2.attributes[].width_factor | double | dimensionless | Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF.WidthFactor từ native source. |
| AssetV2.attributes[].insertion.x_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetV2.attributes[].insertion.y_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetV2.attributes[].insertion.z_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetV2.attributes[].alignment.x_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetV2.attributes[].alignment.y_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetV2.attributes[].alignment.z_mm | double | mm | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetV2.attributes[].rotation_rad | double | rad | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native rotation converted đúng một lần, 05B-F25. |
| AssetV2.attributes[].horizontal_mode | string | — | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native justification enum name, pass-through. |
| AssetV2.attributes[].vertical_mode | string | — | Đủ ATTDEF schema của definition, không thêm tag bịa. / Native vertical alignment enum name, pass-through. |
| AssetV2.layer_id | string | — | Native layer signature key của source. |
| AssetV2.style_id | string? | — | Native style key nếu asset có text; không text null. |
| AssetV2.source_ids[] | string[] | — | SourceV2 Dwg/Dxf/RuleDocument của definition và role. |

### GlyphV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| GlyphV2.style_id | string | — | Font/style hash approved NR16. |
| GlyphV2.codepoint | int | Unicode | Unicode scalar sau encoding decoder đã duyệt. |
| GlyphV2.advance_native_mm | double | native mm | Measured glyph advance tại native height của style; không đoán bằng char count. |
| GlyphV2.ink.min_x_mm | double | mm | Measured actual glyph ink box tại origin. / min của ink/curve X sau transform; 05B-F42. |
| GlyphV2.ink.min_y_mm | double | mm | Measured actual glyph ink box tại origin. / min của ink/curve Y sau transform; 05B-F42. |
| GlyphV2.ink.max_x_mm | double | mm | Measured actual glyph ink box tại origin. / max của ink/curve X sau transform; 05B-F42. |
| GlyphV2.ink.max_y_mm | double | mm | Measured actual glyph ink box tại origin. / max của ink/curve Y sau transform; 05B-F42. |
| GlyphV2.font_file_hash | string | — | Hash font thực dùng để đo. |
| GlyphV2.reference_height_mm | double | native mm | Font metric normalization height supplied by S0 provider; not assumed equal to ATTDEF height. |

### AssetsV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AssetsV2.bundle_hash | string | — | Hash selected bundle checked S0. |
| AssetsV2.definitions[].name | string | — | Exact asset registry từ native manifest/import preflight. / Exact block name từ selected native bundle, không fuzzy. |
| AssetsV2.definitions[].definition_handle | string | — | Exact asset registry từ native manifest/import preflight. / Native definition handle dùng lookup/readback, không entity output handle. |
| AssetsV2.definitions[].content_hash | string | — | Exact asset registry từ native manifest/import preflight. / Hash native definition signature+dependencies, 05B-F46. |
| AssetsV2.definitions[].base_point.x_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native definition origin, không bbox-center. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.definitions[].base_point.y_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native definition origin, không bbox-center. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].base_point.z_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native definition origin, không bbox-center. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].bounds.min_x_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native source extents của immutable glyph, không dùng tính Li. / min của ink/curve X sau transform; 05B-F42. |
| AssetsV2.definitions[].bounds.min_y_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native source extents của immutable glyph, không dùng tính Li. / min của ink/curve Y sau transform; 05B-F42. |
| AssetsV2.definitions[].bounds.max_x_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native source extents của immutable glyph, không dùng tính Li. / max của ink/curve X sau transform; 05B-F42. |
| AssetsV2.definitions[].bounds.max_y_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Native source extents của immutable glyph, không dùng tính Li. / max của ink/curve Y sau transform; 05B-F42. |
| AssetsV2.definitions[].role | string | — | Exact asset registry từ native manifest/import preflight. / Exact role binding từ asset manifest; missing role Block. |
| AssetsV2.definitions[].child_names[] | string[] | — | Exact asset registry từ native manifest/import preflight. / Native INSERT dependencies exact names. |
| AssetsV2.definitions[].attributes[].tag | string | — | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Pass-through ATTDEF.Tag literal, exact case theo schema nguồn. |
| AssetsV2.definitions[].attributes[].default_text | string | — | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF specimen, tuyệt đối không là quantity output. |
| AssetsV2.definitions[].attributes[].style_id | string | — | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF text style exact key trong selected bundle. |
| AssetsV2.definitions[].attributes[].height_mm | double | native mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF.Height từ native definition, không universal200/300. |
| AssetsV2.definitions[].attributes[].width_factor | double | dimensionless | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / ATTDEF.WidthFactor từ native source. |
| AssetsV2.definitions[].attributes[].insertion.x_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.definitions[].attributes[].insertion.y_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].attributes[].insertion.z_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF insertion point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].attributes[].alignment.x_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.definitions[].attributes[].alignment.y_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].attributes[].alignment.z_mm | double | mm | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native ATTDEF alignment point, pass-through. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.definitions[].attributes[].rotation_rad | double | rad | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native rotation converted đúng một lần, 05B-F25. |
| AssetsV2.definitions[].attributes[].horizontal_mode | string | — | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native justification enum name, pass-through. |
| AssetsV2.definitions[].attributes[].vertical_mode | string | — | Exact asset registry từ native manifest/import preflight. / Đủ ATTDEF schema của definition, không thêm tag bịa. / Native vertical alignment enum name, pass-through. |
| AssetsV2.definitions[].layer_id | string | — | Exact asset registry từ native manifest/import preflight. / Native layer signature key của source. |
| AssetsV2.definitions[].style_id | string? | — | Exact asset registry từ native manifest/import preflight. / Native style key nếu asset có text; không text null. |
| AssetsV2.definitions[].source_ids[] | string[] | — | Exact asset registry từ native manifest/import preflight. / SourceV2 Dwg/Dxf/RuleDocument của definition và role. |
| AssetsV2.glyphs[].style_id | string | — | Approved font metrics được đo/cached theo style/font hash. / Font/style hash approved NR16. |
| AssetsV2.glyphs[].codepoint | int | Unicode | Approved font metrics được đo/cached theo style/font hash. / Unicode scalar sau encoding decoder đã duyệt. |
| AssetsV2.glyphs[].advance_native_mm | double | native mm | Approved font metrics được đo/cached theo style/font hash. / Measured glyph advance tại native height của style; không đoán bằng char count. |
| AssetsV2.glyphs[].ink.min_x_mm | double | mm | Approved font metrics được đo/cached theo style/font hash. / Measured actual glyph ink box tại origin. / min của ink/curve X sau transform; 05B-F42. |
| AssetsV2.glyphs[].ink.min_y_mm | double | mm | Approved font metrics được đo/cached theo style/font hash. / Measured actual glyph ink box tại origin. / min của ink/curve Y sau transform; 05B-F42. |
| AssetsV2.glyphs[].ink.max_x_mm | double | mm | Approved font metrics được đo/cached theo style/font hash. / Measured actual glyph ink box tại origin. / max của ink/curve X sau transform; 05B-F42. |
| AssetsV2.glyphs[].ink.max_y_mm | double | mm | Approved font metrics được đo/cached theo style/font hash. / Measured actual glyph ink box tại origin. / max của ink/curve Y sau transform; 05B-F42. |
| AssetsV2.glyphs[].font_file_hash | string | — | Approved font metrics được đo/cached theo style/font hash. / Hash font thực dùng để đo. |
| AssetsV2.glyphs[].reference_height_mm | double | native mm | Approved font metrics được đo/cached theo style/font hash. / Font metric normalization height supplied by S0 provider; not assumed equal to ATTDEF height. |
| AssetsV2.font_policy_id | string | — | NR16 approved version key; chưa có rule thì S0 Block Final. |
| AssetsV2.units | string | — | Native mm; khác unit phải explicit conversion receipt, 05B-F01. |
| AssetsV2.products[].id | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Versioned selected product/PanelAssemblyContract ID. |
| AssetsV2.products[].code | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Exact catalog product code. |
| AssetsV2.products[].plan_asset | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Exact MB name verified S0. |
| AssetsV2.products[].section_aa_asset | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Exact AA native name verified S0. |
| AssetsV2.products[].section_bb_asset | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Exact BB native name verified S0. |
| AssetsV2.products[].aperture_x_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Measured/approved clear aperture X, independent of outside bounds. |
| AssetsV2.products[].aperture_y_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Measured/approved clear aperture Y. |
| AssetsV2.products[].rotation_rad | double | rad | Native/owner-approved product aperture/contact contracts, not guessed from names. / Explicit source registration rotation; approved support set checked S2. |
| AssetsV2.products[].aperture[].id | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / ENCODE(owner,loop_role,index), 05B-F46. |
| AssetsV2.products[].aperture[].role | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| AssetsV2.products[].aperture[].curves[].id | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| AssetsV2.products[].aperture[].curves[].kind | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| AssetsV2.products[].aperture[].curves[].start.x_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.products[].aperture[].curves[].start.y_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.products[].aperture[].curves[].start.z_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.products[].aperture[].curves[].end.x_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.products[].aperture[].curves[].end.y_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.products[].aperture[].curves[].end.z_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.products[].aperture[].curves[].center.x_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent AssetsV2.products[].aperture[].curves[].center |
| AssetsV2.products[].aperture[].curves[].center.y_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent AssetsV2.products[].aperture[].curves[].center |
| AssetsV2.products[].aperture[].curves[].center.z_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent AssetsV2.products[].aperture[].curves[].center |
| AssetsV2.products[].aperture[].curves[].normal.x | double | dimensionless | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent AssetsV2.products[].aperture[].curves[].normal |
| AssetsV2.products[].aperture[].curves[].normal.y | double | dimensionless | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent AssetsV2.products[].aperture[].curves[].normal |
| AssetsV2.products[].aperture[].curves[].normal.z | double | dimensionless | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent AssetsV2.products[].aperture[].curves[].normal |
| AssetsV2.products[].aperture[].curves[].radius_mm | double? | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| AssetsV2.products[].aperture[].curves[].sweep_rad | double? | rad | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| AssetsV2.products[].aperture[].curves[].role | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| AssetsV2.products[].aperture[].closed | bool | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| AssetsV2.products[].aperture[].signed_area_mm2 | double | mm2 | Native/owner-approved product aperture/contact contracts, not guessed from names. / Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Analytic line/arc Green area, 05B-F37. |
| AssetsV2.products[].contact_equations[].surface_a | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Named product/support datums, 05B-F28. / Datum ID vế trái, exact SurfaceId. |
| AssetsV2.products[].contact_equations[].surface_b | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Named product/support datums, 05B-F28. / Datum ID vế phải, exact SurfaceId. |
| AssetsV2.products[].contact_equations[].delta_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Named product/support datums, 05B-F28. / Equation z(a)=z(b)+delta from input/contact evidence, 05B-F28. |
| AssetsV2.products[].contact_equations[].source_ids[] | string[] | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Named product/support datums, 05B-F28. / Native product contact/owner/input evidence, không outside bbox. |
| AssetsV2.products[].approved | bool | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Approval/source evidence of product/contact contract, not asset existence. |
| AssetsV2.products[].source_ids[] | string[] | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Native measurements and owner confirmation references. |
| AssetsV2.products[].placement_surface_id | string | — | Native/owner-approved product aperture/contact contracts, not guessed from names. / Approved placement datum SurfaceId, e.g NeckTop or SeatPlane; not guessed. |
| AssetsV2.products[].registration_offset.x_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Native measured/approved placement offset, not outside bbox center. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AssetsV2.products[].registration_offset.y_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Native measured/approved placement offset, not outside bbox center. / Tọa độ Y của point argument, 05B-F26/F27. |
| AssetsV2.products[].registration_offset.z_mm | double | mm | Native/owner-approved product aperture/contact contracts, not guessed from names. / Native measured/approved placement offset, not outside bbox center. / Tọa độ Z của point argument, 05B-F26/F27. |
| AssetsV2.kerning[].style_id | string | — | Actual font kerning pairs for required literal text corpus, 05B-F40; missing metrics Block. / Same approved font/style key as glyphs. |
| AssetsV2.kerning[].left_codepoint | int | Unicode | Actual font kerning pairs for required literal text corpus, 05B-F40; missing metrics Block. / First character of measured kerning pair. |
| AssetsV2.kerning[].right_codepoint | int | Unicode | Actual font kerning pairs for required literal text corpus, 05B-F40; missing metrics Block. / Second character of measured kerning pair. |
| AssetsV2.kerning[].adjustment_native_mm | double | native mm | Actual font kerning pairs for required literal text corpus, 05B-F40; missing metrics Block. / Measured advance adjustment from approved font provider. |
| AssetsV2.layers[].layer_id | string | — | Native layer signatures/role bindings/plot stroke metrics. / Exact native layer ID/name. |
| AssetsV2.layers[].semantic_role | string | — | Native layer signatures/role bindings/plot stroke metrics. / Role binding from selected template manifest. |
| AssetsV2.layers[].stroke_paper_mm | double | paper mm | Native layer signatures/role bindings/plot stroke metrics. / Native lineweight/approved plot style effective width; hairline0 only if source/profile says0. |
| AssetsV2.layers[].source_ids[] | string[] | — | Native layer signatures/role bindings/plot stroke metrics. / Layer/plot policy evidence, NR18 when profile changes. |
| AssetsV2.font_styles[].style_id | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / Exact native style binding. |
| AssetsV2.font_styles[].font_path | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / Approved font file path. |
| AssetsV2.font_styles[].font_hash | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / Hash actual font bytes, NR16. |
| AssetsV2.font_styles[].encoding | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / Unicode/TCVN3/VNI approved decoder. |
| AssetsV2.font_styles[].kerning_mode | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / None/SparseComplete/QueryRequired, explicit metric provider certificate. |
| AssetsV2.font_styles[].provider_version | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / Measured metric provider version/hash; no char-count substitute. |
| AssetsV2.font_styles[].normalization | string | — | Font engine/encoding/kerning metadata required for exact native ink measurement. / None/NFC explicitly declared by approved FontPolicy, no silent text normalization. |

### ParamV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ParamV2.key | string | — | Closed parameter key trong rule schema; unknown reject. |
| ParamV2.kind | string | — | Number/Text/Bool; đúng một value slot được điền. |
| ParamV2.number | double? | unit field | Approved numeric parameter, finite; khác kind null. |
| ParamV2.text | string? | — | Approved text/enum parameter; khác kind null. |
| ParamV2.flag | bool? | — | Approved boolean parameter; khác kind null. |
| ParamV2.unit | string | — | mm/m/kg/rad/paper_mm/dimensionless/none; theo schema key. |
| ParamV2.source_ids[] | string[] | — | Source evidence/owner decision của parameter; không list rỗng. |

### ExprNodeV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ExprNodeV2.id | string | — | Unique expression node ID trong RuleV2, không cycle. |
| ExprNodeV2.op | string | — | CONST/REF/ADD/SUB/MUL/DIV/MIN/MAX/SQRT/SIN/COS/TAN/ATAN2/CEIL/FLOOR/ROUND; exhaustive helper EVAL. |
| ExprNodeV2.arg_ids[] | string[] | — | Node children theo đúng thứ tự operator; arity kiểm trong EVAL. |
| ExprNodeV2.ref_path | string? | — | REF đọc approved parameter hoặc typed numeric field trong environment; khác REF null. |
| ExprNodeV2.constant | double? | unit field | CONST numeric source-backed; khác CONST null. |
| ExprNodeV2.unit | string | — | Output unit của node, checked dimensional arithmetic. |
| ExprNodeV2.source_ids[] | string[] | — | CONSTANT/formula source IDs; derived node ghi parent source IDs. |
| ExprNodeV2.rounding_mode | string? | — | ROUND only: ToEven/AwayFromZero from approved NR13/06; other operators null. |

### ShapeCornerV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ShapeCornerV2.role | string | — | Ordered corner/end role từ approved shape recipe NR02/04/05/07/08/19. |
| ShapeCornerV2.x_expr | string | — | ExprNode ID cho X trong host support frame, mm. |
| ShapeCornerV2.y_expr | string | — | ExprNode ID cho Y trong host support frame, mm. |
| ShapeCornerV2.z_expr | string | — | ExprNode ID cho Z/depth trong host support frame, mm. |
| ShapeCornerV2.bend_radius_mm | double? | mm | null ở endpoint hoặc sharp-approved corner; radius >0 theo approved bend. |
| ShapeCornerV2.radius_basis | string? | — | Inner/Centreline; null ở endpoint, không tự chọn R8 basis. |

### TagBindingV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| TagBindingV2.tag | string | — | Exact native tag tên trong AssetV2.attributes. |
| TagBindingV2.operation | string | — | Mark/Diameter/Pitch/MeanLength/RawSumMetres/Count/Mass/MeanRole/FixedRole/Literal/SizePair; exhaustive S8 binder. |
| TagBindingV2.role | string? | — | SegmentRole hoặc literal key khi operation cần; không parse số từ glyph. |
| TagBindingV2.literal | string? | — | Chỉ Literal hoặc fixed sourced glyph caption; quantity không lấy ATT default. |
| TagBindingV2.unit | string | — | Typed value unit: mm,m,kg,count,none,kg/m. |

### ShapeRecipeV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ShapeRecipeV2.id | string | — | Exact recipe ID, source-backed scope; không chọn theo tên ga. |
| ShapeRecipeV2.component_kind | string | — | Base/Wall/Roof/Neck/Panel/Catch/Accessory theo scope. |
| ShapeRecipeV2.family | string | — | MainMesh/Tie/Ladder/Dowel/Haunch/Lifting/Anchor/Angle/OpeningReinforcement. |
| ShapeRecipeV2.shape_kind | string | — | Straight/C/U/Z/Loop/Rectangle/Lifting/CustomApproved. |
| ShapeRecipeV2.corners[].role | string | — | Ordered centreline/nominal corner expressions; basis checked before path. / Ordered corner/end role từ approved shape recipe NR02/04/05/07/08/19. |
| ShapeRecipeV2.corners[].x_expr | string | — | Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho X trong host support frame, mm. |
| ShapeRecipeV2.corners[].y_expr | string | — | Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Y trong host support frame, mm. |
| ShapeRecipeV2.corners[].z_expr | string | — | Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Z/depth trong host support frame, mm. |
| ShapeRecipeV2.corners[].bend_radius_mm | double? | mm | Ordered centreline/nominal corner expressions; basis checked before path. / null ở endpoint hoặc sharp-approved corner; radius >0 theo approved bend. |
| ShapeRecipeV2.corners[].radius_basis | string? | — | Ordered centreline/nominal corner expressions; basis checked before path. / Inner/Centreline; null ở endpoint, không tự chọn R8 basis. |
| ShapeRecipeV2.length_expr_id | string | — | Approved schedule length expression ID; fabrication path length tính riêng. |
| ShapeRecipeV2.measurement_basis | string | — | Centreline/NominalApproved; không đổi theo renderer. |
| ShapeRecipeV2.native_asset | string | — | Exact TK asset; thiếu mapping = NATIVE_SCHEDULE_SHAPE_UNMAPPED. |
| ShapeRecipeV2.fixed_glyph_hash | string | — | Hash glyph native content, bất biến khi Li thay đổi. |
| ShapeRecipeV2.tag_bindings[].tag | string | — | Closed mapping exact tags, đủ và không dư. / Exact native tag tên trong AssetV2.attributes. |
| ShapeRecipeV2.tag_bindings[].operation | string | — | Closed mapping exact tags, đủ và không dư. / Mark/Diameter/Pitch/MeanLength/RawSumMetres/Count/Mass/MeanRole/FixedRole/Literal/SizePair; exhaustive S8 binder. |
| ShapeRecipeV2.tag_bindings[].role | string? | — | Closed mapping exact tags, đủ và không dư. / SegmentRole hoặc literal key khi operation cần; không parse số từ glyph. |
| ShapeRecipeV2.tag_bindings[].literal | string? | — | Closed mapping exact tags, đủ và không dư. / Chỉ Literal hoặc fixed sourced glyph caption; quantity không lấy ATT default. |
| ShapeRecipeV2.tag_bindings[].unit | string | — | Closed mapping exact tags, đủ và không dư. / Typed value unit: mm,m,kg,count,none,kg/m. |
| ShapeRecipeV2.source_ids[] | string[] | — | Reference glyph/role/owner decisions; chưa verified = rule chưa approved. |
| ShapeRecipeV2.fabrication_basis | string | — | Centreline only when path/radius/nominal-to-centreline conversion approved; otherwise feature NeedsRule. |
| ShapeRecipeV2.presentation_length_basis | string | — | Schedule/Fabrication explicitly approved for this shape/role; no silent substitution. |

### RuleV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RuleV2.id | string | — | NR/approved rule ID, source snapshot exact. |
| RuleV2.version | string | — | Immutable version string, không dùng latest heuristic. |
| RuleV2.status | string | — | Proposal/Approved/Rejected; Proposal không cấp numeric production values. |
| RuleV2.approver | string? | — | Required khi Approved, else null hoặc draft metadata. |
| RuleV2.approved_at_utc | string? | UTC | Required khi Approved, không timestamp file thay approval. |
| RuleV2.profiles[] | string[] | — | Explicit scope profiles; rỗng không tự là all. |
| RuleV2.components[] | string[] | — | Explicit component scope. |
| RuleV2.shape_kinds[] | string[] | — | Explicit shape scope. |
| RuleV2.parameters[].key | string | — | Closed named parameter list với types/units/source kiểm S0. / Closed parameter key trong rule schema; unknown reject. |
| RuleV2.parameters[].kind | string | — | Closed named parameter list với types/units/source kiểm S0. / Number/Text/Bool; đúng một value slot được điền. |
| RuleV2.parameters[].number | double? | unit field | Closed named parameter list với types/units/source kiểm S0. / Approved numeric parameter, finite; khác kind null. |
| RuleV2.parameters[].text | string? | — | Closed named parameter list với types/units/source kiểm S0. / Approved text/enum parameter; khác kind null. |
| RuleV2.parameters[].flag | bool? | — | Closed named parameter list với types/units/source kiểm S0. / Approved boolean parameter; khác kind null. |
| RuleV2.parameters[].unit | string | — | Closed named parameter list với types/units/source kiểm S0. / mm/m/kg/rad/paper_mm/dimensionless/none; theo schema key. |
| RuleV2.parameters[].source_ids[] | string[] | — | Closed named parameter list với types/units/source kiểm S0. / Source evidence/owner decision của parameter; không list rỗng. |
| RuleV2.expressions[].id | string | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / Unique expression node ID trong RuleV2, không cycle. |
| RuleV2.expressions[].op | string | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / CONST/REF/ADD/SUB/MUL/DIV/MIN/MAX/SQRT/SIN/COS/TAN/ATAN2/CEIL/FLOOR/ROUND; exhaustive helper EVAL. |
| RuleV2.expressions[].arg_ids[] | string[] | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / Node children theo đúng thứ tự operator; arity kiểm trong EVAL. |
| RuleV2.expressions[].ref_path | string? | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / REF đọc approved parameter hoặc typed numeric field trong environment; khác REF null. |
| RuleV2.expressions[].constant | double? | unit field | Finite DAG, helper EVAL kiểm arity/cycle/units. / CONST numeric source-backed; khác CONST null. |
| RuleV2.expressions[].unit | string | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / Output unit của node, checked dimensional arithmetic. |
| RuleV2.expressions[].source_ids[] | string[] | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / CONSTANT/formula source IDs; derived node ghi parent source IDs. |
| RuleV2.expressions[].rounding_mode | string? | — | Finite DAG, helper EVAL kiểm arity/cycle/units. / ROUND only: ToEven/AwayFromZero from approved NR13/06; other operators null. |
| RuleV2.shapes[].id | string | — | Approved physical/presentation recipes cho scope. / Exact recipe ID, source-backed scope; không chọn theo tên ga. |
| RuleV2.shapes[].component_kind | string | — | Approved physical/presentation recipes cho scope. / Base/Wall/Roof/Neck/Panel/Catch/Accessory theo scope. |
| RuleV2.shapes[].family | string | — | Approved physical/presentation recipes cho scope. / MainMesh/Tie/Ladder/Dowel/Haunch/Lifting/Anchor/Angle/OpeningReinforcement. |
| RuleV2.shapes[].shape_kind | string | — | Approved physical/presentation recipes cho scope. / Straight/C/U/Z/Loop/Rectangle/Lifting/CustomApproved. |
| RuleV2.shapes[].corners[].role | string | — | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / Ordered corner/end role từ approved shape recipe NR02/04/05/07/08/19. |
| RuleV2.shapes[].corners[].x_expr | string | — | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho X trong host support frame, mm. |
| RuleV2.shapes[].corners[].y_expr | string | — | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Y trong host support frame, mm. |
| RuleV2.shapes[].corners[].z_expr | string | — | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Z/depth trong host support frame, mm. |
| RuleV2.shapes[].corners[].bend_radius_mm | double? | mm | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / null ở endpoint hoặc sharp-approved corner; radius >0 theo approved bend. |
| RuleV2.shapes[].corners[].radius_basis | string? | — | Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / Inner/Centreline; null ở endpoint, không tự chọn R8 basis. |
| RuleV2.shapes[].length_expr_id | string | — | Approved physical/presentation recipes cho scope. / Approved schedule length expression ID; fabrication path length tính riêng. |
| RuleV2.shapes[].measurement_basis | string | — | Approved physical/presentation recipes cho scope. / Centreline/NominalApproved; không đổi theo renderer. |
| RuleV2.shapes[].native_asset | string | — | Approved physical/presentation recipes cho scope. / Exact TK asset; thiếu mapping = NATIVE_SCHEDULE_SHAPE_UNMAPPED. |
| RuleV2.shapes[].fixed_glyph_hash | string | — | Approved physical/presentation recipes cho scope. / Hash glyph native content, bất biến khi Li thay đổi. |
| RuleV2.shapes[].tag_bindings[].tag | string | — | Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Exact native tag tên trong AssetV2.attributes. |
| RuleV2.shapes[].tag_bindings[].operation | string | — | Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Mark/Diameter/Pitch/MeanLength/RawSumMetres/Count/Mass/MeanRole/FixedRole/Literal/SizePair; exhaustive S8 binder. |
| RuleV2.shapes[].tag_bindings[].role | string? | — | Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / SegmentRole hoặc literal key khi operation cần; không parse số từ glyph. |
| RuleV2.shapes[].tag_bindings[].literal | string? | — | Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Chỉ Literal hoặc fixed sourced glyph caption; quantity không lấy ATT default. |
| RuleV2.shapes[].tag_bindings[].unit | string | — | Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Typed value unit: mm,m,kg,count,none,kg/m. |
| RuleV2.shapes[].source_ids[] | string[] | — | Approved physical/presentation recipes cho scope. / Reference glyph/role/owner decisions; chưa verified = rule chưa approved. |
| RuleV2.shapes[].fabrication_basis | string | — | Approved physical/presentation recipes cho scope. / Centreline only when path/radius/nominal-to-centreline conversion approved; otherwise feature NeedsRule. |
| RuleV2.shapes[].presentation_length_basis | string | — | Approved physical/presentation recipes cho scope. / Schedule/Fabrication explicitly approved for this shape/role; no silent substitution. |
| RuleV2.source_ids[] | string[] | — | Owner/source evidence cho rule và approval. |

### RulesV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RulesV2.snapshot_hash | string | — | Hash full rule snapshot bytes, 05B-F46. |
| RulesV2.entries[].id | string | — | Approved + proposal preserved, không promote Experimental. / NR/approved rule ID, source snapshot exact. |
| RulesV2.entries[].version | string | — | Approved + proposal preserved, không promote Experimental. / Immutable version string, không dùng latest heuristic. |
| RulesV2.entries[].status | string | — | Approved + proposal preserved, không promote Experimental. / Proposal/Approved/Rejected; Proposal không cấp numeric production values. |
| RulesV2.entries[].approver | string? | — | Approved + proposal preserved, không promote Experimental. / Required khi Approved, else null hoặc draft metadata. |
| RulesV2.entries[].approved_at_utc | string? | UTC | Approved + proposal preserved, không promote Experimental. / Required khi Approved, không timestamp file thay approval. |
| RulesV2.entries[].profiles[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Explicit scope profiles; rỗng không tự là all. |
| RulesV2.entries[].components[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Explicit component scope. |
| RulesV2.entries[].shape_kinds[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Explicit shape scope. |
| RulesV2.entries[].parameters[].key | string | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Closed parameter key trong rule schema; unknown reject. |
| RulesV2.entries[].parameters[].kind | string | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Number/Text/Bool; đúng một value slot được điền. |
| RulesV2.entries[].parameters[].number | double? | unit field | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Approved numeric parameter, finite; khác kind null. |
| RulesV2.entries[].parameters[].text | string? | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Approved text/enum parameter; khác kind null. |
| RulesV2.entries[].parameters[].flag | bool? | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Approved boolean parameter; khác kind null. |
| RulesV2.entries[].parameters[].unit | string | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / mm/m/kg/rad/paper_mm/dimensionless/none; theo schema key. |
| RulesV2.entries[].parameters[].source_ids[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Closed named parameter list với types/units/source kiểm S0. / Source evidence/owner decision của parameter; không list rỗng. |
| RulesV2.entries[].expressions[].id | string | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / Unique expression node ID trong RuleV2, không cycle. |
| RulesV2.entries[].expressions[].op | string | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / CONST/REF/ADD/SUB/MUL/DIV/MIN/MAX/SQRT/SIN/COS/TAN/ATAN2/CEIL/FLOOR/ROUND; exhaustive helper EVAL. |
| RulesV2.entries[].expressions[].arg_ids[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / Node children theo đúng thứ tự operator; arity kiểm trong EVAL. |
| RulesV2.entries[].expressions[].ref_path | string? | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / REF đọc approved parameter hoặc typed numeric field trong environment; khác REF null. |
| RulesV2.entries[].expressions[].constant | double? | unit field | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / CONST numeric source-backed; khác CONST null. |
| RulesV2.entries[].expressions[].unit | string | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / Output unit của node, checked dimensional arithmetic. |
| RulesV2.entries[].expressions[].source_ids[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / CONSTANT/formula source IDs; derived node ghi parent source IDs. |
| RulesV2.entries[].expressions[].rounding_mode | string? | — | Approved + proposal preserved, không promote Experimental. / Finite DAG, helper EVAL kiểm arity/cycle/units. / ROUND only: ToEven/AwayFromZero from approved NR13/06; other operators null. |
| RulesV2.entries[].shapes[].id | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Exact recipe ID, source-backed scope; không chọn theo tên ga. |
| RulesV2.entries[].shapes[].component_kind | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Base/Wall/Roof/Neck/Panel/Catch/Accessory theo scope. |
| RulesV2.entries[].shapes[].family | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / MainMesh/Tie/Ladder/Dowel/Haunch/Lifting/Anchor/Angle/OpeningReinforcement. |
| RulesV2.entries[].shapes[].shape_kind | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Straight/C/U/Z/Loop/Rectangle/Lifting/CustomApproved. |
| RulesV2.entries[].shapes[].corners[].role | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / Ordered corner/end role từ approved shape recipe NR02/04/05/07/08/19. |
| RulesV2.entries[].shapes[].corners[].x_expr | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho X trong host support frame, mm. |
| RulesV2.entries[].shapes[].corners[].y_expr | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Y trong host support frame, mm. |
| RulesV2.entries[].shapes[].corners[].z_expr | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / ExprNode ID cho Z/depth trong host support frame, mm. |
| RulesV2.entries[].shapes[].corners[].bend_radius_mm | double? | mm | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / null ở endpoint hoặc sharp-approved corner; radius >0 theo approved bend. |
| RulesV2.entries[].shapes[].corners[].radius_basis | string? | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Ordered centreline/nominal corner expressions; basis checked before path. / Inner/Centreline; null ở endpoint, không tự chọn R8 basis. |
| RulesV2.entries[].shapes[].length_expr_id | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Approved schedule length expression ID; fabrication path length tính riêng. |
| RulesV2.entries[].shapes[].measurement_basis | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Centreline/NominalApproved; không đổi theo renderer. |
| RulesV2.entries[].shapes[].native_asset | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Exact TK asset; thiếu mapping = NATIVE_SCHEDULE_SHAPE_UNMAPPED. |
| RulesV2.entries[].shapes[].fixed_glyph_hash | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Hash glyph native content, bất biến khi Li thay đổi. |
| RulesV2.entries[].shapes[].tag_bindings[].tag | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Exact native tag tên trong AssetV2.attributes. |
| RulesV2.entries[].shapes[].tag_bindings[].operation | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Mark/Diameter/Pitch/MeanLength/RawSumMetres/Count/Mass/MeanRole/FixedRole/Literal/SizePair; exhaustive S8 binder. |
| RulesV2.entries[].shapes[].tag_bindings[].role | string? | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / SegmentRole hoặc literal key khi operation cần; không parse số từ glyph. |
| RulesV2.entries[].shapes[].tag_bindings[].literal | string? | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Chỉ Literal hoặc fixed sourced glyph caption; quantity không lấy ATT default. |
| RulesV2.entries[].shapes[].tag_bindings[].unit | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Closed mapping exact tags, đủ và không dư. / Typed value unit: mm,m,kg,count,none,kg/m. |
| RulesV2.entries[].shapes[].source_ids[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Reference glyph/role/owner decisions; chưa verified = rule chưa approved. |
| RulesV2.entries[].shapes[].fabrication_basis | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Centreline only when path/radius/nominal-to-centreline conversion approved; otherwise feature NeedsRule. |
| RulesV2.entries[].shapes[].presentation_length_basis | string | — | Approved + proposal preserved, không promote Experimental. / Approved physical/presentation recipes cho scope. / Schedule/Fabrication explicitly approved for this shape/role; no silent substitution. |
| RulesV2.entries[].source_ids[] | string[] | — | Approved + proposal preserved, không promote Experimental. / Owner/source evidence cho rule và approval. |
| RulesV2.geometry_tolerance_mm | double? | mm | Approved NR18; null nếu chưa ký, math requiring tolerance Block. |
| RulesV2.length_tolerance_mm | double? | mm | Approved NR18 riêng length. |
| RulesV2.paper_tolerance_mm | double? | paper mm | Approved NR18 riêng paper. |
| RulesV2.mass_tolerance_kg | double? | kg | Approved NR18 riêng mass. |
| RulesV2.basis_tolerance | double? | dimensionless | Approved NR18 tolerance for this unit; null blocks checks needing it, no shared mm epsilon. |
| RulesV2.angle_tolerance_rad | double? | rad | Approved NR18 tolerance for this unit; null blocks checks needing it, no shared mm epsilon. |
| RulesV2.area_tolerance_mm2 | double? | mm2 | Approved NR18 tolerance for this unit; null blocks checks needing it, no shared mm epsilon. |
| RulesV2.pdf_tolerance_points | double? | pdf point | Approved NR18 tolerance for this unit; null blocks checks needing it, no shared mm epsilon. |

### RawCellV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RawCellV2.field_key | string | — | Exact 206 keys trong embedded input table 05A snapshot. |
| RawCellV2.sheet | string | — | Pass-through actual worksheet name. |
| RawCellV2.address | string | — | Value cell actual address; không note/header cell. |
| RawCellV2.kind | string | — | Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawCellV2.text | string? | — | Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawCellV2.raw_number_text | string? | — | OOXML numeric lexical value, invariant, chưa convert. |
| RawCellV2.boolean | bool? | — | Literal OOXML bool value, kind khác Bool = null. |
| RawCellV2.formula | string? | — | Raw formula text không dùng làm numeric value. |
| RawCellV2.cache_kind | string? | — | Number/Text/Bool/Error/Blank cho formula cache. |
| RawCellV2.cache_text | string? | — | Raw cached value; null khi không cache. |
| RawCellV2.cache_verified | bool | — | Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawCellV2.active | bool | — | S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawCellV2.source_id | string | — | SourceV2 WorkbookCell của value. |
| RawCellV2.note_source_ids[] | string[] | — | SourceV2 note cells liên quan, không thay value source. |

### RawRowV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RawRowV2.row_index | int | row | Actual selected/data row index >=4. |
| RawRowV2.name_raw | string | — | Column B literal, chưa normalize identity. |
| RawRowV2.cells[].field_key | string | — | B:AA cells theo MainColumns, không thiếu field. / Exact 206 keys trong embedded input table 05A snapshot. |
| RawRowV2.cells[].sheet | string | — | B:AA cells theo MainColumns, không thiếu field. / Pass-through actual worksheet name. |
| RawRowV2.cells[].address | string | — | B:AA cells theo MainColumns, không thiếu field. / Value cell actual address; không note/header cell. |
| RawRowV2.cells[].kind | string | — | B:AA cells theo MainColumns, không thiếu field. / Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawRowV2.cells[].text | string? | — | B:AA cells theo MainColumns, không thiếu field. / Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawRowV2.cells[].raw_number_text | string? | — | B:AA cells theo MainColumns, không thiếu field. / OOXML numeric lexical value, invariant, chưa convert. |
| RawRowV2.cells[].boolean | bool? | — | B:AA cells theo MainColumns, không thiếu field. / Literal OOXML bool value, kind khác Bool = null. |
| RawRowV2.cells[].formula | string? | — | B:AA cells theo MainColumns, không thiếu field. / Raw formula text không dùng làm numeric value. |
| RawRowV2.cells[].cache_kind | string? | — | B:AA cells theo MainColumns, không thiếu field. / Number/Text/Bool/Error/Blank cho formula cache. |
| RawRowV2.cells[].cache_text | string? | — | B:AA cells theo MainColumns, không thiếu field. / Raw cached value; null khi không cache. |
| RawRowV2.cells[].cache_verified | bool | — | B:AA cells theo MainColumns, không thiếu field. / Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawRowV2.cells[].active | bool | — | B:AA cells theo MainColumns, không thiếu field. / S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawRowV2.cells[].source_id | string | — | B:AA cells theo MainColumns, không thiếu field. / SourceV2 WorkbookCell của value. |
| RawRowV2.cells[].note_source_ids[] | string[] | — | B:AA cells theo MainColumns, không thiếu field. / SourceV2 note cells liên quan, không thay value source. |

### RawCatalogRowV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RawCatalogRowV2.sheet | string | — | CF.Cong/CF.Data/DBTool.Covers exact source sheet. |
| RawCatalogRowV2.row_index | int | row | Catalog row>=4; row trống bỏ riêng row, không dừng toàn catalog. |
| RawCatalogRowV2.cells[].field_key | string | — | Catalog columns từ 206 field table. / Exact 206 keys trong embedded input table 05A snapshot. |
| RawCatalogRowV2.cells[].sheet | string | — | Catalog columns từ 206 field table. / Pass-through actual worksheet name. |
| RawCatalogRowV2.cells[].address | string | — | Catalog columns từ 206 field table. / Value cell actual address; không note/header cell. |
| RawCatalogRowV2.cells[].kind | string | — | Catalog columns từ 206 field table. / Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawCatalogRowV2.cells[].text | string? | — | Catalog columns từ 206 field table. / Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawCatalogRowV2.cells[].raw_number_text | string? | — | Catalog columns từ 206 field table. / OOXML numeric lexical value, invariant, chưa convert. |
| RawCatalogRowV2.cells[].boolean | bool? | — | Catalog columns từ 206 field table. / Literal OOXML bool value, kind khác Bool = null. |
| RawCatalogRowV2.cells[].formula | string? | — | Catalog columns từ 206 field table. / Raw formula text không dùng làm numeric value. |
| RawCatalogRowV2.cells[].cache_kind | string? | — | Catalog columns từ 206 field table. / Number/Text/Bool/Error/Blank cho formula cache. |
| RawCatalogRowV2.cells[].cache_text | string? | — | Catalog columns từ 206 field table. / Raw cached value; null khi không cache. |
| RawCatalogRowV2.cells[].cache_verified | bool | — | Catalog columns từ 206 field table. / Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawCatalogRowV2.cells[].active | bool | — | Catalog columns từ 206 field table. / S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawCatalogRowV2.cells[].source_id | string | — | Catalog columns từ 206 field table. / SourceV2 WorkbookCell của value. |
| RawCatalogRowV2.cells[].note_source_ids[] | string[] | — | Catalog columns từ 206 field table. / SourceV2 note cells liên quan, không thay value source. |

### RawV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RawV2.workbook_hash | string | — | Pass-through current JobV2.workbook_sha256. |
| RawV2.profile | string | — | Pass-through current JobV2.profile. |
| RawV2.main_sheet | string | — | Pass-through current JobV2.main_sheet đã kiểm exact. |
| RawV2.selected_row_index | int | row | Pass-through context.row_index. |
| RawV2.rows[].row_index | int | row | Capture all nonblank main data rows, lookup selected row exact index. / Actual selected/data row index >=4. |
| RawV2.rows[].name_raw | string | — | Capture all nonblank main data rows, lookup selected row exact index. / Column B literal, chưa normalize identity. |
| RawV2.rows[].cells[].field_key | string | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Exact 206 keys trong embedded input table 05A snapshot. |
| RawV2.rows[].cells[].sheet | string | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Pass-through actual worksheet name. |
| RawV2.rows[].cells[].address | string | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Value cell actual address; không note/header cell. |
| RawV2.rows[].cells[].kind | string | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawV2.rows[].cells[].text | string? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawV2.rows[].cells[].raw_number_text | string? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / OOXML numeric lexical value, invariant, chưa convert. |
| RawV2.rows[].cells[].boolean | bool? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Literal OOXML bool value, kind khác Bool = null. |
| RawV2.rows[].cells[].formula | string? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Raw formula text không dùng làm numeric value. |
| RawV2.rows[].cells[].cache_kind | string? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Number/Text/Bool/Error/Blank cho formula cache. |
| RawV2.rows[].cells[].cache_text | string? | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Raw cached value; null khi không cache. |
| RawV2.rows[].cells[].cache_verified | bool | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawV2.rows[].cells[].active | bool | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawV2.rows[].cells[].source_id | string | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / SourceV2 WorkbookCell của value. |
| RawV2.rows[].cells[].note_source_ids[] | string[] | — | Capture all nonblank main data rows, lookup selected row exact index. / B:AA cells theo MainColumns, không thiếu field. / SourceV2 note cells liên quan, không thay value source. |
| RawV2.config[].field_key | string | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Exact 206 keys trong embedded input table 05A snapshot. |
| RawV2.config[].sheet | string | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Pass-through actual worksheet name. |
| RawV2.config[].address | string | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Value cell actual address; không note/header cell. |
| RawV2.config[].kind | string | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawV2.config[].text | string? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawV2.config[].raw_number_text | string? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / OOXML numeric lexical value, invariant, chưa convert. |
| RawV2.config[].boolean | bool? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Literal OOXML bool value, kind khác Bool = null. |
| RawV2.config[].formula | string? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Raw formula text không dùng làm numeric value. |
| RawV2.config[].cache_kind | string? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Number/Text/Bool/Error/Blank cho formula cache. |
| RawV2.config[].cache_text | string? | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Raw cached value; null khi không cache. |
| RawV2.config[].cache_verified | bool | — | Profile-specific CF.Ga addresses trong 206-field binding table. / Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawV2.config[].active | bool | — | Profile-specific CF.Ga addresses trong 206-field binding table. / S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawV2.config[].source_id | string | — | Profile-specific CF.Ga addresses trong 206-field binding table. / SourceV2 WorkbookCell của value. |
| RawV2.config[].note_source_ids[] | string[] | — | Profile-specific CF.Ga addresses trong 206-field binding table. / SourceV2 note cells liên quan, không thay value source. |
| RawV2.catalog_rows[].sheet | string | — | All populated catalog rows immutable. / CF.Cong/CF.Data/DBTool.Covers exact source sheet. |
| RawV2.catalog_rows[].row_index | int | row | All populated catalog rows immutable. / Catalog row>=4; row trống bỏ riêng row, không dừng toàn catalog. |
| RawV2.catalog_rows[].cells[].field_key | string | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Exact 206 keys trong embedded input table 05A snapshot. |
| RawV2.catalog_rows[].cells[].sheet | string | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Pass-through actual worksheet name. |
| RawV2.catalog_rows[].cells[].address | string | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Value cell actual address; không note/header cell. |
| RawV2.catalog_rows[].cells[].kind | string | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Blank/Number/Text/Bool/Formula/Error, captured OOXML. |
| RawV2.catalog_rows[].cells[].text | string? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Literal/raw text slot; numeric dùng raw_number_text để không mất decimal. |
| RawV2.catalog_rows[].cells[].raw_number_text | string? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / OOXML numeric lexical value, invariant, chưa convert. |
| RawV2.catalog_rows[].cells[].boolean | bool? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Literal OOXML bool value, kind khác Bool = null. |
| RawV2.catalog_rows[].cells[].formula | string? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Raw formula text không dùng làm numeric value. |
| RawV2.catalog_rows[].cells[].cache_kind | string? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Number/Text/Bool/Error/Blank cho formula cache. |
| RawV2.catalog_rows[].cells[].cache_text | string? | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Raw cached value; null khi không cache. |
| RawV2.catalog_rows[].cells[].cache_verified | bool | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / Có recalculation receipt phù hợp bytes hash/engine/version; timestamp đơn thuần không đủ. |
| RawV2.catalog_rows[].cells[].active | bool | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / S1 chỉ raw capture; S2 đặt effective activity dựa explicit conditions trong 05 input binding table. |
| RawV2.catalog_rows[].cells[].source_id | string | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / SourceV2 WorkbookCell của value. |
| RawV2.catalog_rows[].cells[].note_source_ids[] | string[] | — | All populated catalog rows immutable. / Catalog columns từ 206 field table. / SourceV2 note cells liên quan, không thay value source. |
| RawV2.version_marker | string | — | VHGpro!L2 literal version, source EX-001. |

### DatumV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| DatumV2.surface_id | string | — | BaseTop/BaseBottom/PadBottom/WallTop/RoofBottom/RoofTop/NeckBottom/NeckTop/FrameBottom/FrameTop/FinishedCoverTop/PanelBottom; exact IDs. |
| DatumV2.z_mm | double | mm | Local elevation giải phương trình 05B-F13/F28; không datum lấy từ glyph. |
| DatumV2.symbol | string? | — | Literal Elevation.D:I khi symbolic; absent symbol required Block. |
| DatumV2.source_ids[] | string[] | — | Input fields/contact surfaces/derived formula source IDs. |

### MemberSpecV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| MemberSpecV2.id | string | — | ENCODE(manhole_id,host_id,face,layer,direction,family), 05B-F46. |
| MemberSpecV2.host_id | string | — | Stable component ID S3 tạo từ exact mapping table. |
| MemberSpecV2.component_kind | string | — | Base/Wall/Roof/Neck/Panel/Catch/Accessory. |
| MemberSpecV2.face | string | — | Top/Bottom/Inner/Outer/Floor/Left/Right/Front/Joint. |
| MemberSpecV2.layer | string | — | Top/Bottom/Inner/Outer/Single theo input và approved stack. |
| MemberSpecV2.direction | string | — | U/V/Loop/Vertical/Horizontal theo mapping, không paper X/Y. |
| MemberSpecV2.family | string | — | MainMesh/Tie/Ladder/Dowel/Haunch/Lifting/Anchor/Angle/OpeningReinforcement. |
| MemberSpecV2.active | bool | — | Explicit input predicate; false giữ suppression_reason, không tự OFF unsupported. |
| MemberSpecV2.suppression_reason | string? | — | Rule/field condition khi inactive, active = null. |
| MemberSpecV2.diameter_mm | int? | mm | Positive round bar diameter; Angle=null (catalog V/L dimensions+kg/m), no numeric placeholder. |
| MemberSpecV2.requested_pitch_mm | double? | mm | null nếu feature Count-based; mesh/tie/dowel >0. |
| MemberSpecV2.spacing_mode | string | — | Equalized/ByPitch/ApprovedStations/CountBased. |
| MemberSpecV2.cover_mm | double | mm | Protect role field theo mapping; hole cover riêng không wallCover. |
| MemberSpecV2.pitch_delta_mm | double | mm | WallSpacingDelta/NeckSpacingDelta hoặc sourced zero khi không áp dụng; 05B-F33. |
| MemberSpecV2.diameter_step_down | int | catalog steps | Wall/Neck configured steps, không trừ mm trực tiếp. |
| MemberSpecV2.hook_pattern | string? | — | HookPattern.C:J role mapping; feature không hook = null. |
| MemberSpecV2.shape_recipe_id | string | — | Exact approved physical/nominal recipe, thiếu RAISE RULE_UNAPPROVED. |
| MemberSpecV2.required_rule_ids[] | string[] | — | Feature-specific approvals; scope không match Block. |
| MemberSpecV2.count | int? | count | Count-based features roof/panel lifting/angle, mesh = null. |
| MemberSpecV2.source_ids[] | string[] | — | Exact input cells and rule references defining member. |

### PipeSpecV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| PipeSpecV2.id | string | — | ENCODE(manhole_id,wall,pipe_slot), 05B-F46. |
| PipeSpecV2.wall | string | — | T1/T2/T3/T4 từ O:V pair, không name heuristic. |
| PipeSpecV2.catalog_code | string | — | Exact code/declared alias đã duyệt. |
| PipeSpecV2.kind | string | — | Round/Box/DoubleBox từ catalog. |
| PipeSpecV2.invert_mm | double | mm | Pass-through InvertT1..4; relative to BaseTop. |
| PipeSpecV2.offset_u_mm | double | mm | Pass-through PipeOffsetT1..4 trong host inside-looking-out frame. |
| PipeSpecV2.inner_width_mm | double | mm | Round diameter hoặc total box clear width từ CF.Cong. |
| PipeSpecV2.inner_height_mm | double | mm | Round diameter hoặc box height từ CF.Cong. |
| PipeSpecV2.wall_thickness_mm | double | mm | CF.Cong thickness của entry exact. |
| PipeSpecV2.mortar_mm | double | mm | PipeMortar/alias Pipe.C, không BeddingMortar. |
| PipeSpecV2.chamfer_x_mm | double | mm | CF.Cong G tuple first; zero chỉ khi catalog khai báo square corner. |
| PipeSpecV2.chamfer_y_mm | double | mm | CF.Cong G tuple second. |
| PipeSpecV2.web_mm | double? | mm | DoubleBox sourced web theo NR20; Round/Box null. |
| PipeSpecV2.source_ids[] | string[] | — | Code/invert/offset/catalog/mortar sources. |

### CatchSpecV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| CatchSpecV2.active | bool | — | Profile3 requested catch; profile1/2 inactive với source profile condition. |
| CatchSpecV2.owner | string? | — | Body/Neck từ Catch.C; inactive null. |
| CatchSpecV2.wall | string? | — | T1..T4 từ Catch.D. |
| CatchSpecV2.position | string? | — | Left/Center/Right từ Catch.D; Neck forced Center source HDSD p13. |
| CatchSpecV2.raw_position | string? | — | Preserve raw Catch.D position dù effective override. |
| CatchSpecV2.offset_u_mm | double | mm | Explicit Catch.D decoded offset nếu grammar cung cấp; không có offset = sourced definition Center/Left/Right at edge, không guess text. |
| CatchSpecV2.opening_width_mm | double | mm | Catch.F first value. |
| CatchSpecV2.opening_height_mm | double | mm | Catch.F second value. |
| CatchSpecV2.clear_depth_mm | double | mm | Catch.G first value, NOTE-0411. |
| CatchSpecV2.clear_height_mm | double | mm | Catch.G second value = clear height above floor; NOTE-0411, legacy mismatch NR-IO-004. |
| CatchSpecV2.wall_mm | double | mm | Catch.H, NOTE-0412. |
| CatchSpecV2.base_mm | double | mm | Catch.I, NOTE-0413. |
| CatchSpecV2.pad_mm | double | mm | Catch.J >=0; zero OFF. |
| CatchSpecV2.pad_expansion_mm | double | mm | Catch.K, NOTE-0415. |
| CatchSpecV2.reinforced | bool | — | Catch.L BTCT=true, Khác=false; copied note text cổ không đổi scope. |
| CatchSpecV2.product_code | string? | — | Approved catch product/aperture source NR09; no source = Block khi requested. |
| CatchSpecV2.source_ids[] | string[] | — | Catch raw fields and effective overrides. |

### PanelSpecV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| PanelSpecV2.active | bool | — | CoverType BTCT/Panel predicate. |
| PanelSpecV2.contract_id | string? | — | Selected PanelAssemblyContract approved NR23, inactive null. |
| PanelSpecV2.count | int | count | PanelCount >=1 khi active; inactive zero có predicate source. |
| PanelSpecV2.split_axis | string? | — | X/Y từ PanelDirection, inactive null. |
| PanelSpecV2.gap_mm | double | mm | PanelGap >=0, 05B-F12. |
| PanelSpecV2.thickness_mm | double | mm | CoverHeight theo approved contact/seat contract, không frame100. |
| PanelSpecV2.seat_expansion_mm | double | mm | NeckHaunchSize/seat contract đã resolve. |
| PanelSpecV2.seat_height_mm | double | mm | NeckHaunchSize height nếu active, else sourced zero. |
| PanelSpecV2.source_ids[] | string[] | — | Panel/seat/count/direction/gap sources + NR23. |
| PanelSpecV2.support_mode | string? | — | Recess/OverOuter chosen in approved NR23 contract, not inferred from support width. |

### EffectiveV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| EffectiveV2.id | string | — | Pass-through context.manhole_id. |
| EffectiveV2.display_name | string | — | Selected RawRowV2.name_raw literal, không làm group key. |
| EffectiveV2.profile | string | — | Pass-through raw.profile. |
| EffectiveV2.clear_x_mm | double | mm | Parsed Length main C; >0. |
| EffectiveV2.clear_y_mm | double | mm | Parsed Width main D; >0. |
| EffectiveV2.clear_h_mm | double | mm | Parsed Height E; clear E includes bedding mortar theo notes loại2/3. |
| EffectiveV2.base_mm | double | mm | BaseThickness F >0. |
| EffectiveV2.wall_mm | double | mm | WallThickness I >0. |
| EffectiveV2.roof_mm | double | mm | RoofThickness L >0. |
| EffectiveV2.base_expansion_mm | double | mm | BaseExpansion CF.Ga C5 >=0. |
| EffectiveV2.pad_mm | double | mm | PadThickness D3 >=0; zero = no Pad. |
| EffectiveV2.pad_expansion_mm | double | mm | PadExpansion C3 >=0. |
| EffectiveV2.base_haunch_mm | double | mm | BaseHaunch D5 >=0; numeric to typed width/height mapping from source spec. |
| EffectiveV2.top_haunch_mm | double | mm | TopHaunch C9 loại1; loại2/3 inactive zero with profile reason. |
| EffectiveV2.bedding_mortar_mm | double | mm | BeddingMortar G9 loại2/3; loại1 zero via monolithic predicate. |
| EffectiveV2.neck_active | bool | — | NeckHeight>0; không tự thêm neck vì template có block. |
| EffectiveV2.neck_clear_x_mm | double | mm | Approved selected product/panel aperture X, lưu raw khác biệt riêng. |
| EffectiveV2.neck_clear_y_mm | double | mm | Approved selected product/panel aperture Y. |
| EffectiveV2.neck_raw_clear_x_mm | double? | mm | Parsed NeckSize first; inactive/raw blank null. |
| EffectiveV2.neck_raw_clear_y_mm | double? | mm | Parsed NeckSize second. |
| EffectiveV2.neck_wall_mm | double | mm | NeckThickness X >0 khi neck active. |
| EffectiveV2.neck_height_mm | double | mm | NeckHeight W phần BTCT, không cộng frame/product vào W. |
| EffectiveV2.neck_position | string | — | Parsed NeckPosition nine-value table trong S2. |
| EffectiveV2.neck_center.x_mm | double | mm | 05B-F29 từ effective clear và NeckPosition. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| EffectiveV2.neck_center.y_mm | double | mm | 05B-F29 từ effective clear và NeckPosition. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| EffectiveV2.neck_reinforced | bool | — | NeckStructure BTCT/Khác predicate. |
| EffectiveV2.cover_kind | string | — | Purchased/Panel/None exact CoverType mapping. |
| EffectiveV2.cover_code | string? | — | Exact selected product code, không đo kích thước từ suffix. |
| EffectiveV2.product_rotation_rad | double | rad | Explicit catalog/request supported angle, 05B-F25; unsupported Block. |
| EffectiveV2.frame_outer_x_mm | double | mm | neck_clear_x_mm+2*neck_wall_mm, 05B-F02. |
| EffectiveV2.frame_outer_y_mm | double | mm | neck_clear_y_mm+2*neck_wall_mm, 05B-F02. |
| EffectiveV2.frame_height_mm | double | mm | 100 từ CONTRACT C01 owner13/10, 05B-F02. |
| EffectiveV2.base_elevation_m | double? | m | Main AA nếu Absolute; Symbolic nullable. |
| EffectiveV2.elevation_mode | string | — | Request override có source hoặc parsed Elevation.C. |
| EffectiveV2.body_scale | double | 1:S | Pass-through request.body_scale. |
| EffectiveV2.panel_scale | double? | 1:S | Request.panel_scale or active Panel.CoverRatioPercent denominator; null when no designed Panel and no override. |
| EffectiveV2.note_style | string | — | NoteStyle1/2 từ Annotation.NoteStyle. |
| EffectiveV2.pitch_symbol | string | — | Literal Annotation.PitchSymbol. |
| EffectiveV2.datums[].surface_id | string | — | Surface-specific Z values 05B-F13/F28, solved contact equations S2. / BaseTop/BaseBottom/PadBottom/WallTop/RoofBottom/RoofTop/NeckBottom/NeckTop/FrameBottom/FrameTop/FinishedCoverTop/PanelBottom; exact IDs. |
| EffectiveV2.datums[].z_mm | double | mm | Surface-specific Z values 05B-F13/F28, solved contact equations S2. / Local elevation giải phương trình 05B-F13/F28; không datum lấy từ glyph. |
| EffectiveV2.datums[].symbol | string? | — | Surface-specific Z values 05B-F13/F28, solved contact equations S2. / Literal Elevation.D:I khi symbolic; absent symbol required Block. |
| EffectiveV2.datums[].source_ids[] | string[] | — | Surface-specific Z values 05B-F13/F28, solved contact equations S2. / Input fields/contact surfaces/derived formula source IDs. |
| EffectiveV2.members[].id | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / ENCODE(manhole_id,host_id,face,layer,direction,family), 05B-F46. |
| EffectiveV2.members[].host_id | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Stable component ID S3 tạo từ exact mapping table. |
| EffectiveV2.members[].component_kind | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Base/Wall/Roof/Neck/Panel/Catch/Accessory. |
| EffectiveV2.members[].face | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Top/Bottom/Inner/Outer/Floor/Left/Right/Front/Joint. |
| EffectiveV2.members[].layer | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Top/Bottom/Inner/Outer/Single theo input và approved stack. |
| EffectiveV2.members[].direction | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / U/V/Loop/Vertical/Horizontal theo mapping, không paper X/Y. |
| EffectiveV2.members[].family | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / MainMesh/Tie/Ladder/Dowel/Haunch/Lifting/Anchor/Angle/OpeningReinforcement. |
| EffectiveV2.members[].active | bool | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Explicit input predicate; false giữ suppression_reason, không tự OFF unsupported. |
| EffectiveV2.members[].suppression_reason | string? | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Rule/field condition khi inactive, active = null. |
| EffectiveV2.members[].diameter_mm | int? | mm | Exact host/face/layer/family mapping table S2, no consumers guessed. / Positive round bar diameter; Angle=null (catalog V/L dimensions+kg/m), no numeric placeholder. |
| EffectiveV2.members[].requested_pitch_mm | double? | mm | Exact host/face/layer/family mapping table S2, no consumers guessed. / null nếu feature Count-based; mesh/tie/dowel >0. |
| EffectiveV2.members[].spacing_mode | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Equalized/ByPitch/ApprovedStations/CountBased. |
| EffectiveV2.members[].cover_mm | double | mm | Exact host/face/layer/family mapping table S2, no consumers guessed. / Protect role field theo mapping; hole cover riêng không wallCover. |
| EffectiveV2.members[].pitch_delta_mm | double | mm | Exact host/face/layer/family mapping table S2, no consumers guessed. / WallSpacingDelta/NeckSpacingDelta hoặc sourced zero khi không áp dụng; 05B-F33. |
| EffectiveV2.members[].diameter_step_down | int | catalog steps | Exact host/face/layer/family mapping table S2, no consumers guessed. / Wall/Neck configured steps, không trừ mm trực tiếp. |
| EffectiveV2.members[].hook_pattern | string? | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / HookPattern.C:J role mapping; feature không hook = null. |
| EffectiveV2.members[].shape_recipe_id | string | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Exact approved physical/nominal recipe, thiếu RAISE RULE_UNAPPROVED. |
| EffectiveV2.members[].required_rule_ids[] | string[] | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Feature-specific approvals; scope không match Block. |
| EffectiveV2.members[].count | int? | count | Exact host/face/layer/family mapping table S2, no consumers guessed. / Count-based features roof/panel lifting/angle, mesh = null. |
| EffectiveV2.members[].source_ids[] | string[] | — | Exact host/face/layer/family mapping table S2, no consumers guessed. / Exact input cells and rule references defining member. |
| EffectiveV2.pipes[].id | string | — | Resolved O:V catalog entries S2. / ENCODE(manhole_id,wall,pipe_slot), 05B-F46. |
| EffectiveV2.pipes[].wall | string | — | Resolved O:V catalog entries S2. / T1/T2/T3/T4 từ O:V pair, không name heuristic. |
| EffectiveV2.pipes[].catalog_code | string | — | Resolved O:V catalog entries S2. / Exact code/declared alias đã duyệt. |
| EffectiveV2.pipes[].kind | string | — | Resolved O:V catalog entries S2. / Round/Box/DoubleBox từ catalog. |
| EffectiveV2.pipes[].invert_mm | double | mm | Resolved O:V catalog entries S2. / Pass-through InvertT1..4; relative to BaseTop. |
| EffectiveV2.pipes[].offset_u_mm | double | mm | Resolved O:V catalog entries S2. / Pass-through PipeOffsetT1..4 trong host inside-looking-out frame. |
| EffectiveV2.pipes[].inner_width_mm | double | mm | Resolved O:V catalog entries S2. / Round diameter hoặc total box clear width từ CF.Cong. |
| EffectiveV2.pipes[].inner_height_mm | double | mm | Resolved O:V catalog entries S2. / Round diameter hoặc box height từ CF.Cong. |
| EffectiveV2.pipes[].wall_thickness_mm | double | mm | Resolved O:V catalog entries S2. / CF.Cong thickness của entry exact. |
| EffectiveV2.pipes[].mortar_mm | double | mm | Resolved O:V catalog entries S2. / PipeMortar/alias Pipe.C, không BeddingMortar. |
| EffectiveV2.pipes[].chamfer_x_mm | double | mm | Resolved O:V catalog entries S2. / CF.Cong G tuple first; zero chỉ khi catalog khai báo square corner. |
| EffectiveV2.pipes[].chamfer_y_mm | double | mm | Resolved O:V catalog entries S2. / CF.Cong G tuple second. |
| EffectiveV2.pipes[].web_mm | double? | mm | Resolved O:V catalog entries S2. / DoubleBox sourced web theo NR20; Round/Box null. |
| EffectiveV2.pipes[].source_ids[] | string[] | — | Resolved O:V catalog entries S2. / Code/invert/offset/catalog/mortar sources. |
| EffectiveV2.catch.active | bool | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Profile3 requested catch; profile1/2 inactive với source profile condition. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.owner | string? | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Body/Neck từ Catch.C; inactive null. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.wall | string? | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / T1..T4 từ Catch.D. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.position | string? | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Left/Center/Right từ Catch.D; Neck forced Center source HDSD p13. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.raw_position | string? | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Preserve raw Catch.D position dù effective override. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.offset_u_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Explicit Catch.D decoded offset nếu grammar cung cấp; không có offset = sourced definition Center/Left/Right at edge, không guess text. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.opening_width_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.F first value. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.opening_height_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.F second value. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.clear_depth_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.G first value, NOTE-0411. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.clear_height_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.G second value = clear height above floor; NOTE-0411, legacy mismatch NR-IO-004. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.wall_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.H, NOTE-0412. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.base_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.I, NOTE-0413. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.pad_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.J >=0; zero OFF. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.pad_expansion_mm | double | mm | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.K, NOTE-0415. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.reinforced | bool | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch.L BTCT=true, Khác=false; copied note text cổ không đổi scope. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.product_code | string? | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Approved catch product/aperture source NR09; no source = Block khi requested. Nullable parent EffectiveV2.catch |
| EffectiveV2.catch.source_ids[] | string[] | — | S2 parsed fields + NeckCenter override.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Catch raw fields and effective overrides. Nullable parent EffectiveV2.catch |
| EffectiveV2.panel.active | bool | — | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / CoverType BTCT/Panel predicate. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.contract_id | string? | — | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Selected PanelAssemblyContract approved NR23, inactive null. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.count | int | count | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / PanelCount >=1 khi active; inactive zero có predicate source. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.split_axis | string? | — | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / X/Y từ PanelDirection, inactive null. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.gap_mm | double | mm | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / PanelGap >=0, 05B-F12. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.thickness_mm | double | mm | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / CoverHeight theo approved contact/seat contract, không frame100. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.seat_expansion_mm | double | mm | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / NeckHaunchSize/seat contract đã resolve. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.seat_height_mm | double | mm | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / NeckHaunchSize height nếu active, else sourced zero. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.source_ids[] | string[] | — | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Panel/seat/count/direction/gap sources + NR23. Nullable parent EffectiveV2.panel |
| EffectiveV2.panel.support_mode | string? | — | Selected panel contract + explicit input quantities.; null khi profile/cover không yêu cầu, không tạo struct số0 cho dữ liệu chưa biết. / Recess/OverOuter chosen in approved NR23 contract, not inferred from support width. Nullable parent EffectiveV2.panel |
| EffectiveV2.source_ids[] | string[] | — | Raw/effective diffs and input field evidence; no lost raw bytes. |
| EffectiveV2.parsed_fields[].field_key | string | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Exact field key trong bảng 206 fields; catalog keys có row_index. |
| EffectiveV2.parsed_fields[].sheet | string | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Pass-through raw source sheet. |
| EffectiveV2.parsed_fields[].address | string | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Pass-through raw value cell address. |
| EffectiveV2.parsed_fields[].row_index | int? | row | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Catalog/main row nếu applicable, config scalar null. |
| EffectiveV2.parsed_fields[].active | bool | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Predicate explicit trong S2 INPUT_ACTIVITY; không dùng active=false để che Unsupported. |
| EffectiveV2.parsed_fields[].inactive_reason | string? | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Tên predicate và raw source gây inactive; active=true null. |
| EffectiveV2.parsed_fields[].kind | string | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Number/Integer/Text/Bool/Tuple/Mesh/Anchor/Counted/TokenSet, exhaustive PARSE_FIELD. |
| EffectiveV2.parsed_fields[].number | double? | unit field | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Number slot finite; kind khác Number null. |
| EffectiveV2.parsed_fields[].integer | int? | unit field | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Integer slot checked exact integral, không truncate. |
| EffectiveV2.parsed_fields[].text | string? | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Literal/config enum/raw catalog code; không fuzzy. |
| EffectiveV2.parsed_fields[].flag | bool? | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Có/Không parsed bool; unsupported literal Block. |
| EffectiveV2.parsed_fields[].tuple_mm[] | double[] | mm | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Ordered values AxB/AxBxC, arity và role order có bảng S2. |
| EffectiveV2.parsed_fields[].diameter_mm | int? | mm | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Mesh/Anchor/Counted parsed diameter >0. |
| EffectiveV2.parsed_fields[].pitch_mm | double? | mm | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Mesh/Anchor parsed pitch>0. |
| EffectiveV2.parsed_fields[].length_mm | double? | mm | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Anchor parsed length>0, other=null. |
| EffectiveV2.parsed_fields[].count | int? | count | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Counted bar number; off allowed only explicit feature predicate. |
| EffectiveV2.parsed_fields[].tokens[] | string[] | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Exact enum set e.g angle positions1/2/3, no duplicates. |
| EffectiveV2.parsed_fields[].unit | string | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Unit của scalar numeric field theo 206 input table. |
| EffectiveV2.parsed_fields[].source_ids[] | string[] | — | Typed values for every applicable 206 field; inactive raw preserved, aliases verified equal. / Raw value/note refs and parse conversion provenance. |
| EffectiveV2.contact_equations[].surface_a | string | — | Explicit immutable datum constraint list used to solve datums in S2. / Datum ID vế trái, exact SurfaceId. |
| EffectiveV2.contact_equations[].surface_b | string | — | Explicit immutable datum constraint list used to solve datums in S2. / Datum ID vế phải, exact SurfaceId. |
| EffectiveV2.contact_equations[].delta_mm | double | mm | Explicit immutable datum constraint list used to solve datums in S2. / Equation z(a)=z(b)+delta from input/contact evidence, 05B-F28. |
| EffectiveV2.contact_equations[].source_ids[] | string[] | — | Explicit immutable datum constraint list used to solve datums in S2. / Native product contact/owner/input evidence, không outside bbox. |

### CurveV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| CurveV2.id | string | — | Stable segment ID from owner/path index, 05B-F46. |
| CurveV2.kind | string | — | Line/Arc, exhaustive geometry helper. |
| CurveV2.start.x_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| CurveV2.start.y_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| CurveV2.start.z_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| CurveV2.end.x_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| CurveV2.end.y_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| CurveV2.end.z_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| CurveV2.center.x_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent CurveV2.center |
| CurveV2.center.y_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent CurveV2.center |
| CurveV2.center.z_mm | double | mm | Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent CurveV2.center |
| CurveV2.normal.x | double | dimensionless | Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent CurveV2.normal |
| CurveV2.normal.y | double | dimensionless | Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent CurveV2.normal |
| CurveV2.normal.z | double | dimensionless | Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent CurveV2.normal |
| CurveV2.radius_mm | double? | mm | Arc radius>0, Line=null. |
| CurveV2.sweep_rad | double? | rad | Signed arc sweep, Line=null; 05B-F35. |
| CurveV2.role | string | — | StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |

### LoopV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| LoopV2.id | string | — | ENCODE(owner,loop_role,index), 05B-F46. |
| LoopV2.role | string | — | Outer/Inner/Exclusion/Contact; không suy từ màu. |
| LoopV2.curves[].id | string | — | Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| LoopV2.curves[].kind | string | — | Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| LoopV2.curves[].start.x_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| LoopV2.curves[].start.y_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| LoopV2.curves[].start.z_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| LoopV2.curves[].end.x_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| LoopV2.curves[].end.y_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| LoopV2.curves[].end.z_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| LoopV2.curves[].center.x_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent LoopV2.curves[].center |
| LoopV2.curves[].center.y_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent LoopV2.curves[].center |
| LoopV2.curves[].center.z_mm | double | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent LoopV2.curves[].center |
| LoopV2.curves[].normal.x | double | dimensionless | Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent LoopV2.curves[].normal |
| LoopV2.curves[].normal.y | double | dimensionless | Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent LoopV2.curves[].normal |
| LoopV2.curves[].normal.z | double | dimensionless | Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent LoopV2.curves[].normal |
| LoopV2.curves[].radius_mm | double? | mm | Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| LoopV2.curves[].sweep_rad | double? | rad | Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| LoopV2.curves[].role | string | — | Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| LoopV2.closed | bool | — | True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| LoopV2.signed_area_mm2 | double | mm2 | Analytic line/arc Green area, 05B-F37. |

### PatchV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| PatchV2.id | string | — | ENCODE(component_id,face_role,patch_index), 05B-F46. |
| PatchV2.component_id | string | — | Owner component exact ID. |
| PatchV2.face_role | string | — | Top/Bottom/Inner/Outer/OpeningReveal/Contact. |
| PatchV2.frame.id | string | — | Patch local plane origin/axes/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| PatchV2.frame.origin.x_mm | double | mm | Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| PatchV2.frame.origin.y_mm | double | mm | Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| PatchV2.frame.origin.z_mm | double | mm | Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| PatchV2.frame.axis_u.x | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.axis_u.y | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.axis_u.z | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.axis_v.x | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.axis_v.y | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.axis_v.z | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.normal.x | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.normal.y | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.normal.z | double | dimensionless | Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| PatchV2.frame.handedness | int | — | Patch local plane origin/axes/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| PatchV2.loops[].id | string | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / ENCODE(owner,loop_role,index), 05B-F46. |
| PatchV2.loops[].role | string | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| PatchV2.loops[].curves[].id | string | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| PatchV2.loops[].curves[].kind | string | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| PatchV2.loops[].curves[].start.x_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| PatchV2.loops[].curves[].start.y_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| PatchV2.loops[].curves[].start.z_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| PatchV2.loops[].curves[].end.x_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| PatchV2.loops[].curves[].end.y_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| PatchV2.loops[].curves[].end.z_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| PatchV2.loops[].curves[].center.x_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent PatchV2.loops[].curves[].center |
| PatchV2.loops[].curves[].center.y_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent PatchV2.loops[].curves[].center |
| PatchV2.loops[].curves[].center.z_mm | double | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent PatchV2.loops[].curves[].center |
| PatchV2.loops[].curves[].normal.x | double | dimensionless | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent PatchV2.loops[].curves[].normal |
| PatchV2.loops[].curves[].normal.y | double | dimensionless | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent PatchV2.loops[].curves[].normal |
| PatchV2.loops[].curves[].normal.z | double | dimensionless | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent PatchV2.loops[].curves[].normal |
| PatchV2.loops[].curves[].radius_mm | double? | mm | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| PatchV2.loops[].curves[].sweep_rad | double? | rad | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| PatchV2.loops[].curves[].role | string | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| PatchV2.loops[].closed | bool | — | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| PatchV2.loops[].signed_area_mm2 | double | mm2 | Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Analytic line/arc Green area, 05B-F37. |
| PatchV2.area_mm2 | double | mm2 | Outer areas minus inner, 05B-F37; no bbox area for curved voids. |
| PatchV2.adjacent_component_id | string? | — | Real contact owner, not nearest label; no contact null. |
| PatchV2.source_ids[] | string[] | — | Component input/void/contact sources. |

### ComponentV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ComponentV2.id | string | — | ENCODE(manhole_id,semantic component key), exact keys in S3. |
| ComponentV2.kind | string | — | Pad/Base/Wall/Roof/Neck/Frame/Panel/Catch/Haunch/Mortar/Product. |
| ComponentV2.assembly_id | string | — | Profile ownership table S3; union only same assembly/material. |
| ComponentV2.material_id | string | — | Concrete/Bedding/BeddingMortar/NativeProduct explicit role. |
| ComponentV2.frame.id | string | — | Physical local-to-world basis; no sheet transform. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ComponentV2.frame.origin.x_mm | double | mm | Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.frame.origin.y_mm | double | mm | Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.frame.origin.z_mm | double | mm | Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.frame.axis_u.x | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.axis_u.y | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.axis_u.z | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.axis_v.x | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.axis_v.y | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.axis_v.z | double | dimensionless | Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.normal.x | double | dimensionless | Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.normal.y | double | dimensionless | Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.normal.z | double | dimensionless | Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.frame.handedness | int | — | Physical local-to-world basis; no sheet transform. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ComponentV2.loops[].id | string | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| ComponentV2.loops[].role | string | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| ComponentV2.loops[].curves[].id | string | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| ComponentV2.loops[].curves[].kind | string | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| ComponentV2.loops[].curves[].start.x_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.loops[].curves[].start.y_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.loops[].curves[].start.z_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.loops[].curves[].end.x_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.loops[].curves[].end.y_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.loops[].curves[].end.z_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.loops[].curves[].center.x_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent ComponentV2.loops[].curves[].center |
| ComponentV2.loops[].curves[].center.y_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent ComponentV2.loops[].curves[].center |
| ComponentV2.loops[].curves[].center.z_mm | double | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent ComponentV2.loops[].curves[].center |
| ComponentV2.loops[].curves[].normal.x | double | dimensionless | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.loops[].curves[].normal |
| ComponentV2.loops[].curves[].normal.y | double | dimensionless | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.loops[].curves[].normal |
| ComponentV2.loops[].curves[].normal.z | double | dimensionless | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.loops[].curves[].normal |
| ComponentV2.loops[].curves[].radius_mm | double? | mm | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| ComponentV2.loops[].curves[].sweep_rad | double? | rad | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| ComponentV2.loops[].curves[].role | string | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| ComponentV2.loops[].closed | bool | — | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| ComponentV2.loops[].signed_area_mm2 | double | mm2 | Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Analytic line/arc Green area, 05B-F37. |
| ComponentV2.z_min_mm | double | mm | Component lower datum from effective.datums and S3 branch. |
| ComponentV2.z_max_mm | double | mm | Component upper datum from effective.datums and S3 branch. |
| ComponentV2.thickness_mm | double | mm | Pass-through active component thickness or derived slab Z span, 05B-F27. |
| ComponentV2.patches[].id | string | — | Exposed/contact/void faces from analytic component boundary. / ENCODE(component_id,face_role,patch_index), 05B-F46. |
| ComponentV2.patches[].component_id | string | — | Exposed/contact/void faces from analytic component boundary. / Owner component exact ID. |
| ComponentV2.patches[].face_role | string | — | Exposed/contact/void faces from analytic component boundary. / Top/Bottom/Inner/Outer/OpeningReveal/Contact. |
| ComponentV2.patches[].frame.id | string | — | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ComponentV2.patches[].frame.origin.x_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.patches[].frame.origin.y_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.patches[].frame.origin.z_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.patches[].frame.axis_u.x | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.axis_u.y | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.axis_u.z | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.axis_v.x | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.axis_v.y | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.axis_v.z | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.normal.x | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.normal.y | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.normal.z | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.patches[].frame.handedness | int | — | Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ComponentV2.patches[].loops[].id | string | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / ENCODE(owner,loop_role,index), 05B-F46. |
| ComponentV2.patches[].loops[].role | string | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| ComponentV2.patches[].loops[].curves[].id | string | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| ComponentV2.patches[].loops[].curves[].kind | string | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| ComponentV2.patches[].loops[].curves[].start.x_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.patches[].loops[].curves[].start.y_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.patches[].loops[].curves[].start.z_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.patches[].loops[].curves[].end.x_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.patches[].loops[].curves[].end.y_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.patches[].loops[].curves[].end.z_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.patches[].loops[].curves[].center.x_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent ComponentV2.patches[].loops[].curves[].center |
| ComponentV2.patches[].loops[].curves[].center.y_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent ComponentV2.patches[].loops[].curves[].center |
| ComponentV2.patches[].loops[].curves[].center.z_mm | double | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent ComponentV2.patches[].loops[].curves[].center |
| ComponentV2.patches[].loops[].curves[].normal.x | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.patches[].loops[].curves[].normal |
| ComponentV2.patches[].loops[].curves[].normal.y | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.patches[].loops[].curves[].normal |
| ComponentV2.patches[].loops[].curves[].normal.z | double | dimensionless | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.patches[].loops[].curves[].normal |
| ComponentV2.patches[].loops[].curves[].radius_mm | double? | mm | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| ComponentV2.patches[].loops[].curves[].sweep_rad | double? | rad | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| ComponentV2.patches[].loops[].curves[].role | string | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| ComponentV2.patches[].loops[].closed | bool | — | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| ComponentV2.patches[].loops[].signed_area_mm2 | double | mm2 | Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Analytic line/arc Green area, 05B-F37. |
| ComponentV2.patches[].area_mm2 | double | mm2 | Exposed/contact/void faces from analytic component boundary. / Outer areas minus inner, 05B-F37; no bbox area for curved voids. |
| ComponentV2.patches[].adjacent_component_id | string? | — | Exposed/contact/void faces from analytic component boundary. / Real contact owner, not nearest label; no contact null. |
| ComponentV2.patches[].source_ids[] | string[] | — | Exposed/contact/void faces from analytic component boundary. / Component input/void/contact sources. |
| ComponentV2.void_ids[] | string[] | — | Owned openings IDs; same opening not subtracted twice. |
| ComponentV2.source_ids[] | string[] | — | Input source IDs + construction formula IDs. |
| ComponentV2.solid_parts[].id | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / ENCODE(component_id,part_role,index), 05B-F46. |
| ComponentV2.solid_parts[].operation | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Union/Subtract in ordered CSG list; void ownership prevents subtraction twice. |
| ComponentV2.solid_parts[].frame.id | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ComponentV2.solid_parts[].frame.origin.x_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.solid_parts[].frame.origin.y_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].frame.origin.z_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].frame.axis_u.x | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.axis_u.y | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.axis_u.z | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.axis_v.x | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.axis_v.y | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.axis_v.z | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.normal.x | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.normal.y | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.normal.z | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ComponentV2.solid_parts[].frame.handedness | int | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ComponentV2.solid_parts[].loops[].id | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / ENCODE(owner,loop_role,index), 05B-F46. |
| ComponentV2.solid_parts[].loops[].role | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| ComponentV2.solid_parts[].loops[].curves[].id | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| ComponentV2.solid_parts[].loops[].curves[].kind | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| ComponentV2.solid_parts[].loops[].curves[].start.x_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.solid_parts[].loops[].curves[].start.y_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].loops[].curves[].start.z_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].loops[].curves[].end.x_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ComponentV2.solid_parts[].loops[].curves[].end.y_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].loops[].curves[].end.z_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ComponentV2.solid_parts[].loops[].curves[].center.x_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent ComponentV2.solid_parts[].loops[].curves[].center |
| ComponentV2.solid_parts[].loops[].curves[].center.y_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent ComponentV2.solid_parts[].loops[].curves[].center |
| ComponentV2.solid_parts[].loops[].curves[].center.z_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent ComponentV2.solid_parts[].loops[].curves[].center |
| ComponentV2.solid_parts[].loops[].curves[].normal.x | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.solid_parts[].loops[].curves[].normal |
| ComponentV2.solid_parts[].loops[].curves[].normal.y | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.solid_parts[].loops[].curves[].normal |
| ComponentV2.solid_parts[].loops[].curves[].normal.z | double | dimensionless | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent ComponentV2.solid_parts[].loops[].curves[].normal |
| ComponentV2.solid_parts[].loops[].curves[].radius_mm | double? | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| ComponentV2.solid_parts[].loops[].curves[].sweep_rad | double? | rad | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| ComponentV2.solid_parts[].loops[].curves[].role | string | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| ComponentV2.solid_parts[].loops[].closed | bool | — | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| ComponentV2.solid_parts[].loops[].signed_area_mm2 | double | mm2 | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Analytic line/arc Green area, 05B-F37. |
| ComponentV2.solid_parts[].depth_min_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion coordinate along frame.normal, 05B-F27. |
| ComponentV2.solid_parts[].depth_max_mm | double | mm | Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion end coordinate; >depth_min for active part. |
| ComponentV2.local_key | string | — | Exact Base/Wall/T1..T4/Roof/Neck/Pad/etc key for legacy adapter; full id remains namespaced. NR-IO-002 records mapping. |
| ComponentV2.material_role | string | — | Bedding/BodyConcrete/NeckConcrete/PanelConcrete/CatchConcrete/PipeConcrete/Plaster/BeddingMortar; exact role binding selects input material key. material_id is actual material family+grade, not merely Concrete. |

### OpeningV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| OpeningV2.id | string | — | ENCODE(manhole_id,host_id,opening_slot), stable. |
| OpeningV2.kind | string | — | Round/Box/DoubleBox/RoofAccess/Catch. |
| OpeningV2.host_ids[] | string[] | — | Partitioned owners from actual contour×depth intersection, 05B-F32. |
| OpeningV2.frame.id | string | — | Host plane U/Z with depth outward, 05B-F26. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| OpeningV2.frame.origin.x_mm | double | mm | Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.frame.origin.y_mm | double | mm | Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.frame.origin.z_mm | double | mm | Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.frame.axis_u.x | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.axis_u.y | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.axis_u.z | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.axis_v.x | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.axis_v.y | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.axis_v.z | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.normal.x | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.normal.y | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.normal.z | double | dimensionless | Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| OpeningV2.frame.handedness | int | — | Host plane U/Z with depth outward, 05B-F26. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| OpeningV2.inner[].id | string | — | Clear aperture true contour, 05B-F06/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| OpeningV2.inner[].role | string | — | Clear aperture true contour, 05B-F06/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| OpeningV2.inner[].curves[].id | string | — | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| OpeningV2.inner[].curves[].kind | string | — | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| OpeningV2.inner[].curves[].start.x_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.inner[].curves[].start.y_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.inner[].curves[].start.z_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.inner[].curves[].end.x_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.inner[].curves[].end.y_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.inner[].curves[].end.z_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.inner[].curves[].center.x_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent OpeningV2.inner[].curves[].center |
| OpeningV2.inner[].curves[].center.y_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent OpeningV2.inner[].curves[].center |
| OpeningV2.inner[].curves[].center.z_mm | double | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent OpeningV2.inner[].curves[].center |
| OpeningV2.inner[].curves[].normal.x | double | dimensionless | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.inner[].curves[].normal |
| OpeningV2.inner[].curves[].normal.y | double | dimensionless | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.inner[].curves[].normal |
| OpeningV2.inner[].curves[].normal.z | double | dimensionless | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.inner[].curves[].normal |
| OpeningV2.inner[].curves[].radius_mm | double? | mm | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| OpeningV2.inner[].curves[].sweep_rad | double? | rad | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| OpeningV2.inner[].curves[].role | string | — | Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| OpeningV2.inner[].closed | bool | — | Clear aperture true contour, 05B-F06/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| OpeningV2.inner[].signed_area_mm2 | double | mm2 | Clear aperture true contour, 05B-F06/F31. / Analytic line/arc Green area, 05B-F37. |
| OpeningV2.pipe_outer[].id | string | — | Pipe physical outer contour; roof/catch no pipe = empty. / ENCODE(owner,loop_role,index), 05B-F46. |
| OpeningV2.pipe_outer[].role | string | — | Pipe physical outer contour; roof/catch no pipe = empty. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| OpeningV2.pipe_outer[].curves[].id | string | — | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| OpeningV2.pipe_outer[].curves[].kind | string | — | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| OpeningV2.pipe_outer[].curves[].start.x_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.pipe_outer[].curves[].start.y_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.pipe_outer[].curves[].start.z_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.pipe_outer[].curves[].end.x_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.pipe_outer[].curves[].end.y_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.pipe_outer[].curves[].end.z_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.pipe_outer[].curves[].center.x_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent OpeningV2.pipe_outer[].curves[].center |
| OpeningV2.pipe_outer[].curves[].center.y_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent OpeningV2.pipe_outer[].curves[].center |
| OpeningV2.pipe_outer[].curves[].center.z_mm | double | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent OpeningV2.pipe_outer[].curves[].center |
| OpeningV2.pipe_outer[].curves[].normal.x | double | dimensionless | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.pipe_outer[].curves[].normal |
| OpeningV2.pipe_outer[].curves[].normal.y | double | dimensionless | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.pipe_outer[].curves[].normal |
| OpeningV2.pipe_outer[].curves[].normal.z | double | dimensionless | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.pipe_outer[].curves[].normal |
| OpeningV2.pipe_outer[].curves[].radius_mm | double? | mm | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| OpeningV2.pipe_outer[].curves[].sweep_rad | double? | rad | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| OpeningV2.pipe_outer[].curves[].role | string | — | Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| OpeningV2.pipe_outer[].closed | bool | — | Pipe physical outer contour; roof/catch no pipe = empty. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| OpeningV2.pipe_outer[].signed_area_mm2 | double | mm2 | Pipe physical outer contour; roof/catch no pipe = empty. / Analytic line/arc Green area, 05B-F37. |
| OpeningV2.concrete_cutout[].id | string | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| OpeningV2.concrete_cutout[].role | string | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| OpeningV2.concrete_cutout[].curves[].id | string | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| OpeningV2.concrete_cutout[].curves[].kind | string | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| OpeningV2.concrete_cutout[].curves[].start.x_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.concrete_cutout[].curves[].start.y_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.concrete_cutout[].curves[].start.z_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.concrete_cutout[].curves[].end.x_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OpeningV2.concrete_cutout[].curves[].end.y_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OpeningV2.concrete_cutout[].curves[].end.z_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OpeningV2.concrete_cutout[].curves[].center.x_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent OpeningV2.concrete_cutout[].curves[].center |
| OpeningV2.concrete_cutout[].curves[].center.y_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent OpeningV2.concrete_cutout[].curves[].center |
| OpeningV2.concrete_cutout[].curves[].center.z_mm | double | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent OpeningV2.concrete_cutout[].curves[].center |
| OpeningV2.concrete_cutout[].curves[].normal.x | double | dimensionless | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.concrete_cutout[].curves[].normal |
| OpeningV2.concrete_cutout[].curves[].normal.y | double | dimensionless | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.concrete_cutout[].curves[].normal |
| OpeningV2.concrete_cutout[].curves[].normal.z | double | dimensionless | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent OpeningV2.concrete_cutout[].curves[].normal |
| OpeningV2.concrete_cutout[].curves[].radius_mm | double? | mm | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| OpeningV2.concrete_cutout[].curves[].sweep_rad | double? | rad | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| OpeningV2.concrete_cutout[].curves[].role | string | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| OpeningV2.concrete_cutout[].closed | bool | — | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| OpeningV2.concrete_cutout[].signed_area_mm2 | double | mm2 | Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Analytic line/arc Green area, 05B-F37. |
| OpeningV2.depth_min_mm | double | mm | Host inward/outward start depth from S3 ownership. |
| OpeningV2.depth_max_mm | double | mm | Host end depth; no double-count adjoining host. |
| OpeningV2.invert_mm | double? | mm | Pipe relative invert from S2; roof/catch=null. |
| OpeningV2.clearance_rule_id | string? | — | NR01 applicable for steel exclusion; no steel exclusion=null. |
| OpeningV2.source_ids[] | string[] | — | Source pipe/catch/access fields and contour derivation. |

### JointV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| JointV2.id | string | — | ENCODE(manhole_id,lower_assembly,upper_assembly), 05B-F46. |
| JointV2.lower_assembly_id | string | — | Body assembly for type2/3 wall-roof joint. |
| JointV2.upper_assembly_id | string | — | Roof assembly for type2/3; type1 joint absent. |
| JointV2.z_mm | double | mm | WallTop/RoofBottom contact surfaces, 05B-F13/F28. |
| JointV2.mortar_component_id | string? | — | ID of separately built bedding mortar ring, zero mortar=null. |
| JointV2.dowel_required | bool | — | HasDowel input; absence not inferred from source BLT count. |
| JointV2.source_ids[] | string[] | — | Profile/G9/HasDowel source IDs. |

### GeometryV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| GeometryV2.components[].id | string | — | Physical components emitted by S3 explicit component table. / ENCODE(manhole_id,semantic component key), exact keys in S3. |
| GeometryV2.components[].kind | string | — | Physical components emitted by S3 explicit component table. / Pad/Base/Wall/Roof/Neck/Frame/Panel/Catch/Haunch/Mortar/Product. |
| GeometryV2.components[].assembly_id | string | — | Physical components emitted by S3 explicit component table. / Profile ownership table S3; union only same assembly/material. |
| GeometryV2.components[].material_id | string | — | Physical components emitted by S3 explicit component table. / Concrete/Bedding/BeddingMortar/NativeProduct explicit role. |
| GeometryV2.components[].frame.id | string | — | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| GeometryV2.components[].frame.origin.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].frame.origin.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].frame.origin.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].frame.axis_u.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.axis_u.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.axis_u.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.axis_v.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.axis_v.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.axis_v.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].frame.handedness | int | — | Physical components emitted by S3 explicit component table. / Physical local-to-world basis; no sheet transform. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| GeometryV2.components[].loops[].id | string | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.components[].loops[].role | string | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.components[].loops[].curves[].id | string | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.components[].loops[].curves[].kind | string | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.components[].loops[].curves[].start.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].loops[].curves[].start.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].loops[].curves[].start.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].loops[].curves[].end.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].loops[].curves[].end.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].loops[].curves[].end.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].loops[].curves[].center.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.components[].loops[].curves[].center |
| GeometryV2.components[].loops[].curves[].center.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].loops[].curves[].center |
| GeometryV2.components[].loops[].curves[].center.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].loops[].curves[].center |
| GeometryV2.components[].loops[].curves[].normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].loops[].curves[].normal |
| GeometryV2.components[].loops[].curves[].normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].loops[].curves[].normal |
| GeometryV2.components[].loops[].curves[].normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].loops[].curves[].normal |
| GeometryV2.components[].loops[].curves[].radius_mm | double? | mm | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.components[].loops[].curves[].sweep_rad | double? | rad | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.components[].loops[].curves[].role | string | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.components[].loops[].closed | bool | — | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.components[].loops[].signed_area_mm2 | double | mm2 | Physical components emitted by S3 explicit component table. / Occupied footprint outer+inner loops; 05B-F27/F30/F31. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.components[].z_min_mm | double | mm | Physical components emitted by S3 explicit component table. / Component lower datum from effective.datums and S3 branch. |
| GeometryV2.components[].z_max_mm | double | mm | Physical components emitted by S3 explicit component table. / Component upper datum from effective.datums and S3 branch. |
| GeometryV2.components[].thickness_mm | double | mm | Physical components emitted by S3 explicit component table. / Pass-through active component thickness or derived slab Z span, 05B-F27. |
| GeometryV2.components[].patches[].id | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / ENCODE(component_id,face_role,patch_index), 05B-F46. |
| GeometryV2.components[].patches[].component_id | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Owner component exact ID. |
| GeometryV2.components[].patches[].face_role | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Top/Bottom/Inner/Outer/OpeningReveal/Contact. |
| GeometryV2.components[].patches[].frame.id | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| GeometryV2.components[].patches[].frame.origin.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].patches[].frame.origin.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].frame.origin.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].frame.axis_u.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.axis_u.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.axis_u.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.axis_v.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.axis_v.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.axis_v.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].patches[].frame.handedness | int | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Patch local plane origin/axes/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| GeometryV2.components[].patches[].loops[].id | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.components[].patches[].loops[].role | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.components[].patches[].loops[].curves[].id | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.components[].patches[].loops[].curves[].kind | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.components[].patches[].loops[].curves[].start.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].patches[].loops[].curves[].start.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].loops[].curves[].start.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].loops[].curves[].end.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].patches[].loops[].curves[].end.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].loops[].curves[].end.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].patches[].loops[].curves[].center.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.components[].patches[].loops[].curves[].center |
| GeometryV2.components[].patches[].loops[].curves[].center.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].patches[].loops[].curves[].center |
| GeometryV2.components[].patches[].loops[].curves[].center.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].patches[].loops[].curves[].center |
| GeometryV2.components[].patches[].loops[].curves[].normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].patches[].loops[].curves[].normal |
| GeometryV2.components[].patches[].loops[].curves[].normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].patches[].loops[].curves[].normal |
| GeometryV2.components[].patches[].loops[].curves[].normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].patches[].loops[].curves[].normal |
| GeometryV2.components[].patches[].loops[].curves[].radius_mm | double? | mm | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.components[].patches[].loops[].curves[].sweep_rad | double? | rad | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.components[].patches[].loops[].curves[].role | string | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.components[].patches[].loops[].closed | bool | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.components[].patches[].loops[].signed_area_mm2 | double | mm2 | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer/inner loops in local XY of patch.frame; not global coordinates transformed twice, 05B-F26/F37. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.components[].patches[].area_mm2 | double | mm2 | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Outer areas minus inner, 05B-F37; no bbox area for curved voids. |
| GeometryV2.components[].patches[].adjacent_component_id | string? | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Real contact owner, not nearest label; no contact null. |
| GeometryV2.components[].patches[].source_ids[] | string[] | — | Physical components emitted by S3 explicit component table. / Exposed/contact/void faces from analytic component boundary. / Component input/void/contact sources. |
| GeometryV2.components[].void_ids[] | string[] | — | Physical components emitted by S3 explicit component table. / Owned openings IDs; same opening not subtracted twice. |
| GeometryV2.components[].source_ids[] | string[] | — | Physical components emitted by S3 explicit component table. / Input source IDs + construction formula IDs. |
| GeometryV2.components[].solid_parts[].id | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / ENCODE(component_id,part_role,index), 05B-F46. |
| GeometryV2.components[].solid_parts[].operation | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Union/Subtract in ordered CSG list; void ownership prevents subtraction twice. |
| GeometryV2.components[].solid_parts[].frame.id | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| GeometryV2.components[].solid_parts[].frame.origin.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].solid_parts[].frame.origin.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].frame.origin.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].frame.axis_u.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.axis_u.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.axis_u.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.axis_v.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.axis_v.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.axis_v.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.components[].solid_parts[].frame.handedness | int | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion plane world origin/U/V/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| GeometryV2.components[].solid_parts[].loops[].id | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.components[].solid_parts[].loops[].role | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.components[].solid_parts[].loops[].curves[].id | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.components[].solid_parts[].loops[].curves[].kind | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.components[].solid_parts[].loops[].curves[].start.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].solid_parts[].loops[].curves[].start.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].loops[].curves[].start.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].loops[].curves[].end.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.components[].solid_parts[].loops[].curves[].end.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].loops[].curves[].end.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.components[].solid_parts[].loops[].curves[].center.x_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].center |
| GeometryV2.components[].solid_parts[].loops[].curves[].center.y_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].center |
| GeometryV2.components[].solid_parts[].loops[].curves[].center.z_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].center |
| GeometryV2.components[].solid_parts[].loops[].curves[].normal.x | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].normal |
| GeometryV2.components[].solid_parts[].loops[].curves[].normal.y | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].normal |
| GeometryV2.components[].solid_parts[].loops[].curves[].normal.z | double | dimensionless | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.components[].solid_parts[].loops[].curves[].normal |
| GeometryV2.components[].solid_parts[].loops[].curves[].radius_mm | double? | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.components[].solid_parts[].loops[].curves[].sweep_rad | double? | rad | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.components[].solid_parts[].loops[].curves[].role | string | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.components[].solid_parts[].loops[].closed | bool | — | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.components[].solid_parts[].loops[].signed_area_mm2 | double | mm2 | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Plane section contours, analytic line/arc, no triangulated schematic length. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.components[].solid_parts[].depth_min_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion coordinate along frame.normal, 05B-F27. |
| GeometryV2.components[].solid_parts[].depth_max_mm | double | mm | Physical components emitted by S3 explicit component table. / Physical union/difference extrusion parts; supports triangular haunch/seat slopes without bbox substitute. / Extrusion end coordinate; >depth_min for active part. |
| GeometryV2.components[].local_key | string | — | Physical components emitted by S3 explicit component table. / Exact Base/Wall/T1..T4/Roof/Neck/Pad/etc key for legacy adapter; full id remains namespaced. NR-IO-002 records mapping. |
| GeometryV2.components[].material_role | string | — | Physical components emitted by S3 explicit component table. / Bedding/BodyConcrete/NeckConcrete/PanelConcrete/CatchConcrete/PipeConcrete/Plaster/BeddingMortar; exact role binding selects input material key. material_id is actual material family+grade, not merely Concrete. |
| GeometryV2.openings[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / ENCODE(manhole_id,host_id,opening_slot), stable. |
| GeometryV2.openings[].kind | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Round/Box/DoubleBox/RoofAccess/Catch. |
| GeometryV2.openings[].host_ids[] | string[] | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Partitioned owners from actual contour×depth intersection, 05B-F32. |
| GeometryV2.openings[].frame.id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| GeometryV2.openings[].frame.origin.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].frame.origin.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].frame.origin.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].frame.axis_u.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.axis_u.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.axis_u.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.axis_v.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.axis_v.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.axis_v.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.normal.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.normal.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.normal.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| GeometryV2.openings[].frame.handedness | int | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host plane U/Z with depth outward, 05B-F26. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| GeometryV2.openings[].inner[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.openings[].inner[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.openings[].inner[].curves[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.openings[].inner[].curves[].kind | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.openings[].inner[].curves[].start.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].inner[].curves[].start.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].inner[].curves[].start.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].inner[].curves[].end.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].inner[].curves[].end.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].inner[].curves[].end.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].inner[].curves[].center.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.openings[].inner[].curves[].center |
| GeometryV2.openings[].inner[].curves[].center.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].inner[].curves[].center |
| GeometryV2.openings[].inner[].curves[].center.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].inner[].curves[].center |
| GeometryV2.openings[].inner[].curves[].normal.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].inner[].curves[].normal |
| GeometryV2.openings[].inner[].curves[].normal.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].inner[].curves[].normal |
| GeometryV2.openings[].inner[].curves[].normal.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].inner[].curves[].normal |
| GeometryV2.openings[].inner[].curves[].radius_mm | double? | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.openings[].inner[].curves[].sweep_rad | double? | rad | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.openings[].inner[].curves[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.openings[].inner[].closed | bool | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.openings[].inner[].signed_area_mm2 | double | mm2 | Roof/pipe/catch openings with stable identity, actual contours/owners. / Clear aperture true contour, 05B-F06/F31. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.openings[].pipe_outer[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.openings[].pipe_outer[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.openings[].pipe_outer[].curves[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.openings[].pipe_outer[].curves[].kind | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.openings[].pipe_outer[].curves[].start.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].pipe_outer[].curves[].start.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].pipe_outer[].curves[].start.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].pipe_outer[].curves[].end.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].pipe_outer[].curves[].end.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].pipe_outer[].curves[].end.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].pipe_outer[].curves[].center.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].center |
| GeometryV2.openings[].pipe_outer[].curves[].center.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].center |
| GeometryV2.openings[].pipe_outer[].curves[].center.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].center |
| GeometryV2.openings[].pipe_outer[].curves[].normal.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].normal |
| GeometryV2.openings[].pipe_outer[].curves[].normal.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].normal |
| GeometryV2.openings[].pipe_outer[].curves[].normal.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].pipe_outer[].curves[].normal |
| GeometryV2.openings[].pipe_outer[].curves[].radius_mm | double? | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.openings[].pipe_outer[].curves[].sweep_rad | double? | rad | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.openings[].pipe_outer[].curves[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.openings[].pipe_outer[].closed | bool | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.openings[].pipe_outer[].signed_area_mm2 | double | mm2 | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe physical outer contour; roof/catch no pipe = empty. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.openings[].concrete_cutout[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / ENCODE(owner,loop_role,index), 05B-F46. |
| GeometryV2.openings[].concrete_cutout[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| GeometryV2.openings[].concrete_cutout[].curves[].id | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| GeometryV2.openings[].concrete_cutout[].curves[].kind | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| GeometryV2.openings[].concrete_cutout[].curves[].start.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].concrete_cutout[].curves[].start.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].concrete_cutout[].curves[].start.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].concrete_cutout[].curves[].end.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.openings[].concrete_cutout[].curves[].end.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.openings[].concrete_cutout[].curves[].end.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.openings[].concrete_cutout[].curves[].center.x_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].center |
| GeometryV2.openings[].concrete_cutout[].curves[].center.y_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].center |
| GeometryV2.openings[].concrete_cutout[].curves[].center.z_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].center |
| GeometryV2.openings[].concrete_cutout[].curves[].normal.x | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].normal |
| GeometryV2.openings[].concrete_cutout[].curves[].normal.y | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].normal |
| GeometryV2.openings[].concrete_cutout[].curves[].normal.z | double | dimensionless | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent GeometryV2.openings[].concrete_cutout[].curves[].normal |
| GeometryV2.openings[].concrete_cutout[].curves[].radius_mm | double? | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| GeometryV2.openings[].concrete_cutout[].curves[].sweep_rad | double? | rad | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| GeometryV2.openings[].concrete_cutout[].curves[].role | string | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| GeometryV2.openings[].concrete_cutout[].closed | bool | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| GeometryV2.openings[].concrete_cutout[].signed_area_mm2 | double | mm2 | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe outer+mortar or roof/catch clear cutout, 05B-F06/F31. / Analytic line/arc Green area, 05B-F37. |
| GeometryV2.openings[].depth_min_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host inward/outward start depth from S3 ownership. |
| GeometryV2.openings[].depth_max_mm | double | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Host end depth; no double-count adjoining host. |
| GeometryV2.openings[].invert_mm | double? | mm | Roof/pipe/catch openings with stable identity, actual contours/owners. / Pipe relative invert from S2; roof/catch=null. |
| GeometryV2.openings[].clearance_rule_id | string? | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / NR01 applicable for steel exclusion; no steel exclusion=null. |
| GeometryV2.openings[].source_ids[] | string[] | — | Roof/pipe/catch openings with stable identity, actual contours/owners. / Source pipe/catch/access fields and contour derivation. |
| GeometryV2.joints[].id | string | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / ENCODE(manhole_id,lower_assembly,upper_assembly), 05B-F46. |
| GeometryV2.joints[].lower_assembly_id | string | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / Body assembly for type2/3 wall-roof joint. |
| GeometryV2.joints[].upper_assembly_id | string | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / Roof assembly for type2/3; type1 joint absent. |
| GeometryV2.joints[].z_mm | double | mm | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / WallTop/RoofBottom contact surfaces, 05B-F13/F28. |
| GeometryV2.joints[].mortar_component_id | string? | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / ID of separately built bedding mortar ring, zero mortar=null. |
| GeometryV2.joints[].dowel_required | bool | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / HasDowel input; absence not inferred from source BLT count. |
| GeometryV2.joints[].source_ids[] | string[] | — | Type2/3 construction joints preserved; type1 excludes wall-roof joint. / Profile/G9/HasDowel source IDs. |
| GeometryV2.physical_hash | string | — | Canonical geometry data excluding layout/name, 05B-F46. |
| GeometryV2.products[].id | string | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / ENCODE(manhole_id,product contract id,placement role), 05B-F46. |
| GeometryV2.products[].contract_id | string | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Exact AssetsV2.products[].id selected in S2. |
| GeometryV2.products[].anchor.x_mm | double | mm | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| GeometryV2.products[].anchor.y_mm | double | mm | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ Y của point argument, 05B-F26/F27. |
| GeometryV2.products[].anchor.z_mm | double | mm | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ Z của point argument, 05B-F26/F27. |
| GeometryV2.products[].rotation_rad | double | rad | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Pass-through approved product registration rotation, not sheet mirror. |
| GeometryV2.products[].physical_scale | double | dimensionless | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / 1 exactly after native unit normalization; CONTRACT C01/05B-F25, not annotation scale. |
| GeometryV2.products[].plan_asset | string | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Pass-through selected ProductContractV2.plan_asset. |
| GeometryV2.products[].section_aa_asset | string | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Pass-through selected section AA asset. |
| GeometryV2.products[].section_bb_asset | string | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Pass-through selected section BB asset. |
| GeometryV2.products[].source_ids[] | string[] | — | Native product placements at physical1:1; never replace actual product by an outside-bounds prism. / Product/aperture/contact/registration evidence. |

### RoleValueV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RoleValueV2.role | string | — | Named run/bend/leg role, ordered where topology requires. |
| RoleValueV2.value_mm | double | mm | Value from approved shape expression/path, not glyph measurement. |
| RoleValueV2.source_ids[] | string[] | — | Expression inputs/shape rule sources. |

### OccurrenceV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| OccurrenceV2.id | string | — | ENCODE(manhole,host,panel_instance,face,layer,direction,family,station,retained ordinal), 05B-F46. |
| OccurrenceV2.parent_id | string | — | Preclip parent bar identity; retained segments share this ID. |
| OccurrenceV2.host_id | string | — | Pass-through MemberSpecV2.host_id. |
| OccurrenceV2.assembly_id | string | — | Pass-through geometry component assembly ownership. |
| OccurrenceV2.panel_instance_id | string? | — | Actual panel instance when applicable, else null. |
| OccurrenceV2.face | string | — | Pass-through member.face. |
| OccurrenceV2.layer | string | — | Pass-through member.layer. |
| OccurrenceV2.direction | string | — | Pass-through member.direction; no paper-coordinate classifier. |
| OccurrenceV2.family | string | — | Pass-through member.family. |
| OccurrenceV2.station_index | int | count | Original grid index before clipping, never reset by removed bars. |
| OccurrenceV2.station_mm | double | mm | 05B-F08/F18/F34 distribution coordinate. |
| OccurrenceV2.diameter_mm | int | mm | Resolved diameter after sourced catalog step-down; 05B-F33. |
| OccurrenceV2.requested_pitch_mm | double? | mm | Original requested pitch, preserved. |
| OccurrenceV2.actual_gap_mm | double? | mm | Distance to previous original station; count-based/first=null. |
| OccurrenceV2.opening_ids[] | string[] | — | Openings affecting retained interval and end roles. |
| OccurrenceV2.region_role | string | — | Uncut/Left/Right/Above/Below/Middle/Multiple in host convention. |
| OccurrenceV2.path[].id | string | — | Physical ordered line/arc centreline path, 05B-F35. / Stable segment ID from owner/path index, 05B-F46. |
| OccurrenceV2.path[].kind | string | — | Physical ordered line/arc centreline path, 05B-F35. / Line/Arc, exhaustive geometry helper. |
| OccurrenceV2.path[].start.x_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OccurrenceV2.path[].start.y_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OccurrenceV2.path[].start.z_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OccurrenceV2.path[].end.x_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| OccurrenceV2.path[].end.y_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| OccurrenceV2.path[].end.z_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| OccurrenceV2.path[].center.x_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent OccurrenceV2.path[].center |
| OccurrenceV2.path[].center.y_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent OccurrenceV2.path[].center |
| OccurrenceV2.path[].center.z_mm | double | mm | Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent OccurrenceV2.path[].center |
| OccurrenceV2.path[].normal.x | double | dimensionless | Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent OccurrenceV2.path[].normal |
| OccurrenceV2.path[].normal.y | double | dimensionless | Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent OccurrenceV2.path[].normal |
| OccurrenceV2.path[].normal.z | double | dimensionless | Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent OccurrenceV2.path[].normal |
| OccurrenceV2.path[].radius_mm | double? | mm | Physical ordered line/arc centreline path, 05B-F35. / Arc radius>0, Line=null. |
| OccurrenceV2.path[].sweep_rad | double? | rad | Physical ordered line/arc centreline path, 05B-F35. / Signed arc sweep, Line=null; 05B-F35. |
| OccurrenceV2.path[].role | string | — | Physical ordered line/arc centreline path, 05B-F35. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| OccurrenceV2.role_values[].role | string | — | Each named run/bend/nominal role, no First() averaging. / Named run/bend/leg role, ordered where topology requires. |
| OccurrenceV2.role_values[].value_mm | double | mm | Each named run/bend/nominal role, no First() averaging. / Value from approved shape expression/path, not glyph measurement. |
| OccurrenceV2.role_values[].source_ids[] | string[] | — | Each named run/bend/nominal role, no First() averaging. / Expression inputs/shape rule sources. |
| OccurrenceV2.straight_run_mm | double | mm | Sum physical straight portions before hook/bend components, 05B-F35. |
| OccurrenceV2.fabrication_length_mm | double? | mm | Sum centreline Lines+Arcs only if basis approved, otherwise null+diagnostic. |
| OccurrenceV2.schedule_length_mm | double? | mm | EVAL approved recipe.length_expr_id on typed roles, separate from fabrication. |
| OccurrenceV2.measurement_basis | string | — | Approved recipe basis; unknown cannot become Known. |
| OccurrenceV2.length_status | string | — | Known/NeedsRule; NeedsRule blocks accounting Final. |
| OccurrenceV2.shape_recipe_id | string | — | Exact approved shape recipe ID used for this occurrence. |
| OccurrenceV2.source_ids[] | string[] | — | Member inputs+clip/hook/length formula sources. |
| OccurrenceV2.spec_id | string | — | Exact MemberSpecV2.id; support/cover lookup never by near name. |
| OccurrenceV2.station_key | string | — | Original grid key ENCODE(spec id,i,j), before clipping and mirror, 05B-F46. |
| OccurrenceV2.fabrication_basis | string | — | Pass-through approved shape recipe.fabrication_basis, separate from schedule measurement_basis. |

### ClipLedgerV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ClipLedgerV2.parent_id | string | — | Original preclip parent occurrence ID. |
| ClipLedgerV2.opening_ids[] | string[] | — | All applicable actual-depth opening IDs. |
| ClipLedgerV2.original_length_mm | double | mm | Preclip support interval length on same basis. |
| ClipLedgerV2.removed_intervals_mm[] | double[] | mm | Flatten pairs [s0,e0,s1,e1,...], sorted non-overlapping; 05B-F32. |
| ClipLedgerV2.retained_intervals_mm[] | double[] | mm | Complement pairs, 05B-F32. |
| ClipLedgerV2.retained_ids[] | string[] | — | Output segment IDs corresponding to retained pairs. |
| ClipLedgerV2.conservation_error_mm | double | mm | original-sum(removed)-sum(retained), 05B-F32. |

### DowelPairV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| DowelPairV2.id | string | — | ENCODE(joint_id,station_index), 05B-F46. |
| DowelPairV2.joint_id | string | — | Actual JointV2 ID. |
| DowelPairV2.dowel_occurrence_id | string | — | Straight physical dowel occurrence. |
| DowelPairV2.hole_id | string | — | Associated hole semantic ID, native S10 mapping later. |
| DowelPairV2.center.x_mm | double | mm | Same station for dowel and hole, 05B-F34. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| DowelPairV2.center.y_mm | double | mm | Same station for dowel and hole, 05B-F34. / Tọa độ Y của point argument, 05B-F26/F27. |
| DowelPairV2.center.z_mm | double | mm | Same station for dowel and hole, 05B-F34. / Tọa độ Z của point argument, 05B-F26/F27. |
| DowelPairV2.hole_diameter_mm | double | mm | DowelHoleDia source > dowel diameter, NR06. |
| DowelPairV2.hole_depth_mm | double | mm | Approved NR06, not half DowelLength fallback. |
| DowelPairV2.hole_axis.x | double | dimensionless | Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| DowelPairV2.hole_axis.y | double | dimensionless | Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| DowelPairV2.hole_axis.z | double | dimensionless | Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| DowelPairV2.hole_z_min_mm | double | mm | RoofBottom from effective.datums for underside hole start, NR06/05B-F34. |
| DowelPairV2.hole_z_max_mm | double | mm | hole_z_min_mm+hole_depth_mm, approved NR06, 05B-F34. |
| DowelPairV2.source_ids[] | string[] | — | Dowel/hole input and approval references, no depth guessed. |

### CanonicalRowV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| CanonicalRowV2.id | string | — | Hash exact topology/diameter/roles/basis/owner signature, 05B-F46. |
| CanonicalRowV2.member_ids[] | string[] | — | Actual equal-signature occurrences, no duplicate view accounting. |
| CanonicalRowV2.diameter_mm | int | mm | Same for all members; mixed diameters cannot canonical merge. |
| CanonicalRowV2.single_length_mm | double | mm | Single known fabrication centreline length; canonical rows split on actual physical signature/length. |
| CanonicalRowV2.quantity | int | count | count distinct member IDs. |
| CanonicalRowV2.total_length_mm | double | mm | quantity*single_length_mm, 05B-F04; no presentation mean substitution. |
| CanonicalRowV2.mass_kg | double? | kg | Approved NR14 raw length×unit mass, else null/NeedsRule. |
| CanonicalRowV2.measurement_basis | string | — | FabricationCentreline, invariant canonical physical basis; presentation nominal lengths remain in occurrences/groups. |
| CanonicalRowV2.accounted_length_mm | double | mm | Length sum selected by approved NR14 mass_length_basis; total_length_mm remains physical fabrication sum. |
| CanonicalRowV2.mass_length_basis | string | — | Fabrication/Schedule from approved NR14, explicit when different. |

### RebarV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RebarV2.occurrences[].id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / ENCODE(manhole,host,panel_instance,face,layer,direction,family,station,retained ordinal), 05B-F46. |
| RebarV2.occurrences[].parent_id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Preclip parent bar identity; retained segments share this ID. |
| RebarV2.occurrences[].host_id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through MemberSpecV2.host_id. |
| RebarV2.occurrences[].assembly_id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through geometry component assembly ownership. |
| RebarV2.occurrences[].panel_instance_id | string? | — | Every actual piece once; per-panel prototypes expanded exactly once. / Actual panel instance when applicable, else null. |
| RebarV2.occurrences[].face | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through member.face. |
| RebarV2.occurrences[].layer | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through member.layer. |
| RebarV2.occurrences[].direction | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through member.direction; no paper-coordinate classifier. |
| RebarV2.occurrences[].family | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through member.family. |
| RebarV2.occurrences[].station_index | int | count | Every actual piece once; per-panel prototypes expanded exactly once. / Original grid index before clipping, never reset by removed bars. |
| RebarV2.occurrences[].station_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / 05B-F08/F18/F34 distribution coordinate. |
| RebarV2.occurrences[].diameter_mm | int | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Resolved diameter after sourced catalog step-down; 05B-F33. |
| RebarV2.occurrences[].requested_pitch_mm | double? | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Original requested pitch, preserved. |
| RebarV2.occurrences[].actual_gap_mm | double? | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Distance to previous original station; count-based/first=null. |
| RebarV2.occurrences[].opening_ids[] | string[] | — | Every actual piece once; per-panel prototypes expanded exactly once. / Openings affecting retained interval and end roles. |
| RebarV2.occurrences[].region_role | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Uncut/Left/Right/Above/Below/Middle/Multiple in host convention. |
| RebarV2.occurrences[].path[].id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Stable segment ID from owner/path index, 05B-F46. |
| RebarV2.occurrences[].path[].kind | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Line/Arc, exhaustive geometry helper. |
| RebarV2.occurrences[].path[].start.x_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RebarV2.occurrences[].path[].start.y_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| RebarV2.occurrences[].path[].start.z_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| RebarV2.occurrences[].path[].end.x_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RebarV2.occurrences[].path[].end.y_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| RebarV2.occurrences[].path[].end.z_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| RebarV2.occurrences[].path[].center.x_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent RebarV2.occurrences[].path[].center |
| RebarV2.occurrences[].path[].center.y_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent RebarV2.occurrences[].path[].center |
| RebarV2.occurrences[].path[].center.z_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent RebarV2.occurrences[].path[].center |
| RebarV2.occurrences[].path[].normal.x | double | dimensionless | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.occurrences[].path[].normal |
| RebarV2.occurrences[].path[].normal.y | double | dimensionless | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.occurrences[].path[].normal |
| RebarV2.occurrences[].path[].normal.z | double | dimensionless | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.occurrences[].path[].normal |
| RebarV2.occurrences[].path[].radius_mm | double? | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Arc radius>0, Line=null. |
| RebarV2.occurrences[].path[].sweep_rad | double? | rad | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / Signed arc sweep, Line=null; 05B-F35. |
| RebarV2.occurrences[].path[].role | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Physical ordered line/arc centreline path, 05B-F35. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| RebarV2.occurrences[].role_values[].role | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Each named run/bend/nominal role, no First() averaging. / Named run/bend/leg role, ordered where topology requires. |
| RebarV2.occurrences[].role_values[].value_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Each named run/bend/nominal role, no First() averaging. / Value from approved shape expression/path, not glyph measurement. |
| RebarV2.occurrences[].role_values[].source_ids[] | string[] | — | Every actual piece once; per-panel prototypes expanded exactly once. / Each named run/bend/nominal role, no First() averaging. / Expression inputs/shape rule sources. |
| RebarV2.occurrences[].straight_run_mm | double | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Sum physical straight portions before hook/bend components, 05B-F35. |
| RebarV2.occurrences[].fabrication_length_mm | double? | mm | Every actual piece once; per-panel prototypes expanded exactly once. / Sum centreline Lines+Arcs only if basis approved, otherwise null+diagnostic. |
| RebarV2.occurrences[].schedule_length_mm | double? | mm | Every actual piece once; per-panel prototypes expanded exactly once. / EVAL approved recipe.length_expr_id on typed roles, separate from fabrication. |
| RebarV2.occurrences[].measurement_basis | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Approved recipe basis; unknown cannot become Known. |
| RebarV2.occurrences[].length_status | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Known/NeedsRule; NeedsRule blocks accounting Final. |
| RebarV2.occurrences[].shape_recipe_id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Exact approved shape recipe ID used for this occurrence. |
| RebarV2.occurrences[].source_ids[] | string[] | — | Every actual piece once; per-panel prototypes expanded exactly once. / Member inputs+clip/hook/length formula sources. |
| RebarV2.occurrences[].spec_id | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Exact MemberSpecV2.id; support/cover lookup never by near name. |
| RebarV2.occurrences[].station_key | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Original grid key ENCODE(spec id,i,j), before clipping and mirror, 05B-F46. |
| RebarV2.occurrences[].fabrication_basis | string | — | Every actual piece once; per-panel prototypes expanded exactly once. / Pass-through approved shape recipe.fabrication_basis, separate from schedule measurement_basis. |
| RebarV2.clip_ledger[].parent_id | string | — | Every parent with removed/retained intervals, including fully removed. / Original preclip parent occurrence ID. |
| RebarV2.clip_ledger[].opening_ids[] | string[] | — | Every parent with removed/retained intervals, including fully removed. / All applicable actual-depth opening IDs. |
| RebarV2.clip_ledger[].original_length_mm | double | mm | Every parent with removed/retained intervals, including fully removed. / Preclip support interval length on same basis. |
| RebarV2.clip_ledger[].removed_intervals_mm[] | double[] | mm | Every parent with removed/retained intervals, including fully removed. / Flatten pairs [s0,e0,s1,e1,...], sorted non-overlapping; 05B-F32. |
| RebarV2.clip_ledger[].retained_intervals_mm[] | double[] | mm | Every parent with removed/retained intervals, including fully removed. / Complement pairs, 05B-F32. |
| RebarV2.clip_ledger[].retained_ids[] | string[] | — | Every parent with removed/retained intervals, including fully removed. / Output segment IDs corresponding to retained pairs. |
| RebarV2.clip_ledger[].conservation_error_mm | double | mm | Every parent with removed/retained intervals, including fully removed. / original-sum(removed)-sum(retained), 05B-F32. |
| RebarV2.features[].spec_id | string | — | State for all requested/inactive features, never silently omitted. / Member/feature spec ID. |
| RebarV2.features[].state | string | — | State for all requested/inactive features, never silently omitted. / Inactive/Ready/NeedsRule/Unsupported/Invalid. |
| RebarV2.features[].reason | string | — | State for all requested/inactive features, never silently omitted. / Explicit input predicate, missing rule, or concrete diagnostic. |
| RebarV2.features[].occurrence_ids[] | string[] | — | State for all requested/inactive features, never silently omitted. / Physical outputs of this feature; inactive empty. |
| RebarV2.features[].needsrule_ids[] | string[] | — | State for all requested/inactive features, never silently omitted. / Missing approved rule IDs, not an arbitrary retry flag. |
| RebarV2.dowel_pairs[].id | string | — | One joint/station creates exactly one dowel-hole pair. / ENCODE(joint_id,station_index), 05B-F46. |
| RebarV2.dowel_pairs[].joint_id | string | — | One joint/station creates exactly one dowel-hole pair. / Actual JointV2 ID. |
| RebarV2.dowel_pairs[].dowel_occurrence_id | string | — | One joint/station creates exactly one dowel-hole pair. / Straight physical dowel occurrence. |
| RebarV2.dowel_pairs[].hole_id | string | — | One joint/station creates exactly one dowel-hole pair. / Associated hole semantic ID, native S10 mapping later. |
| RebarV2.dowel_pairs[].center.x_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / Same station for dowel and hole, 05B-F34. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RebarV2.dowel_pairs[].center.y_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / Same station for dowel and hole, 05B-F34. / Tọa độ Y của point argument, 05B-F26/F27. |
| RebarV2.dowel_pairs[].center.z_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / Same station for dowel and hole, 05B-F34. / Tọa độ Z của point argument, 05B-F26/F27. |
| RebarV2.dowel_pairs[].hole_diameter_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / DowelHoleDia source > dowel diameter, NR06. |
| RebarV2.dowel_pairs[].hole_depth_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / Approved NR06, not half DowelLength fallback. |
| RebarV2.dowel_pairs[].hole_axis.x | double | dimensionless | One joint/station creates exactly one dowel-hole pair. / Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| RebarV2.dowel_pairs[].hole_axis.y | double | dimensionless | One joint/station creates exactly one dowel-hole pair. / Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| RebarV2.dowel_pairs[].hole_axis.z | double | dimensionless | One joint/station creates exactly one dowel-hole pair. / Approved vertical hole direction from joint spec; same physical pair transform. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| RebarV2.dowel_pairs[].hole_z_min_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / RoofBottom from effective.datums for underside hole start, NR06/05B-F34. |
| RebarV2.dowel_pairs[].hole_z_max_mm | double | mm | One joint/station creates exactly one dowel-hole pair. / hole_z_min_mm+hole_depth_mm, approved NR06, 05B-F34. |
| RebarV2.dowel_pairs[].source_ids[] | string[] | — | One joint/station creates exactly one dowel-hole pair. / Dowel/hole input and approval references, no depth guessed. |
| RebarV2.canonical[].id | string | — | Exact shape/length invariant canonical rows. / Hash exact topology/diameter/roles/basis/owner signature, 05B-F46. |
| RebarV2.canonical[].member_ids[] | string[] | — | Exact shape/length invariant canonical rows. / Actual equal-signature occurrences, no duplicate view accounting. |
| RebarV2.canonical[].diameter_mm | int | mm | Exact shape/length invariant canonical rows. / Same for all members; mixed diameters cannot canonical merge. |
| RebarV2.canonical[].single_length_mm | double | mm | Exact shape/length invariant canonical rows. / Single known fabrication centreline length; canonical rows split on actual physical signature/length. |
| RebarV2.canonical[].quantity | int | count | Exact shape/length invariant canonical rows. / count distinct member IDs. |
| RebarV2.canonical[].total_length_mm | double | mm | Exact shape/length invariant canonical rows. / quantity*single_length_mm, 05B-F04; no presentation mean substitution. |
| RebarV2.canonical[].mass_kg | double? | kg | Exact shape/length invariant canonical rows. / Approved NR14 raw length×unit mass, else null/NeedsRule. |
| RebarV2.canonical[].measurement_basis | string | — | Exact shape/length invariant canonical rows. / FabricationCentreline, invariant canonical physical basis; presentation nominal lengths remain in occurrences/groups. |
| RebarV2.canonical[].accounted_length_mm | double | mm | Exact shape/length invariant canonical rows. / Length sum selected by approved NR14 mass_length_basis; total_length_mm remains physical fabrication sum. |
| RebarV2.canonical[].mass_length_basis | string | — | Exact shape/length invariant canonical rows. / Fabrication/Schedule from approved NR14, explicit when different. |
| RebarV2.physical_hash | string | — | Hash physical paths/identity/basis; excludes labels/layout. |
| RebarV2.angles[].id | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / One actual angle frame/member identity, no duplicate view accounting, 05B-F46. |
| RebarV2.angles[].host_id | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual Neck/Panel host component ID. |
| RebarV2.angles[].panel_instance_id | string? | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Physical panel instance when applicable, else null. |
| RebarV2.angles[].catalog_code | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Exact CF.Data angle code, not fuzzy. |
| RebarV2.angles[].unit_mass_kg_per_m | double | kg/m | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / CF.Data C paired with exact B code. |
| RebarV2.angles[].path[].id | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Stable segment ID from owner/path index, 05B-F46. |
| RebarV2.angles[].path[].kind | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Line/Arc, exhaustive geometry helper. |
| RebarV2.angles[].path[].start.x_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RebarV2.angles[].path[].start.y_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| RebarV2.angles[].path[].start.z_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| RebarV2.angles[].path[].end.x_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| RebarV2.angles[].path[].end.y_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| RebarV2.angles[].path[].end.z_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| RebarV2.angles[].path[].center.x_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent RebarV2.angles[].path[].center |
| RebarV2.angles[].path[].center.y_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent RebarV2.angles[].path[].center |
| RebarV2.angles[].path[].center.z_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent RebarV2.angles[].path[].center |
| RebarV2.angles[].path[].normal.x | double | dimensionless | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.angles[].path[].normal |
| RebarV2.angles[].path[].normal.y | double | dimensionless | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.angles[].path[].normal |
| RebarV2.angles[].path[].normal.z | double | dimensionless | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent RebarV2.angles[].path[].normal |
| RebarV2.angles[].path[].radius_mm | double? | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Arc radius>0, Line=null. |
| RebarV2.angles[].path[].sweep_rad | double? | rad | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / Signed arc sweep, Line=null; 05B-F35. |
| RebarV2.angles[].path[].role | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Actual angle accounting path from approved frame support rule, not glyph. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| RebarV2.angles[].total_length_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Sum actual ordered line/arc path lengths, 05B-F35. |
| RebarV2.angles[].mass_kg | double | kg | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / total_length_mm/1000*unit_mass_kg_per_m, 05B-F01/F20. |
| RebarV2.angles[].shape_recipe_id | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Exact TK.TG/physical role recipe. |
| RebarV2.angles[].source_ids[] | string[] | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Angle code/unit mass/position/physical path rule sources. |
| RebarV2.angles[].spec_id | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Exact S2 MemberSpecV2 identity; scope/face/layer never guessed from catalog code or label. |
| RebarV2.angles[].role_values[].role | string | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Pass-through actual ordered physical frame segment roles from approved angle recipe. / Named run/bend/leg role, ordered where topology requires. |
| RebarV2.angles[].role_values[].value_mm | double | mm | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Pass-through actual ordered physical frame segment roles from approved angle recipe. / Value from approved shape expression/path, not glyph measurement. |
| RebarV2.angles[].role_values[].source_ids[] | string[] | — | Physical angle accounting uses catalog kg/m, never round-steel Ø formula. / Pass-through actual ordered physical frame segment roles from approved angle recipe. / Expression inputs/shape rule sources. |
| RebarV2.fabrication_geometry_hash | string | — | Hash geometry S3 plus resolved dowel-hole geometry from pairs, 05B-F46; preserve design geometry hash unchanged. |

### GroupV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| GroupV2.id | string | — | Hash compatibility key and sorted member IDs, 05B-F46. |
| GroupV2.host_id | string | — | Same member host, no merge across owners. |
| GroupV2.face | string | — | Same member face. |
| GroupV2.layer | string | — | Same member layer. |
| GroupV2.direction | string | — | Same member direction. |
| GroupV2.family | string | — | Same member family. |
| GroupV2.region_role | string | — | Host convention from clip endpoint metadata, not label enumeration. |
| GroupV2.opening_ids[] | string[] | — | Sorted exact OpeningRegionSet. |
| GroupV2.shape_recipe_id | string | — | Same topology/role/basis-compatible recipe. |
| GroupV2.diameter_mm | int? | mm | Same round member diameter; Angle=null, never display kg/m as Ø. |
| GroupV2.member_ids[] | string[] | — | Distinct physical retained occurrences sorted by host station. |
| GroupV2.canonical_ids[] | string[] | — | Canonical rows containing members, no fuzzy display join. |
| GroupV2.member_lengths_mm[] | double[] | mm | Member ScheduleLength in same member_ids order. |
| GroupV2.count | int | count | member_ids.Length >0, 05B-F04. |
| GroupV2.raw_sum_mm | double | mm | sum member_lengths_mm without rounding, 05B-F04. |
| GroupV2.mean_mm | double | mm | raw_sum_mm/count, 05B-F04. |
| GroupV2.min_mm | double | mm | min member lengths, diagnostic only. |
| GroupV2.max_mm | double | mm | max member lengths, diagnostic only. |
| GroupV2.mean_roles[].role | string | — | Mean each compatible role separately, 05B-F05. / Named run/bend/leg role, ordered where topology requires. |
| GroupV2.mean_roles[].value_mm | double | mm | Mean each compatible role separately, 05B-F05. / Value from approved shape expression/path, not glyph measurement. |
| GroupV2.mean_roles[].source_ids[] | string[] | — | Mean each compatible role separately, 05B-F05. / Expression inputs/shape rule sources. |
| GroupV2.display_mark | string | — | Owner region naming + literal prefix, explicit aliases only NR10/11/12. |
| GroupV2.pitch_mm | double? | mm | Same compatible requested/actual pitch convention, no fake @150. |
| GroupV2.mass_kg | double? | kg | sum actual member raw masses under approved NR14. |
| GroupV2.measurement_basis | string | — | Pass-through same approved member basis. |
| GroupV2.source_ids[] | string[] | — | Membership/owner naming/rule/length evidence. |
| GroupV2.material_kind | string | — | RoundBar/Angle partition, no mixing unit mass with diameter. |
| GroupV2.angle_code | string? | — | Angle group exact catalog code; round bar=null. |
| GroupV2.unit_mass_kg_per_m | double? | kg/m | Angle catalog unit mass; round bar unit mass from NR14 if needed. |
| GroupV2.member_length_basis | string | — | Schedule/Fabrication from recipe.presentation_length_basis; all members compatible. |
| GroupV2.length_policy_id | string | — | Exact approved shape/length rule version, not inferred from matching PDF numbers. |

### GroupsV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| GroupsV2.items[].id | string | — | One nonempty compatible partition per group. / Hash compatibility key and sorted member IDs, 05B-F46. |
| GroupsV2.items[].host_id | string | — | One nonempty compatible partition per group. / Same member host, no merge across owners. |
| GroupsV2.items[].face | string | — | One nonempty compatible partition per group. / Same member face. |
| GroupsV2.items[].layer | string | — | One nonempty compatible partition per group. / Same member layer. |
| GroupsV2.items[].direction | string | — | One nonempty compatible partition per group. / Same member direction. |
| GroupsV2.items[].family | string | — | One nonempty compatible partition per group. / Same member family. |
| GroupsV2.items[].region_role | string | — | One nonempty compatible partition per group. / Host convention from clip endpoint metadata, not label enumeration. |
| GroupsV2.items[].opening_ids[] | string[] | — | One nonempty compatible partition per group. / Sorted exact OpeningRegionSet. |
| GroupsV2.items[].shape_recipe_id | string | — | One nonempty compatible partition per group. / Same topology/role/basis-compatible recipe. |
| GroupsV2.items[].diameter_mm | int? | mm | One nonempty compatible partition per group. / Same round member diameter; Angle=null, never display kg/m as Ø. |
| GroupsV2.items[].member_ids[] | string[] | — | One nonempty compatible partition per group. / Distinct physical retained occurrences sorted by host station. |
| GroupsV2.items[].canonical_ids[] | string[] | — | One nonempty compatible partition per group. / Canonical rows containing members, no fuzzy display join. |
| GroupsV2.items[].member_lengths_mm[] | double[] | mm | One nonempty compatible partition per group. / Member ScheduleLength in same member_ids order. |
| GroupsV2.items[].count | int | count | One nonempty compatible partition per group. / member_ids.Length >0, 05B-F04. |
| GroupsV2.items[].raw_sum_mm | double | mm | One nonempty compatible partition per group. / sum member_lengths_mm without rounding, 05B-F04. |
| GroupsV2.items[].mean_mm | double | mm | One nonempty compatible partition per group. / raw_sum_mm/count, 05B-F04. |
| GroupsV2.items[].min_mm | double | mm | One nonempty compatible partition per group. / min member lengths, diagnostic only. |
| GroupsV2.items[].max_mm | double | mm | One nonempty compatible partition per group. / max member lengths, diagnostic only. |
| GroupsV2.items[].mean_roles[].role | string | — | One nonempty compatible partition per group. / Mean each compatible role separately, 05B-F05. / Named run/bend/leg role, ordered where topology requires. |
| GroupsV2.items[].mean_roles[].value_mm | double | mm | One nonempty compatible partition per group. / Mean each compatible role separately, 05B-F05. / Value from approved shape expression/path, not glyph measurement. |
| GroupsV2.items[].mean_roles[].source_ids[] | string[] | — | One nonempty compatible partition per group. / Mean each compatible role separately, 05B-F05. / Expression inputs/shape rule sources. |
| GroupsV2.items[].display_mark | string | — | One nonempty compatible partition per group. / Owner region naming + literal prefix, explicit aliases only NR10/11/12. |
| GroupsV2.items[].pitch_mm | double? | mm | One nonempty compatible partition per group. / Same compatible requested/actual pitch convention, no fake @150. |
| GroupsV2.items[].mass_kg | double? | kg | One nonempty compatible partition per group. / sum actual member raw masses under approved NR14. |
| GroupsV2.items[].measurement_basis | string | — | One nonempty compatible partition per group. / Pass-through same approved member basis. |
| GroupsV2.items[].source_ids[] | string[] | — | One nonempty compatible partition per group. / Membership/owner naming/rule/length evidence. |
| GroupsV2.items[].material_kind | string | — | One nonempty compatible partition per group. / RoundBar/Angle partition, no mixing unit mass with diameter. |
| GroupsV2.items[].angle_code | string? | — | One nonempty compatible partition per group. / Angle group exact catalog code; round bar=null. |
| GroupsV2.items[].unit_mass_kg_per_m | double? | kg/m | One nonempty compatible partition per group. / Angle catalog unit mass; round bar unit mass from NR14 if needed. |
| GroupsV2.items[].member_length_basis | string | — | One nonempty compatible partition per group. / Schedule/Fabrication from recipe.presentation_length_basis; all members compatible. |
| GroupsV2.items[].length_policy_id | string | — | One nonempty compatible partition per group. / Exact approved shape/length rule version, not inferred from matching PDF numbers. |
| GroupsV2.registry_hash | string | — | Hash groups and membership/means unrounded, 05B-F46. |

### ViewV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ViewV2.id | string | — | ENCODE(manhole_id,view_role,page_scope), 05B-F46. |
| ViewV2.role | string | — | StructurePlan/SectionAA/SectionBB/SteelAA/SteelBB/WallMesh/BaseTop/BaseBottom/WallT1Outer/WallT1Inner/WallT2Outer/WallT2Inner/WallT3Outer/WallT3Inner/WallT4Outer/WallT4Inner/RoofTop/RoofBottom/NeckPlan/NeckAA/NeckBB/CatchPlan/Catch11/Catch22. |
| ViewV2.owner_id | string | — | Actual assembly/component semantic owner. |
| ViewV2.frame.id | string | — | World-to-view orthonormal frame from role table, 05B-F38. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ViewV2.frame.origin.x_mm | double | mm | World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ViewV2.frame.origin.y_mm | double | mm | World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ViewV2.frame.origin.z_mm | double | mm | World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ViewV2.frame.axis_u.x | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.axis_u.y | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.axis_u.z | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.axis_v.x | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.axis_v.y | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.axis_v.z | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.normal.x | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.normal.y | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.normal.z | double | dimensionless | World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.frame.handedness | int | — | World-to-view orthonormal frame from role table, 05B-F38. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ViewV2.section_offset_mm | double? | mm | Section plane offset from resolved component datum/center; plan=null. |
| ViewV2.scale | double | 1:S | Effective body/panel scale by role; >0. |
| ViewV2.mirror | bool | — | Explicit view convention, not negative physical product scale. |
| ViewV2.required | bool | — | Role predicate profile/input active; false must have na_reason. |
| ViewV2.na_reason | string? | — | Inactive input rule/source reason only, required=true null. |
| ViewV2.component_cuts[].component_id | string | — | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Exact component receiving this cut/projection frame. |
| ViewV2.component_cuts[].frame.id | string | — | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ViewV2.component_cuts[].frame.origin.x_mm | double | mm | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ViewV2.component_cuts[].frame.origin.y_mm | double | mm | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ViewV2.component_cuts[].frame.origin.z_mm | double | mm | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ViewV2.component_cuts[].frame.axis_u.x | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.axis_u.y | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.axis_u.z | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.axis_v.x | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.axis_v.y | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.axis_v.z | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.normal.x | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.normal.y | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.normal.z | double | dimensionless | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewV2.component_cuts[].frame.handedness | int | — | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ViewV2.component_cuts[].mode | string | — | AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / TrueSection/CompositePublished/Projection; no physical geometry deformation. |

### ProjectedCurveV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProjectedCurveV2.id | string | — | ENCODE(view_id,source_id,curve_index,visibility), 05B-F46. |
| ProjectedCurveV2.view_id | string | — | Owner ViewV2.id. |
| ProjectedCurveV2.source_id | string | — | Geometry component/opening/occurrence ID, not a PDF source handle. |
| ProjectedCurveV2.group_id | string? | — | GroupRegistry ID for steel, geometry=null. |
| ProjectedCurveV2.kind | string | — | Line/Arc/Dot/RegionBoundary/ProductReference. |
| ProjectedCurveV2.start.x_mm | double | mm | World point projected by dot axes, 05B-F38. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectedCurveV2.start.y_mm | double | mm | World point projected by dot axes, 05B-F38. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectedCurveV2.end.x_mm | double | mm | Projected endpoint, same transform. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectedCurveV2.end.y_mm | double | mm | Projected endpoint, same transform. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectedCurveV2.center.x_mm | double | mm | Arc/dot centre, Line=null. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectedCurveV2.center |
| ProjectedCurveV2.center.y_mm | double | mm | Arc/dot centre, Line=null. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectedCurveV2.center |
| ProjectedCurveV2.radius_mm | double? | model mm | Arc physical projected radius; not a symbol scale. |
| ProjectedCurveV2.sweep_rad | double? | rad | Arc signed sweep transformed with handedness, 05B-F38. |
| ProjectedCurveV2.visibility | string | — | Visible/Hidden/Sectioned from source depth/section predicate. |
| ProjectedCurveV2.role | string | — | Geometry/Opening/SteelMain/SteelAccessory/DowelHole/Product; no color inference. |
| ProjectedCurveV2.projection_frame_id | string | — | Exact ViewPartCutV2.frame.id used; inverse transform uses this frame, not guessed main view frame. |

### Target2V2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Target2V2.id | string | — | ENCODE(view_id,source_id,target_role,index), 05B-F46. |
| Target2V2.view_id | string | — | Owner view ID. |
| Target2V2.source_id | string | — | Source component/surface/opening/physical member. |
| Target2V2.group_id | string? | — | Group ID when target belongs to steel group. |
| Target2V2.kind | string | — | Bar/Surface/Opening/Material/TieNode/Joint/DowelHole/Product/ViewTitle/Elevation. S6 registers title and datum anchors explicitly for S7. |
| Target2V2.point.x_mm | double | mm | Valid point target, curve/region target may be null. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. Nullable parent Target2V2.point |
| Target2V2.point.y_mm | double | mm | Valid point target, curve/region target may be null. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. Nullable parent Target2V2.point |
| Target2V2.curve_ids[] | string[] | — | Projected path IDs representing target; no nearest-name lookup. |
| Target2V2.region_loop_ids[] | string[] | — | Projected material/void region loops when relevant. |
| Target2V2.visible | bool | — | Projection visibility, source retained even when hidden. |

### ProjectedOpeningV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProjectedOpeningV2.opening_id | string | — | Pass-through geometry.openings[].id. |
| ProjectedOpeningV2.view_id | string | — | View receiving representation. |
| ProjectedOpeningV2.host_id | string | — | Same actual host ownership. |
| ProjectedOpeningV2.representation | string | — | SectionContour/SideHidden/PlanContour chosen from intersection predicate. |
| ProjectedOpeningV2.curve_ids[] | string[] | — | Curves emitted from same inner/outer/cutout contour and same transform. |
| ProjectedOpeningV2.projection_frame_id | string | — | Actual host/component section frame used for this opening representation. |

### ProjectionV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProjectionV2.views[].id | string | — | All required/conditional view roles explicitly expanded S6. / ENCODE(manhole_id,view_role,page_scope), 05B-F46. |
| ProjectionV2.views[].role | string | — | All required/conditional view roles explicitly expanded S6. / StructurePlan/SectionAA/SectionBB/SteelAA/SteelBB/WallMesh/BaseTop/BaseBottom/WallT1Outer/WallT1Inner/WallT2Outer/WallT2Inner/WallT3Outer/WallT3Inner/WallT4Outer/WallT4Inner/RoofTop/RoofBottom/NeckPlan/NeckAA/NeckBB/CatchPlan/Catch11/Catch22. |
| ProjectionV2.views[].owner_id | string | — | All required/conditional view roles explicitly expanded S6. / Actual assembly/component semantic owner. |
| ProjectionV2.views[].frame.id | string | — | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ProjectionV2.views[].frame.origin.x_mm | double | mm | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProjectionV2.views[].frame.origin.y_mm | double | mm | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProjectionV2.views[].frame.origin.z_mm | double | mm | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProjectionV2.views[].frame.axis_u.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.axis_u.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.axis_u.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.axis_v.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.axis_v.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.axis_v.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.normal.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.normal.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.normal.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].frame.handedness | int | — | All required/conditional view roles explicitly expanded S6. / World-to-view orthonormal frame from role table, 05B-F38. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ProjectionV2.views[].section_offset_mm | double? | mm | All required/conditional view roles explicitly expanded S6. / Section plane offset from resolved component datum/center; plan=null. |
| ProjectionV2.views[].scale | double | 1:S | All required/conditional view roles explicitly expanded S6. / Effective body/panel scale by role; >0. |
| ProjectionV2.views[].mirror | bool | — | All required/conditional view roles explicitly expanded S6. / Explicit view convention, not negative physical product scale. |
| ProjectionV2.views[].required | bool | — | All required/conditional view roles explicitly expanded S6. / Role predicate profile/input active; false must have na_reason. |
| ProjectionV2.views[].na_reason | string? | — | All required/conditional view roles explicitly expanded S6. / Inactive input rule/source reason only, required=true null. |
| ProjectionV2.views[].component_cuts[].component_id | string | — | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Exact component receiving this cut/projection frame. |
| ProjectionV2.views[].component_cuts[].frame.id | string | — | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ProjectionV2.views[].component_cuts[].frame.origin.x_mm | double | mm | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProjectionV2.views[].component_cuts[].frame.origin.y_mm | double | mm | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProjectionV2.views[].component_cuts[].frame.origin.z_mm | double | mm | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProjectionV2.views[].component_cuts[].frame.axis_u.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.axis_u.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.axis_u.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.axis_v.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.axis_v.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.axis_v.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.normal.x | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.normal.y | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.normal.z | double | dimensionless | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ProjectionV2.views[].component_cuts[].frame.handedness | int | — | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ProjectionV2.views[].component_cuts[].mode | string | — | All required/conditional view roles explicitly expanded S6. / AA/BB body at centre, roof/neck through access per existing source behavior; no one fake section frame. / TrueSection/CompositePublished/Projection; no physical geometry deformation. |
| ProjectionV2.curves[].id | string | — | Projected shared model, no static fixture geometry. / ENCODE(view_id,source_id,curve_index,visibility), 05B-F46. |
| ProjectionV2.curves[].view_id | string | — | Projected shared model, no static fixture geometry. / Owner ViewV2.id. |
| ProjectionV2.curves[].source_id | string | — | Projected shared model, no static fixture geometry. / Geometry component/opening/occurrence ID, not a PDF source handle. |
| ProjectionV2.curves[].group_id | string? | — | Projected shared model, no static fixture geometry. / GroupRegistry ID for steel, geometry=null. |
| ProjectionV2.curves[].kind | string | — | Projected shared model, no static fixture geometry. / Line/Arc/Dot/RegionBoundary/ProductReference. |
| ProjectionV2.curves[].start.x_mm | double | mm | Projected shared model, no static fixture geometry. / World point projected by dot axes, 05B-F38. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.curves[].start.y_mm | double | mm | Projected shared model, no static fixture geometry. / World point projected by dot axes, 05B-F38. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.curves[].end.x_mm | double | mm | Projected shared model, no static fixture geometry. / Projected endpoint, same transform. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.curves[].end.y_mm | double | mm | Projected shared model, no static fixture geometry. / Projected endpoint, same transform. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.curves[].center.x_mm | double | mm | Projected shared model, no static fixture geometry. / Arc/dot centre, Line=null. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectionV2.curves[].center |
| ProjectionV2.curves[].center.y_mm | double | mm | Projected shared model, no static fixture geometry. / Arc/dot centre, Line=null. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectionV2.curves[].center |
| ProjectionV2.curves[].radius_mm | double? | model mm | Projected shared model, no static fixture geometry. / Arc physical projected radius; not a symbol scale. |
| ProjectionV2.curves[].sweep_rad | double? | rad | Projected shared model, no static fixture geometry. / Arc signed sweep transformed with handedness, 05B-F38. |
| ProjectionV2.curves[].visibility | string | — | Projected shared model, no static fixture geometry. / Visible/Hidden/Sectioned from source depth/section predicate. |
| ProjectionV2.curves[].role | string | — | Projected shared model, no static fixture geometry. / Geometry/Opening/SteelMain/SteelAccessory/DowelHole/Product; no color inference. |
| ProjectionV2.curves[].projection_frame_id | string | — | Projected shared model, no static fixture geometry. / Exact ViewPartCutV2.frame.id used; inverse transform uses this frame, not guessed main view frame. |
| ProjectionV2.targets[].id | string | — | Annotation anchor registry derived from physical sources. / ENCODE(view_id,source_id,target_role,index), 05B-F46. |
| ProjectionV2.targets[].view_id | string | — | Annotation anchor registry derived from physical sources. / Owner view ID. |
| ProjectionV2.targets[].source_id | string | — | Annotation anchor registry derived from physical sources. / Source component/surface/opening/physical member. |
| ProjectionV2.targets[].group_id | string? | — | Annotation anchor registry derived from physical sources. / Group ID when target belongs to steel group. |
| ProjectionV2.targets[].kind | string | — | Annotation anchor registry derived from physical sources. / Bar/Surface/Opening/Material/TieNode/Joint/DowelHole/Product/ViewTitle/Elevation. S6 registers title and datum anchors explicitly for S7. |
| ProjectionV2.targets[].point.x_mm | double | mm | Annotation anchor registry derived from physical sources. / Valid point target, curve/region target may be null. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectionV2.targets[].point |
| ProjectionV2.targets[].point.y_mm | double | mm | Annotation anchor registry derived from physical sources. / Valid point target, curve/region target may be null. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. Nullable parent ProjectionV2.targets[].point |
| ProjectionV2.targets[].curve_ids[] | string[] | — | Annotation anchor registry derived from physical sources. / Projected path IDs representing target; no nearest-name lookup. |
| ProjectionV2.targets[].region_loop_ids[] | string[] | — | Annotation anchor registry derived from physical sources. / Projected material/void region loops when relevant. |
| ProjectionV2.targets[].visible | bool | — | Annotation anchor registry derived from physical sources. / Projection visibility, source retained even when hidden. |
| ProjectionV2.opening_map[].opening_id | string | — | Same OpeningId across views, inverse transform check. / Pass-through geometry.openings[].id. |
| ProjectionV2.opening_map[].view_id | string | — | Same OpeningId across views, inverse transform check. / View receiving representation. |
| ProjectionV2.opening_map[].host_id | string | — | Same OpeningId across views, inverse transform check. / Same actual host ownership. |
| ProjectionV2.opening_map[].representation | string | — | Same OpeningId across views, inverse transform check. / SectionContour/SideHidden/PlanContour chosen from intersection predicate. |
| ProjectionV2.opening_map[].curve_ids[] | string[] | — | Same OpeningId across views, inverse transform check. / Curves emitted from same inner/outer/cutout contour and same transform. |
| ProjectionV2.opening_map[].projection_frame_id | string | — | Same OpeningId across views, inverse transform check. / Actual host/component section frame used for this opening representation. |
| ProjectionV2.regions[].id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / ENCODE(view_id,assembly_id,material_id,region_index), 05B-F46. |
| ProjectionV2.regions[].view_id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Actual view role ID. |
| ProjectionV2.regions[].assembly_id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Same pour assembly, joint/other materials never merged. |
| ProjectionV2.regions[].material_id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Concrete/Bedding/BeddingMortar/native role. |
| ProjectionV2.regions[].component_ids[] | string[] | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / All physical components contributing to this region. |
| ProjectionV2.regions[].loops[].id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / ENCODE(owner,loop_role,index), 05B-F46. |
| ProjectionV2.regions[].loops[].role | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| ProjectionV2.regions[].loops[].curves[].id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| ProjectionV2.regions[].loops[].curves[].kind | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| ProjectionV2.regions[].loops[].curves[].start.x_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProjectionV2.regions[].loops[].curves[].start.y_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProjectionV2.regions[].loops[].curves[].start.z_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProjectionV2.regions[].loops[].curves[].end.x_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProjectionV2.regions[].loops[].curves[].end.y_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProjectionV2.regions[].loops[].curves[].end.z_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProjectionV2.regions[].loops[].curves[].center.x_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent ProjectionV2.regions[].loops[].curves[].center |
| ProjectionV2.regions[].loops[].curves[].center.y_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent ProjectionV2.regions[].loops[].curves[].center |
| ProjectionV2.regions[].loops[].curves[].center.z_mm | double | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent ProjectionV2.regions[].loops[].curves[].center |
| ProjectionV2.regions[].loops[].curves[].normal.x | double | dimensionless | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent ProjectionV2.regions[].loops[].curves[].normal |
| ProjectionV2.regions[].loops[].curves[].normal.y | double | dimensionless | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent ProjectionV2.regions[].loops[].curves[].normal |
| ProjectionV2.regions[].loops[].curves[].normal.z | double | dimensionless | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent ProjectionV2.regions[].loops[].curves[].normal |
| ProjectionV2.regions[].loops[].curves[].radius_mm | double? | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| ProjectionV2.regions[].loops[].curves[].sweep_rad | double? | rad | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| ProjectionV2.regions[].loops[].curves[].role | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| ProjectionV2.regions[].loops[].closed | bool | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| ProjectionV2.regions[].loops[].signed_area_mm2 | double | mm2 | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Analytic line/arc Green area, 05B-F37. |
| ProjectionV2.regions[].projection_frame_id | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Actual section/projection frame key for region; composite body/access planes remain distinguishable. |
| ProjectionV2.regions[].surface_depth_mm | double? | mm | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Projection visible surface depth for step-edge visibility; TrueSection=null. |
| ProjectionV2.regions[].material_role | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Actual contributing role, or explicit mixed Concrete role requiring scoped hatch role binding; grade comes from component material_id. |
| ProjectionV2.regions[].representation_kind | string | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / TrueSection/Projection; visibility is not guessed from a nullable depth value. |
| ProjectionV2.regions[].visible_surface_id | string? | — | Shared projected material regions for geometry outlines and hatch; preserve inner loops. / Actual exposed CSG face/analytic surface ID for Projection; TrueSection=null. Flat surface depth may be scalar, sloped/curved surface depth is evaluated by this surface, never one midpoint depth for all points. |
| ProjectionV2.native_references[].id | string | — | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / ENCODE(view_id,source product id,asset name), 05B-F46. |
| ProjectionV2.native_references[].view_id | string | — | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Exact projection view ID. |
| ProjectionV2.native_references[].source_id | string | — | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Exact GeometryV2.products[].id, not schematic label ID. |
| ProjectionV2.native_references[].asset_name | string | — | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Product MB/AA/BB asset selected by explicit view role table. |
| ProjectionV2.native_references[].insertion.x_mm | double | mm | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Project actual world product anchor in selected view frame, 05B-F26/F38. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.native_references[].insertion.y_mm | double | mm | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Project actual world product anchor in selected view frame, 05B-F26/F38. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectionV2.native_references[].rotation_rad | double | rad | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Native registered in-plane rotation in this view, approved product binding. |
| ProjectionV2.native_references[].scale_x | double | dimensionless | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / 1 after unit normalization for physical product; no source distortion copied. |
| ProjectionV2.native_references[].scale_y | double | dimensionless | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / 1 after unit normalization for physical product. |
| ProjectionV2.native_references[].source_ids[] | string[] | — | Physical product INSERT intents separate from cast concrete CSG and paper label scaling. / Product/source transform evidence. |

### BoundAttributeV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| BoundAttributeV2.tag | string | — | Exact ATTDEF tag in selected AssetV2. |
| BoundAttributeV2.text | string | — | Formatted literal from typed value; no sample ATT fallback. |
| BoundAttributeV2.value_kind | string | — | Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| BoundAttributeV2.raw_number | double? | unit field | Unrounded numeric value when scalar; text/pair=null. |
| BoundAttributeV2.unit | string | — | mm/m/kg/count/kg_per_m/none, explicit per binding. |
| BoundAttributeV2.source_ids[] | string[] | — | Group/input/rule values that produced text. |

### MemberIntersectionV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| MemberIntersectionV2.occurrence_id | string | — | Physical member intersected by this collector branch. |
| MemberIntersectionV2.point.x_mm | double | mm | Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| MemberIntersectionV2.point.y_mm | double | mm | Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| MemberIntersectionV2.collector_curve_id | string | — | Actual branch curve containing crossing. |

### LabelV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| LabelV2.id | string | — | ENCODE(view_id,target_role,group_id/subject_id,label_index), 05B-F46. |
| LabelV2.view_id | string | — | Owner view ID. |
| LabelV2.group_id | string? | — | Required for steel labels, geometry/material=null. |
| LabelV2.target_ids[] | string[] | — | Exact registered target IDs; not coordinate nearest guesses. |
| LabelV2.asset_name | string | — | BLT1/2/3/4/BLTC/CT/TenMC/MC/NotThep or sourced role selector. |
| LabelV2.attributes[].tag | string | — | Exact schema bindings, formatted before ink measurement. / Exact ATTDEF tag in selected AssetV2. |
| LabelV2.attributes[].text | string | — | Exact schema bindings, formatted before ink measurement. / Formatted literal from typed value; no sample ATT fallback. |
| LabelV2.attributes[].value_kind | string | — | Exact schema bindings, formatted before ink measurement. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| LabelV2.attributes[].raw_number | double? | unit field | Exact schema bindings, formatted before ink measurement. / Unrounded numeric value when scalar; text/pair=null. |
| LabelV2.attributes[].unit | string | — | Exact schema bindings, formatted before ink measurement. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| LabelV2.attributes[].source_ids[] | string[] | — | Exact schema bindings, formatted before ink measurement. / Group/input/rule values that produced text. |
| LabelV2.insertion.x_mm | double | mm | Candidate placement maps label port to route landing, 05B-F14/F40. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| LabelV2.insertion.y_mm | double | mm | Candidate placement maps label port to route landing, 05B-F14/F40. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| LabelV2.port_native.x_mm | double | mm | Native route/text landing port, not always block insertion. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| LabelV2.port_native.y_mm | double | mm | Native route/text landing port, not always block insertion. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| LabelV2.asset_scale | double | dimensionless | paper target/native height × view.scale, 05B-F14/F16. |
| LabelV2.rotation_rad | double | rad | Explicit recipe rotation, not guessed by mirrored view. |
| LabelV2.ink.min_x_mm | double | mm | Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| LabelV2.ink.min_y_mm | double | mm | Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| LabelV2.ink.max_x_mm | double | mm | Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| LabelV2.ink.max_y_mm | double | mm | Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / max của ink/curve Y sau transform; 05B-F42. |
| LabelV2.route_ids[] | string[] | — | Polyline/line branches associated with this label. |
| LabelV2.source_ids[] | string[] | — | Group/target/recipe/font/paper offset sources. |

### RouteV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RouteV2.id | string | — | ENCODE(label_id,branch_index), 05B-F46. |
| RouteV2.view_id | string | — | Owner view ID. |
| RouteV2.group_id | string? | — | Steel collector group or null material/CT. |
| RouteV2.points[].x_mm | double | mm | Ordered route vertices from target→elbow→label port, 05B-F39. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| RouteV2.points[].y_mm | double | mm | Ordered route vertices from target→elbow→label port, 05B-F39. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| RouteV2.intersections[].occurrence_id | string | — | Only real same-group bar crossings outside voids. / Physical member intersected by this collector branch. |
| RouteV2.intersections[].point.x_mm | double | mm | Only real same-group bar crossings outside voids. / Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| RouteV2.intersections[].point.y_mm | double | mm | Only real same-group bar crossings outside voids. / Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| RouteV2.intersections[].collector_curve_id | string | — | Only real same-group bar crossings outside voids. / Actual branch curve containing crossing. |
| RouteV2.label_ids[] | string[] | — | Shared stroke may serve multiple labels; keep associations. |
| RouteV2.reason | string | — | Candidate lane/branch/target selection receipt, no arbitrary X/Y. |

### DimensionV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| DimensionV2.id | string | — | ENCODE(view_id,subject_id,axis,endpoints,lane_role), 05B-F46. |
| DimensionV2.view_id | string | — | Owner view ID. |
| DimensionV2.subject_id | string | — | ClearX/OverallX/WallThickness/Spacing/SurfaceZ/etc exact measurement subject. |
| DimensionV2.start.x_mm | double | mm | Projected source endpoint, 05B-F38/F43. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| DimensionV2.start.y_mm | double | mm | Projected source endpoint, 05B-F38/F43. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| DimensionV2.end.x_mm | double | mm | Projected source endpoint, same frame. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| DimensionV2.end.y_mm | double | mm | Projected source endpoint, same frame. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| DimensionV2.axis | string | — | U/V/Aligned based on measured subject. |
| DimensionV2.measured_mm | double | mm | dot(end-start,axis) magnitude or analytic path measure, 05B-F43. |
| DimensionV2.prefix | string | — | Literal WallDimPrefix/NeckPrefix by subject, no value override. |
| DimensionV2.display_text | string | — | Approved formatter of measured value+prefix, 05B-F44. |
| DimensionV2.lane_mm | double | model mm | Approved paper offset×view scale, 05B-F14. |
| DimensionV2.ink.min_x_mm | double | mm | Native-style text/ext line envelope, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| DimensionV2.ink.min_y_mm | double | mm | Native-style text/ext line envelope, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| DimensionV2.ink.max_x_mm | double | mm | Native-style text/ext line envelope, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| DimensionV2.ink.max_y_mm | double | mm | Native-style text/ext line envelope, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| DimensionV2.source_ids[] | string[] | — | Effective field/model endpoints and style/format sources. |

### TickV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| TickV2.id | string | — | ENCODE(route_id,occurrence_id,intersection_index), 05B-F46. |
| TickV2.view_id | string | — | Owner view. |
| TickV2.asset_name | string | — | Exact Gach asset from registry. |
| TickV2.occurrence_id | string | — | Real physical member at crossing. |
| TickV2.point.x_mm | double | mm | Pass-through validated MemberIntersectionV2.point. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| TickV2.point.y_mm | double | mm | Pass-through validated MemberIntersectionV2.point. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| TickV2.scale | double | dimensionless | Approved annotation symbol paper scale, 05B-F14. |

### AnnotationsV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AnnotationsV2.labels[].id | string | — | Complete labels with target/member/asset binding, not placeholder text. / ENCODE(view_id,target_role,group_id/subject_id,label_index), 05B-F46. |
| AnnotationsV2.labels[].view_id | string | — | Complete labels with target/member/asset binding, not placeholder text. / Owner view ID. |
| AnnotationsV2.labels[].group_id | string? | — | Complete labels with target/member/asset binding, not placeholder text. / Required for steel labels, geometry/material=null. |
| AnnotationsV2.labels[].target_ids[] | string[] | — | Complete labels with target/member/asset binding, not placeholder text. / Exact registered target IDs; not coordinate nearest guesses. |
| AnnotationsV2.labels[].asset_name | string | — | Complete labels with target/member/asset binding, not placeholder text. / BLT1/2/3/4/BLTC/CT/TenMC/MC/NotThep or sourced role selector. |
| AnnotationsV2.labels[].attributes[].tag | string | — | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / Exact ATTDEF tag in selected AssetV2. |
| AnnotationsV2.labels[].attributes[].text | string | — | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / Formatted literal from typed value; no sample ATT fallback. |
| AnnotationsV2.labels[].attributes[].value_kind | string | — | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| AnnotationsV2.labels[].attributes[].raw_number | double? | unit field | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / Unrounded numeric value when scalar; text/pair=null. |
| AnnotationsV2.labels[].attributes[].unit | string | — | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| AnnotationsV2.labels[].attributes[].source_ids[] | string[] | — | Complete labels with target/member/asset binding, not placeholder text. / Exact schema bindings, formatted before ink measurement. / Group/input/rule values that produced text. |
| AnnotationsV2.labels[].insertion.x_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Candidate placement maps label port to route landing, 05B-F14/F40. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.labels[].insertion.y_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Candidate placement maps label port to route landing, 05B-F14/F40. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.labels[].port_native.x_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Native route/text landing port, not always block insertion. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.labels[].port_native.y_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Native route/text landing port, not always block insertion. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.labels[].asset_scale | double | dimensionless | Complete labels with target/member/asset binding, not placeholder text. / paper target/native height × view.scale, 05B-F14/F16. |
| AnnotationsV2.labels[].rotation_rad | double | rad | Complete labels with target/member/asset binding, not placeholder text. / Explicit recipe rotation, not guessed by mirrored view. |
| AnnotationsV2.labels[].ink.min_x_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.labels[].ink.min_y_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.labels[].ink.max_x_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.labels[].ink.max_y_mm | double | mm | Complete labels with target/member/asset binding, not placeholder text. / Full glyph+text actual envelope after binding/alignment/scale, 05B-F40/F42. / max của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.labels[].route_ids[] | string[] | — | Complete labels with target/member/asset binding, not placeholder text. / Polyline/line branches associated with this label. |
| AnnotationsV2.labels[].source_ids[] | string[] | — | Complete labels with target/member/asset binding, not placeholder text. / Group/target/recipe/font/paper offset sources. |
| AnnotationsV2.routes[].id | string | — | Finite selected leader/collector routes and associations. / ENCODE(label_id,branch_index), 05B-F46. |
| AnnotationsV2.routes[].view_id | string | — | Finite selected leader/collector routes and associations. / Owner view ID. |
| AnnotationsV2.routes[].group_id | string? | — | Finite selected leader/collector routes and associations. / Steel collector group or null material/CT. |
| AnnotationsV2.routes[].points[].x_mm | double | mm | Finite selected leader/collector routes and associations. / Ordered route vertices from target→elbow→label port, 05B-F39. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.routes[].points[].y_mm | double | mm | Finite selected leader/collector routes and associations. / Ordered route vertices from target→elbow→label port, 05B-F39. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.routes[].intersections[].occurrence_id | string | — | Finite selected leader/collector routes and associations. / Only real same-group bar crossings outside voids. / Physical member intersected by this collector branch. |
| AnnotationsV2.routes[].intersections[].point.x_mm | double | mm | Finite selected leader/collector routes and associations. / Only real same-group bar crossings outside voids. / Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.routes[].intersections[].point.y_mm | double | mm | Finite selected leader/collector routes and associations. / Only real same-group bar crossings outside voids. / Analytic segment/arc crossing, 05B-F31/F39. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.routes[].intersections[].collector_curve_id | string | — | Finite selected leader/collector routes and associations. / Only real same-group bar crossings outside voids. / Actual branch curve containing crossing. |
| AnnotationsV2.routes[].label_ids[] | string[] | — | Finite selected leader/collector routes and associations. / Shared stroke may serve multiple labels; keep associations. |
| AnnotationsV2.routes[].reason | string | — | Finite selected leader/collector routes and associations. / Candidate lane/branch/target selection receipt, no arbitrary X/Y. |
| AnnotationsV2.dimensions[].id | string | — | Typed subjects and native measured endpoints. / ENCODE(view_id,subject_id,axis,endpoints,lane_role), 05B-F46. |
| AnnotationsV2.dimensions[].view_id | string | — | Typed subjects and native measured endpoints. / Owner view ID. |
| AnnotationsV2.dimensions[].subject_id | string | — | Typed subjects and native measured endpoints. / ClearX/OverallX/WallThickness/Spacing/SurfaceZ/etc exact measurement subject. |
| AnnotationsV2.dimensions[].start.x_mm | double | mm | Typed subjects and native measured endpoints. / Projected source endpoint, 05B-F38/F43. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.dimensions[].start.y_mm | double | mm | Typed subjects and native measured endpoints. / Projected source endpoint, 05B-F38/F43. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.dimensions[].end.x_mm | double | mm | Typed subjects and native measured endpoints. / Projected source endpoint, same frame. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.dimensions[].end.y_mm | double | mm | Typed subjects and native measured endpoints. / Projected source endpoint, same frame. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.dimensions[].axis | string | — | Typed subjects and native measured endpoints. / U/V/Aligned based on measured subject. |
| AnnotationsV2.dimensions[].measured_mm | double | mm | Typed subjects and native measured endpoints. / dot(end-start,axis) magnitude or analytic path measure, 05B-F43. |
| AnnotationsV2.dimensions[].prefix | string | — | Typed subjects and native measured endpoints. / Literal WallDimPrefix/NeckPrefix by subject, no value override. |
| AnnotationsV2.dimensions[].display_text | string | — | Typed subjects and native measured endpoints. / Approved formatter of measured value+prefix, 05B-F44. |
| AnnotationsV2.dimensions[].lane_mm | double | model mm | Typed subjects and native measured endpoints. / Approved paper offset×view scale, 05B-F14. |
| AnnotationsV2.dimensions[].ink.min_x_mm | double | mm | Typed subjects and native measured endpoints. / Native-style text/ext line envelope, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.dimensions[].ink.min_y_mm | double | mm | Typed subjects and native measured endpoints. / Native-style text/ext line envelope, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.dimensions[].ink.max_x_mm | double | mm | Typed subjects and native measured endpoints. / Native-style text/ext line envelope, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.dimensions[].ink.max_y_mm | double | mm | Typed subjects and native measured endpoints. / Native-style text/ext line envelope, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.dimensions[].source_ids[] | string[] | — | Typed subjects and native measured endpoints. / Effective field/model endpoints and style/format sources. |
| AnnotationsV2.ticks[].id | string | — | Gach only at actual intersections. / ENCODE(route_id,occurrence_id,intersection_index), 05B-F46. |
| AnnotationsV2.ticks[].view_id | string | — | Gach only at actual intersections. / Owner view. |
| AnnotationsV2.ticks[].asset_name | string | — | Gach only at actual intersections. / Exact Gach asset from registry. |
| AnnotationsV2.ticks[].occurrence_id | string | — | Gach only at actual intersections. / Real physical member at crossing. |
| AnnotationsV2.ticks[].point.x_mm | double | mm | Gach only at actual intersections. / Pass-through validated MemberIntersectionV2.point. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.ticks[].point.y_mm | double | mm | Gach only at actual intersections. / Pass-through validated MemberIntersectionV2.point. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| AnnotationsV2.ticks[].scale | double | dimensionless | Gach only at actual intersections. / Approved annotation symbol paper scale, 05B-F14. |
| AnnotationsV2.view_ink[].view_id | string | — | Union geometry+title+DIM+labels ink by view, 05B-F42. / View ID being measured. |
| AnnotationsV2.view_ink[].bounds.min_x_mm | double | mm | Union geometry+title+DIM+labels ink by view, 05B-F42. / Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.view_ink[].bounds.min_y_mm | double | mm | Union geometry+title+DIM+labels ink by view, 05B-F42. / Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.view_ink[].bounds.max_x_mm | double | mm | Union geometry+title+DIM+labels ink by view, 05B-F42. / Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| AnnotationsV2.view_ink[].bounds.max_y_mm | double | mm | Union geometry+title+DIM+labels ink by view, 05B-F42. / Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| AnnotationsV2.hatches[].id | string | — | Requested material hatch intents use the SAME projected region IDs. / ENCODE(view_id,region_id,pattern role), 05B-F46. |
| AnnotationsV2.hatches[].view_id | string | — | Requested material hatch intents use the SAME projected region IDs. / Owner view ID. |
| AnnotationsV2.hatches[].region_id | string | — | Requested material hatch intents use the SAME projected region IDs. / Exact Region2V2 ID, closed outer+inner loops available. |
| AnnotationsV2.hatches[].pattern_name | string | — | Requested material hatch intents use the SAME projected region IDs. / Exact requested catalog name or approved named fallback NR17. |
| AnnotationsV2.hatches[].pattern_scale | double | dimensionless | Requested material hatch intents use the SAME projected region IDs. / Exact paired CF.Data scale and approved presentation scale expression. |
| AnnotationsV2.hatches[].angle_rad | double | rad | Requested material hatch intents use the SAME projected region IDs. / Approved hatch role/source angle, not arbitrary zero if missing. |
| AnnotationsV2.hatches[].layer_id | string | — | Requested material hatch intents use the SAME projected region IDs. / Exact native semantic layer binding, preflight S0. |
| AnnotationsV2.hatches[].source_ids[] | string[] | — | Requested material hatch intents use the SAME projected region IDs. / CF.Ga/CF.Data/material mapping/approval evidence. |

### ViewInkV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ViewInkV2.view_id | string | — | View ID being measured. |
| ViewInkV2.bounds.min_x_mm | double | mm | Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| ViewInkV2.bounds.min_y_mm | double | mm | Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| ViewInkV2.bounds.max_x_mm | double | mm | Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| ViewInkV2.bounds.max_y_mm | double | mm | Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |

### ScheduleRowV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ScheduleRowV2.id | string | — | ENCODE(manhole_id,group_id,recipe_id), 05B-F46. |
| ScheduleRowV2.group_id | string | — | Same group consumed by labels/caption. |
| ScheduleRowV2.native_asset | string | — | Exact shape recipe native TK asset. |
| ScheduleRowV2.attributes[].tag | string | — | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Exact ATTDEF tag in selected AssetV2. |
| ScheduleRowV2.attributes[].text | string | — | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Formatted literal from typed value; no sample ATT fallback. |
| ScheduleRowV2.attributes[].value_kind | string | — | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| ScheduleRowV2.attributes[].raw_number | double? | unit field | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Unrounded numeric value when scalar; text/pair=null. |
| ScheduleRowV2.attributes[].unit | string | — | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| ScheduleRowV2.attributes[].source_ids[] | string[] | — | SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Group/input/rule values that produced text. |
| ScheduleRowV2.glyph_hash | string | — | Pass-through approved fixed native glyph hash. |
| ScheduleRowV2.insertion.x_mm | double | mm | S8 measured row layout point, 05B-F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ScheduleRowV2.insertion.y_mm | double | mm | S8 measured row layout point, 05B-F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ScheduleRowV2.scale | double | dimensionless | Native baseline scale view.S/100 where sourced, 05B-F14/F15. |
| ScheduleRowV2.ink.min_x_mm | double | mm | Actual row ink including glyph/basepoint/text, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| ScheduleRowV2.ink.min_y_mm | double | mm | Actual row ink including glyph/basepoint/text, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| ScheduleRowV2.ink.max_x_mm | double | mm | Actual row ink including glyph/basepoint/text, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| ScheduleRowV2.ink.max_y_mm | double | mm | Actual row ink including glyph/basepoint/text, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| ScheduleRowV2.view_id | string | — | ENCODE(manhole_id,Schedule,physical owner bucket); sheet includes row only in this view. |

### CaptionV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| CaptionV2.group_id | string | — | Same registry group. |
| CaptionV2.glyph_asset | string | — | Exact fixed topology glyph from recipe, not physical-sized bar. |
| CaptionV2.text | string | — | Mark-DØ,L=... or Ltb=... per approved length display policy. |
| CaptionV2.role_values[].role | string | — | Mean/fixed named segment dimensions, no collector length. / Named run/bend/leg role, ordered where topology requires. |
| CaptionV2.role_values[].value_mm | double | mm | Mean/fixed named segment dimensions, no collector length. / Value from approved shape expression/path, not glyph measurement. |
| CaptionV2.role_values[].source_ids[] | string[] | — | Mean/fixed named segment dimensions, no collector length. / Expression inputs/shape rule sources. |
| CaptionV2.underline_start.x_mm | double | mm | Caption actual ink min X at approved paper baseline. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.underline_start.y_mm | double | mm | Caption actual ink min X at approved paper baseline. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.underline_end.x_mm | double | mm | Caption actual ink max X, 05B-F42. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.underline_end.y_mm | double | mm | Caption actual ink max X, 05B-F42. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.ink.min_x_mm | double | mm | Glyph+dimensions+caption+underline envelope. / min của ink/curve X sau transform; 05B-F42. |
| CaptionV2.ink.min_y_mm | double | mm | Glyph+dimensions+caption+underline envelope. / min của ink/curve Y sau transform; 05B-F42. |
| CaptionV2.ink.max_x_mm | double | mm | Glyph+dimensions+caption+underline envelope. / max của ink/curve X sau transform; 05B-F42. |
| CaptionV2.ink.max_y_mm | double | mm | Glyph+dimensions+caption+underline envelope. / max của ink/curve Y sau transform; 05B-F42. |
| CaptionV2.view_id | string | — | ENCODE(manhole_id,Bending,owner bucket); not inferred from array position. |
| CaptionV2.insertion.x_mm | double | mm | Exact schematic insertion; fixed source glyph scale, F14/F17/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.insertion.y_mm | double | mm | Exact schematic insertion; fixed source glyph scale, F14/F17/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| CaptionV2.asset_scale | double | dimensionless | Source baseline scale S/100; never Li/source glyph dimension. |

### SummaryV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SummaryV2.id | string | — | ENCODE(manhole_id,summary_scope), 05B-F46. |
| SummaryV2.scope | string | — | Body/Neck/Panel/Catch from approved NR14 owner bucket map. |
| SummaryV2.native_asset | string | — | TK.THKL/TK.THKL.CT/approved summary asset. |
| SummaryV2.attributes[].tag | string | — | Exact existing tags, sums raw values not formatted cells. / Exact ATTDEF tag in selected AssetV2. |
| SummaryV2.attributes[].text | string | — | Exact existing tags, sums raw values not formatted cells. / Formatted literal from typed value; no sample ATT fallback. |
| SummaryV2.attributes[].value_kind | string | — | Exact existing tags, sums raw values not formatted cells. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| SummaryV2.attributes[].raw_number | double? | unit field | Exact existing tags, sums raw values not formatted cells. / Unrounded numeric value when scalar; text/pair=null. |
| SummaryV2.attributes[].unit | string | — | Exact existing tags, sums raw values not formatted cells. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| SummaryV2.attributes[].source_ids[] | string[] | — | Exact existing tags, sums raw values not formatted cells. / Group/input/rule values that produced text. |
| SummaryV2.mass_small_kg | double | kg | Sum raw masses for diameter<=10, 05B-F19/F20, native headers source. |
| SummaryV2.mass_large_kg | double | kg | Sum raw masses for diameter>10, 05B-F19/F20. |
| SummaryV2.view_id | string | — | Schedule view owning this summary, stable explicit identity. |
| SummaryV2.insertion.x_mm | double | mm | S8 actual summary insertion after rows, F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SummaryV2.insertion.y_mm | double | mm | S8 actual summary insertion after rows, F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SummaryV2.asset_scale | double | dimensionless | Native baseline scale S/100, F14/F15. |
| SummaryV2.ink.min_x_mm | double | mm | Measured summary glyph+ATTRIB ink, F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| SummaryV2.ink.min_y_mm | double | mm | Measured summary glyph+ATTRIB ink, F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| SummaryV2.ink.max_x_mm | double | mm | Measured summary glyph+ATTRIB ink, F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| SummaryV2.ink.max_y_mm | double | mm | Measured summary glyph+ATTRIB ink, F40/F42. / max của ink/curve Y sau transform; 05B-F42. |

### SchedulesV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SchedulesV2.rows[].id | string | — | One nonempty compatible presentation group per native row. / ENCODE(manhole_id,group_id,recipe_id), 05B-F46. |
| SchedulesV2.rows[].group_id | string | — | One nonempty compatible presentation group per native row. / Same group consumed by labels/caption. |
| SchedulesV2.rows[].native_asset | string | — | One nonempty compatible presentation group per native row. / Exact shape recipe native TK asset. |
| SchedulesV2.rows[].attributes[].tag | string | — | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Exact ATTDEF tag in selected AssetV2. |
| SchedulesV2.rows[].attributes[].text | string | — | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Formatted literal from typed value; no sample ATT fallback. |
| SchedulesV2.rows[].attributes[].value_kind | string | — | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| SchedulesV2.rows[].attributes[].raw_number | double? | unit field | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Unrounded numeric value when scalar; text/pair=null. |
| SchedulesV2.rows[].attributes[].unit | string | — | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| SchedulesV2.rows[].attributes[].source_ids[] | string[] | — | One nonempty compatible presentation group per native row. / SH/D/legs/DAI/SL/TDAI/KL according to exact tags, 05B-F04/F05/F20/F44. / Group/input/rule values that produced text. |
| SchedulesV2.rows[].glyph_hash | string | — | One nonempty compatible presentation group per native row. / Pass-through approved fixed native glyph hash. |
| SchedulesV2.rows[].insertion.x_mm | double | mm | One nonempty compatible presentation group per native row. / S8 measured row layout point, 05B-F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.rows[].insertion.y_mm | double | mm | One nonempty compatible presentation group per native row. / S8 measured row layout point, 05B-F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.rows[].scale | double | dimensionless | One nonempty compatible presentation group per native row. / Native baseline scale view.S/100 where sourced, 05B-F14/F15. |
| SchedulesV2.rows[].ink.min_x_mm | double | mm | One nonempty compatible presentation group per native row. / Actual row ink including glyph/basepoint/text, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.rows[].ink.min_y_mm | double | mm | One nonempty compatible presentation group per native row. / Actual row ink including glyph/basepoint/text, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.rows[].ink.max_x_mm | double | mm | One nonempty compatible presentation group per native row. / Actual row ink including glyph/basepoint/text, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.rows[].ink.max_y_mm | double | mm | One nonempty compatible presentation group per native row. / Actual row ink including glyph/basepoint/text, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.rows[].view_id | string | — | One nonempty compatible presentation group per native row. / ENCODE(manhole_id,Schedule,physical owner bucket); sheet includes row only in this view. |
| SchedulesV2.captions[].group_id | string | — | Separate bending drawing output, group metadata shared. / Same registry group. |
| SchedulesV2.captions[].glyph_asset | string | — | Separate bending drawing output, group metadata shared. / Exact fixed topology glyph from recipe, not physical-sized bar. |
| SchedulesV2.captions[].text | string | — | Separate bending drawing output, group metadata shared. / Mark-DØ,L=... or Ltb=... per approved length display policy. |
| SchedulesV2.captions[].role_values[].role | string | — | Separate bending drawing output, group metadata shared. / Mean/fixed named segment dimensions, no collector length. / Named run/bend/leg role, ordered where topology requires. |
| SchedulesV2.captions[].role_values[].value_mm | double | mm | Separate bending drawing output, group metadata shared. / Mean/fixed named segment dimensions, no collector length. / Value from approved shape expression/path, not glyph measurement. |
| SchedulesV2.captions[].role_values[].source_ids[] | string[] | — | Separate bending drawing output, group metadata shared. / Mean/fixed named segment dimensions, no collector length. / Expression inputs/shape rule sources. |
| SchedulesV2.captions[].underline_start.x_mm | double | mm | Separate bending drawing output, group metadata shared. / Caption actual ink min X at approved paper baseline. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].underline_start.y_mm | double | mm | Separate bending drawing output, group metadata shared. / Caption actual ink min X at approved paper baseline. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].underline_end.x_mm | double | mm | Separate bending drawing output, group metadata shared. / Caption actual ink max X, 05B-F42. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].underline_end.y_mm | double | mm | Separate bending drawing output, group metadata shared. / Caption actual ink max X, 05B-F42. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].ink.min_x_mm | double | mm | Separate bending drawing output, group metadata shared. / Glyph+dimensions+caption+underline envelope. / min của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.captions[].ink.min_y_mm | double | mm | Separate bending drawing output, group metadata shared. / Glyph+dimensions+caption+underline envelope. / min của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.captions[].ink.max_x_mm | double | mm | Separate bending drawing output, group metadata shared. / Glyph+dimensions+caption+underline envelope. / max của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.captions[].ink.max_y_mm | double | mm | Separate bending drawing output, group metadata shared. / Glyph+dimensions+caption+underline envelope. / max của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.captions[].view_id | string | — | Separate bending drawing output, group metadata shared. / ENCODE(manhole_id,Bending,owner bucket); not inferred from array position. |
| SchedulesV2.captions[].insertion.x_mm | double | mm | Separate bending drawing output, group metadata shared. / Exact schematic insertion; fixed source glyph scale, F14/F17/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].insertion.y_mm | double | mm | Separate bending drawing output, group metadata shared. / Exact schematic insertion; fixed source glyph scale, F14/F17/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.captions[].asset_scale | double | dimensionless | Separate bending drawing output, group metadata shared. / Source baseline scale S/100; never Li/source glyph dimension. |
| SchedulesV2.summaries[].id | string | — | Owner bucket raw quantities, no view duplication. / ENCODE(manhole_id,summary_scope), 05B-F46. |
| SchedulesV2.summaries[].scope | string | — | Owner bucket raw quantities, no view duplication. / Body/Neck/Panel/Catch from approved NR14 owner bucket map. |
| SchedulesV2.summaries[].native_asset | string | — | Owner bucket raw quantities, no view duplication. / TK.THKL/TK.THKL.CT/approved summary asset. |
| SchedulesV2.summaries[].attributes[].tag | string | — | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / Exact ATTDEF tag in selected AssetV2. |
| SchedulesV2.summaries[].attributes[].text | string | — | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / Formatted literal from typed value; no sample ATT fallback. |
| SchedulesV2.summaries[].attributes[].value_kind | string | — | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| SchedulesV2.summaries[].attributes[].raw_number | double? | unit field | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / Unrounded numeric value when scalar; text/pair=null. |
| SchedulesV2.summaries[].attributes[].unit | string | — | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| SchedulesV2.summaries[].attributes[].source_ids[] | string[] | — | Owner bucket raw quantities, no view duplication. / Exact existing tags, sums raw values not formatted cells. / Group/input/rule values that produced text. |
| SchedulesV2.summaries[].mass_small_kg | double | kg | Owner bucket raw quantities, no view duplication. / Sum raw masses for diameter<=10, 05B-F19/F20, native headers source. |
| SchedulesV2.summaries[].mass_large_kg | double | kg | Owner bucket raw quantities, no view duplication. / Sum raw masses for diameter>10, 05B-F19/F20. |
| SchedulesV2.summaries[].view_id | string | — | Owner bucket raw quantities, no view duplication. / Schedule view owning this summary, stable explicit identity. |
| SchedulesV2.summaries[].insertion.x_mm | double | mm | Owner bucket raw quantities, no view duplication. / S8 actual summary insertion after rows, F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.summaries[].insertion.y_mm | double | mm | Owner bucket raw quantities, no view duplication. / S8 actual summary insertion after rows, F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.summaries[].asset_scale | double | dimensionless | Owner bucket raw quantities, no view duplication. / Native baseline scale S/100, F14/F15. |
| SchedulesV2.summaries[].ink.min_x_mm | double | mm | Owner bucket raw quantities, no view duplication. / Measured summary glyph+ATTRIB ink, F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.summaries[].ink.min_y_mm | double | mm | Owner bucket raw quantities, no view duplication. / Measured summary glyph+ATTRIB ink, F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.summaries[].ink.max_x_mm | double | mm | Owner bucket raw quantities, no view duplication. / Measured summary glyph+ATTRIB ink, F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.summaries[].ink.max_y_mm | double | mm | Owner bucket raw quantities, no view duplication. / Measured summary glyph+ATTRIB ink, F40/F42. / max của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.ink[].view_id | string | — | Native TK/bending view bounds measured before pagination. / View ID being measured. |
| SchedulesV2.ink[].bounds.min_x_mm | double | mm | Native TK/bending view bounds measured before pagination. / Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.ink[].bounds.min_y_mm | double | mm | Native TK/bending view bounds measured before pagination. / Union all relevant primitive ink envelopes, 05B-F42. / min của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.ink[].bounds.max_x_mm | double | mm | Native TK/bending view bounds measured before pagination. / Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.ink[].bounds.max_y_mm | double | mm | Native TK/bending view bounds measured before pagination. / Union all relevant primitive ink envelopes, 05B-F42. / max của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.headings[].id | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / ENCODE(view_id,heading kind,group_id), 05B-F46. |
| SchedulesV2.headings[].view_id | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Exact Schedule/Bending view ID. |
| SchedulesV2.headings[].group_id | string? | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / First actual group for owner heading; fixed table header=null. |
| SchedulesV2.headings[].kind | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / OwnerHeading/TableHeader/BendingTitle. |
| SchedulesV2.headings[].native_asset | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TK.Tenluoi/TK.Tieude/TieudeMC exact source names. |
| SchedulesV2.headings[].attributes[].tag | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Exact ATTDEF tag in selected AssetV2. |
| SchedulesV2.headings[].attributes[].text | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Formatted literal from typed value; no sample ATT fallback. |
| SchedulesV2.headings[].attributes[].value_kind | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| SchedulesV2.headings[].attributes[].raw_number | double? | unit field | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Unrounded numeric value when scalar; text/pair=null. |
| SchedulesV2.headings[].attributes[].unit | string | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| SchedulesV2.headings[].attributes[].source_ids[] | string[] | — | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Group/input/rule values that produced text. |
| SchedulesV2.headings[].insertion.x_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Source-backed heading offset relative first row or bending origin, 05B-F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.headings[].insertion.y_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Source-backed heading offset relative first row or bending origin, 05B-F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SchedulesV2.headings[].asset_scale | double | dimensionless | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / S/100 native annotation baseline, 05B-F14. |
| SchedulesV2.headings[].ink.min_x_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.headings[].ink.min_y_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| SchedulesV2.headings[].ink.max_x_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| SchedulesV2.headings[].ink.max_y_mm | double | mm | Owner heading anchored with first row, fixed table header and bending group titles; no duplicated free-floating label. / Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / max của ink/curve Y sau transform; 05B-F42. |

### PlacementV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| PlacementV2.view_id | string | — | Actual view/table/bending role ID. |
| PlacementV2.frame_id | string | — | Target sheet frame ID. |
| PlacementV2.translation.x_mm | double | mm | Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| PlacementV2.translation.y_mm | double | mm | Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| PlacementV2.rotation_rad | double | rad | Recipe rotation only, no product deformation. |
| PlacementV2.scale_denominator | double | 1:S | Same view requested/approved Auto scale. |
| PlacementV2.ink_after.min_x_mm | double | mm | Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve X sau transform; 05B-F42. |
| PlacementV2.ink_after.min_y_mm | double | mm | Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve Y sau transform; 05B-F42. |
| PlacementV2.ink_after.max_x_mm | double | mm | Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve X sau transform; 05B-F42. |
| PlacementV2.ink_after.max_y_mm | double | mm | Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve Y sau transform; 05B-F42. |

### SheetFrameV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SheetFrameV2.id | string | — | ENCODE(manhole_id,sheet_role,page_index), 05B-F46. |
| SheetFrameV2.manhole_id | string | — | Pass-through context.manhole_id. |
| SheetFrameV2.sheet_role | string | — | Structure/BaseWalls12/Walls34Roof/NeckAndSchedule/NeckCatch/ScheduleOnly/CoverConstruction/Bending. |
| SheetFrameV2.page_index | int | page | One-based page index within semantic sheet role. |
| SheetFrameV2.scale_denominator | double | 1:S | Explicit body/panel denominator; mixed scale uses separate page in v2. |
| SheetFrameV2.frame_asset | string | — | Exact Khung selected source. |
| SheetFrameV2.window.min_x_mm | double | mm | Actual native plot frame window after placement, 05B-F41. / min của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.window.min_y_mm | double | mm | Actual native plot frame window after placement, 05B-F41. / min của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.window.max_x_mm | double | mm | Actual native plot frame window after placement, 05B-F41. / max của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.window.max_y_mm | double | mm | Actual native plot frame window after placement, 05B-F41. / max của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.content.min_x_mm | double | mm | Approved printable/content region in that frame, 05B-F41. / min của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.content.min_y_mm | double | mm | Approved printable/content region in that frame, 05B-F41. / min của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.content.max_x_mm | double | mm | Approved printable/content region in that frame, 05B-F41. / max của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.content.max_y_mm | double | mm | Approved printable/content region in that frame, 05B-F41. / max của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.placements[].view_id | string | — | Each required view exactly once unless recipe says referenced elsewhere. / Actual view/table/bending role ID. |
| SheetFrameV2.placements[].frame_id | string | — | Each required view exactly once unless recipe says referenced elsewhere. / Target sheet frame ID. |
| SheetFrameV2.placements[].translation.x_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SheetFrameV2.placements[].translation.y_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SheetFrameV2.placements[].rotation_rad | double | rad | Each required view exactly once unless recipe says referenced elsewhere. / Recipe rotation only, no product deformation. |
| SheetFrameV2.placements[].scale_denominator | double | 1:S | Each required view exactly once unless recipe says referenced elsewhere. / Same view requested/approved Auto scale. |
| SheetFrameV2.placements[].ink_after.min_x_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.placements[].ink_after.min_y_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.placements[].ink_after.max_x_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve X sau transform; 05B-F42. |
| SheetFrameV2.placements[].ink_after.max_y_mm | double | mm | Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve Y sau transform; 05B-F42. |
| SheetFrameV2.origin.x_mm | double | mm | Native frame insertion/basepoint transform; frame/page packing places distinct full windows with sourced page gap. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SheetFrameV2.origin.y_mm | double | mm | Native frame insertion/basepoint transform; frame/page packing places distinct full windows with sourced page gap. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |

### SheetV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SheetV2.frames[].id | string | — | Complete FrameRegistry from S9. / ENCODE(manhole_id,sheet_role,page_index), 05B-F46. |
| SheetV2.frames[].manhole_id | string | — | Complete FrameRegistry from S9. / Pass-through context.manhole_id. |
| SheetV2.frames[].sheet_role | string | — | Complete FrameRegistry from S9. / Structure/BaseWalls12/Walls34Roof/NeckAndSchedule/NeckCatch/ScheduleOnly/CoverConstruction/Bending. |
| SheetV2.frames[].page_index | int | page | Complete FrameRegistry from S9. / One-based page index within semantic sheet role. |
| SheetV2.frames[].scale_denominator | double | 1:S | Complete FrameRegistry from S9. / Explicit body/panel denominator; mixed scale uses separate page in v2. |
| SheetV2.frames[].frame_asset | string | — | Complete FrameRegistry from S9. / Exact Khung selected source. |
| SheetV2.frames[].window.min_x_mm | double | mm | Complete FrameRegistry from S9. / Actual native plot frame window after placement, 05B-F41. / min của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].window.min_y_mm | double | mm | Complete FrameRegistry from S9. / Actual native plot frame window after placement, 05B-F41. / min của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].window.max_x_mm | double | mm | Complete FrameRegistry from S9. / Actual native plot frame window after placement, 05B-F41. / max của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].window.max_y_mm | double | mm | Complete FrameRegistry from S9. / Actual native plot frame window after placement, 05B-F41. / max của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].content.min_x_mm | double | mm | Complete FrameRegistry from S9. / Approved printable/content region in that frame, 05B-F41. / min của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].content.min_y_mm | double | mm | Complete FrameRegistry from S9. / Approved printable/content region in that frame, 05B-F41. / min của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].content.max_x_mm | double | mm | Complete FrameRegistry from S9. / Approved printable/content region in that frame, 05B-F41. / max của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].content.max_y_mm | double | mm | Complete FrameRegistry from S9. / Approved printable/content region in that frame, 05B-F41. / max của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].placements[].view_id | string | — | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Actual view/table/bending role ID. |
| SheetV2.frames[].placements[].frame_id | string | — | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Target sheet frame ID. |
| SheetV2.frames[].placements[].translation.x_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SheetV2.frames[].placements[].translation.y_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Derived role placement so translated ink fits content box, 05B-F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SheetV2.frames[].placements[].rotation_rad | double | rad | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Recipe rotation only, no product deformation. |
| SheetV2.frames[].placements[].scale_denominator | double | 1:S | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Same view requested/approved Auto scale. |
| SheetV2.frames[].placements[].ink_after.min_x_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].placements[].ink_after.min_y_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / min của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].placements[].ink_after.max_x_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve X sau transform; 05B-F42. |
| SheetV2.frames[].placements[].ink_after.max_y_mm | double | mm | Complete FrameRegistry from S9. / Each required view exactly once unless recipe says referenced elsewhere. / Apply placement to full view bundle bounds, 05B-F41/F42. / max của ink/curve Y sau transform; 05B-F42. |
| SheetV2.frames[].origin.x_mm | double | mm | Complete FrameRegistry from S9. / Native frame insertion/basepoint transform; frame/page packing places distinct full windows with sourced page gap. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| SheetV2.frames[].origin.y_mm | double | mm | Complete FrameRegistry from S9. / Native frame insertion/basepoint transform; frame/page packing places distinct full windows with sourced page gap. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| SheetV2.geometry_hash_before | string | — | Pass-through rebar.fabrication_geometry_hash (or geometry.physical_hash in explicit GeometryOnly scope). |
| SheetV2.rebar_hash_before | string | — | Pass-through rebar.physical_hash before layout. |
| SheetV2.sheet_hash | string | — | Hash frame/placement/content IDs/ink/scales, 05B-F46. |

### HandleV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| HandleV2.semantic_id | string | — | Intent source ID from geometry/label/TK/frame. |
| HandleV2.entity_handle | string | — | Actual native appended/reopened handle. |
| HandleV2.block_name | string? | — | Actual block name when INSERT; geometry=null. |
| HandleV2.attribute_tags[] | string[] | — | Native ATTRIB tag list; nonblock empty. |
| HandleV2.attribute_values[] | string[] | — | Readback values aligned with tags. |
| HandleV2.owner_manhole_id | string | — | Generation ownership metadata, not inferred from label text. |

### NativeV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| NativeV2.saved_dwg_path | string | — | Actual saved output copy path, never source workbook/DWG input. |
| NativeV2.saved_dwg_sha256 | string | — | SHA256 actual saved DWG bytes, 05B-F46. |
| NativeV2.handles[].semantic_id | string | — | Readback intent-to-native map after reopen. / Intent source ID from geometry/label/TK/frame. |
| NativeV2.handles[].entity_handle | string | — | Readback intent-to-native map after reopen. / Actual native appended/reopened handle. |
| NativeV2.handles[].block_name | string? | — | Readback intent-to-native map after reopen. / Actual block name when INSERT; geometry=null. |
| NativeV2.handles[].attribute_tags[] | string[] | — | Readback intent-to-native map after reopen. / Native ATTRIB tag list; nonblock empty. |
| NativeV2.handles[].attribute_values[] | string[] | — | Readback intent-to-native map after reopen. / Readback values aligned with tags. |
| NativeV2.handles[].owner_manhole_id | string | — | Readback intent-to-native map after reopen. / Generation ownership metadata, not inferred from label text. |
| NativeV2.normalized_geometry_hash | string | — | Hash actual normalized 2D native geometry/projection, undo sheet transform and match S6 intent. Does not reconstruct 3D concrete from a schematic. |
| NativeV2.normalized_rebar_hash | string | — | Hash actual normalized projected steel with source occurrence IDs and stored fabrication provenance. Physical length comes from S4, never measured from a TK glyph. |
| NativeV2.repository_version | string | — | Actual NOD/XData generation schema version. |
| NativeV2.write_status | string | — | Committed/RolledBack/Failed, no fake Committed on exception. |
| NativeV2.readback_status | string | — | Passed/Failed/NotRun based on actual saved/reopened entities. |
| NativeV2.unchanged_manhole_ids[] | string[] | — | Non-target IDs measured unchanged for Update. |
| NativeV2.unchanged_before_hashes[] | string[] | — | Normalized before manifests aligned with IDs. |
| NativeV2.unchanged_after_hashes[] | string[] | — | Actual after manifests, must equal before when success. |
| NativeV2.domain_geometry_hash | string | — | Pass-through rebar.fabrication_geometry_hash; trace to domain data only, not native 3D proof. |
| NativeV2.expected_projection_hash | string | — | Hash S6/S7/S8 intents normalized by S9 placements; compare actual native geometry and values individually before hash. |
| NativeV2.commit_state | string | — | NotCommitted/Committed/Saved/Reopened; postcommit failure cannot be called rolled back. |

### AcceptanceCheckV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AcceptanceCheckV2.name | string | — | Concrete numerical/semantic/visual check name. |
| AcceptanceCheckV2.subject_id | string | — | Actual field/member/handle/frame/region being checked. |
| AcceptanceCheckV2.expected | double? | unit field | Numerical expected from EffectiveInput/rules; visual/text=null. |
| AcceptanceCheckV2.actual | double? | unit field | Actual native/PDF measurement; cannot be guessed from requested value. |
| AcceptanceCheckV2.expected_text | string? | — | Expected asset/tag/role/text for exact checks. |
| AcceptanceCheckV2.actual_text | string? | — | Native/PDF actual exact value. |
| AcceptanceCheckV2.unit | string | — | mm,m,kg,paper_mm,pdf_point,count,none. |
| AcceptanceCheckV2.tolerance_rule_id | string? | — | Approved NR18 numeric/visual tolerance; exact categorical checks=null. |
| AcceptanceCheckV2.source_ids[] | string[] | — | Input/rule/reference sources supporting expected. |
| AcceptanceCheckV2.native_handles[] | string[] | — | Actual subjects in saved output DWG. |
| AcceptanceCheckV2.evidence_paths[] | string[] | — | Actual crop/PDF/readback/ledger paths with artifact hashes in run manifest. |
| AcceptanceCheckV2.verdict | string | — | Passed/Failed/Blocked/NotRun/NA; NA requires reason. |
| AcceptanceCheckV2.reason | string? | — | Missing approval/evidence/NA or numerical mismatch, Passed may null. |

### RowVerdictV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| RowVerdictV2.job_id | string | — | Pass-through selected job identity. |
| RowVerdictV2.row_index | int | row | Exact selected row. |
| RowVerdictV2.manhole_id | string | — | Exact target ID. |
| RowVerdictV2.status | string | — | Complete/Partial/Blocked/Failed/Cancelled/NotRun, no silent missing row. |
| RowVerdictV2.frame_ids[] | string[] | — | Actual generated FrameRegistry IDs for row. |
| RowVerdictV2.checks[].name | string | — | All required checks, actual evidence; no test counts as acceptance. / Concrete numerical/semantic/visual check name. |
| RowVerdictV2.checks[].subject_id | string | — | All required checks, actual evidence; no test counts as acceptance. / Actual field/member/handle/frame/region being checked. |
| RowVerdictV2.checks[].expected | double? | unit field | All required checks, actual evidence; no test counts as acceptance. / Numerical expected from EffectiveInput/rules; visual/text=null. |
| RowVerdictV2.checks[].actual | double? | unit field | All required checks, actual evidence; no test counts as acceptance. / Actual native/PDF measurement; cannot be guessed from requested value. |
| RowVerdictV2.checks[].expected_text | string? | — | All required checks, actual evidence; no test counts as acceptance. / Expected asset/tag/role/text for exact checks. |
| RowVerdictV2.checks[].actual_text | string? | — | All required checks, actual evidence; no test counts as acceptance. / Native/PDF actual exact value. |
| RowVerdictV2.checks[].unit | string | — | All required checks, actual evidence; no test counts as acceptance. / mm,m,kg,paper_mm,pdf_point,count,none. |
| RowVerdictV2.checks[].tolerance_rule_id | string? | — | All required checks, actual evidence; no test counts as acceptance. / Approved NR18 numeric/visual tolerance; exact categorical checks=null. |
| RowVerdictV2.checks[].source_ids[] | string[] | — | All required checks, actual evidence; no test counts as acceptance. / Input/rule/reference sources supporting expected. |
| RowVerdictV2.checks[].native_handles[] | string[] | — | All required checks, actual evidence; no test counts as acceptance. / Actual subjects in saved output DWG. |
| RowVerdictV2.checks[].evidence_paths[] | string[] | — | All required checks, actual evidence; no test counts as acceptance. / Actual crop/PDF/readback/ledger paths with artifact hashes in run manifest. |
| RowVerdictV2.checks[].verdict | string | — | All required checks, actual evidence; no test counts as acceptance. / Passed/Failed/Blocked/NotRun/NA; NA requires reason. |
| RowVerdictV2.checks[].reason | string? | — | All required checks, actual evidence; no test counts as acceptance. / Missing approval/evidence/NA or numerical mismatch, Passed may null. |
| RowVerdictV2.diagnostics[].code | string | — | Row-specific diagnostics including unsuccessful stage. / Mã RAISE được ghi trực tiếp trong pseudocode. |
| RowVerdictV2.diagnostics[].stage | string | — | Row-specific diagnostics including unsuccessful stage. / S0..S11 hoặc PRECHECK. |
| RowVerdictV2.diagnostics[].manhole_id | string? | — | Row-specific diagnostics including unsuccessful stage. / Pass-through context.manhole_id nếu đã có. |
| RowVerdictV2.diagnostics[].subject_id | string? | — | Row-specific diagnostics including unsuccessful stage. / Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| RowVerdictV2.diagnostics[].field_paths[] | string[] | — | Row-specific diagnostics including unsuccessful stage. / Paths cụ thể đã kiểm, không một thông báo chung. |
| RowVerdictV2.diagnostics[].expected_text | string | — | Row-specific diagnostics including unsuccessful stage. / Literal invariant/expected field value từ phép kiểm. |
| RowVerdictV2.diagnostics[].actual_text | string | — | Row-specific diagnostics including unsuccessful stage. / Serialize actual values bị lỗi, culture invariant. |
| RowVerdictV2.diagnostics[].source_ids[] | string[] | — | Row-specific diagnostics including unsuccessful stage. / IDs của các input gây lỗi, tra context.sources[]. |
| RowVerdictV2.diagnostics[].needsrule_ids[] | string[] | — | Row-specific diagnostics including unsuccessful stage. / NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| RowVerdictV2.diagnostics[].severity | string | — | Row-specific diagnostics including unsuccessful stage. / Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |

### PlotPageV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| PlotPageV2.frame_id | string | — | Registered source frame window ID. |
| PlotPageV2.pdf_path | string | — | Actual generated vector PDF file path. |
| PlotPageV2.pdf_sha256 | string | — | Hash actual PDF bytes, 05B-F46. |
| PlotPageV2.page_index | int | page | Actual PDF page index one-based. |
| PlotPageV2.host_session_id | string | — | Actual one healthy plot host session identity. |
| PlotPageV2.pstylemode | int | — | Read actual native PSTYLEMODE, choose compatible CTB/STB explicitly. |
| PlotPageV2.plot_profile_id | string | — | Approved selected profile/hash, no default acad.ctb unless approved. |
| PlotPageV2.nominal_scale | double | 1:S | Frame requested scale. |
| PlotPageV2.actual_scale | double | 1:S | Scale measured from native window→PDF mapping, 05B-F48. |
| PlotPageV2.transcript_path | string | — | Actual host/plot transcript saved, not fabricated success text. |
| PlotPageV2.visual_status | string | — | Passed/Failed/NotRun, actual reviewed regions required for Passed. |
| PlotPageV2.reviewer | string? | — | Actual reviewer identity; null for NotRun. |
| PlotPageV2.reviewed_regions[] | string[] | — | Actual reviewed named regions; complete required coverage for visual Passed. |

### BatchV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| BatchV2.rows[].job_id | string | — | Every selected target has exactly one final receipt, including failures. / Pass-through selected job identity. |
| BatchV2.rows[].row_index | int | row | Every selected target has exactly one final receipt, including failures. / Exact selected row. |
| BatchV2.rows[].manhole_id | string | — | Every selected target has exactly one final receipt, including failures. / Exact target ID. |
| BatchV2.rows[].status | string | — | Every selected target has exactly one final receipt, including failures. / Complete/Partial/Blocked/Failed/Cancelled/NotRun, no silent missing row. |
| BatchV2.rows[].frame_ids[] | string[] | — | Every selected target has exactly one final receipt, including failures. / Actual generated FrameRegistry IDs for row. |
| BatchV2.rows[].checks[].name | string | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Concrete numerical/semantic/visual check name. |
| BatchV2.rows[].checks[].subject_id | string | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Actual field/member/handle/frame/region being checked. |
| BatchV2.rows[].checks[].expected | double? | unit field | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Numerical expected from EffectiveInput/rules; visual/text=null. |
| BatchV2.rows[].checks[].actual | double? | unit field | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Actual native/PDF measurement; cannot be guessed from requested value. |
| BatchV2.rows[].checks[].expected_text | string? | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Expected asset/tag/role/text for exact checks. |
| BatchV2.rows[].checks[].actual_text | string? | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Native/PDF actual exact value. |
| BatchV2.rows[].checks[].unit | string | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / mm,m,kg,paper_mm,pdf_point,count,none. |
| BatchV2.rows[].checks[].tolerance_rule_id | string? | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Approved NR18 numeric/visual tolerance; exact categorical checks=null. |
| BatchV2.rows[].checks[].source_ids[] | string[] | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Input/rule/reference sources supporting expected. |
| BatchV2.rows[].checks[].native_handles[] | string[] | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Actual subjects in saved output DWG. |
| BatchV2.rows[].checks[].evidence_paths[] | string[] | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Actual crop/PDF/readback/ledger paths with artifact hashes in run manifest. |
| BatchV2.rows[].checks[].verdict | string | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Passed/Failed/Blocked/NotRun/NA; NA requires reason. |
| BatchV2.rows[].checks[].reason | string? | — | Every selected target has exactly one final receipt, including failures. / All required checks, actual evidence; no test counts as acceptance. / Missing approval/evidence/NA or numerical mismatch, Passed may null. |
| BatchV2.rows[].diagnostics[].code | string | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Mã RAISE được ghi trực tiếp trong pseudocode. |
| BatchV2.rows[].diagnostics[].stage | string | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / S0..S11 hoặc PRECHECK. |
| BatchV2.rows[].diagnostics[].manhole_id | string? | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Pass-through context.manhole_id nếu đã có. |
| BatchV2.rows[].diagnostics[].subject_id | string? | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| BatchV2.rows[].diagnostics[].field_paths[] | string[] | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Paths cụ thể đã kiểm, không một thông báo chung. |
| BatchV2.rows[].diagnostics[].expected_text | string | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Literal invariant/expected field value từ phép kiểm. |
| BatchV2.rows[].diagnostics[].actual_text | string | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Serialize actual values bị lỗi, culture invariant. |
| BatchV2.rows[].diagnostics[].source_ids[] | string[] | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / IDs của các input gây lỗi, tra context.sources[]. |
| BatchV2.rows[].diagnostics[].needsrule_ids[] | string[] | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| BatchV2.rows[].diagnostics[].severity | string | — | Every selected target has exactly one final receipt, including failures. / Row-specific diagnostics including unsuccessful stage. / Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |
| BatchV2.pages[].frame_id | string | — | Actual plotted frame/page ledger. / Registered source frame window ID. |
| BatchV2.pages[].pdf_path | string | — | Actual plotted frame/page ledger. / Actual generated vector PDF file path. |
| BatchV2.pages[].pdf_sha256 | string | — | Actual plotted frame/page ledger. / Hash actual PDF bytes, 05B-F46. |
| BatchV2.pages[].page_index | int | page | Actual plotted frame/page ledger. / Actual PDF page index one-based. |
| BatchV2.pages[].host_session_id | string | — | Actual plotted frame/page ledger. / Actual one healthy plot host session identity. |
| BatchV2.pages[].pstylemode | int | — | Actual plotted frame/page ledger. / Read actual native PSTYLEMODE, choose compatible CTB/STB explicitly. |
| BatchV2.pages[].plot_profile_id | string | — | Actual plotted frame/page ledger. / Approved selected profile/hash, no default acad.ctb unless approved. |
| BatchV2.pages[].nominal_scale | double | 1:S | Actual plotted frame/page ledger. / Frame requested scale. |
| BatchV2.pages[].actual_scale | double | 1:S | Actual plotted frame/page ledger. / Scale measured from native window→PDF mapping, 05B-F48. |
| BatchV2.pages[].transcript_path | string | — | Actual plotted frame/page ledger. / Actual host/plot transcript saved, not fabricated success text. |
| BatchV2.pages[].visual_status | string | — | Actual plotted frame/page ledger. / Passed/Failed/NotRun, actual reviewed regions required for Passed. |
| BatchV2.pages[].reviewer | string? | — | Actual plotted frame/page ledger. / Actual reviewer identity; null for NotRun. |
| BatchV2.pages[].reviewed_regions[] | string[] | — | Actual plotted frame/page ledger. / Actual reviewed named regions; complete required coverage for visual Passed. |
| BatchV2.status | string | — | Complete iff every selected valid row/all required checks Passed; invalid rows keep Failed. |
| BatchV2.delivery_manifest_path | string | — | Actual DWG/PDF/readback/receipt artifact index for this run. |
| BatchV2.final_dwg_path | string? | — | Actual aggregate output path when any write committed; null if no DWG exists. |
| BatchV2.final_dwg_sha256 | string? | — | Hash final actual aggregate saved bytes; not assumed equal earlier per-row frozen snapshot hashes. |

### ParsedFieldV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ParsedFieldV2.field_key | string | — | Exact field key trong bảng 206 fields; catalog keys có row_index. |
| ParsedFieldV2.sheet | string | — | Pass-through raw source sheet. |
| ParsedFieldV2.address | string | — | Pass-through raw value cell address. |
| ParsedFieldV2.row_index | int? | row | Catalog/main row nếu applicable, config scalar null. |
| ParsedFieldV2.active | bool | — | Predicate explicit trong S2 INPUT_ACTIVITY; không dùng active=false để che Unsupported. |
| ParsedFieldV2.inactive_reason | string? | — | Tên predicate và raw source gây inactive; active=true null. |
| ParsedFieldV2.kind | string | — | Number/Integer/Text/Bool/Tuple/Mesh/Anchor/Counted/TokenSet, exhaustive PARSE_FIELD. |
| ParsedFieldV2.number | double? | unit field | Number slot finite; kind khác Number null. |
| ParsedFieldV2.integer | int? | unit field | Integer slot checked exact integral, không truncate. |
| ParsedFieldV2.text | string? | — | Literal/config enum/raw catalog code; không fuzzy. |
| ParsedFieldV2.flag | bool? | — | Có/Không parsed bool; unsupported literal Block. |
| ParsedFieldV2.tuple_mm[] | double[] | mm | Ordered values AxB/AxBxC, arity và role order có bảng S2. |
| ParsedFieldV2.diameter_mm | int? | mm | Mesh/Anchor/Counted parsed diameter >0. |
| ParsedFieldV2.pitch_mm | double? | mm | Mesh/Anchor parsed pitch>0. |
| ParsedFieldV2.length_mm | double? | mm | Anchor parsed length>0, other=null. |
| ParsedFieldV2.count | int? | count | Counted bar number; off allowed only explicit feature predicate. |
| ParsedFieldV2.tokens[] | string[] | — | Exact enum set e.g angle positions1/2/3, no duplicates. |
| ParsedFieldV2.unit | string | — | Unit của scalar numeric field theo 206 input table. |
| ParsedFieldV2.source_ids[] | string[] | — | Raw value/note refs and parse conversion provenance. |

### SurfaceConstraintV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SurfaceConstraintV2.surface_a | string | — | Datum ID vế trái, exact SurfaceId. |
| SurfaceConstraintV2.surface_b | string | — | Datum ID vế phải, exact SurfaceId. |
| SurfaceConstraintV2.delta_mm | double | mm | Equation z(a)=z(b)+delta from input/contact evidence, 05B-F28. |
| SurfaceConstraintV2.source_ids[] | string[] | — | Native product contact/owner/input evidence, không outside bbox. |

### ProductContractV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProductContractV2.id | string | — | Versioned selected product/PanelAssemblyContract ID. |
| ProductContractV2.code | string | — | Exact catalog product code. |
| ProductContractV2.plan_asset | string | — | Exact MB name verified S0. |
| ProductContractV2.section_aa_asset | string | — | Exact AA native name verified S0. |
| ProductContractV2.section_bb_asset | string | — | Exact BB native name verified S0. |
| ProductContractV2.aperture_x_mm | double | mm | Measured/approved clear aperture X, independent of outside bounds. |
| ProductContractV2.aperture_y_mm | double | mm | Measured/approved clear aperture Y. |
| ProductContractV2.rotation_rad | double | rad | Explicit source registration rotation; approved support set checked S2. |
| ProductContractV2.aperture[].id | string | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / ENCODE(owner,loop_role,index), 05B-F46. |
| ProductContractV2.aperture[].role | string | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| ProductContractV2.aperture[].curves[].id | string | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| ProductContractV2.aperture[].curves[].kind | string | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| ProductContractV2.aperture[].curves[].start.x_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProductContractV2.aperture[].curves[].start.y_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProductContractV2.aperture[].curves[].start.z_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProductContractV2.aperture[].curves[].end.x_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProductContractV2.aperture[].curves[].end.y_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProductContractV2.aperture[].curves[].end.z_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProductContractV2.aperture[].curves[].center.x_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent ProductContractV2.aperture[].curves[].center |
| ProductContractV2.aperture[].curves[].center.y_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent ProductContractV2.aperture[].curves[].center |
| ProductContractV2.aperture[].curves[].center.z_mm | double | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent ProductContractV2.aperture[].curves[].center |
| ProductContractV2.aperture[].curves[].normal.x | double | dimensionless | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent ProductContractV2.aperture[].curves[].normal |
| ProductContractV2.aperture[].curves[].normal.y | double | dimensionless | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent ProductContractV2.aperture[].curves[].normal |
| ProductContractV2.aperture[].curves[].normal.z | double | dimensionless | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent ProductContractV2.aperture[].curves[].normal |
| ProductContractV2.aperture[].curves[].radius_mm | double? | mm | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| ProductContractV2.aperture[].curves[].sweep_rad | double? | rad | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| ProductContractV2.aperture[].curves[].role | string | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| ProductContractV2.aperture[].closed | bool | — | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| ProductContractV2.aperture[].signed_area_mm2 | double | mm2 | Actual clear contour, preserves circle/rounded corners, not bbox substitution. / Analytic line/arc Green area, 05B-F37. |
| ProductContractV2.contact_equations[].surface_a | string | — | Named product/support datums, 05B-F28. / Datum ID vế trái, exact SurfaceId. |
| ProductContractV2.contact_equations[].surface_b | string | — | Named product/support datums, 05B-F28. / Datum ID vế phải, exact SurfaceId. |
| ProductContractV2.contact_equations[].delta_mm | double | mm | Named product/support datums, 05B-F28. / Equation z(a)=z(b)+delta from input/contact evidence, 05B-F28. |
| ProductContractV2.contact_equations[].source_ids[] | string[] | — | Named product/support datums, 05B-F28. / Native product contact/owner/input evidence, không outside bbox. |
| ProductContractV2.approved | bool | — | Approval/source evidence of product/contact contract, not asset existence. |
| ProductContractV2.source_ids[] | string[] | — | Native measurements and owner confirmation references. |
| ProductContractV2.placement_surface_id | string | — | Approved placement datum SurfaceId, e.g NeckTop or SeatPlane; not guessed. |
| ProductContractV2.registration_offset.x_mm | double | mm | Native measured/approved placement offset, not outside bbox center. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProductContractV2.registration_offset.y_mm | double | mm | Native measured/approved placement offset, not outside bbox center. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProductContractV2.registration_offset.z_mm | double | mm | Native measured/approved placement offset, not outside bbox center. / Tọa độ Z của point argument, 05B-F26/F27. |

### KerningV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| KerningV2.style_id | string | — | Same approved font/style key as glyphs. |
| KerningV2.left_codepoint | int | Unicode | First character of measured kerning pair. |
| KerningV2.right_codepoint | int | Unicode | Second character of measured kerning pair. |
| KerningV2.adjustment_native_mm | double | native mm | Measured advance adjustment from approved font provider. |

### LayerMetricV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| LayerMetricV2.layer_id | string | — | Exact native layer ID/name. |
| LayerMetricV2.semantic_role | string | — | Role binding from selected template manifest. |
| LayerMetricV2.stroke_paper_mm | double | paper mm | Native lineweight/approved plot style effective width; hairline0 only if source/profile says0. |
| LayerMetricV2.source_ids[] | string[] | — | Layer/plot policy evidence, NR18 when profile changes. |

### SolidPartV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| SolidPartV2.id | string | — | ENCODE(component_id,part_role,index), 05B-F46. |
| SolidPartV2.operation | string | — | Union/Subtract in ordered CSG list; void ownership prevents subtraction twice. |
| SolidPartV2.frame.id | string | — | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| SolidPartV2.frame.origin.x_mm | double | mm | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| SolidPartV2.frame.origin.y_mm | double | mm | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| SolidPartV2.frame.origin.z_mm | double | mm | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| SolidPartV2.frame.axis_u.x | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.axis_u.y | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.axis_u.z | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.axis_v.x | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.axis_v.y | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.axis_v.z | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.normal.x | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.normal.y | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.normal.z | double | dimensionless | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| SolidPartV2.frame.handedness | int | — | Extrusion plane world origin/U/V/normal, 05B-F26/F27. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| SolidPartV2.loops[].id | string | — | Plane section contours, analytic line/arc, no triangulated schematic length. / ENCODE(owner,loop_role,index), 05B-F46. |
| SolidPartV2.loops[].role | string | — | Plane section contours, analytic line/arc, no triangulated schematic length. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| SolidPartV2.loops[].curves[].id | string | — | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| SolidPartV2.loops[].curves[].kind | string | — | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| SolidPartV2.loops[].curves[].start.x_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| SolidPartV2.loops[].curves[].start.y_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| SolidPartV2.loops[].curves[].start.z_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| SolidPartV2.loops[].curves[].end.x_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| SolidPartV2.loops[].curves[].end.y_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| SolidPartV2.loops[].curves[].end.z_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| SolidPartV2.loops[].curves[].center.x_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent SolidPartV2.loops[].curves[].center |
| SolidPartV2.loops[].curves[].center.y_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent SolidPartV2.loops[].curves[].center |
| SolidPartV2.loops[].curves[].center.z_mm | double | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent SolidPartV2.loops[].curves[].center |
| SolidPartV2.loops[].curves[].normal.x | double | dimensionless | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent SolidPartV2.loops[].curves[].normal |
| SolidPartV2.loops[].curves[].normal.y | double | dimensionless | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent SolidPartV2.loops[].curves[].normal |
| SolidPartV2.loops[].curves[].normal.z | double | dimensionless | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent SolidPartV2.loops[].curves[].normal |
| SolidPartV2.loops[].curves[].radius_mm | double? | mm | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| SolidPartV2.loops[].curves[].sweep_rad | double? | rad | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| SolidPartV2.loops[].curves[].role | string | — | Plane section contours, analytic line/arc, no triangulated schematic length. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| SolidPartV2.loops[].closed | bool | — | Plane section contours, analytic line/arc, no triangulated schematic length. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| SolidPartV2.loops[].signed_area_mm2 | double | mm2 | Plane section contours, analytic line/arc, no triangulated schematic length. / Analytic line/arc Green area, 05B-F37. |
| SolidPartV2.depth_min_mm | double | mm | Extrusion coordinate along frame.normal, 05B-F27. |
| SolidPartV2.depth_max_mm | double | mm | Extrusion end coordinate; >depth_min for active part. |

### ProductPlacementV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProductPlacementV2.id | string | — | ENCODE(manhole_id,product contract id,placement role), 05B-F46. |
| ProductPlacementV2.contract_id | string | — | Exact AssetsV2.products[].id selected in S2. |
| ProductPlacementV2.anchor.x_mm | double | mm | Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ProductPlacementV2.anchor.y_mm | double | mm | Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ Y của point argument, 05B-F26/F27. |
| ProductPlacementV2.anchor.z_mm | double | mm | Neck center+approved registration offset; Z from placement SurfaceId, 05B-F26/F28. / Tọa độ Z của point argument, 05B-F26/F27. |
| ProductPlacementV2.rotation_rad | double | rad | Pass-through approved product registration rotation, not sheet mirror. |
| ProductPlacementV2.physical_scale | double | dimensionless | 1 exactly after native unit normalization; CONTRACT C01/05B-F25, not annotation scale. |
| ProductPlacementV2.plan_asset | string | — | Pass-through selected ProductContractV2.plan_asset. |
| ProductPlacementV2.section_aa_asset | string | — | Pass-through selected section AA asset. |
| ProductPlacementV2.section_bb_asset | string | — | Pass-through selected section BB asset. |
| ProductPlacementV2.source_ids[] | string[] | — | Product/aperture/contact/registration evidence. |

### AngleOccurrenceV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| AngleOccurrenceV2.id | string | — | One actual angle frame/member identity, no duplicate view accounting, 05B-F46. |
| AngleOccurrenceV2.host_id | string | — | Actual Neck/Panel host component ID. |
| AngleOccurrenceV2.panel_instance_id | string? | — | Physical panel instance when applicable, else null. |
| AngleOccurrenceV2.catalog_code | string | — | Exact CF.Data angle code, not fuzzy. |
| AngleOccurrenceV2.unit_mass_kg_per_m | double | kg/m | CF.Data C paired with exact B code. |
| AngleOccurrenceV2.path[].id | string | — | Actual angle accounting path from approved frame support rule, not glyph. / Stable segment ID from owner/path index, 05B-F46. |
| AngleOccurrenceV2.path[].kind | string | — | Actual angle accounting path from approved frame support rule, not glyph. / Line/Arc, exhaustive geometry helper. |
| AngleOccurrenceV2.path[].start.x_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AngleOccurrenceV2.path[].start.y_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| AngleOccurrenceV2.path[].start.z_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| AngleOccurrenceV2.path[].end.x_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| AngleOccurrenceV2.path[].end.y_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| AngleOccurrenceV2.path[].end.z_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| AngleOccurrenceV2.path[].center.x_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent AngleOccurrenceV2.path[].center |
| AngleOccurrenceV2.path[].center.y_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent AngleOccurrenceV2.path[].center |
| AngleOccurrenceV2.path[].center.z_mm | double | mm | Actual angle accounting path from approved frame support rule, not glyph. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent AngleOccurrenceV2.path[].center |
| AngleOccurrenceV2.path[].normal.x | double | dimensionless | Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent AngleOccurrenceV2.path[].normal |
| AngleOccurrenceV2.path[].normal.y | double | dimensionless | Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent AngleOccurrenceV2.path[].normal |
| AngleOccurrenceV2.path[].normal.z | double | dimensionless | Actual angle accounting path from approved frame support rule, not glyph. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent AngleOccurrenceV2.path[].normal |
| AngleOccurrenceV2.path[].radius_mm | double? | mm | Actual angle accounting path from approved frame support rule, not glyph. / Arc radius>0, Line=null. |
| AngleOccurrenceV2.path[].sweep_rad | double? | rad | Actual angle accounting path from approved frame support rule, not glyph. / Signed arc sweep, Line=null; 05B-F35. |
| AngleOccurrenceV2.path[].role | string | — | Actual angle accounting path from approved frame support rule, not glyph. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| AngleOccurrenceV2.total_length_mm | double | mm | Sum actual ordered line/arc path lengths, 05B-F35. |
| AngleOccurrenceV2.mass_kg | double | kg | total_length_mm/1000*unit_mass_kg_per_m, 05B-F01/F20. |
| AngleOccurrenceV2.shape_recipe_id | string | — | Exact TK.TG/physical role recipe. |
| AngleOccurrenceV2.source_ids[] | string[] | — | Angle code/unit mass/position/physical path rule sources. |
| AngleOccurrenceV2.spec_id | string | — | Exact S2 MemberSpecV2 identity; scope/face/layer never guessed from catalog code or label. |
| AngleOccurrenceV2.role_values[].role | string | — | Pass-through actual ordered physical frame segment roles from approved angle recipe. / Named run/bend/leg role, ordered where topology requires. |
| AngleOccurrenceV2.role_values[].value_mm | double | mm | Pass-through actual ordered physical frame segment roles from approved angle recipe. / Value from approved shape expression/path, not glyph measurement. |
| AngleOccurrenceV2.role_values[].source_ids[] | string[] | — | Pass-through actual ordered physical frame segment roles from approved angle recipe. / Expression inputs/shape rule sources. |

### ViewPartCutV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ViewPartCutV2.component_id | string | — | Exact component receiving this cut/projection frame. |
| ViewPartCutV2.frame.id | string | — | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / ENCODE(owner_id,face_role,frame_role), 05B-F46. |
| ViewPartCutV2.frame.origin.x_mm | double | mm | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| ViewPartCutV2.frame.origin.y_mm | double | mm | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Y của point argument, 05B-F26/F27. |
| ViewPartCutV2.frame.origin.z_mm | double | mm | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Điểm gốc component/view/host: argument origin của FRAME; 05B-F26/F29/F38. / Tọa độ Z của point argument, 05B-F26/F27. |
| ViewPartCutV2.frame.axis_u.x | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.axis_u.y | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.axis_u.z | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector U argument của FRAME; bảng T1..T4 tại 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.axis_v.x | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.axis_v.y | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.axis_v.z | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / Vector V argument của FRAME; 05B-F26/F38. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.normal.x | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần X của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.normal.y | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Y của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.normal.z | double | dimensionless | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / cross(axis_u,axis_v), hoặc outward đã khai báo cho host; handedness ghi rõ, 05B-F26. / Thành phần Z của unit basis/normal; 05B-F25/F26. |
| ViewPartCutV2.frame.handedness | int | — | Actual component-specific section plane; source SheetOneSectionViews composite behavior preserved explicitly. / sign(dot(cross(U,V),normal)); chỉ +1 hoặc -1, 05B-F26. |
| ViewPartCutV2.mode | string | — | TrueSection/CompositePublished/Projection; no physical geometry deformation. |

### Region2V2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| Region2V2.id | string | — | ENCODE(view_id,assembly_id,material_id,region_index), 05B-F46. |
| Region2V2.view_id | string | — | Actual view role ID. |
| Region2V2.assembly_id | string | — | Same pour assembly, joint/other materials never merged. |
| Region2V2.material_id | string | — | Concrete/Bedding/BeddingMortar/native role. |
| Region2V2.component_ids[] | string[] | — | All physical components contributing to this region. |
| Region2V2.loops[].id | string | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / ENCODE(owner,loop_role,index), 05B-F46. |
| Region2V2.loops[].role | string | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Outer/Inner/Exclusion/Contact; không suy từ màu. |
| Region2V2.loops[].curves[].id | string | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Stable segment ID from owner/path index, 05B-F46. |
| Region2V2.loops[].curves[].kind | string | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Line/Arc, exhaustive geometry helper. |
| Region2V2.loops[].curves[].start.x_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| Region2V2.loops[].curves[].start.y_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| Region2V2.loops[].curves[].start.z_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| Region2V2.loops[].curves[].end.x_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. |
| Region2V2.loops[].curves[].end.y_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. |
| Region2V2.loops[].curves[].end.z_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. |
| Region2V2.loops[].curves[].center.x_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ X của point argument, 05B-F26/F27; không lấy paper X làm world X. Nullable parent Region2V2.loops[].curves[].center |
| Region2V2.loops[].curves[].center.y_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Y của point argument, 05B-F26/F27. Nullable parent Region2V2.loops[].curves[].center |
| Region2V2.loops[].curves[].center.z_mm | double | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Coordinate in owning object frame: occurrence paths world; solid/patch/opening loops local XY in that owner frame. Constructor mapping in 05B-F26/F27/F31/F35. / Tọa độ Z của point argument, 05B-F26/F27. Nullable parent Region2V2.loops[].curves[].center |
| Region2V2.loops[].curves[].normal.x | double | dimensionless | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần X của unit basis/normal; 05B-F25/F26. Nullable parent Region2V2.loops[].curves[].normal |
| Region2V2.loops[].curves[].normal.y | double | dimensionless | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Y của unit basis/normal; 05B-F25/F26. Nullable parent Region2V2.loops[].curves[].normal |
| Region2V2.loops[].curves[].normal.z | double | dimensionless | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc plane normal, Line=null. / Thành phần Z của unit basis/normal; 05B-F25/F26. Nullable parent Region2V2.loops[].curves[].normal |
| Region2V2.loops[].curves[].radius_mm | double? | mm | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Arc radius>0, Line=null. |
| Region2V2.loops[].curves[].sweep_rad | double? | rad | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / Signed arc sweep, Line=null; 05B-F35. |
| Region2V2.loops[].curves[].role | string | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Ordered analytic closed chain; 05B-F31/F32/F37. / StraightRun/Hook/Bend/Outline/Opening/Collector/Hidden; source-specific. |
| Region2V2.loops[].closed | bool | — | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / True chỉ nếu last.end==first.start trong approved geometry tolerance. |
| Region2V2.loops[].signed_area_mm2 | double | mm2 | Analytic outer/inner loops from shared SECTION_CSG/REGION_BOOLEAN; same for hatch and outline. / Analytic line/arc Green area, 05B-F37. |
| Region2V2.projection_frame_id | string | — | Actual section/projection frame key for region; composite body/access planes remain distinguishable. |
| Region2V2.surface_depth_mm | double? | mm | Projection visible surface depth for step-edge visibility; TrueSection=null. |
| Region2V2.material_role | string | — | Actual contributing role, or explicit mixed Concrete role requiring scoped hatch role binding; grade comes from component material_id. |
| Region2V2.representation_kind | string | — | TrueSection/Projection; visibility is not guessed from a nullable depth value. |
| Region2V2.visible_surface_id | string? | — | Actual exposed CSG face/analytic surface ID for Projection; TrueSection=null. Flat surface depth may be scalar, sloped/curved surface depth is evaluated by this surface, never one midpoint depth for all points. |

### HatchV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| HatchV2.id | string | — | ENCODE(view_id,region_id,pattern role), 05B-F46. |
| HatchV2.view_id | string | — | Owner view ID. |
| HatchV2.region_id | string | — | Exact Region2V2 ID, closed outer+inner loops available. |
| HatchV2.pattern_name | string | — | Exact requested catalog name or approved named fallback NR17. |
| HatchV2.pattern_scale | double | dimensionless | Exact paired CF.Data scale and approved presentation scale expression. |
| HatchV2.angle_rad | double | rad | Approved hatch role/source angle, not arbitrary zero if missing. |
| HatchV2.layer_id | string | — | Exact native semantic layer binding, preflight S0. |
| HatchV2.source_ids[] | string[] | — | CF.Ga/CF.Data/material mapping/approval evidence. |

### FontMetricStyleV2 — DBTool.Domain

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| FontMetricStyleV2.style_id | string | — | Exact native style binding. |
| FontMetricStyleV2.font_path | string | — | Approved font file path. |
| FontMetricStyleV2.font_hash | string | — | Hash actual font bytes, NR16. |
| FontMetricStyleV2.encoding | string | — | Unicode/TCVN3/VNI approved decoder. |
| FontMetricStyleV2.kerning_mode | string | — | None/SparseComplete/QueryRequired, explicit metric provider certificate. |
| FontMetricStyleV2.provider_version | string | — | Measured metric provider version/hash; no char-count substitute. |
| FontMetricStyleV2.normalization | string | — | None/NFC explicitly declared by approved FontPolicy, no silent text normalization. |

### ProjectedNativeV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ProjectedNativeV2.id | string | — | ENCODE(view_id,source product id,asset name), 05B-F46. |
| ProjectedNativeV2.view_id | string | — | Exact projection view ID. |
| ProjectedNativeV2.source_id | string | — | Exact GeometryV2.products[].id, not schematic label ID. |
| ProjectedNativeV2.asset_name | string | — | Product MB/AA/BB asset selected by explicit view role table. |
| ProjectedNativeV2.insertion.x_mm | double | mm | Project actual world product anchor in selected view frame, 05B-F26/F38. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ProjectedNativeV2.insertion.y_mm | double | mm | Project actual world product anchor in selected view frame, 05B-F26/F38. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ProjectedNativeV2.rotation_rad | double | rad | Native registered in-plane rotation in this view, approved product binding. |
| ProjectedNativeV2.scale_x | double | dimensionless | 1 after unit normalization for physical product; no source distortion copied. |
| ProjectedNativeV2.scale_y | double | dimensionless | 1 after unit normalization for physical product. |
| ProjectedNativeV2.source_ids[] | string[] | — | Product/source transform evidence. |

### ScheduleHeadingV2 — DBTool.Drawing

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| ScheduleHeadingV2.id | string | — | ENCODE(view_id,heading kind,group_id), 05B-F46. |
| ScheduleHeadingV2.view_id | string | — | Exact Schedule/Bending view ID. |
| ScheduleHeadingV2.group_id | string? | — | First actual group for owner heading; fixed table header=null. |
| ScheduleHeadingV2.kind | string | — | OwnerHeading/TableHeader/BendingTitle. |
| ScheduleHeadingV2.native_asset | string | — | TK.Tenluoi/TK.Tieude/TieudeMC exact source names. |
| ScheduleHeadingV2.attributes[].tag | string | — | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Exact ATTDEF tag in selected AssetV2. |
| ScheduleHeadingV2.attributes[].text | string | — | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Formatted literal from typed value; no sample ATT fallback. |
| ScheduleHeadingV2.attributes[].value_kind | string | — | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Text/Length/Metres/Mass/Count/Diameter/Pitch/SizePair/UnitMass. |
| ScheduleHeadingV2.attributes[].raw_number | double? | unit field | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Unrounded numeric value when scalar; text/pair=null. |
| ScheduleHeadingV2.attributes[].unit | string | — | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / mm/m/kg/count/kg_per_m/none, explicit per binding. |
| ScheduleHeadingV2.attributes[].source_ids[] | string[] | — | TENLUOI or TIEUDE literal from source owner label; fixed header empty array. / Group/input/rule values that produced text. |
| ScheduleHeadingV2.insertion.x_mm | double | mm | Source-backed heading offset relative first row or bending origin, 05B-F15/F41. / Tọa độ ngang trong frame đã ghi, 05B-F26/F38. |
| ScheduleHeadingV2.insertion.y_mm | double | mm | Source-backed heading offset relative first row or bending origin, 05B-F15/F41. / Tọa độ dọc trong frame đã ghi, 05B-F26/F38. |
| ScheduleHeadingV2.asset_scale | double | dimensionless | S/100 native annotation baseline, 05B-F14. |
| ScheduleHeadingV2.ink.min_x_mm | double | mm | Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / min của ink/curve X sau transform; 05B-F42. |
| ScheduleHeadingV2.ink.min_y_mm | double | mm | Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / min của ink/curve Y sau transform; 05B-F42. |
| ScheduleHeadingV2.ink.max_x_mm | double | mm | Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / max của ink/curve X sau transform; 05B-F42. |
| ScheduleHeadingV2.ink.max_y_mm | double | mm | Actual native fixed stroke+ATTRIB ink, 05B-F40/F42. / max của ink/curve Y sau transform; 05B-F42. |

### FailureV2 — DBTool.Application

| Field lá | Kiểu | Đơn vị | Định nghĩa chính xác |
|---|---|---|---|
| FailureV2.diagnostic.code | string | — | Concrete RAISE payload; no Ready data on failure. / Mã RAISE được ghi trực tiếp trong pseudocode. |
| FailureV2.diagnostic.stage | string | — | Concrete RAISE payload; no Ready data on failure. / S0..S11 hoặc PRECHECK. |
| FailureV2.diagnostic.manhole_id | string? | — | Concrete RAISE payload; no Ready data on failure. / Pass-through context.manhole_id nếu đã có. |
| FailureV2.diagnostic.subject_id | string? | — | Concrete RAISE payload; no Ready data on failure. / Component/occurrence/group/view/field bị lỗi; null chỉ lỗi trước xác định subject. |
| FailureV2.diagnostic.field_paths[] | string[] | — | Concrete RAISE payload; no Ready data on failure. / Paths cụ thể đã kiểm, không một thông báo chung. |
| FailureV2.diagnostic.expected_text | string | — | Concrete RAISE payload; no Ready data on failure. / Literal invariant/expected field value từ phép kiểm. |
| FailureV2.diagnostic.actual_text | string | — | Concrete RAISE payload; no Ready data on failure. / Serialize actual values bị lỗi, culture invariant. |
| FailureV2.diagnostic.source_ids[] | string[] | — | Concrete RAISE payload; no Ready data on failure. / IDs của các input gây lỗi, tra context.sources[]. |
| FailureV2.diagnostic.needsrule_ids[] | string[] | — | Concrete RAISE payload; no Ready data on failure. / NR-01..23 hoặc NR-IO-* liên quan; không tự tạo numeric fallback. |
| FailureV2.diagnostic.severity | string | — | Concrete RAISE payload; no Ready data on failure. / Block/Warning/Info từ call RAISE/REPORT, không đổi khi render. |
| FailureV2.receipt.stage | string | — | Failed stage receipt, output_hash=null; cancellation explicit. / Tên stage vừa thực hiện. |
| FailureV2.receipt.run_id | string | — | Failed stage receipt, output_hash=null; cancellation explicit. / Pass-through context.run_id. |
| FailureV2.receipt.manhole_id | string | — | Failed stage receipt, output_hash=null; cancellation explicit. / Pass-through context.manhole_id. |
| FailureV2.receipt.status | string | — | Failed stage receipt, output_hash=null; cancellation explicit. / Ready/Partial/Blocked/Cancelled theo diagnostic và cancellation. |
| FailureV2.receipt.input_hash | string | — | Failed stage receipt, output_hash=null; cancellation explicit. / Hash canonical input state trước stage, 05B-F46. |
| FailureV2.receipt.output_hash | string? | — | Failed stage receipt, output_hash=null; cancellation explicit. / Hash ready payload; lỗi trước payload = null. |
| FailureV2.receipt.started_utc | string | UTC | Failed stage receipt, output_hash=null; cancellation explicit. / Clock UTC lúc bắt đầu; không dùng làm freshness workbook. |
| FailureV2.receipt.finished_utc | string | UTC | Failed stage receipt, output_hash=null; cancellation explicit. / Clock UTC lúc kết thúc stage, ghi kể cả lỗi. |
