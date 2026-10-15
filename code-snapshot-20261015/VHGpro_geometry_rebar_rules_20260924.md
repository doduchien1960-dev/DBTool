# Quy ước hình học và cốt thép VHGpro 3.2 — bản đối chiếu nguồn

Ngày đối chiếu: 24/09/2026. Đây là đặc tả kiểm chứng trước khi sửa tiếp mã vẽ. `HDSD VHGpro 3.2.pdf` có 33 trang; đã đọc đủ 33 trang. Nguồn Excel được kiểm tra ở chế độ chỉ đọc. Không xem những quy tắc chưa có chứng cứ là công thức của VHGpro.

## 1. Thứ tự nguồn và giới hạn phiên bản

1. HDSD: ý nghĩa các ô, hướng T1–T4, lựa chọn cấu tạo và quy tắc công bố.
2. Workbook Excel tương ứng loại ga: giá trị nhập, cấu hình, catalog cống, công thức thống kê.
3. DWG/PDF Demo/Cad: tọa độ và cách thể hiện thực tế của một lần chạy VHGpro.
4. VHG-Template.dwg/PDF: tên, hình dáng, layer, block, dimstyle, hatch và các tag.

Không ghép số đo của PDF Demo/Cad với workbook Demo/Excel chỉ vì cùng tên `G400-1`: ví dụ PDF/HDSD G400-1 có lòng ga 1000×1250, cao 1400 mm, còn workbook `2.0...4.11.2025.xlsx` dòng 4 có lòng ga 1000×1100, cao 1200 mm. Bảng `KL thép G400-1.xlsx` còn có cửa thu, nên cũng chưa được chứng minh là cặp nguồn của PDF loại 2. Cần cặp golden cùng revision để nghiệm thu tuyệt đối.

## 2. Dữ liệu nền và hệ tọa độ

| Quy ước | Nguồn | Mức chắc chắn |
|---|---|---|
| T1 phía 12 giờ, T2 phía 3 giờ, T3 phía 6 giờ, T4 phía 9 giờ trên mặt bằng. | HDSD tr. 7, 10 | Đã xác nhận |
| Cột C: chiều ngang thông thủy; D: chiều đứng thông thủy; E: chiều cao trong lòng, không tính cổ. | HDSD tr. 6–7 | Đã xác nhận |
| Mỗi thành chỉ xét **có cống/không có cống**. Cặp ô kích thước và chiều sâu đặt cống phải cùng trống khi không có cống. Không phân loại cống vào/ra. | HDSD tr. 7; yêu cầu người dùng | Đã chốt |
| Tâm cống theo phương ngang ở giữa thành nếu độ lệch cấu hình bằng 0. Nhìn từ trong ga ra ngoài: lệch trái dương, lệch phải âm. | HDSD tr. 16–17 | Đã xác nhận |
| Gốc tính cao độ tương đối đặt tại **mặt đáy trong lòng ga** (`z=0`); `AA` là cao độ tuyệt đối của mặt này nếu chọn “Tuyệt đối”. | Suy từ nghĩa “đáy ga” và PDF; cần kiểm tra trên golden | Suy luận có bằng chứng |

Mọi kích thước cấu tạo trong các bảng Excel và khoảng cách sắp xếp bản vẽ theo mm; cao độ tuyệt đối ghi theo mét. Chưa lấy `$INSUNITS` của một DXF trung gian làm bằng chứng duy nhất về đơn vị chèn block: cần kiểm tra trên DWG thực và kích thước đo được.

## 3. Quy ước cấu tạo có công thức hoặc phép cộng trực tiếp

