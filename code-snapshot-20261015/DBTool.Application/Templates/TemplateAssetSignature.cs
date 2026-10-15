using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using DBTool.Domain;

namespace DBTool.Application.Templates;

/// <summary>Native properties, independent of database handles. No Autodesk types cross this boundary.</summary>
public sealed record TemplateAssetSignature
{
    public TemplateAssetSignature(IEnumerable<KeyValuePair<string, string>> properties)
    {
        var values = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var item in properties)
            values.Add(ContractGuard.Text(item.Key, nameof(properties)), item.Value ?? throw new ArgumentNullException(nameof(properties)));
        if (values.Count == 0) throw new ArgumentException("A signature needs native properties.", nameof(properties));
        Properties = values.ToImmutable();
    }
    public ImmutableSortedDictionary<string, string> Properties { get; }
    // Length prefixes avoid ambiguous values containing newlines or separators.
    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Concat(Properties.Select(p => $"{p.Key.Length}:{p.Key}{p.Value.Length}:{p.Value}")))));
}

public static class TemplateTargetValidator
{
    public static OperationResult<TemplateManifest> Validate(TemplateManifest source, IEnumerable<TemplateAsset> target, bool allowMissing = false)
    {
        var actual = target.ToDictionary(a => (a.AssetKind, a.Name.ToUpperInvariant()));
        var diagnostics = new List<Diagnostic>();
        foreach (var expected in source.Assets.Where(a => a.AssetKind != AssetKind.Hatch))
        {
            actual.TryGetValue((expected.AssetKind, expected.Name.ToUpperInvariant()), out var found);
            if (found is null && allowMissing) continue;
            var code = found is null ? "TEMPLATE_TARGET_ASSET_MISSING" :
                expected.Signature is null || found.Signature is null ? "TEMPLATE_SIGNATURE_MISSING" :
                expected.Signature.Hash != found.Signature.Hash ? "TEMPLATE_TARGET_CONFLICT" : null;
            if (code is not null)
                diagnostics.Add(new(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                    expected.VerificationSource, expected.Signature?.Hash, found?.Signature?.Hash,
                    $"{expected.AssetKind} {expected.Name}: native target definition cannot be used for this template."));
        }
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? source : null, diagnostics);
    }
}
