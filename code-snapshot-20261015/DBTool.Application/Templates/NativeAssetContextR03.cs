using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBTool.Domain.StageContractsV2;
namespace DBTool.Application.Templates;

public sealed class NativeAssetContractException(string code, string field, string message) : InvalidOperationException($"{code}: {field}: {message}")
{
    public string Code { get; } = code;
    public string Field { get; } = field;
}
public sealed record UnitBindingR03(string BundleSha256, string CoordinateUnit, string IntendedOutputUnit,
    long ScaleNumerator, long ScaleDenominator, string[] ApprovedSourceIds, string Approver, DateTimeOffset ApprovalTime)
{
    public double ScaleToMm(string bundleHash)
    {
        if (!string.Equals(BundleSha256,bundleHash,StringComparison.OrdinalIgnoreCase)) Fault("UNIT_BINDING_MISMATCH","bundle_sha256");
        var expected = CoordinateUnit switch { "mm" => (1L,1L), "in" => (127L,5L), "ft" => (1524L,5L), "m" => (1000L,1L), "cm" => (10L,1L), _ => (0L,0L) };
        if (expected.Item1==0 || IntendedOutputUnit!="mm" || ScaleNumerator<=0 || ScaleDenominator<=0 ||
            (decimal)ScaleNumerator * expected.Item2 != (decimal)ScaleDenominator * expected.Item1) Fault("UNIT_BINDING_INVALID","scale_to_mm");
        if (ApprovedSourceIds.Length==0 || ApprovedSourceIds.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(Approver) || ApprovalTime==default) Fault("UNIT_BINDING_UNAPPROVED","approval");
        return (double)ScaleNumerator/ScaleDenominator;
    }
    static void Fault(string code,string field)=>throw new NativeAssetContractException(code,field,"Exact approved coordinate binding is required; INSUNITS is metadata.");
}
public sealed record NativeFontContextR03(string StyleId,string NativeStyleHash,string FontPath,string FontFileSha256,
    string? BigFontPath,string? BigFontSha256,string EncodingRuleId,string NormalizationRuleId,
    string ProviderVersion,bool FallbackDetected,bool ActualFontVerified,string[] SourceIds);
