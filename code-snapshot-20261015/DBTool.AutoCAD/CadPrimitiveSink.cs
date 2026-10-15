using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DBTool.AutoCAD.Generation;
using DBTool.Domain.Geometry;
using DBTool.Domain;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Template;
using DBTool.Drawing;

namespace DBTool.AutoCAD.Render;

public sealed record CadPrimitiveStyle(
    string Layer,
    short ColorIndex = 256,
    string? Linetype = null,
    double HatchScale = 1,
    string? HatchPattern = null,
    string? DimensionStyle = null,
    string? TextStyle = null,
    double? TextWidthFactor = null,
    double LinetypeScale = 1,
    double HatchAngleRadians = 0,
    Point2Mm? HatchOrigin = null,
    bool TextRightAligned = false,
    bool TextMidBase = false,
    bool TextMidCenter = false,
    bool NativeBoundaryLoops = false,
    string? AttributeTextStyle = null,
    bool FitMarkToCircle = false,
    string? DimensionTextStyle = null,
    Point2Mm? DimensionTextPosition = null,
    bool MirrorBlockHorizontal = false)
{
    public string EffectiveLayer => string.IsNullOrWhiteSpace(Layer) ? "0" : Layer;
}

public sealed record CadHatchResult(string HatchHandle, IReadOnlyList<string> BoundaryHandles);
public sealed record CadBlockResult(string BlockHandle, IReadOnlyList<string> AttributeHandles);

public interface ICadPrimitiveSink
{
    void ValidateEntityOutput(IReadOnlyList<string> handles, Bounds2Mm? allowedBounds) =>
        throw new InvalidOperationException("CAD sink cannot verify entity output.");
    void SetViewTransform(ViewTransform transform, Bounds2Mm logicalBounds) =>
        throw new InvalidOperationException("CAD sink does not support view placement.");
    string AppendEllipse(Point2Mm center, double stationRadiusMm, double heightRadiusMm,
        double startRadians, double endRadians, CadPrimitiveStyle style) =>
        throw new InvalidOperationException("CAD sink does not support ellipses.");
    OperationResult<TemplateManifest> ValidateAssets(TemplateManifest manifest, bool allowMissing = false) =>
        new(false, null, [new Diagnostic("TEMPLATE_TARGET_UNVERIFIED", DiagnosticSeverity.Block,
            DiagnosticScope.Global, null, null, null, null, null, "Sink cannot inspect native target assets.")]);
    bool HasBlock(string blockName);
    bool HasLayer(string layerName);
    bool HasHatchPattern(string patternName);
    string AppendLine(Point2Mm start, Point2Mm end, CadPrimitiveStyle style);
    string AppendPolyline(IReadOnlyList<Point2Mm> points, bool closed, CadPrimitiveStyle style, IReadOnlyList<double>? bulges = null);
    string AppendSectionDot(Point2Mm center, double graphicDiameterMm, CadPrimitiveStyle style) =>
        throw new InvalidOperationException("CAD sink does not support native section dots.");
    string AppendLeader(IReadOnlyList<Point2Mm> points, CadPrimitiveStyle style) =>
        throw new InvalidOperationException("CAD sink does not support native leaders.");
    string AppendArc(Point2Mm center, double radiusMm, double startRadians, double sweepRadians, CadPrimitiveStyle style);
    string AppendText(Point2Mm position, string content, double heightMm, double rotationRadians, CadPrimitiveStyle style);
    string AppendMText(Point2Mm position, string content, double heightMm, double rotationRadians, CadPrimitiveStyle style) =>
        AppendText(position, content, heightMm, rotationRadians, style);
    string AppendDimension(Point2Mm start, Point2Mm end, double logicalMeasurementMm, double offsetMm, CadPrimitiveStyle style, double? rotationRadians = null, Point2Mm? dimensionLinePoint = null, string? textOverride = null);
    string AppendDiameterDimension(Point2Mm center,double diameterMm,CadPrimitiveStyle style) =>
        throw new InvalidOperationException("CAD sink does not support native diameter dimensions.");
    CadHatchResult AppendHatch(IReadOnlyList<Point2Mm> outerLoop, IReadOnlyList<IReadOnlyList<Point2Mm>> innerLoops, CadPrimitiveStyle style);
    CadBlockResult AppendBlock(string blockName, Point2Mm insertion, double annotationScale, IReadOnlyDictionary<string, string> attributes, CadPrimitiveStyle style, double rotationRadians = 0);
}

