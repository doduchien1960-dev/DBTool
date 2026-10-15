using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DBTool.Drawing;

/// <summary>An immutable, complete R04 capture. Only the shared validator can construct it.</summary>
public sealed class CanonicalPhysicalSnapshot
{
    internal CanonicalPhysicalSnapshot(JsonElement root, JsonElement contract, string contractHash)
    { Root=root.Clone(); Contract=contract.Clone(); ContractHash=contractHash; }
    internal JsonElement Root { get; }
    internal JsonElement Contract { get; }
    public string ContractHash { get; }
    public string Json => Root.GetRawText();
    public string RunId => Root.GetProperty("run_id").GetString()!;
}

/// <summary>Acceptance has one gate: producer and all consumers validate against exact external contract bytes.</summary>
public static class CanonicalPhysicalValidator
{
    public const string SchemaVersion="DBTOOL-PHYSICAL-R04";
    public const string ContractVersion="DBTOOL-PHYSICAL-CONTRACT-R04";
    private static readonly string[] Kinds=["Line","Circle","Arc","Polyline","Text","MText","Block","Dimension","Hatch"];
    public static string Hash(string bytes) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(bytes)));
    public static double NativeArcSweep(double start,double end)
    {
        var delta=end-start;if(!double.IsFinite(start)||!double.IsFinite(end)||!double.IsFinite(delta))throw Fault("SNAPSHOT_DOMAIN","native arc angle delta");
        delta%=Math.Tau;return delta<=0?delta+Math.Tau:delta;
    }
    private static InvalidOperationException Fault(string code,string path) => new(code+": "+path);
    private static void Object(JsonElement node,string path,params string[] keys)
    {
        if(node.ValueKind!=JsonValueKind.Object) throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE",path+" must be an object");
        foreach(var key in keys) if(!node.TryGetProperty(key,out _)) throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE",path+"."+key);
        foreach(var property in node.EnumerateObject()) if(!keys.Contains(property.Name,StringComparer.Ordinal)) throw Fault("SNAPSHOT_FIELD_UNMAPPED",path+"."+property.Name);
    }
    private static void UniqueProperties(JsonElement n,string path)
    {
        if(n.ValueKind==JsonValueKind.Object) {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in n.EnumerateObject()) { if(!names.Add(p.Name)) throw Fault("SNAPSHOT_PROPERTY_DUPLICATE",path+"."+p.Name); UniqueProperties(p.Value,path+"."+p.Name); }
        } else if(n.ValueKind==JsonValueKind.Array) {int i=0;foreach(var item in n.EnumerateArray()) UniqueProperties(item,path+"["+(i++)+"]");}
    }
    private static string Text(JsonElement n,string path,bool allowEmpty=false)
    { if(n.ValueKind!=JsonValueKind.String || (!allowEmpty && string.IsNullOrWhiteSpace(n.GetString()))) throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE",path);return n.GetString()!; }
    private static double Number(JsonElement n,string path,bool positive=false,bool nonnegative=false)
    {if(n.ValueKind!=JsonValueKind.Number || !n.TryGetDouble(out var value)||!double.IsFinite(value)||positive&&value<=0||nonnegative&&value<0) throw Fault("SNAPSHOT_DOMAIN",path); return value;}
    private static bool Bool(JsonElement n,string path)
    {if(n.ValueKind is not(JsonValueKind.True or JsonValueKind.False)) throw Fault("SNAPSHOT_DOMAIN",path);return n.GetBoolean();}
    private static string Sha(JsonElement n,string path)
    {var value=Text(n,path);if(value.Length!=64||!value.All(Uri.IsHexDigit)) throw Fault("SNAPSHOT_SOURCE_INVALID",path);return value;}
    private static JsonElement[] Array(JsonElement n,string path,int minimum=0)
    {if(n.ValueKind!=JsonValueKind.Array||n.GetArrayLength()<minimum) throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE",path);return n.EnumerateArray().ToArray();}
    private static double[] Vector(JsonElement n,string path)
    {var a=Array(n,path,3);if(a.Length!=3) throw Fault("SNAPSHOT_DOMAIN",path);return a.Select((v,i)=>Number(v,path+"["+i+"]")).ToArray();}
    private static double Dot(double[] a,double[] b)=>a.Zip(b,(x,y)=>x*y).Sum();
    private static double[] Cross(double[] a,double[] b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
    private static void UnitVector(JsonElement n,string path,double tolerance)
    {var v=Vector(n,path);if(Math.Abs(Dot(v,v)-1)>tolerance) throw Fault("SNAPSHOT_DOMAIN",path+" unit vector");}
    private static void Point(JsonElement n,string path,string key="xyz_mm") {Object(n,path,key);Vector(n.GetProperty(key),path+"."+key);}
    private static void Normal(JsonElement n,string path,double tolerance) {Object(n,path,"xyz");UnitVector(n.GetProperty("xyz"),path+".xyz",tolerance);}
    private static string[] Strings(JsonElement n,string path,int minimum=0)
    {var values=Array(n,path,minimum).Select((v,i)=>Text(v,path+"["+i+"]")).ToArray();if(values.Distinct(StringComparer.Ordinal).Count()!=values.Length)throw Fault("SNAPSHOT_ID_DUPLICATE",path);return values;}
    private static void SourceIds(JsonElement n,JsonElement contract,string path)
    {
        var values=Strings(n,path,1);var sourceIds=contract.GetProperty("sources").EnumerateArray().Select(s=>s.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        foreach(var id in values) if(!sourceIds.Contains(id))throw Fault("SNAPSHOT_SOURCE_UNRESOLVED",path+":"+id);
    }
    private static double Tolerance(JsonElement c,string field)=>c.GetProperty("tolerances").GetProperty(field).GetDouble();
    private static void Frames(JsonElement frames,JsonElement c)
    {
        var seen=new HashSet<string>(StringComparer.Ordinal);var limit=Tolerance(c,"dimensionless");
        foreach(var f in Array(frames,"frames",1)) {
            Object(f,"frames[]","id","origin_xyz_mm","basis_u_xyz","basis_v_xyz","normal_xyz","source_ids");
            var id=Text(f.GetProperty("id"),"frame.id");if(!seen.Add(id))throw Fault("SNAPSHOT_ID_DUPLICATE","frame:"+id);
            Vector(f.GetProperty("origin_xyz_mm"),"frame.origin_xyz_mm");SourceIds(f.GetProperty("source_ids"),c,"frame.source_ids");
            foreach(var axis in new[]{"basis_u_xyz","basis_v_xyz","normal_xyz"}) UnitVector(f.GetProperty(axis),"frame."+axis,limit);
            var u=Vector(f.GetProperty("basis_u_xyz"),"u");var v=Vector(f.GetProperty("basis_v_xyz"),"v");var normal=Vector(f.GetProperty("normal_xyz"),"normal");
            if(Math.Abs(Dot(u,v))>limit||Cross(u,v).Zip(normal,(a,b)=>Math.Abs(a-b)).Any(d=>d>limit))throw Fault("SNAPSHOT_COORDINATE_BASIS_INVALID",id);
        }
    }
    /// <summary>Checks external coverage, authority, unit and tolerances independently of any actual entities.</summary>
    public static JsonElement ValidateContract(string json)
    {
        using var doc=JsonDocument.Parse(json);var c=doc.RootElement;UniqueProperties(c,"contract");
        var contractKeys=new[]{"schema_version","required_kinds_by_id","allow_extra_entities","sources","frames","unit_bindings","tolerances","entity_bindings","encoding_rules"};
        Object(c,"contract",c.TryGetProperty("generated_output_authorization",out _)?contractKeys.Append("generated_output_authorization").ToArray():contractKeys);
        if(Text(c.GetProperty("schema_version"),"contract.schema_version")!=ContractVersion)throw Fault("SNAPSHOT_EXPECTED_CONTRACT_VERSION",ContractVersion);
        var ids=c.GetProperty("required_kinds_by_id");if(ids.ValueKind!=JsonValueKind.Object||!ids.EnumerateObject().Any())throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED","required_kinds_by_id");
        foreach(var p in ids.EnumerateObject()) {if(string.IsNullOrWhiteSpace(p.Name)||!Kinds.Contains(Text(p.Value,"required kind")))throw Fault("SNAPSHOT_UNKNOWN_KIND",p.Name);}
        Bool(c.GetProperty("allow_extra_entities"),"allow_extra_entities");
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in Array(c.GetProperty("sources"),"sources",1)) {Object(source,"sources[]","id","sha256");if(!seen.Add(Text(source.GetProperty("id"),"source.id")))throw Fault("SNAPSHOT_ID_DUPLICATE","source");Sha(source.GetProperty("sha256"),"source.sha256");}
        var t=c.GetProperty("tolerances");Object(t,"tolerances","coordinate_mm","angle_rad","dimensionless","source_ids");
        foreach(var key in new[]{"coordinate_mm","angle_rad","dimensionless"})Number(t.GetProperty(key),"tolerances."+key,positive:true);
        SourceIds(t.GetProperty("source_ids"),c,"tolerances.source_ids");Frames(c.GetProperty("frames"),c);
        seen.Clear();
        foreach(var binding in Array(c.GetProperty("unit_bindings"),"unit_bindings",c.TryGetProperty("generated_output_authorization",out _)?0:1)) {
            Object(binding,"unit_bindings[]","source_dwg_sha256","coordinate_unit","scale_to_mm","source_ids");
            if(!seen.Add(Sha(binding.GetProperty("source_dwg_sha256"),"unit.dwg")))throw Fault("SNAPSHOT_ID_DUPLICATE","unit.dwg");
            var unit=Text(binding.GetProperty("coordinate_unit"),"coordinate_unit");var scale=Number(binding.GetProperty("scale_to_mm"),"scale_to_mm",positive:true);
            var exact=unit switch {"mm"=>1.0,"cm"=>10.0,"m"=>1000.0,"in"=>25.4,"ft"=>304.8,_=>throw Fault("SNAPSHOT_UNIT_UNRESOLVED",unit)};
            if(scale!=exact)throw Fault("SNAPSHOT_UNIT_BINDING_INVALID",unit);SourceIds(binding.GetProperty("source_ids"),c,"unit.source_ids");
        }
        if(c.TryGetProperty("generated_output_authorization",out var generated)) {
            Object(generated,"generated_output_authorization","source_bundle_sha256","source_ids","unit_rule_id","writer_revision","writer_assembly_sha256","output_units","frame_ids");
            var bundle=Sha(generated.GetProperty("source_bundle_sha256"),"generated.source_bundle_sha256");
            if(!c.GetProperty("sources").EnumerateArray().Any(s=>string.Equals(s.GetProperty("sha256").GetString(),bundle,StringComparison.OrdinalIgnoreCase)))throw Fault("SNAPSHOT_SOURCE_UNRESOLVED","generated source bundle");
            SourceIds(generated.GetProperty("source_ids"),c,"generated.source_ids");Text(generated.GetProperty("unit_rule_id"),"generated.unit_rule_id");Text(generated.GetProperty("writer_revision"),"generated.writer_revision");Sha(generated.GetProperty("writer_assembly_sha256"),"generated.writer_assembly_sha256");
            if(Text(generated.GetProperty("output_units"),"generated.output_units")!="mm")throw Fault("SNAPSHOT_UNIT_BINDING_INVALID","generated output must be mm");
            var frames=c.GetProperty("frames").EnumerateArray().Select(f=>f.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);foreach(var id in Strings(generated.GetProperty("frame_ids"),"generated.frame_ids",1))if(!frames.Contains(id))throw Fault("SNAPSHOT_FRAME_UNRESOLVED",id);
        }
        var bindings=c.GetProperty("entity_bindings");if(bindings.ValueKind!=JsonValueKind.Object)throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED","entity_bindings");
        foreach(var p in ids.EnumerateObject())if(!bindings.TryGetProperty(p.Name,out _))throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED","entity binding:"+p.Name);
        foreach(var p in bindings.EnumerateObject()) {
            var b=p.Value;if(b.ValueKind!=JsonValueKind.Object)throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED",p.Name);
            foreach(var key in new[]{"space","frame_id","source_ids"})if(!b.TryGetProperty(key,out _))throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED",p.Name+"."+key);
            foreach(var q in b.EnumerateObject())if(!new[]{"space","frame_id","source_ids","encoding_rule_id","anchor_rule_id","style_id"}.Contains(q.Name))throw Fault("SNAPSHOT_FIELD_UNMAPPED","binding."+q.Name);
            if(Text(b.GetProperty("space"),"binding.space") is not("World" or "Paper"))throw Fault("SNAPSHOT_DOMAIN","binding.space");
            if(!c.GetProperty("frames").EnumerateArray().Any(f=>f.GetProperty("id").GetString()==Text(b.GetProperty("frame_id"),"binding.frame")))throw Fault("SNAPSHOT_FRAME_UNRESOLVED",p.Name);
            SourceIds(b.GetProperty("source_ids"),c,"binding.source_ids");
        }
        seen.Clear();foreach(var rule in Array(c.GetProperty("encoding_rules"),"encoding_rules")) {
            Object(rule,"encoding_rules[]","id","source_ids","kind");if(!seen.Add(Text(rule.GetProperty("id"),"encoding.id")))throw Fault("SNAPSHOT_ID_DUPLICATE","encoding");
            SourceIds(rule.GetProperty("source_ids"),c,"encoding.source_ids");if(Text(rule.GetProperty("kind"),"encoding.kind")!="IdentityUnicode")throw Fault("SNAPSHOT_ENCODING_UNSUPPORTED",rule.GetRawText());
        }
        return c.Clone();
    }
    public static CanonicalPhysicalSnapshot Validate(string snapshotJson,string expectedContractJson)
    {
        if(string.IsNullOrWhiteSpace(expectedContractJson))throw Fault("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED","external contract");
        var c=ValidateContract(expectedContractJson);using var doc=JsonDocument.Parse(snapshotJson);var root=doc.RootElement;UniqueProperties(root,"snapshot");
        if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("schema_version",out var version)||version.ValueKind!=JsonValueKind.String||version.GetString()!=SchemaVersion)throw Fault("SNAPSHOT_SCHEMA",SchemaVersion+" object required; archived revisions cannot sign acceptance");
        Object(root,"snapshot","schema_version","run_id","units","source_dwg_sha256","expected_contract_sha256","entities","frames","coverage");
        Text(root.GetProperty("run_id"),"run_id");if(Text(root.GetProperty("units"),"units")!="mm")throw Fault("SNAPSHOT_UNIT_UNRESOLVED","canonical units must be mm");
        var hash=Hash(expectedContractJson);if(!string.Equals(Sha(root.GetProperty("expected_contract_sha256"),"expected_contract_sha256"),hash,StringComparison.OrdinalIgnoreCase))throw Fault("SNAPSHOT_EXPECTED_CONTRACT_HASH","external bytes differ");
        var dwg=Sha(root.GetProperty("source_dwg_sha256"),"source_dwg_sha256");
        var bindings=c.GetProperty("unit_bindings").EnumerateArray().Where(b=>string.Equals(b.GetProperty("source_dwg_sha256").GetString(),dwg,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(bindings.Length!=1)throw Fault(c.TryGetProperty("generated_output_authorization",out _)?"SNAPSHOT_GENERATED_OUTPUT_PROOF_MISSING":"SNAPSHOT_UNIT_UNRESOLVED","saved DWG "+dwg+" requires verified normalized-mm write/save/reopen proof linked to its approved origin; input hashes or INSUNITS are insufficient");
        Frames(root.GetProperty("frames"),c);
        var coverage=root.GetProperty("coverage");Object(coverage,"coverage","required_ids","required_kinds_by_id");
        var required=c.GetProperty("required_kinds_by_id");var requiredIds=required.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);
        if(!requiredIds.SetEquals(Strings(coverage.GetProperty("required_ids"),"coverage.required_ids"))||!Same(coverage.GetProperty("required_kinds_by_id"),required))throw Fault("SNAPSHOT_COVERAGE","must pass through external expected contract");
        var seen=new HashSet<string>(StringComparer.Ordinal);var frameIds=root.GetProperty("frames").EnumerateArray().Select(f=>f.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        foreach(var entity in Array(root.GetProperty("entities"),"entities")) {
            Object(entity,"entities[]","semantic_id","native_kind","kind","space","frame_id","source_ids","physical_payload","placement","raw_readback","field_mapping");
            var id=Text(entity.GetProperty("semantic_id"),"semantic_id");if(!seen.Add(id))throw Fault("SNAPSHOT_ID_DUPLICATE",id);
            if(!requiredIds.Contains(id)&&!c.GetProperty("allow_extra_entities").GetBoolean())throw Fault("SNAPSHOT_COVERAGE_UNEXPECTED",id);
            var kind=Text(entity.GetProperty("kind"),"kind");if(!Kinds.Contains(kind))throw Fault("SNAPSHOT_UNKNOWN_KIND",id+":"+kind);
            var native=Text(entity.GetProperty("native_kind"),"native_kind");
            var canonical=native switch {"DBText"=>"Text","BlockReference"=>"Block","RotatedDimension" or "AlignedDimension"=>"Dimension",_=>native};
            if(canonical!=kind)throw Fault("SNAPSHOT_NATIVE_KIND_UNMAPPED",native+" → "+kind);
            if(Text(entity.GetProperty("space"),"space") is not("World" or "Paper"))throw Fault("SNAPSHOT_DOMAIN","space");
            if(!frameIds.Contains(Text(entity.GetProperty("frame_id"),"frame_id")))throw Fault("SNAPSHOT_FRAME_UNRESOLVED",id);
            SourceIds(entity.GetProperty("source_ids"),c,"entity.source_ids");
            Payload(entity.GetProperty("physical_payload"),kind,c,id);
            Placement(entity.GetProperty("placement"),kind);
            Mapping(entity,c,bindings[0].GetProperty("scale_to_mm").GetDouble());
        }
        var missing=requiredIds.Except(seen).ToArray();if(missing.Length>0)throw Fault("SNAPSHOT_COVERAGE_MISSING",string.Join(",",missing));
        return new(root,c,hash);
    }
    private static void Encoding(JsonElement p,JsonElement c,string contentProperty)
    {
        var id=Text(p.GetProperty("encoding_rule_id"),"encoding_rule_id");if(!c.GetProperty("encoding_rules").EnumerateArray().Any(r=>r.GetProperty("id").GetString()==id))throw Fault("SNAPSHOT_ENCODING_UNRESOLVED",id);
        if(contentProperty=="text") {
            var text=p.GetProperty("text");Object(text,"text","raw","decoded");var raw=Text(text.GetProperty("raw"),"text.raw",true);var decoded=Text(text.GetProperty("decoded"),"text.decoded",true);
            if(raw!=decoded)throw Fault("SNAPSHOT_ENCODING_UNSUPPORTED","IdentityUnicode changed raw text");
        }else {var contents=p.GetProperty("contents");Object(contents,"contents","raw");Text(contents.GetProperty("raw"),"contents.raw",true);}
    }
    private static void Payload(JsonElement p,string kind,JsonElement c,string id)
    {
        var tol=Tolerance(c,"dimensionless");
        switch(kind) {
            case "Line": Object(p,id,"start","end");Point(p.GetProperty("start"),"start");Point(p.GetProperty("end"),"end");
                if(Vector(p.GetProperty("start").GetProperty("xyz_mm"),"start").Zip(Vector(p.GetProperty("end").GetProperty("xyz_mm"),"end"),(a,b)=>(a-b)*(a-b)).Sum()<=Math.Pow(Tolerance(c,"coordinate_mm"),2))throw Fault("SNAPSHOT_DOMAIN",id+" degenerate line");break;
            case "Circle": Object(p,id,"center","normal","radius_mm");Point(p.GetProperty("center"),"center");Normal(p.GetProperty("normal"),"normal",tol);Number(p.GetProperty("radius_mm"),"radius_mm",positive:true);break;
            case "Arc": Object(p,id,"center","normal","radius_mm","start_angle_rad","sweep_rad","start","end");Point(p.GetProperty("center"),"center");Normal(p.GetProperty("normal"),"normal",tol);var radius=Number(p.GetProperty("radius_mm"),"radius_mm",positive:true);Number(p.GetProperty("start_angle_rad"),"start_angle_rad");var sweep=Number(p.GetProperty("sweep_rad"),"sweep_rad");
                // L03 curve-domain contract: signed nonzero sweep through one full turn.
                if(sweep==0||Math.Abs(sweep)>Math.Tau)throw Fault("SNAPSHOT_DOMAIN","arc requires 0 < abs(sweep_rad) <= 2π");
                Point(p.GetProperty("start"),"start");Point(p.GetProperty("end"),"end");var center=Vector(p.GetProperty("center").GetProperty("xyz_mm"),"center");
                foreach(var endpoint in new[]{"start","end"}) {var delta=Vector(p.GetProperty(endpoint).GetProperty("xyz_mm"),endpoint).Zip(center,(a,b)=>a-b).ToArray();if(Math.Abs(Math.Sqrt(Dot(delta,delta))-radius)>Tolerance(c,"coordinate_mm")||Math.Abs(Dot(delta,Vector(p.GetProperty("normal").GetProperty("xyz"),"normal")))>Tolerance(c,"coordinate_mm"))throw Fault("SNAPSHOT_DOMAIN","arc "+endpoint+" inconsistent with radius/plane");}
                // Rodrigues rotation validates the oriented analytic domain in any plane.
                // Radius/plane membership alone would accept an unrelated endpoint.
                var arcNormal=Vector(p.GetProperty("normal").GetProperty("xyz"),"normal");
                var normalLength=Math.Sqrt(Dot(arcNormal,arcNormal));
                arcNormal=arcNormal.Select(value=>value/normalLength).ToArray();
                var fromCenter=Vector(p.GetProperty("start").GetProperty("xyz_mm"),"start").Zip(center,(a,b)=>a-b).ToArray();
                var tangent=Cross(arcNormal,fromCenter);var axial=Dot(arcNormal,fromCenter);
                var observedEnd=Vector(p.GetProperty("end").GetProperty("xyz_mm"),"end");
                var endpointError=Enumerable.Range(0,3).Select(i=>{
                    var predicted=center[i]+fromCenter[i]*Math.Cos(sweep)+tangent[i]*Math.Sin(sweep)+arcNormal[i]*axial*(1-Math.Cos(sweep));
                    return Math.Pow(predicted-observedEnd[i],2);
                }).Sum();
                if(Math.Sqrt(endpointError)>Tolerance(c,"coordinate_mm"))throw Fault("SNAPSHOT_DOMAIN",id+" arc endpoint inconsistent with oriented sweep");
                break;
            case "Polyline": Object(p,id,"vertices","closed","elevation_mm","normal");Bool(p.GetProperty("closed"),"closed");Number(p.GetProperty("elevation_mm"),"elevation_mm");Normal(p.GetProperty("normal"),"normal",tol);
                foreach(var v in Array(p.GetProperty("vertices"),"vertices",2)){Object(v,"vertex","xyz_mm","bulge","start_width_mm","end_width_mm");Vector(v.GetProperty("xyz_mm"),"vertex.xyz_mm");Number(v.GetProperty("bulge"),"vertex.bulge");Number(v.GetProperty("start_width_mm"),"vertex.start_width_mm",nonnegative:true);Number(v.GetProperty("end_width_mm"),"vertex.end_width_mm",nonnegative:true);}break;
            case "Text": Object(p,id,"text","encoding_rule_id","height_mm","width_factor","horizontal_mode","vertical_mode","style_id","mirror_x","mirror_y","normal");Encoding(p,c,"text");Number(p.GetProperty("height_mm"),"height_mm",positive:true);Number(p.GetProperty("width_factor"),"width_factor",positive:true);Text(p.GetProperty("style_id"),"style_id");Bool(p.GetProperty("mirror_x"),"mirror_x");Bool(p.GetProperty("mirror_y"),"mirror_y");Normal(p.GetProperty("normal"),"normal",tol);
                if(Text(p.GetProperty("horizontal_mode"),"horizontal_mode")!="TextLeft"||Text(p.GetProperty("vertical_mode"),"vertical_mode")!="TextBase")throw Fault("SNAPSHOT_ANCHOR_MODE_UNSUPPORTED",id+":"+p.GetRawText());break;
            case "MText": Object(p,id,"contents","encoding_rule_id","text_height_mm","width_mm","attachment","style_id","normal");Encoding(p,c,"contents");Number(p.GetProperty("text_height_mm"),"text_height_mm",positive:true);Number(p.GetProperty("width_mm"),"width_mm",nonnegative:true);Text(p.GetProperty("style_id"),"style_id");
                if(!new[]{"TopLeft","TopCenter","TopRight","MiddleLeft","MiddleCenter","MiddleRight","BottomLeft","BottomCenter","BottomRight"}.Contains(Text(p.GetProperty("attachment"),"attachment")))throw Fault("SNAPSHOT_ANCHOR_MODE_UNSUPPORTED",id);Normal(p.GetProperty("normal"),"normal",tol);ValidateMTextControls(p.GetProperty("contents").GetProperty("raw").GetString()!);break;
            case "Block": Object(p,id,"asset_id","asset_content_hash","insertion","scale","rotation_rad","normal","attributes","dependency_ids");Text(p.GetProperty("asset_id"),"asset_id");Sha(p.GetProperty("asset_content_hash"),"asset_content_hash");Point(p.GetProperty("insertion"),"insertion");Point(p.GetProperty("scale"),"scale","xyz");if(Vector(p.GetProperty("scale").GetProperty("xyz"),"scale").Any(v=>v==0))throw Fault("SNAPSHOT_DOMAIN","zero block scale");Number(p.GetProperty("rotation_rad"),"rotation_rad");Normal(p.GetProperty("normal"),"normal",tol);Strings(p.GetProperty("dependency_ids"),"dependency_ids");
                var tags=new HashSet<string>(StringComparer.Ordinal);foreach(var a in Array(p.GetProperty("attributes"),"attributes")) {Object(a,"attribute","tag","value","position","alignment","style_id","height_mm","width_factor","rotation_rad","horizontal_mode","vertical_mode","mirror_x","mirror_y");if(!tags.Add(Text(a.GetProperty("tag"),"tag")))throw Fault("SNAPSHOT_ID_DUPLICATE","attribute tag");Text(a.GetProperty("value"),"value",true);Point(a.GetProperty("position"),"position");Point(a.GetProperty("alignment"),"alignment");Text(a.GetProperty("style_id"),"style_id");Number(a.GetProperty("height_mm"),"height_mm",positive:true);Number(a.GetProperty("width_factor"),"width_factor",positive:true);Number(a.GetProperty("rotation_rad"),"rotation_rad");Text(a.GetProperty("horizontal_mode"),"horizontal_mode");Text(a.GetProperty("vertical_mode"),"vertical_mode");Bool(a.GetProperty("mirror_x"),"mirror_x");Bool(a.GetProperty("mirror_y"),"mirror_y");}break;
            case "Dimension":
                var dk=p.TryGetProperty("dimension_kind",out var dkind)?Text(dkind,"dimension_kind"):throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE","dimension_kind");
                if(dk is not("RotatedDimension" or "AlignedDimension"))throw Fault("SNAPSHOT_NATIVE_KIND_UNMAPPED",dk);
                Object(p,id,dk=="RotatedDimension"?["dimension_kind","measurement_mm","definition_points","text_position","text_override","style_id","style_hash","normal","rotation_rad"]:["dimension_kind","measurement_mm","definition_points","text_position","text_override","style_id","style_hash","normal"]);
                Number(p.GetProperty("measurement_mm"),"measurement_mm",nonnegative:true);Point(p.GetProperty("text_position"),"text_position");Text(p.GetProperty("text_override"),"text_override",true);Text(p.GetProperty("style_id"),"style_id");Sha(p.GetProperty("style_hash"),"style_hash");Normal(p.GetProperty("normal"),"normal",tol);if(dk=="RotatedDimension")Number(p.GetProperty("rotation_rad"),"rotation_rad");
                var roles=new HashSet<string>(StringComparer.Ordinal);foreach(var d in Array(p.GetProperty("definition_points"),"definition_points",3)){Object(d,"definition_point","role","xyz_mm");if(!roles.Add(Text(d.GetProperty("role"),"role")))throw Fault("SNAPSHOT_ID_DUPLICATE","definition point");Vector(d.GetProperty("xyz_mm"),"definition_point.xyz_mm");}if(!roles.SetEquals(new[]{"XLine1Point","XLine2Point","DimLinePoint"}))throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE","dimension definition points");break;
            case "Hatch": Object(p,id,"pattern_id","pattern_scale","pattern_angle_rad","loops","associative","owner_ids");Text(p.GetProperty("pattern_id"),"pattern_id");Number(p.GetProperty("pattern_scale"),"pattern_scale",positive:true);Number(p.GetProperty("pattern_angle_rad"),"pattern_angle_rad");Bool(p.GetProperty("associative"),"associative");Strings(p.GetProperty("owner_ids"),"owner_ids");HatchLoops(p.GetProperty("loops"),c);break;
        }
    }
    private static void ValidateMTextControls(string raw)
    {
        for(int i=0;i<raw.Length-1;i++)if(raw[i]=='\\') {if(!"\\{}P~LlOoKkHhWwTtQqCcFfAaSsU".Contains(raw[i+1]))throw Fault("SNAPSHOT_MTEXT_CONTROL_UNSUPPORTED",raw);i++;}
        if(raw.EndsWith('\\'))throw Fault("SNAPSHOT_MTEXT_CONTROL_UNSUPPORTED",raw);
    }
    private static void HatchLoops(JsonElement loops,JsonElement c)
    {
        var list=Array(loops,"loops",1);var seen=new HashSet<string>(StringComparer.Ordinal);var limit=Tolerance(c,"coordinate_mm");
        foreach(var loop in list){Object(loop,"loop","id","looprole","nesting_parent_id","curves");if(!seen.Add(Text(loop.GetProperty("id"),"loop.id")))throw Fault("SNAPSHOT_ID_DUPLICATE","hatch.loop");if(Text(loop.GetProperty("looprole"),"looprole") is not("External" or "Internal"))throw Fault("SNAPSHOT_DOMAIN","looprole");if(loop.GetProperty("nesting_parent_id").ValueKind!=JsonValueKind.Null)Text(loop.GetProperty("nesting_parent_id"),"nesting_parent_id");
            var curves=Array(loop.GetProperty("curves"),"curves",1);foreach(var curve in curves){Object(curve,"curve","kind","payload");var k=Text(curve.GetProperty("kind"),"curve.kind");if(k is not("Line" or "Arc" or "Circle"))throw Fault("SNAPSHOT_FIELD_UNMAPPED","hatch curve "+k);Payload(curve.GetProperty("payload"),k,c,"curve");}
            if(curves.Length==1&&curves[0].GetProperty("kind").GetString()=="Circle")continue;
            for(int i=0;i<curves.Length;i++){var current=curves[i].GetProperty("payload");var next=curves[(i+1)%curves.Length].GetProperty("payload");if(!current.TryGetProperty("end",out var end)||!next.TryGetProperty("start",out var start))throw Fault("SNAPSHOT_CAPTURE_INCOMPLETE","hatch analytic endpoints");if(Vector(end.GetProperty("xyz_mm"),"end").Zip(Vector(start.GetProperty("xyz_mm"),"start"),(a,b)=>Math.Abs(a-b)).Any(d=>d>limit))throw Fault("SNAPSHOT_DOMAIN","hatch loop not closed");}
        }
        foreach(var loop in list)if(loop.GetProperty("nesting_parent_id").ValueKind!=JsonValueKind.Null){var parent=loop.GetProperty("nesting_parent_id").GetString()!;if(!seen.Contains(parent)||parent==loop.GetProperty("id").GetString())throw Fault("SNAPSHOT_DOMAIN","hatch nesting parent");var visited=new HashSet<string>();var node=loop;while(node.GetProperty("nesting_parent_id").ValueKind!=JsonValueKind.Null){var p=node.GetProperty("nesting_parent_id").GetString()!;if(!visited.Add(p))throw Fault("SNAPSHOT_DOMAIN","hatch nesting cycle");node=list.Single(n=>n.GetProperty("id").GetString()==p);}}
    }
    private static void Placement(JsonElement p,string kind)
    {
        if(kind=="Text") {Object(p,"placement","anchor","alignment","rotation_rad","anchor_semantics","anchor_rule_id");Point(p.GetProperty("anchor"),"placement.anchor");Point(p.GetProperty("alignment"),"placement.alignment");Number(p.GetProperty("rotation_rad"),"placement.rotation_rad");if(Text(p.GetProperty("anchor_semantics"),"anchor_semantics")!="Position")throw Fault("SNAPSHOT_ANCHOR_MODE_UNSUPPORTED","TextLeft/TextBase requires Position");Text(p.GetProperty("anchor_rule_id"),"anchor_rule_id");}
        else if(kind=="MText") {Object(p,"placement","anchor","rotation_rad","anchor_semantics","anchor_rule_id");Point(p.GetProperty("anchor"),"placement.anchor");Number(p.GetProperty("rotation_rad"),"rotation_rad");if(Text(p.GetProperty("anchor_semantics"),"anchor_semantics")!="Location")throw Fault("SNAPSHOT_ANCHOR_MODE_UNSUPPORTED","MText requires Location");Text(p.GetProperty("anchor_rule_id"),"anchor_rule_id");}
        else {Object(p,"placement","transform");var a=Array(p.GetProperty("transform"),"placement.transform",16);if(a.Length!=16)throw Fault("SNAPSHOT_DOMAIN","placement.transform");foreach(var v in a)Number(v,"placement.transform");if(a[12].GetDouble()!=0||a[13].GetDouble()!=0||a[14].GetDouble()!=0||a[15].GetDouble()!=1)throw Fault("SNAPSHOT_DOMAIN","affine placement transform");}
    }
    private static Dictionary<string,JsonElement> Leaves(JsonElement node,string prefix="")
    {
        var result=new Dictionary<string,JsonElement>(StringComparer.Ordinal);void Walk(JsonElement n,string path){if(n.ValueKind==JsonValueKind.Object&&n.EnumerateObject().Any())foreach(var p in n.EnumerateObject())Walk(p.Value,path.Length==0?p.Name:path+"."+p.Name);else if(n.ValueKind==JsonValueKind.Array&&n.GetArrayLength()>0){int i=0;foreach(var item in n.EnumerateArray())Walk(item,path+"["+(i++)+"]");}else result.Add(path,n);}Walk(node,prefix);return result;
    }
    private static void Mapping(JsonElement entity,JsonElement c,double scale)
    {
        var raw=entity.GetProperty("raw_readback");var mappings=entity.GetProperty("field_mapping");if(raw.ValueKind!=JsonValueKind.Object||mappings.ValueKind!=JsonValueKind.Object)throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE","raw_readback/field_mapping");
        var targets=Leaves(entity);var rawKeys=raw.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);
        if(!rawKeys.SetEquals(mappings.EnumerateObject().Select(p=>p.Name)))throw Fault("SNAPSHOT_FIELD_UNMAPPED","raw_readback field inventory");
        foreach(var item in raw.EnumerateObject()){
            if(item.Value.ValueKind is not(JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))throw Fault("SNAPSHOT_FIELD_UNMAPPED","raw_readback."+item.Name+" must be leaf");
            var map=mappings.GetProperty(item.Name);Object(map,"field_mapping."+item.Name,"classification","target","conversion","reason");var classification=Text(map.GetProperty("classification"),"mapping.classification");var conversion=Text(map.GetProperty("conversion"),"mapping.conversion");Text(map.GetProperty("reason"),"mapping.reason");
            if(classification=="ExplicitlyIgnoredNonPhysical") {
                if(map.GetProperty("target").ValueKind!=JsonValueKind.Null||conversion!="Metadata"||!new[]{"Handle","Layer","Linetype","ColorIndex","Lineweight","Role","TextStyle.Font","TextStyle.BigFont","Bounds.Min.X","Bounds.Min.Y","Bounds.Min.Z","Bounds.Max.X","Bounds.Max.Y","Bounds.Max.Z"}.Contains(item.Name,StringComparer.Ordinal))throw Fault("SNAPSHOT_FIELD_UNMAPPED",item.Name+" is not declared nonphysical metadata");continue;
            }
            if(classification!="Consumed")throw Fault("SNAPSHOT_FIELD_UNMAPPED",item.Name);
            var target=Text(map.GetProperty("target"),"mapping.target");if(!targets.TryGetValue(target,out var normalized)||(!target.StartsWith("physical_payload.",StringComparison.Ordinal)&&!target.StartsWith("placement.",StringComparison.Ordinal)))throw Fault("SNAPSHOT_FIELD_UNMAPPED",target);
            if(conversion=="Identity") {if(!Same(item.Value,normalized))throw Fault("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",item.Name);}
            else if(conversion=="ScaleToMm") {if(Math.Abs(Number(item.Value,item.Name)*scale-Number(normalized,target))>Tolerance(c,"coordinate_mm"))throw Fault("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",item.Name);}
            else if(conversion=="ArcEndAngleToSweep") {if(!raw.TryGetProperty("StartAngle",out var start))throw Fault("SNAPSHOT_PAYLOAD_INCOMPLETE","raw StartAngle");var sweep=NativeArcSweep(Number(start,"StartAngle"),Number(item.Value,item.Name));if(Math.Abs(sweep-Number(normalized,target))>Tolerance(c,"angle_rad"))throw Fault("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",item.Name);}
            else if(conversion=="HashUtf8") {if(!string.Equals(Hash(Text(item.Value,item.Name,true)),Text(normalized,target),StringComparison.OrdinalIgnoreCase))throw Fault("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",item.Name);}
            else if(conversion is "PointToFrame" or "VectorToFrame") {
                var suffix=item.Name.LastIndexOf('.');if(suffix<0)throw Fault("SNAPSHOT_FIELD_UNMAPPED",item.Name);
                var prefix=item.Name[..suffix];var vector=new[]{"X","Y","Z"}.Select(axis=>Number(raw.GetProperty(prefix+"."+axis),prefix+"."+axis)).ToArray();
                var frame=c.GetProperty("frames").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==entity.GetProperty("frame_id").GetString());
                if(conversion=="PointToFrame")vector=vector.Zip(Vector(frame.GetProperty("origin_xyz_mm"),"origin"),(v,o)=>v*scale-o).ToArray();
                var component=target.EndsWith("[0]",StringComparison.Ordinal)?"basis_u_xyz":target.EndsWith("[1]",StringComparison.Ordinal)?"basis_v_xyz":target.EndsWith("[2]",StringComparison.Ordinal)?"normal_xyz":throw Fault("SNAPSHOT_FIELD_UNMAPPED",target);
                if(Math.Abs(Dot(vector,Vector(frame.GetProperty(component),component))-Number(normalized,target))>Tolerance(c,conversion=="PointToFrame"?"coordinate_mm":"dimensionless"))throw Fault("SNAPSHOT_RAW_NORMALIZATION_MISMATCH",item.Name);
            }
            else throw Fault("SNAPSHOT_FIELD_UNMAPPED",item.Name+":"+conversion);
        }
    }
    private static bool Same(JsonElement a,JsonElement b)
    {
        if(a.ValueKind!=b.ValueKind)return false;
        if(a.ValueKind==JsonValueKind.Object){var x=a.EnumerateObject().ToArray();var y=b.EnumerateObject().ToArray();return x.Length==y.Length&&x.All(p=>b.TryGetProperty(p.Name,out var v)&&Same(p.Value,v));}
        if(a.ValueKind==JsonValueKind.Array){var x=a.EnumerateArray().ToArray();var y=b.EnumerateArray().ToArray();return x.Length==y.Length&&x.Zip(y,Same).All(v=>v);}
        return a.ValueKind==JsonValueKind.Number?a.GetDouble()==b.GetDouble():a.GetRawText()==b.GetRawText();
    }
    private static double Limit(string path,JsonElement c)=>Tolerance(c,path.Contains("angle",StringComparison.Ordinal)||path.Contains("rotation",StringComparison.Ordinal)||path.Contains("sweep",StringComparison.Ordinal)?"angle_rad":path.Contains("normal",StringComparison.Ordinal)||path.Contains("basis_",StringComparison.Ordinal)||path.Contains("scale",StringComparison.Ordinal)||path.Contains("factor",StringComparison.Ordinal)||path.Contains("bulge",StringComparison.Ordinal)?"dimensionless":"coordinate_mm");
    private static ImmutableArray<SemanticFieldDifference> Deltas(JsonElement e,JsonElement a,JsonElement c,string prefix="")
    {
        var left=Leaves(e,prefix);var right=Leaves(a,prefix);var result=ImmutableArray.CreateBuilder<SemanticFieldDifference>();
        foreach(var key in left.Keys.Union(right.Keys).Order(StringComparer.Ordinal)) {var le=left.TryGetValue(key,out var l);var re=right.TryGetValue(key,out var r);double? limit=le&&re&&l.ValueKind==JsonValueKind.Number&&r.ValueKind==JsonValueKind.Number?Limit(key,c):null;
            if(!le||!re||(limit.HasValue?Math.Abs(l.GetDouble()-r.GetDouble())>limit.Value:!Same(l,r)))result.Add(new(key,le?l.GetRawText():null,re?r.GetRawText():null,limit));}
        return result.ToImmutable();
    }
    private static SemanticEntity EvidenceEntity(JsonElement e)
    {
        var leaves=Leaves(e);return new(e.GetProperty("semantic_id").GetString()!,e.GetProperty("kind").GetString()!,null,null,
            leaves.Where(p=>p.Value.ValueKind!=JsonValueKind.Number).ToDictionary(p=>p.Key,p=>p.Value.GetRawText()),
            leaves.Where(p=>p.Value.ValueKind==JsonValueKind.Number).ToDictionary(p=>p.Key,p=>p.Value.GetDouble()));
    }
    public static DiffReport Compare(CanonicalPhysicalSnapshot expected,CanonicalPhysicalSnapshot actual)
    {
        ArgumentNullException.ThrowIfNull(expected);ArgumentNullException.ThrowIfNull(actual);
        if(expected.ContractHash!=actual.ContractHash)throw Fault("SNAPSHOT_EXPECTED_CONTRACT_HASH","both captures require same locked contract");
        var c=expected.Contract;var left=expected.Root.GetProperty("entities").EnumerateArray().ToDictionary(e=>e.GetProperty("semantic_id").GetString()!,StringComparer.Ordinal);var right=actual.Root.GetProperty("entities").EnumerateArray().ToDictionary(e=>e.GetProperty("semantic_id").GetString()!,StringComparer.Ordinal);
        var changed=ImmutableArray.CreateBuilder<SemanticChange>();
        foreach(var id in left.Keys.Intersect(right.Keys).Order(StringComparer.Ordinal)) {
            var e=left[id];var a=right[id];var fields=Deltas(e,a,c).ToBuilder();
            var ef=expected.Root.GetProperty("frames").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==e.GetProperty("frame_id").GetString());var af=actual.Root.GetProperty("frames").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==a.GetProperty("frame_id").GetString());
            fields.AddRange(Deltas(ef,af,c,"frame"));if(fields.Count==0)continue;
            var deltas=fields.ToImmutable();var kind=e.GetProperty("kind").GetString();DifferenceNature nature;
            if(deltas.Any(f=>f.Field is "kind" or "native_kind" or "space" or "frame_id"||f.Field.StartsWith("frame.",StringComparison.Ordinal)))nature=DifferenceNature.CoordinateBasisMismatch;
            else if(deltas.Any(f=>f.Field.StartsWith("physical_payload.",StringComparison.Ordinal)))nature=kind is "Text" or "MText"||deltas.Any(f=>f.Field.StartsWith("physical_payload.",StringComparison.Ordinal)&&!f.Tolerance.HasValue)?DifferenceNature.SemanticMismatch:DifferenceNature.NumericalMismatch;
            else if(kind is "Text" or "MText") {
                var placement=deltas.Where(f=>f.Field.StartsWith("placement.",StringComparison.Ordinal)).ToArray();
                if(placement.Length==0)nature=DifferenceNature.MetadataMismatch;
                else if(placement.All(f=>f.Field.StartsWith("placement.alignment.",StringComparison.Ordinal))&&kind=="Text")nature=DifferenceNature.MetadataMismatch;
                else if(placement.Any(f=>!f.Field.StartsWith("placement.anchor.",StringComparison.Ordinal)&&!f.Field.StartsWith("placement.alignment.",StringComparison.Ordinal)))nature=DifferenceNature.SemanticMismatch;
                else {
                    var translation=true; if(kind=="Text"&&placement.Any(f=>f.Field.StartsWith("placement.alignment.",StringComparison.Ordinal))) {
                        var ep=e.GetProperty("placement");var ap=a.GetProperty("placement");var ed=Vector(ep.GetProperty("anchor").GetProperty("xyz_mm"),"anchor");var ad=Vector(ap.GetProperty("anchor").GetProperty("xyz_mm"),"anchor");var ea=Vector(ep.GetProperty("alignment").GetProperty("xyz_mm"),"alignment");var aa=Vector(ap.GetProperty("alignment").GetProperty("xyz_mm"),"alignment");translation=ed.Zip(ad,(x,y)=>y-x).Zip(ea.Zip(aa,(x,y)=>y-x),(x,y)=>Math.Abs(x-y)).All(d=>d<=Tolerance(c,"coordinate_mm"));}
                    nature=!translation?DifferenceNature.SemanticMismatch:e.GetProperty("space").GetString()=="Paper"?DifferenceNature.LayoutOnly:DifferenceNature.NumericalMismatch;
                }
            }else nature=deltas.Any(f=>f.Field.StartsWith("placement.",StringComparison.Ordinal))?DifferenceNature.NumericalMismatch:DifferenceNature.MetadataMismatch;
            changed.Add(new(id,null,null,deltas,nature));
        }
        var audit=Deltas(expected.Root,actual.Root,c).Where(f=>!f.Field.StartsWith("entities[",StringComparison.Ordinal)).ToImmutableArray();
        return new(left.Keys.Except(right.Keys).Select(id=>EvidenceEntity(left[id])).ToImmutableArray(),right.Keys.Except(left.Keys).Select(id=>EvidenceEntity(right[id])).ToImmutableArray(),changed.ToImmutable(),audit);
    }
}
