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
            // Điểm gốc các khung nhìn (Viewports) trong tờ A3 (12600 x 8910 mm):
            // Khung nhìn 1: Mặt bằng (Nửa trái, trên) -> Tâm tại (sheetOrigin.X + 3200, sheetOrigin.Y - 3200)
            // Khung nhìn 2: Mặt cắt A-A (Nửa phải, trên) -> Tâm tại (sheetOrigin.X + 8800, sheetOrigin.Y - 2600)
            // Khung nhìn 3: Mặt cắt B-B (Nửa phải, dưới) -> Tâm tại (sheetOrigin.X + 8800, sheetOrigin.Y - 6200)

            Point2D planOrigin = new(sheetOrigin.X + 3200, sheetOrigin.Y - 3400);
            Point2D secAOrigin = new(sheetOrigin.X + 8800, sheetOrigin.Y - 2600);
            Point2D secBOrigin = new(sheetOrigin.X + 8800, sheetOrigin.Y - 6400);

            // 1. VẼ MẶT BẰNG (PLAN)
            DrawPlanView(blockTable, btr, tr, db, spec, geom.Plan, planOrigin, dimStyleId);

            // 2. VẼ MẶT CẮT A-A
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionA, secAOrigin, dimStyleId);

            // 3. VẼ MẶT CẮT B-B
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionB, secBOrigin, dimStyleId);
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
            ObjectId dimStyleId)
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
            CadPrimitives.CreateHatch(btr, tr, "AR-CONC", 1, "VHG-VL", outerId, holes);
            CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName, spec.HatchBase.Scale, "VHG-VL", outerId, holes);

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
                CadPrimitives.CreateHatch(btr, tr, "AR-CONC", 1, "VHG-VL", slabId);
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
            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X - sec.HalfOuter,
                origin.Y + sec.ZPadBottom, origin.Y + sec.ZBaseBottom,
                xLeft - dimOffset);

            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X - sec.HalfOuter,
                origin.Y + sec.ZBaseBottom, origin.Y + sec.ZInsideBottom,
                xLeft - dimOffset);

            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X - sec.HalfOuter,
                origin.Y + sec.ZInsideBottom, origin.Y + sec.ZInsideTop,
                xLeft - dimOffset);

            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X - sec.HalfOuter,
                origin.Y + sec.ZInsideTop, origin.Y + sec.ZTopSlabTop,
                xLeft - dimOffset);

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
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                sec.Title, "TỶ LỆ 1/30",
                origin.X, yTitle);
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

            // Nhãn cống (ví dụ "D200")
            double textX = (origin.X + pipe.CutoutRect.MinX + origin.X + pipe.CutoutRect.MaxX) / 2.0;
            double textY = origin.Y + pipe.CutoutRect.MaxY + 60;
            CadPrimitives.DrawText(btr, tr, db, pipe.PipeCode, textX, textY, height: 90, layer: "VHG-Cong", colorIndex: 5);

            // Cao độ đáy cống
            double xElev = pipe.IsOnLeft ? (origin.X + pipe.ExtensionRect.MinX - 50) : (origin.X + pipe.ExtensionRect.MaxX + 50);
            CadPrimitives.DrawElevationSymbol(
                btr, tr, db,
                xElev, origin.Y + pipe.InvertLevel,
                pipe.AbsoluteInvertElevation, isLeft: pipe.IsOnLeft);
        }
    }
}