| Đại lượng | Quy tắc / nguồn Excel | Mức chắc chắn |
|---|---|---|
| Kích thước ngoài thân ga | `(C + 2I) × (D + 2I)` với `I` là chiều dày tường. HDSD xác nhận C,D là kích thước trong và I là dày tường; phép cộng được DWG mẫu kiểm tra ở ga G400. | Đã đối chiếu mẫu |
| Kích thước bản đáy | Kích thước ngoài thân ga cộng `2 × CF.Ga!C5` mỗi phương; `CF.Ga!D5` là vát góc trong. | HDSD tr. 8–9; cần kiểm tra từng biến thể |
| Kích thước lớp lót | Kích thước bản đáy cộng `2 × CF.Ga!C3` mỗi phương; dày lót là `CF.Ga!D3`; dày 0 thì không vẽ. | HDSD tr. 8; cần kiểm tra từng biến thể |
| Mặt dưới bản đáy | `z = −F` (`F` là chiều dày bản đáy). | Suy từ mặt đáy trong lòng; cần kiểm DWG |
| Mặt dưới lớp lót | `z = −F − CF.Ga!D3`. | Suy từ cấu tạo xếp lớp; cần kiểm DWG |
| Đáy trong trần ga nắp rời | `z = E`. **E đã bao gồm chiều dày vữa đệm** `CF.Ga!G9`; vì vậy đỉnh bê tông tường là `E − G9`, lớp vữa từ `E − G9` đến `E`. Không cộng vữa thêm một lần vào chiều cao thông thủy. | HDSD tr. 10, mục 2.3.5; đã đối chiếu mặt cắt mẫu |
| Đỉnh bản trần | `z = E + L`, với `L` là dày trần. | Suy từ cấu tạo; đã đối chiếu ca mẫu |
| Đỉnh cổ | `z = E + L + W`, với `W` là cao cổ nếu có cổ. `W=0` thì không dựng thân cổ. | HDSD tr. 7; cần kiểm các kiểu vát |
| Đỉnh ga/nắp | Với nắp Gang không vát và hình học mẫu: `z = E + L + W + CF.Ga!D13`. Vát cổ/vát nắp hoặc nắp BTCT đòi hỏi nhánh riêng theo HDSD tr. 10–12. | Chỉ xác nhận cho nhánh nắp Gang đơn giản |
| Cao độ ghi tuyệt đối | `Z = AA + z/1000`, nếu `CF.Ga!C32 = Tuyệt đối` ở workbook loại 1/2. Loại 3 có hàng cấu hình dịch xuống. Nếu “Tương đối”, dùng ký hiệu cấu hình thay số. | HDSD tr. 17 + quy đổi mm→m |

Ví dụ đối chiếu cho hình G400-1 trong HDSD/PDF: `AA=55.24 m`, `E=1400`, `L=150`, `W=400`, cao nắp Gang `80` mm ⇒ đỉnh thiết kế `55.24+(1400+150+400+80)/1000=57.27 m`. Đây là ví dụ hình mẫu, **không phải** dòng 4 của workbook Demo/Excel 2025.

### Cống trên từng thành

- HDSD tr. 7 định nghĩa ô chiều sâu đặt cống là **khoảng cách từ đáy cống đến đáy ga**. Với `h` mm do người dùng nhập: `Z_đáy_cống = AA + h/1000` nếu dùng cao độ tuyệt đối. `h=250` hoặc `300` chỉ là giá trị nhập/mặc định có thể cấu hình, không phải hằng số của VHGpro. Dòng 4 workbook loại 2 có T2 `200`, T3 `150`, T4 `100` mm; dòng 4 loại 3 có T2 `150` mm.
- Công thức mép **lỗ khoét** từ kích thước trong cống, chiều dày thành cống (`CF.Cong`) và vữa trát (`CF.Ga`) **không được HDSD công bố đủ**. Không khóa công thức `kích thước trong + 2 × thành + 2 × vữa` trước khi đo DWG cùng revision. Cống hộp và cống đôi là nhánh riêng; cống đôi có thêm tường chia khoang (HDSD tr. 18–19).
- Nếu có kích thước cống nhưng thiếu chiều sâu hoặc ngược lại, đây là cặp dữ liệu chưa đầy đủ; không tự gán `0` hay 250/300 mm.

### Cổ, nắp và cửa thu

