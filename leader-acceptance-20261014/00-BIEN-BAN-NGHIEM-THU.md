# BIÊN BẢN SOÁT XÉT NGHIỆM THU IMPLEMENTATION DBTOOL W01–W13

**Mã:** DBTOOL-W01-W13-20261014-R01 · **Ngày:** 14/10/2026 · **Spec:** leader-field-contracts-20261013-r2.

**KẾT LUẬN: CHƯA ĐẠT. CHƯA ĐỦ ĐIỀU KIỆN MỞ ĐỢT NGHIỆM THU NATIVE CHÍNH THỨC.** Không xác nhận các InterfaceGate hiện tại đủ tin cậy làm nền cho nghiệm thu sản phẩm. Được tiếp tục chạy chẩn đoán trên bản sao để sửa lỗi; kết quả chẩn đoán không nâng NativeGate thành Passed.

## 1. Phạm vi và căn cứ

Đã đọc file yêu cầu, 13 receipt gốc, toàn bộ danh mục ChangedFiles và các caller/test cần truy vết. Ba reviewer đọc code theo nhánh độc lập; project leader kiểm lại source và chạy kiểm chứng. Có 111 bản ghi thay đổi, tương ứng 102 path: 96 file hiện có và 6 file được gỡ có chủ đích ở W12. DTO 103 record khớp field/type của spec; schema đúng không đồng nghĩa implementation đúng.

23 đề xuất NR01–NR23 đã được DucBao duyệt; giữ nguyên hiệu lực. Review này không yêu cầu duyệt lại và không tự tạo dữ liệu kỹ thuật còn thiếu. Không sửa code sản phẩm, UI chính, receipt cũ hoặc workbook/PDF/template gốc; không làm 3D.

## 2. Số liệu và bằng chứng đã kiểm

| Hạng mục | Kết quả thực tế | Giới hạn |
|---|---|---|
| Receipt | 13/13 ghi Complete/Passed; 59 checks; NativeGate đều NotRun | Bảng trong prompt ghi W06=13; receipt W06=4. Tổng đúng từ receipts là 59. |
| Build mới | Solution build exit 0; 0 error, 5 warning MSB3277 | Cần kiểm runtime; không phải bằng chứng DWG/PDF đúng. |
| Software suites mới | 1508 Passed / 1508 tests, 5 suites | DBTool.AutoCAD.Tests không có test runner: dotnet test chỉ restore, không chạy host test. |
| Counterexamples | 6 phép chạy trực tiếp đều tái hiện lỗi | Input thiếu vẫn Ready; hash không đổi; view thiếu; native Passed giả; semantic diff sai. |
| Host probe | AutoCAD Core2026 load DBTool.dll, VEHOGABATCH báo file thiếu đúng; exit 0 | Chỉ load/negative probe; không sinh sản phẩm. Seed INSUNITS=2, không dùng cho nghiệm thu mm. |
| Dependency | 19 link hash receipt khớp; mtime phù hợp | Không chứng minh execution order; có 1 gap BeforeHash và 1 current AfterHash mismatch. |
| Native sản phẩm | 0 DWG/PDF sản phẩm mới được nghiệm thu | Scratch DWG của probe có thay đổi; nguồn gốc giữ hash. Phase 2 chưa có PDF để xem. |

## 3. Thiếu sót phải đóng

Có **39 finding**: 34 P1 và 5 P2. P1 ảnh hưởng tính đúng hoặc độ tin cậy gate bắt buộc; P2 là lỗi phụ về cấu hình/provenance/hash. Mọi finding phải có disposition và bằng chứng đóng trước khi ký Phase 1; không dùng số test pass để đóng finding.

| Nhóm | Finding | Gói sửa | Điều kiện đóng |
|---|---|---|---|
| Dữ liệu và hình học | G01–G03, G07, G10 | W02/W06 | Không tự điền số, không mất feature; source/void/host/hash đúng. |
| Thép và clipping | G04–G06, G11–G12 | W07 | Physical paths/station/cover/holes đúng nguồn, thiếu binding phải Block. |
| Template và trình bày | G08–G09, D03–D09, D11 | W02/W03/W04/W08/W09/W10 | Exact native assets, boolean, targets và ink; không fake schema/metrics. |
| Đủ view và sheet | D01–D02, D12–D13 | W10/W11/W12 | Role table đủ, S8 được đặt, hash khóa đủ layout. |
| Đường chạy và native | N01–N04, N07, N10, N12 | W05/W13 | Command thật nối pipeline, render thật, save/reopen và field equality. |
| Bằng chứng và batch | N05–N06, N08–N11, R01–R02 | W01/W05/W13 | Actual độc lập, đủ row receipt/manifest và DucBao xem đúng PDF. |

Sáu phép tái hiện: input row trống vẫn ra 1000×1000×1500; đổi L 1000→1137 nhưng physical_hash giữ nguyên; ga không catch chỉ có ba view catch required=false; không native port/không DWG vẫn Committed/Passed; hai manifest{} được NoDifference; endpoint world Start.X đổi 10 mm bị coi LayoutOnly. Đây là phép chẩn đoán có input/log riêng, không phải số fixture nghiệm thu.

