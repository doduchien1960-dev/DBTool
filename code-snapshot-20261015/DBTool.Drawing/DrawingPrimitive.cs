using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

[JsonPolymorphic(TypeDiscriminatorPropertyName="$type")]
[JsonDerivedType(typeof(PolylinePrimitive),"polyline")]
[JsonDerivedType(typeof(CirclePrimitive),"circle")]
[JsonDerivedType(typeof(TextPrimitive),"text")]
[JsonDerivedType(typeof(DimensionIntentPrimitive),"dimension")]
[JsonDerivedType(typeof(DiameterDimensionPrimitive),"diameterDimension")]
[JsonDerivedType(typeof(TableRowPrimitive),"tableRow")]
[JsonDerivedType(typeof(SectionBoundaryPrimitive),"sectionBoundary")]
[JsonDerivedType(typeof(PathPrimitive),"path")]
[JsonDerivedType(typeof(HatchPrimitive),"hatch")]
[JsonDerivedType(typeof(BlockPrimitive),"block")]
public abstract record DrawingPrimitive
{
    private protected DrawingPrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark)
    {
        SemanticId=ContractGuard.Text(semanticId,nameof(semanticId)); Role=ContractGuard.Text(role,nameof(role)); ViewId=ContractGuard.Text(viewId,nameof(viewId));
        ComponentId=componentId; OccurrenceId=occurrenceId; ScheduleMark=scheduleMark;
    }
    public string SemanticId { get; }
    public string Role { get; }
    public string ViewId { get; }
    public string? ComponentId { get; }
    public string? OccurrenceId { get; }
    public string? ScheduleMark { get; }
}
public sealed record PolylinePrimitive : DrawingPrimitive
{
    public PolylinePrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark,IEnumerable<Point2Mm> points, bool closed,
        double? logicalLengthMm = null, DrawingMaterialKind materialKind = DrawingMaterialKind.None,
        IEnumerable<double>? bulges = null) : base(semanticId,role,viewId,componentId,occurrenceId,scheduleMark)
    {
        Points = ContractGuard.Copy(points);
        Closed = closed;
        LogicalLengthMm = logicalLengthMm;
        MaterialKind = ContractGuard.EnumValue(materialKind);
        Bulges = bulges is not null ? [.. bulges] : [];
        if (logicalLengthMm.HasValue) ContractGuard.Positive(logicalLengthMm.Value, nameof(logicalLengthMm));
        if(Points.Length<(closed?3:2)) throw new ArgumentException("Too few polyline points.");
        for(int i=1;i<Points.Length;i++) if(Points[i]==Points[i-1]) throw new ArgumentException("Zero edge.");
        if(closed && Points[0]==Points[^1]) throw new ArgumentException("Closure is implicit.");
    }
    public ImmutableArray<Point2Mm> Points { get; }
    public bool Closed { get; }
    public double? LogicalLengthMm { get; }
    public DrawingMaterialKind MaterialKind { get; }
    public ImmutableArray<double> Bulges { get; }
}
public sealed record CirclePrimitive : DrawingPrimitive
{
    public CirclePrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark,Point2Mm center, double radiusMm) : base(semanticId,role,viewId,componentId,occurrenceId,scheduleMark)
    {
        Center = center;
        RadiusMm = ContractGuard.Positive(radiusMm, nameof(radiusMm));

    }
    public Point2Mm Center { get; }
    public double RadiusMm { get; }
}
public sealed record SectionBoundaryPrimitive : DrawingPrimitive
{
    public SectionBoundaryPrimitive(string semanticId,string role,string viewId,
        string componentId,string? sourceOpeningId,SectionBoundaryLoop boundary)
        :base(semanticId,role,viewId,componentId,null,null)
    {
        SourceOpeningId=sourceOpeningId;
        Boundary=ContractGuard.Required(boundary);
    }
    public string? SourceOpeningId { get; }
    public SectionBoundaryLoop Boundary { get; }
}
public sealed record TextPrimitive : DrawingPrimitive
{
    public TextPrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark,Point2Mm position, string content, double heightMm, double rotationRadians) : base(semanticId,role,viewId,componentId,occurrenceId,scheduleMark)
    {
        Position = position;
        Content = ContractGuard.Text(content, nameof(content));
        HeightMm = ContractGuard.Positive(heightMm, nameof(heightMm));
        RotationRadians = ContractGuard.Finite(rotationRadians, nameof(rotationRadians));

    }
    public Point2Mm Position { get; }
    public string Content { get; }
    public double HeightMm { get; }
    public double RotationRadians { get; }
}
public sealed record DimensionIntentPrimitive : DrawingPrimitive
{
    public DimensionIntentPrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark,Point2Mm start, Point2Mm end, double logicalMeasurementMm, string label, double offsetMm, double? rotationRadians = null, Point2Mm? dimensionLinePoint = null) : base(semanticId,role,viewId,componentId,occurrenceId,scheduleMark)
    {
        Start = start;
        End = end;
        LogicalMeasurementMm = ContractGuard.NonNegative(logicalMeasurementMm, nameof(logicalMeasurementMm));
        Label = ContractGuard.Text(label, nameof(label));
        OffsetMm = ContractGuard.Finite(offsetMm, nameof(offsetMm));
        RotationRadians = rotationRadians.HasValue ? ContractGuard.Finite(rotationRadians.Value, nameof(rotationRadians)) : null;
        DimensionLinePoint = dimensionLinePoint;
        Points = [start, end];
        TextRole = "DimensionText";

    }
    public Point2Mm Start { get; }
    public Point2Mm End { get; }
    public double LogicalMeasurementMm { get; }
    public string Label { get; }
    public double OffsetMm { get; }
    public double? RotationRadians { get; }
    public Point2Mm? DimensionLinePoint { get; }
    public ImmutableArray<Point2Mm> Points { get; }
    public string TextRole { get; }
}
public sealed record DiameterDimensionPrimitive : DrawingPrimitive
{
    public DiameterDimensionPrimitive(string semanticId,string role,string viewId,string? componentId,
        Point2Mm center,double diameterMm):base(semanticId,role,viewId,componentId,null,null)
    { Center=center;DiameterMm=ContractGuard.Positive(diameterMm,nameof(diameterMm)); }
    public Point2Mm Center {get;}
    public double DiameterMm {get;}
}
public sealed record TableRowPrimitive : DrawingPrimitive
{
    public TableRowPrimitive(string semanticId,string role,string viewId,string? componentId,string? occurrenceId,string? scheduleMark,string mark, IEnumerable<TableCell> cells,
        DrawingMaterialKind materialKind = DrawingMaterialKind.None, IEnumerable<TableCellPlacement>? placements = null) : base(semanticId,role,viewId,componentId,occurrenceId,scheduleMark)
    {
        Mark = ContractGuard.Text(mark, nameof(mark));
        Cells = ContractGuard.Copy(cells);
        Placements = ContractGuard.Copy(placements ?? []);
        if (!Placements.IsEmpty && (!Placements.Select(p => p.ColumnKey).SequenceEqual(Cells.Select(c => c.ColumnKey))))
            throw new ArgumentException("Table coordinates must cover cells in column order.");
        if(Cells.Select(c=>c.ColumnKey).Distinct().Count()!=Cells.Length) throw new ArgumentException("Duplicate table column.");
        MaterialKind = ContractGuard.EnumValue(materialKind);
    }
    public string Mark { get; }
    public ImmutableArray<TableCell> Cells { get; }
    public ImmutableArray<TableCellPlacement> Placements { get; }
    public DrawingMaterialKind MaterialKind { get; }
}

