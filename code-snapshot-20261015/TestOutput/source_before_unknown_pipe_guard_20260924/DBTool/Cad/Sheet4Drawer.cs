using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ 4: Chi tiết Cổ ga, Thang leo và Lắp ghép Block Bảng Thống kê Cốt thép (TK.*)
    /// </summary>
    public class Sheet4Drawer
    {
        public static void Draw(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            ManholeGeometryModel geom,
            SteelScheduleModel schedule,
            Point2D sheetOrigin,
            ObjectId dimStyleId)
        {
            // Nửa trái: Chi tiết thang thép và ghi chú
            Point2D ladderOrigin = new(sheetOrigin.X + 2800, sheetOrigin.Y - 2600);
            DrawLadderDetail(blockTable, btr, tr, db, spec, ladderOrigin, dimStyleId);

            // Nửa phải: Bảng thống kê cốt thép bằng cách lắp ghép các Block TK.*
            Point2D tableOrigin = new(sheetOrigin.X + 5200, sheetOrigin.Y - 650);
            DrawRebarScheduleTable(blockTable, btr, tr, spec, schedule, tableOrigin);
        }

        private static void DrawLadderDetail(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            NormalizedManholeSpec spec,
            Point2D origin,
            ObjectId dimStyleId)
        {
            // Bậc thang leo uốn: móc 150, sâu 250, rộng 300 mm
            double w = spec.LadderWidth;
            double d = spec.LadderDepth;
            double h = spec.LadderHook;

            double halfW = w / 2.0;

            // Hình chiếu bằng của bậc thang (uốn hình chữ U có 2 móc ngàm vào tường)
            var pts = new[]
            {
                new Point2D(origin.X - halfW - d, origin.Y + h),
                new Point2D(origin.X - halfW - d, origin.Y),
                new Point2D(origin.X - halfW, origin.Y),
                new Point2D(origin.X + halfW, origin.Y),
                new Point2D(origin.X + halfW + d, origin.Y),
                new Point2D(origin.X + halfW + d, origin.Y + h)
            };
            CadPrimitives.DrawPolyline(btr, tr, pts, false, "VHG-Thep", colorIndex: 1);

            // Tiêu đề
            CadPrimitives.DrawTitleWithScale(
                blockTable, btr, tr, db,
                "CHI TIẾT BẬC THANG LEO Th1", "TỶ LỆ 1/10",
                origin.X, origin.Y + 600);

            // Kích thước bậc thang
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfW, origin.X + halfW, origin.Y, origin.Y - 200);
            CadPrimitives.DrawDimNgang(btr, tr, dimStyleId, origin.X - halfW - d, origin.X - halfW, origin.Y, origin.Y - 200);
            CadPrimitives.DrawDimDoc(btr, tr, dimStyleId, origin.X - halfW - d, origin.Y, origin.Y + h, origin.X - halfW - d - 150);

            // Ghi chú kỹ thuật bên dưới
            double yNote = origin.Y - 1800;
            CadPrimitives.DrawText(btr, tr, db, "GHI CHÚ KỸ THUẬT:", origin.X - 1800, yNote, height: 75, layer: "VHG-Text", colorIndex: 3, horiz: Autodesk.AutoCAD.DatabaseServices.TextHorizontalMode.TextLeft);
            CadPrimitives.DrawText(btr, tr, db, "1. Bê tông hố ga M250 đá 1x2, cốt thép CB240-T, CB300-V.", origin.X - 1800, yNote - 180, height: 60, layer: "VHG-Text", colorIndex: 7, horiz: Autodesk.AutoCAD.DatabaseServices.TextHorizontalMode.TextLeft);
            CadPrimitives.DrawText(btr, tr, db, "2. Bậc thang leo mạ kẽm nhúng nóng hoặc thép tròn trơn sơn chống gỉ.", origin.X - 1800, yNote - 360, height: 60, layer: "VHG-Text", colorIndex: 7, horiz: Autodesk.AutoCAD.DatabaseServices.TextHorizontalMode.TextLeft);
            CadPrimitives.DrawText(btr, tr, db, "3. Mối nối cốt thép buộc so le, chiều dài nối tối thiểu 30d.", origin.X - 1800, yNote - 540, height: 60, layer: "VHG-Text", colorIndex: 7, horiz: Autodesk.AutoCAD.DatabaseServices.TextHorizontalMode.TextLeft);
        }

        private static void DrawRebarScheduleTable(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            NormalizedManholeSpec spec,
            SteelScheduleModel schedule,
            Point2D origin)
        {
            const double tableScale = 0.270;
            const double headerHeight = 270.0; // 1000 * 0.27
            const double rowHeight = 135.0;    // 500 * 0.27
            const double groupHeight = 135.0;  // 500 * 0.27
            const double columnGap = 4000.0;

            // Split whole component groups into two balanced columns. The table blocks are
            // 12,500 units wide at 0.3 scale, so each column stays within the A3 frame.
            var columns = new List<List<KeyValuePair<string, List<BarScheduleItem>>>>
            {
                new(), new()
            };
            double[] columnDepth = new double[2];
            foreach (var group in schedule.GroupedItems)
            {
                int column = columnDepth[0] <= columnDepth[1] ? 0 : 1;
                columns[column].Add(group);
                columnDepth[column] += groupHeight + group.Value.Count * rowHeight;
            }

            double[] curY = { origin.Y, origin.Y };
            double[] curX = { origin.X, origin.X + columnGap };
            for (int column = 0; column < columns.Count; column++)
            {
                double x = curX[column];
                CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.Tieude", x, curY[column], scale: tableScale, layer: "VHG-Text");
                curY[column] -= headerHeight;

                foreach (var group in columns[column])
                {
                    var attTenLuoi = new Dictionary<string, string>
                    {
                        ["TENLUOI"] = group.Key.ToUpperInvariant()
                    };
                    CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.Tenluoi", x, curY[column], scale: tableScale, layer: "VHG-Text", attributes: attTenLuoi);
                    curY[column] -= groupHeight;

                    foreach (var item in group.Value)
                    {
                        string blockName = ChooseRowBlockName(item.Shape);
                        var atts = BuildRowAttributes(item);
                        CadPrimitives.InsertBlock(blockTable, btr, tr, blockName, x, curY[column], scale: tableScale, layer: "VHG-Text", attributes: atts);
                        curY[column] -= rowHeight;
                    }
                }
            }

            // 3. Chèn Footer summary block dưới cột sâu nhất, tránh đè lên dòng cuối.
            var attSummary = new Dictionary<string, string>
            {
                ["DAY1"] = schedule.Summary.Day1.ToString("F2"),
                ["DAY2"] = schedule.Summary.Day2.ToString("F2"),
                ["TUONG1"] = schedule.Summary.Tuong1.ToString("F2"),
                ["TUONG2"] = schedule.Summary.Tuong2.ToString("F2"),
                ["TRAN1"] = schedule.Summary.Tran1.ToString("F2"),
                ["TRAN2"] = schedule.Summary.Tran2.ToString("F2"),
                ["CO1"] = schedule.Summary.Co1.ToString("F2"),
                ["CO2"] = schedule.Summary.Co2.ToString("F2"),
                ["TONG"] = schedule.Summary.Tong.ToString("F2")
            };

            int footerColumn = curY[0] <= curY[1] ? 0 : 1;
            double footerY = curY[footerColumn] - 150.0;
            CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.THKL", curX[footerColumn], footerY, scale: tableScale, layer: "VHG-Text", attributes: attSummary);
        }

        private static string ChooseRowBlockName(BarShapeType shape)
        {
            return shape switch
            {
                BarShapeType.L0M => "TK.L0M",
                BarShapeType.L1M => "TK.L1M",
                BarShapeType.L2M => "TK.L2M",
                BarShapeType.M_C => "TK.M.C",
                BarShapeType.M_Z => "TK.M.Z",
                BarShapeType.M_U => "TK.M.U",
                BarShapeType.VG => "TK.VG",
                BarShapeType.GC_H => "TK.GC.H",
                BarShapeType.GC_T => "TK.GC.T",
                _ => "TK.L0M"
            };
        }

        private static Dictionary<string, string> BuildRowAttributes(BarScheduleItem item)
        {
            var atts = new Dictionary<string, string>
            {
                ["SH"] = item.BarMark,
                ["D"] = item.IsAngleSteel ? item.AngleCode : item.Diameter.ToString(),
                ["L"] = item.L.ToString("F0"),
                ["LT"] = item.LT.ToString("F0"),
                ["LP"] = item.LP.ToString("F0"),
                ["L1T"] = item.L1T.ToString("F0"),
                ["L2T"] = item.L2T.ToString("F0"),
                ["L3"] = item.L3.ToString("F0"),
                ["L1P"] = item.L1P.ToString("F0"),
                ["L2P"] = item.L2P.ToString("F0"),
                ["DAI"] = item.LengthMm.ToString("F0"),
                ["SL"] = item.Quantity.ToString(),
                ["TDAI"] = item.TotalLengthM.ToString("F2"),
                ["KL"] = item.TotalWeightKg.ToString("F2")
            };
            return atts;
        }
    }
}
