using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ 3: Bố trí cốt thép Tường T3, T4 và Bản trần ga
    /// </summary>
    public class Sheet3Drawer
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
            // Sáu view: T3/T4 ngoài-trong thành hai cột trái; hai lớp trần ở cột phải.
            Point2D wallT3OuterOrigin = new(sheetOrigin.X + 2200, sheetOrigin.Y - 2600);
            Point2D wallT3InnerOrigin = new(sheetOrigin.X + 5100, sheetOrigin.Y - 2600);
            Point2D wallT4OuterOrigin = new(sheetOrigin.X + 2200, sheetOrigin.Y - 6200);
            Point2D wallT4InnerOrigin = new(sheetOrigin.X + 5100, sheetOrigin.Y - 6200);
            Point2D topBotOrigin = new(sheetOrigin.X + 9300, sheetOrigin.Y - 2600);
            Point2D topTopOrigin = new(sheetOrigin.X + 9300, sheetOrigin.Y - 6200);

            // 1-2. Hai mặt T3
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T3, spec.PipeT3, wallT3OuterOrigin, isOuter: true, dimStyleId);
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T3, spec.PipeT3, wallT3InnerOrigin, isOuter: false, dimStyleId);

            // 3-4. Hai mặt T4
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T4, spec.PipeT4, wallT4OuterOrigin, isOuter: true, dimStyleId);
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T4, spec.PipeT4, wallT4InnerOrigin, isOuter: false, dimStyleId);

            // 5. Bản trần - Lớp dưới
            DrawTopSlabMesh(blockTable, btr, tr, db, spec, geom, topBotOrigin, isTop: false, dimStyleId);

            // 6. Bản trần - Lớp trên
            DrawTopSlabMesh(blockTable, btr, tr, db, spec, geom, topTopOrigin, isTop: true, dimStyleId);
        }

        private static void DrawWallMesh(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            WallDirection dir,
            PipeConnectionSpec? pipe,
            Point2D origin,
            bool isOuter,
            ObjectId dimStyleId)
        {
            WallMeshDrawer.Draw(blockTable, btr, tr, db, spec, dir, pipe, origin, isOuter, dimStyleId);
        }

        private static void DrawTopSlabMesh(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            Point2D origin,
            bool isTop,
            ObjectId dimStyleId)
        {
            double slabW_X = spec.L + 2 * spec.WallThickness;
            double slabW_Y = spec.B + 2 * spec.WallThickness;
            double halfX = slabW_X / 2.0;
            double halfY = slabW_Y / 2.0;
            double c = spec.CoverTop;

            // Đường bao viền bản trần
            CadPrimitives.DrawRectangle(btr, tr, origin.X - halfX, origin.Y - halfY, origin.X + halfX, origin.Y + halfY, "VHG-BT");

            // Lỗ mở nắp ga
            var neckRect = geom.Plan.NeckOpeningRect;
            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X + neckRect.MinX, origin.Y + neckRect.MinY,
                origin.X + neckRect.MaxX, origin.Y + neckRect.MaxY,
                layer: "VHG-BT");

            RebarSpec? rebarSpec = isTop ? spec.TopTopRebar : spec.TopBottomRebar;
            int dia = rebarSpec?.Diameter ?? 10;
            double pitch = rebarSpec?.Spacing ?? 150;

            var distX = RebarEngine.DistributeBars(slabW_Y, c, dia, pitch, spec.RebarDistributionMode);
            var distY = RebarEngine.DistributeBars(slabW_X, c, dia, pitch, spec.RebarDistributionMode);
            double lineClearance = c + dia / 2.0;
            double xStart = -halfX + lineClearance;
            double xEnd = halfX - lineClearance;
            double yStart = -halfY + lineClearance;
            double yEnd = halfY - lineClearance;

            // Vẽ các thanh ngang phương X
            foreach (double yOff in distX.Offsets)
            {
                double yLine = origin.Y - halfY + yOff;
                var segments = RectangleOpeningClipper.SubtractFromHorizontal(
                    xStart, xEnd, yLine - origin.Y, neckRect, lineClearance);
                foreach (var segment in segments)
                {
                    if (segment.Length > 0.01)
                        CadPrimitives.DrawLine(btr, tr, origin.X + segment.Start, yLine, origin.X + segment.End, yLine, "VHG-Thep", colorIndex: 1);
                }
            }

            // Vẽ các thanh dọc phương Y
            foreach (double xOff in distY.Offsets)
            {
                double xLine = origin.X - halfX + xOff;
                var segments = RectangleOpeningClipper.SubtractFromVertical(
                    yStart, yEnd, xLine - origin.X, neckRect, lineClearance);
                foreach (var segment in segments)
                {
                    if (segment.Length > 0.01)
                        CadPrimitives.DrawLine(btr, tr, xLine, origin.Y + segment.Start, xLine, origin.Y + segment.End, "VHG-Thep2", colorIndex: 2);
                }
            }

            // Tiêu đề
            string title = isTop ? "LƯỚI THÉP BẢN TRẦN - LỚP TRÊN" : "LƯỚI THÉP BẢN TRẦN - LỚP DƯỚI";
            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db, title, "TỶ LỆ 1/30", origin.X, origin.Y + halfY + 450);

            // Kích thước
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfX, origin.X + halfX, origin.Y - halfY, origin.Y - halfY - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId, origin.X - halfX, origin.Y - halfY, origin.Y + halfY, origin.X - halfX - 250);
        }
    }
}