- Vị trí cổ có 9 lựa chọn: Giữa T1–T4, Góc T1-T2/T2-T3/T3-T4/T4-T1, Giữa ga. Kích thước nhập `BxL` theo ngang×đứng trên mặt bằng (HDSD tr. 10). Vát cổ, vát nắp, nắp Gang và nắp BTCT là các lựa chọn khác nhau; block nắp Gang dùng cùng tiền tố tên với hậu tố `.MB`, `.AA`, `.BB`, không tự scale block (tr. 11–12).
- Cửa thu chỉ có ở ga thăm+thu. `CF.Ga!C17:M17` gồm: vị trí thân/cổ, hướng, kiểu, lỗ rộng×cao, buồng dài×cao, dày tường, dày đáy, lót, mở rộng lót, kết cấu, thép. HDSD tr. 13 chỉ hỗ trợ `Kiểu 1`. Lỗ mặc định sát đỉnh thành hoặc cổ; bề rộng trong buồng bằng bề rộng lỗ. Trái/phải được nhìn từ trong ga ra ngoài. Với cửa thu ở cổ, lỗ ở giữa thành cổ dù chọn trái/phải. Chưa có công thức đầy đủ cho toàn bộ biên dạng cửa thu trong HDSD; phải đo DWG loại 3.

## 4. Quy tắc cốt thép đã được công bố

| Hạng mục | Quy tắc xác nhận từ HDSD |
|---|---|
| Chuỗi thép | `DxxAyyy`: đường kính `xx` mm, bước/mật độ `yyy` mm (tr. 7). Thép đứng tường lấy từ sheet ga. Thép ngang tường/cổ có bước bằng bước đứng cộng hiệu chỉnh “Thép ngang” trong CF.Ga; “Giảm thép” giảm cấp đường kính theo catalog, ví dụ D12 giảm 1 cấp thành D10 (tr. 9, 11). |
| Lưới | “Chia đều”: khoảng cách đều, không vượt bước đã nhập. “Theo mật độ”: giữ đúng bước nhập, chia đều phần dư hai bên, không tính thanh ở chỗ giao hai tường vào phần dư (tr. 14). HDSD không nêu rõ công thức xử lý biên/đếm thanh khi kích thước không chia hết. |
| Dạng thanh | `0M` thẳng, `1M` một móc, `2M` hai móc. Chọn độc lập cho đáy, tường, trần, cổ và từng lớp (tr. 14). |
| Chiều dài móc | “Theo KC”: dày kết cấu trừ bê tông bảo vệ **hai bên**. “Theo ĐK”: hệ số `n × đường kính D`; `n` ở cấu hình “Số lần ĐK” (tr. 14). |
| Thép buộc | Có thể bố trí “Đều” hoặc “Lệch”; hình C/Z/U theo từng cấu kiện. Đường kính và bước từ CF.Ga. Chiều dài móc hai đầu C, hai đầu Z/U, giữa U là ba tham số riêng (tr. 15). |
| Chiều dài nối | `nD` cho thép bo lỗ cống tròn, lỗ cổ chữ nhật và đai cổ; `n` tại “L nối thép” (tr. 15). |
| Gia cường cống | Cống hộp: bo viền, số lượng cấu hình 1 ↔ 4 thanh/1 lớp, 2 ↔ 8 thanh/1 lớp. Cống tròn có Bo/Xiên/Vuông và tổ hợp; số lượng 1 ↔ 1 bo + 4 xiên nếu chọn xiên, 2 ↔ 1 bo + 8 xiên nếu chọn xiên. Độ mở rộng bị chặn bởi lớp bảo vệ mép ngoài (tr. 16). |
| Gia cường lỗ thăm | Bo 1 thanh nếu chọn; “Số lượng” chỉ áp dụng thép Vuông; độ mở rộng cũng bị chặn bởi lớp bảo vệ (tr. 17). |
| Thang | Vị trí theo T1–T4 hoặc vị trí cổ. Mật độ `AxB`: A từ đỉnh ga tới thang đầu, B giữa các thang. Kích thước `MxHxB`: móc hai bên, chiều sâu, chiều rộng (tr. 15). |
| Chốt trần nắp rời | Số lượng từ kích thước ga và bước chốt; khoảng cách chốt được làm tròn đến 50 mm. HDSD không nêu công thức đếm cụ thể (tr. 9–10). |
| Cửa thu BTCT | Mặc định một lớp thép; đường kính và mật độ theo `CF.Ga!M17` (tr. 13). |

