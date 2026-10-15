using DBTool.Application.Host;
using DBTool.Domain;
using Xunit;

namespace DBTool.Application.Tests;

public class HostRuntimeRequirementTests
{
    private static readonly CompatibilityMatrix Matrix = new(new Dictionary<string, int>
    {
        ["AutoCAD 2026 through Update 1.1"] = 8,
        ["AutoCAD 2026 Update 1.2 and later"] = 10
    });

    [Theory]
    [InlineData("AutoCAD 2026 through Update 1.1", new[] { 8 }, PreflightStatus.Compatible, 8)]
    [InlineData("AutoCAD 2026 Update 1.2 and later", new[] { 8 }, PreflightStatus.MissingRuntime, 10)]
    [InlineData("AutoCAD 2026 through Update 1.1", new[] { 8, 10 }, PreflightStatus.Compatible, 8)]
    [InlineData(null, new[] { 8, 10 }, PreflightStatus.UnknownUpdate, null)]
    public void EvaluatesDocumentedRequirementOnly(string? update, int[] installed, PreflightStatus expected, int? required)
    {
        var host = new HostDescriptor("AutoCAD 2026", "25.1", update, "25.1.164.0.0", null, "synthetic");
        var result = HostRuntimeRequirement.Evaluate(host, new RuntimeInventory(installed), Matrix);
        Assert.Equal(expected, result.Status);
        Assert.Equal(required, result.RequiredRuntimeMajor);
        Assert.Equal(expected == PreflightStatus.Compatible, result.Diagnostics.Count == 0);
    }

    [Fact]
    public void CompatibleSnapshotProducesReadyReport()
    {
        var result = HostRuntimeRequirement.Evaluate(Snapshot("AutoCAD 2026 through Update 1.1", 8, [8], templatePresent: true), Requirement());

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(PreflightStatus.Compatible, result.Value!.Status);
        Assert.Equal("net8.0-windows", result.Value.TargetFramework);
    }

    [Fact]
    public void RequiredTenWithOnlyEightIsMissingRuntimeAndBlocks()
    {
        var result = HostRuntimeRequirement.Evaluate(Snapshot("AutoCAD 2026 Update 1.2 and later", 8, [8], templatePresent: true), Requirement());

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "HOST_RUNTIME_MISSING" && d.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void UnknownUpdateIsNotReadyAndBlocksBeforeNetload()
    {
        var result = HostRuntimeRequirement.Evaluate(Snapshot(null, null, [8, 10], templatePresent: true), Requirement());

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "HOST_UPDATE_UNKNOWN" && d.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void MissingTemplateResourceBlocksPreflight()
    {
        var result = HostRuntimeRequirement.Evaluate(Snapshot("AutoCAD 2026 through Update 1.1", 8, [8], templatePresent: false), Requirement());

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "TEMPLATE_MISSING" && d.Severity == DiagnosticSeverity.Block);
    }

    private static HostSnapshot Snapshot(string? update, int? runtimeConfigMajor, int[] installed, bool templatePresent) =>
        new("Autodesk Civil 3D 2026 - English", "25.1", "acad.exe", "R25.1.164.0.0", update,
            runtimeConfigMajor, installed,
            new Dictionary<string, string> { ["acmgd.dll"] = "25.1.164.0.0", ["acdbmgd.dll"] = "25.1.164.0.0", ["accoremgd.dll"] = "25.1.164.0.0" },
            new Dictionary<string, bool> { ["template"] = templatePresent }, "synthetic");

    private static HostRequirement Requirement() =>
        new("Autodesk Civil 3D 2026 - English", "25.1",
            new Dictionary<string, int>
            {
                ["AutoCAD 2026 through Update 1.1"] = 8,
                ["AutoCAD 2026 Update 1.2 and later"] = 10
            },
            "25.1.164.0.0", ["acmgd.dll", "acdbmgd.dll", "accoremgd.dll"], ["template"]);
}
