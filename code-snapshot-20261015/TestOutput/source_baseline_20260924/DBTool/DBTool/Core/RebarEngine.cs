using System;
using System.Collections.Generic;
using System.Linq;

namespace DBTool.Core
{
    public enum BarShapeType
    {
        L0M,   // Thẳng không móc
        L1M,   // Bẻ 1 móc
        L2M,   // Bẻ 2 móc
        M_C,   // Chữ C
        M_Z,   // Chữ Z
        M_U,   // Chữ U
        VG,    // Thép góc uốn
        GC_H,  // Gia cường tròn (Hoop)
        GC_T   // Gia cường vuông / chữ nhật (Frame)
    }

    /// <summary>
    /// Thực thể một thanh cốt thép cụ thể trong mô hình
    /// </summary>
    public class BarInstance
    {
        public string ComponentGroup { get; init; } = "";
        public string BarMark { get; init; } = "";
        public int Diameter { get; init; }
        public string AngleCode { get; init; } = "";
        public bool IsAngleSteel => !string.IsNullOrEmpty(AngleCode);
        public BarShapeType Shape { get; init; } = BarShapeType.L0M;
        public int Quantity { get; init; }
        public double LengthMm { get; init; }
        public double SpacingMm { get; init; }

        // Các đoạn kích thước hình học uốn (mm)
        public double L { get; init; }
        public double LT { get; init; }
        public double LP { get; init; }
        public double L1T { get; init; }
        public double L2T { get; init; }
        public double L3 { get; init; }
        public double L1P { get; init; }
        public double L2P { get; init; }

        public string Note { get; init; } = "";
    }

    /// <summary>
    /// Lưới thép 2 phương của một mặt cấu kiện
    /// </summary>
    public class RebarMesh
    {
        public string Name { get; init; } = "";
        public double WidthMm { get; init; }
        public double HeightMm { get; init; }
        public double CoverMm { get; init; }
        public List<BarInstance> Bars { get; init; } = new();
    }

    /// <summary>
    /// Động cơ tính toán phân bổ, va chạm và bẻ móc cốt thép (DBTool.Core.RebarEngine)
    /// </summary>
    public class RebarEngine
    {
        public static List<BarInstance> GenerateRebarModel(
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            Dictionary<string, SteelAngleRecord> angleCatalog)
        {
            var bars = new List<BarInstance>();

            // 1. Bản đáy (Lớp dưới & Lớp trên)
            GenerateBaseRebars(spec, geom, bars);

            // 2. Bốn tường T1, T2, T3, T4 (Lớp ngoài & Lớp trong, xử lý cắt lỗ cống)
            GenerateWallRebars(spec, geom, bars);

            // 3. Bản trần (Lớp trên & Lớp dưới, xử lý cắt lỗ nắp)
            GenerateTopSlabRebars(spec, geom, bars);

            // 4. Cổ ga (nếu W > 0)
            if (spec.NeckHeight > 0)
            {
                GenerateNeckRebars(spec, geom, bars);
            }

            // 5. Thép thang leo Th
            GenerateLadderRebars(spec, bars);

            // 6. Thép đai buộc liên kết (BLTC)
            GenerateTieRebars(spec, bars);

            // 7. Gia cường lỗ cống (GC)
            GeneratePipeReinforcement(spec, geom, bars);

            // 8. Gia cường lỗ nắp trần (GC)
            GenerateTopHoleReinforcement(spec, geom, bars);

            // 9. Thép hình miệng nắp ga (nếu có góc V)
            GenerateAngleSteel(spec, angleCatalog, bars);

            // 10. Tấm nắp BTCT đúc sẵn (nếu Loại 2 hoặc Loại 3 có tấm nắp BTCT)
            if (spec.CoverType.Equals("BTCT", StringComparison.OrdinalIgnoreCase))
            {
                GeneratePrecastCoverRebars(spec, angleCatalog, bars);
            }

            // 11. Cửa thu nước (nếu Loại 3)
            if (spec.Type == ManholeType.GaThamThuNapRoi)
            {
                GenerateCatchBasinRebars(spec, bars);
            }

            return bars;
        }

