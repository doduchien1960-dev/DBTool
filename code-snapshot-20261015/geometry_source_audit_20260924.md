# Đối chiếu hình học, layer và block VHGpro 3.2

Ngày kiểm tra: 2026-09-24. Chỉ đọc và đối chiếu nguồn; không sửa code hoặc DWG gốc. Kích thước dùng mm, cao độ dùng m.

## Nguồn

- Template/VHG-Template-Model.pdf và Template/ACAD-VHG-Template.dxf: hình block, tên layer, định nghĩa block và tag.
- HDSD VHGpro 3.2.pdf tại thư mục gốc (trùng nội dung bản trong Helps; đối chiếu SHA-256), đủ 33 trang: ý nghĩa các ô và quy ước công cụ.
- Ba workbook 1.0/2.0/3.0 ngày 04.11.2025: ô dữ liệu và công thức sheet KL HG.
- 60 PDF trong Demo/Cad. Đã rà nội dung text toàn bộ, xem ảnh các tờ đại diện loại 2 và 3. Demo/Cad không chứa PDF loại 1, nên xem thêm ReferenceVHGpro_20260924/reference_sheet1.pdf được in từ bản sao DWG mẫu.
- DXF tham chiếu: DBTool/TestOutput/BatchCheck_roofclip_20260924/VHGpro_G400_sample_copy.dxf; dùng để xác nhận layer thật của entity và block INSERT.

## Không được ghép sai revision

| Loại G400-1 | Workbook hiện có | PDF mẫu |
|---|---|---|
| Liền khối | GaThamLienKhoi C4:E4 = 1000 × 1000 × 1200; W4=0; CF.Ga E11=820×570 | Tờ tham chiếu: lòng 1000 × 1250; Htb 1400; Ctb 500; cổ lòng 700 |
| Nắp rời | GaThamNapRoi C4:E4 = 1000 × 1100 × 1200; W4=400 | Demo Model-1: lòng 1000 × 1250; Htb 1400; Ctb 480 |
| Thăm + thu | GaThamThuNapRoi C4:E4 = 1000 × 1100 × 1200; W4=500 | Demo Model-1: lòng 1000 × 1250; Htb 1400; Ctb 600 |

Vì vậy PDF demo cho thấy quy ước trình bày nhưng không thể dùng số PDF G400-1 để nghiệm thu trực tiếp workbook 2025 hiện có.

## Catalog layer đã xác nhận

| Layer | Chứng cứ và vai trò |
|---|---|
| VHG-BT | Đường bao bê tông: chủ yếu LWPOLYLINE, CIRCLE. |
| VHG-VL | Hatch vật liệu; mẫu có 294 HATCH trên layer này. |
| VHG-Thep | Thép chính; nhiều LINE/LWPOLYLINE và ký hiệu Gach. |
| VHG-Thep2 | Thép/phần biểu diễn thứ hai; Gach tại tờ 2–3. |
| VHG-Thep3 | Thép phụ DASHED, color 6 trong template; nhiều BLTC tờ 2–3. |
| VHG-Thep4 | Nét phụ DASHED, color 8; có 140 LINE trong mẫu. |
| VHG-Dashed | Nét khuất, ký hiệu MC1/MC2 ở tờ 1. |
| VHG-Center | Tim, có 33 LINE trong mẫu. |
| VHG-Dim | DIMENSION, LEADER, Khung, BLT1/BLT2 và đa số dòng TK.*. |
| VHG-Text | TEXT/MTEXT, TenMC, TieudeMC, tên nhóm bảng. |
| VHG-140 | Đường kẻ bên trong block bảng TK.* của template. |
| VHG-150 | Có trong template và bản mẫu; vai trò chi tiết từng entity còn phải kiểm tra trên DWG. |
| VHG-Nap1..4 | Layer bên trong định nghĩa block nắp. |
| VHG-Cong | Có 110 entity trong DXF bản vẽ tham chiếu, nhưng không thấy trong DXF template đã xuất. Cần xác minh trực tiếp DWG template. |

VHG-Khung không có trong DXF template hay bản vẽ tham chiếu. Cả 5 INSERT Khung đầu của G400-1 nằm trên VHG-Dim. Metadata INSUNITS của DXF template là 2, của bản tham chiếu là 1, trong khi tọa độ khung và kích thước trình bày theo mm: không tự scale theo metadata này.

## Block, tag và vị trí

