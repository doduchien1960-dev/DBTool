# Kế hoạch cuối cùng đã phản biện: hoàn thiện DBTool cho AutoCAD / Civil 3D 2026

**Trạng thái:** Quy hoạch kỹ thuật; chưa sửa mã nguồn, Excel, DWG, DXF hoặc template.  
**Phạm vi:** Hoàn thiện DBTool bằng C# độc lập với DLL VHGpro; ưu tiên ga thăm BTCT liền khối G400-1; sau đó hỗ trợ ga nắp rời và ga thăm + thu.  
**Đầu ra mục tiêu:** Năm tờ A3 cho mỗi ga trên ModelSpace, bảng thép thống nhất giữa CAD và Excel, lệnh VEHOGA với giao diện WPF trong AutoCAD/Civil 3D.

Tài liệu implementation_plan.md do người dùng gửi được dùng làm đề xuất để đánh giá. Các con số hoặc chỉ dẫn nằm trong tài liệu đó không tự động trở thành yêu cầu hay tiêu chuẩn kỹ thuật. Prompt hiện tại và quyết định đã thống nhất với người dùng là thẩm quyền cao nhất.

## 1. Tóm tắt quyết định thiết kế

- Làm tiếp trên mã DBTool hiện có; không viết lại toàn bộ. Giữ các module đã có và sửa theo cổng kiểm chứng.
- Tách rõ lớp tính toán thuần khỏi lệnh và database AutoCAD. Core chỉ nhận mô hình dữ liệu chuẩn hóa, đơn vị mm; không gọi Autodesk API.
- Đọc Excel theo profile của từng workbook và lưu nguồn từng giá trị. Dữ liệu thiếu hoặc không hiểu phải hiện chẩn đoán; chỉ dùng mặc định khi PDF hoặc mẫu chứng minh đó là quy ước.
- Một mô hình thanh vật lý là nguồn chung cho hình chiếu thép, bảng TK tờ 4, hình uốn tờ 5 và Excel. Phép cắt hình học tại lỗ không tự quyết định chiều dài neo hoặc hình dạng móc.
- Giữ WPF PaletteSet hiện có và lệnh VEHOGA. Không dùng kỹ thuật ghép/re-parent cửa sổ Win32 bằng SetWindowLongPtr trong thiết kế chính.
- Chọn target framework theo phiên bản và update thực tế của AutoCAD/Civil 3D đích. Project hiện đang target net8.0-windows; Autodesk ghi AutoCAD 2026 hỗ trợ .NET 8 đến Update 1.1 và .NET 10 từ Update 1.2 trở đi. Không khóa net8 cho mọi máy.
- Giữ năm khung trên ModelSpace theo bố cục VHGpro làm hợp đồng V1; nghiệm thu bằng PDF A3 từng khung, không chỉ đếm block hoặc entity.
- Chỉ dùng bảng thép mẫu làm golden khi xác nhận cùng mã ga, revision, lựa chọn cấu kiện và cấu hình. Hiện G400-1 chưa đạt điều kiện này.

## 2. Phản biện bản implementation_plan.md

| Nội dung đề xuất | Nhận xét dựa trên mã/tệp đã rà soát | Điều chỉnh trong kế hoạch cuối |
|---|---|---|
| Target cố định .NET 8 | DBTool.csproj hiện dùng net8.0-windows x64. Autodesk hiện phân nhánh theo update AutoCAD 2026: .NET 8 tới Update 1.1, .NET 10 từ Update 1.2. | Khóa phiên bản AutoCAD/Civil 3D và update trên máy đích trước; build theo runtime tương ứng, rồi kiểm thử NETLOAD/VEHOGA trên đúng host. |
| Xây mới toàn bộ Clean-room | Mã đã có ExcelWorkbookReader, GeometryEngine, RebarEngine, ScheduleEngine, Sheet1–5, SheetAux, WPF PaletteSet, VEHOGABATCH và ExcelSteelWriter. | Clean-room áp dụng cho nghiệp vụ mới/được sửa; tận dụng và kiểm thử lại module hiện hữu. Không gọi hay phụ thuộc DLL VHGpro. |
| Dùng AcadWindowHost và SetWindowLongPtr | DBTool đã chứa WPF UserControl được gắn bằng PaletteSet.AddVisual. Re-parent cửa sổ Win32 tăng rủi ro DPI, focus, owner và vòng đời. | Giữ PaletteSet cho bảng điều khiển dockable. Dùng API AutoCAD modeless nếu cần cửa sổ riêng, không tự quản HWND nếu chưa có yêu cầu được chứng minh. |
| Đọc đủ C13:K13 | Reader hiện đọc C13:E13 và I13:K13; F13:H13 chưa được đưa vào mô hình. PDF HDSD trang 11–12 mô tả C15:M15 là cấu hình thép tấm khi sử dụng nắp BTCT. | Tạo field inventory cho toàn vùng C13:M15, ánh xạ từng cột theo nhãn Excel + PDF + mẫu. Chưa tuyên bố đã đọc đủ cho đến khi F13:H13 được giải nghĩa hoặc được xác nhận là trường không dùng. |
| Hatch bê tông theo scale AR-CONC=10, ANSI32=15 | Con số này không khớp dữ liệu đang đọc: catalog mặc định trong ExcelWorkbookReader là AR-CONC=1, ANSI32=10; CadPrimitives nhân hệ số VHGpro 12. Mẫu DXF được rà trước đó thể hiện PatternScale AR-CONC=12, ANSI32=120. | Không chép 10/15 thành hằng số. Đọc catalog của workbook; kiểm tra cách quy đổi theo template/DXF nguồn và bản PDF A3. Hệ số 12 hiện có chỉ được giữ nếu regression mới xác nhận. |
| Cắt thanh qua lỗ rồi tự thêm móc neo 90°/135° | Cắt để biểu diễn khoảng trống là hình học. Neo và móc là quyết định cấu tạo; không suy ra chỉ từ giao cắt. | Tách ClipBars khỏi AnchoragePolicy. Chỉ sinh móc khi cấu hình, PDF hoặc bản vẽ/thống kê cùng cấu hình chứng minh quy tắc. Đoạn còn ngắn được báo kiểm tra, không tự bỏ. |
| Cống h=0 khi ô cao độ trống | Workbook G400-1 có ô cao độ trống và baseline hiện đọc h=0; đó chưa đủ để biến blank thành quy tắc cho mọi workbook. | Ghi nhận semantics theo profile. Chỉ profile đã xác nhận mới cho phép blank→0; trường hợp khác thiếu giá trị thì block. |
| Bảng thép mẫu G400-1 là expected trực tiếp | Test hiện tại cho tổng khoảng 192,57 kg, trong khi workbook KL thép mẫu báo 321,92 kg. Input G400-1 ghi nắp Gang và W=0; bảng vàng có nhóm thép cổ/tấm đan BTCT. Chưa chứng minh cùng cấu hình/revision. | Không hiệu chuẩn thuật toán bằng tổng 321,92 kg. Truy vết từng mark và lựa chọn cấu kiện, sau đó lập golden dataset có nguồn rõ. |
| Tự nhận tương thích 100% ATP Plotter/PUBLISH | Chưa có bằng chứng publish trên đúng AutoCAD 2026, cấu hình ATP, block ATT và plot page setup. | Chỉ xác nhận tương thích sau khi chạy file test thật trên host, kiểm tra ATT/tag và xuất đủ trang PDF. |
| Hình học lỗ cống dùng công thức đường tròn cho tất cả | Workbook có catalog cống; các loại tiết diện phải được phân loại từ CF.Cong. | Mô hình PipeShape có tròn/hộp; chỉ bật dạng đã có đủ kích thước và test. Cống lạ/mã không tìm thấy phải block, không bịa thành D300 hoặc thành 50 mm. |
| Nắp BTCT/khối phụ dựa trên vùng ô có dữ liệu | C15:M15 có thể được điền cấu hình dù C13 chọn Gang; Orchestrator hiện chỉ gọi SheetAuxDrawer khi CoverType=BTCT và drawer phụ đang tập trung vào tấm đan. | Trigger phụ theo cấu kiện thực sự hoạt động, không theo dữ liệu cấu hình nằm im. Thêm hộp cửa thu vào điều kiện/tờ phụ của Loại 3. |