        // ====================================================================
        // THUẬT TOÁN RẢI BƯỚC THÉP CƠ SỞ (Fixed Pitch / Equal Pitch)
        // ====================================================================
        public static (int Count, double ActualSpacing, List<double> Offsets) DistributeBars(
            double spanLength, double cover, int barDia, double designPitch, string mode = "Chia đều")
        {
            double s = spanLength - 2.0 * (cover + barDia / 2.0);
            var offsets = new List<double>();

            if (s <= 0)
            {
                offsets.Add(spanLength / 2.0);
                return (1, 0, offsets);
            }

            if (designPitch <= 0) designPitch = 150;

            if (mode.Equals("Theo mật độ", StringComparison.OrdinalIgnoreCase))
            {
                int k = (int)Math.Floor(s / designPitch);
                int count = k + 1;
                double r = (s - k * designPitch) / 2.0;
                for (int i = 0; i < count; i++)
                {
                    offsets.Add(cover + barDia / 2.0 + r + i * designPitch);
                }
                return (count, designPitch, offsets);
            }
            else
            {
                // "Chia đều" (Equal Pitch)
                int k = (int)Math.Max(1, Math.Ceiling(s / designPitch));
                double actualPitch = s / k;
                int count = k + 1;
                for (int i = 0; i < count; i++)
                {
                    offsets.Add(cover + barDia / 2.0 + i * actualPitch);
                }
                return (count, actualPitch, offsets);
            }
        }

        // ====================================================================
        // 1. BẢN ĐÁY
        // ====================================================================
        private static void GenerateBaseRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            double baseWidthX = geom.Plan.BaseRect.Width;  // Phương X
            double baseWidthY = geom.Plan.BaseRect.Height; // Phương Y
            double c = spec.CoverBottom;
            double f = spec.BaseThickness;

            // Lớp trên: Thường có bẻ móc 2M ngàm lên tường
            double hookLenTop = Math.Max(f - 2 * c, spec.RebarHookDiaMultiple * 10);
            int dTop = spec.BaseTopRebar?.Diameter ?? 10;
            double aTop = spec.BaseTopRebar?.Spacing ?? 150;