public sealed record TableCellPlacement
{
    public TableCellPlacement(string columnKey, Point2Mm position, double heightMm)
    {
        ColumnKey = ContractGuard.Text(columnKey, nameof(columnKey));
        Position = position;
        HeightMm = ContractGuard.Positive(heightMm, nameof(heightMm));
    }
    public string ColumnKey { get; }
    public Point2Mm Position { get; }
    public double HeightMm { get; }
}

public enum DrawingMaterialKind { None, Concrete, Formwork, RoundSteel, AngleSteel, PurchasedCover }

[JsonPolymorphic(TypeDiscriminatorPropertyName="$type")]
[JsonDerivedType(typeof(LinePathSegment),"line")]
[JsonDerivedType(typeof(ArcPathSegment),"arc")]
public abstract record DrawingPathSegment
{
    private protected DrawingPathSegment() { }
    public abstract Point2Mm Start { get; }
    public abstract Point2Mm End { get; }
}

public sealed record LinePathSegment : DrawingPathSegment
{
    public LinePathSegment(Point2Mm start, Point2Mm end)
    {
        if (start == end) throw new ArgumentException("Zero length line.");
        Start = start; End = end;
    }
    public override Point2Mm Start { get; }
    public override Point2Mm End { get; }
}

public sealed record ArcPathSegment : DrawingPathSegment
{
    public ArcPathSegment(Point2Mm center, double radiusMm, double startRadians, double sweepRadians)
    {
        Center = center;
        RadiusMm = ContractGuard.Positive(radiusMm, nameof(radiusMm));
        StartRadians = ContractGuard.Finite(startRadians, nameof(startRadians));
        SweepRadians = ContractGuard.Finite(sweepRadians, nameof(sweepRadians));
        if (sweepRadians == 0 || Math.Abs(sweepRadians) > 2 * Math.PI) throw new ArgumentException("Invalid arc sweep.");
        Start = new(center.X + radiusMm * Math.Cos(startRadians), center.Y + radiusMm * Math.Sin(startRadians));
        End = new(center.X + radiusMm * Math.Cos(startRadians + sweepRadians), center.Y + radiusMm * Math.Sin(startRadians + sweepRadians));
    }
    public Point2Mm Center { get; }
    public double RadiusMm { get; }
    public double StartRadians { get; }
    public double SweepRadians { get; }
    public override Point2Mm Start { get; }
    public override Point2Mm End { get; }
}

