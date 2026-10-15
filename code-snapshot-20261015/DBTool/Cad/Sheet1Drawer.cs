using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
            var views = Sheet1ViewLayout.Calculate(sheetOrigin, geom.SectionB.ZCoverTop, spec.Type);
            DrawPlanView(blockTable, btr, tr, db, spec, geom.Plan, geom.CatchBasin, views.Plan, dimStyleId, views.TopTitleY);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionB, geom.CatchBasin, views.SectionB, dimStyleId, views.TopTitleY);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionA, geom.CatchBasin, views.SectionARebar, dimStyleId, views.TopTitleY, showRebar: true);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionA, geom.CatchBasin, views.SectionA, dimStyleId, views.BottomTitleY);
            DrawWallRebarPlan(blockTable, btr, tr, db, spec, geom, views.WallRebarPlan, dimStyleId, views.BottomTitleY);
            DrawSectionView(blockTable, btr, tr, db, spec, geom.SectionB, geom.CatchBasin, views.SectionBRebar, dimStyleId, views.BottomTitleY, showRebar: true);
        }

        private static void DrawWallRebarPlan(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            Point2D origin,
            ObjectId dimStyleId,
            double titleY)
        {
            var outer = geom.Plan.OuterWallRect;
            var inner = geom.Plan.InnerChamberRect;
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + outer.MinX, origin.Y + outer.MinY,
                origin.X + outer.MaxX, origin.Y + outer.MaxY, "VHG-BT");
            CadPrimitives.DrawRectangle(btr, tr,
                origin.X + inner.MinX, origin.Y + inner.MinY,
                origin.X + inner.MaxX, origin.Y + inner.MaxY, "VHG-BT");

            double outerInset = spec.CoverWall + (spec.WallOuterRebar?.Diameter ?? 0) / 2.0;
            double innerInset = spec.CoverWall + (spec.WallInnerRebar?.Diameter ?? 0) / 2.0;
            if (spec.WallOuterRebar is { Diameter: > 0 })
            {
                CadPrimitives.DrawRectangle(btr, tr,
                    origin.X + outer.MinX + outerInset, origin.Y + outer.MinY + outerInset,
                    origin.X + outer.MaxX - outerInset, origin.Y + outer.MaxY - outerInset, "VHG-Thep");
            }
            if (spec.WallInnerRebar is { Diameter: > 0 })
            {
                CadPrimitives.DrawRectangle(btr, tr,
                    origin.X + inner.MinX - innerInset, origin.Y + inner.MinY - innerInset,
                    origin.X + inner.MaxX + innerInset, origin.Y + inner.MaxY + innerInset, "VHG-Thep2", colorIndex: 2);
            }

            double tieInset = spec.CoverWall + Math.Max(
                spec.WallOuterRebar?.Diameter ?? 0,
                spec.WallInnerRebar?.Diameter ?? 0) / 2.0;
            if (spec.TieSpec is { Diameter: > 0, Spacing: > 0 } tieSpec)
            {
                double tieSpacing = tieSpec.Spacing;
                double halfL = spec.L / 2.0;
                double halfB = spec.B / 2.0;
                double wallT = spec.WallThickness;
                for (double x = -halfL + tieSpacing / 2.0; x < halfL; x += tieSpacing)
                {
                    CadPrimitives.DrawLine(btr, tr, origin.X + x, origin.Y - halfB - wallT + tieInset,
                        origin.X + x, origin.Y - halfB - tieInset, "VHG-Thep3");
                    CadPrimitives.DrawLine(btr, tr, origin.X + x, origin.Y + halfB + tieInset,
                        origin.X + x, origin.Y + halfB + wallT - tieInset, "VHG-Thep3");
                }
                for (double y = -halfB + tieSpacing / 2.0; y < halfB; y += tieSpacing)
                {
                    CadPrimitives.DrawLine(btr, tr, origin.X - halfL - wallT + tieInset, origin.Y + y,
                        origin.X - halfL - tieInset, origin.Y + y, "VHG-Thep3");
                    CadPrimitives.DrawLine(btr, tr, origin.X + halfL + tieInset, origin.Y + y,
                        origin.X + halfL + wallT - tieInset, origin.Y + y, "VHG-Thep3");
                }
            }

            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db,
                "MẶT BẰNG LƯỚI THÉP TƯỜNG", "Tỷ lệ 1:30", origin.X,
                titleY);
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId,
                origin.X + outer.MinX, origin.X + outer.MaxX,
                origin.Y + outer.MinY, origin.Y + outer.MinY - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId,
                origin.X + outer.MinX, origin.Y + outer.MinY, origin.Y + outer.MaxY,
                origin.X + outer.MinX - 250);

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
            CatchBasinGeometryModel? catchBasin,
            Point2D origin,
            ObjectId dimStyleId,
            double titleY)
        {
            // DWG VHGpro thể hiện viền bản đáy bằng bốn nét VHG-BT; lớp lót không hiện ở mặt bằng.
            var baseRect = plan.BaseRect;
            CadPrimitives.DrawLine(btr, tr,
                origin.X + baseRect.MinX, origin.Y + baseRect.MinY,
                origin.X + baseRect.MaxX, origin.Y + baseRect.MinY, "VHG-BT");
            CadPrimitives.DrawLine(btr, tr,
                origin.X + baseRect.MaxX, origin.Y + baseRect.MinY,
                origin.X + baseRect.MaxX, origin.Y + baseRect.MaxY, "VHG-BT");
            CadPrimitives.DrawLine(btr, tr,
                origin.X + baseRect.MaxX, origin.Y + baseRect.MaxY,
                origin.X + baseRect.MinX, origin.Y + baseRect.MaxY, "VHG-BT");
            CadPrimitives.DrawLine(btr, tr,
                origin.X + baseRect.MinX, origin.Y + baseRect.MaxY,
                origin.X + baseRect.MinX, origin.Y + baseRect.MinY, "VHG-BT");

            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.OuterWallRect.MinX, origin.Y + plan.OuterWallRect.MinY,
                origin.X + plan.OuterWallRect.MaxX, origin.Y + plan.OuterWallRect.MaxY,
                layer: "VHG-BT");

            // Lòng tường và lỗ cổ bị trần che nên thể hiện bằng nét khuất.
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.InnerChamberRect.MinX, origin.Y + plan.InnerChamberRect.MinY,
                origin.X + plan.InnerChamberRect.MaxX, origin.Y + plan.InnerChamberRect.MaxY,
                layer: "VHG-Dashed");

            // Lỗ mở cổ ga dưới nắp.
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + plan.NeckOpeningRect.MinX, origin.Y + plan.NeckOpeningRect.MinY,
                origin.X + plan.NeckOpeningRect.MaxX, origin.Y + plan.NeckOpeningRect.MaxY,
                layer: "VHG-Dashed");

            if (catchBasin != null)
            {
                Rect2D outerCatch = catchBasin.OuterPlanRect;
                Rect2D innerCatch = catchBasin.HiddenInnerPlanRect;
                CadPrimitives.DrawRectangle(btr, tr,
                    origin.X + outerCatch.MinX, origin.Y + outerCatch.MinY,
                    origin.X + outerCatch.MaxX, origin.Y + outerCatch.MaxY, "VHG-BT");
                CadPrimitives.DrawRectangle(btr, tr,
                    origin.X + innerCatch.MinX, origin.Y + innerCatch.MinY,
                    origin.X + innerCatch.MaxX, origin.Y + innerCatch.MaxY, "VHG-Dashed");
                DrawCatchBasinGrille(blockTable, btr, tr, spec, catchBasin, origin);
            }

            // Chèn block nắp ga nếu có
            if (!string.IsNullOrEmpty(spec.CoverBlockName))
            {
                string blockMb = CoverBlockNameResolver.ForView(spec.CoverBlockName, ".MB");
                if (blockTable.Has(blockMb))
                {
                    BlockBounds2D bounds = CadPrimitives.GetBlockBounds2D(blockTable, tr, blockMb);
                    CoverBlockTransform transform = CoverBlockScaleResolver.Resolve(
                        spec.Type, blockMb, ".MB", spec.CoverHeight,
                        bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
                    CadPrimitives.InsertBlock(
                        blockTable, btr, tr, blockMb,
                        origin.X + plan.NeckCenter.X + transform.OffsetX,
                        origin.Y + plan.NeckCenter.Y + transform.OffsetY,
                        new Scale3d(transform.ScaleX, transform.ScaleY, transform.ScaleZ),
                        layer: "VHG-Nap1");
                }
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

            // Section markers are native template blocks. The measured G400-1
            // drawing inserts two MC1 and two MC2 at the plan perimeter.
            if (blockTable.Has("MC1"))
            {
                double markerX = origin.X - 25;
                CadPrimitives.InsertBlock(blockTable, btr, tr, "MC1",
                    markerX, origin.Y + plan.OuterWallRect.MaxY + 280,
                    scale: 0.3, layer: "VHG-Dashed");
                CadPrimitives.InsertBlock(blockTable, btr, tr, "MC1",
                    markerX, origin.Y + plan.OuterWallRect.MinY - 580,
                    scale: 0.3, layer: "VHG-Dashed");
            }
            if (blockTable.Has("MC2"))
            {
                CadPrimitives.InsertBlock(blockTable, btr, tr, "MC2",
                    origin.X + plan.BaseRect.MinX - 180, origin.Y,
                    scale: 0.3, layer: "VHG-Dashed");
                CadPrimitives.InsertBlock(blockTable, btr, tr, "MC2",
                    origin.X + plan.BaseRect.MaxX + 480, origin.Y,
                    scale: 0.3, layer: "VHG-Dashed");
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
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                $"MẶT BẰNG HỐ GA {spec.Name}", "Tỷ lệ 1:30",
                origin.X, titleY);
        }

        // ====================================================================
        // VẼ MẶT CẮT (SECTION A-A / B-B)
        // ====================================================================
        private static void DrawCatchBasinGrille(
            BlockTable blockTable, BlockTableRecord btr, Transaction tr,
            NormalizedManholeSpec spec, CatchBasinGeometryModel catchBasin, Point2D origin)
        {
            const string grilleBlock = "SCR960x530";
            if (!blockTable.Has(grilleBlock)) return;

            // In the VHGpro T2 sample the insertion is at the far inner face;
            // the local X axis points back toward the manhole. The 960-mm
            // grille direction is scaled by the configured opening width.
            double angle = catchBasin.Direction switch
            {
                WallDirection.T1 => 270,
                WallDirection.T2 => 180,
                WallDirection.T3 => 90,
                _ => 0
            };
            Point2D center = catchBasin.HiddenInnerPlanRect.Center;
            double x = catchBasin.IsSectionA ? center.X : catchBasin.InnerFar;
            double y = catchBasin.IsSectionA ? catchBasin.InnerFar : center.Y;
            ObjectId id = CadPrimitives.InsertBlock(blockTable, btr, tr, grilleBlock,
                origin.X + x, origin.Y + y,
                new Scale3d(spec.CatchBasinChamberLength / 500.0,
                    spec.CatchBasinHoleWidth / 850.0, 1), "VHG-VL");
            if (!id.IsNull)
                ((BlockReference)tr.GetObject(id, OpenMode.ForWrite)).Rotation = angle * Math.PI / 180.0;
        }

        private static void DrawCatchBasinSection(
            BlockTable blockTable, BlockTableRecord btr, Transaction tr,
            NormalizedManholeSpec spec, CatchBasinGeometryModel catchBasin,
            Point2D origin, bool showRebar)
        {
            double near = catchBasin.OuterStart;
            double farInner = catchBasin.InnerFar;
            double farOuter = catchBasin.OuterFar;
            double padFar = farOuter + (catchBasin.IsPositiveSide ? 1 : -1) * catchBasin.PadExpansion;

            var pad = new List<Point2D>
            {
                new(origin.X + near, origin.Y + catchBasin.PadBottomZ),
                new(origin.X + padFar, origin.Y + catchBasin.PadBottomZ),
                new(origin.X + padFar, origin.Y + catchBasin.BaseBottomZ),
                new(origin.X + near, origin.Y + catchBasin.BaseBottomZ)
            };
            ObjectId padId = CadPrimitives.DrawPolyline(btr, tr, pad, true, "VHG-BT");
            CadPrimitives.CreateHatch(btr, tr, spec.HatchPad.PatternName,
                spec.HatchPad.Scale, "VHG-VL", padId);

            var concrete = new List<Point2D>
            {
                new(origin.X + near, origin.Y + catchBasin.FloorZ),
                new(origin.X + farInner, origin.Y + catchBasin.FloorZ),
                new(origin.X + farInner, origin.Y + catchBasin.TopZ),
                new(origin.X + farOuter, origin.Y + catchBasin.TopZ),
                new(origin.X + farOuter, origin.Y + catchBasin.BaseBottomZ),
                new(origin.X + near, origin.Y + catchBasin.BaseBottomZ)
            };
            ObjectId concreteId = CadPrimitives.DrawPolyline(btr, tr, concrete, true, "VHG-BT");
            CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName,
                spec.HatchBase.Scale, "VHG-VL", concreteId);

            // VHGpro shows the opening against the neck/body as a hidden edge.
            CadPrimitives.DrawLine(btr, tr,
                origin.X + catchBasin.AttachmentInnerFace, origin.Y + catchBasin.TopZ,
                origin.X + catchBasin.AttachmentInnerFace, origin.Y + catchBasin.FloorZ,
                "VHG-Dashed");

            const string coverBlock = "NAPCUATHU1";
            if (blockTable.Has(coverBlock))
            {
                double x = catchBasin.IsPositiveSide
                    ? near + 40 : near - 40;
                ObjectId coverId = CadPrimitives.InsertBlock(blockTable, btr, tr,
                    coverBlock, origin.X + x, origin.Y + catchBasin.TopZ,
                    new Scale3d(catchBasin.IsPositiveSide ? 1 : -1, 1, 1),
                    "VHG-VL");
                if (showRebar && !coverId.IsNull)
                    ((BlockReference)tr.GetObject(coverId, OpenMode.ForWrite)).Layer = "VHG-BT";
            }
        }

        private static void DrawSectionView(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            CatchBasinGeometryModel? catchBasin,
            Point2D origin,
            ObjectId dimStyleId,
            double titleY,
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
                DrawSectionPipe(blockTable, btr, tr, db, spec, sec.RightPipe, origin, holes);
            }
            if (sec.LeftPipe != null)
            {
                DrawSectionPipe(blockTable, btr, tr, db, spec, sec.LeftPipe, origin, holes);
            }

            // Trong mặt cắt cốt thép mẫu, viền BTCT vẫn còn nhưng hatch thân/trần/cổ bị ẩn.
            if (!showRebar)
                CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName, spec.HatchBase.Scale, "VHG-VL", outerId, holes);

            if (showRebar)
                DrawSectionRebar(btr, tr, spec, sec, origin);

            // 6. Nắp rời / Vữa đệm (nếu có)
            foreach (var mortarProfile in sec.BeddingMortarProfiles)
            {
                var mortarShifted = new List<Point2D>();
                foreach (var p in mortarProfile.Points)
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
                if (!showRebar)
                    CadPrimitives.CreateHatch(btr, tr, spec.HatchBase.PatternName, spec.HatchBase.Scale, "VHG-VL", slabId);
            }

            foreach (var neckWallProfile in sec.NeckWallProfiles)
            {
                var neckShifted = new List<Point2D>();
                foreach (var p in neckWallProfile.Points)
                    neckShifted.Add(new Point2D(origin.X + p.X, origin.Y + p.Y));
                ObjectId neckId = CadPrimitives.DrawPolyline(btr, tr, neckShifted, true, "VHG-BT");
                if (!showRebar)
                    CadPrimitives.CreateHatch(btr, tr, spec.HatchNeck.PatternName, spec.HatchNeck.Scale, "VHG-VL", neckId);
            }

            DrawNeckCornerSteelSymbols(blockTable, btr, tr, spec, sec, catchBasin, origin);

            if (catchBasin != null && catchBasin.IsSectionA == sec.IsSectionA)
                DrawCatchBasinSection(blockTable, btr, tr, spec, catchBasin, origin, showRebar);

            // Nắp ga được thể hiện ở cả mặt bằng và hai mặt cắt theo tỷ lệ đo từ mẫu VHGpro.
            if (!string.IsNullOrWhiteSpace(spec.CoverBlockName) && spec.CoverHeight > 0)
            {
                string sectionSuffix = sec.IsSectionA ? ".AA" : ".BB";
                string coverSectionBlock = CoverBlockNameResolver.ForView(spec.CoverBlockName, sectionSuffix);
                if (blockTable.Has(coverSectionBlock))
                {
                    double openingCenter = (sec.NeckHoleXMin + sec.NeckHoleXMax) / 2.0;
                    BlockBounds2D bounds = CadPrimitives.GetBlockBounds2D(blockTable, tr, coverSectionBlock);
                    CoverBlockTransform transform = CoverBlockScaleResolver.Resolve(
                        spec.Type, coverSectionBlock, sectionSuffix, spec.CoverHeight,
                        bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
                    CadPrimitives.InsertBlock(
                        blockTable, btr, tr, coverSectionBlock,
                        origin.X + openingCenter + transform.OffsetX,
                        origin.Y + sec.ZCoverTop + transform.OffsetY,
                        new Scale3d(transform.ScaleX, transform.ScaleY, transform.ScaleZ),
                        layer: "VHG-Nap1");
                }
            }

            // 7. Kích thước 2 tầng (Mặt cắt)
            double dimOffset = 300;
            double yBottom = origin.Y + sec.ZPadBottom;
            double xLeft = origin.X - sec.HalfPad;

            // DWG mẫu dùng một chuỗi kích thước các bậc chân ga, rồi một
            // kích thước phủ bì lót ở hàng dưới. Bốn kích thước toàn nhịp
            // chồng lên ghi chú vật liệu và không phản ánh cách ghi của mẫu.
            double[] foundationEdges =
            {
                -sec.HalfPad, -sec.HalfBase, -sec.HalfOuter, -sec.HalfSpan,
                sec.HalfSpan, sec.HalfOuter, sec.HalfBase, sec.HalfPad
            };
            for (int edge = 0; edge < foundationEdges.Length - 1; edge++)
            {
                CadPrimitives.DrawDimNgang(
                    btr, tr, dimStyleId,
                    origin.X + foundationEdges[edge],
                    origin.X + foundationEdges[edge + 1],
                    origin.Y + sec.ZPadBottom,
                    yBottom - 180);
            }
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId,
                origin.X - sec.HalfPad, origin.X + sec.HalfPad,
                origin.Y + sec.ZPadBottom, yBottom - 360);

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
                blockTable, btr, tr, db,
                origin.X, origin.Y + sec.ZInsideBottom,
                spec.BaseInvertElevation, labelPrefix: spec.LabelZBottom, isLeft: false);

            // Cao độ đỉnh ga đặt tại mặt trên nắp, như ký hiệu Z2 trên PDF VHGpro.
            CadPrimitives.DrawElevationSymbol(
                blockTable, btr, tr, db,
                origin.X + (sec.NeckHoleXMin + sec.NeckHoleXMax) / 2.0,
                origin.Y + sec.ZCoverTop,
                spec.BaseInvertElevation + sec.ZCoverTop / 1000.0, labelPrefix: spec.LabelZTop, isLeft: false);

            // 9. Tiêu đề mặt cắt
            if (!showRebar)
            {
                if (sec.PrecastSlabProfile != null && spec.MortarThickness > 0)
                {
                    double mortarX = origin.X + (sec.IsSectionA ? 145 : 20);
                    double mortarY = origin.Y + sec.ZInsideTop - 115;
                    CadPrimitives.DrawText(btr, tr, db, spec.MaterialMortar,
                        mortarX, mortarY, height: 60, layer: "VHG-Text", colorIndex: 7,
                        horiz: TextHorizontalMode.TextLeft);
                    CadPrimitives.DrawText(btr, tr, db,
                        $"Dày {spec.MortarThickness:0}mm",
                        mortarX, mortarY - 110, height: 60, layer: "VHG-Text", colorIndex: 7,
                        horiz: TextHorizontalMode.TextLeft);
                }

                double noteX = origin.X - (sec.IsSectionA ? 293 : 230);
                double noteY = origin.Y + sec.ZPadBottom - 460;
                if (!string.IsNullOrWhiteSpace(spec.MaterialBase))
                    CadPrimitives.DrawText(btr, tr, db, spec.MaterialBase,
                        noteX, noteY, height: 60, layer: "VHG-Text", colorIndex: 7,
                        horiz: TextHorizontalMode.TextLeft);
                if (!string.IsNullOrWhiteSpace(spec.MaterialPad))
                    CadPrimitives.DrawText(btr, tr, db, spec.MaterialPad,
                        noteX, noteY - 120, height: 60, layer: "VHG-Text", colorIndex: 7,
                        horiz: TextHorizontalMode.TextLeft);
            }

            string sectionTitle = showRebar
                ? $"MẶT CẮT CỐT THÉP {(sec.IsSectionA ? "A-A" : "B-B")}"
                : sec.Title;
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                sectionTitle, "Tỷ lệ 1:30",
                origin.X, titleY);
        }

        private static void DrawNeckCornerSteelSymbols(
            BlockTable blockTable, BlockTableRecord btr, Transaction tr,
            NormalizedManholeSpec spec, SectionGeometryModel sec,
            CatchBasinGeometryModel? catchBasin, Point2D origin)
        {
            if (spec.NeckHeight <= 0 || spec.NeckWallThickness <= 0 ||
                !blockTable.Has("ThepGocLoi") || !blockTable.Has("ThepGocLom"))
                return;

            bool leftOpenToCatch = catchBasin is { IsAtNeck: true, IsPositiveSide: false } &&
                catchBasin.IsSectionA == sec.IsSectionA;
            bool rightOpenToCatch = catchBasin is { IsAtNeck: true, IsPositiveSide: true } &&
                catchBasin.IsSectionA == sec.IsSectionA;

            void InsertCorner(string name, double x, double z, double rotationDegrees)
            {
                ObjectId id = CadPrimitives.InsertBlock(blockTable, btr, tr, name,
                    origin.X + x, origin.Y + z, scale: 0.6, layer: "VHG-VL");
                if (!id.IsNull)
                    ((BlockReference)tr.GetObject(id, OpenMode.ForWrite)).Rotation =
                        rotationDegrees * Math.PI / 180.0;
            }

            if (!leftOpenToCatch)
            {
                double outside = sec.NeckHoleXMin - spec.NeckWallThickness + 50;
                InsertCorner("ThepGocLoi", outside, sec.ZNeckTop, 180);
                InsertCorner("ThepGocLom", outside, sec.ZNeckTop - 50, 0);
                InsertCorner("ThepGocLoi", sec.NeckHoleXMin, sec.ZNeckTop - 50, 180);
            }
            if (!rightOpenToCatch)
            {
                double outside = sec.NeckHoleXMax + spec.NeckWallThickness - 50;
                InsertCorner("ThepGocLoi", outside, sec.ZNeckTop, 270);
                InsertCorner("ThepGocLom", outside, sec.ZNeckTop - 50, 90);
                InsertCorner("ThepGocLoi", sec.NeckHoleXMax, sec.ZNeckTop - 50, 270);
            }
        }

        private static void DrawSectionRebar(
            BlockTableRecord btr,
            Transaction tr,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            Point2D origin)
        {
            double baseLeft = origin.X - sec.HalfOuter + spec.CoverBottom;
            double baseRight = origin.X + sec.HalfOuter - spec.CoverBottom;
            if (spec.BaseBottomRebar is { Diameter: > 0 } baseBottomRebar)
            {
                double zBottom = sec.ZBaseBottom + spec.CoverBottom + baseBottomRebar.Diameter / 2.0;
                CadPrimitives.DrawLine(btr, tr, baseLeft, origin.Y + zBottom, baseRight, origin.Y + zBottom, "VHG-Thep", colorIndex: 1);
            }
            if (spec.BaseTopRebar is { Diameter: > 0 } baseTopRebar)
            {
                double zTopBase = sec.ZInsideBottom - spec.CoverBottom - baseTopRebar.Diameter / 2.0;
                CadPrimitives.DrawLine(btr, tr, baseLeft, origin.Y + zTopBase, baseRight, origin.Y + zTopBase, "VHG-Thep2", colorIndex: 2);
            }

            if (spec.TopBottomRebar is { Diameter: > 0 } topBottomRebar)
            {
                double zBottomRoof = sec.ZInsideTop + spec.CoverTop + topBottomRebar.Diameter / 2.0;
                DrawSectionRoofBars(btr, tr, spec, sec, origin, zBottomRoof,
                    topBottomRebar.Diameter, "VHG-Thep2", 2);
            }
            if (spec.TopTopRebar is { Diameter: > 0 } topTopRebar)
            {
                double zTopRoof = sec.ZTopSlabTop - spec.CoverTop - topTopRebar.Diameter / 2.0;
                DrawSectionRoofBars(btr, tr, spec, sec, origin, zTopRoof,
                    topTopRebar.Diameter, "VHG-Thep", 1);
            }

            if (spec.WallInnerRebar is { Diameter: > 0 } wallInnerRebar)
            {
                double cover = spec.CoverWall + wallInnerRebar.Diameter / 2.0;
                foreach (double side in new[] { -1.0, 1.0 })
                {
                    double innerFace = side * (sec.HalfSpan + cover);
                    DrawSectionWallBar(btr, tr, spec, sec, origin, innerFace,
                        side < 0 ? sec.LeftPipe : sec.RightPipe,
                        wallInnerRebar.Diameter, "VHG-Thep2", 2);
                }
            }
            if (spec.WallOuterRebar is { Diameter: > 0 } wallOuterRebar)
            {
                double cover = spec.CoverWall + wallOuterRebar.Diameter / 2.0;
                foreach (double side in new[] { -1.0, 1.0 })
                {
                    double outerFace = side * (sec.HalfOuter - cover);
                    DrawSectionWallBar(btr, tr, spec, sec, origin, outerFace,
                        side < 0 ? sec.LeftPipe : sec.RightPipe,
                        wallOuterRebar.Diameter, "VHG-Thep", 1);
                }
            }
        }

        private static void DrawSectionRoofBars(
            BlockTableRecord btr,
            Transaction tr,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            Point2D origin,
            double z,
            int diameter,
            string layer,
            short colorIndex)
        {
            double clearance = spec.CoverTop + diameter / 2.0;
            double start = -sec.HalfOuter + clearance;
            double end = sec.HalfOuter - clearance;
            IReadOnlyList<Interval1D> retained = sec.NeckHoleXMax - sec.NeckHoleXMin > 0.01
                ? RectangleOpeningClipper.SubtractFromHorizontal(
                    start, end, 0,
                    new Rect2D(sec.NeckHoleXMin, -1, sec.NeckHoleXMax, 1),
                    clearance)
                : new[] { new Interval1D(start, end) };
            foreach (var segment in retained)
            {
                if (segment.Length > 0.01)
                    CadPrimitives.DrawLine(btr, tr,
                        origin.X + segment.Start, origin.Y + z,
                        origin.X + segment.End, origin.Y + z, layer, colorIndex);
            }
        }

        private static void DrawSectionWallBar(
            BlockTableRecord btr,
            Transaction tr,
            NormalizedManholeSpec spec,
            SectionGeometryModel sec,
            Point2D origin,
            double x,
            SectionPipeModel? pipe,
            int diameter,
            string layer,
            short colorIndex)
        {
            // Same embedded start/end levels as RebarEngine.AddWallFaceSegments.
            double start = sec.ZBaseBottom + spec.CoverBottom;
            double end = spec.Type == ManholeType.GaThamLienKhoi
                ? sec.ZTopSlabTop - spec.CoverTop
                : sec.ZInsideTop - spec.CoverWall;
            IReadOnlyList<Interval1D> retained = pipe is not null
                ? RectangleOpeningClipper.SubtractFromVertical(
                    start, end, 0,
                    new Rect2D(-1, pipe.CutoutBottomZ, 1, pipe.CutoutTopZ),
                    spec.CoverPipe + diameter / 2.0)
                : new[] { new Interval1D(start, end) };
            foreach (var segment in retained)
            {
                if (segment.Length > 0.01)
                    CadPrimitives.DrawLine(btr, tr,
                        origin.X + x, origin.Y + segment.Start,
                        origin.X + x, origin.Y + segment.End,
                        layer, colorIndex);
            }
        }

        private static void DrawSectionPipe(
            BlockTable blockTable,
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
            CadPrimitives.DrawText(btr, tr, db, pipe.PipeCode, textX, textY, height: 90, layer: "VHG-Text", colorIndex: 7);

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
                blockTable, btr, tr, db,
                xElev, origin.Y + pipe.InvertLevel,
                pipe.AbsoluteInvertElevation, labelPrefix: levelLabel, isLeft: pipe.IsOnLeft);
        }
    }
}