            // Phương X (rải theo Y)
            var distTopY = DistributeBars(baseWidthY, c, dTop, aTop, spec.RebarDistributionMode);
            double lTopX = baseWidthX - 2 * c;
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản đáy - lớp trên",
                BarMark = "D1",
                Diameter = dTop,
                Shape = BarShapeType.L2M,
                Quantity = distTopY.Count,
                L = lTopX,
                LT = hookLenTop,
                LP = hookLenTop,
                LengthMm = lTopX + 2 * hookLenTop,
                SpacingMm = distTopY.ActualSpacing
            });

            // Phương Y (rải theo X)
            var distTopX = DistributeBars(baseWidthX, c, dTop, aTop, spec.RebarDistributionMode);
            double lTopY = baseWidthY - 2 * c;
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản đáy - lớp trên",
                BarMark = "D2",
                Diameter = dTop,
                Shape = BarShapeType.L2M,
                Quantity = distTopX.Count,
                L = lTopY,
                LT = hookLenTop,
                LP = hookLenTop,
                LengthMm = lTopY + 2 * hookLenTop,
                SpacingMm = distTopX.ActualSpacing
            });

            // Lớp dưới: Thường thẳng 0M
            int dBot = spec.BaseBottomRebar?.Diameter ?? 10;
            double aBot = spec.BaseBottomRebar?.Spacing ?? 150;

            // Phương X
            var distBotY = DistributeBars(baseWidthY, c, dBot, aBot, spec.RebarDistributionMode);
            double lBotX = baseWidthX - 2 * c;
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản đáy - lớp dưới",
                BarMark = "D1",
                Diameter = dBot,
                Shape = spec.HookBaseBottom == "2M" ? BarShapeType.L2M : BarShapeType.L0M,
                Quantity = distBotY.Count,
                L = lBotX,
                LT = spec.HookBaseBottom == "2M" ? hookLenTop : 0,
                LP = spec.HookBaseBottom == "2M" ? hookLenTop : 0,
                LengthMm = lBotX + (spec.HookBaseBottom == "2M" ? 2 * hookLenTop : 0),
                SpacingMm = distBotY.ActualSpacing
            });

            // Phương Y
            var distBotX = DistributeBars(baseWidthX, c, dBot, aBot, spec.RebarDistributionMode);
            double lBotY = baseWidthY - 2 * c;
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản đáy - lớp dưới",
                BarMark = "D2",
                Diameter = dBot,
                Shape = spec.HookBaseBottom == "2M" ? BarShapeType.L2M : BarShapeType.L0M,
                Quantity = distBotX.Count,
                L = lBotY,
                LT = spec.HookBaseBottom == "2M" ? hookLenTop : 0,
                LP = spec.HookBaseBottom == "2M" ? hookLenTop : 0,
                LengthMm = lBotY + (spec.HookBaseBottom == "2M" ? 2 * hookLenTop : 0),
                SpacingMm = distBotX.ActualSpacing
            });
        }

        // ====================================================================
        // 2. BỐN TƯỜNG T1, T2, T3, T4
        // ====================================================================
        private static void GenerateWallRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            GenerateSingleWallRebars(spec, geom, WallDirection.T1, spec.PipeT1, bars);
            GenerateSingleWallRebars(spec, geom, WallDirection.T2, spec.PipeT2, bars);
            GenerateSingleWallRebars(spec, geom, WallDirection.T3, spec.PipeT3, bars);
            GenerateSingleWallRebars(spec, geom, WallDirection.T4, spec.PipeT4, bars);
        }

        private static void GenerateSingleWallRebars(
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            WallDirection dir,
            PipeConnectionSpec? pipe,
            List<BarInstance> bars)
        {
            string wallName = $"Tường {dir}";
            double wallSpan = (dir == WallDirection.T1 || dir == WallDirection.T3) ? spec.L : spec.B;
            double wallHeight = spec.H;
            double c = spec.CoverWall;

            // Thanh đứng (Vertical)
            int dVert = spec.WallOuterRebar?.Diameter ?? 10;
            double aVert = spec.WallOuterRebar?.Spacing ?? 150;
            double hookVert = Math.Max(spec.BaseThickness - 2 * c, spec.RebarHookDiaMultiple * dVert);
            double lVert = wallHeight + spec.BaseThickness + (spec.Type == ManholeType.GaThamLienKhoi ? spec.TopThickness : 0) - 2 * c;

            // Thanh ngang (Horizontal)
            double aHoriz = aVert + spec.WallHorizSpacingDelta;
            int dHoriz = dVert - spec.WallHorizDiaStepDown;
            if (dHoriz < 6) dHoriz = dVert;
            double lHoriz = wallSpan + 2 * spec.WallThickness - 2 * c;
            double hookHoriz = Math.Max(spec.WallThickness - 2 * c, spec.RebarHookDiaMultiple * dHoriz);

            // Rải thanh đứng theo chiều dài tường
            var distVert = DistributeBars(wallSpan, c, dVert, aVert, spec.RebarDistributionMode);

            // Rải thanh ngang theo chiều cao tường
            var distHoriz = DistributeBars(wallHeight, c, dHoriz, aHoriz, spec.RebarDistributionMode);

            // Nếu không có cống cắt qua:
            if (pipe == null)
            {
                // Lớp ngoài
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp ngoài",
                    BarMark = "T1",
                    Diameter = dVert,
                    Shape = BarShapeType.L2M,
                    Quantity = distVert.Count,
                    L = lVert,
                    LT = hookVert,
                    LP = hookVert,
                    LengthMm = lVert + 2 * hookVert,
                    SpacingMm = distVert.ActualSpacing
                });
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp ngoài",
                    BarMark = "T2",
                    Diameter = dHoriz,
                    Shape = BarShapeType.L2M,
                    Quantity = distHoriz.Count,
                    L = lHoriz,
                    LT = hookHoriz,
                    LP = hookHoriz,
                    LengthMm = lHoriz + 2 * hookHoriz,
                    SpacingMm = distHoriz.ActualSpacing
                });

                // Lớp trong
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp trong",
                    BarMark = "T1",
                    Diameter = dVert,
                    Shape = spec.HookWallInner == "2M" ? BarShapeType.L2M : BarShapeType.L0M,
                    Quantity = distVert.Count,
                    L = lVert,
                    LT = spec.HookWallInner == "2M" ? hookVert : 0,
                    LP = spec.HookWallInner == "2M" ? hookVert : 0,
                    LengthMm = lVert + (spec.HookWallInner == "2M" ? 2 * hookVert : 0),
                    SpacingMm = distVert.ActualSpacing
                });
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp trong",
                    BarMark = "T2",
                    Diameter = dHoriz,
                    Shape = spec.HookWallInner == "2M" ? BarShapeType.L2M : BarShapeType.L0M,
                    Quantity = distHoriz.Count,
                    L = lHoriz,
                    LT = spec.HookWallInner == "2M" ? hookHoriz : 0,
                    LP = spec.HookWallInner == "2M" ? hookHoriz : 0,
                    LengthMm = lHoriz + (spec.HookWallInner == "2M" ? 2 * hookHoriz : 0),
                    SpacingMm = distHoriz.ActualSpacing
                });
            }
            else
            {
                // Có cống cắt qua: Áp dụng thuật toán cắt lỗ
                double rCut = pipe.CutoutDia / 2.0;
                double pipeZCenter = pipe.InvertLevelFromBottom + pipe.InnerDia / 2.0;
                double pipeXCenter = wallSpan / 2.0 + pipe.OffsetFromCenter;

                // Phân loại thanh đứng cắt qua lỗ cống
                int vertUncutCount = 0;
                int vertCutCount = 0;
                foreach (double x in distVert.Offsets)
                {
                    if (Math.Abs(x - pipeXCenter) < rCut)
                        vertCutCount++;
                    else
                        vertUncutCount++;
                }

                // Phân loại thanh ngang cắt qua lỗ cống
                int horizUncutCount = 0;
                int horizCutCount = 0;
                foreach (double z in distHoriz.Offsets)
                {
                    if (Math.Abs(z - pipeZCenter) < rCut)
                        horizCutCount++;
                    else
                        horizUncutCount++;
                }

                // Thêm các thanh nguyên vẹn
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp ngoài",
                    BarMark = "T1",
                    Diameter = dVert,
                    Shape = BarShapeType.L2M,
                    Quantity = Math.Max(1, vertUncutCount),
                    L = lVert,
                    LT = hookVert,
                    LP = hookVert,
                    LengthMm = lVert + 2 * hookVert,
                    SpacingMm = distVert.ActualSpacing
                });

                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp ngoài",
                    BarMark = "T2",
                    Diameter = dHoriz,
                    Shape = BarShapeType.L2M,
                    Quantity = Math.Max(1, horizUncutCount),
                    L = lHoriz,
                    LT = hookHoriz,
                    LP = hookHoriz,
                    LengthMm = lHoriz + 2 * hookHoriz,
                    SpacingMm = distHoriz.ActualSpacing
                });

                // Thêm các thanh bị cắt ngắn qua lỗ cống (Sub-bars T1A / T2A)
                if (vertCutCount > 0)
                {
                    double subLenTop = Math.Max(100, wallHeight - (pipeZCenter + rCut));
                    bars.Add(new BarInstance
                    {
                        ComponentGroup = $"{wallName} - lớp ngoài",
                        BarMark = "T1A",
                        Diameter = dVert,
                        Shape = BarShapeType.L1M,
                        Quantity = vertCutCount,
                        L = subLenTop,
                        LT = hookVert,
                        LengthMm = subLenTop + hookVert,
                        SpacingMm = distVert.ActualSpacing
                    });
                }

                if (horizCutCount > 0)
                {
                    double subLenLeft = Math.Max(100, pipeXCenter - rCut);
                    bars.Add(new BarInstance
                    {
                        ComponentGroup = $"{wallName} - lớp ngoài",
                        BarMark = "T2A",
                        Diameter = dHoriz,
                        Shape = BarShapeType.L1M,
                        Quantity = horizCutCount,
                        L = subLenLeft,
                        LT = hookHoriz,
                        LengthMm = subLenLeft + hookHoriz,
                        SpacingMm = distHoriz.ActualSpacing
                    });
                }

                // Lớp trong
                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp trong",
                    BarMark = "T1",
                    Diameter = dVert,
                    Shape = BarShapeType.L0M,
                    Quantity = Math.Max(1, vertUncutCount),
                    L = lVert,
                    LengthMm = lVert,
                    SpacingMm = distVert.ActualSpacing
                });

                bars.Add(new BarInstance
                {
                    ComponentGroup = $"{wallName} - lớp trong",
                    BarMark = "T2",
                    Diameter = dHoriz,
                    Shape = BarShapeType.L0M,
                    Quantity = Math.Max(1, horizUncutCount),
                    L = lHoriz,
                    LengthMm = lHoriz,
                    SpacingMm = distHoriz.ActualSpacing
                });
            }
        }

        // ====================================================================
        // 3. BẢN TRẦN
        // ====================================================================
        private static void GenerateTopSlabRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            double slabWidthX = spec.L + 2 * spec.WallThickness;
            double slabWidthY = spec.B + 2 * spec.WallThickness;
            double c = spec.CoverTop;
            int dTop = spec.TopTopRebar?.Diameter ?? 10;
            double aTop = spec.TopTopRebar?.Spacing ?? 150;
            double hookTop = Math.Max(spec.TopThickness - 2 * c, spec.RebarHookDiaMultiple * dTop);

            // Rải phương X và phương Y
            var distX = DistributeBars(slabWidthY, c, dTop, aTop, spec.RebarDistributionMode);
            var distY = DistributeBars(slabWidthX, c, dTop, aTop, spec.RebarDistributionMode);

            // Thanh nguyên N1, N2
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản trần - lớp trên",
                BarMark = "N1",
                Diameter = dTop,
                Shape = BarShapeType.L0M,
                Quantity = (int)(distX.Count * 0.6), // Trừ phần lỗ mở
                L = slabWidthX - 2 * c,
                LengthMm = slabWidthX - 2 * c,
                SpacingMm = distX.ActualSpacing
            });

            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản trần - lớp trên",
                BarMark = "N2",
                Diameter = dTop,
                Shape = BarShapeType.L0M,
                Quantity = (int)(distY.Count * 0.6),
                L = slabWidthY - 2 * c,
                LengthMm = slabWidthY - 2 * c,
                SpacingMm = distY.ActualSpacing
            });

            // Thanh bị cắt bởi lỗ nắp (N1A, N1B, N2A, N2B)
            int cutCountX = (int)(distX.Count * 0.4);
            int cutCountY = (int)(distY.Count * 0.4);
            if (cutCountX > 0)
            {
                double lCut = Math.Max(100, (slabWidthX - spec.NeckInnerWidth) / 2.0);
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản trần - lớp trên",
                    BarMark = "N1A",
                    Diameter = dTop,
                    Shape = BarShapeType.L1M,
                    Quantity = cutCountX,
                    L = lCut,
                    LT = hookTop,
                    LengthMm = lCut + hookTop,
                    SpacingMm = distX.ActualSpacing
                });
            }

            if (cutCountY > 0)
            {
                double lCut = Math.Max(100, (slabWidthY - spec.NeckInnerLength) / 2.0);
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản trần - lớp trên",
                    BarMark = "N2A",
                    Diameter = dTop,
                    Shape = BarShapeType.L1M,
                    Quantity = cutCountY,
                    L = lCut,
                    LT = hookTop,
                    LengthMm = lCut + hookTop,
                    SpacingMm = distY.ActualSpacing
                });
            }

            // Lớp dưới (N1, N2 với bẻ móc 2M)
            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản trần - lớp dưới",
                BarMark = "N1",
                Diameter = dTop,
                Shape = BarShapeType.L2M,
                Quantity = (int)(distX.Count * 0.6),
                L = slabWidthX - 2 * c,
                LT = hookTop,
                LP = hookTop,
                LengthMm = slabWidthX - 2 * c + 2 * hookTop,
                SpacingMm = distX.ActualSpacing
            });

            bars.Add(new BarInstance
            {
                ComponentGroup = "Bản trần - lớp dưới",
                BarMark = "N2",
                Diameter = dTop,
                Shape = BarShapeType.L2M,
                Quantity = (int)(distY.Count * 0.6),
                L = slabWidthY - 2 * c,
                LT = hookTop,
                LP = hookTop,
                LengthMm = slabWidthY - 2 * c + 2 * hookTop,
                SpacingMm = distY.ActualSpacing
            });
        }

        // ====================================================================
        // 4. CỔ GA (Khi W > 0)
        // ====================================================================
        private static void GenerateNeckRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            double w = spec.NeckHeight;
            double tW = spec.NeckWallThickness;
            double c = spec.CoverNeck;
            int d = spec.NeckOuterRebar?.Diameter ?? 10;
            double a = spec.NeckOuterRebar?.Spacing ?? 150;
            double hook = Math.Max(spec.TopThickness - 2 * c, spec.RebarHookDiaMultiple * d);

            // Chu vi cổ ga
            double perim = 2 * (spec.NeckInnerWidth + spec.NeckInnerLength + 4 * tW);
            int vertCount = (int)Math.Max(4, Math.Ceiling(perim / a));

            bars.Add(new BarInstance
            {
                ComponentGroup = "Cổ ga",
                BarMark = "C1",
                Diameter = d,
                Shape = BarShapeType.L2M,
                Quantity = vertCount,
                L = w + hook,
                LT = hook,
                LP = hook,
                LengthMm = w + 3 * hook,
                SpacingMm = a
            });

            // Thép đai cổ ga
            int horizCount = (int)Math.Max(2, Math.Ceiling(w / a));
            double lLoop = 2 * (spec.NeckInnerWidth + 2 * tW + spec.NeckInnerLength + 2 * tW) + 30 * d;
            bars.Add(new BarInstance
            {
                ComponentGroup = "Cổ ga",
                BarMark = "C2",
                Diameter = d,
                Shape = BarShapeType.GC_T,
                Quantity = horizCount,
                L = lLoop,
                LengthMm = lLoop,
                SpacingMm = a
            });
        }

        // ====================================================================
        // 5. THANG THÉP (Th)
        // ====================================================================
        private static void GenerateLadderRebars(NormalizedManholeSpec spec, List<BarInstance> bars)
        {
            // Bậc thang uốn: móc 150, sâu 250, rộng 300 mm -> L = 150 + 250 + 300 + 250 + 150 = 1100 mm
            double ladderHeight = spec.H + spec.NeckHeight - spec.LadderFirstStep;
            int count = (int)Math.Max(1, Math.Floor(ladderHeight / spec.LadderPitch) + 1);

            double lOne = 2 * spec.LadderHook + 2 * spec.LadderDepth + spec.LadderWidth;

            bars.Add(new BarInstance
            {
                ComponentGroup = "Thang thép",
                BarMark = spec.LadderPrefix,
                Diameter = spec.LadderDia,
                Shape = BarShapeType.M_U,
                Quantity = count,
                L = spec.LadderWidth,
                L1T = spec.LadderHook,
                L2T = spec.LadderDepth,
                L3 = spec.LadderWidth,
                L1P = spec.LadderHook,
                L2P = spec.LadderDepth,
                LengthMm = lOne,
                SpacingMm = spec.LadderPitch,
                Note = $"{count} bậc thang D{spec.LadderDia}"
            });
        }

        // ====================================================================
        // 6. THÉP ĐAI BUỘC LIÊN KẾT (BLTC)
        // ====================================================================
        private static void GenerateTieRebars(NormalizedManholeSpec spec, List<BarInstance> bars)
        {
            int dTie = spec.TieSpec?.Diameter ?? 6;
            double aTie = spec.TieSpec?.Spacing ?? 300;

            // Thép đai chữ C tường: dày tường t - 2c + 2 móc C (50mm)
            double lTieWall = (spec.WallThickness - 2 * spec.CoverWall) + 2 * spec.TieHookC;
            int tieWallCount = (int)Math.Max(4, (spec.L * spec.H) / (aTie * aTie));

            bars.Add(new BarInstance
            {
                ComponentGroup = "Thép đai liên kết",
                BarMark = "T3",
                Diameter = dTie,
                Shape = BarShapeType.M_C,
                Quantity = tieWallCount,
                L = spec.WallThickness - 2 * spec.CoverWall,
                LT = spec.TieHookC,
                LP = spec.TieHookC,
                LengthMm = lTieWall,
                SpacingMm = aTie
            });

            // Thép đai chữ U đáy
            double lTieBase = (spec.BaseThickness - 2 * spec.CoverBottom) + 2 * spec.TieHookZU;
            int tieBaseCount = (int)Math.Max(4, (spec.L * spec.B) / (aTie * aTie));

            bars.Add(new BarInstance
            {
                ComponentGroup = "Thép đai liên kết",
                BarMark = "D3",
                Diameter = dTie,
                Shape = BarShapeType.M_U,
                Quantity = tieBaseCount,
                L = spec.BaseThickness - 2 * spec.CoverBottom,
                LT = spec.TieHookZU,
                LP = spec.TieHookZU,
                LengthMm = lTieBase,
                SpacingMm = aTie
            });
        }

        // ====================================================================
        // 7. GIA CƯỜNG LỖ CỐNG (GC)
        // ====================================================================
        private static void GeneratePipeReinforcement(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            foreach (var pipe in spec.AllPipes)
            {
                int dia = spec.PipeReinforceDia;
                int count = spec.PipeReinforceCount;
                double exp = spec.PipeReinforceExpansion;

                // Khung gia cường vuông / tròn ôm quanh cống
                double dCut = pipe.CutoutDia;
                double side = dCut + 2 * exp;
                double lFrame = 4 * side + 30 * dia;

                bars.Add(new BarInstance
                {
                    ComponentGroup = "Gia cường lỗ cống",
                    BarMark = $"{spec.PipeReinforcePrefix}{pipe.Direction}",
                    Diameter = dia,
                    Shape = BarShapeType.GC_T,
                    Quantity = count,
                    L = side,
                    LengthMm = lFrame,
                    Note = $"Gia cường cống {pipe.Direction} ({pipe.PipeCode})"
                });
            }
        }

        // ====================================================================
        // 8. GIA CƯỜNG LỖ NẮP TRẦN (GC)
        // ====================================================================
        private static void GenerateTopHoleReinforcement(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            int dia = spec.TopHoleReinforceDia;
            int count = spec.TopHoleReinforceCount;
            double exp = spec.TopHoleReinforceExpansion;

            double sideX = spec.NeckInnerWidth + 2 * exp;
            double sideY = spec.NeckInnerLength + 2 * exp;
            double lFrame = 2 * (sideX + sideY) + 30 * dia;

            bars.Add(new BarInstance
            {
                ComponentGroup = "Gia cường lỗ thăm",
                BarMark = $"{spec.TopHoleReinforcePrefix}_Trần",
                Diameter = dia,
                Shape = BarShapeType.GC_T,
                Quantity = count,
                L = sideX,
                LT = sideY,
                LengthMm = lFrame,
                Note = "Gia cường quanh lỗ nắp trần"
            });
        }

        // ====================================================================
        // 9. THÉP HÌNH GÓC MIỆNG NẮP GA
        // ====================================================================
        private static void GenerateAngleSteel(
            NormalizedManholeSpec spec, Dictionary<string, SteelAngleRecord> angleCatalog, List<BarInstance> bars)
        {
            if (string.IsNullOrEmpty(spec.NeckAngleSteel) ||
                spec.NeckAngleSteel.Equals("Không", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            double perimMm = 2 * (spec.NeckInnerWidth + spec.NeckInnerLength);
            bars.Add(new BarInstance
            {
                ComponentGroup = "Thép hình góc",
                BarMark = "GN1",
                AngleCode = spec.NeckAngleSteel,
                Shape = BarShapeType.VG,
                Quantity = 1,
                LengthMm = perimMm,
                Note = $"Viền thép góc {spec.NeckAngleSteel} miệng ga"
            });
        }

        // ====================================================================
        // 10. TẤM NẮP BTCT ĐÚC SẴN (Khi có)
        // ====================================================================
        private static void GeneratePrecastCoverRebars(
            NormalizedManholeSpec spec, Dictionary<string, SteelAngleRecord> angleCatalog, List<BarInstance> bars)
        {
            // Tấm nắp BTCT kích thước nắp
            int dBot = 10;
            int dTop = 8;
            double lCoverX = spec.NeckInnerWidth;
            double lCoverY = spec.NeckInnerLength;
            int count = spec.CoverPanelCount;

            bars.Add(new BarInstance
            {
                ComponentGroup = "Tấm nắp BTCT",
                BarMark = "1",
                Diameter = dBot,
                Shape = BarShapeType.L0M,
                Quantity = 10 * count,
                L = lCoverX - 30,
                LengthMm = lCoverX - 30
            });

            bars.Add(new BarInstance
            {
                ComponentGroup = "Tấm nắp BTCT",
                BarMark = "2",
                Diameter = dTop,
                Shape = BarShapeType.L0M,
                Quantity = 10 * count,
                L = lCoverY - 30,
                LengthMm = lCoverY - 30
            });
        }

        // ====================================================================
        // 11. CỬA THU NƯỚC (Loại 3)
        // ====================================================================
        private static void GenerateCatchBasinRebars(NormalizedManholeSpec spec, List<BarInstance> bars)
        {
            int dia = spec.CatchBasinRebar?.Diameter ?? 10;
            double pitch = spec.CatchBasinRebar?.Spacing ?? 150;
            double lChamber = spec.CatchBasinChamberLength;
            double hChamber = spec.CatchBasinChamberHeight;

            int count = (int)Math.Max(2, Math.Ceiling(lChamber / pitch));
            double lBar = lChamber + hChamber;

            bars.Add(new BarInstance
            {
                ComponentGroup = "Cửa thu nước",
                BarMark = "CT1",
                Diameter = dia,
                Shape = BarShapeType.L2M,
                Quantity = count * 2,
                L = lBar,
                LT = 100,
                LP = 100,
                LengthMm = lBar + 200,
                SpacingMm = pitch
            });
        }
    }
}
