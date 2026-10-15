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
            // 4 khung nhìn trên tờ A3:
            // 1. Tường T3:         (X + 3200, Y - 2600)
            // 2. Tường T4:         (X + 3200, Y - 6200)
            // 3. Bản trần lớp dưới:(X + 8800, Y - 2600)
            // 4. Bản trần lớp trên:(X + 8800, Y - 6200)

            Point2D wallT3Origin = new(sheetOrigin.X + 3200, sheetOrigin.Y - 2600);
            Point2D wallT4Origin = new(sheetOrigin.X + 3200, sheetOrigin.Y - 6200);
            Point2D topBotOrigin = new(sheetOrigin.X + 8800, sheetOrigin.Y - 2600);
            Point2D topTopOrigin = new(sheetOrigin.X + 8800, sheetOrigin.Y - 6200);

            // 1. Tường T3
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T3, spec.PipeT3, wallT3Origin, dimStyleId);

            // 2. Tường T4
            DrawWallMesh(blockTable, btr, tr, db, spec, geom, WallDirection.T4, spec.PipeT4, wallT4Origin, dimStyleId);

            // 3. Bản trần - Lớp dưới
            DrawTopSlabMesh(blockTable, btr, tr, db, spec, geom, topBotOrigin, isTop: false, dimStyleId);

            // 4. Bản trần - Lớp trên
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

            if (pipe != null)
            {
                CadPrimitives.DrawRectangle(btr, tr, pX - rCut, pY - rCut, pX + rCut, pY + rCut, "VHG-Cong");
            }

            // Vẽ các thanh đứng
            foreach (double xOff in distVert.Offsets)
            {
                double xLine = origin.X - halfSpan + xOff;
                if (pipe != null && Math.Abs(xLine - pX) < rCut)
                {
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

            // Tiêu đề
            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db, $"LƯỚI THÉP TƯỜNG {dir}", "TỶ LỆ 1/30", origin.X, origin.Y + wallHeight + 450);

            // Kích thước
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfSpan, origin.X + halfSpan, origin.Y, origin.Y - 250);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId, origin.X - halfSpan, origin.Y, origin.Y + wallHeight, origin.X - halfSpan - 250);
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

            int dia = spec.TopTopRebar?.Diameter ?? 10;
            double pitch = spec.TopTopRebar?.Spacing ?? 150;

            var distX = RebarEngine.DistributeBars(slabW_Y, c, dia, pitch, spec.RebarDistributionMode);
            var distY = RebarEngine.DistributeBars(slabW_X, c, dia, pitch, spec.RebarDistributionMode);

            // Vẽ các thanh ngang phương X
            foreach (double yOff in distX.Offsets)
            {
                double yLine = origin.Y - halfY + yOff;
                if (yLine > origin.Y + neckRect.MinY && yLine < origin.Y + neckRect.MaxY)
                {
                    // Cắt ngang qua lỗ mở
                    double xLeftCut = origin.X + neckRect.MinX;
                    double xRightCut = origin.X + neckRect.MaxX;

                    if (xLeftCut > origin.X - halfX + c)
                    {
                        CadPrimitives.DrawLine(btr, tr, origin.X - halfX + c, yLine, xLeftCut, yLine, "VHG-Thep", colorIndex: 1);
                    }
                    if (origin.X + halfX - c > xRightCut)
                    {
                        CadPrimitives.DrawLine(btr, tr, xRightCut, yLine, origin.X + halfX - c, yLine, "VHG-Thep", colorIndex: 1);
                    }
                }
                else
                {
                    CadPrimitives.DrawLine(btr, tr, origin.X - halfX + c, yLine, origin.X + halfX - c, yLine, "VHG-Thep", colorIndex: 1);
                }
            }

            // Vẽ các thanh dọc phương Y
            foreach (double xOff in distY.Offsets)
            {
                double xLine = origin.X - halfX + xOff;
                if (xLine > origin.X + neckRect.MinX && xLine < origin.X + neckRect.MaxX)
                {
                    // Cắt dọc qua lỗ mở
                    double yBotCut = origin.Y + neckRect.MinY;
                    double yTopCut = origin.Y + neckRect.MaxY;

                    if (yBotCut > origin.Y - halfY + c)
                    {
                        CadPrimitives.DrawLine(btr, tr, xLine, origin.Y - halfY + c, xLine, yBotCut, "VHG-Thep2", colorIndex: 2);
                    }
                    if (origin.Y + halfY - c > yTopCut)
                    {
                        CadPrimitives.DrawLine(btr, tr, xLine, yTopCut, xLine, origin.Y + halfY - c, "VHG-Thep2", colorIndex: 2);
                    }
                }
                else
                {
                    CadPrimitives.DrawLine(btr, tr, xLine, origin.Y - halfY + c, xLine, origin.Y + halfY - c, "VHG-Thep2", colorIndex: 2);
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
