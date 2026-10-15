# CHIẾN LƯỢC DỨT ĐIỂM — LEADER CHỊU TRÁCH NHIỆM

Mỗi finding mở có một chẩn đoán chính và một đường xử lý. (a) = năng lực thực thi; (b) = hiểu sai; (c) = cách tiếp cận sai; (d) = spec thiếu/mâu thuẫn. Đường 1 áp dụng cho (a); Đường 2 cho (c)/(d); Đường 3 chỉ cho (b). Không có finding nào được chẩn đoán chắc chắn là (b), nên không chọn Đường 3.

| Finding | Chẩn đoán | Đường | Gói xử lý của leader | Lý do / khác hai vòng trước |
| --- | --- | --- | --- | --- |
| R01 | c | 2 | L01 | Gate đang dùng any-field/optional-version và chỉ kiểm helper; native consumer đi đường khác. Đổi sang canonical typed capture, dùng chung một validator trước mọi Compare. |
| NEW-A03 | c | 2 | L01 | Prefix tên field được xét trước space/frame; đổi thứ tự phân loại thành identity→coordinate basis→physical payload→sheet transform. |
| G08 | c | 2 | L02 | Danh mục glyph cố định, tỷ lệ theo tên Bold và bbox chung không thể chứng minh native text tùy ý/escape/encoding/kerning. Thay bằng đo toàn chuỗi native có font-byte identity; tách inventory khỏi contract sản phẩm đã được duyệt. |
| G09 | c | 2 | L02 | Harness chỉ chọn 2 role nền rồi gọi import, chưa save/reopen. Thay bằng active-feature role plan sinh từ effective input và một native round-trip bắt buộc kiểm từng field. |
| G10 | d | 2 | L06 | Lệnh W02 chỉ sửa model, consumer thuộc W06; yêu cầu A đóng toàn bộ finding trước khi B được mở tạo vòng dependency. Tách local obligations và giao bridge do leader sở hữu trước khi ký GO; không miễn gate tổng. |
| R02 | d | 2 | L07 | Spec yêu cầu actor/time lịch sử nhưng snapshot hiện tại không chứa thông tin đó. Tách current-byte reconciliation khỏi historical attestation; evidence bị mất phải ghi Unverified, không dựng lại lịch sử bằng timestamp. |
| NEW-A01 | a | 1 | L04 | Sau hai vòng, một guard hữu hạn vẫn bỏ 7 trường SourceV2 và chỉ dùng 8 ký tự hash dù lệnh nêu full content hash. Leader nhận việc sửa guard, viết code và kiểm từng trường; không giao lại cùng implementer. |
| D08 | c | 2 | L03 | Flat-loop/global-inner-subtraction và greedy stitching không mô hình hóa complete occupied region. Thay kernel contract và analytic provider, giữ ownership/nesting của từng component. |
| D11 | a | 1 | L05 | Yêu cầu approved + exact scope đã rõ; hai vòng vẫn cho Rejected và blank vào S5. Đây là thiếu năng lực chứng minh tính đầy đủ của guard trong phạm vi nhiệm vụ; leader viết patch và phản ví dụ trực tiếp. |
| NEW-R03-W01-COMPAT | d | 2 | L01 | Native producer và strict parser không có schema/normalization chung. Bổ sung mapping lossless; không mở rộng allowlist độc lập rồi giả định đã tích hợp. |
| R03-W02-N01 | c | 2 | L02 | Thay signature geometry bằng signature metrics làm mất độ nhạy hình học. Khóa canonical entity payload + dependencies dùng chung source/target, không dùng bounds làm content identity. |
| R03-W02-N02 | d | 2 | L02 | Metadata unit không có receipt ràng buộc source bytes→coordinate unit. Bổ sung UnitBinding theo exact bundle, thiếu thì Block; không suy ra feet thật chỉ từ INSUNITS. |
| R03-W02-N03 | c | 2 | L02 | Narrative/manual sample và existence/count được dùng thay acceptance. Đổi sang evidence sinh từ run mới, readback độc lập, fail process khi bất kỳ predicate nào sai. |
| R03-W03-N01 | c | 2 | L03 | Chủ thể toán học phải là miền có outer/holes/islands thuộc component, không phải tập polygon cùng dấu. |
| R03-W03-N02 | c | 2 | L03 | Biến đổi endpoint/center rồi sao chép sweep làm đổi miền arc. Chuyển toàn bộ oriented curve, xác minh point/tangent/domain. |
| R03-W03-N03 | d | 2 | L03 | CurveV2 chưa nói rõ 2π/full circle/multi-turn. Chốt admissible domain trước kernel; thêm equivalent representation gate. |
| R03-W03-N04 | c | 2 | L03 | Có một loop khép kín chưa chứng minh đã tiêu thụ hết boundary. Validate all inputs và all output edges, cấm partial success. |
| R03-W03-N05 | c | 2 | L03 | Round3 tạo grid ẩn lớn hơn tolerance đang khai báo. Dùng tolerance có source và event/winding classification, không rounding tọa độ. |
| R03-W03-N06 | a | 1 | L05 | Cùng nguyên nhân/predicate của D11; gộp xử lý vào patch leader, giữ ID để truy vết; không đếm thành nghĩa vụ sửa độc lập. |

