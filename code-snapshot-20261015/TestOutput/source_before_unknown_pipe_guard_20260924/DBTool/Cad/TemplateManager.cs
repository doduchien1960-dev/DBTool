using System;
using System.IO;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace DBTool.Cad
{
    /// <summary>
    /// Quản lý nạp và clone an toàn tài nguyên (Layers, TextStyles, DimStyles, Blocks) từ VHG-Template.dwg
    /// </summary>
    public class TemplateManager
    {
        public static string ResolveTemplatePath()
        {
            string[] possiblePaths =
            {
                @"c:\Users\DucBao\Desktop\Dbtool\Ver 1\VHGpro 3.2\Template\VHG-Template.dwg",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template", "VHG-Template.dwg"),
                @"C:\Users\DucBao\Downloads\VHGpro 3.2\VHGpro 3.2\Template\VHG-Template.dwg"
            };

            foreach (var p in possiblePaths)
            {
                if (File.Exists(p)) return p;
            }

            return possiblePaths[0];
        }

        public static void EnsureTemplateLoaded(Database targetDb)
        {
            EnsureStandardLayers(targetDb);
            EnsureStandardTextStyles(targetDb);

            string templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath)) return;

            try
            {
                using var sourceDb = new Database(false, true);
                sourceDb.ReadDwgFile(templatePath, FileOpenMode.OpenForReadAndAllShare, true, "");

                var textStyleIds = new ObjectIdCollection();
                var dimStyleIds = new ObjectIdCollection();
                var blockIds = new ObjectIdCollection();

                using (var tr = sourceDb.TransactionManager.StartTransaction())
                {
                    // 1. TextStyle
                    var textStyles = (TextStyleTable)tr.GetObject(sourceDb.TextStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in textStyles)
                    {
                        var record = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (record.Name.StartsWith("VHG", StringComparison.OrdinalIgnoreCase))
                        {
                            textStyleIds.Add(id);
                        }
                    }

                    // 2. DimStyle
                    var dimStyles = (DimStyleTable)tr.GetObject(sourceDb.DimStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in dimStyles)
                    {
                        var record = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (record.Name.StartsWith("VHG", StringComparison.OrdinalIgnoreCase))
                        {
                            dimStyleIds.Add(id);
                        }
                    }

                    // 3. Blocks (Khung, TK.*, BLT*, TenMC, etc.)
                    var blocks = (BlockTable)tr.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId id in blocks)
                    {
                        var record = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (!record.IsLayout && !record.IsAnonymous && !record.Name.StartsWith("*"))
                        {
                            blockIds.Add(id);
                        }
                    }

                    tr.Commit();
                }

                var map = new IdMapping();
                if (textStyleIds.Count > 0)
                {
                    sourceDb.WblockCloneObjects(textStyleIds, targetDb.TextStyleTableId, map, DuplicateRecordCloning.Ignore, false);
                }
                if (dimStyleIds.Count > 0)
                {
                    sourceDb.WblockCloneObjects(dimStyleIds, targetDb.DimStyleTableId, map, DuplicateRecordCloning.Ignore, false);
                }
                if (blockIds.Count > 0)
                {
                    sourceDb.WblockCloneObjects(blockIds, targetDb.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
                }

                EnsureUnicodeStylesUseArial(targetDb);
            }
            catch (Exception)
            {
                // Fallback graceful
            }
        }

        public static void EnsureStandardLayers(Database db)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            // Bảng layer chuẩn VHG
            (string Name, short ColorIndex)[] defs =
            {
                ("VHG-BT", 4),       // Nét thấy bê tông (Cyan)
                ("VHG-VL", 251),     // Hatch vật liệu (Xám nhạt)
                ("VHG-Thep", 1),     // Nét thấy cốt thép (Đỏ)
                ("VHG-Thep2", 2),    // Nét phụ cốt thép (Vàng)
                ("VHG-Thep3", 3),    // Cốt thép 3 (Xanh lá)
                ("VHG-Text", 7),     // Chữ / Tiêu đề (Trắng/Đen)
                ("VHG-Dim", 140),    // Kích thước (Xanh dương nhạt)
                ("VHG-Dashed", 5),   // Nét khuất (Xanh dương)
                ("VHG-Center", 6),   // Trục tim cống / tim ga (Magenta)
                ("VHG-Nap1", 4),     // Nắp ga (Cyan)
                ("VHG-Cong", 5),     // Cống thoát nước (Xanh dương)
                ("VHG-Khung", 7)     // Khung tên A3 (Trắng)
            };

            foreach (var (name, colorIndex) in defs)
            {
                if (!layers.Has(name))
                {
                    layers.UpgradeOpen();
                    using var record = new LayerTableRecord
                    {
                        Name = name,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                    };
                    layers.Add(record);
                    tr.AddNewlyCreatedDBObject(record, true);
                    layers.DowngradeOpen();
                }
            }

            tr.Commit();
        }

        public static void EnsureStandardTextStyles(Database db)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

            string[] names = { "VHG-Unicode.Regular", "VHG-Unicode.Bold" };
            foreach (var name in names)
            {
                if (!styles.Has(name))
                {
                    styles.UpgradeOpen();
                    using var record = new TextStyleTableRecord
                    {
                        Name = name,
                        FileName = "Arial.ttf",
                        TextSize = 0
                    };
                    styles.Add(record);
                    tr.AddNewlyCreatedDBObject(record, true);
                    styles.DowngradeOpen();
                }
            }

            tr.Commit();
        }

        private static void EnsureUnicodeStylesUseArial(Database db)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            foreach (string name in new[] { "VHG-Unicode.Regular", "VHG-Unicode.Bold" })
            {
                if (!styles.Has(name)) continue;
                var style = (TextStyleTableRecord)tr.GetObject(styles[name], OpenMode.ForWrite);
                style.FileName = "Arial.ttf";
            }
            tr.Commit();
        }

        public static ObjectId GetDimStyleId(Database db, string styleName = "VHG1-30")
        {
            using var tr = db.TransactionManager.StartTransaction();
            var dimStyles = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (dimStyles.Has(styleName))
            {
                return dimStyles[styleName];
            }
            return db.Dimstyle;
        }

        public static ObjectId GetTextStyleId(Database db, string styleName = "VHG-Unicode.Regular")
        {
            using var tr = db.TransactionManager.StartTransaction();
            var textStyles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (textStyles.Has(styleName))
            {
                return textStyles[styleName];
            }
            return db.Textstyle;
        }
    }
}
