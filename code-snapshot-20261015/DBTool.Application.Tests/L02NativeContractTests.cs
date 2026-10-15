using DBTool.Application.Templates;
using DBTool.Domain;
using Xunit;
namespace DBTool.Application.Tests;
public sealed class L02NativeContractTests
{
    const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    static TemplateManifest Block(string? remove = null, string? nonfinite = null)
    {
        var p = new Dictionary<string,string>{["Layer"]="VHG-Text",["Handle"]="1A",["Origin"]="0,0,0",["Bounds/MinX"]="0",["Bounds/MinY"]="0",["Bounds/MaxX"]="100",["Bounds/MaxY"]="100",["AttDef/T/Height"]="10",["AttDef/T/WidthFactor"]="0.8",["AttDef/T/Style"]="Regular",["AttDef/T/Position"]="0,0,0",["AttDef/T/Alignment"]="0,0,0",["AttDef/T/Rotation"]="0",["AttDef/T/HorizontalMode"]="TextLeft",["AttDef/T/VerticalMode"]="TextBase",["AttDef/T/DefaultText"]="actual"};
        if(remove!=null)p.Remove(remove);
        if(nonfinite!=null)p[nonfinite]="NaN";
        return Manifest(new TemplateAsset("Summary",AssetKind.Block,"Summary",true,["T"],new(SourceKind.Dwg,"source.dwg",Hash,null,null,null,"read"),new(p)));
    }
    static TemplateManifest Manifest(params TemplateAsset[] a)=>new("template-manifest-v2",Hash,InspectionStatus.Inspected,a);
    [Theory]
    [InlineData("AttDef/T/Rotation")][InlineData("AttDef/T/HorizontalMode")][InlineData("AttDef/T/VerticalMode")]
    public void MissingRequiredNativeAttributeNeverDefaults(string field)=>Assert.ThrowsAny<InvalidOperationException>(()=>TemplateAssetsV2Converter.ToAssetsV2(Block(remove:field)));
    [Theory]
    [InlineData("AttDef/T/Rotation")][InlineData("AttDef/T/Height")][InlineData("AttDef/T/WidthFactor")][InlineData("Bounds/MaxX")]
    public void NonfiniteNativeValuesNeverEscape(string field)=>Assert.ThrowsAny<InvalidOperationException>(()=>TemplateAssetsV2Converter.ToAssetsV2(Block(nonfinite:field)));
    [Fact] public void InspectedCoordinatesNeedExactApprovedUnitBinding()=>Assert.ThrowsAny<InvalidOperationException>(()=>TemplateAssetsV2Converter.ToAssetsV2(Block()));
    [Fact] public void FontStyleHashCannotStandInForMissingFontBytes()
    {
        var a=new TemplateAsset("TextStyle:Regular",AssetKind.TextStyle,"Regular",true,[],new(SourceKind.Dwg,"source.dwg",Hash,null,null,null,"read"),new(new Dictionary<string,string>{["FileName"]="missing-font.shx"}));
        Assert.ThrowsAny<InvalidOperationException>(()=>TemplateAssetsV2Converter.ToAssetsV2(Manifest(a)));
    }
    [Fact] public void LayerWithoutResolvedStrokeCannotBecomePoint25()
    {
        var a=new TemplateAsset("Layer:Text",AssetKind.Layer,"Text",true,[],new(SourceKind.Dwg,"source.dwg",Hash,null,null,null,"read"),new(new Dictionary<string,string>{["LineWeight"]="ByLineWeightDefault"}));
        Assert.ThrowsAny<InvalidOperationException>(()=>TemplateAssetsV2Converter.ToAssetsV2(Manifest(a)));
    }
}