### 2.1. Cách đọc bằng chứng

- Workbook quyết định giá trị, công thức, kiểu dữ liệu và địa chỉ nguồn.
- HDSD VHGpro 3.2.pdf giải thích ý nghĩa trường, hướng và lựa chọn; trang 7 là nguồn quy ước hướng T1–T4 đã được rà ở vòng trước, trang 11–12 giải thích cấu hình thép nắp BTCT.
- Template/DWG/DXF mẫu quyết định cách thể hiện: layer, block, thuộc tính, text/dim, hatch và bố cục.
- Mã hiện tại chỉ chứng minh baseline đang làm gì, không chứng minh quy tắc đó đúng.
- DLL mẫu nếu được dùng trong phân tích thì chỉ để quan sát hành vi/đầu ra. Không đưa mã hay phụ thuộc DLL vào DBTool mới.

## 3. Hiện trạng mã và phạm vi thay đổi

| Khu vực | Đang có | Việc kế hoạch yêu cầu |
|---|---|---|
| Project/host | Một project C# WPF x64, ClosedXML 0.105.1, tham chiếu Autodesk assemblies theo đường dẫn AutoCAD 2026. | Khóa host/update/target framework; thay đường dẫn build theo cấu hình máy/SDK, không xem đường dẫn cài đặt cục bộ là hợp đồng phát hành. |
| Lệnh/UI | VEHOGA mở PaletteSet; VEHOGABATCH dùng cho script/Core Console; có MainPaletteControl và MainViewModel. | Hoàn thiện chọn Excel/ga, diagnostics, preview, điểm đặt, hủy/tiến trình và chính sách vẽ trùng; giữ batch cho regression. |
| Excel | Đã nhận diện 3 nhóm workbook, đọc CF.Ga, CF.Cong, CF.Data và sheet ga; có SourceRef/Diagnostic một phần. | Hoàn thiện field map; đưa unit/state/formula provenance vào model; gỡ fallback kỹ thuật không có nguồn. |
| Hình học | Có mô hình mặt bằng/mặt cắt, vị trí cổ, lỗ tròn/hộp, cao độ và các profile loại nắp rời. | Đồng nhất trục và quy ước T1–T4; phân biệt hình học thiết kế với độ nhô/đường dóng chỉ phục vụ đồ họa; mở rộng kiểm tra giao lỗ. |
| Cốt thép/thống kê | Có rải lưới, cắt lỗ tròn/hộp, hooks, ties, ladder, cover và ScheduleEngine. | Loại mặc định đường kính/bước/thép góc chưa có nguồn; lưu thanh vật lý/đường hình học đủ chi tiết; bỏ phân loại tổng theo chuỗi tên. |
| CAD | Có drawer tờ 1–5, phụ, TemplateManager, CadPrimitives và LayoutArranger. | Kiểm kê tài nguyên template/ATT; sửa bố cục, hatch, dim/text; có FrameSpec cho 1:30 và 1:20; lỗi template không được nuốt. |
| Excel output | Có writer tạo bảng từ VHG-DataForm.xlsx và hàm tạo bản sao nguồn. | Chỉ ghi file output; xác minh công thức được tính lại trong Excel/engine đích và đối chiếu theo mark, không chỉ kiểm tra file tồn tại. |
| Kiểm thử | Có DBTool.TestRunner và các regression cơ bản. | Bổ sung fixture vàng có lineage, test các biến thể/biên, test host AutoCAD/Civil 3D và PDF A3. |

**Nguyên tắc kiến trúc:** trước mắt giữ cấu trúc solution hiện hữu và tách ranh giới bằng API/module. Chỉ tách thành nhiều project vật lý nếu việc đó giúp test Core độc lập mà không làm phức tạp load AutoCAD.

## 4. Hợp đồng Excel và chuẩn hóa dữ liệu

### 4.1. Quy trình nạp workbook

1. Chọn file .xlsx; ghi đường dẫn, thời gian đọc, kích thước và SHA-256. Không sửa workbook nguồn.
2. Liệt kê sheet, vùng dữ liệu, công thức và tên bảng. So khớp với profile Liền khối, Nắp rời hoặc Thăm + Thu; nếu có nhiều profile khớp hoặc không khớp thì báo lỗi.
3. Đọc CF.Ga, CF.Cong, CF.Data và sheet ga theo profile. Không giả định workbook chỉ khác tên sheet.
4. Với mỗi trường lưu: giá trị thô, kiểu ô, công thức nếu có, cached value nếu có, giá trị chuẩn hóa, đơn vị, trạng thái, workbook hash và SourceRef(file, sheet, cell).
5. Parse chuỗi cốt thép/quy cách bằng parser có kiểm tra cú pháp, không chuyển chuỗi lỗi thành 0.
6. Chạy validation schema, catalog, miền số, cặp ô phụ thuộc, trùng mã ga và chồng lỗ trước khi cho chọn Vẽ.
7. Trả về WorkbookSnapshot bất biến gồm profile, config, catalogs, manholes, diagnostics và nguồn.

ClosedXML không được xem là bộ tính lại công thức tương đương Excel. Trường công thức cần phân biệt formula/cached value; nếu kết quả phụ thuộc cached value mà không xác nhận được thì cảnh báo/block theo mức ảnh hưởng.

### 4.2. Sheet dữ liệu ga

