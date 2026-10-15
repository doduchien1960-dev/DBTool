using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    public record Polygon2D(IReadOnlyList<Point2D> Points, bool Closed = true);

    /// <summary>
    /// Đặc tả hình học mặt bằng (Plan View)
    /// </summary>
    public class PlanGeometryModel
    {
        public Rect2D PadRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D BaseRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D OuterWallRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D InnerChamberRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D NeckOpeningRect { get; init; } = new(0, 0, 0, 0);
        public Point2D NeckCenter { get; init; } = new(0, 0);

        public List<PlanPipeModel> Pipes { get; init; } = new();
    }

    public class PlanPipeModel
    {
        public WallDirection Direction { get; init; }
        public string PipeCode { get; init; } = "";
        public double OuterDia { get; init; }
        public double CutoutDia { get; init; }
        public Point2D WallCenterPoint { get; init; } = new(0, 0);
        public Point2D PipeOuterEnd1 { get; init; } = new(0, 0);
        public Point2D PipeOuterEnd2 { get; init; } = new(0, 0);
        public Point2D WallCutoutPoint1 { get; init; } = new(0, 0);
        public Point2D WallCutoutPoint2 { get; init; } = new(0, 0);
        public Point2D CenterLineStart { get; init; } = new(0, 0);
        public Point2D CenterLineEnd { get; init; } = new(0, 0);
    }

    /// <summary>
    /// Cửa thu Kiểu 1: hệ tọa độ mặt bằng dùng chung với ga; cao độ tính từ đáy trong ga.
    /// </summary>
    public class CatchBasinGeometryModel
    {
        public WallDirection Direction { get; init; }
        public bool IsAtNeck { get; init; }
        public bool IsSectionA => Direction == WallDirection.T1 || Direction == WallDirection.T3;
        public bool IsPositiveSide => Direction == WallDirection.T1 || Direction == WallDirection.T2;
        public Rect2D OuterPlanRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D HiddenInnerPlanRect { get; init; } = new(0, 0, 0, 0);
        public double AttachmentInnerFace { get; init; }
        public double OuterStart { get; init; }
        public double InnerFar { get; init; }
        public double OuterFar { get; init; }
        public double TopZ { get; init; }
        public double FloorZ { get; init; }
        public double BaseBottomZ { get; init; }
        public double PadBottomZ { get; init; }
        public double PadExpansion { get; init; }
        public double HoleHeight { get; init; }
    }

    /// <summary>
    /// Đặc tả hình học mặt cắt (Section A-A hoặc B-B)
    /// </summary>
    public class SectionGeometryModel
    {
        public string Title { get; init; } = "";
        public bool IsSectionA { get; init; } // true: A-A (dọc qua T1-T3, rộng B); false: B-B (ngang qua T4-T2, dài L)
        public double SpanLength { get; init; } // B (cho A-A) hoặc L (cho B-B)

        // Các mức cao độ khống chế (mm, z=0 tại đáy trong ga)
        public double ZPadBottom { get; init; }
        public double ZBaseBottom { get; init; }
        public double ZInsideBottom { get; init; } = 0;
        public double ZInsideTop { get; init; }
        public double ZWallTop { get; init; }
        public double ZTopSlabTop { get; init; }
        public double ZNeckTop { get; init; }
        public double ZCoverTop { get; init; }

        // Bề rộng các khối trên mặt cắt (trục X cục bộ của mặt cắt từ -Span/2 ra ngoài)
        public double HalfSpan => SpanLength / 2.0;
        public double WallThickness { get; init; }
        public double BaseExpansion { get; init; }
        public double PadExpansion { get; init; }

        public double HalfOuter => HalfSpan + WallThickness;
        public double HalfBase => HalfOuter + BaseExpansion;
        public double HalfPad => HalfBase + PadExpansion;

        // Các đường bao polygon
        public Polygon2D PadProfile { get; init; } = new(Array.Empty<Point2D>());
        public Polygon2D OuterConcreteProfile { get; init; } = new(Array.Empty<Point2D>());
        public Polygon2D InnerVoidProfile { get; init; } = new(Array.Empty<Point2D>());
        public Polygon2D? PrecastSlabProfile { get; init; }
        public IReadOnlyList<Polygon2D> BeddingMortarProfiles { get; init; } = Array.Empty<Polygon2D>();
        public IReadOnlyList<Polygon2D> NeckWallProfiles { get; init; } = Array.Empty<Polygon2D>();

        // Lỗ mở nắp trên trần
        public double NeckHoleXMin { get; init; }
        public double NeckHoleXMax { get; init; }

        // Cống trên mặt cắt (bên trái và bên phải)
        public SectionPipeModel? LeftPipe { get; init; }
        public SectionPipeModel? RightPipe { get; init; }

        // Vát nách
        public double BaseHaunch { get; init; }
        public double TopHaunch { get; init; }
    }

    public class SectionPipeModel
    {
        public WallDirection Direction { get; init; }
        public string PipeCode { get; init; } = "";
        public bool IsRound { get; init; }
        public double InnerDia { get; init; }
        public double InnerHeight { get; init; }
        public double OuterDia { get; init; }
        public double OuterHeight { get; init; }
        public double CutoutDia { get; init; }
        public double InvertLevel { get; init; }
        public double CutoutBottomZ { get; init; }
        public double CutoutTopZ { get; init; }
        public bool IsOnLeft { get; init; }
        public Rect2D CutoutRect { get; init; } = new(0, 0, 0, 0);
        public Rect2D ExtensionRect { get; init; } = new(0, 0, 0, 0);
        public Point2D CenterLineStart { get; init; } = new(0, 0);
        public Point2D CenterLineEnd { get; init; } = new(0, 0);
        public double AbsoluteInvertElevation { get; init; }
    }

    /// <summary>
    /// Toàn bộ hình học hoàn chỉnh của một hố ga
    /// </summary>
    public class ManholeGeometryModel
    {
        public NormalizedManholeSpec Spec { get; init; }
        public PlanGeometryModel Plan { get; init; }
        public SectionGeometryModel SectionA { get; init; }
        public SectionGeometryModel SectionB { get; init; }
        public CatchBasinGeometryModel? CatchBasin { get; init; }

        public ManholeGeometryModel(
            NormalizedManholeSpec spec,
            PlanGeometryModel plan,
            SectionGeometryModel sectionA,
            SectionGeometryModel sectionB,
            CatchBasinGeometryModel? catchBasin = null)
        {
            Spec = spec;
            Plan = plan;
            SectionA = sectionA;
            SectionB = sectionB;
            CatchBasin = catchBasin;
        }
    }

    /// <summary>
    /// Động cơ tính toán hình học không gian hố ga (DBTool.Core.GeometryEngine)
    /// Đơn vị hoàn toàn bằng milimet (mm)
    /// </summary>
    public class GeometryEngine
    {
        public static ManholeGeometryModel Build(NormalizedManholeSpec spec)
        {
            var plan = BuildPlan(spec);
            var catchBasin = BuildCatchBasin(spec, plan);
            var sectionA = BuildSection(spec, isSectionA: true, plan.NeckCenter, catchBasin);
            var sectionB = BuildSection(spec, isSectionA: false, plan.NeckCenter, catchBasin);

            return new ManholeGeometryModel(spec, plan, sectionA, sectionB, catchBasin);
        }

        public static CatchBasinGeometryModel? BuildCatchBasin(NormalizedManholeSpec spec, PlanGeometryModel plan)
        {
            if (spec.Type != ManholeType.GaThamThuNapRoi || spec.CatchBasinHoleWidth <= 0 ||
                spec.CatchBasinChamberLength <= 0 || spec.CatchBasinWallThickness <= 0)
                return null;
            if (!string.Equals(spec.CatchBasinStyle?.Trim(), "Kiểu 1", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Kiểu cửa thu không hỗ trợ: '{spec.CatchBasinStyle}'.", nameof(spec));

            string directionText = spec.CatchBasinWallDir?.Trim() ?? "";
            WallDirection direction = directionText.Contains("T1", StringComparison.OrdinalIgnoreCase) ? WallDirection.T1 :
                directionText.Contains("T2", StringComparison.OrdinalIgnoreCase) ? WallDirection.T2 :
                directionText.Contains("T3", StringComparison.OrdinalIgnoreCase) ? WallDirection.T3 :
                directionText.Contains("T4", StringComparison.OrdinalIgnoreCase) ? WallDirection.T4 :
                throw new ArgumentException($"Hướng cửa thu không hợp lệ: '{directionText}'.", nameof(spec));
            bool atNeck = (spec.CatchBasinPosition?.Trim() ?? "").StartsWith("Cổ", StringComparison.OrdinalIgnoreCase);
            bool alongX = direction == WallDirection.T2 || direction == WallDirection.T4;
            double normalSign = direction == WallDirection.T1 || direction == WallDirection.T2 ? 1 : -1;
            Rect2D attachment = atNeck ? plan.NeckOpeningRect : plan.InnerChamberRect;
            double innerFace = direction switch
            {
                WallDirection.T1 => attachment.MaxY,
                WallDirection.T2 => attachment.MaxX,
                WallDirection.T3 => attachment.MinY,
                _ => attachment.MinX
            };

            double tangentCenter;
            if (atNeck)
            {
                tangentCenter = alongX ? plan.NeckCenter.Y : plan.NeckCenter.X;
            }
            else
            {
                double halfTangent = alongX ? spec.B / 2.0 : spec.L / 2.0;
                double edgeCenter = Math.Max(0, halfTangent - spec.CatchBasinHoleWidth / 2.0);
                bool right = directionText.StartsWith("Phải", StringComparison.OrdinalIgnoreCase);
                bool left = directionText.StartsWith("Trái", StringComparison.OrdinalIgnoreCase);
                double rightSign = direction == WallDirection.T1 || direction == WallDirection.T4 ? 1 : -1;
                tangentCenter = right ? rightSign * edgeCenter : left ? -rightSign * edgeCenter : 0;
            }

            double t = spec.CatchBasinWallThickness;
            double start = innerFace + normalSign * t;
            double innerFar = innerFace + normalSign * (t + spec.CatchBasinChamberLength);
            double outerFar = innerFar + normalSign * t;
            double outerHalfTangential = spec.CatchBasinHoleWidth / 2.0 + t;
            double innerHalfTangential = spec.CatchBasinHoleWidth / 2.0;

            static Rect2D Rect(double n1, double n2, double tc, double halfT, bool normalIsX) =>
                normalIsX
                    ? new Rect2D(Math.Min(n1, n2), tc - halfT, Math.Max(n1, n2), tc + halfT)
                    : new Rect2D(tc - halfT, Math.Min(n1, n2), tc + halfT, Math.Max(n1, n2));

            double topZ = atNeck ? spec.H + spec.TopThickness + spec.NeckHeight : spec.H - spec.MortarThickness;
            double floorZ = topZ - spec.CatchBasinChamberHeight;
            return new CatchBasinGeometryModel
            {
                Direction = direction,
                IsAtNeck = atNeck,
                OuterPlanRect = Rect(start, outerFar, tangentCenter, outerHalfTangential, alongX),
                HiddenInnerPlanRect = Rect(innerFace, innerFar, tangentCenter, innerHalfTangential, alongX),
                AttachmentInnerFace = innerFace,
                OuterStart = start,
                InnerFar = innerFar,
                OuterFar = outerFar,
                TopZ = topZ,
                FloorZ = floorZ,
                BaseBottomZ = floorZ - spec.CatchBasinBaseThickness,
                PadBottomZ = floorZ - spec.CatchBasinBaseThickness - spec.CatchBasinPadThickness,
                PadExpansion = spec.CatchBasinPadExpansion,
                HoleHeight = spec.CatchBasinHoleHeight
            };
        }

        // ====================================================================
        // DỰNG MẶT BẰNG (PLAN)
        // ====================================================================
        public static PlanGeometryModel BuildPlan(NormalizedManholeSpec spec)
        {
            double halfL = spec.L / 2.0;
            double halfB = spec.B / 2.0;
            double t = spec.WallThickness;
            double baseExp = spec.BaseExpansion;
            double padExp = spec.PadExpansion;

            double pbX = halfL + t;
            double pbY = halfB + t;
            double mdX = pbX + baseExp;
            double mdY = pbY + baseExp;
            double lotX = mdX + padExp;
            double lotY = mdY + padExp;

            // Xác định tâm lỗ cổ ga trên trần
            Point2D neckCenter = CalculateNeckCenter(spec, halfL, halfB);

            // Kích thước lỗ cổ ga
            double nHalfW = spec.NeckInnerWidth / 2.0;
            double nHalfL = spec.NeckInnerLength / 2.0;
            var neckOpeningRect = new Rect2D(
                neckCenter.X - nHalfW, neckCenter.Y - nHalfL,
                neckCenter.X + nHalfW, neckCenter.Y + nHalfL);

            // Xử lý các cống trên mặt bằng
            var planPipes = new List<PlanPipeModel>();
            if (spec.PipeT1 != null) planPipes.Add(BuildPlanPipe(spec.PipeT1, WallDirection.T1, spec, pbX, pbY));
            if (spec.PipeT2 != null) planPipes.Add(BuildPlanPipe(spec.PipeT2, WallDirection.T2, spec, pbX, pbY));
            if (spec.PipeT3 != null) planPipes.Add(BuildPlanPipe(spec.PipeT3, WallDirection.T3, spec, pbX, pbY));
            if (spec.PipeT4 != null) planPipes.Add(BuildPlanPipe(spec.PipeT4, WallDirection.T4, spec, pbX, pbY));

            return new PlanGeometryModel
            {
                PadRect = new Rect2D(-lotX, -lotY, lotX, lotY),
                BaseRect = new Rect2D(-mdX, -mdY, mdX, mdY),
                OuterWallRect = new Rect2D(-pbX, -pbY, pbX, pbY),
                InnerChamberRect = new Rect2D(-halfL, -halfB, halfL, halfB),
                NeckOpeningRect = neckOpeningRect,
                NeckCenter = neckCenter,
                Pipes = planPipes
            };
        }

        public static Point2D CalculateNeckCenter(NormalizedManholeSpec spec, double halfL, double halfB)
        {
            double bC = spec.NeckInnerWidth;
            double lC = spec.NeckInnerLength;
            string pos = spec.NeckPosition?.Trim() ?? "";
            double right = halfL - bC / 2.0;
            double left = -right;
            double top = halfB - lC / 2.0;
            double bottom = -top;
            if (pos.Equals("Giữa ga", StringComparison.OrdinalIgnoreCase) ||
                pos.Equals("Giua ga", StringComparison.OrdinalIgnoreCase))
                return new Point2D(0, 0);
            if (pos.StartsWith("Góc", StringComparison.OrdinalIgnoreCase) ||
                pos.StartsWith("Goc", StringComparison.OrdinalIgnoreCase))
            {
                if (pos.Contains("T1-T2", StringComparison.OrdinalIgnoreCase)) return new Point2D(right, top);
                if (pos.Contains("T2-T3", StringComparison.OrdinalIgnoreCase)) return new Point2D(right, bottom);
                if (pos.Contains("T3-T4", StringComparison.OrdinalIgnoreCase)) return new Point2D(left, bottom);
                if (pos.Contains("T4-T1", StringComparison.OrdinalIgnoreCase)) return new Point2D(left, top);
            }
            if (pos.StartsWith("Giữa", StringComparison.OrdinalIgnoreCase) ||
                pos.StartsWith("Giua", StringComparison.OrdinalIgnoreCase))
            {
                if (pos.EndsWith("T1", StringComparison.OrdinalIgnoreCase)) return new Point2D(0, top);
                if (pos.EndsWith("T2", StringComparison.OrdinalIgnoreCase)) return new Point2D(right, 0);
                if (pos.EndsWith("T3", StringComparison.OrdinalIgnoreCase)) return new Point2D(0, bottom);
                if (pos.EndsWith("T4", StringComparison.OrdinalIgnoreCase)) return new Point2D(left, 0);
            }
            throw new ArgumentException($"Vị trí cổ ga không hợp lệ: '{pos}'.", nameof(spec));
        }

        private static PlanPipeModel BuildPlanPipe(
            PipeConnectionSpec pipe, WallDirection dir, NormalizedManholeSpec spec, double pbX, double pbY)
        {
            double halfD = pipe.OuterDia / 2.0;
            double halfCut = pipe.CutoutDia / 2.0;
            double projLen = Math.Max(pipe.OuterDia * 1.5, 400); // Chiều dài ống nhô ra ngoài tường
            double centerLineExtra = 200;

            Point2D wallCenter;
            Point2D pipeEnd1, pipeEnd2;
            Point2D cut1, cut2;
            Point2D cLineStart, cLineEnd;

            double localOffset = WallViewTransform.ToLocalOffset(dir, pipe.OffsetFromCenter);

            // Offset dương là bên trái khi nhìn từ trong ga ra ngoài.
            switch (dir)
            {
                case WallDirection.T1:
                    wallCenter = new Point2D(localOffset, pbY);
                    cut1 = new Point2D(wallCenter.X - halfCut, pbY);
                    cut2 = new Point2D(wallCenter.X + halfCut, pbY);
                    pipeEnd1 = new Point2D(wallCenter.X - halfD, pbY + projLen);
                    pipeEnd2 = new Point2D(wallCenter.X + halfD, pbY + projLen);
                    cLineStart = new Point2D(wallCenter.X, pbY - 100);
                    cLineEnd = new Point2D(wallCenter.X, pbY + projLen + centerLineExtra);
                    break;

                case WallDirection.T2:
                    wallCenter = new Point2D(pbX, localOffset);
                    cut1 = new Point2D(pbX, wallCenter.Y - halfCut);
                    cut2 = new Point2D(pbX, wallCenter.Y + halfCut);
                    pipeEnd1 = new Point2D(pbX + projLen, wallCenter.Y - halfD);
                    pipeEnd2 = new Point2D(pbX + projLen, wallCenter.Y + halfD);
                    cLineStart = new Point2D(pbX - 100, wallCenter.Y);
                    cLineEnd = new Point2D(pbX + projLen + centerLineExtra, wallCenter.Y);
                    break;

                case WallDirection.T3:
                    wallCenter = new Point2D(localOffset, -pbY);
                    cut1 = new Point2D(wallCenter.X - halfCut, -pbY);
                    cut2 = new Point2D(wallCenter.X + halfCut, -pbY);
                    pipeEnd1 = new Point2D(wallCenter.X - halfD, -pbY - projLen);
                    pipeEnd2 = new Point2D(wallCenter.X + halfD, -pbY - projLen);
                    cLineStart = new Point2D(wallCenter.X, -pbY + 100);
                    cLineEnd = new Point2D(wallCenter.X, -pbY - projLen - centerLineExtra);
                    break;

                case WallDirection.T4:
                default:
                    wallCenter = new Point2D(-pbX, localOffset);
                    cut1 = new Point2D(-pbX, wallCenter.Y - halfCut);
                    cut2 = new Point2D(-pbX, wallCenter.Y + halfCut);
                    pipeEnd1 = new Point2D(-pbX - projLen, wallCenter.Y - halfD);
                    pipeEnd2 = new Point2D(-pbX - projLen, wallCenter.Y + halfD);
                    cLineStart = new Point2D(-pbX + 100, wallCenter.Y);
                    cLineEnd = new Point2D(-pbX - projLen - centerLineExtra, wallCenter.Y);
                    break;
            }

            return new PlanPipeModel
            {
                Direction = dir,
                PipeCode = pipe.PipeCode,
                OuterDia = pipe.OuterDia,
                CutoutDia = pipe.CutoutDia,
                WallCenterPoint = wallCenter,
                PipeOuterEnd1 = pipeEnd1,
                PipeOuterEnd2 = pipeEnd2,
                WallCutoutPoint1 = cut1,
                WallCutoutPoint2 = cut2,
                CenterLineStart = cLineStart,
                CenterLineEnd = cLineEnd
            };
        }

        // ====================================================================
        // DỰNG MẶT CẮT (SECTION A-A / B-B)
        // ====================================================================
        public static SectionGeometryModel BuildSection(
            NormalizedManholeSpec spec, bool isSectionA, Point2D neckCenter,
            CatchBasinGeometryModel? catchBasin = null)
        {
            string title = isSectionA ? "MẶT CẮT A-A" : "MẶT CẮT B-B";
            double span = isSectionA ? spec.B : spec.L;
            double halfSpan = span / 2.0;
            double t = spec.WallThickness;
            double baseF = spec.BaseThickness;
            double topT = spec.TopThickness;
            double padExp = spec.PadExpansion;
            double padThick = spec.PadThickness;
            double baseExp = spec.BaseExpansion;
            double baseHaunch = spec.BaseHaunch;
            double topHaunch = (spec.Type == ManholeType.GaThamLienKhoi) ? spec.TopHaunch : 0;
            if (baseHaunch < 0 || baseHaunch >= halfSpan || baseHaunch >= spec.H)
                throw new ArgumentOutOfRangeException(nameof(spec.BaseHaunch),
                    "Vát nách chân tường phải không âm và nhỏ hơn nửa nhịp cũng như chiều cao lòng ga.");

            // Cao độ Z khống chế (z = 0 tại mặt trên đáy ga trong)
            double zPadBottom = -baseF - padThick;
            double zBaseBottom = -baseF;
            double zInsideBottom = 0;
            double zInsideTop = spec.H;
            double zWallTop = (spec.Type == ManholeType.GaThamNapRoi || spec.Type == ManholeType.GaThamThuNapRoi)
                ? (spec.H - spec.MortarThickness)
                : spec.H;
            double zVoidTop = zWallTop;
            double zTopSlabTop = spec.H + topT;
            double zNeckTop = zTopSlabTop + spec.NeckHeight;
            double zCoverTop = zNeckTop + spec.CoverHeight;

            double halfOuter = halfSpan + t;
            double halfBase = halfOuter + baseExp;
            double halfPad = halfBase + padExp;
            // CF.Ga!D5 is the inside wall-to-floor haunch. In the golden DWG,
            // the underside and outer sides of the base slab remain square.

            // 1. Polygon Bê tông lót
            var padPoints = new List<Point2D>
            {
                new(-halfPad, zPadBottom),
                new(halfPad, zPadBottom),
                new(halfPad, zBaseBottom),
                new(-halfPad, zBaseBottom)
            };
            var padProfile = new Polygon2D(padPoints, true);

            // 2. Xác định cống bên trái và bên phải của mặt cắt
            // Mặt cắt A-A: Trục ngang là Y. Bên trái là T3 (-Y), Bên phải là T1 (+Y)
            // Mặt cắt B-B: Trục ngang là X. Bên trái là T4 (-X), Bên phải là T2 (+X)
            PipeConnectionSpec? leftSpec = isSectionA ? spec.PipeT3 : spec.PipeT4;
            PipeConnectionSpec? rightSpec = isSectionA ? spec.PipeT1 : spec.PipeT2;

            SectionPipeModel? leftPipe = leftSpec != null ? BuildSectionPipe(leftSpec, isOnLeft: true, halfSpan, t, spec.BaseInvertElevation) : null;
            SectionPipeModel? rightPipe = rightSpec != null ? BuildSectionPipe(rightSpec, isOnLeft: false, halfSpan, t, spec.BaseInvertElevation) : null;

            // 3. Tọa độ lỗ mở nắp trên trần
            // Trong MC A-A: Chiều ngang là Y -> tọa độ tâm nắp là neckCenter.Y, kích thước là NeckInnerLength
            // Trong MC B-B: Chiều ngang là X -> tọa độ tâm nắp là neckCenter.X, kích thước là NeckInnerWidth
            double neckCenterCoord = isSectionA ? neckCenter.Y : neckCenter.X;
            double neckDim = isSectionA ? spec.NeckInnerLength : spec.NeckInnerWidth;
            double neckHoleMin = neckCenterCoord - neckDim / 2.0;
            double neckHoleMax = neckCenterCoord + neckDim / 2.0;

            // Đảm bảo lỗ nắp không vượt ra ngoài mép lòng ga
            neckHoleMin = Math.Max(neckHoleMin, -halfSpan);
            neckHoleMax = Math.Min(neckHoleMax, halfSpan);

            // 4. Polygon Đường bao ngoài BTCT (OuterConcreteProfile)
            var outerPoints = new List<Point2D>();
            outerPoints.Add(new Point2D(-halfBase, zBaseBottom));
            outerPoints.Add(new Point2D(halfBase, zBaseBottom));

            // Vai bản đáy phía ngoài vẫn giữ đủ bề rộng tại z=0.
            outerPoints.Add(new Point2D(halfBase, 0));
            if (baseExp > 0.01)
                outerPoints.Add(new Point2D(halfOuter, 0));

            // Đi lên đỉnh tường / trần bên phải
            if (spec.Type == ManholeType.GaThamLienKhoi)
            {
                // Liền khối: Tường liền trần
                outerPoints.Add(new Point2D(halfOuter, zTopSlabTop));

                // Nếu có cổ ga W > 0
                if (spec.NeckHeight > 0)
                {
                    double neckWallRight = neckHoleMax + spec.NeckWallThickness;
                    double neckWallLeft = neckHoleMin - spec.NeckWallThickness;

                    outerPoints.Add(new Point2D(neckWallRight, zTopSlabTop));
                    outerPoints.Add(new Point2D(neckWallRight, zNeckTop));
                    outerPoints.Add(new Point2D(neckHoleMax, zNeckTop));
                    outerPoints.Add(new Point2D(neckHoleMax, zTopSlabTop));

                    outerPoints.Add(new Point2D(neckHoleMin, zTopSlabTop));
                    outerPoints.Add(new Point2D(neckHoleMin, zNeckTop));
                    outerPoints.Add(new Point2D(neckWallLeft, zNeckTop));
                    outerPoints.Add(new Point2D(neckWallLeft, zTopSlabTop));
                }
                else
                {
                    // Lỗ mở trần nắp phẳng
                    outerPoints.Add(new Point2D(neckHoleMax, zTopSlabTop));
                    outerPoints.Add(new Point2D(neckHoleMax, zInsideTop));
                    outerPoints.Add(new Point2D(neckHoleMin, zInsideTop));
                    outerPoints.Add(new Point2D(neckHoleMin, zTopSlabTop));
                }

                outerPoints.Add(new Point2D(-halfOuter, zTopSlabTop));
            }
            else
            {
                // Nắp rời (Loại 2 & 3): Thân tường kết thúc tại zWallTop
                outerPoints.Add(new Point2D(halfOuter, zWallTop));
                outerPoints.Add(new Point2D(halfSpan, zWallTop));
                outerPoints.Add(new Point2D(-halfSpan, zWallTop));
                outerPoints.Add(new Point2D(-halfOuter, zWallTop));
            }

            // Đi xuống mép đáy bên trái
            outerPoints.Add(new Point2D(-halfOuter, 0));
            if (baseExp > 0.01)
                outerPoints.Add(new Point2D(-halfBase, 0));
            var outerConcreteProfile = new Polygon2D(outerPoints, true);

            // 5. Polygon Khoang trong (InnerVoidProfile)
            var innerPoints = new List<Point2D>();
            // CF.Ga!D5 forms the two 45-degree inside haunches. The sample
            // section dimensions both legs as 100 mm for G400-1.
            innerPoints.Add(new Point2D(-halfSpan + baseHaunch, 0));
            innerPoints.Add(new Point2D(halfSpan - baseHaunch, 0));
            if (baseHaunch > 0)
                innerPoints.Add(new Point2D(halfSpan, baseHaunch));

            // Góc nách trần bên phải
            if (topHaunch > 0 && spec.Type == ManholeType.GaThamLienKhoi)
            {
                innerPoints.Add(new Point2D(halfSpan, zInsideTop - topHaunch));
                innerPoints.Add(new Point2D(halfSpan - topHaunch, zInsideTop));
            }
            else
            {
                innerPoints.Add(new Point2D(halfSpan, zVoidTop));
            }

            // Góc nách trần bên trái
            if (topHaunch > 0 && spec.Type == ManholeType.GaThamLienKhoi)
            {
                innerPoints.Add(new Point2D(-halfSpan + topHaunch, zInsideTop));
                innerPoints.Add(new Point2D(-halfSpan, zInsideTop - topHaunch));
            }
            else
            {
                innerPoints.Add(new Point2D(-halfSpan, zVoidTop));
            }

            if (baseHaunch > 0)
                innerPoints.Add(new Point2D(-halfSpan, baseHaunch));

            var innerVoidProfile = new Polygon2D(innerPoints, true);

            // 6. Tấm nắp đúc sẵn rời (Loại 2 & 3)
            Polygon2D? precastSlabProfile = null;
            var beddingMortarProfiles = new List<Polygon2D>();
            var neckWallProfiles = new List<Polygon2D>();

            static Polygon2D RectProfile(double minX, double minY, double maxX, double maxY) =>
                new(new List<Point2D>
                {
                    new(minX, minY), new(maxX, minY),
                    new(maxX, maxY), new(minX, maxY)
                }, true);

            if (spec.Type == ManholeType.GaThamNapRoi || spec.Type == ManholeType.GaThamThuNapRoi)
            {
                // Vữa đệm chỉ đặt trên hai thành; không bịt lòng ga.
                if (spec.MortarThickness > 0)
                {
                    beddingMortarProfiles.Add(RectProfile(-halfOuter, zWallTop, -halfSpan, zInsideTop));
                    beddingMortarProfiles.Add(RectProfile(halfSpan, zWallTop, halfOuter, zInsideTop));
                }

                // Tấm đan trần: từ zInsideTop đến zTopSlabTop, phủ bì ngoài có thể vát nắp
                var slabPts = new List<Point2D>
                {
                    new(-halfOuter, zInsideTop),
                    new(halfOuter, zInsideTop),
                    new(halfOuter, zTopSlabTop),
                    new(neckHoleMax, zTopSlabTop),
                    new(neckHoleMax, zInsideTop),
                    new(neckHoleMin, zInsideTop),
                    new(neckHoleMin, zTopSlabTop),
                    new(-halfOuter, zTopSlabTop)
                };
                precastSlabProfile = new Polygon2D(slabPts, true);

                if (spec.NeckHeight > 0 && spec.NeckWallThickness > 0)
                {
                    bool catchOnThisSection = catchBasin is { IsAtNeck: true } && catchBasin.IsSectionA == isSectionA;
                    double leftTop = catchOnThisSection && !catchBasin!.IsPositiveSide
                        ? Math.Min(zNeckTop, catchBasin.FloorZ)
                        : zNeckTop;
                    double rightTop = catchOnThisSection && catchBasin!.IsPositiveSide
                        ? Math.Min(zNeckTop, catchBasin.FloorZ)
                        : zNeckTop;
                    neckWallProfiles.Add(RectProfile(
                        neckHoleMin - spec.NeckWallThickness, zTopSlabTop,
                        neckHoleMin, leftTop));
                    neckWallProfiles.Add(RectProfile(
                        neckHoleMax, zTopSlabTop,
                        neckHoleMax + spec.NeckWallThickness, rightTop));
                }
            }

            return new SectionGeometryModel
            {
                Title = title,
                IsSectionA = isSectionA,
                SpanLength = span,
                ZPadBottom = zPadBottom,
                ZBaseBottom = zBaseBottom,
                ZInsideBottom = zInsideBottom,
                ZInsideTop = zInsideTop,
                ZWallTop = zWallTop,
                ZTopSlabTop = zTopSlabTop,
                ZNeckTop = zNeckTop,
                ZCoverTop = zCoverTop,
                WallThickness = t,
                BaseExpansion = baseExp,
                PadExpansion = padExp,
                PadProfile = padProfile,
                OuterConcreteProfile = outerConcreteProfile,
                InnerVoidProfile = innerVoidProfile,
                PrecastSlabProfile = precastSlabProfile,
                BeddingMortarProfiles = beddingMortarProfiles,
                NeckWallProfiles = neckWallProfiles,
                NeckHoleXMin = neckHoleMin,
                NeckHoleXMax = neckHoleMax,
                LeftPipe = leftPipe,
                RightPipe = rightPipe,
                BaseHaunch = baseHaunch,
                TopHaunch = topHaunch
            };
        }

        private static SectionPipeModel BuildSectionPipe(
            PipeConnectionSpec pipe, bool isOnLeft, double halfSpan, double wallThickness, double baseElevation)
        {
            double extLength = 200; // Đoạn ống nhô ra ngoài tường trên mặt cắt
            double centerLineExtra = 150;

            double xInner = isOnLeft ? -halfSpan : halfSpan;
            double xOuter = isOnLeft ? (-halfSpan - wallThickness) : (halfSpan + wallThickness);
            double xExt = isOnLeft ? (xOuter - extLength) : (xOuter + extLength);

            double xMinCutout = Math.Min(xInner, xOuter);
            double xMaxCutout = Math.Max(xInner, xOuter);

            double xMinExt = Math.Min(xOuter, xExt);
            double xMaxExt = Math.Max(xOuter, xExt);

            var cutoutRect = new Rect2D(xMinCutout, pipe.CutoutBottomZ, xMaxCutout, pipe.CutoutTopZ);
            // InvertLevelFromBottom is the bottom of the pipe's clear internal opening.
            // The concrete/pipe wall therefore extends one wall thickness below and
            // above the clear opening. Keep this aligned with CutoutBottomZ/TopZ.
            var extensionRect = new Rect2D(
                xMinExt,
                pipe.InvertLevelFromBottom - pipe.WallThickness,
                xMaxExt,
                pipe.InvertLevelFromBottom + pipe.InnerHeight + pipe.WallThickness);

            double zCenter = pipe.InvertLevelFromBottom + pipe.InnerHeight / 2.0;
            Point2D cLineStart = isOnLeft ? new Point2D(xInner + 100, zCenter) : new Point2D(xInner - 100, zCenter);
            Point2D cLineEnd = isOnLeft ? new Point2D(xExt - centerLineExtra, zCenter) : new Point2D(xExt + centerLineExtra, zCenter);

            double absInvert = baseElevation + pipe.InvertLevelFromBottom / 1000.0;

            return new SectionPipeModel
            {
                Direction = pipe.Direction,
                PipeCode = pipe.PipeCode,
                IsRound = !pipe.PipeCatalogInfo?.IsBox ?? true,
                InnerDia = pipe.InnerDia,
                InnerHeight = pipe.InnerHeight,
                OuterDia = pipe.OuterDia,
                OuterHeight = pipe.OuterHeight,
                CutoutDia = pipe.CutoutDia,
                InvertLevel = pipe.InvertLevelFromBottom,
                CutoutBottomZ = pipe.CutoutBottomZ,
                CutoutTopZ = pipe.CutoutTopZ,
                IsOnLeft = isOnLeft,
                CutoutRect = cutoutRect,
                ExtensionRect = extensionRect,
                CenterLineStart = cLineStart,
                CenterLineEnd = cLineEnd,
                AbsoluteInvertElevation = absInvert
            };
        }
    }
}
