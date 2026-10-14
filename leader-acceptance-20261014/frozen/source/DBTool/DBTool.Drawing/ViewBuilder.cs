using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Builds immutable semantic drawing data without CAD objects or quantity recalculation.</summary>
public static class ViewBuilder
{
    /// <summary>Locked S1 overload. It retains the original six-view primitive projection.</summary>
    public static OperationResult<DrawingPackage> Build(GeometryModel geometry, RebarLayout layout, SteelSchedule schedule,
        PresentationRequest presentation, PackageIdentity identity) =>
        BuildCore(new DrawingInputs(geometry, layout, schedule, null, DrawingQuantitySnapshot.NotCalculated),
            presentation, identity, false);

    /// <summary>Rich additive drawing boundary for geometry, rebar, schedules and quantity snapshots.</summary>
    public static OperationResult<DrawingPackage> Build(DrawingInputs inputs, PresentationRequest presentation,
        PackageIdentity identity) => BuildCore(inputs, presentation, identity, true);

    private static OperationResult<DrawingPackage> BuildCore(DrawingInputs inputs, PresentationRequest presentation,
        PackageIdentity identity, bool rich)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(identity);
        var geometry = inputs.Geometry;
        var layout = inputs.RebarLayout;
        var schedule = inputs.SteelSchedule;
        string activeManhole = Family21ManholeHelper.Resolve(inputs);
        SourceRef? source = layout.Occurrences.SelectMany(o => o.Sources).FirstOrDefault()
            ?? geometry.Components.SelectMany(c => c.Sources).FirstOrDefault();

