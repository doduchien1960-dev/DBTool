#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// S7 Annotation lane allocator.
/// Plans dimension lanes, material and elevation callouts, multi-elbow rebar leader routes,
/// tick markers (Gach/BLTC), obstacle keepouts, and hatches for drawing views.
/// </summary>
public sealed class AnnotationLaneAllocator
{
    /// <summary>
    /// Plans annotations (labels, routes, dimensions, ticks, hatches, and view ink envelopes)
    /// across all required projection views.
    /// </summary>
    public AnnotationsV2 Plan(
        EffectiveV2 effective,
        ProjectionV2 projection,
        GroupsV2 groups,
        AssetsV2 assets,
        RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(rules);

        var labels = new List<LabelV2>();
        var routes = new List<RouteV2>();
        var dimensions = new List<DimensionV2>();
        var ticks = new List<TickV2>();
        var viewInks = new List<ViewInkV2>();
        var hatches = new List<HatchV2>();

        var views = projection.views ?? [];
        var allCurves = projection.curves ?? [];
        var allTargets = projection.targets ?? [];
        var allRegions = projection.regions ?? [];
        var allGroups = groups.items ?? [];

        foreach (var view in views)
        {
            if (!view.required)
            {
                if (string.IsNullOrEmpty(view.na_reason))
                {
                    throw new DrawingStageFaultException(
                        "VIEW_NA_REASON_MISSING",
                        view.id,
                        ["na_reason"],
                        "non-null reason for unrequired view",
                        "null",
                        effective.source_ids);
                }
                continue;
            }

            var viewCurves = allCurves.Where(c => string.Equals(c.view_id, view.id, StringComparison.Ordinal)).ToList();
            var viewTargets = allTargets.Where(t => string.Equals(t.view_id, view.id, StringComparison.Ordinal)).ToList();
            var viewRegions = allRegions.Where(r => string.Equals(r.view_id, view.id, StringComparison.Ordinal)).ToList();

            var obstacles = new List<(string Id, Bounds2V2 Bounds, string Kind)>();

            // 1. Dimensions (H13)
            var viewDims = MakeDimensions(view, effective, viewCurves, rules);
            dimensions.AddRange(viewDims);
            foreach (var d in viewDims)
            {
                obstacles.Add((d.id, d.ink, "Dimension"));
            }

            // 2. Base labels (ViewTitle, Cao độ, Vật liệu)
            var (baseLabels, baseRoutes) = MakeBaseLabels(view, effective, viewTargets, viewRegions, assets, rules);
            labels.AddRange(baseLabels);
            routes.AddRange(baseRoutes);
            foreach (var bl in baseLabels)
            {
                obstacles.Add((bl.id, bl.ink, "Text"));
            }

            // Add keepout geometry curves
            foreach (var c in viewCurves.Where(c => string.Equals(c.role, "Geometry", StringComparison.OrdinalIgnoreCase)))
            {
                obstacles.Add((c.id, CurveBounds(c), "Geometry"));
            }

            // 3. Steel Rebar Labels per Group (H11 / H28)
            var groupsInView = allGroups
                .Where(g => viewTargets.Any(t => string.Equals(t.group_id, g.id, StringComparison.Ordinal)))
                .OrderBy(g => g.host_id, StringComparer.Ordinal)
                .ThenBy(g => g.face, StringComparer.Ordinal)
                .ThenBy(g => g.layer, StringComparer.Ordinal)
                .ThenBy(g => g.direction, StringComparer.Ordinal)
                .ThenBy(g => g.region_role, StringComparer.Ordinal)
                .ThenBy(g => g.id, StringComparer.Ordinal)
                .ToList();

            double scale = view.scale > 0 ? (view.scale / 100.0) : 0.3;

            var nr22Rule = (rules.entries ?? []).FirstOrDefault(r => string.Equals(r.id, "NR-22", StringComparison.Ordinal));
            double minGapPaper = (nr22Rule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, "min_ink_gap_paper_mm", StringComparison.OrdinalIgnoreCase))?.number ?? 2.0;
            double minGapModel = minGapPaper * (view.scale > 0 ? view.scale : 30.0);

            foreach (var group in groupsInView)
            {
                var groupTargets = viewTargets.Where(t => string.Equals(t.group_id, group.id, StringComparison.Ordinal)).ToList();
                if (groupTargets.Count == 0) continue;

                var recipe = FindLabelRecipe(rules, view.role, group.family, group.region_role, group.material_kind);

                // Build collectors
                var anchorPoints = groupTargets.Where(t => t.point != null).Select(t => t.point!).ToList();
                if (anchorPoints.Count == 0)
                {
                    anchorPoints.Add(new Point2V2(0, 0));
                }

                // Determine asset name & bound attributes
                string side = recipe?.Side ?? "Right";
                string assetName;
                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                if (string.Equals(group.material_kind, "Angle", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = "BLT_ANGLE";
                    values["NOTE"] = $"{group.display_mark}-{group.angle_code}";
                }
                else if (string.Equals(effective.note_style, "NoteStyle2", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase) ? "BLT4" : "BLT3";
                    values["NOTE"] = $"{group.display_mark}-%%c{SchematicPresentationPlanner.FormatValue(group.diameter_mm, "mm", rules, "Diameter")}";
                }
                else
                {
                    assetName = string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase) ? "BLT2" : "BLT1";
                    values["SH"] = group.display_mark;
                    values["DK"] = "%%c" + SchematicPresentationPlanner.FormatValue(group.diameter_mm, "mm", rules, "Diameter");
                    if (group.pitch_mm.HasValue)
                    {
                        values["A"] = (effective.pitch_symbol ?? "@") + SchematicPresentationPlanner.FormatValue(group.pitch_mm, "mm", rules, "Pitch");
                    }
                    else
                    {
                        values["A"] = "SL" + group.count.ToString(CultureInfo.InvariantCulture);
                    }
                }

                var asset = ExactAsset(assets, assetName);
                var boundAttrs = BindLabelAttributes(values, group, asset, recipe);
                var nativeInk = MeasureAssetInk(asset, boundAttrs, assets, rules);

                // Port relative to asset in native mm
                Point2V2 portNative = string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase)
                    ? new Point2V2(asset.bounds!.max_x_mm, 0.0)
                    : new Point2V2(asset.bounds!.min_x_mm, 0.0);

                string labelId = Encode(view.id, group.id, "BarLabel");

                // Finite lane allocation (D06)
                var lanes = LanesFromExplicitRecipe(view, anchorPoints, recipe, rules);
                var placement = AllocateLabelInLane(lanes, obstacles, nativeInk, portNative, scale, anchorPoints, minGapModel, labelId);

                var routeIds = new List<string>();

                for (int bIdx = 0; bIdx < placement.Routes.Count; bIdx++)
                {
                    var rPoints = placement.Routes[bIdx];
                    string rid = Encode(labelId, bIdx.ToString(CultureInfo.InvariantCulture));
                    routeIds.Add(rid);

                    var intersections = new List<MemberIntersectionV2>();
                    foreach (var target in groupTargets.Where(t => t.point != null))
                    {
                        intersections.Add(new MemberIntersectionV2(
                            occurrence_id: target.source_id,
                            point: target.point!,
                            collector_curve_id: rid
                        ));

                        if (string.Equals(group.family, "MainMesh", StringComparison.OrdinalIgnoreCase))
                        {
                            var gachAsset = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, "Gach", StringComparison.OrdinalIgnoreCase));
                            string gachName = gachAsset?.name ?? "Gach";
                            ticks.Add(new TickV2(
                                id: Encode(rid, target.source_id, "Tick"),
                                view_id: view.id,
                                asset_name: gachName,
                                occurrence_id: target.source_id,
                                point: target.point!,
                                scale: scale
                            ));
                        }
                    }

