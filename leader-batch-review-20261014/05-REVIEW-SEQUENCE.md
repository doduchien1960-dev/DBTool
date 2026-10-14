# QUY TRÌNH REVIEW VÀ KHÓA ĐỢT CÒN LẠI

Áp dụng yêu cầu chủ nhiệm: đợt sau chỉ chạy khi đợt trước reviewer GO, không tạm bỏ qua vì tiến độ. Khi nhận bàn giao mới sẽ review đúng chuỗi dưới đây; văn bản này không tạo automation hoặc tự động gửi lệnh tới các chat.

| Bước | Bàn giao phải có | Reviewer gate | Được mở sau GO |
|---|---|---|---|
| Sửa A/B/C | Code + receipt version mới + per-finding four-component evidence; source/input/dependency hashes | Review lại từng finding và NewIssue, cross-boundary, shared methods; hiện cả ba NO-GO | Đợt D |
| Đợt D | W09+W10 hoàn tất và upstream hashes đúng GO | Toàn12 watch items và findings D scope; required views/annotation/TK/bending/source/ink/registry | W11+W12 |
| Đợt E | W11+W12 hoàn tất | S6+S8 bundle coverage exactly once, layout/full ink/frame fit/hash; replacement đủ mọi predicate, static modules vẫn absent | W05 |
| W05 ports | Orchestrator/readback/evidence ports + fault isolation + independent observations | Chỉ ký phạm vi port; không giả integration/native/visual đã chạy | W13 |
| Đợt F cuối | W13 composition/writer thật + full pipeline evidence | S0–S11 entrypoint thật; native entities/ownership/transactions/Create/Replace/save/reopen/readback; plot/reference/PDF + DucBao visual | Nghiệm thu tích hợp nếu mọi gate đạt |

NativeGate phải từ NotRun sang Executed ở nghiệm thu F bằng một actual end-to-end native run. Executed chỉ là trạng thái đã chạy; còn phải có NativeVerdict/kết quả đo Passed/Failed/Blocked rõ ràng. Không coi Executed tự động là đạt; thiếu measurement/plot/reviewer không DeliveryComplete.

Mọi GO ràng buộc exact source/build/input/rule/template/receipt hashes. Source upstream đổi sau GO: invalidate dependent gate, review lại phần bị tác động. Implementer không tự cấp GO; reviewer phải kiểm code diff, bốn món evidence, regressions và downstream watch-list. Categorical gate chưa chạy giữ NotRun; thiếu binding/source/tolerance giữ Blocked.

Hiện trạng khóa: A=NO-GO; B=NO-GO; C=NO-GO; D/E/W05/F=Locked. W09 đang chạy phải dừng theo khuyến nghị đã báo; không mở W10/W11/W12/W05/W13 theo kế hoạch mới trước khi có GO hợp lệ.
