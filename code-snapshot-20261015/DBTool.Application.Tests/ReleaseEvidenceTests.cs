using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using DBTool.Application.Release;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class ReleaseEvidenceTests
{
    [Theory]
    [InlineData("Visual", ReleaseReadiness.ReadyForHumanReview)]
    [InlineData("HostUndo", ReleaseReadiness.Blocked)]
    [InlineData("ExcelRecalculation", ReleaseReadiness.Blocked)]
    [InlineData("Template", ReleaseReadiness.Blocked)]
    [InlineData("Profile", ReleaseReadiness.Blocked)]
    [InlineData("Unsupported", ReleaseReadiness.Blocked)]
    [InlineData("Hash", ReleaseReadiness.Blocked)]
    [InlineData("Stale", ReleaseReadiness.Blocked)]
    [InlineData("Version", ReleaseReadiness.Blocked)]
    public void RequiredMissingOrInvalidEvidenceCannotRelease(string missing, ReleaseReadiness readiness)
    {
        using var fixture=new EvidenceFixture();
        var evidence=fixture.Evidence;
        evidence=missing switch
        {
            "Visual" => evidence with { VisualApproval=null },
            "HostUndo" => evidence with { HostTestEvidence=evidence.HostTestEvidence.Remove("HostUndo") },
            "ExcelRecalculation" => evidence with { ExcelRecalculationEvidence=null },
            "Template" => evidence with { TemplateVerified=false },
            "Profile" => evidence with { Artifacts3Profiles=evidence.Artifacts3Profiles.Remove("GaThamThuNapRoi") },
            "Unsupported" => evidence with { UnsupportedRequiredFeatures=["Catch"] },
            "Hash" => evidence with { Artifacts=evidence.Artifacts.SetItem(0,evidence.Artifacts[0] with { Sha256=new string('0',64) }) },
            "Stale" => evidence with { CapturedAtUtc=fixture.Now.AddDays(-10) },
            _ => evidence with { ContractVersion="old/v1" }
        };
        var result=ReleaseEvidenceEvaluator.Evaluate(evidence,fixture.Requirements);
        Assert.Equal(readiness,result.Status);
        Assert.NotEmpty(result.MissingGates);
    }

    [Fact] public void CompleteSyntheticSignoffOnlyTestsEvaluatorReadyBranch()
    {
        using var fixture=new EvidenceFixture();
        Assert.Equal(ReleaseReadiness.Ready,ReleaseEvidenceEvaluator.Evaluate(fixture.Evidence,fixture.Requirements).Status);
    }

    [Fact] public void SemanticBooleanWithoutCanonicalComparisonArtifactsCannotRelease()
    {
        using var fixture=new EvidenceFixture();
        var artifact=fixture.Evidence.Artifacts.Single(a=>a.Id=="HostRegression");
        var node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(artifact.Path))!.AsObject();node.Remove("PhysicalComparisons");File.WriteAllText(artifact.Path,node.ToJsonString());
        var evidence=fixture.Evidence with { Artifacts=fixture.Evidence.Artifacts.Select(a=>a.Id==artifact.Id?a with {Sha256=EvidenceFixture.Hash(a.Path)}:a).ToImmutableArray(),VisualApproval=null };
        Assert.Contains(ReleaseEvidenceEvaluator.Evaluate(evidence,fixture.Requirements).MissingGates,r=>r.StartsWith("HostRegression_LOG_INVALID"));
    }

    [Fact] public void HarnessPassStringWithoutStructuredProofIsBlocked()
    {
        using var fixture=new EvidenceFixture();
        var artifact=fixture.Evidence.Artifacts.Single(a=>a.Id=="HostUndo");
        File.WriteAllText(artifact.Path,"{\"ExitStatus\":\"PASS\"}");
        var evidence=fixture.Evidence with { Artifacts=fixture.Evidence.Artifacts.Select(a=>a.Id==artifact.Id ? a with { Sha256=EvidenceFixture.Hash(a.Path) } : a).ToImmutableArray() };
        Assert.Equal(ReleaseReadiness.Blocked,ReleaseEvidenceEvaluator.Evaluate(evidence,fixture.Requirements).Status);
    }

    [Fact] public void ProfilePackageVersionMismatchBlocksEvenWhenArtifactHashMatches()
    {
        using var fixture=new EvidenceFixture();
        var artifact=fixture.Evidence.Artifacts.Single(a=>a.Id=="GaThamLienKhoi-Package");
        File.WriteAllText(artifact.Path,JsonSerializer.Serialize(new { ContractVersion="old",RuleVersion="rule/v1",InputHash=new string('C',64),SourceWorkbookHash=new string('B',64) }));
        var evidence=fixture.Evidence with { Artifacts=fixture.Evidence.Artifacts.Select(a=>a.Id==artifact.Id ? a with { Sha256=EvidenceFixture.Hash(a.Path) } : a).ToImmutableArray(), VisualApproval=null };
        Assert.Contains(ReleaseEvidenceEvaluator.Evaluate(evidence,fixture.Requirements).MissingGates,r=>r.StartsWith("PROFILE_PACKAGE_INVALID"));
    }

    [Fact] public void SyntheticReadyFixtureCannotAuthorizeProductionRelease()
    {
        using var fixture=new EvidenceFixture();
        var decision=ReleaseEvidenceEvaluator.Evaluate(fixture.Evidence,fixture.Requirements with { AllowSyntheticEvidence=false });
        Assert.Equal(ReleaseReadiness.Blocked,decision.Status);
    }

    [Fact] public void HostInputHashMustMatchFrozenPackagesNotOnlyBeValidHex()
    {
        using var fixture=new EvidenceFixture();
        var artifact=fixture.Evidence.Artifacts.Single(a=>a.Id=="HostUndo");
        var json=File.ReadAllText(artifact.Path).Replace(new string('C',64),new string('D',64),StringComparison.Ordinal);
        File.WriteAllText(artifact.Path,json);
        var evidence=fixture.Evidence with { Artifacts=fixture.Evidence.Artifacts.Select(a=>a.Id==artifact.Id ? a with { Sha256=EvidenceFixture.Hash(a.Path) } : a).ToImmutableArray(),VisualApproval=null };
        Assert.Contains(ReleaseEvidenceEvaluator.Evaluate(evidence,fixture.Requirements).MissingGates,r=>r.StartsWith("HostUndo_LOG_INVALID"));
    }

    private sealed class EvidenceFixture : IDisposable
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"dbtool-release-"+Guid.NewGuid().ToString("N"));
        public DateTimeOffset Now { get; }=new(2026,9,30,0,0,0,TimeSpan.Zero);
        public ReleaseRequirements Requirements { get; }
        public ReleaseEvidence Evidence { get; }
        public EvidenceFixture()
        {
            Directory.CreateDirectory(directory);
            var profiles=ImmutableArray.Create("GaThamLienKhoi","GaThamNapRoi","GaThamThuNapRoi");
            var kinds=ImmutableArray.Create("Input","Package","Drawing","Workbook","DWG","PDF","Trace","SemanticDiff","SourceHashes");
            var rules=ImmutableDictionary<string,string>.Empty.Add("rule","v1");
            Requirements=new("build","fingerprint","contract/v1",rules,"Host2026.1.1/net8",Now,TimeSpan.FromDays(2),profiles,kinds,["HostSmoke","HostUndo","HostRender","HostRegression"],true,true);
            var artifacts=ImmutableArray.CreateBuilder<ReleaseArtifact>();
            ReleaseArtifact Add(string id,string content,string kind)
            {
                var path=Path.Combine(directory,id+".json"); File.WriteAllText(path,content);
                var artifact=new ReleaseArtifact(id,path,Hash(path),kind); artifacts.Add(artifact); return artifact;
            }
            foreach(var profile in profiles) foreach(var kind in kinds)
                Add(profile+"-"+kind,kind=="Package" ? JsonSerializer.Serialize(new { ContractVersion="contract/v1",RuleVersion="rule/v1",InputHash=new string('C',64),SourceWorkbookHash=new string('B',64) }) : "synthetic test payload",kind);
            Add("Scenario","<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results><UnitTestResult testName=\"RequiredFixture\" outcome=\"Passed\" /></Results><ResultSummary><Counters total=\"1\" passed=\"1\" failed=\"0\" notExecuted=\"0\" /></ResultSummary></TestRun>","TRX");
            var physical=CanonicalAcceptanceFixtures.Circle();
            foreach(var profile in profiles) {Add(profile+"-PhysicalContract",physical.Contract,"PhysicalContractR04");Add(profile+"-ExpectedPhysical",physical.Snapshot,"PhysicalSnapshotR04");Add(profile+"-ActualPhysical",physical.Snapshot,"PhysicalSnapshotR04");}
            foreach(var stage in Requirements.RequiredHostGates.Append("ExcelRecalculation"))
                Add(stage,JsonSerializer.Serialize(new { Stage=stage,BuildId="build",ContractVersion="contract/v1",RuleVersions=rules,TemplateHash=new string('A',64),InputHashes=new[] {new string('C',64)},SourceHashes=new[] {new string('B',64)},HostProductUpdateRuntime="Host2026.1.1/net8",CapturedAtUtc=Now,Synthetic=true,Executed=true,Verified=true,Proofs=new { UndoRestored=true,RedoRestored=true,RecalculatedSavedReopened=true,SemanticNoDifference=true },PhysicalComparisons=profiles.Select(p=>new{Profile=p,ExpectedArtifactId=p+"-ExpectedPhysical",ActualArtifactId=p+"-ActualPhysical",ExpectedContractArtifactId=p+"-PhysicalContract"}).ToArray(),Transcript="actual command transcript" }),"HostLog");
            var visual=Add("Visual",JsonSerializer.Serialize(new { Reviewer="Synthetic test reviewer",DateUtc=Now,Decision="Pass",Synthetic=true,Regions=new[]{"MB","AA","BB","Tables"},ArtifactHashes=artifacts.ToDictionary(a=>a.Id,a=>a.Sha256) }),"VisualApproval");
            Evidence=new("build","fingerprint","contract/v1",rules,"Host2026.1.1/net8",new string('A',64),true,ImmutableDictionary<string,string>.Empty.Add("source",new string('B',64)),Now,["Scenario"],Requirements.RequiredHostGates.ToImmutableDictionary(x=>x,x=>x),"ExcelRecalculation",visual.Id,[],[],profiles.ToImmutableDictionary(p=>p,p=>kinds.ToImmutableDictionary(k=>k,k=>p+"-"+k)),artifacts.ToImmutable());
        }
        public static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        public void Dispose()=>Directory.Delete(directory,true);
    }
}