| Trường logic | Vùng/cột workbook mẫu | Đơn vị/quy tắc |
|---|---|---|
| Mã ga | B | Khóa duy nhất; không tự đổi nếu trùng. |
| Lòng ga L, B, H | C, D, E | mm; L theo X, B theo Y, H theo Z theo quy ước được khóa ở GeometryEngine. |
| Bản đáy | F; G, H | F mm; G/H là thép hai lớp theo nhãn thực tế trong header/PDF. |
| Tường | I; J, K | I mm; J/K thép hai mặt theo profile. |
| Bản trần | L; M, N | L mm; M/N thép lớp trên/dưới theo header. |
| Cống T1–T4 | O:P, Q:R, S:T, U:V | Mã/quy cách và cao độ invert; cặp bỏ trống có nghĩa không có cống. |
| Cổ | W, X, Y, Z | Cao, dày, thép hai mặt; W=0 chỉ loại bỏ thân cổ khi semantics được profile xác nhận, không tự xóa lỗ mở. |
| Cao độ chuẩn | AA | Workbook mẫu là mét; chuẩn hóa sang mm nội bộ hoặc giữ elevation mét với kiểu đơn vị rõ ràng, chỉ đổi tại boundary. |

Tên cột, dòng header và công thức là một phần schema; không chỉ dựa vào vị trí nếu cấu trúc file thay đổi.

### 4.3. Cấu hình CF.Ga và profile

| Nhóm | Vùng cấu hình mẫu | Dữ liệu cần chuẩn hóa |
|---|---|---|
| Lớp lót | C3:D3 | Mở rộng khỏi bản đáy, chiều dày. |
| Bản đáy | C5:D5 | Mở rộng khỏi vỏ tường, chiều cao/vát nách. |
| Thép ngang tường | C7:E7 | Điều chỉnh bước, giảm cấp đường kính, tiền tố dim. |
| Trần/nắp rời | C9 hoặc C9:I9 | Liền khối: vát nách; nắp rời: chốt, lỗ chốt, vữa đệm, móc nâng. Xác minh từng cột bằng nhãn và PDF. |
| Cổ ga | C11:M11 | Kết cấu, vị trí, kích thước, vát, thép góc, neo và điều chỉnh thép. |
| Nắp ga | C13:K13 | Loại Gang/BTCT, cao, block, hình học và tấm chia. F13:H13 hiện chưa được model hóa trong reader; bắt buộc giải nghĩa. |
| Thép tấm nắp BTCT | C15:M15 | Hai lớp thép, bước, thép buộc/góc, neo/móc và tỷ lệ theo header/PDF. Chỉ dùng nếu cấu kiện BTCT đang hoạt động. |
| Cửa thu | C17:M17 ở profile Thăm + Thu | Vị trí, hướng, lỗ, khoang, chiều dày, lót, cấu tạo và cốt thép. |
| Lớp bảo vệ | C17:H17; Loại 3 dùng vùng dịch theo profile | Đáy/tường/trần/cổ/nắp/lỗ cống/cửa thu. Không chấp nhận giá trị âm hoặc dùng mặc định chưa có nguồn. |
| Hình dạng móc và tiền tố | C20:J21; Loại 3 dịch theo profile | Hình dạng 0M/1M/2M và prefix từng lớp. |
| Chế độ rải/móc | K19:L21; Loại 3 dịch theo profile | Chia đều/theo mật độ; móc theo khoảng cách/đường kính; số lần D. |
| Thép buộc | C23:L23; Loại 3 dịch theo profile | Lệch/đều, dạng C/Z/U, D-A, chiều dài nối và nhánh móc. |
| Thang | C25:G25; Loại 3 dịch theo profile | Vị trí, đường kính, khoảng cách đầu/bước, hình học móc, prefix. |
| Lỗ cống/gia cường | C28:L28; Loại 3 dịch theo profile | Vữa, đường kính, kiểu, số lượng, mở rộng, prefix, offsets T1–T4. |
| Lỗ thăm trần | C30:G30; Loại 3 dịch theo profile | Thép bao lỗ, dạng, số thanh/mở rộng. |
| Cao trình | C32:I32; Loại 3 dịch theo profile | Chế độ tuyệt đối/tương đối và nhãn từng cao độ. |
| Vật liệu/hatch | Hàng 34/36; Loại 3 dịch theo profile | Ghi chú vật liệu và pattern/scale từ catalog. |

Reader hiện dùng shift=2 cho nhiều cấu hình Loại 3; Catch Basin ở hàng 17. Đây là quy tắc baseline cần kiểm tra lại trên đủ ba workbook, không phải lý do để áp shift cho mọi trường. Một ConfigProfile phải định nghĩa rõ từng vùng và nguồn.

### 4.4. Quy tắc lỗi và giá trị trống

- Required geometry: thiếu/không hợp lệ → Block cho ga đó, không tạo hình.
- Optional feature: cả mã và trường phụ trống → không có feature.
- Cặp bất nhất (ví dụ có cao độ nhưng không có mã cống) → Review/Block theo ảnh hưởng.
- Blank có ý nghĩa mặc định (ví dụ invert=0) chỉ được áp theo profile có bằng chứng; Diagnostic vẫn lưu trạng thái blank và quy tắc đã áp.
- Mã cống không có trong CF.Cong, thép góc không có trong CF.Data, mẫu hatch không tồn tại, block/style bắt buộc không có → không thay bằng cống/thép/style giả định.
- Giá trị cấu hình mặc định chỉ hợp lệ nếu được ghi thành DefaultRule có nguồn cụ thể (PDF/trang, workbook mẫu/ô, hoặc template/object), lý do, phạm vi profile và test.
- Mỗi Diagnostic gồm severity, ga, field, SourceRef, raw value, rule, expected domain và tác động.

## 5. Domain model và luồng dữ liệu

Luồng xử lý mục tiêu:

Excel nguồn → WorkbookSnapshot → validation/profile → NormalizedManholeSpec → GeometryModel → RebarModel → SteelSchedule → CAD sheets + Excel output.

Các kiểu dữ liệu chính:

- SourceRef: file/hash, sheet, cell, raw/formula/cached value.
- InputValue<T>: raw, normalized, unit, state (Blank/Valid/Invalid/Defaulted) và SourceRef.
- ManholeConfigProfile: địa chỉ trường theo loại workbook, đơn vị, required/optional/default rule.
- ManholeSpec: kích thước, cấu kiện, catalog, cống, cổ/nắp, cửa thu và thép.
- GeometryModel: profile mặt bằng, mặt cắt A-A/B-B, openings, mức Z và transform từng view.
- BarInstance: một thanh vật lý hoặc một đường thanh có hình học rõ ràng; vị trí, cấu kiện, mặt/lớp, đường kính, mark, các đoạn/móc, source/config. Nếu grouping để vẽ thì phải giữ được danh sách thành phần tạo ra grouping.
- SteelScheduleItem: tổng hợp BarInstance theo mark, hình dạng, đường kính và chiều dài cắt; dữ liệu gốc vẫn truy được.
- FrameSpec: khổ giấy, hướng, tỷ lệ, footprint, gốc, plot window, page setup và số tờ.