public sealed record NativeLayerContextR03(string Id,string NativeLineweight,double EffectiveStrokePaperMm,string[] SourceIds);
public sealed record NativeAssetContextR03(string Revision,string BundleSha256,string MetadataInsunits,UnitBindingR03 Units,
    NativeFontContextR03[] Styles,NativeLayerContextR03[] Layers,ImmutableDictionary<string,string> InsertionLayers,
    ImmutableDictionary<string,SourceV2> Sources)
{
    public const string CurrentRevision="asset-context-R03";
    [System.Text.Json.Serialization.JsonIgnore] public string Sha256=>NativeContractHash.Compute(this);
    public double Validate(TemplateManifest manifest)
    {
        if(Revision!=CurrentRevision || BundleSha256!=manifest.SourceFileHash) throw new NativeAssetContractException("ASSET_CONTEXT_MISMATCH","revision/bundle_sha256","Exact revision and bundle required.");
        var scale=Units.ScaleToMm(BundleSha256);
        if(Styles.Select(x=>x.StyleId).Distinct(StringComparer.Ordinal).Count()!=Styles.Length||Layers.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=Layers.Length)throw new NativeAssetContractException("ASSET_CONTEXT_DUPLICATE","styles/layers","Native identities must be unique.");
        foreach(var id in Units.ApprovedSourceIds.Concat(Styles.SelectMany(x=>x.SourceIds)).Concat(Layers.SelectMany(x=>x.SourceIds)))
            if(!Sources.ContainsKey(id))throw new NativeAssetContractException("ASSET_SOURCE_UNRESOLVED",id,"Source ID does not resolve.");
        foreach(var f in Styles)
        {
            if(!File.Exists(f.FontPath) || NativeContractHash.File(f.FontPath)!=f.FontFileSha256)throw new NativeAssetContractException("FONT_SOURCE_MISSING",f.StyleId,"Exact font bytes unavailable.");
            if((f.BigFontPath is null)!=(f.BigFontSha256 is null) || f.BigFontPath is not null && (!File.Exists(f.BigFontPath)||NativeContractHash.File(f.BigFontPath)!=f.BigFontSha256))throw new NativeAssetContractException("FONT_SOURCE_MISSING",f.StyleId,"Exact bigfont bytes unavailable.");
            if(string.IsNullOrWhiteSpace(f.EncodingRuleId)||string.IsNullOrWhiteSpace(f.NormalizationRuleId)||f.SourceIds.Length==0)throw new NativeAssetContractException("FONT_ENCODING_UNRESOLVED",f.StyleId,"Approved encoding/normalization sources required.");
            if(f.FallbackDetected || !f.ActualFontVerified || string.IsNullOrWhiteSpace(f.ProviderVersion))throw new NativeAssetContractException("FONT_SUBSTITUTION_BLOCKED",f.StyleId,"Actual engine font identity not verified.");
            var style=manifest.Assets.SingleOrDefault(x=>x.AssetKind==AssetKind.TextStyle && x.Name==f.StyleId);
            if(style?.Signature?.Properties.GetValueOrDefault("FileName") is not string declaredFont || !string.Equals(Path.GetFileName(declaredFont),Path.GetFileName(f.FontPath),StringComparison.OrdinalIgnoreCase))throw new NativeAssetContractException("FONT_SOURCE_MISMATCH",f.StyleId,"Resolved font filename differs from native declared font; substitution is prohibited.");
            if(!f.SourceIds.Any(id=>Sources.TryGetValue(id,out var src)&&string.Equals(src.sha256,f.FontFileSha256,StringComparison.OrdinalIgnoreCase)))throw new NativeAssetContractException("FONT_SOURCE_UNRESOLVED",f.StyleId,"Font byte source descriptor does not resolve exact hash.");
            if(style?.Signature?.Hash!=f.NativeStyleHash)throw new NativeAssetContractException("FONT_STYLE_MISMATCH",f.StyleId,"Native style hash differs.");
        }
        foreach(var layer in Layers)
            if(!double.IsFinite(layer.EffectiveStrokePaperMm)||layer.EffectiveStrokePaperMm<=0||layer.SourceIds.Length==0)throw new NativeAssetContractException("LAYER_STROKE_UNRESOLVED",layer.Id,"Positive source-bound effective stroke required.");
        return scale;
    }
}
public static class NativeContractHash
{
    public static string Compute<T>(T value)
    {
        using var document=JsonDocument.Parse(JsonSerializer.Serialize(value));
        using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream))Write(document.RootElement,writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    static void Write(JsonElement element,Utf8JsonWriter writer)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {writer.WriteStartObject();foreach(var p in element.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(p.Value,writer);}writer.WriteEndObject();}
        else if(element.ValueKind==JsonValueKind.Array){writer.WriteStartArray();foreach(var item in element.EnumerateArray())Write(item,writer);writer.WriteEndArray();}
        else element.WriteTo(writer);
    }
    public static string File(string path)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path)));
}
public sealed record NativeTextMeasureRequestR03(string Id,string RunId,string ContextSha256,string TextRaw,string TextDecoded,
    string EntityKind,string StyleId,string FontFileSha256,string? BigFontSha256,double HeightMm,double WidthFactor,double RotationRad,
    string HorizontalMode,string VerticalMode,string? Attachment,bool MirroredX,bool MirroredY,bool Vertical,double? WrappingWidthMm,string[] SourceIds);
public sealed record NativeRawTextMeasurementR03(string EntityKind,double[] Minimum,double[] Maximum,string CoordinateUnit);
public sealed record NativeTextMeasureResultR03(string Revision,string RequestId,string RequestSha256,string ContextSha256,Bounds2V2 Ink,
    bool HasInk,double? AdvanceXmm,double? AdvanceYmm,string[] ResolvedFontHashes,string EngineVersion,string EncodingRuleId,
    NativeRawTextMeasurementR03 NativeRawMeasurement,string[] SourceIds,string[] DiagnosticCodes,string MeasurementSha256);
public interface INativeWholeTextMeasurementR03
{
    NativeTextMeasureResultR03 Measure(NativeAssetContextR03 context,NativeTextMeasureRequestR03 request);
}
public static class NativeActiveFeaturePlanR03
{
    public static IReadOnlyList<TemplateRequirement> Requirements(bool reinforcedCatch,bool dowelHole)=>VhgTemplateRoles.Presentation
        .Concat(reinforcedCatch?VhgTemplateRoles.CatchBasinRequirements:[]).Concat(dowelHole?VhgTemplateRoles.GeometryRequirements:[])
        .GroupBy(x=>x.Role,StringComparer.Ordinal).Select(x=>x.First()).ToArray();
}




