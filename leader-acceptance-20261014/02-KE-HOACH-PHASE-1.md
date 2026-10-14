# Phase 1 — Leader kiểm trên máy và điều kiện chuyển cho chủ nhiệm

**Trạng thái hiện tại: Blocked_Preflight.** Build, software checks, counterexamples và host-load probe đã thực hiện; chưa có run sinh DWG/PDF sản phẩm đủ điều kiện nghiệm thu. Kế hoạch dưới đây là quy trình sau khi sửa, không phải bản ghi những bước native đã chạy.

## 1. Gate và điểm dừng

| Gate | Leader làm gì | Bằng chứng bắt buộc | Điều kiện qua / khi không qua |
|---|---|---|---|
| G0 — Freeze | Khóa code, build DLL, 13 receipts + amendments, spec, 23 approval, nguồn workbook/template/reference và host version. Tách hoàn toàn output khỏi nguồn. | run-manifest.json; source-build-hashes.json; command transcript; dependency/file-chain ledger. | Hash/path/phiên bản giải được; mọi gap có amendment. Thiếu thì Block, không dùng build cũ hoặc receipt cũ làm bằng chứng mới. |
| G1 — Implementation | Đóng findings theo 01-FINDINGS; chạy lại các counterexamples và public-entrypoint checks; đọc composition thật. | finding-closure.json theo từng ID; source line/diff; diagnostic và output từng reproduction. | Không fake port/actual/hash; active feature không bị bỏ; tất cả port bắt buộc có implementation thật. Tests xanh đơn thuần không qua G1. |
| G2 — Active bindings/input | Parse đủ field đúng profile/row/culture/cache; kiểm SourceV2 và approved bindings; đối chiếu expected độc lập từ raw Excel + công thức nguồn. | input-snapshot.json; raw-cells.json; effective-expected.json; approved-bindings.json; binding-preflight.json. | Mọi active required value có type/unit/source; 8 tolerance fields của seed chưa điền phải được bind theo scope trước phép đo tương ứng. Thiếu trả lỗi field/source; không chọn mặc định. |
| G3 — Host/unit/assets | Chạy đúng binary đã hash trong AutoCAD 2026; kiểm database units, block units, native asset roles/tags/hashes, fonts, CTB/STB, PC3/PMP và plot area. | host-preflight.json; template-native-manifest.json; font/plot hashes; actual INSUNITS và conversion receipt nếu có. | Numeric mm và insertion-unit boundary được chứng minh. Probe scratch INSUNITS=2 chỉ kiểm load, không được làm seed numeric. Dùng seed mm riêng; không đổi units để che hình học sai. |
| G4 — Smoke 3 profile | Chạy C001/C073/C145 sau khi input cụ thể được tạo và validate; kiểm đủ stage, row, physical source, view và native intent. | S0…S11 receipts thực tế; geometry/rebar/group/projection/annotation/TK/sheet manifests; DWG. | Mọi required branch có output hoặc lỗi chính xác. Smoke lỗi thì dừng mở rộng; sửa W được gán. |
| G5 — Save/reopen/readback | Save output copy, đóng DB đọc, mở DB mới, kiểm semantic handle ownership và native property từng đối tượng. | write-transaction.json; saved DWG hash; reopened-native.json; expected-actual-checks.json. | Coverage exact; actual đọc từ DB mới; field equality theo unit/tolerance nguồn; không có input→actual copy. |
| G6 — Plot/compare | Plot từng frame đúng window/profile; kiểm file thật và page count; đo vector PDF, render cạnh reference và overlay. | plot-session/plot-receipt.json; mỗi PDF/hash; affine-controls.json; vector-diff.json; comparison images. | Không mất/chồng view/ink/TK; scale và measurements đúng nguồn; reference identity đúng hoặc chỉ dùng reference làm recipe. Không lấy điểm ảnh/glyph để tính chiều dài thanh. |
| G7 — Coverage/edge | Mở rộng wave B/C/D và mọi dòng hợp lệ trong 3 workbook gốc; chạy update/undo/cancel/failure/rename/multiple runs. | case-ledger có từng variant; mỗi case/row/frame có artifacts hoặc final diagnostic; mutation receipts. | Mọi mục áp dụng có evidence; coverage conflicts có disposition nguồn minh bạch; không gộp 40 families thành 40 chạy tượng trưng. |
| G8 — Handoff | Tổng hợp manifest cuối, rà finding/row/page/region coverage, kiểm lại hash, đóng gói PDF cho DucBao. | phase1-signoff.json; product-pdf-manifest.json; checksum manifest; checklist từng trang/vùng. | Chỉ ReadyForHumanReview nếu G0…G7 đạt, không còn gap bắt buộc. Visual vẫn NotRun; chưa nghiệm thu cuối. |

