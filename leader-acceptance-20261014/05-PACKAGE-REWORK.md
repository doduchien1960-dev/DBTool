# Giao trả implementer theo dependency

Đây là yêu cầu sửa sau review, chưa phải thông báo đã sửa. Giữ nguyên UI sản phẩm và các23 phê duyệt. Không reset repo/checkout HEAD theo rollback receipt cũ; trước sửa phải snapshot exact paths và kiểm hash.

| Gói | Phán quyết review | Findings phải xử lý | Gate cụ thể |
|---|---|---|---|
| W01 | Chưa đủ nghiệm thu chức năng | R01 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W02 | Chưa đủ nghiệm thu chức năng | G08, G09, G10, R02 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W03 | Chưa đủ nghiệm thu chức năng | D08, D11 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W04 | Chưa đủ nghiệm thu chức năng | D03, D04, D05, D06, D07, D09 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W05 | Chưa đủ nghiệm thu chức năng | N01, N02, N04, N05, N06, N08, N11 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W06 | Chưa đủ nghiệm thu chức năng | G01, G02, G03, G05, G07, G10, G12 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W07 | Chưa đủ nghiệm thu chức năng | G02, G04, G05, G06, G07, G11, G12, R02 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W08 | Chưa đủ nghiệm thu chức năng | D10, D11 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W09 | Chưa đủ nghiệm thu chức năng | D03, D04, D05, D09, D10, N10 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W10 | Chưa đủ nghiệm thu chức năng | D01, D04, D05, D06, D07, D08, D10, D12 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W11 | Chưa đủ nghiệm thu chức năng | D02, D13 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W12 | Chưa đủ nghiệm thu chức năng | D12 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |
| W13 | Chưa đủ nghiệm thu chức năng | D01, D10, N01, N02, N03, N04, N05, N06, N07, N08, N09, N10, N12 | Đóng mọi finding được gán; receipt mới có source/log/hash và kết quả gate theo behavior, không theo test count. |

Thứ tự sửa khuyến nghị: W01 integrity + W02 source assets/DTO/data → W03 grouping/CSG → W06 effective/geometry → W07 physical steel → W04 planners + W08 shared registry → W09 TK/bending → W10 views/annotations → W11 placement/hash → W12 xác minh replacement → W05 native/evidence ports → W13 composition và native writer. W05 có thể sửa port/evidence song song với core, nhưng không ký integration trước khi đúng payload. Gói trước có lỗi thì không giả lập thành Passed để gói sau chạy.

Mỗi finding cần: Before/After hashes, predicate input, command exit, output/log, expected/actual/unit/tolerance/source, kết luận closed/not-closed. Khi sửa field/algorithm phải đối chiếu 05D/05C và callers; không thay signature âm thầm. Sau đóng lớp nào, chạy lại downstream bị tác động và native mutation scope tương ứng.

R02 là thiếu audit trail, không chứng minh có gian lận hoặc sai logic DTO: 103 schema vẫn khớp. Cần amendment giải thích gap và khóa source build đang dùng. 19 dependency hashes khớp chứng minh receipt liên kết đúng phiên bản file, không chứng minh thứ tự chạy code hoặc mọi gói thực sự hoàn thành.
