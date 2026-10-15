using System;
using System.IO;
using System.Text;
using DBTool.Core;
using DBTool.IO;

namespace DBTool.TestRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================");
            Console.WriteLine("    DBTOOL AUTOMATED VERIFICATION SUITE - GATE 1: CORE EXCEL    ");
            Console.WriteLine("================================================================");

            string baseDir = @"c:\Users\DucBao\Desktop\Dbtool\Ver 1\VHGpro 3.2";
            string file1 = Path.Combine(baseDir, "1.0.HO GA v3.2 (Thăm - Liền khối) 4.11.2025.xlsx");
            string file2 = Path.Combine(baseDir, "2.0.HO GA v3.2 (Thăm - Nắp rời) 4.11.2025.xlsx");
            string file3 = Path.Combine(baseDir, "3.0.HO GA v3.2 (Thăm+thu - Nắp rời) 4.11.2025.xlsx");

            var reader = new ExcelWorkbookReader();

            // -------------------------------------------------------------
            // TEST 1: File 1 - Ga thăm BTCT liền khối (Loại 1)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 1] Đọc File 1: Ga thăm BTCT liền khối...");
            var res1 = reader.Read(file1);
            Assert(res1.DetectedType == ManholeType.GaThamLienKhoi, "DetectedType phải là GaThamLienKhoi");
            Assert(res1.Manholes.Count == 20, $"Phải đọc được 20 hố ga, thực tế: {res1.Manholes.Count}");
            Assert(res1.PipeCatalog.ContainsKey("D200"), "Danh mục cống phải có D200");
            Assert(res1.PipeCatalog["D200"].WallThickness == 50, "Cống D200 phải có dày thành 50mm");
            Assert(res1.PipeCatalog["D300"].WallThickness == 50, "Cống D300 phải có dày thành 50mm");
            Assert(res1.AngleCatalog.ContainsKey("V30x30x3"), "Danh mục thép hình phải có V30x30x3");
            Assert(res1.HatchCatalog["ANSI32"].Scale == 10, "Mẫu hatch ANSI32 phải có tỷ lệ 10");
            Assert(res1.HatchCatalog["AR-CONC"].Scale == 1, "Mẫu hatch AR-CONC phải có tỷ lệ 1");

            // Kiểm tra chi tiết hố ga chuẩn G400-1
            var g1 = res1.Manholes[0];
            Console.WriteLine($"\n--- Kiểm tra Hố ga mẫu G400-1 (File 1) ---");
            Console.WriteLine($"Tên: {g1.Name}, Loại: {g1.Type}, Kích thước: {g1.L}x{g1.B}x{g1.H}");
            Console.WriteLine($"Bản dày: Đáy={g1.BaseThickness}, Tường={g1.WallThickness}, Trần={g1.TopThickness}");
            Console.WriteLine($"Mở rộng: Lót={g1.PadExpansion}, Đáy={g1.BaseExpansion}, Vát móng={g1.BaseHaunch}, Vát trần={g1.TopHaunch}");

            Assert(g1.Name == "G400-1", "Mã ga phải là G400-1");
            Assert(g1.L == 1000 && g1.B == 1000 && g1.H == 1200, "L=1000, B=1000, H=1200");
            Assert(g1.BaseThickness == 150 && g1.WallThickness == 150 && g1.TopThickness == 150, "F=150, t=150, Top=150");
            Assert(g1.PadExpansion == 100 && g1.PadThickness == 100, "Lót rộng 100, dày 100");
            Assert(g1.BaseExpansion == 100 && g1.BaseHaunch == 100, "Đáy rộng 100, vát móng 100");
            Assert(g1.TopHaunch == 100, "Vát trần liền khối = 100");

            // Cốt thép G400-1
            Assert(g1.BaseBottomRebar?.RawText == "D10A150", "Thép đáy dưới phải là D10A150");
            Assert(g1.BaseBottomRebar?.Diameter == 10 && g1.BaseBottomRebar?.Spacing == 150, "D=10, a=150");
            Assert(g1.BaseTopRebar?.RawText == "D10A150", "Thép đáy trên phải là D10A150");
            Assert(g1.WallOuterRebar?.RawText == "D10A150", "Thép tường ngoài phải là D10A150");
            Assert(g1.WallInnerRebar?.RawText == "D10A150", "Thép tường trong phải là D10A150");
            Assert(g1.TopTopRebar?.RawText == "D10A150", "Thép trần trên phải là D10A150");
            Assert(g1.TopBottomRebar?.RawText == "D10A150", "Thép trần dưới phải là D10A150");

            // Cống T1
            Assert(g1.PipeT1 != null, "Cống T1 phải tồn tại");
            Assert(g1.PipeT1?.PipeCode == "D200", "Cống T1 phải là D200");
            Assert(g1.PipeT1?.InnerDia == 200, "Cống T1 D trong = 200");
            Assert(g1.PipeT1?.WallThickness == 50, "Cống T1 dày thành = 50");
            Assert(g1.PipeT1?.OuterDia == 300, "Cống T1 D ngoài = 300");
            Assert(g1.PipeT1?.CutoutDia == 400, "Cống T1 D khoét = 400");
            Assert(g1.PipeT1?.CutoutBottomZ == -100, $"Cống T1 CutoutBottomZ phải là -100, thực tế {g1.PipeT1?.CutoutBottomZ}");
            Assert(g1.PipeT1?.CutoutTopZ == 300, $"Cống T1 CutoutTopZ phải là 300, thực tế {g1.PipeT1?.CutoutTopZ}");

            // Cống T2
            Assert(g1.PipeT2 != null, "Cống T2 phải tồn tại");
            Assert(g1.PipeT2?.PipeCode == "D300", "Cống T2 phải là D300");
            Assert(g1.PipeT2?.InvertLevelFromBottom == 100, "Cống T2 đáy thông thủy h = 100");
            Assert(g1.PipeT2?.CutoutBottomZ == 0, $"Cống T2 CutoutBottomZ phải là 0, thực tế {g1.PipeT2?.CutoutBottomZ}");
            Assert(g1.PipeT2?.CutoutTopZ == 500, $"Cống T2 CutoutTopZ phải là 500, thực tế {g1.PipeT2?.CutoutTopZ}");

            // Cống T3, T4
            Assert(g1.PipeT3 == null, "Cống T3 phải null");
            Assert(g1.PipeT4 == null, "Cống T4 phải null");

            // Cổ ga & Cao độ
            Assert(g1.NeckHeight == 0, "G400-1 có W = 0 (không dựng cổ)");
            Assert(g1.NeckPosition == "Góc T1-T2", "Vị trí nắp góc T1-T2");
            Assert(g1.NeckInnerWidth == 820 && g1.NeckInnerLength == 570, "Lỗ nắp 820x570");
            Assert(Math.Abs(g1.BaseInvertElevation - 55.24) < 1e-4, "Cao độ AA = 55.24m");
            Console.WriteLine("-> PASS: G400-1 (Loại 1) hoàn toàn chính xác theo hợp đồng kỹ thuật!");

            // -------------------------------------------------------------
            // TEST 2: File 2 - Ga thăm BTCT nắp rời (Loại 2)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 2] Đọc File 2: Ga thăm BTCT nắp rời...");
            var res2 = reader.Read(file2);
            Assert(res2.DetectedType == ManholeType.GaThamNapRoi, "DetectedType phải là GaThamNapRoi");
            Assert(res2.Manholes.Count == 20, $"Phải đọc được 20 hố ga, thực tế: {res2.Manholes.Count}");
            var g2 = res2.Manholes[0];
            Assert(g2.HasDowel == true, "Loại 2 phải có chốt thép");
            Assert(g2.DowelSpec?.RawText == "D18A500", "Chốt thép D18A500");
            Assert(g2.DowelLength == 500, "Chiều dài chốt thép 500");
            Assert(g2.DowelHoleDia == 25, "ĐK lỗ chốt 25");
            Assert(g2.MortarThickness == 15, "Chiều dày vữa đệm G9 = 15");
            Assert(g2.LiftingHookSpec == "2d12", "Móc nâng 2d12");
            Assert(g2.LiftingHookSize == "100x150x80", "Kích thước móc nâng 100x150x80");
            Console.WriteLine("-> PASS: Loại 2 đọc chính xác cấu hình nắp rời, chốt thép, vữa đệm, móc nâng!");

            // -------------------------------------------------------------
            // TEST 3: File 3 - Ga thăm + thu BTCT nắp rời (Loại 3)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 3] Đọc File 3: Ga thăm + thu BTCT nắp rời (độ dịch dòng +2)...");
            var res3 = reader.Read(file3);
            Assert(res3.DetectedType == ManholeType.GaThamThuNapRoi, "DetectedType phải là GaThamThuNapRoi");
            Assert(res3.Manholes.Count == 20, $"Phải đọc được 20 hố ga, thực tế: {res3.Manholes.Count}");
            var g3 = res3.Manholes[0];
            Assert(g3.CatchBasinPosition == "Thân ga", "Vị trí cửa thu: Thân ga");
            Assert(g3.CatchBasinWallDir == "Phải T1", "Hướng cửa thu: Phải T1");
            Assert(g3.CatchBasinHoleWidth == 850 && g3.CatchBasinHoleHeight == 250, "Kích thước lỗ cửa thu 850x250");
            Assert(g3.CatchBasinChamberLength == 500 && g3.CatchBasinChamberHeight == 250, "Khoang cửa thu 500x250");
            Assert(g3.CatchBasinWallThickness == 125, "Dày tường cửa thu 125");
            Assert(g3.CatchBasinBaseThickness == 125, "Dày đáy cửa thu 125");
            Assert(g3.CatchBasinPadThickness == 100, "Dày lót cửa thu 100");
            Assert(g3.CatchBasinPadExpansion == 50, "Mở rộng lót cửa thu 50");
            Assert(g3.CatchBasinStructure == "BTCT", "Kết cấu cửa thu BTCT");
            Assert(g3.CatchBasinRebar?.RawText == "D10A150", "Cốt thép cửa thu D10A150");

            // Kiểm tra các khối bị dịch dòng sau cửa thu
            Assert(g3.CoverCatchBasin == 25, "Lớp bảo vệ cửa thu C19 = 25");
            Assert(g3.TieSpec?.RawText == "D6A500", "Thép buộc hàng 25 phải là D6A500");
            Assert(g3.LadderDia == 18, "Đường kính thang thép hàng 27 phải là 18");
            Assert(g3.PipeMortar == 50, "Vữa trát cống hàng 30 phải là 50");
            Assert(g3.TopHoleReinforceDia == 10, "Gia cường lỗ trần hàng 32 phải là 10");
            Assert(g3.IsAbsoluteElevation == true, "Cao trình hàng 34 là Tuyệt đối");
            Assert(g3.MaterialPad == "BT lót M100 đá 2x4", "Vật liệu lót hàng 36");
            Assert(g3.HatchBase.PatternName == "ANSI32", "Hatch đáy hàng 38 là ANSI32");
            Console.WriteLine("-> PASS: Loại 3 đọc chính xác toàn bộ cấu hình với độ dịch dòng +2!");

            // -------------------------------------------------------------
            // TEST 4: Kiểm tra Provenance Tracking & Diagnostics
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 4] Kiểm tra Lưu vết Provenance Tracking và Diagnostics...");
            Assert(g1.BaseBottomRebar?.Source != null, "Rebar phải có SourceRef");
            Assert(g1.BaseBottomRebar?.Source?.SheetName == "GaThamLienKhoi", "SheetName phải là GaThamLienKhoi");
            Assert(g1.BaseBottomRebar?.Source?.CellAddress == "G4", "CellAddress phải là G4");
            Assert(g1.BaseBottomRebar?.Source?.RawValue == "D10A150", "RawValue phải là D10A150");
            Console.WriteLine($"Lưu vết thành công: {g1.BaseBottomRebar?.Source}");

            int blockCount1 = res1.Diagnostics.FindAll(d => d.Severity == DiagnosticSeverity.Block).Count;
            int blockCount2 = res2.Diagnostics.FindAll(d => d.Severity == DiagnosticSeverity.Block).Count;
            int blockCount3 = res3.Diagnostics.FindAll(d => d.Severity == DiagnosticSeverity.Block).Count;
            Assert(blockCount1 == 0, $"File 1 không được có lỗi Block (thực tế: {blockCount1})");
            Assert(blockCount2 == 0, $"File 2 không được có lỗi Block (thực tế: {blockCount2})");
            Assert(blockCount3 == 0, $"File 3 không được có lỗi Block (thực tế: {blockCount3})");

            // -------------------------------------------------------------
            // TEST 5: GATE 2 - Kiểm tra Động cơ Hình học (GeometryEngine)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 5] GATE 2 - Kiểm tra Động cơ Hình học (GeometryEngine)...");
            var geom = GeometryEngine.Build(g1);

            // 1. Mặt bằng (Plan)
            Console.WriteLine("--- Kiểm tra Mặt bằng G400-1 ---");
            Assert(geom.Plan.PadRect.Width == 1700 && geom.Plan.PadRect.Height == 1700,
                $"Kích thước Lớp lót: {geom.Plan.PadRect.Width}x{geom.Plan.PadRect.Height} (kỳ vọng 1700x1700)");
            Assert(geom.Plan.BaseRect.Width == 1500 && geom.Plan.BaseRect.Height == 1500,
                $"Kích thước Móng đáy: {geom.Plan.BaseRect.Width}x{geom.Plan.BaseRect.Height} (kỳ vọng 1500x1500)");
            Assert(geom.Plan.OuterWallRect.Width == 1300 && geom.Plan.OuterWallRect.Height == 1300,
                $"Kích thước Phủ bì thân ga: {geom.Plan.OuterWallRect.Width}x{geom.Plan.OuterWallRect.Height} (kỳ vọng 1300x1300)");
            Assert(geom.Plan.InnerChamberRect.Width == 1000 && geom.Plan.InnerChamberRect.Height == 1000,
                $"Kích thước Lòng trong: {geom.Plan.InnerChamberRect.Width}x{geom.Plan.InnerChamberRect.Height} (kỳ vọng 1000x1000)");
            Assert(geom.Plan.NeckOpeningRect.Width == 820 && geom.Plan.NeckOpeningRect.Height == 570,
                $"Kích thước Lỗ mở nắp: {geom.Plan.NeckOpeningRect.Width}x{geom.Plan.NeckOpeningRect.Height} (kỳ vọng 820x570)");
            Assert(geom.Plan.Pipes.Count == 2, $"Số lượng cống trên mặt bằng: {geom.Plan.Pipes.Count} (kỳ vọng 2)");

            // 2. Mặt cắt A-A
            Console.WriteLine("\n--- Kiểm tra Mặt cắt A-A G400-1 ---");
            var secA = geom.SectionA;
            Assert(secA.IsSectionA == true, "secA.IsSectionA == true");
            Assert(secA.SpanLength == 1000, "Nhịp trong mặt cắt A-A (chiều rộng B) = 1000");
            Assert(secA.ZPadBottom == -250, $"Cao độ đáy lót: {secA.ZPadBottom} (kỳ vọng -250)");
            Assert(secA.ZBaseBottom == -150, $"Cao độ đáy BTCT: {secA.ZBaseBottom} (kỳ vọng -150)");
            Assert(secA.ZInsideBottom == 0, $"Cao độ mặt đáy trong: {secA.ZInsideBottom} (kỳ vọng 0)");
            Assert(secA.ZInsideTop == 1200, $"Cao độ đỉnh trong: {secA.ZInsideTop} (kỳ vọng 1200)");
            Assert(secA.ZTopSlabTop == 1350, $"Cao độ đỉnh bản trần: {secA.ZTopSlabTop} (kỳ vọng 1350)");
            Assert(secA.ZCoverTop == 1450, $"Cao độ đỉnh nắp ga: {secA.ZCoverTop} (kỳ vọng 1450)");

            Assert(secA.RightPipe != null, "Mặt cắt A-A phải có cống bên phải (T1)");
            Assert(secA.RightPipe?.Direction == WallDirection.T1, "Cống bên phải là T1");
            Assert(secA.RightPipe?.CutoutBottomZ == -100, $"T1 CutoutBottomZ = {secA.RightPipe?.CutoutBottomZ} (cắt sâu 100mm vào móng đáy)");
            Assert(secA.RightPipe?.CutoutTopZ == 300, $"T1 CutoutTopZ = {secA.RightPipe?.CutoutTopZ}");
            Assert(secA.LeftPipe == null, "Mặt cắt A-A cống bên trái (T3) = null");

            // 3. Mặt cắt B-B
            Console.WriteLine("\n--- Kiểm tra Mặt cắt B-B G400-1 ---");
            var secB = geom.SectionB;
            Assert(secB.IsSectionA == false, "secB.IsSectionA == false");
            Assert(secB.SpanLength == 1000, "Nhịp trong mặt cắt B-B (chiều dài L) = 1000");
            Assert(secB.RightPipe != null, "Mặt cắt B-B phải có cống bên phải (T2)");
            Assert(secB.RightPipe?.Direction == WallDirection.T2, "Cống bên phải là T2");
            Assert(secB.RightPipe?.CutoutBottomZ == 0, $"T2 CutoutBottomZ = {secB.RightPipe?.CutoutBottomZ} (đáy khoét trùng mặt đáy trong)");
            Assert(secB.RightPipe?.CutoutTopZ == 500, $"T2 CutoutTopZ = {secB.RightPipe?.CutoutTopZ}");
            Assert(secB.LeftPipe == null, "Mặt cắt B-B cống bên trái (T4) = null");

            // -------------------------------------------------------------
            // TEST 6: GATE 3 - Kiểm tra Động cơ Cốt thép & Thống kê (RebarEngine & ScheduleEngine)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 6] GATE 3 - Kiểm tra Động cơ Cốt thép & Thống kê...");
            var bars = RebarEngine.GenerateRebarModel(g1, geom, res1.AngleCatalog);
            Assert(bars.Count > 0, $"Số lượng thanh cốt thép sinh ra: {bars.Count} (phải > 0)");

            // Kiểm tra các nhóm cấu kiện
            var baseBars = bars.FindAll(b => b.ComponentGroup.Contains("Bản đáy"));
            Assert(baseBars.Count >= 4, $"Số loại thanh bản đáy: {baseBars.Count} (phải >= 4)");

            var wallBars = bars.FindAll(b => b.ComponentGroup.Contains("Tường"));
            Assert(wallBars.Count > 0, "Phải có cốt thép tường");

            var ladderBars = bars.FindAll(b => b.ComponentGroup.Contains("Thang"));
            Assert(ladderBars.Count == 1, "Phải có 1 nhóm thang thép");
            Assert(ladderBars[0].Diameter == 18, "Thang leo D18");
            Assert(ladderBars[0].LengthMm == 1100, $"Chiều dài uốn 1 bậc thang: {ladderBars[0].LengthMm}mm (kỳ vọng 1100mm)");
            Assert(ladderBars[0].Quantity > 0, $"Số bậc thang: {ladderBars[0].Quantity}");

            // Thống kê khối lượng (ScheduleEngine)
            var schedule = ScheduleEngine.CalculateSchedule(g1, bars, res1.AngleCatalog);
            Assert(schedule.Items.Count == bars.Count, "Số mục trong bảng thống kê phải bằng số thanh sinh ra");
            Assert(schedule.Summary.Tong > 0, $"Tổng khối lượng cốt thép ga G400-1: {schedule.Summary.Tong:F2} kg (phải > 0)");
            Console.WriteLine($"\n--- Bảng tổng hợp khối lượng ga G400-1 ---");
            Console.WriteLine($"Bản đáy (D<=10): {schedule.Summary.Day1:F2} kg | (D>10): {schedule.Summary.Day2:F2} kg");
            Console.WriteLine($"Tường   (D<=10): {schedule.Summary.Tuong1:F2} kg | (D>10): {schedule.Summary.Tuong2:F2} kg");
            Console.WriteLine($"Trần    (D<=10): {schedule.Summary.Tran1:F2} kg | (D>10): {schedule.Summary.Tran2:F2} kg");
            Console.WriteLine($"Cổ ga   (D<=10): {schedule.Summary.Co1:F2} kg | (D>10): {schedule.Summary.Co2:F2} kg");
            Console.WriteLine($"Thép hình góc  : {schedule.Summary.ThepHinh:F2} kg");
            Console.WriteLine($"TỔNG CỘNG      : {schedule.Summary.Tong:F2} kg");

            // Kiểm tra tính nhất quán toán học
            double sumFromItems = schedule.Items.Sum(it => it.TotalWeightKg);
            Assert(Math.Abs(sumFromItems - schedule.Summary.Tong) < 1e-4,
                $"Tổng chi tiết ({sumFromItems:F2}) phải bằng đúng Tổng tổng hợp ({schedule.Summary.Tong:F2})");

            // -------------------------------------------------------------
            // TEST 7: GATE 5 (Một phần) - Xuất Excel Khối lượng Thép An Toàn
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 7] GATE 5 - Kiểm tra Xuất Excel Khối lượng Thép (ExcelSteelWriter)...");
            string templateForm = Path.Combine(baseDir, "Template", "VHG-DataForm.xlsx");
            string testOutDir = @"c:\Users\DucBao\Desktop\Dbtool\Ver 1\DBTool\TestOutput";

            var manholeList = new List<(NormalizedManholeSpec, SteelScheduleModel)>
            {
                (g1, schedule)
            };

            string exportedPath = ExcelSteelWriter.ExportSteelScheduleWorkbook(
                templateForm, testOutDir, g1.Name, manholeList);
            Assert(File.Exists(exportedPath), $"Tệp xuất bản phải tồn tại: {exportedPath}");
            Console.WriteLine($"Đã xuất tệp thành công: {exportedPath}");

            // Mở lại tệp xuất bằng ClosedXML để kiểm tra
            using (var wbCheck = new ClosedXML.Excel.XLWorkbook(exportedPath))
            {
                Assert(wbCheck.Worksheets.Contains("TH"), "Tệp xuất phải có sheet TH");
                Assert(wbCheck.Worksheets.Contains("G400-1"), "Tệp xuất phải có sheet G400-1");
                Assert(!wbCheck.Worksheets.Contains("Temp"), "Sheet Temp gốc phải đã được dọn dẹp xóa bỏ");

                var wsCheck = wbCheck.Worksheet("G400-1");
                Assert(wsCheck.Cell(4, 12).GetString() == "G400-1", "Ô L4 phải chứa mã ga G400-1");
                Assert(wsCheck.Cell(5, 1).GetDouble() == 1, "Dòng 5 cột A phải bắt đầu từ TT=1");
                Assert(wsCheck.Cell(5, 2).GetString().Contains("Bản đáy"), "Dòng 5 cột B phải là cấu kiện Bản đáy");
            }

            // Kiểm tra cập nhật bản sao workbook nguồn
            string dupSource = ExcelSteelWriter.SyncToDuplicateSourceWorkbook(file1, testOutDir, manholeList);
            Assert(File.Exists(dupSource), $"Bản sao workbook nguồn phải tồn tại: {dupSource}");
            Assert(dupSource != file1, "Đường dẫn bản sao phải khác đường dẫn tệp gốc để bảo vệ dữ liệu người dùng");
            using (var wbDup = new ClosedXML.Excel.XLWorkbook(dupSource))
            {
                var wsThep = wbDup.Worksheet("Thep");
                Assert(wsThep.Cell(5, 2).GetString() == "G400-1", "Dòng 5 sheet Thep phải chứa G400-1");
                Assert(wsThep.Cell(5, 3).GetDouble() > 0, "Bản đáy D<=10 phải có số liệu > 0");
            }

            Console.WriteLine("\n==================================================================================");
            Console.WriteLine(">>> TẤT CẢ CÁC BÀI KIỂM THỬ GATE 1, GATE 2, GATE 3 & EXCEL ĐÃ VƯỢT QUA 100%! <<<");
            Console.WriteLine("==================================================================================");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] ASSERTION FAILED: {message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [OK] {message}");
                Console.ResetColor();
            }
        }
    }
}
