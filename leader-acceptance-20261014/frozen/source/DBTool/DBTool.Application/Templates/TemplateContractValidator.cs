using DBTool.Domain;
namespace DBTool.Application.Templates;
public interface ITemplateContractValidator
{
    OperationResult<ValidatedTemplateManifest> Validate(TemplateManifest manifest, IReadOnlyList<TemplateRequirement> requirements, TemplateValidationMode mode = TemplateValidationMode.ProductionRender);
}
public sealed class TemplateContractValidator : ITemplateContractValidator
{
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
                if (source?.SourceKind != SourceKind.Dwg || source.Sha256 != manifest.SourceFileHash)
                    Block("TEMPLATE_NOT_INSPECTED", $"Asset {asset.Role} requires evidence from this exact DWG.", source, manifest.SourceFileHash, source?.Sha256);
            }
        }
        if (diagnostics.Count > 0) return new(false, null, diagnostics);
        if (mode == TemplateValidationMode.SchemaOnly)
            diagnostics.Add(new Diagnostic("TEMPLATE_SCHEMA_ONLY", DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, null, null, null, "Schema validation does not authorize CAD rendering."));
        return new(true, new ValidatedTemplateManifest(manifest, mode), diagnostics);
    }
}