| Block | Tag trong DXF template | Vị trí dùng trong 5 tờ G400-1 |
|---|---|---|
| Khung | Không có ATTDEF | 1 INSERT mỗi tờ, layer VHG-Dim. |
| TenMC | TIEUDE, TYLE | Tên view và tỷ lệ; 6 ở tờ 1, 6 ở tờ 2, 6 ở tờ 3, 3 ở tờ 4; VHG-Text. |
| TieudeMC | TIEUDE | 14 tiêu đề nhóm thép tờ 5; VHG-Text. |
| BLT1/BLT2 | SH, DK, A | Ghi chú số hiệu, đường kính, bước; VHG-Dim. |
| BLT3/BLT4 | NOTE | Ghi chú kiểu 2 theo HDSD trang 14. |
| Gach | Không có tag | Vạch lưới trên VHG-Thep/Thep2 tờ 2–3. |
| BLTC | Không có tag | Thép buộc, thường trên VHG-Thep3, đôi khi VHG-Thep. |
| MC1..MC4 | Không có tag | Ký hiệu mặt cắt; layer INSERT phụ thuộc view. |
| CT | CT | Ký hiệu cao độ. |
| TK.Tieude/TK.Tenluoi | Không tag/TENLUOI | Đầu bảng và tên lưới. |
| TK.L0M/L1M/L2M | SH, D, L, DAI, SL, TDAI, KL; dạng móc thêm LT/LP | Dòng bảng theo hình dạng thanh, chủ yếu VHG-Dim tờ 4. |
| TK.THKL | TONG, DAY1/2, TUONG1/2, TRAN1/2, CO1/2 | Tổng bảng tờ 4. |
| TK.THKLNAP, TK.THKL.CT, TK.CT.N/T | Tag riêng | Nắp và cửa thu tờ phụ. |
| Tên nắp từ CF.Ga E13 | Hậu tố .MB/.AA/.BB | Ba block mặt bằng và mặt cắt; HDSD trang 11 và PDF template xác nhận. |
| NAPCUATHU1/2 | Chưa kiểm tag | Tài nguyên cửa thu trong template và mẫu loại 3. |

Layer của INSERT và layer entity bên trong block là hai cấp khác nhau. Không được suy một cấp từ cấp còn lại.

## Sai khác DBTool hiện tại

- TemplateManager tạo VHG-Khung, LayoutArranger chèn Khung lên đó. Mẫu chèn trên VHG-Dim.
- Sheet4Drawer chèn dòng TK.* lên VHG-Text; mẫu chủ yếu dùng VHG-Dim. Sheet5Drawer chèn BLT1 lên VHG-Text; mẫu dùng VHG-Dim.
- TemplateManager tự đặt VHG-Thep3 color 3/continuous, trong khi template là color 6/DASHED; thiếu VHG-Thep4, VHG-150, VHG-140.
- Kế hoạch cũ giả định Khung có thuộc tính; DXF template cho thấy Khung không có ATTDEF.
- VHG-Cong có trong bản vẽ tham chiếu nhưng không thấy trong DXF template; chưa khóa cách nạp layer này.

## Excel → hình học đã có nguồn

| Đối tượng | Ô nguồn | Ý nghĩa/phép tính |
|---|---|---|
| Lòng ga | Sheet ga C, D | C ngang, D dọc mặt bằng (HDSD trang 6). |
| Chiều cao lòng nhập | E | Với nắp rời, E bao gồm vữa đệm (trang 10). |
| Chiều dày đáy, tường, trần | F, I, L | Ba đại lượng riêng. Thân ngoài = (C+2I) × (D+2I). |
| Bản đáy | CF.Ga C5, D5 | C5 mở mỗi bên từ mép ngoài tường, D5 vát chân. |
| Lớp lót | CF.Ga C3, D3 | C3 mở mỗi bên từ mép bản đáy, D3 dày. Ví dụ mẫu: 1300 → 1500 → 1700. |
| Trần liền khối | CF.Ga C9 | Vát trần. |
| Trần nắp rời | CF.Ga C9:I9 | Chốt, vữa G9, móc nâng; trừ vữa đúng một lần khi dựng chiều cao tường. |
| Cổ/nắp | CF.Ga C11:M11, C13:K13; sheet ga W,X | Vị trí, lòng cổ, vát, dày cổ, cao cổ, cao nắp và block nắp. |
| Cống T1–T4 | O:P, Q:R, S:T, U:V | Mã cống và khoảng cách đáy cống so với đáy ga. T1=12h, T2=3h, T3=6h, T4=9h. |
| Catalog cống | CF.Cong B:C, D:H | Tròn: D trong và thành. Hộp: rộng, cao, thành, vát, cống đôi. |
| Vữa/gia cường lỗ cống | CF.Ga C28:H28 loại 1/2, C30:H30 loại 3 | C=50 là vữa trát; G=250 là độ mở rộng thép gia cường, không phải cao cống. |
| Lệch tim cống | CF.Ga I28:L28 loại 1/2, I30:L30 loại 3 | Giá trị có dấu, 0 đặt giữa tường. Trái/phải theo hướng từ trong ga nhìn ra. |
| Cửa thu loại 3 | CF.Ga C17:M17 | Vị trí, hướng, kiểu 1, lỗ khoét, lòng cửa, tường/đáy/lót, thép. |
| Hatch/ghi chú | CF.Ga hàng 34/36 loại 1/2, hàng 36/38 loại 3; CF.Data D:E | Tên hatch và scale phải đọc từ workbook. |

