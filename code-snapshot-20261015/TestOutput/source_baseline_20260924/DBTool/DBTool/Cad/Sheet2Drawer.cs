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
            // 4 khung nhìn trên tờ A3:
            // 1. Bản đáy lớp dưới: (X + 3200, Y - 2600)
            // 2. Bản đáy lớp trên: (X + 3200, Y - 6200)
            // 3. Tường T1:         (X + 8800, Y - 2600)
            // 4. Tường T2:         (X + 8800, Y - 6200)

            Point2D baseBotOrigin = new(sheetOrigin.X + 3200, sheetOrigin.Y - 2600);
            Point2D baseTopOrigin = new(sheetOrigin.X + 3200, sheetOrigin.Y - 6200);
            Point2D wallT1Origin  = new(sheetOrigin.X + 8800, sheetOrigin.Y - 2600);
            Point2D wallT2Origin  = new(sheetOrigin.X + 8800, sheetOrigin.Y - 6200);

            // 1. Bản đáy - Lớp dưới
            DrawBaseMesh(blockTable, btr, tr, db, spec, geom, baseBotOrigin, isTop: false, dimStyleId);

            // 2. Bản đáy - Lớp trên
            DrawBaseMesh(blockTable, btr, tr, db, spec, geom, baseTopOrigin, isTop: true, dimStyleId);

            // 3. Tường T1 (có cắt cống T1)
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T1, spec.PipeT1, wallT1Origin, dimStyleId);

            // 4. Tường T2 (có cắt cống T2)
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T2, spec.PipeT2, wallT2Origin, dimStyleId);
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
            ObjectId dimStyleId)
        {
            double wallSpan = (dir == WallDirection.T1 || dir == WallDirection.T3) ? spec.L : spec.B;
            double wallHeight = spec.H;
            double halfSpan = wallSpan / 2.0;
            double c = spec.CoverWall;

            // Đường bao viền tường
            CadPrimitives.DrawRectangle(btr, tr, origin.X - halfSpan, origin.Y, origin.X + halfSpan, origin.Y + wallHeight, "VHG-BT");

            int dia = spec.WallOuterRebar?.Diameter ?? 10;
            double pitchVert = spec.WallOuterRebar?.Spacing ?? 150;
            double pitchHoriz = pitchVert + spec.WallHorizSpacingDelta;

            var distVert = RebarEngine.DistributeBars(wallSpan, c, dia, pitchVert, spec.RebarDistributionMode);
            var distHoriz = RebarEngine.DistributeBars(wallHeight, c, dia, pitchHoriz, spec.RebarDistributionMode);

            double rCut = pipe != null ? (pipe.CutoutDia / 2.0) : 0;
            double pX = pipe != null ? (origin.X + pipe.OffsetFromCenter) : 0;
            double pY = pipe != null ? (origin.Y + pipe.InvertLevelFromBottom + pipe.InnerDia / 2.0) : 0;

            // Nếu có cống, vẽ vòng tròn lỗ khoét cống
            if (pipe != null)
            {
                CadPrimitives.DrawRectangle(
                    btr, tr,
                    pX - rCut, pY - rCut,
                    pX + rCut, pY + rCut,
                    layer: "VHG-Cong");
            }

            // Vẽ các thanh đứng
            foreach (double xOff in distVert.Offsets)
            {
                double xLine = origin.X - halfSpan + xOff;
                if (pipe != null && Math.Abs(xLine - pX) < rCut)
                {
                    // Thanh bị cắt: vẽ 2 đoạn trên và dưới lỗ cống
                    double yBotCut = pY - rCut;
                    double yTopCut = pY + rCut;

                    if (yBotCut > origin.Y + c)
                    {
                        CadPrimitives.DrawLine(btr, tr, xLine, origin.Y + c, xLine, yBotCut, "VHG-Thep", colorIndex: 1);
                    }
                    if (origin.Y + wallHeight - c > yTopCut)
                    {
                        CadPrimitives.DrawLine(btr, tr, xLine, yTopCut, xLine, origin.Y + wallHeight - c, "VHG-Thep", colorIndex: 1);
                    }
                }
                else
                {
                    CadPrimitives.DrawLine(btr, tr, xLine, origin.Y + c, xLine, origin.Y + wallHeight - c, "VHG-Thep", colorIndex: 1);
                }
            }

            // Vẽ các thanh ngang
            foreach (double yOff in distHoriz.Offsets)
            {
                double yLine = origin.Y + yOff;
                if (pipe != null && Math.Abs(yLine - pY) < rCut)
                {
                    // Thanh bị cắt: vẽ 2 đoạn trái và phải lỗ cống
                    double xLeftCut = pX - rCut;
                    double xRightCut = pX + rCut;

                    if (xLeftCut > origin.X - halfSpan + c)
                    {
                        CadPrimitives.DrawLine(btr, tr, origin.X - halfSpan + c, yLine, xLeftCut, yLine, "VHG-Thep2", colorIndex: 2);
                    }
                    if (origin.X + halfSpan - c > xRightCut)
                    {
                        CadPrimitives.DrawLine(btr, tr, xRightCut, yLine, origin.X + halfSpan - c, yLine, "VHG-Thep2", colorIndex: 2);
                    }
                }
                else
                {
                    CadPrimitives.DrawLine(btr, tr, origin.X - halfSpan + c, yLine, origin.X + halfSpan - c, yLine, "VHG-Thep2", colorIndex: 2);
                }
            }

            // Tiêu đề tường
            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db, $"LƯỚI THÉP TƯỜNG {dir}", "TỶ LỆ 1/30", origin.X, origin.Y + wallHeight + 450);

            // Kích thước
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfSpan, origin.X + halfSpan, origin.Y, origin.Y - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId, origin.X - halfSpan, origin.Y, origin.Y + wallHeight, origin.X - halfSpan - 250);
        }
    }
}