        if (!presentation.IncludeDrawing || identity.Scope is not (CalculationScope.BaseRebarOnly or CalculationScope.FullRebar or CalculationScope.Full) ||
            layout.Scope is not (CalculationScope.BaseRebarOnly or CalculationScope.FullRebar))
            return Fail("DRAWING_SCOPE_UNSUPPORTED", source, "enabled BaseRebarOnly/FullRebar/Full drawing", identity.Scope.ToString());
        if (layout.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block))
            return Fail("DRAWING_LAYOUT_BLOCKED", source, "layout without Block diagnostics", "blocked layout");
        if(geometry.PurchasedCovers.Any(c=>c.NativeViewPlacement is not null && c.Scale!=1))
            return Fail("COVER_NATIVE_SCALE_INVALID",source,"source-native purchased cover at unit physical scale","fit scaling");
        if (!new[] { "Pad", "Base", "Roof" }.All(id => geometry.Components.Any(c => c.Id == id)))
            return Fail("DRAWING_GEOMETRY_MISSING", source, "Pad, Base, Roof", "incomplete geometry");
        if (layout.Occurrences.IsEmpty)
            return Fail("DRAWING_LAYOUT_EMPTY", source, "S1 base occurrences", "empty layout");

        var occurrences = layout.Occurrences.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var markByOccurrence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in schedule.Rows)
            foreach (var id in row.OccurrenceIds)
            {
                if (!occurrences.ContainsKey(id))
                    return Fail("DRAWING_MARK_UNRESOLVED", source, "schedule occurrence in layout", id);
                if (!markByOccurrence.TryAdd(id, row.Mark))
                    return Fail("DRAWING_MARK_DUPLICATE", source, "one schedule row per occurrence", id);
            }
        foreach (var bar in layout.Occurrences)
            if (!markByOccurrence.ContainsKey(bar.Id))
                return Fail("DRAWING_MARK_UNRESOLVED", bar.Sources.FirstOrDefault() ?? source, "mark for every occurrence", bar.Id);
        foreach (var bar in layout.Occurrences)
            if (!rich && (bar.ComponentKind != ComponentKind.Base || bar.ComponentId != "Base" ||
                bar.Path.Segments.Length != 1 || bar.Path.Segments[0] is not LineSegment3Mm ||
                bar.Path.HookPattern != HookPattern.Hook0M))
                return Fail("DRAWING_BAR_PATH_UNSUPPORTED", bar.Sources.FirstOrDefault() ?? source,
                    "straight S1 Base/0M path", bar.Id);
            else if (rich && bar.Path.Segments.Any(segment => segment is not (LineSegment3Mm or ArcSegment3Mm)))
                return Fail("DRAWING_BAR_PATH_UNSUPPORTED", bar.Sources.FirstOrDefault() ?? source,
                    "line or arc drawing path", bar.Id);

        var projectionGeometry=rich?SheetOnePresentationPolicy.ProjectionGeometry(geometry,presentation):geometry;
        var neckAssembly=rich?SheetOneNeckAssembly.Resolve(geometry,presentation):null;
        var plan = PlanProjector.BuildPlan(projectionGeometry);
        if (!plan.Success) return new(false, null, plan.Diagnostics);
        var aa = SectionProjector.BuildSection(projectionGeometry, new SectionDefinition("AA", SectionAxis.AlongY, 0, SectionMode.TrueSection));
        if (!aa.Success) return new(false, null, aa.Diagnostics);
        var bb = SectionProjector.BuildSection(projectionGeometry, new SectionDefinition("BB", SectionAxis.AlongX, 0, SectionMode.TrueSection));
        if (!bb.Success) return new(false, null, bb.Diagnostics);
        if(rich && SheetOnePresentationPolicy.Enabled(presentation))
        {
            aa=SheetOneSectionViews.ThroughNeck(projectionGeometry,aa.Value!);
            if(!aa.Success)return new(false,null,aa.Diagnostics);
            bb=SheetOneSectionViews.ThroughNeck(projectionGeometry,bb.Value!);
            if(!bb.Success)return new(false,null,bb.Diagnostics);
        }
        // The conventional support plate is an outline/annotation contract, not new physical bar material.
        var rebarAA=aa.Value!;var rebarBB=bb.Value!;
        if(neckAssembly is not null)
        {
            var assembledAA=neckAssembly.Section(aa.Value!);
            var assembledBB=neckAssembly.Section(bb.Value!);
            // One shared datum is valid only when both native section assemblies are supported.
            if(ReferenceEquals(assembledAA,aa.Value) || ReferenceEquals(assembledBB,bb.Value)) neckAssembly=null;
            else { aa=new(true,assembledAA,aa.Diagnostics);bb=new(true,assembledBB,bb.Diagnostics); }
        }

        var baseComponent = geometry.Components.Single(c => c.Id == "Base");
        var primitives = new List<DrawingPrimitive>();
        AddProfile(plan.Value!);
        AddProfile(aa.Value!);
        AddProfile(bb.Value!);
        if(rich)
            foreach(var section in new[]{"AA","BB"})
                primitives.AddRange(SheetOneSectionHiddenEdges.Build(geometry,section,presentation));
        if (rich && !SheetOnePresentationPolicy.Enabled(presentation) && geometry.Components.Count(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar")==4)
            primitives.AddRange(VhgSheetDecoration.CutMarkers(Bounds(geometry.Components.Single(c=>c.Id=="Base").Footprint.Outer.Vertices)!));
        primitives.AddRange(rich && layout.Occurrences.Any(o=>o.ComponentKind==ComponentKind.Wall)
            ? AnnotationPlanner.NativeSectionElevations(geometry, presentation,neckAssembly?.PlateTopMm) ?? AnnotationPlanner.Elevations(geometry, presentation).ToArray()
            : AnnotationPlanner.Elevations(geometry, presentation));
        foreach (var layer in new[] { Layer.Bottom, Layer.Top })
        {
            string viewId = layer == Layer.Bottom ? "BaseBottom" : "BaseTop";
            primitives.Add(new PolylinePrimitive($"{viewId}/Base/Outline", "Geometry/BaseOutline", viewId, "Base", null, null,
                baseComponent.Footprint.Outer.Vertices, true));
            foreach (var bar in layout.Occurrences.Where(o => o.Layer == layer && o.ComponentKind == ComponentKind.Base && o.ComponentId == "Base").OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                string mark = markByOccurrence[bar.Id];
                if (bar.Path.Segments.Length == 1 && bar.Path.Segments[0] is LineSegment3Mm line &&
                    (line.Start.X != line.End.X || line.Start.Y != line.End.Y))
                    primitives.Add(new PolylinePrimitive($"{viewId}/Bar/{bar.Id}", $"Rebar/{layer}", viewId, bar.ComponentId,
                        bar.Id, mark, [new Point2Mm(line.Start.X, line.Start.Y), new Point2Mm(line.End.X, line.End.Y)], false,
                        bar.ScheduleLengthMm, DrawingMaterialKind.RoundSteel));
                else
                {
                    var projected = PlanBarProjector.Build(bar, viewId, $"Rebar/{layer}", mark);
                    if (!projected.Success) return new(false, null, projected.Diagnostics);
                    primitives.AddRange(projected.Value!.Where(p =>
                        !(p is CirclePrimitive dot && SheetMeshPlacement.Title(dot.ViewId) is not null)));
                }
            }
        }
        foreach (var row in schedule.Rows)
        {
            var componentIds = layout.Occurrences.Where(o => row.OccurrenceIds.Contains(o.Id))
                .Select(o => o.ComponentId).Distinct(StringComparer.Ordinal).ToArray();
            // Aggregated rows trace through their schedule mark; a kind is not a geometry ID.
            string? componentId = componentIds.Length == 1 ? componentIds[0] : null;
            var cells = new TableCell[]
            {
                TextCell("Mark", row.Mark), TextCell("Component", row.ComponentKind.ToString()), TextCell("Layer", row.Layer.ToString()),
                TextCell("Axis", row.Axis.ToString()), NumberCell("DiameterMm", row.DiameterMm),
                NumberCell("Quantity", row.Quantity), NumberCell("SingleLengthMm", row.SingleLengthMm),
                NumberCell("TotalLengthMm", row.TotalLengthMm), NumberCell("UnitMassKgPerM", row.UnitMassKgPerM), NumberCell("MassKg", row.MassKg.Value)
            };
            primitives.Add(new TableRowPrimitive($"SteelSchedule/Row/{row.Mark}", "Schedule/Row", "SteelSchedule",
                componentId, null, row.Mark, row.Mark, cells, DrawingMaterialKind.RoundSteel));
        }
        primitives.Add(AnnotationPlanner.ScheduleTotal(schedule, presentation));

        var views = new List<ViewDefinition>();
        foreach (var profile in new[] { plan.Value!, aa.Value!, bb.Value! })
            views.Add(new ViewDefinition(profile.Id, profile.Purpose, Origin(profile.Id),
                Bounds(profile.Regions.SelectMany(r => r.Footprint.Outer.Vertices)), presentation.ScaleDenominator));
        var baseBounds = Bounds(baseComponent.Footprint.Outer.Vertices);
        views.Add(new ViewDefinition("BaseBottom", ViewPurpose.BaseMesh, Origin("BaseBottom"), baseBounds, presentation.ScaleDenominator));
        views.Add(new ViewDefinition("BaseTop", ViewPurpose.BaseMesh, Origin("BaseTop"), baseBounds, presentation.ScaleDenominator));
        views.Add(new ViewDefinition("SteelSchedule", ViewPurpose.SteelTable, Origin("SteelSchedule"), new Bounds2Mm(0, 0, 1000, 250), presentation.ScaleDenominator));

        if (rich)
        {
            int reinforcementIndex = 0;
            foreach (var group in layout.Occurrences.Where(b => b.ComponentKind is ComponentKind.Wall or ComponentKind.Roof or ComponentKind.Neck or ComponentKind.CatchBasin)
                         .GroupBy(b => (b.ComponentId, b.FrameId, b.Surface, b.Layer))
                         .OrderBy(g => g.Key.ComponentId, StringComparer.Ordinal).ThenBy(g => g.Key.FrameId, StringComparer.Ordinal)
                         .ThenBy(g => g.Key.Surface).ThenBy(g => g.Key.Layer))
            {
                var key = group.Key;
                string id = $"Reinforcement/{key.ComponentId}/{key.FrameId ?? "Default"}/{key.Surface?.ToString() ?? "Default"}/{key.Layer}";
                var projection = ComponentRebarProjector.Build(layout.Occurrences, markByOccurrence, id,
                    key.ComponentId, key.FrameId, key.Surface, key.Layer);
                if (!projection.Success) return new(false, null, projection.Diagnostics);
                var projected = projection.Value!.Where(p =>
                    !(p is CirclePrimitive dot && SheetMeshPlacement.Title(dot.ViewId) is not null)).ToArray();
                primitives.AddRange(projected);
                var points = projected.SelectMany(p => p switch {
                    PolylinePrimitive line => line.Points.AsEnumerable(),
                    CirclePrimitive dot => new[] { new Point2Mm(dot.Center.X-dot.RadiusMm,dot.Center.Y-dot.RadiusMm),
                        new Point2Mm(dot.Center.X+dot.RadiusMm,dot.Center.Y+dot.RadiusMm) },
                    PathPrimitive path => path.Segments.SelectMany(s => s is ArcPathSegment arc
                        ? new[] { new Point2Mm(arc.Center.X-arc.RadiusMm,arc.Center.Y-arc.RadiusMm),
                            new Point2Mm(arc.Center.X+arc.RadiusMm,arc.Center.Y+arc.RadiusMm) }
                        : new[] { s.Start, s.End }),
                    _ => Array.Empty<Point2Mm>() }).ToArray();
                views.Add(new ViewDefinition(id, group.First().ComponentKind is ComponentKind.Wall or ComponentKind.Neck ? ViewPurpose.Section : ViewPurpose.Plan,
                    new Point2Mm(reinforcementIndex++ * 2500, -5000), Bounds(points), presentation.ScaleDenominator));
            }
            if(layout.Scope==CalculationScope.FullRebar)
            {
                primitives.AddRange(SheetTwoHiddenEdges.Build(geometry,views.Select(v=>v.Id)));
                var mortarHatches=SheetTwoMortar.Build(geometry,views.Select(v=>v.Id));
                primitives.AddRange(mortarHatches);
                primitives.AddRange(SheetTwoMortar.Boundaries(mortarHatches));
                var context=SheetTwoMaterialViews.Build(geometry,views.Select(v=>v.Id));
                primitives.AddRange(context);
                foreach(var group in context.OfType<PolylinePrimitive>().GroupBy(p=>p.ViewId))
                {
                    var old=views.Single(v=>v.Id==group.Key);var b=old.Bounds!;
                    var points=group.SelectMany(p=>p.Points).ToArray();
                    var expanded=new Bounds2Mm(Math.Min(b.MinX,points.Min(p=>p.X)),Math.Min(b.MinY,points.Min(p=>p.Y)),
                        Math.Max(b.MaxX,points.Max(p=>p.X)),Math.Max(b.MaxY,points.Max(p=>p.Y)));
                    views[views.IndexOf(old)]=new ViewDefinition(old.Id,old.Purpose,old.Origin,expanded,old.ScaleDenominator);
                }
                var calloutMetadata=new Dictionary<string,(int Diameter,double? Pitch)>(StringComparer.Ordinal);
                foreach(var bar in layout.Occurrences)
                {
                    double? pitch=null;
                    if(bar.ComponentKind==ComponentKind.Base && bar.ComponentId=="Base")
                    {
                        var mesh=bar.Layer==Layer.Top?inputs.BaseTop:inputs.BaseBottom;
                        if(mesh?.DiameterMm==bar.DiameterMm) pitch=mesh.SpacingMm;
                    }
                    else if(bar.ComponentKind==ComponentKind.Wall && bar.FamilyId=="Wall")
                    {
                        var face=bar.Surface==RebarSurface.Outer?inputs.WallOuter:
                            bar.Surface==RebarSurface.Inner?inputs.WallInner:null;
                        if(face is not null && face.Structure==StructureKind.ReinforcedConcrete)
                            pitch=bar.Direction switch {
                                RebarDirection.Vertical=>face.VerticalSpacingMm,
                                RebarDirection.Horizontal=>face.VerticalSpacingMm+face.HorizontalSpacingDeltaMm,
                                _=>null };
                    }
                    else if(bar.ComponentKind==ComponentKind.Roof && bar.FamilyId=="Slab")
                    {
                        var mesh=bar.Layer==Layer.Top?inputs.RoofTop:inputs.RoofBottom;
                        if(mesh?.DiameterMm==bar.DiameterMm) pitch=mesh.SpacingMm;
                    }
                    calloutMetadata.Add(bar.Id,(bar.DiameterMm,pitch));
                }
                var callouts=SheetTwoBarAnnotations.Build(primitives,calloutMetadata,useNominalLabels:true);
                primitives.AddRange(callouts);
                var overallDimensions=SheetTwoDimensionPlanner.Build(geometry,views.Select(v=>v.Id));
                var mainBarIds=layout.Occurrences.Where(b=>b.ComponentKind==ComponentKind.Base && b.ComponentId=="Base"
                    || b.ComponentKind==ComponentKind.Wall && b.FamilyId=="Wall").Select(b=>b.Id).ToHashSet(StringComparer.Ordinal);
                var detailDimensions=SheetTwoDetailDimensions.Build(geometry,primitives.Concat(overallDimensions),mainBarIds,calloutMetadata);
                var sheetTwoDimensions=overallDimensions.Concat(detailDimensions).ToArray();
                primitives.AddRange(sheetTwoDimensions);
                primitives.AddRange(SheetTwoTieSymbols.Build(primitives, activeManhole,
                    layout.Occurrences.Where(b=>b.FamilyId=="Tie").Select(b=>b.Id).ToHashSet(StringComparer.Ordinal),
                    presentation.ScaleDenominator));
                var nominalBase=SheetTwoNominalWallMesh.Apply(geometry,SheetTwoNominalBaseMesh.Apply(primitives));
                primitives.Clear();primitives.AddRange(nominalBase);
                primitives.AddRange(SheetTwoAdjacentSlabSteel.Build(geometry,primitives,
                    layout.Occurrences.ToDictionary(b=>b.Id,b=>b.DiameterMm,StringComparer.Ordinal)));
                var nominalAccessories=SheetTwoAccessoryVisibility.Apply(primitives,
                    layout.Occurrences.ToDictionary(b=>b.Id,b=>b.FamilyId,StringComparer.Ordinal));
                primitives.Clear();primitives.AddRange(nominalAccessories);
                primitives.AddRange(SheetTwoNominalAnchorage.Build(geometry,primitives));
                var representativePaths=SheetTwoRepresentativePaths.Apply(primitives);
                primitives.Clear();primitives.AddRange(representativePaths);
                foreach(var group in sheetTwoDimensions.GroupBy(d=>d.ViewId))
                {
                    var old=views.Single(v=>v.Id==group.Key);var b=old.Bounds!;
                    var points=group.SelectMany(d=>new[]{d.Start,d.End,d.DimensionLinePoint!.Value}).ToArray();
                    views[views.IndexOf(old)]=new ViewDefinition(old.Id,old.Purpose,old.Origin,
                        new(Math.Min(b.MinX,points.Min(p=>p.X)),Math.Min(b.MinY,points.Min(p=>p.Y)),
                            Math.Max(b.MaxX,points.Max(p=>p.X)),Math.Max(b.MaxY,points.Max(p=>p.Y))),old.ScaleDenominator);
                }
                foreach(var group in callouts.GroupBy(p=>p.ViewId))
                {
                    var old=views.Single(v=>v.Id==group.Key);var b=old.Bounds!;
                    var points=SheetTwoBarAnnotations.Envelope(group).ToArray();
                    views[views.IndexOf(old)]=new ViewDefinition(old.Id,old.Purpose,old.Origin,
                        new Bounds2Mm(Math.Min(b.MinX,points.Min(p=>p.X)),Math.Min(b.MinY,points.Min(p=>p.Y)),
                            Math.Max(b.MaxX,points.Max(p=>p.X)),Math.Max(b.MaxY,points.Max(p=>p.Y))),old.ScaleDenominator);
                }


                void EnsureSheetThreeView(string viewId, Bounds2Mm bounds)
                {
                    int idx = views.FindIndex(v => v.Id == viewId);
                    var vdef = new ViewDefinition(viewId, viewId.Contains("/Wall/") ? ViewPurpose.Section : ViewPurpose.Plan,
                        new Point2Mm(reinforcementIndex++ * 2500, -5000), bounds, presentation.ScaleDenominator);
                    if (idx >= 0) views[idx] = vdef;
                    else views.Add(vdef);
                }

                Bounds2Mm PrimitiveBounds(IEnumerable<DrawingPrimitive> viewPrimitives)
                {
                    var pts = viewPrimitives.SelectMany(p => p switch
                    {
                        PolylinePrimitive pl => pl.Points.AsEnumerable(),
                        CirclePrimitive c => [new Point2Mm(c.Center.X - c.RadiusMm, c.Center.Y - c.RadiusMm), new Point2Mm(c.Center.X + c.RadiusMm, c.Center.Y + c.RadiusMm)],
                        DimensionIntentPrimitive dp => [dp.Start, dp.End, dp.DimensionLinePoint ?? dp.Start],
                        BlockPrimitive bp => [bp.Insertion],
                        TextPrimitive tp => [tp.Position],
                        _ => Enumerable.Empty<Point2Mm>()
                    }).ToArray();
                    return Bounds(pts) ?? new Bounds2Mm(-1000, -1000, 1000, 1000);
                }

                var actualSheetThreeWalls=SheetThreeWallViews.Build(primitives,activeManhole);
                foreach(var group in actualSheetThreeWalls.GroupBy(p=>p.ViewId))
                    EnsureSheetThreeView(group.Key,PrimitiveBounds(group));
                foreach (string wallId in new[] { "Wall/T1", "Wall/T2", "Wall/T3", "Wall/T4" })
                {
                    var symbols=primitives.OfType<BlockPrimitive>().Where(p=>p.ComponentId==wallId &&
                        p.BlockRole is "Block:BLTC" or "Block:Gach").ToArray();
                    var visibleSymbols=WallOpeningSymbolClipper.Clip(symbols,geometry,wallId);
                    if(!visibleSymbols.Success) return new(false,null,visibleSymbols.Diagnostics);
                    var visibleIds=visibleSymbols.Value!.Select(p=>p.SemanticId).ToHashSet(StringComparer.Ordinal);
                    primitives.RemoveAll(p=>p is BlockPrimitive bp && symbols.Contains(bp) && !visibleIds.Contains(bp.SemanticId));
                    var physicalDisplays=primitives.OfType<PolylinePrimitive>().Where(p=>p.ComponentId==wallId &&
                        p.ViewId.Contains("/Wall/",StringComparison.Ordinal) &&
                        p.Role is SheetTwoNominalWallMesh.Role or SheetTwoNominalWallMesh.EdgeRole or "Rebar/Main").ToArray();
                    var visibleDisplays=WallOpeningSymbolClipper.ClipMesh(physicalDisplays,geometry,wallId);
                    if(!visibleDisplays.Success) return new(false,null,visibleDisplays.Diagnostics);
                    primitives.RemoveAll(p=>p is PolylinePrimitive line && physicalDisplays.Contains(line));
                    primitives.AddRange(visibleDisplays.Value!);
                    // Dynamic pipe detailing applies to all walls from actual geometry model
                    if (SheetWallPipeDetailer.HasPipe(geometry, wallId))
                    {
                        string dir = wallId.Replace("Wall/", "");
                        var wallViews = primitives.Select(p => p.ViewId)
                            .Where(v => v.Contains($"/Wall/{dir}/", StringComparison.Ordinal))
                            .Distinct().ToArray();
                        foreach (var wv in wallViews)
                        {
                            var wallBars=physicalDisplays.Where(p=>p.ViewId==wv).ToArray();
                            var viewDef = views.SingleOrDefault(v => v.Id == wv);
                            if (viewDef is not null && viewDef.Bounds is not null)
                            {
                                var b = viewDef.Bounds;
                                var meshBounds = b;
                                if (wallBars.Length > 0)
                                {
                                    double mbMinX = wallBars.Min(p => Math.Min(p.Points[0].X, p.Points[1].X));
                                    double mbMaxX = wallBars.Max(p => Math.Max(p.Points[0].X, p.Points[1].X));
                                    double mbMinY = wallBars.Min(p => Math.Min(p.Points[0].Y, p.Points[1].Y));
                                    double mbMaxY = wallBars.Max(p => Math.Max(p.Points[0].Y, p.Points[1].Y));
                                    meshBounds = new Bounds2Mm(mbMinX, mbMinY, mbMaxX, mbMaxY);
                                }
                                var pipePrims = SheetWallPipeDetailer.BuildPipePrimitives(geometry, presentation, wv, wallId, meshBounds);
                                primitives.AddRange(pipePrims);
                                double newMinX = Math.Min(b.MinX, b.MinX - 350);
                                double newMinY = Math.Min(b.MinY, b.MinY - 250);
                                double newMaxX = Math.Max(b.MaxX, b.MaxX + 350);
                                views[views.IndexOf(viewDef)] = new ViewDefinition(viewDef.Id, viewDef.Purpose, viewDef.Origin,
                                    new Bounds2Mm(newMinX, newMinY, newMaxX, b.MaxY), viewDef.ScaleDenominator);
                            }
                        }
                    }
                }

                var collectorResult=SheetTwoGachCollector.BuildForGeometry(primitives,mainBarIds,geometry);
                if(!collectorResult.Success) return new(false,null,collectorResult.Diagnostics);
                var collected=collectorResult.Value!.ToList();
                foreach(string wallId in new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"})
                {
                    var ticks=collected.OfType<BlockPrimitive>().Where(p=>p.ComponentId==wallId).ToArray();
                    var visible=WallOpeningSymbolClipper.Clip(ticks,geometry,wallId);
                    if(!visible.Success) return new(false,null,visible.Diagnostics);
                    var visibleIds=visible.Value!.Select(p=>p.SemanticId).ToHashSet(StringComparer.Ordinal);
                    collected.RemoveAll(p=>p is BlockPrimitive tick && ticks.Contains(tick) && !visibleIds.Contains(tick.SemanticId));
                }
                // A removed tick cannot leave a collector with no remaining association.
                collected.RemoveAll(p=>p.Role==SheetTwoGachCollector.CollectorRole &&
                    !collected.OfType<BlockPrimitive>().Any(t=>t.SemanticId.StartsWith(p.SemanticId+"/Gach/",StringComparison.Ordinal)));
                primitives.AddRange(collected);
                var collectorLabels=SheetTwoCollectorLabels.Apply(primitives,calloutMetadata);
                primitives.Clear();primitives.AddRange(collectorLabels);
                foreach(var group in collected.GroupBy(p=>p.ViewId))
                {
                    int index=views.FindIndex(v=>v.Id==group.Key);
                    if(index<0) continue;
                    var old=views[index];var b=old.Bounds!;
                    var extent=group.OfType<PolylinePrimitive>().SelectMany(p=>p.Points)
                        .Concat(SheetTwoBarAnnotations.Envelope(primitives.Where(p=>p.ViewId==group.Key &&
                            (p.Role==SheetTwoBarAnnotations.Role || p.Role=="Rebar/Leader")))).ToArray();
                    if(extent.Length>0) views[index]=new ViewDefinition(old.Id,old.Purpose,old.Origin,
                        new(Math.Min(b.MinX,extent.Min(p=>p.X)),Math.Min(b.MinY,extent.Min(p=>p.Y)),
                            Math.Max(b.MaxX,extent.Max(p=>p.X)),Math.Max(b.MaxY,extent.Max(p=>p.Y))),old.ScaleDenominator);
                }

                var dowelProjection=RoofJointDowelProjection.Build(layout.Occurrences,markByOccurrence,inputs.DowelHoleDiameterMm,presentation.ScaleDenominator);
                if(!dowelProjection.Success) return new(false,null,dowelProjection.Diagnostics);
                if(dowelProjection.Value!.Count>0)
                {
                    primitives.AddRange(dowelProjection.Value);
                    if(!views.Any(v=>v.Id==SheetThreeRoofViews.TopViewId))
                        views.Add(new ViewDefinition(SheetThreeRoofViews.TopViewId,ViewPurpose.Plan,new(0,0),
                            Bounds(geometry.Components.Single(c=>c.Id=="Roof").Footprint.Outer.Vertices),presentation.ScaleDenominator));
                }
                if (geometry.Components.Any(c => c.Id == "Roof") && views.Any(v => v.Id.Contains("/Roof/")))
                {
                    var roofViews=views.Where(v=>v.Id.Contains("/Roof/",StringComparison.Ordinal)).ToArray();
                    var roofDetails=roofViews.SelectMany(v=>
                    {
                        var mesh=v.Id==SheetThreeRoofViews.TopViewId?inputs.RoofTop:inputs.RoofBottom;
                        var mainIds=layout.Occurrences.Where(b=>b.ComponentKind==ComponentKind.Roof && b.FamilyId=="Slab" &&
                            mesh is not null && b.DiameterMm==mesh.DiameterMm).Select(b=>b.Id).ToHashSet(StringComparer.Ordinal);
                        return SheetThreeRoofDetailer.Build(geometry,primitives,v.Id,mesh,inputs.RoofCoverMm,mainIds,activeManhole,calloutMetadata,presentation.ScaleDenominator);
                    }).ToArray();
                    primitives.RemoveAll(p => p.ViewId.Contains("/Roof/", StringComparison.Ordinal));
                    primitives.AddRange(roofDetails);
                    if (activeManhole is not null)
                    {
                        EnsureSheetThreeView(SheetThreeRoofViews.TopViewId, PrimitiveBounds(roofDetails.Where(p => p.ViewId == SheetThreeRoofViews.TopViewId)));
                        EnsureSheetThreeView(SheetThreeRoofViews.BottomViewId, PrimitiveBounds(roofDetails.Where(p => p.ViewId == SheetThreeRoofViews.BottomViewId)));
                    }
                }

                if (geometry.Components.Any(c => c.Id == "Neck" || c.Kind == ComponentKind.Neck) && views.Any(v => v.Id.Contains("/Neck/")))
                {
                    var neckDetails=SheetFourNeckDetailer.TryBuild(inputs,markByOccurrence,presentation);
                    if(!neckDetails.Success) return new(false,null,neckDetails.Diagnostics);
                    var neckPlan=neckDetails.Value!.Plan;var neckAA=neckDetails.Value.AA;var neckBB=neckDetails.Value.BB;

                    primitives.RemoveAll(p => p.ViewId.Contains("Neck", StringComparison.Ordinal));
                    primitives.AddRange(neckPlan);
                    primitives.AddRange(neckAA);
                    primitives.AddRange(neckBB);

                    views.RemoveAll(v => v.Id.Contains("Neck"));
                    views.Add(new ViewDefinition(SheetFourViews.PlanViewId, ViewPurpose.Plan, new Point2Mm(0, 0), PrimitiveBounds(neckPlan), presentation.ScaleDenominator));
                    views.Add(new ViewDefinition(SheetFourViews.AAViewId, ViewPurpose.Section, new Point2Mm(0, 0), PrimitiveBounds(neckAA), presentation.ScaleDenominator));
                    views.Add(new ViewDefinition(SheetFourViews.BBViewId, ViewPurpose.Section, new Point2Mm(0, 0), PrimitiveBounds(neckBB), presentation.ScaleDenominator));

                    bool catchFamily = inputs.Profile == Profile.GaThamThuNapRoi || geometry.Components.Any(c => c.Kind == ComponentKind.CatchBasin || c.Id.Contains("Catch", StringComparison.OrdinalIgnoreCase)) || layout.Occurrences.Any(o => o.ComponentKind == ComponentKind.CatchBasin);
                    if (catchFamily)
                    {
                        var catchComponents = geometry.Components.Where(c => c.Kind == ComponentKind.CatchBasin || c.Id.Contains("Catch", StringComparison.OrdinalIgnoreCase)).ToArray();
                        var catchBars = layout.Occurrences.Where(o => o.ComponentKind == ComponentKind.CatchBasin || o.ComponentId.Contains("Catch", StringComparison.OrdinalIgnoreCase)).ToArray();

                        var catchPlanPrims = new List<DrawingPrimitive>();
                        var catch11Prims = new List<DrawingPrimitive>();
                        var catch22Prims = new List<DrawingPrimitive>();

                        string planId = SheetFourViews.CatchPlanViewId;
                        string sec11Id = SheetFourViews.CatchSec11ViewId;
                        string sec22Id = SheetFourViews.CatchSec22ViewId;

                        foreach (var comp in catchComponents)
                        {
                            var outerVerts = comp.Footprint.Outer.Vertices;
                            catchPlanPrims.Add(new PolylinePrimitive($"{comp.Id}/Plan/Outline", "Geometry/BaseOutline", planId, comp.Id, null, null, outerVerts, true));
                            foreach (var inner in comp.Footprint.InnerLoops)
                            {
                                catchPlanPrims.Add(new PolylinePrimitive($"{comp.Id}/Plan/Inner", "Geometry/RoofOpening", planId, comp.Id, null, null, inner.Vertices, true));
                            }

                            double minX = outerVerts.Min(v => v.X), maxX = outerVerts.Max(v => v.X);
                            double minY = outerVerts.Min(v => v.Y), maxY = outerVerts.Max(v => v.Y);
                            double z0 = comp.ZRange.Start, z1 = comp.ZRange.End;

                            catch11Prims.Add(new PolylinePrimitive($"{comp.Id}/Sec11/Outline", "Geometry/BaseOutline", sec11Id, comp.Id, null, null,
                                [new(minY, z0), new(maxY, z0), new(maxY, z1), new(minY, z1)], true));

                            catch22Prims.Add(new PolylinePrimitive($"{comp.Id}/Sec22/Outline", "Geometry/BaseOutline", sec22Id, comp.Id, null, null,
                                [new(minX, z0), new(maxX, z0), new(maxX, z1), new(minX, z1)], true));
                        }

                        foreach (var bar in catchBars)
                        {
                            string mark = markByOccurrence.TryGetValue(bar.Id, out var m) ? m : bar.Id;
                            if (bar.Path.Segments.Length > 0)
                            {
                                var ptsPlan = bar.Path.Segments.Select(s => new Point2Mm(s.Start.X, s.Start.Y)).Append(new Point2Mm(bar.Path.Segments.Last().End.X, bar.Path.Segments.Last().End.Y)).Distinct().ToArray();
                                if (ptsPlan.Length > 1)
                                    catchPlanPrims.Add(new PolylinePrimitive($"{bar.Id}/Plan/Rebar", "Rebar/Main", planId, bar.ComponentId, bar.Id, mark, ptsPlan, false, materialKind: DrawingMaterialKind.RoundSteel));

                                var pts11 = bar.Path.Segments.Select(s => new Point2Mm(s.Start.Y, s.Start.Z)).Append(new Point2Mm(bar.Path.Segments.Last().End.Y, bar.Path.Segments.Last().End.Z)).Distinct().ToArray();
                                if (pts11.Length > 1)
                                    catch11Prims.Add(new PolylinePrimitive($"{bar.Id}/Sec11/Rebar", "Rebar/Main", sec11Id, bar.ComponentId, bar.Id, mark, pts11, false, materialKind: DrawingMaterialKind.RoundSteel));

                                var pts22 = bar.Path.Segments.Select(s => new Point2Mm(s.Start.X, s.Start.Z)).Append(new Point2Mm(bar.Path.Segments.Last().End.X, bar.Path.Segments.Last().End.Z)).Distinct().ToArray();
                                if (pts22.Length > 1)
                                    catch22Prims.Add(new PolylinePrimitive($"{bar.Id}/Sec22/Rebar", "Rebar/Main", sec22Id, bar.ComponentId, bar.Id, mark, pts22, false, materialKind: DrawingMaterialKind.RoundSteel));
                            }
                        }

                        primitives.AddRange(catchPlanPrims);
                        primitives.AddRange(catch11Prims);
                        primitives.AddRange(catch22Prims);

                        var boundsPlan = catchPlanPrims.Count > 0 ? PrimitiveBounds(catchPlanPrims) : new Bounds2Mm(-1250, -1250, 1150, 1150);
                        var bounds11 = catch11Prims.Count > 0 ? PrimitiveBounds(catch11Prims) : new Bounds2Mm(-950, -750, 850, 600);
                        var bounds22 = catch22Prims.Count > 0 ? PrimitiveBounds(catch22Prims) : new Bounds2Mm(-1200, -800, 950, 700);

                        views.Add(new ViewDefinition(planId, ViewPurpose.Plan, new Point2Mm(0, 0), boundsPlan, presentation.ScaleDenominator));
                        views.Add(new ViewDefinition(sec11Id, ViewPurpose.Section, new Point2Mm(0, 0), bounds11, presentation.ScaleDenominator));
                        views.Add(new ViewDefinition(sec22Id, ViewPurpose.Section, new Point2Mm(0, 0), bounds22, presentation.ScaleDenominator));
                    }

                    // AddSchematicBars below projects the actual occurrence paths once.
                }
            }
            if (layout.Occurrences.Any(o => o.ComponentKind == ComponentKind.Wall))
            {
                var structural = StructuralRebarProjector.Build(layout, markByOccurrence, rebarAA, rebarBB,
                    SheetOnePresentationPolicy.Enabled(presentation));
                if (!structural.Success) return new(false, null, structural.Diagnostics);
                foreach (var (id, profile) in new[] { ("SteelAA", aa.Value!), ("SteelBB", bb.Value!) })
                {
                    var sectionRegions=SheetOnePresentationPolicy.Enabled(presentation)
                        ? SheetOneHatchRegions.Build(profile,inputs.Profile==Profile.GaThamLienKhoi) : profile.Regions.ToArray();
                    foreach (var region in sectionRegions)
                    {
                        var compId = geometry.Components.Any(c => c.Id == region.SourceComponentId) ? region.SourceComponentId : geometry.Openings.FirstOrDefault(o => o.Id == region.SourceComponentId)?.OwnerId ?? region.SourceComponentId;
                        if(SheetOnePresentationPolicy.Enabled(presentation) && region.MaterialId=="Pipe" &&
                            geometry.Openings.Any(o=>o.OwnerId==region.SourceComponentId && o.Inner is CircleProfile)) continue;
                        foreach(var hatch in SectionMaterialPolicy.ForSheetOneMaterial(region.MaterialId, steelSection: true))
                            primitives.Add(new HatchPrimitive($"{id}/{region.Id}/Hatch/{hatch.Pattern}", hatch.Role, id,
                                compId, region.Footprint.Outer.Vertices,
                                region.Footprint.InnerLoops.Select(loop => loop.Vertices.Cast<Point2Mm>()), hatch.Kind,
                                nativeBoundaryLoops: SheetOnePresentationPolicy.Enabled(presentation)));
                        primitives.Add(new PolylinePrimitive($"{id}/{region.Id}/Outline", "Geometry/Material", id,
                            compId, null, null, region.Footprint.Outer.Vertices, true));
                    }
                    foreach (var aperture in profile.Voids)
                        primitives.Add(new PolylinePrimitive($"{id}/{aperture.Id}/Boundary",
                            aperture.Role == ProjectedVoidRole.PipeOuter ? "Geometry/PipeOuter" :
                            aperture.Role == ProjectedVoidRole.Inner ? "Geometry/PipeInner" : "Geometry/ConcreteCutout",
                            id, aperture.OwnerId, null, null, aperture.Boundary.Vertices, true));
                    primitives.AddRange(DimensionsFor(new ViewProfile(id, ViewPurpose.Section,
                        profile.Regions, profile.Voids, profile.Dimensions)));
                    views.Add(new ViewDefinition(id, ViewPurpose.Section,
                        new Point2Mm(id == "SteelAA" ? 3000 : 6000, 3000),
                        Bounds(profile.Regions.SelectMany(r => r.Footprint.Outer.Vertices)), presentation.ScaleDenominator));
                }
                primitives.AddRange(structural.Value!);
                foreach(var section in new[]{"SteelAA","SteelBB"})
                    primitives.AddRange(SheetOneSectionHiddenEdges.Build(geometry,section,presentation));
                primitives.AddRange(SectionRebarLabels.Build(geometry,layout,rebarAA,rebarBB,structural.Value!,presentation.ScaleDenominator));
                if(SheetOnePresentationPolicy.Enabled(presentation))
                    primitives.AddRange(SheetOneLadderPresentation.Build(layout,markByOccurrence,aa.Value!,bb.Value!,presentation.ScaleDenominator));
                var wallPoints = geometry.Components.Where(c => c.Kind == ComponentKind.Wall)
                    .SelectMany(c => c.Footprint.Outer.Vertices).ToArray();
                if (wallPoints.Length == 0)
                    return Fail("DRAWING_WALL_GEOMETRY_MISSING", source, "physical wall components for wall occurrences", "missing walls");
                var meshBase=geometry.Components.Single(c=>c.Id=="Base");
                var meshOutline=meshBase.Footprint.Outer.Vertices.Select(WallMeshPresentation.GraphicPoint).ToArray();
                primitives.Add(new PolylinePrimitive("WallMeshPlan/Base/Outline", "Geometry/Material", "WallMeshPlan",
                    meshBase.Id, null, null, meshOutline, true));
                primitives.AddRange(SheetOneDimensionPlanner.Build(geometry, "WallMeshPlan") ?? []);
                primitives.AddRange(WallMeshPresentation.Build(geometry, layout));
                primitives.AddRange(SheetOneWallMeshHiddenEdges.Build(geometry, presentation));
                views.Add(new ViewDefinition("WallMeshPlan", ViewPurpose.Plan, new Point2Mm(0, 3000),
                    Bounds(meshOutline), presentation.ScaleDenominator));
            }
            var bending = AddSchematicBars(layout, markByOccurrence, presentation, views, primitives, identity.Scope != CalculationScope.BaseRebarOnly);
            if (!bending.Success) return new(false, null, bending.Diagnostics);
            AddDetailViews(geometry, presentation, views, primitives);
            AddQuantityView(inputs.Quantities, presentation, views, primitives);
            AddAngleRows(inputs.AngleSchedule, primitives);
            if(inputs.AngleSchedule?.Rows.Length > 0) views.Add(new ViewDefinition("AngleSchedule",ViewPurpose.SteelTable,new(9000,-2500),new Bounds2Mm(0,0,1000,250),presentation.ScaleDenominator));
            // Family 2.1 first-sheet convention has no separate angle-frame plan lines.
            // Physical angles and the supplied angle schedule above remain unchanged.
            if (!SheetOnePresentationPolicy.Enabled(presentation))
                AddAngleOutlines(layout.Angles, primitives);
            AddPurchasedCovers(geometry, presentation, views, primitives,neckAssembly?.PlateTopMm);
            if(SheetOnePresentationPolicy.Enabled(presentation))
            {
                primitives.AddRange(SheetOnePipeViews.Build(geometry,presentation).Where(p => views.Any(v => v.Id == p.ViewId)));
                if(geometry.Components.Count(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar")==4)
                    primitives.AddRange(VhgSheetDecoration.CutMarkers(geometry,primitives));
            }
        }

        foreach (var group in primitives.OfType<TableRowPrimitive>().GroupBy(row => row.ViewId).ToArray())
        {
            var old = views.Single(v => v.Id == group.Key);
            var total = primitives.OfType<TextPrimitive>().SingleOrDefault(p => p.ViewId == group.Key && p.Role == "Schedule/TotalMass");
            if(total is not null) primitives.Remove(total);
            var rows=group.ToArray();
            foreach(var row in rows) primitives.Remove(row);
            views.Remove(old);
            var pages=ScheduleTablePlanner.Plan(rows,old.Origin,old.ScaleDenominator,
                group.Key=="QuantityTable"?"BẢNG TỔNG HỢP KHỐI LƯỢNG":group.Key=="AngleSchedule"?"BẢNG THỐNG KÊ THÉP GÓC":"BẢNG THỐNG KÊ CỐT THÉP",
                rows[0].Cells.Select(c=>c.ColumnKey switch { "Mark"=>"Số hiệu","Component"=>"Cấu kiện","Layer"=>"Lớp","Axis"=>"Phương","DiameterMm"=>"Ø (mm)","Quantity"=>"Số lượng","SingleLengthMm"=>"Chiều dài (mm)","TotalLengthMm"=>"Tổng C.Dài (m)","UnitMassKgPerM"=>"Trọng lượng (kG/m)","MassKg"=>"Khối lượng (kG)","CatalogCode"=>"Quy cách","Material"=>"Vật liệu","Unit"=>"Đơn vị","Net"=>"Khối lượng",_=>c.ColumnKey }).ToArray(),total?.Content);
            foreach(var page in pages)
            {
                views.Add(page.View);
                foreach(var primitive in page.Primitives)
                    if(rich && group.Key=="SteelSchedule" && primitive is TextPrimitive nativeFooter && nativeFooter.Role=="Schedule/TotalMass")
                        primitives.Add(new BlockPrimitive("SteelSchedule/NativeSummary","Schedule/TotalMass",nativeFooter.ViewId,null,"SteelSummary",
                            new(nativeFooter.Position.X-presentation.ScaleDenominator,nativeFooter.Position.Y+9*presentation.ScaleDenominator),
                            presentation.ScaleDenominator/30*0.2,ScheduleNativeAttributes.Summary(schedule,inputs.AngleSchedule)));
                    else
                    primitives.Add(primitive is TextPrimitive footer && footer.Role=="Schedule/TotalMass" && total is not null
                        ? new TextPrimitive(total.SemanticId,total.Role,footer.ViewId,null,null,null,footer.Position,footer.Content,footer.HeightMm,0)
                        : primitive);
            }
        }
        var bounds = new Bounds2Mm(views.Min(v => v.Origin.X), views.Min(v => v.Origin.Y),
            views.Max(v => v.Origin.X + v.Bounds!.MaxX - v.Bounds.MinX),
            views.Max(v => v.Origin.Y + v.Bounds!.MaxY - v.Bounds.MinY));
        var nativePrimitives=SheetOneSectionOutlinePolicy.Compose(SheetOnePlanHiddenPolicy.Compose(WallMeshGuidePolicy.Compose(WallMeshBandPolicy.Compose(primitives,inputs,presentation),inputs,presentation),presentation),presentation)
                .Select(p => SheetOneRebarDotPolicy.Apply(SheetOnePlanOutlinePolicy.Apply(SheetOnePlanBaseOutlinePolicy.Apply(
                    SheetTwoMaterialViews.Apply(p,presentation),presentation),presentation), presentation));
        var package = new DrawingPackage(identity.InputHash, identity.RuleVersion, views,
            SheetOnePlanBaseOutlinePolicy.ClipAtPipes(nativePrimitives,presentation), bounds, identity.Scope);
        var valid = DrawingPackageValidator.ValidatePrimitives(package.Views, package.Primitives);
        var assemblyDiagnostics=neckAssembly is null ? Array.Empty<Diagnostic>() :
            [new Diagnostic("SHEET_ONE_NECK_PRESENTATION_ONLY",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,source,
                "source-evidenced drawing support plate; physical quantity snapshot retained",
                FormattableString.Invariant($"bodyTop={neckAssembly.BodyTopMm}; plateTop={neckAssembly.PlateTopMm}; plateHeight=80"),
                "Bounded NapGaV first-sheet assembly differs from the physical recessed seat/cap datum. Physical and export parity are not certified.")];
        return valid.Success ? new(true, package, plan.Diagnostics.Concat(aa.Diagnostics).Concat(bb.Diagnostics).Concat(assemblyDiagnostics)) : new(false, null, valid.Diagnostics);

        IEnumerable<DrawingPrimitive> DimensionsFor(ViewProfile profile) =>
            rich ? SheetOneDimensionPlanner.Build(geometry, profile.Id,neckAssembly?.PlateTopMm) ?? AnnotationPlanner.Dimensions(profile, presentation).ToArray()
                : AnnotationPlanner.Dimensions(profile, presentation);

        void AddProfile(ViewProfile profile)
        {
            var materialRegions = rich && profile.Purpose==ViewPurpose.Section && SheetOnePresentationPolicy.Enabled(presentation)
                ? SheetOneHatchRegions.Build(profile,inputs.Profile==Profile.GaThamLienKhoi) : profile.Regions.ToArray();
            if (rich && profile.Purpose==ViewPurpose.Section && SheetOnePresentationPolicy.Enabled(presentation))
                foreach (var region in materialRegions
                    .Where(r=>r.MaterialId!="Pipe" || !geometry.Openings.Any(o=>o.OwnerId==r.SourceComponentId && o.Inner is CircleProfile)))
                {
                    var compId = geometry.Components.Any(c => c.Id == region.SourceComponentId) ? region.SourceComponentId : geometry.Openings.FirstOrDefault(o => o.Id == region.SourceComponentId)?.OwnerId ?? region.SourceComponentId;
                    foreach (var material in SectionMaterialPolicy.ForSheetOneMaterial(region.MaterialId))
                        primitives.Add(new HatchPrimitive($"{profile.Id}/{region.Id}/Hatch/{material.Pattern}",material.Role,
                            profile.Id,compId,region.Footprint.Outer.Vertices,
                            region.Footprint.InnerLoops.Select(loop=>loop.Vertices.Cast<Point2Mm>()),material.Kind,
                            nativeBoundaryLoops:true));
                }
            foreach (var region in materialRegions)
            {
                var compId = geometry.Components.Any(c => c.Id == region.SourceComponentId) ? region.SourceComponentId : geometry.Openings.FirstOrDefault(o => o.Id == region.SourceComponentId)?.OwnerId ?? region.SourceComponentId;
                if(rich && SheetOnePresentationPolicy.Enabled(presentation) && region.MaterialId=="Pipe" &&
                    geometry.Openings.Any(o=>o.OwnerId==region.SourceComponentId && o.Inner is CircleProfile)) continue;
                // The plan shows the base footprint; buried bedding is only visible in the sections.
                if (rich && profile.Id == "Plan" && region.SourceComponentId == "Pad") continue;
                // The haunch is below the roof, and the native access plate replaces the collar
                // in this published assembly. Their physical footprints remain in the model.
                if(rich && profile.Id=="Plan" && SheetOnePresentationPolicy.Enabled(presentation) &&
                    (region.SourceComponentId=="Base/Haunch" || neckAssembly is not null && region.SourceComponentId=="Collar")) continue;
                // Union boundaries are emitted below, without seams between adjacent wall materials.
                if(rich && profile.Id=="Plan" && region.SourceComponentId.StartsWith("Wall/",StringComparison.Ordinal)) continue;
                if (rich && profile.Purpose == ViewPurpose.Section && !SheetOnePresentationPolicy.Enabled(presentation))
                {
                    foreach(var material in SectionMaterialPolicy.ForSheetOneMaterial(region.MaterialId))
                        primitives.Add(new HatchPrimitive($"{profile.Id}/{region.Id}/Hatch/{material.Pattern}", material.Role,
                            profile.Id, compId, region.Footprint.Outer.Vertices,
                            region.Footprint.InnerLoops.Select(loop => loop.Vertices.Cast<Point2Mm>()), material.Kind));
                }
                primitives.Add(new PolylinePrimitive($"{profile.Id}/{region.Id}/Outer", "Geometry/Material", profile.Id,
                    compId, null, null, region.Footprint.Outer.Vertices, true));
                for (int index = 0; index < region.Footprint.InnerLoops.Length; index++)
                {
                    var loop = region.Footprint.InnerLoops[index];
                    bool represented = profile.Voids.Any(v => v.OwnerId == region.SourceComponentId &&
                        v.Boundary.Vertices.ToHashSet().SetEquals(loop.Vertices));
                    if (!represented)
                        primitives.Add(new PolylinePrimitive($"{profile.Id}/{region.Id}/Inner/{index + 1}", "Geometry/InnerLoop",
                            profile.Id, compId, null, null, loop.Vertices, true));
                }
            }
            if(rich && profile.Id=="Plan") primitives.AddRange(PlanHiddenEdgePlanner.Build(profile));
            foreach (var projected in profile.Voids)
            {
                // Round pipe outlines are supplied by the inner/outer stub projection.
                // The concrete tunnel's bounding rectangle is not an extra visible pipe.
                if(rich && profile.Id=="Plan" && SheetOnePresentationPolicy.Enabled(presentation) &&
                    projected.Role==ProjectedVoidRole.PipePlanSilhouette && geometry.Openings.Any(o=>
                        o.Id==projected.SourceOpeningId && o.Inner is CircleProfile)) continue;
                string role = projected.Role switch
                {
                    ProjectedVoidRole.RoofOpening => rich && profile.Id=="Plan"?"Geometry/PlanHidden":"Geometry/RoofOpening",
                    ProjectedVoidRole.PipePlanSilhouette => "Geometry/PipePlanSilhouette",
                    ProjectedVoidRole.ConcreteCutout => "Geometry/ConcreteCutout",
                    ProjectedVoidRole.PipeOuter => "Geometry/PipeOuter",
                    _ => "Geometry/PipeInner"
                };
                primitives.Add(new PolylinePrimitive($"{profile.Id}/{projected.Id}", role, profile.Id, projected.OwnerId,
                    null, null, projected.Boundary.Vertices, true));
            }
            primitives.AddRange(DimensionsFor(profile));
        }
    }

    private static OperationResult<IReadOnlyList<BendingShapeIntent>> AddSchematicBars(RebarLayout layout,
        IReadOnlyDictionary<string, string> marks, PresentationRequest presentation,
        ICollection<ViewDefinition> views, ICollection<DrawingPrimitive> primitives, bool separateViews)
    {
        var result = BendingSchematicPlanner.Build(layout.Occurrences, marks);
        if (!result.Success) return result;
        int index = 0;
        foreach (var shape in result.Value!)
        {
            string id = $"Bending/{shape.Mark}";
            if (!separateViews)
            {
                primitives.Add(new PathPrimitive($"{id}/Shape","Rebar/Schematic","SteelSchedule",shape.ComponentId,null,shape.Mark,
                    shape.Segments,shape.Closed,ViewPurpose.Schematic,shape.ScheduleLengthMm,DrawingMaterialKind.RoundSteel,
                    shape.SourceOccurrenceIds[0]));
                continue;
            }
            var points = shape.Segments.SelectMany(s => s is ArcPathSegment arc
                ? new[] { new Point2Mm(arc.Center.X-arc.RadiusMm,arc.Center.Y-arc.RadiusMm),
                    new Point2Mm(arc.Center.X+arc.RadiusMm,arc.Center.Y+arc.RadiusMm) }
                : new[] { s.Start, s.End }).ToArray();
            var bounds = Bounds(points)!;
            double textHeight = 2 * presentation.ScaleDenominator;
            var padded = new Bounds2Mm(bounds.MinX-3*textHeight,bounds.MinY-3*textHeight,
                Math.Max(bounds.MaxX,bounds.MinX+40*textHeight)+3*textHeight,bounds.MaxY+4*textHeight);
            views.Add(new ViewDefinition(id,ViewPurpose.Schematic,new Point2Mm(index++*2500,-8000),padded,presentation.ScaleDenominator));
            primitives.Add(new PathPrimitive($"{id}/Shape","Rebar/Schematic",id,shape.ComponentId,null,shape.Mark,
                shape.Segments,shape.Closed,ViewPurpose.Schematic,shape.ScheduleLengthMm,DrawingMaterialKind.RoundSteel,
                shape.SourceOccurrenceIds[0]));
            primitives.Add(new TextPrimitive($"{id}/Name","Rebar/Mark",id,shape.ComponentId,null,shape.Mark,
                new Point2Mm(bounds.MinX,bounds.MaxY+2*textHeight),
                FormattableString.Invariant($"{shape.Mark} SL={shape.Quantity} L={Math.Round(shape.ScheduleLengthMm, 0):0}"),textHeight,0));
            for(int leg=0;leg<shape.Segments.Length;leg++)
            {
                var segment=shape.Segments[leg];
                if(segment is LinePathSegment line)
                {
                    double length=Math.Sqrt(Math.Pow(line.End.X-line.Start.X,2)+Math.Pow(line.End.Y-line.Start.Y,2));
                    primitives.Add(new DimensionIntentPrimitive($"{id}/Leg/{leg+1}","Dimension",id,shape.ComponentId,null,
                        shape.Mark,line.Start,line.End,length,(length % 1 == 0 ? length.ToString("0",System.Globalization.CultureInfo.InvariantCulture) : length.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)),textHeight));
                }
                else if(segment is ArcPathSegment arc)
                    primitives.Add(new TextPrimitive($"{id}/Arc/{leg+1}","Rebar/Mark",id,shape.ComponentId,null,shape.Mark,
                        new Point2Mm(arc.Center.X,arc.Center.Y+textHeight),
                        FormattableString.Invariant($"R={arc.RadiusMm:0.##} A={arc.SweepRadians*180/Math.PI:0.##}"),textHeight,0));
            }
        }
        return result;
    }
    private static PathPrimitive ToPathPrimitive(string semanticId, string role, string viewId, BarOccurrence bar,
        string mark, ViewPurpose purpose, string? sourceOccurrenceId = null)
    {
        if (purpose == ViewPurpose.Schematic && bar.Path.Segments.Length == 1 &&
            bar.Path.Segments[0] is LineSegment3Mm straight && straight.Start.X == straight.End.X && straight.Start.Y == straight.End.Y)
            return new PathPrimitive(semanticId, role, viewId, bar.ComponentId, null, mark,
                [new LinePathSegment(new(0, 0), new(Math.Abs(straight.End.Z - straight.Start.Z), 0))], false, purpose,
                bar.ScheduleLengthMm, DrawingMaterialKind.RoundSteel, sourceOccurrenceId);
        var segments = bar.Path.Segments.Select(segment => segment switch
        {
            LineSegment3Mm line => (DrawingPathSegment)new LinePathSegment(new Point2Mm(line.Start.X, line.Start.Y), new Point2Mm(line.End.X, line.End.Y)),
            ArcSegment3Mm arc => (DrawingPathSegment)new ArcPathSegment(new Point2Mm(arc.Center.X, arc.Center.Y), arc.CenterlineRadiusMm,
                Math.Atan2(arc.StartDirection.Y, arc.StartDirection.X), arc.SweepRadians),
            _ => throw new ArgumentException("Unsupported drawing path segment.")
        });
        return new PathPrimitive(semanticId, role, viewId, bar.ComponentId,
            purpose == ViewPurpose.Schematic ? null : bar.Id, mark, segments, bar.Path.Closed, purpose,
            bar.ScheduleLengthMm, DrawingMaterialKind.RoundSteel, sourceOccurrenceId);
    }

    private static void AddDetailViews(GeometryModel geometry, PresentationRequest presentation,
        ICollection<ViewDefinition> views, ICollection<DrawingPrimitive> primitives)
    {
        var detailKinds = new[]
        {
            (ComponentKind.Neck, ViewPurpose.NeckDetail), (ComponentKind.CatchBasin, ViewPurpose.CatchBasinDetail),
            (ComponentKind.Panel, ViewPurpose.PanelDetail)
        };
        int index = 0;
        foreach (var (kind, purpose) in detailKinds)
            foreach (var component in geometry.Components.Where(c => c.Kind == kind).OrderBy(c => c.Id, StringComparer.Ordinal))
                AddDetail(component, purpose);
        foreach (var component in geometry.Components.Where(c => c.Kind == ComponentKind.Collar).OrderBy(c => c.Id, StringComparer.Ordinal))
            AddDetail(component, ViewPurpose.AccessoryDetail);

        void AddDetail(ComponentGeometry component, ViewPurpose purpose)
        {
            string viewId = $"{purpose}/{component.Id}";
            var localBounds = Bounds(component.Footprint.Outer.Vertices)!;
            views.Add(new ViewDefinition(viewId, purpose, new Point2Mm(7500 + index++ * 1800, 0), localBounds, presentation.ScaleDenominator));
            var material = component.Kind == ComponentKind.Collar ? DrawingMaterialKind.PurchasedCover : DrawingMaterialKind.Concrete;
            primitives.Add(new HatchPrimitive($"{viewId}/Hatch", "Geometry/Hatch", viewId, component.Id,
                component.Footprint.Outer.Vertices, component.Footprint.InnerLoops.Select(loop => loop.Vertices.Cast<Point2Mm>()), material));
            var center = new Point2Mm((localBounds.MinX + localBounds.MaxX) / 2, (localBounds.MinY + localBounds.MaxY) / 2);
            primitives.Add(new TextPrimitive($"{viewId}/Tag", "Accessory/Tag", viewId, component.Id, null, null,
                center, component.Id, 2.5 * presentation.ScaleDenominator, 0));
            primitives.Add(new TextPrimitive($"{viewId}/Title", "ViewTitle", viewId, component.Id, null, null,
                new Point2Mm(localBounds.MinX, localBounds.MaxY), purpose.ToString(), 2.5 * presentation.ScaleDenominator, 0));
        }
    }

    private static void AddQuantityView(DrawingQuantitySnapshot quantities, PresentationRequest presentation,
        ICollection<ViewDefinition> views, ICollection<DrawingPrimitive> primitives)
    {
        var lines = (quantities.ConcreteLines ?? []).Concat(quantities.FormworkLines ?? []).ToArray();
        if (lines.Length == 0) return;
        const string viewId = "QuantityTable";
        views.Add(new ViewDefinition(viewId, ViewPurpose.QuantityTable, new Point2Mm(9000, 0), new Bounds2Mm(0, 0, 1200, 250), presentation.ScaleDenominator));
        foreach (var line in lines)
            primitives.Add(new TableRowPrimitive($"{viewId}/Row/{line.Id}", "Quantity/Row", viewId, line.ComponentId, null, null,
                line.Id, [TextCell("Component", line.ComponentId), TextCell("Material", line.MaterialId),
                TextCell("Unit", line.Unit.ToString()), NumberCell("Net", line.Net)],
                line.Unit == QuantityUnit.Kg ? DrawingMaterialKind.RoundSteel :
                line.Unit == QuantityUnit.M2 ? DrawingMaterialKind.Formwork : DrawingMaterialKind.Concrete));
    }

    private static void AddAngleOutlines(IEnumerable<AngleOccurrence> angles, ICollection<DrawingPrimitive> primitives)
    {
        foreach (var angle in angles.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            var segments = angle.Path.Segments.Select(segment => segment switch
            {
                LineSegment3Mm line => (DrawingPathSegment)new LinePathSegment(new Point2Mm(line.Start.X, line.Start.Y), new Point2Mm(line.End.X, line.End.Y)),
                ArcSegment3Mm arc => (DrawingPathSegment)new ArcPathSegment(new Point2Mm(arc.Center.X, arc.Center.Y), arc.CenterlineRadiusMm,
                    Math.Atan2(arc.StartDirection.Y, arc.StartDirection.X), arc.SweepRadians),
                _ => throw new ArgumentException("Unsupported angle path segment.")
            });
            primitives.Add(new PathPrimitive($"Plan/Angle/{angle.Id}", "Accessory/AngleFrame", "Plan", angle.ComponentId,
                null, null, segments, angle.Path.Closed, ViewPurpose.Plan, angle.LengthMm, DrawingMaterialKind.AngleSteel, angle.Id));
        }
    }

    private static void AddAngleRows(AngleSchedule? schedule, ICollection<DrawingPrimitive> primitives)
    {
        if (schedule is null) return;
        foreach (var row in schedule.Rows)
            primitives.Add(new TableRowPrimitive($"SteelSchedule/Angle/{row.Mark}", "Schedule/AngleRow", "AngleSchedule",
                row.ComponentId, null, null, row.Mark, [TextCell("Mark", row.Mark), TextCell("CatalogCode", row.CatalogCode), NumberCell("Quantity", row.Quantity),
                NumberCell("SingleLengthMm", row.SingleLengthMm), NumberCell("TotalLengthMm", row.TotalLengthMm), NumberCell("UnitMassKgPerM", row.UnitMassKgPerM), NumberCell("MassKg", row.MassKg.Value)],
                DrawingMaterialKind.AngleSteel));
    }

    private static void AddPurchasedCovers(GeometryModel geometry, PresentationRequest presentation,
        ICollection<ViewDefinition> views, ICollection<DrawingPrimitive> primitives,double? assembledTopMm=null)
    {
        for(int index=0;index<geometry.PurchasedCovers.Length;index++)
        {
            var cover=geometry.PurchasedCovers[index];
            var b=cover.LocalBounds;
            double angle=cover.RotationDegrees*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle);
            Point2Mm Transform(double x,double y)=>new(cover.Anchor.X+cover.Scale*(x*cos-y*sin),cover.Anchor.Y+cover.Scale*(x*sin+y*cos));
            Point2Mm[] points=[Transform(b.MinX,b.MinY),Transform(b.MaxX,b.MinY),Transform(b.MaxX,b.MaxY),Transform(b.MinX,b.MaxY)];
            string id=$"PurchasedCover/{index}";
            string owner=geometry.Components.Any(c=>c.Id=="Neck")?"Neck":"Roof";
            // Physical catalog bounds remain native millimetres; no fit to the neck or paper scaling.
            bool legacy=cover.CatalogId.StartsWith("NapGaV.",StringComparison.Ordinal) || cover.CatalogId.StartsWith("NapGaCN.",StringComparison.Ordinal);
            // Source-native 700 cover transforms: plan7/8 and section8/9 were formerly
            bool directNative=cover.CatalogId=="NapGaV.700";
            primitives.Add(new BlockPrimitive($"{id}/Plan",legacy || directNative?"Geometry/LegacyCoverPlan":"Geometry/PurchasedCover","Plan",owner,
                directNative?"Block:NapGaV-MB":$"Block:{cover.CatalogId}.MB",new(cover.Anchor.X,cover.Anchor.Y),
                cover.Scale*(directNative?7d/8:1),[],angle));
            // Native orthographic cover blocks are defined with their top at local Y=0.
            // Oblique cover sections need a separate verified projection; do not rotate a section in 2D.
            if (Math.Abs(cover.RotationDegrees / 90 - Math.Round(cover.RotationDegrees / 90)) < 1e-9)
            {
                bool swap = Math.Abs((int)Math.Round(cover.RotationDegrees / 90)) % 2 == 1;
                foreach (string section in new[] { "AA", "BB", "SteelAA", "SteelBB" }.Where(v=>views.Any(d=>d.Id==v)))
                {
                    bool alongY=section.EndsWith("AA",StringComparison.Ordinal);
                    if(cover.SectionOutlines is { } outline)
                    {
                        var rectangle=alongY?outline.AA:outline.BB;
                        primitives.Add(new PolylinePrimitive($"{id}/{section}/Outline","Geometry/PurchasedCover",section,owner,null,null,
                            [new(rectangle.MinX,rectangle.MaxY),new(rectangle.MaxX,rectangle.MaxY),
                             new(rectangle.MaxX,rectangle.MinY),new(rectangle.MinX,rectangle.MinY)],true));
                        var oldView=views.Single(v=>v.Id==section);
                        var oldBounds=oldView.Bounds;
                        var outlineBounds=oldBounds is null?new Bounds2Mm(rectangle.MinX,rectangle.MinY,rectangle.MaxX,rectangle.MaxY):
                            new Bounds2Mm(Math.Min(oldBounds.MinX,rectangle.MinX),Math.Min(oldBounds.MinY,rectangle.MinY),
                                Math.Max(oldBounds.MaxX,rectangle.MaxX),Math.Max(oldBounds.MaxY,rectangle.MaxY));
                        views.Remove(oldView);
                        views.Add(new ViewDefinition(oldView.Id,oldView.Purpose,oldView.Origin,outlineBounds,oldView.ScaleDenominator));
                    }
                    if(cover.NativeViewPlacement is { } resolved)
                    {
                        var native=alongY?resolved.SectionAA:resolved.SectionBB;
                        primitives.Add(new BlockPrimitive($"{id}/{section}","Geometry/PurchasedCover",section,owner,
                            $"Block:{cover.CatalogId}.{native.NativeView}",native.Insertion,1,[],
                            mirrorHorizontal:native.HorizontalDirection<0));
                        continue;
                    }
                    string suffix=alongY != swap ? "AA" : "BB";
                    primitives.Add(new BlockPrimitive($"{id}/{section}",legacy || directNative?"Geometry/LegacyCoverSection":"Geometry/PurchasedCover",section,owner,
                        directNative?"Block:NapGaV-MC":$"Block:{cover.CatalogId}.{suffix}",new(alongY?cover.Anchor.Y:cover.Anchor.X,assembledTopMm ?? cover.Anchor.Z),
                        cover.Scale*(directNative?8d/9:1),[]));
                }
            }
            var plan=views.Single(v=>v.Id=="Plan");var bounds=plan.Bounds!;
            var expanded=new Bounds2Mm(Math.Min(bounds.MinX,points.Min(p=>p.X)),Math.Min(bounds.MinY,points.Min(p=>p.Y)),
                Math.Max(bounds.MaxX,points.Max(p=>p.X)),Math.Max(bounds.MaxY,points.Max(p=>p.Y)));
            views.Remove(plan);views.Add(new ViewDefinition(plan.Id,plan.Purpose,plan.Origin,expanded,presentation.ScaleDenominator));
        }
    }

    private static TableCell TextCell(string key, string value) => new(key, TableCellKind.Text, value, null);
    private static TableCell NumberCell(string key, double value) => new(key, TableCellKind.Number, null, value);

    private static Point2Mm Origin(string id) => id switch
    {
        "Plan" => new(0, 0), "AA" => new(2500, 0), "BB" => new(5000, 0),
        "BaseBottom" => new(0, -2500), "BaseTop" => new(2500, -2500),
        "SteelSchedule" => new(5000, -2500), _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static Bounds2Mm? Bounds(IEnumerable<Point2Mm> points)
    {
        var all = points.ToArray();
        return all.Length == 0 ? null : new Bounds2Mm(all.Min(p => p.X), all.Min(p => p.Y), all.Max(p => p.X), all.Max(p => p.Y));
    }

    private static OperationResult<DrawingPackage> Fail(string code, SourceRef? source, string expected, string actual) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source,
            expected, actual, "The requested semantic drawing package cannot resolve this request.")]);
}