### Công thức khối lượng đã thấy trực tiếp trong workbook kết quả

Trong `KL thép G400-1.xlsx`, sheet `G400-1`, các hàng thống kê dùng:

`G_i = F_i × E_i / 1000` (tổng chiều dài thép, m);

`H_i = π × (D_i/1000)^2 × 7850 / 4` (khối lượng riêng thép tròn, kg/m);

`I_i = H_i × G_i` (kg).

Thép hình lấy khối lượng riêng tra sẵn từ `CF.Data`, không áp công thức thép tròn. Sheet thống kê dùng `SUMIFS` theo đường kính và nhóm cấu kiện để phân `D≤10`/`D>10`, sau đó cộng tổng. Các cột `E_i` (số thanh) và `F_i` (chiều dài một thanh) là **giá trị đầu ra**, không chứa công thức truy ngược hình học trong file mẫu. Vì thế chưa thể nói HDSD đã cung cấp “toàn bộ công thức tính thép”.

## 5. Những công thức phải đo/kiểm chứng trước khi mã hóa

1. Kích thước lỗ khoét cống thực, kể cả vữa trát, vát cống hộp, cống đôi và vị trí lỗ trên hai mặt thành.
2. Quy tắc đếm thanh chính xác ở góc/giao tường, hai chế độ lưới, vùng bị cắt bởi lỗ cống/lỗ cổ, chiều dài neo và hình dạng sau cắt.
3. Biên dạng vát cổ/vát nắp, cao độ “đỉnh lọt lòng” cho tất cả nhánh nắp rời, liền khối, BTCT, Gang và trường hợp `W=0`.
4. Bố trí chốt trần và cách làm tròn 50 mm; thép gia cường Vuông; thép buộc tại biên/lỗ; chiều dài gia công tính cung uốn nếu có.
5. Biên dạng và thép cửa thu Kiểu 1 ở thân/cổ, trái/giữa/phải; chi tiết cửa thu trên tờ phụ.
6. Ghép đúng workbook cùng revision với DWG/PDF vàng để so tọa độ, số thanh, chiều dài, khối lượng từng dòng. Chỉ sau khi qua phép đối chiếu này mới khóa công thức dùng trong code.

### Bổ sung đo DWG mẫu sau khi đọc HDSD

Đã xuất **bản sao DXF chỉ đọc** từ hai DWG mẫu `2.1.Ga thăm BTCT nắp rời.dwg` và `3.1.Ga thăm + thu BTCT nắp rời.dwg` bằng AutoCAD 2026. Các số dưới đây là tọa độ tương đối của **khung đầu G400-1**, đơn vị hình học trong bản vẽ. Tọa độ này chứng minh cách vẽ ở đúng ca mẫu; chưa tự mở rộng thành quy tắc cho mọi ga.

| Thành phần mặt bằng | Loại 2 | Loại 3 | Nhận xét |
|---|---:|---:|---|
| Viền bản đáy trên `VHG-BT` | 1500×1750, bốn `LINE` | 1500×1750, bốn `LINE` | **Có hiển thị ở mặt bằng.** Bản DBTool hiện tại đã bỏ viền này; cần sửa lại. Lớp lót 1700×1950 không vẽ viền ở mặt bằng. |
| Viền ngoài thân `VHG-BT` | 1300×1550 | 1300×1550 | Cùng kích thước; nằm trong viền bản đáy. |
| Lòng trong `VHG-Dashed` | 1000×1250 | 1000×1250 | Nét khuất. |
| Lỗ cổ `VHG-Dashed` | 700×700 ở góc T2–T3 | 700×700 ở giữa T2 | Vị trí cổ thay đổi theo cấu hình. |
| Cửa thu trên mặt bằng | Không có | Viền ngoài 625×800; viền lòng 625×550 trên `VHG-Dashed`; block cửa `SCR960x530` | Cửa thu của **ca PDF G400-1** nằm ở cổ hướng T2. |

