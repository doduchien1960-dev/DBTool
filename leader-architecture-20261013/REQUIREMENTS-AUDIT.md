# Audit yêu cầu — bản cập nhật r2

Audit xác minh hồ sơ triển khai; không chứng nhận sản phẩm đã chạy đạt chuẩn.

| Yêu cầu | Hồ sơ / bằng chứng | Kết quả và phạm vi |
|---|---|---|
| 23 đề xuất được cập nhật | 04; owner-approval-23.json; runtime seed; phụ lục nguồn | 23/23 ApprovedProposal; dữ liệu binding thiếu được ghi riêng. |
| A1: field-level S0–S11 | 05: 12 stage; helper bodies; 49 formula IDs; 206 input bindings; lỗi/debug | Đã viết lại và tự rà. Chưa thực thi thuật toán thành code sản phẩm. |
| A2: 12 bảng Stage I/O | 05D; stage-io-contracts-v2.json | 11 ranh giới khớp path/type/unit/nullability; có IN/OUT/FAIL đến field lá. |
| 05C map 1-1 | Type registry; C# DTO; log build | 103 record đối chiếu 1-1; ba project DTO/interface compile thành công. |
| 05 độc lập | 05 nhúng field, formula, helper, contract, approval và asset schema | Không cần đọc 05C để có spec triển khai. |
| B1: một lần paste, tự chứa | 07; 13 TXT; kiểm nội dung ô Copy | Mỗi prompt có spec cần thiết; B3.01–B3.12 cùng format. |
| B2: ràng buộc chia gói | Module ownership map; package manifest | 9 phase + 3 NEW + 1 REMOVE; 116/10/6 module có gói; 168 reuse không sửa. |
| B3: format chi tiết | 07, bảng B3 và từng prompt | Định danh; điều kiện; file; rule; I/O; formula; algorithm; caller; lỗi; gate; token; receipt. |
| B4: dependency, token, fail fast | 07; sơ đồ SVG/Mermaid; package manifest | 13 < 15; ngân sách là ước tính có nêu giới hạn; compile DTO trước khi nối consumer. |
| B5: tự kiểm từng gói | Self-check manifest; spec/gate nhúng trong prompt | Không phụ thuộc spec 05 bên ngoài; ghi lý do không gộp; acceptance có phép đo thực tế. |
| Không fixture/fuzzy/schematic/test-count proof | Rule, thuật toán, error branches và gói gỡ code | Nguồn mẫu dùng làm bằng chứng trong phạm vi; nghiệm thu cần DWG/PDF thực tế. |
| Mapping VHGpro độc lập | Giữ mapping trước | Không thực hiện theo chỉ thị chủ nhiệm. |
| Triển khai sản phẩm và native acceptance | 290 hash sản phẩm không đổi; chưa có artifact native mới | NotRun; không tuyên bố zero lỗi. |

[Báo cáo tổng thể](08-DELIVERY-REPORT.html) · [Kiểm tra chi tiết](data/verification-v2.json).
