using DBTool.Domain;
using System.Collections.Immutable;
using System.Globalization;
namespace DBTool.Application.Templates;
public interface ITemplateContractValidator
{
    OperationResult<ValidatedTemplateManifest> Validate(TemplateManifest manifest, IReadOnlyList<TemplateRequirement> requirements, TemplateValidationMode mode = TemplateValidationMode.ProductionRender);
}
public sealed class TemplateContractValidator : ITemplateContractValidator
{
    public OperationResult<ValidatedTemplateManifest> ValidateWithNativeContext(TemplateManifest manifest,IReadOnlyList<TemplateRequirement> requirements,NativeAssetContextR03 context)
    {
        try
        {
            var scale=context.Validate(manifest);
            var rawRequirements=requirements.Select(r=>new TemplateRequirement(r.Role,r.Kind,r.RequiredTags,r.NativeProperties.Select(p=>
                p.Key is "NativeWidthMm" or "NativeHeightMm"?new KeyValuePair<string,string>(p.Key=="NativeWidthMm"?"RawNativeWidthCoordinates":"RawNativeHeightCoordinates",
                    (double.Parse(p.Value,CultureInfo.InvariantCulture)/scale).ToString("R",CultureInfo.InvariantCulture)):p))).ToArray();
            return Validate(manifest,rawRequirements);
        }
        catch(NativeAssetContractException ex){return new(false,null,[new(ex.Code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,ex.Field,null,null,null,ex.Message)]);}
    }
    public OperationResult<ValidatedTemplateManifest> Validate(TemplateManifest manifest, IReadOnlyList<TemplateRequirement> requirements, TemplateValidationMode mode = TemplateValidationMode.ProductionRender)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(requirements);
        ContractGuard.EnumValue(mode);
        var required = ContractGuard.Copy(requirements);
        var diagnostics = new List<Diagnostic>();
        void Block(string code, string message, SourceRef? source = null, string? expected = null, string? actual = null)
            => diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, expected, actual, message));

        if (manifest.SchemaVersion is not ("template-manifest-v1" or "template-manifest-v2"))
            Block("TEMPLATE_SCHEMA_UNSUPPORTED", "Unsupported template schema.", expected: "template-manifest-v1", actual: manifest.SchemaVersion);

        foreach (var group in manifest.Assets.GroupBy(a => a.Role, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Block("TEMPLATE_ROLE_DUPLICATE", $"Multiple assets map to role {group.Key}.");
        foreach (var group in required.GroupBy(r => r.Role, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Block("TEMPLATE_ROLE_DUPLICATE", $"Multiple requirements for role {group.Key}.");

        // Same-name conflict check: do not treat assets with the same name as the same definition if signatures differ
        foreach (var group in manifest.Assets.GroupBy(a => (a.AssetKind, a.Name)))
        {
            var list = group.ToList();
            if (list.Count > 1)
            {
                var first = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    var other = list[i];
                    bool propDiff = first.Signature != null && other.Signature != null &&
                        !first.Signature.Properties.OrderBy(p => p.Key).SequenceEqual(other.Signature.Properties.OrderBy(p => p.Key));
                    bool tagDiff = !first.AttributeTags.OrderBy(t => t).SequenceEqual(other.AttributeTags.OrderBy(t => t));
                    if (propDiff || tagDiff)
                    {
                        Block("TEMPLATE_DEFINITION_CONFLICT", $"Asset {group.Key.Name} of kind {group.Key.AssetKind} has conflicting definitions across roles.",
                            other.VerificationSource);
                    }
                }
            }
        }

        foreach (var asset in manifest.Assets)
        {
            var duplicateTags = asset.AttributeTags.GroupBy(t => t, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
            foreach (var dt in duplicateTags)
                Block("TEMPLATE_DUPLICATE_TAG", $"Asset {asset.Name} contains duplicate tag {dt.Key}.", asset.VerificationSource);
        }

        foreach (var requirement in required)
        {
            var matches = manifest.Assets.Where(a => a.Role == requirement.Role).ToArray();
            if (matches.Length == 0)
            {
                Block(requirement.Kind == AssetKind.Block ? "TEMPLATE_BLOCK_MISSING" : "TEMPLATE_ASSET_MISSING", $"Required role {requirement.Role} is absent.");
                continue;
            }
            if (matches.Length != 1) continue;
            var asset = matches[0];
            if (asset.AssetKind != requirement.Kind)
            {
                Block("TEMPLATE_KIND_MISMATCH", $"Wrong asset kind for {requirement.Role}.", asset.VerificationSource, requirement.Kind.ToString(), asset.AssetKind.ToString());
                continue;
            }
            foreach (var tag in requirement.RequiredTags)
                if (!asset.AttributeTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    Block("TEMPLATE_TAG_MISSING", $"{asset.Name} lacks required tag {tag}.", asset.VerificationSource, tag, string.Join(",", asset.AttributeTags));
            foreach (var property in requirement.NativeProperties)
            {
                string? actual = null;
                asset.Signature?.Properties.TryGetValue(property.Key, out actual);
                if (!string.Equals(property.Value, actual, StringComparison.Ordinal))
                    Block("TEMPLATE_NATIVE_PROPERTY_MISMATCH", $"{asset.Name}: native {property.Key} differs from the VHG presentation contract.",
                        asset.VerificationSource, property.Value, actual);
            }
        }

        foreach (var asset in manifest.Assets.Where(a => a.AssetKind == AssetKind.TextStyle))
        {
            if (asset.Signature != null && asset.Signature.Properties.TryGetValue("FileName", out var fontFile) && string.IsNullOrWhiteSpace(fontFile))
                Block("FONT_FILE_NAME_EMPTY", $"TextStyle {asset.Name} has empty font file name.", asset.VerificationSource);
        }

        if (mode == TemplateValidationMode.ProductionRender)
        {
            if (manifest.InspectionStatus != InspectionStatus.Inspected)
                Block("TEMPLATE_NOT_INSPECTED", "The production DWG has not been inspected.");
            foreach (var asset in manifest.Assets)
            {
                if (manifest.SchemaVersion == "template-manifest-v2" && asset.AssetKind != AssetKind.Hatch && asset.Signature is null)
                    Block("TEMPLATE_SIGNATURE_MISSING", $"Asset {asset.Name} has no native signature.", asset.VerificationSource);
                var source = asset.VerificationSource;
                if (source?.SourceKind is not (SourceKind.Dwg or SourceKind.CadHandle) || source.Sha256 != manifest.SourceFileHash)
                    Block("TEMPLATE_NOT_INSPECTED", $"Asset {asset.Role} requires evidence from this exact DWG.", source, manifest.SourceFileHash, source?.Sha256);
            }
        }
        if (diagnostics.Count > 0) return new(false, null, diagnostics);
        if (mode == TemplateValidationMode.SchemaOnly)
            diagnostics.Add(new Diagnostic("TEMPLATE_SCHEMA_ONLY", DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, null, null, null, "Schema validation does not authorize CAD rendering."));
        return new(true, new ValidatedTemplateManifest(manifest, mode), diagnostics);
    }
}

public sealed record TemplateAssetsBundle(
    DBTool.Domain.StageContractsV2.AssetsV2 Assets,
    ImmutableDictionary<string, DBTool.Domain.StageContractsV2.SourceV2> Sources
);

public static class TemplateAssetsV2Converter
{
    public static TemplateAssetsBundle ConvertToAssetsBundle(
        TemplateManifest manifest,
        string fontPolicyId = "NR-16",
        string units = "mm",
        ImmutableDictionary<string, DBTool.Domain.StageContractsV2.SourceV2>? existingSources = null, NativeAssetContextR03? nativeContext = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var registry = existingSources is null
            ? ImmutableDictionary.CreateBuilder<string, DBTool.Domain.StageContractsV2.SourceV2>(StringComparer.Ordinal)
            : existingSources.ToBuilder();

        void RegisterSource(DBTool.Domain.StageContractsV2.SourceV2 source)
        {
            if (registry.TryGetValue(source.id, out var existing))
            {
                bool identical =
                    existing.id == source.id &&
                    existing.kind == source.kind &&
                    existing.relative_path == source.relative_path &&
                    existing.sha256 == source.sha256 &&
                    existing.sheet == source.sheet &&
                    existing.cell == source.cell &&
                    existing.page == source.page &&
                    existing.handle == source.handle &&
                    existing.raw_text == source.raw_text &&
                    existing.formula_id == source.formula_id &&
                    existing.parent_ids is not null &&
                    source.parent_ids is not null &&
                    existing.parent_ids.SequenceEqual(source.parent_ids, StringComparer.Ordinal);
                if (!identical)
                    throw new InvalidOperationException(
                        $"SOURCE_DESCRIPTOR_CONFLICT: id='{source.id}', " +
                        $"existing={System.Text.Json.JsonSerializer.Serialize(existing)}, " +
                        $"incoming={System.Text.Json.JsonSerializer.Serialize(source)}");
            }
            else
            {
                registry.Add(source.id, source);
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.SourceFileHash) ||
            manifest.SourceFileHash.Length != 64 ||
            !manifest.SourceFileHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException(
                $"SOURCE_HASH_INVALID: expected=SHA256 hex, actual='{manifest.SourceFileHash}'.");
        string bundlePrefix = manifest.SourceFileHash.ToUpperInvariant();
        string bundleSourceId = $"SRC-DWG-{bundlePrefix}";
        var bundleSource = new DBTool.Domain.StageContractsV2.SourceV2(
            bundleSourceId,
            "Dwg",
            "template.dwg",
            manifest.SourceFileHash,
            null,
            null,
            null,
            null,
            "Template DWG bundle",
            [],
            null
        );
        RegisterSource(bundleSource);

        double NativeNumber(TemplateAsset a,string key,bool positive=false)
        {
            if(a.Signature is null || !a.Signature.Properties.TryGetValue(key,out var raw) ||
                !double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var value) || !double.IsFinite(value) || positive && value<=0)
                throw new NativeAssetContractException("ASSET_NATIVE_FIELD_INVALID",$"{a.Name}/{key}","Required finite native metric is absent or invalid.");
            return value;
        }
        string NativeText(TemplateAsset a,string key)
        {
            if(a.Signature is null || !a.Signature.Properties.TryGetValue(key,out var raw) || string.IsNullOrWhiteSpace(raw))
                throw new NativeAssetContractException("ASSET_NATIVE_FIELD_MISSING",$"{a.Name}/{key}","Required native field is absent.");
            return raw;
        }
        // Validate every leaf before applying a binding; malformed native input never becomes a default.
        foreach(var a in manifest.Assets.Where(a=>a.AssetKind==AssetKind.Block))
        {
            foreach(var k in new[]{"Bounds/MinX","Bounds/MinY","Bounds/MaxX","Bounds/MaxY"}) NativeNumber(a,k);
            foreach(var tag in a.AttributeTags)
            {
                NativeNumber(a,$"AttDef/{tag}/Height",true); NativeNumber(a,$"AttDef/{tag}/WidthFactor",true); NativeNumber(a,$"AttDef/{tag}/Rotation");
                var h=NativeText(a,$"AttDef/{tag}/HorizontalMode"); var v=NativeText(a,$"AttDef/{tag}/VerticalMode");
                if(!new[]{"TextLeft","TextCenter","TextRight","TextAlign","TextMid","TextFit","Left","Center","Right","Align","Mid","Fit"}.Contains(h) ||
                   !new[]{"TextBase","TextBottom","TextVertMid","TextTop","Base","Bottom","Middle","Top"}.Contains(v))
                    throw new NativeAssetContractException("ASSET_ATTRIBUTE_MODE_UNSUPPORTED",a.Name+"/"+tag,"Unsupported native justification mode.");
            }
        }
        if(nativeContext is null)throw new NativeAssetContractException("UNIT_BINDING_MISSING","units","Native conversion requires exact bundle-bound asset-context-R03.");
        var scale=nativeContext.Validate(manifest);
        if(units!="mm")throw new NativeAssetContractException("UNIT_TARGET_MISMATCH","units","Output DTO coordinate unit must be mm.");
        double Mm(double raw){var mm=raw*scale;return double.IsFinite(mm)?mm:throw new NativeAssetContractException("ASSET_NATIVE_FIELD_INVALID","normalized_mm","Unit conversion overflow.");}
        foreach(var source in nativeContext.Sources.Values)RegisterSource(source);
        DBTool.Domain.StageContractsV2.Point3V2 Point(TemplateAsset a,string key)
        {
            var parts=NativeText(a,key).Split(',');
            if(parts.Length!=3)throw new NativeAssetContractException("ASSET_NATIVE_FIELD_INVALID",a.Name+"/"+key,"Expected XYZ.");
            var numbers=parts.Select(x=>double.TryParse(x,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:
                throw new NativeAssetContractException("ASSET_NATIVE_FIELD_INVALID",a.Name+"/"+key,"Expected finite XYZ.")).ToArray();
            return new(Mm(numbers[0]),Mm(numbers[1]),Mm(numbers[2]));
        }
        var definitions = new List<DBTool.Domain.StageContractsV2.AssetV2>();
        foreach(var a in manifest.Assets.Where(a=>a.AssetKind==AssetKind.Block))
        {
            var handle=a.VerificationSource?.Handle ?? NativeText(a,"Handle");
            string descId=$"SRC-BLK-{bundlePrefix}-{handle}";
            RegisterSource(new(descId,"CadHandle",a.VerificationSource?.RelativePath??"template.dwg",manifest.SourceFileHash,null,null,null,handle,$"Block definition {a.Name}",[bundleSourceId],null));
            if(!nativeContext.InsertionLayers.TryGetValue(a.Name,out var layer)||!nativeContext.Layers.Any(x=>x.Id==layer))throw new NativeAssetContractException("ASSET_INSERTION_LAYER_UNRESOLVED",a.Name,"Explicit INSERT layer binding required.");
            var attrs=a.AttributeTags.Select(tag=>
            {
                var style=NativeText(a,$"AttDef/{tag}/Style");
                if(!nativeContext.Styles.Any(x=>x.StyleId==style))throw new NativeAssetContractException("FONT_SOURCE_MISSING",style,"Attribute style context absent.");
                if(a.Signature is null||!a.Signature.Properties.TryGetValue($"AttDef/{tag}/DefaultText",out var text))throw new NativeAssetContractException("ASSET_NATIVE_FIELD_MISSING",a.Name+"/"+tag+"/DefaultText","Native default text required, including empty string.");
                return new DBTool.Domain.StageContractsV2.AttributeDefV2(tag,text,style,Mm(NativeNumber(a,$"AttDef/{tag}/Height",true)),
                    NativeNumber(a,$"AttDef/{tag}/WidthFactor",true),Point(a,$"AttDef/{tag}/Position"),Point(a,$"AttDef/{tag}/Alignment"),
                    NativeNumber(a,$"AttDef/{tag}/Rotation"),NativeText(a,$"AttDef/{tag}/HorizontalMode"),NativeText(a,$"AttDef/{tag}/VerticalMode"));
            }).ToArray();
            var bounds=new DBTool.Domain.StageContractsV2.Bounds2V2(Mm(NativeNumber(a,"Bounds/MinX")),Mm(NativeNumber(a,"Bounds/MinY")),Mm(NativeNumber(a,"Bounds/MaxX")),Mm(NativeNumber(a,"Bounds/MaxY")));
            if(bounds.min_x_mm>bounds.max_x_mm||bounds.min_y_mm>bounds.max_y_mm)throw new NativeAssetContractException("ASSET_BOUNDS_INVALID",a.Name,"Inverted bounds.");
            definitions.Add(new(a.Name,handle,a.Signature!.Hash,Point(a,"Origin"),bounds,a.Role,a.Signature.Properties.TryGetValue("ChildAssetIds",out var children)?System.Text.Json.JsonSerializer.Deserialize<string[]>(children)??[]:[],attrs,layer,null,[descId]));
        }
        var layers=manifest.Assets.Where(x=>x.AssetKind==AssetKind.Layer).Select(x=>
        {
            var binding=nativeContext.Layers.SingleOrDefault(l=>l.Id==x.Name)??throw new NativeAssetContractException("LAYER_STROKE_UNRESOLVED",x.Name,"Effective stroke binding required.");
            return new DBTool.Domain.StageContractsV2.LayerMetricV2(x.Name,x.Name,binding.EffectiveStrokePaperMm,binding.SourceIds);
        }).ToArray();
        var fontStyles=manifest.Assets.Where(x=>x.AssetKind==AssetKind.TextStyle).Select(x=>
        {
            var font=nativeContext.Styles.SingleOrDefault(f=>f.StyleId==x.Name)??throw new NativeAssetContractException("FONT_SOURCE_MISSING",x.Name,"Exact font byte context required.");
            return new DBTool.Domain.StageContractsV2.FontMetricStyleV2(x.Name,font.FontPath,font.FontFileSha256,font.EncodingRuleId,"None",font.ProviderVersion,font.NormalizationRuleId);
        }).ToArray();
        if(manifest.Inventory?.Covers.Length>0)throw new NativeAssetContractException("PRODUCT_CONTRACT_INCOMPLETE","products","Native cover inventory requires approved plan/AA/BB/aperture/contact binding; bounds are insufficient.");
        // S0 is inventory. Actual formatted text must use INativeWholeTextMeasurementR03 after formatting.
        var assetsV2=new DBTool.Domain.StageContractsV2.AssetsV2(manifest.SourceFileHash,definitions.ToArray(),[],fontPolicyId,"mm",[],[],layers,fontStyles);
        return new TemplateAssetsBundle(assetsV2, registry.ToImmutable());
    }

    public static DBTool.Domain.StageContractsV2.AssetsV2 ToAssetsV2(TemplateManifest manifest, string fontPolicyId = "NR-16", string units = "mm") =>
        ConvertToAssetsBundle(manifest, fontPolicyId, units).Assets;

    public static DBTool.Domain.StageContractsV2.AssetsV2 ToAssetsV2(
        TemplateManifest manifest,
        out ImmutableDictionary<string, DBTool.Domain.StageContractsV2.SourceV2> sources,
        string fontPolicyId = "NR-16",
        string units = "mm")
    {
        var bundle = ConvertToAssetsBundle(manifest, fontPolicyId, units);
        sources = bundle.Sources;
        return bundle.Assets;
    }
}