Ở mặt cắt B-B của ca loại 3, cửa thu nằm bên phải cổ, buồng trong dài 500 mm, cao trong 250 mm; tường cuối dày 125 mm, bản đáy dày 125 mm, lớp lót dày 100 mm và mở rộng 50 mm. Lỗ qua thành cổ nằm sát đỉnh cổ. Chiều dài ngoài từ mép trong liên kết tới tường cuối là 625 mm = 500 + 125. Mặt bằng có block `SCR960x530` tại vị trí cửa thu; mặt cắt dùng block `NAPCUATHU1`. Ca workbook Demo/Excel loại 3 hiện lại ghi `CF.Ga!C17=Thân ga`, `D17=Phải T1`, `F17=850x250`, `G17=500x250`, nên **không thể dùng workbook này để kiểm đúng các tọa độ cửa thu trong PDF G400-1**.

Ở hai mặt cắt **cốt thép**, golden vẫn giữ hatch lớp lót `AR-CONC` và hai hatch vữa đệm `ANSI37`, nhưng không có hatch thân/bản trần/cổ `ANSI32`. DBTool hiện đang hatch bê tông các mặt cắt cốt thép; đây là sai khác rõ ràng cần sửa. Mặt cắt kết cấu thường có hatch bê tông.

Đã xuất thêm DWG loại 1 (ga thăm BTCT liền khối) ra DXF đọc độc lập. Khung đầu có cùng viền bản đáy 1500×1750, thân ngoài 1300×1550, lòng trong 1000×1250 trên mặt bằng. Mặt cắt loại 1 **không có vữa đệm**; trần liền với tường và góc trong tường–trần có vát 100×100 mm khi `CF.Ga!C9=100`. Ở tâm khoang, mặt dưới trần phẳng là `z=E`; gần tường mép dưới của phần vát thấp hơn 100 mm. Như vậy “cao đỉnh lọt lòng” trong ca liền khối được đo tới phần trần phẳng giữa khoang, không đo tới điểm thấp nhất của phần vát ở hai mép. Cả loại 1 và 2 đều có hatch `AR-CONC` lớp lót trong mặt cắt cốt thép; loại 1 không có hatch `ANSI37` vì không có vữa đệm.

**Sửa chứng cứ chân ga theo phản hồi của người dùng:** DXF của cả ba loại G400-1 đều cho thấy bản đáy có mặt dưới phẳng đủ bề rộng và hai mép ngoài thẳng đứng; `CF.Ga!D5=100` chỉ tạo vát 100×100 mm tại **nách trong chân tường**. Bản DBTool trước 25/09/2026 vát nhầm hai góc ngoài phía dưới, làm mặt dưới hẹp đi 200 mm. Hình học trong `GeometryEngine` đã bỏ vát ngoài, giữ vát trong; DXF thử nghiệm mới xác nhận mặt dưới từ `−HalfBase` đến `+HalfBase` ở `z=−F`.

## 6. Mốc kiểm tra cho DBTool

- Trước mọi thay đổi tiếp theo, đối chiếu từng trường Excel với source cell, kiểu ga và đơn vị; không tự suy cống vào/ra.
- Tạo fixture riêng, không sửa workbook/DWG gốc; dùng tối thiểu một ca cống trên từng hướng, một ca không cống, một ca cổ ở góc, một ca cửa thu.
- Đối chiếu theo thứ tự: hình học/nét lớp → layer/block/tag → thép từng thanh → bảng thép → PDF ở tỷ lệ in.
- Với các điểm mục 5 còn thiếu bằng chứng, đánh dấu `chưa xác minh` trong thiết kế và không hard-code như quy tắc VHGpro.

## Nguồn đã đọc

- `VHGpro 3.2/HDSD VHGpro 3.2.pdf`, đủ trang 1–33, đặc biệt tr. 6–19, 27–32.
- Ba workbook `VHGpro 3.2/Demo/Excel/{1.0,2.0,3.0}...4.11.2025.xlsx`, sheet ga, `CF.Ga`, `CF.Cong`, `CF.Data`.
- `VHGpro 3.2/KL thép G400-1.xlsx`, sheet `G400-1` và `TH`.
- PDF/DWG đầu ra cùng template trong `VHGpro 3.2/Demo/Cad` và `VHGpro 3.2/Template`, theo kiểm tra hình học của lượt làm việc hiện tại.
