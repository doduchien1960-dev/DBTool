using System;
using System.IO;
using System.Text;
using ClosedXML.Excel;
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

            // REGRESSION: unknown pipe codes must never fabricate construction geometry.
            string unknownPipeWorkbook = Path.Combine(Path.GetTempPath(), $"DBTool_unknown_pipe_{Guid.NewGuid():N}.xlsx");
            using (var workbookWithUnknownPipe = new XLWorkbook(file1))
            {
                workbookWithUnknownPipe.Worksheet("GaThamLienKhoi").Cell("O4").Value = "D999";
                workbookWithUnknownPipe.SaveAs(unknownPipeWorkbook);
            }
            try
            {
                var unknownPipeResult = reader.Read(unknownPipeWorkbook);
                var unknownPipeManhole = unknownPipeResult.Manholes.Find(m => m.Name == "G400-1");
                var unknownPipeDiagnostic = unknownPipeResult.Diagnostics.Find(d =>
                    d.ManholeName == "G400-1" && d.Source?.SheetName == "GaThamLienKhoi" && d.Source.CellAddress == "O4");
                Assert(unknownPipeManhole?.PipeT1 == null,
                    "Mã cống không có trong CF.Cong không được biến thành cống ước lượng");
                Assert(unknownPipeDiagnostic?.Severity == DiagnosticSeverity.Block,
                    "Mã cống không có trong CF.Cong phải tạo lỗi Block tại ô O4");
            }
            finally
            {
                if (File.Exists(unknownPipeWorkbook)) File.Delete(unknownPipeWorkbook);
            }

            string missingDataWorkbook = Path.Combine(Path.GetTempPath(), $"DBTool_missing_data_{Guid.NewGuid():N}.xlsx");
            using (var workbookWithoutDataCatalog = new XLWorkbook(file1))
            {
                workbookWithoutDataCatalog.Worksheet("CF.Data").Delete();
                workbookWithoutDataCatalog.SaveAs(missingDataWorkbook);
            }
            try
            {
                var missingDataResult = reader.Read(missingDataWorkbook);
                Assert(missingDataResult.AngleCatalog.Count == 0 && missingDataResult.HatchCatalog.Count == 0,
                    "Thiếu CF.Data không được tự chèn catalog thép/hatch mặc định");
                Assert(missingDataResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block &&
                       d.Source?.SheetName == "CF.Data"),
                    "Thiếu CF.Data phải tạo lỗi Block tại cấp workbook");
            }
            finally
            {
                if (File.Exists(missingDataWorkbook)) File.Delete(missingDataWorkbook);
            }

            var gateGoodSpec = new NormalizedManholeSpec { Name = "G500-1" };
            var gateBadSpec = new NormalizedManholeSpec { Name = "G400-1" };
            var gateDiagnostics = new[]
            {
                new Diagnostic(DiagnosticSeverity.Block, "G400-1", new SourceRef("fixture.xlsx", "Ga", "O4", "D999"), "Cống không có trong catalog."),
                new Diagnostic(DiagnosticSeverity.Review, "G500-1", new SourceRef("fixture.xlsx", "Ga", "P4", ""), "Cao độ cần xem lại.")
            };
            Assert(DrawingInputGate.GetBlockingDiagnostics(new[] { gateGoodSpec }, gateDiagnostics).Count == 0,
                "Lỗi Block của ga không được chọn không chặn ga hợp lệ khác");
            Assert(DrawingInputGate.GetBlockingDiagnostics(new[] { gateBadSpec }, gateDiagnostics).Count == 1,
                "Ga có lỗi Block phải bị chặn trước khi vào AutoCAD transaction");
            var workbookLevelBlock = new Diagnostic(
                DiagnosticSeverity.Block, "", new SourceRef("fixture.xlsx", "CF.Ga", "A1", ""), "Thiếu sheet cấu hình.");
            Assert(DrawingInputGate.GetBlockingDiagnostics(new[] { gateGoodSpec }, gateDiagnostics.Append(workbookLevelBlock)).Count == 1,
                "Lỗi cấu trúc workbook phải chặn mọi ga được chọn");

            var sheet4Columns = Sheet4TableLayout.CalculateTwoColumn(
                frameWidth: 12600, firstColumnX: 4700, blockBaseWidth: 12500,
                scale: 0.270, minRightMargin: 400, minColumnGap: 300);
            Assert(Math.Abs(sheet4Columns.RightEdge - 12200) < 1e-9 && sheet4Columns.RightMargin >= 400,
                "Bảng Tờ 4 phải chừa ít nhất 400 mm tới mép trong khung A3");
            Assert(sheet4Columns.InterColumnGap >= 300,
                "Hai cột bảng Tờ 4 phải giữ tối thiểu 300 mm khoảng hở");
            bool overwideSheet4Rejected = false;
            try
            {
                Sheet4TableLayout.CalculateTwoColumn(
                    frameWidth: 12600, firstColumnX: 5000, blockBaseWidth: 12500,
                    scale: 0.300, minRightMargin: 400, minColumnGap: 300);
            }
            catch (ArgumentOutOfRangeException) { overwideSheet4Rejected = true; }
            Assert(overwideSheet4Rejected, "Bảng Tờ 4 không được âm thầm tràn khung khi block quá rộng");

            // Cống T3, T4
            Assert(g1.PipeT3 == null, "Cống T3 phải null");
            Assert(g1.PipeT4 == null, "Cống T4 phải null");

            // Cổ ga & Cao độ
            Assert(g1.NeckHeight == 0, "G400-1 có W = 0 (không dựng cổ)");
            Assert(g1.NeckPosition == "Góc T1-T2", "Vị trí nắp góc T1-T2");
            Assert(g1.NeckInnerWidth == 820 && g1.NeckInnerLength == 570, "Lỗ nắp 820x570");
            Assert(g1.CoverPanelCount == 2 && g1.CoverGap == 5 && g1.CoverPanelDirection == "Ngang",
                "Số tấm, khe hở và chiều đặt đọc từ CF.Ga!I13:K13");
            Assert(g1.PrecastCover.BottomLayerBarDiameter == 10, "Thép lớp dưới tấm đan lấy từ CF.Ga!C15");
            Assert(g1.PrecastCover.TopLayerBarDiameter == 8, "Thép lớp trên tấm đan lấy từ CF.Ga!D15");
            Assert(g1.PrecastCover.BarSpacingMm == 100, "Mật độ thép tấm đan lấy từ CF.Ga!E15");
            Assert(g1.PrecastCover.TieRebar?.RawText == "D6A400", "Thép buộc tấm đan lấy từ CF.Ga!F15");
            Assert(g1.PrecastCover.TieShape == "Chữ Z", "Kiểu thép buộc tấm đan lấy từ CF.Ga!G15");
            Assert(g1.PrecastCover.AngleSteelCode == "V50x50x3" && g1.PrecastCover.AngleSteelQuantity == 2,
                "Thép góc và số lượng lấy từ CF.Ga!H15:I15");
            Assert(g1.PrecastCover.AngleAnchorRebar?.RawText == "D6A200L100", "Neo thép góc tấm đan lấy từ CF.Ga!J15");
            Assert(g1.PrecastCover.LiftingHookSpec == "2D8" && g1.PrecastCover.LiftingHookSize == "100x100x80",
                "Móc nâng và kích thước lấy từ CF.Ga!K15:L15");
            Assert(g1.PrecastCover.CoverRatioPercent == 20, "Tỷ lệ bảo vệ tấm đan lấy từ CF.Ga!M15");
            Assert(g1.PrecastCover.BottomLayerBarDiameterSource?.CellAddress == "C15" &&
                   g1.PrecastCover.AngleAnchorRebar?.Source?.CellAddress == "J15",
                "Cấu hình thép tấm đan phải giữ SourceRef đến từng ô CF.Ga");
            Assert(Math.Abs(g1.BaseInvertElevation - 55.24) < 1e-4, "Cao độ AA = 55.24m");
            Console.WriteLine("-> PASS: Các trường Excel G400-1 được kiểm tra khớp dữ liệu tham chiếu; schedule chưa được đối chiếu tại đây.");

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

            string missingCatchBasinConfigWorkbook = Path.Combine(Path.GetTempPath(), $"DBTool_missing_catch_basin_rebar_{Guid.NewGuid():N}.xlsx");
            using (var workbookWithoutCatchBasinRebar = new XLWorkbook(file3))
            {
                workbookWithoutCatchBasinRebar.Worksheet("CF.Ga").Cell("M17").Clear(XLClearOptions.Contents);
                workbookWithoutCatchBasinRebar.SaveAs(missingCatchBasinConfigWorkbook);
            }
            try
            {
                var missingCatchBasinConfigResult = reader.Read(missingCatchBasinConfigWorkbook);
                Assert(missingCatchBasinConfigResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block &&
                       d.Source?.SheetName == "CF.Ga" && d.Source.CellAddress == "M17"),
                    "Thiếu thép cửa thu BTCT phải báo lỗi Block tại CF.Ga!M17");
            }
            finally
            {
                if (File.Exists(missingCatchBasinConfigWorkbook)) File.Delete(missingCatchBasinConfigWorkbook);
            }

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
            Assert(secA.RightPipe?.ExtensionRect.MinY == -50,
                $"Đáy ngoài ống T1 phải thấp hơn invert trong 50mm thành ống; thực tế {secA.RightPipe?.ExtensionRect.MinY}");
            Assert(secA.RightPipe?.ExtensionRect.MaxY == 250,
                $"Đỉnh ngoài ống T1 phải là invert + D trong + thành ống; thực tế {secA.RightPipe?.ExtensionRect.MaxY}");
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
            Assert(secB.RightPipe?.ExtensionRect.MinY == 50,
                $"Đáy ngoài ống T2 phải là invert trừ thành ống; thực tế {secB.RightPipe?.ExtensionRect.MinY}");
            Assert(secB.RightPipe?.ExtensionRect.MaxY == 450,
                $"Đỉnh ngoài ống T2 phải là invert + D trong + thành ống; thực tế {secB.RightPipe?.ExtensionRect.MaxY}");
            Assert(secB.LeftPipe == null, "Mặt cắt B-B cống bên trái (T4) = null");

            // -------------------------------------------------------------
            // REGRESSION: wall bars, wall openings, and bar schedule use one layout.
            // -------------------------------------------------------------
            Console.WriteLine("\n[REGRESSION] Lưới thép tường theo đúng lớp, cấp thép và đoạn cắt...");
            var wallPipe = new PipeConnectionSpec
            {
                Direction = WallDirection.T1,
                PipeCode = "D100",
                InvertLevelFromBottom = 200,
                OffsetFromCenter = 0,
                MortarThickness = 10,
                PipeCatalogInfo = new PipeRecord("D100", false, 100, 20, 140, 0, 0, 0, false)
            };
            var wallProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "WALL-PROBE",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 120,
                WallThickness = 100,
                TopThickness = 120,
                CoverBottom = 30,
                CoverWall = 30,
                CoverTop = 30,
                CoverPipe = 15,
                WallOuterRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                WallInnerRebar = new RebarSpec { Diameter = 8, Spacing = 80, RawText = "D8A80" },
                WallHorizSpacingDelta = 20,
                WallHorizDiaStepDown = 1,
                RebarDistributionMode = "Chia đều",
                RebarHookMode = "Theo KC",
                RebarHookDiaMultiple = 10,
                HookWallOuter = "2M",
                HookWallInner = "0M",
                PipeT1 = wallPipe
            };

            var wallLayout = RebarEngine.BuildWallRebarLayout(wallProbe, WallDirection.T1, wallPipe);
            Assert(wallLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Vertical && s.BarMark == "T1A"),
                "Thanh đứng phía dưới lỗ có mark T1A");
            Assert(wallLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Vertical && s.BarMark == "T1B"),
                "Thanh đứng phía trên lỗ có mark T1B");
            Assert(wallLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Horizontal && s.BarMark == "T2A") &&
                   wallLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Horizontal && s.BarMark == "T2B"),
                "Thanh ngang hai phía lỗ có mark T2A và T2B");
            Assert(wallLayout.Where(s => s.IsOuterLayer && s.Axis == WallBarAxis.Horizontal).All(s => s.Diameter == 8),
                "D10 giảm một cấp theo D7 phải thành D8, không phải D9");
            Assert(wallLayout.Where(s => !s.IsOuterLayer).All(s => s.Diameter <= 8),
                "Lưới tường lớp trong lấy quy cách D8 độc lập với lớp ngoài D10");
            Assert(wallLayout.Where(s => s.IsOuterLayer && s.BarMark is "T1A" or "T1B" or "T2A" or "T2B")
                    .All(s => s.LengthMm > 0),
                "Mỗi nhánh thép cắt có chiều dài hình học dương");

            var wallScheduleProbe = RebarEngine.GenerateRebarModel(
                wallProbe, GeometryEngine.Build(wallProbe), new Dictionary<string, SteelAngleRecord>());
            var innerWallScheduleProbe = wallScheduleProbe.Where(b =>
                b.ComponentGroup == "Tường T1 - lớp trong").ToList();
            Assert(innerWallScheduleProbe.Count > 0 &&
                   innerWallScheduleProbe.Where(b => b.BarMark.StartsWith("T1", StringComparison.Ordinal) && b.BarMark is "T1" or "T1A" or "T1B" or "T1C")
                       .All(b => b.Diameter == 8) &&
                   innerWallScheduleProbe.Where(b => b.BarMark.StartsWith("T2", StringComparison.Ordinal))
                       .All(b => b.Diameter == 6),
                "Schedule lớp trong giữ D8 đứng và giảm theo cấp thành D6 ngang");
            Assert(innerWallScheduleProbe.Any(b => b.BarMark == "T1A") &&
                   innerWallScheduleProbe.Any(b => b.BarMark == "T1B"),
                "Schedule lớp trong phải có cả hai nhánh thanh đứng bị cắt");

            Console.WriteLine("\n[REGRESSION] Cắt lưới quanh cống hộp theo đúng B×H...");
            var boxPipe = wallPipe with
            {
                PipeCode = "H300x200",
                InvertLevelFromBottom = 150,
                OffsetFromCenter = 0,
                MortarThickness = 10,
                PipeCatalogInfo = new PipeRecord("H300x200", true, 0, 50, 0, 300, 200, 40, false)
            };
            var boxProbe = new NormalizedManholeSpec
            {
                Type = wallProbe.Type,
                Name = "BoxProbe",
                L = wallProbe.L,
                B = wallProbe.B,
                H = wallProbe.H,
                BaseThickness = wallProbe.BaseThickness,
                WallThickness = wallProbe.WallThickness,
                TopThickness = wallProbe.TopThickness,
                CoverBottom = wallProbe.CoverBottom,
                CoverWall = wallProbe.CoverWall,
                CoverTop = wallProbe.CoverTop,
                CoverPipe = wallProbe.CoverPipe,
                WallOuterRebar = wallProbe.WallOuterRebar,
                WallInnerRebar = wallProbe.WallInnerRebar,
                WallHorizSpacingDelta = wallProbe.WallHorizSpacingDelta,
                WallHorizDiaStepDown = wallProbe.WallHorizDiaStepDown,
                RebarDistributionMode = wallProbe.RebarDistributionMode,
                RebarHookMode = wallProbe.RebarHookMode,
                RebarHookDiaMultiple = wallProbe.RebarHookDiaMultiple,
                HookWallOuter = wallProbe.HookWallOuter,
                HookWallInner = wallProbe.HookWallInner,
                PipeT1 = boxPipe
            };
            var boxLayout = RebarEngine.BuildWallRebarLayout(boxProbe, WallDirection.T1, boxPipe);
            Assert(boxPipe.CutoutWidth == 420 && boxPipe.CutoutHeight == 320,
                $"Kích thước lỗ cống hộp phải gồm thành cống và vữa ({boxPipe.CutoutWidth}×{boxPipe.CutoutHeight})");
            Assert(boxLayout.Any(s => s.IsOuterLayer && s.BarMark == "T1A" && Math.Abs(s.EndMm - 70) < 1e-6) &&
                   boxLayout.Any(s => s.IsOuterLayer && s.BarMark == "T1B" && Math.Abs(s.StartMm - 430) < 1e-6),
                "Thanh đứng phải dừng ngoài hai mép trên/dưới lỗ hộp cộng lớp hở bảo vệ");
            Assert(boxLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Horizontal && s.BarMark == "T2A") &&
                   boxLayout.Any(s => s.IsOuterLayer && s.Axis == WallBarAxis.Horizontal && s.BarMark == "T2B"),
                "Thanh ngang phải tách thành hai nhánh ở hai bên lỗ hộp");
            var boxGeometry = GeometryEngine.Build(boxProbe);
            var boxPlanPipe = boxGeometry.Plan.Pipes.Single(p => p.Direction == WallDirection.T1);
            Assert(Math.Abs(Math.Abs(boxPlanPipe.WallCutoutPoint2.X - boxPlanPipe.WallCutoutPoint1.X) - boxPipe.CutoutWidth) < 1e-6,
                "Mặt bằng phải dùng bề rộng lỗ hộp, không dùng đường kính bằng 0");
            var boxSectionPipe = boxGeometry.SectionA.LeftPipe ?? boxGeometry.SectionA.RightPipe;
            Assert(boxSectionPipe is not null &&
                   Math.Abs(boxSectionPipe.ExtensionRect.Height - boxPipe.OuterHeight) < 1e-6 &&
                   Math.Abs(boxSectionPipe.CutoutRect.Height - boxPipe.CutoutHeight) < 1e-6,
                "Mặt cắt phải dùng chiều cao phủ bì và lỗ hộp");

            Console.WriteLine("\n[REGRESSION] Móc thép đúng cấu hình và đúng đầu thanh còn nguyên...");
            var hookProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "HookProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                NeckHeight = 300,
                NeckWallThickness = 100,
                NeckInnerWidth = 100,
                NeckInnerLength = 100,
                NeckPosition = "Giữa ga",
                CoverBottom = 30,
                CoverWall = 30,
                CoverTop = 30,
                CoverNeck = 25,
                BaseTopRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                BaseBottomRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                WallOuterRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                WallInnerRebar = new RebarSpec { Diameter = 8, Spacing = 100, RawText = "D8A100" },
                TopTopRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                TopBottomRebar = new RebarSpec { Diameter = 10, Spacing = 100, RawText = "D10A100" },
                NeckOuterRebar = new RebarSpec { Diameter = 10, Spacing = 150, RawText = "D10A150" },
                NeckInnerRebar = new RebarSpec { Diameter = 8, Spacing = 150, RawText = "D8A150" },
                HookBaseTop = "1M",
                HookBaseBottom = "1M",
                HookTopTop = "2M",
                HookTopBottom = "2M",
                HookNeckOuter = "1M",
                HookNeckInner = "0M",
                RebarDistributionMode = "Chia đều",
                RebarHookMode = "Theo KC",
                RebarHookDiaMultiple = 10
            };
            var hookProbeBars = RebarEngine.GenerateRebarModel(
                hookProbe, GeometryEngine.Build(hookProbe), new Dictionary<string, SteelAngleRecord>());
            var hookedBaseTop = hookProbeBars.Single(b => b.ComponentGroup == "Bản đáy - lớp trên" && b.BarMark == "D1");
            var hookedBaseBottom = hookProbeBars.Single(b => b.ComponentGroup == "Bản đáy - lớp dưới" && b.BarMark == "D1");
            Assert(hookedBaseTop.Shape == BarShapeType.L1M && hookedBaseTop.LT == 90 && hookedBaseTop.LP == 0 &&
                   Math.Abs(hookedBaseTop.LengthMm - (hookedBaseTop.L + 90)) < 1e-6,
                "Thép lớp trên bản đáy phải theo cấu hình 1M và chỉ cộng một móc");
            Assert(hookedBaseBottom.Shape == BarShapeType.L1M && hookedBaseBottom.LT == 90 && hookedBaseBottom.LP == 0 &&
                   Math.Abs(hookedBaseBottom.LengthMm - (hookedBaseBottom.L + 90)) < 1e-6,
                "Thép lớp dưới bản đáy phải hỗ trợ cấu hình 1M");
            var roofCutBars = hookProbeBars.Where(b => b.ComponentGroup == "Bản trần - lớp trên" &&
                (b.BarMark == "N1A" || b.BarMark == "N1B")).ToList();
            Assert(roofCutBars.Count > 0 && roofCutBars.All(b => b.Shape == BarShapeType.L1M &&
                   Math.Abs(b.LengthMm - (b.L + 60)) < 1e-6),
                "Thanh trần 2M bị cắt bởi lỗ mở chỉ giữ móc ở đầu thanh gốc, không cộng móc tại mép cắt");
            Assert(roofCutBars.Any(b => b.BarMark == "N1A") && roofCutBars.Any(b => b.BarMark == "N1B") &&
                   roofCutBars.Where(b => b.BarMark == "N1A").All(b => b.LT == 60 && b.LP == 0) &&
                   roofCutBars.Where(b => b.BarMark == "N1B").All(b => b.LT == 0 && b.LP == 60),
                "Các nhánh trần N1A/N1B phải giữ móc ở đúng đầu thanh ban đầu");
            var rightHookPoints = BarBendingGeometry.BuildL1MPolyline(100, 0, 50, 0.6);
            Assert(rightHookPoints.Count == 3 &&
                   rightHookPoints[0] == new Point2D(0, 0) &&
                   rightHookPoints[1] == new Point2D(60, 0) &&
                   rightHookPoints[2] == new Point2D(60, 30),
                "Thanh 1M có LP phải dựng móc tại đầu phải của polyline");
            var leftHookPoints = BarBendingGeometry.BuildL1MPolyline(100, 50, 0, 0.6);
            Assert(leftHookPoints.Count == 3 &&
                   leftHookPoints[0] == new Point2D(0, 30) &&
                   leftHookPoints[1] == new Point2D(0, 0) &&
                   leftHookPoints[2] == new Point2D(60, 0),
                "Thanh 1M có LT phải giữ cách dựng móc tại đầu trái");
            var innerNeckBars = hookProbeBars.Where(b => b.ComponentGroup == "Cổ ga - lớp trong").ToList();
            Assert(innerNeckBars.Count > 0 && innerNeckBars.All(b => b.Diameter == 8),
                "Cổ ga phải sinh riêng lớp trong theo đúng quy cách cốt thép lớp trong");
            Assert(hookProbeBars.Any(b => b.ComponentGroup == "Cổ ga - lớp ngoài" &&
                   b.BarMark == "C1" && b.Shape == BarShapeType.L1M),
                "Cốt thép đứng cổ ga lớp ngoài phải dùng cấu hình móc 1M");
            var outerNeckMain = hookProbeBars.Single(b => b.ComponentGroup == "Cổ ga - lớp ngoài" && b.BarMark == "C1");
            var innerNeckMain = hookProbeBars.Single(b => b.ComponentGroup == "Cổ ga - lớp trong" && b.BarMark == "C1");
            Assert(outerNeckMain.Diameter == 10 && outerNeckMain.LT == 50 && outerNeckMain.LP == 0 &&
                   Math.Abs(outerNeckMain.LengthMm - 350) < 1e-6,
                "Thép đứng cổ ga ngoài phải cộng đúng một móc theo chiều dày tường cổ");
            Assert(innerNeckMain.Diameter == 8 && innerNeckMain.Shape == BarShapeType.L0M &&
                   innerNeckMain.LengthMm == hookProbe.NeckHeight,
                "Thép đứng cổ ga trong phải dùng quy cách và cấu hình móc riêng");
            Assert(hookProbeBars.Any(b => b.ComponentGroup == "Cổ ga - lớp trong" && b.BarMark == "C2" && b.Diameter == 8),
                "Thép đai cổ ga phải được thống kê riêng theo quy cách lớp trong");
            var noNeckRebarProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "NoNeckRebarProbe",
                L = 600, B = 400, H = 500,
                BaseThickness = 150, WallThickness = 100, TopThickness = 120,
                NeckHeight = 300, NeckWallThickness = 100,
                NeckInnerWidth = 100, NeckInnerLength = 100,
                CoverBottom = 30, CoverWall = 30, CoverTop = 30, CoverNeck = 25,
                RebarDistributionMode = "Chia đều",
                RebarHookMode = "Theo KC"
            };
            var noNeckRebarBars = RebarEngine.GenerateRebarModel(noNeckRebarProbe,
                GeometryEngine.Build(noNeckRebarProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!noNeckRebarBars.Any(b => b.ComponentGroup.StartsWith("Cổ ga", StringComparison.Ordinal)),
                "Không tự tạo thép cổ ga mặc định khi Excel không có quy cách cổ ga");

            Console.WriteLine("\n[REGRESSION] Không tạo thang khi cấu hình vị trí là 'Không'...");
            var ladderDisabledProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "LadderDisabledProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                LadderLocation = "Không",
                LadderFirstStep = 500,
                LadderPitch = 300,
                LadderWidth = 300,
                LadderDepth = 250,
                LadderHook = 150
            };
            var ladderDisabledBars = RebarEngine.GenerateRebarModel(
                ladderDisabledProbe, GeometryEngine.Build(ladderDisabledProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!ladderDisabledBars.Any(b => b.ComponentGroup == "Thang thép"),
                "Cấu hình LadderLocation='Không' phải loại toàn bộ thang khỏi schedule");

            var noAvailableLadderProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "NoAvailableLadderProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                LadderLocation = "Trong ga",
                LadderFirstStep = 600,
                LadderPitch = 300,
                LadderWidth = 300,
                LadderDepth = 250,
                LadderHook = 150
            };
            var noAvailableLadderBars = RebarEngine.GenerateRebarModel(
                noAvailableLadderProbe, GeometryEngine.Build(noAvailableLadderProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!noAvailableLadderBars.Any(b => b.ComponentGroup == "Thang thép"),
                "Không tạo bậc thang nếu cao độ bậc đầu tiên vượt quá chiều cao ga");

            Console.WriteLine("\n[REGRESSION] Đường kính gia cường lỗ thăm bằng 0 phải tắt cấu kiện...");
            var disabledTopHoleProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "DisabledTopHoleProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                NeckHeight = 300,
                NeckWallThickness = 100,
                NeckInnerWidth = 100,
                NeckInnerLength = 100,
                TopHoleReinforceDia = 0
            };
            var disabledTopHoleBars = RebarEngine.GenerateRebarModel(
                disabledTopHoleProbe, GeometryEngine.Build(disabledTopHoleProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!disabledTopHoleBars.Any(b => b.ComponentGroup == "Gia cường lỗ thăm"),
                "TopHoleReinforceDia=0 phải loại thanh gia cường lỗ thăm khỏi schedule");

            Console.WriteLine("\n[REGRESSION] Đường kính gia cường lỗ cống bằng 0 phải tắt cấu kiện...");
            var disabledPipeReinforceProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "DisabledPipeReinforceProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                PipeT1 = wallPipe,
                PipeReinforceDia = 0
            };
            var disabledPipeReinforceBars = RebarEngine.GenerateRebarModel(
                disabledPipeReinforceProbe, GeometryEngine.Build(disabledPipeReinforceProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!disabledPipeReinforceBars.Any(b => b.ComponentGroup == "Gia cường lỗ cống"),
                "PipeReinforceDia=0 phải loại thanh gia cường lỗ cống khỏi schedule");

            Console.WriteLine("\n[REGRESSION] Không thống kê thép cửa thu khi kết cấu không phải BTCT...");
            var nonConcreteCatchBasinProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamThuNapRoi,
                Name = "NonConcreteCatchBasinProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                CatchBasinStructure = "Khác",
                CatchBasinChamberLength = 500,
                CatchBasinChamberHeight = 250
            };
            var nonConcreteCatchBasinBars = RebarEngine.GenerateRebarModel(
                nonConcreteCatchBasinProbe, GeometryEngine.Build(nonConcreteCatchBasinProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!nonConcreteCatchBasinBars.Any(b => b.ComponentGroup == "Cửa thu nước"),
                "Không tạo cốt thép cửa thu giả cho cấu kiện không phải BTCT");

            var missingCatchBasinRebarProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamThuNapRoi,
                Name = "MissingCatchBasinRebarProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                CatchBasinStructure = "BTCT",
                CatchBasinChamberLength = 500,
                CatchBasinChamberHeight = 250
            };
            var missingCatchBasinRebarBars = RebarEngine.GenerateRebarModel(
                missingCatchBasinRebarProbe, GeometryEngine.Build(missingCatchBasinRebarProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!missingCatchBasinRebarBars.Any(b => b.ComponentGroup == "Cửa thu nước"),
                "Thiếu quy cách thép cửa thu phải bị bỏ qua, không thay bằng D10A150 tự đặt");

            var missingBaseRebarsProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "MissingBaseRebarsProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120
            };
            var missingBaseRebars = RebarEngine.GenerateRebarModel(
                missingBaseRebarsProbe, GeometryEngine.Build(missingBaseRebarsProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(!missingBaseRebars.Any(b => b.ComponentGroup.StartsWith("Bản đáy", StringComparison.Ordinal)),
                "Thiếu cả hai quy cách thép đáy phải không sinh thanh D10A150 tự đặt");
            Assert(!missingBaseRebars.Any(b => b.ComponentGroup == "Thép đai liên kết"),
                "Thiếu TieSpec phải không sinh thép buộc D6A300 tự đặt");

            var tieShapeProbe = new NormalizedManholeSpec
            {
                Type = ManholeType.GaThamLienKhoi,
                Name = "TieShapeProbe",
                L = 600,
                B = 400,
                H = 500,
                BaseThickness = 150,
                WallThickness = 100,
                TopThickness = 120,
                TieSpec = new RebarSpec { Diameter = 6, Spacing = 300, RawText = "D6A300" },
                TieShapeBase = "Chữ C",
                TieShapeWall = "Chữ Z"
            };
            var tieShapeBars = RebarEngine.GenerateRebarModel(
                tieShapeProbe, GeometryEngine.Build(tieShapeProbe), new Dictionary<string, SteelAngleRecord>());
            Assert(tieShapeBars.Single(b => b.BarMark == "D3" && b.ComponentGroup == "Thép đai liên kết").Shape == BarShapeType.M_C,
                "Hình dạng thép buộc đáy phải theo CF.Ga!D23");
            Assert(tieShapeBars.Single(b => b.BarMark == "T3" && b.ComponentGroup == "Thép đai liên kết").Shape == BarShapeType.M_Z,
                "Hình dạng thép buộc tường phải theo CF.Ga!E23");

            var oversizedPipe = wallPipe with
            {
                PipeCode = "D3000",
                InvertLevelFromBottom = -1250,
                PipeCatalogInfo = new PipeRecord("D3000", false, 3000, 0, 3000, 0, 0, 0, false)
            };
            var fullyCutLayout = RebarEngine.BuildWallRebarLayout(wallProbe, WallDirection.T1, oversizedPipe);
            Assert(fullyCutLayout.Count == 0,
                "Không được tạo thanh nguyên giả khi vùng mở cắt hết mọi vị trí trên tường");

            Console.WriteLine("\n[REGRESSION] Không thay dữ liệu rải sai bằng bước thép mặc định...");
            bool invalidPitchRejected = false;
            try { RebarEngine.DistributeBars(500, 30, 10, 0); }
            catch (ArgumentOutOfRangeException) { invalidPitchRejected = true; }
            Assert(invalidPitchRejected, "Bước thép bằng 0 phải bị chặn thay vì tự đổi thành 150 mm");

            bool insufficientSpanRejected = false;
            try { RebarEngine.DistributeBars(50, 30, 10, 100); }
            catch (ArgumentOutOfRangeException) { insufficientSpanRejected = true; }
            Assert(insufficientSpanRejected, "Span âm sau khi trừ lớp bảo vệ phải báo lỗi");

            // -------------------------------------------------------------
            // TEST 6: GATE 3 - Kiểm tra Động cơ Cốt thép & Thống kê (RebarEngine & ScheduleEngine)
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 6] GATE 3 - Kiểm tra Động cơ Cốt thép & Thống kê...");
            var bars = RebarEngine.GenerateRebarModel(g1, geom, res1.AngleCatalog);
            Assert(bars.Count > 0, $"Số lượng thanh cốt thép sinh ra: {bars.Count} (phải > 0)");

            // Kiểm tra các nhóm cấu kiện
            var baseBars = bars.FindAll(b => b.ComponentGroup.Contains("Bản đáy"));
            Assert(baseBars.Count >= 4, $"Số loại thanh bản đáy: {baseBars.Count} (phải >= 4)");
            var baseTopD1 = baseBars.Single(b => b.ComponentGroup == "Bản đáy - lớp trên" && b.BarMark == "D1");
            Assert(baseTopD1.LT == 90 && baseTopD1.LP == 90,
                $"Móc D1 bản đáy theo KC = 150 - 2×30 = 90mm (thực tế {baseTopD1.LT}/{baseTopD1.LP}mm)");

            var wallBars = bars.FindAll(b => b.ComponentGroup.Contains("Tường"));
            Assert(wallBars.Count > 0, "Phải có cốt thép tường");
            var wallOuterT1 = wallBars.Single(b => b.ComponentGroup == "Tường T1 - lớp ngoài" && b.BarMark == "T1");
            Assert(wallOuterT1.LT == 90 && wallOuterT1.LP == 90,
                $"Móc thép đứng tường theo KC = 150 - 2×30 = 90mm (thực tế {wallOuterT1.LT}/{wallOuterT1.LP}mm)");

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

            bool unknownAngleRejected = false;
            try
            {
                ScheduleEngine.CalculateSchedule(g1, new List<BarInstance>
                {
                    new() { ComponentGroup = "Thép hình", BarMark = "VG1", AngleCode = "V99x99x9", Quantity = 1, LengthMm = 1000 }
                }, new Dictionary<string, SteelAngleRecord>());
            }
            catch (InvalidOperationException ex)
            {
                unknownAngleRejected = ex.Message.Contains("V99x99x9", StringComparison.OrdinalIgnoreCase);
            }
            Assert(unknownAngleRejected,
                "Không được tính thép góc bằng khối lượng mặc định khi mã không có trong CF.Data");

            Console.WriteLine("\n[REGRESSION] Phân nhóm thép gia cường theo đúng cấu kiện...");
            var reinforcementProbe = ScheduleEngine.CalculateSchedule(g1, new List<BarInstance>
            {
                new()
                {
                    ComponentGroup = "Gia cường lỗ cống",
                    BarMark = "GC1",
                    Diameter = 10,
                    Quantity = 2,
                    LengthMm = 1000
                },
                new()
                {
                    ComponentGroup = "Gia cường lỗ thăm",
                    BarMark = "GN1",
                    Diameter = 10,
                    Quantity = 1,
                    LengthMm = 500
                }
            }, res1.AngleCatalog);
            double kgPerMeterD10 = Math.PI * Math.Pow(10.0 / 1000.0, 2) * 7850.0 / 4.0;
            Assert(Math.Abs(reinforcementProbe.Summary.Tuong1 - 2 * kgPerMeterD10) < 1e-9,
                "Gia cường lỗ cống phải tính vào tổng thép tường");
            Assert(Math.Abs(reinforcementProbe.Summary.Tran1 - 0.5 * kgPerMeterD10) < 1e-9,
                "Gia cường lỗ thăm phải tính vào tổng thép trần");

            // -------------------------------------------------------------
            // TEST 7: GATE 5 (Một phần) - Xuất Excel Khối lượng Thép An Toàn
            // -------------------------------------------------------------
            Console.WriteLine("\n[TEST 7] GATE 5 - Kiểm tra Xuất Excel Khối lượng Thép (ExcelSteelWriter)...");
            string templateForm = Path.Combine(baseDir, "Template", "VHG-DataForm.xlsx");
            string testOutDir = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.Combine(Path.GetTempPath(), "DBTool.TestRunner", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testOutDir);

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

            // -------------------------------------------------------------
            // REGRESSION: Circle-opening clipping uses the true circle/line intersection.
            // -------------------------------------------------------------
            Console.WriteLine("\n[REGRESSION] Quy ước độ lệch tim theo từng tường...");
            Assert(WallViewTransform.ToLocalOffset(WallDirection.T1, 100) == -100,
                "Độ lệch trái dương của T1 phải đổi dấu trong hình chiếu tường");
            Assert(WallViewTransform.ToLocalOffset(WallDirection.T2, 100) == 100,
                "Độ lệch trái dương của T2 phải giữ dấu trong hình chiếu tường");
            Assert(WallViewTransform.ToLocalOffset(WallDirection.T3, 100) == 100,
                "Độ lệch trái dương của T3 phải giữ dấu trong hình chiếu tường");
            Assert(WallViewTransform.ToLocalOffset(WallDirection.T4, 100) == -100,
                "Độ lệch trái dương của T4 phải đổi dấu trong hình chiếu tường");

            Console.WriteLine("\n[REGRESSION] Kiểm tra cắt thanh tại lỗ cống tròn...");
            var centerCut = CircleOpeningClipper.SubtractCircleFromHorizontal(
                -10, 10, 0, 0, 0, 5);
            Assert(centerCut.Count == 2, "Đường qua tâm lỗ tròn phải còn 2 đoạn");
            Assert(Math.Abs(centerCut[0].Start - (-10)) < 1e-9 && Math.Abs(centerCut[0].End - (-5)) < 1e-9,
                "Đoạn trái của đường qua tâm phải kết thúc tại x=-R");
            Assert(Math.Abs(centerCut[1].Start - 5) < 1e-9 && Math.Abs(centerCut[1].End - 10) < 1e-9,
                "Đoạn phải của đường qua tâm phải bắt đầu tại x=+R");

            var offsetCut = CircleOpeningClipper.SubtractCircleFromHorizontal(
                -10, 10, 3, 0, 0, 5);
            Assert(offsetCut.Count == 2, "Đường lệch tâm phải cắt theo giao tuyến tròn");
            Assert(Math.Abs(offsetCut[0].End - (-4)) < 1e-9 && Math.Abs(offsetCut[1].Start - 4) < 1e-9,
                "Độ rộng giao tuyến y=3 của đường tròn R=5 phải là 8");

            var tangentCut = CircleOpeningClipper.SubtractCircleFromHorizontal(
                -10, 10, 5, 0, 0, 5);
            Assert(tangentCut.Count == 1 && tangentCut[0].Start == -10 && tangentCut[0].End == 10,
                "Tiếp tuyến không được làm đứt thanh");

            var noCut = CircleOpeningClipper.SubtractCircleFromHorizontal(
                -10, 10, 6, 0, 0, 5);
            Assert(noCut.Count == 1 && noCut[0].Start == -10 && noCut[0].End == 10,
                "Đường ngoài lỗ phải giữ nguyên");

            var verticalCut = CircleOpeningClipper.SubtractCircleFromVertical(
                -10, 10, 0, 0, 0, 5);
            Assert(verticalCut.Count == 2 && verticalCut[0].End == -5 && verticalCut[1].Start == 5,
                "Đường đứng qua tâm phải được cắt đối xứng");

            // -------------------------------------------------------------
            // REGRESSION: Roof reinforcement is cut by the real rectangular opening.
            // -------------------------------------------------------------
            Console.WriteLine("\n[REGRESSION] Kiểm tra cắt thanh trần tại lỗ mở chữ nhật...");
            var opening = new Rect2D(-2, -1, 2, 1);
            var horizontalRectCut = RectangleOpeningClipper.SubtractFromHorizontal(-5, 5, 0, opening);
            Assert(horizontalRectCut.Count == 2, "Thanh ngang qua giữa lỗ chữ nhật phải còn hai đoạn");
            Assert(Math.Abs(horizontalRectCut[0].End - (-2)) < 1e-9 && Math.Abs(horizontalRectCut[1].Start - 2) < 1e-9,
                "Thanh ngang phải dừng đúng tại hai mép lỗ mở");

            var clearRectCut = RectangleOpeningClipper.SubtractFromHorizontal(-5, 5, 0, opening, clearance: 0.5);
            Assert(clearRectCut.Count == 2 && Math.Abs(clearRectCut[0].End + 2.5) < 1e-9 && Math.Abs(clearRectCut[1].Start - 2.5) < 1e-9,
                "Khoảng hở bảo vệ phải mở rộng vùng cắt lỗ trần");

            var outsideRectCut = RectangleOpeningClipper.SubtractFromHorizontal(-5, 5, 2, opening);
            Assert(outsideRectCut.Count == 1 && outsideRectCut[0].Length == 10,
                "Thanh ngoài phạm vi lỗ chữ nhật phải giữ nguyên");

            var verticalRectCut = RectangleOpeningClipper.SubtractFromVertical(-5, 5, 0, opening);
            Assert(verticalRectCut.Count == 2 && Math.Abs(verticalRectCut[0].End + 1) < 1e-9 && Math.Abs(verticalRectCut[1].Start - 1) < 1e-9,
                "Thanh dọc qua giữa lỗ chữ nhật phải được cắt theo đúng hai mép");

            var roofBars = bars.FindAll(b => b.ComponentGroup.StartsWith("Bản trần", StringComparison.Ordinal));
            Assert(roofBars.Exists(b => b.BarMark == "N1A") && roofBars.Exists(b => b.BarMark == "N1B"),
                "Lưới trần G400-1 phải có hai phía thanh bị cắt quanh lỗ lệch góc");
            Assert(roofBars.Exists(b => b.BarMark == "N2A") && roofBars.Exists(b => b.BarMark == "N2B"),
                "Phương vuông góc của lưới trần cũng phải được cắt theo hình chữ nhật");

            Console.WriteLine("\n[REGRESSION] Fit chi tiết uốn Tờ 5 vào ô mà không làm đổi kích thước ghi chú...");
            var tallBendingItem = new BarScheduleItem
            {
                Shape = BarShapeType.M_C,
                L = 2400,
                LT = 120,
                LengthMm = 2640
            };
            var tallEnvelope = Sheet5DiagramLayout.GetEnvelope(tallBendingItem);
            double tallScale = Sheet5DiagramLayout.ScaleToFit(
                tallEnvelope.WidthMm, tallEnvelope.HeightMm,
                availableWidth: 1800, availableHeight: 120);
            Assert(tallEnvelope.HeightMm * tallScale <= 120 + 1e-9,
                "Chi tiết thanh đứng dài phải nằm trong chiều cao hàng Tờ 5");
            Assert(tallEnvelope.WidthMm * tallScale <= 1800 + 1e-9,
                "Chi tiết thanh uốn phải nằm trong chiều rộng cột Tờ 5");
            Assert(tallScale < 0.1,
                "Thanh cao phải được thu nhỏ theo ô thay vì vượt sang hàng kế tiếp");

            double longStraightScale = Sheet5DiagramLayout.ScaleToFit(
                widthMm: 6000, heightMm: 0,
                availableWidth: 1200, availableHeight: 120);
            Assert(Math.Abs(longStraightScale - 0.2) < 1e-9,
                "Thanh thẳng dài phải thu nhỏ theo chiều rộng cột");

            var uTieEnvelope = Sheet5DiagramLayout.GetEnvelope(new BarScheduleItem
            {
                Shape = BarShapeType.M_U,
                L = 300,
                LT = 100,
                LP = 100,
                L3 = 250,
                LengthMm = 650
            });
            Assert(Math.Abs(uTieEnvelope.HeightMm - 250) < 1e-9,
                "Chi tiết U phải dự trù chiều cao cho móc giữa theo cấu hình CF.Ga!L23");

            Console.WriteLine("\n==================================================================================");
            Console.WriteLine(">>> PASS: Excel, hình học mẫu, schedule nội bộ, xuất Excel và regression clipping. <<<");
            Console.WriteLine(">>> Chưa so khớp tổng schedule với workbook vàng VHGpro.                         <<<");
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
