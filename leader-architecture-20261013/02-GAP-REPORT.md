# 2. Báo cáo lấp bốn lỗ hổng

> Cập nhật leader-field-contracts-20261013-r2: 23/23 đề xuất NR01..23 đã được chủ nhiệm duyệt. Đọc 05/05C/05D bản mới và 07 — Gói việc. Những câu mô tả “chưa chốt” trong trích nguồn là trạng thái lịch sử. Binding nguồn chưa đủ vẫn có lỗi cụ thể theo schema; không đồng nghĩa chủ nhiệm chưa duyệt. Chưa có phase sản phẩm mới được đánh Passed trong lượt hoàn thiện tài liệu này.


13/10/2026. Bàn giao nghiên cứu/kiến trúc. **Không sửa production, không chạy bản sinh DWG mới, không đóng STD hoặc nghiệm thu phần mềm trong lượt này.** Các gate DWG/PDF tại tài liệu06 là công việc triển khai còn phải chạy. Phần hoàn thành của báo cáo là nguồn đã đối soát, phân tích bổ sung, ma trận quyết định và quy trình cụ thể.

## 2.1. Gap (a): bổ sung loại 2 và loại 3

Đã chọn PDF-012 (loại 2) và PDF-040 (loại 3) từ 71 nguồn đã có; không nghiên cứu lại tám PDF loại 1. Đã kiểm SHA256 PDF gốc, đọc native entities/blocks/attrs/dims/routes theo đúng frame candidate, render PDF bằng PDFium và xem riêng toàn trang cùng các crop. Join cũ được giữ nguyên trạng thái, review mới có scope, không nâng toàn bộ ledger thành Certified.

### Loại2 — PDF-012 / G400-1 / CAD-2-F001

Nguồn `2.1.Ga thăm BTCT nắp rời-Model-1.pdf`, SHA256 `f8946eaa2ec712f3e2f76ae01abdb07a64ab6a920e32482d32fbed4fc70db3bb`. [Toàn trang](PDF-012-review.png), [jointAA](PDF-012-joint-AA.png), [steelAA](PDF-012-steel-AA.png), [evidence đầy đủ](data/PDF-012-deep-evidence.json).

Sáu hình theo Structure: top MB/BB/SteelAA; bottom AA/WallMesh/SteelBB. Titles local frame lần lượt X2295/6185/10075, Y8610/4550. Đây là số đo recipe mẫu, không origin của ga mới. Plan clear1000×1250; tường150 -> ngoài thành1300×1550; chân100 -> đáy1500×1750; mở rộng lót100 -> lót1700×1950. Phải phân biệt các đường kích thước theo subject, không gọi mọi rectangle là kích thước thân.

CT mặt trên nắp57.27, đáy trong55.24. Cổ clear700 theo hai section của mẫu nắp vuông này, thành125; roof150; chiều cao trong nguồn1400, cổ chú thích Ctb480. Mẫu này không chứng minh AA luôn700 hay cổ luôn125. Quyết định mới frame100 và catalog clear hiệu lực vẫn ưu tiên; không ép tổng chiều cao/cover mẫu cũ lên input hiện tại.

Hai callout `Vữa XM M75 / Dày15mm` xuất hiện tại joint đỉnh thành–trần, khác vữa cống và khác vật liệu lót. Handle6F9DE → text6F9DF/6F9E0 (AA), 6FBC8 → 6FBC9/6FBCA (BB). Đường joint rời phải còn nét, không union cả roof/body như loại 1. Note Excel G9 nói chiều cao E bao gồm lớp vữa; đây là nguồn cho datum `WallTop=RoofBottom−MortarThickness`.

SteelAA có `Chốt D18` text6FECD, route6FECC; SteelBB text70118, route70117. ThangD18 là đối tượng khác: text70070/route7006F ở SteelAA và6FB8A/6FB89 ở AA cấu tạo. Chốt và thang cùngØ18 không được gộp role hoặc dùng chung quantities. Chốt pair/hole phải dựng vật lý loại 2/3; nguồn tờ này không cung cấp đầy đủ holeDepth/neo hai phía/rounding50, giữ NR06.

