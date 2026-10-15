using System.Collections.Immutable;
using DBTool.Domain;
namespace DBTool.Application.Templates;
public enum InspectionStatus { Unverified, Inspected }
public enum AssetKind { Layer, Block, TextStyle, DimStyle, Hatch, Linetype }
public enum TemplateValidationMode { ProductionRender, SchemaOnly }
public enum TemplateShapeKind { Unknown, RoundRing, RectangularFourBar, Mixed, Other }

public sealed record TemplateAnchor
{
    public TemplateAnchor(double x, double y, double z)
    {
        X = ContractGuard.Finite(x, nameof(x));
        Y = ContractGuard.Finite(y, nameof(y));
        Z = ContractGuard.Finite(z, nameof(z));
    }
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}

public sealed record TemplateGeometryPreview
{
    public TemplateGeometryPreview(TemplateShapeKind shapeKind, IEnumerable<KeyValuePair<string, int>> entityTypeCounts, string geometryHash, string preview,
        double widthMm = 0, double heightMm = 0, double depthMm = 0)
    {
        ShapeKind = ContractGuard.EnumValue(shapeKind);
        var counts = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var pair in entityTypeCounts)
        {
            ContractGuard.Text(pair.Key, nameof(entityTypeCounts));
            if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(entityTypeCounts));
            counts.Add(pair.Key, pair.Value);
        }
        EntityTypeCounts = counts.ToImmutable();
        GeometryHash = ContractGuard.Hash(geometryHash);
        Preview = ContractGuard.Text(preview, nameof(preview));
        WidthMm = ContractGuard.NonNegative(widthMm, nameof(widthMm));
        HeightMm = ContractGuard.NonNegative(heightMm, nameof(heightMm));
        DepthMm = ContractGuard.NonNegative(depthMm, nameof(depthMm));
    }
    public TemplateShapeKind ShapeKind { get; }
    public ImmutableDictionary<string, int> EntityTypeCounts { get; }
    public string GeometryHash { get; }
    public string Preview { get; }
    public double WidthMm { get; }
    public double HeightMm { get; }
    public double DepthMm { get; }
}

public sealed record TemplateBlockDefinition
{
    public TemplateBlockDefinition(string name, IEnumerable<string> attributeTags, IEnumerable<string> dynamicProperties, TemplateGeometryPreview geometry)
    {
        Name = ContractGuard.Text(name, nameof(name));
        AttributeTags = ContractGuard.Copy(attributeTags);
        DynamicProperties = ContractGuard.Copy(dynamicProperties);
        Geometry = ContractGuard.Required(geometry);
    }
    public string Name { get; }
    public ImmutableArray<string> AttributeTags { get; }
    public ImmutableArray<string> DynamicProperties { get; }
    public TemplateGeometryPreview Geometry { get; }
}

public sealed record TemplateCoverCatalogEntry
{
    public TemplateCoverCatalogEntry(string catalogId, string blockName, TemplateAnchor anchor, double nativeWidthMm, double nativeHeightMm, double nativeDepthMm, double scale)
    {
        CatalogId = ContractGuard.Text(catalogId, nameof(catalogId));
        BlockName = ContractGuard.Text(blockName, nameof(blockName));
        Anchor = ContractGuard.Required(anchor);
        NativeWidthMm = ContractGuard.NonNegative(nativeWidthMm, nameof(nativeWidthMm));
        NativeHeightMm = ContractGuard.NonNegative(nativeHeightMm, nameof(nativeHeightMm));
        NativeDepthMm = ContractGuard.NonNegative(nativeDepthMm, nameof(nativeDepthMm));
        Scale = ContractGuard.Positive(scale, nameof(scale));
    }
    public string CatalogId { get; }
    public string BlockName { get; }
    public TemplateAnchor Anchor { get; }
    public double NativeWidthMm { get; }
    public double NativeHeightMm { get; }
    public double NativeDepthMm { get; }
    public double Scale { get; }
}