Core nhận/sinh kiểu .NET thuần; Cad adapter chuyển kết quả sang AutoCAD entities trong document context.

## 6. GeometryEngine: hợp đồng hình học

### 6.1. Hệ tọa độ

- Đơn vị hình học nội bộ: mm. Trục X theo L, Y theo B, Z theo chiều cao; gốc (0,0,0) ở tâm lòng ga theo mặt trên đáy bên trong.
- Lòng ga: X ∈ [-L/2,L/2], Y ∈ [-B/2,B/2], đáy trong z=0.
- Hướng thành: T1 ở +Y, T2 ở +X, T3 ở -Y, T4 ở -X. T1/T3 dài theo X; T2/T4 dài theo Y.
- Quy tắc dấu offset: giữ theo quy ước đứng từ trong ga nhìn ra ngoài, dương về bên trái, nhưng phải test theo trang 7 HDSD và bản mẫu cho ga L≠B; mỗi hướng có test riêng.
- Các phép biến đổi giữa world model, view local và sheet origin phải là các hàm transform có thể đảo ngược và được kiểm thử.

### 6.2. Mặt bằng và profile

- Vỏ ngoài tường: (L+2t) × (B+2t).
- Bản đáy: vỏ tường cộng 2 lần độ mở rộng C5 trên mỗi kích thước.
- Lớp lót: mép bản đáy cộng tiếp độ mở rộng C3 trên mỗi kích thước.
- Với benchmark G400-1 đã được rà ở baseline: L=B=1000, t=150 → ngoài tường 1300×1300; C5=100 → đáy 1500×1500; C3=100 → lót 1700×1700. Các giá trị này là ca regression của workbook cụ thể, không phải mặc định chung.
- Dựng Polyline/Region profile kín theo cấu kiện. Dựng lỗ mở trần theo kích thước/vị trí cổ thực tế; nếu W=0 thì chỉ bỏ thân cổ khi workbook xác nhận, còn lỗ mở phụ thuộc vào cấu hình nắp/cổ.
- Vát đáy/trần/cổ là profile mặt cắt có nguồn; không vẽ vát mặt bằng nếu chưa có định nghĩa.

### 6.3. Cao độ và mặt cắt

Dùng z=0 ở mặt trên đáy trong ga. Cấu kiện cơ sở:

- đáy bản: từ -F đến 0;
- lót: từ -F-Hlot đến -F;
- mặt trong trên ga: H;
- trần: từ H đến H+T, trừ khi semantics profile nói khác;
- nắp/cổ: cộng theo cấu tạo đang chọn.

Đối với ga nắp rời, baseline hiện lấy đỉnh tường H−Gmortar, vữa đệm từ H−Gmortar đến H, rồi tấm trần từ H. Đây là cách diễn giải cần khóa bằng PDF/mẫu Type2 trước khi nghiệm thu; không cộng vữa hai lần.

Cao độ tuyệt đối:

Zabs (m) = AA (m) + z (mm)/1000.

AA, invert và các nhãn phải giữ đơn vị rõ. Không trộn cao độ tương đối và tuyệt đối trong geometry; chuyển thành text ở lớp annotation.

Tờ 1 dùng 6 view: mặt bằng, cấu tạo A-A/B-B, cốt thép A-A/B-B, mặt bằng thép tường. Section A-A cắt T1–T3 theo trục Y; Section B-B cắt T4–T2 theo X. Transform/chiều nhìn được test cho L≠B, không dựa vào hình vuông đối xứng.

### 6.4. Cống và lỗ xuyên tường

PipeSpec mang loại tiết diện, kích thước lòng, chiều dày thành, kích thước ngoài, mã CF.Cong, invert, vữa, offset và SourceRef.

Với cống tròn, nếu catalog xác nhận D là đường kính trong và tp là thành ống:
- Dngoài = Din + 2tp
- Dkhoét = Dngoài + 2m
- zđáy,khoét = h − tp − m
- zđỉnh,khoét = h + Din + tp + m

Các công thức trên áp dụng khi h là invert đáy trong và lớp vữa m phủ đều quanh ống. Chúng phải được so với HDSD và mẫu; không áp sang cống hộp. Cống hộp dùng rộng/cao trong + chiều dày thành/vữa theo đúng hướng.

GeometryEngine trả về lỗ khoét theo dạng và vị trí trên tường; phần lỗ nằm dưới z=0 phải được cắt thống nhất vào profile đáy/tường theo cấu hình. Không cần boolean 3D nếu đầu ra chỉ là hình chiếu 2D; dùng profile/clipper 2D có kiểm tra topology.

Các độ nhô ống, đoạn tim và leader là nét trình bày. Mã hiện có dùng một số chiều dài cứng 200, 400, 150 mm trong GeometryEngine; từng giá trị phải so mẫu và đưa vào GraphicConvention nếu chỉ phục vụ thể hiện, không dùng làm kích thước thiết kế.

Nếu mã cống không tra được, thiếu kích thước ngoài hoặc không xác định loại hình thì dừng ga đó. Không fallback sang D300, thành 50 mm.

### 6.5. Cổ và cửa thu

- NeckPosition parse thành enum rõ: giữa ga, giữa từng tường, từng góc; không dùng tìm chuỗi rồi mặc định góc T1-T2 cho giá trị lạ.
- Tâm/lỗ cổ được tính từ lòng ga, kích thước cổ và bề dày cấu kiện theo quy ước PDF. Kiểm tra lỗ nằm trong trần, không chạm biên ngoài và không giao cống ngoài miền cho phép.
- Loại 3: cửa thu gồm lỗ thu, khoang, tường/đáy/lót và cốt thép riêng. Hướng cửa thu ánh xạ qua view transform và profile type; không ghép vào thân ga bằng một offset tùy ý.
- Cửa thu và lỗ cống phải kiểm tra giao nhau, khoảng cách biên, giao với bản đáy/trần và xung đột cốt thép. Không tự đặt dốc 2% hay kích thước không có nguồn.

### 6.6. Geometry invariants

Với từng spec đã chuẩn hóa, test:
- kích thước lòng/ngoài/đáy/lót bằng hiệu giữa extents;
- mặt cắt và mặt bằng dùng cùng tường/lỗ/vị trí cổ;
- profile kín, không tự cắt, không có bề dày âm;
- opening nằm trên đúng mặt tường, đúng cao độ/offset;
- transforms của T1–T4 cho cùng kết quả khi chiếu qua lại;
- đảo L và B đổi hướng đúng, không nhầm chiều cống;
- không sinh nắp, cổ, cống hay cửa thu từ giá trị thiếu.

## 7. RebarEngine: sinh hình học thanh và lưới

### 7.1. Parser quy cách và lưới cơ sở