Native frame: 24 INSERT,407LWPOLYLINE,69LINE,134DIMENSION,40HATCH,38LEADER,40TEXT. INSERT gồm Khung1,TenMC6,MC1×2,MC2×2,CT8,NapGaV-MB1,NapGaV-MC4. Không BLT/Gach/BLTC/TK trên tờ cấu tạo; không được kết luận thiếu ties hoặc schedule toàn bộ hồ sơ. Phải có tờ lưới/TK theo recipe khác.

Hatch có ANSI32 scale120, AR-CONC12, ANSI37 tại joint trong native source. Đây là số đo của template/demo/source scale1:30, không pattern scale chung cho mọi vật liệu/tỷ lệ. Có LINE thép thật và tầng thép/đáy/trần khác nhau; dấu chấm không là bằng chứng toàn fabrication length.

Đã lưu đủ38routes/40texts và bảng dưới để tìm lại từng callout. Candidate text từ landing được kiểm theo nghĩa role trên full/crops; hai cặp duplicate stroke cổ/vật liệu giữ hai semantic associations. Không tạo fake physical OccurrenceIds cho source.

| Route→text | Nội dung literal | Role | Vertices local mm |
| --- | --- | --- | --- |
| 6F9DE→6F9DF | Vữa XM M75 | JointMortar | [[2995.0, 3122.5], [2890.0, 2995.0], [2440.0, 2995.0]] |
| 6FB80→6FB81 | BTCT M250 đá 1x2 | ConcreteMaterial | [[1982.5, 1655.0], [1982.5, 1000.0], [2558.5, 1000.0]] |
| 6FB82→6FB83 | BT lót M100 đá 2x4 | BeddingMaterial | [[1982.5, 1530.0], [1982.5, 880.0], [2630.5, 880.0]] |
| 6FB89→6FB8A | Thang D18 | Ladder | [[1760.0, 2960.0], [1850.0, 2810.0], [2210.0, 2810.0]] |
| 6FBC8→6FBC9 | Vữa XM M75 | JointMortar | [[6760.0, 7192.5], [6655.0, 7065.0], [6205.0, 7065.0]] |
| 6FD6A→6FD6B | BTCT M250 đá 1x2 | ConcreteMaterial | [[5935.0, 5725.0], [5935.0, 5070.0], [6511.0, 5070.0]] |
| 6FD6C→6FD6D | BT lót M100 đá 2x4 | BeddingMaterial | [[5935.0, 5600.0], [5935.0, 4950.0], [6583.0, 4950.0]] |
| 6FE49→6FE4A | T1 - ngoài | WallFaceLayer | [[5885.0, 3538.0], [5945.0, 3698.0], [6305.0, 3698.0]] |
| 6FE4C→6FE4D | T1 - trong | WallFaceLayer | [[5885.0, 3412.0], [5945.0, 3292.0], [6305.0, 3292.0]] |
| 6FE4F→6FE50 | T3 - ngoài | WallFaceLayer | [[5885.0, 2012.0], [5945.0, 1792.0], [6305.0, 1792.0]] |
| 6FE52→6FE53 | T3 - trong | WallFaceLayer | [[5885.0, 2138.0], [5945.0, 2198.0], [6305.0, 2198.0]] |
| 6FE55→6FE56 | T4 - ngoài | WallFaceLayer | [[5547.0, 2775.0], [5387.0, 2835.0], [5027.0, 2835.0]] |
| 6FE58→6FE59 | T4 - trong | WallFaceLayer | [[5673.0, 2775.0], [5733.0, 2835.0], [6093.0, 2835.0]] |
| 6FE5B→6FE5C | T2 - ngoài | WallFaceLayer | [[6787.0, 2625.0], [6625.0, 2565.0], [6265.0, 2565.0]] |
| 6FE5E→6FE5F | T2 - trong | WallFaceLayer | [[6697.0, 2925.0], [6637.0, 2985.0], [6277.0, 2985.0]] |
| 6FECC→6FECD | Chốt D18 | Dowel | [[9375.0, 7080.0], [9210.0, 7140.0], [8910.0, 7140.0]] |
| 7006F→70070 | Thang D18 | Ladder | [[9540.0, 7030.0], [9630.0, 6880.0], [9990.0, 6880.0]] |
| 70098→70099 | T3 trong | WallFaceLayer | [[9420.0, 5850.0], [9540.0, 5970.0], [9840.0, 5970.0]] |
| 7009A→7009B | T3 ngoài | WallFaceLayer | [[9330.0, 5860.0], [9210.0, 5920.0], [8910.0, 5920.0]] |
| 7009C→7009D | T1 trong | WallFaceLayer | [[10730.0, 5860.0], [10610.0, 5920.0], [10310.0, 5920.0]] |
| 7009E→7009F | T1 ngoài | WallFaceLayer | [[10820.0, 5860.0], [10940.0, 5920.0], [11240.0, 5920.0]] |
| 700A0→700A1 | Đáy trên | BaseLayer | [[9642.5, 5770.0], [9702.5, 5860.0], [10002.5, 5860.0]] |
| 700A2→700A3 | Đáy dưới | BaseLayer | [[9478.333333, 5680.0], [9598.333333, 5400.0], [9898.333333, 5400.0]] |
| 700A4→700A5 | Trần dưới | RoofLayer | [[10550.0, 7230.0], [10430.0, 7050.0], [10130.0, 7050.0]] |
| 700A6→700A7 | Trần trên | RoofLayer | [[10550.0, 7320.0], [10670.0, 7440.0], [10970.0, 7440.0]] |
| 700A8→700A9 | Cổ ga | Neck | [[9350.0, 7550.0], [9230.0, 7610.0], [9020.0, 7610.0]] |
| 700AA→700AB | BTCT M250 đá 1x2 | ConcreteMaterial | [[9350.0, 7550.0], [9230.0, 7610.0], [9020.0, 7610.0]] |
| 70117→70118 | Chốt D18 | Dowel | [[9500.0, 3010.0], [9335.0, 3070.0], [9035.0, 3070.0]] |
| 702B3→702B4 | T4 trong | WallFaceLayer | [[9545.0, 1780.0], [9665.0, 1900.0], [9995.0, 1900.0]] |
| 702B5→702B6 | T4 ngoài | WallFaceLayer | [[9455.0, 1790.0], [9335.0, 1850.0], [9035.0, 1850.0]] |
| 702B7→702B8 | T2 trong | WallFaceLayer | [[10605.0, 1790.0], [10485.0, 1850.0], [10155.0, 1850.0]] |
| 702B9→702BA | T2 ngoài | WallFaceLayer | [[10695.0, 1790.0], [10815.0, 1850.0], [11115.0, 1850.0]] |
| 702BB→702BC | Đáy trên | BaseLayer | [[9705.0, 1700.0], [9765.0, 1790.0], [10095.0, 1790.0]] |
| 702BD→702BE | Đáy dưới | BaseLayer | [[9561.666667, 1610.0], [9681.666667, 1330.0], [10011.666667, 1330.0]] |
| 702BF→702C0 | Trần dưới | RoofLayer | [[9725.0, 3160.0], [9845.0, 2980.0], [10145.0, 2980.0]] |
| 702C1→702C2 | Trần trên | RoofLayer | [[9725.0, 3250.0], [9605.0, 3370.0], [9305.0, 3370.0]] |
| 702C3→702C4 | Cổ ga | Neck | [[9775.0, 3480.0], [9655.0, 3540.0], [9445.0, 3540.0]] |
| 702C5→702C6 | BTCT M250 đá 1x2 | ConcreteMaterial | [[9775.0, 3480.0], [9655.0, 3540.0], [9445.0, 3540.0]] |

