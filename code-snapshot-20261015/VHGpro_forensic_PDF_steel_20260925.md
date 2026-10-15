# Tính ngược PDF, bảng thép xuất và file nhập VHGpro 3.2

Ngày kiểm tra: 25/09/2026. Đơn vị kích thước: mm; khối lượng: kg.

## Nguồn và nguyên tắc ghép

- Bản vẽ: `VHGpro 3.2/Demo/Cad`. Bộ loại 3 có các PDF `3.1...Model-1` đến `Model-33`; G400-1 dùng Model-1 đến Model-6, G500-2 dùng Model-7 đến Model-13, G600-3 dùng Model-14 đến Model-20, G800-1 dùng Model-21 đến Model-27. Loại 1 chỉ có DWG/PNG trong thư mục này, không có PDF riêng.
- Bảng thép xuất: đúng ba workbook trong `VHGpro 3.2/Demo/Cad/Thep`, mỗi workbook có bốn sheet G400-1, G500-2, G600-3, G800-1. Số liệu dưới đây đọc từ chính các sheet đó.
- File nhập để kiểm tra sau cùng: ba workbook tháng 11/2025 trong `VHGpro 3.2/Demo/Excel`. Sheet `Thep` của chúng chứa **số cố định** ở các cột khối lượng (ví dụ C5:C8 cùng bằng 57,438365 ở cả ba file), không phải công thức tính lại theo từng ga. Vì vậy so thông số `Ga...` và `CF.Ga` trước; không lấy cache `Thep` làm kết quả hiện hành.

## Công thức kiểm chứng được ở cả 12 sheet thép xuất

Với đường kính tròn `d` mm, số thanh `E`, chiều dài một thanh `F` mm:

```text
G = E × F / 1000                         (m)
H = π × (d / 1000)² × 7850 / 4           (kg/m)
I = G × H                                (kg)
```

Ví dụ G400-1 liền khối, D1 đáy trên: `12 × 1420 / 1000 × 0,616537558267 = 10,505799993 kg`, đúng dòng 5. Kiểm tra từng dòng có dữ liệu của cả 12 sheet cho sai số tính `E×F×H` bằng 0 trong độ chính xác số thực. Thép góc dùng đơn trọng catalogue ở cột H, ví dụ V30x30x3 = 1,36 kg/m.

Các ô M:Y dòng 4 dùng `SUMIFS` theo nhãn nhóm J và đường kính số `≤10` hoặc `>10`; cột Y cộng thép hình. Z4 = SUM(M4:Y4). Đây là **công thức tổng hợp của workbook**, khác phép cộng trực tiếp mọi dòng I khi nhãn J bị trống.

## Tính ngược bản đáy từ bảng thép xuất

Tất cả ba workbook thép xuất có cùng năm dòng bản đáy cho từng mã ga. Với các mẫu này: `tường t=150`, `bảo vệ c=30`, thép D10@150; móc lớp trên `h=t−2c=90`; lớp dưới thanh thẳng. **Phạm vi lưới thép** mà các chiều dài thanh chứng minh là `Sx=L+2t`, `Sy=B+2t`, tức không cộng mở rộng C5 vào chiều dài thanh. Workbook thép một mình không chứng minh kích thước bê tông bản đáy.

```text
D1 trên = Sx − 2c + 2h = L + 420
D2 trên = Sy − 2c + 2h = B + 420
D1 dưới = Sx − 2c = L + 240
D2 dưới = Sy − 2c = B + 240
D3 buộc = 90 + 100 + 200 + 100 + 90 = 580
```

Với bốn cấu hình đã có, số D1 ở mỗi lớp là `floor((Sy−390)/150)+5`; số D2 là `floor((Sx−390)/150)+5`. Số 390 đúng chuỗi biên `2×30 + 2×90 + 2×75`; 75 là nửa bước 150. Đây là quy luật **đã khớp 4/4 mẫu không mở rộng**, và chuỗi kích thước trên PDF loại 3 G600/G800 xác nhận cách chia khoảng dư. Chưa coi nó là công thức tổng quát cho mọi đường kính, bước, vát và mức mở rộng.

| Ga | L, B suy từ F5/F6 | Phạm vi thép `Sx×Sy` | D1/D2 mỗi lớp | Đáy workbook |
|---|---:|---:|---:|---:|
| G400-1 | 1000 × 1250 | 1300 × 1550 | 12 / 11 | 42,65552 |
| G500-2 | 1150 × 1350 | 1450 × 1650 | 13 / 12 | 50,12796 |
| G600-3 | 1200 × 1450 | 1500 × 1750 | 14 / 12 | 54,81068 |
| G800-1 | 1250 × 1550 | 1550 × 1850 | 14 / 12 | 57,66846 |

