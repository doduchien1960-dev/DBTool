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
            Point2D tableOrigin = new(sheetOrigin.X + 6800, sheetOrigin.Y - 1200);
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
            const double tableScale = 0.300;
            const double headerHeight = 300.0; // 1000 * 0.3
            const double rowHeight = 150.0;    // 500 * 0.3
            const double footerHeight = 450.0; // 1500 * 0.3

            double curX = origin.X;
            double curY = origin.Y;

            // 1. Chèn Header block: TK.Tieude
            CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.Tieude", curX, curY, scale: tableScale, layer: "VHG-Text");
            curY -= headerHeight;

            // 2. Chèn các nhóm cấu kiện
            foreach (var group in schedule.GroupedItems)
            {
                // Chèn block tên lưới: TK.Tenluoi
                var attTenLuoi = new Dictionary<string, string>
                {
                    ["TENLUOI"] = group.Key.ToUpperInvariant()
                };
                CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.Tenluoi", curX, curY, scale: tableScale, layer: "VHG-Text", attributes: attTenLuoi);

                // Chèn các dòng thanh trong nhóm
                foreach (var item in group.Value)
                {
                    string blockName = ChooseRowBlockName(item.Shape);
                    var atts = BuildRowAttributes(item);

                    CadPrimitives.InsertBlock(blockTable, btr, tr, blockName, curX, curY, scale: tableScale, layer: "VHG-Text", attributes: atts);
                    curY -= rowHeight;
                }
            }

            // 3. Chèn Footer summary block: TK.THKL
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

            CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.THKL", curX, curY, scale: tableScale, layer: "VHG-Text", attributes: attSummary);
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