## Cao độ: công thức trong chính workbook

KL HG!D8 của cả ba loại: D8 = E8 + Q8 + T8 + U8 + CF.Ga!D13/1000.
E8 lấy AA4 (cao độ đáy ga). T8 lấy L4/1000 (dày trần). U8 lấy W4/1000 (cao cổ).
Q8 loại 1 = E4/1000. Q8 loại 2/3 = (E4 - CF.Ga!G9)/1000.

Do đó:

- Liền khối: Z đỉnh hoàn thiện = AA + (E + L + W + cao nắp)/1000.
- Nắp rời: Z đỉnh hoàn thiện = AA + (E - vữa đệm + L + W + cao nắp)/1000.
- Đỉnh bê tông tường của ga nắp rời = AA + (E - G9)/1000. Mặt dưới tấm trần qua lớp vữa đệm = AA + E/1000, nếu E đúng là chiều cao lòng bao gồm vữa theo HDSD trang 10. Hai mốc này không được gộp làm một. Ga liền khối không có vữa đệm: đỉnh lọt lòng/mặt dưới trần = AA + E/1000.

Workbook hiện tại cho G400-1: 56,690 m (liền khối), 57,025 m (nắp rời), 57,125 m (thăm+thu). PDF demo lần lượt thể hiện 57,29 / 57,27 / 57,39 m. Trên PDF demo, 55,24 + Htb 1,40 + trần 0,15 + Ctb 0,50/0,48/0,60 = 57,29/57,27/57,39. Ctb in trên tờ là chuỗi chiều cao cổ+nắp; workbook tách W và CF.Ga D13.

## Đáy cống và cao độ nhập

Quyết định mới nhất của người dùng: T1–T4 chỉ là bốn thành; trên mỗi thành chỉ xác định **có cống hay không có cống**, không phân loại cống vào/ra. Cao độ đáy cống có thể nhập cho thành có cống; nếu hỗ trợ nhập cao độ tuyệt đối thì cần phân biệt với khoảng cách tương đối trong workbook. Chưa triển khai code. Không tự lấy mặc định 250–300 mm khi chưa có quy ước được xác nhận.

HDSD trang 7: P/R/T/V là khoảng cách từ đáy cống đến đáy ga. Nếu AA là đáy trong ga thì cao độ đáy trong cống thể hiện trên PDF là AA + h/1000. Tại Demo/Cad loại 3 Model-7, G500-2: đáy ga 56,36; D500 56,64 (chênh 280 mm, khớp R5), D200 56,68 (chênh 320 mm, khớp V5). D400 trên PDF là 57,06 (chênh 700 mm) nhưng workbook hiện tại P5=350: thêm bằng chứng khác revision.

Không có bằng chứng VHGpro mặc định h=250–300 mm. Workbook có h=150, 200, 280, 320, 350, 700 mm và ô trống. P4 trống trong khi O4=D200 ở loại 1 cần diagnostic, không tự biến thành 0 hoặc 250. Giá trị CF.Ga G28=250 là mở rộng thép lỗ cống, không liên quan cao độ cống.

Nếu muốn chế độ nhập cao độ đáy cống tuyệt đối, phép đổi là h = 1000 × (Z đáy cống - Z đáy ga). Chế độ và đơn vị phải tường minh. Đỉnh lòng cống tròn = đáy lòng + D trong; biên ngoài dùng thêm thành cống từ CF.Cong, lỗ mở/vữa xử lý riêng. Cống hộp không quy thành cống tròn.

## Các quy ước khác từ HDSD

