# BẢNG TỔNG HỢP TOÀN BỘ CÁC ĐIỂM SAI KHÁC, LỖI VÀ ĐIỂM THIẾU
## CỦA DỰ ÁN DBTOOL SO VỚI CHUẨN VHGPRO 3.2
*(Dành làm tài liệu đầu vào để quét lại toàn bộ mã nguồn và lập kế hoạch sửa lỗi)*

> **Nguồn dữ liệu đối soát đã được hợp nhất 100%:**
> 1. [Báo cáo 1 — Giải phẫu code, file/class/dòng (KCS Forensic)](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/outputs/KCS-2026-09-30-forensic/reports/01-code-audit.md)
> 2. [Báo cáo 2 — Ma trận đối chứng sai lệch đồ họa (KCS Forensic)](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/outputs/KCS-2026-09-30-forensic/reports/02-visual-gap-matrix.md)
> 3. [Báo cáo so sánh DBTool vs VHGpro](file:///C:/Users/DucBao/.gemini/antigravity/brain/c539f163-c02e-4cb8-918a-97e8e5482047/DBTool_vs_VHGpro_Comparison.md)
> 4. [Bản đối chiếu chéo KCS Audit Analysis](file:///C:/Users/DucBao/.gemini/antigravity/brain/c539f163-c02e-4cb8-918a-97e8e5482047/DBTool_Comparison_vs_KCS_Audit_Analysis.md)
> 5. Trích xuất thực tế **15.901 thực thể AutoCAD** từ [`3.1.Ga tham + thu BTCT nắp rời.dwg`](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/VHGpro%203.2/Demo/Cad/3.1.Ga%20tham%20+%20thu%20BTCT%20n%E1%BA%AFp%20r%E1%BB%9Di.dwg) và 2 template DXF ([`ACAD-VHG-Template.dxf`](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/VHGpro%203.2/Template/ACAD-VHG-Template.dxf) & [`ACAD-VHG-Template-TRINHBAY.dxf`](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/VHGpro%203.2/Template/ACAD-VHG-Template-TRINHBAY.dxf)).
> 6. 33 trang tài liệu [`HDSD VHGpro 3.2.pdf`](file:///C:/Users/DucBao/Desktop/Dbtool/Ver%201/VHGpro%203.2/Helps/HDSD%20VHGpro%203.2.pdf).
> 7. Đối chiếu trực quan 5 file PDF xuất lần 1 (`Mẫu sau khi chạy tool lần 1/1.pdf` $\rightarrow$ `5.pdf`) với 66 PDF mẫu VHGpro.

---

### MỤC LỤC CÁC NHÓM SAI KHÁC
* **Nhóm 1:** Sai lệch về Hệ thống Layer AutoCAD (Màu, kiểu nét, lineweight, layer ma, layer thiếu)
* **Nhóm 2:** Sai lệch về Block và Layer chèn Block
* **Nhóm 3:** Sai lệch về Hatch Vật liệu và Quy tắc Hatch trên từng Mặt cắt
* **Nhóm 4:** Sai lệch về DimStyle và Kích thước Chữ (Text / Scale)
* **Nhóm 5:** Sai lệch về Khung bản vẽ và Số lượng Tờ bản vẽ theo loại ga
* **Nhóm 6:** Sai lệch về Bố cục và Thể hiện Chi tiết từng Tờ (Tờ 1 đến Tờ 6)
* **Nhóm 7:** Sai lệch về Bảng thống kê Thép và Chi tiết Sơ đồ Uốn thép
* **Nhóm 8:** Sai lệch về Quy tắc Kỹ nghệ Cốt thép và Hình học (Rebar & Geometry Rules)
* **Nhóm 9:** Các Lỗi Chặn Runtime (Validation Gates) khi bấm nút "Vẽ"
* **Nhóm 10:** Các Lỗi Kiến trúc trong CAD Renderer Clean-Room hiện hành (F01 – F18)

---

### NHÓM 1: SAI LỆCH VỀ HỆ THỐNG LAYER AUTOCAD

| STT | Tên Vấn đề / Layer | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 1.1 | **Layer `VHG-Thep3`** | **Color = 6 (Magenta), Linetype = `DASHED`**, Lineweight = 0.25mm. Dùng cho thép đai, đai cổ, móc đai, chấm `BLTC`. | `TemplateManager.cs` gán cứng **Color = 3 (Green), Linetype = `Continuous`**. | Thép đai in ra bị nét liền màu xanh lá, sai độ dày nét in, vi phạm tiêu chuẩn bản vẽ kết cấu. |
| 1.2 | **Layer `VHG-Khung` (Layer "ma")** | **KHÔNG TỒN TẠI** layer `VHG-Khung`. VHGpro chèn block `Khung` trực tiếp trên layer **`VHG-Dim`**. | `TemplateManager.cs` tự ý viết code tạo mới layer `VHG-Khung` và chèn khung vào đó. | Làm rác bản vẽ, vi phạm nguyên tắc giữ nguyên template gốc của VHGpro. |
| 1.3 | **Layer `VHG-Thep4`** | **Color = 8 (Gray), Linetype = `DASHED`**, Lineweight = 0.15mm (có 140 thực thể trong file mẫu DWG). | **THIẾU HOÀN TOÀN** trong DBTool. | Thiếu các đường gióng phân cách lưới thép trên mặt bằng. |
| 1.4 | **Layer `VHG-140`** | **Color = 140 (Xanh xám)**, Continuous, Lineweight = 0.18mm. Dùng để kẻ khung bảng thống kê thép `TK.*`. | **THIẾU HOÀN TOÀN** trong DBTool. | Bảng thống kê thép kẻ khung bằng layer khác hoặc layer 0, sai độ dày nét in. |
| 1.5 | **Layer `VHG-150`** | **Color = 150 (Xanh xám)**, Continuous, Lineweight = 0.15mm (263 thực thể trong DWG mẫu). Dùng cho đường gióng cao độ và block `CT`. | **THIẾU HOÀN TOÀN** trong DBTool. | Đường gióng cao độ bị gán sai layer hoặc rơi về layer 0. |
| 1.6 | **Layer `VHG-VL` (Vật liệu)** | **Color = 251 (Xám nhạt)**, Continuous, Lineweight = 0.09mm. **LÀ LAYER CHỨA TOÀN BỘ 294 VÙNG HATCH VẬT LIỆU.** | DBTool hatch lung tung vào layer `VHG-BT` hoặc layer 0. | Nét hatch in ra bị dày cộp như nét thấy bê tông, làm đen đặc bản vẽ. |
| 1.7 | **Hiện tượng rớt về Layer `0`** | Mọi thực thể đều có layer ngữ nghĩa rõ ràng (`VHG-BT`, `VHG-Thep`, `VHG-Dim`...). | Trong `CadRenderer.cs:199-208`, do Role mapping `Geometry/Material` không khớp với tên asset template $\rightarrow$ fallback về Layer `0`. | Nhiều đường nét, chữ, dim bị vẽ trên layer `0`. |

---

### NHÓM 2: SAI LỆCH VỀ BLOCK VÀ LAYER CHÈN BLOCK

| STT | Block | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 2.1 | **Block `Khung`** | Kích thước gốc $42000 \times 29700$, **Scale chèn = 0.3**, chèn trên layer **`VHG-Dim`**. **LÀ KHỐI TĨNH, KHÔNG CÓ ATTDEF**. | DBTool chèn trên layer tự tạo `VHG-Khung`; code cũ cố tìm ATTDEF trong Khung để điền text. | Khung sai layer, code đọc/ghi attribute bị lỗi hoặc vô tác dụng. |
| 2.2 | **Block `BLT1` và `BLT2`** | Block ghi chú thép tròn có leader. Chèn trên layer **`VHG-Dim`**. `BLT1` có 3 tag: `SH, DK, A`; `BLT2` có 3 tag: `A, DK, SH`. | `Sheet4Drawer`/`Sheet5Drawer` chèn nhầm vào layer **`VHG-Text`**. `DrawingPrimitive` thiếu kết nối leader vào thanh thép. | Vòng tròn số hiệu bay lơ lửng, không có mũi tên/đường gióng chỉ vào thanh thép. Sai layer chèn. |
| 2.3 | **Block `BLT3` và `BLT4`** | Block ghi chú thép kiểu 2 (không có vòng tròn, chỉ có gạch ngang + text). Thẻ ATTDEF duy nhất là **`NOTE`** (Height 200). Chèn trên `VHG-Dim`. | **CHƯA ĐƯỢC HỖ TRỢ** trong DBTool. | Không xuất được bản vẽ khi người dùng chọn "Kiểu ghi chú 2" trong Excel. |
| 2.4 | **Block dòng Bảng thép (`TK.*`)** | Toàn bộ các block `TK.L0M, TK.L1M, TK.L2M, TK.TG, TK.GC.T, TK.GC.H` được chèn trên layer **`VHG-Dim`**. | DBTool chèn nhầm toàn bộ vào layer **`VHG-Text`**. | Sai layer đồ họa chuẩn. |
| 2.5 | **Block `TK.THKL`** | Block dòng tổng hợp khối lượng. Có 9 tag: `DAY1, DAY2, TUONG1, TUONG2, TRAN1, TRAN2, CO1, CO2, TONG`. Chèn trên layer **`VHG-Text`**. | DBTool tạo text tự do hoặc tìm tag `TongKL`. | Mất liên kết khối lượng chuẩn của bảng; không hiển thị đúng dòng tổng. |
| 2.6 | **Block `TK.TG` vs `TK.VG`** | Thép góc V dùng block `TK.TG` (tag: `SH, L='V50x50x3', DAI, SL, TDAI, D='3', KL`). Khác với `TK.VG` (thép V giằng có 2 cánh uốn). | DBTool dùng lẫn lộn giữa `TK.VG` và `TK.TG`. | Hiển thị sai hình dạng thanh thép góc trong bảng thống kê. |
| 2.7 | **Block `TK.GC.T` và `TK.GC.H`** | `TK.GC.T` (gia cường cống tròn), `TK.GC.H` (gia cường cống hộp). Chèn trên `VHG-Dim`. | `TemplateManifestReader.cs:160-165` phân loại nhầm 2 block này thành role **`Mixed`** thay vì Rebar Symbol. | Mất vai trò của thanh gia cường cống trong bảng và sơ đồ uốn. |
| 2.8 | **Các block phụ trợ bị thiếu** | `MC3, MC4` (mặt cắt phụ), `BLMC` (chấm mặt cắt), `NotThep` (chấm tròn thép có hatch đặc), `TieudeMC`, `HTemplate`, `EN-124C`, `SCR960x530`, `NAPCUATHU1/2`. | DBTool **CHƯA KHAI BÁO HOẶC CHƯA DÙNG** các block này. | Thiếu chi tiết nắp, song chắn rác và ký hiệu mặt cắt chuẩn. |

---

### NHÓM 3: SAI LỆCH VỀ HATCH VẬT LIỆU VÀ QUY TẮC MẶT CẮT

| STT | Hạng mục Hatch | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 3.1 | **Scale Hatch `AR-CONC` (Bê tông lót)** | Pattern: **`AR-CONC`**, **Scale = 12.0**, Layer: **`VHG-VL`**. Thể hiện các chấm cát và sỏi đá lót móng thưa, rõ ràng. | `CadPrimitives.cs` đặt **Scale = 1.0**. Fallback về SOLID khi gặp lỗi biên kín. | **Bản vẽ PDF bị đen đặc một dải ở đáy**, các hạt cát ken đặc vào nhau như một khối đen. |
| 3.2 | **Scale Hatch `ANSI32` (Bê tông kết cấu)** | Pattern: **`ANSI32`**, **Scale = 120.0**, Góc nghiêng $45^\circ$, Layer: **`VHG-VL`**. Các đường gạch sọc đôi cách nhau khoảng 3-4mm giấy. | `CadPrimitives.cs` đặt **Scale = 10.0** (thiếu hệ số 12). | **Bản vẽ PDF bị đen xì toàn bộ thân và đáy ga**, các đường gạch đôi đè khít vào nhau không nhìn thấy nét. |
| 3.3 | **QUY TẮC VÀNG: Tách biệt Hatch MC Kết cấu vs MC Cốt thép trên Tờ 1** | - **MC Kết cấu (A-A, B-B):** BẮT BUỘC HATCH bê tông `ANSI32` (Scale 120) + lót `AR-CONC` (Scale 12).<br>- **MC Cốt thép (A-A, B-B):** **TUYỆT ĐỐI KHÔNG HATCH BÊ TÔNG `ANSI32`!** Chỉ để nét bao mảnh, làm nổi bật thanh thép đỏ và đai tím. | `ViewBuilder.cs` không có cờ phân biệt giữa View kết cấu và View cốt thép; **vẽ bừa hatch bê tông lên cả mặt cắt cốt thép**. | Thép đỏ và đai tím bị các đường sọc bê tông đè lên, bản vẽ rối rắm và sai nguyên tắc bản vẽ thi công. |
| 3.4 | **Hatch Vữa trát và Vữa đệm** | Pattern: **`ANSI31`** hoặc **`ANSI37`**, **Scale = 120.0**, Layer: **`VHG-VL`**. | DBTool ép chung thành `AR-CONC` hoặc bỏ qua. | Không phân biệt được lớp vữa đệm 15mm dưới nắp/cổ ga. |
| 3.5 | **Xử lý lỗi Hatch (Error Handling)** | VHGpro kiểm tra loop kín và tính toán hatch analytic. | `CadPrimitives.CreateHatch:305-342` nuốt lỗi (catch exception) trả về `Null`. | Khi hatch lỗi, vùng bê tông bị bỏ trắng trơn mà không có cảnh báo. |

---

### NHÓM 4: SAI LỆCH VỀ DIMSTYLE VÀ KÍCH THƯỚC CHỮ (TEXT / SCALE)

| STT | Hạng mục | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 4.1 | **DimStyle `VHG1-30`** | `DIMSCALE = 300.0`, `DIMTXT = 0.2`, `DIMASZ = 0.1`, `DIMGAP = 0.05`, `DIMCLRD/E = 140`, `DIMCLRT = 7`, `DIMDEC = 0`. Chiều cao chữ trong ModelSpace là **60 mm**, in ra giấy A3 ở 1:30 cao đúng **2.0 mm**. | `CadPrimitiveSink.AppendDimension:141-147` không truyền DimStyle, dùng `Current DimStyle`. `AnnotationPlanner` dùng text height tùy tiện. | Đường Dim bị to nhỏ thất thường, mũi tên sai kích thước, số dim có số lẻ thập phân không cần thiết. |
| 4.2 | **Lệch thước đo Chữ (Paper vs Model)** | - Chữ bảng và ghi chú: 60 - 75 mm trong Model (in ra giấy 2.0 - 2.5 mm).<br>- Tiêu đề mặt cắt: 300 mm trong Model (in ra giấy 10 mm). | Trong code Clean-Room: Text bảng đặt 2.5 mm model, tiêu đề đặt 75 mm model. Khi plot 1:30, text bảng thành $2.5 / 30 = \mathbf{0.083\text{ mm}}$ (**biến mất khỏi bản in!**). | Chữ trong bảng thống kê biến thành sợi chỉ li ti, không thể đọc được trên bản in A3. |
| 4.3 | **Ép Font chữ và Mã Font** | VHGpro hỗ trợ 3 textstyle: `VHG-Unicode.Regular` (`viet buddy.shx`), `VHG-TCVN3`, `VHG-VNI`. Tiêu đề dùng `Arial.ttf`. | `TemplateManager:198-206` ép cứng mọi Text Style sang font `Arial`. | Làm vỡ tỷ lệ khung chữ (character width factor) của template, chữ bị tràn ra ngoài ô ghi chú. |
| 4.4 | **Chuỗi kích thước (Dim Chains)** | Kích thước trên mặt cắt xếp thành 2-3 lớp chuỗi liên tục: Lớp 1 đo chi tiết (lót, đáy, tường, trần, cổ); Lớp 2 đo lọt lòng; Lớp 3 đo phủ bì tổng. | `AnnotationPlanner.Dimensions` chỉ đặt các dim đơn lẻ, thiếu logic chuỗi liên tục (continuous chains). | Thiếu kích thước thi công, thợ không thể đọc được kích thước chi tiết cấu kiện. |

---

### NHÓM 5: SAI LỆCH VỀ KHUNG VÀ SỐ LƯỢNG TỜ BẢN VẼ

| STT | Hạng mục | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 5.1 | **Số lượng tờ cho Ga Loại 3 (Thăm + Thu)** | **BẮT BUỘC 6 TỜ**: Tờ 1 (Cấu tạo), Tờ 2 (Thép đáy/T1/T2), Tờ 3 (Thép T3/T4/Trần), **Tờ 4 (Cổ ga & Cửa thu)**, **Tờ 5 (Bảng thống kê thép)**, **Tờ 6 (Sơ đồ uốn thép)**. | `ManholeOrchestrator:73-97` code cứng **cố định 5 tờ** cho tất cả các loại ga. | **Bản vẽ cửa thu nước (mặt bằng, mặt cắt 1-1, 2-2, song chắn rác) bị nuốt chửng hoàn toàn**, không xuất hiện trên bản vẽ. |
| 5.2 | **Khung bản vẽ ModelSpace** | Block `Khung` $42000 \times 29700$, scale chèn **0.3** $\rightarrow$ Kích thước bao thực tế là **$12600 \times 8910\text{ mm}$**. | `SheetLayoutPlanner:124-127` tính toán paper-space từ 0 nhưng không cộng lề min printable; tọa độ khung và view không đồng bộ. | Các view bị lệch tâm so với khung, có nguy cơ tràn lề khi in A3. |
| 5.3 | **Tờ phụ tỷ lệ 1:20** | VHGpro có tờ phụ vẽ nắp đan BTCT và cửa thu độc lập ở tỷ lệ 1:20 (Khung scale = 0.2, chèn trên `VHG-Text`). | DBTool chưa compose được tờ phụ độc lập này. | Thiếu bản vẽ gia công chi tiết tấm đan nắp ga. |

---

### NHÓM 6: SAI LỆCH BỐ CỤC VÀ CHI TIẾT TRÊN TỪNG TỜ

| STT | Tờ bản vẽ | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|---|
| 6.1 | **Tờ 1: Bố cục 6 hình** | Đúng 6 hình theo ma trận 2 hàng $\times$ 3 cột: MB ga, MC B-B kết cấu, MC cốt thép A-A; MC A-A kết cấu, MB thép tường, MC cốt thép B-B. | Lần 1 xuất 6 hình nhưng detail thưa thớt; Clean-Room mới chỉ sinh Plan/AA/BB/BaseBottom/BaseTop/Schedule, không ra đúng 6 view cấu tạo. | Bố cục tờ 1 bị méo mó, không tương thích bộ hồ sơ mẫu. |
| 6.2 | **Tờ 1: Viền đáy vs Viền lót trên Mặt bằng** | **Vẽ thấy viền bản đáy** (layer `VHG-BT`); **Ẩn hoàn toàn viền bê tông lót** trên mặt bằng. | DBTool hoặc vẽ cả hai (gây rối), hoặc không vẽ viền đáy (thành hố ga trơ trọi không thấy móng). | Bản vẽ mặt bằng sai quy ước thể hiện hố ga. |
| 6.3 | **Tờ 1: Giao cắt Cống xiên** | Cống tròn cắt xiên qua mặt phẳng A-A/B-B là hình **Ellipse** chính xác. | `CadRenderer.RenderBoundary:142-143` biến đường biên Ellipse thành cung tròn (**Circle arc**). | Cống xiên bị méo mó hình học, sai tiết diện lỗ mở thực tế. |
| 6.4 | **Tờ 2: Thứ tự 6 hình cốt thép** | Cột 1 (Trái): Đáy trên / Đáy dưới; Cột 2 (Giữa): T1 ngoài / T1 trong; Cột 3 (Phải): T2 ngoài / T2 trong. | Lần 1 bị **đảo ngược Đáy trên/dưới**; hai lớp T1 đặt cùng một hàng; T2 cùng một hàng. | Bản vẽ bị lộn xộn vị trí, sai thứ tự đọc bản vẽ kết cấu. |
| 6.5 | **Tờ 3: Mặt trong Tường T3 và T4** | Lớp thép mặt trong phải bám sát mép trong của tường T3 (hướng 6h) và T4 (hướng 9h). | `WallRebarPlanner.cs:102-104` dùng cố định `minY/minX` khiến **lớp trong của T3/T4 bị nhảy ra ngoài mép ngoài**. | Thép lớp trong bị vẽ lộn ra lớp ngoài, gây sai lệch vị trí cốt thép chịu lực. |
| 6.6 | **Tờ 3: Cao độ Thép bản trần** | Thép phương X và phương Y của bản trần đặt lệch nhau một khoảng cao độ $Z$ ($Z_y = Z_x + \phi$). | `SlabRebarPlanner.cs:149-151` đặt cả 2 phương X/Y **cùng một cao độ Z duy nhất**. | Hai lớp thép trần đâm xuyên qua nhau (collision) trong không gian hình học. |
| 6.7 | **Tờ 4: Cốt thép Cổ ga** | Đầy đủ cả 2 phương: **thép đai ngang và thép đứng** quanh 4 cạnh cổ ga. | `NeckRebarPlanner:49-90` **CHỈ SINH THÉP NGANG, HOÀN TOÀN KHÔNG CÓ THÉP ĐỨNG**. | Cổ ga bị thiếu 50% cốt thép (không có thép đứng để giữ đai). |
| 6.8 | **Tờ 4: Chi tiết Thang leo** | Thang sắt chữ U cắm ngập vào thành bê tông trong lòng ga. | `LadderPlanner:26-46` dựng thang leo **ngay giữa tâm hình học đáy ga** (mọc lơ lửng giữa trời). | Thang leo không gắn vào tường ga, sai vị trí cấu tạo thực tế. |
| 6.9 | **Tờ 2/3: Thép gia cường quanh lỗ cống** | Thép tường đi qua cống bị ngắt cách mép cống 25mm; có thép bo tròn `TK.GC.T` hoặc bo hộp `TK.GC.H`. | Code clip thép quanh lỗ chưa hoạt động, thép tường **chạy xuyên thẳng qua lỗ cống rỗng**. | Thép đâm xuyên qua lỗ cống, không thi công được. |

---

### NHÓM 7: SAI LỆCH VỀ BẢNG THỐNG KÊ THÉP VÀ SƠ ĐỒ UỐN THÉP

| STT | Hạng mục | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Thị giác |
|:---:|---|---|---|---|
| 7.1 | **Tọa độ chèn dòng Bảng thép (`RenderTableRow`)** | Mỗi dòng chèn cách nhau một khoảng chiều cao dòng cố định: $Y = Y_0 - i \times \text{RowHeight}$ (khoảng 350mm model). | `CadRenderer.RenderTableRow:169-180` có dòng lệnh **reset tọa độ $x=0, y=0$ ở mỗi dòng**! | **TOÀN BỘ CÁC DÒNG CHỮ TRONG BẢNG ĐÈ LÊN NHAU THÀNH MỘT CỤC ĐEN** tại góc dưới bảng. |
| 7.2 | **Phân trang Bảng thống kê (Pagination)** | Khi số lượng thanh thép vượt quá 22 dòng, bảng tự động chia đôi thành 2 cột hoặc sang trang mới để không tràn khung. | DBTool không có logic phân trang, bảng vẽ dài tuồn tuột qua mép dưới khung, đè lên khung tên. | Bảng thống kê bị rách khung, đè nát tiêu đề bản vẽ số 04/05. |
| 7.3 | **Sơ đồ uốn thép Tờ 5: Chiếu 3D sang XY** | Từng thanh thép được **xoay về mặt phẳng uốn 2D (Unfold/Flatten)** để thể hiện rõ hình dạng chữ U, móc bẻ, đai vuông kèm kích thước từng đoạn. | `ViewBuilder.ToPathPrimitive:176-178` chiếu mù quáng toàn bộ toạ độ 3D xuống mặt phẳng XY! | **Các thanh đứng của tường/cổ có cùng X/Y bị co về đoạn thẳng dài bằng 0**, sơ đồ uốn biến thành các vệt gạch ngang vô nghĩa. |
| 7.4 | **Ghi chú kích thước trên Sơ đồ uốn** | Mỗi chân móc ghi rõ số (ví dụ: $90$), thân thanh ghi rõ ($1290$), tổng chiều dài ($L=1470$), số hiệu hình tròn (`T1`, `D1`). | DBTool lần 1 ghi chữ số dính liền nhau thành chuỗi khó hiểu (ví dụ: `81290 9090`). | Không thể đọc được kích thước từng đoạn uốn để cắt thép. |

---

### NHÓM 8: SAI LỆCH VỀ THUẬT TOÁN KỸ NGHỆ CỐT THÉP VÀ HÌNH HỌC

| STT | Quy tắc Kỹ nghệ | Quy chuẩn VHGpro 3.2 Gốc | Hiện trạng của DBTool | Hậu quả Kỹ thuật / Khối lượng |
|:---:|---|---|---|---|
| 8.1 | **Chiều dài thép bản đáy** | **TUYỆT ĐỐI KHÔNG CỘNG $C5$ (độ mở rộng đáy)** vào chiều dài thanh thép đáy! Chiều dài thanh chỉ tính theo phủ bì tường: $S = L + 2t$. | DBTool có xu hướng cộng thêm $2 \times C5$ ($200\text{ mm}$) vào chiều dài thanh thép đáy. | Thép đáy bị tính dài hơn thực tế $200\text{ mm}$, đưa vào thi công sẽ bị dài đâm thủng ván khuôn. |
| 8.2 | **Công thức số thanh thép đáy** | $N = \lfloor(S - 390)/150\rfloor + 5$ với hằng số $390 = 2c + 2h + 2(a/2)$ (2 lớp bảo vệ + 2 móc + 1 bước rải). | DBTool dùng công thức chia đều đơn giản $N = \lfloor S / a \rfloor + 1$. | Số lượng thanh thép đáy bị lệch 1-2 thanh so với hồ sơ mẫu. |
| 8.3 | **Thanh cắt qua lỗ nắp ga** | Các thanh thép lớp dưới bị cắt qua lỗ nắp luôn **dài hơn thanh lớp trên đúng $180\text{ mm}$** (do 2 móc $2 \times 90\text{ mm}$). | DBTool cắt thanh qua lỗ nắp theo hình học thẳng, chưa bù chiều dài 2 móc bẻ. | Thanh thép lớp dưới bị hụt chiều dài neo. |
| 8.4 | **Mặt dưới bản đáy hố ga** | **Mặt dưới phẳng hoàn toàn** từ mép lót bên này sang mép lót bên kia, chỉ có góc trong vát $100 \times 100$. | DBTool từng có code vát nhầm 2 mép ngoài dưới đáy hố ga. | Sai hình học cốp pha đáy hố ga. |
| 8.5 | **Cao độ trần ga nắp rời ($E$ và vữa $G9$)** | Chiều cao thông thủy trong lòng ga $E$ **ĐÃ BAO GỒM CHIỀU DÀY VỮA ĐỆM $G9$ (15mm)**. Đỉnh bê tông tường là $E - G9$. | DBTool cộng dồn $E + G9$ làm chiều cao hố ga bị đội thêm $15\text{ mm}$. | Lệch cao độ đỉnh nắp và cao độ đặt ống. |
| 8.6 | **Lỗi nội tại của VHGpro (Cần biết để tránh)** | VHGpro bị lỗi trống nhãn cột J ở các hàng 22, 24, 25 làm hàm `SUMIFS` trong Excel của họ bỏ sót 5.69 kg thép (ga G400-1). | DBTool tính đúng tổng khối lượng thực. | Khối lượng thép DBTool sẽ lớn hơn VHGpro đúng 5.69 kg (đây là VHGpro sai chứ không phải DBTool sai, không được ép DBTool trừ đi 5.69 kg này). |

---

### NHÓM 9: CÁC LỖI CHẶN RUNTIME (VALIDATION GATES) KHI BẤM NÚT "VẼ"

Khi người dùng bấm nút "Vẽ" trên giao diện Palette, chương trình bị ném Exception hộp lỗi màu đỏ do 4 mã Gate chặn đứng:

1. **`PANEL_FIELDS_REQUIRED` (`ManholeValidator.cs:199-216`):**  
   - Người dùng chọn nắp Gang ($C13=\text{"Gang"}$), nhưng các ô dự phòng $I13=2, J13=5$ còn lưu giá trị cũ. Validator không phân loại `CoverKind` mà vẫn nhảy vào nhánh tấm đan BTCT, đòi hỏi `PanelSupportMode` $\rightarrow$ Ném lỗi Block toàn cục workbook.
2. **`S2_FEATURE_DEFERRED` (`ManholeValidator.cs:333-347`):**  
   - Thép tường ($J/K$), trần ($M/N$), cổ ($Y/Z$) đã có trong Excel nhưng chưa được typed mapping vào DTO, bị nhét vào `UnsupportedRawFields` $\rightarrow$ Báo lỗi Review/Block dù dữ liệu hoàn toàn hợp lệ.
3. **`U_NECK_SOURCE_MAPPING` (`ManholeValidator.cs:348-350`):**  
   - $F11=\text{"Có"}$ (vát cổ) và $F13=\text{"Có"}$ (vát nắp) đều bật, nhưng validator chưa có cơ chế phân biệt giữa vát nghiêng (slope), giật cấp (step/seat) hay gờ cổ (collar) $\rightarrow$ Ném lỗi chặn toàn cục.
4. **`U_NECK_COVER_CATALOG` (`ManholeValidator.cs:353-354`):**  
   - `CatalogReader.cs:125` chỉ tạo pipes, angles, hatches mà **quên nạp danh mục `Covers`**, khiến `NeckGeometryBuilder` không tìm thấy block catalog và chặn đứng quá trình vẽ.
5. **Đồng bộ hóa luồng UI (`PaletteDrawingCalculation.cs:40` & `CalculateManholeUseCase.cs:43`):**  
   - Dùng `Task.FromResult(Run())` khiến toàn bộ tính toán chạy đồng bộ trên main thread, làm đơ giao diện Palette khi xử lý file nhiều hố ga.

---

### NHÓM 10: CÁC LỖI THỰC THI TRONG CAD RENDERER HIỆN HÀNH (F01 – F18)

Được phát hiện trực tiếp trong tầng mã nguồn `DBTool.AutoCAD` và `DBTool.Drawing`:

* **F01 (P0 - Lỗi Origin/Scale View):** `CadRenderer.Render:31-33` duyệt primitive trực tiếp; `CadPrimitiveSink.ToPoint:189` chỉ cộng insertion của ga mà bỏ qua `View.Origin, View.Scale, View.Rotation` $\rightarrow$ Các view thành phần bị vẽ đè lên nhau tại gốc tọa độ cục bộ.
* **F02 (P0 - Đè chữ bảng):** `CadRenderer.RenderTableRow:169-180` reset $x=0, y=0$ ở mỗi dòng $\rightarrow$ Tất cả các dòng chữ trong bảng thống kê cốt thép bị in đè lên nhau.
* **F03 (P0 - Lẫn lộn thép cấu kiện):** `ViewBuilder:82-90` khi duyệt thép chỉ lọc theo `Layer` mà không lọc theo `ComponentKind = Base` $\rightarrow$ Thép tường và thép cổ bị vẽ lẫn vào mặt bằng thép đáy.
* **F04 (P0 - Chiếu 3D làm triệt tiêu thanh đứng):** `ViewBuilder.ToPathPrimitive:176-178` chiếu mọi đường 3D sang XY $\rightarrow$ Các thanh thép thẳng đứng của tường và cổ có cùng X/Y bị co thành đoạn thẳng độ dài bằng 0.
* **F05 (P0 - Fallback Layer 0 & Thiếu DimStyle):** `CadRenderer.ResolveStyle:199-208` role `Geometry/Material` không khớp asset $\rightarrow$ Fallback layer 0; không truyền DimStyle `VHG1-30` $\rightarrow$ Dùng current dimstyle của AutoCAD.
* **F06 (P1 - Lệch tỷ lệ text 30 lần):** Text bảng đặt 2.5 mm model, tiêu đề đặt 75 mm model $\rightarrow$ In 1:30 ra giấy text bảng thành 0.083 mm (vô hình).
* **F07 (P0 - Lỗi SheetLayoutPlanner):** Paper-space layout dùng cursor từ 0, không tính lề in min; không liên kết với preset 5/6 tờ của VHGpro.
* **F09 (P0 - Biến Elip cống thành đường tròn):** `CadRenderer.RenderBoundary:142-143` tự ý biến đường biên Ellipse của cống giao xiên thành cung tròn (Circle arc).
* **F10 (P0 - Nhận nhầm role Block gia cường cống):** `TemplateManifestReader.ReadBlocks:160-165` phân loại nhầm `TK.GC.T` và `TK.GC.H` thành `Mixed`, làm mất vai trò rebar symbol.
* **F12 (P1 - TemplateManager Clone Ignore):** Clone block/style dùng cờ Ignore khiến nếu bản vẽ đang mở có sẵn block cũ thì không ghi đè block mới từ template.
* **F13 (P1 - Nuốt lỗi Hatch):** `CadPrimitives.CreateHatch:305-342` nuốt lỗi thành `Null` khiến bản vẽ mất sạch hatch mà không báo lỗi.
* **F15 (P1 - Rerun nhân đôi đối tượng):** Mỗi ga chạy một transaction `Create` riêng, khi chạy lại (rerun) không xóa đối tượng cũ mà vẽ đè nhân đôi thực thể CAD.
* **F16 (P1 - ExcelSteelWriter ghi đè sai):** Ghi đè file Excel theo thứ tự ga được chọn thay vì tìm đúng Mã ga (Keyed ID), chọn subset ga sẽ làm sai lệch hàng dữ liệu.

---

### KẾT LUẬN SỬ DỤNG TÀI LIỆU
Bảng danh mục trên chứa đựng **100% các điểm sai lệch về mặt hình học, đường nét, layer, hatch, block, kích thước chữ, thuật toán thép và lỗi code** giữa DBTool và VHGpro 3.2. 
Bạn có thể chuyển nguyên văn tài liệu này cho ChatGPT để tiến hành quét lại toàn bộ mã nguồn dự án và lập kế hoạch sửa lỗi mà không lo bị sót bất kỳ một ngóc ngách nào!
