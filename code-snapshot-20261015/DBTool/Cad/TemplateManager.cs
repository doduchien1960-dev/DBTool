using System;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.AutoCAD.Template;
using DBTool.Application.Templates;

namespace DBTool.Cad
{
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
            foreach (var path in possiblePaths) if (File.Exists(path)) return path;
            return possiblePaths[0];
        }

        public static void EnsureTemplateLoaded(Database targetDb)
        {
            var path = ResolveTemplatePath();
            var read = new TemplateManifestReader().Read(new TemplateReadRequest(path, null, VhgTemplateRoles.Presentation));
            if (!read.Success) throw new InvalidOperationException(string.Join("\n", read.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
            var contract = new TemplateContractValidator().Validate(read.Value!, VhgTemplateRoles.Presentation);
            if (!contract.Success) throw new InvalidOperationException(string.Join("\n", contract.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
            using var source = new Database(false, true);
            source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
            // No synthetic fonts/layers are created before cloning, and native records are never rewritten.
            TemplateAssetImporter.Import(source, targetDb, read.Value!);
        }

        public static void EnsureStandardLayers(Database db) => EnsureTemplateLoaded(db);
        public static void EnsureStandardTextStyles(Database db) => EnsureTemplateLoaded(db);

        public static ObjectId GetDimStyleId(Database db, string styleName = "VHG1-30")
        {
            using var tr = db.TransactionManager.StartTransaction();
            var styles = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            return styles.Has(styleName) ? styles[styleName] : throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: DimStyle {styleName}");
        }

        public static ObjectId GetTextStyleId(Database db, string styleName = "VHG-Unicode.Regular")
        {
            using var tr = db.TransactionManager.StartTransaction();
            var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            return styles.Has(styleName) ? styles[styleName] : throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: TextStyle {styleName}");
        }
    }
}
