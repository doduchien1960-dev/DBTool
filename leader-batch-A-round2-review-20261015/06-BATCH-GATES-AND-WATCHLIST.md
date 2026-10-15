# KHÓA ĐỢT VÀ WATCH-LIST SAU R03

Đợt A = NO-GO. Không dùng 4 finding Closed hoặc candidate patch để mở đợt B. Không tự gửi lệnh tới các chat implementer; tài liệu này là trạng thái/gate do leader bàn giao cho chủ nhiệm.

## Điều kiện duy nhất để đổi verdict

GO: cả 13 finding được chỉ định đều đã Closed trên code thực tế được apply và replay; mọi new P1/P2 blocker ảnh hưởng đến phạm vi cũng Closed; các native/output gate bắt buộc đã được chạy với đúng artifact.

CONDITIONAL-GO chỉ được cân nhắc nếu rủi ro của lỗi còn lại được giới hạn bằng cơ chế thật, leader có công việc/owner/acceptance commitment rõ ràng và không cho output unsafe thành Ready. Hiện tại, R01/G08/D08 chưa đủ điều kiện này, nên verdict là NO-GO.

Không miễn R02 historical gap bằng 23 NR đã duyệt; nếu cần exception thì phải có quyết định riêng, giữ rõ lý do/evidence. Không cần quyết định đó để chốt biên bản NO-GO hiện tại.

## Dependency và thứ tự fail fast

1. Áp dụng/replay L04 registry và L05 recipe trên bản sao hợp nhất; giữ source/diff/receipt chain. Candidate đã có code, không cần junior viết lại.
2. Khóa schema/port revision L01,L02,L03 với actual producer/consumer và source/tolerance binding; thiếu contract phải Block trước khi chạy downstream.
3. Làm L01 canonical gate trước để mọi evidence mới không đi qua lỗ hổng NoDifference.
4. Làm L02 native context/asset identity/unit/encoding rồi L03 kernel; có thể phát triển riêng trên bản sao sau khi khóa interface, nhưng production consumer chưa được mở.
5. Leader thực hiện L06 pad bridge và L07 ledger amendment; overall G10/R02 chưa Closed thì ghi rõ, không đổi ownership để giả đóng.
6. Review lại A trên đúng applied source hash và native artifacts; chỉ GO/CONDITIONAL-GO hợp lệ mới mở B.

| Downstream | Điểm bắt buộc kiểm lại | Expected gate |
| --- | --- | --- |
| W06/W07 — Đợt B | Pad 0 consumer; source unit và exact registry; datums; actual S3→S4 nối đúng source; không dùng current row default | Legacy/V2/native pad không degenerate; inputs/output hash/source đúng. |
| W04/W08 — Đợt C | Consume full text metrics thay glyph ratio; exact recipe/host/alias proof; source registry không trôi | Missing native context/recipe→Block; ink và mark scope đúng. |
| W09/W10 — Đợt D | Canonical capture không bị bypass; block content/dependency hash; complete occupied region giữ holes/islands/arcs; tolerance có nguồn | Actual generated DWG readback khớp physical contract/PDF; không dùng schematic length. |
| W11/W12 — Đợt E | Whole text ink/alignment; units/asset physical scale; saved target/plot/evidence freshness | Layout/native plot gate có artifact/source/tolerance; không dựa test count. |
| W05 port | DTO/port revision L01/L02/L03, injected native provider, release gate fail closed | Port GO riêng; chưa ký integration thay W13. |
| W13 — Đợt F | Chuỗi full pipeline + native/save/reopen/PDF; không có Passed giả; đầy đủ source/recipe/tolerance contracts | Full pipeline NativeGate từ NotRun→Executed, mỗi acceptance predicate đều đạt. |

## Quy trình tiếp theo giữ nguyên

W09+W10 xong→review D; D GO mới đến W11+W12. W11+W12 xong→review E; E GO mới đến W05 port. W05 port GO mới đến W13. W13 xong→review F và nghiệm thu tích hợp toàn pipeline. Đợt sau chỉ chạy khi đợt trước GO theo rule của chủ nhiệm; điểm CONDITIONAL-GO duy nhất của yêu cầu hiện tại phải được leader nêu cụ thể, không tự suy rộng sang đợt khác.

Không có “tạm cho qua”. Báo cáo trạng thái không đồng nghĩa với tự dừng/chỉ đạo chat khác; chủ nhiệm có thể dùng verdict này để điều phối, leader không giả rằng đã gửi lệnh.