- Cổ cao 0 không vẽ thân cổ. Cửa thu nằm ở đỉnh tường/cổ theo cấu hình; trái/phải nhìn từ trong ga ra; chỉ kiểu 1 được tài liệu xác nhận (trang 13).
- Thép ngang tường/cổ có thể tăng bước hoặc giảm cấp so với thép đứng bằng CF.Ga C7:D7 hoặc L11:M11; không dùng mặc định cố định (trang 9, 11).
- Rải lưới Chia đều: bước thực không lớn hơn bước nhập. Theo mật độ: giữ bước nhập và chia đều khoảng dư hai biên (trang 14).
- Móc Theo KC: chiều dày kết cấu trừ bảo vệ hai bên. Móc Theo ĐK: n×D (trang 14). Thang lấy vị trí, Ø, mật độ bậc, kích thước uốn từ CF.Ga, không hardcode 500/300.
- Theo trang 16, số lượng gia cường cống hộp 1 = bốn thanh bao lỗ; 2 = tám thanh. Cống tròn 1 = một vòng bo và bốn xiên nếu bật; 2 = một vòng bo và tám xiên nếu bật.

## Cổng trước khi chỉnh code

1. Có một bộ vàng ghép đúng workbook, DWG và PDF từ cùng lần chạy. Các nguồn hiện tại không đủ để nghiệm thu từng con số của G400-1.
2. Theo quyết định mới nhất, chỉ ghi nhận có/không có cống trên T1–T4. Với thành có cống, dữ liệu đặt cống lấy theo cặp mã cống và khoảng cách đáy cống–đáy ga; nếu cho phép nhập cao độ đáy cống tuyệt đối thì chuyển đổi tường minh. Hai ô cùng trống nghĩa là không có cống; một ô thiếu là dữ liệu chưa đầy đủ. Không nhận 250–300 mm là mặc định của mẫu.
3. Xác nhận datum của AA và mặt đáy trong; kiểm Z mặt dưới trần, đỉnh cổ, đỉnh nắp bằng một DWG cùng revision.
4. Xác minh trực tiếp DWG template về layer VHG-Cong, đơn vị, block động, mọi tag và dimstyle trước khi sửa TemplateManager.
5. Kiểm một ga không vuông, cống lệch tim, cống hộp và ga thu để khóa dấu trục, lỗ mở và giao cắt; những quy tắc chưa có chứng cứ phải để trạng thái chưa xác minh.

Chỉ sau khi ô nguồn → biến chuẩn → công thức → đối tượng CAD đều đối chiếu đạt trên bộ vàng mới sửa drawer và geometry engine.

## Đọc bổ sung toàn bộ HDSD VHGpro 3.2 (33 trang)

Quy ước nguồn: các mục dưới đây phân biệt rõ điều tài liệu **nói trực tiếp**, công thức **đọc được trong workbook**, và thuật toán **chưa được công bố**. Không xem số ví dụ trong ảnh HDSD là giá trị mặc định bắt buộc.

### Mô hình dữ liệu và hình học được tài liệu xác nhận

| Trang | Quy tắc |
|---|---|
| 6–7 | C/D/E là dài ngang, rộng dọc mặt bằng, cao lòng không tính cổ; đáy/tường/trần có bề dày và thép riêng. T1/T2/T3/T4 là tường 12h/3h/6h/9h, mỗi tường có cặp mã cống và khoảng cách từ đáy cống đến đáy ga. Không có cống thì xóa cả hai ô. Cao độ đáy ga nhập riêng; có thể dùng cao trình tương đối. |
| 8–9 | Lót mở rộng khỏi bản đáy về hai phía; bản đáy mở rộng khỏi mép ngoài tường; có vát chân tường. Trần liền khối có vát giữa trần và tường. |
| 9–10 | Trần đúc sẵn có chốt, lỗ chốt, vữa đệm, móc nâng. Chiều cao lòng E **bao gồm vữa đệm**; số chốt và khoảng cách do chương trình tính theo kích thước và mật độ, khoảng cách làm tròn tới 50 mm. Tài liệu không nêu thuật toán đếm chốt. |
| 10–12 | Cổ ga có vị trí giữa/cạnh/góc, lòng B×L, vát, thép góc, neo; cao cổ bằng 0 thì không vẽ cổ. Cao nắp tính từ đỉnh cổ (dưới vát nếu có) đến đỉnh ga. Nắp gang chèn block mặt bằng và hai mặt cắt theo hậu tố .MB/.AA/.BB, không tự scale block theo lòng cổ; nắp BTCT có số tấm, khe, hướng, thép và móc nâng. |
| 13 | Cửa thu thân/cổ chỉ có kiểu 1; lỗ mặc định sát đỉnh tường/cổ; hướng trái/phải nhìn từ trong ra. Lòng cửa thu có rộng bằng rộng lỗ; dài/cao, tường/đáy/lót nhập riêng; BTCT có một lớp thép. |
| 16–19 | Cống tròn lấy đường kính lòng và dày thành; cống hộp lấy rộng/cao lòng, dày thành, vát, tùy chọn vách giữa. Dịch tim cống theo phương ngang: nhìn từ trong ra, trái dương, phải âm, 0 giữa tường. Cao trình tuyệt đối lấy đáy ga nhập và tính các mốc; tương đối in ký hiệu. |
| 24–26 | Có tùy chọn bật/tắt vẽ thép, xuất chiết tính, chọn tỷ lệ tự động theo kích thước ga và nhóm khung, khoảng cách bố trí mặt cắt tính bằng mm. |
| 29–32 | Dữ liệu template nằm trong Htemplate; có thể đổi hình/layer/màu nhưng giữ tên block và tag. Không đổi tên layer, dimstyle, text style. Hatch phụ thuộc template AutoCAD. |

