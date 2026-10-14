using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Domain.StageContractsV2;

namespace DBTool.AutoCAD.Template;

public sealed record TemplateReadRequest(
    string Path,
    string? ExpectedHash,
    IReadOnlyList<TemplateRequirement> Roles,
    string? RelativePath = null);

public static class TemplateRoleCatalog
{
    public static IReadOnlyList<TemplateRequirement> RequiredRoles { get; } =
    [
        new("SteelSummary", AssetKind.Block, ["TONG"]),
        new("PanelSummary", AssetKind.Block, ["TONG"]),
        new("CatchSummary", AssetKind.Block, ["TONG"]),
        new("Geometry/DowelHole", AssetKind.Block, []),
        new("RoundOpeningReinforcement", AssetKind.Block, []),
        new("RectangularOpeningReinforcement", AssetKind.Block, [])
    ];
}

public sealed class TemplateManifestReader
{
    private readonly TemplateResourceLoader _resources;

    public TemplateManifestReader(TemplateResourceLoader? resources = null) => _resources = resources ?? new TemplateResourceLoader();

    public OperationResult<TemplateManifest> Read(TemplateReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Roles);
        var diagnostics = new List<Diagnostic>();
        string fullPath;
        string sourceHash;
        try
        {
            fullPath = _resources.ResolveReadOnly(request.Path);
            sourceHash = _resources.HashBytes(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure("TEMPLATE_READ_FAILED", ex.Message);
        }

        if (request.ExpectedHash is not null && !string.Equals(sourceHash, request.ExpectedHash, StringComparison.OrdinalIgnoreCase))
        {
            var source = CreateSource(request.RelativePath, sourceHash);
            diagnostics.Add(Block("TEMPLATE_SOURCE_HASH_MISMATCH", "Template bytes do not match the expected SHA256.", source,
                request.ExpectedHash, sourceHash));
            return new(false, null, diagnostics);
        }

        var sourceRef = CreateSource(request.RelativePath, sourceHash);
        try
        {
            using var database = new Database(false, true);
            database.ReadDwgFile(fullPath, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
            using var transaction = database.TransactionManager.StartTransaction();

            var layers = ReadNames<LayerTable, LayerTableRecord>(transaction, database.LayerTableId);
            var linetypes = ReadNames<LinetypeTable, LinetypeTableRecord>(transaction, database.LinetypeTableId);
            var textStyles = ReadNames<TextStyleTable, TextStyleTableRecord>(transaction, database.TextStyleTableId);
            var dimStyles = ReadNames<DimStyleTable, DimStyleTableRecord>(transaction, database.DimStyleTableId);
            var blocks = ReadBlocks(transaction, database.BlockTableId, sourceRef, request.Roles, out var hatchPatterns);
            var inventory = new TemplateInventory(layers, linetypes, textStyles, dimStyles, hatchPatterns,
                blocks.Select(x => x.Definition), blocks.Where(x => IsCoverBlock(x.Definition.Name))
                    .Select(x => new TemplateCoverCatalogEntry(
                        Path.GetFileNameWithoutExtension(x.Definition.Name), x.Definition.Name, x.Anchor,
                        x.Definition.Geometry.WidthMm, x.Definition.Geometry.HeightMm, x.Definition.Geometry.DepthMm, 1)));

            var assets = new List<TemplateAsset>();
            assets.AddRange(layers.Select(name => SymbolAsset(AssetKind.Layer, name)));
            assets.AddRange(linetypes.Select(name => SymbolAsset(AssetKind.Linetype, name)));
            assets.AddRange(textStyles.Select(name => SymbolAsset(AssetKind.TextStyle, name)));
            assets.AddRange(dimStyles.Select(name => SymbolAsset(AssetKind.DimStyle, name)));
            TemplateAsset SymbolAsset(AssetKind kind, string name) => new($"{kind}:{name}", kind, name,
                IsRequired(request.Roles, $"{kind}:{name}"), [], sourceRef, NativeAssetSignatures.Find(database, transaction, kind, name));
            assets.AddRange(hatchPatterns.Select(name => new TemplateAsset($"Hatch:{name}", AssetKind.Hatch, name, IsRequired(request.Roles, $"Hatch:{name}"), [], sourceRef)));
            foreach (var block in blocks)
            {
                var role = MapBlockRole(block.Definition.Name, block.Definition.Geometry.ShapeKind);
                assets.Add(new TemplateAsset(role, AssetKind.Block, block.Definition.Name,
                    IsRequired(request.Roles, role), block.Definition.AttributeTags, sourceRef, BlockSignature(block)));
                if (IsShapeSensitiveBlock(block.Definition.Name) && block.Definition.Geometry.ShapeKind is TemplateShapeKind.Unknown or TemplateShapeKind.Mixed or TemplateShapeKind.Other)
                    diagnostics.Add(Review("TEMPLATE_SHAPE_UNRESOLVED", $"Geometry preview could not identify the role shape for {block.Definition.Name}; the role remains unresolved and production rendering must be blocked.", sourceRef,
                        "RoundRing or RectangularFourBar", block.Definition.Geometry.ShapeKind.ToString()));
            }

            transaction.Commit();
            var manifest = new TemplateManifest("template-manifest-v2", sourceHash, InspectionStatus.Inspected, assets, inventory);
            return diagnostics.Any(x => x.Severity == DiagnosticSeverity.Block)
                ? new(false, null, diagnostics)
                : new(true, manifest, diagnostics);
        }
        catch (Exception ex) when (ex is Autodesk.AutoCAD.Runtime.Exception or IOException or InvalidOperationException)
        {
            diagnostics.Add(Block("TEMPLATE_READ_FAILED", ex.Message, sourceRef));
            return new(false, null, diagnostics);
        }
    }

    private static ImmutableArray<string> ReadNames<TTable, TRecord>(Transaction transaction, ObjectId tableId)
        where TTable : SymbolTable
        where TRecord : SymbolTableRecord
    {
        var table = (TTable)transaction.GetObject(tableId, OpenMode.ForRead);
        var values = new List<string>();
        foreach (ObjectId id in table)
            values.Add(((TRecord)transaction.GetObject(id, OpenMode.ForRead)).Name);
        return values.OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray();
    }

    private static List<BlockInspection> ReadBlocks(Transaction transaction, ObjectId tableId, SourceRef source,
        IReadOnlyList<TemplateRequirement> roles, out ImmutableArray<string> hatchPatterns)
    {
        var table = (BlockTable)transaction.GetObject(tableId, OpenMode.ForRead);
        var blocks = new List<BlockInspection>();
        var hatches = new HashSet<string>(StringComparer.Ordinal);
        foreach (ObjectId id in table)
        {
            var definition = (BlockTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            var tags = new List<string>();
            var entityRecords = new List<string>();
            var entityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var circles = 0;
            var linearEntities = 0;
            var extents = new List<Extents3d>();
            foreach (ObjectId entityId in definition)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is not DBObject dbObject) continue;
                if (dbObject is AttributeDefinition attribute)
                    tags.Add(attribute.Tag);
                if (dbObject is not Entity entity) continue;
                var typeName = entity.GetType().Name;
                entityCounts[typeName] = entityCounts.GetValueOrDefault(typeName) + 1;
                entityRecords.Add(EntityFingerprint(transaction, entity));
                if (entity is Circle circle)
                {
                    circles++;
                    entityRecords.Add($"circle:{Format(circle.Center.X)},{Format(circle.Center.Y)},{Format(circle.Center.Z)};r={Format(circle.Radius)}");
                }
                else if (entity is Line line)
                {
                    linearEntities++;
                    entityRecords.Add($"line:{Format(line.StartPoint.X)},{Format(line.StartPoint.Y)},{Format(line.StartPoint.Z)}->{Format(line.EndPoint.X)},{Format(line.EndPoint.Y)},{Format(line.EndPoint.Z)}");
                }
                else if (entity is Polyline polyline)
                {
                    linearEntities += polyline.NumberOfVertices;
                    entityRecords.Add($"polyline:vertices={polyline.NumberOfVertices};closed={polyline.Closed};elevation={Format(polyline.Elevation)};normal={polyline.Normal}");
                    for (var vertex = 0; vertex < polyline.NumberOfVertices; vertex++)
                        entityRecords.Add($"vertex:{vertex}:{polyline.GetPoint2dAt(vertex)}:{Format(polyline.GetBulgeAt(vertex))}:{Format(polyline.GetStartWidthAt(vertex))}:{Format(polyline.GetEndWidthAt(vertex))}");
                }
                if (entity is Hatch hatch && !string.IsNullOrWhiteSpace(hatch.PatternName)) hatches.Add(hatch.PatternName);
                try { extents.Add(entity.GeometricExtents); } catch (Autodesk.AutoCAD.Runtime.Exception) { }
            }

            var shape = circles > 0 && linearEntities == 0
                ? TemplateShapeKind.RoundRing
                : circles == 0 && linearEntities >= 4
                    ? TemplateShapeKind.RectangularFourBar
                    : circles > 0 && linearEntities > 0 ? TemplateShapeKind.Mixed
                    : entityRecords.Count == 0 ? TemplateShapeKind.Unknown : TemplateShapeKind.Other;
            var bounds = Bounds(extents);
            var canonical = string.Join("\n", entityRecords.OrderBy(x => x, StringComparer.Ordinal));
            var geometryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            var preview = $"shape={shape};entities={string.Join(',', entityCounts.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{x.Value}"))};bounds={bounds}";
            var geometry = new TemplateGeometryPreview(shape, entityCounts, geometryHash, preview,
                bounds.Width, bounds.Height, bounds.Depth);
            var dynamicProperties = ReadDynamicProperties(transaction, definition);
            var block = new TemplateBlockDefinition(definition.Name, tags.OrderBy(x => x, StringComparer.Ordinal), dynamicProperties, geometry);
            blocks.Add(new BlockInspection(block, new TemplateAnchor(definition.Origin.X, definition.Origin.Y, definition.Origin.Z), definition.Units.ToString()));
        }
        hatchPatterns = hatches.OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray();
        return blocks.OrderBy(x => x.Definition.Name, StringComparer.Ordinal).ToList();
    }

    private static ImmutableArray<string> ReadDynamicProperties(Transaction transaction, BlockTableRecord definition)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (ObjectId referenceId in definition.GetBlockReferenceIds(true, true))
            {
                if (transaction.GetObject(referenceId, OpenMode.ForRead) is not BlockReference reference) continue;
                foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
                    if (!string.IsNullOrWhiteSpace(property.PropertyName)) properties.Add(property.PropertyName);
            }
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            // Dynamic properties are optional inventory; the definition and its source hash remain observable.
        }
        return properties.OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray();
    }

    private static string MapBlockRole(string name, TemplateShapeKind shape) => name switch
    {
        "TK.THKL" => "SteelSummary",
        "TK.TG" => "AngleScheduleRow",
        "TK.THKLNAP" => "PanelSummary",
        "TK.GC.T" when shape == TemplateShapeKind.RoundRing => "RoundOpeningReinforcement",
        "TK.GC.H" when shape == TemplateShapeKind.RectangularFourBar => "RectangularOpeningReinforcement",
        _ => $"Block:{name}"
    };

    private static bool IsShapeSensitiveBlock(string name) => name is "TK.GC.T" or "TK.GC.H";
    private static bool IsCoverBlock(string name) => name.Contains(".MB", StringComparison.OrdinalIgnoreCase) ||
        name.Contains(".AA", StringComparison.OrdinalIgnoreCase) || name.Contains(".BB", StringComparison.OrdinalIgnoreCase);
    private static bool IsRequired(IReadOnlyList<TemplateRequirement> roles, string role) => roles.Any(x => string.Equals(x.Role, role, StringComparison.Ordinal));

    private static SourceRef CreateSource(string? relativePath, string hash) =>
        new(SourceKind.Dwg, NormalizeRelative(relativePath), hash, null, null, null, "read-only DWG manifest inspection");

    private static string NormalizeRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "template.dwg";
        var normalized = path.Replace('\\', '/');
        return Path.IsPathRooted(normalized) || normalized.Contains(':') ? Path.GetFileName(normalized) : normalized;
    }

    private static Diagnostic Block(string code, string message, SourceRef? source = null, string? expected = null, string? actual = null) =>
        new(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, expected, actual, message);

    private static Diagnostic Review(string code, string message, SourceRef? source = null, string? expected = null, string? actual = null) =>
        new(code, DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, source, expected, actual, message);

    private static string EntityFingerprint(Transaction transaction, Entity entity)
    {
        string P(Point3d p) => $"{Format(p.X)},{Format(p.Y)},{Format(p.Z)}";
        string V(Vector3d p) => $"{Format(p.X)},{Format(p.Y)},{Format(p.Z)}";
        var content = entity switch
        {
            AttributeDefinition a => FormattableString.Invariant($"att:{a.Tag}:{a.Prompt}:{a.TextString}:{P(a.Position)}:{a.Height}:{a.WidthFactor}:{a.Rotation}:{a.Invisible}:{a.Constant}:{SymbolName(a.TextStyleId)}"),
            DBText t => FormattableString.Invariant($"text:{t.TextString}:{P(t.Position)}:{t.Height}:{t.WidthFactor}:{t.Rotation}:{SymbolName(t.TextStyleId)}"),
            MText t => FormattableString.Invariant($"mtext:{t.Contents}:{P(t.Location)}:{t.TextHeight}:{t.Width}:{t.Rotation}:{SymbolName(t.TextStyleId)}"),
            Arc a => FormattableString.Invariant($"arc:{P(a.Center)}:{V(a.Normal)}:{a.Radius}:{a.StartAngle}:{a.EndAngle}"),
            BlockReference b => FormattableString.Invariant($"insert:{SymbolName(b.BlockTableRecord)}:{P(b.Position)}:{b.ScaleFactors.X},{b.ScaleFactors.Y},{b.ScaleFactors.Z}:{b.Rotation}:{V(b.Normal)}"),
            Hatch h => FormattableString.Invariant($"hatch:{h.PatternName}:{h.PatternScale}:{h.PatternAngle}"),
            RotatedDimension d => FormattableString.Invariant($"dimension:{P(d.XLine1Point)}:{P(d.XLine2Point)}:{P(d.DimLinePoint)}:{d.Rotation}:{P(d.TextPosition)}:{V(d.Normal)}:{NativeAssetSignatures.DimensionProperties(transaction, d).Hash}"),
            AlignedDimension d => $"dimension:{P(d.XLine1Point)}:{P(d.XLine2Point)}:{P(d.DimLinePoint)}:{P(d.TextPosition)}:{V(d.Normal)}:{NativeAssetSignatures.DimensionProperties(transaction, d).Hash}",
            Dimension d => $"dimension:{P(d.TextPosition)}:{V(d.Normal)}:{NativeAssetSignatures.DimensionProperties(transaction, d).Hash}",
            _ => ""
        };
        string SymbolName(ObjectId id) => id.IsNull ? "null" : ((SymbolTableRecord)transaction.GetObject(id, OpenMode.ForRead)).Name;
        var style = FormattableString.Invariant($"{entity.Color}:{entity.Linetype}:{entity.LinetypeScale}:{entity.LineWeight}:{content}");
        try
        {
            var extents = entity.GeometricExtents;
            return $"{entity.GetType().FullName}|{entity.Layer}|{style}|{Format(extents.MinPoint.X)},{Format(extents.MinPoint.Y)},{Format(extents.MinPoint.Z)}|{Format(extents.MaxPoint.X)},{Format(extents.MaxPoint.Y)},{Format(extents.MaxPoint.Z)}";
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return $"{entity.GetType().FullName}|{entity.Layer}|{style}|no-extents";
        }
    }

    private static TemplateAssetSignature BlockSignature(BlockInspection inspection) => new(new Dictionary<string, string>
    {
        ["GeometryHash"] = inspection.Definition.Geometry.GeometryHash,
        ["AttributeTags"] = string.Join("\n", inspection.Definition.AttributeTags),
        ["DynamicProperties"] = string.Join("\n", inspection.Definition.DynamicProperties),
        ["Origin"] = $"{Format(inspection.Anchor.X)},{Format(inspection.Anchor.Y)},{Format(inspection.Anchor.Z)}",
        ["Units"] = inspection.Units,
        ["NativeWidthMm"] = Format(inspection.Definition.Geometry.WidthMm),
        ["NativeHeightMm"] = Format(inspection.Definition.Geometry.HeightMm)
    });

    public static IReadOnlyList<TemplateAsset> ReadTargetAssets(Database database, Transaction transaction, TemplateManifest source)
    {
        var result = new List<TemplateAsset>();
        // Model/layout and anonymous blocks are not imported named resources.
        var sourceBlocks = source.Assets.Where(a => a.AssetKind == AssetKind.Block).Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocks = sourceBlocks.Count == 0 ? [] : ReadBlocks(transaction, database.BlockTableId,
            source.Assets.First().VerificationSource!, [], out _);
        foreach (var expected in source.Assets.Where(a => a.AssetKind != AssetKind.Hatch))
        {
            var signature = expected.AssetKind == AssetKind.Block
                ? blocks.Where(b => string.Equals(b.Definition.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(BlockSignature).FirstOrDefault()
                : NativeAssetSignatures.Find(database, transaction, expected.AssetKind, expected.Name);
            if (signature is not null)
                result.Add(new(expected.Role, expected.AssetKind, expected.Name, expected.Required, expected.AttributeTags, null, signature));
        }
        return result;
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static BoundsValue Bounds(IEnumerable<Extents3d> values)
    {
        var extents = values.ToArray();
        if (extents.Length == 0) return new(0, 0, 0, "empty");
        var minX = extents.Min(x => x.MinPoint.X); var minY = extents.Min(x => x.MinPoint.Y); var minZ = extents.Min(x => x.MinPoint.Z);
        var maxX = extents.Max(x => x.MaxPoint.X); var maxY = extents.Max(x => x.MaxPoint.Y); var maxZ = extents.Max(x => x.MaxPoint.Z);
        return new(maxX - minX, maxY - minY, maxZ - minZ,
            $"{Format(minX)},{Format(minY)},{Format(minZ)}->{Format(maxX)},{Format(maxY)},{Format(maxZ)}");
    }

    private sealed record BlockInspection(TemplateBlockDefinition Definition, TemplateAnchor Anchor, string Units);
    private sealed record BoundsValue(double Width, double Height, double Depth, string Text);
    private static OperationResult<TemplateManifest> Failure(string code, string message) =>
        new(false, null, [Block(code, message)]);

    public static AssetsV2 ToAssetsV2(TemplateManifest manifest, string fontPolicyId = "NR-16", string units = "mm")
    {
        var definitions = manifest.Assets.Select(a =>
        {
            var p = a.Signature?.Properties;
            double x = 0, y = 0, z = 0;
            if (p != null && p.TryGetValue("Origin", out var oStr))
            {
                var parts = oStr.Split(',');
                if (parts.Length == 3)
                {
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                }
            }
            var bp = new Point3V2(x, y, z);
            var bounds = new Bounds2V2(0, 0, 0, 0);
            var attrs = a.AttributeTags.Select(tag => new AttributeDefV2(
                tag, "", "", 200, 1.0, bp, bp, 0, "Left", "Base")).ToArray();

            return new AssetV2(
                a.Name,
                "",
                a.Signature?.Hash ?? "",
                bp,
                bounds,
                a.Role,
                Array.Empty<string>(),
                attrs,
                "",
                null,
                a.VerificationSource != null ? new[] { a.VerificationSource.Sha256 } : Array.Empty<string>()
            );
        }).ToArray();

        return new AssetsV2(
            manifest.SourceFileHash,
            definitions,
            Array.Empty<GlyphV2>(),
            fontPolicyId,
            units,
            Array.Empty<ProductContractV2>(),
            Array.Empty<KerningV2>(),
            Array.Empty<LayerMetricV2>(),
            Array.Empty<FontMetricStyleV2>()
        );
    }
}
