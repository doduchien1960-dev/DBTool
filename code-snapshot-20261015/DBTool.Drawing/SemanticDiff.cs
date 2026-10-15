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

    private static readonly ImmutableHashSet<string> SupportedSchemaVersions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "VHG-PHYSICAL-MANIFEST-V1",
        "VHG-PHYSICAL-MANIFEST-V2",
        "1.0",
        "2.0",
        "leader-field-contracts-20261013-r2",
        "vhgpro-standard-20261012"
    );

    private static readonly ImmutableHashSet<string> RecognizedEntityKinds = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "Line", "Circle", "Arc", "Polyline", "LwPolyline", "Text", "MText",
        "Dimension", "Hatch", "Solid", "Point", "Block", "BlockReference",
        "Rebar", "Mesh", "Tie", "Opening", "Component", "Profile"
    );

    public static SemanticSnapshot FromPhysicalManifest(
        string documentIdentity,
        string generationIdentity,
        string manifestJson,
        int? requiredMinimumCoverage = null,
        IEnumerable<string>? requiredSemanticIds = null)
    {
        throw new InvalidOperationException("SNAPSHOT_LEGACY_INCOMPATIBLE: Archived manifests require explicit migration to DBTOOL-PHYSICAL-R04 and an external expected contract.");
    }

    public static SemanticSnapshot ReadArchivedPhysicalManifest(
        string documentIdentity, string generationIdentity, string manifestJson,
        int? requiredMinimumCoverage = null, IEnumerable<string>? requiredSemanticIds = null)
    {
        ContractGuard.Text(documentIdentity, nameof(documentIdentity));
        ContractGuard.Text(generationIdentity, nameof(generationIdentity));

        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new ArgumentException("PHYSICAL_MANIFEST_EMPTY: Manifest JSON content is empty.", nameof(manifestJson));

        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object && root.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new InvalidOperationException("PHYSICAL_MANIFEST_INVALID: Manifest root must be a JSON object or array.");

        string? schemaVersion = null;
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            int propertyCount = 0;
            foreach (var _ in root.EnumerateObject()) propertyCount++;
            if (propertyCount == 0)
                throw new InvalidOperationException("PHYSICAL_MANIFEST_EMPTY: Manifest is an empty JSON object ({}).");

            if (root.TryGetProperty("SchemaVersion", out var sv)) schemaVersion = sv.GetString();
            else if (root.TryGetProperty("schemaVersion", out var svL)) schemaVersion = svL.GetString();

            if (schemaVersion is not null)
            {
                if (string.IsNullOrWhiteSpace(schemaVersion))
                    throw new InvalidOperationException("PHYSICAL_MANIFEST_INVALID: SchemaVersion cannot be empty.");
                if (!SupportedSchemaVersions.Contains(schemaVersion))
                    throw new InvalidOperationException($"PHYSICAL_MANIFEST_UNSUPPORTED_SCHEMA: SchemaVersion '{schemaVersion}' is not supported.");
            }
        }

        System.Text.Json.JsonElement entitiesElement = default;
        bool foundEntities = false;

        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            entitiesElement = root;
            foundEntities = true;
        }
        else
        {
            if (root.TryGetProperty("Entities", out var eNode) && eNode.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                entitiesElement = eNode;
                foundEntities = true;
            }
            else if (root.TryGetProperty("entities", out var eNodeL) && eNodeL.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                entitiesElement = eNodeL;
                foundEntities = true;
            }
            else if (root.TryGetProperty("Records", out var rNode) && rNode.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                entitiesElement = rNode;
                foundEntities = true;
            }
            else if (root.TryGetProperty("records", out var rNodeL) && rNodeL.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                entitiesElement = rNodeL;
                foundEntities = true;
            }
        }

        if (!foundEntities)
            throw new InvalidOperationException("PHYSICAL_MANIFEST_INVALID: Manifest does not declare an 'Entities' array.");

        int entityCount = entitiesElement.GetArrayLength();
        if (entityCount == 0)
            throw new InvalidOperationException("PHYSICAL_MANIFEST_EMPTY: Manifest contains 0 entities.");

        if (requiredMinimumCoverage.HasValue && entityCount < requiredMinimumCoverage.Value)
            throw new InvalidOperationException($"PHYSICAL_MANIFEST_COVERAGE_DEFICIT: Expected at least {requiredMinimumCoverage.Value} entities, found {entityCount}.");

        var entities = ImmutableArray.CreateBuilder<SemanticEntity>(entityCount);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in entitiesElement.EnumerateArray())
        {
            if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new InvalidOperationException("PHYSICAL_MANIFEST_INVALID: Entity in manifest must be a JSON object.");

            string? id = null;
            if (item.TryGetProperty("SemanticId", out var idProp)) id = idProp.GetString();
            else if (item.TryGetProperty("semanticId", out var idPropL)) id = idPropL.GetString();
            else if (item.TryGetProperty("Id", out var idProp2)) id = idProp2.GetString();
            else if (item.TryGetProperty("id", out var idProp2L)) id = idProp2L.GetString();

            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("PHYSICAL_MANIFEST_INVALID: Entity is missing required SemanticId.");

            if (!seenIds.Add(id))
                throw new ArgumentException($"PHYSICAL_MANIFEST_DUPLICATE_ID: Duplicate semantic identity '{id}' in manifest.", nameof(manifestJson));

            string? type = null;
            if (item.TryGetProperty("Type", out var typeProp)) type = typeProp.GetString();
            else if (item.TryGetProperty("type", out var typePropL)) type = typePropL.GetString();
            else if (item.TryGetProperty("Kind", out var kindProp)) type = kindProp.GetString();
            else if (item.TryGetProperty("kind", out var kindPropL)) type = kindPropL.GetString();

            if (string.IsNullOrWhiteSpace(type))
                throw new InvalidOperationException($"PHYSICAL_MANIFEST_INVALID: Entity '{id}' is missing required Type/Kind.");

            if (!RecognizedEntityKinds.Contains(type))
                throw new InvalidOperationException($"PHYSICAL_MANIFEST_UNKNOWN_KIND: Entity '{id}' has unknown or unsupported kind '{type}'.");

            string? owner = null;
            if (item.TryGetProperty("Owner", out var oProp)) owner = oProp.GetString();
            else if (item.TryGetProperty("owner", out var oPropL)) owner = oPropL.GetString();

            string? handle = null;
            if (item.TryGetProperty("RawHandle", out var hProp)) handle = hProp.GetString();
            else if (item.TryGetProperty("rawHandle", out var hPropL)) handle = hPropL.GetString();
            else if (item.TryGetProperty("Handle", out var hProp2)) handle = hProp2.GetString();
            else if (item.TryGetProperty("handle", out var hProp2L)) handle = hProp2L.GetString();

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (item.TryGetProperty("Space", out var spProp) && spProp.GetString() is { } sp) fields["Space"] = sp;
            else if (item.TryGetProperty("space", out var spPropL) && spPropL.GetString() is { } spL) fields["Space"] = spL;

            if (item.TryGetProperty("Frame", out var frProp) && frProp.GetString() is { } fr) fields["Frame"] = fr;
            else if (item.TryGetProperty("frame", out var frPropL) && frPropL.GetString() is { } frL) fields["Frame"] = frL;
            else if (item.TryGetProperty("FrameId", out var frIdProp) && frIdProp.GetString() is { } frId) fields["Frame"] = frId;

            if (item.TryGetProperty("Layer", out var layProp) && layProp.GetString() is { } lay) fields["Layer"] = lay;
            else if (item.TryGetProperty("layer", out var layPropL) && layPropL.GetString() is { } layL) fields["Layer"] = layL;

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

            ExtractDirectAndNestedGeometry(item, numbers);

            ValidateEntityGeometryPayload(id, type, numbers, fields, schemaVersion);

            entities.Add(new SemanticEntity(id, type, owner, null, fields, numbers, handle));
        }

        if (requiredSemanticIds != null)
        {
            var requiredSet = requiredSemanticIds.ToHashSet(StringComparer.Ordinal);
            var missingRequired = requiredSet.Except(seenIds).ToList();
            if (missingRequired.Count > 0)
                throw new InvalidOperationException($"PHYSICAL_MANIFEST_COVERAGE_DEFICIT: Externally declared contract requires semantic IDs [{string.Join(", ", missingRequired)}] which are missing from manifest.");
        }

        return new SemanticSnapshot(documentIdentity, generationIdentity, entities.ToImmutable());
    }

    private static void ExtractDirectAndNestedGeometry(System.Text.Json.JsonElement item, Dictionary<string, double> numbers)
    {
        void CheckNum(string key, string numKey)
        {
            if (item.TryGetProperty(key, out var prop) && prop.TryGetDouble(out var val) && !numbers.ContainsKey(numKey))
                numbers[numKey] = val;
        }
        void CheckNested(string objKey, string prefix)
        {
            if (item.TryGetProperty(objKey, out var obj) && obj.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (obj.TryGetProperty("X", out var x) && x.TryGetDouble(out var vx) && !numbers.ContainsKey(prefix + ".X"))
                    numbers[prefix + ".X"] = vx;
                else if (obj.TryGetProperty("x", out var xL) && xL.TryGetDouble(out var vxL) && !numbers.ContainsKey(prefix + ".X"))
                    numbers[prefix + ".X"] = vxL;

                if (obj.TryGetProperty("Y", out var y) && y.TryGetDouble(out var vy) && !numbers.ContainsKey(prefix + ".Y"))
                    numbers[prefix + ".Y"] = vy;
                else if (obj.TryGetProperty("y", out var yL) && yL.TryGetDouble(out var vyL) && !numbers.ContainsKey(prefix + ".Y"))
                    numbers[prefix + ".Y"] = vyL;

                if (obj.TryGetProperty("Z", out var z) && z.TryGetDouble(out var vz) && !numbers.ContainsKey(prefix + ".Z"))
                    numbers[prefix + ".Z"] = vz;
                else if (obj.TryGetProperty("z", out var zL) && zL.TryGetDouble(out var vzL) && !numbers.ContainsKey(prefix + ".Z"))
                    numbers[prefix + ".Z"] = vzL;
            }
        }

        CheckNested("Start", "Start");
        CheckNested("start", "Start");
        CheckNested("End", "End");
        CheckNested("end", "End");
        CheckNested("Center", "Center");
        CheckNested("center", "Center");
        CheckNested("Point", "Point");
        CheckNested("point", "Point");
        CheckNested("SheetPlacement", "SheetPlacement");
        CheckNested("sheetPlacement", "SheetPlacement");

        CheckNum("StartX", "Start.X");
        CheckNum("StartY", "Start.Y");
        CheckNum("StartZ", "Start.Z");
        CheckNum("EndX", "End.X");
        CheckNum("EndY", "End.Y");
        CheckNum("EndZ", "End.Z");
        CheckNum("CenterX", "Center.X");
        CheckNum("CenterY", "Center.Y");
        CheckNum("CenterZ", "Center.Z");
        CheckNum("Radius", "RadiusMm");
        CheckNum("RadiusMm", "RadiusMm");
        CheckNum("Length", "LengthMm");
        CheckNum("LengthMm", "LengthMm");
        CheckNum("Measurement", "MeasurementMm");
        CheckNum("MeasurementMm", "MeasurementMm");
        CheckNum("Width", "WidthMm");
        CheckNum("WidthMm", "WidthMm");
        CheckNum("Height", "HeightMm");
        CheckNum("HeightMm", "HeightMm");
        CheckNum("Thickness", "ThicknessMm");
        CheckNum("ThicknessMm", "ThicknessMm");
    }

    private static void ValidateEntityGeometryPayload(string id, string type, Dictionary<string, double> numbers, Dictionary<string, string> fields, string? schemaVersion)
    {
        bool hasGeometry = false;
        switch (type.ToUpperInvariant())
        {
            case "LINE":
                hasGeometry = numbers.ContainsKey("Start.X") || numbers.ContainsKey("Start.Y") || numbers.ContainsKey("Start.Z") ||
                              numbers.ContainsKey("End.X") || numbers.ContainsKey("End.Y") || numbers.ContainsKey("End.Z") ||
                              numbers.ContainsKey("LengthMm") || numbers.ContainsKey("MeasurementMm");
                break;
            case "CIRCLE":
            case "ARC":
                hasGeometry = (numbers.ContainsKey("Center.X") || numbers.ContainsKey("Center.Y")) &&
                              (numbers.ContainsKey("RadiusMm") || numbers.ContainsKey("MeasurementMm"));
                break;
            case "POLYLINE":
            case "LWPOLYLINE":
                hasGeometry = numbers.Keys.Any(k => k.StartsWith("Vertex", StringComparison.OrdinalIgnoreCase)) ||
                              numbers.ContainsKey("LengthMm") || numbers.ContainsKey("AreaMm2");
                break;
            case "POINT":
                hasGeometry = numbers.ContainsKey("Point.X") || numbers.ContainsKey("Point.Y") || numbers.ContainsKey("Start.X");
                break;
            case "TEXT":
            case "MTEXT":
                hasGeometry = fields.ContainsKey("Text") || fields.ContainsKey("Contents") || fields.ContainsKey("Value") ||
                              numbers.ContainsKey("HeightMm") || numbers.ContainsKey("Point.X") || numbers.ContainsKey("Start.X");
                break;
            default:
                hasGeometry = numbers.Count > 0 || fields.Count > 0;
                break;
        }

        if (!hasGeometry)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion))
                throw new InvalidOperationException($"PHYSICAL_MANIFEST_INVALID: Manifest lacks explicit schema/version and entity '{id}' of kind '{type}' has missing geometry payload.");
            throw new InvalidOperationException($"PHYSICAL_MANIFEST_MISSING_GEOMETRY: Entity '{id}' of kind '{type}' is missing required geometry payload.");
        }
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
    AttributeDifference,
    CoordinateBasisMismatch,
    SemanticMismatch,
    MetadataMismatch
}
public sealed record SemanticChange
{
    public SemanticChange(string semanticId,string? owner,SourceRef? source,ImmutableArray<SemanticFieldDifference> fields)
        : this(semanticId, owner, source, fields, ClassifyDifference(fields, source, null, null)) { }