public sealed record PathPrimitive : DrawingPrimitive
{
    public PathPrimitive(string semanticId, string role, string viewId, string? componentId, string? occurrenceId,
        string? scheduleMark, IEnumerable<DrawingPathSegment> segments, bool closed, ViewPurpose purpose,
        double scheduleLengthMm, DrawingMaterialKind materialKind, string? sourceOccurrenceId = null)
        : base(semanticId, role, viewId, componentId, occurrenceId, scheduleMark)
    {
        Segments = ContractGuard.Copy(segments);
        if (Segments.IsEmpty) throw new ArgumentException("Path needs segments.");
        Closed = closed;
        Purpose = ContractGuard.EnumValue(purpose);
        ScheduleLengthMm = ContractGuard.Positive(scheduleLengthMm, nameof(scheduleLengthMm));
        MaterialKind = ContractGuard.EnumValue(materialKind);
        SourceOccurrenceId = sourceOccurrenceId;
    }
    public ImmutableArray<DrawingPathSegment> Segments { get; }
    public bool Closed { get; }
    public ViewPurpose Purpose { get; }
    public double ScheduleLengthMm { get; }
    public DrawingMaterialKind MaterialKind { get; }
    public string? SourceOccurrenceId { get; }
}

public sealed record HatchPrimitive : DrawingPrimitive
{
    public HatchPrimitive(string semanticId, string role, string viewId, string? componentId,
        IEnumerable<Point2Mm> outerLoop, IEnumerable<IEnumerable<Point2Mm>> innerLoops, DrawingMaterialKind materialKind,
        Point2Mm? patternOrigin = null, bool nativeBoundaryLoops = false)
        : base(semanticId, role, viewId, componentId, null, null)
    {
        OuterLoop = ContractGuard.Copy(outerLoop);
        InnerLoops = innerLoops.Select(ContractGuard.Copy).ToImmutableArray();
        if (OuterLoop.Length < 3 || InnerLoops.Any(loop => loop.Length < 3)) throw new ArgumentException("Hatch loops must be closed polygons.");
        MaterialKind = ContractGuard.EnumValue(materialKind);
        PatternOrigin = patternOrigin;
        NativeBoundaryLoops = nativeBoundaryLoops;
    }
    public ImmutableArray<Point2Mm> OuterLoop { get; }
    public Point2Mm? PatternOrigin { get; }
    public bool NativeBoundaryLoops { get; }
    public ImmutableArray<ImmutableArray<Point2Mm>> InnerLoops { get; }
    public DrawingMaterialKind MaterialKind { get; }
}

