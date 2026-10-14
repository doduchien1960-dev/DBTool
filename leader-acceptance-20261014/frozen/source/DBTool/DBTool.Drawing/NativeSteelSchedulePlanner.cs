#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// S8 Native steel schedule planner.
/// Plans exact native TK schedule rows, table headings, summaries, and ink envelopes
/// from presentation groups and approved shape recipes.
/// </summary>
public sealed class NativeSteelSchedulePlanner
{
    /// <summary>
    /// Plans schedule rows, headings, bending captions, and mass summaries.
    /// </summary>
    public SchedulesV2 PlanRowsAndSummaries(
        EffectiveV2 effective,
        GeometryV2 geometry,
        GroupsV2 groups,
        AssetsV2 assets,
        RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(rules);

        var items = groups.items ?? [];
        if (items.Length == 0)
        {
            return new SchedulesV2([], [], [], [], []);
        }

        // Validate group counts
        foreach (var g in items)
        {
            if (g.count <= 0 || (g.member_ids != null && g.member_ids.Length != g.count))
            {
                throw new DrawingStageFaultException(
                    "SCHEDULE_GROUP_COUNT_INVALID",
                    g.id,
                    ["count", "member_ids"],
                    ">0 and equal",
                    $"{g.count} != {g.member_ids?.Length ?? 0}",
                    g.source_ids);
            }
        }

        var rows = new List<ScheduleRowV2>();
        var headings = new List<ScheduleHeadingV2>();
        var summaries = new List<SummaryV2>();
        var inkList = new List<ViewInkV2>();

        // Bucket members
        var bucketGroups = items.GroupBy(g => PhysicalOwnerBucket(g, geometry, rules))
            .OrderBy(b => BucketSortKey(b.Key), StringComparer.Ordinal)
            .ToList();

        foreach (var bucket in bucketGroups)
        {
            string bucketKey = bucket.Key;
            string scheduleView = Encode(effective.id, "Schedule", bucketKey);
            string bendingView = Encode(effective.id, "Bending", bucketKey);

            double s = (string.Equals(bucketKey, "Panel", StringComparison.OrdinalIgnoreCase) && effective.panel_scale.HasValue)
                ? effective.panel_scale.Value
                : effective.body_scale;

            if (s <= 0) s = 30.0;
            double scale = s / 100.0;

            var layoutRule = FindRule(rules, "NR-22", effective.profile, bucketKey, "ScheduleLayout");
            double cursorY = (GetParamNumber(layoutRule, "first_row_y_native_mm") ?? 0.0) * scale;

            var orderedRows = bucket
                .OrderBy(g => g.host_id, StringComparer.Ordinal)
                .ThenBy(g => g.face, StringComparer.Ordinal)
                .ThenBy(g => g.layer, StringComparer.Ordinal)
                .ThenBy(g => g.direction, StringComparer.Ordinal)
                .ThenBy(g => g.region_role, StringComparer.Ordinal)
                .ThenBy(g => g.id, StringComparer.Ordinal)
                .ToList();

            for (int index = 0; index < orderedRows.Count; index++)
            {
                var group = orderedRows[index];
                var recipe = FindShapeRecipe(rules, group.shape_recipe_id, effective.profile, group.family);
                string assetName = recipe?.native_asset ?? ResolveDefaultAsset(group);

                var asset = ExactAsset(assets, assetName);
                var attrs = BindRowAttributes(group, recipe, asset, bucketKey, index == 0, rules);

                var localInk = MeasureAssetInk(asset, attrs, assets, rules);
                var insertion = new Point2V2(0, cursorY);
                var ink = TransformBounds(localInk, insertion, scale);

                rows.Add(new ScheduleRowV2(
                    id: Encode(scheduleView, group.id),
                    group_id: group.id,
                    native_asset: asset.name,
                    attributes: attrs.ToArray(),
                    glyph_hash: asset.content_hash,
                    insertion: insertion,
                    scale: scale,
                    ink: ink,
                    view_id: scheduleView
                ));

                if (index == 0)
                {
                    EmitHeadings(headings, scheduleView, bendingView, group, bucketKey, insertion, scale, layoutRule, assets, rules);
                }

                cursorY -= 500.0 * scale; // 05B-F15 row pitch
            }
        }

        // Bending captions
        var presentationPlanner = new SchematicPresentationPlanner();
        var captions = presentationPlanner.PlanCaptions(effective, groups, assets, rules);

        // Summaries
        var summaryScopes = new[] { "Main", "Panel", "Catch" };
        foreach (var scope in summaryScopes)
        {
            var included = items.Where(g =>
            {
                string b = PhysicalOwnerBucket(g, geometry, rules);
                return scope switch
                {
                    "Main" => string.Equals(b, "Body", StringComparison.OrdinalIgnoreCase) || string.Equals(b, "Neck", StringComparison.OrdinalIgnoreCase),
                    "Panel" => string.Equals(b, "Panel", StringComparison.OrdinalIgnoreCase),
                    "Catch" => string.Equals(b, "Catch", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }).ToList();

            if (included.Count == 0) continue;

            string targetBucket = scope switch
            {
                "Main" => included.Any(g => string.Equals(PhysicalOwnerBucket(g, geometry, rules), "Body", StringComparison.OrdinalIgnoreCase)) ? "Body" : "Neck",
                "Panel" => "Panel",
                "Catch" => "Catch",
                _ => "Body"
            };

            string viewId = Encode(effective.id, "Schedule", targetBucket);
            double s = (string.Equals(scope, "Panel", StringComparison.OrdinalIgnoreCase) && effective.panel_scale.HasValue)
                ? effective.panel_scale.Value
                : effective.body_scale;

            if (s <= 0) s = 30.0;
            double scale = s / 100.0;

            string assetName = scope switch
            {
                "Main" => "TK.THKL",
                "Panel" => "TK.THKLNAP",
                "Catch" => "TK.THKL.CT",
                _ => "TK.THKL"
            };

            var asset = ExactAsset(assets, assetName);
            var summaryRule = FindRule(rules, "NR-14", effective.profile, scope, "Summary");

            double small = included
                .Where(g => string.Equals(g.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10)
                .Sum(g => g.mass_kg ?? 0.0);

            double large = included
                .Where(g => string.Equals(g.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10)
                .Sum(g => g.mass_kg ?? 0.0);

            var attrs = BindSummaryTags(asset, small, large, included, summaryRule, rules);

            // Position below previous rows
            var viewRows = rows.Where(r => string.Equals(r.view_id, viewId, StringComparison.Ordinal)).ToList();
            double minY = viewRows.Count > 0 ? viewRows.Min(r => r.ink.min_y_mm) : 0.0;
            double minX = viewRows.Count > 0 ? viewRows.Min(r => r.ink.min_x_mm) : 0.0;

            double summaryGap = (GetParamNumber(summaryRule, "summary_gap_paper_mm") ?? 5.0) * s;
            var pos = new Point2V2(minX, minY - summaryGap);
            var localSummaryInk = MeasureAssetInk(asset, attrs, assets, rules);
            var ink = TransformBounds(localSummaryInk, pos, scale);

            summaries.Add(new SummaryV2(
                id: Encode(viewId, "Summary"),
                scope: scope,
                native_asset: assetName,
                attributes: attrs.ToArray(),
                mass_small_kg: small,
                mass_large_kg: large,
                view_id: viewId,
                insertion: pos,
                asset_scale: scale,
                ink: ink
            ));
        }

        // Compute ink envelopes per view
        var allViewIds = rows.Select(r => r.view_id)
            .Concat(captions.Select(c => c.view_id))
            .Concat(headings.Select(h => h.view_id))
            .Concat(summaries.Select(s => s.view_id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

        foreach (var vid in allViewIds)
        {
            var viewInks = new List<Bounds2V2>();
            viewInks.AddRange(rows.Where(r => string.Equals(r.view_id, vid, StringComparison.Ordinal)).Select(r => r.ink));
            viewInks.AddRange(captions.Where(c => string.Equals(c.view_id, vid, StringComparison.Ordinal)).Select(c => c.ink));
            viewInks.AddRange(headings.Where(h => string.Equals(h.view_id, vid, StringComparison.Ordinal)).Select(h => h.ink));
            viewInks.AddRange(summaries.Where(s => string.Equals(s.view_id, vid, StringComparison.Ordinal)).Select(s => s.ink));

            if (viewInks.Count > 0)
            {
                var combined = viewInks.Aggregate(UnionBounds);
                inkList.Add(new ViewInkV2(vid, combined));
            }
        }

        // Coverage assertion
        var scheduledGroupIds = rows.Select(r => r.group_id).ToHashSet(StringComparer.Ordinal);
        var inputGroupIds = items.Select(g => g.id).ToHashSet(StringComparer.Ordinal);
        if (!scheduledGroupIds.SetEquals(inputGroupIds))
        {
            throw new DrawingStageFaultException(
                "SCHEDULE_GROUP_COVERAGE",
                effective.id,
                ["rows.group_id"],
                $"all {inputGroupIds.Count} groups covered",
                $"{scheduledGroupIds.Count} scheduled",
                effective.source_ids);
        }

        return new SchedulesV2(
            rows: rows.ToArray(),
            captions: captions,
            summaries: summaries.ToArray(),
            ink: inkList.ToArray(),
            headings: headings.ToArray()
        );
    }

    private static string PhysicalOwnerBucket(GroupV2 group, GeometryV2 geometry, RulesV2 rules)
    {
        var host = (geometry.components ?? []).FirstOrDefault(c => string.Equals(c.id, group.host_id, StringComparison.Ordinal));
        if (host != null)
        {
            if (string.Equals(host.kind, "Panel", StringComparison.OrdinalIgnoreCase)) return "Panel";
            if (string.Equals(host.kind, "Catch", StringComparison.OrdinalIgnoreCase)) return "Catch";
            if (string.Equals(host.kind, "Neck", StringComparison.OrdinalIgnoreCase)) return "Neck";
            return "Body";
        }

        return SchematicPresentationPlanner.PhysicalOwnerBucket(group, rules);
    }

    private static string BucketSortKey(string bucket) => bucket switch
    {
        "Body" => "1_Body",
        "Neck" => "2_Neck",
        "Panel" => "3_Panel",
        "Catch" => "4_Catch",
        _ => "5_" + bucket
    };

    private static List<BoundAttributeV2> BindRowAttributes(
        GroupV2 group,
        ShapeRecipeV2? recipe,
        AssetV2 asset,
        string bucketKey,
        bool isFirstRow,
        RulesV2 rules)
    {
        var attrs = new List<BoundAttributeV2>();
        var bindings = recipe?.tag_bindings ?? [];

        if (bindings.Length > 0)
        {
            foreach (var b in bindings)
            {
                string tag = b.tag;
                string value;
                double? raw = null;
                string unit = b.unit ?? "none";

                switch (b.operation)
                {
                    case "Literal":
                        value = b.literal ?? "";
                        break;
                    case "Mark":
                        value = group.display_mark;
                        break;
                    case "Diameter":
                        raw = group.diameter_mm;
                        unit = "mm";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Diameter");
                        break;
                    case "RoleMean":
                        var roleVal = (group.mean_roles ?? []).FirstOrDefault(r => string.Equals(r.role, b.role, StringComparison.OrdinalIgnoreCase));
                        raw = roleVal?.value_mm ?? 0.0;
                        unit = "mm";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Leg");
                        break;
                    case "MeanLength":
                        raw = group.mean_mm;
                        unit = "mm";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "DAI");
                        break;
                    case "Count":
                        raw = group.count;
                        unit = "count";
                        value = group.count.ToString(CultureInfo.InvariantCulture);
                        break;
                    case "RawSumM":
                        raw = group.raw_sum_mm / 1000.0;
                        unit = "m";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "TDAI");
                        break;
                    case "MassKg":
                        raw = group.mass_kg;
                        unit = "kg";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "KL");
                        break;
                    case "OwnerLabel":
                        value = isFirstRow ? BucketTitleLiteral(bucketKey, rules) : "";
                        break;
                    case "AngleCode":
                        value = group.angle_code ?? "";
                        break;
                    case "UnitMass":
                        raw = group.unit_mass_kg_per_m;
                        unit = "kg/m";
                        value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "AngleUnitMass");
                        break;
                    default:
                        value = b.literal ?? "";
                        break;
                }

                attrs.Add(new BoundAttributeV2(
                    tag: tag,
                    text: value,
                    value_kind: raw.HasValue ? "Number" : "Text",
                    raw_number: raw,
                    unit: unit,
                    source_ids: group.source_ids ?? []
                ));
            }
        }
        else
        {
            // Default tag bindings based on native asset attributes
            foreach (var att in asset.attributes ?? [])
            {
                string tag = att.tag;
                string value = tag switch
                {
                    "SH" => group.display_mark,
                    "D" or "DK" => group.diameter_mm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    "DAI" => SchematicPresentationPlanner.FormatValue(group.mean_mm, "mm", rules, "DAI"),
                    "SL" => group.count.ToString(CultureInfo.InvariantCulture),
                    "TDAI" => SchematicPresentationPlanner.FormatValue(group.raw_sum_mm / 1000.0, "m", rules, "TDAI"),
                    "KL" => SchematicPresentationPlanner.FormatValue(group.mass_kg, "kg", rules, "KL"),
                    "TENLUOI" => isFirstRow ? BucketTitleLiteral(bucketKey, rules) : "",
                    _ => ""
                };

                double? raw = tag switch
                {
                    "D" or "DK" => group.diameter_mm,
                    "DAI" => group.mean_mm,
                    "SL" => group.count,
                    "TDAI" => group.raw_sum_mm / 1000.0,
                    "KL" => group.mass_kg,
                    _ => null
                };

                string unit = tag switch
                {
                    "D" or "DK" or "DAI" => "mm",
                    "SL" => "count",
                    "TDAI" => "m",
                    "KL" => "kg",
                    _ => "none"
                };

                attrs.Add(new BoundAttributeV2(
                    tag: tag,
                    text: value,
                    value_kind: raw.HasValue ? "Number" : "Text",
                    raw_number: raw,
                    unit: unit,
                    source_ids: group.source_ids ?? []
                ));
            }
        }

        return attrs;
    }

    private static List<BoundAttributeV2> BindSummaryTags(
        AssetV2 asset,
        double small,
        double large,
        List<GroupV2> groups,
        RuleV2? rule,
        RulesV2 rules)
    {
        var attrs = new List<BoundAttributeV2>();
        var sourceIds = groups.SelectMany(g => g.source_ids ?? []).Distinct(StringComparer.Ordinal).ToArray();

        foreach (var att in asset.attributes ?? [])
        {
            string tag = att.tag;
            double value = 0.0;

            if (tag.EndsWith("1", StringComparison.Ordinal))
            {
                // Small diameter bucket (<= 10mm)
                if (tag.StartsWith("DAY", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Base", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("TUONG", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Wall", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("TRAN", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Roof", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("CO", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Neck", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
                else
                    value = small;
            }
            else if (tag.EndsWith("2", StringComparison.Ordinal))
            {
                // Large diameter bucket (> 10mm)
                if (tag.StartsWith("DAY", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Base", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("TUONG", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Wall", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("TRAN", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Roof", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10).Sum(g => g.mass_kg ?? 0.0);
                else if (tag.StartsWith("CO", StringComparison.OrdinalIgnoreCase))
                    value = groups.Where(g => g.host_id.StartsWith("Neck", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10).Sum(g => g.mass_kg ?? 0.0);
                else
                    value = large;
            }
            else if (string.Equals(tag, "THEPHINH", StringComparison.OrdinalIgnoreCase))
            {
                value = groups.Where(g => string.Equals(g.material_kind, "Angle", StringComparison.OrdinalIgnoreCase)).Sum(g => g.mass_kg ?? 0.0);
            }
            else if (string.Equals(tag, "TONG", StringComparison.OrdinalIgnoreCase))
            {
                value = groups.Sum(g => g.mass_kg ?? 0.0);
            }

            attrs.Add(new BoundAttributeV2(
                tag: tag,
                text: SchematicPresentationPlanner.FormatValue(value, "kg", rules, "KL"),
                value_kind: "Number",
                raw_number: value,
                unit: "kg",
                source_ids: sourceIds
            ));
        }

        return attrs;
    }

    private static void EmitHeadings(
        List<ScheduleHeadingV2> headings,
        string scheduleView,
        string bendingView,
        GroupV2 group,
        string bucket,
        Point2V2 firstInsert,
        double scale,
        RuleV2? layoutRule,
        AssetsV2 assets,
        RulesV2 rules)
    {
        var headingSpecs = new[]
        {
            ("OwnerHeading", "TK.Tenluoi", scheduleView, "TENLUOI"),
            ("TableHeader", "TK.Tieude", scheduleView, (string?)null),
            ("BendingTitle", "TieudeMC", bendingView, "TIEUDE")
        };

        foreach (var (kind, assetName, viewId, tag) in headingSpecs)
        {
            var asset = ExactAsset(assets, assetName);
            string title = BucketTitleLiteral(bucket, rules);

            var attrs = tag != null
                ? [new BoundAttributeV2(tag, title, "Text", null, "none", group.source_ids ?? [])]
                : Array.Empty<BoundAttributeV2>();

            double offsetX = (GetParamNumber(layoutRule, "heading_x_native_mm." + kind) ?? 0.0) * scale;
            double offsetY = (GetParamNumber(layoutRule, "heading_y_native_mm." + kind) ?? 500.0) * scale;

            var insertion = string.Equals(kind, "BendingTitle", StringComparison.Ordinal)
                ? new Point2V2(offsetX, offsetY)
                : new Point2V2(firstInsert.x_mm + offsetX, firstInsert.y_mm + offsetY);

            var localInk = MeasureAssetInk(asset, attrs, assets, rules);
            var ink = TransformBounds(localInk, insertion, scale);

            headings.Add(new ScheduleHeadingV2(
                id: Encode(viewId, kind, group.id),
                view_id: viewId,
                group_id: !string.Equals(kind, "TableHeader", StringComparison.Ordinal) ? group.id : null,
                kind: kind,
                native_asset: assetName,
                attributes: attrs,
                insertion: insertion,
                asset_scale: scale,
                ink: ink
            ));
        }
    }

    private static string BucketTitleLiteral(string bucket, RulesV2 rules)
    {
        return bucket switch
        {
            "Body" => "THÂN GA",
            "Neck" => "CỔ GA",
            "Panel" => "NẮP GA",
            "Catch" => "CỬA THU",
            _ => bucket.ToUpperInvariant()
        };
    }

    private static Bounds2V2 MeasureAssetInk(
        AssetV2 asset,
        IEnumerable<BoundAttributeV2> attributes,
        AssetsV2 assets,
        RulesV2 rules)
    {
        var box = asset.bounds ?? new Bounds2V2(0, -500, 12500, 0);

        foreach (var bound in attributes)
        {
            var def = (asset.attributes ?? []).FirstOrDefault(a => string.Equals(a.tag, bound.tag, StringComparison.OrdinalIgnoreCase));
            if (def == null) continue;

            double h = def.height_mm > 0 ? def.height_mm : 200.0;
            double w = (bound.text?.Length ?? 0) * h * 0.6 * (def.width_factor > 0 ? def.width_factor : 0.8);
            var attrBox = new Bounds2V2(def.insertion.x_mm, def.insertion.y_mm, def.insertion.x_mm + w, def.insertion.y_mm + h);
            box = UnionBounds(box, attrBox);
        }

        return box;
    }

    private static AssetV2 ExactAsset(AssetsV2 assets, string name)
    {
        var found = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
        if (found != null) return found;

        // Fallback default definition if template did not contain named asset
        return new AssetV2(
            name: name,
            definition_handle: "0",
            content_hash: "default_hash",
            base_point: new Point3V2(0, 0, 0),
            bounds: new Bounds2V2(0, -500, 12500, 0),
            role: "ScheduleAsset",
            child_names: [],
            attributes:
            [
                new AttributeDefV2("SH", "1", "Standard", 200, 0.8, new(2000, -250, 0), new(2000, -250, 0), 0, "Center", "Middle"),
                new AttributeDefV2("D", "10", "Standard", 200, 0.8, new(6500, -250, 0), new(6500, -250, 0), 0, "Center", "Middle"),
                new AttributeDefV2("DAI", "1000", "Standard", 200, 0.8, new(7750, -250, 0), new(7750, -250, 0), 0, "Center", "Middle"),
                new AttributeDefV2("SL", "1", "Standard", 200, 0.8, new(9000, -250, 0), new(9000, -250, 0), 0, "Center", "Middle"),
                new AttributeDefV2("TDAI", "1.00", "Standard", 200, 0.8, new(10250, -250, 0), new(10250, -250, 0), 0, "Center", "Middle"),
                new AttributeDefV2("KL", "1.00", "Standard", 200, 0.8, new(11750, -250, 0), new(11750, -250, 0), 0, "Center", "Middle")
            ],
            layer_id: "0",
            style_id: "Standard",
            source_ids: []
        );
    }

    private static string ResolveDefaultAsset(GroupV2 group)
    {
        if (group.family.Contains("Tie", StringComparison.OrdinalIgnoreCase)) return "TK.C";
        if (group.host_id.StartsWith("Panel", StringComparison.OrdinalIgnoreCase)) return "TK.NA1";
        if (group.host_id.StartsWith("Catch", StringComparison.OrdinalIgnoreCase)) return "TK.CT.N";
        return "TK.T";
    }

    private static ShapeRecipeV2? FindShapeRecipe(RulesV2 rules, string recipeId, string profile, string family)
    {
        foreach (var r in rules.entries ?? [])
        {
            if (r.shapes == null) continue;
            var match = r.shapes.FirstOrDefault(s => string.Equals(s.id, recipeId, StringComparison.Ordinal));
            if (match != null) return match;
        }
        return null;
    }

    private static RuleV2? FindRule(RulesV2 rules, string id, string profile, string component, string shapeKind)
    {
        return (rules.entries ?? []).FirstOrDefault(r =>
            string.Equals(r.id, id, StringComparison.Ordinal) &&
            (r.profiles == null || r.profiles.Contains(profile)));
    }

    private static double? GetParamNumber(RuleV2? rule, string key)
    {
        return (rule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))?.number;
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

    private static string Encode(params string?[] parts)
    {
        return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
