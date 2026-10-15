# R05 — Thực thi theo ủy quyền toàn dự án

Trạng thái: InProgress. Không phải biên bản nghiệm thu hoặc GO. NativeGate toàn pipeline: NotRun. IndependentAcceptance: NotRun.

Nguồn ủy quyền: tin nhắn chủ nhiệm DucBao trong chat này, ngày 2026-10-15: toàn quyền sửa toàn pipeline, giữ 23 NR/UI/không 3D; snapshot/hash guard bắt buộc; người sửa không tự ký nghiệm thu cuối.

## Quyết định kỹ thuật đã thực hiện

D-R05-01: tiếp nhận S-R04-01 tại hồ sơ R04 thành sửa đổi kỹ thuật của leader, không gán nhãn chủ nhiệm đã phê duyệt riêng nội dung này. Chọn protocol DBTOOL-PHYSICAL-R04, tách intrinsic text khỏi placement, chỉ tịnh tiến thuần Paper được LayoutOnly, World là NumericalMismatch, basis/space khác là CoordinateBasisMismatch. Mọi delta vẫn được giữ. Lý do: spec R03 có mâu thuẫn thứ tự classifier đã được chứng minh trong R04. Không sửa quy tắc engineering, tolerance hoặc NR. Bản R03/R04 giữ nguyên để truy vết.

D-R05-02: triển khai L01/L02/L03 song song trên ba bản sao tách biệt, leader tích hợp theo dependency sau kiểm chứng. Chưa mở gate B–F chỉ vì một candidate compile. L06 do leader thực hiện riêng; chỉ root được apply live. L01 sở hữu canonical producer/consumer; L02 sở hữu native assets/typography; L03 sở hữu occupied-region emitter/kernel. Mọi file ngoài phạm vi phải được phối hợp trước khi sửa.

## Trình tự và acceptance

1. Đóng băng baseline live/source/13 receipts, đối chiếu 4 post-hash R04. Đã chạy bootstrap.py: mọi hash khớp; 536 file source/project/script/data, 13 receipts đã lưu; candidate L01/L02/L03/L06 tách riêng.
2. L01/L02/L03: độc lập tái tạo RED, viết candidate, chạy oracle/compile/core/SDK. Mỗi fault phải có code/field/nguồn; thiếu dữ liệu vẫn Blocked. Native host chạy tuần tự dưới điều phối của leader.
3. L06: pad0 phải bỏ component/annotation/hatch/datum PadBottom; base bounds không đổi; positive dùng input; negative có diagnostic nguồn. Giữ global G10 mở đến đủ legacy/V2/native.
4. Mỗi apply: khóa danh sách thay đổi + baseline hash; chụp snapshot tất cả file trước; kiểm mọi hash trước khi ghi bất kỳ file; mismatch dừng; xác nhận post-hash; chạy lại đúng predicates trên live. Không dùng candidate evidence thay live evidence.
5. L07: tìm lịch sử được giữ; verified có lệnh/log/actor/time thật; unknown để null/Unverified, không dựng chronology bằng mtime. Không tự miễn historical finding.
6. Rà soát toàn bộ original findings + NewIssue theo bytes hiện hành, lần lượt input/geometry/rebar → presentation → drawing/layout/release → port → native integration. Upstream thay đổi làm downstream cần kiểm lại; ghi dependency rõ.
7. NativeGate chỉ Executed sau chạy thật AutoCAD2026, Excel → DWG → PDF. Mỗi mục có Passed/Failed/Blocked từ log và expected/actual/unit/tolerance. Không nâng gate từ build hoặc số test.
8. Bàn giao manifests/receipts/evidence/code/demo để audit độc lập. Không tự ký final GO.

## Báo cáo

Mỗi batch có báo cáo hoàn thành thực tế và finding còn mở. % chỉ tính theo nghĩa vụ có evidence, không suy từ thời gian/candidate/test count. Số Closed của đợt A tại baseline R04: 6/13 requested findings (46.15%); 7 còn mở, cộng các NewIssue riêng. Đây không phải % hoàn thành DBTool.


## Quyết định bổ sung và phê duyệt mới

D-R05-01A: Chủ nhiệm đã xác nhận trực tiếp “Phê duyệt đề xuất S-R04-01 để tiếp tục L01” trong lần tiếp tục này. Hồ sơ: owner-decisions/S-R04-01-owner-approval.json. D-R05-01 phía trên mô tả đúng nguồn quyền tại thời điểm cũ; xác nhận mới không ghi lùi thời gian và không sửa archive R04. Không sửa 23 NR.

D-R05-03: canonical contract có thể khai báo generated_output_authorization, nhưng saved-DWG vẫn phải có unit binding và bằng chứng ghi/save/reopen thật theo đúng hash. Không lấy input hash hoặc INSUNITS thay bằng chứng output. Hiện proof sản xuất chưa có; native L01 vẫn mở.

D-R05-04: L03 yêu cầu tolerance nguồn NR-18 Approved, tách basis dimensionless khỏi angular rad. Các thông số số học NR-18 đang null trong registry được giữ nguyên; không tự thêm giá trị. Projection xét occupied geometry tại mặt lộ đã khai báo, qua ordered CSG; nhiều Union mặt lộ khác mức trả CSG_PROJECTION_LAYER_UNRESOLVED. Đây là giới hạn còn phải triển khai, không phải miễn nghiệm thu. Kernel bảo toàn phân hoạch cạnh tròn đồng tâm, từ chối ID rỗng/trùng/null và opening không có geometry. D08 vẫn mở đến đủ năng lực/native/PDF.

D-R05-05: L02 bỏ glyph ratio/stroke tự điền, dùng unit binding theo bundle và exact font bytes. Whole text native dùng database đo tách biệt, clone đúng style, phục hồi đúng hai đường dẫn font bị WblockClone rút ngắn rồi so toàn signature; không thay font. Lý do: đo inventory native chứng minh chữ tạm tồn tại sau transaction abort trong side database mới (Undo mặc định tắt). ATTRIB phải thuộc INSERT. Các ca native hiện gồm DBText tiếng Việt, căn giữa/xoay/lật, ATT, MText wrap; bounds đối chiếu đối tượng tạo riêng rồi save/reopen. Font BigFont/SHX/CFF/TTC hoặc control streams chưa có bằng chứng coverage thì trả fault, không tự thay Arial. Unicode Arial synthetic không dùng đóng G08/G09 production. DWG hiện đọc Arial, DXF/contract yêu cầu Viet Buddy; giữ conflict mở.

D-R05-06: Kiểm tra/native oracle execution và NativeGate toàn pipeline là hai trường riêng. complete.sentinel chỉ chứng minh lệnh đã kết thúc; Status/Checks mới xác định oracle Passed/Failed/Blocked. Missing production context đã chạy và trả Blocked với Checks rỗng, không ghi Passed. S0–S11 NativeGate vẫn NotRun.

D-R05-07: Receipts downstream giữ nguyên DependencyReceiptHashes đã tiêu thụ trước đây và đánh dấu StaleNeedsReverify; cấm đổi sang hash mới rồi giả định gói downstream đã chạy lại. R05 có chuỗi snapshot/hash/command thật; lịch sử R02 không đủ bằng chứng vẫn Unverified. Chưa tự ký GO/acceptance.