public sealed record BlockAttributeIntent
{
    public BlockAttributeIntent(string key, string value)
    {
        Key = ContractGuard.Text(key, nameof(key));
        Value = ContractGuard.Required(value);
    }
    public string Key { get; }
    public string Value { get; }
}

public sealed record BlockPrimitive : DrawingPrimitive
{
    public BlockPrimitive(string semanticId, string role, string viewId, string? componentId, string blockRole,
        Point2Mm insertion, double annotationScale, IEnumerable<BlockAttributeIntent> attributes, double rotationRadians = 0,
        string? occurrenceId = null, string? scheduleMark = null, bool mirrorHorizontal = false)
        : base(semanticId, role, viewId, componentId, occurrenceId, scheduleMark)
    {
        BlockRole = ContractGuard.Text(blockRole, nameof(blockRole));
        Insertion = insertion;
        AnnotationScale = ContractGuard.Positive(annotationScale, nameof(annotationScale));
        Attributes = ContractGuard.Copy(attributes);
        RotationRadians = ContractGuard.Finite(rotationRadians, nameof(rotationRadians));
        MirrorHorizontal = mirrorHorizontal;
    }
    public string BlockRole { get; }
    public Point2Mm Insertion { get; }
    public double AnnotationScale { get; }
    public ImmutableArray<BlockAttributeIntent> Attributes { get; }
    public double RotationRadians { get; }
    public bool MirrorHorizontal { get; }
}

/// <summary>Explicit JSON round trip for the locked constructor-based DrawingPackage contract.</summary>
public static class DrawingPackageJson
{
    public const string SupportedSchemaVersion = "drawing-v2";

    public static string Serialize(DrawingPackage package,bool indented=false)
    {
        ArgumentNullException.ThrowIfNull(package);
        return JsonSerializer.Serialize(package,new JsonSerializerOptions {WriteIndented=indented});
    }

    public static DrawingPackage Deserialize(string json)
    {
        var result = DeserializeResult(json);
        if (result.Success) return result.Value!;
        throw new JsonException(result.Diagnostics.FirstOrDefault()?.Message ?? "Unsupported drawing package.");
    }

    public static OperationResult<DrawingPackage> DeserializeResult(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        if (root.TryGetProperty("SchemaVersion", out var schema) && schema.GetString() is string version &&
            !string.Equals(version, SupportedSchemaVersion, StringComparison.Ordinal))
            return Block("DRAWING_SCHEMA_UNSUPPORTED", version);
        var views=root.GetProperty("Views").EnumerateArray().Select(v=>new ViewDefinition(
            v.GetProperty("Id").GetString()!,ReadEnum<ViewPurpose>(v.GetProperty("Purpose")),
            ReadPoint(v.GetProperty("Origin")),v.GetProperty("Bounds").ValueKind==JsonValueKind.Null?null:ReadBounds(v.GetProperty("Bounds")),
            v.GetProperty("ScaleDenominator").GetDouble())).ToArray();
        var primitives=root.GetProperty("Primitives").EnumerateArray().Select(ReadPrimitive).ToArray();
        var bounds=root.GetProperty("Bounds").ValueKind==JsonValueKind.Null?null:ReadBounds(root.GetProperty("Bounds"));
        var package = new DrawingPackage(root.GetProperty("InputHash").GetString()!,root.GetProperty("RuleVersion").GetString()!,
            views,primitives,bounds,ReadEnum<CalculationScope>(root.GetProperty("Scope")));
        var valid = DrawingPackageValidator.ValidatePrimitives(package.Views, package.Primitives);
        return valid.Success ? new(true, package, valid.Diagnostics) : new(false, null, valid.Diagnostics);
        }
        catch (JsonException ex)
        {
            return Block("DRAWING_SCHEMA_UNSUPPORTED", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Block("DRAWING_SCHEMA_UNSUPPORTED", ex.Message);
        }
    }

