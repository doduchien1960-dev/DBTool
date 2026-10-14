# ADJUDICATION 23 FINDINGS GỐC

| ID/mức | Gói | Kết luận | File:dòng | Bằng chứng | Command/predicate |
|---|---|---|---|---|---|
| R01 / P1 | W01 | PartiallyClosed | DBTool/DBTool.Drawing/SemanticDiff.cs:50; DBTool/DBTool.Drawing/SemanticDiff.cs:72 | {} bị từ chối và Start.X được nhận là numerical; nhưng manifest có ID/type mà thiếu schema/geometry vẫn NoDifference, unsupported schema vẫn nhận. | A; R01-empty, R01-start-x, R01-missing-schema-and-payload |
| G08 / P1 | W02 | StillOpen | DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:32; DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:91 | Reader chưa gọi native metric helper; converter vẫn có fabricated missing metrics; project AutoCAD lỗi CS1061 trước khi có thể round-trip. | A,NATIVE; G08-reader-signature-boundary, G08-incomplete-metrics |
| G09 / P1 | W02 | PartiallyClosed | DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:205; DBTool/DBTool.Application/Templates/VhgTemplateRoles.cs:32 | Map CatchSummary/CUATHU1/CUATHU2 đã sửa; chưa có original native template round-trip hoặc active-feature preflight được chứng minh. | A; G09-missing-active-catch |
| G10 / P1 | W02,W06 | PartiallyClosed | DBTool/DBTool.Domain/Input/ManholeInput.cs:18; DBTool/DBTool.Domain/Geometry/ChamberGeometryBuilder.cs:44 | Model và V2 pad0 đã đúng; đường Build hiện có vẫn dựng Pad extrusion depth0 và ném lỗi. | A,B; G10-domain-invariant, G10-product-geometry-build, G10-v2-s2-s3-suppression |
| R02 / P2 | W02,W07 | PartiallyClosed | DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs:386; DBTool/DBTool.Application/StageAdapters.cs:1 | DTO khác một trailing blank line được tái dựng/kiểm hash; receipt/dependency chain mới khớp. Giải thích thời điểm/actor của gap W06→W07 cũ còn là lời khai. | LEDGER;  |
| D08 / P1 | W03 | PartiallyClosed | DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:100; DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:683 | Có engine split/stitch, nhưng trừ phủ hết trả lại 10000 mm²; public Emit làm donut9600 thành10000; nửa hình tròn bị giữ nguyên. | AC; CSG-ENGULFED, CSG-ARC-HALFCUT, CSG-EMIT-HOLE |
| D11 / P1 | W03,W08 | StillOpen | DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:221; DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:432 | Một số missing recipe/prefix đã Block; scoped alias thiếu member proof vẫn qua cả W03→W08; angle thiếu recipe vẫn có group; ordered roles bị sort mất. | AC; ALIAS-NO-PROOF, V2-ADAPTER-NO-PROOF, ANGLE-MISSING-RECIPE |
| G01 / P1 | W06 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:73; DBTool/DBTool.Application/StageAdapters.cs:126 | Row missing đã Block; active NeckSize/catch fields thiếu vẫn default, Text Length="1,500" thành1500 dù không declared culture. | B; B01, B02, B05 |
| G02 / P1 | W06,W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:315; DBTool/DBTool.Application/StageAdapters.cs:352 | Đã có pipes/catch/panel objects nhưng S2 chỉ tự cấp bốn Base D12@150; mesh mutations không tác dụng, unknown catalog sinh box300; reinforced catch bị S4 bỏ. | B; B03, B04, B05 |
| G03 / P1 | W06 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:577; DBTool/DBTool.Application/StageAdapters.cs:607 | Roof void/pad guards có tiến bộ; catch T1→T2 giữ nguyên chamber frame/loops; Round pipe thành rectangle và thiếu outer; panel X→Y không đổi partition. | B; B06, B18, B20 |
| G04 / P1 | W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:1157; DBTool/DBTool.Application/StageAdapters.cs:1169 | Paths đã phân station nhưng vẫn cover50/D10/pitch150; missing recipe/rules vẫn Ready; Angle unit mass3.77 tự cấp; chưa gọi đủ physical recipes/planners. | B; B08 |
| G05 / P1 | W06,W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:1445; DBTool/DBTool.Application/StageAdapters.cs:1453 | Bar/hole có cùng XY nhưng count5 ra4; missing NR06 vẫn chia neo đôi, holeDia=D+7/depth=upper+10; joint datum bị cộng mortar lần nữa. | B; B10, B19 |
| G06 / P1 | W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:1147; DBTool/DBTool.Application/StageAdapters.cs:1179 | Missing host ở Create đã REBAR_REQUIRED_FEATURE_BLOCKED; missing recipe/D/pitch vẫn Ready; Preview trả cùng S4State với BindingMissing. | B; B08, B09, B09P |
| G07 / P1 | W06,W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:773; DBTool/DBTool.Application/StageAdapters.cs:1582 | Geometry length mutation đã nhạy hơn; rebar paths đổi khi Length1000→1000.0001 nhưng physical hash y hệt vì F3 quantization; hash không phải canonical F46. | B; B13 |
| G11 / P1 | W07 | PartiallyClosed | DBTool/DBTool.Application/StageAdapters.cs:1018; DBTool/DBTool.Application/StageAdapters.cs:1022 | Legacy bắt đầu lấy Catch.M/depth/frame nhưng vẫn fallback D8@150/cover25; V2 reinforced catch có0 occurrences và0 feature accounting. | B; B17 |
| G12 / P1 | W06,W07 | PartiallyClosed | DBTool/DBTool.Domain/Rebar/WallRebarPlanner.cs:137; DBTool/DBTool.Application/StageAdapters.cs:1276 | Legacy clearance carrier đã thêm nhưng S4 V2 clip dùng cover của member; Pipe.D=0, Protect.H50→75 cho paths hoàn toàn giống nhau. | B; B12 |
| D03 / P1 | W04 | PartiallyClosed | DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:470; DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:496 | Tên operations/unknown fault đã sửa; FixedRole ExprNode ID FIXED_X=100 vẫn lỗi vì parse ID như numeric literal; selected role provenance bị bỏ. | C;  |
| D04 / P1 | W04 | PartiallyClosed | DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:190; DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:217 | Không còn tạo TK definition ở một nhánh, nhưng handle0/default_hash vẫn nhận; thiếu Gach vẫn phát tick; duplicate glyph được FirstOrDefault nhận. | C;  |
| D05 / P1 | W04 | StillOpen | DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:731; DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:798 | S8/captions vẫn text.Length×height×0.6 hoặc skip missing glyph; synthetic ink evidence ghi120×50 nhưng executable fixture ra1080×150; nguồn glyph/font chưa nối. | C;  |
| D06 / P1 | W04 | PartiallyClosed | DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:161; DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:467 | Unchecked final fallback đã bỏ; vẫn defaults400/250 và fixed coordinate samples; Bottom thànhRight; có chỗ y50 nhưng false NoFit; routes xuyên chữ. | C;  |
| D07 / P1 | W04 | PartiallyClosed | DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:179; DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:219 | IDs và routes/ticks envelope đã cải thiện nhưng target ngoài member/phantom ID vẫn nhận; collector_axis Vertical bị bỏ; product ink thiếu. | C;  |
| D09 / P1 | W04 | PartiallyClosed | DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:285; DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:294 | Base/Wall/Roof/Neck có join tốt hơn; CatchChamber/CoverPanel thật từ W06 rơi vềBody/Main; missing owner vẫn TONG có nhưng subtotals0. | C;  |
| D10 / P1 | W08 | PartiallyClosed | DBTool/DBTool.Drawing/PresentationGroup.cs:283; DBTool/DBTool.Drawing/PresentationGroup.cs:401 | FromPlannedGroups có caller và registry carrier; nhưng two1m angles source13.7kg thành27.4kg; catalog-only join trộn Base/Roof; actual consumers còn pending. | AC; ANGLE-ROW-MASS-DUPLICATED, ANGLE-CROSS-HOST, BAR-MASS-MISSING-SOURCE |