### Loại3 — PDF-040 / G500-2 / CAD-3-F010

Nguồn `3.1.Ga thăm + thu BTCT nắp rời-Model-10.pdf`, SHA256 `c14f625be67bdae88e19892be6185f2604ed65c00a155742465da2f17d9bdd82`. [Toàn trang](PDF-040-review.png), [catchplan](PDF-040-catch-plan.png), [1-1](PDF-040-catch-11.png), [2-2](PDF-040-catch-22.png), [neckAA](PDF-040-neck-AA.png), [evidence đủ](data/PDF-040-deep-evidence.json).

Đây là NeckCatch: ba hình cổ ở trái, cửa thu MB/1-1/2-2 ở cột kế; TK không ở tờ này. Không lấy PDF039 liền trước làm numerical bundle vì PDF039 CT57.39, PDF040 CT58.56. Source candidate G500-2 phải giữ frame/hash riêng. Muốn kiểm TK.CT cần đúng bundle/source độc lập, không ghép số theo Modelordinal; NR08 vẫn mở cho topology/fabrication binding CT.

Cổ trên tờ này clear700×700, ngoài950×950, thành125; CT58.56 ởAA/BB. Nhãn cổ có C1/C1A/C1B/C1C,C2/C2A/C2B/C2C,C3 vàGC1. Hậu tốC ở cổ không chứng minh cống sinhT1C. Annotation trong source AA dày/chồng; DBTool giữ semantic và làm sạch ink, không copy va chữ.