Parser quy cách nhận đúng dạng trong workbook mẫu (ví dụ D10A150), trả đường kính, bước, SourceRef và lỗi có vị trí. Định nghĩa rõ ký tự phân cách, locale, khoảng trắng và trường hợp chuỗi thép góc; không ép chuỗi không nhận dạng về 0.

Với span một phương, cover c, đường kính d, khoảng thiết kế a:

- Khoảng chạy tâm thanh S = span − 2(c + d/2).
- S < 0: cấu kiện không đủ chỗ, Block.
- “Theo mật độ”: k=floor(S/a), N=k+1, dư biên r=(S−k·a)/2; tọa độ uᵢ=c+d/2+r+i·a.
- “Chia đều”: k=max(1,ceil(S/a)), bước thực aᵣₑₐₗ=S/k≤a; N=k+1; uᵢ=c+d/2+i·aᵣₑₐₗ.
- S≈0: chỉ một vị trí ở giữa miền hợp lệ. Không cho phép N=0 hoặc bước chia cho 0.

Các công thức này mô tả rải hình học; xác minh ý nghĩa tên mode và quy tắc mép bằng workbook/PDF/VHGpro output trước khi chốt bản phát hành.

### 7.2. Sinh lưới theo cấu kiện

Mỗi họ thanh mang cấu kiện, lớp/mặt, phương, diameter, spacing, coordinates, chiều dài, 0M/1M/2M và móc đầu/cuối độc lập.

- Đáy: lớp trên/lớp dưới, phương X/Y; cover theo Cbv đáy.
- Tường T1–T4: lớp trong/ngoài, thanh đứng/ngang; chiều dài thanh theo đúng chiều dài panel, cover từng mặt, điều chỉnh bước/đường kính ngang theo CF.Ga.
- Trần: lớp trên/dưới, phương X/Y; trừ opening cổ và sinh thanh tăng cường theo cấu hình.
- Cổ, tấm đan BTCT, cửa thu: profile/lưới riêng theo lựa chọn cấu kiện.
- Không dùng mặc định D10/A150 khi ô thép trống. Trường hợp không có thép chỉ được coi hợp lệ khi spec cho phép và được báo trạng thái.

### 7.3. Clip thanh tại openings

CircleOpeningClipper và RectangleOpeningClipper nhận đường thanh, opening theo hình học, cover, đường kính, clearance policy và các opening khác. Trả về danh sách đoạn theo tọa độ/chiều dài và quan hệ với thanh nguồn.

- Cắt thanh giao vùng cấm ở lỗ tròn/hộp; hỗ trợ lỗ chồng nhau, sát mép panel, đi qua đáy/trần và nhiều cống.
- Chọn vùng cấm theo mặt opening + lớp bảo vệ thanh/ống và đường kính thanh. Công thức bán kính cần xác nhận:
  Rclear = Ropening + Cbv_lỗ + d/2
  (opening đã bao gồm tường ống/vữa nếu quy ước Dkhoét như trên).
- Không tự chuyển đoạn sau clip thành hai thanh neo có móc. AnchoragePolicy riêng cần cấu hình/tham chiếu để xác định hook, chiều dài phát triển, đoạn tối thiểu và số hiệu.
- Đoạn ngắn hoặc không đủ neo: sinh Diagnostic và cờ review; không xóa khỏi schedule âm thầm.
- Dựng hướng segment nhất quán để các mark A/B, số thanh, chiều dài và hướng mũi tên giữa CAD/Excel không đảo lộn.

### 7.4. Thép tăng cường lỗ và thép buộc

- Lỗ cống: parse lựa chọn “Bo/Xiên/Vuông/Bo+…” và từng trường D28:L28. Tạo BarInstance riêng cho mỗi thanh/vòng/đai theo định nghĩa hình học nguồn; không mặc định thép xiên 45°, số lượng hoặc khoảng vươn.
- Lỗ thăm trần: dùng cấu hình C30:G30 tương ứng profile; mô hình thanh riêng với số hiệu và chiều dài đúng.
- Tie bars C/Z/U: phân bố theo vùng lưới, mật độ và chế độ lệch/đều. Thêm xử lý loại bỏ vị trí trùng với opening và thống kê đúng số lượng thực tế. Không ước lượng bằng diện tích panel/a².
- Hình dạng, nhánh, móc và chiều dài nối đọc theo trường cấu hình. “30d”, chiều dài C/Z/U là quy tắc cấu hình, không là fallback nếu trường bị thiếu.
- Tạo số vị trí cụ thể trước; schedule tổng hợp từ danh sách này.

### 7.5. Móc, thang và cấu kiện phụ

- HookSpec cho từng lớp đáy/tường/trần/cổ quy định shape 0M/1M/2M, đầu móc và chiều dài. Chế độ “Theo KC”/“Theo ĐK” có hai nhánh tính riêng; xác minh ý nghĩa “KC” và kích thước tham chiếu từ PDF, không tự diễn giải nếu mơ hồ.
- Hook length được lưu thành segment trong BarGeometry; hình Tờ 5, chiều dài cắt và Excel lấy từ cùng geometry.
- LadderSpec parse vị trí, diameter, khoảng cách bậc đầu, bước bậc và kích thước móc. Bậc chỉ sinh trong phạm vi chiều cao; vị trí trên tường tránh lỗ và cổ. Không hardcode 500/300/150×250×300 nếu workbook không quy định.
- Type2 dowels, lifting hooks, mortar, angle steel và precast slab phải bật theo profile/type; Type3 bổ sung catch basin.

## 8. ScheduleEngine: chiều dài và khối lượng

- Tách BarGeometry (đường tâm các đoạn thẳng/cung, bán kính và hook) khỏi CutLengthPolicy (quy đổi ra chiều dài cắt theo quy tắc bảng mẫu).
- Chưa có quy tắc bend deduction/allowance hoặc radius được xác nhận thì không tuyên bố chiều dài cắt chuẩn gia công. Soi mark trong workbook thép vàng/DWG mẫu cùng cấu hình trước khi khóa.
- Công thức khối lượng tham khảo:
  q = π(d/1000)²×7850/4 kg/m; kiểm tra với công thức workbook 0.006165×d² và quy tắc làm tròn nguồn. Không làm tròn mỗi phép tính trung gian.