### Quy tắc thép được tài liệu xác nhận

| Trang | Quy tắc |
|---|---|
| 7, 9, 11 | Thép lưới nhập DxxAyyy. Thép ngang tường/cổ có thể thay bước bằng bước đứng + giá trị điều chỉnh. Giảm thép 1 cấp ví dụ D12 xuống D10; không có bảng chuyển cấp tổng quát. |
| 9–13 | Chốt trần, thép góc cổ và neo, lưới nắp BTCT, thép cửa thu, lớp bê tông bảo vệ theo từng cấu kiện đều lấy từ cấu hình. |
| 14 | Thanh 0M thẳng, 1M một móc, 2M hai móc. Chia đều: bước thực không lớn hơn bước nhập. Theo mật độ: giữ đúng bước và chia dư hai mép. Móc Theo KC = dày kết cấu − bảo vệ hai mặt; Theo ĐK = n × đường kính. |
| 15 | Thép buộc bố trí đều/lệch, hình C/Z/U, đường kính và bước nhập riêng, các đoạn móc nhập riêng. Nối vòng gia cường, lỗ cổ và đai cổ dài nD. Bậc thang: AxB là khoảng từ đỉnh tới bậc đầu và bước bậc; MxHxB là kích thước uốn. |
| 16–17 | Gia cường lỗ cống tròn: Bo/Xiên/Vuông hoặc kết hợp; cống hộp chỉ bo cạnh. Số lượng 1/2 tương ứng 4/8 thanh bao lỗ hộp; vòng bo tròn là một thanh và 4/8 thanh xiên khi chọn. Mở rộng bị giới hạn bởi lớp bảo vệ. Lỗ thăm: Bo/Vuông/kết hợp; thanh bo mặc định một, số lượng chỉ áp dụng thanh Vuông. |
| 27–28 | Chương 4 hướng dẫn xuất chiết tính thép rồi copy tổng hợp vào sheet Thep; KL HG là công thức Excel có thể chỉnh sửa, không phải mô tả thuật toán dựng hình. |

### Công thức đã thấy trong workbook xuất thép

Tại tệp KL thép G400-1.xlsx và các tệp mẫu trong Demo/Cad/Thep, cột E là số thanh, F là chiều dài một thanh (mm), D là đường kính (mm), G là tổng dài (m), H là khối lượng đơn vị (kg/m), I là khối lượng (kg):

- G = E × F / 1000.
- H = π × (D/1000)^2 × 7850 / 4.
- I = G × H.
- Sheet TH tổng hợp theo cấu kiện và hai nhóm D ≤ 10, D > 10 qua SUMIFS/SUM. Đây là **công thức khối lượng từ số thanh và chiều dài đã có**, không chứng minh được cách phần mềm tạo E/F.

### Các khoảng trống phải đối chiếu thêm để tái hiện chính xác

HDSD là tài liệu thao tác, không cung cấp toàn bộ công thức đếm thanh, điểm bắt đầu/kết thúc lưới, chiều dài cắt từng dạng 0M/1M/2M, quy tắc cắt thép qua lỗ cống/cửa thu, bố trí thép ở góc giao tường, quy tắc làm tròn chiều dài và khối lượng, số chốt cụ thể hay hình học chi tiết nắp/cửa thu. Các giá trị E/F trong tệp chiết tính là kết quả do phần mềm ghi ra, không phải công thức dựng ra chúng. Cần so một bộ Excel đầu vào, DWG/PDF và tệp chiết tính xuất ra từ **cùng một lần chạy** theo từng biến thể; chỉ khi từng thanh và từng hình khớp mới có thể nói bắt chước tuyệt đối.
