# Kế hoạch cuối cùng đã phản biện: nâng cấp DBTool cho AutoCAD / Civil 3D 2026

**Mục tiêu:** hoàn thiện DBTool thành extension C# độc lập với DLL VHGpro, đọc ba cấu trúc workbook ga, tính hình học và cốt thép có lưu vết, tạo đủ năm tờ A3 cho mỗi hố ga trên ModelSpace và xuất workbook chiết tính có thể đối chiếu theo thanh.

**Kiến trúc:** giữ AutoCAD adapter và giao diện WPF đang có; chuẩn hóa luồng Excel thành mô hình miền bất biến; tách phép tính hình học, thép và khối lượng khỏi Autodesk API; dùng một mô hình thanh thép duy nhất cho hình vẽ, bảng TK và Excel. Tờ phụ tỷ lệ 1:20 được tạo khi có cấu kiện phụ hợp lệ.

**Công nghệ:** C# / AutoCAD Managed API, WPF PaletteSet, ClosedXML 0.105.1, AutoCAD Core Console cho kiểm thử CAD tự động. Target framework phải khớp build AutoCAD/Civil 3D được hỗ trợ: AutoCAD 2026 đến Update 1.1 dùng .NET 8; Update 1.2 trở lên dùng .NET 10. Không khóa .NET 8 trước khi chốt host/build.

**Phạm vi đầu tiên:** ga thăm BTCT liền khối, workbook 1.0, sheet GaThamLienKhoi, G400-1 ở hàng 4. Sau khi cổng tính thép và năm tờ chính đạt, mở rộng Loại 2 rồi Loại 3.

**Tài liệu đối chiếu:** C:\Users\DucBao\Downloads\implementation_plan.md được xem là đề xuất kỹ thuật để phản biện. Prompt của người dùng quyết định mục tiêu, giới hạn file nguồn và yêu cầu chỉ lập kế hoạch ở bước này.

## Ràng buộc chung

- Không nạp, gọi hoặc sao chép mã từ VHGpro 3.2.dll, GaBTCTLienKhoi.dll, GaBTCTNapRoi.dll, GaBTCTThamThuNapRoi.dll, Shared.dll. DLL chỉ là tài liệu tham khảo hành vi nếu được phép; toàn bộ nghiệp vụ được triển khai lại độc lập.
- Không ghi lên DWG, Excel hoặc template nguồn. Mỗi kiểm thử vẽ bắt đầu từ bản sao template hoặc bản sao DWG mẫu. File Excel xuất là file mới hoặc bản sao có hậu tố DBTool.
- Đơn vị nội bộ của DBTool và hình học bản vẽ chi tiết là mm. Excel AA là mét theo cấu trúc mẫu; chỉ đổi đơn vị tại ranh giới nhập/hiển thị. Ghi nhận build AutoCAD/Civil 3D đích và target framework tương thích trước khi build phát hành.
- Excel cung cấp trị số và địa chỉ; HDSD VHGpro giải thích ý nghĩa; DWG/DXF/template cung cấp cách thể hiện. Khi ba nguồn không khớp, tạo Diagnostic và ghi lại quyết định, không tự suy diễn thành chuẩn.
- Giá trị bắt buộc không hợp lệ phải chặn vẽ. Ô trống chỉ được xem là hợp lệ nếu schema và tài liệu xác định rõ ý nghĩa của ô trống.
- Mọi hình CAD được tạo trong transaction AutoCAD. Không gọi AutoCAD Database API từ worker thread. Có thể đọc Excel và chạy phép tính thuần ở luồng nền; thao tác vẽ vẫn chạy trên ngữ cảnh document được khóa.
- Không khẳng định tương thích ATP Plotter, tỷ lệ, thuộc tính block hoặc độ dày nét trước khi kiểm thử trên đúng AutoCAD 2026 và bộ template thực tế.

## Review Focus

- H invert để trống trong G400-1: kiểm tra đúng quy ước 0 hay báo thiếu dữ liệu bằng workbook, PDF và test nguồn ô.
- Pipe code không có trong CF.Cong: kiểm tra phải chặn vẽ, không tự gán chiều dày thành 50 mm.
- Loại 3 có cửa thu và Type 2 có nắp BTCT: kiểm tra điều kiện sinh tờ phụ; ca Gang không được tự sinh chi tiết đan.
- Thanh thép giao lỗ tròn, hộp hoặc lỗ cổ lệch tâm: kiểm tra hình học đoạn còn lại, mã thanh và số lượng bằng unit test.
- DWG đích đã có sẵn layer/style/block trùng tên nhưng khác template: kiểm tra TemplateManager báo sai khác, không âm thầm dùng thuộc tính cũ.

Mỗi thay đổi core phải theo nhịp test thất bại → sửa tối thiểu → test đạt. Lệnh kiểm tra baseline trong workspace:

- Build: dotnet build DBTool/DBTool/DBTool.csproj -c Release --no-restore
- Core: dotnet run --project DBTool/DBTool.TestRunner/DBTool.TestRunner.csproj -c Debug --no-build
- CAD integration: chạy VEHOGABATCH trên bản sao VHG-Template.dwg bằng AutoCAD Core Console, export DXF và plot từng cửa sổ khung ra PDF để kiểm tra.

## 1. Phản biện kế hoạch đầu vào

| Nội dung trong bản đề xuất | Bằng chứng đã kiểm tra | Điều chỉnh trong kế hoạch cuối |
|---|---|---|
| ANSI32 dùng tỷ lệ 15 | TestRunner hiện đọc catalog với ANSI32 = 10 và AR-CONC = 1. Giá trị pattern phải lấy từ workbook/catalog thực tế, không chép 15 vào code. | Giữ tên và scale trong vật liệu nguồn. Sau đó hiệu chỉnh cách plot bằng PDF so với DWG mẫu. |
| Hatch BTCT đã đủ nếu đặt theo catalog | Bản in A3 mới của DBTool cho G400-1 thể hiện hatch BTCT gần như mảng đen. | Hatch là cổng chất lượng riêng: kiểm tra boundary, pattern, scale, layer, lineweight và PDF. Không nghiệm thu chỉ bằng việc có entity Hatch. |
| Map C13:K13 là một dải liên tục đã được đọc | Code hiện đọc C13:E13 và I13:K13; F13:H13 không được đưa vào mô hình. C13 trong cả ba workbook mẫu là “Gang”. | Đối chiếu PDF và template để quyết định F13:H13 có nghĩa gì. Chỉ sau đó mới đánh dấu schema đã đọc đủ trường. |
| G400-1 cần tờ phụ BTCT | Workbook 1.0 tại CF.Ga!C13 ghi “Gang”; bộ đọc không phân loại đây là nắp BTCT. Dữ liệu CF.Ga!C15:M15 có quy cách thép tấm nhưng không tự chứng minh cấu kiện này đang được chọn. DWG sinh mới có đúng năm khung chính. | Không tạo tờ phụ chỉ vì C15:M15 có dữ liệu. Tạo phụ khi loại cấu kiện thực tế được bật. |
| Tờ phụ có cho cửa thu | Kế hoạch người dùng yêu cầu thêm chi tiết khi có hộp cửa thu. Orchestrator hiện chỉ tạo phụ khi CoverType = BTCT; SheetAuxDrawer chỉ vẽ tấm đan, chưa vẽ cửa thu. | Bổ sung điều kiện cửa thu. Một tờ phụ 1:20 chứa mọi chi tiết phụ đang hoạt động; nếu vượt diện tích thì tách tờ có kiểm soát. |
| Schedule đã đúng nếu tổng các dòng nội bộ khớp tổng | TestRunner tự cộng ra 192,57 kg. Workbook “KL thép mẫu hố ga thăm liền khối.xlsx”, sheet G400-1, cho 321,92 kg; nhưng workbook input hiện có C13=Gang, W=0, còn bảng vàng có thép cổ và tấm đan BTCT. HDSD trang 11–12 nói C15:M15 là cấu hình thép khi chọn nắp BTCT. Chưa chứng minh hai bộ dữ liệu cùng revision/cấu hình. | Ghi nhận delta 129,35 kg như dấu hiệu cần điều tra, không dùng 321,92 kg làm expected tuyệt đối trước khi xác nhận provenance/config. Reconcile từng mark và tách chênh lệch cấu hình khỏi lỗi tính. |
| Mọi quy cách cống được tính như cống tròn | Hình học hiện có clipping lỗ tròn; schema và PDF cần xác nhận đầy đủ các dạng tiết diện có trong CF.Cong. | Giai đoạn đầu chốt cống tròn theo dữ liệu G400-1. Mô hình PipeShape cho phép thêm cống hộp sau khi map được kích thước rộng, cao và chiều dày. |
| Template đã được clone đủ | TemplateManager hiện clone TextStyle, DimStyle và block; layer được tạo bằng màu mặc định thay vì clone đầy đủ layer template. Lỗi nạp template bị nuốt trong catch. | Clone/kiểm tra cả layer và style; thiếu tài nguyên bắt buộc phải hiện lỗi rõ ràng, không âm thầm chuyển sang style mặc định. |
| Khung tên có đầy đủ ATT và tương thích ATP Plotter | LayoutArranger hiện chèn block Khung rồi ghi thông tin bằng DBText; chưa xác minh ATT, tag thực và quy tắc ATP. | Đọc tag attribute thực từ block Khung, điền theo tag có sẵn, kiểm thử ATP/PUBLISH bằng file mẫu. |
| UI cần viết mới | DBTool đã có lệnh VEHOGA, WPF UserControl trong PaletteSet và VEHOGABATCH. | Nâng cấp UI hiện hữu. Bổ sung DBTOOL alias nếu cần, không xây lại cửa sổ trước khi luồng dữ liệu và bản vẽ đạt. |
| Quy tắc cốt thép có thể dùng hằng số mẫu | Workbook có các quy cách và lựa chọn riêng; một số giá trị mặc định hiện nằm trong ExcelWorkbookReader. | Không biến giá trị mẫu 50, 100, 150, 300, 500 mm thành quy tắc phổ quát. Tách “trống hợp lệ”, “mặc định có nguồn” và “thiếu dữ liệu”. |

### Bằng chứng baseline hiện tại