- Thép góc lấy kg/m từ CF.Data; không dùng fallback 1.36 kg/m nếu mã không có trong bảng.
- Schedule phân nhóm bằng enum ComponentGroup (đáy, tường, trần, cổ, cửa thu, nắp, thép hình), không phân nhóm bằng Contains() trên tên tiếng Việt.
- Một BarScheduleItem ghi mark, shape, diameter/quy cách, L1…Ln, số lượng, chiều dài một thanh, tổng chiều dài, kg/m, tổng kg, nguồn và policy.
- Reconcile: tổng khối lượng item = tổng từng nhóm = ATT bảng tờ 4 = tổng trên sheet chi tiết Excel = sheet TH; sai số do format chỉ áp ở lớp hiển thị.
- Khóa ngưỡng theo từng loại dữ liệu: mark/count phải khớp chính xác; kích thước theo mm và tolerance đã thống nhất; khối lượng theo precision/cách làm tròn của bảng nguồn.
- Golden G400-1 chỉ được chốt sau khi khớp revision và cấu hình. Điều tra chênh hiện ghi nhận: khoảng 192,57 kg DBTool so với 321,92 kg bảng mẫu; chênh 129,35 kg cần giải bằng nhóm thép cổ/nắp và từng mark, không bằng cách nới tolerance.

## 9. Cad Drawer, template và năm tờ A3

### 9.1. TemplateManager và tài nguyên CAD

Trước khi vẽ, lập inventory từ Template/VHG-Template.dwg và bản mẫu:
- Block names, block extents, nested blocks, ATTDEF tag, vị trí/hướng/scale;
- layer color, linetype, lineweight, plot flag;
- DimStyle vars (DIMLFAC, DIMSCALE, DIMTXT, arrow size, precision);
- TextStyle/font/width, hatch patterns và scale;
- linetype/style dependencies và dictionary/table liên quan.

Clone tài nguyên bằng WblockCloneObjects hoặc cơ chế tương đương. Nếu đích đã có tên trùng nhưng thuộc tính khác, báo xung đột hoặc dùng bản copy có version rõ; không chọn ngầm phiên bản đang có. Bỏ catch rỗng; TemplateDiagnostic chỉ rõ tên tài nguyên lỗi.

Block Khung, COTCAO, BLT*, BLTC, Gach, TieudeMC và TK.* chỉ được điền theo tag ATTDEF thực tế. Không thay ATT bằng DBText nếu ATP yêu cầu attribute.

### 9.2. Nội dung và tiêu chí từng tờ

**Tờ 1 — Cấu tạo và mặt cắt tổng thể**
- Mặt bằng; mặt cắt cấu tạo A-A/B-B; mặt cắt thép A-A/B-B; mặt bằng thép tường.
- Mức Z, lỗ cống, kích thước lòng/phủ bì, cổ/nắp, vật liệu và ghi chú.
- Hatch có boundary hợp lệ, đảo vùng đúng, dễ phân biệt bê tông lót/BTCT/vữa/ống; text và dim không đè.
- Pass: đủ sáu view theo layout mẫu; mọi view dùng cùng GeometryModel; không có lỗi đen đặc, hatch spill hoặc cao độ/dim trùng.

**Tờ 2 — Đáy và tường T1/T2**
- Đáy lớp trên/dưới; T1/T2 lớp trong/ngoài; opening/clip, mark, dim, vạch dóng và ties.
- Pass: số vị trí và pattern khớp RebarModel; lỗ cống sạch; kích thước panel/cover rõ; mã thanh tra được về schedule.

**Tờ 3 — Tường T3/T4 và trần**
- T3/T4 lớp trong/ngoài; trần trên/dưới; lỗ mở cổ và thép tăng cường.
- Pass: đúng hướng T3/T4; opening cổ lệch tâm cùng tọa độ Tờ 1; không thanh nào xuyên vùng cấm.

**Tờ 4 — Cổ, thang và thống kê**
- Cổ ga (khi có), thang theo spec, bảng thép lắp từ block TK.*.
- Pass: tất cả mark trong BarScheduleModel xuất hiện đúng một lần theo quy cách nhóm; ATT đủ tag; tổng dòng khớp tổng summary; không tràn khung.

**Tờ 5 — Chi tiết hình dạng uốn**
- Các nhóm TieudeMC theo danh mục template; trình bày hình dạng và kích thước từng đoạn theo BarGeometry/CutLengthPolicy.
- Số nhóm 14 là mục tiêu từ mẫu, phải inventory block/nhóm thật để xác định nhóm không dùng được bỏ trống hay ẩn.
- Pass: mỗi mark schedule có hình uốn hoặc lý do loại trừ; text/mũi tên không chồng; kích thước ghi theo cùng quy ước với bảng Tờ 4.

**Tờ phụ 1:20 — cấu kiện đúc sẵn/cửa thu**
- Sinh khi có nắp BTCT đúc sẵn hoặc chi tiết cửa thu cần thể hiện riêng. Nếu cả hai có mặt, thử dàn chung; nếu không đạt khoảng trống thì tạo thêm tờ phụ và cập nhật số hiệu/publish manifest.
- Pass: tỷ lệ/footprint đúng 1:20; chỉ vẽ cấu kiện active; khối lượng các nhóm phụ reconcile với schedule.

### 9.3. Hatch, tỷ lệ và bố trí ModelSpace

- Đơn vị hình học ModelSpace là mm nếu kiểm chứng trên mẫu/host. Khung block gốc 42000×29700 scale 0.3 cho footprint 12600×8910; xác nhận bằng extents và PDF khổ ngang A3.
- Ma trận chính: X = Xbase + sheetIndex×17600; Y = Ybase − manholeIndex×12425. Tờ phụ có footprint 1:20 riêng, không dùng nhầm 1:30.
- Mỗi khung có FrameSpec, title block data và named view/plot window. Plot manifest ánh xạ ga+tờ tới page setup/PDF.
- Scale DIMLFAC/DIMSCALE/DIMTXT và text height chỉ khóa sau khi đo output trên giấy. Với geometry mm và plot 1:30, text model 60 mm cho text giấy 2 mm; đó là phép suy ra cần so với template/mẫu, không phải giá trị tự đặt.
- Hatch lấy pattern và factor từ workbook/template. Mẫu nguồn trước đó tương ứng AR-CONC factor 1→PatternScale 12, ANSI32 factor 10→PatternScale 120; phải kiểm tra lại đầu ra build hiện hành. Đạt khi PDF A3 xem ở 100% đọc được, hatch không làm đặc mặt cắt và không làm chậm plot bất thường.
- Hợp đồng V1 vẫn là năm khung A3 ModelSpace như mẫu. Không tuyên bố tương thích ATP/PUBLISH trước khi test job thực trên ATP Plotter 6.3 và AutoCAD Publish.

## 10. UI, lệnh, an toàn và triển khai

### 10.1. Luồng UI VEHOGA

1. Gõ VEHOGA → mở PaletteSet DBTool hiện có.
2. Chọn/kéo file Excel → đọc profile, hiện tên file, loại ga, trạng thái lỗi.
3. Bảng ga có tìm kiếm, checkbox chọn, L×B×H, số cống, nắp/cửa thu và khối lượng dự tính (chỉ hiển thị nếu schedule đã tính đủ).
4. Mini preview chỉ dùng GeometryModel đã tính, không tự tính lại bằng công thức UI.
5. Chọn tỷ lệ, nhóm tờ, điểm gốc, tùy chọn chi tiết thép/Excel, skip/block policy và xử lý tên ga trùng.
6. Chọn “Kiểm tra” để xem diagnostics; chỉ ga không có Block mới được vẽ.
7. Vẽ, theo dõi tiến trình từng ga/tờ; cho phép hủy an toàn giữa các ga.
8. Báo DWG entities tạo, PDF/Excel output path và lỗi Review.