## R01 / P1 — PartiallyClosed

Spec gốc: W01 evidence integrity; physical semantic diff. Gói: W01.

Hiện trạng: {} bị từ chối và Start.X được nhận là numerical; nhưng manifest có ID/type mà thiếu schema/geometry vẫn NoDifference, unsupported schema vẫn nhận.

Phải sửa: Bắt schema/version, required per-kind physical fields và exact semantic coverage độc lập actual; phân loại theo space/frame.

Gate đóng gốc giữ nguyên: P-EMPTY-PHYSICAL-MANIFEST và P-PHYSICAL-COORDINATE-CLASSIFICATION hết tái hiện; đổi physical endpoint không được là LayoutOnly.

Downstream phải kiểm lại: W05,W13.


## G08 / P1 — StillOpen

Spec gốc: S0; AssetsV2/AttributeDefV2; NR16. Gói: W02.

Hiện trạng: Reader chưa gọi native metric helper; converter vẫn có fabricated missing metrics; project AutoCAD lỗi CS1061 trước khi có thể round-trip.

Phải sửa: Sửa compile; nối một transaction reader thật; bỏ inspected-metric defaults; lấy layer theo entities thực, glyph/font/dependencies/source descriptor thật.

Gate đóng gốc giữ nguyên: So từng field của ít nhất hai block có alignment/style khác nhau với native; mọi source_id giải được trong registry.