G0–G3 fail thì không mở native product run. Lỗi row sau mutation chỉ tiếp tục row khác khi host/plot session được chứng minh khỏe; giữ trạng thái Committed/Saved thật và recovery path, không giả rollback. Mọi selected row phải có final receipt, kể cả chưa chạy vì cancel hoặc session hỏng.

## 2. Môi trường và lệnh thực tế

Đã xác minh executable: `C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe`, file version 25.1.164.0.0; transcript engine W.164.0.0. DLL command là `DBTool\DBTool\bin\Debug\net8.0-windows\DBTool.dll`, không phải chỉ DBTool.AutoCAD.dll. Target net8.0-windows/x64. DLL này đã load được trong Core Console ở probe cô lập; không suy đoán rằng WPF reference tự động làm Core Console không dùng được.

Lệnh đăng ký thật là `VEHOGABATCH`. `HoGaCommands.RunBatch` chỉ đọc **một** GetString là đường dẫn workbook, cho phép khoảng trắng. Không đọc sheetName, baseX, baseY hoặc scale/elevation từ command line. Không thêm những dòng đó vào script vì chúng có thể thành lệnh CAD kế tiếp. Đường chạy hiện tại vẫn cố định 1:30/Symbolic và chưa tự save/reopen/plot đầy đủ: N01/N09 phải sửa trước.

Script load/negative probe đã thực thi nằm tại `native-probe/load-only.scr`; executable arguments và PID/exit có trong `native-probe/process-result.json`. Nó dùng workbook cố ý không tồn tại để kiểm load và error path; không phải ca valid nghiệm thu. Trust path thay đổi trong profile `/isolate` của probe; không hạ SECURELOAD toàn máy hoặc kill CAD người dùng.

Để chạy command sản phẩm sau khi được sửa và G0–G3 đạt, thứ tự script phải là:

```text
_.NETLOAD
"<đường dẫn DLL đã hash trong run-manifest>"
VEHOGABATCH
"<đường dẫn workbook bản sao cụ thể của case đã validate>"
```

Hai đường dẫn này phải do runner sinh từ manifest đã khóa; đây là mô tả grammar, không phải yêu cầu chủ nhiệm tự điền placeholder để vận hành. Runner phải cung cấp entrypoint nhận RequestV2 và xuất receipts/artifacts đúng hợp đồng. **Runner sản phẩm/QA như vậy hiện chưa được nối: N01.** Không ghi một lệnh QA chưa tồn tại như lệnh đã chạy được.

Phân công host: Core Console cho các run batch lặp và đọc DB; AutoCAD desktop 2026 cho smoke thật qua palette cũ, undo/redo, display order và xác minh các khác biệt font/DIM/plot. Cả hai đều dùng cùng build/request/source snapshot. Không thay UI sản phẩm để tạo đường test dễ hơn; tool nghiệm thu tách riêng được phép cải tiến. Full-native baseline chỉ bắt đầu sau khi main command và palette cùng nối đúng pipeline.

## 3. Chọn bộ ca và thứ tự mở rộng

1. **Wave A:** C001, C073, C145 — một case mỗi profile, không phải đại diện nghiệm thu toàn profile. Tạo workbook copy, chọn exact row và bind đầy đủ trước khi chạy. Reference số học chỉ dùng nếu input/profile/rule identity khớp.
2. **Wave B:** tập pairwise trong `data/wave-selection.json`, chọn deterministically theo cặp type/foot/haunch/size/pipe hiện không mâu thuẫn scope; tie chọn case ID nhỏ nhất. Mục đích phát hiện interface/branch lỗi sớm, không thay full coverage.
3. **Wave C:** mọi core tổ hợp áp dụng còn lại. Không coi `nho/vua/lon-ngoai-mau` là con số; mỗi category phải được map thành giá trị có nguồn trong case binding manifest. Không tự chọn một size preset.
4. **Wave D:** đủ 40 edge requirement families và mọi variants bắt buộc. E12 có đủ các chiều dày nguồn nêu; E17 phải có ON và OFF; E23 phải chạy từng tỷ lệ nêu trong matrix; E29/E30 có isolation/concurrency; E35 mọi dòng hợp lệ của cả ba workbook. Ca invalid phải kiểm đúng diagnostic và mutation=0; không sinh PDF giả cho nó.