- Build Release của DBTool thành công với 0 lỗi và 18 cảnh báo. Một phần cảnh báo nullable nằm trong mã hiện có; phần còn lại là xung đột phiên bản assembly Autodesk / .NET cần ghi nhận khi thiết lập ngưỡng build.
- TestRunner đọc được 20 ga trong mỗi workbook mẫu, nhận diện đúng 3 loại workbook, vượt các kiểm tra G400-1, hình học cơ sở, clipping lỗ tròn, clipping lỗ trần chữ nhật và xuất workbook thử.
- Kiểm tra G400-1 trong test: lòng trong 1000 × 1000 × 1200 mm; đáy, tường và trần dày 150 mm; phủ bì tường 1300 × 1300 mm; đáy 1500 × 1500 mm; lớp lót 1700 × 1700 mm.
- Cống T1 là D200, thành 50 mm, vữa trát 50 mm, ô cao độ trống được code hiện hiểu là h = 0; lỗ khoét tính ra 400 mm, cao độ từ -100 đến +300 mm. Cống T2 là D300, thành 50 mm, h = 100 mm; lỗ khoét 500 mm, cao độ từ 0 đến +500 mm.
- Cao độ AA của dòng G400-1 là 55,24 m. W = 0 nhưng vẫn có lỗ mở cổ/nắp 820 × 570 mm ở góc T1-T2.
- HDSD VHGpro 3.2.pdf, trang 7, là bằng chứng hướng T1–T4; map cống trên sheet ga được khóa theo trang này và regression có L khác B.
- DXF sinh từ DLL hiện tại có năm block Khung ở các vị trí 0, 17600, 35200, 52800 và 70400 mm; 214 kích thước và 188 hatch. Các con số này chỉ xác nhận đã sinh entity, không thay cho nghiệm thu hình ảnh.
- PDF A3 sinh mới cho năm tờ cho thấy: hatch tờ 1 đen đặc; nhãn cao độ và dim đứng còn chồng/sát; tờ 2–3 nhỏ và thưa; bảng tờ 4 co ở vùng trên giữa; tờ 5 có mã thanh và kích thước chồng nhau. Đây là lỗi cần sửa trong các cổng CAD bên dưới.
- DWG gốc “Tool chạy lần 1.dwg” được kiểm tra SHA256 là 8A05C93E74BE830A7C631746F5EBF4DF07665CD26B3FD1D4227A3CC6BCD64F72; không được dùng làm file đầu ra kiểm thử.

### Trạng thái code và cách xử lý

| Thành phần hiện có | Trạng thái đã thấy | Hướng xử lý |
|---|---|---|
| ExcelWorkbookReader.cs, ManholeSpec.cs, Models.cs | Đọc ba loại, catalog, SourceRef một phần và nhiều default. | Giữ ClosedXML; chuẩn hóa schema profile, provenance đủ trường, Diagnostic và loại fallback không được xác nhận. |
| GeometryEngine.cs | Có plan/section/profile và test G400-1. | Giữ API miền thuần mm; tách hàm build profile, pipe/catch basin/opening; mở rộng regression hình học. |
| RebarEngine.cs, ScheduleEngine.cs | Có mesh, clipping lỗ tròn/lỗ chữ nhật, một schedule nội bộ. | Giữ thành phần đã test; hoàn thiện shape/cutout/nhóm thanh và reconcile theo từng dòng với reference đã xác nhận cùng cấu hình. |
| Sheet1Drawer.cs–Sheet5Drawer.cs | Đã có drawer cho năm trang nhưng PDF có lỗi hatch, mật độ bố cục và text. | Giữ quy trình vẽ, sửa bố trí và annotation từng tờ; không thay core geometry bằng số đo vẽ tay. |
| TemplateManager.cs, CadPrimitives.cs | Clone block/text/dimstyle; layer fallback, đường dẫn máy và catch rỗng. | Sửa nguồn tài nguyên, kiểm tra layer/style/block/ATT và trả Diagnostic lỗi template. |
| SheetAuxDrawer.cs, ManholeOrchestrator.cs | Có tờ phụ nắp BTCT; chưa vẽ cửa thu phụ; LayoutArranger giữ footprint 1:30. | Mở rộng trigger và nội dung phụ; footprint, plot window, title/footer nhận tỷ lệ 1:20. |
| HoGaCommands.cs, MainPaletteControl.xaml/.cs, MainViewModel.cs | Đã có VEHOGA, WPF PaletteSet, VEHOGABATCH, chọn workbook và vẽ. | Nâng cấp workflow/diagnostics/filter/preview; giữ lệnh batch cho regression CAD. |
| ExcelSteelWriter.cs | Đã tạo workbook và bản sao nguồn; TestRunner kiểm tra file tồn tại. | Giữ writer, đối chiếu nội dung theo golden, không ghi ngược lên workbook nguồn. |

## 2. Kiến trúc và hợp đồng dữ liệu

Luồng duy nhất:

1. Chọn workbook và sheet ga.
2. Nhận diện schema theo loại workbook, phiên bản và sheet.
3. Đọc cấu hình, danh mục, hàng ga; gắn SourceRef cho từng giá trị.
4. Kiểm tra dữ liệu và phát Diagnostic.
5. Chuẩn hóa thành NormalizedManholeSpec.
6. Tính ManholeGeometryModel.
7. Tính danh sách RebarInstance.
8. Tạo SteelScheduleModel từ chính danh sách thanh đó.
9. Dựng các tờ CAD bằng CAD adapter.
10. Xuất bảng Excel từ SteelScheduleModel.
11. Kiểm tra DWG/DXF, workbook đầu ra và PDF A3.

Mô hình miền không tham chiếu Autodesk API. CAD adapter chỉ chuyển các đối tượng hình học và thanh đã tính sang Entity, BlockReference, Dimension, Hatch và AttributeReference. Bảng CAD, chi tiết uốn và Excel cùng tiêu thụ một SteelScheduleModel, không tự tính lại riêng từng nơi.

Các kiểu dữ liệu logic cần có:

- WorkbookSchemaProfile: loại ga, phiên bản, tên sheet, map ô, trạng thái trường bắt buộc / tùy chọn.
- SourceRef: đường dẫn nguồn, sheet, địa chỉ ô, giá trị gốc và giá trị đã parse.
- Diagnostic: mức Info / Warning / Block, mã ga, nguồn lỗi, điều kiện và cách khắc phục.
- NormalizedManholeSpec: thông số chuẩn hóa, đơn vị mm, quy cách thép parse, cống, cổ, nắp và cửa thu.
- ManholeGeometryModel: mặt bằng, hai mặt cắt, các profile bê tông, lỗ mở, tim cống và cao độ.
- RebarInstance: mã thanh, cấu kiện, lớp/mặt, hình dạng, đường kính, tọa độ hoặc biên dạng, số lượng, chiều dài cắt và nguồn dữ liệu.
- SteelScheduleModel: dòng thống kê, nhóm cấu kiện, tổng đường kính, tổng khối lượng và quy tắc làm tròn.

## 3. Module Excel: đọc, kiểm tra và chuẩn hóa

### 3.1. Chọn và nhận diện workbook

- Lệnh VEHOGA mở PaletteSet. Nút chọn file nhận .xlsx/.xlsm; workbook nguồn được mở read-only.
- Nhận diện loại ga bằng sheet dữ liệu và schema đã biết, không dựa riêng vào tên file.
- Hỗ trợ chọn sheet cụ thể khi workbook có nhiều sheet dữ liệu. Mã ga phải duy nhất trong phạm vi lần chạy.
- Nếu thiếu CF.Ga, CF.Cong, CF.Data hoặc sheet ga yêu cầu, tạo Diagnostic Block chỉ rõ workbook và sheet thiếu.
- Đọc header/nhãn quan trọng để phát hiện workbook đã đổi cấu trúc. Nếu địa chỉ ô cũ không còn đúng, không tiếp tục với map im lặng.

### 3.2. Schema cấu hình CF.Ga

Với workbook liền khối và nắp rời, các hàng đầu cấu hình đang được code đọc như sau:

| Nhóm | Ô đang được code đọc | Diễn giải theo mô hình |
|---|---|---|
| Lớp lót | C3:D3 | Mở rộng và chiều dày lớp lót |
| Bản đáy | C5:D5 | Mở rộng đáy và thông số vát chân tường |
| Tường | C7:E7 | Điều chỉnh bước thép ngang, cấp giảm đường kính, tiền tố dim |
| Trần liền khối | C9 | Vát nách trần |
| Trần nắp rời | C9:I9 | Có chốt, quy cách chốt, chiều dài, lỗ, vữa đệm, móc nâng |
| Cổ ga | C11:M11 | Kết cấu, vị trí, kích thước, vát, thép góc, râu neo, dim và điều chỉnh thép |
| Nắp ga | C13:E13 và I13:K13 | Loại nắp, cao nắp, block nắp, số tấm, khe, hướng tấm |
| Thép tấm đan | C15:M15 | Hai lớp thép, bước, thép buộc, thép góc, neo, móc và tỷ lệ |
| Lớp bảo vệ | C17:H17 | Đáy, tường, trần, cổ, nắp, lỗ cống |
| Hình dạng móc | C20:J20 | Dạng 0M/1M/2M theo cấu kiện |
| Tiền tố thanh | C21:J21 | Prefix thanh theo cấu kiện |
| Chế độ rải/móc | K19:L21 | Chia đều/theo mật độ; Theo KC/Theo ĐK; hệ số D |
| Thép buộc | C23:L23 | Bố trí, dạng, quy cách, chiều dài nối và móc |
| Thang thép | C25:G25 | Vị trí, đường kính, bước, kích thước uốn, prefix |
| Lỗ cống | C28:L28 | Vữa trát, gia cường, dạng, số lượng, mở rộng, prefix, độ lệch |
| Lỗ mở trần | C30:G30 | Gia cường quanh lỗ mở |
| Cao độ | C32:I32 | Tuyệt đối/tương đối và nhãn đáy, đỉnh, T1–T4 |
| Vật liệu/hatch | hàng 34 và 36 | Ghi chú vật liệu và tên mẫu hatch |

Các vùng C13:H13 cần được chốt lại theo nhãn trong PDF và workbook vì code hiện bỏ qua F13:H13. Không được ghi “đã đọc toàn bộ” khi chưa có map cho các ô đó.

Workbook Loại 3 đặt cấu hình cửa thu tại C17:M17. Các block phía sau dùng độ dịch +2 trong code hiện tại, nhưng các trường C9:I9, C11:M11, C13:K13, C15:M15 và cấu hình cửa thu không dịch. Map cần được lưu tường minh theo schema, không rải toán tử shift ở nhiều vị trí.

| Block sau cửa thu Loại 3 | Hàng code hiện đọc | Dải chính |
|---|---:|---|
| Lớp bảo vệ | 19 | C:I |
| Chế độ móc | 21 | C:J; K:L |
| Hệ số móc theo D | 23 | L |
| Thép buộc | 25 | C:L |
| Thang thép | 27 | C:G |
| Lỗ cống | 30 | C:L |
| Lỗ mở trần | 32 | C:G |
| Cao độ | 34 | C:I |
| Vật liệu | 36 | C:F |
| Hatch | 38 | C:I |

