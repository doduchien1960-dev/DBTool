using System.Collections.Immutable;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using DBTool.AutoCAD.Template;

namespace DBTool.AutoCAD.Render;

/// <summary>Write-only adapter from immutable DrawingPackage primitives to an existing CAD transaction.</summary>
public sealed class CadRenderer
{
    public OperationResult<RenderedEntityMap> Render(
        DrawingPackage package,
        ValidatedTemplateManifest template,
        CadWriteContext context, SheetLayout? layout = null, double plotDenominator = 30, double sheetPitchMm = 12600)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);
        return Render(package, template, context.Sink, layout, plotDenominator, sheetPitchMm);
    }

    public OperationResult<RenderedEntityMap> Render(DrawingPackage package, ValidatedTemplateManifest template,
        ICadPrimitiveSink sink, SheetLayout? layout = null, double plotDenominator = 30, double sheetPitchMm = 12600)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(sink);
        if (!template.CanRender) return Block("TEMPLATE_NOT_RENDERABLE", "", "Template validation does not authorize rendering.");
        if (package.Primitives.IsEmpty) return Block("DRAWING_OUTPUT_EMPTY", "", "An empty package cannot create a CAD generation.");
        var valid = DrawingPackageValidator.ValidatePrimitives(package.Views, package.Primitives);
        if (!valid.Success) return new(false, null, valid.Diagnostics);

        var preflight = Preflight(package, template, sink);
        if (!preflight.Success) return new(false, null, preflight.Diagnostics);

        var rendered = new List<RenderedEntity>();
        var concreteAssembly=SheetOneConcreteAssembly.Build(package.Primitives);
        var aliases=SheetOneRebarDotPolicy.RenderAliases(package.Primitives).Concat(WallMeshBandPolicy.RenderAliases(package.Primitives))
            .Concat(WallMeshGuidePolicy.RenderAliases(package.Primitives))
            .Concat(SheetOnePlanHiddenPolicy.RenderAliases(package.Primitives))
            .Concat(SheetOneContinuousWallDots.RenderAliases(package.Primitives))
            .Concat(concreteAssembly.Aliases)
            .ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var renderedHandles=new Dictionary<string,IReadOnlyList<string>>(StringComparer.Ordinal);
        string currentSemanticId = "";
        try
        {
            foreach (var primitive in package.Primitives)
            {
                currentSemanticId = primitive.SemanticId;
                var view = package.Views.Single(v => v.Id == primitive.ViewId);
                if (view.Bounds is null) return Block("LAYOUT_VIEW_BOUNDS_MISSING", view.Id, "View placement needs local bounds.");
                var transform = new ViewTransform(view.Origin, 1);
                Bounds2Mm? allowed = null;
                if (layout is not null)
                {
                    var matches = layout.Sheets.SelectMany(sheet => sheet.Frames.Where(frame => frame.ViewId == view.Id)
                        .Select(frame => (sheet, frame))).ToArray();
                    if (matches.Length != 1) return Block("LAYOUT_VIEW_UNRESOLVED", view.Id, "Each view needs exactly one layout frame.");
                    var (sheet, frame) = matches[0];
                    if (frame.LogicalBounds != view.Bounds)
                        return Block("LAYOUT_BOUNDS_MISMATCH", view.Id, "Frame logical bounds differ from its view.");
                    var pitch = (sheet.PageIndex - 1) * sheetPitchMm;
                    transform = frame.Transform.ToModel(plotDenominator, pitch);
                    allowed = new Bounds2Mm(frame.PaperBounds.MinX * plotDenominator + pitch,
                        frame.PaperBounds.MinY * plotDenominator, frame.PaperBounds.MaxX * plotDenominator + pitch,
                        frame.PaperBounds.MaxY * plotDenominator);
                }
                sink.SetViewTransform(transform, view.Bounds);
                // Coincident first-sheet diagram symbols share native ink, but each
                // semantic/occurrence ID retains a handle mapping in the artifact.
                var handles=aliases.TryGetValue(primitive.SemanticId,out var representative)
                    ? renderedHandles[representative] : concreteAssembly.Paths.TryGetValue(primitive.SemanticId,out var exposedPaths)
                        ? exposedPaths.Select(path=>sink.AppendPolyline(path,false,ResolveStyle(template.Manifest,primitive).Style)).ToArray()
                        : RenderPrimitive(primitive, template, sink);
                if (handles.Count == 0)
                    return Block("CAD_PRIMITIVE_EMPTY", primitive.SemanticId, "The primitive produced no entity.");
                sink.ValidateEntityOutput(handles, allowed);
                rendered.Add(new RenderedEntity(primitive.SemanticId, handles[0], handles.Skip(1)));
                renderedHandles.Add(primitive.SemanticId,handles);
            }

            return new(true, new RenderedEntityMap(rendered), []);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return Block("CAD_RENDER_FAILED", currentSemanticId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Block("CAD_RENDER_FAILED", currentSemanticId, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Block("CAD_RENDER_FAILED", currentSemanticId, ex.Message);
        }
    }

    private static OperationResult<RenderedEntityMap> Preflight(
        DrawingPackage package,
        ValidatedTemplateManifest template,
        ICadPrimitiveSink sink)
    {
        var diagnostics = new List<Diagnostic>();
        if (template.Manifest.SchemaVersion == "template-manifest-v2")
        {
            diagnostics.AddRange(sink.ValidateAssets(TemplateAssetImporter.Importable(template.Manifest)).Diagnostics);
            diagnostics.AddRange(sink.ValidateAssets(TemplateAssetImporter.DependencyContract(template.Manifest), allowMissing: true).Diagnostics);
        }
        foreach (var primitive in package.Primitives)
        {
            if (primitive is CirclePrimitive { Role: "Rebar/Main" })
            {
                var dot = FindBlockAsset(template.Manifest, "Block:NotThep");
                if (dot is null || !sink.HasBlock(dot.Name))
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_BLOCK_MISSING", primitive.SemanticId,
                        "NotThep native solid-filled section symbol is required."));
            }
            if (SemanticRoleRegistry.Resolve(primitive.Role) is null &&
                !template.Manifest.Assets.Any(a => a.AssetKind == AssetKind.Layer &&
                    (a.Role == $"Layer:{primitive.Role}" || a.Name == primitive.Role)))
                diagnostics.Add(BlockDiagnostic("CAD_SEMANTIC_ROLE_UNRESOLVED", primitive.SemanticId, $"No native style mapping for {primitive.Role}."));
            if (primitive is BlockPrimitive block)
            {
                var asset = FindBlockAsset(template.Manifest, block.BlockRole);
                if (asset is null)
                {
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_BLOCK_MISSING", block.SemanticId,
                        $"Required block role {block.BlockRole} is absent."));
                    continue;
                }
                if (!sink.HasBlock(asset.Name))
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_BLOCK_MISSING", block.SemanticId,
                        $"Block definition {asset.Name} is not present in the target drawing."));
                if (asset.Required)
                {
                    var supplied = block.Attributes.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var tag in asset.AttributeTags)
                        if (!supplied.Contains(tag))
                            diagnostics.Add(BlockDiagnostic("TEMPLATE_TAG_MISSING", block.SemanticId,
                                $"Block {asset.Name} requires attribute tag {tag}."));
                }
                if (block.BlockRole == "RoundOpeningReinforcement" && ShapeForAsset(template.Manifest, asset) != TemplateShapeKind.RoundRing)
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_ROLE_SHAPE_MISMATCH", block.SemanticId,
                        "Round opening block does not match the inspected round-ring role."));
                if (block.BlockRole == "RectangularOpeningReinforcement" && ShapeForAsset(template.Manifest, asset) != TemplateShapeKind.RectangularFourBar)
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_ROLE_SHAPE_MISMATCH", block.SemanticId,
                        "Rectangular opening block does not match the inspected rectangular role."));
            }

            if(primitive is TableRowPrimitive {MaterialKind:DrawingMaterialKind.AngleSteel} angle)
            {
                var asset=FindBlockAsset(template.Manifest,"AngleScheduleRow") ?? FindBlockAsset(template.Manifest,ScheduleNativeAttributes.AngleBlockName);
                if(asset is null || !sink.HasBlock(asset.Name))
                    diagnostics.Add(BlockDiagnostic("TEMPLATE_BLOCK_MISSING",angle.SemanticId,"TK.TG required for angle steel."));
                else foreach(var tag in new[]{"SH","L","D","DAI","SL","TDAI","KL"})
                    if(!asset.AttributeTags.Contains(tag,StringComparer.OrdinalIgnoreCase))
                        diagnostics.Add(BlockDiagnostic("TEMPLATE_TAG_MISSING",angle.SemanticId,"TK.TG is missing "+tag));
            }

            var style = ResolveStyle(template.Manifest, primitive);
            if (style.RequiredLayer && !sink.HasLayer(style.Layer))
                diagnostics.Add(BlockDiagnostic("TEMPLATE_LAYER_MISSING", primitive.SemanticId,
                    $"Required layer {style.Layer} is not present in the target drawing."));
            if (primitive is HatchPrimitive && style.RequiredPattern && !sink.HasHatchPattern(style.HatchPattern!))
                diagnostics.Add(BlockDiagnostic("TEMPLATE_HATCH_MISSING", primitive.SemanticId,
                    $"Required hatch pattern {style.HatchPattern} is not present in the target drawing."));
        }

        return diagnostics.Count == 0
            ? new(true, new RenderedEntityMap([]), [])
            : new(false, null, diagnostics);
    }

    private static IReadOnlyList<string> RenderPrimitive(DrawingPrimitive primitive, ValidatedTemplateManifest template, ICadPrimitiveSink sink)
    {
        var style = ResolveStyle(template.Manifest, primitive).Style;
        return primitive switch
        {
            PolylinePrimitive { Role: "Rebar/Leader" or "Material/Leader" } leader => [sink.AppendLeader(leader.Points, style)],
            PolylinePrimitive { Role: SheetOnePlanBaseOutlinePolicy.Role } outline => SheetOnePlanBaseOutlinePolicy.NativeEdges(outline)
                .Select(line=>sink.AppendLine(line.Start,line.End,style)).ToArray(),
            PolylinePrimitive { Role: WallMeshGuidePolicy.Role } guide when guide.Points.Length==2 => [sink.AppendLine(guide.Points[0],guide.Points[1],style)],
            PolylinePrimitive { Role: SheetTwoMortar.BoundaryRole } boundary when boundary.Points.Length==2 => [sink.AppendLine(boundary.Points[0],boundary.Points[1],style)],
            PolylinePrimitive { Role: SheetTwoMaterialViews.WallRole } wall =>
                [sink.AppendPolyline(wall.Points.Append(wall.Points[0]).ToArray(),true,style)],
            PolylinePrimitive nominal when nominal.Role is SheetTwoNominalBaseMesh.Role or SheetTwoNominalWallMesh.Role or SheetTwoNominalWallMesh.EdgeRole or SheetTwoAdjacentSlabSteel.Role or SheetTwoNominalAnchorage.Role && nominal.Points.Length==2 =>
                [sink.AppendLine(nominal.Points[0],nominal.Points[1],style)],
            PolylinePrimitive polyline => [sink.AppendPolyline(polyline.Points, polyline.Closed, style, polyline.Bulges.IsDefaultOrEmpty ? null : polyline.Bulges)],
            CirclePrimitive { Role: SheetOneRebarDotPolicy.Role or SheetOneContinuousWallDots.Role } dot =>
                [sink.AppendSectionDot(dot.Center, SheetOneRebarDotPolicy.GraphicDiameterMm, style)],
            CirclePrimitive { Role: "Rebar/Main" } dot => RenderRebarDot(dot, template.Manifest, sink, style),
            CirclePrimitive circle => [sink.AppendArc(circle.Center, circle.RadiusMm, 0, 2 * Math.PI, style)],
            TextPrimitive text => primitive.Role == "Rebar/BendingMark"
                ? [sink.AppendMText(text.Position, text.Content, text.HeightMm, text.RotationRadians, style)]
                : [sink.AppendText(text.Position, text.Content, text.HeightMm, text.RotationRadians, style)],
            DimensionIntentPrimitive dimension => [sink.AppendDimension(dimension.Start, dimension.End, dimension.LogicalMeasurementMm, dimension.OffsetMm, style with { DimensionTextPosition=SheetTwoDimensionTextPlacement.Position(dimension) }, dimension.RotationRadians, dimension.DimensionLinePoint, dimension.Label.Contains("<>", StringComparison.Ordinal) ? dimension.Label : null)],
            DiameterDimensionPrimitive diameter => [sink.AppendDiameterDimension(diameter.Center,diameter.DiameterMm,style)],
            PathPrimitive path => RenderPath(path, sink, style),
            SectionBoundaryPrimitive boundary => RenderBoundary(boundary.Boundary, sink, style),
            HatchPrimitive hatch => RenderHatch(hatch, sink, style),
            BlockPrimitive block => RenderBlock(block, template.Manifest, sink, style),
            TableRowPrimitive row => RenderTableRow(row, template.Manifest, sink, style),
            _ => throw new InvalidOperationException($"Unsupported drawing primitive {primitive.GetType().Name}.")
        };
    }

    private static IReadOnlyList<string> RenderRebarDot(CirclePrimitive dot, TemplateManifest manifest,
        ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var asset = FindBlockAsset(manifest, "Block:NotThep")
            ?? throw new InvalidOperationException("NotThep native solid-filled section symbol is required.");
        // Original VHG NotThep is centred at zero with radius 50 model mm.
        // Keep physical diameter/identity; source first-sheet dots use a fixed 9mm graphic diameter.
        var result = sink.AppendBlock(asset.Name, dot.Center, VhgPresentationStandards.RebarDotInsertionScale(dot.ViewId,dot.RadiusMm),
            new Dictionary<string, string>(), style);
        return [result.BlockHandle, .. result.AttributeHandles];
    }

    private static IReadOnlyList<string> RenderPath(PathPrimitive path, ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var handles = new List<string>();
        foreach (var segment in path.Segments)
        {
            handles.Add(segment switch
            {
                LinePathSegment line => sink.AppendLine(line.Start, line.End, style),
                ArcPathSegment arc => sink.AppendArc(arc.Center, arc.RadiusMm, arc.StartRadians, arc.SweepRadians, style),
                _ => throw new InvalidOperationException("Unsupported path segment.")
            });
        }
        return handles;
    }

    private static IReadOnlyList<string> RenderBoundary(SectionBoundaryLoop boundary, ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var handles = new List<string>();
        foreach (var edge in boundary.Edges)
        {
            handles.Add(edge switch
            {
                SectionLineEdge line => sink.AppendLine(line.Start, line.End, style),
                SectionEllipseEdge ellipse => sink.AppendEllipse(ellipse.Center, ellipse.StationRadiusMm,
                    ellipse.HeightRadiusMm, ellipse.StartRadians, ellipse.EndRadians, style),
                _ => throw new InvalidOperationException("Unsupported section boundary edge.")
            });
        }
        return handles;
    }

    private static IReadOnlyList<string> RenderHatch(HatchPrimitive hatch, ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var outer = EnsureCounterClockwise(hatch.OuterLoop).ToArray();
        var holes = hatch.InnerLoops
            .Select(loop => (IReadOnlyList<Point2Mm>)EnsureClockwise(loop).ToArray())
            .ToArray();
        var result = sink.AppendHatch(outer, holes, style with
            { HatchOrigin=hatch.PatternOrigin, NativeBoundaryLoops=hatch.NativeBoundaryLoops });
        return [result.HatchHandle, .. result.BoundaryHandles];
    }

    private static IReadOnlyList<string> RenderBlock(BlockPrimitive block, TemplateManifest manifest, ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var asset = FindBlockAsset(manifest, block.BlockRole)
            ?? throw new InvalidOperationException($"Block role {block.BlockRole} is missing.");
        var attributes = block.Attributes.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var result = sink.AppendBlock(asset.Name, block.Insertion, block.AnnotationScale, attributes,
            style with { MirrorBlockHorizontal=block.MirrorHorizontal }, block.RotationRadians);
        return [result.BlockHandle, .. result.AttributeHandles];
    }

    private static IReadOnlyList<string> RenderTableRow(TableRowPrimitive row, TemplateManifest manifest, ICadPrimitiveSink sink, CadPrimitiveStyle style)
    {
        var handles = new List<string>();
        if (row.Placements.Length != row.Cells.Length || row.Cells.IsEmpty)
            throw new InvalidOperationException("CAD_TABLE_LAYOUT_MISSING: table cells need explicit coordinates.");
        if(row.MaterialKind==DrawingMaterialKind.AngleSteel)
        {
            var asset=FindBlockAsset(manifest,"AngleScheduleRow") ?? FindBlockAsset(manifest,ScheduleNativeAttributes.AngleBlockName)
                ?? throw new InvalidOperationException("TEMPLATE_BLOCK_MISSING: TK.TG required for angle steel.");
            var cells=row.Cells.ToDictionary(c=>c.ColumnKey);
            double N(string key)=>cells[key].Number!.Value;
            var attributes=ScheduleNativeAttributes.Angle(row.Mark,cells["CatalogCode"].Text!,N("Quantity"),N("SingleLengthMm"),N("TotalLengthMm"),N("UnitMassKgPerM"),N("MassKg"))
                .ToDictionary(a=>a.Key,a=>a.Value,StringComparer.OrdinalIgnoreCase);
            var first=row.Placements[0];
            // Native TK.TG anchor is its upper-left corner, 100 mm high at insertion scale 0.2.
            var block=sink.AppendBlock(asset.Name,new(first.Position.X-first.HeightMm/2,first.Position.Y+first.HeightMm*1.5),
                first.HeightMm/60*0.2,attributes,style);
            return [block.BlockHandle,..block.AttributeHandles];
        }

        if (row.Role == "Schedule/Row" && !string.IsNullOrEmpty(row.ScheduleMark))
        {
            var schedAsset = FindBlockAsset(manifest, row.ScheduleMark) ?? FindBlockAsset(manifest, $"ScheduleRow/{row.Mark}");
            if (schedAsset != null && sink.HasBlock(schedAsset.Name))
            {
                var cells = row.Cells.ToDictionary(c => c.ColumnKey);
                double singleLen = cells.TryGetValue("SingleLengthMm", out var slCell) && slCell.Number.HasValue ? slCell.Number.Value :
                                   cells.TryGetValue("LengthMm", out var lCell) && lCell.Number.HasValue ? lCell.Number.Value : 0;
                double qty = cells.TryGetValue("Quantity", out var qCell) && qCell.Number.HasValue ? qCell.Number.Value : 0;
                double totalLen = cells.TryGetValue("TotalLengthMm", out var tlCell) && tlCell.Number.HasValue ? tlCell.Number.Value : 0;
                double mass = cells.TryGetValue("MassKg", out var mCell) && mCell.Number.HasValue ? mCell.Number.Value : 0;
                double diam = cells.TryGetValue("DiameterMm", out var dCell) && dCell.Number.HasValue ? dCell.Number.Value : 0;

                var attributes = ScheduleNativeAttributes.BindScheduleRow(
                    schedAsset.Name, row.Mark, diam, singleLen, qty, totalLen, mass)
                    .ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);

                var first = row.Placements[0];
                var block = sink.AppendBlock(schedAsset.Name, new(first.Position.X, first.Position.Y),
                    first.HeightMm / 60 * 0.2, attributes, style);
                return [block.BlockHandle, .. block.AttributeHandles];
            }
        }

        for (int index = 0; index < row.Cells.Length; index++)
        {
            var cell = row.Cells[index];
            var placement = row.Placements[index];
            var text = cell.Kind == TableCellKind.Text
                ? cell.Text!
                : (cell.Number!.Value % 1 == 0
                    ? cell.Number.Value.ToString("0", CultureInfo.InvariantCulture)
                    : cell.Number.Value.ToString("0.00", CultureInfo.InvariantCulture));
            handles.Add(sink.AppendText(placement.Position, text, placement.HeightMm, 0, style));
        }
        return handles;
    }

    private static TemplateAsset? FindBlockAsset(TemplateManifest manifest, string role) =>
        manifest.Assets.FirstOrDefault(x => x.AssetKind == AssetKind.Block &&
            (string.Equals(x.Role, role, StringComparison.Ordinal) || string.Equals(x.Name, role, StringComparison.Ordinal)));

    private static TemplateShapeKind ShapeForAsset(TemplateManifest manifest, TemplateAsset asset) =>
        manifest.Inventory?.Blocks.FirstOrDefault(x => string.Equals(x.Name, asset.Name, StringComparison.Ordinal))?.Geometry.ShapeKind
        ?? asset.Role switch
        {
            "RoundOpeningReinforcement" => TemplateShapeKind.RoundRing,
            "RectangularOpeningReinforcement" => TemplateShapeKind.RectangularFourBar,
            _ => TemplateShapeKind.Unknown
        };

    private static (CadPrimitiveStyle Style, bool RequiredLayer, bool RequiredPattern, string Layer, string? HatchPattern) ResolveStyle(TemplateManifest manifest, DrawingPrimitive primitive)
    {
        var layerAsset = manifest.Assets.FirstOrDefault(x => x.AssetKind == AssetKind.Layer &&
            (string.Equals(x.Role, $"Layer:{primitive.Role}", StringComparison.Ordinal) ||
             string.Equals(x.Name, primitive.Role, StringComparison.Ordinal)));
        var semantic = SemanticRoleRegistry.Resolve(primitive.Role);
        var layer = layerAsset?.Name ?? semantic?.Layer ?? "0";
        var materialHatch = primitive is HatchPrimitive ? SectionMaterialPolicy.ForRole(primitive.Role) : null;
        var hatchAsset = primitive is HatchPrimitive
            ? manifest.Assets.FirstOrDefault(x => x.AssetKind == AssetKind.Hatch &&
                (materialHatch is not null ? string.Equals(x.Name, materialHatch.Pattern, StringComparison.Ordinal) :
                    string.Equals(x.Role, $"Hatch:{primitive.Role}", StringComparison.Ordinal)))
            : null;
        var pattern = materialHatch?.Pattern ?? hatchAsset?.Name ?? "SOLID";
        var style = new CadPrimitiveStyle(layer, ColorIndex: 256, Linetype: "ByLayer", HatchScale: materialHatch?.Scale ?? 1, HatchPattern: pattern,
            DimensionStyle: semantic?.DimStyle, TextStyle: semantic?.TextStyle,
            TextWidthFactor: primitive is TextPrimitive && (primitive.ViewId is "Plan" or "AA" or "BB" or "SteelAA" or "SteelBB" or "WallMeshPlan" or "Bending/Details") ? .85 : null,
            LinetypeScale: primitive.Role=="Geometry/PlanPipeCenter" ? 6 :
                (primitive.ViewId == "WallMeshPlan" && (primitive.Role is "Rebar/Hidden" or "Rebar/HiddenSecondary" or WallMeshBandPolicy.Role or WallMeshGuidePolicy.Role)) ||
                primitive.ViewId=="Plan" && (primitive.Role is "Geometry/PlanHidden" or "Geometry/PlanPipeHidden" or SheetOnePlanBaseOutlinePolicy.Role or SheetOnePlanOutlinePolicy.Role or SheetOnePlanHiddenPolicy.WallRole or SheetOnePlanHiddenPolicy.HoleRole) || primitive.Role is "Geometry/SectionHidden" or SheetOneSectionOutlinePolicy.Role or SheetTwoMaterialViews.WallRole or SheetTwoMaterialViews.RoofRole or SheetTwoHiddenEdges.Role ? 120 : 1,
            HatchAngleRadians: materialHatch is not null && (primitive.Role==SheetTwoMortar.Role || primitive.ViewId is "AA" or "BB" or "SteelAA" or "SteelBB") ? Math.PI : 0,
            TextRightAligned: primitive.Role=="Rebar/MarkRight",
            TextMidBase: primitive.Role == "Dimension/Bending",
            TextMidCenter: primitive.Role == "Rebar/BendingMark",
            AttributeTextStyle: primitive.Role==SheetTwoBarAnnotations.Role?semantic?.TextStyle:null,
            FitMarkToCircle: primitive.Role==SheetTwoBarAnnotations.Role,
            DimensionTextStyle: primitive.Role==SheetTwoDimensionPlanner.Role?semantic?.TextStyle:null);
        return (style, materialHatch is not null || semantic is not null || layerAsset?.Required == true,
            materialHatch is not null || hatchAsset?.Required == true, layer, pattern);
    }

    private static ImmutableArray<Point2Mm> EnsureCounterClockwise(IEnumerable<Point2Mm> points)
    {
        var array = points.ToImmutableArray();
        return SignedArea(array) < 0 ? array.Reverse().ToImmutableArray() : array;
    }

    private static ImmutableArray<Point2Mm> EnsureClockwise(IEnumerable<Point2Mm> points)
    {
        var array = points.ToImmutableArray();
        return SignedArea(array) > 0 ? array.Reverse().ToImmutableArray() : array;
    }

    private static double SignedArea(IReadOnlyList<Point2Mm> points)
    {
        var area = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var next = points[(index + 1) % points.Count];
            area += points[index].X * next.Y - next.X * points[index].Y;
        }
        return area / 2;
    }

    private static Diagnostic BlockDiagnostic(string code, string semanticId, string message) =>
        new(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null, semanticId, null, message);

    private static OperationResult<RenderedEntityMap> Block(string code, string semanticId, string message) =>
        new(false, null, [BlockDiagnostic(code, semanticId, message)]);
}
