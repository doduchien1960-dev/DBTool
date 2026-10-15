# Đối chiếu báo cáo review với bản vẽ VHGpro

Nguồn review: `C:\Users\DucBao\Desktop\Dbtool\Bao_Cao_Doi_Chieu_VHGpro_va_Ke_Hoach_DBTool.docx`.
Nguồn kiểm chứng: ba DWG mẫu VHGpro, DXF xuất từ các DWG đó và code DBTool hiện tại.

## Nhận xét đúng và đã sửa

- Tờ 1 thiếu block cao độ `CT`: đã thay ký hiệu tự vẽ bằng `CT` với thuộc tính `CT`, tỷ lệ 0.3. DXF test có đúng 8 block CT trong khung G400-1; vị trí và giá trị khớp mẫu loại 3.
- Mặt bằng tờ 1 thiếu ký hiệu `MC1` và `MC2`: đã chèn 2 block mỗi loại. Tọa độ G400-1 loại 3 khớp DWG mẫu: MC1 `(2270,-1200)`, `(2270,-3610)`; MC2 `(1365,-2255)`, `(3525,-2255)` so với góc khung.
- Thiếu ghi chú vật liệu: đã đọc từ các trường `MaterialBase`, `MaterialPad` và đặt ở hai mặt cắt kết cấu; sửa chuỗi kích thước chân ga sang 7 đoạn + 1 kích thước phủ bì để không chồng chữ.
- Thiếu block thép góc cổ ga: đã chèn `ThepGocLoi`/`ThepGocLom`; mẫu loại 3 có 12/6 block và các vị trí/góc xoay trong DXF test khớp DXF gốc.
- Thép hình V trong Sheet 4 đang dùng sai `TK.VG`: đã chuyển sang `TK.TG`, map `L=V30x30x3`, `D=1.36kg/m` cho fixture; DXF test xác nhận hàng GN1 xuất đúng block và thuộc tính.
- Mã cống trên mặt cắt đang ghi ở `VHG-Cong`: đã chuyển text sang `VHG-Text`.

## Nhận xét không khớp bằng chứng

- Báo cáo nói tờ 1 mẫu chỉ có 3 hình và yêu cầu chuyển 3 hình thép sang tờ khác. DWG/PDF mẫu có **6 hình** theo bố cục 2 hàng x 3 cột. Giữ bố cục 6 hình.
- Báo cáo nói thiếu hoàn toàn ánh xạ `TK.M.U`, `TK.M.Z`, `TK.GC.H`, `TK.GC.T`: `Sheet4Drawer.ChooseRowBlockName` đã ánh xạ các hình này trước review. Không sửa phần ánh xạ đó.
- Báo cáo mô tả `MC1`/`MC2` có thuộc tính `TIEUDE`, `TYLE`; định nghĩa block thực tế không có ATTDEF. Chèn block trực tiếp.
- Các tuyên bố “khớp 100%” về toàn bộ schedule, cắt thanh và hình học trong báo cáo chưa được chứng minh bởi đối soát từng thanh với workbook vàng. Không dùng làm tiêu chí hoàn thành.

## Việc còn lại của mục tiêu chính

- Cốt thép trong hai mặt cắt tờ 1 còn khác rõ so với mẫu: nét rải, thép đai, mũi tên và chú thích.
- Mặt bằng lưới thép tường trên tờ 1 còn sơ lược.
- Cửa thu gắn ở **thân ga** chưa có mẫu DWG cùng thông số Excel để xác nhận toàn bộ nét cắt tại tường; vị trí gắn cổ T2 đã khớp đường bao, lớp lót, block song chắn và nắp cửa thu trong mẫu loại 3 G400-1.
- Chưa đối soát đầy đủ từng loại hố ga và schedule thép với file VHGpro vàng.

## Báo cáo review lần 2 và nguồn thép mẫu

Đã đọc `C:\Users\DucBao\Desktop\Dbtool\Bao_Cao_Doi_Chieu_VHGpro_va_Ke_Hoach_DBTool_Lan_2.docx`. Khi đối chiếu các PDF/DWG trong `VHGpro 3.2\Demo\Cad`, nguồn workbook thép mẫu là **`VHGpro 3.2\Demo\Cad\Thep`**. File `Demo\Excel\1.0...xlsx` là dữ liệu đầu vào riêng, không phải workbook thép xuất của các bản vẽ mẫu.

- Báo cáo lần 2 tiếp tục coi Tờ 1 chỉ có ba hình; DWG/PDF mẫu có sáu hình. Không chuyển bố cục theo nhận xét này.
- Báo cáo yêu cầu năm bậc thang cho G400-1 và nêu công thức `floor((H−H_dau)/Pitch)+1`. Với các giá trị chính báo cáo nêu (`H=1200`, `H_dau=500`, `Pitch=300`), biểu thức cho kết quả 3, không phải 5. Hơn nữa file nhập `Demo\Excel` có `W4=0`, còn mẫu xuất trong `Demo\Cad\Thep` thể hiện cấu tạo cổ và năm bậc. Chưa có cặp đầu vào/đầu ra cùng cấu hình để đổi công thức.
- Báo cáo coi nhóm “Thang thép” bị loại khỏi khối lượng tường. `ScheduleEngine.AccumulateSummary` hiện đã gộp nhóm chứa `thang` vào thép tường D>10; kiểm tra nội bộ xác nhận tổng chi tiết bằng tổng hợp.
- Workbook mẫu G400-1 tại `Demo\Cad\Thep` có thép đáy tổng 42,66 kg, còn sheet `Thep` của file nhập `Demo\Excel\1.0...xlsx` ghi 57,438 kg tại G400. Chênh này củng cố rằng không thể lấy từng dòng workbook xuất đó làm chuẩn cho cấu hình G400-1 hiện tại nếu chưa tìm đúng file đầu vào đã sinh bản mẫu.
- Nhận xét thiếu `Gach` và block chú thích `BLT1`/`BLT2` ở Tờ 2–3 là đúng. Đã chèn các block thật từ template; nhãn `A` dùng bước danh nghĩa từ Excel (150 mm trong fixture), còn lượng và vị trí thanh tiếp tục tính theo quy tắc rải của engine. Đã kiểm chứng trên PDF xuất Tờ 2–3 của fixture G400-1 loại 3.

Kiểm tra tiếp ngày 25/09/2026 cho thấy cần phân biệt cả **PDF** với workbook thép xuất trong cùng `Demo/Cad`: loại 3 G400-1/G500-2 có thép đáy trong PDF khác workbook `Demo/Cad/Thep`, trong khi G600-3/G800-1 khớp. Hai sheet workbook loại 3 còn có dòng thép thiếu nhãn nhóm khiến Z4 bỏ sót khối lượng. Số liệu, công thức tính ngược và bảng đối chiếu nằm trong `VHGpro_forensic_PDF_steel_20260925.md`. Vì vậy nhận xét cũ rằng chỉ cần đổi cặp từ `Demo/Excel` sang PDF + workbook xuất để có một bộ vàng đồng nhất là chưa đủ.