### 3.3. Map hàng dữ liệu ga

Trong ba workbook hiện có, sheet dữ liệu dùng các cột:

- B: mã ga.
- C:E: L, B, H lòng trong.
- F: dày đáy; G:H: thép đáy dưới/trên.
- I: dày tường; J:K: thép tường ngoài/trong.
- L: dày trần; M:N: thép trần trên/dưới.
- O:P, Q:R, S:T, U:V: mã cống và cao độ đáy thông thủy T1–T4.
- W:X: cao và dày cổ; Y:Z: thép cổ ngoài/trong.
- AA: cao độ đáy trong ga, mét.

Parser quy cách thép phải phân biệt rebar dạng D10A150 với móc, thép góc và các dạng ký hiệu khác; lỗi định dạng phải giữ giá trị gốc và địa chỉ ô trong Diagnostic. Không ép mọi ô lỗi thành 0.

### 3.4. Danh mục và quy tắc fallback

- CF.Cong: tra mã cống và chiều dày thành; nếu mã cống không tồn tại, tạo Block. Bỏ fallback tự động 50 mm nếu chưa có cấu hình chỉ rõ.
- CF.Data: tra khối lượng thép hình và catalog vật liệu/hatch theo cấu trúc thực tế. Kiểm tra giá trị catalog với workbook thay vì dùng giá trị dự đoán.
- Ô cống hoàn toàn trống: không tạo PipeConnection.
- Ô cao độ cống trống: ở G400-1, test hiện dùng h = 0. Trước khi coi đây là quy tắc tổng quát, đối chiếu HDSD và mọi workbook; nếu workbook profile cho phép h=0 thì lưu Diagnostic Info, nếu không thì Block.
- Mọi default tùy chọn phải có tên quy tắc và SourceRef hoặc chỉ ra là giá trị profile đã được xác nhận. Không để fallback ẩn trong ReadDouble.

### 3.5. Kết quả reader

Reader trả về ReadResult gồm loại workbook, danh sách ga, pipe catalog, angle catalog, hatch catalog, schema profile và Diagnostics. Mỗi số đo kết cấu và thép có nguồn. UI chỉ cho phép vẽ khi ga được chọn không còn Diagnostic Block, trừ khi kỹ sư sửa input và quét lại.

## 4. GeometryEngine: phép tính hình học

### 4.1. Quy ước tọa độ

- Hệ cục bộ: x theo L, y theo B, z theo cao độ.
- Gốc (0,0,0) tại tâm mặt trên bản đáy trong ga.
- T1 ở +Y, T2 ở +X, T3 ở -Y, T4 ở -X. Phải khóa quy ước bằng một bài test L khác B để phát hiện đảo trục.
- Mọi phép tính dùng mm. Cao độ in tuyệt đối đổi từ m sang m bằng AA + z/1000.

### 4.2. Profile mặt bằng

Với lòng trong L × B và tường đều t:

- Lòng trong: x từ -L/2 đến +L/2; y từ -B/2 đến +B/2.
- Phủ bì tường: tăng t mỗi phía.
- Bản đáy: tăng C5 mỗi phía so với phủ bì tường theo đúng cách định nghĩa trong HDSD.
- Lớp lót: tăng C3 mỗi phía so với biên bản đáy.
- Lỗ cổ lấy kích thước và vị trí từ cấu hình cổ. W=0 nghĩa là không dựng thân cổ; không được bỏ lỗ mở trần nếu nắp/cổ vẫn yêu cầu lỗ.
- Các hình chữ nhật lưu Min/Max và profile kín; kiểm tra tự giao, bề rộng âm và thứ tự vòng.

### 4.3. Cao độ và mặt cắt

Gốc z=0 tại mặt trên bản đáy trong. Với F là dày đáy, D3 là dày lót, H là cao trong, LT là dày trần:

- Đáy bản đáy: z=-F.
- Đáy lót: z=-F-D3.
- Đỉnh tường/liên hệ trần: z=H, có điều chỉnh vữa theo quy tắc riêng của nắp rời.
- Đỉnh trần: z=H+LT.
- Đỉnh cổ: z=H+LT+W nếu W>0.
- Đỉnh nắp: tính từ cao nắp và loại cấu tạo; không mặc định cộng vào khi dữ liệu C13:D13 không kích hoạt loại nắp tương ứng.

Mặt cắt A-A đi qua T1/T3; B-B qua T4/T2. Mỗi mặt cắt sinh outer concrete profile, inner void, pad, roof opening, bevel và pipe opening. Các profile dùng cùng một tọa độ nguồn để mặt bằng và mặt cắt không lệch nhau.

### 4.4. Cống và lỗ xuyên

Với cống tròn có đường kính trong Di, chiều dày thành tp và vữa trát m:

- D ngoài = Di + 2tp.
- D khoét = Di + 2tp + 2m.
- z khoét dưới = h - tp - m.
- z khoét trên = h + Di + tp + m.

Tim cống được đặt vào đúng tường theo hướng T1–T4. Độ lệch e1–e4 có dấu theo hướng nhìn từ trong ga ra ngoài; kiểm thử từng phía bằng một độ lệch dương để chắc chắn dấu trái/phải đúng.

Khi lỗ cống giao bản đáy, lỗ phải cắt đúng phần giao của tường và bản đáy. Không chỉ vẽ circle trên section hoặc xóa thanh thép ở một mặt. Hình học trả về cutout dùng chung cho bê tông, hatch, cốt thép và dim.

Thiết kế PipeOpening có loại tiết diện Round hoặc Rectangular. Loại hộp chỉ được bật khi CF.Cong thực sự cung cấp rộng/cao/thành; không nội suy đường kính từ mã bất kỳ.

### 4.5. Cửa thu

Loại 3 đọc vị trí cửa thu, tường đặt, lỗ thu, khoang, dày tường/đáy, lót và thép. GeometryEngine tạo một CatchBasinGeometry gắn với thân ga bằng transform theo hướng tường. Kiểm tra va chạm với ống cống, lỗ cổ, mép đáy và lớp lót. Trường E17 chưa được code đọc phải xác minh theo workbook/PDF trước khi đưa vào mô hình.

## 5. RebarEngine: sinh thanh và lưới thép

### 5.1. Lưới thép cơ sở

Cho cạnh hữu dụng Lc, lớp bảo vệ c, đường kính d và bước a, vùng tâm thanh:

S = Lc - 2(c + d/2)

Nếu S < 0, tạo Block cho cấu kiện đó. Nếu S = 0, chỉ một thanh ở tim nếu quy định cho phép.

- Theo mật độ: giữ bước danh nghĩa a; số khoảng k = floor(S/a); N=k+1; phần dư chia đều hai đầu để không vượt cover.
- Chia đều: k = max(1, ceil(S/a)); bước thực = S/k; N=k+1. Bước thực không vượt a.
- Luôn lưu bước thực, vị trí thanh đầu/cuối và quy tắc đã dùng để có thể kiểm tra trong log.
- Phân biệt lớp trên/lớp dưới, mặt trong/mặt ngoài và phương thanh theo hệ trục; không dùng chung một lưới làm sai chiều dài cho L khác B.

Điều chỉnh bước/đường kính thép ngang tường dùng C7/D7 theo cấp thép mà workbook hoặc tiêu chuẩn dự án hỗ trợ. Nếu giảm cấp vượt danh sách hợp lệ, báo Block; không tự lấy đường kính kế tiếp gần nhất.

Chiều dài móc thép chủ Theo KC bắt đầu bằng chiều dày cấu kiện trừ hai lớp bảo vệ. Test G400-1 xác nhận móc bản đáy và móc đứng tường là 150 - 2×30 = 90 mm. Theo ĐK dùng n×d theo L21. Áp dụng cover của đúng cấu kiện/lớp, không dùng chung một cover cho mọi thanh.

### 5.2. Cắt lưới tại lỗ

- Round: với thanh là đường thẳng và lỗ tròn tâm C bán kính R, giải giao tuyến đường-thẳng/đường-tròn và trừ khoảng cấm mở rộng theo cover, đường kính thanh và vữa theo quy ước được xác nhận.
- Rectangular: trừ đoạn giao với rectangle đã nới theo cover; lỗ cổ trên bản trần dùng đúng hình chữ nhật/vát góc từ GeometryEngine.
- Sau khi cắt, tạo hai sub-bar nếu thanh còn hai nhánh; giữ lại đoạn neo ngắn và phát Diagnostic thay vì âm thầm bỏ.
- Lưu mark quy tắc T1A/T1B hoặc mã tương đương theo workbook vàng. Số lượng thanh là số vị trí thực; không nhân số lượng hai lần khi schedule nhóm.
- Giao cắt nhiều lỗ trên cùng thanh được xử lý thành tập đoạn còn lại theo thứ tự tọa độ.

### 5.3. Thép gia cường và liên kết

- Vòng/đai quanh cống, thanh vuông và thanh xiên lấy hình dạng, đường kính, số lượng, mở rộng từ cấu hình CF.Ga!D28:G28 và H28:L28 theo map đã xác minh.
- Lỗ thăm trần lấy cấu hình CF.Ga!C30:G30; tính khoảng vượt mép theo trường mở rộng, không dùng hằng số.
- Thép buộc C/Z/U lấy quy cách, dạng, vị trí, bước và móc từ C23:L23 (Type 1/2) hoặc hàng đã dịch của Type 3.
- Thang thép lấy vị trí, đường kính, bước và kích thước từ C25:G25. Chỉ dùng 500/300 hoặc 150×250×300 khi được đọc từ cấu hình tương ứng.
- Nắp BTCT, chốt nắp rời, móc cẩu, thép cổ và cửa thu là nhóm riêng có hình học và số lượng riêng. Không đưa thép nắp vào Schedule nếu loại nắp thực tế là Gang.

Mỗi thanh phải mang dữ liệu hình học uốn, chiều dài từng đoạn, góc/bán kính uốn nếu có trong nguồn, số lượng, đường kính, cover, vị trí và SourceRef. Đây là dữ liệu đầu vào trực tiếp cho Tờ 2–5 và Excel.

## 6. ScheduleEngine: chiều dài và khối lượng

