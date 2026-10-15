using System.Collections.Immutable;
using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Domain.StageContractsV2;
using Xunit;
namespace DBTool.Application.Tests;
public sealed class L02NativeContextTests
{
    const string Hash="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    static SourceV2 Approval=>new("UNIT-APPROVAL","Dwg","units.fixture.json",Hash,null,null,null,null,"synthetic unit test approval",[],null);
    static NativeAssetContextR03 Context(string unit="mm",long numerator=1,long denominator=1)=>new("asset-context-R03",Hash,"Feet",
        new(Hash,unit,"mm",numerator,denominator,[Approval.id],"test authority",DateTimeOffset.Parse("2026-10-15T00:00:00Z")),[],[new("Drawing","LineWeight050",0.5,[Approval.id])],
        ImmutableDictionary<string,string>.Empty.Add("Test","Drawing"),ImmutableDictionary<string,SourceV2>.Empty.Add(Approval.id,Approval));
    static TemplateManifest Block=>new("template-manifest-v2",Hash,InspectionStatus.Inspected,[new("Test",AssetKind.Block,"Test",true,[],new(SourceKind.CadHandle,"source.dwg",Hash,null,null,null,"read",handle:"1A"),new(new Dictionary<string,string>{["Origin"]="100,0,0",["Bounds/MinX"]="0",["Bounds/MinY"]="0",["Bounds/MaxX"]="100",["Bounds/MaxY"]="100"}))]);
    [Fact] public void OneHundredActualFeetNormalizeExactlyOnceTo30480mm()
    {
        var bundle=TemplateAssetsV2Converter.ConvertToAssetsBundle(Block,nativeContext:Context("ft",1524,5));
        Assert.Equal(30480,bundle.Assets.definitions.Single().base_point.x_mm);
        Assert.Equal(30480,bundle.Assets.definitions.Single().bounds.max_x_mm);
        Assert.Equal("mm",bundle.Assets.units);Assert.Empty(bundle.Assets.glyphs);
        Assert.All(bundle.Assets.definitions.Single().source_ids,id=>Assert.Equal(Hash,bundle.Sources[id].sha256));
    }
    [Fact] public void FeetMetadataWithApprovedMillimeterCoordinatesStays100mm()=>Assert.Equal(100,TemplateAssetsV2Converter.ConvertToAssetsBundle(Block,nativeContext:Context()).Assets.definitions.Single().bounds.max_x_mm);
    [Fact] public void BindingForDifferentBundleBlocks()=>Assert.Equal("UNIT_BINDING_MISMATCH",Assert.Throws<NativeAssetContractException>(()=>(Context().Units with{BundleSha256=new string('B',64)}).ScaleToMm(Hash)).Code);
    [Fact] public void NonstandardFeetRatioBlocks()=>Assert.Equal("UNIT_BINDING_INVALID",Assert.Throws<NativeAssetContractException>(()=>Context("ft",1,1).Units.ScaleToMm(Hash)).Code);
    [Fact] public void NoApprovalSourceBlocks()=>Assert.Equal("ASSET_SOURCE_UNRESOLVED",Assert.Throws<NativeAssetContractException>(()=>(Context() with{Sources=ImmutableDictionary<string,SourceV2>.Empty}).Validate(Block)).Code);
    [Fact] public void EffectiveStrokeReadsBindingInsteadOfPoint25()
    {
        var a=new TemplateAsset("Layer:Drawing",AssetKind.Layer,"Drawing",true,[],new(SourceKind.Dwg,"source.dwg",Hash,null,null,null,"read"),new(new Dictionary<string,string>{["LineWeight"]="LineWeight050"}));
        var m=new TemplateManifest("template-manifest-v2",Hash,InspectionStatus.Inspected,[a]);
        Assert.Equal(.5,TemplateAssetsV2Converter.ConvertToAssetsBundle(m,nativeContext:Context()).Assets.layers.Single().stroke_paper_mm);
    }
    [Fact] public void MissingLayerStrokeHasTypedFault()
    {
        var a=new TemplateAsset("Layer:Drawing",AssetKind.Layer,"Drawing",true,[],null,new(new Dictionary<string,string>{["LineWeight"]="ByLineWeightDefault"}));
        var m=new TemplateManifest("template-manifest-v2",Hash,InspectionStatus.Inspected,[a]);
        Assert.Equal("LAYER_STROKE_UNRESOLVED",Assert.Throws<NativeAssetContractException>(()=>TemplateAssetsV2Converter.ConvertToAssetsBundle(m,nativeContext:Context() with{Layers=[]})).Code);
    }
    [Fact] public void NativeMissingFontBytesHaveTypedFault()
    {
        var style=new NativeFontContextR03("S",Hash,"Z:/deliberately-absent-font.ttf",Hash,null,null,"approved-encoding","approved-normalization","AutoCAD2026",false,true,[Approval.id]);
        Assert.Equal("FONT_SOURCE_MISSING",Assert.Throws<NativeAssetContractException>(()=>(Context() with{Styles=[style]}).Validate(Block)).Code);
    }
    [Fact] public void FullPresentationBaselineAndBothRequestedFeaturesStayRequired()
    {
        var roles=NativeActiveFeaturePlanR03.Requirements(true,true);
        Assert.All(VhgTemplateRoles.Presentation,r=>Assert.Contains(roles,x=>x.Role==r.Role));
        Assert.Contains(roles,x=>x.Role=="CatchSummary"&&x.RequiredTags.Contains("CUATHU1")&&x.RequiredTags.Contains("CUATHU2"));
        Assert.Contains(roles,x=>x.Role=="Geometry/DowelHole");
    }
    [Fact] public void CanonicalContextHashIndependentOfDictionaryInsertionOrder()
    {
        var a=ImmutableDictionary<string,string>.Empty.Add("b","2").Add("a","1");var b=ImmutableDictionary<string,string>.Empty.Add("a","1").Add("b","2");
        Assert.Equal(NativeContractHash.Compute(a),NativeContractHash.Compute(b));Assert.Equal(64,Context().Sha256.Length);
    }
}