Giữ VEHOGABATCH cho script/Core Console. Có thể thêm alias DBTOOL nếu cần tương thích thao tác, nhưng VEHOGA là lệnh UI chính theo yêu cầu hiện tại.

### 10.2. An toàn AutoCAD

- Excel read/geometry thuần có thể chạy nền. Autodesk DatabaseServices, Editor và transaction chỉ chạy trên document context/command queue đúng host.
- Dùng DocumentLock khi cần; transaction ngắn theo ga hoặc nhóm tờ. Lỗi giữa một ga rollback toàn bộ phần ga đó; lỗi ga kế tiếp không làm hỏng các ga trước.
- Vẽ lặp phải có chính sách: bỏ qua, thay đúng nhóm entity DBTool theo run ID, hoặc chèn bản mới có cảnh báo. Không để chạy lặp sinh bản vẽ trùng âm thầm.
- Điểm đặt do UI chọn phải lưu riêng; cancel placement không commit entities.
- Không tự save/close DWG. Bản gốc dùng read-only/backup/hash; mọi CAD integration test chạy trên bản sao trong TestOutput.
- Không gọi AutoCAD API từ Task.Run; không giữ DBObject ngoài transaction; group undo/rollback theo ga.

### 10.3. Build/load

- Ghi tên sản phẩm, version/update/build, runtime, SDK, bitness và AutoCAD managed DLL versions vào build manifest.
- .NET 8/10 theo host; AutoCAD 2026 Update 1.2+ cần target/runtime .NET 10 theo bảng Autodesk, các bản đến Update 1.1 dùng .NET 8.
- NETLOAD/PackageContents.xml hoặc cấu hình deployment có hướng dẫn; không ship Autodesk DLL bên cạnh plugin nếu license/runtime cấm.
- Kiểm thử riêng AutoCAD 2026 và Civil 3D 2026 nếu người dùng cần cả hai; DBTool hiện tạo AutoCAD entities, không cần gọi Civil 3D object model nếu không dùng đối tượng Civil.
- Autodesk references: [About Managed .NET Compatibility](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm), [About Civil 3D .NET Core Development](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-DevGuide/files/GUID-E6657034-71E5-4753-8AFD-139DC612B86D.htm).

## 11. ExcelSteelWriter

- Nguồn template: Template/VHG-DataForm.xlsx; tạo output mới KL thép [Tên dự án/ga].xlsx.
- TH chứa một hàng mỗi ga được chọn; mỗi sheet ga có mark, nhóm cấu kiện, hình dạng, đường kính/quy cách, số thanh, chiều dài thanh, tổng chiều dài, kg/m, tổng kg.
- Cấu trúc, công thức, named ranges, merged cells, styles và formula references trong template phải inventory trước khi ghi.
- Không ghi vào workbook nguồn. Nếu nghiệp vụ yêu cầu sheet Thep/THKL, cập nhật một bản copy có hậu tố _DBTool sau khi người dùng bật tùy chọn; không overwrite input.
- Ghi file tạm → mở lại kiểm tra sheet/formula/range/total → tính lại bằng Excel hoặc engine được chỉ định → rename nguyên tử thành output.
- Test cả tên sheet dài/trùng/ký tự cấm, số ga lớn hơn vùng mẫu, không có thép, và workbook có công thức. Excel output phải reconcile với CAD từ cùng SteelScheduleModel.

## 12. Roadmap và cổng nghiệm thu

| Giai đoạn | Công việc | Artifact/pass gate |
|---|---|---|
| G0 — Khóa baseline và host | Hash các workbook, PDF, template, sample DWG/DXF; ghi host/update/runtime; inventory tài nguyên CAD; xác minh lineage G400. | Manifest nguồn; report schema; block/layer/style/tag inventory; bản gốc hash trước/sau không đổi. |
| G1 — Excel contract | Hoàn thiện 3 profile, CF.Ga/Cong/Data, cell states/units/source refs; xử lý F13:H13 và các blank/default; đọc đủ 20 ga mỗi workbook. | JSON/debug dump G400-1 và 3 profile; không mất trường; lỗi ghi đúng Sheet!Cell; không dùng fallback không có nguồn. |
| G2 — Geometry | Chốt trục/offset/levels/pipe/opening/neck/catch basin; viết test thuần mm. | Regression G400 kích thước và cống; tests T1–T4 với L≠B, cống tròn/hộp, neck center/corner, giao lỗ, invert âm; invariants đạt. |
| G3 — Rebar geometry | Mesh, lớp/mặt, clipping đa opening, reinforcement, ties, hook policy, ladder. | Danh sách BarInstance có tọa độ/hình học/source; test biên phân bố, cover, số lượng, stub, cutout. Không có default D10/A150. |
| G4 — Schedule golden | Tính length/quantity/kg; truy vết mẫu vàng theo revision/cấu hình; reconcile từng mark. | Report DBTool vs workbook vàng theo mark; chính xác số lượng/mã; length/weight theo tolerance được chốt. Chưa có lineage thì gate ghi Pending, không giả đạt. |
| G5 — CAD vertical slice G400 | Nạp template vào test drawing copy, sinh Tờ 1 trước; sửa hatch, dims, text, six views. | DWG/DXF output; PDF A3 Tờ 1; kiểm tra extents/layer/style/block/tag và visual checklist. Không mở rộng 4 sheet còn lại trước khi Tờ 1 đạt. |
| G6 — Tờ 2–5 và phụ | Vẽ từ cùng Geometry/Rebar/Schedule models; bố trí và annotate. | Năm PDF A3 chính, PDF phụ khi active; từng tờ pass checklist mục 9.2; không lệch mark/số lượng/shape. |
| G7 — UI/host/transactions | Hoàn thiện PaletteSet VEHOGA, placement/cancel, progress, idempotence, undo/rollback và batch. | Smoke test NETLOAD trên host đã khóa; vẽ lại, hủy, ga lỗi, undo, reopen; không entity rác hoặc lock document. |
| G8 — Excel/export/release | Tạo Excel output và publish package; chạy 3 loại ga; test ATP/PUBLISH và PDF. | Excel mở/recalculate thành công; 5 trang A3/ga; output QA; hướng dẫn cài/load/rollback; checklist giới hạn còn lại. |

## 13. Kiểm thử và tiêu chí hoàn thành

### 13.1. Test Excel/Core

