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

    public enum WallBarAxis
    {
        Vertical,
        Horizontal
    }

    /// <summary>
    /// Một nhánh thanh thép thực trên mặt tường. PositionMm là X cục bộ đối
    /// với thanh đứng và cao độ Z đối với thanh ngang. StartMm/EndMm nằm trên
    /// chính trục thanh; dữ liệu này được dùng chung cho CAD và thống kê.
    /// </summary>
    public sealed record WallRebarSegment(
        WallDirection Direction,
        bool IsOuterLayer,
        WallBarAxis Axis,
        string BarMark,
        int Diameter,
        double PositionMm,
        double StartMm,
        double EndMm,
        double SpacingMm,
        BarShapeType Shape,
        double HookAtStartMm,
        double HookAtEndMm)
    {
        public double LengthMm => Math.Max(0, EndMm - StartMm) + HookAtStartMm + HookAtEndMm;
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
                GenerateNeckRebars(spec, bars);
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
            if (!double.IsFinite(spanLength) || spanLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(spanLength), "Chiều dài phân bố phải hữu hạn và lớn hơn 0.");
            if (!double.IsFinite(cover) || cover < 0)
                throw new ArgumentOutOfRangeException(nameof(cover), "Lớp bảo vệ phải hữu hạn và không âm.");
            if (barDia <= 0)
                throw new ArgumentOutOfRangeException(nameof(barDia), "Đường kính thanh thép phải lớn hơn 0.");
            if (!double.IsFinite(designPitch) || designPitch <= 0)
                throw new ArgumentOutOfRangeException(nameof(designPitch), "Bước thép thiết kế phải hữu hạn và lớn hơn 0.");

            string distributionMode = (mode ?? string.Empty).Trim();
            bool fixedPitch = distributionMode.Equals("Theo mật độ", StringComparison.OrdinalIgnoreCase);
            bool equalPitch = distributionMode.Equals("Chia đều", StringComparison.OrdinalIgnoreCase);
            if (!fixedPitch && !equalPitch)
                throw new ArgumentException($"Chế độ rải thép không được hỗ trợ: '{mode}'.", nameof(mode));

            double edgeClearance = cover + barDia / 2.0;
            double s = spanLength - 2.0 * edgeClearance;
            var offsets = new List<double>();

            const double spanTolerance = 1e-8;
            if (s < -spanTolerance)
                throw new ArgumentOutOfRangeException(nameof(spanLength),
                    $"Không đủ chiều dài để bố trí thép: span={spanLength:F3} mm, cần tối thiểu {2 * edgeClearance:F3} mm.");

            if (s <= spanTolerance)
            {
                offsets.Add(spanLength / 2.0);
                return (1, 0, offsets);
            }

            if (fixedPitch)
            {
                int k = (int)Math.Floor(s / designPitch);
                int count = k + 1;
                double r = (s - k * designPitch) / 2.0;
                for (int i = 0; i < count; i++)
                {
                    offsets.Add(edgeClearance + r + i * designPitch);
                }
                return (count, designPitch, offsets);
            }
            else
            {
                // "Chia đều" (Equal Pitch)
                int k = (int)Math.Ceiling(s / designPitch);
                double actualPitch = s / k;
                int count = k + 1;
                for (int i = 0; i < count; i++)
                {
                    offsets.Add(edgeClearance + i * actualPitch);
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

            // Lớp trên: giữ nguyên quy cách đầu vào, không suy đoán đường kính/bước.
            if (spec.BaseTopRebar is { } topRebar)
            {
                ValidateRebarSpec(topRebar, "thép lớp trên bản đáy");
                int dTop = topRebar.Diameter;
                double aTop = topRebar.Spacing;
                double hookLenTop = CalculateHookLength(spec, f, c, dTop);
                var topHooks = ResolveHooks(spec.HookBaseTop, hookLenTop, hookLenTop);

                var distTopY = DistributeBars(baseWidthY, c, dTop, aTop, spec.RebarDistributionMode);
                double lTopX = baseWidthX - 2 * c;
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản đáy - lớp trên",
                    BarMark = "D1",
                    Diameter = dTop,
                    Shape = topHooks.Shape,
                    Quantity = distTopY.Count,
                    L = lTopX,
                    LT = topHooks.AtStartMm,
                    LP = topHooks.AtEndMm,
                    LengthMm = lTopX + topHooks.TotalMm,
                    SpacingMm = distTopY.ActualSpacing
                });

                var distTopX = DistributeBars(baseWidthX, c, dTop, aTop, spec.RebarDistributionMode);
                double lTopY = baseWidthY - 2 * c;
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản đáy - lớp trên",
                    BarMark = "D2",
                    Diameter = dTop,
                    Shape = topHooks.Shape,
                    Quantity = distTopX.Count,
                    L = lTopY,
                    LT = topHooks.AtStartMm,
                    LP = topHooks.AtEndMm,
                    LengthMm = lTopY + topHooks.TotalMm,
                    SpacingMm = distTopX.ActualSpacing
                });
            }

            // Lớp dưới: hình dạng móc cũng lấy từ cấu hình của lớp dưới.
            if (spec.BaseBottomRebar is { } bottomRebar)
            {
                ValidateRebarSpec(bottomRebar, "thép lớp dưới bản đáy");
                int dBot = bottomRebar.Diameter;
                double aBot = bottomRebar.Spacing;
                double hookLenBottom = CalculateHookLength(spec, f, c, dBot);
                var bottomHooks = ResolveHooks(spec.HookBaseBottom, hookLenBottom, hookLenBottom);

                var distBotY = DistributeBars(baseWidthY, c, dBot, aBot, spec.RebarDistributionMode);
                double lBotX = baseWidthX - 2 * c;
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản đáy - lớp dưới",
                    BarMark = "D1",
                    Diameter = dBot,
                    Shape = bottomHooks.Shape,
                    Quantity = distBotY.Count,
                    L = lBotX,
                    LT = bottomHooks.AtStartMm,
                    LP = bottomHooks.AtEndMm,
                    LengthMm = lBotX + bottomHooks.TotalMm,
                    SpacingMm = distBotY.ActualSpacing
                });

                var distBotX = DistributeBars(baseWidthX, c, dBot, aBot, spec.RebarDistributionMode);
                double lBotY = baseWidthY - 2 * c;
                bars.Add(new BarInstance
                {
                    ComponentGroup = "Bản đáy - lớp dưới",
                    BarMark = "D2",
                    Diameter = dBot,
                    Shape = bottomHooks.Shape,
                    Quantity = distBotX.Count,
                    L = lBotY,
                    LT = bottomHooks.AtStartMm,
                    LP = bottomHooks.AtEndMm,
                    LengthMm = lBotY + bottomHooks.TotalMm,
                    SpacingMm = distBotX.ActualSpacing
                });
            }
        }

        private static void ValidateRebarSpec(RebarSpec rebar, string description)
        {
            if (rebar.Diameter <= 0 || !double.IsFinite(rebar.Spacing) || rebar.Spacing <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rebar),
                    $"Quy cách {description} phải có đường kính và bước dương.");
            }
        }

        // ====================================================================
        // 2. BỐN TƯỜNG T1, T2, T3, T4
        // ====================================================================
        private static void GenerateWallRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            AddWallScheduleBars(spec, WallDirection.T1, spec.PipeT1, bars);
            AddWallScheduleBars(spec, WallDirection.T2, spec.PipeT2, bars);
            AddWallScheduleBars(spec, WallDirection.T3, spec.PipeT3, bars);
            AddWallScheduleBars(spec, WallDirection.T4, spec.PipeT4, bars);
        }

        public static IReadOnlyList<WallRebarSegment> BuildWallRebarLayout(
            NormalizedManholeSpec spec,
            WallDirection dir,
            PipeConnectionSpec? pipe)
        {
            ArgumentNullException.ThrowIfNull(spec);
            double wallSpan = (dir == WallDirection.T1 || dir == WallDirection.T3) ? spec.L : spec.B;
            var segments = new List<WallRebarSegment>();
            AddWallFaceSegments(spec, dir, wallSpan, pipe, true, spec.WallOuterRebar,
                spec.HookWallOuter, segments);
            AddWallFaceSegments(spec, dir, wallSpan, pipe, false, spec.WallInnerRebar,
                spec.HookWallInner, segments);
            return segments;
        }

        private static void AddWallFaceSegments(
            NormalizedManholeSpec spec,
            WallDirection dir,
            double wallSpan,
            PipeConnectionSpec? pipe,
            bool isOuter,
            RebarSpec? rebar,
            string hookMode,
            List<WallRebarSegment> output)
        {
            if (rebar == null) return;
            if (rebar.Diameter <= 0 || rebar.Spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(rebar), "Quy cách thép tường phải có đường kính và bước dương.");

            double cover = spec.CoverWall;
            int verticalDia = rebar.Diameter;
            int horizontalDia = ReduceDiameterByGrade(verticalDia, spec.WallHorizDiaStepDown);
            double verticalPitch = rebar.Spacing;
            double horizontalPitch = verticalPitch + spec.WallHorizSpacingDelta;
            var verticalDistribution = DistributeBars(wallSpan, cover, verticalDia, verticalPitch, spec.RebarDistributionMode);
            var horizontalDistribution = DistributeBars(spec.H, cover, horizontalDia, horizontalPitch, spec.RebarDistributionMode);

            double verticalStart = -spec.BaseThickness + spec.CoverBottom;
            double verticalEnd = spec.H + (spec.Type == ManholeType.GaThamLienKhoi
                ? spec.TopThickness - spec.CoverTop
                : -cover);
            double verticalHookStart = CalculateHookLength(spec, spec.BaseThickness, spec.CoverBottom, verticalDia);
            double verticalHookEnd = CalculateHookLength(
                spec,
                spec.Type == ManholeType.GaThamLienKhoi ? spec.TopThickness : spec.WallThickness,
                spec.Type == ManholeType.GaThamLienKhoi ? spec.CoverTop : cover,
                verticalDia);

            double halfSpan = wallSpan / 2.0;
            double horizontalStart = -halfSpan - spec.WallThickness + cover;
            double horizontalEnd = halfSpan + spec.WallThickness - cover;
            double horizontalHook = CalculateHookLength(spec, spec.WallThickness, cover, horizontalDia);
            double circleX = pipe == null ? 0 : WallViewTransform.ToLocalOffset(dir, pipe.OffsetFromCenter);

            foreach (double offset in verticalDistribution.Offsets)
            {
                double x = -halfSpan + offset;
                var retained = ClipWallBarAtOpening(
                    pipe, WallBarAxis.Vertical, verticalStart, verticalEnd, x,
                    circleX, spec.CoverPipe + verticalDia / 2.0);
                AddRetainedWallSegments(output, dir, isOuter, WallBarAxis.Vertical,
                    "T1", verticalDia, x, verticalStart, verticalEnd,
                    verticalDistribution.ActualSpacing, hookMode,
                    verticalHookStart, verticalHookEnd, retained);
            }

            foreach (double offset in horizontalDistribution.Offsets)
            {
                double z = offset;
                var retained = ClipWallBarAtOpening(
                    pipe, WallBarAxis.Horizontal, horizontalStart, horizontalEnd, z,
                    circleX, spec.CoverPipe + horizontalDia / 2.0);
                AddRetainedWallSegments(output, dir, isOuter, WallBarAxis.Horizontal,
                    "T2", horizontalDia, z, horizontalStart, horizontalEnd,
                    horizontalDistribution.ActualSpacing, hookMode,
                    horizontalHook, horizontalHook, retained);
            }
        }

        private static IReadOnlyList<Interval1D> ClipWallBarAtOpening(
            PipeConnectionSpec? pipe,
            WallBarAxis axis,
            double intervalStart,
            double intervalEnd,
            double fixedCoordinate,
            double centerX,
            double clearance)
        {
            if (pipe is null)
                return new[] { new Interval1D(intervalStart, intervalEnd) };

            if (pipe.IsBox)
            {
                var opening = new Rect2D(
                    centerX - pipe.CutoutWidth / 2.0,
                    pipe.CutoutBottomZ,
                    centerX + pipe.CutoutWidth / 2.0,
                    pipe.CutoutTopZ);
                return axis == WallBarAxis.Vertical
                    ? RectangleOpeningClipper.SubtractFromVertical(
                        intervalStart, intervalEnd, fixedCoordinate, opening, clearance)
                    : RectangleOpeningClipper.SubtractFromHorizontal(
                        intervalStart, intervalEnd, fixedCoordinate, opening, clearance);
            }

            double centerZ = pipe.InvertLevelFromBottom + pipe.InnerHeight / 2.0;
            double exclusionRadius = pipe.CutoutDia / 2.0 + clearance;
            return axis == WallBarAxis.Vertical
                ? CircleOpeningClipper.SubtractCircleFromVertical(
                    intervalStart, intervalEnd, fixedCoordinate, centerX, centerZ, exclusionRadius)
                : CircleOpeningClipper.SubtractCircleFromHorizontal(
                    intervalStart, intervalEnd, fixedCoordinate, centerX, centerZ, exclusionRadius);
        }

        private static void AddRetainedWallSegments(
            List<WallRebarSegment> output,
            WallDirection direction,
            bool isOuter,
            WallBarAxis axis,
            string familyMark,
            int diameter,
            double position,
            double fullStart,
            double fullEnd,
            double spacing,
            string hookMode,
            double hookAtStart,
            double hookAtEnd,
            IReadOnlyList<Interval1D> retained)
        {
            const double endpointTolerance = 1e-7;
            string mode = (hookMode ?? "").Trim().ToUpperInvariant();
            foreach (var interval in retained)
            {
                if (interval.End - interval.Start <= 1e-9) continue;

                bool retainsStart = Math.Abs(interval.Start - fullStart) <= endpointTolerance;
                bool retainsEnd = Math.Abs(interval.End - fullEnd) <= endpointTolerance;
                var hooks = ResolveHooks(mode, hookAtStart, hookAtEnd, retainsStart, retainsEnd);

                bool full = retainsStart && retainsEnd;
                string mark = full
                    ? familyMark
                    : retainsStart ? familyMark + "A"
                    : retainsEnd ? familyMark + "B"
                    : familyMark + "C";

                output.Add(new WallRebarSegment(
                    direction,
                    isOuter,
                    axis,
                    mark,
                    diameter,
                    position,
                    interval.Start,
                    interval.End,
                    spacing,
                    hooks.Shape,
                    hooks.AtStartMm,
                    hooks.AtEndMm));
            }
        }

        private static int ReduceDiameterByGrade(int diameter, int steps)
        {
            if (steps < 0)
                throw new ArgumentOutOfRangeException(nameof(steps), "Số cấp giảm đường kính không được âm.");
            if (steps == 0) return diameter;

            // Dải cấp thép thông dụng của bộ mẫu; HDSD trang 9 xác nhận ví dụ
            // một cấp giảm đưa D12 xuống D10 (không trừ trực tiếp 1 mm).
            int[] grades = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32 };
            int index = Array.IndexOf(grades, diameter);
            if (index < 0 || index - steps < 0)
                throw new ArgumentOutOfRangeException(nameof(diameter),
                    $"Không thể giảm {steps} cấp từ đường kính D{diameter} trong dải cấp thép được hỗ trợ.");
            return grades[index - steps];
        }

        private static void AddWallScheduleBars(
            NormalizedManholeSpec spec,
            WallDirection direction,
            PipeConnectionSpec? pipe,
            List<BarInstance> bars)
        {
            foreach (var segment in BuildWallRebarLayout(spec, direction, pipe))
            {
                string face = segment.IsOuterLayer ? "lớp ngoài" : "lớp trong";
                string component = $"Tường {direction} - {face}";
                var candidate = new BarInstance
                {
                    ComponentGroup = component,
                    BarMark = segment.BarMark,
                    Diameter = segment.Diameter,
                    Shape = segment.Shape,
                    Quantity = 1,
                    L = segment.EndMm - segment.StartMm,
                    LT = segment.HookAtStartMm,
                    LP = segment.HookAtEndMm,
                    LengthMm = segment.LengthMm,
                    SpacingMm = segment.SpacingMm
                };

                int existingIndex = bars.FindIndex(existing =>
                    existing.ComponentGroup == candidate.ComponentGroup &&
                    existing.BarMark == candidate.BarMark &&
                    existing.Diameter == candidate.Diameter &&
                    existing.Shape == candidate.Shape &&
                    Math.Abs(existing.LengthMm - candidate.LengthMm) < 0.5 &&
                    Math.Abs(existing.SpacingMm - candidate.SpacingMm) < 0.5 &&
                    Math.Abs(existing.LT - candidate.LT) < 0.5 &&
                    Math.Abs(existing.LP - candidate.LP) < 0.5);
                if (existingIndex < 0)
                {
                    bars.Add(candidate);
                }
                else
                {
                    var existing = bars[existingIndex];
                    bars[existingIndex] = new BarInstance
                    {
                        ComponentGroup = existing.ComponentGroup,
                        BarMark = existing.BarMark,
                        Diameter = existing.Diameter,
                        Shape = existing.Shape,
                        Quantity = existing.Quantity + 1,
                        L = existing.L,
                        LT = existing.LT,
                        LP = existing.LP,
                        LengthMm = existing.LengthMm,
                        SpacingMm = existing.SpacingMm
                    };
                }
            }
        }

        // ====================================================================
        // 3. BẢN TRẦN
        // ====================================================================
        private static void GenerateTopSlabRebars(
            NormalizedManholeSpec spec, ManholeGeometryModel geom, List<BarInstance> bars)
        {
            var slab = geom.Plan.OuterWallRect;
            Rect2D opening = geom.Plan.NeckOpeningRect;
            GenerateTopSlabLayer(spec, slab, opening, spec.TopTopRebar, "Bản trần - lớp trên", spec.HookTopTop, bars);
            GenerateTopSlabLayer(spec, slab, opening, spec.TopBottomRebar, "Bản trần - lớp dưới", spec.HookTopBottom, bars);
        }

        private static void GenerateTopSlabLayer(
            NormalizedManholeSpec spec,
            Rect2D slab,
            Rect2D opening,
            RebarSpec? rebarSpec,
            string componentGroup,
            string hookMode,
            List<BarInstance> bars)
        {
            if (rebarSpec == null || rebarSpec.Diameter <= 0 || rebarSpec.Spacing <= 0)
                return;

            int diameter = rebarSpec.Diameter;
            double clearance = spec.CoverTop + diameter / 2.0;
            double startX = slab.MinX + clearance;
            double endX = slab.MaxX - clearance;
            double startY = slab.MinY + clearance;
            double endY = slab.MaxY - clearance;
            double hookLength = CalculateHookLength(spec, spec.TopThickness, spec.CoverTop, diameter);
            var roofBars = new List<BarInstance>();
            var distY = DistributeBars(slab.Height, spec.CoverTop, diameter, rebarSpec.Spacing, spec.RebarDistributionMode);
            foreach (double offset in distY.Offsets)
            {
                double y = slab.MinY + offset;
                var segments = RectangleOpeningClipper.SubtractFromHorizontal(startX, endX, y, opening, clearance);
                AddRoofSegments(roofBars, componentGroup, "N1", "N1A", "N1B", diameter,
                    hookMode, hookLength, distY.ActualSpacing, startX, endX, opening.MinX, opening.MaxX, segments);
            }

            var distX = DistributeBars(slab.Width, spec.CoverTop, diameter, rebarSpec.Spacing, spec.RebarDistributionMode);
            foreach (double offset in distX.Offsets)
            {
                double x = slab.MinX + offset;
                var segments = RectangleOpeningClipper.SubtractFromVertical(startY, endY, x, opening, clearance);
                AddRoofSegments(roofBars, componentGroup, "N2", "N2A", "N2B", diameter,
                    hookMode, hookLength, distX.ActualSpacing, startY, endY, opening.MinY, opening.MaxY, segments);
            }

            bars.AddRange(roofBars);
        }

        private static void AddRoofSegments(
            List<BarInstance> roofBars,
            string componentGroup,
            string fullMark,
            string lowSideMark,
            string highSideMark,
            int diameter,
            string hookMode,
            double hookLength,
            double spacing,
            double fullStart,
            double fullEnd,
            double openingLow,
            double openingHigh,
            IReadOnlyList<Interval1D> segments)
        {
            foreach (var segment in segments)
            {
                if (segment.Length <= 1.0)
                    continue;

                bool full = Math.Abs(segment.Start - fullStart) <= 1.0 && Math.Abs(segment.End - fullEnd) <= 1.0;
                string mark = full
                    ? fullMark
                    : segment.End <= openingLow ? lowSideMark
                    : segment.Start >= openingHigh ? highSideMark
                    : (segment.Start + segment.End) / 2.0 < (openingLow + openingHigh) / 2.0 ? lowSideMark : highSideMark;

                bool retainsStart = Math.Abs(segment.Start - fullStart) <= 1.0;
                bool retainsEnd = Math.Abs(segment.End - fullEnd) <= 1.0;
                var hooks = ResolveHooks(hookMode, hookLength, hookLength, retainsStart, retainsEnd);
                var candidate = new BarInstance
                {
                    ComponentGroup = componentGroup,
                    BarMark = mark,
                    Diameter = diameter,
                    Shape = hooks.Shape,
                    Quantity = 1,
                    L = segment.Length,
                    LT = hooks.AtStartMm,
                    LP = hooks.AtEndMm,
                    LengthMm = segment.Length + hooks.TotalMm,
                    SpacingMm = spacing
                };

                int existingIndex = roofBars.FindIndex(existing =>
                    existing.ComponentGroup == candidate.ComponentGroup && existing.BarMark == candidate.BarMark &&
                    existing.Diameter == candidate.Diameter && existing.Shape == candidate.Shape &&
                    Math.Abs(existing.LengthMm - candidate.LengthMm) < 0.5 &&
                    Math.Abs(existing.SpacingMm - candidate.SpacingMm) < 0.5 &&
                    Math.Abs(existing.LT - candidate.LT) < 0.5 &&
                    Math.Abs(existing.LP - candidate.LP) < 0.5);
                if (existingIndex < 0)
                {
                    roofBars.Add(candidate);
                }
                else
                {
                    var existing = roofBars[existingIndex];
                    roofBars[existingIndex] = new BarInstance
                    {
                        ComponentGroup = existing.ComponentGroup,
                        BarMark = existing.BarMark,
                        Diameter = existing.Diameter,
                        Shape = existing.Shape,
                        Quantity = existing.Quantity + 1,
                        L = existing.L,
                        LT = existing.LT,
                        LP = existing.LP,
                        LengthMm = existing.LengthMm,
                        SpacingMm = existing.SpacingMm
                    };
                }
            }
        }

        private readonly record struct HookGeometry(BarShapeType Shape, double AtStartMm, double AtEndMm)
        {
            public double TotalMm => AtStartMm + AtEndMm;
        }

        private static HookGeometry ResolveHooks(
            string? mode,
            double startHookLength,
            double endHookLength,
            bool retainsStart = true,
            bool retainsEnd = true)
        {
            string normalizedMode = (mode ?? string.Empty).Trim().ToUpperInvariant();
            bool hasStartHook = (normalizedMode is "1M" or "2M") && retainsStart;
            bool hasEndHook = normalizedMode == "2M" && retainsEnd;
            int count = (hasStartHook ? 1 : 0) + (hasEndHook ? 1 : 0);
            return new HookGeometry(
                count switch
                {
                    2 => BarShapeType.L2M,
                    1 => BarShapeType.L1M,
                    _ => BarShapeType.L0M
                },
                hasStartHook ? startHookLength : 0,
                hasEndHook ? endHookLength : 0);
        }

        private static double CalculateHookLength(
            NormalizedManholeSpec spec,
            double memberThickness,
            double cover,
            int diameter)
        {
            if (spec.RebarHookMode.Contains("ĐK", StringComparison.OrdinalIgnoreCase) ||
                spec.RebarHookMode.Contains("DK", StringComparison.OrdinalIgnoreCase))
                return spec.RebarHookDiaMultiple * diameter;
            return Math.Max(0, memberThickness - 2 * cover);
        }

        // ====================================================================
        // 4. CỔ GA (Khi W > 0)
        // ====================================================================
        private static void GenerateNeckRebars(
            NormalizedManholeSpec spec, List<BarInstance> bars)
        {
            AddNeckLayerBars(spec, spec.NeckOuterRebar, isOuter: true, bars);
            AddNeckLayerBars(spec, spec.NeckInnerRebar, isOuter: false, bars);
        }

        private static void AddNeckLayerBars(
            NormalizedManholeSpec spec,
            RebarSpec? rebar,
            bool isOuter,
            List<BarInstance> bars)
        {
            if (rebar is null) return;
            if (rebar.Diameter <= 0 || !double.IsFinite(rebar.Spacing) || rebar.Spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(rebar), "Quy cách thép cổ ga phải có đường kính và bước dương.");
            if (spec.NeckInnerWidth <= 0 || spec.NeckInnerLength <= 0 || spec.NeckHeight <= 0 || spec.NeckWallThickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(spec), "Kích thước cổ ga phải dương khi có quy cách cốt thép cổ ga.");

            string componentGroup = isOuter ? "Cổ ga - lớp ngoài" : "Cổ ga - lớp trong";
            string hookMode = isOuter ? spec.HookNeckOuter : spec.HookNeckInner;
            double faceOffset = isOuter
                ? spec.NeckWallThickness - spec.CoverNeck
                : spec.CoverNeck;
            double barWidth = spec.NeckInnerWidth + 2 * faceOffset;
            double barLength = spec.NeckInnerLength + 2 * faceOffset;
            double perimeter = 2 * (barWidth + barLength);

            var verticalDistribution = DistributeBars(
                perimeter, 0, rebar.Diameter, rebar.Spacing, spec.RebarDistributionMode);
            double hookLength = CalculateHookLength(
                spec, spec.NeckWallThickness, spec.CoverNeck, rebar.Diameter);
            var hooks = ResolveHooks(hookMode, hookLength, hookLength);
            bars.Add(new BarInstance
            {
                ComponentGroup = componentGroup,
                BarMark = "C1",
                Diameter = rebar.Diameter,
                Shape = hooks.Shape,
                Quantity = Math.Max(4, verticalDistribution.Count),
                L = spec.NeckHeight,
                LT = hooks.AtStartMm,
                LP = hooks.AtEndMm,
                LengthMm = spec.NeckHeight + hooks.TotalMm,
                SpacingMm = verticalDistribution.ActualSpacing
            });

            var hoopDistribution = DistributeBars(
                spec.NeckHeight, spec.CoverNeck, rebar.Diameter, rebar.Spacing, spec.RebarDistributionMode);
            double hoopLength = perimeter + 30 * rebar.Diameter;
            bars.Add(new BarInstance
            {
                ComponentGroup = componentGroup,
                BarMark = "C2",
                Diameter = rebar.Diameter,
                Shape = BarShapeType.GC_T,
                Quantity = Math.Max(2, hoopDistribution.Count),
                L = hoopLength,
                LengthMm = hoopLength,
                SpacingMm = hoopDistribution.ActualSpacing
            });
        }

        // ====================================================================
        // 5. THANG THÉP (Th)
        // ====================================================================
        private static void GenerateLadderRebars(NormalizedManholeSpec spec, List<BarInstance> bars)
        {
            if (string.Equals(spec.LadderLocation?.Trim(), "Không", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(spec.LadderLocation?.Trim(), "Khong", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Bậc thang uốn: móc 150, sâu 250, rộng 300 mm -> L = 150 + 250 + 300 + 250 + 150 = 1100 mm
            double ladderHeight = spec.H + spec.NeckHeight - spec.LadderFirstStep;
            if (ladderHeight < 0)
            {
                return;
            }

            int count = (int)Math.Floor(ladderHeight / spec.LadderPitch) + 1;

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
            if (spec.TieSpec is not { } tieSpec)
            {
                return;
            }

            ValidateRebarSpec(tieSpec, "thép buộc");
            int dTie = tieSpec.Diameter;
            double aTie = tieSpec.Spacing;
            BarShapeType wallShape = ResolveTieShape(spec.TieShapeWall, "tường");
            BarShapeType baseShape = ResolveTieShape(spec.TieShapeBase, "bản đáy");

            // Thép buộc tường đi qua chiều dày tường giữa hai lớp thép.
            double tieWallSpan = spec.WallThickness - 2 * spec.CoverWall;
            var wallDims = CalculateTieBarDimensions(wallShape, tieWallSpan, spec);
            int tieWallCount = (int)Math.Max(4, (spec.L * spec.H) / (aTie * aTie));

            bars.Add(new BarInstance
            {
                ComponentGroup = "Thép đai liên kết",
                BarMark = "T3",
                Diameter = dTie,
                Shape = wallShape,
                Quantity = tieWallCount,
                L = wallDims.L,
                LT = wallDims.LT,
                LP = wallDims.LP,
                L3 = wallDims.L3,
                LengthMm = wallDims.LengthMm,
                SpacingMm = aTie
            });

            // Thép buộc bản đáy nối hai lớp lưới trên và dưới.
            double tieBaseSpan = spec.BaseThickness - 2 * spec.CoverBottom;
            var baseDims = CalculateTieBarDimensions(baseShape, tieBaseSpan, spec);
            int tieBaseCount = (int)Math.Max(4, (spec.L * spec.B) / (aTie * aTie));

            bars.Add(new BarInstance
            {
                ComponentGroup = "Thép đai liên kết",
                BarMark = "D3",
                Diameter = dTie,
                Shape = baseShape,
                Quantity = tieBaseCount,
                L = baseDims.L,
                LT = baseDims.LT,
                LP = baseDims.LP,
                L3 = baseDims.L3,
                LengthMm = baseDims.LengthMm,
                SpacingMm = aTie
            });
        }

        private static BarShapeType ResolveTieShape(string? configuredShape, string component)
        {
            string shape = configuredShape?.Trim() ?? string.Empty;
            if (shape.Equals("Chữ C", StringComparison.OrdinalIgnoreCase) || shape.Equals("C", StringComparison.OrdinalIgnoreCase))
                return BarShapeType.M_C;
            if (shape.Equals("Chữ Z", StringComparison.OrdinalIgnoreCase) || shape.Equals("Z", StringComparison.OrdinalIgnoreCase))
                return BarShapeType.M_Z;
            if (shape.Equals("Chữ U", StringComparison.OrdinalIgnoreCase) || shape.Equals("U", StringComparison.OrdinalIgnoreCase))
                return BarShapeType.M_U;

            throw new ArgumentException($"Hình dạng thép buộc {component} không hợp lệ: '{configuredShape}'.");
        }

        private static (double L, double LT, double LP, double L3, double LengthMm) CalculateTieBarDimensions(
            BarShapeType shape, double spanMm, NormalizedManholeSpec spec)
        {
            if (!double.IsFinite(spanMm) || spanMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(spanMm), "Chiều dày cấu kiện không đủ cho thép buộc.");

            if (shape == BarShapeType.M_C)
            {
                return (spanMm, spec.TieHookC, spec.TieHookC, 0,
                    spanMm + 2 * spec.TieHookC);
            }

            if (shape == BarShapeType.M_Z)
            {
                return (spec.TieHookZU, spanMm, 0, 0,
                    2 * spec.TieHookZU + spanMm);
            }

            return (spanMm, spec.TieHookZU, spec.TieHookZU, spec.TieHookMidU,
                spanMm + 2 * spec.TieHookZU + spec.TieHookMidU);
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
                if (dia <= 0 || count <= 0)
                {
                    continue;
                }

                double exp = spec.PipeReinforceExpansion;

                // Khung gia cường theo hình mở: khung tròn được triển khai theo
                // đường kính; cống hộp theo đúng hai cạnh phủ bì.
                double frameWidth = pipe.CutoutWidth + 2 * exp;
                double frameHeight = pipe.CutoutHeight + 2 * exp;
                double lFrame = pipe.IsBox
                    ? 2 * (frameWidth + frameHeight) + 30 * dia
                    : 4 * frameWidth + 30 * dia;

                bars.Add(new BarInstance
                {
                    ComponentGroup = "Gia cường lỗ cống",
                    BarMark = $"{spec.PipeReinforcePrefix}{pipe.Direction}",
                    Diameter = dia,
                    Shape = BarShapeType.GC_T,
                    Quantity = count,
                    L = frameWidth,
                    LT = frameHeight,
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
            if (dia <= 0)
            {
                return;
            }

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
            if (!string.Equals(spec.CatchBasinStructure?.Trim(), "BTCT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (spec.CatchBasinRebar is not { } catchBasinRebar)
            {
                return;
            }

            int dia = catchBasinRebar.Diameter;
            double pitch = catchBasinRebar.Spacing;
            if (dia <= 0 || pitch <= 0)
            {
                return;
            }

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
