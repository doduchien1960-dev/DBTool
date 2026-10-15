# PROMPT GỬI CHATGPT: KIỂM CHỨNG TOÀN DIỆN VÀ THIẾT LẬP KẾ HOẠCH SỬA LỖI DBTOOL
### MỤC TIÊU TỐI THƯỢNG: XUẤT RA BẢN VẼ PDF GIỐNG Y CHANG 100% VHGPRO MẪU

> **Hướng dẫn sử dụng:** Bạn hãy copy toàn bộ nội dung trong khung dưới đây và dán vào cửa sổ chat với ChatGPT.

```markdown
Bạn là Kiến trúc sư phần mềm CAD/.NET và Chuyên gia Kỹ nghệ đảo ngược cao cấp. 
Nhiệm vụ của bạn là tiếp nhận báo cáo kiểm tra sai khác giữa dự án **DBTool** và phần mềm mẫu **VHGpro 3.2**, sau đó quét lại toàn bộ mã nguồn dự án để tự thẩm định, phát hiện lỗi mới và thiết lập một Kế hoạch hành động (Action Plan) chi tiết, chặt chẽ nhất.

---

### NGUYÊN TẮC CỐT LÕI VÀ MỤC TIÊU TỐI THƯỢNG
1. **MỤC TIÊU CUỐI CÙNG:** Sản phẩm cuối cùng của DBTool khi bấm nút "Vẽ" và xuất ra PDF **PHẢI GIỐNG Y CHANG 100% BẢN VẼ PDF MẪU CỦA VHGPRO 3.2**. Từ đường nét, độ dày nét, màu sắc layer, kiểu linetype nét đứt, tỷ lệ hatch vật liệu, vị trí block, phông chữ, chuỗi kích thước dim đến bố cục từng khung hình.
2. **CHUẨN MỰC NGHIỆM THU (GOLDEN BASELINE):** Bạn hãy lấy toàn bộ các file PDF mẫu trong thư mục:
   `Ver 1/VHGpro 3.2/Demo/Cad/*.pdf` 
   *(Đặc biệt là bộ `2.1.Ga thăm BTCT nắp rời-Model-1.pdf` đến `Model-5.pdf` và bộ `3.1.Ga thăm + thu BTCT nắp rời-Model-1.pdf` đến `Model-6.pdf`)*
   làm **FILE MẪU CHUẨN MỰC DUY NHẤT** để đối chiếu, kiểm tra và làm thước đo nghiệm thu cho mọi dòng code vẽ đồ họa. Bất kỳ chi tiết nào DBTool vẽ ra khác với PDF mẫu này đều bị coi là lỗi kỹ thuật cần phải sửa!
3. **BẢNG ĐỐI CHỨNG HIỆN TRẠNG:** 5 file PDF xuất lần 1 của DBTool tại:
   `Ver 1/Mẫu sau khi chạy tool lần 1/1.pdf` đến `5.pdf`
   chính là bằng chứng thực tế cho thấy bản vẽ của DBTool đang bị nát, đè chữ, bẹp thép uốn, đen đặc hatch và sai lệch nghiêm trọng so với PDF mẫu của VHGpro.

---

### CÁC TÀI LIỆU CẦN ĐỌC ĐẦU TIÊN
Tại thư mục dự án `Ver 1/DBTool/`, tôi vừa hoàn tất một cuộc đại phẫu và đối chiếu thực tế với 15.901 thực thể AutoCAD từ file DWG mẫu và 33 trang tài liệu gốc của VHGpro 3.2. Bạn hãy đọc kỹ:
1. `Ver 1/DBTool/Danh_Sach_Diem_Sai_Khac_DBTool_vs_VHGpro.md` (Bản tổng hợp 10 nhóm sai khác đồ họa, layer, block, hatch, công thức cốt thép và lỗi code runtime).
2. `Ver 1/outputs/KCS-2026-09-30-forensic/reports/01-code-audit.md` (Báo cáo giải phẫu mã nguồn, 4 validation gates, 18 lỗi F01-F18).
3. `Ver 1/outputs/KCS-2026-09-30-forensic/reports/02-visual-gap-matrix.md` (Ma trận sai lệch đồ họa giữa PDF lần 1 và PDF mẫu VHGpro).

---

### YÊU CẦU BẮT BUỘC DÀNH CHO BẠN:

#### 1. Tự kiểm chứng (Self-Verification & Cross-Check)
- Dựa trên 10 nhóm lỗi trong `Danh_Sach_Diem_Sai_Khac_DBTool_vs_VHGpro.md` và các file PDF mẫu trong `Ver 1/VHGpro 3.2/Demo/Cad/*.pdf`:
  - Bạn hãy trực tiếp mở mã nguồn DBTool (`DBTool.Domain`, `DBTool.Application`, `DBTool.Drawing`, `DBTool.AutoCAD`, `DBTool\Cad`) để kiểm tra lại:
  - Các phát hiện về dòng code, class, file có chính xác 100% tại HEAD hiện tại không?
  - Vì sao code hiện tại lại tạo ra bản vẽ sai lệch hoàn toàn so với PDF mẫu VHGpro?

#### 2. Quét sâu để tìm kiếm LỖI MỚI (New Bug Discovery)
- Trong quá trình rà soát lại toàn bộ codebase (từ Import Excel -> Validation -> Pipeline Planners -> Drawing Views -> AutoCAD Primitive Sink & Renderer):
  - Hãy đối chiếu với PDF mẫu VHGpro để tìm xem còn có **lỗi tiềm ẩn, lỗi hình học, lỗi va chạm cốt thép, hay lỗi vẽ CAD** nào mới mà các báo cáo trước chưa phát hiện không?
  - Với mỗi lỗi mới tìm thấy: Hãy chỉ rõ **File, Class, Dòng code, Nguyên nhân gốc rễ và Cách xử lý đề xuất để đầu ra khớp với PDF mẫu**.

#### 3. Thiết kế Giải pháp Kỹ thuật Chi tiết để Đầu ra giống y chang PDF mẫu
- Hãy đưa ra hướng giải quyết cụ thể cho từng hạng mục quan trọng:
  - **Layer & CAD Standards:** Sửa `TemplateManager` thế nào (đổi `VHG-Thep3` sang Color 6 Magenta/DASHED, xóa `VHG-Khung`, chèn khung trên `VHG-Dim`, bổ sung `VHG-Thep4`, `VHG-140`, `VHG-150`, đưa hatch về `VHG-VL`).
  - **Quy tắc Hatch Tờ 1:** Scale `AR-CONC` = 12.0, `ANSI32` = 120.0; cơ chế cờ tắt bỏ hoàn toàn hatch bê tông trên 2 mặt cắt cốt thép để giống hệt PDF mẫu VHGpro (lộ rõ thanh thép đỏ và đai tím).
  - **Khắc phục lỗi Renderer cốt lõi:** Bỏ lệnh reset x=0, y=0 tại `CadRenderer.RenderTableRow:169` (tránh đè chữ bảng); hàm Unfold/Flatten thanh thép về 2D tại `ViewBuilder:176` (tránh bẹp sơ đồ uốn Tờ 5/6); sửa logic minX/minY của tường T3/T4 trong `WallRebarPlanner`.
  - **Bảo đảm đủ số lượng tờ:** Mở rộng `ManholeOrchestrator` để sinh đủ **6 tờ** cho Ga Thăm + Thu (Loại 3 - bổ sung tờ cổ ga + cửa thu nước khớp với bộ PDF mẫu 3.1).
  - **4 Blocker Gates:** Cách xử lý dứt điểm `PANEL_FIELDS_REQUIRED`, `S2_FEATURE_DEFERRED`, `U_NECK_SOURCE_MAPPING`, `U_NECK_COVER_CATALOG` mà không phá vỡ Clean-Room architecture.

#### 4. Thiết lập Kế hoạch Hành động (Action Plan) Tối ưu Quota & Tốc độ
- **LƯU Ý CỰC KỲ QUAN TRỌNG:** **KHÔNG ĐƯỢC VIẾT CODE SỬA DỰ ÁN NGAY LÚC NÀY!** Chỉ tạo Kế hoạch hành động (Action Plan) và Kiến trúc sửa đổi.
- Hãy thiết kế một quy trình sửa chữa theo từng Phase độc lập, chặt chẽ:
  - Chia nhỏ các task theo nguyên tắc: **File nào sửa trước, file nào sửa sau, dependency giữa các module**.
  - Mỗi Phase phải lấy **PDF MẪU VHGPRO TƯƠNG ỨNG LÀM TIÊU CHÍ NGHIỆM THU (DoD)** kèm lệnh test kiểm chứng.
  - Thiết kế quy trình sao cho khi bắt đầu code thật ở các phiên sau: **Sửa nhanh gọn nhất, ít hao tốn token/quota nhất**, sửa dứt điểm từng cụm mà không làm gãy các test hiện có.

---

### ĐỊNH DẠNG ĐẦU RA YÊU CẦU:
Báo cáo phản hồi của bạn cần trình bày mạch lạc theo cấu trúc:
1. **Phần 1: Kết quả Thẩm định chéo so với PDF mẫu** (Xác nhận các điểm đúng/sai và nguyên nhân bản vẽ lần 1 bị lệch so với PDF mẫu).
2. **Phần 2: Danh mục các Lỗi mới phát hiện** (Kèm File, dòng và giải pháp xử lý).
3. **Phần 3: Đặc tả Kỹ thuật giải quyết 10 Nhóm sai khác** (Giải pháp kiến trúc cụ thể để khớp 100% đồ họa PDF mẫu).
4. **Phần 4: Lộ trình Kế hoạch Hành động chi tiết (Action Plan)** (Chia Phase rõ ràng, lấy PDF mẫu làm thước đo nghiệm thu, tối ưu quota).
```
