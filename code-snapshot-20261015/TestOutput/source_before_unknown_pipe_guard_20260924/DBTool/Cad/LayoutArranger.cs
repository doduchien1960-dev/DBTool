using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Định vị và điều phối ma trận ModelSpace cho các tờ bản vẽ A3
    /// Bước ngang dX = 17600 mm, Bước dọc dY = -12425 mm
    /// </summary>
    public class LayoutArranger
    {
        public const double DeltaX = 17600.0;
        public const double DeltaY = -12425.0;

        public const double FrameWidth = 12600.0; // 42000 * 0.3
        public const double FrameHeight = 8910.0; // 29700 * 0.3

        public double BaseX { get; set; } = 0;
        public double BaseY { get; set; } = 0;

        public LayoutArranger(double baseX = 0, double baseY = 0)
        {
            BaseX = baseX;
            BaseY = baseY;
        }

        public Point2D GetSheetOrigin(int manholeIndex, int sheetIndex)
        {
            double x = BaseX + sheetIndex * DeltaX;
            double y = BaseY + manholeIndex * DeltaY;
            return new Point2D(x, y);
        }

        public Point2D GetSheetCenter(int manholeIndex, int sheetIndex)
        {
            var orig = GetSheetOrigin(manholeIndex, sheetIndex);
            return new Point2D(orig.X + FrameWidth / 2.0, orig.Y - FrameHeight / 2.0);
        }

        public void InsertSheetFrame(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            Point2D origin,
            string manholeName,
            string sheetTitle,
            int sheetNumber,
            int totalSheets = 5,
            double scale = 0.3)
        {
            // 1. Chèn Block Khung (gốc 42000x29700, scale 0.3 -> 12600x8910)
            CadPrimitives.InsertBlock(blockTable, btr, tr, "Khung", origin.X, origin.Y, scale: scale, layer: "VHG-Khung");

            // 2. Chú thích góc khung tên phục vụ ATP Plotter & Quản lý bản vẽ
            // Góc dưới bên phải của khung tên A3 là: (origin.X + FrameWidth, origin.Y - FrameHeight)
            double cornerX = origin.X + FrameWidth;
            double cornerY = origin.Y - FrameHeight;

            // Tên hố ga & Tên tờ bản vẽ
            CadPrimitives.DrawText(btr, tr, db, manholeName, cornerX - 1200, cornerY + 450, height: 90, layer: "VHG-Text", colorIndex: 3);
            CadPrimitives.DrawText(btr, tr, db, sheetTitle, cornerX - 1200, cornerY + 300, height: 75, layer: "VHG-Text", colorIndex: 7);
            CadPrimitives.DrawText(btr, tr, db, $"BẢN VẼ SỐ: {sheetNumber:D2}/{totalSheets:D2}", cornerX - 1200, cornerY + 180, height: 60, layer: "VHG-Text", colorIndex: 7);
            CadPrimitives.DrawText(btr, tr, db, "TỶ LỆ: 1/30", cornerX - 1200, cornerY + 80, height: 60, layer: "VHG-Text", colorIndex: 7);
        }
    }
}