- ScheduleEngine nhận NormalizedManholeSpec, RebarInstance[] và catalog thép hình; không tái suy luận hình học từ mã thanh.
- Chiều dài cắt được cộng từ các đoạn theo quy tắc uốn/nối đã xác minh trong HDSD và workbook tham chiếu đã chứng minh cùng cấu hình. Không tự cộng/trừ chiều dài cung uốn nếu chưa có quy tắc bán kính uốn.
- Khối lượng thép cây dùng q = π × (d/1000)² × 7850 / 4 kg/m, tương đương xấp xỉ 0,006165×d² theo d tính bằng mm. Khối lượng riêng thép góc đọc từ CF.Data.
- Lưu giá trị chưa làm tròn trong mô hình; chỉ làm tròn tại đúng cấp hiển thị giống workbook vàng.
- Nhóm tổng hợp tối thiểu: đáy, tường T1–T4, trần, cổ, thép buộc, gia cường cống, thang, thép hình, nắp BTCT, cửa thu. Định dạng nhóm theo TK.Tieude/TieudeMC mẫu.
- Reconcile G400-1 theo từng dòng: tên cấu kiện, số hiệu, D/code, số thanh, L1…Ln, tổng chiều dài, kg/m, tổng kg. Delta đang thấy là code 192,57 kg so với workbook vàng 321,92 kg, nhưng nguồn vàng chưa được xác minh cùng cấu hình: nó có thép cổ và tấm đan BTCT trong khi workbook hiện tại ghi W=0 và C13=Gang. Trước tiên kiểm tra provenance/cấu hình; không lấy delta tổng làm bằng chứng DBTool sai hoặc làm lý do thêm thép.
- Cổng này phải đạt trước khi ScheduleModel được dùng cho bảng Tờ 4 hoặc xuất Excel.

## 7. CAD adapter, template và năm tờ A3

### 7.1. TemplateManager

- Nạp VHG-Template.dwg bằng Database riêng read-only.
- Audit danh mục Layer, Linetype, TextStyle, DimStyle, block Khung, TenMC, TieudeMC, COTCAO, BLT*, BLTC, Gach, TK.* và block nắp.
- Clone layer/style/block theo nhóm bằng WblockCloneObjects. Với tài nguyên đã có trong bản vẽ đích, so sánh thuộc tính cần dùng; không âm thầm nhận style cũ khác template.
- Bỏ đường dẫn cố định theo máy phát triển. Tìm template cạnh DLL hoặc trong thư mục support đã cấu hình; cho phép người dùng chọn. Thiếu block hoặc dimstyle bắt buộc thì dừng trước transaction vẽ và nêu tên tài nguyên.
- Dùng HatchRecord của workbook/catalog nhưng kiểm tra PatternName có tồn tại trong AutoCAD. Không tạo hatch lỗi trong catch rỗng.

### 7.2. Nội dung và tiêu chí từng tờ

| Tờ | Nội dung | Kiểm tra nghiệm thu |
|---|---|---|
| 1. Cấu tạo | Mặt bằng; A-A/B-B cấu tạo; A-A/B-B cốt thép tổng thể; mặt bằng lưới thép tường; cao độ, lỗ cống, vật liệu và ghi chú. | Đủ sáu hình; mặt bằng/mặt cắt đồng nhất; hai tầng kích thước phân biệt; nhãn cao độ không chồng; hatch lót/BTCT phân biệt rõ trên PDF; view cân trong khung. |
| 2. Đáy, T1, T2 | Đáy trên/dưới; T1 ngoài/trong; T2 ngoài/trong; lỗ cống và ký hiệu lưới. | Số thanh và spacing khớp mô hình; T1/T2 bị cắt đúng theo lỗ; mã sub-bar đúng; dim/mark không đè lưới. |
| 3. T3, T4, trần | T3/T4 hai mặt; trần trên/dưới; lỗ cổ và thép tăng cường. | Đúng mặt/tường; lỗ cổ đúng vị trí/kích thước; lưới bị cắt và gia cường kín; trường hợp lỗ lệch/gần mép có regression. |
| 4. Cổ, thang, bảng thép | Cổ ga, mặt cắt, thang, các nhóm bảng TK và tổng hợp. | Dòng bảng khớp ScheduleModel từng thanh; ATT đúng tag; bảng vừa khung, chữ đọc được; tổng mỗi nhóm khớp file Excel. |
| 5. Chi tiết uốn | Tối đa 14 nhóm cấu kiện TieudeMC, thanh đại diện, kích thước từng đoạn và mã thanh. | Không chồng mã/kích thước; hình thanh tương ứng shape và L1…Ln của schedule; mỗi nhóm xuất hiện đúng một lần. |
| Phụ 1:20 | Tấm đan BTCT và/hoặc chi tiết hộp cửa thu khi cấu kiện đó hoạt động. | Chỉ sinh nếu có cấu kiện phụ; nội dung phụ nằm trong khung A3; tỷ lệ/frame/ghi chú tính theo 1:20; nếu có cả nắp và cửa thu thì cả hai được thể hiện hoặc có tờ phụ thứ hai nếu quá chật. |

### 7.3. Layer, block, dim và hatch

- Layer và màu/nét kế thừa template; tối thiểu map rõ bê tông, thép chủ, thép phụ, thép buộc, vật liệu hatch, dim, text, cống, tâm, nét khuất, khung.
- Chèn block có attributes bằng tag có trong block definition. Không giả định tên tag ATT theo kế hoạch nếu chưa đọc block thật.
- Đọc VHG1-30 và TextStyle từ template; test dim đo trực tiếp mm trong DWG rồi in tỷ lệ 1:30. Không hardcode DIMSCALE=300, DIMTXT=0.2 như một sự thật trước khi đọc style record.
- Tách layer hatch khỏi nét bao. Kiểm tra hatch có island cho lòng rỗng và lỗ cống; không tạo hai hatch chồng cùng pattern.
- Chọn pattern/scale/lineweight theo nguồn. Tiêu chí là bản PDF nhìn thấy bê tông có hatch, không thành mảng đen và không mất nét nhỏ.

## 8. Layout, in và điều phối ModelSpace

- Năm tờ chính tỷ lệ 1:30: block Khung gốc 42000×29700 scale 0.3 cho footprint 12600×8910 mm.
- Dùng khoảng bước hiện có dX=17600 mm, dY=-12425 mm làm cấu hình mặc định; kiểm tra extents giữa mọi ga và tờ phụ.
- LayoutArranger nhận FrameSpec(scale, paper width, paper height, origin, sheet number). Không dùng FrameWidth/FrameHeight cố định 1:30 khi chèn khung 1:20.
- Tên ga, tên tờ, số tờ và tỷ lệ nằm đúng vùng title block. Nếu template có ATT thì cập nhật ATT; nếu không, đặt text không tràn vào khung.
- ModelSpace dùng mm; page setup A3 ngang, plot window lấy footprint thực, tỷ lệ in 1:30 hoặc 1:20, plot style và lineweight lấy từ cấu hình mẫu.
- Kiểm tra PUBLISH/ATP bằng DWG copy. Đo thực tế khoảng cách khung và kiểm tra PDF metadata một trang A3 cho mỗi khung; không dùng Extents để thay thế plot window từng tờ.

## 9. UI, lệnh và an toàn thao tác

- Giữ VEHOGA mở WPF PaletteSet; thêm DBTOOL alias nếu cần tương thích prompt người dùng. VEHOGABATCH tiếp tục là đường kiểm thử tự động.
- Bổ sung trạng thái loại ga, sheet, số ga hợp lệ/lỗi; tìm theo mã; chọn tất cả/bỏ chọn/đảo chọn; mini preview mặt bằng; bảng Diagnostic có Sheet!Cell.
- Nút Vẽ bị khóa khi còn Diagnostic Block. Bỏ lựa chọn “có chắc muốn tiếp tục vẽ” đối với dữ liệu hình học thiếu; sửa Excel rồi đọc lại.
- Cho chọn điểm gốc, tùy chọn ghi đè ga trùng tên, xuất Excel và nhóm cột. Scale chỉ mở rộng khi test A3 xác nhận style và plot cho từng tỷ lệ.
- Hiển thị progress theo ga và theo tờ. Hủy chỉ giữa các ga hoặc transaction; không hủy giữa lúc database đang mở dở.
- Mỗi ga có transaction riêng để lỗi một ga không làm rollback các ga trước hoặc ghi nửa bộ năm tờ. Nếu một ga thất bại, rollback ga đó và báo log.
- Giao diện modern hóa trên PaletteSet hiện có sau khi vertical slice G400-1 hoạt động; tránh đưa dependency theme lớn nếu không cần cho thao tác CAD.

## 10. ExcelSteelWriter

- Tạo file KL thép [Tên ga].xlsx từ VHG-DataForm.xlsx, gồm TH và một sheet chi tiết mỗi ga.
- Một dòng output cho từng bar mark/shape/component: nhóm, mã, D, số thanh, L1…Ln, chiều dài 1 thanh, tổng chiều dài, kg/m, tổng kg.
- Ghi công thức hoặc giá trị theo mẫu đã kiểm chứng; ClosedXML không thay Excel calculation engine nên phải kiểm tra giá trị cache bằng Excel/LibreOffice hoặc tính kết quả trong C# trước khi ghi.
- Nếu cần thêm tổng hợp vào workbook nguồn, chỉ làm trên bản sao có hậu tố DBTool; tuyệt đối không ghi ngược vào đường dẫn workbook nguồn.
- Dùng golden workbook làm expected values chỉ khi đã xác nhận cùng revision, schema và cấu hình ga. Nếu chưa, dùng nó như tài liệu tham khảo để truy dấu quy tắc; báo khác biệt theo sheet!cell và phân loại nguyên nhân.

## 11. Các giai đoạn và cổng nghiệm thu

### Giai đoạn 0 — Khóa nguồn, schema và hợp đồng

Đầu ra: bảng inventory 3 workbook, PDF HDSD, DWG demo, VHG-Template, steel schedules và code baseline; map ô được ghi kèm SourceRef; checksum các DWG nguồn.

Đạt khi: workbook profile phân biệt đúng 3 loại; mọi ô còn mơ hồ (đặc biệt C13:H13, ô cao độ trống, kích hoạt tờ phụ) có quyết định hoặc Diagnostic. Chốt map block/ATT/style/hatch từ template.

### Giai đoạn 1 — Reader và kiểm tra Excel

Files chính: ExcelWorkbookReader.cs, Models.cs, ManholeSpec.cs, DBTool.TestRunner/Program.cs.

Kiểm thử: đọc 3 workbook, đủ 20 dòng ga; G400-1 đúng cột; lỗi mã cống; mã ga trùng; ô thép sai cú pháp; dữ liệu mồ côi; ô optional trống; Type3 shift; SourceRef chính xác.

