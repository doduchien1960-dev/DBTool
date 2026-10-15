using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    public record Point2D(double X, double Y)
    {
        public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    }

    public record Rect2D(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public Point2D Center => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
        public Point2D BottomLeft => new(MinX, MinY);
        public Point2D TopRight => new(MaxX, MaxY);
    }

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
        public Polygon2D? BeddingMortarProfile { get; init; }
        public Polygon2D? NeckShaftProfile { get; init; }

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

        public ManholeGeometryModel(
            NormalizedManholeSpec spec,
            PlanGeometryModel plan,
            SectionGeometryModel sectionA,
            SectionGeometryModel sectionB)
        {
            Spec = spec;
            Plan = plan;
            SectionA = sectionA;
            SectionB = sectionB;
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
            var sectionA = BuildSection(spec, isSectionA: true, plan.NeckCenter);
            var sectionB = BuildSection(spec, isSectionA: false, plan.NeckCenter);

            return new ManholeGeometryModel(spec, plan, sectionA, sectionB);
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

            if (pos.IndexOf("Giữa ga", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pos.IndexOf("Giua ga", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Point2D(0, 0);
            }

            if (pos.IndexOf("Góc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pos.IndexOf("Goc", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Mặc định góc T1-T2 (góc trên bên phải lòng ga)
                // Lỗ nắp đặt sát góc lọt lòng:
                double x = halfL - bC / 2.0;
                double y = halfB - lC / 2.0;
                return new Point2D(x, y);
            }

            if (pos.IndexOf("T1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Point2D(0, halfB - lC / 2.0);
            }

            if (pos.IndexOf("T2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Point2D(halfL - bC / 2.0, 0);
            }

            // Fallback góc T1-T2
            return new Point2D(halfL - bC / 2.0, halfB - lC / 2.0);
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
            NormalizedManholeSpec spec, bool isSectionA, Point2D neckCenter)
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

            // Cao độ Z khống chế (z = 0 tại mặt trên đáy ga trong)
            double zPadBottom = -baseF - padThick;
            double zBaseBottom = -baseF;
            double zInsideBottom = 0;
            double zInsideTop = spec.H;
            double zWallTop = (spec.Type == ManholeType.GaThamNapRoi || spec.Type == ManholeType.GaThamThuNapRoi)
                ? (spec.H - spec.MortarThickness)
                : spec.H;
            double zTopSlabTop = spec.H + topT;
            double zNeckTop = zTopSlabTop + spec.NeckHeight;
            double zCoverTop = zNeckTop + spec.CoverHeight;

            double halfOuter = halfSpan + t;
            double halfBase = halfOuter + baseExp;
            double halfPad = halfBase + padExp;

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
            // Bắt đầu từ mép đáy bên trái: (-halfBase, zBaseBottom)
            outerPoints.Add(new Point2D(-halfBase, zBaseBottom));
            outerPoints.Add(new Point2D(halfBase, zBaseBottom));

            if (baseHaunch > 0)
            {
                outerPoints.Add(new Point2D(halfBase, zBaseBottom + baseHaunch / 2.0));
                outerPoints.Add(new Point2D(halfOuter, zBaseBottom + baseHaunch));
            }
            else
            {
                outerPoints.Add(new Point2D(halfBase, 0));
                outerPoints.Add(new Point2D(halfOuter, 0));
            }

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
            if (baseHaunch > 0)
            {
                outerPoints.Add(new Point2D(-halfOuter, zBaseBottom + baseHaunch));
                outerPoints.Add(new Point2D(-halfBase, zBaseBottom + baseHaunch / 2.0));
            }
            else
            {
                outerPoints.Add(new Point2D(-halfOuter, 0));
                outerPoints.Add(new Point2D(-halfBase, 0));
            }

            var outerConcreteProfile = new Polygon2D(outerPoints, true);

            // 5. Polygon Khoang trong (InnerVoidProfile)
            var innerPoints = new List<Point2D>();
            // Bắt đầu từ góc đáy trái trong: (-halfSpan, 0)
            innerPoints.Add(new Point2D(-halfSpan, 0));
            innerPoints.Add(new Point2D(halfSpan, 0));

            // Góc nách trần bên phải
            if (topHaunch > 0 && spec.Type == ManholeType.GaThamLienKhoi)
            {
                innerPoints.Add(new Point2D(halfSpan, zInsideTop - topHaunch));
                innerPoints.Add(new Point2D(halfSpan - topHaunch, zInsideTop));
            }
            else
            {
                innerPoints.Add(new Point2D(halfSpan, zInsideTop));
            }

            // Góc nách trần bên trái
            if (topHaunch > 0 && spec.Type == ManholeType.GaThamLienKhoi)
            {
                innerPoints.Add(new Point2D(-halfSpan + topHaunch, zInsideTop));
                innerPoints.Add(new Point2D(-halfSpan, zInsideTop - topHaunch));
            }
            else
            {
                innerPoints.Add(new Point2D(-halfSpan, zInsideTop));
            }

            var innerVoidProfile = new Polygon2D(innerPoints, true);

            // 6. Tấm nắp đúc sẵn rời (Loại 2 & 3)
            Polygon2D? precastSlabProfile = null;
            Polygon2D? beddingMortarProfile = null;

            if (spec.Type == ManholeType.GaThamNapRoi || spec.Type == ManholeType.GaThamThuNapRoi)
            {
                // Vữa đệm đỉnh tường: dày MortarThickness nằm trên đỉnh tường
                var mortarPts = new List<Point2D>
                {
                    new(-halfOuter, zWallTop),
                    new(halfOuter, zWallTop),
                    new(halfOuter, zInsideTop),
                    new(-halfOuter, zInsideTop)
                };
                beddingMortarProfile = new Polygon2D(mortarPts, true);

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
                BeddingMortarProfile = beddingMortarProfile,
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
            var extensionRect = new Rect2D(xMinExt, pipe.InvertLevelFromBottom, xMaxExt, pipe.InvertLevelFromBottom + pipe.OuterHeight);

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