Cửa thu: clear plan500×550, thành125, ngoài750×800; section1-1 clear500 và cao250, đáy125,lót100; section2-2 theo phươngclear550. Dimensions78BCD/78BD1/78BDE là125/500/125, tổng750 ở78BEC;78B8D/78B9B/78BA8 là125/550/125,total800 ở78BB6. Không lấy530/960 trong tên grille làm kích thước lỗclear.

Marks1..4 trong vùng catch Ø10@150 là dữ liệu mẫu: plan có6BLT (mark1 hai phía,2,3 hai phía,4);1-1 có3BLT(1/2/4);2-2 có4BLT(1/2/4 hai phía). Mẫu thể hiện role theo mặt cắt/nhóm, không mộtmark=toànga hoặc mộtleader/mỗithanh. Đường uốn đỏ/điểm cắt phải từ physical catch mesh một lớp theo M17 và support, không dùng các dots/sourcecoords làm occurrences DBTool. Map hoàn chỉnh shape 1..4 tới TK.CT còn NR08.

Native frame có91INSERT,219LWPOLYLINE,54LINE,115DIMENSION,3HATCH, không LEADER. 43BLT=29BLT1+14BLT2,17Gach,6TenMC,2CT; không BLTC trong tờ này. Tuyến dẫn dùng LINE/LWPOLYLINE, nhãn có thể đặt giữa tuyến. Đã kiểm43/43 insertion points nằm trên route sau OCS;31/43 trùng endpoint. Đây là chứng minh association2D, chưa là member/path/fabrication proof.

**Phát hiện nguồn mới:** SCR960x530 handle78AC7 scale[1,0.6470588235294118,1],rotation180°. NAPCUATHU1 handle78C05 scale1. NAPCUATHU2 handle78D2D scale[0.71875,1,1]. Native product geometry bị biến dạng theo hai views nguồn; không nhập các hệ số này vào code biến thiên. Bề rộng section2 sauco=960×0.71875=690mm; đó không phải clear550mm. NR09 yêu cầu chọn sản phẩm tương thích/catalog extension có nguồn hoặc chốt ngoại lệ schematic rõ, giữ physical sản phẩm thật.