Đạt khi: mọi block cấu hình có map tường minh, không fallback ẩn, không Diagnostic Block trên ba input mẫu hợp lệ.

### Giai đoạn 2 — GeometryEngine

Files chính: GeometryEngine.cs, Models.cs và bộ test core.

Kiểm thử: G400-1 profile 1700/1500/1300/1000 mm; mặt cắt A/B; mức đáy -250/-150/0 và đỉnh 1200/1350/1450; cống T1/T2 theo số liệu mẫu; lỗ cổ W=0; L khác B; cống lệch tâm; cổ giữa/góc/T1; nhiều cống; lỗ cắt đáy.

Đạt khi: các profile khép kín, mặt bằng/mặt cắt cùng vị trí, tất cả dimensions được kiểm toán độc lập bằng phép tính tay hoặc bảng kiểm.

### Giai đoạn 3 — RebarEngine và ScheduleEngine

Files chính: RebarEngine.cs, ScheduleEngine.cs, Models.cs, test runner.

Kiểm thử: lưới fixed/equal spacing; cover và số thanh; hai lớp/mặt; clipping tròn và chữ nhật; sub-bar; thanh ngắn; gia cường; thép buộc; ladder; bar shapes; thép góc.

Cổng bắt buộc: trước tiên xác nhận sheet G400-1 trong “KL thép mẫu hố ga thăm liền khối.xlsx” được sinh từ đúng workbook/config G400-1 đang kiểm thử. So từng mark, group, D/code, quantity, L và kg. Nếu provenance hoặc cấu hình không trùng (hiện có chỉ dấu C13=Gang/W=0 so với thép tấm đan/cổ trong bảng vàng), phân loại mismatch theo cấu kiện đang hoạt động; không ép tổng 321,92 kg thành expected. CAD schedule chỉ qua cổng khi mọi dòng của cấu kiện đang hoạt động có nguồn expected đáng tin cậy hoặc được kỹ sư duyệt.

Đạt khi: số lượng và chiều dài từng mark khớp quy tắc mẫu; tổng nhóm khớp sau cùng quy tắc làm tròn; không có thanh mồ côi hoặc tính đôi.

### Giai đoạn 4 — TemplateManager và Tờ 1

Files chính: TemplateManager.cs, CadPrimitives.cs, Sheet1Drawer.cs, LayoutArranger.cs, ManholeOrchestrator.cs.

Kiểm thử: vẽ trên bản sao VHG-Template.dwg; kiểm tra layer/style/block bắt buộc; export DXF; đo kích thước; plot PDF A3; render và xem.

Đạt khi: năm view chính đúng; hatch không đen đặc; nhãn cao độ/dim tách nhau; title block đúng; không sửa DWG gốc.

### Giai đoạn 5 — Tờ 2–5 và model CAD chung

Files chính: Sheet2Drawer.cs, Sheet3Drawer.cs, Sheet4Drawer.cs, Sheet5Drawer.cs, CadPrimitives.cs.

Kiểm thử: số lưới, vị trí cutout và mã thanh so với RebarInstance; bảng TK khớp ScheduleModel; 14 nhóm detail; plot từng khung sang một trang A3.

Đạt khi: chữ đọc được ở PDF thực, không chỉ trong zoom CAD; không có text/dim chồng; không bị clip ra ngoài khung; mỗi block ATT đúng.

### Giai đoạn 6 — Tờ phụ và Loại 2/3

Files chính: SheetAuxDrawer.cs, ManholeOrchestrator.cs, GeometryEngine.cs, ExcelWorkbookReader.cs, RebarEngine.cs.

Kiểm thử: Type2 nắp rời, vữa đệm, chốt, móc nâng; Type3 cửa thu ở thân/cổ, các hướng, lỗ thu, thép và lót; cấu hình bị dịch +2; trường hợp Gang không sinh chi tiết nắp BTCT; cửa thu sinh chi tiết phụ.

Đạt khi: loại cấu kiện phụ quyết định sheet chính xác; tờ phụ dùng footprint/tỷ lệ 1:20; kết quả xuất CAD và Excel giống nhau.

### Giai đoạn 7 — UI và điều phối

Files chính: HoGaCommands.cs, MainPaletteControl.xaml/.cs, MainViewModel.cs, ManholeOrchestrator.cs.

Kiểm thử: VEHOGA/DBTOOL, chọn file/sheet/ga, diagnostic, chọn nhiều ga, chọn điểm gốc, trùng tên, progress, rollback riêng một ga lỗi.

Đạt khi: người dùng chỉ cần chọn Excel, ga và tùy chọn; không thể vô tình vẽ input lỗi; lệnh batch và Palette tạo cùng kết quả.

### Giai đoạn 8 — Excel output, plotter và nghiệm thu

Files chính: ExcelSteelWriter.cs, test runner, script kiểm thử CAD và hướng dẫn cài đặt.

Kiểm thử: output Excel mở được; so sánh với golden; thử PDF 5 tờ mỗi loại; ATP Plotter/PUBLISH trên bản sao; kiểm tra layer, hatch, font, lineweight, khoảng cách khung, số trang.

Đạt khi: cả ba workbook được đọc đúng; năm tờ chính đạt PDF; tờ phụ chỉ xuất theo điều kiện; steel schedule đối chiếu được; không phát sinh chỉnh sửa file nguồn; build không có lỗi và không có cảnh báo mới chưa giải thích.

## 12. Bộ regression tối thiểu

- G400-1 Type1: kích thước vuông, hai cống T1/T2, T1 invert trống, W=0, nắp Gang, năm tờ chính.
- Ga L khác B: kiểm tra trục L/B và section A/B.
- Ga có invert âm và lỗ chạm đáy: kiểm tra cut concrete, hatch, bar, dim.
- Ga có cống lệch tim: kiểm tra dấu offset từng T1–T4.
- Ga cổ giữa, cổ góc T1-T2, cổ giữa T1; cả W=0 và W>0.
- Type2 nắp rời: vữa, chốt, lỗ chốt, móc nâng và tấm đan BTCT nếu cấu hình bật.
- Type3: cửa thu thân ga và cổ ga, nhiều hướng, row shift, steel and concrete interfaces.
- Workbook sai/mất catalog, duplicate manhole ID, invalid rebar string, negative dimensions và cell blank không được quy định.
- Nhiều ga: tọa độ không trùng; một ga lỗi rollback riêng, các ga hợp lệ còn lại hoàn thành.

## 13. Tiêu chí hoàn thành tổng thể

1. Excel output lưu SourceRef và Diagnostic đến từng trường quan trọng.
2. GeometryEngine thuần toán học, có regression độc lập và tất cả kích thước CAD lấy từ một model.
3. Một danh sách RebarInstance duy nhất điều khiển detail, bảng thống kê và Excel.
4. G400-1 ScheduleEngine được reconcile từng dòng với golden đã xác nhận cùng cấu hình; reference chưa cùng revision được gắn comparative-only, không làm expected test.
5. Sinh đủ năm tờ A3 trên ModelSpace, mỗi tờ plot thành đúng một trang A3 ở tỷ lệ được ghi.
6. Hatch, dim, text, layers, fonts, block tags và khoảng khung đạt kiểm tra hình ảnh/PDF.
7. Loại 2/3 và tờ phụ chạy theo đúng điều kiện cấu kiện, không theo dữ liệu không hoạt động.
8. Các file nguồn không đổi checksum; chỉ file trong thư mục output/test được ghi.

## 14. Quyết định kỹ thuật cần chốt trước khi khóa cổng triển khai

- Ô invert cống trống có nghĩa là h=0 cho mọi workbook hay chỉ là quy ước của các dòng mẫu đã xác minh.
- F13:H13 trong CF.Ga đại diện cấu tạo nào; hiện code chưa đọc.
- Theo HDSD trang 11–12, cốt thép C15:M15 áp dụng khi chọn loại nắp BTCT. Cần xác nhận bảng vàng G400-1 được lập với loại nắp nào, vì workbook 1.0 hiện chọn Gang nhưng sheet vàng có nhóm tấm đan/cổ.
- Khi vừa có nắp BTCT vừa có cửa thu, có gộp lên một tờ phụ 1:20 hay tách thành hai tờ nếu chật.
- Quy tắc chiều dài bẻ móc, bán kính uốn, nối chồng và làm tròn khối lượng theo bảng vàng nào sẽ là chuẩn.
- Layer, DimStyle, TextStyle, block attribute và thiết lập plot nào là chuẩn phát hành nếu template khác DWG demo.

## 15. Phản biện và phần hợp nhất từ file implementation_plan.md mới gửi

File tại C:\Users\DucBao\Downloads\implementation_plan.md là đề xuất để đánh giá, không phải chỉ thị thay thế yêu cầu hiện tại. Các phần dưới đây được giữ lại khi phù hợp, được sửa khi mâu thuẫn với bằng chứng code/input, và được coi là giả thuyết khi chưa truy được về nguồn.

### 15.1. Giữ lại, hiệu chỉnh và không khóa sớm