Downstream phải kiểm lại: W04,W09,W10,W13.


## G09 / P1 — PartiallyClosed

Spec gốc: S0; exact native role/tag schema. Gói: W02.

Hiện trạng: Map CatchSummary/CUATHU1/CUATHU2 đã sửa; chưa có original native template round-trip hoặc active-feature preflight được chứng minh.

Phải sửa: Sau sửa G08, chạy reader→validator→import trên bản sao template gốc; xóa mỗi active asset/tag phải ra diagnostic đúng scope.

Gate đóng gốc giữ nguyên: Template gốc phải qua round-trip với role/tag đúng; xóa asset active phải lỗi chính xác, không thiếu role giả toàn cục.

Downstream phải kiểm lại: W06,W09,W10,W13.


## G10 / P1 — PartiallyClosed

Spec gốc: S3 padOFF; input PadThickness. Gói: W02, W06.

Hiện trạng: Model và V2 pad0 đã đúng; đường Build hiện có vẫn dựng Pad extrusion depth0 và ném lỗi.

Phải sửa: Guard PadThickness>0 tại ChamberGeometryBuilder và downstream hatch/datum; kiểm cả legacy Build và V2, giữ base datum, số âm diagnostic.

Gate đóng gốc giữ nguyên: Chỉ đổi pad dương→0: validate được, không Pad/hatch/PadBottom; pad âm trả diagnostic theo field.

Downstream phải kiểm lại: W06,W10,W13.


## R02 / P2 — PartiallyClosed

Spec gốc: B3.12 evidence/BeforeHash chain. Gói: W02, W07.

Hiện trạng: DTO khác một trailing blank line được tái dựng/kiểm hash; receipt/dependency chain mới khớp. Giải thích thời điểm/actor của gap W06→W07 cũ còn là lời khai.

Phải sửa: Giữ old receipts/snapshots; append forensic amendment cho chuyển tiếp lịch sử, phân biệt rebase mới với chứng minh execution order cũ; không bịa actor/time.

Gate đóng gốc giữ nguyên: Hash snapshot hiện tại và chuỗi sửa được giải thích đầy đủ; 19 dependency links hiện khớp không thay thế bằng chứng execution order.

Downstream phải kiểm lại: Tất cả downstream.


## D08 / P1 — PartiallyClosed

Spec gốc: S6; H06/H26; F37/F38. Gói: W03.

Hiện trạng: Có engine split/stitch, nhưng trừ phủ hết trả lại 10000 mm²; public Emit làm donut9600 thành10000; nửa hình tròn bị giữ nguyên.

Phải sửa: Giữ empty boolean success; union signed occupied regions thay standalone loops; analytic arc classifier/intersections; lỗi stitch phải fault, không trả base.

Gate đóng gốc giữ nguyên: Union hình chồng, void cắt mép, seam cùng vật liệu, lỗ tròn: topology/area/orientation theo tolerance nguồn và native/PDF.

Downstream phải kiểm lại: W10,W11,W13.


## D11 / P1 — StillOpen

Spec gốc: S5; F49; scoped aliases. Gói: W03, W08.

Hiện trạng: Một số missing recipe/prefix đã Block; scoped alias thiếu member proof vẫn qua cả W03→W08; angle thiếu recipe vẫn có group; ordered roles bị sort mất.

Phải sửa: Bắt alias member-set evidence vô điều kiện; exact parent/nested recipe scope và basis/prefix cho cả angles; giữ orderedRoleNames; carrier phải giữ toàn F49 key.