**Phát hiện nghiên cứu mới:** polyline78DD3 có extrusion≈(0,0,−1); raw X âm nhưng bounds WCS dương. Nếu chỉ sửa INSERT OCStoWCS như audit cũ, tuyến trái của nhãn78DD9 sẽ bị báo mất sai. Đợt này đã chuyển riêng LWPOLYLINE qua ezdxf.OCS khi lập path_records của evidence mới, rawsource không đổi. Handle78DD1/78DD2 là duplicate stroke bên phải, không xóa label4 bên trái; cần dedup stroke nhưng giữ target associations.

Bảng sau ghi43 label → route và attrs. `distance` là số đo nguồn đến segment, không là dung sai acceptance tự đặt; cực đại≈7.1e−15mm. 1e−6 trong script chỉ lọc equal-coordinate evidence theo numerical policy hiện có, không trở thành source rule kết cấu.

| BLT handle | SH / DK / A | Route / vị trí trên tuyến | Label local mm |
| --- | --- | --- | --- |
| 785FE | {'A': '@150', 'DK': '%%c10', 'SH': 'C1C'} | 785FD endpoint / error=0.0 | [1845.0, 7235.0] |
| 78605 | {'SH': 'C2C', 'DK': '%%c10', 'A': '@150'} | 78604 endpoint / error=0.0 | [2020.0, 7235.0] |
| 7860C | {'A': '@150', 'DK': '%%c10', 'SH': 'C1B'} | 7860B endpoint / error=0.0 | [2345.0, 6645.0] |
| 78613 | {'A': '@150', 'DK': '%%c10', 'SH': 'C2B'} | 78612 endpoint / error=0.0 | [2345.0, 7000.0] |
| 78690 | {'A': '@150', 'DK': '%%c10', 'SH': 'C1'} | 7868F endpoint / error=0.0 | [1854.0, 6935.0] |
| 78698 | {'SH': 'C3', 'DK': '%%c6', 'A': '@300'} | 78697 endpoint / error=0.0 | [2020.0, 7316.0] |
| 7870B | {'SH': 'C1A', 'DK': '%%c10', 'A': '@150'} | 7870A endpoint / error=0.0 | [2645.0, 6645.0] |
| 78721 | {'SH': 'C2', 'DK': '%%c10', 'A': '@150'} | 78720 endpoint / error=0.0 | [2011.0, 7075.0] |
| 78731 | {'SH': 'C2A', 'DK': '%%c10', 'A': '@150'} | 78730 endpoint / error=0.0 | [2645.0, 7000.0] |
| 7875B | {'A': '@150', 'DK': '%%c10', 'SH': 'C1A'} | 7875A endpoint / error=0.0 | [1810.0, 4715.0] |
| 78762 | {'SH': 'C1', 'DK': '%%c10', 'A': '@150'} | 78761 endpoint / error=0.0 | [2880.0, 4715.0] |
| 78769 | {'SH': 'C2A', 'DK': '%%c10', 'A': '@150'} | 78768 endpoint / error=0.0 | [2055.0, 4715.0] |
| 78770 | {'A': '@150', 'DK': '%%c10', 'SH': 'C2'} | 7876F endpoint / error=0.0 | [2635.0, 4715.0] |
| 7877B | {'SH': 'C2B', 'DK': '%%c10', 'A': '@150'} | 7877A endpoint / error=0.0 | [2165.0, 4678.5] |
| 78787 | {'A': '@150', 'DK': '%%c10', 'SH': 'C2C'} | 78786 interior / error=7.105427357601002e-15 | [2635.0, 4942.25] |
| 78791 | {'A': '@150', 'DK': '%%c10', 'SH': 'C1B'} | 78790 interior / error=0.0 | [1810.0, 4753.5] |
| 787FD | {'SH': 'C1B', 'DK': '%%c10', 'A': '@150'} | 787FC interior / error=0.0 | [2880.0, 4753.5] |
| 78803 | {'SH': 'C1C', 'DK': '%%c10', 'A': '@150'} | 78802 interior / error=0.0 | [2880.0, 5042.25] |
| 7880C | {'SH': 'C3', 'DK': '%%c6', 'A': '@300'} | 7880B endpoint / error=0.0 | [2757.5, 4310.0] |
| 78813 | {'SH': 'GC1', 'DK': '%%c10', 'A': 'SL2'} | 78811 endpoint / error=0.0 | [2055.0, 4916.0] |
| 788DE | {'A': '@150', 'DK': '%%c10', 'SH': 'C1'} | 788DD endpoint / error=0.0 | [1810.0, 2445.0] |
| 788E5 | {'SH': 'C1', 'DK': '%%c10', 'A': '@150'} | 788E4 endpoint / error=0.0 | [2880.0, 2445.0] |
| 788EC | {'SH': 'C2', 'DK': '%%c10', 'A': '@150'} | 788EB endpoint / error=0.0 | [2165.0, 2445.0] |
| 788FF | {'SH': 'C2B', 'DK': '%%c10', 'A': '@150'} | 788FE endpoint / error=0.0 | [2165.0, 2408.5] |
| 7890A | {'SH': 'C2C', 'DK': '%%c10', 'A': '@150'} | 78909 endpoint / error=0.0 | [2165.0, 2672.25] |
| 7897C | {'SH': 'C1B', 'DK': '%%c10', 'A': '@150'} | 7897B interior / error=0.0 | [2880.0, 2483.5] |
| 78982 | {'A': '@150', 'DK': '%%c10', 'SH': 'C1B'} | 78981 interior / error=0.0 | [1810.0, 2483.5] |
| 78988 | {'A': '@150', 'DK': '%%c10', 'SH': 'C1C'} | 78987 interior / error=0.0 | [1810.0, 2772.25] |
| 7898E | {'SH': 'C1C', 'DK': '%%c10', 'A': '@150'} | 7898D interior / error=0.0 | [2880.0, 2772.25] |
| 78997 | {'SH': 'C3', 'DK': '%%c6', 'A': '@300'} | 78996 endpoint / error=0.0 | [2757.5, 2040.0] |
| 78B22 | {'SH': '1', 'DK': '%%c10', 'A': '@150'} | 78B21 endpoint / error=0.0 | [5736.5, 7770.0] |
| 78B28 | {'SH': '1', 'DK': '%%c10', 'A': '@150'} | 78B27 endpoint / error=0.0 | [5736.5, 6610.0] |
| 78B75 | {'SH': '2', 'DK': '%%c10', 'A': '@150'} | 78B74 interior / error=0.0 | [6180.0, 7190.0] |
| 78B7B | {'SH': '3', 'DK': '%%c10', 'A': '@150'} | 78B7A endpoint / error=0.0 | [6180.0, 7650.0] |
| 78B81 | {'SH': '3', 'DK': '%%c10', 'A': '@150'} | 78B80 endpoint / error=0.0 | [6180.0, 6730.0] |
| 78B88 | {'SH': '4', 'DK': '%%c10', 'A': '@150'} | 78B86 endpoint / error=0.0 | [6189.0, 6905.0] |
| 78CCA | {'SH': '1', 'DK': '%%c10', 'A': '@150'} | 78CC9 endpoint / error=0.0 | [5736.5, 4275.0] |
| 78CD1 | {'A': '@150', 'DK': '%%c10', 'SH': '2'} | 78CCF endpoint / error=0.0 | [5995.0, 4805.0] |
| 78D1F | {'SH': '4', 'DK': '%%c10', 'A': '@150'} | 78D1E interior / error=0.0 | [6180.0, 4742.5] |
| 78D31 | {'A': '@150', 'DK': '%%c10', 'SH': '1'} | 78D2F endpoint / error=0.0 | [6110.0, 2800.0] |
| 78D81 | {'SH': '2', 'DK': '%%c10', 'A': '@150'} | 78D80 endpoint / error=0.0 | [5835.0, 2300.0] |
| 78DD4 | {'SH': '4', 'DK': '%%c10', 'A': '@150'} | 78DD1 interior / error=0.0 | [6295.0, 2767.5] |
| 78DD9 | {'A': '@150', 'DK': '%%c10', 'SH': '4'} | 78DD3 interior / error=0.0 | [5375.0, 2767.5] |

