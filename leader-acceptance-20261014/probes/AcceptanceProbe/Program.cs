using System.Reflection;
using System.Text.Json;
using DBTool.Application;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Drawing;

// Diagnostic counterexamples only. Values below are synthetic negative inputs,
// never accepted engineering data, PDF baselines, or product defaults.
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var factory = typeof(DBTool.Application.Tests.W13StageAdapterTests)
    .GetMethod("CreateMockS9State", BindingFlags.Static | BindingFlags.NonPublic)!;
var s9 = (S9StateV2)factory.Invoke(null, null)!;
var request = Clone(s9.request, new() { ["target_dwg_path"] = Path.Combine(output,"never-written.dwg") });
var context = Clone(s9.context, new() { ["artifact_directory"] = output, ["row_index"] = 4 });
s9 = Clone(s9, new() { ["request"] = request, ["context"] = context });
var results = new List<object>();

var emptyRow = Make<RawRowV2>(new() { ["row_index"] = 4, ["name_raw"] = "NegativeMissingData", ["cells"] = Array.Empty<RawCellV2>() });
var raw = Clone(s9.raw, new() { ["rows"] = new[] { emptyRow }, ["profile"] = "GaThamLienKhoi", ["config"] = Array.Empty<RawCellV2>() });
var s1 = new S1StateV2(context, request, s9.assets, s9.rules, raw);
var geometry = new GeometryStageAdapter();
try {
    var s2 = geometry.Execute(s1);
    results.Add(new { id="P-MISSING-INPUT", expected="StageFault for missing required fields and source bindings", actual="S2StateV2 returned", s2.effective.clear_x_mm, s2.effective.clear_y_mm, s2.effective.clear_h_mm, members=s2.effective.members.Length, sources=s2.effective.source_ids.Length, verdict="CounterexampleObserved" });
    var a = geometry.Execute(s2);
    var effectiveB = Clone(s2.effective,new() { ["clear_x_mm"] = s2.effective.clear_x_mm + 137 });
    var b = geometry.Execute(Clone(s2,new() { ["effective"] = effectiveB }));
    results.Add(new { id="P-GEOMETRY-HASH", expected="Different physical hashes for different X dimensions", widthA=s2.effective.clear_x_mm, widthB=effectiveB.clear_x_mm, hashA=a.geometry.physical_hash, hashB=b.geometry.physical_hash, actual=a.geometry.physical_hash==b.geometry.physical_hash?"Hashes equal":"Hashes differ", verdict=a.geometry.physical_hash==b.geometry.physical_hash?"CounterexampleObserved":"NotReproduced" });
    var s4 = new FullRebarStage().Execute(a);
    var s5 = new DrawingStageAdapter().Execute(s4);
    var s6 = new DrawingStageAdapter().Execute(s5);
    results.Add(new { id="P-STRUCTURE-VIEWS", expected="Required body/base/wall/roof structure views even with no catch", actual=s6.projection.views.Select(v=>new {v.role,v.required}).ToArray(), requiredViewCount=s6.projection.views.Count(v=>v.required), verdict=s6.projection.views.All(v=>!v.required)?"CounterexampleObserved":"NotReproduced" });
} catch(Exception e) { results.Add(new { id="P-GEOMETRY-PROBE-EXCEPTION", type=e.GetType().FullName, message=e.Message, verdict="ProbeError" }); }

try {
    if(File.Exists(request.target_dwg_path))throw new InvalidOperationException("Probe target must not preexist");
    var s10 = new S10StageAdapter().Execute(s9);
    results.Add(new { id="P-DEFAULT-NATIVE", expected="Missing native port must fail; cannot declare readback Passed", actual=s10.native.readback_status, s10.native.write_status, fileExists=File.Exists(request.target_dwg_path), savedPath=s10.native.saved_dwg_path, verdict=s10.native.readback_status=="Passed"&&!File.Exists(request.target_dwg_path)?"CounterexampleObserved":"NotReproduced" });
} catch(Exception e) { results.Add(new { id="P-DEFAULT-NATIVE-EXCEPTION", type=e.GetType().FullName,message=e.Message,verdict="ProbeError" }); }

try {
    var empty=SemanticDiff.ComparePhysicalManifests("{}","{}");
    results.Add(new {id="P-EMPTY-PHYSICAL-MANIFEST",expected="Missing physical manifest schema must reject, never NoDifference",actual=empty.NoDifference,verdict=empty.NoDifference?"CounterexampleObserved":"NotReproduced"});
    string Physical(double x)=>JsonSerializer.Serialize(new {Entities=new[]{new {SemanticId="PhysicalLine",Type="Line",Owner="MH01",Fields=new Dictionary<string,string>(),Numbers=new Dictionary<string,double>{{"Start.X",x}}}}});
    var moved=SemanticDiff.ComparePhysicalManifests(Physical(0),Physical(10));
    results.Add(new {id="P-PHYSICAL-COORDINATE-CLASSIFICATION",expected="Physical world endpoint moved 10 mm must be numerical/geometry difference",actual=moved.Changed.Select(x=>x.Nature.ToString()).ToArray(),moved.IsLayoutOnly,verdict=moved.IsLayoutOnly?"CounterexampleObserved":"NotReproduced"});
} catch(Exception e) { results.Add(new {id="P-SEMANTIC-EXCEPTION",type=e.GetType().FullName,message=e.Message,verdict="ProbeError"}); }

File.WriteAllText(Path.Combine(output,"counterexamples.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true}));

static T Clone<T>(T source,Dictionary<string,object?> replacements) where T:class {
    var c=typeof(T).GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
    return (T)c.Invoke(c.GetParameters().Select(p=>replacements.TryGetValue(p.Name!,out var v)?v:typeof(T).GetProperty(p.Name!)!.GetValue(source)).ToArray());
}
static T Make<T>(Dictionary<string,object?> values) {
    var c=typeof(T).GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
    return (T)c.Invoke(c.GetParameters().Select(p=>values.TryGetValue(p.Name!,out var v)?v:p.ParameterType.IsArray?Array.CreateInstance(p.ParameterType.GetElementType()!,0):p.ParameterType==typeof(string)?"":p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray());
}