Gate đóng gốc giữ nguyên: Recipe sai profile/thiếu prefix/alias sai member set phải lỗi; cùng chữ mark ở host khác không cross-join.

Downstream phải kiểm lại: W08,W09,W10,W13.

W03=StillOpen; W08=PartiallyClosed. Verdict chung lấy mức còn mở của engine; không dùng carrier LocalPartDone để đóng engine.


## G01 / P1 — PartiallyClosed

Spec gốc: S2; H14; F47. Gói: W06.

Hiện trạng: Row missing đã Block; active NeckSize/catch fields thiếu vẫn default, Text Length="1,500" thành1500 dù không declared culture.

Phải sửa: Một typed parser F47 cho mọi active field; cấm thousands separator/inferred culture; kiểm source/cache/finite/enum; thiếu aperture/catch data phải Block.

Gate đóng gốc giữ nguyên: Input trống, dòng không tồn tại, số lỗi và cache chưa xác minh phải Block; probe P-MISSING-INPUT phải hết tái hiện.

Downstream phải kiểm lại: W07,W09,W10,W13.


## G02 / P1 — PartiallyClosed

Spec gốc: S2; H14; NR23. Gói: W06, W07.

Hiện trạng: Đã có pipes/catch/panel objects nhưng S2 chỉ tự cấp bốn Base D12@150; mesh mutations không tác dụng, unknown catalog sinh box300; reinforced catch bị S4 bỏ.

Phải sửa: Map đủ typed 206 bindings theo activity và feature DAG; exact pipe/product catalogs; sinh requested members mọi family; S4 account mọi feature ON, không chỉ duyệt mảng đã thiếu.

Gate đóng gốc giữ nguyên: Bật riêng từng family, 4 tường cống, catch, panel và vị trí cổ; OUT có đúng giá trị/source hoặc diagnostic cụ thể.

Downstream phải kiểm lại: W04,W08,W09,W10,W13.


## G03 / P1 — PartiallyClosed

Spec gốc: S3; H03/H04/H05; F27/F28. Gói: W06.

Hiện trạng: Roof void/pad guards có tiến bộ; catch T1→T2 giữ nguyên chamber frame/loops; Round pipe thành rectangle và thiếu outer; panel X→Y không đổi partition.

Phải sửa: Dùng host-local frames/datums/offset cho catch/pipe; giữ analytic round/box profiles và inner/outer/cutout riêng; panel split_axis/gap/seat đúng; haunch phải đúng occupied solid, không slab lấp lòng.

Gate đóng gốc giữ nguyên: Đối chiếu contour/depth/owner từng component; roof hole trừ đúng một lần; type3 không catch phải lỗi; pad0 không có Pad.

Downstream phải kiểm lại: W07,W10,W11,W13.


## G04 / P1 — PartiallyClosed

Spec gốc: S4; H07/H08; F32/F36. Gói: W07.

Hiện trạng: Paths đã phân station nhưng vẫn cover50/D10/pitch150; missing recipe/rules vẫn Ready; Angle unit mass3.77 tự cấp; chưa gọi đủ physical recipes/planners.

Phải sửa: Bỏ defaults; exact recipe/basis/support/stack; gọi physical planners clip/hooks/ties/panel/catch; Angle mass từ exact catalog kg/m; canonical mass không literal thiếu nguồn.

Gate đóng gốc giữ nguyên: Thanh nằm đúng host và cover; station khác nhau; conservation khi clip; thiếu recipe phải Block, không Known/Ready.

Downstream phải kiểm lại: W04,W08,W09,W10,W13.


## G05 / P1 — PartiallyClosed

Spec gốc: S4; H09; NR06; F34. Gói: W06, W07.

Hiện trạng: Bar/hole có cùng XY nhưng count5 ra4; missing NR06 vẫn chia neo đôi, holeDia=D+7/depth=upper+10; joint datum bị cộng mortar lần nữa.

Phải sửa: Đọc NR06 typed embeds/hole/stations/rounding và exact joint; thiếu Block; cardinality không truncation; dùng WallTop/RoofBottom surface IDs thay diễn giải lại joint.z.

Gate đóng gốc giữ nguyên: Tâm bar/hole khớp từng pair, khác nhau theo station; chiều dài/đường kính theo input; thiếu joint/binding phải Block.

Downstream phải kiểm lại: W09,W10,W13.