## Đánh giá năng lực và trách nhiệm leader

Không kết luận chung rằng model implementer không có năng lực. Với NEW-A01 và D11, phạm vi guard hữu hạn đã được yêu cầu rõ; hai lần bàn giao vẫn bỏ nhánh bắt buộc và tự ký bằng fixture không phủ nhánh. Leader đánh giá năng lực tự chứng minh tính đầy đủ của chính hai guard này chưa đạt, chọn (a) và viết code trực tiếp. Đây là quyết định kỹ thuật của leader; reviewer chéo ban đầu đề xuất (c). Lý do leader đổi chẩn đoán: sửa hai guard không cần thay kernel/thư viện/schema; tiếp tục giao cùng implementer sẽ lặp lại thất bại đã quan sát.

Với G08, có lỗi triển khai nghiêm trọng và receipt không đáng tin; tuy nhiên, thay thêm tỷ lệ glyph bằng tỷ lệ khác vẫn là ngõ cụt. Leader chọn (c) làm nguyên nhân chính, kèm phần thiếu contract về encoding/font substitution/product approval thuộc (d). Không xếp (a) để rồi giao một bản vá chỉ sửa số A mà giữ kiến trúc giả. Leader sở hữu L02, dùng native đo toàn chuỗi và bằng chứng font byte; không giao một REWORK G08 lần 3 theo cách cũ.

Leader nhận trách nhiệm vì các lệnh trước chưa buộc tất cả consumer dùng chung gate và chưa khóa đầy đủ mô hình occupied region/encoding/unit binding. Không dùng spec thiếu để miễn các yêu cầu đã rõ về missing field, full hash, approval và không bịa số.

| Thứ tự | Lệnh leader | Đầu vào bắt buộc | Đầu ra / gate |
| --- | --- | --- | --- |
| 1 | L04+L05 — Đường 1 | Frozen R03 và patch guard | Payload 3 files, Before/After hash, RED/GREEN, guard apply; chưa áp dụng live. |
| 2 | L01 — Canonical capture | Native producer + diff consumer + expected coverage bên ngoài | Một validator dùng chung; mỗi field mutation phải còn trong diff; malformed không được coi là equality. |
| 3 | L02 — Native assets / measurement / evidence | Exact template + fonts + feature plan + unit binding + product approval | Native imported target save/reopen; independent readback từng field; gồm cả negative cases. |
| 4 | L03 — Complete occupied region | Analytic physical component, ordered operations, sources/tolerances | Kernel port và native provider; bảo toàn void/island/arc/domain; W10/PDF gate. |
| 5 | L06 — Pad bridge | W02 local Closed, consumer W06 legacy + V2 | Leader patch consumer + native pad entity/hatch/datum gate; G10 global vẫn mở đến khi đủ evidence. |
| 6 | L07 — Forensic ledger | Immutable historic bytes + receipt chain + raw transcripts | Current reconciliation + verified history hoặc Unverified gap; không fabricate. |
| 7 | Review lại A tích hợp | L01..L07 actual applied/replayed artifacts | GO chỉ khi mọi requested finding Closed và new blockers Closed; không giảm gate. |

L01/L02/L03 có thể phát triển trên nhánh bản sao riêng sau khi khóa port DTO cùng revision; việc chạy/ký consumer production vẫn bị khóa đến khi gate trước đạt. Không suy diễn rằng pipeline được phép chạy chỉ vì có thể thiết kế các phương án song song. Chi tiết field-level và rủi ro ở [spec thay đổi](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/04-REPLACEMENT-SPECS.html>); bản sửa trực tiếp ở [patch leader](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/03-LEADER-PATCHES.html>).
