# BIÊN BẢN REVIEW REWORK THEO ĐỢT A / B / C

Ngày review: 2026-10-14T22:19:57+07:00 (Asia/Saigon). Mã hồ sơ: DBTOOL-BATCH-REWORK-20261014-R02.

**Đợt B NO-GO: khuyến nghị dừng W09 ngay. Đợt A NO-GO. Đợt C NO-GO. Chưa có đợt nào mở khóa downstream.**

Phạm vi: W01,W02,W03,W06,W07,W04,W08; 23 finding gốc duy nhất, 30 phân công package/finding. W09 đang sửa chỉ được ghi nhận phần thay đổi S8 để giải thích shared-file hashes, chưa được nghiệm thu Đợt D. Không sửa code/UI/receipt sản phẩm trong lần review này.

Kết quả: Closed=0; PartiallyClosed=20; StillOpen=3. Có 16 records NewIssue/phản chứng hồi quy bổ sung, có liên kết finding gốc để không cộng cơ học thành một tổng lỗi độc lập. Không suy đoán ý đồ từ receipt sai.

## 1. Verdict riêng từng đợt
| Đợt | Gói | Verdict | Finding gốc chưa đạt | Việc tiếp theo |
|---|---|---|---|---|
| A | W01,W02,W03 | NO-GO | R01, G08, G09, G10, R02, D08, D11 | Sửa theo 02-REWORK-ORDERS; review lại, chưa chạy downstream |
| B | W06,W07 | NO-GO | G10, R02, G01, G02, G03, G04, G05, G06, G07, G11, G12 | Sửa theo 02-REWORK-ORDERS; review lại, chưa chạy downstream |
| C | W04,W08 | NO-GO | D11, D03, D04, D05, D06, D07, D09, D10 | Sửa theo 02-REWORK-ORDERS; review lại, chưa chạy downstream |

Đợt B đã được báo NO-GO ngay khi xác minh source còn số tự cấp tại S4, rồi được củng cố bằng các phản chứng chạy thật. Không gửi lệnh tới chat W09 và không tự dừng tiến trình của chủ nhiệm; khuyến nghị dừng đã thông báo trực tiếp trong chat này.

## 2. Bằng chứng quyết định

- B03: BaseBottomMesh D20A75→D8A200 và WallOuterMesh D16A100→D10A250 vẫn ra bốn Base D12@150. B04: UNKNOWN_BOX không catalog vẫn ra300×300/thickness50. B05: thiếu catch fields vẫn tự dựng400/300/500/600/150.
- B10/B19: missing NR06 vẫn Ready, count5 ra4; DowelHoleDia80 bị thay25; WallTop1470/RoofBottom1500/mortar30 cho hole start1530. B11: target row length600 nhận900 của foreign row.
- B12/B13/B16: Protect.H50→75 không đổi S4 mesh paths; Length1000→1000.0001 làm path đổi nhưng rebar hash không đổi; retained30mm bị bỏ và conservation_error30mm vẫn Ready.
- A/NATIVE: root chạy lại build project AutoCAD với SDK cài đặt AutoCAD2026, exit1/CS1061 tại NativeAssetSignatures.cs:32. G08 native reader/converter chưa nối; source registry có cross-bundle overwrite.
- AC: public Emit donut9600mm² thành10000mm² mất hole; complete Difference trả lại10000mm²; half-disk vẫn full area. Two1m angles sourced13.7kg thành27.4kg và same-catalog Base/Roof lấy nhầm mark/mass.
- C: FixedRole ExprNode ID không evaluate; Gach thiếu vẫn được phát; native ink fallback còn tồn tại; collector nhận phantom/off-member points; CatchChamber/CoverPanel bị chuyển Body/Main.

Các kích thước/khối lượng trên là predicate kiểm chứng hoặc independent algebra của chính fixture, không phải hằng số được phép thêm vào sản phẩm và không phải nghiệm thu DWG/PDF. Probe exit0 chỉ nghĩa harness ghi xong quan sát; nhiều quan sát là failure. Mọi numerical acceptance thật vẫn cần tolerance nguồn, thiếu thì Blocked.

## 3. Adjudication: mỗi finding gốc một dòng

Closed: đủ yêu cầu và chứng cứ kiểm độc lập. PartiallyClosed: có predicate đã sửa nhưng còn predicate lỗi/chưa chứng minh. StillOpen: cơ chế lỗi trọng yếu vẫn tồn tại. NewIssue: phát hiện bổ sung, ghi riêng. Tất cả status là reviewer adjudication, không copy receipt.

File:dòng là source sau rework trong snapshot này. Command keys A/B/AC/C/NATIVE/LEDGER được triển khai thành command/exit/log đầy đủ tại 04-EVIDENCE-AUDIT và data/review-commands.json.

| ID/mức | Gói | Kết luận | File:dòng sau sửa | Bằng chứng vắn tắt | Command/predicate |
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

## 4. Cross-package và shared-file audit

- W06→W07: probes B chạy GeometryStageAdapter S2/S3 thật rồi FullRebarStage S4 thật trên cùng frozen build. S4 dùng object S3 mới nhưng không giữ đầy đủ semantic contract: bỏ Catch.M feature, dùng wall cover thay Protect.H, đọc lại raw rows và hiểu sai Joint.z. Không thể nói “dùng đúng geometry” chỉ vì constructor nhận S3StateV2.
- W03→W08: V2-ADAPTER-NO-PROOF chạy S5 adapter thật; scoped alias thiếu member proof vẫn qua. Carrier không tự sửa được engine grouping và coverage validator còn nhận duplicate membership.
- W02→W04/W09/W10: reader metric helper chưa nối; converter trả thiếu glyph metrics; W04 S7 strict một phía nhưng S8/caption vẫn fallback. Exact ownership vocabulary CatchChamber/CoverPanel từ W06 chưa được W04 xử lý đúng.
- StageAdapters không bị kết luận “ghi đè upstream” chỉ vì SHA hiện tại khác receipt trước. Đã tái dựng W06/W07/W08 từ complete.patch và git base blob: cả ba AfterHash khớp exact UTF8/no-BOM/CRLF. W06→W07 chỉ FullRebarStage đổi; W07→W08 chỉ DrawingStageAdapter đổi; W08→snapshot chỉ SteelScheduleStageAdapter đổi. Geometry/FullRebar/S5 đã giữ nguyên qua các bước sau, lỗi review là lỗi logic còn lại.
- WallRebarPlanner W06 AfterHash khớp W07 BeforeHash. 9 dependency receipt hashes mới đều khớp frozen provider receipts. PreviousReceiptHash của cả bảy gói đều giải được tới snapshot đã giữ. Điều này chứng minh phiên bản liên kết, không chứng minh behavior hoặc chronology lịch sử R02.

## 5. Yêu cầu sửa và khóa tiếp tục

[02-REWORK-ORDERS](02-REWORK-ORDERS.html) có bảy khối copy-paste đúng owner và phép kiểm phải chạy lại. [03-WATCHLIST-D](03-WATCHLIST-D.html) có watch-list W09/W10. [05-REVIEW-SEQUENCE](05-REVIEW-SEQUENCE.html) khóa mọi đợt sau theo GO.

Gói upstream còn lỗi interface cần dùng phải giữ Blocked. Chỉ chủ nhiệm đưa tiếp công việc sau khi có biên bản reviewer GO đúng source/receipt hashes; receipt implementer tự ghi Passed không mở khóa. NativeGate hiện NotRun: lần này không chạy generation/plot/save-reopen nghiệm thu sản phẩm và không AI visual approval.