## G06 / P1 — PartiallyClosed

Spec gốc: S4 required-feature gate; H00. Gói: W07.

Hiện trạng: Missing host ở Create đã REBAR_REQUIRED_FEATURE_BLOCKED; missing recipe/D/pitch vẫn Ready; Preview trả cùng S4State với BindingMissing.

Phải sửa: Required-feature gate gồm host/recipe/shape/basis/source/support/collision; partial preview tách khỏi consumable S4; aggregate mọi lỗi trước accounting.

Gate đóng gốc giữ nguyên: Một member thiếu host cùng một member hợp lệ: không được có Ready S4; receipt và partial output phải đủ nguồn lỗi.

Downstream phải kiểm lại: W08,W09,W10,W13.


## G07 / P1 — PartiallyClosed

Spec gốc: S3/S4; F46. Gói: W06, W07.

Hiện trạng: Geometry length mutation đã nhạy hơn; rebar paths đổi khi Length1000→1000.0001 nhưng physical hash y hệt vì F3 quantization; hash không phải canonical F46.

Phải sửa: Một canonical serializer F46 có ordinal keys/stable unordered IDs/ordered topology/invariant round-trip/null explicit/escape chuẩn; bỏ F3 và delimiter concat; hash đủ physical payload, loại self hash.

Gate đóng gốc giữ nguyên: Probe P-GEOMETRY-HASH hết tái hiện; đổi từng endpoint, hole center, aperture, product rotation phải đổi hash.

Downstream phải kiểm lại: W08,W09,W10,W11,W13.


## G11 / P1 — PartiallyClosed

Spec gốc: S4; NR08; Catch.M/Protect.I. Gói: W07.

Hiện trạng: Legacy bắt đầu lấy Catch.M/depth/frame nhưng vẫn fallback D8@150/cover25; V2 reinforced catch có0 occurrences và0 feature accounting.

Phải sửa: Catch.M/Protect.I typed bắt buộc khi active; floor/side/front theo shared host frame; nối vào V2 S4; thiếu recipe/support phải Block, không silent omission.

Gate đóng gốc giữ nguyên: Đổi Catch.M, Protect.I, depth khác height, đổi tường và offset: mỗi path/length/count đúng support và nguồn.

Downstream phải kiểm lại: W04,W09,W10,W13.


## G12 / P1 — PartiallyClosed

Spec gốc: S4; NR01; Protect.H. Gói: W06, W07.

Hiện trạng: Legacy clearance carrier đã thêm nhưng S4 V2 clip dùng cover của member; Pipe.D=0, Protect.H50→75 cho paths hoàn toàn giống nhau.

Phải sửa: Một opening-clearance consumer dùng q=Protect.H+D/2 độc lập Pipe.D; analytical clip mọi opening, không bbox-first-hit; đo endpoints/interval ledger trên chính S4 V2.

Gate đóng gốc giữ nguyên: Giữ Pipe.D=0, thay riêng Protect.H: endpoint mesh phải đổi đúng q; ON/OFF phụ kiện không đổi clearance mesh.

Downstream phải kiểm lại: W09,W10,W13.


## D03 / P1 — PartiallyClosed

Spec gốc: S8; TagBindingV2.operation. Gói: W04.

Hiện trạng: Tên operations/unknown fault đã sửa; FixedRole ExprNode ID FIXED_X=100 vẫn lỗi vì parse ID như numeric literal; selected role provenance bị bỏ.

Phải sửa: Evaluate FixedRole qua approved expression DAG/environment; bind raw/unit/role-source trước format; required Literal/missing role phải fault.

Gate đóng gốc giữ nguyên: Mỗi operation hợp lệ có raw/text đúng; unknown Block; reopen ATTRIB đủ tag và giá trị, không ô rỗng thay số.

Downstream phải kiểm lại: W09,W13.


## D04 / P1 — PartiallyClosed

Spec gốc: S7/S8; exact asset gate. Gói: W04.

Hiện trạng: Không còn tạo TK definition ở một nhánh, nhưng handle0/default_hash vẫn nhận; thiếu Gach vẫn phát tick; duplicate glyph được FirstOrDefault nhận.

Phải sửa: Một exact asset validator cho mọi active label/TK/tick/tie/glyph; validate unique name/handle/hash/source/native bounds/tags; missing/duplicate phải Block.