### Quy tắc mới đủ rõ và phần vẫn cần chốt

- Loại2 giữ joint/mortar thật, chốt khác thang, source heights theo datum; thêm required Dowel/Hole roles và cùngpairID.
- Loại3 cần NeckCatch+ScheduleOnly, ownerBody/Neck đúng frame, catch một lớp khi BTCT; không chỉ thêm chữ “cửa thu” vào loại 2.
- Native source/polyline OCS theo entity, annotation có thể ở giữa collector; endpoint-only audit không đầy đủ.
- Distorted catch product là source conflict NR09; full map shape 1..4/TK.CT NR08; chốt length/hole depth NR06; aliases cổ NR10. Không claim các NR đã được giải bởi ảnh.

Phân tích sâu hai tờ đã bổ sung nội dung triển khai, tọa độ/handles/dims/routes/style và source conflicts theo yêu cầu. Chưa kiểm chứng mọi glyph/vector curve hay toàn bộ fabrication vật lý; những phần đó là acceptance/implementation có gate trong06, không báo software đã đạt.

## 2.2. Gap (b): “337 Excel notes còn lại”

Đã đối soát `EXCEL-NOTES.html` ↔ `author-notes-verbatim.json` ↔ **comments trong cả ba XLSX gốc**, 528/528 exact text. Số theo workbook166/173/189. Có191text khác nhau sau Trim;337 occurrence còn lại lặp các text đó, không phải 337 nội dung mới. Đây là điều chỉnh phạm vi theo bằng chứng, không bỏ337 note.