    public SemanticChange(string semanticId, string? owner, SourceRef? source, ImmutableArray<SemanticFieldDifference> fields, SemanticEntity? expected, SemanticEntity? actual)
        : this(semanticId, owner, source, fields, ClassifyDifference(fields, source, expected, actual)) { }

    public SemanticChange(string semanticId,string? owner,SourceRef? source,ImmutableArray<SemanticFieldDifference> fields, DifferenceNature nature)
    {
        SemanticId=semanticId; Owner=owner; Source=source; Fields=fields; Nature=nature;
    }

    public string SemanticId { get; }
    public string? Owner { get; }
    public SourceRef? Source { get; }
    public ImmutableArray<SemanticFieldDifference> Fields { get; }
    public DifferenceNature Nature { get; }

    private static DifferenceNature ClassifyDifference(
        ImmutableArray<SemanticFieldDifference> fields,
        SourceRef? source,
        SemanticEntity? expectedEntity,
        SemanticEntity? actualEntity)
    {
        if (fields.IsEmpty) return DifferenceNature.AttributeDifference;

        // 1. Any physical coordinate or endpoint difference is a numerical/physical mismatch
        // "World endpoint đổi là physical difference... đổi physical endpoint không được LayoutOnly"
        bool hasPhysicalGeometryChange = fields.Any(f => IsPhysicalGeometryField(f.Field));
        if (hasPhysicalGeometryChange)
            return DifferenceNature.NumericalMismatch;

        // 2. Numeric sheet placement when physical payload is unchanged
        // "numeric SheetPlacement đổi khi physical payload giữ nguyên là LayoutOnly, không blanket NumericalMismatch."
        bool allLayoutFields = fields.All(f => IsLayoutField(f.Field));
        if (allLayoutFields)
            return DifferenceNature.LayoutOnly;

        // Check declared entity space/frame
        string? expectedSpace = expectedEntity?.Fields.GetValueOrDefault("Space");
        string? actualSpace = actualEntity?.Fields.GetValueOrDefault("Space");
        bool isSheetSpace = string.Equals(expectedSpace, "Sheet", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(expectedSpace, "Paper", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actualSpace, "Sheet", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actualSpace, "Paper", StringComparison.OrdinalIgnoreCase);

        if (isSheetSpace && fields.All(f => IsLayoutField(f.Field) || f.Field == "Space" || f.Field == "Frame"))
            return DifferenceNature.LayoutOnly;

        // 3. InputBasis: only when there is actual proof of source/input change
        if (fields.All(f => IsInputBasisField(f.Field)))
            return DifferenceNature.InputBasis;

        // 4. Rename: only naming/identifier changes
        if (fields.All(f => IsRenameField(f.Field)))
            return DifferenceNature.Rename;

        // 5. Numerical mismatches for non-layout numerical fields beyond tolerance
        if (fields.Any(f => !IsLayoutField(f.Field) && (f.Tolerance.HasValue || IsNumericalField(f.Field))))
            return DifferenceNature.NumericalMismatch;

        return DifferenceNature.AttributeDifference;
    }

    private static bool IsPhysicalGeometryField(string field) =>
        field.StartsWith("Start.", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("End.", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Center.", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Vertex", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Point.", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("StartX", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("StartY", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("StartZ", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("EndX", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("EndY", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("EndZ", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("CenterX", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("CenterY", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("CenterZ", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("MeasurementMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("RadiusMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Radius", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("LengthMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Length", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("WidthMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Width", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("HeightMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("Height", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("ThicknessMm", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("MassKg", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("AreaMm2", StringComparison.OrdinalIgnoreCase);

    private static bool IsLayoutField(string field) =>
        field.StartsWith("SheetPlacement", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Sheet.Placement", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("SheetOffset", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Sheet.Offset", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("Viewport", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("PaperPlacement", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("PaperSpace", StringComparison.OrdinalIgnoreCase) ||
        field.StartsWith("SheetSlot", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SheetName", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SheetIndex", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("SheetNumber", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("LayoutName", StringComparison.OrdinalIgnoreCase) ||
        field.Equals("ViewPlacement", StringComparison.OrdinalIgnoreCase);

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
    public DiffReport(ImmutableArray<SemanticEntity> missing,ImmutableArray<SemanticEntity> unexpected,ImmutableArray<SemanticChange> changed,ImmutableArray<SemanticFieldDifference> auditDifferences=default) { Missing=missing; Unexpected=unexpected; Changed=changed; AuditDifferences=auditDifferences.IsDefault?[]:auditDifferences; }
    public ImmutableArray<SemanticFieldDifference> AuditDifferences { get; }
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
        => throw new InvalidOperationException("SNAPSHOT_LEGACY_INCOMPATIBLE: SemanticSnapshot is archival only; acceptance requires validated CanonicalPhysicalSnapshot and an external expected contract.");

    /// <summary>Archive inspection only. This result is never acceptance evidence.</summary>
    public static DiffReport CompareArchived(SemanticSnapshot expected, SemanticSnapshot actual, SemanticTolerance tolerance)
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
            if (fields.Count>0) changes.Add(new(id,e.Owner,e.Source,fields.ToImmutable(),e,a));
        }
        return new(left.Keys.Except(right.Keys).Order(StringComparer.Ordinal).Select(id=>left[id]).ToImmutableArray(),
            right.Keys.Except(left.Keys).Order(StringComparer.Ordinal).Select(id=>right[id]).ToImmutableArray(),changes.ToImmutable());
    }

    public static DiffReport ComparePhysicalManifests(
        string expectedManifestJson,
        string actualManifestJson,
        SemanticTolerance? tolerance = null,
        int? expectedMinimumCoverage = null,
        IEnumerable<string>? requiredSemanticIds = null)
    {
        throw new InvalidOperationException("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED: DBTOOL-PHYSICAL-R04 acceptance requires an externally locked expected contract.");
    }

    public static DiffReport ComparePhysicalManifests(string expectedManifestJson, string actualManifestJson, string expectedContractJson)
        => CanonicalPhysicalValidator.Compare(
            CanonicalPhysicalValidator.Validate(expectedManifestJson, expectedContractJson),
            CanonicalPhysicalValidator.Validate(actualManifestJson, expectedContractJson));

    public static DiffReport Compare(CanonicalPhysicalSnapshot expected, CanonicalPhysicalSnapshot actual)
        => CanonicalPhysicalValidator.Compare(expected, actual);

    public static DiffReport CompareArchivedPhysicalManifests(
        string expectedManifestJson, string actualManifestJson, SemanticTolerance? tolerance = null,
        int? expectedMinimumCoverage = null, IEnumerable<string>? requiredSemanticIds = null)
    {
        if (string.IsNullOrWhiteSpace(expectedManifestJson))
            throw new ArgumentException("PHYSICAL_MANIFEST_EMPTY: Expected manifest JSON is empty.", nameof(expectedManifestJson));
        if (string.IsNullOrWhiteSpace(actualManifestJson))
            throw new ArgumentException("PHYSICAL_MANIFEST_EMPTY: Actual manifest JSON is empty.", nameof(actualManifestJson));

        var expected = SemanticSnapshot.ReadArchivedPhysicalManifest("expected-manifest", "baseline", expectedManifestJson, expectedMinimumCoverage, requiredSemanticIds);
        var actual = SemanticSnapshot.ReadArchivedPhysicalManifest("actual-manifest", "reopened", actualManifestJson, null, requiredSemanticIds);

        if (expectedMinimumCoverage.HasValue && actual.Entities.Length < expectedMinimumCoverage.Value)
            throw new InvalidOperationException($"PHYSICAL_MANIFEST_COVERAGE_DEFICIT: Expected at least {expectedMinimumCoverage.Value} entities, actual manifest has {actual.Entities.Length}.");

        if (expected.Entities.IsEmpty)
            throw new InvalidOperationException("PHYSICAL_MANIFEST_EMPTY: Expected manifest contains no entities.");
        if (actual.Entities.IsEmpty)
            throw new InvalidOperationException("PHYSICAL_MANIFEST_EMPTY: Actual manifest contains no entities.");

        return CompareArchived(expected, actual, tolerance ?? new SemanticTolerance());
    }
}
