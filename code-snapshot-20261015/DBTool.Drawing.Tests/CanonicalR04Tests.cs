using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class CanonicalR04Tests
{
    private static readonly string Hash = new('a',64);
    private static JsonNode Vector(double x=0,double y=0,double z=0) => new JsonArray(x,y,z);
    private static JsonObject Frame() => new() { ["id"]="f", ["origin_xyz_mm"]=Vector(),["basis_u_xyz"]=Vector(1),["basis_v_xyz"]=Vector(0,1),["normal_xyz"]=Vector(0,0,1),["source_ids"]=new JsonArray("src") };
    internal static JsonObject Contract(string kind) => new() {
        ["schema_version"]="DBTOOL-PHYSICAL-CONTRACT-R04",["required_kinds_by_id"]=new JsonObject{["a"]=kind},["allow_extra_entities"]=false,
        ["sources"]=new JsonArray(new JsonObject{["id"]="src",["sha256"]=Hash}),["frames"]=new JsonArray(Frame()),
        ["unit_bindings"]=new JsonArray(new JsonObject{["source_dwg_sha256"]=Hash,["coordinate_unit"]="mm",["scale_to_mm"]=1,["source_ids"]=new JsonArray("src")}),
        ["tolerances"]=new JsonObject{["coordinate_mm"]=0.000001,["angle_rad"]=0.00000001,["dimensionless"]=0.00000001,["source_ids"]=new JsonArray("src")},
        ["entity_bindings"]=new JsonObject{["a"]=new JsonObject{["space"]="Paper",["frame_id"]="f",["source_ids"]=new JsonArray("src"),["encoding_rule_id"]="unicode-reviewed",["anchor_rule_id"]="native-left-base",["style_id"]="S"}},
        ["encoding_rules"]=new JsonArray(new JsonObject{["id"]="unicode-reviewed",["source_ids"]=new JsonArray("src"),["kind"]="IdentityUnicode"})
    };
    internal static JsonObject Snapshot(JsonObject contract,string kind="Text")
    {
        var payload = kind switch {
            "Line" => new JsonObject{["start"]=new JsonObject{["xyz_mm"]=Vector()},["end"]=new JsonObject{["xyz_mm"]=Vector(10)}},
            "Circle" => new JsonObject{["center"]=new JsonObject{["xyz_mm"]=Vector()},["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)},["radius_mm"]=5},
            "Arc" => new JsonObject{["center"]=new JsonObject{["xyz_mm"]=Vector()},["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)},["radius_mm"]=5,["start_angle_rad"]=0,["sweep_rad"]=Math.PI/2,["start"]=new JsonObject{["xyz_mm"]=Vector(5)},["end"]=new JsonObject{["xyz_mm"]=Vector(0,5)}},
            "Polyline" => new JsonObject{["vertices"]=new JsonArray(Vertex(0),Vertex(10)),["closed"]=false,["elevation_mm"]=0,["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)}},
            "MText" => new JsonObject{["contents"]=new JsonObject{["raw"]="A\\PB"},["encoding_rule_id"]="unicode-reviewed",["text_height_mm"]=2,["width_mm"]=10,["attachment"]="TopLeft",["style_id"]="S",["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)}},
            "Block" => new JsonObject{["asset_id"]="asset",["asset_content_hash"]=Hash,["insertion"]=new JsonObject{["xyz_mm"]=Vector()},["scale"]=new JsonObject{["xyz"]=Vector(1,1,1)},["rotation_rad"]=0,["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)},["attributes"]=new JsonArray(new JsonObject{["tag"]="A",["value"]="text",["position"]=new JsonObject{["xyz_mm"]=Vector()},["alignment"]=new JsonObject{["xyz_mm"]=Vector()},["style_id"]="S",["height_mm"]=2,["width_factor"]=1,["rotation_rad"]=0,["horizontal_mode"]="TextLeft",["vertical_mode"]="TextBase",["mirror_x"]=false,["mirror_y"]=false}),["dependency_ids"]=new JsonArray("child")},
            "Dimension" => new JsonObject{["dimension_kind"]="RotatedDimension",["measurement_mm"]=10,["definition_points"]=new JsonArray(Definition("XLine1Point",0),Definition("XLine2Point",10),Definition("DimLinePoint",5)),["text_position"]=new JsonObject{["xyz_mm"]=Vector(5,2)},["text_override"]="",["style_id"]="S",["style_hash"]=Hash,["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)},["rotation_rad"]=0},
            "Hatch" => new JsonObject{["pattern_id"]="SOLID",["pattern_scale"]=1,["pattern_angle_rad"]=0,["loops"]=new JsonArray(new JsonObject{["id"]="loop",["looprole"]="External",["nesting_parent_id"]=null,["curves"]=new JsonArray(Curve(0,0,10,0),Curve(10,0,10,10),Curve(10,10,0,10),Curve(0,10,0,0))}),["associative"]=false,["owner_ids"]=new JsonArray()},
            _ => new JsonObject{["text"]=new JsonObject{["raw"]="A",["decoded"]="A"},["encoding_rule_id"]="unicode-reviewed",["height_mm"]=2,["width_factor"]=1,["horizontal_mode"]="TextLeft",["vertical_mode"]="TextBase",["style_id"]="S",["mirror_x"]=false,["mirror_y"]=false,["normal"]=new JsonObject{["xyz"]=Vector(0,0,1)}}
        };
        var placement = kind=="Text" ? new JsonObject{["anchor"]=new JsonObject{["xyz_mm"]=Vector(10)},["alignment"]=new JsonObject{["xyz_mm"]=Vector(10)},["rotation_rad"]=0,["anchor_semantics"]="Position",["anchor_rule_id"]="native-left-base"} : kind=="MText"?new JsonObject{["anchor"]=new JsonObject{["xyz_mm"]=Vector(10)},["rotation_rad"]=0,["anchor_semantics"]="Location",["anchor_rule_id"]="native-left-base"}:new JsonObject{["transform"]=new JsonArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1)};
        return new JsonObject{["schema_version"]="DBTOOL-PHYSICAL-R04",["run_id"]="run",["units"]="mm",["source_dwg_sha256"]=Hash,
            ["expected_contract_sha256"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contract.ToJsonString()))),["frames"]=new JsonArray(Frame()),
            ["coverage"]=new JsonObject{["required_ids"]=new JsonArray("a"),["required_kinds_by_id"]=new JsonObject{["a"]=kind}},
            ["entities"]=new JsonArray(new JsonObject{["semantic_id"]="a",["native_kind"]=kind=="Text"?"DBText":kind=="Dimension"?"RotatedDimension":kind=="Block"?"BlockReference":kind,["kind"]=kind,["space"]="Paper",["frame_id"]="f",["source_ids"]=new JsonArray("src"),["physical_payload"]=payload,["placement"]=placement,["raw_readback"]=new JsonObject(),["field_mapping"]=new JsonObject()})};
    }
    private static JsonObject Vertex(double x)=>new(){["xyz_mm"]=Vector(x),["bulge"]=0,["start_width_mm"]=0,["end_width_mm"]=0};
    private static JsonObject Definition(string role,double x)=>new(){["role"]=role,["xyz_mm"]=Vector(x)};
    private static JsonObject Curve(double x,double y,double ex,double ey)=>new(){["kind"]="Line",["payload"]=new JsonObject{["start"]=new JsonObject{["xyz_mm"]=Vector(x,y)},["end"]=new JsonObject{["xyz_mm"]=Vector(ex,ey)}}};
    private static DiffReport Compare(JsonObject c,JsonObject e,JsonObject a) => SemanticDiff.ComparePhysicalManifests(e.ToJsonString(),a.ToJsonString(),c.ToJsonString());
    [Fact] public void ValidSameCaptureEquals() { var c=Contract("Circle"); var s=Snapshot(c,"Circle"); Assert.True(Compare(c,s,s).NoDifference); }
    [Theory] [InlineData("Paper",DifferenceNature.LayoutOnly)] [InlineData("World",DifferenceNature.NumericalMismatch)]
    public void TextTranslationClassifiesAndRetainsBothAnchorDeltas(string space,DifferenceNature expected)
    {
        var c=Contract("Text"); c["entity_bindings"]!["a"]!["space"]=space;
        var e=Snapshot(c); e["entities"]![0]!["space"]=space; var a=(JsonObject)e.DeepClone();
        a["entities"]![0]!["placement"]!["anchor"]!["xyz_mm"]![0]=20;
        a["entities"]![0]!["placement"]!["alignment"]!["xyz_mm"]![0]=20;
        var change=Assert.Single(Compare(c,e,a).Changed); Assert.Equal(expected,change.Nature); Assert.Equal(2,change.Fields.Length);
    }
    [Fact] public void SpaceChangeIsCoordinateBasisMismatch() { var c=Contract("Text"); var e=Snapshot(c); var a=(JsonObject)e.DeepClone(); a["entities"]![0]!["space"]="World"; Assert.Equal(DifferenceNature.CoordinateBasisMismatch,Assert.Single(Compare(c,e,a).Changed).Nature); }
    [Fact] public void TextHeightAndContentAreSemanticChanges() { var c=Contract("Text"); var e=Snapshot(c); var a=(JsonObject)e.DeepClone(); a["entities"]![0]!["physical_payload"]!["height_mm"]=3; a["entities"]![0]!["physical_payload"]!["text"]!["raw"]="B"; a["entities"]![0]!["physical_payload"]!["text"]!["decoded"]="B"; Assert.Equal(DifferenceNature.SemanticMismatch,Assert.Single(Compare(c,e,a).Changed).Nature); }
    [Theory] [InlineData("missing-schema","SNAPSHOT_SCHEMA")] [InlineData("partial","SNAPSHOT_PAYLOAD_INCOMPLETE")] [InlineData("negative-radius","SNAPSHOT_DOMAIN")] [InlineData("unknown","SNAPSHOT_FIELD_UNMAPPED")] [InlineData("empty","SNAPSHOT_COVERAGE_MISSING")] [InlineData("actual-coverage","SNAPSHOT_COVERAGE")] [InlineData("duplicate-id","SNAPSHOT_ID_DUPLICATE")]
    public void MalformedNeverEquals(string mutation,string code)
    {
        var c=Contract("Circle"); var s=Snapshot(c,"Circle");
        switch(mutation) {
            case "missing-schema": s.Remove("schema_version"); break;
            case "partial": ((JsonObject)s["entities"]![0]!["physical_payload"]!).Remove("normal");break;
            case "negative-radius": s["entities"]![0]!["physical_payload"]!["radius_mm"]=-1;break;
            case "unknown": s["entities"]![0]!["physical_payload"]!["VertexBogus"]=1;break;
            case "empty": s["entities"]=new JsonArray();break;
            case "actual-coverage": s["coverage"]!["required_ids"]=new JsonArray();break;
            case "duplicate-id": ((JsonArray)s["entities"]!).Add(s["entities"]![0]!.DeepClone());break;
        }
        Assert.Contains(code,Assert.Throws<InvalidOperationException>(()=>Compare(c,s,s)).Message);
    }
    [Fact] public void DuplicateSchemaPropertyIsRejected() { var c=Contract("Circle"); var s=Snapshot(c,"Circle").ToJsonString(); s=s.Replace("{\"schema_version\":","{\"schema_version\":\"DBTOOL-PHYSICAL-R04\",\"schema_version\":"); Assert.Contains("SNAPSHOT_PROPERTY_DUPLICATE",Assert.Throws<InvalidOperationException>(()=>SemanticDiff.ComparePhysicalManifests(s,s,c.ToJsonString())).Message); }
    [Fact] public void ArcAngleMutationIsPreserved() { var c=Contract("Arc"); var e=Snapshot(c,"Arc"); var a=(JsonObject)e.DeepClone(); a["entities"]![0]!["physical_payload"]!["sweep_rad"]=Math.PI; a["entities"]![0]!["physical_payload"]!["end"]!["xyz_mm"]=Vector(-5); var change=Assert.Single(Compare(c,e,a).Changed); Assert.Equal(DifferenceNature.NumericalMismatch,change.Nature); Assert.Contains(change.Fields,f=>f.Field.EndsWith("sweep_rad")); }
    [Fact] public void InactiveAlignmentMutationIsMetadata() {var c=Contract("Text");var e=Snapshot(c);var a=(JsonObject)e.DeepClone();a["entities"]![0]!["placement"]!["alignment"]!["xyz_mm"]![0]=15;Assert.Equal(DifferenceNature.MetadataMismatch,Assert.Single(Compare(c,e,a).Changed).Nature);}
    [Fact] public void UnmappedRawFieldIsBlocked() {var c=Contract("Text");var s=Snapshot(c);s["entities"]![0]!["raw_readback"]!["NativeBogus"]=1;Assert.Contains("SNAPSHOT_FIELD_UNMAPPED",Assert.Throws<InvalidOperationException>(()=>Compare(c,s,s)).Message);}
    [Theory] [InlineData("Line")] [InlineData("Circle")] [InlineData("Arc")] [InlineData("Polyline")] [InlineData("Text")] [InlineData("MText")] [InlineData("Block")] [InlineData("Dimension")] [InlineData("Hatch")]
    public void EveryKindRequiresAllPayloadProperties(string kind)
    {
        var c=Contract(kind);var s=Snapshot(c,kind);Assert.True(Compare(c,s,s).NoDifference);
        foreach(var field in ((JsonObject)s["entities"]![0]!["physical_payload"]!).Select(p=>p.Key).ToArray()) {
            var malformed=(JsonObject)s.DeepClone();((JsonObject)malformed["entities"]![0]!["physical_payload"]!).Remove(field);
            Assert.Contains("SNAPSHOT_PAYLOAD_INCOMPLETE",Assert.Throws<InvalidOperationException>(()=>Compare(c,malformed,malformed)).Message);
        }
    }
    [Theory] [InlineData("Circle","Circle","Radius","physical_payload.radius_mm")] [InlineData("Text","DBText","TextString","physical_payload.text.raw")] [InlineData("Dimension","RotatedDimension","Measurement","physical_payload.measurement_mm")]
    public void NativeKindsAndConsumedFieldsRoundTrip(string kind,string nativeKind,string rawName,string target)
    {
        var c=Contract(kind);var s=Snapshot(c,kind);var entity=s["entities"]![0]!;
        var raw=new NativePhysicalEntityReadback("a",nativeKind,"Paper","f",new JsonArray("src"));raw.Entity["physical_payload"]=entity["physical_payload"]!.DeepClone();raw.Entity["placement"]=entity["placement"]!.DeepClone();
        JsonNode value=kind=="Text"?JsonValue.Create("A")!:kind=="Circle"?JsonValue.Create(5)!:JsonValue.Create(10)!;
        raw.Consume(rawName,value,target);var actual=NativePhysicalCaptureAdapter.Normalize([raw],c.ToJsonString(),Hash,"native-probe");
        Assert.True(JsonNode.DeepEquals(entity["physical_payload"],JsonNode.Parse(actual.Json)!["entities"]![0]!["physical_payload"]));
        Assert.Equal(DifferenceNature.MetadataMismatch,Assert.Single(SemanticDiff.Compare(CanonicalPhysicalValidator.Validate(s.ToJsonString(),c.ToJsonString()),actual).Changed).Nature);
        raw.Raw[rawName]=kind=="Text"?JsonValue.Create("mutated"):JsonValue.Create(-1);
        Assert.Contains("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",Assert.Throws<InvalidOperationException>(()=>NativePhysicalCaptureAdapter.Normalize([raw],c.ToJsonString(),Hash,"native-probe")).Message);
    }
    [Fact] public void GeneratedAuthorizationNeedsActualWriterProofInsteadOfPerOutputOwnerApproval()
    {
        var c=Contract("Circle");c["unit_bindings"]=new JsonArray();c["generated_output_authorization"]=new JsonObject{["source_bundle_sha256"]=Hash,["source_ids"]=new JsonArray("src"),["unit_rule_id"]="reviewed-source-unit",["writer_revision"]="R04-writer",["writer_assembly_sha256"]=Hash,["output_units"]="mm",["frame_ids"]=new JsonArray("f")};
        var s=Snapshot(c,"Circle");Assert.Contains("SNAPSHOT_GENERATED_OUTPUT_PROOF_MISSING",Assert.Throws<InvalidOperationException>(()=>Compare(c,s,s)).Message);
    }
}
