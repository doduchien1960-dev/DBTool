using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace DBTool.Domain.Tests.Fixtures;

public sealed class FixtureManifestTests
{
    [Fact]
    public void ResolveWorkspacePath_UsesConfiguredAssetRoot()
    {
        var previous = Environment.GetEnvironmentVariable("DBTOOL_TEST_ASSETS_ROOT");
        var root = Path.Combine(Path.GetTempPath(), $"dbtool-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var fixture = Path.Combine(root, "controlled-fixture.json");
        File.WriteAllText(fixture, "{}");
        try
        {
            Environment.SetEnvironmentVariable("DBTOOL_TEST_ASSETS_ROOT", root);
            Assert.Equal(fixture, FixtureManifest.ResolveWorkspacePath("controlled-fixture.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBTOOL_TEST_ASSETS_ROOT", previous);
            File.Delete(fixture);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void Manifest_HasDistinctFourReferenceInputs()
    {
        var manifest = FixtureManifest.Load();
        var referenceInputs = manifest.Entries
            .Where(entry => entry.Kind == "SourceSnapshot")
            .ToArray();

        Assert.Equal(4, referenceInputs.Length);
        Assert.Equal(4, referenceInputs.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, referenceInputs.Select(entry => entry.InputPath).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(referenceInputs, entry => entry.InputPath.Contains("1.0.HO GA", StringComparison.Ordinal));
        Assert.Contains(referenceInputs, entry => entry.InputPath.Contains("2.0.HO GA", StringComparison.Ordinal));
        Assert.Contains(referenceInputs, entry => entry.InputPath.Contains("3.0.HO GA", StringComparison.Ordinal));
        Assert.Contains(referenceInputs, entry => entry.InputPath.EndsWith("Model-1.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void ApprovedCurrentP4RevisionIs300()
    {
        var manifest = FixtureManifest.Load();
        var source = manifest.Entries.Single(entry => entry.Id == "SRC-VHG-EXCEL-GA-THAM-LIEN-KHOI-G400-1");
        var p4 = source.SourceRefs.Single(sourceRef => sourceRef.CellAddress == "P4");

        Assert.Equal("WorkbookCell", p4.SourceKind);
        Assert.Equal("300", p4.RawText);
        Assert.DoesNotContain("GaThamLienKhoi.P4", source.UnknownFields);

    }

    [Fact]
    [Trait("Category", "SourceIntegration")]
    public void AllReferencedSourceHashesMatch()
    {
        var manifest = FixtureManifest.Load();

        foreach (var sourceRef in manifest.Entries.SelectMany(entry => entry.SourceRefs))
        {
            var path = FixtureManifest.ResolveWorkspacePath(sourceRef.RelativePath);
            Assert.True(File.Exists(path), $"Missing source: {sourceRef.RelativePath}");

            using var stream = File.OpenRead(path);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream));
            Assert.Equal(sourceRef.Sha256, actualHash, ignoreCase: true);
        }
    }

    [Fact]
    public void SyntheticFixture_Has48BarsAnd74620Mm()
    {
        var manifest = FixtureManifest.Load();
        var entry = manifest.Entries.Single(item => item.Kind == "SyntheticAnalytic");
        using var expected = JsonDocument.Parse(File.ReadAllText(
            FixtureManifest.ResolveWorkspacePath(entry.ExpectedPath!)));
        var root = expected.RootElement;

        Assert.Equal(48, root.GetProperty("bars").GetArrayLength());
        Assert.Equal(74620d, root.GetProperty("steelSchedule").GetProperty("totalLengthMm").GetDouble(), 1e-6);
    }

    [Fact]
    public void ExpectedMass_Is46_06172839506173()
    {
        var manifest = FixtureManifest.Load();
        var entry = manifest.Entries.Single(item => item.Kind == "SyntheticAnalytic");
        using var expected = JsonDocument.Parse(File.ReadAllText(
            FixtureManifest.ResolveWorkspacePath(entry.ExpectedPath!)));
        var mass = expected.RootElement.GetProperty("steelSchedule").GetProperty("totalMassKg").GetDouble();

        Assert.Equal(46.06172839506173d, mass, 1e-9);
    }

    [Fact]
    [Trait("Category", "SourceIntegration")]
    public void MutatingFixtureCopy_DoesNotChangeSourceHash()
    {
        var manifest = FixtureManifest.Load();
        var sourceRef = manifest.Entries
            .Single(item => item.Kind == "SyntheticAnalytic")
            .SourceRefs
            .Single(item => item.SourceKind == "SyntheticFixture");
        var sourcePath = FixtureManifest.ResolveWorkspacePath(sourceRef.RelativePath);
        var copyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            File.Copy(sourcePath, copyPath);
            File.AppendAllText(copyPath, Environment.NewLine);

            using var original = File.OpenRead(sourcePath);
            using var mutatedCopy = File.OpenRead(copyPath);
            var originalHash = Convert.ToHexString(SHA256.HashData(original));
            var mutatedHash = Convert.ToHexString(SHA256.HashData(mutatedCopy));

            Assert.Equal(sourceRef.Sha256, originalHash, ignoreCase: true);
            Assert.False(string.Equals(originalHash, mutatedHash, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(copyPath))
            {
                File.Delete(copyPath);
            }
        }
    }
}
