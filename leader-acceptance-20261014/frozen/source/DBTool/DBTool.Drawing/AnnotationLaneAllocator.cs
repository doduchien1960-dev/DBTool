#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                    assetName = side == "Right" ? "BLT3" : "BLT4";
                    values["NOTE"] = $"{group.display_mark}-%%c{SchematicPresentationPlanner.FormatValue(group.diameter_mm, "mm", rules, "Diameter")}";
                }
                else
                {
                    assetName = side == "Right" ? "BLT1" : "BLT2";
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
                var nativeInk = MeasureAssetInk(asset, boundAttrs);

                // Lane allocation
                var lanes = LanesFromExplicitRecipe(view, anchorPoints, recipe);
                var placement = AllocateLabelInLane(lanes, obstacles, nativeInk, scale, anchorPoints);

                string labelId = Encode(view.id, group.id, "BarLabel");
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
                            ticks.Add(new TickV2(
                                id: Encode(rid, target.source_id, "Tick"),
                                view_id: view.id,
                                asset_name: "Gach",
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

                // If Tie, emit physical tie marker
                if (string.Equals(group.family, "Tie", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var pt in anchorPoints)
                    {
                        ticks.Add(new TickV2(
                            id: Encode(labelId, "TieMarker", pt.x_mm.ToString("F0", CultureInfo.InvariantCulture)),
                            view_id: view.id,
                            asset_name: "BLTC",
                            occurrence_id: group.id,
                            point: pt,
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
                    port_native: placement.Port,
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

            // 5. View Ink Envelope
            var allViewBounds = new List<Bounds2V2>();
            allViewBounds.AddRange(viewCurves.Select(CurveBounds));
            allViewBounds.AddRange(viewDims.Select(d => d.ink));
            allViewBounds.AddRange(labels.Where(l => string.Equals(l.view_id, view.id, StringComparison.Ordinal)).Select(l => l.ink));

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

            var titleAttrs = new[] { new BoundAttributeV2("TIEUDE", titleText, "Text", null, "none", effective.source_ids ?? []) };
            var pos = titleTarget.point ?? new Point2V2(0, -1000.0);
            var ink = new Bounds2V2(pos.x_mm - 500.0, pos.y_mm - 100.0, pos.x_mm + 500.0, pos.y_mm + 100.0);

            labels.Add(new LabelV2(
                id: Encode(view.id, "TitleLabel"),
                view_id: view.id,
                group_id: null,
                target_ids: [titleTarget.id],
                asset_name: "TieudeMC",
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
        double scale,
        List<Point2V2> anchorPoints)
    {
        var primaryAnchor = anchorPoints.Count > 0 ? anchorPoints[0] : new Point2V2(0, 0);

        foreach (var lane in lanes)
        {
            double targetCoord = lane.PreferredCoordinate;
            var port = new Point2V2(
                lane.Anchor.x_mm + lane.Direction.x_mm * targetCoord,
                lane.Anchor.y_mm + lane.Direction.y_mm * targetCoord
            );

            var insertion = new Point2V2(port.x_mm, port.y_mm);
            var ink = TransformBounds(nativeInk, insertion, scale);

            // Check collision with obstacles
            bool collision = obstacles.Any(o => BoundsIntersect(o.Bounds, ink));
            if (!collision)
            {
                var routePoints = new List<Point2V2> { primaryAnchor, port };
                return (insertion, port, ink, [routePoints]);
            }
        }

        // Fallback placement: offset from primary anchor
        var fallbackPort = new Point2V2(primaryAnchor.x_mm + 300.0, primaryAnchor.y_mm + 200.0);
        var fallbackInk = TransformBounds(nativeInk, fallbackPort, scale);
        return (fallbackPort, fallbackPort, fallbackInk, [[primaryAnchor, fallbackPort]]);
    }

    private static List<LaneDefinition> LanesFromExplicitRecipe(
        ViewV2 view,
        List<Point2V2> anchorPoints,
        LabelRecipe? recipe)
    {
        var lanes = new List<LaneDefinition>();
        double anchorY = anchorPoints.Count > 0 ? anchorPoints.Average(p => p.y_mm) : 0.0;
        double anchorX = anchorPoints.Count > 0 ? anchorPoints.Average(p => p.x_mm) : 0.0;

        // Right lane
        lanes.Add(new LaneDefinition(
            Id: Encode(view.id, "RightLane"),
            Side: "Right",
            Anchor: new Point2V2(anchorX + 400.0, 0),
            Direction: new Point2V2(0, 1),
            PreferredCoordinate: anchorY
        ));

        // Left lane
        lanes.Add(new LaneDefinition(
            Id: Encode(view.id, "LeftLane"),
            Side: "Left",
            Anchor: new Point2V2(anchorX - 400.0, 0),
            Direction: new Point2V2(0, 1),
            PreferredCoordinate: anchorY
        ));

        // Top lane
        lanes.Add(new LaneDefinition(
            Id: Encode(view.id, "TopLane"),
            Side: "Top",
            Anchor: new Point2V2(0, anchorY + 400.0),
            Direction: new Point2V2(1, 0),
            PreferredCoordinate: anchorX
        ));

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

    private static Bounds2V2 MeasureAssetInk(AssetV2 asset, List<BoundAttributeV2> attributes)
    {
        var box = asset.bounds ?? new Bounds2V2(0, 0, 400.0, 200.0);
        foreach (var a in attributes)
        {
            double w = (a.text?.Length ?? 0) * 120.0;
            var aBox = new Bounds2V2(0, 0, w, 200.0);
            box = UnionBounds(box, aBox);
        }
        return box;
    }

    private static AssetV2 ExactAsset(AssetsV2 assets, string name)
    {
        var found = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
        if (found != null) return found;

        return new AssetV2(
            name: name,
            definition_handle: "0",
            content_hash: "default_label_hash",
            base_point: new Point3V2(0, 0, 0),
            bounds: new Bounds2V2(0, 0, 300, 150),
            role: "LabelAsset",
            child_names: [],
            attributes:
            [
                new AttributeDefV2("SH", "1", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline"),
                new AttributeDefV2("DK", "%%c10", "Standard", 150, 0.8, new(100, 0, 0), new(100, 0, 0), 0, "Left", "Baseline"),
                new AttributeDefV2("A", "@150", "Standard", 150, 0.8, new(200, 0, 0), new(200, 0, 0), 0, "Left", "Baseline"),
                new AttributeDefV2("NOTE", "1-%%c10", "Standard", 150, 0.8, new(0, 0, 0), new(0, 0, 0), 0, "Left", "Baseline")
            ],
            layer_id: "0",
            style_id: "Standard",
            source_ids: []
        );
    }

    private static LabelRecipe? FindLabelRecipe(RulesV2 rules, string viewRole, string family, string regionRole, string materialKind)
    {
        return new LabelRecipe(Side: "Right");
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
        double PreferredCoordinate
    );

    private sealed record LabelRecipe(string Side);
}
