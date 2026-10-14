using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace DBTool.Application.Release;

public enum ReleaseReadiness { Ready, Blocked, ReadyForHumanReview }
public enum EvidenceVerdict { Passed, Failed, NotRun, Partial, NeedsRule, Blocked }
public sealed record ReleaseArtifact(string Id, string Path, string Sha256, string Kind);
public sealed record ReleaseDecision(ReleaseReadiness Status, ImmutableArray<string> MissingGates);
public sealed record ReleaseRequirements(string BuildId, string Fingerprint, string ContractVersion,
    ImmutableDictionary<string,string> RuleVersions, string HostProductUpdateRuntime, DateTimeOffset NowUtc, TimeSpan MaxAge,
    ImmutableArray<string> RequiredProfiles, ImmutableArray<string> RequiredArtifactKinds, ImmutableArray<string> RequiredHostGates,
    bool RequireExcelRecalculation, bool AllowSyntheticEvidence = false);
public sealed record StageVerdictRecord(
    string Stage,
    EvidenceVerdict Verdict,
    string? RunId = null,
    string? InputHash = null,
    string? OutputHash = null,
    ImmutableArray<string>? ArtifactHashes = null,
    string? Message = null);
public sealed record RowVerdictRecord(
    int RowIndex,
    string Profile,
    EvidenceVerdict Verdict,
    string? InputHash = null,
    ImmutableArray<string>? ArtifactHashes = null,
    string? Message = null);
public sealed record PageVerdictRecord(
    int PageIndex,
    string PageId,
    EvidenceVerdict Verdict,
    string? FrameId = null,
    string? PdfHash = null,
    string? DwgHash = null,
    ImmutableArray<string>? ReviewedRegions = null,
    string? Message = null);
public sealed record ReleaseEvidence(string BuildId, string Fingerprint, string ContractVersion,
    ImmutableDictionary<string,string> RuleVersions, string HostProductUpdateRuntime, string TemplateHash, bool TemplateVerified,
    ImmutableDictionary<string,string> SourceHashes, DateTimeOffset CapturedAtUtc, ImmutableArray<string> ScenarioEvidence,
    ImmutableDictionary<string,string> HostTestEvidence, string? ExcelRecalculationEvidence, string? VisualApproval,
    ImmutableArray<string> KnownLimitations, ImmutableArray<string> UnsupportedRequiredFeatures,
    ImmutableDictionary<string,ImmutableDictionary<string,string>> Artifacts3Profiles, ImmutableArray<ReleaseArtifact> Artifacts,
    ImmutableArray<StageVerdictRecord>? StageVerdicts = null,
    ImmutableArray<RowVerdictRecord>? RowVerdicts = null,
    ImmutableArray<PageVerdictRecord>? PageVerdicts = null);

