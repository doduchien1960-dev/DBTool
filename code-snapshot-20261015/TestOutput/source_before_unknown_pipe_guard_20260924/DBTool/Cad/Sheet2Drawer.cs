using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ 2: Bố trí cốt thép Bản đáy và Tường T1, T2
    /// </summary>
    public class Sheet2Drawer
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
            // Sáu view: hai lưới đáy ở cột trái, hai mặt T1 ở cột giữa,
            // hai mặt T2 ở cột phải.
            Point2D baseBotOrigin = new(sheetOrigin.X + 2500, sheetOrigin.Y - 2600);
            Point2D baseTopOrigin = new(sheetOrigin.X + 2500, sheetOrigin.Y - 6200);
            Point2D wallT1OuterOrigin = new(sheetOrigin.X + 6900, sheetOrigin.Y - 2600);
            Point2D wallT1InnerOrigin = new(sheetOrigin.X + 10300, sheetOrigin.Y - 2600);
            Point2D wallT2OuterOrigin = new(sheetOrigin.X + 6900, sheetOrigin.Y - 6200);
            Point2D wallT2InnerOrigin = new(sheetOrigin.X + 10300, sheetOrigin.Y - 6200);

            // 1. Bản đáy - Lớp dưới
            DrawBaseMesh(blockTable, btr, tr, db, spec, geom, baseBotOrigin, isTop: false, dimStyleId);

            // 2. Bản đáy - Lớp trên
            DrawBaseMesh(blockTable, btr, tr, db, spec, geom, baseTopOrigin, isTop: true, dimStyleId);

            // 3-4. Hai mặt lưới của T1 (có cắt cống T1)
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T1, spec.PipeT1, wallT1OuterOrigin, isOuter: true, dimStyleId);
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T1, spec.PipeT1, wallT1InnerOrigin, isOuter: false, dimStyleId);

            // 5-6. Hai mặt lưới của T2 (có cắt cống T2)
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T2, spec.PipeT2, wallT2OuterOrigin, isOuter: true, dimStyleId);
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T2, spec.PipeT2, wallT2InnerOrigin, isOuter: false, dimStyleId);
        }

        private static void DrawBaseMesh(
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
            double wX = geom.Plan.BaseRect.Width;
            double wY = geom.Plan.BaseRect.Height;
            double halfX = wX / 2.0;
            double halfY = wY / 2.0;
            double c = spec.CoverBottom;

            // Đường bao viền bản đáy
            CadPrimitives.DrawRectangle(btr, tr, origin.X - halfX, origin.Y - halfY, origin.X + halfX, origin.Y + halfY, "VHG-BT");

            // Rải thanh thép phương X và phương Y
            var rebarSpec = isTop ? spec.BaseTopRebar : spec.BaseBottomRebar;
            int dia = rebarSpec?.Diameter ?? 10;
            double pitch = rebarSpec?.Spacing ?? 150;

            var distX = RebarEngine.DistributeBars(wY, c, dia, pitch, spec.RebarDistributionMode);
            var distY = RebarEngine.DistributeBars(wX, c, dia, pitch, spec.RebarDistributionMode);

            // Vẽ các thanh thép phương X (nằm ngang)
            foreach (double yOff in distX.Offsets)
            {
                double yLine = origin.Y - halfY + yOff;
                CadPrimitives.DrawLine(btr, tr, origin.X - halfX + c, yLine, origin.X + halfX - c, yLine, "VHG-Thep", colorIndex: 1);
            }

            // Vẽ các thanh thép phương Y (thẳng đứng)
            foreach (double xOff in distY.Offsets)
            {
                double xLine = origin.X - halfX + xOff;
                CadPrimitives.DrawLine(btr, tr, xLine, origin.Y - halfY + c, xLine, origin.Y + halfY - c, "VHG-Thep2", colorIndex: 2);
            }

            // Tiêu đề lưới
            string title = isTop ? "LƯỚI THÉP BẢN ĐÁY - LỚP TRÊN" : "LƯỚI THÉP BẢN ĐÁY - LỚP DƯỚI";
            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db, title, "TỶ LỆ 1/30", origin.X, origin.Y + halfY + 450);

            // Kích thước
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfX, origin.X + halfX, origin.Y - halfY, origin.Y - halfY - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId, origin.X - halfX, origin.Y - halfY, origin.Y + halfY, origin.X - halfX - 250);
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
    }
}
