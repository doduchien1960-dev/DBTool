using System.Collections.Generic;

namespace DBTool.Core
{
    /// <summary>
    /// Đặc tả kết nối cống tại một hướng tường (T1..T4)
    /// </summary>
    public record PipeConnectionSpec
    {
        public WallDirection Direction { get; init; }
        public string PipeCode { get; init; } = "";
        public double InvertLevelFromBottom { get; init; } // h: Đáy thông thủy cống tính từ đáy lòng ga (z=0)
        public double OffsetFromCenter { get; init; }      // e: Lệch tim theo quy ước (dương về bên trái nhìn từ trong ra)
        public PipeRecord? PipeCatalogInfo { get; init; }
        public double MortarThickness { get; init; }       // m

        // Các thuộc tính hình học dẫn xuất (mm). Với cống hộp, các thuộc tính
        // legacy Dia được hiểu là kích thước phủ bì theo phương ngang trên mặt bằng.
        public bool IsBox => PipeCatalogInfo?.IsBox == true;
        public double InnerDia => PipeCatalogInfo?.InnerDia ?? 0;
        public double InnerHeight => IsBox ? PipeCatalogInfo!.BoxInnerHeight : InnerDia;
        public double BoxInnerWidth => PipeCatalogInfo?.BoxInnerWidth ?? 0;
        public double BoxInnerHeight => PipeCatalogInfo?.BoxInnerHeight ?? 0;
        public double WallThickness => PipeCatalogInfo?.WallThickness ?? 0;
        public double OuterWidth => IsBox
            ? BoxInnerWidth + 2 * WallThickness
            : PipeCatalogInfo?.OuterDia ?? (InnerDia + 2 * WallThickness);
        public double OuterHeight => IsBox
            ? BoxInnerHeight + 2 * WallThickness
            : PipeCatalogInfo?.OuterDia ?? (InnerDia + 2 * WallThickness);
        public double CutoutWidth => OuterWidth + 2 * MortarThickness;
        public double CutoutHeight => OuterHeight + 2 * MortarThickness;
        public double OuterDia => OuterWidth;
        public double CutoutDia => CutoutWidth;
        public double CutoutBottomZ => InvertLevelFromBottom - WallThickness - MortarThickness;
        public double CutoutTopZ => InvertLevelFromBottom + InnerHeight + WallThickness + MortarThickness;

        public SourceRef? SourceCode { get; init; }
        public SourceRef? SourceLevel { get; init; }
    }

    /// <summary>
    /// Đặc tả hoàn chỉnh, chuẩn hóa và bất biến của một hố ga
    /// </summary>
    public class NormalizedManholeSpec
    {
        public ManholeType Type { get; init; }
        public string Name { get; init; } = "";
        public int RowIndex { get; init; }

        // --- KÍCH THƯỚC LÒNG GA (mm) ---
        public double L { get; init; } // Chiều dài lòng trong (phương X)
        public double B { get; init; } // Chiều rộng lòng trong (phương Y)
        public double H { get; init; } // Chiều cao lòng trong (đỉnh trần trong - đáy trong)

        // --- CHIỀU DÀY CÁC BẢN (mm) ---
        public double BaseThickness { get; init; } // F: Dày bản đáy
        public double WallThickness { get; init; } // t = I: Dày thành tường chung cho 4 tường
        public double TopThickness { get; init; }  // L_trần: Dày bản trần

        // --- CẤU HÌNH BẢN ĐÁY & LỚP LÓT (CF.Ga) ---
        public double PadExpansion { get; init; }  // C3
        public double PadThickness { get; init; }  // D3
        public double BaseExpansion { get; init; } // C5
        public double BaseHaunch { get; init; }    // CF.Ga!D5 (vát nách trong tại chân tường)
        public double TopHaunch { get; init; }     // C9 (vát nách trần loại liền khối)

        // --- CẤU HÌNH TRẦN NẮP RỜI (Loại 2 & 3: C9:I9) ---
        public bool HasDowel { get; init; }
        public RebarSpec? DowelSpec { get; init; }
        public double DowelLength { get; init; }
        public double DowelHoleDia { get; init; }
        public double MortarThickness { get; init; } // G9: Chiều dày vữa đệm đỉnh tường
        public string LiftingHookSpec { get; init; } = "";
        public string LiftingHookSize { get; init; } = "";

