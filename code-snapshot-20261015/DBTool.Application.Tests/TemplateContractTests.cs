using DBTool.Domain;
using DBTool.Application.Templates;
using Xunit;
namespace DBTool.Application.Tests;
public class TemplateContractTests
{
    [Fact] public void TwoJsonFixturesHaveIndependentTagsAndStayNonProduction()
    {
        using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Tests","Fixtures","template-manifest.synthetic.json")));
        var cases=json.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(2,cases.Length);
        var expected=new[]{("SteelSummary","TK.THKL","TongKL"),("PanelSummary","TK.THKLNAP","TONG")};
        for(int i=0;i<cases.Length;i++)
        {
            var item=cases[i]; var m=item.GetProperty("manifest");
            var a=m.GetProperty("assets").EnumerateArray().Single();
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.GetProperty("sourcePayload").GetString()!)));
            Assert.Equal(hash,m.GetProperty("sourceFileHash").GetString());
            Assert.Equal("Unverified",m.GetProperty("inspectionStatus").GetString());
            Assert.Equal(expected[i].Item1,a.GetProperty("role").GetString());
            Assert.Equal(expected[i].Item2,a.GetProperty("name").GetString());
            Assert.Equal(expected[i].Item3,a.GetProperty("attributeTags")[0].GetString());
            var tags=a.GetProperty("attributeTags").EnumerateArray().Select(t=>t.GetString()!).ToArray();
            var manifest=new TemplateManifest(m.GetProperty("schemaVersion").GetString()!,hash,InspectionStatus.Unverified,[new TemplateAsset(expected[i].Item1,AssetKind.Block,expected[i].Item2,true,tags,null)]);
            var requirements=new[]{new TemplateRequirement(expected[i].Item1,AssetKind.Block,[expected[i].Item3])};
            Assert.True(new TemplateContractValidator().Validate(manifest,requirements,TemplateValidationMode.SchemaOnly).Success);
            Assert.False(new TemplateContractValidator().Validate(manifest,requirements).Success);
        }
    }
    private const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static SourceRef DwgSource(string hash = Hash) => new(SourceKind.Dwg,"VHGpro 3.2/Template/VHG-Template.dwg",hash,null,null,null,"read-only DWG manifest inspection");
    private static TemplateRequirement Steel => new("SteelSummary",AssetKind.Block,["TongKL"]);
    private static TemplateManifest Manifest(params TemplateAsset[] assets) => new("template-manifest-v1",Hash,InspectionStatus.Unverified,assets);
    private static TemplateAsset Block(string role,string name,params string[] tags)=>new(role,AssetKind.Block,name,true,tags,null);
    private static TemplateAsset InspectedBlock(string role,string name,params string[] tags)=>new(role,AssetKind.Block,name,true,tags,DwgSource());

    [Fact] public void InspectedDwgWithMatchedHashAndRequiredTagCanRender()
    {
        var manifest=new TemplateManifest("template-manifest-v1",Hash,InspectionStatus.Inspected,[InspectedBlock("SteelSummary","TK.THKL","TongKL")]);
        var result=new TemplateContractValidator().Validate(manifest,[Steel]);
        Assert.True(result.Success); Assert.True(result.Value!.CanRender);
    }
    [Fact] public void InspectedDwgWithWrongSourceHashIsBlocked()
    {
        var manifest=new TemplateManifest("template-manifest-v1",Hash,InspectionStatus.Inspected,[new("SteelSummary",AssetKind.Block,"TK.THKL",true,["TongKL"],DwgSource("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"))]);
        var result=new TemplateContractValidator().Validate(manifest,[Steel]);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Code=="TEMPLATE_NOT_INSPECTED" && d.Severity==DiagnosticSeverity.Block);
    }
    [Fact] public void MissingBlockIsBlocked()
    {
        var result=new TemplateContractValidator().Validate(Manifest(),[Steel],TemplateValidationMode.SchemaOnly);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="TEMPLATE_BLOCK_MISSING" && d.Severity==DiagnosticSeverity.Block);
    }
    [Fact] public void TongIsNotTongKl()
    {
        var result=new TemplateContractValidator().Validate(Manifest(Block("SteelSummary","TK.THKL","TONG")),[Steel],TemplateValidationMode.SchemaOnly);
        Assert.False(result.Success); Assert.Null(result.Value); Assert.Contains(result.Diagnostics,d=>d.Code=="TEMPLATE_TAG_MISSING");
    }
    [Fact] public void SteelSummaryAcceptsItsOwnTagOnlyInSchemaMode()
    {
        var result=new TemplateContractValidator().Validate(Manifest(Block("SteelSummary","TK.THKL","TongKL")),[Steel],TemplateValidationMode.SchemaOnly);
        Assert.True(result.Success); Assert.False(result.Value!.CanRender);
    }
    [Fact] public void PanelSummaryUsesTong()
    {
        var result=new TemplateContractValidator().Validate(Manifest(Block("PanelSummary","TK.THKLNAP","TONG")),[new("PanelSummary",AssetKind.Block,["TONG"])],TemplateValidationMode.SchemaOnly);
        Assert.True(result.Success); Assert.False(result.Value!.CanRender);
    }
    [Fact] public void DuplicateRequiredRoleIsBlocked()
    {
        var a=Block("SteelSummary","TK.THKL","TongKL");
        var result=new TemplateContractValidator().Validate(Manifest(a,a),[Steel],TemplateValidationMode.SchemaOnly);
        Assert.Contains(result.Diagnostics,d=>d.Code=="TEMPLATE_ROLE_DUPLICATE"); Assert.Null(result.Value);
    }
    [Fact] public void DefaultProductionModeNeverAcceptsUnverifiedAssets()
    {
        var result=new TemplateContractValidator().Validate(Manifest(Block("SteelSummary","TK.THKL","TongKL")),[Steel]);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Code=="TEMPLATE_NOT_INSPECTED");
    }
    [Fact] public void InspectedFlagAloneCannotAuthorizeRendering()
    {
        var manifest=new TemplateManifest("template-manifest-v1",Hash,InspectionStatus.Inspected,[Block("SteelSummary","TK.THKL","TongKL")]);
        Assert.False(new TemplateContractValidator().Validate(manifest,[Steel]).Success);
    }
    [Fact] public void DxfEvidenceCannotAuthorizeDwgRendering()
    {
        var source=new SourceRef(SourceKind.Dxf,"template.dxf",Hash,null,null,null,"synthetic DXF evidence");
        var manifest=new TemplateManifest("template-manifest-v1",Hash,InspectionStatus.Inspected,[new("SteelSummary",AssetKind.Block,"TK.THKL",true,["TongKL"],source)]);
        Assert.False(new TemplateContractValidator().Validate(manifest,[Steel]).Success);
    }
    [Fact] public void ManifestAndTagsAreDefensiveCopies()
    {
        var tags=new List<string>{"TongKL"}; var assets=new List<TemplateAsset>{new("SteelSummary",AssetKind.Block,"TK.THKL",true,tags,null)};
        var manifest=new TemplateManifest("template-manifest-v1",Hash,InspectionStatus.Unverified,assets);
        tags.Clear(); assets.Clear(); Assert.Equal("TongKL",Assert.Single(Assert.Single(manifest.Assets).AttributeTags));
    }
}