public static class ReleaseEvidenceEvaluator
{
    public static ReleaseDecision Evaluate(ReleaseEvidence evidence, ReleaseRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(requirements);
        var missing=new List<string>();
        bool SameRules(IReadOnlyDictionary<string,string> a,IReadOnlyDictionary<string,string> b)=>a.Count==b.Count && a.All(x=>b.TryGetValue(x.Key,out var v)&&v==x.Value);
        bool HashValid(string hash)=>hash.Length==64 && hash.All(Uri.IsHexDigit);
        bool Fresh(DateTimeOffset date)=>date<=requirements.NowUtc && requirements.NowUtc-date<=requirements.MaxAge;
        if(evidence.BuildId!=requirements.BuildId || evidence.Fingerprint!=requirements.Fingerprint) missing.Add("BUILD_FINGERPRINT_MISMATCH");
        if(evidence.ContractVersion!=requirements.ContractVersion || !SameRules(evidence.RuleVersions,requirements.RuleVersions)) missing.Add("PACKAGE_VERSION_MISMATCH");
        if(evidence.HostProductUpdateRuntime!=requirements.HostProductUpdateRuntime) missing.Add("HOST_RUNTIME_MISMATCH");
        if(!Fresh(evidence.CapturedAtUtc)) missing.Add("EVIDENCE_STALE");
        if(!evidence.TemplateVerified || !HashValid(evidence.TemplateHash)) missing.Add("TEMPLATE_UNVERIFIED");
        if(evidence.SourceHashes.Count==0 || evidence.SourceHashes.Values.Any(h=>!HashValid(h))) missing.Add("SOURCE_HASH_MISSING");
        if(!evidence.UnsupportedRequiredFeatures.IsEmpty) missing.Add("REQUIRED_FEATURE_UNSUPPORTED: "+string.Join(",",evidence.UnsupportedRequiredFeatures));
        var artifacts=new Dictionary<string,ReleaseArtifact>(StringComparer.Ordinal);
        var verified=new HashSet<string>(StringComparer.Ordinal);
        foreach(var artifact in evidence.Artifacts)
        {
            if(!artifacts.TryAdd(artifact.Id,artifact)) { missing.Add("ARTIFACT_ID_DUPLICATE: "+artifact.Id); continue; }
            try
            {
                if(!HashValid(artifact.Sha256) || !File.Exists(artifact.Path) || new FileInfo(artifact.Path).Length==0 || !string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifact.Path))),artifact.Sha256,StringComparison.OrdinalIgnoreCase))
                    missing.Add("ARTIFACT_HASH_MISMATCH: "+artifact.Id);
                else verified.Add(artifact.Id);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { missing.Add("ARTIFACT_UNREADABLE: "+artifact.Id); }
        }
        bool Available(string? id)=>id is not null && verified.Contains(id);
        var packageInputHashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var profile in requirements.RequiredProfiles)
        {
            if(!evidence.Artifacts3Profiles.TryGetValue(profile,out var map)) { missing.Add("PROFILE_MISSING: "+profile); continue; }
                foreach(var kind in requirements.RequiredArtifactKinds)
                if(!map.TryGetValue(kind,out var id) || !Available(id) || artifacts[id].Kind!=kind) missing.Add("PROFILE_ARTIFACT_MISSING: "+profile+"/"+kind);
            if(!requirements.AllowSyntheticEvidence)
                foreach(var item in map.Where(x=>Available(x.Value)))
                {
                    try
                    {
                        var bytes=File.ReadAllBytes(artifacts[item.Value].Path);
                        var valid=item.Key switch
                        {
                            "DWG" => bytes.Length>=6 && System.Text.Encoding.ASCII.GetString(bytes,0,4)=="AC10",
                            "PDF" => bytes.Length>=5 && System.Text.Encoding.ASCII.GetString(bytes,0,5)=="%PDF-",
                            "Workbook" => bytes.Length>=4 && bytes[0]==0x50 && bytes[1]==0x4b && bytes[2]==3 && bytes[3]==4,
                            "Input" or "Drawing" or "Trace" or "SemanticDiff" or "SourceHashes" => ValidJson(bytes),
                            _ => true
                        };
                        if(!valid) missing.Add("ARTIFACT_PAYLOAD_INVALID: "+profile+"/"+item.Key);
                    }
                    catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException) { missing.Add("ARTIFACT_PAYLOAD_INVALID: "+item.Value); }
                }
            if(map.TryGetValue("Package",out var packageId) && Available(packageId))
            {
                try
                {
                    using var packageDoc=JsonDocument.Parse(File.ReadAllText(artifacts[packageId].Path)); var package=packageDoc.RootElement;
                    var source=package.GetProperty("SourceWorkbookHash").GetString()!;
                    packageInputHashes.Add(package.GetProperty("InputHash").GetString()!);
                    if(package.GetProperty("ContractVersion").GetString()!=requirements.ContractVersion ||
                        !requirements.RuleVersions.Any(r=>r.Key+"/"+r.Value==package.GetProperty("RuleVersion").GetString()) ||
                        !HashValid(package.GetProperty("InputHash").GetString()!) || !evidence.SourceHashes.Values.Contains(source,StringComparer.OrdinalIgnoreCase) ||
                        (!requirements.AllowSyntheticEvidence && package.GetProperty("Scope").GetString()!="Full")) throw new InvalidOperationException();
                }
                catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { missing.Add("PROFILE_PACKAGE_INVALID: "+profile); }
            }
        }
        if(evidence.ScenarioEvidence.IsEmpty) missing.Add("SCENARIO_EVIDENCE_MISSING");
        foreach(var id in evidence.ScenarioEvidence)
        {
            if(!Available(id)) { missing.Add("SCENARIO_EVIDENCE_MISSING: "+id); continue; }
            try
            {
                var doc=XDocument.Load(artifacts[id].Path); var ns=doc.Root!.Name.Namespace;
                var counters=doc.Descendants(ns+"Counters").Single();
                var total=int.Parse(counters.Attribute("total")!.Value);
                var passed=int.Parse(counters.Attribute("passed")!.Value);
                var results=doc.Descendants(ns+"UnitTestResult").ToArray();
                if(total<=0 || passed!=total || (string?)counters.Attribute("failed")!="0" || (string?)counters.Attribute("notExecuted")!="0" || results.Length!=total || results.Any(r=>(string?)r.Attribute("outcome")!="Passed")) missing.Add("SCENARIOS_NOT_ALL_PASSED: "+id);
            }
            catch(Exception ex) when(ex is IOException or System.Xml.XmlException or InvalidOperationException or FormatException or NullReferenceException) { missing.Add("SCENARIO_LOG_INVALID: "+id); }
        }
        bool CheckLog(string stage,string? id)
        {
            if(!Available(id)) { missing.Add(stage+"_MISSING"); return false; }
            try
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(artifacts[id!].Path)); var log=doc.RootElement;
                bool True(string key)=>log.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.True;
                bool Match(string key,string value)=>log.TryGetProperty(key,out var node)&&node.GetString()==value;
                if(!Match("Stage",stage)||!Match("BuildId",evidence.BuildId)||!Match("ContractVersion",evidence.ContractVersion)||!Match("TemplateHash",evidence.TemplateHash)||!Match("HostProductUpdateRuntime",requirements.HostProductUpdateRuntime)||!True("Executed")||!True("Verified")||!Fresh(log.GetProperty("CapturedAtUtc").GetDateTimeOffset()) || (!requirements.AllowSyntheticEvidence&&True("Synthetic"))) throw new InvalidOperationException("identity/status");
                var rules=log.GetProperty("RuleVersions").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!);
                if(!SameRules(rules,evidence.RuleVersions)) throw new InvalidOperationException("rules");
                var inputHashes=log.GetProperty("InputHashes").EnumerateArray().Select(n=>n.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if(inputHashes.Count==0 || inputHashes.Any(h=>!HashValid(h)) || !inputHashes.SetEquals(packageInputHashes)) throw new InvalidOperationException("inputs");
                var sourceHashes=log.GetProperty("SourceHashes").EnumerateArray().Select(n=>n.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if(!evidence.SourceHashes.Values.All(sourceHashes.Contains)) throw new InvalidOperationException("sources");
                if(string.IsNullOrWhiteSpace(log.GetProperty("Transcript").GetString())) throw new InvalidOperationException("transcript");
                if(!requirements.AllowSyntheticEvidence)
                {
                    var stageHashes=log.GetProperty("ArtifactHashes");
                    foreach(var profile in evidence.Artifacts3Profiles.Values)
                        foreach(var artifactId in profile.Values)
                            if(!Available(artifactId)||!stageHashes.TryGetProperty(artifactId,out var hash)||!string.Equals(hash.GetString(),artifacts[artifactId].Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("stage artifact hash");
                }
                var proofs=log.GetProperty("Proofs");
                bool Proof(string key)=>proofs.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.True;
                if(stage=="HostUndo" && (!Proof("UndoRestored")||!Proof("RedoRestored"))) throw new InvalidOperationException("undo");
                if(stage=="ExcelRecalculation" && !Proof("RecalculatedSavedReopened")) throw new InvalidOperationException("recalc");
                if(stage=="HostRegression" && !Proof("SemanticNoDifference")) throw new InvalidOperationException("semantic diff");
                if(log.TryGetProperty("Verdict", out var verdictProp) && IsNonPassedVerdict(verdictProp.GetString())) throw new InvalidOperationException("verdict not passed: " + verdictProp.GetString());
                if(log.TryGetProperty("Status", out var statusProp) && IsNonPassedVerdict(statusProp.GetString())) throw new InvalidOperationException("status not passed: " + statusProp.GetString());
                if(log.TryGetProperty("StageVerdicts", out var svList) && svList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sv in svList.EnumerateArray())
                        if (sv.TryGetProperty("Verdict", out var svv) && IsNonPassedVerdict(svv.GetString())) throw new InvalidOperationException("stage verdict not passed");
                }
                if(log.TryGetProperty("RowVerdicts", out var rvList) && rvList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rv in rvList.EnumerateArray())
                        if (rv.TryGetProperty("Verdict", out var rvv) && IsNonPassedVerdict(rvv.GetString())) throw new InvalidOperationException("row verdict not passed");
                }
                if(log.TryGetProperty("PageVerdicts", out var pvList) && pvList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pv in pvList.EnumerateArray())
                        if (pv.TryGetProperty("Verdict", out var pvv) && IsNonPassedVerdict(pvv.GetString())) throw new InvalidOperationException("page verdict not passed");
                }
                return true;
            }
            catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { missing.Add(stage+"_LOG_INVALID: "+id); return false; }
        }
        foreach(var stage in requirements.RequiredHostGates) CheckLog(stage,evidence.HostTestEvidence.GetValueOrDefault(stage));
        if(requirements.RequireExcelRecalculation) CheckLog("ExcelRecalculation",evidence.ExcelRecalculationEvidence);
        if(evidence.StageVerdicts.HasValue)
            foreach (var sv in evidence.StageVerdicts.Value)
                if (sv.Verdict != EvidenceVerdict.Passed) missing.Add($"STAGE_VERDICT_NOT_PASSED: {sv.Stage}={sv.Verdict}");
        if(evidence.RowVerdicts.HasValue)
            foreach (var rv in evidence.RowVerdicts.Value)
                if (rv.Verdict != EvidenceVerdict.Passed) missing.Add($"ROW_VERDICT_NOT_PASSED: Row{rv.RowIndex}={rv.Verdict}");
        if(evidence.PageVerdicts.HasValue)
            foreach (var pv in evidence.PageVerdicts.Value)
                if (pv.Verdict != EvidenceVerdict.Passed) missing.Add($"PAGE_VERDICT_NOT_PASSED: {pv.PageId}={pv.Verdict}");
        var visualMissing=!Available(evidence.VisualApproval);
        if(!visualMissing)
        {
            try
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(artifacts[evidence.VisualApproval!].Path)); var visual=doc.RootElement;
                if(string.IsNullOrWhiteSpace(visual.GetProperty("Reviewer").GetString()) || visual.GetProperty("Decision").GetString()!="Pass" || !Fresh(visual.GetProperty("DateUtc").GetDateTimeOffset()) || visual.GetProperty("Regions").GetArrayLength()==0 || (!requirements.AllowSyntheticEvidence && visual.TryGetProperty("Synthetic",out var synthetic)&&synthetic.ValueKind==JsonValueKind.True)) throw new InvalidOperationException();
                if(visual.TryGetProperty("Verdict", out var vProp) && IsNonPassedVerdict(vProp.GetString())) throw new InvalidOperationException("visual verdict");
                var hashes=visual.GetProperty("ArtifactHashes");
                foreach(var artifact in artifacts.Values.Where(a=>a.Id!=evidence.VisualApproval))
                    if(!hashes.TryGetProperty(artifact.Id,out var hash)||!string.Equals(hash.GetString(),artifact.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            }
            catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { missing.Add("VISUAL_APPROVAL_INVALID"); }
        }
        var blocked=missing.Count>0;
        if(visualMissing) missing.Add("VISUAL_APPROVAL_PENDING");
        return new(blocked ? ReleaseReadiness.Blocked : visualMissing ? ReleaseReadiness.ReadyForHumanReview : ReleaseReadiness.Ready,missing.ToImmutableArray());
    }

    private static bool IsNonPassedVerdict(string? verdict) =>
        string.Equals(verdict, "NotRun", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verdict, "Partial", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verdict, "NeedsRule", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verdict, "Blocked", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verdict, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verdict, "PREREQUISITE_NOT_IMPLEMENTED", StringComparison.OrdinalIgnoreCase);

    private static bool ValidJson(byte[] bytes)
    {
        using var document=JsonDocument.Parse(bytes);
        return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
    }
}
