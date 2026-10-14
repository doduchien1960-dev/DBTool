# LỆNH SỬA SAU BATCH REVIEW — BẢY GÓI

Copy đúng một khối tới đúng chat. Đây là giao sửa lần tiếp, không phải thông báo GO. Không gửi tự động tới chat implementer.

```text
REWORK-W01 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W01/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: SemanticDiff.cs, ReferenceComparisonService.cs, ReleaseEvidence.cs. Xử lý R01 và NEW-A03.
1. SemanticDiff.cs:50/72/170: require explicit schema/version và per-kind geometry payload; externally declared required semantic IDs, không actual tự làm coverage chuẩn. Unknown schema, missing geometry, unknown field/kind phải diagnostic, không empty/NoDifference.
2. :265/397: classifier dùng declared world/sheet space+frame. World endpoint đổi là physical difference; numeric SheetPlacement đổi khi physical payload giữ nguyên là LayoutOnly, không blanket NumericalMismatch.
3. Chạy predicates R01-missing-schema-and-payload, R01-unsupported-schema, R01-unrecognized-geometry, R01-start-x, R01-numeric-sheet-placement tại probe A. Chứng minh cả malformed Block và valid world/layout classification. Không lấy tests existing xanh thay những predicates này.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W02 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W02/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: native assets/reader/validator/importer/model/provenance của gói. Xử lý G08,G09,G10(local model),R02(local DTO),NEW-A01,NEW-A02.
1. NativeAssetSignatures.cs:32 không compile với SDK AutoCAD2026 vì BlockTableRecord không có Layer. Lấy layer từ entities thực và mô tả explicit trường hợp nhiều layer; không thay bằng layer0 giả. Build đúng target SDK phải hết CS1061.
2. TemplateManifestReader.cs:91/268 phải gọi một block-signature reader transaction thật gồm ATTDEF height/width/style/alignment/position/rotation, block origin/extents/dependency/layers. Converter TemplateContractValidator.cs:158/190/246 không fill height200/width1/bounds0 khi inspected field thiếu.
3. Font/glyph metrics phải native/source-bound và truyền sang AssetsV2; không trả glyphs[] rỗng cho active typography rồi buộc W04 tự đoán.
4. TemplateContractValidator.cs:171/185: bỏ static mutable SourceRegistry; IDs gồm bundle content hash+native handle/descriptor, registry immutable theo run, conflicting ID phải fault. Hai bundle cùng block name/handle phải cùng tồn tại mà không ghi đè provenance.
5. G09: exact CUATHU1/CUATHU2 và Geometry/DowelHole theo active feature. Chạy original template reader→validator→import trong bản sao; xóa từng active asset/tag kiểm diagnostic. Build/probe synthetic không thay native round-trip.
6. Giữ model PadThickness>=0; bàn giao W06 sửa legacy ChamberGeometryBuilder. R02 giữ immutable old receipts, đính kèm exact historic/current DTO hashes và whitespace diff; không khẳng định chronology W07 nếu không có evidence.
7. Rerun probe A và native build; record command+exit/log/native field expected/actual/unit/tolerance/source. G08/G09 chưa native thì chưa Closed.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W03 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W03/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: AssemblyBoundaryEmitter.cs, PresentationGroupPlanner.cs; không đổi 23 NR trong RuleApprovalRegistry. Xử lý D08,D11 và AC-N03/04/05.
1. AssemblyBoundaryEmitter.cs:683: empty result của Difference hợp lệ phải giữ empty, không return original basis. Invalid stitch phải fault riêng, không coi empty là lỗi để resurrect vật liệu.
2. :100: union complete signed occupied regions (outer+inner winding), không union từng inner loop như polygon dương. Public Emit donut diện tích9600 phải giữ inner và9600, không10000.
3. Analytic line/arc intersections, inside classifier/sweep/tangency và vertical prism section; không chỉ giữ Arc kind nhưng classifier dùng chord. Half disk r10 phải đúng một nửa area theo independent geometry calculation; circular prism vertical section phải có regions đúng.
4. PresentationGroupPlanner.cs:702: alias luôn cần exact member-set evidence; không để alias_requires_exact_member_set=false/absent bỏ kiểm. Áp dụng cả W03→W08 S5 Execute.
5. :221/432/474: Angle cũng require exact recipe/basis/prefix; kiểm parent rule và nested shape scope; orderedRoleNames giữ thứ tự, không sort; không fuzzy C3/C3A.
6. Rerun CSG-ENGULFED/EQUAL/ARC-HALFCUT/EMIT-HOLE/CIRCULAR-PRISM-VERTICAL-SECTION, ALIAS-NO-PROOF/V2-ADAPTER-NO-PROOF/ANGLE-MISSING-RECIPE/ROLE-ORDER-ERASED. Native/PDF/W10 integration vẫn gate riêng chưa chạy.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W06 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W06/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: GeometryStageAdapter S2/S3, mapper/validator và geometry thuộc gói. Mở rộng có lý do sang Domain/Geometry/ChamberGeometryBuilder.cs để đóng consumer legacy của G10; đây là thay đổi scope tài liệu giao sửa, chưa sửa code trong lần review. W07 giữ S4/WallRebarPlanner.
Xử lý G01,G02,G03,G05(producer),G07(geometry),G10,G12(producer),BR-B03(shared datum).
1. StageAdapters.cs:126/195/352/394/428: bỏ defaults aperture/catch/panel và bốn Base D12@150. Parse đủ 206 contract fields theo activity; typed mesh diameter/pitch, flags/enums/tuples/cache/source. Text không declared culture và thousands separators phải Block. Missing active NeckSize/Catch fields phải Block.
2. :315: exact pipe catalog; UNKNOWN_BOX hoặc missing catalog không được tự300×300/thickness50. Phân biệt source inner/outer/cutout; giữ round analytic geometry.
3. :577/607/705: opening/catch dùng correct host-local wall/neck frame, offset/contact/depth; T1→T2 phải đổi physical chamber vị trí. Không dùng Plan frame cho mọi wall opening hoặc rectangle cho Round.
4. :727: panel split_axis X/Y, count/gap/seat/thickness từ typed source; không luôn chia X. Haunch phải đúng occupied wedge/union, không prism phủ lòng ga. Requested feature chưa dựng đúng phải Block.
5. :666 + W07:1483: thống nhất Joint.z semantics, ưu tiên explicit WallTop/RoofBottom surface IDs; với WallTop1470/RoofBottom1500/mortar30, hole bắt đầu1500, không1530.
6. HashPhysicalGeometry:773: dùng canonical F46 serialization, không custom delimiter concat; field/ordered topology/source-bound physical dependency phải được khóa đúng.
7. ChamberGeometryBuilder.cs:44: PadThickness0 không tạo extrusion/component; legacy Build/V2/hatch/PadBottom đồng bộ, base datum giữ đúng. Số âm field diagnostic.
8. Giữ Protect.H typed độc lập Pipe.D và truyền đúng sang V2 clip W07; không chỉ chứng minh mapper legacy.
9. Rerun B01/B02/B03/B04/B05/B06/B07/B15/B18/B20 và cross B19 với W07. Feature field changes phải phản ánh đúng output hoặc Block có field/source, không bật/tắt để né.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W07 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W07/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: FullRebarStage S4 và rebar planners; không overwrite S2/S3/S5/S8. Xử lý G02(consume),G04,G05,G06,G07(rebar),G11,G12,R02 và BR-B01/02/03.
1. StageAdapters.cs:1157/1169/1179: bỏ 700/3.77/cover50/D10/pitch150 thiếu nguồn. Exact recipe/support/frame/stack/shape/basis; mỗi requested family có planner hoặc Block. Angle mass=actual path_mm/1000×exact catalog kg/m; canonical mass coefficient phải source-bound.
2. :1130/1406: không coi Tie/Haunch/Lifting/Neck/Panel/Catch là generic straight mesh. Compose actual planners; clip→hooks/lifting→dowel→accounting với provenance và whole-path support.
3. :1453/1463/1472: cấm SelectMany(all rows) lấy raw dowel. Consume effective fields của exact current row; missing fields/bindings Block. Probe B11: row1 length600, row2 length900 đứng trước vẫn phải600 hoặc Block, không900.
4. :1493/1499/1500/1518: NR06 cấp embeds/stations/rounding/hole depth/diameter. Không chia length/2, D+7, upper+10; count5 không được ra4. Exact joint source, không FirstOrDefault bất kỳ joint dowel_required.
5. :1483 phối hợp W06: Joint.z đang là RoofBottom nhưng bạn hiểu WallTop rồi cộng mortar; đọc explicit surface datums. B19 hole start1500, không1530.
6. :1265/1276/1284/1342/1353: analytic union clipping mọi opening; q=Protect.H+D/2 dù Pipe.D=0. Không bbox/FirstOrDefault/wallCover substitute. Retained30mm không tự bỏ vì threshold50; conservation_error30mm phải Block, không Ready.
7. :1555: missing host/recipe/basis/source/support/collision đều required-feature Block; preview partial riêng không consumable S4. Không Ready chỉ vì có geometry output.
8. :1018/1022/1130: Catch.M/Protect.I bắt buộc khi reinforced catch active; floor/side/front đúng depth/frame. V2 phải có catch occurrences và feature accounting hoặc Block, không silently0.
9. :1582/1604: F46 canonical JSON/invariant round-trip, bỏ F3 và delimiter concat; mutate Length1000→1000.0001 thay paths phải thay physical hash. Include full physical rebar/dowel fields/topology, exclude self-hash.
10. R02: rebase hash mới khớp không chứng minh execution lịch sử; bổ sung immutable forensic amendment, không bịa actor/time, không sửa old receipts.
11. Rerun B08/B09/B09P/B10/B11/B12/B13/B16/B17/B19 trên actual W06 S3 output. Receipt hiện ghi8 Closed chỉ có code citations: thay bằng kết luận đúng và evidence executable per-predicate; không dùng tổng test pass.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W04 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W04/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: NativeSteelSchedulePlanner, SchematicPresentationPlanner, AnnotationLaneAllocator, CatchAssemblyProjector. Xử lý D03/D04/D05/D06/D07/D09 và C-R01/C-N01/C-N02.
1. NativeSteelSchedulePlanner.cs:470: FixedRole.x_expr là ExprNode ID, evaluate approved expression DAG với recipe/group environment, không Double.TryParse(ID). :496/540 validate required Literal và giữ selected role source IDs/unit trước format.
2. :820, AnnotationLaneAllocator.cs:190/217/808, SchematicPresentationPlanner.cs:93: exact unique active asset + real handle/hash/source/bounds/tags. Missing Gach/BLTC và duplicate glyph phải Block, không tên/bounds giả. handle0/default_hash không hợp lệ.
3. NativeSteelSchedulePlanner.cs:731/798 và SchematicPresentationPlanner.cs:225/239: bỏ skip missing glyph/text.Length×height×0.6; native style/font-file hash/codepoint metrics/escapes/kerning/alignment/rotation/mirror. W02 phải cấp glyph/source registry thật; thiếu Block.
4. AnnotationLaneAllocator.cs:507/511/524: bỏ400/250 và fixed seven samples; require scoped NR22 lane intervals/side/extents/extensions/tie-break/port source. Bottom không Right. Tính feasible intervals bằng subtract forbidden ink/collision intervals; có chỗ y50 không được NoFit vì grid bỏ sót. Kiểm leader/routes xuyên text và approved gap, không chỉ label bbox.
5. :179/219: claimed point phải analytic intersection của actual collector với actual group member; phantom/out-of-member phải fault; collector_axis Vertical phải thật sự vertical; BLTC validate actual Tie occurrence và host/frame.
6. :307: envelope union đủ native product ink từ CatchAssemblyProjector, label/route/tick/DIM/hatch; không omit native_references. Truyền bounds thật sang W10/W11.
7. NativeSteelSchedulePlanner.cs:285/294: join component→assembly ownership thật; producer vocabulary là CatchChamber/CoverPanel, không chỉ Catch/Panel. Missing/ambiguous owner fault, không Body/Main default; conservation subtotals=TONG theo đúng bucket nguồn.
8. Regenerate D05 evidence từ command output thật: fixture hiện ghi[0,-15,120,35], execution ra[0,0,1080,150]. Không giữ Passed. Rerun toàn runner probe C và native metric/save-reopen cases; pure fixtures không chứng minh native ink đạt.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

```text
REWORK-W08 — BATCH REVIEW NO-GO
ROOT=C:/Users/DucBao/Desktop/Dbtool/Ver 1
Receipt=C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W08/receipt.json
Path code trong khối tính từ ROOT/DBTool; dòng là mốc frozen review, locate class/method khi dòng dịch.
Scope: CalculationPipeline, DrawingInputs, PresentationGroup, ScheduleNativeAttributes, S5/carrier trong StageAdapters. Xử lý D10/D11 và AC-N01/02/06/07. Engine W03 sửa trước; không sửa ViewBuilder W10 ngoài scope.
1. PresentationGroup.cs:283/401: join angle schedule theo exact OccurrenceIds/physical host/length/unit basis, không chỉ catalog code. Group mass là tổng đúng members; two1m angles sourced13.7kg không thành27.4; Base/Roof cùng catalog không copy mark/mass của row đầu.
2. Giữ một immutable S5 registry/hash và exact group/member identities qua DrawingInputs/Calculate paths; không dựng mass0 khi nguồn chưa có. Nếu thiếu approved mass basis, Block.
3. F49 key gồm host/assembly/panel scope/face/layer/direction/family/topology/D/angleCode/basis/pitch/hookRule/regionRole/sortedOpeningSet/orderedRoleNames; stationKey không phải pitch, không làm mỗi station thành nhóm riêng. Giữ actual spacing và ordered roles.
4. ValidateCoverageV2 phải kiểm multiplicity, unique primary group membership, duplicate group/member IDs, group count và member-length cardinality; HashSet equality không đủ. Hai primary groups cùng member phải fail.
5. Propagate W03 missing alias proof/recipe/basis/prefix/scope faults qua actual S5 Execute; không retry widened scope.
6. VerifyW08Rework/Program.cs:305–325 tự throw StageFault trong test body không chứng minh adapter guard. Thay bằng actual invalid input→production adapter→captured fault, kèm command/output và source hash.
7. Rerun ANGLE-ROW-MASS-DUPLICATED/ANGLE-CROSS-HOST/BAR-MASS-MISSING-SOURCE/CARRIER-STATION-KEY-AS-PITCH/COVERAGE-DUPLICATE/V2-ADAPTER-NO-PROOF. Consumer integration W09/W10/W13 chưa chạy vẫn not-closed, không copy LocalPartDone thành global Passed.
Luật bàn giao: snapshot exact paths/input/receipt và SHA256 trước sửa; không reset repo. Chỉ patch owned methods trên bản mới nhất. Thiếu data/source/recipe/tolerance→StageFault/Blocked, không default/chọn row khác/tắt feature. Giữ23 NR; cấm fuzzy grouping/đo schematic tính chiều dài; không sửa UI chính, không3D. Gate chưa chạy NotRun; không test count đóng lỗi. Mỗi ID cần Before/After hashes+predicate+command/exit+log+independent expected/actual/unit/tolerance/source+Closed/PartiallyClosed/StillOpen có lý do. Receipt version mới giữ previous snapshot/dependency hashes; không sửa review frozen. Chỉ LocalPartDone nếu chưa đủ integration; không tự Passed để mở khóa downstream.
Output: code sửa trong scope + receipt.json cập nhật + per-finding evidence và lý do mọi mục chưa đóng. Gói trước chưa GO/interface còn lỗi thì Blocked.
```