Gate đóng gốc giữ nguyên: Xóa từng TK/BLT/glyph active phải báo tên/source đúng trước native write; không có fake handle/hash trong output.

Downstream phải kiểm lại: W09,W10,W13.


## D05 / P1 — StillOpen

Spec gốc: S7/S8; H12; NR16. Gói: W04.

Hiện trạng: S8/captions vẫn text.Length×height×0.6 hoặc skip missing glyph; synthetic ink evidence ghi120×50 nhưng executable fixture ra1080×150; nguồn glyph/font chưa nối.

Phải sửa: Bỏ mọi approximate fallback; style/font-file hash/glyph/codepoint/escape/kerning/alignment/rotation/mirror phải từ native metrics source; nối W02 glyph outputs; regenerate chứng cứ.

Gate đóng gốc giữ nguyên: Các chữ tiếng Việt, %%c, căn giữa/phải, quay/mirror và font khác: ink tính và native quan sát khớp tolerance nguồn.

Downstream phải kiểm lại: W09,W10,W11,W13.


## D06 / P1 — PartiallyClosed

Spec gốc: S7; H28; NR22; NR-IO-011. Gói: W04.

Hiện trạng: Unchecked final fallback đã bỏ; vẫn defaults400/250 và fixed coordinate samples; Bottom thànhRight; có chỗ y50 nhưng false NoFit; routes xuyên chữ.

Phải sửa: Sourced scoped lanes/side/extent/port; tính collision-free intervals/candidates theo constraints thay grid tùy ý; kiểm cả routes/text; missing rule Block, exhausted đúng NoFit.

Gate đóng gốc giữ nguyên: Che kín mọi lane phải NoFit với rejected candidates; case có chỗ phải đúng min gap và native port transform.

Downstream phải kiểm lại: W10,W11,W13.


## D07 / P1 — PartiallyClosed

Spec gốc: S7; H11; physical target association. Gói: W04.

Hiện trạng: IDs và routes/ticks envelope đã cải thiện nhưng target ngoài member/phantom ID vẫn nhận; collector_axis Vertical bị bỏ; product ink thiếu.

Phải sửa: Join actual group/member/tie occurrences và analytic collector intersections; validate host/frame/axis; union native product/labels/routes/ticks ink đầy đủ.

Gate đóng gốc giữ nguyên: Mỗi claimed point nằm trên collector và member; BLTC ID là tie thật; routes/ticks đều nằm trong certified envelope.

Downstream phải kiểm lại: W10,W11,W13.


## D09 / P1 — PartiallyClosed

Spec gốc: S8; NR14 physical owner buckets. Gói: W04.

Hiện trạng: Base/Wall/Roof/Neck có join tốt hơn; CatchChamber/CoverPanel thật từ W06 rơi vềBody/Main; missing owner vẫn TONG có nhưng subtotals0.

Phải sửa: Exact component→assembly ownership với đúng CatchChamber/CoverPanel vocabulary; orphan/ambiguous owner phải fault; không fallback Body hoặc suffix alias.

Gate đóng gốc giữ nguyên: Đổi tên/prefix ga tùy ý: DAY/TUONG/TRAN/CO và TONG phải khớp raw rows cho cả hai bucket Ø.

Downstream phải kiểm lại: W09,W13.


## D10 / P1 — PartiallyClosed

Spec gốc: S5; F49; shared registry. Gói: W08.

Hiện trạng: FromPlannedGroups có caller và registry carrier; nhưng two1m angles source13.7kg thành27.4kg; catalog-only join trộn Base/Roof; actual consumers còn pending.

Phải sửa: Join angle quantity/mass theo exact OccurrenceIds/host/length/unit basis, conserve raw mass; một S5 registry xuyên consumers; không station-key-as-pitch, không missing mass→0.

Gate đóng gốc giữ nguyên: Trace từ command thật; group marks khác canonical vẫn xuất nhất quán; host/panel/opening scopes không bị trộn.

Downstream phải kiểm lại: W09,W10,W13.


## NewIssue / hồi quy bổ sung

