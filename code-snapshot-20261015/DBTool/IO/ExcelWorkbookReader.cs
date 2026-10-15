using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DBTool.Core;

namespace DBTool.IO
{
    /// <summary>
    /// Bộ đọc bảng tính Excel cho DBTool:
    /// - Nhận diện 3 profile hố ga (GaThamLienKhoi, GaThamNapRoi, GaThamThuNapRoi)
    /// - Bóc tách cấu hình CF.Ga (xử lý độ lệch dòng 2 hàng của Loại 3)
    /// - Bóc tách bảng cống CF.Cong và bảng dữ liệu phụ CF.Data
    /// - Quét toàn bộ hàng hố ga từ hàng 4, lưu vết SourceRef và kiểm tra chẩn đoán Diagnostic
    /// </summary>
    public class ExcelWorkbookReader
    {
        public record ReadResult(
            List<NormalizedManholeSpec> Manholes,
            List<Diagnostic> Diagnostics,
            Dictionary<string, PipeRecord> PipeCatalog,
            Dictionary<string, SteelAngleRecord> AngleCatalog,
            Dictionary<string, HatchRecord> HatchCatalog,
            ManholeType DetectedType);

        public ReadResult Read(string filePath, string? targetSheetName = null)
        {
            var diagnostics = new List<Diagnostic>();
            var manholes = new List<NormalizedManholeSpec>();
            var pipeCatalog = new Dictionary<string, PipeRecord>(StringComparer.OrdinalIgnoreCase);
            var angleCatalog = new Dictionary<string, SteelAngleRecord>(StringComparer.OrdinalIgnoreCase);
            var hatchCatalog = new Dictionary<string, HatchRecord>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(filePath))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    "",
                    null,
                    $"Tệp Excel không tồn tại: {filePath}"));
                return new ReadResult(manholes, diagnostics, pipeCatalog, angleCatalog, hatchCatalog, ManholeType.GaThamLienKhoi);
            }

            using var workbook = new XLWorkbook(filePath);

            // 1. Đọc CF.Cong (Bảng quy cách cống)
            if (workbook.TryGetWorksheet("CF.Cong", out var wsCong))
            {
                ReadPipeCatalog(wsCong, filePath, pipeCatalog, diagnostics);
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    "",
                    new SourceRef(filePath, "CF.Cong", "A1", ""),
                    "Không tìm thấy sheet bắt buộc 'CF.Cong'; không thể xác định kích thước cống để dựng hình học an toàn."));
            }

            // 2. Đọc CF.Data (Thép hình, Mẫu hatch, Tên block nắp)
            if (workbook.TryGetWorksheet("CF.Data", out var wsData))
            {
                ReadDataCatalog(wsData, filePath, angleCatalog, hatchCatalog, diagnostics);
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    "",
                    new SourceRef(filePath, "CF.Data", "A1", ""),
                    "Không tìm thấy sheet bắt buộc 'CF.Data'; không thể xác định khối lượng thép hình và quy cách hatch an toàn."));
            }

            // 3. Nhận diện sheet hố ga và loại hố ga
            IXLWorksheet? wsMain = null;
            ManholeType detectedType = ManholeType.GaThamLienKhoi;

            if (!string.IsNullOrWhiteSpace(targetSheetName) && workbook.TryGetWorksheet(targetSheetName, out var requestedSheet))
            {
                wsMain = requestedSheet;
                detectedType = DeduceTypeFromSheetName(targetSheetName);
            }
            else
            {
                if (workbook.TryGetWorksheet("GaThamLienKhoi", out var ws1))
                {
                    wsMain = ws1;
                    detectedType = ManholeType.GaThamLienKhoi;
                }
                else if (workbook.TryGetWorksheet("GaThamNapRoi", out var ws2))
                {
                    wsMain = ws2;
                    detectedType = ManholeType.GaThamNapRoi;
                }
                else if (workbook.TryGetWorksheet("GaThamThuNapRoi", out var ws3))
                {
                    wsMain = ws3;
                    detectedType = ManholeType.GaThamThuNapRoi;
                }
            }

            if (wsMain == null)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    "",
                    new SourceRef(filePath, "", "", ""),
                    "Không tìm thấy sheet hố ga hợp lệ ('GaThamLienKhoi', 'GaThamNapRoi', hoặc 'GaThamThuNapRoi')."));
                return new ReadResult(manholes, diagnostics, pipeCatalog, angleCatalog, hatchCatalog, detectedType);
            }

            // 4. Đọc CF.Ga (Cấu hình hố ga)
            if (!workbook.TryGetWorksheet("CF.Ga", out var wsConfig))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    "",
                    new SourceRef(filePath, "CF.Ga", "A1", ""),
                    "Không tìm thấy sheet cấu hình bắt buộc 'CF.Ga'."));
                return new ReadResult(manholes, diagnostics, pipeCatalog, angleCatalog, hatchCatalog, detectedType);
            }

            var baseSpec = ReadConfigSheet(wsConfig, filePath, detectedType, hatchCatalog, diagnostics);

            // 5. Quét từng hàng hố ga trong sheet chính (bắt đầu từ hàng 4)
            int row = 4;
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var cellName = wsMain.Cell(row, 2); // Cột B
                string name = cellName.GetString().Trim();

                // Kiểm tra xem dòng này có dữ liệu ở các cột khác không
                bool hasOtherData = false;
                for (int col = 3; col <= 27; col++)
                {
                    if (!wsMain.Cell(row, col).IsEmpty())
                    {
                        hasOtherData = true;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(name))
                {
                    if (hasOtherData)
                    {
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Block,
                            $"(Hàng {row})",
                            new SourceRef(filePath, wsMain.Name, cellName.Address.ToString(), ""),
                            $"Dữ liệu mồ côi không có mã hố ga tại hàng {row} (cột B trống nhưng có dữ liệu kích thước)."));
                    }
                    else
                    {
                        // Đã hết dữ liệu
                        break;
                    }
                }
                else
                {
                    // Kiểm tra trùng tên ga
                    if (seenNames.Contains(name))
                    {
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Block,
                            name,
                            new SourceRef(filePath, wsMain.Name, cellName.Address.ToString(), name),
                            $"Mã hố ga '{name}' bị trùng lặp tại hàng {row}. Mỗi mã ga phải là duy nhất."));
                    }
                    else
                    {
                        seenNames.Add(name);
                    }

                    // Đọc chi tiết hố ga
                    var spec = ReadManholeRow(
                        wsMain, row, filePath, name, detectedType, baseSpec, pipeCatalog, diagnostics);

                    if (spec != null)
                    {
                        manholes.Add(spec);
                    }
                }

                row++;
                if (row > 2000) break; // Giới hạn an toàn chống vòng lặp vô tận
            }

            return new ReadResult(manholes, diagnostics, pipeCatalog, angleCatalog, hatchCatalog, detectedType);
        }

        private static ManholeType DeduceTypeFromSheetName(string sheetName)
        {
            if (sheetName.IndexOf("Thu", StringComparison.OrdinalIgnoreCase) >= 0)
                return ManholeType.GaThamThuNapRoi;
            if (sheetName.IndexOf("NapRoi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sheetName.IndexOf("Nắp rời", StringComparison.OrdinalIgnoreCase) >= 0)
                return ManholeType.GaThamNapRoi;
            return ManholeType.GaThamLienKhoi;
        }

        // ====================================================================
        // ĐỌC DANH MỤC CỐNG CF.Cong
        // ====================================================================
        private static void ReadPipeCatalog(
            IXLWorksheet ws,
            string filePath,
            Dictionary<string, PipeRecord> catalog,
            List<Diagnostic> diagnostics)
        {
            int row = 4;
            while (true)
            {
                var cellD = ws.Cell(row, 2); // Cột B: D cống tròn
                var cellTp = ws.Cell(row, 3); // Cột C: dày thành cống tròn
                var cellBoxB = ws.Cell(row, 4); // Cột D: B cống hộp
                var cellBoxH = ws.Cell(row, 5); // Cột E: H cống hộp
                var cellBoxTp = ws.Cell(row, 6); // Cột F: dày thành hộp
                var cellBoxVat = ws.Cell(row, 7); // Cột G: vát góc hộp
                var cellBoxDoi = ws.Cell(row, 8); // Cột H: cống đôi

                double dVal = 0;
                double bVal = 0;
                double hVal = 0;
                bool hasRound = TryGetDouble(cellD, out dVal) && dVal > 0;
                bool hasBox = TryGetDouble(cellBoxB, out bVal) && bVal > 0 &&
                              TryGetDouble(cellBoxH, out hVal) && hVal > 0;

                if (!hasRound && !hasBox && cellD.IsEmpty() && cellBoxB.IsEmpty())
                {
                    break;
                }

                if (hasRound)
                {
                    TryGetDouble(cellTp, out double tpVal);
                    string codeD = $"D{(int)dVal}";
                    string codeNum = $"{(int)dVal}";
                    var record = new PipeRecord(
                        codeD, false, dVal, tpVal, dVal + 2 * tpVal, 0, 0, 0, false);

                    catalog[codeD] = record;
                    catalog[codeNum] = record;
                }

                if (hasBox)
                {
                    TryGetDouble(cellBoxTp, out double boxTp);
                    TryGetDouble(cellBoxVat, out double boxVat);
                    string boxDoiStr = cellBoxDoi.GetString().Trim();
                    bool isDouble = boxDoiStr.Equals("Có", StringComparison.OrdinalIgnoreCase) ||
                                    boxDoiStr.Equals("Co", StringComparison.OrdinalIgnoreCase);

                    string codeBox = $"{(int)bVal}x{(int)hVal}";
                    var record = new PipeRecord(
                        codeBox, true, 0, boxTp, 0, bVal, hVal, boxVat, isDouble);

                    catalog[codeBox] = record;
                    catalog[$"H{codeBox}"] = record;
                    catalog[$"B{codeBox}"] = record;
                }

                row++;
                if (row > 100) break;
            }
        }

        // ====================================================================
        // ĐỌC DANH MỤC THÉP HÌNH VÀ HATCH CF.Data
        // ====================================================================
        private static void ReadDataCatalog(
            IXLWorksheet ws,
            string filePath,
            Dictionary<string, SteelAngleRecord> angleCatalog,
            Dictionary<string, HatchRecord> hatchCatalog,
            List<Diagnostic> diagnostics)
        {
            int row = 4;
            while (true)
            {
                var cellAngle = ws.Cell(row, 2); // B: Loại thép
                var cellWeight = ws.Cell(row, 3); // C: KL riêng kg/m
                var cellHatch = ws.Cell(row, 4); // D: Mẫu hatch
                var cellScale = ws.Cell(row, 5); // E: Tỷ lệ hatch

                string angleCode = cellAngle.GetString().Trim();
                if (!string.IsNullOrEmpty(angleCode) && !angleCode.Equals("Không", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetDouble(cellWeight, out double weight))
                    {
                        angleCatalog[angleCode] = new SteelAngleRecord(angleCode, weight);
                    }
                }

                string hatchName = cellHatch.GetString().Trim();
                if (!string.IsNullOrEmpty(hatchName) && !hatchName.Equals("Không", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetDouble(cellScale, out double scale))
                    {
                        hatchCatalog[hatchName] = new HatchRecord(hatchName, scale);
                    }
                }

                if (cellAngle.IsEmpty() && cellWeight.IsEmpty() && cellHatch.IsEmpty() && cellScale.IsEmpty())
                {
                    break;
                }

                row++;
                if (row > 100) break;
            }
        }

        // ====================================================================
        // ĐỌC SHEET CẤU HÌNH CF.Ga
        // ====================================================================
        private record ConfigSpecTemplate(
            double PadExpansion,
            double PadThickness,
            double BaseExpansion,
            double BaseHaunch,
            double TopHaunch,
            double WallHorizSpacingDelta,
            int WallHorizDiaStepDown,
            string WallDimPrefix,
            bool HasDowel,
            RebarSpec? DowelSpec,
            double DowelLength,
            double DowelHoleDia,
            double MortarThickness,
            string LiftingHookSpec,
            string LiftingHookSize,
            string NeckStructure,
            string NeckPosition,
            double NeckInnerWidth,
            double NeckInnerLength,
            bool HasNeckHaunch,
            double NeckHaunchWidth,
            double NeckHaunchHeight,
            string NeckAngleSteel,
            string NeckAnglePosition,
            RebarSpec? NeckAnchorSpec,
            string NeckDimPrefix,
            double NeckHorizSpacingDelta,
            int NeckHorizDiaStepDown,
            string CoverType,
            double CoverHeight,
            string CoverBlockName,
            int CoverPanelCount,
            double CoverGap,
            string CoverPanelDirection,
            PrecastCoverConfig PrecastCover,
            string CatchBasinStyle,
            string CatchBasinPosition,
            string CatchBasinWallDir,
            double CatchBasinHoleWidth,
            double CatchBasinHoleHeight,
            double CatchBasinChamberLength,
            double CatchBasinChamberHeight,
            double CatchBasinWallThickness,
            double CatchBasinBaseThickness,
            double CatchBasinPadThickness,
            double CatchBasinPadExpansion,
            string CatchBasinStructure,
            RebarSpec? CatchBasinRebar,
            double CoverBottom,
            double CoverWall,
            double CoverTop,
            double CoverNeck,
            double CoverCover,
            double CoverPipe,
            double CoverCatchBasin,
            string HookBaseBottom,
            string HookBaseTop,
            string HookWallOuter,
            string HookWallInner,
            string HookTopTop,
            string HookTopBottom,
            string HookNeckOuter,
            string HookNeckInner,
            string RebarDistributionMode,
            string RebarHookMode,
            int RebarHookDiaMultiple,
            string TieArrangement,
            string TieShapeBase,
            string TieShapeWall,
            string TieShapeTop,
            string TieShapeNeck,
            RebarSpec? TieSpec,
            int TieLapMultiple,
            double TieHookC,
            double TieHookZU,
            double TieHookMidU,
            string LadderLocation,
            int LadderDia,
            double LadderFirstStep,
            double LadderPitch,
            double LadderHook,
            double LadderDepth,
            double LadderWidth,
            string LadderPrefix,
            double PipeMortar,
            int PipeReinforceDia,
            string PipeReinforceShape,
            int PipeReinforceCount,
            double PipeReinforceExpansion,
            string PipeReinforcePrefix,
            double[] PipeOffsets,
            int TopHoleReinforceDia,
            string TopHoleReinforceShape,
            int TopHoleReinforceCount,
            double TopHoleReinforceExpansion,
            string TopHoleReinforcePrefix,
            bool IsAbsoluteElevation,
            string LabelZBottom,
            string LabelZTop,
            string LabelZT1,
            string LabelZT2,
            string LabelZT3,
            string LabelZT4,
            string MaterialPad,
            string MaterialBase,
            string MaterialNeck,
            string MaterialMortar,
            HatchRecord HatchPad,
            HatchRecord HatchBase,
            HatchRecord HatchNeck,
            HatchRecord HatchPipe,
            HatchRecord HatchMortar);

        private static ConfigSpecTemplate ReadConfigSheet(
            IXLWorksheet ws,
            string filePath,
            ManholeType type,
            Dictionary<string, HatchRecord> hatchCatalog,
            List<Diagnostic> diagnostics)
        {
            // Xác định độ dịch dòng nếu là Loại 3 (Ga thăm + thu nắp rời)
            int shift = (type == ManholeType.GaThamThuNapRoi) ? 2 : 0;

            // Khối 1: Lớp lót (C3:D3)
            double padExp = ReadDouble(ws.Cell(3, 3), 100);
            double padThick = ReadDouble(ws.Cell(3, 4), 100);

            // Khối 2: Bản đáy ga (C5:D5)
            double baseExp = ReadDouble(ws.Cell(5, 3), 100);
            double baseHaunch = ReadDouble(ws.Cell(5, 4), 100);

            // Khối 3: Tường ga (C7:E7)
            double wallHorizSpacingDelta = ReadDouble(ws.Cell(7, 3), 0);
            int wallHorizDiaStepDown = (int)ReadDouble(ws.Cell(7, 4), 0);
            string wallDimPrefix = ws.Cell(7, 5).GetString().Trim();
            if (string.IsNullOrEmpty(wallDimPrefix)) wallDimPrefix = "Htb=";

            // Khối 4: Trần ga (Hàng 9)
            double topHaunch = 0;
            bool hasDowel = false;
            RebarSpec? dowelSpec = null;
            double dowelLength = 0;
            double dowelHoleDia = 0;
            double mortarThickness = 0;
            string liftingHookSpec = "";
            string liftingHookSize = "";

            if (type == ManholeType.GaThamLienKhoi)
            {
                topHaunch = ReadDouble(ws.Cell(9, 3), 100);
            }
            else
            {
                string dowelStr = ws.Cell(9, 3).GetString().Trim();
                hasDowel = dowelStr.Equals("Có", StringComparison.OrdinalIgnoreCase) ||
                           dowelStr.Equals("Co", StringComparison.OrdinalIgnoreCase);
                string dowelRebarText = ws.Cell(9, 4).GetString().Trim();
                dowelSpec = RebarSpec.Parse(dowelRebarText);
                dowelLength = ReadDouble(ws.Cell(9, 5), 400);
                dowelHoleDia = ReadDouble(ws.Cell(9, 6), 25);
                mortarThickness = ReadDouble(ws.Cell(9, 7), 15);
                liftingHookSpec = ws.Cell(9, 8).GetString().Trim();
                liftingHookSize = ws.Cell(9, 9).GetString().Trim();
            }

            // Khối 5: Cổ ga (Hàng 11)
            string neckStructure = ws.Cell(11, 3).GetString().Trim();
            string neckPosition = ws.Cell(11, 4).GetString().Trim();
            string neckSizeStr = ws.Cell(11, 5).GetString().Trim(); // 820x570
            ParseDimensions(neckSizeStr, out double neckW, out double neckL);

            string neckHaunchStr = ws.Cell(11, 6).GetString().Trim();
            bool hasNeckHaunch = neckHaunchStr.Equals("Có", StringComparison.OrdinalIgnoreCase);
            ParseDimensions(ws.Cell(11, 7).GetString().Trim(), out double nhW, out double nhH);

            string neckAngleSteel = ws.Cell(11, 8).GetString().Trim();
            string neckAnglePos = ws.Cell(11, 9).GetString().Trim();
            RebarSpec? neckAnchorSpec = RebarSpec.Parse(ws.Cell(11, 10).GetString().Trim());
            string neckDimPrefix = ws.Cell(11, 11).GetString().Trim();
            double neckHorizSpacingDelta = ReadDouble(ws.Cell(11, 12), 0);
            int neckHorizDiaStepDown = (int)ReadDouble(ws.Cell(11, 13), 0);

            // Khối 6: Nắp ga (Hàng 13)
            string coverType = ws.Cell(13, 3).GetString().Trim();
            double coverHeight = ReadDouble(ws.Cell(13, 4), 100);
            string coverBlockName = ws.Cell(13, 5).GetString().Trim();
            int coverPanelCount = (int)ReadDouble(ws.Cell(13, 9), 2);
            double coverGap = ReadDouble(ws.Cell(13, 10), 5);
            string coverPanelDir = ws.Cell(13, 11).GetString().Trim();
            if (string.IsNullOrEmpty(coverPanelDir)) coverPanelDir = "Ngang";

            SourceRef CoverCellSource(IXLCell cell) =>
                new(filePath, "CF.Ga", cell.Address.ToString(), cell.GetString());

            var coverBottomDiaCell = ws.Cell(15, 3);
            var coverTopDiaCell = ws.Cell(15, 4);
            var coverSpacingCell = ws.Cell(15, 5);
            var coverTieCell = ws.Cell(15, 6);
            var coverTieShapeCell = ws.Cell(15, 7);
            var coverAngleCell = ws.Cell(15, 8);
            var coverAngleQtyCell = ws.Cell(15, 9);
            var coverAngleAnchorCell = ws.Cell(15, 10);
            var coverLiftingHookCell = ws.Cell(15, 11);
            var coverLiftingHookSizeCell = ws.Cell(15, 12);
            var coverRatioCell = ws.Cell(15, 13);
            var precastCover = new PrecastCoverConfig
            {
                BottomLayerBarDiameter = (int)ReadDouble(coverBottomDiaCell, 0),
                BottomLayerBarDiameterSource = CoverCellSource(coverBottomDiaCell),
                TopLayerBarDiameter = (int)ReadDouble(coverTopDiaCell, 0),
                TopLayerBarDiameterSource = CoverCellSource(coverTopDiaCell),
                BarSpacingMm = ReadDouble(coverSpacingCell, 0),
                BarSpacingSource = CoverCellSource(coverSpacingCell),
                TieRebar = RebarSpec.Parse(
                    coverTieCell.GetString().Trim(),
                    CoverCellSource(coverTieCell), diagnostics, "CF.Ga"),
                TieShape = coverTieShapeCell.GetString().Trim(),
                TieShapeSource = CoverCellSource(coverTieShapeCell),
                AngleSteelCode = coverAngleCell.GetString().Trim(),
                AngleSteelCodeSource = CoverCellSource(coverAngleCell),
                AngleSteelQuantity = (int)ReadDouble(coverAngleQtyCell, 0),
                AngleSteelQuantitySource = CoverCellSource(coverAngleQtyCell),
                AngleAnchorRebar = RebarSpec.Parse(
                    coverAngleAnchorCell.GetString().Trim(),
                    CoverCellSource(coverAngleAnchorCell), diagnostics, "CF.Ga"),
                LiftingHookSpec = coverLiftingHookCell.GetString().Trim(),
                LiftingHookSpecSource = CoverCellSource(coverLiftingHookCell),
                LiftingHookSize = coverLiftingHookSizeCell.GetString().Trim(),
                LiftingHookSizeSource = CoverCellSource(coverLiftingHookSizeCell),
                CoverRatioPercent = ReadDouble(coverRatioCell, 0),
                CoverRatioSource = CoverCellSource(coverRatioCell)
            };

            // Cửa thu nước (Chỉ có ở Loại 3: Hàng 17)
            string cbStyle = "Kiểu 1";
            string cbPos = "Thân ga";
            string cbDir = "Phải T1";
            double cbHoleW = 0, cbHoleH = 0;
            double cbChamberL = 0, cbChamberH = 0;
            double cbWallT = 0, cbBaseT = 0, cbPadT = 0, cbPadExp = 0;
            string cbStruct = "BTCT";
            RebarSpec? cbRebar = null;

            if (type == ManholeType.GaThamThuNapRoi)
            {
                cbPos = ws.Cell(17, 3).GetString().Trim();
                cbDir = ws.Cell(17, 4).GetString().Trim();
                cbStyle = ws.Cell(17, 5).GetString().Trim();
                if (!string.Equals(cbStyle, "Kiểu 1", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Block, "", CoverCellSource(ws.Cell(17, 5)),
                        $"Kiểu cửa thu '{cbStyle}' chưa được VHGpro 3.2 hỗ trợ; cần chọn 'Kiểu 1'."));
                ParseDimensions(ws.Cell(17, 6).GetString().Trim(), out cbHoleW, out cbHoleH);
                ParseDimensions(ws.Cell(17, 7).GetString().Trim(), out cbChamberL, out cbChamberH);
                cbWallT = ReadDouble(ws.Cell(17, 8), 125);
                cbBaseT = ReadDouble(ws.Cell(17, 9), 125);
                cbPadT = ReadDouble(ws.Cell(17, 10), 100);
                cbPadExp = ReadDouble(ws.Cell(17, 11), 50);
                var cbStructureCell = ws.Cell(17, 12);
                var cbRebarCell = ws.Cell(17, 13);
                cbStruct = cbStructureCell.GetString().Trim();
                cbRebar = RebarSpec.Parse(
                    cbRebarCell.GetString().Trim(), CoverCellSource(cbRebarCell), diagnostics);

                if (string.IsNullOrWhiteSpace(cbStruct))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Block,
                        "",
                        CoverCellSource(cbStructureCell),
                        "Cấu hình kết cấu cửa thu bắt buộc phải có giá trị trong CF.Ga!L17."));
                }

                if (string.Equals(cbStruct, "BTCT", StringComparison.OrdinalIgnoreCase))
                {
                    if (cbRebar is null && string.IsNullOrWhiteSpace(cbRebarCell.GetString()))
                    {
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Block,
                            "",
                            CoverCellSource(cbRebarCell),
                            "Cửa thu BTCT cần có quy cách thép trong CF.Ga!M17."));
                    }
                    else if (cbRebar is { } catchBasinRebar &&
                             (catchBasinRebar.Diameter <= 0 || catchBasinRebar.Spacing <= 0))
                    {
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Block,
                            "",
                            CoverCellSource(cbRebarCell),
                            "Quy cách thép cửa thu BTCT phải có đường kính và bước rải lớn hơn 0."));
                    }
                }
            }

            // Lớp bê tông bảo vệ (Hàng 17 + shift)
            int rProtect = 17 + shift;
            double covBottom = ReadDouble(ws.Cell(rProtect, 3), 30);
            double covWall = ReadDouble(ws.Cell(rProtect, 4), 30);
            double covTop = ReadDouble(ws.Cell(rProtect, 5), 30);
            double covNeck = ReadDouble(ws.Cell(rProtect, 6), 25);
            double covCover = ReadDouble(ws.Cell(rProtect, 7), 15);
            double covPipe = ReadDouble(ws.Cell(rProtect, 8), 15);
            double covCatchBasin = (type == ManholeType.GaThamThuNapRoi) ? ReadDouble(ws.Cell(rProtect, 9), 25) : 25;

            // Định dạng thép ga & Uốn móc (Hàng 20 + shift)
            int rHook = 20 + shift;
            string hookBB = ws.Cell(rHook, 3).GetString().Trim(); // Đáy dưới
            string hookBT = ws.Cell(rHook, 4).GetString().Trim(); // Đáy trên
            string hookWO = ws.Cell(rHook, 5).GetString().Trim(); // Tường ngoài
            string hookWI = ws.Cell(rHook, 6).GetString().Trim(); // Tường trong
            string hookTT = ws.Cell(rHook, 7).GetString().Trim(); // Trần trên
            string hookTB = ws.Cell(rHook, 8).GetString().Trim(); // Trần dưới
            string hookNO = ws.Cell(rHook, 9).GetString().Trim(); // Cổ ngoài
            string hookNI = ws.Cell(rHook, 10).GetString().Trim(); // Cổ trong

            // Chế độ rải & bẻ móc (Hàng 19/21 + shift)
            int rDist = 19 + shift;
            string rebarDistMode = ws.Cell(rDist, 11).GetString().Trim();
            if (string.IsNullOrEmpty(rebarDistMode)) rebarDistMode = "Chia đều";
            string rebarHookMode = ws.Cell(rDist, 12).GetString().Trim();
            if (string.IsNullOrEmpty(rebarHookMode)) rebarHookMode = "Theo KC";

            int rHookMult = 21 + shift;
            int hookDiaMult = (int)ReadDouble(ws.Cell(rHookMult, 12), 10);

            // Thép buộc (Hàng 23 + shift)
            int rTie = 23 + shift;
            string tieArrangement = ws.Cell(rTie, 3).GetString().Trim();
            string tieBase = ws.Cell(rTie, 4).GetString().Trim();
            string tieWall = ws.Cell(rTie, 5).GetString().Trim();
            string tieTop = ws.Cell(rTie, 6).GetString().Trim();
            string tieNeck = ws.Cell(rTie, 7).GetString().Trim();
            RebarSpec? tieSpec = RebarSpec.Parse(ws.Cell(rTie, 8).GetString().Trim());
            int tieLapMult = (int)ReadDouble(ws.Cell(rTie, 9), 30);
            double tieHookC = ReadDouble(ws.Cell(rTie, 10), 50);
            double tieHookZU = ReadDouble(ws.Cell(rTie, 11), 100);
            double tieHookMidU = ReadDouble(ws.Cell(rTie, 12), 200);

            // Thang thép (Hàng 25 + shift)
            int rLadder = 25 + shift;
            string ladderLoc = ws.Cell(rLadder, 3).GetString().Trim();
            int ladderDia = (int)ReadDouble(ws.Cell(rLadder, 4), 18);
            ParseDimensions(ws.Cell(rLadder, 5).GetString().Trim(), out double ladStep1, out double ladPitch);
            ParseDimensions3(ws.Cell(rLadder, 6).GetString().Trim(), out double ladHook, out double ladDepth, out double ladWidth);
            string ladderPrefix = ws.Cell(rLadder, 7).GetString().Trim();
            if (string.IsNullOrEmpty(ladderPrefix)) ladderPrefix = "Th";

            // Lỗ cống & Gia cường (Hàng 28 + shift)
            int rPipe = 28 + shift;
            double pipeMortar = ReadDouble(ws.Cell(rPipe, 3), 50);
            int pipeReinfDia = (int)ReadDouble(ws.Cell(rPipe, 4), 10);
            string pipeReinfShape = ws.Cell(rPipe, 5).GetString().Trim();
            int pipeReinfCount = (int)ReadDouble(ws.Cell(rPipe, 6), 2);
            double pipeReinfExp = ReadDouble(ws.Cell(rPipe, 7), 250);
            string pipeReinfPrefix = ws.Cell(rPipe, 8).GetString().Trim();
            if (string.IsNullOrEmpty(pipeReinfPrefix)) pipeReinfPrefix = "GC";

            double[] pipeOffsets = new double[4];
            pipeOffsets[0] = ReadDouble(ws.Cell(rPipe, 9), 0);  // e1 (T1)
            pipeOffsets[1] = ReadDouble(ws.Cell(rPipe, 10), 0); // e2 (T2)
            pipeOffsets[2] = ReadDouble(ws.Cell(rPipe, 11), 0); // e3 (T3)
            pipeOffsets[3] = ReadDouble(ws.Cell(rPipe, 12), 0); // e4 (T4)

            // Lỗ thăm trần (Hàng 30 + shift)
            int rTopHole = 30 + shift;
            int topHoleReinfDia = (int)ReadDouble(ws.Cell(rTopHole, 3), 10);
            string topHoleReinfShape = ws.Cell(rTopHole, 4).GetString().Trim();
            int topHoleReinfCount = (int)ReadDouble(ws.Cell(rTopHole, 5), 2);
            double topHoleReinfExp = ReadDouble(ws.Cell(rTopHole, 6), 250);
            string topHoleReinfPrefix = ws.Cell(rTopHole, 7).GetString().Trim();
            if (string.IsNullOrEmpty(topHoleReinfPrefix)) topHoleReinfPrefix = "GC";

            // Cao độ (Hàng 32 + shift)
            int rElev = 32 + shift;
            string elevModeStr = ws.Cell(rElev, 3).GetString().Trim();
            bool isAbsElev = elevModeStr.Equals("Tuyệt đối", StringComparison.OrdinalIgnoreCase);
            string lZBottom = ws.Cell(rElev, 4).GetString().Trim();
            string lZTop = ws.Cell(rElev, 5).GetString().Trim();
            string lZT1 = ws.Cell(rElev, 6).GetString().Trim();
            string lZT2 = ws.Cell(rElev, 7).GetString().Trim();
            string lZT3 = ws.Cell(rElev, 8).GetString().Trim();
            string lZT4 = ws.Cell(rElev, 9).GetString().Trim();

            // Ghi chú vật liệu (Hàng 34 + shift)
            int rMat = 34 + shift;
            string matPad = ws.Cell(rMat, 3).GetString().Trim();
            string matBase = ws.Cell(rMat, 4).GetString().Trim();
            string matNeck = ws.Cell(rMat, 5).GetString().Trim();
            string matMortar = ws.Cell(rMat, 6).GetString().Trim();

            // Hatch vật liệu (Hàng 36 + shift)
            int rHatch = 36 + shift;
            string hPad = ws.Cell(rHatch, 3).GetString().Trim();
            string hBase = ws.Cell(rHatch, 4).GetString().Trim();
            string hNeck = ws.Cell(rHatch, 6).GetString().Trim();
            string hPipe = ws.Cell(rHatch, 8).GetString().Trim();
            string hMortar = ws.Cell(rHatch, 9).GetString().Trim();

            HatchRecord hatchPad = LookupHatch(hatchCatalog, hPad, "AR-CONC", 1);
            HatchRecord hatchBase = LookupHatch(hatchCatalog, hBase, "ANSI32", 10);
            HatchRecord hatchNeck = LookupHatch(hatchCatalog, hNeck, "ANSI32", 10);
            HatchRecord hatchPipe = LookupHatch(hatchCatalog, hPipe, "ANSI31", 10);
            HatchRecord hatchMortar = LookupHatch(hatchCatalog, hMortar, "ANSI37", 10);

            return new ConfigSpecTemplate(
                padExp, padThick, baseExp, baseHaunch, topHaunch,
                wallHorizSpacingDelta, wallHorizDiaStepDown, wallDimPrefix,
                hasDowel, dowelSpec, dowelLength, dowelHoleDia, mortarThickness, liftingHookSpec, liftingHookSize,
                neckStructure, neckPosition, neckW, neckL, hasNeckHaunch, nhW, nhH,
                neckAngleSteel, neckAnglePos, neckAnchorSpec, neckDimPrefix, neckHorizSpacingDelta, neckHorizDiaStepDown,
                coverType, coverHeight, coverBlockName, coverPanelCount, coverGap, coverPanelDir, precastCover,
                cbStyle, cbPos, cbDir, cbHoleW, cbHoleH, cbChamberL, cbChamberH, cbWallT, cbBaseT, cbPadT, cbPadExp, cbStruct, cbRebar,
                covBottom, covWall, covTop, covNeck, covCover, covPipe, covCatchBasin,
                hookBB, hookBT, hookWO, hookWI, hookTT, hookTB, hookNO, hookNI,
                rebarDistMode, rebarHookMode, hookDiaMult,
                tieArrangement, tieBase, tieWall, tieTop, tieNeck, tieSpec, tieLapMult, tieHookC, tieHookZU, tieHookMidU,
                ladderLoc, ladderDia, ladStep1, ladPitch, ladHook, ladDepth, ladWidth, ladderPrefix,
                pipeMortar, pipeReinfDia, pipeReinfShape, pipeReinfCount, pipeReinfExp, pipeReinfPrefix, pipeOffsets,
                topHoleReinfDia, topHoleReinfShape, topHoleReinfCount, topHoleReinfExp, topHoleReinfPrefix,
                isAbsElev, lZBottom, lZTop, lZT1, lZT2, lZT3, lZT4,
                matPad, matBase, matNeck, matMortar,
                hatchPad, hatchBase, hatchNeck, hatchPipe, hatchMortar);
        }

        private static HatchRecord LookupHatch(
            Dictionary<string, HatchRecord> catalog, string name, string defaultPattern, double defaultScale)
        {
            if (!string.IsNullOrEmpty(name) && catalog.TryGetValue(name, out var rec))
                return rec;
            return new HatchRecord(string.IsNullOrEmpty(name) ? defaultPattern : name, defaultScale);
        }

        // ====================================================================
        // ĐỌC MỘT DÒNG HỐ GA
        // ====================================================================
        private static NormalizedManholeSpec? ReadManholeRow(
            IXLWorksheet ws,
            int row,
            string filePath,
            string name,
            ManholeType type,
            ConfigSpecTemplate cfg,
            Dictionary<string, PipeRecord> pipeCatalog,
            List<Diagnostic> diagnostics)
        {
            string sheetName = ws.Name;

            // Kích thước lòng ga (Cột C, D, E)
            double L = ReadDoubleWithValidation(ws.Cell(row, 3), filePath, sheetName, name, "Chiều dài L", diagnostics, min: 100);
            double B = ReadDoubleWithValidation(ws.Cell(row, 4), filePath, sheetName, name, "Chiều rộng B", diagnostics, min: 100);
            double H = ReadDoubleWithValidation(ws.Cell(row, 5), filePath, sheetName, name, "Chiều cao H", diagnostics, min: 100);

            // Chiều dày cấu kiện (Cột F, I, L)
            double baseF = ReadDoubleWithValidation(ws.Cell(row, 6), filePath, sheetName, name, "Dày đáy F", diagnostics, min: 50);
            double wallT = ReadDoubleWithValidation(ws.Cell(row, 9), filePath, sheetName, name, "Dày tường t", diagnostics, min: 50);
            double topT = ReadDoubleWithValidation(ws.Cell(row, 12), filePath, sheetName, name, "Dày trần Ltrần", diagnostics, min: 50);

            // Cốt thép chỉ định
            var baseBottomRebar = ReadRebarCell(ws.Cell(row, 7), filePath, sheetName, name, diagnostics);
            var baseTopRebar = ReadRebarCell(ws.Cell(row, 8), filePath, sheetName, name, diagnostics);
            var wallOuterRebar = ReadRebarCell(ws.Cell(row, 10), filePath, sheetName, name, diagnostics);
            var wallInnerRebar = ReadRebarCell(ws.Cell(row, 11), filePath, sheetName, name, diagnostics);
            var topTopRebar = ReadRebarCell(ws.Cell(row, 13), filePath, sheetName, name, diagnostics);
            var topBottomRebar = ReadRebarCell(ws.Cell(row, 14), filePath, sheetName, name, diagnostics);

            // Cống kết nối 4 hướng (T1: O-P, T2: Q-R, T3: S-T, T4: U-V)
            var pipeT1 = ReadPipeConnection(ws.Cell(row, 15), ws.Cell(row, 16), WallDirection.T1, filePath, sheetName, name, cfg.PipeMortar, cfg.PipeOffsets[0], pipeCatalog, diagnostics);
            var pipeT2 = ReadPipeConnection(ws.Cell(row, 17), ws.Cell(row, 18), WallDirection.T2, filePath, sheetName, name, cfg.PipeMortar, cfg.PipeOffsets[1], pipeCatalog, diagnostics);
            var pipeT3 = ReadPipeConnection(ws.Cell(row, 19), ws.Cell(row, 20), WallDirection.T3, filePath, sheetName, name, cfg.PipeMortar, cfg.PipeOffsets[2], pipeCatalog, diagnostics);
            var pipeT4 = ReadPipeConnection(ws.Cell(row, 21), ws.Cell(row, 22), WallDirection.T4, filePath, sheetName, name, cfg.PipeMortar, cfg.PipeOffsets[3], pipeCatalog, diagnostics);

            // Cổ ga (Cột W, X, Y, Z)
            double neckHeight = ReadDouble(ws.Cell(row, 23), 0);
            double neckThickness = ReadDouble(ws.Cell(row, 24), 0);
            var neckOuterRebar = ReadRebarCell(ws.Cell(row, 25), filePath, sheetName, name, diagnostics);
            var neckInnerRebar = ReadRebarCell(ws.Cell(row, 26), filePath, sheetName, name, diagnostics);

            // Cao độ đáy ga (Cột AA: mét)
            double elevationAA = ReadDouble(ws.Cell(row, 27), 0);

            return new NormalizedManholeSpec
            {
                Type = type,
                Name = name,
                RowIndex = row,

                L = L,
                B = B,
                H = H,

                BaseThickness = baseF,
                WallThickness = wallT,
                TopThickness = topT,

                PadExpansion = cfg.PadExpansion,
                PadThickness = cfg.PadThickness,
                BaseExpansion = cfg.BaseExpansion,
                BaseHaunch = cfg.BaseHaunch,
                TopHaunch = cfg.TopHaunch,

                HasDowel = cfg.HasDowel,
                DowelSpec = cfg.DowelSpec,
                DowelLength = cfg.DowelLength,
                DowelHoleDia = cfg.DowelHoleDia,
                MortarThickness = cfg.MortarThickness,
                LiftingHookSpec = cfg.LiftingHookSpec,
                LiftingHookSize = cfg.LiftingHookSize,

                NeckHeight = neckHeight,
                NeckWallThickness = neckThickness,
                NeckStructure = cfg.NeckStructure,
                NeckPosition = cfg.NeckPosition,
                NeckInnerWidth = cfg.NeckInnerWidth,
                NeckInnerLength = cfg.NeckInnerLength,
                HasNeckHaunch = cfg.HasNeckHaunch,
                NeckHaunchWidth = cfg.NeckHaunchWidth,
                NeckHaunchHeight = cfg.NeckHaunchHeight,
                NeckAngleSteel = cfg.NeckAngleSteel,
                NeckAnglePosition = cfg.NeckAnglePosition,
                NeckAnchorSpec = cfg.NeckAnchorSpec,
                NeckHorizSpacingDelta = cfg.NeckHorizSpacingDelta,
                NeckHorizDiaStepDown = cfg.NeckHorizDiaStepDown,

                CoverType = cfg.CoverType,
                CoverHeight = cfg.CoverHeight,
                CoverBlockName = cfg.CoverBlockName,
                CoverPanelCount = cfg.CoverPanelCount,
                CoverGap = cfg.CoverGap,
                CoverPanelDirection = cfg.CoverPanelDirection,
                PrecastCover = cfg.PrecastCover,

                CatchBasinStyle = cfg.CatchBasinStyle,
                CatchBasinPosition = cfg.CatchBasinPosition,
                CatchBasinWallDir = cfg.CatchBasinWallDir,
                CatchBasinHoleWidth = cfg.CatchBasinHoleWidth,
                CatchBasinHoleHeight = cfg.CatchBasinHoleHeight,
                CatchBasinChamberLength = cfg.CatchBasinChamberLength,
                CatchBasinChamberHeight = cfg.CatchBasinChamberHeight,
                CatchBasinWallThickness = cfg.CatchBasinWallThickness,
                CatchBasinBaseThickness = cfg.CatchBasinBaseThickness,
                CatchBasinPadThickness = cfg.CatchBasinPadThickness,
                CatchBasinPadExpansion = cfg.CatchBasinPadExpansion,
                CatchBasinStructure = cfg.CatchBasinStructure,
                CatchBasinRebar = cfg.CatchBasinRebar,

                BaseBottomRebar = baseBottomRebar,
                BaseTopRebar = baseTopRebar,
                WallOuterRebar = wallOuterRebar,
                WallInnerRebar = wallInnerRebar,
                TopTopRebar = topTopRebar,
                TopBottomRebar = topBottomRebar,
                NeckOuterRebar = neckOuterRebar,
                NeckInnerRebar = neckInnerRebar,

                WallHorizSpacingDelta = cfg.WallHorizSpacingDelta,
                WallHorizDiaStepDown = cfg.WallHorizDiaStepDown,
                WallDimPrefix = cfg.WallDimPrefix,

                CoverBottom = cfg.CoverBottom,
                CoverWall = cfg.CoverWall,
                CoverTop = cfg.CoverTop,
                CoverNeck = cfg.CoverNeck,
                CoverCover = cfg.CoverCover,
                CoverPipe = cfg.CoverPipe,
                CoverCatchBasin = cfg.CoverCatchBasin,

                HookBaseBottom = cfg.HookBaseBottom,
                HookBaseTop = cfg.HookBaseTop,
                HookWallOuter = cfg.HookWallOuter,
                HookWallInner = cfg.HookWallInner,
                HookTopTop = cfg.HookTopTop,
                HookTopBottom = cfg.HookTopBottom,
                HookNeckOuter = cfg.HookNeckOuter,
                HookNeckInner = cfg.HookNeckInner,

                RebarDistributionMode = cfg.RebarDistributionMode,
                RebarHookMode = cfg.RebarHookMode,
                RebarHookDiaMultiple = cfg.RebarHookDiaMultiple,

                TieArrangement = cfg.TieArrangement,
                TieShapeBase = cfg.TieShapeBase,
                TieShapeWall = cfg.TieShapeWall,
                TieShapeTop = cfg.TieShapeTop,
                TieShapeNeck = cfg.TieShapeNeck,
                TieSpec = cfg.TieSpec,
                TieLapMultiple = cfg.TieLapMultiple,
                TieHookC = cfg.TieHookC,
                TieHookZU = cfg.TieHookZU,
                TieHookMidU = cfg.TieHookMidU,

                LadderLocation = cfg.LadderLocation,
                LadderDia = cfg.LadderDia,
                LadderFirstStep = cfg.LadderFirstStep,
                LadderPitch = cfg.LadderPitch,
                LadderHook = cfg.LadderHook,
                LadderDepth = cfg.LadderDepth,
                LadderWidth = cfg.LadderWidth,
                LadderPrefix = cfg.LadderPrefix,

                PipeMortar = cfg.PipeMortar,
                PipeReinforceDia = cfg.PipeReinforceDia,
                PipeReinforceShape = cfg.PipeReinforceShape,
                PipeReinforceCount = cfg.PipeReinforceCount,
                PipeReinforceExpansion = cfg.PipeReinforceExpansion,
                PipeReinforcePrefix = cfg.PipeReinforcePrefix,

                TopHoleReinforceDia = cfg.TopHoleReinforceDia,
                TopHoleReinforceShape = cfg.TopHoleReinforceShape,
                TopHoleReinforceCount = cfg.TopHoleReinforceCount,
                TopHoleReinforceExpansion = cfg.TopHoleReinforceExpansion,
                TopHoleReinforcePrefix = cfg.TopHoleReinforcePrefix,

                PipeT1 = pipeT1,
                PipeT2 = pipeT2,
                PipeT3 = pipeT3,
                PipeT4 = pipeT4,

                IsAbsoluteElevation = cfg.IsAbsoluteElevation,
                BaseInvertElevation = elevationAA,
                LabelZBottom = cfg.LabelZBottom,
                LabelZTop = cfg.LabelZTop,
                LabelZT1 = cfg.LabelZT1,
                LabelZT2 = cfg.LabelZT2,
                LabelZT3 = cfg.LabelZT3,
                LabelZT4 = cfg.LabelZT4,

                MaterialPad = cfg.MaterialPad,
                MaterialBase = cfg.MaterialBase,
                MaterialNeck = cfg.MaterialNeck,
                MaterialMortar = cfg.MaterialMortar,

                HatchPad = cfg.HatchPad,
                HatchBase = cfg.HatchBase,
                HatchNeck = cfg.HatchNeck,
                HatchPipe = cfg.HatchPipe,
                HatchMortar = cfg.HatchMortar
            };
        }

        // ====================================================================
        // HELPER FUNCTIONS
        // ====================================================================

        private static PipeConnectionSpec? ReadPipeConnection(
            IXLCell cellCode,
            IXLCell cellLevel,
            WallDirection dir,
            string filePath,
            string sheetName,
            string manholeName,
            double mortarThickness,
            double defaultOffset,
            Dictionary<string, PipeRecord> catalog,
            List<Diagnostic> diagnostics)
        {
            string rawCode = cellCode.GetString().Trim();
            var sourceCode = new SourceRef(filePath, sheetName, cellCode.Address.ToString(), rawCode);
            var sourceLevel = new SourceRef(filePath, sheetName, cellLevel.Address.ToString(), cellLevel.GetString());
            bool hasCode = !string.IsNullOrWhiteSpace(rawCode);
            bool hasLevel = !string.IsNullOrWhiteSpace(sourceLevel.RawValue);
            if (!hasCode && !hasLevel) return null;
            if (!hasCode)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block, manholeName, sourceCode,
                    $"Thành {dir} có khoảng cách đáy cống nhưng thiếu mã cống. Cần nhập cả hai ô hoặc xóa cả hai ô."));
                return null;
            }
            if (!hasLevel || !TryGetDouble(cellLevel, out double level) ||
                !double.IsFinite(level) || level < 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block, manholeName, sourceLevel,
                    $"Thành {dir} có mã cống '{rawCode}' nhưng khoảng cách từ đáy cống đến đáy ga thiếu hoặc không hợp lệ. Cần nhập giá trị mm không âm."));
                return null;
            }

            // Tra cứu cống trong catalog
            catalog.TryGetValue(rawCode, out var pipeRecord);
            if (pipeRecord == null)
            {
                // Thử tìm theo dạng số nếu nhập ví dụ "200" thay vì "D200" hoặc ngược lại
                string altCode = rawCode.StartsWith("D", StringComparison.OrdinalIgnoreCase)
                    ? rawCode.Substring(1)
                    : $"D{rawCode}";

                catalog.TryGetValue(altCode, out pipeRecord);
            }

            if (pipeRecord == null)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    manholeName,
                    sourceCode,
                    $"Quy cách cống '{rawCode}' hướng {dir} không tồn tại trong sheet 'CF.Cong'; cần bổ sung mã hợp lệ trước khi vẽ.",
                    "PipeCatalogLookup"));
                return null;
            }

            return new PipeConnectionSpec
            {
                Direction = dir,
                PipeCode = rawCode,
                InvertLevelFromBottom = level,
                OffsetFromCenter = defaultOffset,
                PipeCatalogInfo = pipeRecord,
                MortarThickness = mortarThickness,
                SourceCode = sourceCode,
                SourceLevel = sourceLevel
            };
        }

        private static RebarSpec? ReadRebarCell(
            IXLCell cell,
            string filePath,
            string sheetName,
            string manholeName,
            List<Diagnostic> diagnostics)
        {
            if (cell.IsEmpty()) return null;
            string text = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(text)) return null;

            var source = new SourceRef(filePath, sheetName, cell.Address.ToString(), text);
            return RebarSpec.Parse(text, source, diagnostics, manholeName);
        }

        private static double ReadDouble(IXLCell cell, double defaultValue)
        {
            if (cell.IsEmpty()) return defaultValue;
            if (TryGetDouble(cell, out double val)) return val;
            return defaultValue;
        }

        private static double ReadDoubleWithValidation(
            IXLCell cell,
            string filePath,
            string sheetName,
            string manholeName,
            string fieldName,
            List<Diagnostic> diagnostics,
            double min = 0)
        {
            var source = new SourceRef(filePath, sheetName, cell.Address.ToString(), cell.GetString());
            if (cell.IsEmpty())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    manholeName,
                    source,
                    $"Trường '{fieldName}' bắt buộc nhưng để trống trong ô {cell.Address}."));
                return 0;
            }

            if (!TryGetDouble(cell, out double val))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    manholeName,
                    source,
                    $"Trường '{fieldName}' có giá trị không phải số: '{source.RawValue}' tại ô {cell.Address}."));
                return 0;
            }

            if (val < min)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    manholeName,
                    source,
                    $"Trường '{fieldName}' có giá trị {val} nhỏ hơn giới hạn cho phép ({min} mm)."));
            }

            return val;
        }

        private static bool TryGetDouble(IXLCell cell, out double result)
        {
            result = 0;
            if (cell == null || cell.IsEmpty()) return false;

            if (cell.TryGetValue(out result))
                return true;

            string s = cell.GetString().Trim().Replace(",", ".");
            return double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        private static void ParseDimensions(string text, out double w, out double h)
        {
            w = 0;
            h = 0;
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] parts = text.Split(new[] { 'x', 'X', '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                double.TryParse(parts[0].Trim(), out w);
                double.TryParse(parts[1].Trim(), out h);
            }
        }

        private static void ParseDimensions3(string text, out double a, out double b, out double c)
        {
            a = 0;
            b = 0;
            c = 0;
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] parts = text.Split(new[] { 'x', 'X', '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                double.TryParse(parts[0].Trim(), out a);
                double.TryParse(parts[1].Trim(), out b);
                double.TryParse(parts[2].Trim(), out c);
            }
        }
    }
}