    private static DrawingPrimitive ReadPrimitive(JsonElement e)
    {
        string type=e.GetProperty("$type").GetString()!;
        string id=e.GetProperty("SemanticId").GetString()!;
        string role=e.GetProperty("Role").GetString()!;
        string view=e.GetProperty("ViewId").GetString()!;
        string? component=NullableText(e,"ComponentId");
        string? occurrence=NullableText(e,"OccurrenceId");
        string? mark=NullableText(e,"ScheduleMark");
        return type switch
        {
            "polyline"=>new PolylinePrimitive(id,role,view,component,occurrence,mark,
                e.GetProperty("Points").EnumerateArray().Select(ReadPoint),e.GetProperty("Closed").GetBoolean(),
                e.TryGetProperty("LogicalLengthMm",out var length) && length.ValueKind!=JsonValueKind.Null?length.GetDouble():null,
                e.TryGetProperty("MaterialKind",out var material)?ReadEnum<DrawingMaterialKind>(material):DrawingMaterialKind.None),
            "circle"=>new CirclePrimitive(id,role,view,component,occurrence,mark,
                ReadPoint(e.GetProperty("Center")),e.GetProperty("RadiusMm").GetDouble()),
            "sectionBoundary"=>new SectionBoundaryPrimitive(id,role,view,component!,
                NullableText(e,"SourceOpeningId"),ReadBoundary(e.GetProperty("Boundary"))),
            "text"=>new TextPrimitive(id,role,view,component,occurrence,mark,
                ReadPoint(e.GetProperty("Position")),e.GetProperty("Content").GetString()!,
                e.GetProperty("HeightMm").GetDouble(),e.GetProperty("RotationRadians").GetDouble()),
            "dimension"=>new DimensionIntentPrimitive(id,role,view,component,occurrence,mark,
                ReadPoint(e.GetProperty("Start")),ReadPoint(e.GetProperty("End")),
                e.GetProperty("LogicalMeasurementMm").GetDouble(),e.GetProperty("Label").GetString()!,
                e.GetProperty("OffsetMm").GetDouble(),
                e.TryGetProperty("RotationRadians", out var dr) && dr.ValueKind != JsonValueKind.Null ? dr.GetDouble() : null,
                e.TryGetProperty("DimensionLinePoint", out var dp) && dp.ValueKind != JsonValueKind.Null ? ReadPoint(dp) : null),
            "diameterDimension"=>new DiameterDimensionPrimitive(id,role,view,component,
                ReadPoint(e.GetProperty("Center")),e.GetProperty("DiameterMm").GetDouble()),
            "tableRow"=>new TableRowPrimitive(id,role,view,component,occurrence,mark,
                e.GetProperty("Mark").GetString()!,e.GetProperty("Cells").EnumerateArray().Select(ReadCell),
                e.TryGetProperty("MaterialKind",out var tableMaterial)?ReadEnum<DrawingMaterialKind>(tableMaterial):DrawingMaterialKind.None,
                e.TryGetProperty("Placements", out var placements) ? placements.EnumerateArray().Select(p => new TableCellPlacement(
                    p.GetProperty("ColumnKey").GetString()!, ReadPoint(p.GetProperty("Position")), p.GetProperty("HeightMm").GetDouble())) : []),
            "path"=>new PathPrimitive(id,role,view,component,occurrence,mark,
                e.GetProperty("Segments").EnumerateArray().Select(ReadPathSegment),e.GetProperty("Closed").GetBoolean(),
                ReadEnum<ViewPurpose>(e.GetProperty("Purpose")),e.GetProperty("ScheduleLengthMm").GetDouble(),
                ReadEnum<DrawingMaterialKind>(e.GetProperty("MaterialKind")),NullableText(e,"SourceOccurrenceId")),
            "hatch"=>new HatchPrimitive(id,role,view,component,
                e.GetProperty("OuterLoop").EnumerateArray().Select(ReadPoint),
                e.GetProperty("InnerLoops").EnumerateArray().Select(loop=>loop.EnumerateArray().Select(ReadPoint)),
                ReadEnum<DrawingMaterialKind>(e.GetProperty("MaterialKind")),
                e.TryGetProperty("PatternOrigin",out var hp) && hp.ValueKind!=JsonValueKind.Null?ReadPoint(hp):null,
                e.TryGetProperty("NativeBoundaryLoops",out var nb) && nb.GetBoolean()),
            "block"=>new BlockPrimitive(id,role,view,component,e.GetProperty("BlockRole").GetString()!,
                ReadPoint(e.GetProperty("Insertion")),e.GetProperty("AnnotationScale").GetDouble(),
                e.GetProperty("Attributes").EnumerateArray().Select(a=>new BlockAttributeIntent(
                    a.GetProperty("Key").GetString()!,a.GetProperty("Value").GetString()!)),
                e.TryGetProperty("RotationRadians",out var br) ? br.GetDouble() : 0,occurrence,mark,
                e.TryGetProperty("MirrorHorizontal",out var mirror) && mirror.GetBoolean()),
            _=>throw new JsonException($"Unknown drawing primitive kind: {type}")
        };
    }

