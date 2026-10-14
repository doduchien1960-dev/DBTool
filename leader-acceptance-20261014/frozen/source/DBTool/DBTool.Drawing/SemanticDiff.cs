using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Drawing;

public sealed record SemanticEntity
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SemanticEntity(string semanticId, string type, string? owner, SourceRef? source,
        ImmutableSortedDictionary<string,string> fields, ImmutableSortedDictionary<string,double> numbers, string? rawHandle)
        : this(semanticId,type,owner,source,(IReadOnlyDictionary<string,string>)fields,(IReadOnlyDictionary<string,double>)numbers,rawHandle) { }

    public SemanticEntity(string semanticId, string type, string? owner, SourceRef? source,
        IReadOnlyDictionary<string,string> fields, IReadOnlyDictionary<string,double> numbers, string? rawHandle = null)
    {
        SemanticId = ContractGuard.Text(semanticId, nameof(semanticId));
        Type = ContractGuard.Text(type, nameof(type));
        Owner = owner; Source = source; RawHandle = rawHandle;
        Fields = fields.ToImmutableSortedDictionary(StringComparer.Ordinal);
        Numbers = numbers.ToImmutableSortedDictionary(StringComparer.Ordinal);
        foreach (var number in Numbers.Values) ContractGuard.Finite(number, nameof(numbers));
    }
    public string SemanticId { get; }
    public string Type { get; }
    public string? Owner { get; }
    public SourceRef? Source { get; }
    public ImmutableSortedDictionary<string,string> Fields { get; }
    public ImmutableSortedDictionary<string,double> Numbers { get; }
    public string? RawHandle { get; }
}

public sealed record SemanticSnapshot
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SemanticSnapshot(string documentIdentity, string generationIdentity, ImmutableArray<SemanticEntity> entities)
        : this(documentIdentity,generationIdentity,(IEnumerable<SemanticEntity>)entities) { }

    public SemanticSnapshot(string documentIdentity, string generationIdentity, IEnumerable<SemanticEntity> entities)
    {
        DocumentIdentity = ContractGuard.Text(documentIdentity, nameof(documentIdentity));
        GenerationIdentity = ContractGuard.Text(generationIdentity, nameof(generationIdentity));
        Entities = entities.ToImmutableArray();
        if (Entities.Select(e => e.SemanticId).Distinct(StringComparer.Ordinal).Count() != Entities.Length)
            throw new ArgumentException("Duplicate semantic identity.", nameof(entities));
    }
    public string DocumentIdentity { get; }
    public string GenerationIdentity { get; }
    public ImmutableArray<SemanticEntity> Entities { get; }

    public static SemanticSnapshot FromPhysicalManifest(string documentIdentity, string generationIdentity, string manifestJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        var entities = ImmutableArray.CreateBuilder<SemanticEntity>();
        var root = doc.RootElement;
        var array = root.ValueKind == System.Text.Json.JsonValueKind.Array
            ? root.EnumerateArray()
            : root.TryGetProperty("Entities", out var eNode) && eNode.ValueKind == System.Text.Json.JsonValueKind.Array
                ? eNode.EnumerateArray()
                : root.TryGetProperty("entities", out var eLower) && eLower.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? eLower.EnumerateArray()
                    : Enumerable.Empty<System.Text.Json.JsonElement>();

        foreach (var item in array)
        {
            var id = item.TryGetProperty("SemanticId", out var idProp) ? idProp.GetString()! :
                     item.TryGetProperty("semanticId", out var idPropL) ? idPropL.GetString()! :
                     item.TryGetProperty("Id", out var idProp2) ? idProp2.GetString()! : Guid.NewGuid().ToString("N");
            var type = item.TryGetProperty("Type", out var typeProp) ? typeProp.GetString()! :
                       item.TryGetProperty("type", out var typePropL) ? typePropL.GetString()! : "Unknown";
            var owner = item.TryGetProperty("Owner", out var oProp) ? oProp.GetString() :
                        item.TryGetProperty("owner", out var oPropL) ? oPropL.GetString() : null;
            var handle = item.TryGetProperty("RawHandle", out var hProp) ? hProp.GetString() :
                         item.TryGetProperty("rawHandle", out var hPropL) ? hPropL.GetString() :
                         item.TryGetProperty("Handle", out var hProp2) ? hProp2.GetString() : null;

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (item.TryGetProperty("Fields", out var fNode) && fNode.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in fNode.EnumerateObject())
                    fields[p.Name] = p.Value.GetString() ?? string.Empty;
            }
            else if (item.TryGetProperty("fields", out var fNodeL) && fNodeL.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in fNodeL.EnumerateObject())
                    fields[p.Name] = p.Value.GetString() ?? string.Empty;
            }

            var numbers = new Dictionary<string, double>(StringComparer.Ordinal);
            if (item.TryGetProperty("Numbers", out var nNode) && nNode.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in nNode.EnumerateObject())
                    if (p.Value.TryGetDouble(out var d)) numbers[p.Name] = d;
            }
            else if (item.TryGetProperty("numbers", out var nNodeL) && nNodeL.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in nNodeL.EnumerateObject())
                    if (p.Value.TryGetDouble(out var d)) numbers[p.Name] = d;
            }

            entities.Add(new SemanticEntity(id, type, owner, null, fields, numbers, handle));
        }

        return new SemanticSnapshot(documentIdentity, generationIdentity, entities.ToImmutable());
    }
}
public sealed record SemanticTolerance
{
    public SemanticTolerance(double coordinateMm=1e-6,double rawMassKg=1e-9) { CoordinateMm=ContractGuard.Positive(coordinateMm,nameof(coordinateMm)); RawMassKg=ContractGuard.Positive(rawMassKg,nameof(rawMassKg)); }
    public double CoordinateMm { get; }
    public double RawMassKg { get; }
}
public sealed record SemanticFieldDifference
{
    public SemanticFieldDifference(string field,string? expected,string? actual,double? tolerance) { Field=field; Expected=expected; Actual=actual; Tolerance=tolerance; }
    public string Field { get; }
    public string? Expected { get; }
    public string? Actual { get; }
    public double? Tolerance { get; }
}
public enum DifferenceNature
{
    ContentLoss,
    NumericalMismatch,
    LayoutOnly,
    Rename,
    InputBasis,
    AttributeDifference
}
public sealed record SemanticChange
{
    public SemanticChange(string semanticId,string? owner,SourceRef? source,ImmutableArray<SemanticFieldDifference> fields)
        : this(semanticId, owner, source, fields, ClassifyDifference(fields, source)) { }