Danh sách đầy đủ có file/dòng/spec/gói sửa/cách đo tại [Phụ lục finding](01-FINDINGS.html). [59 checks](04-ADJUDICATION-59-CHECKS.html) được đánh giá riêng, không giữ nguyên chữ Passed của implementer.

## 4. Quyết định về điều kiện chạy native

Gate hiện tại là **NO-GO** do pipeline sản phẩm chưa nối S0–S11, có dữ liệu giả và feature bị bỏ, render/readback chưa đo thật, thiếu plot/review ports và còn missing bindings bị che. Seed rule có 8 tolerance fields chưa có giá trị; việc phê duyệt 23 phương án không cấp các con số này. Mọi thiếu dữ liệu active phải Block theo field/source, không tự nới dung sai hoặc tắt feature.

Chỉ mở lại khi: các lỗi ảnh hưởng run được sửa và tái hiện không còn; composition thật và nguồn/phiên bản được đóng băng; binding active đầy đủ; runner và output guards có thật; diagnostic negatives chặn đúng; unit-mm/asset/host preflight đạt. Sau đó mới chạy batch smoke, coverage, save/reopen/plot. Không yêu cầu NativeGate đã Passed trước lần chạy đầu, nhưng phải có implementation và dữ liệu đủ để chạy trung thực.

## 5. Hai phase và điều kiện ký

**Phase 1 – Leader kiểm trên máy:** đã hoàn thành audit/build/software/counterexamples và host-load probe. Sinh DWG/PDF đầy đủ, readback số học và overlay còn **Blocked_Preflight**. Kế hoạch chi tiết có gate, thứ tự ca, lệnh thật, output path và owner sửa tại [Phase 1](02-KE-HOACH-PHASE-1.html).

216 core IDs và 40 edge requirement families hiện chưa phải 256 workbook hợp lệ. Có 72 core selectors yêu cầu vát trần ở profile2/3 trong khi field TopHaunch chỉ có profile1; review ghi CoverageConflict_Hold, đề xuất N/A/alias theo nguồn và giữ nguyên matrix gốc. Không tự xóa 72 mục hoặc tính chúng là Passed. 144 tổ hợp chưa vướng mâu thuẫn này vẫn phải có input và validation cụ thể;40 edge families phải tách đủ variants/negative cases. Ba workbook gốc chạy mọi dòng thực sự hợp lệ, không chọn một dòng đại diện.

**Phase 2 – DucBao xem bằng mắt:** bắt buộc sau Phase 1 đạt. Danh sách PDF sản phẩm hiện rỗng; hồ sơ chuẩn bị gồm manifest template, checklist theo role/vùng và form ghi nhận. Chỉ PDF thật có hash/frame/page được Leader bàn giao mới được ghi review. Không dùng ảnh mẫu, unit output hoặc metadata visual Passed thay chữ ký của DucBao.

**Điều kiện Leader ký “máy kiểm xong”:** mọi selected row và required frame được đối chiếu đủ; expected từ nguồn độc lập, actual native/PDF; tolerance có nguồn; không missing/failed measurement; đủ entity ownership, view/TK/bending/ink coverage, save/reopen và plot evidence; các ảnh hưởng cross-ga/undo/cancel/negative cases đạt; không còn finding chưa có disposition; manifest hash khép kín. Chữ ký này chỉ chuyển sang ReadyForHumanReview, không phải nghiệm thu cuối.

**Điều kiện nghiệm thu cuối:** Phase 1 đạt và DucBao xác nhận từng vùng bắt buộc trên mọi trang PDF thực tế; mismatch hoặc vùng chưa xem giữ Failed/NotRun. Mọi đổi code/input/template/rule/plot/PDF sau review làm mất hiệu lực phần bị ảnh hưởng và phải kiểm lại.

## 6. Rủi ro và bước tiếp theo

1. **Passed giả:** fake port, self-comparison và test hình thức. Đóng N02–N06/R01; dùng mutation native thật và kiểm thiếu artifact không bao giờ Passed.
2. **Sai hoặc mất đối tượng khi thay ga:** topology/ownership/hash/Replace sai. Chạy bản sao, kiểm stale guards, A→B→updateB và before/after A, undo/save failure.
3. **DWG có hình nhưng PDF thiếu/chồng/sai tỷ lệ:** thiếu view/S8, ink giả, font/CTB/unit khác. Khóa source profiles, đo native/vector, render so sánh và bắt buộc DucBao review.

Giao trả implementer theo finding và [bảng từng gói](05-PACKAGE-REWORK.html). Không sửa bằng cách giảm expected coverage, tăng tolerance, sửa PDF mẫu hoặc giữ stub để qua tests. Sau sửa phải xuất amendment receipts kèm command/log/hash thực tế và chạy lại gate bị ảnh hưởng.

**Người lập:** Codex – project leader, kết luận review dựa trên bằng chứng máy và source. **Chủ nhiệm:** DucBao – chưa ký nghiệm thu sản phẩm trong biên bản này. Phê duyệt NeedsRule ngày 13/10 không được dùng làm chữ ký Phase 2.

[Mục lục](INDEX.html) · [Dữ liệu review](data/review-summary.json) · [Manifest kiểm chứng](DELIVERY-MANIFEST.json).
