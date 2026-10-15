using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Vẽ Tờ phụ tỷ lệ 1:20 (Chi tiết Tấm đan nắp BTCT đúc sẵn hoặc Cửa thu nước)
    /// </summary>
    public class SheetAuxDrawer
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
            // Bố trí chi tiết tấm đan nắp đúc sẵn
            Point2D planOrigin = new(sheetOrigin.X + 2800, sheetOrigin.Y - 2600);
            Point2D tableOrigin = new(sheetOrigin.X + 6800, sheetOrigin.Y - 1200);

            // 1. Mặt bằng tấm đan
            double wX = spec.NeckInnerWidth;
            double wY = spec.NeckInnerLength;
            CadPrimitives.DrawRectangle(btr, tr, planOrigin.X - wX / 2.0, planOrigin.Y - wY / 2.0, planOrigin.X + wX / 2.0, planOrigin.Y + wY / 2.0, "VHG-BT");

            // Tiêu đề
            CadPrimitives.DrawTitleWithScale(blockTable, btr, tr, db, "CHI TIẾT TẤM ĐAN NẮP BTCT", "TỶ LỆ 1/20", planOrigin.X, planOrigin.Y + wY / 2.0 + 400);

            // 2. Chèn bảng TK.THKLNAP nếu có
            if (blockTable.Has("TK.THKLNAP"))
            {
                var atts = new Dictionary<string, string>
                {
                    ["NAP1"] = schedule.Summary.Nap1.ToString("F2"),
                    ["NAP2"] = schedule.Summary.Nap2.ToString("F2"),
                    ["THEPHINH"] = schedule.Summary.ThepHinh.ToString("F2"),
                    ["TONG"] = (schedule.Summary.Nap1 + schedule.Summary.Nap2 + schedule.Summary.ThepHinh).ToString("F2")
                };
                CadPrimitives.InsertBlock(blockTable, btr, tr, "TK.THKLNAP", tableOrigin.X, tableOrigin.Y, scale: 0.2, layer: "VHG-Text", attributes: atts);
            }
        }
    }
}
