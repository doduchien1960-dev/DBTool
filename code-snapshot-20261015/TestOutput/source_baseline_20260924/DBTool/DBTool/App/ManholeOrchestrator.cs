using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Cad;
using DBTool.Core;
using DBTool.IO;

namespace DBTool.App
{
    public record GenerationProgress(int CurrentIndex, int TotalCount, string CurrentManhole, string StatusMessage);

    /// <summary>
    /// Nhạc trưởng điều phối toàn bộ luồng xử lý: Excel -> Geometry -> Rebar -> CAD ModelSpace -> Excel Export
    /// Đảm bảo tính giao dịch, an toàn dữ liệu và rollback khi có lỗi
    /// </summary>
    public class ManholeOrchestrator
    {
        public static (List<NormalizedManholeSpec> Manholes, List<Diagnostic> Diagnostics, ExcelWorkbookReader.ReadResult Result) ScanWorkbook(
            string excelFilePath, string? sheetName = null)
        {
            var reader = new ExcelWorkbookReader();
            var result = reader.Read(excelFilePath, sheetName);
            return (result.Manholes, result.Diagnostics, result);
        }

        public static void GenerateDrawings(
            Document doc,
            List<NormalizedManholeSpec> selectedManholes,
            ExcelWorkbookReader.ReadResult readResult,
            double baseX = 0,
            double baseY = 0,
            IProgress<GenerationProgress>? progress = null)
        {
            if (doc == null || selectedManholes == null || selectedManholes.Count == 0) return;

            Database db = doc.Database;
            var arranger = new LayoutArranger(baseX, baseY);

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btrModelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 1. Nạp và clone an toàn tài nguyên template (layers, textstyles, dimstyles, blocks)
                TemplateManager.EnsureTemplateLoaded(db);
                ObjectId dimStyleId = TemplateManager.GetDimStyleId(db, "VHG1-30");

                for (int mIndex = 0; mIndex < selectedManholes.Count; mIndex++)
                {
                    var spec = selectedManholes[mIndex];
                    progress?.Report(new GenerationProgress(mIndex + 1, selectedManholes.Count, spec.Name, $"Đang dựng hình học và cốt thép cho ga {spec.Name}..."));

                    // 2. Tính toán hình học độc lập (GeometryEngine)
                    var geom = GeometryEngine.Build(spec);

                    // 3. Tính toán mô hình cốt thép (RebarEngine)
                    var bars = RebarEngine.GenerateRebarModel(spec, geom, readResult.AngleCatalog);

                    // 4. Lập bảng thống kê cốt thép (ScheduleEngine)
                    var schedule = ScheduleEngine.CalculateSchedule(spec, bars, readResult.AngleCatalog);

                    // 5. Xác định tọa độ 5 tờ của hố ga
                    // Tờ 1: Cấu tạo & MC kết cấu
                    var orig1 = arranger.GetSheetOrigin(mIndex, 0);
                    arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, orig1, spec.Name, "CẤU TẠO & MẶT CẮT KẾT CẤU", 1, 5);
                    Sheet1Drawer.Draw(blockTable, btrModelSpace, tr, db, spec, geom, orig1, dimStyleId);

                    // Tờ 2: Lưới thép Bản đáy & Tường T1, T2
                    var orig2 = arranger.GetSheetOrigin(mIndex, 1);
                    arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, orig2, spec.Name, "CỐT THÉP BẢN ĐÁY & TƯỜNG T1, T2", 2, 5);
                    Sheet2Drawer.Draw(blockTable, btrModelSpace, tr, db, spec, geom, orig2, dimStyleId);

                    // Tờ 3: Lưới thép Tường T3, T4 & Bản trần
                    var orig3 = arranger.GetSheetOrigin(mIndex, 2);
                    arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, orig3, spec.Name, "CỐT THÉP TƯỜNG T3, T4 & BẢN TRẦN", 3, 5);
                    Sheet3Drawer.Draw(blockTable, btrModelSpace, tr, db, spec, geom, orig3, dimStyleId);

                    // Tờ 4: Cổ ga, Thang leo & Bảng Thống kê cốt thép
                    var orig4 = arranger.GetSheetOrigin(mIndex, 3);
                    arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, orig4, spec.Name, "CHI TIẾT & BẢNG THỐNG KÊ CỐT THÉP", 4, 5);
                    Sheet4Drawer.Draw(blockTable, btrModelSpace, tr, db, spec, geom, schedule, orig4, dimStyleId);

                    // Tờ 5: Chi tiết uốn thanh thép (Bar Bending Schedule)
                    var orig5 = arranger.GetSheetOrigin(mIndex, 4);
                    arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, orig5, spec.Name, "CHI TIẾT UỐN THANH THÉP", 5, 5);
                    Sheet5Drawer.Draw(blockTable, btrModelSpace, tr, db, spec, schedule, orig5, dimStyleId);

                    // Tờ phụ 1:20 (nếu có nắp đan BTCT đúc sẵn)
                    if (spec.CoverType.Equals("BTCT", StringComparison.OrdinalIgnoreCase))
                    {
                        var origAux = arranger.GetSheetOrigin(mIndex, 5);
                        arranger.InsertSheetFrame(blockTable, btrModelSpace, tr, db, origAux, spec.Name, "CHI TIẾT TẤM ĐAN NẮP BTCT ĐÚC SẴN", 6, 6, scale: 0.2);
                        SheetAuxDrawer.Draw(blockTable, btrModelSpace, tr, db, spec, schedule, origAux, dimStyleId);
                    }
                }

                tr.Commit();
            }

            progress?.Report(new GenerationProgress(selectedManholes.Count, selectedManholes.Count, "", "Hoàn thành vẽ tất cả hố ga lên ModelSpace!"));
        }
    }
}
