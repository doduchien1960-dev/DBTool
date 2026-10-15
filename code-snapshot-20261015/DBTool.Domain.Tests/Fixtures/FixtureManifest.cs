using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DBTool.Domain.Tests.Fixtures;

public sealed record FixtureManifest
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    [JsonConstructor]
    public FixtureManifest(string schemaVersion, IReadOnlyList<FixtureEntry> entries)
    {
        SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
        Entries = Array.AsReadOnly((entries ?? throw new ArgumentNullException(nameof(entries))).ToArray());
    }

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<FixtureEntry> Entries { get; }

    public static FixtureManifest Load(string? manifestPath = null)
    {
        var resolvedPath = manifestPath ?? ResolveWorkspacePath("DBTool/Tests/Fixtures/manifest.json");
        var json = File.ReadAllText(resolvedPath);
        return JsonSerializer.Deserialize<FixtureManifest>(json, SerializerOptions)
            ?? throw new InvalidDataException($"Fixture manifest is empty: {resolvedPath}");
    }

    public static string ResolveWorkspacePath(string relativePath)
    {
        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var configuredRoot = Environment.GetEnvironmentVariable("DBTOOL_TEST_ASSETS_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var configuredCandidate = Path.Combine(configuredRoot, normalizedRelativePath);
            if (File.Exists(configuredCandidate))
                return configuredCandidate;
            throw new FileNotFoundException($"Configured fixture file was not found: {relativePath}", configuredCandidate);
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, normalizedRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var currentDirectoryCandidate = Path.GetFullPath(normalizedRelativePath, Directory.GetCurrentDirectory());
        if (File.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        throw new FileNotFoundException($"Workspace fixture file was not found: {relativePath}");
    }
}

public sealed record FixtureEntry
{
    [JsonConstructor]
    public FixtureEntry(
        string id,
        string kind,
        string schemaVersion,
        string inputPath,
        string? expectedPath,
        IReadOnlyList<FixtureSourceRef> sourceRefs,
        string ruleVersion,
        IReadOnlyList<string> unknownFields,
        bool comparisonEligible)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
        InputPath = inputPath ?? throw new ArgumentNullException(nameof(inputPath));
        ExpectedPath = expectedPath;
        SourceRefs = Array.AsReadOnly((sourceRefs ?? throw new ArgumentNullException(nameof(sourceRefs))).ToArray());
        RuleVersion = ruleVersion ?? throw new ArgumentNullException(nameof(ruleVersion));
        UnknownFields = Array.AsReadOnly((unknownFields ?? throw new ArgumentNullException(nameof(unknownFields))).ToArray());
        ComparisonEligible = comparisonEligible;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; }

    [JsonPropertyName("inputPath")]
    public string InputPath { get; }

    [JsonPropertyName("expectedPath")]
    public string? ExpectedPath { get; }

    [JsonPropertyName("sourceRefs")]
    public IReadOnlyList<FixtureSourceRef> SourceRefs { get; }

    [JsonPropertyName("ruleVersion")]
    public string RuleVersion { get; }

    [JsonPropertyName("unknownFields")]
    public IReadOnlyList<string> UnknownFields { get; }

    [JsonPropertyName("comparisonEligible")]
    public bool ComparisonEligible { get; }
}

public sealed record FixtureSourceRef
{
    [JsonConstructor]
    public FixtureSourceRef(
        string sourceKind,
        string relativePath,
        string sha256,
        string? sheetName,
        string? cellAddress,
        int? pdfPage,
        string rawText)
    {
        SourceKind = sourceKind ?? throw new ArgumentNullException(nameof(sourceKind));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
        SheetName = sheetName;
        CellAddress = cellAddress;
        PdfPage = pdfPage;
        RawText = rawText ?? throw new ArgumentNullException(nameof(rawText));
    }

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; }

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; }

    [JsonPropertyName("sheetName")]
    public string? SheetName { get; }

    [JsonPropertyName("cellAddress")]
    public string? CellAddress { get; }

    [JsonPropertyName("pdfPage")]
    public int? PdfPage { get; }

    [JsonPropertyName("rawText")]
    public string RawText { get; }
}