        // --- CỔ GA (Cột W, X sheet ga & C11:M11 CF.Ga) ---
        public double NeckHeight { get; init; }          // W: Chiều cao cổ ga (0 = không dựng cổ)
        public double NeckWallThickness { get; init; }   // X: Chiều dày thành cổ ga
        public string NeckStructure { get; init; } = "BTCT"; // C11
        public string NeckPosition { get; init; } = "Góc T1-T2"; // D11
        public double NeckInnerWidth { get; init; }      // B theo X từ E11
        public double NeckInnerLength { get; init; }     // L theo Y từ E11
        public bool HasNeckHaunch { get; init; }
        public double NeckHaunchWidth { get; init; }
        public double NeckHaunchHeight { get; init; }
        public string NeckAngleSteel { get; init; } = "Không";
        public string NeckAnglePosition { get; init; } = "";
        public RebarSpec? NeckAnchorSpec { get; init; }
        public double NeckHorizSpacingDelta { get; init; }
        public int NeckHorizDiaStepDown { get; init; }

        // --- NẮP GA (C13:M15) ---
        public string CoverType { get; init; } = "Gang";
        public double CoverHeight { get; init; }
        public string CoverBlockName { get; init; } = "";
        public int CoverPanelCount { get; init; } = 1;
        public double CoverGap { get; init; } = 0;
        public string CoverPanelDirection { get; init; } = "Ngang";
        public PrecastCoverConfig PrecastCover { get; init; } = new();

        // --- CỬA THU NƯỚC (Loại 3: C17:M17) ---
        public string CatchBasinStyle { get; init; } = "Kiểu 1";
        public string CatchBasinPosition { get; init; } = "Thân ga";
        public string CatchBasinWallDir { get; init; } = "Phải T1";
        public double CatchBasinHoleWidth { get; init; }
        public double CatchBasinHoleHeight { get; init; }
        public double CatchBasinChamberLength { get; init; }
        public double CatchBasinChamberHeight { get; init; }
        public double CatchBasinWallThickness { get; init; }
        public double CatchBasinBaseThickness { get; init; }
        public double CatchBasinPadThickness { get; init; }
        public double CatchBasinPadExpansion { get; init; }
        public string CatchBasinStructure { get; init; } = "BTCT";
        public RebarSpec? CatchBasinRebar { get; init; }

        // --- CỐT THÉP CHỈ ĐỊNH TỪ SHEET GA ---
        public RebarSpec? BaseBottomRebar { get; init; } // G
        public RebarSpec? BaseTopRebar { get; init; }    // H
        public RebarSpec? WallOuterRebar { get; init; }   // J
        public RebarSpec? WallInnerRebar { get; init; }   // K
        public RebarSpec? TopTopRebar { get; init; }      // M
        public RebarSpec? TopBottomRebar { get; init; }   // N
        public RebarSpec? NeckOuterRebar { get; init; }   // Y
        public RebarSpec? NeckInnerRebar { get; init; }   // Z

        // --- ĐIỀU CHỈNH THÉP NGANG TƯỜNG (C7, D7, E7) ---
        public double WallHorizSpacingDelta { get; init; } // C7: a_ngang = a_đứng + C7
        public int WallHorizDiaStepDown { get; init; }      // D7: Giảm cấp đường kính
        public string WallDimPrefix { get; init; } = "Htb=";

        // --- LỚP BÊ TÔNG BẢO VỆ (C_bv) ---
        public double CoverBottom { get; init; } = 30;
        public double CoverWall { get; init; } = 30;
        public double CoverTop { get; init; } = 30;
        public double CoverNeck { get; init; } = 25;
        public double CoverCover { get; init; } = 15;
        public double CoverPipe { get; init; } = 15;
        public double CoverCatchBasin { get; init; } = 25;

        // --- ĐỊNH DẠNG UỐN MÓC (0M / 1M / 2M) ---
        public string HookBaseBottom { get; init; } = "0M";
        public string HookBaseTop { get; init; } = "2M";
        public string HookWallOuter { get; init; } = "2M";
        public string HookWallInner { get; init; } = "0M";
        public string HookTopTop { get; init; } = "0M";
        public string HookTopBottom { get; init; } = "2M";
        public string HookNeckOuter { get; init; } = "2M";
        public string HookNeckInner { get; init; } = "0M";

