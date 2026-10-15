using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ 1: Cấu tạo & Mặt cắt kết cấu hố ga (Mặt bằng, MC A-A, MC B-B, Dim 2 tầng, Hatch kép)
    /// </summary>
    public class Sheet1Drawer
    {
        public static void Draw(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            Point2D sheetOrigin,
            ObjectId dimStyleId)
        {
            // Hai hàng, ba cột giúp giữ đủ 6 hình trong khung A3:
            // mặt bằng, hai mặt cắt kết cấu, hai mặt cắt cốt thép và mặt bằng lưới tường.
            Point2D planOrigin = new(sheetOrigin.X + 2100, sheetOrigin.Y - 2400);
            Point2D secAOrigin = new(sheetOrigin.X + 6300, sheetOrigin.Y - 2800);
            Point2D secBOrigin = new(sheetOrigin.X + 10500, sheetOrigin.Y - 2800);
            Point2D wallPlanOrigin = new(sheetOrigin.X + 2100, sheetOrigin.Y - 6500);
            Point2D secARebarOrigin = new(sheetOrigin.X + 6300, sheetOrigin.Y - 6200);
            Point2D secBRebarOrigin = new(sheetOrigin.X + 10500, sheetOrigin.Y - 6200);

            DrawPlanView(blockTable, btr, tr, db, spec, geom.Plan, planOrigin, dimStyleId);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionA, secAOrigin, dimStyleId);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionB, secBOrigin, dimStyleId);
            DrawWallRebarPlan(blockTable, btr, tr, db, spec, geom, wallPlanOrigin, dimStyleId);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionA, secARebarOrigin, dimStyleId, showRebar: true);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionB, secBRebarOrigin, dimStyleId, showRebar: true);
        }

        private static void DrawWallRebarPlan(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            Point2D origin,
            ObjectId dimStyleId)
        {
            var outer = geom.Plan.OuterWallRect;
            var inner = geom.Plan.InnerChamberRect;
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + outer.MinX, origin.Y + outer.MinY,
                origin.X + outer.MaxX, origin.Y + outer.MaxY, "VHG-BT");
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + inner.MinX, origin.Y + inner.MinY,
                origin.X + inner.MaxX, origin.Y + inner.MaxY, "VHG-BT");

            int dia = spec.WallOuterRebar?.Diameter ?? 10;
            double inset = spec.CoverWall + dia / 2.0;
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + outer.MinX + inset, origin.Y + outer.MinY + inset,
                origin.X + outer.MaxX - inset, origin.Y + outer.MaxY - inset, "VHG-Thep");
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + inner.MinX - inset, origin.Y + inner.MinY - inset,
                origin.X + inner.MaxX + inset, origin.Y + inner.MaxY + inset, "VHG-Thep2", colorIndex: 2);

            double tieSpacing = spec.TieSpec?.Spacing ?? 300;
            int tieDia = spec.TieSpec?.Diameter ?? 6;
            double halfL = spec.L / 2.0;
            double halfB = spec.B / 2.0;
            double wallT = spec.WallThickness;
            for (double x = -halfL + tieSpacing / 2.0; x < halfL; x += tieSpacing)
            {
                CadPrimitives.DrawLine(btr, tr, origin.X + x, origin.Y - halfB - wallT + inset,
                    origin.X + x, origin.Y - halfB - inset, "VHG-Thep3", colorIndex: 3);
                CadPrimitives.DrawLine(btr, tr, origin.X + x, origin.Y + halfB + inset,
                    origin.X + x, origin.Y + halfB + wallT - inset, "VHG-Thep3", colorIndex: 3);
            }
            for (double y = -halfB + tieSpacing / 2.0; y < halfB; y += tieSpacing)
            {
                CadPrimitives.DrawLine(btr, tr, origin.X - halfL - wallT + inset, origin.Y + y,
                    origin.X - halfL - inset, origin.Y + y, "VHG-Thep3", colorIndex: 3);
                CadPrimitives.DrawLine(btr, tr, origin.X + halfL + inset, origin.Y + y,
                    origin.X + halfL + wallT - inset, origin.Y + y, "VHG-Thep3", colorIndex: 3);
            }

            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db,
                "MẶT BẰNG LƯỚI THÉP TƯỜNG", "TỶ LỆ 1/30", origin.X,
                origin.Y + outer.MaxY + 600);
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId,
                origin.X + outer.MinX, origin.X + outer.MaxX,
                origin.Y + outer.MinY, origin.Y + outer.MinY - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId,
                origin.X + outer.MinX, origin.Y + outer.MinY, origin.Y + outer.MaxY,
                origin.X + outer.MinX - 250);

            if (tieDia <= 0)
                return;
        }

        // ====================================================================
        // VẼ MẶT BẰNG
        // ====================================================================
        private static void DrawPlanView(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            PlanGeometryModel plan,
            Point2D origin,
            ObjectId dimStyleId)
        {
            // 1. Viền bê tông lót (ngoài cùng)
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.PadRect.MinX, origin.Y + plan.PadRect.MinY,
                origin.X + plan.PadRect.MaxX, origin.Y + plan.PadRect.MaxY,
                layer: "VHG-BT");

            // 2. Viền móng đáy (nét khuất nếu có mở rộng)
            if (spec.BaseExpansion > 0.1)
            {
                CadPrimitives.DrawRectangle(
                    btr, tr,
                    origin.X + plan.BaseRect.MinX, origin.Y + plan.BaseRect.MinY,
                    origin.X + plan.BaseRect.MaxX, origin.Y + plan.BaseRect.MaxY,
                    layer: "VHG-Dashed");
            }

            // 3. Tường phủ bì ngoài
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.OuterWallRect.MinX, origin.Y + plan.OuterWallRect.MinY,
                origin.X + plan.OuterWallRect.MaxX, origin.Y + plan.OuterWallRect.MaxY,
                layer: "VHG-BT");

            // 4. Tường lòng trong
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.InnerChamberRect.MinX, origin.Y + plan.InnerChamberRect.MinY,
                origin.X + plan.InnerChamberRect.MaxX, origin.Y + plan.InnerChamberRect.MaxY,
                layer: "VHG-BT");

            // 5. Lỗ mở nắp ga trên trần
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.NeckOpeningRect.MinX, origin.Y + plan.NeckOpeningRect.MinY,
                origin.X + plan.NeckOpeningRect.MaxX, origin.Y + plan.NeckOpeningRect.MaxY,
                layer: "VHG-BT");

            // Chèn block nắp ga nếu có
            if (!string.IsNullOrEmpty(spec.CoverBlockName))
            {
                string blockMb = spec.CoverBlockName.EndsWith(".MB", StringComparison.OrdinalIgnoreCase)
                    ? spec.CoverBlockName
                    : $"{spec.CoverBlockName}.MB";

                CadPrimitives.InsertBlock(
                    blockTable, btr, tr, blockMb,
                    origin.X + plan.NeckCenter.X, origin.Y + plan.NeckCenter.Y,
                    scale: 0.3, layer: "VHG-Nap1");
            }

            // 6. Ống cống nhô ra
            foreach (var p in plan.Pipes)
            {
                // Đoạn cống ngoài tường
                CadPrimitives.DrawLine(
                    btr, tr,
                    origin.X + p.WallCutoutPoint1.X, origin.Y + p.WallCutoutPoint1.Y,
                    origin.X + p.PipeOuterEnd1.X, origin.Y + p.PipeOuterEnd1.Y,
                    layer: "VHG-Cong");

                CadPrimitives.DrawLine(
                    btr, tr,
                    origin.X + p.WallCutoutPoint2.X, origin.Y + p.WallCutoutPoint2.Y,
                    origin.X + p.PipeOuterEnd2.X, origin.Y + p.PipeOuterEnd2.Y,
                    layer: "VHG-Cong");

                CadPrimitives.DrawLine(
                    btr, tr,
                    origin.X + p.PipeOuterEnd1.X, origin.Y + p.PipeOuterEnd1.Y,
                    origin.X + p.PipeOuterEnd2.X, origin.Y + p.PipeOuterEnd2.Y,
                    layer: "VHG-Cong");

                // Đường tim cống
                CadPrimitives.DrawLine(
                    btr, tr,
                    origin.X + p.CenterLineStart.X, origin.Y + p.CenterLineStart.Y,
                    origin.X + p.CenterLineEnd.X, origin.Y + p.CenterLineEnd.Y,
                    layer: "VHG-Center");
            }

            // 7. Kích thước 2 tầng (Mặt bằng)
            double dimOffset = 300;
            double yBottom = origin.Y + plan.PadRect.MinY;
            double xLeft = origin.X + plan.PadRect.MinX;

            // Tầng 1: Kích thước lòng trong
            CadPrimitives.DrawDimNgang(
                btr, tr, dimStyleId,
                origin.X + plan.InnerChamberRect.MinX, origin.X + plan.InnerChamberRect.MaxX,
                origin.Y + plan.InnerChamberRect.MinY, yBottom - dimOffset);

            // Tầng 2: Kích thước phủ bì thân ga
            CadPrimitives.DrawDimNgang(
                btr, tr, dimStyleId,
                origin.X + plan.OuterWallRect.MinX, origin.X + plan.OuterWallRect.MaxX,
                origin.Y + plan.OuterWallRect.MinY, yBottom - dimOffset * 2);

            // Kích thước dọc bên trái
            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X + plan.InnerChamberRect.MinX,
                origin.Y + plan.InnerChamberRect.MinY, origin.Y + plan.InnerChamberRect.MaxY,
                xLeft - dimOffset);

            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X + plan.OuterWallRect.MinX,
                origin.Y + plan.OuterWallRect.MinY, origin.Y + plan.OuterWallRect.MaxY,
                xLeft - dimOffset * 2);

            // 8. Tiêu đề mặt bằng
            double yTitle = origin.Y + plan.PadRect.MaxY + 600;
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                $"MẶT BẰNG KẾT CẤU GA {spec.Name}", "TỶ LỆ 1/30",
                origin.X, yTitle);
        }

        // ====================================================================
        // VẼ MẶT CẮT (SECTION A-A / B-B)
        // ====================================================================
        private static void DrawSectionView(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            Point2D origin,
            ObjectId dimStyleId,
            bool showRebar = false)
        {
            // 1. Bê tông lót (Polygon + Hatch AR-CONC)
            var padShifted = new List<Point2D>();
            foreach (var p in sec.PadProfile.Points)
            {
                padShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
            }
            ObjectId padId = CadPrimitives.DrawPolyline(btr, tr, padShifted, true, "VHG-BT");
            CadPrimitives.CreateHatch(btr, tr, spec.HatchPad.PatternName, spec.HatchPad.Scale, "VHG-VL", padId);

            // 2. Đường bao ngoài BTCT
            var outerShifted = new List<Point2D>();
            foreach (var p in sec.OuterConcreteProfile.Points)
            {
                outerShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
            }
            ObjectId outerId = CadPrimitives.DrawPolyline(btr, tr, outerShifted, true, "VHG-BT");

            // 3. Đường bao khoang rỗng trong
            var innerShifted = new List<Point2D>();
            foreach (var p in sec.InnerVoidProfile.Points)
            {
                innerShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
            }
            ObjectId innerId = CadPrimitives.DrawPolyline(btr, tr, innerShifted, true, "VHG-BT");

            // Danh sách các lỗ cần trừ khỏi hatch bê tông (khoang trong + lỗ cống)
            var holes = new List<ObjectId> { innerId };

            // 4. Vẽ cống xuyên tường
            if (sec.RightPipe != null)
            {
                DrawSectionPipe(btr, tr, db, spec, sec.RightPipe, origin, holes);
            }
            if (sec.LeftPipe != null)
            {
                DrawSectionPipe(btr, tr, db, spec, sec.LeftPipe, origin, holes);
            }

            // 5. Đổ Hatch kép BTCT (AR-CONC + ANSI32)
            CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName, spec.HatchBase.Scale, "VHG-VL", outerId, holes);

            if (showRebar)
                DrawSectionRebar(btr, tr, spec, sec, origin);

            // 6. Nắp rời / Vữa đệm (nếu có)
            if (sec.BeddingMortarProfile != null)
            {
                var mortarShifted = new List<Point2D>();
                foreach (var p in sec.BeddingMortarProfile.Points)
                {
                    mortarShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
                }
                ObjectId mId = CadPrimitives.DrawPolyline(btr, tr, mortarShifted, true, "VHG-BT");
                CadPrimitives.CreateHatch(btr, tr, spec.HatchMortar.PatternName, spec.HatchMortar.Scale, "VHG-VL", mId);
            }

            if (sec.PrecastSlabProfile != null)
            {
                var slabShifted = new List<Point2D>();
                foreach (var p in sec.PrecastSlabProfile.Points)
                {
                    slabShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
                }
                ObjectId slabId = CadPrimitives.DrawPolyline(btr, tr, slabShifted, true, "VHG-BT");
                CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName, spec.HatchBase.Scale, "VHG-VL", slabId);
            }

            // 7. Kích thước 2 tầng (Mặt cắt)
            double dimOffset = 300;
            double yBottom = origin.Y + sec.ZPadBottom;
            double xLeft = origin.X - sec.HalfPad;

            // Kích thước ngang dưới đáy: Nhịp trong và Phủ bì
            CadPrimitives.DrawDimNgang(
                btr, tr, dimStyleId,
                origin.X - sec.HalfSpan, origin.X + sec.HalfSpan,
                origin.Y + sec.ZInsideBottom, yBottom - dimOffset);

            CadPrimitives.DrawDimNgang(
                btr, tr, dimStyleId,
                origin.X - sec.HalfOuter, origin.X + sec.HalfOuter,
                origin.Y + sec.ZInsideBottom, yBottom - dimOffset * 2);

            // Kích thước đứng bên trái: Dày lót, Dày đáy, Chiều cao trong, Dày trần
            var verticalDimensions = new (double Bottom, double Top)[]
            {
                (sec.ZPadBottom, sec.ZBaseBottom),
                (sec.ZBaseBottom, sec.ZInsideBottom),
                (sec.ZInsideBottom, sec.ZInsideTop),
                (sec.ZInsideTop, sec.ZTopSlabTop)
            };
            for (int index = 0; index < verticalDimensions.Length; index++)
            {
                var levels = verticalDimensions[index];
                if (Math.Abs(levels.Top - levels.Bottom) < 0.01)
                    continue;

                CadPrimitives.DrawDimDoc(
                    btr, tr, dimStyleId,
                    origin.X - sec.HalfOuter,
                    origin.Y + levels.Bottom, origin.Y + levels.Top,
                    xLeft - dimOffset * (index + 1));
            }

            // 8. Ký hiệu cao độ khống chế (Level Elevations)
            // Đáy trong ga (z = 0)
            CadPrimitives.DrawElevationSymbol(
                btr, tr, db,
                origin.X + sec.HalfSpan + 150, origin.Y + sec.ZInsideBottom,
                spec.BaseInvertElevation, labelPrefix: spec.LabelZBottom, isLeft: false);

            // Đáy móng BTCT (z = -F)
            CadPrimitives.DrawElevationSymbol(
                btr, tr, db,
                origin.X - sec.HalfBase - 150, origin.Y + sec.ZBaseBottom,
                spec.BaseInvertElevation - spec.BaseThickness / 1000.0, isLeft: true);

            // Đỉnh trần
            CadPrimitives.DrawElevationSymbol(
                btr, tr, db,
                origin.X + sec.HalfOuter + 150, origin.Y + sec.ZTopSlabTop,
                spec.BaseInvertElevation + (sec.ZTopSlabTop) / 1000.0, labelPrefix: spec.LabelZTop, isLeft: false);

            // 9. Tiêu đề mặt cắt
            double yTitle = origin.Y + sec.ZCoverTop + 750;
            string sectionTitle = showRebar
                ? $"MẶT CẮT CỐT THÉP {(sec.IsSectionA ? "A-A" : "B-B")}"
                : sec.Title;
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                sectionTitle, "TỶ LỆ 1/30",
                origin.X, yTitle);
        }

        private static void DrawSectionRebar(
            BlockTableRecord btr,
            Transaction tr,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            Point2D origin)
        {
            int dia = spec.WallOuterRebar?.Diameter ?? 10;
            double cover = spec.CoverWall + dia / 2.0;
            double zBottom = sec.ZBaseBottom + spec.CoverBottom + dia / 2.0;
            double zTopBase = sec.ZInsideBottom - spec.CoverBottom - dia / 2.0;
            double zBottomRoof = sec.ZInsideTop + spec.CoverTop + dia / 2.0;
            double zTopRoof = sec.ZTopSlabTop - spec.CoverTop - dia / 2.0;
            double wallBottom = sec.ZBaseBottom + cover;
            double wallTop = sec.ZTopSlabTop - cover;

            foreach (double side in new[] { -1.0, 1.0 })
            {
                double innerFace = side * (sec.HalfSpan + cover);
                double outerFace = side * (sec.HalfOuter - cover);
                CadPrimitives.DrawLine(btr, tr,
                    origin.X + innerFace, origin.Y + wallBottom,
                    origin.X + innerFace, origin.Y + wallTop, "VHG-Thep", colorIndex: 1);
                CadPrimitives.DrawLine(btr, tr,
                    origin.X + outerFace, origin.Y + wallBottom,
                    origin.X + outerFace, origin.Y + wallTop, "VHG-Thep2", colorIndex: 2);
            }

            double baseLeft = origin.X - sec.HalfOuter + spec.CoverBottom;
            double baseRight = origin.X + sec.HalfOuter - spec.CoverBottom;
            CadPrimitives.DrawLine(btr, tr, baseLeft, origin.Y + zBottom, baseRight, origin.Y + zBottom, "VHG-Thep", colorIndex: 1);
            CadPrimitives.DrawLine(btr, tr, baseLeft, origin.Y + zTopBase, baseRight, origin.Y + zTopBase, "VHG-Thep2", colorIndex: 2);

            double roofLeft = origin.X - sec.HalfOuter + spec.CoverTop;
            double roofRight = origin.X + sec.HalfOuter - spec.CoverTop;
            CadPrimitives.DrawLine(btr, tr, roofLeft, origin.Y + zBottomRoof, roofRight, origin.Y + zBottomRoof, "VHG-Thep2", colorIndex: 2);
            CadPrimitives.DrawLine(btr, tr, roofLeft, origin.Y + zTopRoof, roofRight, origin.Y + zTopRoof, "VHG-Thep", colorIndex: 1);
        }

        private static void DrawSectionPipe(
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            SectionPipeModel pipe,
            Point2D origin,
            List<ObjectId> holes)
        {
            // Hình chữ nhật lỗ cống trong tường
            ObjectId cutId = CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + pipe.CutoutRect.MinX, origin.Y + pipe.CutoutRect.MinY,
                origin.X + pipe.CutoutRect.MaxX, origin.Y + pipe.CutoutRect.MaxY,
                layer: "VHG-Cong");
            holes.Add(cutId);

            // Đoạn cống nhô ra ngoài tường
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + pipe.ExtensionRect.MinX, origin.Y + pipe.ExtensionRect.MinY,
                origin.X + pipe.ExtensionRect.MaxX, origin.Y + pipe.ExtensionRect.MaxY,
                layer: "VHG-Cong");

            // Đường tim cống
            CadPrimitives.DrawLine(
                btr, tr,
                origin.X + pipe.CenterLineStart.X, origin.Y + pipe.CenterLineStart.Y,
                origin.X + pipe.CenterLineEnd.X, origin.Y + pipe.CenterLineEnd.Y,
                layer: "VHG-Center");

            // Đặt mã cống ngoài đầu ống để không đè lên nét tường/lỗ cống.
            double textX = origin.X + (pipe.IsOnLeft
                ? pipe.ExtensionRect.MinX - 220
                : pipe.ExtensionRect.MaxX + 100);
            double textY = origin.Y + pipe.CutoutRect.MaxY + 100;
            CadPrimitives.DrawText(btr, tr, db, pipe.PipeCode, textX, textY, height: 90, layer: "VHG-Cong", colorIndex: 5);

            // Cao độ đáy cống
            double xElev = pipe.IsOnLeft
                ? origin.X + pipe.ExtensionRect.MinX - 650
                : origin.X + pipe.ExtensionRect.MaxX + 650;
            string levelLabel = pipe.Direction switch
            {
                WallDirection.T1 => spec.LabelZT1,
                WallDirection.T2 => spec.LabelZT2,
                WallDirection.T3 => spec.LabelZT3,
                WallDirection.T4 => spec.LabelZT4,
                _ => string.Empty
            };
            CadPrimitives.DrawElevationSymbol(
                btr, tr, db,
                xElev, origin.Y + pipe.InvertLevel,
                pipe.AbsoluteInvertElevation, labelPrefix: levelLabel, isLeft: pipe.IsOnLeft);
        }
    }
}