| ID/mức | Gói | Vị trí | Phát hiện | Liên kết finding gốc | Phải sửa |
|---|---|---|---|---|---|
| NEW-A01 / P1 | W02 | C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:171; C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:185 | Source registry mutable ghi đè provenance giữa hai bundle | G08 | Bundle-scope IDs using full source hash plus native handle; return a per-run immutable registry; reject duplicate ID with conflicting descriptor. Add two-bundle coexistence/concurrency test. |
| NEW-A02 / P1 | W02 | C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:32 | Rework W02 làm project AutoCAD lỗi compile CS1061 | G08 | Use valid entity-level layer data with explicit mixed-block layer semantics, then rebuild exact adapter against target AutoCAD2026 SDK and run host round-trip. |
| NEW-A03 / P2 | W01 | C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:265; C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:397 | Numeric sheet placement bị coi NumericalMismatch | R01 | Classify using declared entity space/frame and physical payload; allow numeric layout fields to remain LayoutOnly only when physical geometry unchanged; add numeric-vs-string placement tests. |
| AC-N01 / P1 | W08 | DBTool/DBTool.Drawing/PresentationGroup.cs:399 | Angle group nhân bản cả aggregate row mass | D10,D11 | Compute mass over exact group member lengths using sourced unit mass or form the matching quantity2 group. |
| AC-N02 / P1 | W08 | DBTool/DBTool.Drawing/PresentationGroup.cs:284 | Angle catalog-only join lấy nhầm host/mark/length | D10,D11 | Index schedule source by exact OccurrenceIds and validate host/unit/length membership. |
| AC-N03 / P1 | W03 | DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:690 | Difference phủ hết làm sống lại original material | D08 | Retain empty Boolean success; distinguish invalid stitching; remove same-direction coincident pieces for difference. |
| AC-N04 / P1 | W03 | DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:100 | Public section union lấp lại inner hole | D08 | Union complete signed regions by occupied winding rather than standalone positive polygons. |
| AC-N05 / P1 | W03 | DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:1045 | Boolean classifier vẫn xem arc như endpoint chord | D08 | Use analytic arc crossings/occupied-side classification, test tangencies and signed sweeps, reject invalid stitching explicitly. |
| AC-N06 / P2 | W08 | DBTool/DBTool.Drawing/PresentationGroup.cs:302 | Carrier lấy stationKey làm pitch, phân mảnh groups | D10,D11 | Use requested/source pitch or route both carrier paths through the single exact W03 planner. |
| AC-N07 / P1 | W08 | DBTool/DBTool.Drawing/PresentationGroup.cs:210 | CoverageV2 nhận duplicate primary membership | D10,D11 | Count multiplicities, reject duplicate groups/memberIDs and assert count/length cardinalities. |
| C-R01 / P1 | W02,W04 | DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:610; DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:281 | S7 strict metric gate không tương thích glyph output của S0 converter | G08,D05 | Coordinate W02 native glyph metric extraction with W04 strict measurement before W10 integration; keep D05 blocked, do not restore a guessed width fallback. |
| C-N01 / P1 | W04 | DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:272; DBTool/DBTool.Drawing/CatchAssemblyProjector.cs:312 | Certified view envelope bỏ native product ink | D07 | Union exact transformed native asset ink with all other certified view ink and collision keepouts; propagate envelope to W10/W11. |
| C-N02 / P1 | W04 | C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261014-batch-review-02/frozen/packages/W04/rework/D05-Native-Ink-Measurement-Evidence.json:7 | Evidence Passed trái executable fixture và thiếu log/native observation | D05 | Regenerate evidence from real command output and persisted native measurement; attach exact command/exit/log and independent expected/actual/unit/sourced tolerance; correct Passed to not-closed until then. |
| BR-B01 / P1 | W07 | DBTool/DBTool.Application/StageAdapters.cs:1454 | S4 đọc DowelLength từ foreign row | G01,G05 | Chỉ consume effective typed fields current row hoặc exact source lookup; không SelectMany(all rows). |
| BR-B02 / P1 | W07 | DBTool/DBTool.Application/StageAdapters.cs:1284 | Clip tự bỏ retained30 mm rồi Ready với conservation_error30 mm | G04,G12 | Bỏ threshold50 thiếu nguồn; preserve retained hoặc source-bound suppression/accounting; nonzero conservation Block. |
| BR-B03 / P1 | W06,W07 | DBTool/DBTool.Application/StageAdapters.cs:1483 | W06 Joint.z=RoofBottom bị W07 hiểu WallTop và cộng mortar lần nữa | G03,G05 | Joint surface identities có nghĩa thống nhất; đọc exact WallTop/RoofBottom datums, hole starts RoofBottom. |