| Nội dung file đề xuất | Quyết định cuối |
|---|---|
| Provenance cho từng ô; workbook read-only; model hình học và model thanh dùng chung | Giữ. SourceRef gắn file, sheet, cell, raw value, parsed value, unit và quy tắc parse. Geometry/Rebar/Schedule thuần AutoCAD API; CAD chỉ chuyển model sang entity. |
| Cấu trúc 3 loại ga, 5 tờ chính, tờ phụ 1:20 | Giữ theo luồng Type 1 trước, Type 2/3 sau. Tờ phụ chỉ sinh khi cấu kiện phụ đang hoạt động, không chỉ vì hàng cấu hình có sẵn dữ liệu. |
| Target .NET 8-windows x64 cho AutoCAD/Civil 3D 2026 | Sửa thành gate tương thích theo update. Autodesk hiện ghi AutoCAD 2026 đến Update 1.1 dùng .NET 8 và Update 1.2+ dùng .NET 10. Máy audit có acad.exe R25.1.164.0.0, nhưng cần xác nhận Build trong About AutoCAD; môi trường mới xác nhận thư mục Civil 3D Object Enabler, chưa xác nhận host Civil 3D đầy đủ. Xem [compatibility](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm) và [AutoCAD 2026 Updates](https://help.autodesk.com/view/ACD/2026/ENU/?guid=AUTOCAD_2026_UPDATES). |
| C13:K13 là map đầy đủ của cấu hình nắp | Không giữ cách diễn giải này. Code hiện đọc C13:E13 và I13:K13; F13:H13 chưa được map. Nhãn thực trong workbook/PDF phải quyết định ý nghĩa. |
| C15:M15 đồng nghĩa nắp BTCT được sử dụng | Không. C13 trong ba workbook mẫu là Gang; cấu hình thép C15:M15 được đọc nhưng cấu kiện chưa chắc hoạt động. Schedule và tờ phụ chỉ có thép nắp khi loại nắp và cấu hình xác nhận BTCT. |
| ANSI32 scale 15; AR-CONC scale 10 | Không giữ. Reader hiện có ANSI32 scale 10, AR-CONC scale 1. PDF cho hatch BTCT đen/tối. Chốt bằng inventory, DWG/PDF và test plot chứ không bằng một con số đề xuất. |
| DIMLFAC=1, DIMSCALE=300, DIMTXT=0.2 như giá trị chuẩn | Không hardcode. Đọc DimStyle thật trong template. Nếu model text height 60 mm ở tỷ lệ 1:30 thì chiều cao giấy tương đương 2 mm; không mô tả nhầm 60 mm là chiều cao trên giấy. |
| ATP Plotter/PUBLISH tương thích 100% | Đổi thành điều kiện nghiệm thu có bằng chứng: attribute tag thật, block Khung, page setup, plot style, đúng ATP Plotter 6.3 và PDF. |
| Tính khối lượng đã đạt nếu tổng nội bộ tự cộng khớp | Chưa đạt: runner khoảng 192.57 kg, file tham chiếu 321.92 kg. Nhưng file tham chiếu có thép cổ và tấm đan BTCT; workbook 1.0 hiện có W=0 và C13=Gang; HDSD trang 11–12 chỉ áp cấu hình C15 cho nắp BTCT. Xác nhận hai nguồn cùng cấu hình trước khi coi 129.35 kg là lỗi phần mềm. |
| Boolean solid để khoét hợp nhất đáy và tường | Đầu ra giai đoạn này là bản vẽ 2D. Dùng profile/cutout 2D chung cho nét, hatch, dimension và thép. Không bắt buộc tạo solid hoặc boolean 3D. |
| Viết lại giao diện ngay bằng Modern WPF | Giữ WPF PaletteSet và VEHOGA đang có; nâng UX sau khi pipeline G400-1 đạt cổng tính toán và bản vẽ. VEHOGABATCH tiếp tục làm đường regression. |

### 15.2. Hợp đồng Excel trước khi tạo model hình học

Reader cần thực hiện tuần tự: (1) nhận diện schema/sheet; (2) đọc catalog CF.Cong, CF.Data và hatch/material; (3) map cấu hình theo profile loại ga; (4) đọc dòng ga; (5) parse mã thép, kích thước và cao độ; (6) validate; (7) chuẩn hóa ra NormalizedManholeSpec; (8) trả Diagnostics và SourceRef. Không để GeometryEngine tự đọc workbook hoặc tự điền giá trị còn thiếu.

Địa chỉ dữ liệu ga đã thấy: B mã ga; C:E L/B/H; F đáy, G:H thép đáy; I tường, J:K thép tường; L trần, M:N thép trần; O:V cặp mã cống/cao độ T1–T4; W:X cổ; Y:Z thép cổ; AA cao độ đáy ga. Đây là schema ba file đang có và cần regression khi workbook đổi revision.

Địa chỉ cấu hình nền đã thấy: C3:D3 lớp lót; C5:D5 đáy; C7:E7 tường; hàng 9 trần/chốt/mortar tùy loại; C11:M11 cổ; hàng 13 nắp đang được code đọc hai vùng C:E và I:K; C15:M15 cấu hình nắp BTCT; hàng 17 cửa thu Type 3; lớp bảo vệ, hình dạng/prefix thép, ties, ladder, gia cường ống, lỗ trần, cao độ, vật liệu, hatch chuyển xuống +2 hàng ở Type 3 sau cấu hình cửa thu. Mọi cột/hàng còn thiếu nghĩa phải ghi unresolved, không tuyên bố đã đọc đủ chỉ vì reader truy cập cả dải.

Mỗi trường phải có state riêng: Value, ValidBlank, MissingRequired, ParseError hoặc DefaultedWithSource. Ví dụ, pipe pair trống hợp lệ là không tạo pipe; pipe code có nhưng không nằm trong catalog là lỗi; invert trống chỉ được hiểu 0 nếu PDF/profile/golden xác nhận. Các fallback hiện tại trong ExcelWorkbookReader như cover, mortar, ladder, expansion hoặc reinforcement không được coi là chuẩn kỹ thuật chỉ vì chúng giúp chương trình chạy.

### 15.3. Hợp đồng hình học và tọa độ

Tọa độ cục bộ: x theo L, y theo B, z theo cao độ; gốc tại tâm mặt trên bản đáy phía trong. T1 ở +Y, T2 ở +X, T3 ở -Y, T4 ở -X theo HDSD trang 7; test phải dùng L khác B để bắt lỗi đổi trục. Mỗi hướng có transform local-to-world riêng cho mặt tường, tim cống, độ lệch và chiều nhìn.

Các phép dựng được chia thành BuildPlanProfiles, BuildElevationLevels, BuildSection(A-A/B-B), BuildPipeOpening, BuildRoofOpening, BuildCatchBasin và ValidateGeometry. Đầu ra có profile kín, level list, opening polygons/circles, local frames, nguồn và diagnostics. Drawer không tự tính lại hình học.

Với G400-1 theo workbook/code hiện đọc: lòng 1000×1000×1200 mm; tường/đáy/trần 150 mm; phủ bì tường 1300×1300; đáy 1500×1500; lót 1700×1700. z đáy lót=-250, đáy BTCT=-150, mặt trong đáy=0. Nếu H=1200 là chiều cao trong đến đáy trần, đỉnh trần=1350. Cao cổ W=0 nên không dựng thành cổ; lỗ nắp 820×570 ở góc T1-T2 vẫn giữ nếu cấu hình lỗ hoạt động. Level nắp phải dựa đúng C13:D13 và cấu tạo, không tự cộng chiều cao nắp Gang nếu không có nắp trong bản vẽ.

Với ống tròn có Di và tp đã xác nhận từ catalog, Dngoài=Di+2tp; Dkhoét=Di+2tp+2m nếu m là lớp trát mỗi phía. Nếu h là invert lòng ống, z dưới=h-tp-m và z trên=h+Di+tp+m. G400-1 cho T1 ra Dkhoét 400 mm, bounds -100…+300; T2 ra 500 mm, bounds 0…+500. Các con số này là fixture kiểm thử cho dòng G400-1, không phải mặc định mọi ống. Nếu CF.Cong mô tả kích thước khác thì dùng schema/catalog thực. Opening model được dùng chung cho concrete profile, hatch, dim, pipe label và rebar clipping.

### 15.4. Hợp đồng lưới thép, opening và schedule

Với span Lc, cover c, đường kính d, bước thiết kế a, vùng tâm hữu dụng S=Lc-2(c+d/2). Nếu S<0 thì báo lỗi cấu kiện; nếu S=0 thì tạo một thanh ở tim nếu cấu hình cho phép. Fixed pitch: k=floor(S/a), N=k+1, r=(S-k·a)/2; vị trí j=c+d/2+r+j·a. Equal pitch với S>0: k=max(1,ceil(S/a)), N=k+1, aThực=S/k. S=0 phải xử lý riêng; không áp max(1,...) rồi sinh 2 thanh trùng nhau. a<=0 hoặc d<=0 là lỗi dữ liệu.

Mỗi thanh là đoạn/polyline có hệ tọa độ cấu kiện. Clipper giải giao tuyến round hoặc trừ interval trong polygon/rectangle, gộp các khoảng giao với nhiều opening và trả lại toàn bộ đoạn còn lại theo thứ tự. Offset vùng cắt phải được xác nhận để không cộng hai lần thành ống, vữa, cover hay đường kính thanh. Không tự tạo hook 90/135, không bỏ short bar và không gán chiều dài neo khi chưa có quy tắc nguồn; trường hợp không đủ neo tạo Diagnostic review.

Mô hình thép một lần điều khiển CAD Tờ 2–5, bảng Tờ 4 và workbook output. Mark/shape, face/layer, direction, số lượng, từng đoạn uốn, cut length, kg/m và group enum nằm trên BarInstance/ScheduleItem. Summary dùng enum; bỏ quy tắc substring và fallback thép góc 1.36 kg/m. Đối chiếu golden theo key group+mark+shape+D/code, số lượng, từng chiều dài, tổng chiều dài, kg/m, kg; chỉ dùng tolerance sau khi xác định precision của nguồn mẫu.

Lỗi baseline cần khóa riêng: `ScheduleEngine.AccumulateSummary` hiện đưa mọi nhóm có chuỗi “gia cường” vào tổng trần. `RebarEngine` tạo riêng nhóm “Gia cường lỗ cống” và “Gia cường lỗ thăm”; nhóm lỗ cống phải theo tường, còn gia cường lỗ thăm trên trần phải theo trần. Bổ sung regression cho cả hai trước khi thay cách phân loại sang enum, để sửa đúng số tổng hợp mà không đổi chiều dài hoặc hình học thanh.

### 15.5. Điều kiện chặn chuyển giai đoạn

1. Không chuyển sang CAD table nếu G400-1 còn mark thiếu/thừa so với expected có nguồn. Delta 129.35 kg chỉ là blocker khi nguồn vàng được xác nhận cùng cấu hình; nếu không thì phải lập expected đáng tin cậy khác trước.
2. Không chuyển sang nghiệm thu A3 nếu chỉ có DXF/entity counts mà chưa plot và xem đủ năm trang ở đúng kích thước giấy.
3. Không bật Type 2/3 nếu schema/profile row-shift chưa test với workbook tương ứng và các cấu kiện chưa có geometry/schedule.
4. Không tuyên bố .NET/Civil 3D tương thích nếu chỉ build ở AutoCAD. Phải test host và update đúng mục tiêu phát hành; Autodesk ghi Update 1.2+ đổi sang .NET 10.
5. Không ghi file mẫu nguồn. Giữ SHA256 của DWG gốc, workbook, template trước/sau và tạo toàn bộ artifact thử trong TestOutput.

Các quyết định này được chốt bằng dữ liệu workbook/PDF/DWG trước khi code cổng tương ứng; không cần quyết định về tên lớp hay cách chia file nếu không ảnh hưởng đầu ra kỹ thuật.

### 15.6. Xác nhận nguồn schedule vàng trước khi dùng làm test

- Workbook đầu vào 1.0 ở thư mục gốc và bản sao Demo/Excel có cùng SHA256 da80766911fcfc56904dcedd8d926c0bbfb07ad6fdeb0ddd049184a32f6bd418. Cả hai có CF.Ga!C13=Gang, G400-1 W=0 và các ô C15:M15 có cấu hình.
- HDSD VHGpro 3.2 trang 11–12 mô tả C15:M15 là thông số cốt thép của tấm nắp khi chọn loại nắp BTCT. Vì vậy C15:M15 trong workbook hiện tại không đủ căn cứ để bật thép nắp khi C13=Gang.
- KL thép mẫu hố ga thăm liền khối.xlsx, sheet G400-1, có dòng nhóm Cổ ga và Nắp - Tấm đan; tổng 321.92474184953005 kg. Bằng chứng này chưa chứng minh workbook vàng được sinh từ đúng cấu hình workbook 1.0 4.11.2025.
- File KL thép G400-1.xlsx rời trong thư mục cũng không phải expected đáng tin cậy: sheet G400-1 có dòng Cửa thu nước CT1 dù workbook Type 1 hiện được đối chiếu không có cửa thu.
- DWG Demo/Cad/1.1.Ga thăm BTCT liền khối.dwg từng được kiểm tra và không cùng bộ kích thước với hàng G400-1 hiện tại. Không sử dụng DWG hay bảng thép đó như golden geometry chính xác trước khi đối chiếu revision/cấu hình.
- Gate 0 phải truy vết workbook, DWG và bảng thép theo cùng lần chạy/cấu hình VHGpro. Nếu không thể xác nhận lineage, dùng các file này để đối chiếu cách trình bày/hành vi, còn expected số học phải được lập từ input hiện hành, tài liệu quy tắc và xác nhận kỹ sư.

## 16. Bổ sung sau khi rà soát bản implementation_plan.md mới nhất

Phần này là cập nhật cho các cổng kỹ thuật phía trên. Nó ghi rõ các điểm mới cần giữ trong bản kế hoạch thống nhất; không thay đổi các hợp đồng dữ liệu/hình học đã nêu ở mục 1–15.

### 16.1. Runtime AutoCAD/Civil 3D 2026 phải khóa theo máy phát hành

DBTool hiện target net8.0-windows x64 và tham chiếu managed assemblies từ AutoCAD 2026. Đó là cấu hình build hiện tại, chưa chứng minh tương thích với mọi bản cập nhật AutoCAD/Civil 3D 2026. Tài liệu Autodesk hiện phân biệt AutoCAD 2026 đến Update 1.1 dùng .NET 8 với AutoCAD 2026 Update 1.2 trở lên dùng .NET 10; Civil 3D 2026 đến 2026.2.1 dùng .NET 8, còn 2026.2.2 trở lên dùng .NET 10.

Theo bảng tương thích và ghi chú phát hành Autodesk, AutoCAD 2026 đến Update 1.1 chạy plugin .NET 8; Update 1.2 trở lên chuyển sang .NET 10. Civil 3D 2026 có mốc tương ứng ở 2026.2.2. Autodesk cũng cảnh báo về khả năng không tương thích khi các toolset/Object Enabler liên quan chưa cùng nhánh runtime. Vì vậy Giai đoạn 0 phải ghi Build từ About AutoCAD và Civil 3D, inventory các toolset/Object Enabler liên quan, rồi mới chọn target và SDK. Không nâng target hoặc phát hành DLL trước khi thử trên đúng tổ hợp đang dùng. Nếu phải hỗ trợ cả máy còn ở .NET 8 lẫn máy đã lên .NET 10, phát hành và kiểm thử hai gói riêng; không giả định một DLL dùng chung được cho cả hai host.

Tài liệu tham khảo chính thức: [Managed .NET compatibility](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm), [AutoCAD 2026 updates](https://help.autodesk.com/view/ACD/2026/ENU/?guid=AUTOCAD_2026_UPDATES), [Civil 3D 2026 release notes](https://help.autodesk.com/view/CIV3D/2026/ENU/?guid=Civil3D_2026_Release_Notes), [Civil 3D .NET compatibility guidance](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-DevGuide/files/GUID-521909C2-5A46-46E7-BB60-A3C8058468FC.htm).

### 16.2. Rải thép và hatch là quy tắc cần kiểm chứng bằng bản in

Các công thức fixed pitch/equal pitch trong đề xuất là ứng viên thuật toán, chưa phải quy tắc được chốt cho tất cả profile. Đối chiếu vị trí thanh đầu/cuối, số thanh, bước thực và cover với bảng thép/bản vẽ có cùng cấu hình trước khi chuẩn hóa. Xử lý riêng span khả dụng bằng 0, nhỏ hơn bước, bằng bội số bước và không chia hết; không cho phép hai thanh trùng tọa độ.

Đối với hatch, DXF VHGpro đã quan sát có AR-CONC scale 12, ANSI32 120, ANSI31/ANSI37 120; reader DBTool hiện đọc các scale nguồn khác và CadPrimitives đang có hệ số chuyển 12. Chưa coi hệ số đó là chuẩn cho đến khi xuất DXF rồi plot A3 và xác nhận mật độ, hướng nét, boundary và island. Chấp nhận theo PDF thực, không chỉ theo PatternScale/entity count.

### 16.3. Không phát hành hình học thanh bị cắt nếu chưa rõ cấu tạo neo

Clipper xác định các khoảng thanh còn lại sau khi trừ lỗ; nó không tự quyết định thanh phải bẻ móc, neo bao xa, bỏ đoạn ngắn hay nối với thanh gia cường nào. Do đó kế hoạch giữ hai bước riêng: (1) geometry trả đoạn còn lại đầy đủ và ổn định; (2) policy cấu tạo áp dụng shape/neo theo HDSD hoặc bảng thép cùng cấu hình. Khi nguồn không đủ, tạo Diagnostic yêu cầu review thay vì sinh một chi tiết kết cấu suy đoán.

Mọi view thép tổng thể ở Tờ 1 phải lấy từ cùng mô hình thanh/clipper như Tờ 2–3 và ScheduleModel. Drawer không được vẽ lại lưới độc lập, vì như vậy có thể cho hình nhìn hợp lý nhưng bảng thống kê sai.

### 16.4. Cập nhật trạng thái baseline code

- Regression cho lỗi phân nhóm “gia cường lỗ cống” sang tổng trần đã được thêm; logic ScheduleEngine đã được sửa để phân biệt gia cường lỗ cống, lỗ thăm và cửa thu. TestRunner đã chạy đạt sau sửa. Test này phải giữ làm cổng hồi quy khi chuyển summary sang enum.
- CadPrimitives hiện nhân scale hatch nguồn với hệ số 12 để khớp các giá trị quan sát được trong DXF mẫu. Thay đổi này đang chờ xác minh bằng PDF mới; chưa được xem là nghiệm thu đồ họa.
- Bản in trước đó còn vấn đề hatch quá đậm, text/dim gần nhau, trang lưới thưa, bảng Tờ 4 nén và nhãn Tờ 5 chồng. Đây là blocker cho nghiệm thu, dù build/test core đạt.
- Các bản thử nghiệm tiếp tục nằm trong TestOutput. Giữ nguyên SHA256 của DWG “Tool chạy lần 1” và các workbook/template mẫu.

### 16.5. Chuẩn phát hành cần phân biệt ba lớp kiểm tra

1. **Core verification:** parse/schema, geometry invariants, clipping, bar marks, schedule và Excel bằng test tự động không gọi Autodesk API.
2. **CAD integration:** AutoCAD Core Console chạy VEHOGABATCH trên bản sao, audit DWG/DXF, đo frame/coordinates/resources và tạo PDF.
3. **Host visual smoke test:** mở đúng AutoCAD hoặc Civil 3D build, NETLOAD/VEHOGA, thao tác PaletteSet, chọn ga, vẽ, lưu, đóng/mở lại và chạy ATP/PUBLISH theo cấu hình người dùng.

Chỉ đạt build hoặc chạy Core Console không chứng minh PaletteSet/WPF hay Civil 3D host hoạt động. Chỉ đạt entity audit cũng không chứng minh bản in đọc được. Ba lớp trên đều là cổng release.

## 17. Phản biện các giả định cố định trong implementation_plan.md mới nhất

Phần này khóa cách dùng các con số và công thức trong tài liệu mới: số nào đã được quan sát ở template/workbook cụ thể thì dùng làm fixture kiểm thử; số nào chưa có bằng chứng cùng cấu hình thì chưa biến thành quy tắc chung. Nội dung file kế hoạch là đầu vào tham khảo; phạm vi và quyền thay đổi vẫn theo yêu cầu của người dùng.

### 17.1. Đơn vị, DimStyle và tỷ lệ khung là ba lớp khác nhau

- Core và tọa độ hình học chi tiết thống nhất mm; cao độ AA trong workbook đang đọc theo m và chỉ đổi tại ranh giới tính/ghi nhãn. Tại Gate 0 phải kiểm tra INSUNITS, MEASUREMENT, đơn vị template, đơn vị workbook và mọi phép scale khi clone block. Không được dựa riêng vào tọa độ số để kết luận drawing unit đã là mm.
- Block Khung có kích thước nguồn quan sát được khoảng 42000×29700. Chèn scale 0.3 tạo footprint model 12600×8910; in footprint đó ở 1:30 mới ra giấy 420×297 mm. `0.3` là scale chèn block theo template, không phải tỷ lệ plot 1:30.
- Nếu style thật có DIMTXT=0.2 và DIMSCALE=300, chiều cao text danh nghĩa trong model là 60 mm; khi plot 1:30, chiều cao giấy xấp xỉ 2 mm. Câu “chữ in ra cao 60 mm” trong kế hoạch mới sai đơn vị/ý nghĩa. Trước khi đặt thông số, đọc DimStyle thực, test một dimension đo chuẩn 1000 mm và đo text/arrow trên PDF A3.
- Các giá trị `DIMLFAC=1`, `DIMSCALE=300`, `DIMTXT=0.2`, footprint 12600×8910 và bước dX/dY là hợp đồng riêng của bộ template/plot đã thử. Không ghi chúng thành hằng số dùng cho template khác hoặc tỷ lệ phụ 1:20.

### 17.2. Giữ công thức hình học có nguồn; chặn suy diễn cấu tạo

- Các tọa độ T1..T4 trong workbook được quy ước theo HDSD trang 7; core hiện dùng x theo L, y theo B, z theo cao độ và có helper đổi dấu độ lệch trên hình chiếu tường. Regression bắt buộc dùng L khác B, mỗi hướng một cống lệch dương, và so sánh tim trong mặt bằng, mặt cắt, hình chiếu thép.
- Với cống tròn, công thức `Dngoài = Di + 2tp`, `Dkhoét = Dngoài + 2m`, `zđáy = h - tp - m`, `zđỉnh = h + Di + tp + m` chỉ hợp lệ nếu CF.Cong thật sự cung cấp đường kính trong Di và chiều dày tp, C28 là lớp trát mỗi phía, và cao độ ô dữ liệu là invert lòng ống. Gate 0 phải đối chiếu nhãn/đơn vị trong workbook và HDSD. Cống hộp cần mô hình riêng (rộng, cao, thành, vát góc); không biểu diễn nó bằng một đường kính quy đổi.
- G400-1 có ô cao độ T1 trống và reader hiện gán h=0. Đây là hành vi code hiện có, chưa phải quy ước tổng quát. Profile chỉ chấp nhận blank→0 khi tài liệu và mẫu cùng phiên bản xác nhận; còn lại báo review/block tùy tính bắt buộc.
- Cao độ mốc z=0, chiều cao H, lớp vữa đệm của nắp rời, vị trí lỗ cổ khi W=0 và cao độ đỉnh nắp phải được định nghĩa theo nhãn trong workbook/PDF. Các level kiểm thử G400-1 là fixture, không thể suy rộng nếu định nghĩa H hoặc loại nắp thay đổi.
- Cửa thu loại 3 cần profile hình học riêng: tường đặt cửa thu, hướng, lỗ thu, khoang, đáy/lót và giao cắt cống/cổ/đáy. Mỗi tham số C17:M17 phải được map từ workbook; ô chưa rõ nghĩa như E17 không được lờ đi hoặc tự diễn giải.

### 17.3. Rải thép, cắt lỗ và neo là ba quyết định riêng

- Công thức miền tâm thanh `S = Lc - 2(c+d/2)` là ứng viên tốt khi lớp bảo vệ được đo đến mặt ngoài thanh. Cần xác minh định nghĩa cover trong HDSD cho từng mặt/lớp; xử lý riêng S<0, S=0, a≤0, span<a và span chia hết cho a. Chỉ hai chế độ rải đã xác nhận mới được bật.
- “Chia đều”/“Theo mật độ”, C7 điều chỉnh bước ngang và D7 giảm cấp đường kính hiện được đưa vào kế hoạch dưới dạng đề xuất. Danh sách cấp thép không được hardcode nếu workbook hoặc quy chuẩn dự án chưa xác nhận thứ tự cấp và trường hợp không tồn tại cấp thấp hơn.
- Clipper chỉ tìm phần giao giữa thanh thẳng và vùng cấm. Nó không quyết định thép phải neo, bẻ 90°/135°, quấn quanh ống, nối vào thanh gia cường hay loại đoạn ngắn. Thư viện domain phải trả các đoạn hình học chính xác; một policy cấu tạo riêng chỉ được gắn khi có HDSD/bảng thống kê cùng cấu hình làm căn cứ. Nếu thiếu, phát Diagnostic và không đưa thanh suy đoán vào bảng khối lượng phát hành.
- Các dạng Bo, Vuông, Xiên hoặc kết hợp chỉ là nhãn hình thức. Cần xác minh số thanh, mặt phẳng uốn, đoạn neo, bán kính uốn, cover quanh lỗ và mã thanh từ bảng thép/template. Không biến “xiên 45°”, `10d`, “bỏ thanh dưới 200 mm” hay chiều dài neo mẫu thành mặc định.
- Thép buộc không thể tính đúng chỉ từ mật độ trung bình `(L×H)/a²`. Phải sinh tọa độ cụ thể theo panel/lưới, bố trí Lệch/Đều, cắt trừ lỗ, hình dạng C/Z/U, chiều dài nhánh và nối theo cấu hình. Schedule đếm số vị trí được tạo, không dùng ước lượng diện tích.
- Mọi view lưới ở Tờ 1–3, bảng TK ở Tờ 4, chi tiết uốn ở Tờ 5 và file Excel lấy từ một tập BarInstance/ScheduleItem. Với thanh bị cắt qua lỗ, số nhánh, mark A/B, số lượng và tổng chiều dài phải reconcile xuyên suốt các đầu ra.

### 17.4. Schedule và tiêu chí khớp cần gắn với lineage mẫu

- `q = π(d/1000)^2×7850/4` kg/m và xấp xỉ `0.006165×d²` là hai cách tính gần tương đương; expected phải khớp cách làm tròn trong workbook vàng, không so độ lệch phần trăm tùy ý.
- “Khớp tuyệt đối” và ngưỡng `<0.01%` không phù hợp nếu nguồn có làm tròn, công thức Excel chưa tính lại hoặc khác quy ước bẻ uốn. Lưu số thô; quy định precision/tolerance theo từng trường sau khi xác minh workbook vàng cùng revision/cấu hình. Với số đếm/mã thanh yêu cầu chính xác; với kích thước hình học dùng tolerance mm đã phê duyệt; với số hiển thị so theo quy tắc làm tròn nguồn.
- Bảng `KL thép mẫu hố ga thăm liền khối.xlsx` hiện có nhóm cổ và tấm đan, trong khi workbook 1.0 G400-1 chọn Gang và W=0. Vì chưa có lineage chung, tổng 321.92 kg chỉ là dấu hỏi điều tra, không phải nghiệm thu hoặc hiệu chuẩn thuật toán. Phải truy vết mã ga, revision, lựa chọn nắp, cấu hình và phiên bản VHGpro trước khi tạo golden.
- Không để `ClosedXML` ghi ngược lên workbook nguồn. Tạo output tạm, xác minh mở lại/số liệu, rồi đổi tên nguyên tử sang file đích; mọi cập nhật Thep/THKL chỉ thực hiện trong bản sao được đặt tên rõ.

### 17.5. CAD, bố cục, plot và UI phải nghiệm thu bằng artifact

- Layer, màu, lineweight, linetype, TextStyle, DimStyle, block và ATT phải đọc từ `VHG-Template.dwg`. Danh sách tên trong kế hoạch không chứng minh tag attribute hiện hữu; TemplateManager phải xuất inventory tên block/tag/style và báo thiếu/sai khác trước khi vẽ.
- Hatch source/catalog và PatternScale quan sát trong DWG có khác giá trị đọc từ workbook; CadPrimitives còn hệ số scale 12. Không khóa scale này theo entity counts. Test lại trên PDF: hatch không đen đặc, vùng rỗng/island đúng, nét bê tông/cốt thép không bị che và thời gian plot nằm trong ngưỡng thực dụng. Plot sheet 1 không hatch để phân biệt nghẽn do hatch với cấu hình plot.
- Mỗi tờ CAD nghiệm thu bằng PDF A3 thực, được render kiểm tra ở độ phân giải đọc được, kèm đo khung/title block/plot window. DXF entity count là check cấu trúc, không phải check chất lượng bản in. Dùng plot native của AutoCAD/Core Console cho tự động; ATP Plotter và thao tác PaletteSet/WPF cần smoke test trên host có giao diện.
- V1 giữ năm tờ A3 trên ModelSpace để tương thích mẫu VHGpro. Dữ liệu bố cục được mô tả bằng FrameSpec (paper size, orientation, effective scale, footprint, origin, plot window) để không dùng nhầm footprint 1:30 cho tờ phụ 1:20. Chỉ tuyên bố ATP/PUBLISH tương thích sau khi chạy được file publish thật.
- Tờ phụ phải được quyết định theo cấu kiện đang hoạt động, không theo ô cấu hình có dữ liệu nhưng bị loại nắp vô hiệu hóa. Nếu có cả nắp BTCT và hộp cửa thu, thử bố trí chung; nếu chữ/hình không đạt, sinh hai tờ phụ và đánh số tiếp.
- UI WPF hiện hữu trong PaletteSet là nền tảng. Ưu tiên file picker/drag-drop, nhận diện loại ga, danh sách chọn, preview, diagnostics, chọn gốc/tỷ lệ, trạng thái tiến trình, export. Fluent theme/4K polish là tiêu chí UI sau khi vertical slice CAD đạt; không tạo UI mới trong tiến độ trước khi xác định runtime/host.

### 17.6. Cập nhật cổng nghiệm thu theo bản kế hoạch mới

1. **G0 — Host và nguồn:** ghi phiên bản đầy đủ AutoCAD, Civil 3D, update, .NET runtime, SDK; hash workbook/PDF/template/DWG/reference schedule; xác minh schema 3 workbook, đơn vị và dòng G400-1.
2. **G1 — Excel:** profile theo loại ga; workbook read-only; field map có SourceRef, state và Diagnostic; kiểm tra hàng 4 G400-1, 20 ga mỗi workbook mẫu, mã cống thiếu, cống trống, blank có nghĩa và lỗi format.
3. **G2 — Geometry:** tests cho extents, level, orientation, pipe round/box, lỗ cổ, giao lỗ với đáy/trần và cửa thu; kiểm tra hình học bất biến, không tự giao, mặt bằng/mặt cắt dùng cùng profile.
4. **G3 — Rebar/Schedule:** tests vị trí đầu-cuối, cover, bước thực, các hướng T1–T4, nhiều opening, cắt hai đoạn, lớp trong/ngoài, gia cường có nguồn, ties tọa độ thật, hook policy và schedule theo mark; golden chỉ bật sau xác nhận lineage.
5. **G4 — CAD vertical slice:** G400-1 sinh trên bản sao template, 5 khung; compare entity properties/coordinates với model; render tờ 1 và khắc phục density, hatch, dim/text collision trước khi mở rộng đủ Tờ 2–5.
6. **G5 — 5 tờ và tờ phụ:** từng trang PDF A3, bbox view, kích thước, block ATT, font, layer, scale, plot time và visual checklist; thử tờ phụ nắp/cửa thu.
7. **G6 — UI/host:** load DLL trong đúng host/runtime, mở VEHOGA, chọn workbook/ga, diagnostics, đặt gốc, hủy, undo, chạy lại không nhân đôi, rollback lỗi một ga.
8. **G7 — Excel/3 profiles/release:** output workbook được mở lại và đối chiếu theo dòng; test 3 loại ga, PUBLISH/ATP theo cấu hình; đóng/mở DWG; lưu evidence, hướng dẫn build/load/rollback và giới hạn kỹ thuật cần kỹ sư phê duyệt.

Mỗi gate có artifact đầu ra: JSON/log chẩn đoán có SourceRef, test report, DWG/DXF test copy, PDF từng tờ, workbook output, hash nguồn trước/sau và checklist visual. Chỉ cho phép chuyển gate khi không còn lỗi Block; lỗi Review phải nêu rõ nội dung và người duyệt.
