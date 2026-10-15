using System.Text.Json;
using System.Text.Json.Nodes;

namespace DBTool.Drawing;

/// <summary>Native adapters declare every read property and its canonical destination. The validator checks conversion.</summary>
public sealed class NativePhysicalEntityReadback
{
    public NativePhysicalEntityReadback(string semanticId,string nativeKind,string space,string frameId,JsonNode sourceIds)
    {
        var kind=nativeKind switch {"DBText"=>"Text","BlockReference"=>"Block","RotatedDimension" or "AlignedDimension"=>"Dimension",_=>nativeKind};
        Entity=new JsonObject{["semantic_id"]=semanticId,["native_kind"]=nativeKind,["kind"]=kind,["space"]=space,["frame_id"]=frameId,["source_ids"]=sourceIds.DeepClone(),["physical_payload"]=new JsonObject(),["placement"]=new JsonObject(),["raw_readback"]=new JsonObject(),["field_mapping"]=new JsonObject()};
    }
    public JsonObject Entity { get; }
    public JsonObject Payload => (JsonObject)Entity["physical_payload"]!;
    public JsonObject Placement => (JsonObject)Entity["placement"]!;
    public JsonObject Raw => (JsonObject)Entity["raw_readback"]!;
    public JsonObject Mapping => (JsonObject)Entity["field_mapping"]!;
    public void Consume(string nativeField,JsonNode? nativeValue,string canonicalPath,string conversion="Identity")
    {
        Raw.Add(nativeField,nativeValue?.DeepClone());
        Mapping.Add(nativeField,new JsonObject{["classification"]="Consumed",["target"]=canonicalPath,["conversion"]=conversion,["reason"]="Native property read directly; normalized once by the approved unit binding."});
    }
    public void Metadata(string nativeField,JsonNode? nativeValue,string reason)
    {
        Raw.Add(nativeField,nativeValue?.DeepClone());
        Mapping.Add(nativeField,new JsonObject{["classification"]="ExplicitlyIgnoredNonPhysical",["target"]=null,["conversion"]="Metadata",["reason"]=reason});
    }
}

public static class NativePhysicalCaptureAdapter
{
    /// <summary>The external contract is loaded before capture. No coverage is derived from these records.</summary>
    public static CanonicalPhysicalSnapshot Normalize(IEnumerable<NativePhysicalEntityReadback> records,string expectedContractJson,string sourceDwgSha256,string runId)
    {
        var c=CanonicalPhysicalValidator.ValidateContract(expectedContractJson);var required=c.GetProperty("required_kinds_by_id");
        var root=new JsonObject {
            ["schema_version"]=CanonicalPhysicalValidator.SchemaVersion,["run_id"]=runId,["units"]="mm",["source_dwg_sha256"]=sourceDwgSha256,
            ["expected_contract_sha256"]=CanonicalPhysicalValidator.Hash(expectedContractJson),["frames"]=JsonNode.Parse(c.GetProperty("frames").GetRawText()),
            ["coverage"]=new JsonObject{["required_ids"]=new JsonArray(required.EnumerateObject().Select(p=>(JsonNode?)JsonValue.Create(p.Name)).ToArray()),["required_kinds_by_id"]=JsonNode.Parse(required.GetRawText())},
            ["entities"]=new JsonArray(records.Select(r=>(JsonNode?)r.Entity.DeepClone()).ToArray())
        };
        return CanonicalPhysicalValidator.Validate(root.ToJsonString(),expectedContractJson);
    }
}