/// <summary>
/// The only object that creates AutoCAD DBObjects for a generation transaction.
/// It never starts/commits a transaction and never disposes an appended DBObject.
/// </summary>
public sealed class CadPrimitiveSink : ICadPrimitiveSink
{
    private readonly Database _database;
    private readonly Transaction _transaction;
    private readonly BlockTableRecord _space;
    private readonly Point2Mm _insertion;
    private readonly Action<DBObject>? _tag;
    private Matrix3d _viewMatrix = Matrix3d.Identity;
    private double _viewScale = 1;
    private double _viewRotation;

    public void ValidateEntityOutput(IReadOnlyList<string> handles, Bounds2Mm? allowedBounds)
    {
        foreach (var handle in handles)
        {
            var id = _database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
            var entity = (Entity)_transaction.GetObject(id, OpenMode.ForRead);
            if (entity is AttributeReference attRef && string.IsNullOrWhiteSpace(attRef.TextString))
                continue;
            Extents3d extents;
            try { extents = entity.GeometricExtents; }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            { throw new InvalidOperationException($"CAD_ENTITY_EXTENTS_INVALID: {handle}/{entity.GetType().Name}: {ex.Message}", ex); }
            if (!double.IsFinite(extents.MinPoint.X) || !double.IsFinite(extents.MinPoint.Y) ||
                !double.IsFinite(extents.MaxPoint.X) || !double.IsFinite(extents.MaxPoint.Y))
                throw new InvalidOperationException("CAD_ENTITY_EXTENTS_INVALID: " + handle);
            if (entity is Curve curve && curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam) <= 0)
                throw new InvalidOperationException("CAD_ENTITY_ZERO_PATH: " + handle);
            if (allowedBounds is not null && (extents.MinPoint.X < allowedBounds.MinX + _insertion.X ||
                extents.MinPoint.Y < allowedBounds.MinY + _insertion.Y || extents.MaxPoint.X > allowedBounds.MaxX + _insertion.X ||
                extents.MaxPoint.Y > allowedBounds.MaxY + _insertion.Y))
                throw new InvalidOperationException($"CAD_ENTITY_CROPPED: {handle}; native=[{extents.MinPoint.X},{extents.MinPoint.Y},{extents.MaxPoint.X},{extents.MaxPoint.Y}]; allowed=[{allowedBounds.MinX + _insertion.X},{allowedBounds.MinY + _insertion.Y},{allowedBounds.MaxX + _insertion.X},{allowedBounds.MaxY + _insertion.Y}]");
        }
    }

    public void SetViewTransform(ViewTransform transform, Bounds2Mm logicalBounds)
    {
        _viewScale = transform.Scale;
        _viewRotation = transform.Rotated ? Math.PI / 2 : 0;
        var zero = transform.Apply(new Point2Mm(0, 0), logicalBounds);
        _viewMatrix = Matrix3d.Displacement(new Vector3d(zero.X + _insertion.X, zero.Y + _insertion.Y, 0)) *
            (transform.Rotated ? new Matrix3d(new double[] { 0,-1,0,0, 1,0,0,0, 0,0,1,0, 0,0,0,1 }) : Matrix3d.Identity) *
            Matrix3d.Scaling(transform.Scale, Point3d.Origin);
    }

    public CadPrimitiveSink(Database database, Transaction transaction, BlockTableRecord space, Point2Mm insertion,
        GenerationRepository? repository = null, string? generationId = null, string? inputHash = null, string? templateHash = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _space = space ?? throw new ArgumentNullException(nameof(space));
        _insertion = insertion;
        _viewMatrix = Matrix3d.Displacement(new Vector3d(insertion.X, insertion.Y, 0));
        if (repository is not null && generationId is not null && inputHash is not null && templateHash is not null)
            _tag = entity => repository.Tag(entity, generationId, inputHash, templateHash);
    }

    public bool HasBlock(string blockName)
    {
        var table = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
        return table.Has(blockName);
    }

    public OperationResult<TemplateManifest> ValidateAssets(TemplateManifest manifest, bool allowMissing = false) =>
        TemplateTargetValidator.Validate(manifest, TemplateManifestReader.ReadTargetAssets(_database, _transaction, manifest), allowMissing);

    public bool HasLayer(string layerName)
    {
        var table = (LayerTable)_transaction.GetObject(_database.LayerTableId, OpenMode.ForRead);
        if (table.Has(layerName)) return true;
        if (string.Equals(layerName, "VHG-Cong", StringComparison.OrdinalIgnoreCase))
        {
            table.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = "VHG-Cong",
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5)
            };
            table.Add(ltr);
            _transaction.AddNewlyCreatedDBObject(ltr, true);
            table.DowngradeOpen();
            return true;
        }
        return false;
    }

    public bool HasHatchPattern(string patternName)
    {
        try
        {
            var hatch = new Hatch();
            hatch.SetHatchPattern(HatchPatternType.PreDefined, patternName);
            hatch.Dispose();
            return true;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return false;
        }
    }

    public string AppendLine(Point2Mm start, Point2Mm end, CadPrimitiveStyle style)
    {
        var entity = new Line(ToPoint(start), ToPoint(end));
        Apply(entity, style);
        return Append(entity);
    }

    public string AppendPolyline(IReadOnlyList<Point2Mm> points, bool closed, CadPrimitiveStyle style, IReadOnlyList<double>? bulges = null)
    {
        return AppendPolylineObject(points, closed, style, bulges).Handle.ToString();
    }

    public string AppendSectionDot(Point2Mm center, double graphicDiameterMm, CadPrimitiveStyle style)
    {
        ContractGuard.Positive(graphicDiameterMm, nameof(graphicDiameterMm));
        // Published native dots: two semicircles, bulge=1, width=diameter.
        // Order matches the source (right endpoint, then left), not a polygon approximation.
        var entity = new Polyline();
        var radius = graphicDiameterMm / 2;
        entity.AddVertexAt(0, new Point2d(center.X + radius, center.Y), 1, 0, 0);
        entity.AddVertexAt(1, new Point2d(center.X - radius, center.Y), 1, 0, 0);
        entity.ConstantWidth=graphicDiameterMm;
        entity.Closed = true;
        Apply(entity, style);
        return Append(entity);
    }

    public string AppendLeader(IReadOnlyList<Point2Mm> points, CadPrimitiveStyle style)
    {
        var leader=new Leader();
        leader.SetDatabaseDefaults(_database);
        if(!string.IsNullOrWhiteSpace(style.DimensionStyle))
        {
            var table=(DimStyleTable)_transaction.GetObject(_database.DimStyleTableId,OpenMode.ForRead);
            if(!table.Has(style.DimensionStyle)) throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: DimStyle {style.DimensionStyle}");
            leader.DimensionStyle=table[style.DimensionStyle];
        }
        foreach(var point in points) leader.AppendVertex(ToPoint(point));
        leader.HasArrowHead=true;
        Apply(leader,style);
        return Append(leader);
    }

    public string AppendArc(Point2Mm center, double radiusMm, double startRadians, double sweepRadians, CadPrimitiveStyle style)
    {
        if (!double.IsFinite(radiusMm) || radiusMm <= 0) throw new ArgumentOutOfRangeException(nameof(radiusMm));
        var endRadians = startRadians + sweepRadians;
        if (!double.IsFinite(startRadians) || !double.IsFinite(sweepRadians) || sweepRadians == 0 || Math.Abs(sweepRadians) > 2 * Math.PI)
            throw new ArgumentOutOfRangeException(nameof(sweepRadians));
        // AutoCAD normalizes a full-turn Arc's end angle to its start; use a physical Circle.
        Entity entity = Math.Abs(Math.Abs(sweepRadians) - 2 * Math.PI) <= 1e-12
            ? new Circle(ToPoint(center), Vector3d.ZAxis, radiusMm)
            : sweepRadians > 0 ? new Arc(ToPoint(center), radiusMm, startRadians, endRadians)
                : new Arc(ToPoint(center), radiusMm, endRadians, startRadians);
        Apply(entity, style);
        return Append(entity);
    }

    public string AppendEllipse(Point2Mm center, double stationRadiusMm, double heightRadiusMm,
        double startRadians, double endRadians, CadPrimitiveStyle style)
    {
        ContractGuard.Positive(stationRadiusMm, nameof(stationRadiusMm));
        ContractGuard.Positive(heightRadiusMm, nameof(heightRadiusMm));
        ContractGuard.Finite(startRadians, nameof(startRadians));
        ContractGuard.Finite(endRadians, nameof(endRadians));
        if (endRadians == startRadians || Math.Abs(endRadians - startRadians) > 2 * Math.PI)
            throw new ArgumentException("Invalid ellipse sweep.");
        if (endRadians < startRadians) (startRadians, endRadians) = (endRadians, startRadians);
        bool vertical = heightRadiusMm > stationRadiusMm;
        var major = vertical ? new Vector3d(0, heightRadiusMm, 0) : new Vector3d(stationRadiusMm, 0, 0);
        var shift = vertical ? Math.PI / 2 : 0;
        var entity = new Ellipse(ToPoint(center), Vector3d.ZAxis, major,
            Math.Min(stationRadiusMm, heightRadiusMm) / Math.Max(stationRadiusMm, heightRadiusMm),
            startRadians - shift, endRadians - shift);
        if (entity.IsNull) { entity.Dispose(); throw new InvalidOperationException("CAD_ELLIPSE_INVALID"); }
        Apply(entity, style);
        return Append(entity);
    }

    public string AppendText(Point2Mm position, string content, double heightMm, double rotationRadians, CadPrimitiveStyle style)
    {
        var entity = new DBText
        {
            Position = ToPoint(position),
            TextString = content,
            Height = heightMm,
            Rotation = rotationRadians,
            Layer = style.EffectiveLayer,
            ColorIndex = style.ColorIndex
        };
        if (!string.IsNullOrWhiteSpace(style.TextStyle))
        {
            var table = (TextStyleTable)_transaction.GetObject(_database.TextStyleTableId, OpenMode.ForRead);
            if (!table.Has(style.TextStyle)) throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: TextStyle {style.TextStyle}");
            entity.TextStyleId = table[style.TextStyle];
            entity.WidthFactor = style.TextWidthFactor ?? ((TextStyleTableRecord)_transaction.GetObject(entity.TextStyleId, OpenMode.ForRead)).XScale;
        }
        if(style.TextRightAligned)
        {
            entity.HorizontalMode=TextHorizontalMode.TextRight;
            entity.AlignmentPoint=ToPoint(position);
        }
        else if(style.TextMidCenter)
        {
            entity.HorizontalMode=TextHorizontalMode.TextMid;
            entity.VerticalMode=TextVerticalMode.TextVerticalMid;
            entity.AlignmentPoint=ToPoint(position);
        }
        else if(style.TextMidBase)
        {
            entity.HorizontalMode=TextHorizontalMode.TextMid;
            entity.VerticalMode=TextVerticalMode.TextBase;
            entity.AlignmentPoint=ToPoint(position);
        }
        var handle=Append(entity);
        if(style.TextRightAligned || style.TextMidCenter || style.TextMidBase) entity.AdjustAlignment(_database);
        return handle;
    }

    public string AppendMText(Point2Mm position, string content, double heightMm, double rotationRadians, CadPrimitiveStyle style)
    {
        var entity = new MText
        {
            Location = ToPoint(position),
            Contents = content,
            TextHeight = heightMm,
            Rotation = rotationRadians,
            Attachment = AttachmentPoint.MiddleCenter,
            Layer = style.EffectiveLayer,
            ColorIndex = style.ColorIndex
        };
        if (!string.IsNullOrWhiteSpace(style.TextStyle))
        {
            var table = (TextStyleTable)_transaction.GetObject(_database.TextStyleTableId, OpenMode.ForRead);
            if (!table.Has(style.TextStyle)) throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: TextStyle {style.TextStyle}");
            entity.TextStyleId = table[style.TextStyle];
        }
        return Append(entity);
    }

    public string AppendDimension(Point2Mm start, Point2Mm end, double logicalMeasurementMm, double offsetMm, CadPrimitiveStyle style, double? rotationRadians = null, Point2Mm? dimensionLinePoint = null, string? textOverride = null)
    {
        var first = ToPoint(start);
        var second = ToPoint(end);
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        if (Math.Abs(dx) <= 1e-12 && Math.Abs(dy) <= 1e-12) throw new ArgumentException("Dimension endpoints must differ.");
        var length = Math.Sqrt(dx * dx + dy * dy);
        var nx = -dy / length;
        var ny = dx / length;
        var midpoint = new Point3d((first.X + second.X) / 2 + nx * offsetMm, (first.Y + second.Y) / 2 + ny * offsetMm, 0);
        var styleId = _database.Dimstyle;
        if (!string.IsNullOrWhiteSpace(style.DimensionStyle))
        {
            var table = (DimStyleTable)_transaction.GetObject(_database.DimStyleTableId, OpenMode.ForRead);
            if (!table.Has(style.DimensionStyle)) throw new InvalidOperationException($"TEMPLATE_ASSET_MISSING: DimStyle {style.DimensionStyle}");
            styleId = table[style.DimensionStyle];
        }
        // The Sheet2 template stores automatic measurement text as an empty override.
        // Preserve compound n*p=<> expressions and every other caller's text policy.
        if (style.DimensionTextStyle is not null && textOverride == "<>") textOverride = string.Empty;
        var entity = new RotatedDimension(rotationRadians ?? Math.Atan2(dy, dx), first, second,
            dimensionLinePoint is { } linePoint ? ToPoint(linePoint) : midpoint,
            textOverride ?? (_viewScale == 1 ? string.Empty : logicalMeasurementMm.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)), styleId);
        if(style.DimensionTextStyle is not null)
        {
            var styles=(TextStyleTable)_transaction.GetObject(_database.TextStyleTableId,OpenMode.ForRead);
            if(!styles.Has(style.DimensionTextStyle)) throw new InvalidOperationException("TEMPLATE_ASSET_MISSING: TextStyle "+style.DimensionTextStyle);
            using var overrides=entity.GetDimstyleData();
            var original=(TextStyleTableRecord)_transaction.GetObject(overrides.Dimtxsty,OpenMode.ForRead);
            string name="DBTool.SheetTwo.Dimension."+style.DimensionTextStyle;
            if(!styles.Has(name))
            {
                var font=(TextStyleTableRecord)_transaction.GetObject(styles[style.DimensionTextStyle],OpenMode.ForRead);
                var isolated=(TextStyleTableRecord)font.Clone();
                isolated.Name=name;isolated.XScale=original.XScale;
                styles.UpgradeOpen();styles.Add(isolated);_transaction.AddNewlyCreatedDBObject(isolated,true);
            }
            // Preserve native VHG1-30 and its widths/settings; override only this new
            // dimension's font, leaving the shared Sheet1 style records untouched.
            overrides.Dimtxsty=styles[name];
            entity.SetDimstyleData(overrides);
        }
        Apply(entity, style);
        if (style.DimensionTextPosition is {} textPosition)
            entity.TextPosition = ToPoint(textPosition);
        var handle = Append(entity);
        entity.RecomputeDimensionBlock(true);
        return handle;
    }

    public string AppendDiameterDimension(Point2Mm center,double diameterMm,CadPrimitiveStyle style)
    {
        ContractGuard.Positive(diameterMm,nameof(diameterMm));
        var styles=(DimStyleTable)_transaction.GetObject(_database.DimStyleTableId,OpenMode.ForRead);
        if(style.DimensionStyle is null || !styles.Has(style.DimensionStyle))
            throw new InvalidOperationException("TEMPLATE_ASSET_MISSING: native pipe dimension style");
        var entity=new DiametricDimension(ToPoint(new(center.X-diameterMm/2,center.Y)),
            ToPoint(new(center.X+diameterMm/2,center.Y)),0,string.Empty,styles[style.DimensionStyle]);
        entity.TextPosition=ToPoint(center);
        Apply(entity,style);
        var handle=Append(entity);
        entity.RecomputeDimensionBlock(true);
        return handle;
    }

    public CadHatchResult AppendHatch(IReadOnlyList<Point2Mm> outerLoop, IReadOnlyList<IReadOnlyList<Point2Mm>> innerLoops, CadPrimitiveStyle style)
    {
        var boundaries = new List<ObjectId>();
        if (!style.NativeBoundaryLoops)
        {
            boundaries.Add(AppendPolylineObject(outerLoop, true, style));
            foreach (var inner in innerLoops) boundaries.Add(AppendPolylineObject(inner, true, style));
        }

        var hatch = new Hatch();
        Apply(hatch, style);
        var pattern = string.IsNullOrWhiteSpace(style.HatchPattern) ? "SOLID" : style.HatchPattern;
        hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
        if (style.HatchScale <= 0 || !double.IsFinite(style.HatchScale))
            throw new ArgumentOutOfRangeException(nameof(style.HatchScale));
        hatch.PatternScale = style.HatchScale * _viewScale;
        hatch.PatternAngle = _viewRotation + style.HatchAngleRadians;
        var localOrigin=style.HatchOrigin ?? new Point2Mm(0,0);
        var patternOrigin = new Point3d(localOrigin.X,localOrigin.Y,0).TransformBy(_viewMatrix);
        hatch.Origin = new Point2d(patternOrigin.X, patternOrigin.Y);
        // Refresh pattern line definitions after changing scale/angle/origin (AutoCAD hatch API contract).
        hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
        // Loops already refer to transformed WCS entities. Do not transform the empty hatch again.
        Append(hatch, transform: false);
        if (style.NativeBoundaryLoops)
        {
            AppendCoordinateLoop(outerLoop, HatchLoopTypes.Outermost);
            foreach (var inner in innerLoops) AppendCoordinateLoop(inner, HatchLoopTypes.Default);
        }
        else
        {
            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { boundaries[0] });
            for (var index = 1; index < boundaries.Count; index++)
                hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { boundaries[index] });
        }
        hatch.EvaluateHatch(true);
        // Resolve native loops before hiding their owned construction entities.
        foreach (var id in boundaries)
            ((Entity)_transaction.GetObject(id, OpenMode.ForWrite)).Visible = false;
        return new CadHatchResult(Handle(hatch), boundaries.Select(id => id.Handle.ToString()).ToArray());

        void AppendCoordinateLoop(IReadOnlyList<Point2Mm> loop, HatchLoopTypes type)
        {
            var vertices = new Point2dCollection();
            var bulges = new DoubleCollection();
            foreach (var point in loop)
            {
                var world = ToPoint(point).TransformBy(_viewMatrix);
                vertices.Add(new Point2d(world.X, world.Y));
                bulges.Add(0);
            }
            // Coordinate loops have no Polyline.Closed property: include the closing edge.
            if (loop[0] != loop[^1])
            {
                vertices.Add(vertices[0]);
                bulges.Add(0);
            }
            hatch.AppendLoop(type, vertices, bulges);
        }
    }

    public CadBlockResult AppendBlock(string blockName, Point2Mm insertion, double annotationScale, IReadOnlyDictionary<string, string> attributes, CadPrimitiveStyle style, double rotationRadians = 0)
    {
        var table = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
        if (!table.Has(blockName)) throw new InvalidOperationException($"Block {blockName} is missing.");
        var definition = (BlockTableRecord)_transaction.GetObject(table[blockName], OpenMode.ForRead);
        var reference = new BlockReference(ToPoint(insertion), definition.ObjectId)
        {
            ScaleFactors = new Scale3d(style.MirrorBlockHorizontal?-annotationScale:annotationScale,annotationScale,annotationScale),
            Rotation = rotationRadians,
            Layer = style.EffectiveLayer,
            ColorIndex = style.ColorIndex
        };
        var blockHandle = Append(reference);
        var childHandles = BlockAttributeWriter.Write(_transaction, definition, reference, attributes, _tag,
            style.AttributeTextStyle,style.FitMarkToCircle);
        return new CadBlockResult(blockHandle, childHandles);
    }

    private Point3d ToPoint(Point2Mm point) => new(point.X, point.Y, 0);

    private string Append(Entity entity, bool transform = true)
    {
        if (transform) entity.TransformBy(_viewMatrix);
        _tag?.Invoke(entity);
        _space.AppendEntity(entity);
        _transaction.AddNewlyCreatedDBObject(entity, true);
        return Handle(entity);
    }

    private ObjectId AppendPolylineObject(IReadOnlyList<Point2Mm> points, bool closed, CadPrimitiveStyle style, IReadOnlyList<double>? bulges = null)
    {
        if (points.Count < (closed ? 3 : 2)) throw new ArgumentException("A polyline has too few points.", nameof(points));
        var entity = new Polyline();
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var bulge = (bulges is not null && index < bulges.Count) ? bulges[index] : 0.0;
            entity.AddVertexAt(index, new Point2d(point.X, point.Y), bulge, 0, 0);
        }
        entity.Closed = closed;
        entity.TransformBy(_viewMatrix);
        Apply(entity, style);
        _tag?.Invoke(entity);
        _space.AppendEntity(entity);
        _transaction.AddNewlyCreatedDBObject(entity, true);
        return entity.ObjectId;
    }

    private static string Handle(DBObject entity) => entity.ObjectId.Handle.ToString();

    private static void Apply(Entity entity, CadPrimitiveStyle style)
    {
        entity.Layer = style.EffectiveLayer;
        entity.ColorIndex = style.ColorIndex;
        entity.LinetypeScale = style.LinetypeScale;
        if (!string.IsNullOrWhiteSpace(style.Linetype)) entity.Linetype = style.Linetype;
    }
}
