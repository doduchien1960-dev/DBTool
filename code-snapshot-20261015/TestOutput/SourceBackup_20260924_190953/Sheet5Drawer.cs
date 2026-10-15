using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ 5: Bảng Chi tiết uốn thanh thép (Bar Bending Schedule) với tiêu đề phân nhóm TieudeMC
    /// </summary>
    public class Sheet5Drawer
    {
        public static void Draw(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            SteelScheduleModel schedule,
            Point2D sheetOrigin,
            ObjectId dimStyleId)
        {
            const double startOffsetY = 900;
            const double bottomMargin = 400;
            const double headerStep = 250;
            const double groupGap = 40;
            const double availableHeight = 8910 - startOffsetY - bottomMargin;
            var groups = schedule.GroupedItems
                .Select((group, index) => (Index: index, Group: group))
                .ToList();
            if (groups.Count == 0) return;

            int columnCount = 2;
            List<List<(int Index, KeyValuePair<string, List<BarScheduleItem>> Group)>>? columns = null;
            double rowStep = 0;
            foreach (int candidateColumns in new[] { 2, 3, 4, 5 })
            {
                var candidate = DistributeGroups(groups, candidateColumns, headerStep, groupGap, 270);
                double candidateRowStep = CalculateRowStep(candidate, availableHeight, headerStep, groupGap);
                if (candidateRowStep >= 260 || candidateColumns == 5)
                {
                    columnCount = candidateColumns;
                    columns = candidate;
                    rowStep = candidateRowStep;
                    break;
                }
            }
            if (columns == null || rowStep < 190)
                throw new InvalidOperationException(
                    $"Không thể bố trí chi tiết uốn của ga {spec.Name} trên Tờ 5 mà vẫn giữ khoảng cách đọc được. " +
                    "Cần rà lại số nhóm/thanh hoặc mở rộng số tờ chi tiết trước khi vẽ.");
            rowStep = Math.Min(360, Math.Floor(rowStep / 10.0) * 10.0);

            double[] xOffsets = columnCount switch
            {
                2 => new[] { 1200.0, 6800.0 },
                3 => new[] { 800.0, 4800.0, 8800.0 },
                4 => new[] { 350.0, 3250.0, 6150.0, 9050.0 },
                _ => new[] { 350.0, 2650.0, 4950.0, 7250.0, 9550.0 }
            };
            double columnPitch = columnCount switch { 2 => 5600, 3 => 4000, 4 => 2900, _ => 2300 };
            double titleBlockScale = columnCount <= 3 ? 0.3 : columnCount == 4 ? 0.23 : 0.18;
            double safeRight = sheetOrigin.X + 12200;
            double[] curY = Enumerable.Repeat(sheetOrigin.Y - startOffsetY, columnCount).ToArray();
            for (int column = 0; column < columns.Count; column++)
            {
                foreach (var entry in columns[column].OrderBy(item => item.Index))
                {
                    var group = entry.Group;
                    double x = sheetOrigin.X + xOffsets[column];

                    if (blockTable.Has("TieudeMC"))
                    {
                        var atts = new Dictionary<string, string>
                        {
                            ["TIEUDE"] = group.Key.ToUpperInvariant()
                        };
                        CadPrimitives.InsertBlock(blockTable, btr, tr, "TieudeMC", x, curY[column], scale: titleBlockScale, layer: "VHG-Text", attributes: atts);
                    }
                    else
                    {
                        CadPrimitives.DrawText(btr, tr, db, group.Key.ToUpperInvariant(), x, curY[column], height: 100, layer: "VHG-Text", colorIndex: 3);
                    }
                    curY[column] -= headerStep;

                    foreach (var item in group.Value)
                    {
                        double diagramWidth = Math.Min(
                            columnPitch - 1050,
                            safeRight - (x + 850));
                        DrawBarBendingDetail(blockTable, btr, tr, db, item, x, curY[column], rowStep, diagramWidth);
                        curY[column] -= rowStep;
                    }

                    curY[column] -= groupGap;
                }
            }
        }

        private static List<List<(int Index, KeyValuePair<string, List<BarScheduleItem>> Group)>> DistributeGroups(
            List<(int Index, KeyValuePair<string, List<BarScheduleItem>> Group)> groups,
            int columnCount,
            double headerStep,
            double groupGap,
            double estimatedRowStep)
        {
            var columns = new List<List<(int Index, KeyValuePair<string, List<BarScheduleItem>> Group)>>();
            var depths = new double[columnCount];
            for (int i = 0; i < columnCount; i++) columns.Add(new());

            foreach (var entry in groups.OrderByDescending(item => item.Group.Value.Count).ThenBy(item => item.Index))
            {
                int target = Array.IndexOf(depths, depths.Min());
                columns[target].Add(entry);
                depths[target] += headerStep + groupGap + entry.Group.Value.Count * estimatedRowStep;
            }
            return columns;
        }

        private static double CalculateRowStep(
            List<List<(int Index, KeyValuePair<string, List<BarScheduleItem>> Group)>> columns,
            double availableHeight,
            double headerStep,
            double groupGap)
        {
            int maxBars = columns.Max(column => column.Sum(entry => entry.Group.Value.Count));
            int maxGroups = columns.Max(column => column.Count);
            if (maxBars == 0) return 300;
            return (availableHeight - maxGroups * (headerStep + groupGap)) / maxBars;
        }

        private static void DrawBarBendingDetail(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            BarScheduleItem item,
            double cellX, double y,
            double rowStep,
            double maxDiagramWidth)
        {
            double x = cellX + 100;
            double diagramX = cellX + 850;
            var envelope = Sheet5DiagramLayout.GetEnvelope(item);
            double geomScale = Sheet5DiagramLayout.ScaleToFit(
                envelope.WidthMm,
                envelope.HeightMm,
                maxDiagramWidth,
                Math.Max(40, rowStep - 130));

            // 1. Chèn block số hiệu thanh BLT1 (hoặc vẽ nhãn)
            string calloutText = $"{item.BarMark} (D{item.Diameter})";
            if (blockTable.Has("BLT1"))
            {
                var atts = new Dictionary<string, string>
                {
                    ["SH"] = item.BarMark,
                    ["DK"] = item.IsAngleSteel ? item.AngleCode : $"D{item.Diameter}",
                    ["A"] = item.Quantity.ToString()
                };
                CadPrimitives.InsertBlock(blockTable, btr, tr, "BLT1", x, y, scale: 0.3, layer: "VHG-Text", attributes: atts);
            }
            else
            {
                CadPrimitives.DrawText(btr, tr, db, calloutText, x, y, height: 60, layer: "VHG-Text", colorIndex: 7);
            }

            // 2. Vẽ sơ đồ theo tỷ lệ đồng nhất, tự co để nằm trọn trong hàng/cột.
            // Kích thước chữ và con số vẫn là giá trị thật lấy từ schedule.
            switch (item.Shape)
            {
                case BarShapeType.L0M:
                    // Đoạn thẳng
                    double lLine = item.LengthMm * geomScale;
                    CadPrimitives.DrawLine(btr, tr, diagramX, y, diagramX + lLine, y, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LengthMm:F0}", diagramX + lLine / 2.0, y + 50, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.L1M:
                    // 1 móc
                    double l1 = item.L * geomScale;
                    var pts1 = BarBendingGeometry.BuildL1MPolyline(
                        item.L, item.LT, item.LP, geomScale)
                        .Select(point => new Point2D(diagramX + point.X, y + point.Y))
                        .ToArray();
                    CadPrimitives.DrawPolyline(btr, tr, pts1, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", diagramX + l1 / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    if (item.LT > 0)
                    {
                        CadPrimitives.DrawText(btr, tr, db, $"{item.LT:F0}", diagramX - 80,
                            y + item.LT * geomScale / 2.0, height: 50, layer: "VHG-Text");
                    }
                    else if (item.LP > 0)
                    {
                        CadPrimitives.DrawText(btr, tr, db, $"{item.LP:F0}", diagramX + l1 + 80,
                            y + item.LP * geomScale / 2.0, height: 50, layer: "VHG-Text");
                    }
                    break;

                case BarShapeType.L2M:
                    // 2 móc
                    double l2 = item.L * geomScale;
                    double lt2 = item.LT * geomScale;
                    double lp2 = item.LP * geomScale;
                    var pts2 = new[]
                    {
                        new Point2D(diagramX, y + lt2),
                        new Point2D(diagramX, y),
                        new Point2D(diagramX + l2, y),
                        new Point2D(diagramX + l2, y + lp2)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, pts2, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", diagramX + l2 / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LT:F0}", diagramX - 80, y + lt2 / 2.0, height: 50, layer: "VHG-Text");
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LP:F0}", diagramX + l2 + 80, y + lp2 / 2.0, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.M_C:
                    // Chữ C
                    double lC = item.L * geomScale;
                    double hC = item.LT * geomScale;
                    var ptsC = new[]
                    {
                        new Point2D(diagramX + hC, y + lC),
                        new Point2D(diagramX, y + lC),
                        new Point2D(diagramX, y),
                        new Point2D(diagramX + hC, y)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, ptsC, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", diagramX - 80, y + lC / 2.0, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.M_U:
                    // Chữ U (như bậc thang)
                    double wU = item.L * geomScale;
                    double dU = Math.Max(60, Math.Max(item.LT, item.LP) * geomScale);
                    double midHookU = Math.Max(0, item.L3) * geomScale;
                    var ptsU = midHookU > 0
                        ? new[]
                        {
                            new Point2D(diagramX, y + dU),
                            new Point2D(diagramX, y),
                            new Point2D(diagramX + wU / 2.0, y),
                            new Point2D(diagramX + wU / 2.0, y + midHookU),
                            new Point2D(diagramX + wU / 2.0, y),
                            new Point2D(diagramX + wU, y),
                            new Point2D(diagramX + wU, y + dU)
                        }
                        : new[]
                    {
                        new Point2D(diagramX, y + dU),
                        new Point2D(diagramX, y),
                        new Point2D(diagramX + wU, y),
                        new Point2D(diagramX + wU, y + dU)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, ptsU, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"L={item.LengthMm:F0}", diagramX + wU / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    if (item.L3 > 0)
                    {
                        CadPrimitives.DrawText(btr, tr, db, $"{item.L3:F0}", diagramX + wU / 2.0 + 60,
                            y + midHookU / 2.0, height: 50, layer: "VHG-Text");
                    }
                    break;

                case BarShapeType.GC_T:
                    double frameWidthMm = item.L > 0 ? item.L : Math.Sqrt(Math.Max(0, item.LengthMm));
                    double frameHeightMm = item.LT > 0 ? item.LT : frameWidthMm;
                    double frameWidth = frameWidthMm * geomScale;
                    double frameHeight = frameHeightMm * geomScale;
                    double frameBottom = y - frameHeight / 2.0;
                    CadPrimitives.DrawRectangle(
                        btr, tr, diagramX, frameBottom,
                        diagramX + frameWidth, frameBottom + frameHeight,
                        "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(
                        btr, tr, db, $"{frameWidthMm:F0}×{frameHeightMm:F0}",
                        diagramX + frameWidth + 80, y - 25, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.VG:
                    double angleLength = Math.Max(200, item.LengthMm * geomScale);
                    var anglePts = new[]
                    {
                        new Point2D(diagramX, y + 60),
                        new Point2D(diagramX, y),
                        new Point2D(diagramX + angleLength, y)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, anglePts, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(
                        btr, tr, db, $"{item.AngleCode}  L={item.LengthMm:F0}",
                        diagramX, y + 75, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.GC_H:
                    double hoopDiameterMm = item.L > 0 ? item.L : Math.Sqrt(Math.Max(0, item.LengthMm / Math.PI));
                    double hoopRadius = hoopDiameterMm * geomScale / 2.0;
                    CadPrimitives.DrawCircle(btr, tr, diagramX + hoopRadius, y, hoopRadius, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"D={hoopDiameterMm:F0}", diagramX + 2 * hoopRadius + 80, y - 25, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.M_Z:
                    double zLeg = Math.Max(60, item.LT * geomScale);
                    double zRun = Math.Max(100, item.L * geomScale);
                    var zPts = new[]
                    {
                        new Point2D(diagramX, y + zLeg),
                        new Point2D(diagramX + zRun, y + zLeg),
                        new Point2D(diagramX + zRun, y),
                        new Point2D(diagramX + 2 * zRun, y)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, zPts, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", diagramX + zRun, y - 50, height: 50, layer: "VHG-Text");
                    break;

                default:
                    double straightLength = Math.Max(200, item.LengthMm * geomScale);
                    CadPrimitives.DrawLine(btr, tr, diagramX, y, diagramX + straightLength, y, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"L={item.LengthMm:F0}", diagramX, y + 50, height: 50, layer: "VHG-Text");
                    break;
            }
        }
    }
}