**Coverage conflict cần xử lý trước G7:** 72 core IDs yêu cầu `vat-tran` hoặc `vat-day-va-tran` tại profile2/3, trong khi 05A chỉ định TopHaunch ở CF.Ga!C9 của profile1. Giữ nguyên 216 IDs và đánh CoverageConflict_Hold. Đề xuất source-based N/A hoặc alias sang ca hợp lệ cùng type/foot/size/pipe đã ghi trong case-ledger; chưa tự duyệt N/A, chưa giảm tổng coverage và chưa tính Passed. Đây là mâu thuẫn matrix/spec cần khép phạm vi, không phải yêu cầu duyệt lại 23 NR.

216 core và40 edge đang là mô tả kế hoạch, chưa có workbook/path/hash gắn từng case. Không được gọi chúng là bộ256 input đã sẵn chạy. Mọi variant mới có ID, nguồn mutation, expected và negative/positive kind riêng; số lượng run thực tế có thể lớn hơn số family.

## 4. Output bắt buộc và tên file

Root run: `outputs/leader-acceptance/leader-field-contracts-20261013-r2/native-runs/{RunId}/{CaseId}/{ManholeId}/`. Các token RunId/CaseId/ManholeId do runner tạo và ghi manifest; không nhận path tùy ý ra ngoài root, không overwrite source.

| Thời điểm | File/output | Nội dung cần kiểm |
|---|---|---|
| Trước run | run-manifest.json; sources.json; input.xlsx bản sao | Source/build/template/rule/font/plot/reference hashes; selected targets; units; modes; SourceRefs; expected generation/input/document IDs. |
| S0–S2 | stages/S0.json…S2.json; raw-cells.json; effective.json; expected-independent.json | Field/type/unit/source; formula cache receipt; active/inactive predicate; expected được tính riêng từ nguồn, không copy output đang kiểm. |
| S3–S5 | geometry.json; rebar.json; clip-ledger.json; canonical.json; groups.json | Contour/depth/void/host; full physical paths; parent/station/cut; fabrication vs nominal; group/member raw sums và basis. |
| S6–S9 | projection.json; targets.json; annotations.json; schedules.json; sheet.json; native-intents.json | Exact required roles; frame IDs; all labels/TK/bending/ink; mỗi view bundle một placement; no fake assets. |
| S10 | model.dwg; native/write-receipt.json; native/reopened.json; native/measurements.json | Commit/save/reopen trạng thái thật; actual handles/type/fields/ownership; normalized actual hashes; before/after ga khác. |
| S11 plot | pdf/{FrameId}.pdf; plot/{FrameId}.json; compare/{FrameId}/ | PDF bytes/hash/pages, frame/window/scale/profile; vector measurement mapping; render/reference/overlay; no empty or stale PDF. |
| Cuối row/batch | row-receipt.json; batch-receipt.json; delivery-manifest.json | Một final row receipt mỗi selected row; nguồn/log/hash/artifacts khép kín; mọi lỗi lưu được hoặc báo persist failure rõ. |
| Handoff | phase1-signoff.json; product-pdf-manifest.json | Leader gate; exact PDF hash/page/frame/required regions/reference identity; visual_status=NotRun. |

Mỗi numerical check có `check_id, CaseId, RunId, ManholeId, FrameId, subject_id, field_path, expected, actual, delta, unit, tolerance_value, tolerance_rule_id, source_ids, native_handles, evidence_paths, evidence_hashes, method, verdict`. Actual thiếu = null + NotRun/Blocked, không bằng expected hoặc0. Chuỗi không numeric có expected_text/actual_text và exact schema/identity comparison. Header hash/số count không thay phép so field.

## 5. Đo DWG sau save/reopen