- Ba workbook loại 1–3, mỗi workbook có 20 ga: nhận diện loại và số ga; đối chiếu trường/cell cho toàn vùng cấu hình.
- Blank hợp lệ, blank không rõ nghĩa, sai chuỗi thép, pipe code thiếu, duplicate name, locale dấu phẩy, formula/cached value.
- Pipe round/box, lỗ sát chân tường, hai lỗ giao nhau, lỗ cắt bản đáy, neck tại giữa/góc, L≠B, W=0.
- Distribution: S<0, S=0, S<a, S chia hết a, số lẻ khoảng; endpoint/edge cover chính xác.
- Schedule: số lượng và chiều dài từng mark; reconciliation tổng; không mất nhóm khi mark trùng nhưng hình học khác.
- Excel output round-trip: mở lại, sheet/formulas/styles, TH ↔ sheet ga, tổng và giá trị tính lại.

### 13.2. Test CAD/host

- Chạy VEHOGABATCH trên copy của VHG-Template.dwg qua accoreconsole phù hợp với target runtime.
- Mở trên AutoCAD/Civil 3D đúng phiên bản; NETLOAD, VEHOGA, chọn Excel, chọn ga, đặt gốc, cancel, undo, chạy lại.
- Kiểm tra bằng DXF/entity audit: khung, origin/footprint, layer, color, linetype, lineweight, dimstyle, textstyle, block refs, ATT tags, hatch boundary/scale, không có entity ngoài vùng trang ngoài dự kiến.
- Plot đủ từng window thành PDF A3 ngang; xác nhận 5 trang chính mỗi ga, 1:30 đúng footprint, phụ 1:20 độc lập, đúng title/ga/số tờ.
- Review PDF ở tỷ lệ giấy bình thường và crops vùng dày: hatch, text, dim, bảng, thanh bị cắt, ký hiệu cao độ.
- So sánh với VHGpro theo cùng ga/cấu hình; nếu sample DWG chứa revision/ga khác, dùng nó làm tham chiếu đồ họa và ghi rõ không phải geometry golden.
- ATP Plotter 6.3 và AutoCAD PUBLISH pass riêng; chỉ ghi tính tương thích của đường đã thực sự chạy.

### 13.3. Điều kiện phát hành

- Không còn lỗi Block ở inputs đã chọn.
- Năm tờ CAD, schedule CAD và Excel sinh từ cùng một model; mọi tổng đối chiếu được.
- Không ghi vào file nguồn; hash file nguồn không đổi.
- Đúng version/update/runtime AutoCAD/Civil 3D; build/load/rollback được mô tả.
- Mỗi tờ có PDF A3 kiểm duyệt; báo cáo ghi rõ test tự động, host đã thử, giả định và lỗi Review còn lại.
- Đây là xác nhận hành vi phần mềm theo nguồn/mẫu đã chọn, không thay thế phê duyệt thiết kế kết cấu của kỹ sư có thẩm quyền.

## 14. Mở rộng từ Liền khối sang Nắp rời và Thăm + Thu

### 14.1. Ga nắp rời

Sau khi Loại 1 đạt G0–G8:
- Thêm profile Type2 và khóa semantics H, đỉnh tường, vữa đệm, đáy/đỉnh tấm trần.
- Bổ sung chốt thép, lỗ chốt, móc nâng, tấm đan chia, khe hở, thép tấm và thép góc theo CF.Ga/PDF.
- Geometry tạo contact interfaces tường–vữa–tấm, không cộng bù chiều cao hai lần.
- Rebar/Schedule xử lý riêng thanh chốt, móc nâng và thanh từng panel; bảng tờ 4/Excel tách nhóm nắp.
- Tờ phụ 1:20 chỉ sinh khi panel BTCT active; test Gang để bảo đảm không sinh panel/tổng thép phụ.

### 14.2. Ga thăm + thu nắp rời

- Khóa profile shift và schema hàng 17 cửa thu; phân biệt thân ga/cổ ga, hướng tường, lỗ, khoang và dày đáy/tường/lót.
- Tạo CatchBasinGeometry nối với tường đúng hướng; kiểm tra cao độ, khoảng cách tới cống/cổ và footprint.
- Rebar riêng cho cửa thu, lớp bảo vệ, openings và schedule group.
- SheetAuxDrawer mở rộng để vẽ hộp cửa thu/tấm ghi/lưới khi các đối tượng đó có nguồn dữ liệu; trigger phụ theo active feature.
- Regression riêng theo 3.0 workbook và mẫu DWG/PDF Thăm + Thu; không suy diễn dốc, lưới chắn rác hoặc steel details khi file không cấp dữ liệu.

## 15. Quyết định kỹ thuật cần chốt trước khi khóa acceptance

1. Build target theo update AutoCAD/Civil 3D thật trên máy triển khai (AutoCAD 2026 .NET 8 hay .NET 10).
2. Ý nghĩa chính xác các ô C13:H13 và các trường không đọc hiện nay; semantics h trống theo từng profile.
3. Loại cống/hình học và đơn vị trong CF.Cong; xác nhận D trong/ngoài, thành ống và cống hộp.
4. Quy ước cover, bẻ móc, neo, bán kính uốn, chiều dài cắt, đoạn tối thiểu, và các hình thức GC theo PDF/DWG vàng.
5. Lineage của bảng thép 321,92 kg so với workbook G400-1 cho 192,57 kg hiện tại; xác định mẫu golden đúng.
6. Tên/tag ATT thực của Khung/TK.* và cấu hình plot ATP/PUBLISH.
7. Khi ga có cả tấm đan BTCT và cửa thu, dùng chung tờ phụ hay tách thêm tờ.
8. Chính sách chạy lại trong bản vẽ: replace theo run/ga, skip ga đã có, hay luôn chèn mới; mặc định đề xuất là hỏi/skip, không xóa entity không thuộc DBTool.
9. Dung sai mm/khối lượng và quy tắc làm tròn theo workbook vàng.
10. Các trường cấu tạo thiếu trong workbook: báo Block hay cho phép override người dùng trong UI kèm lưu vết.

## 16. Đầu ra theo từng lần triển khai

Mỗi cổng tạo bộ reviewable artifacts trong TestOutput, không ghi lên nguồn:
- manifest/hash và bảng map Excel;
- JSON/CSV normalized spec và diagnostics;
- test report với expected/actual/tolerance/source;
- DWG/DXF test output;
- PDF A3 cho từng tờ, có tên ga, tờ, scale và build id;
- Excel output và reconciliation theo mark;
- ảnh preview/checklist do host AutoCAD/Civil 3D tạo;
- quyết định kỹ thuật đã chốt và mục cần người dùng/kỹ sư duyệt.

Bản kế hoạch này là hợp đồng kỹ thuật để bắt đầu triển khai ở bước sau. G0/G1 phải đóng trước khi khóa công thức; không coi các con số ví dụ của tài liệu hoặc fallback hiện tại trong code là tiêu chuẩn thiết kế.