[Bảng191 rule và hợp đồng](01-KNOWLEDGE.md), [ledger528](data/excel-528-reconciliation.json), [kiểmcommentgốc](data/workbook-comment-verification.json). Mỗi occurrence giữ workbook/sheet/note cell/value-cell candidates/old mapping/override/new EX rule. Một text giống nhau có thể khác scope (NeckStructure/CatchStructure), nên triển khai không chỉ dedup text rồi map một field.

Đã sửa cách hiểu các candidate nguy hiểm: R2 mô tả cả4 cặp O:V, không chỉInvertT2/PipeT3; CF.Ga!L16 loại 3 nói cấu trúc cửa thu dù text copy “cổ”; prefix value cùng row 21/23; type3 chỉ shift vùng dưới Catch thêm 2 hàng; lifting I9 chỉloại 2/3. Toàn191 implementation contracts có trong01; KL HG/THKL giữ quantityscope rõ, không dùng làm authoritygeometry.

**Không tuyên bố consumer của 528 ghi chú đã chạy đúng.** 528 địa chỉ candidate không chứng minh output. 05A xác định field/type/unit/source,03 chỉ module/caller,06 yêu cầu trace đến DWG/PDF. Gap về diễn giải note được lấp; gap implementation được chuyển thành nhiệm vụ có acceptanceartifact cụ thể.

## 2.3. Gap (c): toàn bộ module và dependencies

Đã inventory 7 production projects/290 file C#,14 project definitions kể cảverification,10 trách nhiệm mới. [Ma trận4cột](03-MODULE-MATRIX.md), [JSON](data/module-matrix.json), [code hash/symbol/dependency/callsite index](data/code-inventory.json). `bin/obj/TestOutput/archive` là build/evidence/snapshots, không module runtime mới; giữ nguyên không xóa.

File được loại khỏi runtime có caller cập nhật, replacement và gate. Constants view IDs còn consumer phải được giữ khi loại storemethods. Legacy Core/Cad/IO giữ compatibility island tới migration parity, không kết luận dead từ một search. Inventory lexical không tự chứng minh runtime reachability hay toàn bộ thuật toán đúng; đường active/dead quan trọng đã đọc trực tiếp và nêu trong03.