Số thanh D3 buộc ở bốn mẫu là 12/12/16/20. PDF ghi @300 và chi tiết chiều dài 580; bốn mẫu chưa đủ để xác định duy nhất công thức vị trí/số lượng D3. Không nội suy thành quy tắc thiết kế.

## Tính ngược chiều cao và chiều dài thanh tường nguyên

Ở workbook liền khối, các thanh T1/T2 không bị lỗ cống cắt cho ra cùng quy luật chiều dài với thanh đáy: lớp ngoài có hai móc 90, lớp trong là thanh thẳng, bảo vệ hai đầu 30. T1 theo phương ngang của thành; T2 theo chiều cao thành. Bề cao hình triển khai tường là `H+2t`, với `H` là chiều cao lòng ga.

```text
T1 ngoài = S_tường_ngang − 60 + 180 = chiều_lòng_theo_tường + 420
T1 trong = S_tường_ngang − 60       = chiều_lòng_theo_tường + 240
T2 ngoài = (H + 300) − 60 + 180     = H + 420
T2 trong = (H + 300) − 60           = H + 240
```

Từ T2 trong ở workbook liền khối: G400 = 1640 → **H=1400**; G500 = 1690 → 1450; G600 = 1740 → 1500; G800 = 1790 → 1550. PDF loại 3 G400-1 Model-1 cũng ghi `Htb=1400`, trong khi ba file nhập `Demo/Excel` hiện ghi H=1200 ở hàng G400-1. Các dòng thép bị cắt tại lỗ cống vẫn phải tính theo giao hình học riêng; công thức trên chỉ áp cho thanh nguyên.

## Bản trần: chiều dài thanh chính và thanh bị lỗ nắp chia

Ở workbook liền khối, N1/N2 lớp **trên** là thanh thẳng; lớp **dưới** thêm hai móc 90. Theo kích thước lòng đã suy ra:

```text
N1 trên = L + 240; N2 trên = B + 240
N1 dưới = L + 420; N2 dưới = B + 420
```

Quy luật này khớp 4/4 ga: ví dụ G400-1 là N1 1240/1420 và N2 1490/1670; G800-1 là N1 1490/1670 và N2 1790/1970. Các thanh N1A/N1B/N2A/N2B do lỗ nắp chia cũng dài hơn **180 mm** ở lớp dưới so với đoạn tương ứng lớp trên: G400-1 N1A 395→575, N1B 95→275, N2A/N2B 470→650. Độ dài đoạn thẳng của các thanh này phụ thuộc vị trí và kích thước lỗ mở; phải lấy từ hình cắt, không suy bằng tỷ lệ khối lượng.

## PDF loại 3 và workbook thép xuất không khớp toàn bộ

Các trang PDF Model-5/11/18/25 có bảng chi tiết đáy; Model-2/8/15/22 có kích thước đáy. Dùng chính số thanh, chiều dài in trong PDF để tính lại khối lượng:

| Ga | Bản đáy trong PDF | Thép đáy tính từ PDF | Đáy trong workbook `Thep` | Kết luận |
|---|---:|---:|---:|---|
| G400-1 | 1500 × 1750 | 56,49062 | 42,65552 | Khác phạm vi thép, số thanh và chiều dài |
| G500-2 | 1650 × 1850 | 65,07283 | 50,12796 | Khác phạm vi thép, số thanh và chiều dài |
| G600-3 | 1500 × 1750 | 54,81068 | 54,81068 | Khớp bản đáy |
| G800-1 | 1550 × 1850 | 57,66846 | 57,66846 | Khớp bản đáy |

Ví dụ có thể kiểm tra độc lập: PDF G400-1 Model-5 ghi D1 đáy trên **14 thanh × 1620**, D2 **13 thanh × 1870**; workbook `KL thép mẫu hố ga thăm + thu nắp rời.xlsx`, sheet G400-1, ô E5:F6 ghi **12 × 1420** và **11 × 1670**. Mỗi chiều dài chênh đúng 200 mm. Vì vậy cặp PDF và workbook ở chính `Demo/Cad` **cũng không phải một kết quả đồng nhất cho mọi ga**; không thể ép mã vẽ khớp đồng thời hai giá trị G400-1 đó.

## Đối chiếu với thông số file nhập `Demo/Excel`