public sealed record TemplateInventory
{
    public TemplateInventory(IEnumerable<string> layers, IEnumerable<string> linetypes, IEnumerable<string> textStyles,
        IEnumerable<string> dimStyles, IEnumerable<string> hatchPatterns, IEnumerable<TemplateBlockDefinition> blocks,
        IEnumerable<TemplateCoverCatalogEntry> covers)
    {
        Layers = ContractGuard.Copy(layers);
        Linetypes = ContractGuard.Copy(linetypes);
        TextStyles = ContractGuard.Copy(textStyles);
        DimStyles = ContractGuard.Copy(dimStyles);
        HatchPatterns = ContractGuard.Copy(hatchPatterns);
        Blocks = ContractGuard.Copy(blocks);
        Covers = ContractGuard.Copy(covers);
    }
    public ImmutableArray<string> Layers { get; }
    public ImmutableArray<string> Linetypes { get; }
    public ImmutableArray<string> TextStyles { get; }
    public ImmutableArray<string> DimStyles { get; }
    public ImmutableArray<string> HatchPatterns { get; }
    public ImmutableArray<TemplateBlockDefinition> Blocks { get; }
    public ImmutableArray<TemplateCoverCatalogEntry> Covers { get; }
}

public sealed record TemplateManifest
{
    public TemplateManifest(string schemaVersion, string sourceFileHash, InspectionStatus inspectionStatus, IEnumerable<TemplateAsset> assets, TemplateInventory? inventory = null)
    {
        SchemaVersion = ContractGuard.Text(schemaVersion, nameof(schemaVersion));
        SourceFileHash = ContractGuard.Hash(sourceFileHash);
        InspectionStatus = ContractGuard.EnumValue(inspectionStatus);
        Assets = ContractGuard.Copy(assets);
        Inventory = inventory;

    }
    public string SchemaVersion { get; }
    public string SourceFileHash { get; }
    public InspectionStatus InspectionStatus { get; }
    public ImmutableArray<TemplateAsset> Assets { get; }
    public TemplateInventory? Inventory { get; }
}
public sealed record TemplateAsset
{
    public TemplateAsset(string role, AssetKind assetKind, string name, bool required, IEnumerable<string> attributeTags, SourceRef? verificationSource, TemplateAssetSignature? signature = null)
    {
        Role = ContractGuard.Text(role, nameof(role));
        AssetKind = ContractGuard.EnumValue(assetKind);
        Name = ContractGuard.Text(name, nameof(name));
        Required = required;
        AttributeTags = ContractGuard.Copy(attributeTags);
        VerificationSource = verificationSource;
        Signature = signature;
        if(AttributeTags.Any(string.IsNullOrWhiteSpace) || AttributeTags.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=AttributeTags.Length) throw new ArgumentException("Invalid or duplicate attribute tag.");
        if(assetKind!=AssetKind.Block && !AttributeTags.IsEmpty) throw new ArgumentException("Only blocks have ATTDEF tags.");
    }
    public string Role { get; }
    public AssetKind AssetKind { get; }
    public string Name { get; }
    public bool Required { get; }
    public ImmutableArray<string> AttributeTags { get; }
    public SourceRef? VerificationSource { get; }
    public TemplateAssetSignature? Signature { get; }
}
public sealed record TemplateRequirement
{
    public TemplateRequirement(string role, AssetKind kind, IEnumerable<string> requiredTags, IEnumerable<KeyValuePair<string, string>>? nativeProperties = null)
    {
        Role = ContractGuard.Text(role, nameof(role));
        Kind = ContractGuard.EnumValue(kind);
        RequiredTags = ContractGuard.Copy(requiredTags);
        NativeProperties = nativeProperties is null ? ImmutableDictionary<string, string>.Empty : nativeProperties.ToImmutableDictionary(StringComparer.Ordinal);
        if(RequiredTags.Any(string.IsNullOrWhiteSpace) || RequiredTags.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=RequiredTags.Length) throw new ArgumentException("Invalid required tags.");
        if(kind!=AssetKind.Block && !RequiredTags.IsEmpty) throw new ArgumentException("Only blocks have required tags.");
    }
    public string Role { get; }
    public AssetKind Kind { get; }
    public ImmutableArray<string> RequiredTags { get; }
    public ImmutableDictionary<string, string> NativeProperties { get; }
}
public sealed record ValidatedTemplateManifest
{
    internal ValidatedTemplateManifest(TemplateManifest manifest, TemplateValidationMode mode)
    {
        Manifest = ContractGuard.Required(manifest);
        Mode = ContractGuard.EnumValue(mode);

    }
    public bool CanRender => Mode == TemplateValidationMode.ProductionRender;
    public TemplateManifest Manifest { get; }
    public TemplateValidationMode Mode { get; }
}