## 2.4. Gap (d): một NeedsRule register

Đã hợp nhất **23 đề xuất đã được chủ nhiệm chấp thuận** có scope/source/proposal/blockedstage/approvalfields tại [04-NEEDSRULE](04-NEEDSRULE.md), JSON `decision=null`. Có 60 đoạn HTML nhắc NeedsRule/thiếu rule được đối soát disposition; conflict đã có owner override giữ ở resolution log thay lặp trong openlist. Technical NotRun/native glyph pending không biến thành câu hỏi domain.

23 mục gồm cover basis, C/bends, arbitrary ties/component shape, haunch, chốt/lifting/catch, source product scale, aliases/multi-openings, rounding/mass/template/font/hatch/plot/stack/complexpipe/collision/layout và scope khóa clear cho BTCT tự thiết kế. Phần quyết định mới phát hiện từ phân tích/coderead được ghi riêng, không tự chốt vì muốn đủ tài liệu.

## 2.5. Findings mới từ tiếp nhận, chưa phải lỗi đã sửa

| ID | Bằng chứng hiện tại | Quyết định kiến trúc/gate |
| --- | --- | --- |
| PL-01 | Family21ManholeHelper.cs ResolveFromGeometry/ResolveFromPackage/GetSheet* còn suy kích thước/name và tọa độ preset; callers placement trong codeindex | Loại lookup khỏi runtime theo03; sharedRecipeContext+InkLayout; rename/perturbgeometrygate |
| PL-02 | PaletteDrawingCalculation.Presentation fixed30,Symbolic; row error trả fail toàn batch | Shared request BodyScale/ElevationMode; row receipts, không dùng default để bỏ ExcelAbsolute |
| PL-03 | CalculationPolicy tất cả RuleBinding ExperimentalSoftwareFixture, SourceRefs=[] và unresolvedVHG equivalence | RuleApprovalRegistry+NR19/18; không promote experimental bằng đổi status |
| PL-04 | SourcePDF040 SCR/NAPCUATHU2 nonuniform scales;polyline78DD3 OCSnormal−Z | NR09 product mismatch; research path correction từng entity, không cáo buộc DBTool thiếu route |
| PL-05 | Comments 528 exact trong XLSX;191 unique/337 duplicate. Candidate field mapping có quá hẹp/copy text sai scope | Ledger 528 +contracts 191 +05A; consumerstatus tách khỏi meaning status |

## 2.6. Minh bạch điều chỉnh phạm vi

1. Đổi “rút 337 rule mới” thành đối soát337 duplicate occurrence và bổ sung191 implementation contracts: nguồn chứng minh337 không phải unique unread.
2. Thêm 2 tờ deep loại 2/3 đúng yêu cầu mới; không tái phân tích63 tờ như điều kiện cũ cấm, chỉ hai tờ cần thiết và guide liên quan.
3. Thêmfield schema 05A và machine JSON/manifest/callsite ledger để junior có địa chỉ cụ thể. Không thay production code, không phát sinh pipeline sản phẩm thứ hai.
4. Giữ quantity Excel writer ngoài ưu tiên nhưng bảo tồn toàn bộ notes và nativeTK quantities; không bỏ góc ngách bằng cách xóa khỏi hồ sơ.
5. Thêm NR23 về scope khóa clear cho BTCT tự thiết kế để không tự lấyraw cổ làm authority khi rule owner chọn nắp trước chưa có selected aperture contract.
6. Browser file:// bị security policy chặn; đọc HTML nguyên văn từ filesystem theo A–E. Không workaround browser. HTML bàn giao lưu để người dùng tự mở trực tiếp trên máy.

Không dùng source object count/test count làm nghiệm thu. Hồ sơ này hoàn thành bàn giao kiến trúc/nghiên cứu có NeedsRule công khai; **DBTool chưa được xác nhận zero lỗi**. Chỉ phase06 với actual DWG/PDF/evidence mới đóng sản phẩm.