    private static DrawingPathSegment ReadPathSegment(JsonElement e) => e.GetProperty("$type").GetString() switch
    {
        "line" => new LinePathSegment(ReadPoint(e.GetProperty("Start")), ReadPoint(e.GetProperty("End"))),
        "arc" => new ArcPathSegment(ReadPoint(e.GetProperty("Center")), e.GetProperty("RadiusMm").GetDouble(),
            e.GetProperty("StartRadians").GetDouble(), e.GetProperty("SweepRadians").GetDouble()),
        _ => throw new JsonException($"Unknown path segment kind: {e.GetProperty("$type").GetString()}")
    };

    private static SectionBoundaryLoop ReadBoundary(JsonElement e)=>new(
        e.GetProperty("Edges").EnumerateArray().Select(ReadEdge),
        ReadEnum<BoundaryWinding>(e.GetProperty("Winding")));
    private static SectionBoundaryEdge ReadEdge(JsonElement e)=>e.GetProperty("$type").GetString() switch
    {
        "line"=>new SectionLineEdge(ReadPoint(e.GetProperty("Start")),ReadPoint(e.GetProperty("End"))),
        "ellipse"=>new SectionEllipseEdge(ReadPoint(e.GetProperty("Center")),
            e.GetProperty("StationRadiusMm").GetDouble(),e.GetProperty("HeightRadiusMm").GetDouble(),
            e.GetProperty("StartRadians").GetDouble(),e.GetProperty("EndRadians").GetDouble()),
        _=>throw new JsonException("Unknown section boundary edge kind.")
    };

    private static TableCell ReadCell(JsonElement e)=>new(e.GetProperty("ColumnKey").GetString()!,
        ReadEnum<TableCellKind>(e.GetProperty("Kind")),NullableText(e,"Text"),
        e.GetProperty("Number").ValueKind==JsonValueKind.Null?null:e.GetProperty("Number").GetDouble());
    private static string? NullableText(JsonElement e,string name)=>e.GetProperty(name).ValueKind==JsonValueKind.Null?null:e.GetProperty(name).GetString();
    private static Point2Mm ReadPoint(JsonElement e)=>new(e.GetProperty("X").GetDouble(),e.GetProperty("Y").GetDouble());
    private static Bounds2Mm ReadBounds(JsonElement e)=>new(e.GetProperty("MinX").GetDouble(),e.GetProperty("MinY").GetDouble(),
        e.GetProperty("MaxX").GetDouble(),e.GetProperty("MaxY").GetDouble());
    private static T ReadEnum<T>(JsonElement e) where T:struct,Enum=>e.ValueKind==JsonValueKind.String
        ? Enum.Parse<T>(e.GetString()!,false):(T)Enum.ToObject(typeof(T),e.GetInt32());

    private static OperationResult<DrawingPackage> Block(string code, string actual) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, actual, "Drawing package schema is unsupported or malformed.")]);
}
