using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Renders a wall reinforcement view from the same segment layout used by
    /// RebarEngine and ScheduleEngine.
    /// </summary>
    internal static class WallMeshDrawer
    {
        public static void Draw(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            SteelScheduleModel schedule,
            WallDirection direction,
            PipeConnectionSpec? pipe,
            Point2D origin,
            bool isOuter,
            ObjectId dimStyleId)
        {
            double wallSpan = direction is WallDirection.T1 or WallDirection.T3 ? spec.L : spec.B;
            double halfSpan = wallSpan / 2.0;
            double wallHeight = spec.H;

            CadPrimitives.DrawRectangle(
                btr, tr,
                origin.X - halfSpan, origin.Y,
                origin.X + halfSpan, origin.Y + wallHeight,
                "VHG-BT");

            if (pipe is not null)
            {
                double pipeX = origin.X + WallViewTransform.ToLocalOffset(direction, pipe.OffsetFromCenter);
                if (pipe.IsBox)
                {
                    CadPrimitives.DrawRectangle(
                        btr, tr,
                        pipeX - pipe.CutoutWidth / 2.0, origin.Y + pipe.CutoutBottomZ,
                        pipeX + pipe.CutoutWidth / 2.0, origin.Y + pipe.CutoutTopZ,
                        "VHG-Cong");
                }
                else
                {
                    double pipeY = origin.Y + pipe.InvertLevelFromBottom + pipe.InnerHeight / 2.0;
                    CadPrimitives.DrawCircle(btr, tr, pipeX, pipeY, pipe.CutoutDia / 2.0, "VHG-Cong");
                }
            }

            foreach (var segment in RebarEngine.BuildWallRebarLayout(spec, direction, pipe)
                         .Where(s => s.IsOuterLayer == isOuter))
            {
                if (segment.Axis == WallBarAxis.Vertical)
                {
                    if (segment.PositionMm < -halfSpan - 1e-7 || segment.PositionMm > halfSpan + 1e-7)
                        continue;

                    double start = Math.Max(0, segment.StartMm);
                    double end = Math.Min(wallHeight, segment.EndMm);
                    if (end - start > 0.01)
                    {
                        double x = origin.X + segment.PositionMm;
                        CadPrimitives.DrawLine(
                            btr, tr, x, origin.Y + start, x, origin.Y + end,
                            "VHG-Thep", colorIndex: 1);
                        MeshScheduleAnnotations.DrawTick(blockTable, btr, tr, x, origin.Y + start);
                    }
                }
                else
                {
                    if (segment.PositionMm < -1e-7 || segment.PositionMm > wallHeight + 1e-7)
                        continue;

                    double start = Math.Max(-halfSpan, segment.StartMm);
                    double end = Math.Min(halfSpan, segment.EndMm);
                    if (end - start > 0.01)
                    {
                        double y = origin.Y + segment.PositionMm;
                        CadPrimitives.DrawLine(
                            btr, tr, origin.X + start, y, origin.X + end, y,
                            "VHG-Thep2", colorIndex: 2);
                        MeshScheduleAnnotations.DrawTick(blockTable, btr, tr, origin.X + start, y);
                    }
                }
            }

            string faceLabel = isOuter ? "NGOÀI" : "TRONG";
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                $"LƯỚI THÉP TƯỜNG {direction} - {faceLabel}",
                "TỶ LỆ 1/30", origin.X, origin.Y + wallHeight + 450);

            CadPrimitives.DrawDimNgang(
                btr, tr, dimStyleId,
                origin.X - halfSpan, origin.X + halfSpan,
                origin.Y, origin.Y - 250);
            CadPrimitives.DrawDimDoc(
                btr, tr, dimStyleId,
                origin.X - halfSpan, origin.Y,
                origin.Y + wallHeight, origin.X - halfSpan - 250);
            MeshScheduleAnnotations.Draw(blockTable, btr, tr, db, schedule,
                $"Tường {direction} - {(isOuter ? "lớp ngoài" : "lớp trong")}",
                origin.X - halfSpan, origin.Y - 500,
                (isOuter ? spec.WallOuterRebar : spec.WallInnerRebar)?.Spacing ?? 0, "T1", "T2");
        }
    }
}
