using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application.Templates;
using DBTool.Domain;

namespace DBTool.AutoCAD.Template;

public static class TemplateAssetImporter
{
    // Built-ins are not cloned explicitly, but their definitions can still win during dependency cloning.
    // Compare them too. *D dimension graphics are regenerated caches, not named assets;
    // their owning dimension's native parameters are included in block signatures instead.
    public static TemplateManifest DependencyContract(TemplateManifest manifest) => new(manifest.SchemaVersion,
        manifest.SourceFileHash, manifest.InspectionStatus, manifest.Assets.Where(a => a.AssetKind != AssetKind.Hatch &&
            (a.AssetKind != AssetKind.Block || a.Name is not ("*Model_Space" or "*Paper_Space") &&
                !a.Name.StartsWith("*Paper_Space", StringComparison.Ordinal) && !a.Name.StartsWith("*D", StringComparison.Ordinal))), manifest.Inventory);
    // Built-ins cannot be imported as named VHG resources. Production primitives never select them.
    public static TemplateManifest Importable(TemplateManifest manifest) => new(manifest.SchemaVersion,
        manifest.SourceFileHash, manifest.InspectionStatus, manifest.Assets.Where(a => a.AssetKind != AssetKind.Hatch &&
            a.Name is not ("0" or "Defpoints" or "Standard" or "ByLayer" or "ByBlock" or "Continuous") &&
            !a.Name.StartsWith("*", StringComparison.Ordinal)), manifest.Inventory);

    public static OperationResult<TemplateManifest> ValidateTarget(Database target, TemplateManifest source, bool allowMissing = false)
    {
        using var transaction = target.TransactionManager.StartTransaction();
        return TemplateTargetValidator.Validate(source, TemplateManifestReader.ReadTargetAssets(target, transaction, source), allowMissing);
    }

    public static void VerifyUnits(Database db, string stage)
    {
        if (db.Insunits != UnitsValue.Millimeters && db.Insunits != UnitsValue.Undefined)
            throw new InvalidOperationException($"Database INSUNITS at {stage} must be Millimeters (4) or Undefined (0), actual: {db.Insunits}");
    }

    public static void Import(Database source, Database target, TemplateManifest manifest)
    {
        VerifyUnits(source, "preflight-source");
        VerifyUnits(target, "preflight-target");

        var resources = Importable(manifest);
        var preflight = ValidateTarget(target, DependencyContract(manifest), allowMissing: true);
        if (!preflight.Success)
            throw new InvalidOperationException(string.Join("\n", preflight.Diagnostics.Select(d => $"{d.Code}: {d.Message} (MigrationVersion: {NativeAssetSignatures.CanonicalBundleVersion})")));

        var map = new IdMapping();
        // Dependencies first. Ignore is safe only after the content comparison above.
        foreach (var kind in new[] { AssetKind.Linetype, AssetKind.TextStyle, AssetKind.Layer, AssetKind.DimStyle, AssetKind.Block })
        {
            ObjectId SourceTable() => kind switch
            {
                AssetKind.Linetype => source.LinetypeTableId, AssetKind.TextStyle => source.TextStyleTableId,
                AssetKind.Layer => source.LayerTableId, AssetKind.DimStyle => source.DimStyleTableId, _ => source.BlockTableId
            };
            var targetTable = kind switch
            {
                AssetKind.Linetype => target.LinetypeTableId, AssetKind.TextStyle => target.TextStyleTableId,
                AssetKind.Layer => target.LayerTableId, AssetKind.DimStyle => target.DimStyleTableId, _ => target.BlockTableId
            };
            var ids = new ObjectIdCollection();
            using (var tr = source.TransactionManager.StartTransaction())
            {
                var table = (SymbolTable)tr.GetObject(SourceTable(), OpenMode.ForRead);
                foreach (var asset in resources.Assets.Where(a => a.AssetKind == kind)) ids.Add(table[asset.Name]);
            }
            if (ids.Count > 0) source.WblockCloneObjects(ids, targetTable, map, DuplicateRecordCloning.Ignore, false);
        }
        var postflight = ValidateTarget(target, resources);
        if (!postflight.Success)
            throw new InvalidOperationException(string.Join("\n", postflight.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        VerifyUnits(target, "postflight-target");
    }
}
