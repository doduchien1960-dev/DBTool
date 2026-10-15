using DBTool.Application.Templates;
using DBTool.Domain;
using Xunit;

namespace DBTool.Application.Tests;

public class Phase2ATemplateTests
{
    [Fact]
    public void NativeSourceMismatchIsBlockedAndTongIsAnAttributeOfSteelSummary()
    {
        var required = VhgTemplateRoles.Presentation.Single(r => r.Role == "TextStyle:VHG-Unicode.Regular");
        var signature = new TemplateAssetSignature(new Dictionary<string, string> { ["FileName"] = "arial.ttf", ["XScale"] = "0.8" });
        var manifest = new TemplateManifest("template-manifest-v2", new string('a', 64), InspectionStatus.Unverified,
            [new TemplateAsset(required.Role, AssetKind.TextStyle, "VHG-Unicode.Regular", true, [], null, signature)]);
        var result = new TemplateContractValidator().Validate(manifest, [required], TemplateValidationMode.SchemaOnly);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "TEMPLATE_NATIVE_PROPERTY_MISMATCH" && d.Expected == "viet buddy.shx" && d.Actual == "arial.ttf");
        Assert.Contains("TONG", VhgTemplateRoles.Presentation.Single(r => r.Role == "SteelSummary").RequiredTags);
        Assert.DoesNotContain(VhgTemplateRoles.Presentation, r => r.Role == "Block:TONG");
    }
    [Theory]
    [InlineData("font", "viet buddy.shx", "arial.ttf")]
    [InlineData("width", "0.8", "1")]
    [InlineData("Dimlfac", "1", "30")]
    [InlineData("linetype", "DASHED", "Continuous")]
    public void SameNameDifferentNativePropertiesBlocksTarget(string key, string expected, string actual)
    {
        TemplateAsset Asset(string value) => new("Native", AssetKind.TextStyle, "VHG", true, [], null,
            new TemplateAssetSignature(new Dictionary<string, string> { [key] = value }));
        var manifest = new TemplateManifest("template-manifest-v2", new string('a', 64), InspectionStatus.Inspected, [Asset(expected)]);
        var result = TemplateTargetValidator.Validate(manifest, [Asset(actual)]);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "TEMPLATE_TARGET_CONFLICT");
        Assert.True(TemplateTargetValidator.Validate(manifest, [Asset(expected)]).Success);
        Assert.True(TemplateTargetValidator.Validate(manifest, [], allowMissing: true).Success);
        Assert.False(TemplateTargetValidator.Validate(manifest, []).Success);
    }

    [Fact]
    public void SignaturesAreImmutableOrderIndependentAndUnambiguous()
    {
        var input = new Dictionary<string, string> { ["font"] = "native", ["width"] = "0.8" };
        var signature = new TemplateAssetSignature(input);
        input["font"] = "replacement";
        Assert.Equal("native", signature.Properties["font"]);
        Assert.Equal(signature.Hash, new TemplateAssetSignature(signature.Properties.Reverse()).Hash);
    }
    [Fact]
    public void ProductionV2RejectsNameOnlyInspectedAsset()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var source = new SourceRef(SourceKind.Dwg, "template.dwg", hash, null, null, null, "inspection");
        var manifest = new TemplateManifest("template-manifest-v2", hash, InspectionStatus.Inspected,
            [new TemplateAsset("TextStyle:VHG-Unicode.Regular", AssetKind.TextStyle, "VHG-Unicode.Regular", true, [], source)]);
        var result = new TemplateContractValidator().Validate(manifest, []);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "TEMPLATE_SIGNATURE_MISSING");
    }
}
