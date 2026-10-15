using System;
using System.Collections.Generic;
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
            // Bố trí 2 cột chi tiết uốn thép trên tờ A3 (rộng 12600 mm, cao 8910 mm):
            // Cột 1: X = sheetOrigin.X + 1200, Y bắt đầu từ sheetOrigin.Y - 1200
            // Cột 2: X = sheetOrigin.X + 7000, Y bắt đầu từ sheetOrigin.Y - 1200

            double col1X = sheetOrigin.X + 1200;
            double col2X = sheetOrigin.X + 6800;
            double startY = sheetOrigin.Y - 1200;

            double curY1 = startY;
            double curY2 = startY;

            int groupIndex = 0;

            foreach (var group in schedule.GroupedItems)
            {
                // Chọn cột có chiều cao tích lũy ít hơn để cân bằng 2 bên
                bool useCol1 = (curY1 >= curY2);
                double curX = useCol1 ? col1X : col2X;
                ref double curY = ref (useCol1 ? ref curY1 : ref curY2);

                // 1. Vẽ tiêu đề nhóm cấu kiện bằng block TieudeMC
                if (blockTable.Has("TieudeMC"))
                {
                    var atts = new Dictionary<string, string>
                    {
                        ["TIEUDE"] = group.Key.ToUpperInvariant()
                    };
                    CadPrimitives.InsertBlock(blockTable, btr, tr, "TieudeMC", curX, curY, scale: 0.3, layer: "VHG-Text", attributes: atts);
                }
                else
                {
                    CadPrimitives.DrawText(btr, tr, db, group.Key.ToUpperInvariant(), curX, curY, height: 100, layer: "VHG-Text", colorIndex: 3);
                }
                curY -= 450;

                // 2. Vẽ từng thanh uốn trong nhóm
                foreach (var item in group.Value)
                {
                    DrawBarBendingDetail(blockTable, btr, tr, db, item, curX + 600, curY, dimStyleId);
                    curY -= 450; // Bước dịch xuống cho mỗi thanh
                }

                curY -= 300; // Khoảng cách giữa các nhóm
                groupIndex++;
            }
        }

        private static void DrawBarBendingDetail(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            BarScheduleItem item,
            double x, double y,
            ObjectId dimStyleId)
        {
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
                CadPrimitives.InsertBlock(blockTable, btr, tr, "BLT1", x - 400, y, scale: 0.3, layer: "VHG-Text", attributes: atts);
            }
            else
            {
                CadPrimitives.DrawText(btr, tr, db, calloutText, x - 400, y, height: 60, layer: "VHG-Text", colorIndex: 7);
            }

            // 2. Vẽ hình dạng uốn Polyline thu nhỏ
            double geomScale = 0.25; // Tỷ lệ thu nhỏ hình uốn
            switch (item.Shape)
            {
                case BarShapeType.L0M:
                    // Đoạn thẳng
                    double lLine = item.LengthMm * geomScale;
                    CadPrimitives.DrawLine(btr, tr, x, y, x + lLine, y, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LengthMm:F0}", x + lLine / 2.0, y + 50, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.L1M:
                    // 1 móc
                    double l1 = item.L * geomScale;
                    double lt1 = item.LT * geomScale;
                    var pts1 = new[]
                    {
                        new Point2D(x, y + lt1),
                        new Point2D(x, y),
                        new Point2D(x + l1, y)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, pts1, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", x + l1 / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LT:F0}", x - 50, y + lt1 / 2.0, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.L2M:
                    // 2 móc
                    double l2 = item.L * geomScale;
                    double lt2 = item.LT * geomScale;
                    double lp2 = item.LP * geomScale;
                    var pts2 = new[]
                    {
                        new Point2D(x, y + lt2),
                        new Point2D(x, y),
                        new Point2D(x + l2, y),
                        new Point2D(x + l2, y + lp2)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, pts2, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", x + l2 / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LT:F0}", x - 50, y + lt2 / 2.0, height: 50, layer: "VHG-Text");
                    CadPrimitives.DrawText(btr, tr, db, $"{item.LP:F0}", x + l2 + 50, y + lp2 / 2.0, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.M_C:
                    // Chữ C
                    double lC = item.L * geomScale;
                    double hC = item.LT * geomScale;
                    var ptsC = new[]
                    {
                        new Point2D(x + hC, y + lC),
                        new Point2D(x, y + lC),
                        new Point2D(x, y),
                        new Point2D(x + hC, y)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, ptsC, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"{item.L:F0}", x - 50, y + lC / 2.0, height: 50, layer: "VHG-Text");
                    break;

                case BarShapeType.M_U:
                    // Chữ U (như bậc thang)
                    double wU = item.L * geomScale;
                    double dU = 60; // Thu nhỏ
                    var ptsU = new[]
                    {
                        new Point2D(x, y + dU),
                        new Point2D(x, y),
                        new Point2D(x + wU, y),
                        new Point2D(x + wU, y + dU)
                    };
                    CadPrimitives.DrawPolyline(btr, tr, ptsU, false, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"L={item.LengthMm:F0}", x + wU / 2.0, y - 50, height: 50, layer: "VHG-Text");
                    break;

                default:
                    // Hình chữ nhật / Khung GC
                    double wFrame = 150;
                    double hFrame = 100;
                    CadPrimitives.DrawRectangle(btr, tr, x, y, x + wFrame, y + hFrame, "VHG-Thep", colorIndex: 1);
                    CadPrimitives.DrawText(btr, tr, db, $"L={item.LengthMm:F0}", x + wFrame / 2.0, y + hFrame / 2.0, height: 50, layer: "VHG-Text");
                    break;
            }
        }
    }
}