                    routes.Add(new RouteV2(
                        id: rid,
                        view_id: view.id,
                        group_id: group.id,
                        points: rPoints.ToArray(),
                        intersections: intersections.ToArray(),
                        label_ids: [labelId],
                        reason: "finite lane allocator"
                    ));
                }

                // If Tie, emit physical tie marker with REAL occurrence_id (D07)
                if (string.Equals(group.family, "Tie", StringComparison.OrdinalIgnoreCase))
                {
                    var bltcAsset = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, "BLTC", StringComparison.OrdinalIgnoreCase));
                    string bltcName = bltcAsset?.name ?? "BLTC";
                    foreach (var target in groupTargets.Where(t => t.point != null))
                    {
                        ticks.Add(new TickV2(
                            id: Encode(labelId, "TieMarker", target.source_id),
                            view_id: view.id,
                            asset_name: bltcName,
                            occurrence_id: target.source_id,
                            point: target.point!,
                            scale: scale
                        ));
                    }
                }

                var label = new LabelV2(
                    id: labelId,
                    view_id: view.id,
                    group_id: group.id,
                    target_ids: groupTargets.Select(t => t.id).ToArray(),
                    asset_name: assetName,
                    attributes: boundAttrs.ToArray(),
                    insertion: placement.Insertion,
                    port_native: portNative,
                    asset_scale: scale,
                    rotation_rad: 0.0,
                    ink: placement.Ink,
                    route_ids: routeIds.ToArray(),
                    source_ids: group.source_ids ?? []
                );

                labels.Add(label);
                obstacles.Add((labelId, placement.Ink, "Text"));
            }

            // 4. Hatches (H13)
            foreach (var region in viewRegions)
            {
                string patternName = string.Equals(region.material_role, "Bedding", StringComparison.OrdinalIgnoreCase)
                    ? "AR-SAND"
                    : "ANSI31";

                string hatchId = Encode(view.id, region.id, patternName);
                hatches.Add(new HatchV2(
                    id: hatchId,
                    view_id: view.id,
                    region_id: region.id,
                    pattern_name: patternName,
                    pattern_scale: 1.0,
                    angle_rad: 0.7853981633974483, // 45 deg
                    layer_id: "Hatch",
                    source_ids: []
                ));
            }

            // 5. Certified View Ink Envelope (D07: includes curves, dims, labels, routes, ticks, hatches)
            var allViewBounds = new List<Bounds2V2>();
            allViewBounds.AddRange(viewCurves.Select(CurveBounds));
            allViewBounds.AddRange(viewDims.Select(d => d.ink));
            allViewBounds.AddRange(labels.Where(l => string.Equals(l.view_id, view.id, StringComparison.Ordinal)).Select(l => l.ink));
            allViewBounds.AddRange(routes.Where(r => string.Equals(r.view_id, view.id, StringComparison.Ordinal)).Select(RouteBounds));
            allViewBounds.AddRange(ticks.Where(t => string.Equals(t.view_id, view.id, StringComparison.Ordinal)).Select(t => TickBounds(t, assets)));
            allViewBounds.AddRange(hatches.Where(h => string.Equals(h.view_id, view.id, StringComparison.Ordinal))
                .Select(h =>
                {
                    var reg = viewRegions.FirstOrDefault(r => string.Equals(r.id, h.region_id, StringComparison.Ordinal));
                    return reg != null ? RegionBounds(reg) : null;
                })
                .Where(b => b != null)!);

            if (allViewBounds.Count > 0)
            {
                var combined = allViewBounds.Aggregate(UnionBounds);
                viewInks.Add(new ViewInkV2(view.id, combined));
            }
        }

        return new AnnotationsV2(
            labels: labels.ToArray(),
            routes: routes.ToArray(),
            dimensions: dimensions.ToArray(),
            ticks: ticks.ToArray(),
            view_ink: viewInks.ToArray(),
            hatches: hatches.ToArray()
        );
    }

    private static List<DimensionV2> MakeDimensions(
        ViewV2 view,
        EffectiveV2 effective,
        List<ProjectedCurveV2> curves,
        RulesV2 rules)
    {
        var result = new List<DimensionV2>();
        bool isPlan = view.role.EndsWith("Plan", StringComparison.OrdinalIgnoreCase);

        if (isPlan)
        {
            // Clear span X
            double clearX = effective.clear_x_mm > 0 ? effective.clear_x_mm : 1500.0;
            double clearY = effective.clear_y_mm > 0 ? effective.clear_y_mm : 1500.0;

            var dX = new DimensionV2(
                id: Encode(view.id, "Dim_ClearX"),
                view_id: view.id,
                subject_id: "BodyClearX",
                start: new Point2V2(-clearX / 2.0, 0),
                end: new Point2V2(clearX / 2.0, 0),
                axis: "X",
                measured_mm: clearX,
                prefix: "",
                display_text: clearX.ToString("F0", CultureInfo.InvariantCulture),
                lane_mm: clearY / 2.0 + 300.0,
                ink: new Bounds2V2(-clearX / 2.0, clearY / 2.0 + 250.0, clearX / 2.0, clearY / 2.0 + 350.0),
                source_ids: effective.source_ids ?? []
            );
            result.Add(dX);

            // Clear span Y
            var dY = new DimensionV2(
                id: Encode(view.id, "Dim_ClearY"),
                view_id: view.id,
                subject_id: "BodyClearY",
                start: new Point2V2(0, -clearY / 2.0),
                end: new Point2V2(0, clearY / 2.0),
                axis: "Y",
                measured_mm: clearY,
                prefix: "",
                display_text: clearY.ToString("F0", CultureInfo.InvariantCulture),
                lane_mm: clearX / 2.0 + 300.0,
                ink: new Bounds2V2(clearX / 2.0 + 250.0, -clearY / 2.0, clearX / 2.0 + 350.0, clearY / 2.0),
                source_ids: effective.source_ids ?? []
            );
            result.Add(dY);
        }
        else
        {
            // Section heights
            double clearH = effective.clear_h_mm > 0 ? effective.clear_h_mm : 2000.0;
            var dH = new DimensionV2(
                id: Encode(view.id, "Dim_ClearH"),
                view_id: view.id,
                subject_id: "BodyClearH",
                start: new Point2V2(0, 0),
                end: new Point2V2(0, clearH),
                axis: "Y",
                measured_mm: clearH,
                prefix: "",
                display_text: clearH.ToString("F0", CultureInfo.InvariantCulture),
                lane_mm: 500.0,
                ink: new Bounds2V2(-550.0, 0, -450.0, clearH),
                source_ids: effective.source_ids ?? []
            );
            result.Add(dH);
        }

        return result;
    }

    private static (List<LabelV2> Labels, List<RouteV2> Routes) MakeBaseLabels(
        ViewV2 view,
        EffectiveV2 effective,
        List<Target2V2> targets,
        List<Region2V2> regions,
        AssetsV2 assets,
        RulesV2 rules)
    {
        var labels = new List<LabelV2>();
        var routes = new List<RouteV2>();

        // 1. ViewTitle
        var titleTarget = targets.FirstOrDefault(t => string.Equals(t.kind, "ViewTitle", StringComparison.OrdinalIgnoreCase));
        if (titleTarget != null)
        {
            string titleText = view.role switch
            {
                "StructurePlan" => "MẶT BẰNG GA",
                "AA" => "MẶT CẮT A-A",
                "BB" => "MẶT CẮT B-B",
                "CatchPlan" => "MẶT BẰNG CỬA THU",
                "Catch11" => "MẶT CẮT 1-1",
                "Catch22" => "MẶT CẮT 2-2",
                _ => view.role
            };

            var titleAsset = ExactAsset(assets, "TieudeMC");
            var titleAttrs = new[] { new BoundAttributeV2("TIEUDE", titleText, "Text", null, "none", effective.source_ids ?? []) };
            var pos = titleTarget.point ?? new Point2V2(0, -1000.0);
            var localInk = MeasureAssetInk(titleAsset, titleAttrs, assets, rules);
            var ink = TransformBounds(localInk, pos, 1.0);

            labels.Add(new LabelV2(
                id: Encode(view.id, "TitleLabel"),
                view_id: view.id,
                group_id: null,
                target_ids: [titleTarget.id],
                asset_name: titleAsset.name,
                attributes: titleAttrs,
                insertion: pos,
                port_native: new Point2V2(0, 0),
                asset_scale: 1.0,
                rotation_rad: 0.0,
                ink: ink,
                route_ids: [],
                source_ids: effective.source_ids ?? []
            ));
        }

        return (labels, routes);
    }

    private static (Point2V2 Insertion, Point2V2 Port, Bounds2V2 Ink, List<List<Point2V2>> Routes) AllocateLabelInLane(
        List<LaneDefinition> lanes,
        List<(string Id, Bounds2V2 Bounds, string Kind)> obstacles,
        Bounds2V2 nativeInk,
        Point2V2 portNative,
        double scale,
        List<Point2V2> anchorPoints,
        double minGapModel,
        string labelId)
    {
        var sortedAnchors = anchorPoints.OrderBy(p => p.y_mm).ThenBy(p => p.x_mm).ToList();
        var primaryAnchor = sortedAnchors.Count > 0 ? sortedAnchors[0] : new Point2V2(0, 0);

        var rejectedCandidates = new List<string>();

        foreach (var lane in lanes)
        {
            foreach (double offset in lane.CoordinateOffsets)
            {
                double targetCoord = lane.PreferredCoordinate + offset;
                var port = new Point2V2(
                    lane.Anchor.x_mm + lane.Direction.x_mm * targetCoord,
                    lane.Anchor.y_mm + lane.Direction.y_mm * targetCoord
                );

                var insertion = new Point2V2(
                    port.x_mm - portNative.x_mm * scale,
                    port.y_mm - portNative.y_mm * scale
                );

                var ink = TransformBounds(nativeInk, insertion, scale);
                var inflatedInk = new Bounds2V2(
                    ink.min_x_mm - minGapModel,
                    ink.min_y_mm - minGapModel,
                    ink.max_x_mm + minGapModel,
                    ink.max_y_mm + minGapModel
                );

                // Collision check against obstacles with min gap
                var colliding = obstacles.FirstOrDefault(o => BoundsIntersect(o.Bounds, inflatedInk));
                if (colliding != default)
                {
                    rejectedCandidates.Add($"Lane {lane.Id} at ({port.x_mm:F0},{port.y_mm:F0}): COLLISION with {colliding.Id} ({colliding.Kind})");
                    continue;
                }

                // Found a valid placement in finite lane candidate!
                var routePoints = new List<Point2V2>();
                foreach (var a in sortedAnchors)
                {
                    routePoints.Add(a);
                }
                routePoints.Add(port);

                return (insertion, port, ink, [routePoints]);
            }
        }

        // D06: Exhausted all finite lane candidates -> RAISE LAYOUT_ANNOTATION_NO_FIT with rejected candidates
        throw new DrawingStageFaultException(
            "LAYOUT_ANNOTATION_NO_FIT",
            labelId,
            ["candidates"],
            "clear candidate with required min gap",
            $"all {rejectedCandidates.Count} finite candidates rejected: {string.Join("; ", rejectedCandidates.Take(5))}",
            needsRuleIds: ["NR-22"]);
    }

    private static List<LaneDefinition> LanesFromExplicitRecipe(
        ViewV2 view,
        List<Point2V2> anchorPoints,
        LabelRecipe? recipe,
        RulesV2 rules)
    {
        var lanes = new List<LaneDefinition>();
        double anchorY = anchorPoints.Count > 0 ? anchorPoints.Average(p => p.y_mm) : 0.0;
        double anchorX = anchorPoints.Count > 0 ? anchorPoints.Average(p => p.x_mm) : 0.0;

        var nr22 = (rules.entries ?? []).FirstOrDefault(r => string.Equals(r.id, "NR-22", StringComparison.Ordinal));
        double laneOffset = (nr22?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, "lane_offset_native_mm", StringComparison.OrdinalIgnoreCase))?.number ?? 400.0;
        double extStep = (nr22?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, "lane_extension_step_native_mm", StringComparison.OrdinalIgnoreCase))?.number ?? 250.0;

        string preferredSide = recipe?.Side ?? "Right";
        double[] offsets = [0.0, 100.0, -100.0, 200.0, -200.0, 300.0, -300.0];

        if (string.Equals(preferredSide, "Left", StringComparison.OrdinalIgnoreCase))
        {
            lanes.Add(new LaneDefinition(Encode(view.id, "LeftLane", "Primary"), "Left", new Point2V2(anchorX - laneOffset, 0), new Point2V2(0, 1), anchorY, offsets));
            lanes.Add(new LaneDefinition(Encode(view.id, "LeftLane", "Ext1"), "Left", new Point2V2(anchorX - laneOffset - extStep, 0), new Point2V2(0, 1), anchorY, offsets));
            lanes.Add(new LaneDefinition(Encode(view.id, "LeftLane", "Ext2"), "Left", new Point2V2(anchorX - laneOffset - 2 * extStep, 0), new Point2V2(0, 1), anchorY, offsets));
        }
        else if (string.Equals(preferredSide, "Top", StringComparison.OrdinalIgnoreCase))
        {
            lanes.Add(new LaneDefinition(Encode(view.id, "TopLane", "Primary"), "Top", new Point2V2(0, anchorY + laneOffset), new Point2V2(1, 0), anchorX, offsets));
            lanes.Add(new LaneDefinition(Encode(view.id, "TopLane", "Ext1"), "Top", new Point2V2(0, anchorY + laneOffset + extStep), new Point2V2(1, 0), anchorX, offsets));
        }
        else
        {
            // Default: Right
            lanes.Add(new LaneDefinition(Encode(view.id, "RightLane", "Primary"), "Right", new Point2V2(anchorX + laneOffset, 0), new Point2V2(0, 1), anchorY, offsets));
            lanes.Add(new LaneDefinition(Encode(view.id, "RightLane", "Ext1"), "Right", new Point2V2(anchorX + laneOffset + extStep, 0), new Point2V2(0, 1), anchorY, offsets));
            lanes.Add(new LaneDefinition(Encode(view.id, "RightLane", "Ext2"), "Right", new Point2V2(anchorX + laneOffset + 2 * extStep, 0), new Point2V2(0, 1), anchorY, offsets));
        }

        return lanes;
    }

    private static List<BoundAttributeV2> BindLabelAttributes(
        Dictionary<string, string> values,
        GroupV2 group,
        AssetV2 asset,
        LabelRecipe? recipe)
    {
        var result = new List<BoundAttributeV2>();
        foreach (var (k, v) in values)
        {
            double? num = null;
            string unit = "none";

            if (string.Equals(k, "DK", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "Diameter", StringComparison.OrdinalIgnoreCase))
            {
                num = group.diameter_mm;
                unit = "mm";
            }
            else if (string.Equals(k, "A", StringComparison.OrdinalIgnoreCase) && group.pitch_mm.HasValue)
            {
                num = group.pitch_mm;
                unit = "mm";
            }
            else if (string.Equals(k, "SL", StringComparison.OrdinalIgnoreCase))
            {
                num = group.count;
                unit = "count";
            }

            result.Add(new BoundAttributeV2(
                tag: k,
                text: v,
                value_kind: num.HasValue ? "Number" : "Text",
                raw_number: num,
                unit: unit,
                source_ids: group.source_ids ?? []
            ));
        }

        return result;
    }

    public static Bounds2V2 MeasureAssetInk(
        AssetV2 asset,
        IEnumerable<BoundAttributeV2> attributes,
        AssetsV2 assets,
        RulesV2 rules)
    {
        if (asset.bounds == null)
        {
            throw new DrawingStageFaultException(
                "ASSET_BOUNDS_MISSING",
                asset.name,
                ["bounds"],
                "non-null bounds",
                "null");
        }

        var box = asset.bounds;

        foreach (var bound in attributes)
        {
            var def = (asset.attributes ?? []).FirstOrDefault(a => string.Equals(a.tag, bound.tag, StringComparison.OrdinalIgnoreCase));
            if (def == null) continue;

            var style = (assets.font_styles ?? []).FirstOrDefault(s => string.Equals(s.style_id, def.style_id, StringComparison.OrdinalIgnoreCase));
            if (style == null)
            {
                throw new DrawingStageFaultException(
                    "FONT_STYLE_MISSING",
                    def.style_id,
                    ["assets.font_styles"],
                    "exact font style definition",
                    "null");
            }

            if (assets.glyphs == null || assets.glyphs.Length == 0)
            {
                throw new DrawingStageFaultException(
                    "GLYPH_METRIC_MISSING",
                    def.style_id,
                    ["assets.glyphs"],
                    "non-empty glyphs table",
                    "empty");
            }

            string rawText = bound.text ?? "";
            string text = DecodeAutoCadEscapes(rawText);

            double penX = 0.0;
            Bounds2V2? rawInk = null;
            int? previous = null;

            foreach (int cp in text)
            {
                var glyph = assets.glyphs.FirstOrDefault(g =>
                    string.Equals(g.style_id, def.style_id, StringComparison.OrdinalIgnoreCase) &&
                    g.codepoint == cp);

                if (glyph == null)
                {
                    throw new DrawingStageFaultException(
                        "GLYPH_METRIC_MISSING",
                        def.style_id,
                        ["codepoint"],
                        $"exact glyph for codepoint {cp} ('{(char)cp}')",
                        "null");
                }

                if (glyph.reference_height_mm <= 0)
                {
                    throw new DrawingStageFaultException(
                        "GLYPH_METRIC_INVALID",
                        def.style_id,
                        ["reference_height_mm"],
                        "> 0",
                        glyph.reference_height_mm.ToString(CultureInfo.InvariantCulture));
                }

                double factor = def.height_mm / glyph.reference_height_mm;
                if (previous.HasValue && assets.kerning != null)
                {
                    var pair = assets.kerning.FirstOrDefault(k =>
                        string.Equals(k.style_id, def.style_id, StringComparison.OrdinalIgnoreCase) &&
                        k.left_codepoint == previous.Value &&
                        k.right_codepoint == cp);
                    if (pair != null)
                    {
                        penX += pair.adjustment_native_mm * factor * def.width_factor;
                    }
                }

                var scaledGlyph = new Bounds2V2(
                    glyph.ink.min_x_mm * factor * def.width_factor + penX,
                    glyph.ink.min_y_mm * factor,
                    glyph.ink.max_x_mm * factor * def.width_factor + penX,
                    glyph.ink.max_y_mm * factor
                );
                rawInk = rawInk == null ? scaledGlyph : UnionBounds(rawInk, scaledGlyph);
                penX += glyph.advance_native_mm * factor * def.width_factor;
                previous = cp;
            }

            if (rawInk != null)
            {
                double shiftX = def.horizontal_mode switch
                {
                    "Center" => -penX / 2.0,
                    "Right" => -penX,
                    _ => 0.0
                };

                double shiftY = def.vertical_mode switch
                {
                    "Bottom" => -rawInk.min_y_mm,
                    "Middle" => -(rawInk.min_y_mm + rawInk.max_y_mm) / 2.0,
                    "Top" => -rawInk.max_y_mm,
                    _ => 0.0
                };

                var anchor = (string.Equals(def.horizontal_mode, "Left", StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(def.vertical_mode, "Baseline", StringComparison.OrdinalIgnoreCase))
                    ? def.insertion
                    : def.alignment;

                var shifted = new Bounds2V2(
                    rawInk.min_x_mm + shiftX,
                    rawInk.min_y_mm + shiftY,
                    rawInk.max_x_mm + shiftX,
                    rawInk.max_y_mm + shiftY
                );

                var rotated = RotateBounds(shifted, def.rotation_rad);
                var placed = new Bounds2V2(
                    rotated.min_x_mm + anchor.x_mm,
                    rotated.min_y_mm + anchor.y_mm,
                    rotated.max_x_mm + anchor.x_mm,
                    rotated.max_y_mm + anchor.y_mm
                );

                box = UnionBounds(box, placed);
            }
        }

        return box;
    }

    private static string DecodeAutoCadEscapes(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i + 2 < text.Length && text[i] == '%' && text[i + 1] == '%')
            {
                char c = char.ToLowerInvariant(text[i + 2]);
                switch (c)
                {
                    case 'c': sb.Append('Ø'); i += 2; continue;
                    case 'p': sb.Append('±'); i += 2; continue;
                    case 'd': sb.Append('°'); i += 2; continue;
                    case '%': sb.Append('%'); i += 2; continue;
                    default:
                        throw new DrawingStageFaultException(
                            "TEXT_FORMAT_UNSUPPORTED",
                            "Escape",
                            ["escape"],
                            "supported escape (%%c, %%p, %%d, %%%)",
                            text.Substring(i, 3));
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    public static AssetV2 ExactAsset(AssetsV2 assets, string name)
    {
        var found = (assets.definitions ?? []).Where(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (found.Count != 1)
        {
            throw new DrawingStageFaultException(
                "ASSET_EXACT_NAME_MISSING",
                name,
                ["assets.definitions"],
                "one exact definition",
                found.Count.ToString(CultureInfo.InvariantCulture));
        }
        return found[0];
    }

    private static LabelRecipe? FindLabelRecipe(RulesV2 rules, string viewRole, string family, string regionRole, string materialKind)
    {
        var rule = (rules.entries ?? []).FirstOrDefault(r => string.Equals(r.id, "NR-22", StringComparison.Ordinal));
        string? sideParam = (rule?.parameters ?? []).FirstOrDefault(p =>
            string.Equals(p.key, $"side.{viewRole}.{family}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.key, $"side.{family}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.key, "label_side", StringComparison.OrdinalIgnoreCase))?.text;

        string side = !string.IsNullOrWhiteSpace(sideParam) ? sideParam : "Right";
        return new LabelRecipe(Side: side);
    }

    private static Bounds2V2 CurveBounds(ProjectedCurveV2 c)
    {
        double minX = Math.Min(c.start.x_mm, c.end.x_mm);
        double maxX = Math.Max(c.start.x_mm, c.end.x_mm);
        double minY = Math.Min(c.start.y_mm, c.end.y_mm);
        double maxY = Math.Max(c.start.y_mm, c.end.y_mm);

        if (c.center != null && c.radius_mm.HasValue)
        {
            minX = Math.Min(minX, c.center.x_mm - c.radius_mm.Value);
            maxX = Math.Max(maxX, c.center.x_mm + c.radius_mm.Value);
            minY = Math.Min(minY, c.center.y_mm - c.radius_mm.Value);
            maxY = Math.Max(maxY, c.center.y_mm + c.radius_mm.Value);
        }

        return new Bounds2V2(minX, minY, maxX, maxY);
    }

    private static Bounds2V2 RouteBounds(RouteV2 r)
    {
        if (r.points == null || r.points.Length == 0) return new Bounds2V2(0, 0, 0, 0);
        double minX = r.points.Min(p => p.x_mm);
        double maxX = r.points.Max(p => p.x_mm);
        double minY = r.points.Min(p => p.y_mm);
        double maxY = r.points.Max(p => p.y_mm);
        return new Bounds2V2(minX - 0.25, minY - 0.25, maxX + 0.25, maxY + 0.25);
    }

    private static Bounds2V2 TickBounds(TickV2 t, AssetsV2 assets)
    {
        var asset = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, t.asset_name, StringComparison.OrdinalIgnoreCase));
        var bounds = asset?.bounds ?? new Bounds2V2(-5, -5, 5, 5);
        return TransformBounds(bounds, t.point, t.scale);
    }

    private static Bounds2V2 RegionBounds(Region2V2 r)
    {
        var allCurves = (r.loops ?? []).SelectMany(l => l.curves ?? []).ToList();
        if (allCurves.Count == 0) return new Bounds2V2(0, 0, 0, 0);
        double minX = allCurves.Min(c => Math.Min(c.start.x_mm, c.end.x_mm));
        double maxX = allCurves.Max(c => Math.Max(c.start.x_mm, c.end.x_mm));
        double minY = allCurves.Min(c => Math.Min(c.start.y_mm, c.end.y_mm));
        double maxY = allCurves.Max(c => Math.Max(c.start.y_mm, c.end.y_mm));
        return new Bounds2V2(minX, minY, maxX, maxY);
    }

    private static Bounds2V2 RotateBounds(Bounds2V2 b, double rad)
    {
        if (Math.Abs(rad) < 1e-6) return b;

        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        var points = new[]
        {
            (b.min_x_mm, b.min_y_mm),
            (b.max_x_mm, b.min_y_mm),
            (b.max_x_mm, b.max_y_mm),
            (b.min_x_mm, b.max_y_mm)
        };

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (x, y) in points)
        {
            double rx = x * cos - y * sin;
            double ry = x * sin + y * cos;
            if (rx < minX) minX = rx;
            if (rx > maxX) maxX = rx;
            if (ry < minY) minY = ry;
            if (ry > maxY) maxY = ry;
        }

        return new Bounds2V2(minX, minY, maxX, maxY);
    }

    private static Bounds2V2 TransformBounds(Bounds2V2 b, Point2V2 offset, double scale)
    {
        double x0 = b.min_x_mm * scale + offset.x_mm;
        double y0 = b.min_y_mm * scale + offset.y_mm;
        double x1 = b.max_x_mm * scale + offset.x_mm;
        double y1 = b.max_y_mm * scale + offset.y_mm;
        return new Bounds2V2(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
    }

    private static Bounds2V2 UnionBounds(Bounds2V2 a, Bounds2V2 b)
    {
        return new Bounds2V2(
            Math.Min(a.min_x_mm, b.min_x_mm),
            Math.Min(a.min_y_mm, b.min_y_mm),
            Math.Max(a.max_x_mm, b.max_x_mm),
            Math.Max(a.max_y_mm, b.max_y_mm)
        );
    }

    private static bool BoundsIntersect(Bounds2V2 a, Bounds2V2 b)
    {
        return a.min_x_mm <= b.max_x_mm && a.max_x_mm >= b.min_x_mm &&
               a.min_y_mm <= b.max_y_mm && a.max_y_mm >= b.min_y_mm;
    }

    private static string Encode(params string?[] parts)
    {
        return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private sealed record LaneDefinition(
        string Id,
        string Side,
        Point2V2 Anchor,
        Point2V2 Direction,
        double PreferredCoordinate,
        double[] CoordinateOffsets
    );

    private sealed record LabelRecipe(string Side);
}
