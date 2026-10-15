using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Labels the mesh with the same bar marks and quantities that appear in
    /// the steel schedule. Split bars at an opening retain their own marks.
    /// </summary>
    internal static class MeshScheduleAnnotations
    {
        public static void Draw(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            SteelScheduleModel schedule,
            string componentGroup,
            double leftX,
            double topY,
            double nominalSpacing,
            string firstFamily,
            string secondFamily)
        {
            var items = schedule.Items
                .Where(item => item.ComponentGroup.Equals(componentGroup, StringComparison.OrdinalIgnoreCase))
                .ToList();
            DrawFamily(blockTable, btr, tr, db, items, firstFamily, leftX, topY, nominalSpacing, "BLT1");
            DrawFamily(blockTable, btr, tr, db, items, secondFamily, leftX, topY - 200, nominalSpacing, "BLT2");
        }

        public static void DrawTick(
            BlockTable blockTable, BlockTableRecord btr, Transaction tr,
            double x, double y)
        {
            if (blockTable.Has("Gach"))
                CadPrimitives.InsertBlock(blockTable, btr, tr, "Gach", x, y,
                    scale: 0.3, layer: "VHG-Thep");
        }

        private static void DrawFamily(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            IReadOnlyList<BarScheduleItem> items,
            string family,
            double x,
            double y,
            double nominalSpacing,
            string blockName)
        {
            var familyItems = items
                .Where(item => item.BarMark.Equals(family, StringComparison.OrdinalIgnoreCase)
                    || item.BarMark.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (familyItems.Count == 0) return;

            BarScheduleItem representative = familyItems.FirstOrDefault(item =>
                item.BarMark.Equals(family, StringComparison.OrdinalIgnoreCase)) ?? familyItems[0];
            if (blockTable.Has(blockName))
            {
                var attributes = new Dictionary<string, string>
                {
                    ["SH"] = representative.BarMark,
                    ["DK"] = $"%%c{representative.Diameter}",
                    ["A"] = nominalSpacing > 0
                        ? "@" + nominalSpacing.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                        : ""
                };
                CadPrimitives.InsertBlock(blockTable, btr, tr, blockName,
                    x, y, scale: 0.3, layer: "VHG-Dim", attributes: attributes);
                return;
            }

            string marks = string.Join("/", familyItems
                .Select(item => item.BarMark)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            string diameters = string.Join("/", familyItems
                .Select(item => item.Diameter)
                .Distinct()
                .Select(diameter => $"D{diameter}"));
            string pitches = string.Join("/", familyItems
                .Select(item => item.SpacingMm)
                .Where(spacing => spacing > 0)
                .DistinctBy(spacing => Math.Round(spacing, 1))
                .Select(spacing => spacing.ToString("0.#")));
            int count = familyItems.Sum(item => item.Quantity);
            string pitchLabel = pitches.Length > 0 ? $" a{pitches}" : "";
            string text = $"{marks}: {diameters}{pitchLabel} ({count} thanh)";
            CadPrimitives.DrawText(btr, tr, db, text, x, y, height: 60,
                layer: "VHG-Text", colorIndex: 7,
                horiz: TextHorizontalMode.TextLeft);
        }
    }
}
