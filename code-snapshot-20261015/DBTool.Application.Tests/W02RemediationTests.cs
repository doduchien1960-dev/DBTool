using System.Collections.Immutable;
using System.Globalization;
using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Domain.StageContractsV2;
using Xunit;

namespace DBTool.Application.Tests;

public class W02RemediationTests
{
    private const string SampleHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void G08_ToAssetsV2_ExtractsExactAttDefAndBoundsMetricsAndResolvesSourceDescriptors()
    {
        // Block 1: Steel summary with Left/Base alignment and Regular style
        var sig1 = new TemplateAssetSignature(new Dictionary<string, string>
        {
            ["Name"] = "TK.THKL",
            ["Handle"] = "1A0",
            ["Layer"] = "VHG-Thep",
            ["Origin"] = "100.5,200.25,0",
            ["Bounds/MinX"] = "100",
            ["Bounds/MinY"] = "200",
            ["Bounds/MaxX"] = "500",
            ["Bounds/MaxY"] = "600",
            ["AttributeTags"] = "TONG",
            ["AttDef/TONG/Tag"] = "TONG",
            ["AttDef/TONG/DefaultText"] = "0.00",
            ["AttDef/TONG/Style"] = "VHG-Unicode.Regular",
            ["AttDef/TONG/Height"] = "200",
            ["AttDef/TONG/WidthFactor"] = "0.8",
            ["AttDef/TONG/Position"] = "100,200,0",
            ["AttDef/TONG/Alignment"] = "100,200,0",
            ["AttDef/TONG/Rotation"] = "0",
            ["AttDef/TONG/HorizontalMode"] = "Left",
            ["AttDef/TONG/VerticalMode"] = "Base"
        });

        // Block 2: Panel summary with Center/Middle alignment and Bold style
        var sig2 = new TemplateAssetSignature(new Dictionary<string, string>
        {
            ["Name"] = "TK.THKLNAP",
            ["Handle"] = "2B1",
            ["Layer"] = "VHG-Nap2",
            ["Origin"] = "300,400,10",
            ["Bounds/MinX"] = "250",
            ["Bounds/MinY"] = "350",
            ["Bounds/MaxX"] = "850",
            ["Bounds/MaxY"] = "950",
            ["AttributeTags"] = "TONG",
            ["AttDef/TONG/Tag"] = "TONG",
            ["AttDef/TONG/DefaultText"] = "123.45",
            ["AttDef/TONG/Style"] = "VHG-Unicode.Bold",
            ["AttDef/TONG/Height"] = "250",
            ["AttDef/TONG/WidthFactor"] = "1.0",
            ["AttDef/TONG/Position"] = "310,410,10",
            ["AttDef/TONG/Alignment"] = "320,420,10",
            ["AttDef/TONG/Rotation"] = "1.5707963267948966",
            ["AttDef/TONG/HorizontalMode"] = "Center",
            ["AttDef/TONG/VerticalMode"] = "Middle"
        });

        var dwgSource = new SourceRef(SourceKind.Dwg, "VHG-Template.dwg", SampleHash, null, null, null, "inspected");
        var asset1 = new TemplateAsset("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSource, sig1);
        var asset2 = new TemplateAsset("PanelSummary", AssetKind.Block, "TK.THKLNAP", true, ["TONG"], dwgSource, sig2);

        var manifest = new TemplateManifest("template-manifest-v2", SampleHash, InspectionStatus.Inspected, [asset1, asset2]);

        var fixtureBundle = BoundFixture(manifest);
        var assetsV2=fixtureBundle.Assets;var sources=fixtureBundle.Sources;

        Assert.Equal(SampleHash, assetsV2.bundle_hash);
        Assert.Equal(2, assetsV2.definitions.Length);

        // Verify Block 1 exact fields
        var def1 = assetsV2.definitions.Single(d => d.name == "TK.THKL");
        Assert.Equal("1A0", def1.definition_handle);
        Assert.Equal("SteelSummary", def1.role);
        Assert.Equal("VHG-Thep", def1.layer_id);
        Assert.Equal(100.5, def1.base_point.x_mm);
        Assert.Equal(200.25, def1.base_point.y_mm);
        Assert.Equal(0, def1.base_point.z_mm);
        Assert.Equal(100, def1.bounds.min_x_mm);
        Assert.Equal(200, def1.bounds.min_y_mm);
        Assert.Equal(500, def1.bounds.max_x_mm);
        Assert.Equal(600, def1.bounds.max_y_mm);

        var attr1 = Assert.Single(def1.attributes);
        Assert.Equal("TONG", attr1.tag);
        Assert.Equal("0.00", attr1.default_text);
        Assert.Equal("VHG-Unicode.Regular", attr1.style_id);
        Assert.Equal(200, attr1.height_mm);
        Assert.Equal(0.8, attr1.width_factor);
        Assert.Equal(100, attr1.insertion.x_mm);
        Assert.Equal(200, attr1.insertion.y_mm);
        Assert.Equal(0, attr1.rotation_rad);
        Assert.Equal("Left", attr1.horizontal_mode);
        Assert.Equal("Base", attr1.vertical_mode);

        // Verify Block 2 exact fields
        var def2 = assetsV2.definitions.Single(d => d.name == "TK.THKLNAP");
        Assert.Equal("2B1", def2.definition_handle);
        Assert.Equal("PanelSummary", def2.role);
        Assert.Equal("VHG-Nap2", def2.layer_id);
        Assert.Equal(300, def2.base_point.x_mm);
        Assert.Equal(400, def2.base_point.y_mm);
        Assert.Equal(10, def2.base_point.z_mm);
        Assert.Equal(250, def2.bounds.min_x_mm);
        Assert.Equal(350, def2.bounds.min_y_mm);
        Assert.Equal(850, def2.bounds.max_x_mm);
        Assert.Equal(950, def2.bounds.max_y_mm);

        var attr2 = Assert.Single(def2.attributes);
        Assert.Equal("TONG", attr2.tag);
        Assert.Equal("123.45", attr2.default_text);
        Assert.Equal("VHG-Unicode.Bold", attr2.style_id);
        Assert.Equal(250, attr2.height_mm);
        Assert.Equal(1.0, attr2.width_factor);
        Assert.Equal(310, attr2.insertion.x_mm);
        Assert.Equal(410, attr2.insertion.y_mm);
        Assert.Equal(320, attr2.alignment.x_mm);
        Assert.Equal(420, attr2.alignment.y_mm);
        Assert.Equal(1.5707963267948966, attr2.rotation_rad);
        Assert.Equal("Center", attr2.horizontal_mode);
        Assert.Equal("Middle", attr2.vertical_mode);

        // Verify source_ids resolve in registry
        foreach (var def in assetsV2.definitions)
        {
            Assert.NotEmpty(def.source_ids);
            foreach (var sid in def.source_ids)
            {
                Assert.True(sources.TryGetValue(sid, out var descriptor),
                    $"Source id '{sid}' must resolve in run sources.");
                Assert.NotNull(descriptor);
                Assert.NotEmpty(descriptor.sha256);
                Assert.Equal(def.definition_handle, descriptor.handle);
            }
        }
    }

    [Fact]
    public void G09_RoleMappingAndScopeFeatures_DoNotBlockBaselineTemplate()
    {
        // Standard presentation template has SteelSummary, PanelSummary, Khung, layers, etc.
        var dwgSource = new SourceRef(SourceKind.Dwg, "VHG-Template.dwg", SampleHash, null, null, null, "inspected");
        var assets = new List<TemplateAsset>
        {
            new("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSource, new TemplateAssetSignature(new Dictionary<string, string> { ["Name"] = "TK.THKL" })),
            new("PanelSummary", AssetKind.Block, "TK.THKLNAP", true, ["TONG"], dwgSource, new TemplateAssetSignature(new Dictionary<string, string> { ["Name"] = "TK.THKLNAP" })),
            new("Block:Khung", AssetKind.Block, "Khung", true, [], dwgSource, new TemplateAssetSignature(new Dictionary<string, string> { ["NativeWidthMm"] = "42000", ["NativeHeightMm"] = "29700" }))
        };

        // Add layers matching VhgTemplateRoles.Presentation
        foreach (var req in VhgTemplateRoles.Presentation.Where(r => r.Kind == AssetKind.Layer))
        {
            assets.Add(new(req.Role, AssetKind.Layer, req.Role.Replace("Layer:", ""), true, [], dwgSource,
                new TemplateAssetSignature(new Dictionary<string, string>(req.NativeProperties))));
        }
        // Add textstyles, dimstyles, linetypes
        foreach (var req in VhgTemplateRoles.Presentation.Where(r => r.Kind is AssetKind.TextStyle or AssetKind.DimStyle or AssetKind.Linetype))
        {
            var props = req.NativeProperties.Count > 0 
                ? new Dictionary<string, string>(req.NativeProperties) 
                : new Dictionary<string, string> { ["PatternLength"] = "1.0" };
            assets.Add(new(req.Role, req.Kind, req.Role.Split(':')[1], true, [], dwgSource,
                new TemplateAssetSignature(props)));
        }

        var manifest = new TemplateManifest("template-manifest-v2", SampleHash, InspectionStatus.Inspected, assets);

        // Baseline validation MUST pass without requiring CatchSummary or Geometry/DowelHole
        var validator = new TemplateContractValidator();
        var baselineResult = validator.Validate(manifest, VhgTemplateRoles.Presentation);
        Assert.True(baselineResult.Success, string.Join("; ", baselineResult.Diagnostics.Select(d => d.Message)));

        // Feature-scoped validation: When CatchBasin is active, CatchBasinRequirements requires CatchSummary with CUATHU1/CUATHU2
        var catchRequirements = VhgTemplateRoles.CatchBasinRequirements;
        var catchCheckWithoutCatch = validator.Validate(manifest, catchRequirements);
        Assert.False(catchCheckWithoutCatch.Success);
        Assert.Contains(catchCheckWithoutCatch.Diagnostics, d => d.Code == "TEMPLATE_BLOCK_MISSING" && d.Message.Contains("CatchSummary"));

        // When CatchSummary block and VHG-HoThu layer are added, it passes CatchBasinRequirements
        assets.Add(new("CatchSummary", AssetKind.Block, "TK.THKL.CT", true, ["CUATHU1", "CUATHU2"], dwgSource,
            new TemplateAssetSignature(new Dictionary<string, string> { ["Name"] = "TK.THKL.CT" })));
        assets.Add(new("Layer:VHG-HoThu", AssetKind.Layer, "VHG-HoThu", true, [], dwgSource,
            new TemplateAssetSignature(new Dictionary<string, string> { ["ColorIndex"] = "3", ["Linetype"] = "Continuous" })));
        var manifestWithCatch = new TemplateManifest("template-manifest-v2", SampleHash, InspectionStatus.Inspected, assets);
        var catchCheckWithCatch = validator.Validate(manifestWithCatch, catchRequirements);
        Assert.True(catchCheckWithCatch.Success, string.Join("; ", catchCheckWithCatch.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void G10_PadThicknessZeroIsValid_AndNegativeThrowsArgumentOutOfRangeException()
    {
        // Constructing ManholeInput with PadThicknessMm = 0 (padOFF) must succeed
        var inputZeroPad = new ManholeInput(
            id: "MH-01",
            name: "Hố ga 1",
            profile: Profile.GaThamLienKhoi,
            l: 1200,
            b: 1000,
            h: 1500,
            baseThicknessMm: 200,
            wallThicknessMm: 200,
            roofThicknessMm: 200,
            padThicknessMm: 0, // padOFF!
            baseExpansionMm: 100,
            padExpansionMm: 100,
            beddingMortarMm: 20,
            baseHaunch: 0,
            topHaunch: 0,
            neckHeight: 300,
            roofOpening: new Rectangle2Mm(0, 0, 600, 600),
            pipeConnections: [],
            baseBottomMesh: new MeshSpec(10, 150),
            baseTopMesh: new MeshSpec(10, 150),
            coverMm: 30,
            elevationMode: ElevationMode.Symbolic,
            baseElevationM: null,
            pipeInvertReference: PipeInvertReference.InternalInvert,
            hookBaseBottom: HookPattern.Hook0M,
            hookBaseTop: HookPattern.Hook0M,
            distributionMode: DistributionMode.Equalized,
            meshJunctionPolicy: MeshJunctionPolicy.UniformBaseTestOnly,
            layerStackPolicy: LayerStackPolicy.TwoDirectionsTouchingTestOnly,
            featureRequests: [],
            sourceRefs: [],
            unsupportedRawFields: []
        );

        Assert.Equal(0, inputZeroPad.PadThicknessMm);

        // Constructing ManholeInput with negative PadThicknessMm must fail invariant
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManholeInput(
            id: "MH-01",
            name: "Hố ga 1",
            profile: Profile.GaThamLienKhoi,
            l: 1200,
            b: 1000,
            h: 1500,
            baseThicknessMm: 200,
            wallThicknessMm: 200,
            roofThicknessMm: 200,
            padThicknessMm: -50, // Negative!
            baseExpansionMm: 100,
            padExpansionMm: 100,
            beddingMortarMm: 20,
            baseHaunch: 0,
            topHaunch: 0,
            neckHeight: 300,
            roofOpening: new Rectangle2Mm(0, 0, 600, 600),
            pipeConnections: [],
            baseBottomMesh: new MeshSpec(10, 150),
            baseTopMesh: new MeshSpec(10, 150),
            coverMm: 30,
            elevationMode: ElevationMode.Symbolic,
            baseElevationM: null,
            pipeInvertReference: PipeInvertReference.InternalInvert,
            hookBaseBottom: HookPattern.Hook0M,
            hookBaseTop: HookPattern.Hook0M,
            distributionMode: DistributionMode.Equalized,
            meshJunctionPolicy: MeshJunctionPolicy.UniformBaseTestOnly,
            layerStackPolicy: LayerStackPolicy.TwoDirectionsTouchingTestOnly,
            featureRequests: [],
            sourceRefs: [],
            unsupportedRawFields: []
        ));
    }

    [Fact]
    public void G08_MissingInspectedMetrics_DoesNotFillSyntheticDefaults_AndBlocksConversion()
    {
        var dwgSource = new SourceRef(SourceKind.Dwg, "VHG-Template.dwg", SampleHash, null, null, null, "inspected");

        // 1. Missing bounds in inspected manifest must throw, not fill bounds0
        var sigMissingBounds = new TemplateAssetSignature(new Dictionary<string, string>
        {
            ["Name"] = "TK.THKL",
            ["Handle"] = "1A0",
            ["Layer"] = "VHG-Thep",
            ["Origin"] = "0,0,0",
            ["AttributeTags"] = "TONG",
            ["AttDef/TONG/Tag"] = "TONG",
            ["AttDef/TONG/DefaultText"] = "",
            ["AttDef/TONG/Style"] = "VHG-Unicode.Regular",
            ["AttDef/TONG/Height"] = "200",
            ["AttDef/TONG/WidthFactor"] = "0.8",
            ["AttDef/TONG/Position"] = "0,0,0",
            ["AttDef/TONG/Alignment"] = "0,0,0",
            ["AttDef/TONG/Rotation"] = "0",
            ["AttDef/TONG/HorizontalMode"] = "Left",
            ["AttDef/TONG/VerticalMode"] = "Base"
        });
        var assetNoBounds = new TemplateAsset("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSource, sigMissingBounds);
        var manifestNoBounds = new TemplateManifest("template-manifest-v2", SampleHash, InspectionStatus.Inspected, [assetNoBounds]);
        var exBounds = Assert.Throws<NativeAssetContractException>(() => TemplateAssetsV2Converter.ToAssetsV2(manifestNoBounds));
        Assert.Equal("ASSET_NATIVE_FIELD_INVALID",exBounds.Code);Assert.Contains("Bounds/MinX",exBounds.Field);

        // 2. Missing AttDef Height metric in inspected manifest must throw, not fill height200
        var sigMissingHeight = new TemplateAssetSignature(new Dictionary<string, string>
        {
            ["Name"] = "TK.THKL",
            ["Handle"] = "1A0",
            ["Layer"] = "VHG-Thep",
            ["Origin"] = "0,0,0",
            ["Bounds/MinX"] = "0",
            ["Bounds/MinY"] = "0",
            ["Bounds/MaxX"] = "100",
            ["Bounds/MaxY"] = "100",
            ["AttributeTags"] = "TONG",
            ["AttDef/TONG/Tag"] = "TONG",
            ["AttDef/TONG/DefaultText"] = "",
            ["AttDef/TONG/Style"] = "VHG-Unicode.Regular",
            // AttDef/TONG/Height is missing!
            ["AttDef/TONG/WidthFactor"] = "0.8",
            ["AttDef/TONG/Position"] = "0,0,0",
            ["AttDef/TONG/Alignment"] = "0,0,0",
            ["AttDef/TONG/Rotation"] = "0",
            ["AttDef/TONG/HorizontalMode"] = "Left",
            ["AttDef/TONG/VerticalMode"] = "Base"
        });
        var assetNoHeight = new TemplateAsset("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSource, sigMissingHeight);
        var manifestNoHeight = new TemplateManifest("template-manifest-v2", SampleHash, InspectionStatus.Inspected, [assetNoHeight]);
        var exHeight = Assert.Throws<NativeAssetContractException>(() => TemplateAssetsV2Converter.ToAssetsV2(manifestNoHeight));
        Assert.Equal("ASSET_NATIVE_FIELD_INVALID",exHeight.Code);Assert.Contains("AttDef/TONG/Height",exHeight.Field);
    }

    [Fact]
    public void NEW_A01_InspectedTypographyCannotGenerateUnsourcedGlyphsWithoutContext()
    {
        var source=new SourceRef(SourceKind.Dwg,"source.dwg",SampleHash,null,null,null,"synthetic unbound inspection");
        var manifest=new TemplateManifest("template-manifest-v2",SampleHash,InspectionStatus.Inspected,[new("TextStyle:Regular",AssetKind.TextStyle,"Regular",true,[],source,new(new Dictionary<string,string>{["FileName"]="arial.ttf",["XScale"]="0.8"}))]);
        var fault=Assert.Throws<NativeAssetContractException>(()=>TemplateAssetsV2Converter.ToAssetsV2(manifest));
        Assert.Equal("UNIT_BINDING_MISSING",fault.Code);
        var bound=BoundFixture(manifest);
        Assert.Empty(bound.Assets.glyphs); // S0 inventory never fabricates metrics for future formatted strings.
        Assert.Equal(NativeContractHash.File(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf")),Assert.Single(bound.Assets.font_styles).font_hash);
    }
    [Fact]
    public void NEW_A01_SourceRegistry_IsImmutablePerRun_IncludesFullBundleHash_AndPreservesSourceBytes()
    {
        string hashA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        string hashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        var dwgSourceA = new SourceRef(SourceKind.Dwg, "TemplateA.dwg", hashA, null, null, null, "bundle A");
        var dwgSourceB = new SourceRef(SourceKind.Dwg, "TemplateB.dwg", hashB, null, null, null, "bundle B");

        var sig = new TemplateAssetSignature(new Dictionary<string, string>
        {
            ["Name"] = "TK.THKL",
            ["Handle"] = "1A0",
            ["Layer"] = "VHG-Thep",
            ["Origin"] = "0,0,0",
            ["Bounds/MinX"] = "0",
            ["Bounds/MinY"] = "0",
            ["Bounds/MaxX"] = "100",
            ["Bounds/MaxY"] = "100",
            ["AttributeTags"] = "TONG",
            ["AttDef/TONG/Tag"] = "TONG",
            ["AttDef/TONG/DefaultText"] = "",
            ["AttDef/TONG/Style"] = "VHG-Unicode.Regular",
            ["AttDef/TONG/Height"] = "200",
            ["AttDef/TONG/WidthFactor"] = "0.8",
            ["AttDef/TONG/Position"] = "0,0,0",
            ["AttDef/TONG/Alignment"] = "0,0,0",
            ["AttDef/TONG/Rotation"] = "0",
            ["AttDef/TONG/HorizontalMode"] = "Left",
            ["AttDef/TONG/VerticalMode"] = "Base"
        });

        // Two bundles with SAME block name and SAME handle
        var manifestA = new TemplateManifest("template-manifest-v2", hashA, InspectionStatus.Inspected,
            [new TemplateAsset("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSourceA, sig)]);
        var manifestB = new TemplateManifest("template-manifest-v2", hashB, InspectionStatus.Inspected,
            [new TemplateAsset("SteelSummary", AssetKind.Block, "TK.THKL", true, ["TONG"], dwgSourceB, sig)]);

        var bundleA = BoundFixture(manifestA);
        var bundleB = BoundFixture(manifestB,bundleA.Sources);

        // Source identity includes the entire SHA256, as required by the rework order.
        string idA = $"SRC-BLK-{hashA}-1A0";
        string idB = $"SRC-BLK-{hashB}-1A0";

        Assert.True(bundleB.Sources.ContainsKey(idA), $"Sources must contain {idA}");
        Assert.True(bundleB.Sources.ContainsKey(idB), $"Sources must contain {idB}");
        Assert.NotEqual(idA, idB);

        // Both block descriptors coexist without overwriting provenance
        Assert.Equal(hashA, bundleB.Sources[idA].sha256);
        Assert.Equal("TemplateA.dwg", bundleB.Sources[idA].relative_path);
        Assert.Equal(hashB, bundleB.Sources[idB].sha256);
        Assert.Equal("TemplateB.dwg", bundleB.Sources[idB].relative_path);

        // Bundle sources also exist with their DWG file hashes
        string dwgIdA = $"SRC-DWG-{hashA}";
        string dwgIdB = $"SRC-DWG-{hashB}";
        Assert.True(bundleB.Sources.ContainsKey(dwgIdA));
        Assert.True(bundleB.Sources.ContainsKey(dwgIdB));
        Assert.Equal(hashA, bundleB.Sources[dwgIdA].sha256);
        Assert.Equal(hashB, bundleB.Sources[dwgIdB].sha256);

        // Conflicting ID with different content must fault
        var conflictingSource = new DBTool.Domain.StageContractsV2.SourceV2(
            idA, "CadHandle", "DifferentFile.dwg", "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null, null, null, "1A0", "Conflicting", [], null);

        var conflictingDict = System.Collections.Immutable.ImmutableDictionary.CreateRange(
            new[] { new KeyValuePair<string, DBTool.Domain.StageContractsV2.SourceV2>(idA, conflictingSource) });

        var exConflict = Assert.Throws<InvalidOperationException>(() =>
            BoundFixture(manifestA,conflictingDict));
        Assert.Contains("SOURCE_DESCRIPTOR_CONFLICT", exConflict.Message);
    }
    // Synthetic authority exercises conversion boundaries only. It does not attest any native engine or production bundle.
    static TemplateAssetsBundle BoundFixture(TemplateManifest input,System.Collections.Immutable.ImmutableDictionary<string,SourceV2>? existing=null)
    {
        var assets=input.Assets.ToList();
        var styleNames=assets.Where(x=>x.AssetKind==AssetKind.TextStyle).Select(x=>x.Name).Concat(assets.Where(x=>x.AssetKind==AssetKind.Block).SelectMany(x=>x.AttributeTags.Select(tag=>x.Signature!.Properties[$"AttDef/{tag}/Style"]))).Distinct().ToArray();
        foreach(var style in styleNames)if(!assets.Any(x=>x.AssetKind==AssetKind.TextStyle&&x.Name==style))assets.Add(new("TextStyle:"+style,AssetKind.TextStyle,style,true,[],assets.First().VerificationSource,new(new Dictionary<string,string>{{"FileName","arial.ttf"},{"XScale","0.8"}})));
        var manifest=new TemplateManifest(input.SchemaVersion,input.SourceFileHash,input.InspectionStatus,assets,input.Inventory);
        var fontPath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");
        var fontHash=NativeContractHash.File(fontPath);var unitId="SYNTHETIC-UNIT-"+input.SourceFileHash;var fontId="FONT-BYTES-"+fontHash;
        var registry=System.Collections.Immutable.ImmutableDictionary<string,SourceV2>.Empty
            .Add(unitId,new(unitId,"Dwg","synthetic-unit-binding.json",input.SourceFileHash,null,null,null,null,"Synthetic test authority only",[],null))
            .Add(fontId,new(fontId,"File","arial.ttf",fontHash,null,null,null,null,"Actual font bytes; synthetic authority only",[],null));
        var layers=assets.Where(x=>x.AssetKind==AssetKind.Block).Select(x=>x.Signature!.Properties["Layer"]).Distinct().Select(x=>new NativeLayerContextR03(x,"LineWeight050",.5,[unitId])).ToArray();
        var fonts=styleNames.Select(name=>new NativeFontContextR03(name,assets.Single(x=>x.AssetKind==AssetKind.TextStyle&&x.Name==name).Signature!.Hash,fontPath,fontHash,null,null,"SYNTHETIC-ENCODING","SYNTHETIC-NORMALIZATION","SYNTHETIC-CONVERSION-ONLY",false,true,[unitId,fontId])).ToArray();
        var insertion=assets.Where(x=>x.AssetKind==AssetKind.Block).ToImmutableDictionary(x=>x.Name,x=>x.Signature!.Properties["Layer"]);
        var context=new NativeAssetContextR03("asset-context-R03",input.SourceFileHash,"Feet",new(input.SourceFileHash,"mm","mm",1,1,[unitId],"Synthetic test authority",DateTimeOffset.Parse("2026-10-15T00:00:00Z")),fonts,layers,insertion,registry);
        return TemplateAssetsV2Converter.ConvertToAssetsBundle(manifest,existingSources:existing,nativeContext:context);
    }}