        public string RebarDistributionMode { get; init; } = "Chia đều"; // K19
        public string RebarHookMode { get; init; } = "Theo KC";          // L19
        public int RebarHookDiaMultiple { get; init; } = 10;            // L21

        // --- THÉP BUỘC (Khối 9/10) ---
        public string TieArrangement { get; init; } = "Lệch";
        public string TieShapeBase { get; init; } = "Chữ U";
        public string TieShapeWall { get; init; } = "Chữ C";
        public string TieShapeTop { get; init; } = "Chữ U";
        public string TieShapeNeck { get; init; } = "Chữ C";
        public RebarSpec? TieSpec { get; init; } // D6A300
        public int TieLapMultiple { get; init; } = 30; // 30d
        public double TieHookC { get; init; } = 50;
        public double TieHookZU { get; init; } = 100;
        public double TieHookMidU { get; init; } = 200;

        // --- THANG THÉP (Khối 10/11) ---
        public string LadderLocation { get; init; } = "Theo vị trí cổ";
        public int LadderDia { get; init; } = 18;
        public double LadderFirstStep { get; init; } = 500;
        public double LadderPitch { get; init; } = 300;
        public double LadderHook { get; init; } = 150;
        public double LadderDepth { get; init; } = 250;
        public double LadderWidth { get; init; } = 300;
        public string LadderPrefix { get; init; } = "Th";

        // --- LỖ CỐNG & GIA CƯỜNG (Khối 11/12) ---
        public double PipeMortar { get; init; } = 50;
        public int PipeReinforceDia { get; init; } = 10;
        public string PipeReinforceShape { get; init; } = "Bo+Vuông";
        public int PipeReinforceCount { get; init; } = 2;
        public double PipeReinforceExpansion { get; init; } = 250;
        public string PipeReinforcePrefix { get; init; } = "GC";

        // --- LỖ THĂM TRẦN (Khối 12/13) ---
        public int TopHoleReinforceDia { get; init; } = 10;
        public string TopHoleReinforceShape { get; init; } = "Bo+Vuông";
        public int TopHoleReinforceCount { get; init; } = 2;
        public double TopHoleReinforceExpansion { get; init; } = 250;
        public string TopHoleReinforcePrefix { get; init; } = "GC";

        // --- CỐNG KẾT NỐI 4 HƯỚNG ---
        public PipeConnectionSpec? PipeT1 { get; init; }
        public PipeConnectionSpec? PipeT2 { get; init; }
        public PipeConnectionSpec? PipeT3 { get; init; }
        public PipeConnectionSpec? PipeT4 { get; init; }

        public IEnumerable<PipeConnectionSpec> AllPipes
        {
            get
            {
                if (PipeT1 != null) yield return PipeT1;
                if (PipeT2 != null) yield return PipeT2;
                if (PipeT3 != null) yield return PipeT3;
                if (PipeT4 != null) yield return PipeT4;
            }
        }

        // --- CAO ĐỘ (Khối 13/14) ---
        public bool IsAbsoluteElevation { get; init; } = true;
        public double BaseInvertElevation { get; init; } // Cột AA (mét)
        public string LabelZBottom { get; init; } = "Z1";
        public string LabelZTop { get; init; } = "Z2";
        public string LabelZT1 { get; init; } = "ZT1";
        public string LabelZT2 { get; init; } = "ZT2";
        public string LabelZT3 { get; init; } = "ZT3";
        public string LabelZT4 { get; init; } = "ZT4";

        // --- VẬT LIỆU & HATCH (Khối 14/15/16) ---
        public string MaterialPad { get; init; } = "BT lót M100 đá 2x4";
        public string MaterialBase { get; init; } = "BTCT M250 đá 1x2";
        public string MaterialNeck { get; init; } = "BTCT M250 đá 1x2";
        public string MaterialMortar { get; init; } = "Vữa XM M100";

        public HatchRecord HatchPad { get; init; } = new("AR-CONC", 1);
        public HatchRecord HatchBase { get; init; } = new("ANSI32", 10);
        public HatchRecord HatchNeck { get; init; } = new("ANSI32", 10);
        public HatchRecord HatchPipe { get; init; } = new("ANSI31", 10);
        public HatchRecord HatchMortar { get; init; } = new("ANSI37", 10);
    }
}