    public SemanticChange(string semanticId,string? owner,SourceRef? source,ImmutableArray<SemanticFieldDifference> fields, DifferenceNature nature)
    {
        SemanticId=semanticId; Owner=owner; Source=source; Fields=fields; Nature=nature;
    }

    public string SemanticId { get; }
    public string? Owner { get; }
    public SourceRef? Source { get; }
    public ImmutableArray<SemanticFieldDifference> Fields { get; }
    public DifferenceNature Nature { get; }

    private static DifferenceNature ClassifyDifference(ImmutableArray<SemanticFieldDifference> fields, SourceRef? source)
    {
        if (fields.IsEmpty) return DifferenceNature.AttributeDifference;
        if (fields.All(f => IsLayoutField(f.Field))) return DifferenceNature.LayoutOnly;
        if (fields.All(f => IsRenameField(f.Field))) return DifferenceNature.Rename;
        if (fields.All(f => IsInputBasisField(f.Field))) return DifferenceNature.InputBasis;
        if (fields.Any(f => f.Tolerance.HasValue || IsNumericalField(f.Field))) return DifferenceNature.NumericalMismatch;
        return DifferenceNature.AttributeDifference;
    }

    private static bool IsLayoutField(string field) =>
        field.EndsWith(".X", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith(".Y", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith(".Z", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Insert", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Align", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Position", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Bounds", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Viewport", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Sheet", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Rotation", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Scale", StringComparison.OrdinalIgnoreCase);

    private static bool IsRenameField(string field) =>
        field.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SemanticId", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("RawHandle", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Handle", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("DisplayMark", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("ATT.", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase);

    private static bool IsInputBasisField(string field) =>
        field.Equals("Source", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SourceHash", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("InputHash", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("RelativePath", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SheetName", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("CellAddress", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Formula", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumericalField(string field) =>
        field.EndsWith("Mm", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Kg", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Rad", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Count", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Length", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Width", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Height", StringComparison.OrdinalIgnoreCase) ||
        field.EndsWith("Thickness", StringComparison.OrdinalIgnoreCase);
}
public sealed record DiffReport
{
    public DiffReport(ImmutableArray<SemanticEntity> missing,ImmutableArray<SemanticEntity> unexpected,ImmutableArray<SemanticChange> changed) { Missing=missing; Unexpected=unexpected; Changed=changed; }
    public ImmutableArray<SemanticEntity> Missing { get; }
    public ImmutableArray<SemanticEntity> Unexpected { get; }
    public ImmutableArray<SemanticChange> Changed { get; }
    public bool NoDifference => Missing.IsEmpty && Unexpected.IsEmpty && Changed.IsEmpty;

    public bool HasContentLoss => !Missing.IsEmpty;
    public bool IsLayoutOnly => Missing.IsEmpty && Unexpected.IsEmpty && !Changed.IsEmpty && Changed.All(c => c.Nature == DifferenceNature.LayoutOnly);
    public bool IsRenameOnly => Missing.IsEmpty && Unexpected.IsEmpty && !Changed.IsEmpty && Changed.All(c => c.Nature == DifferenceNature.Rename);
    public bool IsInputBasisDifference => Missing.IsEmpty && Unexpected.IsEmpty && !Changed.IsEmpty && Changed.All(c => c.Nature == DifferenceNature.InputBasis);
    public ImmutableArray<SemanticEntity> ContentLoss => Missing;
    public ImmutableArray<SemanticChange> LayoutDifferences => Changed.Where(c => c.Nature == DifferenceNature.LayoutOnly).ToImmutableArray();
    public ImmutableArray<SemanticChange> RenameDifferences => Changed.Where(c => c.Nature == DifferenceNature.Rename).ToImmutableArray();
    public ImmutableArray<SemanticChange> InputBasisDifferences => Changed.Where(c => c.Nature == DifferenceNature.InputBasis).ToImmutableArray();
    public ImmutableArray<SemanticChange> NumericalDifferences => Changed.Where(c => c.Nature == DifferenceNature.NumericalMismatch).ToImmutableArray();
}
public static class SemanticDiff
{
    public static DiffReport Compare(SemanticSnapshot expected, SemanticSnapshot actual, SemanticTolerance tolerance)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(tolerance);
        ContractGuard.Positive(tolerance.CoordinateMm, nameof(tolerance.CoordinateMm));
        ContractGuard.Positive(tolerance.RawMassKg, nameof(tolerance.RawMassKg));
        var left = expected.Entities.ToDictionary(e => e.SemanticId, StringComparer.Ordinal);
        var right = actual.Entities.ToDictionary(e => e.SemanticId, StringComparer.Ordinal);
        var changes = ImmutableArray.CreateBuilder<SemanticChange>();
        foreach (var id in left.Keys.Intersect(right.Keys).Order(StringComparer.Ordinal))
        {
            var e = left[id]; var a = right[id];
            var fields = ImmutableArray.CreateBuilder<SemanticFieldDifference>();
            void Text(string key, string? x, string? y)
            {
                if (!string.Equals(x,y,StringComparison.Ordinal)) fields.Add(new(key,x,y,null));
            }
            Text("Type",e.Type,a.Type); Text("Owner",e.Owner,a.Owner);
            foreach (var key in e.Fields.Keys.Union(a.Fields.Keys).Order(StringComparer.Ordinal))
                Text(key,e.Fields.GetValueOrDefault(key),a.Fields.GetValueOrDefault(key));
            foreach (var key in e.Numbers.Keys.Union(a.Numbers.Keys).Order(StringComparer.Ordinal))
            {
                var hasE=e.Numbers.TryGetValue(key,out var x); var hasA=a.Numbers.TryGetValue(key,out var y);
                var limit=key.EndsWith("Kg",StringComparison.Ordinal) ? tolerance.RawMassKg : tolerance.CoordinateMm;
                if (!hasE || !hasA || Math.Abs(x-y)>limit)
                    fields.Add(new(key,hasE ? x.ToString("R",System.Globalization.CultureInfo.InvariantCulture) : null,
                        hasA ? y.ToString("R",System.Globalization.CultureInfo.InvariantCulture) : null,limit));
            }
            if (fields.Count>0) changes.Add(new(id,e.Owner,e.Source,fields.ToImmutable()));
        }
        return new(left.Keys.Except(right.Keys).Order(StringComparer.Ordinal).Select(id=>left[id]).ToImmutableArray(),
            right.Keys.Except(left.Keys).Order(StringComparer.Ordinal).Select(id=>right[id]).ToImmutableArray(),changes.ToImmutable());
    }

    public static DiffReport ComparePhysicalManifests(string expectedManifestJson, string actualManifestJson, SemanticTolerance? tolerance = null)
    {
        var expected = SemanticSnapshot.FromPhysicalManifest("expected-manifest", "baseline", expectedManifestJson);
        var actual = SemanticSnapshot.FromPhysicalManifest("actual-manifest", "reopened", actualManifestJson);
        return Compare(expected, actual, tolerance ?? new SemanticTolerance());
    }
}