- **Line/Polyline/Arc:** actual start/end/vertices/bulges, plane/OCS, radius/center/signed sweep, closure, layer và role. Đo contour vật lý theo analytic geometry; không fit circle vào hộp hoặc lấy schematic làm fabrication.
- **DIM:** actual Dimension.Measurement, endpoints/axis và DIMSTYLE; expected từ Excel/effective độc lập. Numeric override che measurement sai phải fail; tiền tố/format được kiểm riêng.
- **INSERT/ATTRIB:** exact block definition/role/signature, basepoint, transform/scale, attributes đủ và không dư theo active schema; text/raw display mapping, style, height, width factor, rotation, insertion/alignment. Product scale vật lý và label paper scale là hai hợp đồng riêng.
- **Layer/DrawOrder:** exact tên/màu/linetype/lineweight/plot flag theo native manifest; kiểm effective ByLayer/ByBlock inheritance. Layer enumeration không biểu thị thứ tự vẽ: đọc DrawOrderTable/SORTENTSTABLE thực tế để kiểm outline/steel/text/hatch overlap.
- **Ownership:** từng semantic ID/handle join đúng ManholeId/GenerationId/RunId và version; dictionary manifest không thay XData/entity readback. Sau update B phải đọc lại A thực tế, không so hai hash đầu vào caller.
- **Coverage:** expected IDs từ source/model độc lập == actual IDs; không orphan, phantom, duplicate required member/view/TK/bending. Count chỉ phát hiện thiếu/thừa; mỗi đối tượng vẫn phải so properties.

## 6. So hai PDF và kiểm bố cục

1. Chọn reference bằng profile/input/rule/reference-bundle identity, không chọn bằng tên ga gần giống. Input khác kích thước thì reference chỉ kiểm recipe/trình bày; expected numeric lấy Excel current. Ghi `ComparisonMode=ExactInput` hoặc `RecipeOnly` và lý do.
2. Plot từng frame với window thật và PC3/CTB/STB đã hash. Mở PDF đã ghi trên đĩa; kiểm signature, bytes>0, pages, media/crop/rotation, text/vector corpus và mốc frame. Exit0 không đủ.
3. Lập transform model-mm→PDF-point từ ít nhất3 mốc độc lập không thẳng hàng, kiểm residual bằng tolerance nguồn NR18. Hằng số72point/inch và25.4mm/inch là định nghĩa đơn vị; không phải tolerance. Không warp riêng từng view để làm diff đẹp.
4. So vector theo semantic region: kích thước/frame/role coverage, endpoints/radii, native label text/positions, bảng/shape captions. Không diễn giải layer/block/ownership từ PDF nếu PDF không chứa thông tin đó; kiểm các mục này trong DWG.
5. Render reference và PDF mới ở cùng DPI/màu/clip được ghi trong qa-render-config; dùng side-by-side, blink/overlay và difference map. DPI chỉ là cấu hình quan sát, không được biến thành dung sai kỹ thuật chưa duyệt. Không dùng ngưỡng pixel tự đặt để nâng Passed.
6. Leader xem lại những vùng diff và checklist layout/role/number/font/ink trước G8. Việc Leader nhìn phục vụ kiểm máy không thay lượt DucBao bắt buộc ở Phase2. Giữ cả ảnh so sánh và PDF nguyên bản.

## 7. Điều kiện ký và quay lại sửa

Leader chỉ ký ReadyForHumanReview khi mọi G0–G7 đáp ứng bằng chứng thật, mọi case/row/frame bắt buộc đã có disposition, mọi actual measurement có nguồn/tolerance và tất cả finding đóng bằng phép tái hiện tương ứng. Không ký nếu còn stub/default fixture/missing binding, mismatched native/property/ink, row mất, PDF không tồn tại hoặc manifest lỗi.

Routing lỗi: input/asset→W02/W06; physical steel→W07; exact groups→W03/W08; TK/bending→W04/W09; views/labels→W04/W10/W12; sheet→W11; actual readback/plot/manifest→W05/W13; entrypoint/mutation safety→W13; evidence-classification→W01. Chỉ chạy lại downstream bị tác động sau khi freeze revision mới; giữ nguyên failed evidence cũ và gắn superseded, không sửa lịch sử thành xanh.

Phiếu ký Phase1 phải ghi build/source/rule/template/font/plot hashes, case/row/frame coverage, numerical verdict, open findings, PDF manifest hash, reviewer Leader và thời điểm thật. **Không điền sẵn chữ ký hoặc timestamp nghiệm thu chưa diễn ra.**
