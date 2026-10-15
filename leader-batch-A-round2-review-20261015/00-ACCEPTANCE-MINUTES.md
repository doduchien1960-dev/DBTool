# BIÊN BẢN NGHIỆM THU ĐỢT A — VÒNG SỬA 2 / R03

Ngày 2026-10-15. Phạm vi: W01/W02/W03; 13 finding được chủ nhiệm chỉ định. Review độc lập theo finding gốc, adjudication R02 và 7 lệnh REWORK R02; không lấy receipt mới làm kết luận.

**VERDICT: NO-GO. Đợt B tiếp tục khóa.**

Code nhận bàn giao: 4/13 Closed (30,8%), 9/13 StillOpen (69,2%). Tỷ lệ này đo số finding đã đóng trong phạm vi chỉ định, không phải phần trăm hoàn thành DBTool hay độ đúng của DWG. Không dùng CONDITIONAL-GO vì R01/G08/D08 còn lỗi P1 ảnh hưởng trực tiếp đến độ tin cậy của evidence, kích thước ink và miền vật liệu; hiện chưa có biện pháp giới hạn rủi ro để mở B.

## Findings đã đóng

NEW-A02: bản build native dùng đúng SDK AutoCAD 2026 đã hết lỗi BlockTableRecord.Layer. AC-N03: Difference trả kết quả empty mà không phục sinh vật liệu. AC-N04: ca donut gốc giữ được lỗ qua public Emit. AC-N05: ca half disk gốc đã dùng analytic arc đúng.

Đóng các predicate hẹp trên không đóng G08/G09/D08. Các lỗi về signed region/island, reflected arc/full circle/tolerance được ghi riêng; không phủ nhận những bản sửa thực tế đã đạt.

## Findings còn mở

R01, NEW-A03, G08, G09, G10, R02, NEW-A01, D08, D11.

Phần local W02 của G10 (model>=0) và R02 (DTO whitespace + preserved receipt) đạt; overall finding vẫn mở vì consumer legacy và lịch sử cross-package chưa đủ bằng chứng.

## Phát hiện quan trọng

- Manifest thiếu schema hoặc geometry chưa đầy đủ vẫn có thể trả NoDifference; thay đổi ArcEndAngle có thể bị bỏ mất. Paper text translation và World→Paper vẫn bị phân loại sai; strict parser không khớp native producer.
- Native compile đã được sửa, nhưng typography vẫn được tạo bằng tỷ lệ và tên Bold. Fresh AutoCAD 2026 đo A,VHG-Unicode.Regular cho ink maxX≈74,706685, trong khi converter cho 56 ở cùng ref height 100; chưa có cơ sở ký G08.
- Native block hash không đổi khi interior line Y20→40 trong cùng bounds 100×100; child content thay đổi cũng không làm parent hash đổi.
- Public Emit trả 8400 cho donut 8400 cộng island 400, thay vì 8800. Trừ cutter 400 đã nằm trong void làm diện tích từ 8400→8000. Đây là lỗi occupied region, không thể chữa bằng cách đổi fixture.
- Alias/member proof và nhiều recipe guard đã đúng; Rejected/blank scope vẫn vào S5 trong code nhận bàn giao.
- Receipt AfterHash 23/23 khớp file thật; điều đó không làm các predicate sai trở thành đạt. Sample manual và copy input DWG không thay thế native field readback và generated target artifact.

## Chiến lược dứt điểm đã bàn giao

Leader chọn Đường 1 cho NEW-A01 và D11: đã viết full patch 4 file trên bản sao, có Before/After SHA256, các predicate thất bại trước sửa, expected/actual sau sửa, review chéo và apply script có hash guard/backup. Bản sửa bao gồm deep immutable parent IDs và host guard cho Angle; public DTO 11 field được giữ nguyên.

Các finding còn lại đi Đường 2: canonical producer/consumer gate (L01), native full text/asset/unit/product/evidence workflow (L02), complete occupied region kernel (L03), pad consumer bridge để gỡ vòng dependency (L06), forensic ledger với explicit unknown history (L07). Mỗi hướng có field contract, lỗi, thuật toán, risk và acceptance gate cụ thể.

Không chọn Đường 3: chưa có bằng chứng chắc chắn về một hiểu sai có thể giải quyết bằng cách viết lại cùng lệnh. Không giao một vòng REWORK thứ 3 chung chung cho implementer cũ.

## Trạng thái thực tế

Bản sửa của leader trong candidate đã được kiểm chứng trong phạm vi guard; chưa apply vào code live và chưa ký receipt mới thay implementer. Native probes R03 đã Executed và phát hiện lỗi; full pipeline NativeGate vẫn NotRun. Chưa có generated target DWG/PDF integration đủ để nghiệm thu DBTool.

Đây là biên bản review/chiến lược hoàn chỉnh cho yêu cầu hiện tại; không tuyên bố toàn bộ DBTool đã hoàn thành hoặc mọi lỗi đã được sửa.

| Tài liệu bàn giao | Nội dung |
| --- | --- |
| [01 — Bảng nghiệm thu](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/01-ADJUDICATION.html>) | 13 ID bắt buộc, 10 quan sát mới, đủ 4 thành phần bằng chứng ở từng dòng; R03-W03-N06 gộp vào D11 khi tính nghĩa vụ. |
| [02 — Chiến lược từng finding](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/02-DECISIVE-STRATEGY.html>) | 19 dòng triage cho 9 finding mở và 10 quan sát; chẩn đoán/đường xử lý/lý do/owner. |
| [03 — Code leader viết trực tiếp](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/03-LEADER-PATCHES.html>) | Diff + 4 full files + manifest + hash guard + replay command. |
| [04 — Spec cách làm mới](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/04-REPLACEMENT-SPECS.html>) | Canonical capture, native full text, complete occupied region, unit/product binding, pad bridge, ledger. |
| [05 — Sổ bằng chứng/gate](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/05-EVIDENCE-AND-GATES.html>) | Exact command/log/exit/hashes; phân biệt Executed/Failed/NotRun. |
| [06 — Khóa đợt và watch-list](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/06-BATCH-GATES-AND-WATCHLIST.html>) | Điều kiện mở B và chuỗi review D/E/port/F, không có ngoại lệ. |