| Trường | Bảng thép xuất suy ra | PDF loại 3 | File nhập tháng 11/2025 |
|---|---|---|---|
| G400-1 lòng ga L×B | 1000×1250 | 1000×1250 (Model-1/2) | Loại 1: 1000×1000; loại 2/3: 1000×1100 |
| G400-1 chiều cao lòng H | 1400 từ T2 trong | 1400 (`Htb`, Model-1) | 1200 cả ba loại |
| Mở rộng bản đáy G400/G500 | 0 theo chiều dài thép | 100 mỗi phía theo kích thước PDF | `CF.Ga!C5=100` cả ba loại |
| Mở rộng bản đáy G600/G800 | 0 theo chiều dài thép | 0 theo kích thước PDF | `CF.Ga!C5=100` cả ba loại |
| Cổ ga G400-1 liền khối | Có thép C1, suy ra W≈400 | DWG/PNG, chưa khóa chiều cao từ PDF | `GaThamLienKhoi!W4=0` |

Chế độ móc hiện lưu ở `CF.Ga` (loại 1/2: hàng 20, loại 3: hàng 22) vẫn ghi đáy dưới `0M`, đáy trên `2M`, tường ngoài `2M`, tường trong `0M`, trần trên `0M`, trần dưới `2M`. Các chế độ này giải thích chênh 180 mm giữa thanh có hai móc và thanh thẳng; chúng không phải thay đổi đầu vào cần sửa.

Chiều dài C1 của loại liền khối và nắp rời khớp quy luật mẫu `F(C1)=W+2×dày_cổ−80`: ví dụ loại 1 G500 có W=450, cổ dày 150, F=670; loại 2 G400 W=400, cổ dày 125, F=570. Nếu áp dụng **giả thuyết** này cho workbook thăm+thu, F(C1)=745 ở G400/G500/G600 gợi W=575 với cổ dày125, còn F=595 ở G800 gợi W=375 với cổ dày150. File nhập hiện ghi W=500 cho cả bốn; PDF G400 Model-5 ghi C1=670, hợp với W=500. Cần hình học cổ ga của PDF và các dòng C1A/C2 để xác nhận giả thuyết trước khi đưa vào mã.

## Hai lỗi nội tại trong bảng thép xuất loại 3

- G400-1: tổng mọi dòng chi tiết I = **349,305516**, nhưng Z4 = **343,608709**. Các dòng 22, 24, 25 (T1A, T2A, GC1 của T2 trong) có J trống dù I lần lượt 1,529013; 3,501933; 0,665861. Tổng bỏ sót = **5,696807**.
- G600-3: tổng mọi dòng chi tiết I = **422,693647**, nhưng Z4 = **416,503610**. Các dòng 36, 38, 39 tương tự có J trống; tổng bỏ sót = **6,190037**.

Do đó khi kiểm chứng mã, cần so **dòng thép** và tổng chi tiết độc lập, đồng thời ghi nhận Z4 gốc; không coi Z4 của hai sheet này là tổng khối lượng đúng.

## So tổng workbook xuất với cache `Thep` trong file nhập tháng 11

Đây là so hai kết quả đã lưu, **không phải** kiểm chứng công thức thiết kế vì cột khối lượng trong `Demo/Excel!Thep` là hằng số.

| Loại | G400 xuất / nhập | G500 xuất / nhập | G600 xuất / nhập | G800 xuất / nhập |
|---|---:|---:|---:|---:|
| Liền khối | 321,92 / 282,26 | 370,30 / 278,27 | 388,22 / 285,69 | 390,82 / 295,41 |
| Nắp rời | 339,57 / 302,22 | 379,60 / 302,07 | 399,35 / 307,14 | 408,57 / 323,36 |
| Thăm + thu | 343,61 / 302,22 | 417,74 / 303,61 | 416,50 / 309,90 | 436,55 / 325,88 |

## Quy tắc sử dụng để sửa DBTool

1. Dùng **dòng thép chi tiết** của workbook xuất để kiểm chứng công thức đơn trọng, chiều dài hình uốn và lượng thanh, theo từng cấu kiện.
2. Ghép PDF với workbook theo **giá trị hình học thực** của từng ga và từng cấu kiện. G600/G800 loại 3 khớp đáy; G400/G500 loại 3 không khớp đáy, nên phải giữ hai ca tham chiếu riêng.
3. Dùng `Demo/Excel` để kiểm tra những trường đầu vào đã đổi: bề rộng G400, mở rộng đáy, chiều cao cổ và các cặp cống; không dùng cache khối lượng trong `Thep` để suy công thức.
4. Chưa khóa quy tắc tổng quát cho thép buộc, cắt thép quanh cống/lỗ nắp, thép gia cường, cổ và cửa thu chỉ từ bốn mẫu. Những phần này cần tiếp tục đối chiếu từng hình và từng dòng trước khi sửa engine.
