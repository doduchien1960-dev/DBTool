using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>
/// Record of an opening projection in a specific drawing view.
/// </summary>
public sealed record OpeningViewEntry
{
    [JsonConstructor]
    public OpeningViewEntry(
        string ViewId,
        string VisibleMode,
        string TransformDescription,
        ImmutableArray<string> LocalContourIds,
        ImmutableArray<string> DimIds)
    {
        this.ViewId = ViewId;
        this.VisibleMode = VisibleMode;
        this.TransformDescription = TransformDescription;
        this.LocalContourIds = LocalContourIds.IsDefault ? [] : LocalContourIds;
        this.DimIds = DimIds.IsDefault ? [] : DimIds;
    }

    public OpeningViewEntry(
        string ViewId,
        string VisibleMode,
        string TransformDescription,
        IEnumerable<string> LocalContourIds,
        IEnumerable<string> DimIds)
        : this(ViewId, VisibleMode, TransformDescription, LocalContourIds.ToImmutableArray(), DimIds.ToImmutableArray())
    {
    }

    public string ViewId { get; }
    public string VisibleMode { get; }
    public string TransformDescription { get; }
    public ImmutableArray<string> LocalContourIds { get; }
    public ImmutableArray<string> DimIds { get; }
}

/// <summary>
/// Record representing an Opening and its cross-view representations.
/// </summary>
public sealed record OpeningViewRecord
{
    [JsonConstructor]
    public OpeningViewRecord(
        string OpeningId,
        string OwnerId,
        string Kind,
        string SourceFrame,
        Point3Mm WorldCenter,
        string InnerContourSignature,
        ImmutableArray<OpeningViewEntry> Views)
    {
        this.OpeningId = OpeningId;
        this.OwnerId = OwnerId;
        this.Kind = Kind;
        this.SourceFrame = SourceFrame;
        this.WorldCenter = WorldCenter;
        this.InnerContourSignature = InnerContourSignature;
        this.Views = Views.IsDefault ? [] : Views;
    }

    public OpeningViewRecord(
        string OpeningId,
        string OwnerId,
        string Kind,
        string SourceFrame,
        Point3Mm WorldCenter,
        string InnerContourSignature,
        IEnumerable<OpeningViewEntry> Views)
        : this(OpeningId, OwnerId, Kind, SourceFrame, WorldCenter, InnerContourSignature, Views.ToImmutableArray())
    {
    }

    public string OpeningId { get; }
    public string OwnerId { get; }
    public string Kind { get; }
    public string SourceFrame { get; }
    public Point3Mm WorldCenter { get; }
    public string InnerContourSignature { get; }
    public ImmutableArray<OpeningViewEntry> Views { get; }
}

/// <summary>
/// Complete opening-view map tracking openings across all drawing views.
/// </summary>
public sealed record OpeningViewMap
{
    [JsonConstructor]
    public OpeningViewMap(string RuleVersion, ImmutableArray<OpeningViewRecord> Openings)
    {
        this.RuleVersion = RuleVersion;
        this.Openings = Openings.IsDefault ? [] : Openings;
    }

    public OpeningViewMap(string RuleVersion, IEnumerable<OpeningViewRecord> Openings)
        : this(RuleVersion, Openings.ToImmutableArray())
    {
    }

    public string RuleVersion { get; }
    public ImmutableArray<OpeningViewRecord> Openings { get; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static OpeningViewMap FromJson(string json) =>
        JsonSerializer.Deserialize<OpeningViewMap>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize OpeningViewMap.");
}

/// <summary>
/// Builder and exporter for opening-view map.
/// </summary>
public static class OpeningViewMapExporter
{
    public const string DefaultRuleVersion = "DBTOOL-FINAL-SPEC-20261012-v1";

    public static OpeningViewMap Build(GeometryModel geometry, DrawingPackage package)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(package);

        var records = new List<OpeningViewRecord>();

        foreach (var opening in geometry.Openings)
        {
            var viewEntries = new List<OpeningViewEntry>();

            foreach (var view in package.Views)
            {
                var relatedPrims = package.Primitives
                    .Where(p => p.ViewId == view.Id &&
                                (p.SemanticId.Contains(opening.Id, StringComparison.OrdinalIgnoreCase) ||
                                 p.OccurrenceId == opening.Id ||
                                 p.ComponentId == opening.Id ||
                                 (p.ComponentId == opening.OwnerId && p.Role.Contains("Opening", StringComparison.OrdinalIgnoreCase))))
                    .ToArray();

                var contourPrims = relatedPrims
                    .Where(p => p.Role.StartsWith("Geometry/", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.SemanticId)
                    .Distinct(StringComparer.Ordinal)
                    .ToImmutableArray();

                var dimPrims = relatedPrims
                    .Where(p => p.Role.StartsWith("Dimension/", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.SemanticId)
                    .Distinct(StringComparer.Ordinal)
                    .ToImmutableArray();

                string visibleMode;
                if (contourPrims.Length > 0)
                {
                    bool isHidden = relatedPrims.Any(p => p.Role.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
                    visibleMode = isHidden ? "Hidden" : "Visible";
                }
                else
                {
                    visibleMode = "NotCut";
                }

                string transformDesc = $"View({view.Id}) <= Frame({opening.Frame.Origin})";

                viewEntries.Add(new OpeningViewEntry(
                    ViewId: view.Id,
                    VisibleMode: visibleMode,
                    TransformDescription: transformDesc,
                    LocalContourIds: contourPrims,
                    DimIds: dimPrims
                ));
            }

            string innerSig = opening.Inner switch
            {
                CircleProfile c => $"Circle(R={c.RadiusMm}, Center={c.Center})",
                RectangleProfile r => $"Rectangle(W={r.Rectangle.MaxX - r.Rectangle.MinX}, H={r.Rectangle.MaxY - r.Rectangle.MinY})",
                MultiCellProfile m => $"MultiCell(Cells={m.Cells.Length})",
                _ => opening.Inner.ToString() ?? "Unknown"
            };

            records.Add(new OpeningViewRecord(
                OpeningId: opening.Id,
                OwnerId: opening.OwnerId,
                Kind: opening.Kind.ToString(),
                SourceFrame: $"Origin={opening.Frame.Origin}, U={opening.Frame.U}, V={opening.Frame.V}, N={opening.Frame.N}",
                WorldCenter: opening.Frame.Origin,
                InnerContourSignature: innerSig,
                Views: viewEntries.ToImmutableArray()
            ));
        }

        return new OpeningViewMap(package.RuleVersion, records.ToImmutableArray());
    }

    public static void ExportJson(GeometryModel geometry, DrawingPackage package, string outputPath)
    {
        var map = Build(geometry, package);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, map.ToJson());
    }
}
