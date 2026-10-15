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

        // Bucket members by exact physical owner
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
                var hostComp = (geometry.components ?? []).FirstOrDefault(c => string.Equals(c.id, group.host_id, StringComparison.Ordinal));
                string componentKind = hostComp?.kind ?? "Wall";

                var recipe = FindShapeRecipe(rules, group.shape_recipe_id, effective.profile, componentKind, group.family);
                string assetName = recipe.native_asset;
                if (string.IsNullOrWhiteSpace(assetName))
                {
                    throw new DrawingStageFaultException(
                        "ASSET_EXACT_NAME_MISSING",
                        group.shape_recipe_id,
                        ["native_asset"],
                        "exact non-empty native asset name in recipe",
                        "empty",
                        recipe.source_ids);
                }

                var asset = ExactAsset(assets, assetName);
                if (!string.Equals(asset.content_hash, recipe.fixed_glyph_hash, StringComparison.Ordinal))
                {
                    throw new DrawingStageFaultException(
                        "SCHEDULE_GLYPH_SIGNATURE_CHANGED",
                        group.id,
                        ["fixed_glyph_hash"],
                        recipe.fixed_glyph_hash,
                        asset.content_hash,
                        recipe.source_ids);
                }

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
        var captions = presentationPlanner.PlanCaptions(effective, geometry, groups, assets, rules);

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

            var summaryRule = FindRule(rules, "NR-14", effective.profile, scope, "Summary")
                ?? FindRule(rules, "NR-14", effective.profile, "", "Summary");

            string? ruleAsset = (summaryRule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, "summary_asset", StringComparison.OrdinalIgnoreCase))?.text;

            string assetName = !string.IsNullOrWhiteSpace(ruleAsset)
                ? ruleAsset
                : (scope switch
                {
                    "Main" => "TK.THKL",
                    "Panel" => "TK.THKLNAP",
                    "Catch" => "TK.THKL.CT",
                    _ => "TK.THKL"
                });

            var asset = ExactAsset(assets, assetName);

            double small = included
                .Where(g => string.Equals(g.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10)
                .Sum(g => g.mass_kg ?? 0.0);

            double large = included
                .Where(g => string.Equals(g.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10)
                .Sum(g => g.mass_kg ?? 0.0);

            var attrs = BindSummaryTags(asset, small, large, included, geometry, summaryRule, rules);

            // Position below previous rows in that view
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

        // Coverage assertion: 100% group coverage
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

    internal static string PhysicalOwnerBucket(GroupV2 group, GeometryV2 geometry, RulesV2 rules)
    {
        var host = (geometry.components ?? []).FirstOrDefault(c => string.Equals(c.id, group.host_id, StringComparison.Ordinal));
        if (host != null)
        {
            if (string.Equals(host.kind, "Panel", StringComparison.OrdinalIgnoreCase)) return "Panel";
            if (string.Equals(host.kind, "Catch", StringComparison.OrdinalIgnoreCase)) return "Catch";
            if (string.Equals(host.kind, "Neck", StringComparison.OrdinalIgnoreCase)) return "Neck";
            return "Body";
        }

        return SchematicPresentationPlanner.PhysicalOwnerBucket(group, geometry, rules);
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
        ShapeRecipeV2 recipe,
        AssetV2 asset,
        string bucketKey,
        bool isFirstRow,
        RulesV2 rules)
    {
        var attrs = new List<BoundAttributeV2>();
        var bindings = recipe.tag_bindings ?? [];
        if (bindings.Length == 0)
        {
            throw new DrawingStageFaultException(
                "RULE_BINDING_DATA_MISSING",
                recipe.id,
                ["tag_bindings"],
                "non-empty tag bindings in shape recipe",
                "empty",
                recipe.source_ids,
                ["NR-04", "NR-08"]);
        }

        foreach (var b in bindings)
        {
            string tag = b.tag;
            string value;
            double? raw;
            string unit;
            string valueKind;

            switch (b.operation)
            {
                case "Mark":
                    value = group.display_mark;
                    raw = null;
                    unit = "none";
                    valueKind = "Text";
                    break;

                case "Diameter":
                    if (!group.diameter_mm.HasValue)
                    {
                        throw new DrawingStageFaultException(
                            "FIELD_MISSING",
                            group.id,
                            ["diameter_mm"],
                            "valid diameter_mm",
                            "null",
                            group.source_ids);
                    }
                    raw = group.diameter_mm.Value;
                    unit = "mm";
                    valueKind = "Diameter";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Diameter");
                    break;

                case "Pitch":
                    if (!group.pitch_mm.HasValue)
                    {
                        throw new DrawingStageFaultException(
                            "FIELD_MISSING",
                            group.id,
                            ["pitch_mm"],
                            "valid pitch_mm",
                            "null",
                            group.source_ids);
                    }
                    raw = group.pitch_mm.Value;
                    unit = "mm";
                    valueKind = "Pitch";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Pitch");
                    break;

                case "MeanLength":
                    if (group.count <= 0)
                    {
                        throw new DrawingStageFaultException(
                            "SCHEDULE_GROUP_COUNT_INVALID",
                            group.id,
                            ["count"],
                            ">0",
                            group.count.ToString(CultureInfo.InvariantCulture),
                            group.source_ids);
                    }
                    raw = group.raw_sum_mm / group.count;
                    unit = "mm";
                    valueKind = "Length";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "DAI");
                    break;

                case "RawSumMetres":
                    raw = group.raw_sum_mm / 1000.0;
                    unit = "m";
                    valueKind = "Metres";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "TDAI");
                    break;

                case "Count":
                    raw = group.count;
                    unit = "count";
                    valueKind = "Count";
                    value = group.count.ToString(CultureInfo.InvariantCulture);
                    break;

                case "Mass":
                    if (!group.mass_kg.HasValue)
                    {
                        throw new DrawingStageFaultException(
                            "FIELD_MISSING",
                            group.id,
                            ["mass_kg"],
                            "valid mass_kg",
                            "null",
                            group.source_ids);
                    }
                    raw = group.mass_kg.Value;
                    unit = "kg";
                    valueKind = "Mass";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "KL");
                    break;

                case "MeanRole":
                    if (string.IsNullOrEmpty(b.role))
                    {
                        throw new DrawingStageFaultException(
                            "TAG_BIND_ROLE_MISSING",
                            group.id,
                            [b.tag],
                            "named role for MeanRole",
                            "null",
                            recipe.source_ids);
                    }
                    var roleMatch = (group.mean_roles ?? []).FirstOrDefault(r => string.Equals(r.role, b.role, StringComparison.OrdinalIgnoreCase));
                    if (roleMatch == null)
                    {
                        throw new DrawingStageFaultException(
                            "ROLE_VALUE_MISSING",
                            group.id,
                            [b.role],
                            $"role {b.role} present in mean_roles",
                            "absent",
                            group.source_ids);
                    }
                    raw = roleMatch.value_mm;
                    unit = "mm";
                    valueKind = "Length";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Leg");
                    break;

                case "FixedRole":
                    if (string.IsNullOrEmpty(b.role))
                    {
                        throw new DrawingStageFaultException(
                            "TAG_BIND_ROLE_MISSING",
                            group.id,
                            [b.tag],
                            "named role for FixedRole",
                            "null",
                            recipe.source_ids);
                    }
                    double? fixedVal = null;
                    if (recipe.corners != null)
                    {
                        var corner = recipe.corners.FirstOrDefault(c => string.Equals(c.role, b.role, StringComparison.OrdinalIgnoreCase));
                        if (corner != null && double.TryParse(corner.x_expr, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVal))
                        {
                            fixedVal = parsedVal;
                        }
                    }
                    if (!fixedVal.HasValue && !string.IsNullOrEmpty(b.literal) && double.TryParse(b.literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double litVal))
                    {
                        fixedVal = litVal;
                    }
                    if (!fixedVal.HasValue)
                    {
                        throw new DrawingStageFaultException(
                            "FIXED_ROLE_VALUE_MISSING",
                            group.id,
                            [b.role],
                            $"fixed role {b.role} in recipe corners or literal",
                            "absent",
                            recipe.source_ids);
                    }
                    raw = fixedVal.Value;
                    unit = "mm";
                    valueKind = "Length";
                    value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Leg");
                    break;

                case "Literal":
                    value = b.literal ?? "";
                    raw = null;
                    unit = "none";
                    valueKind = "Text";
                    break;

                case "SizePair":
                    string[] pairRoles = (b.role ?? "Width,Height").Split(new[] { ',', '/', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (pairRoles.Length != 2)
                    {
                        throw new DrawingStageFaultException(
                            "SIZE_PAIR_ROLES_INVALID",
                            group.id,
                            [b.tag],
                            "two comma-separated roles for SizePair",
                            b.role ?? "null",
                            recipe.source_ids);
                    }
                    var valA = (group.mean_roles ?? []).FirstOrDefault(r => string.Equals(r.role, pairRoles[0], StringComparison.OrdinalIgnoreCase))
                        ?? throw new DrawingStageFaultException("ROLE_VALUE_MISSING", group.id, [pairRoles[0]], $"role {pairRoles[0]} present", "absent", group.source_ids);
                    var valB = (group.mean_roles ?? []).FirstOrDefault(r => string.Equals(r.role, pairRoles[1], StringComparison.OrdinalIgnoreCase))
                        ?? throw new DrawingStageFaultException("ROLE_VALUE_MISSING", group.id, [pairRoles[1]], $"role {pairRoles[1]} present", "absent", group.source_ids);
                    raw = null;
                    unit = "mm";
                    valueKind = "SizePair";
                    value = $"{SchematicPresentationPlanner.FormatValue(valA.value_mm, "mm", rules, "Leg")}x{SchematicPresentationPlanner.FormatValue(valB.value_mm, "mm", rules, "Leg")}";
                    break;

                default:
                    throw new DrawingStageFaultException(
                        "TK_TAG_OPERATION_UNSUPPORTED",
                        group.id,
                        [b.tag],
                        "closed operation set: Mark/Diameter/Pitch/MeanLength/RawSumMetres/Count/Mass/MeanRole/FixedRole/Literal/SizePair",
                        b.operation,
                        recipe.source_ids);
            }

            attrs.Add(new BoundAttributeV2(
                tag: tag,
                text: value,
                value_kind: valueKind,
                raw_number: raw,
                unit: unit,
                source_ids: (group.source_ids ?? []).Concat(recipe.source_ids ?? []).Distinct(StringComparer.Ordinal).ToArray()
            ));
        }

        // Schema tag comparison: must match asset attribute tags exactly
        var boundTags = attrs.Select(a => a.tag).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var assetTags = (asset.attributes ?? []).Select(a => a.tag).OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (!boundTags.SequenceEqual(assetTags))
        {
            throw new DrawingStageFaultException(
                "TK_TAG_SCHEMA_MISMATCH",
                group.id,
                ["attributes"],
                string.Join(", ", assetTags),
                string.Join(", ", boundTags),
                asset.source_ids);
        }

        return attrs;
    }

    private static List<BoundAttributeV2> BindSummaryTags(
        AssetV2 asset,
        double small,
        double large,
        List<GroupV2> groups,
        GeometryV2 geometry,
        RuleV2? rule,
        RulesV2 rules)
    {
        var attrs = new List<BoundAttributeV2>();
        var sourceIds = groups.SelectMany(g => g.source_ids ?? []).Distinct(StringComparer.Ordinal).ToArray();

        foreach (var att in asset.attributes ?? [])
        {
            string tag = att.tag;
            double value;

            if (tag.StartsWith("DAY", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("TUONG", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("TRAN", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("CO", StringComparison.OrdinalIgnoreCase))
            {
                string compKind = tag.Substring(0, tag.Length - 1).ToUpperInvariant() switch
                {
                    "DAY" => "Base",
                    "TUONG" => "Wall",
                    "TRAN" => "Roof",
                    "CO" => "Neck",
                    _ => ""
                };
                bool isSmall = tag.EndsWith("1", StringComparison.Ordinal);

                value = groups.Where(g =>
                {
                    var h = (geometry.components ?? []).FirstOrDefault(c => string.Equals(c.id, g.host_id, StringComparison.Ordinal));
                    if (h == null || !string.Equals(h.kind, compKind, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.Equals(g.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase)) return false;
                    int dia = g.diameter_mm ?? 0;
                    return isSmall ? dia <= 10 : dia > 10;
                }).Sum(g => g.mass_kg ?? 0.0);
            }
            else if (tag.StartsWith("NAP", StringComparison.OrdinalIgnoreCase) ||
                     tag.StartsWith("CUATHU", StringComparison.OrdinalIgnoreCase))
            {
                bool isSmall = tag.EndsWith("1", StringComparison.Ordinal);
                value = isSmall ? small : large;
            }
            else if (string.Equals(tag, "THEPHINH", StringComparison.OrdinalIgnoreCase))
            {
                value = groups.Where(g => string.Equals(g.material_kind, "Angle", StringComparison.OrdinalIgnoreCase)).Sum(g => g.mass_kg ?? 0.0);
            }
            else if (string.Equals(tag, "TONG", StringComparison.OrdinalIgnoreCase))
            {
                value = groups.Sum(g => g.mass_kg ?? 0.0);
            }
            else
            {
                throw new DrawingStageFaultException(
                    "SUMMARY_TAG_OPERATION_MISSING",
                    asset.name,
                    [tag],
                    "declared exact native summary tag: DAY1/2, TUONG1/2, TRAN1/2, CO1/2, NAP1/2, CUATHU1/2, THEPHINH, TONG",
                    tag,
                    asset.source_ids,
                    ["NR-14"]);
            }

            attrs.Add(new BoundAttributeV2(
                tag: tag,
                text: SchematicPresentationPlanner.FormatValue(value, "kg", rules, "KL"),
                value_kind: "Mass",
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
                ? new[] { new BoundAttributeV2(tag, title, "Text", null, "none", group.source_ids ?? []) }
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

    internal static Bounds2V2 MeasureAssetInk(
        AssetV2 asset,
        IEnumerable<BoundAttributeV2> attributes,
        AssetsV2 assets,
        RulesV2 rules)
    {
        var box = asset.bounds;

        foreach (var bound in attributes)
        {
            var def = (asset.attributes ?? []).FirstOrDefault(a => string.Equals(a.tag, bound.tag, StringComparison.OrdinalIgnoreCase));
            if (def == null) continue;

            var style = (assets.font_styles ?? []).FirstOrDefault(s => string.Equals(s.style_id, def.style_id, StringComparison.OrdinalIgnoreCase));
            if (style != null && assets.glyphs != null && assets.glyphs.Length > 0)
            {
                // Measure using H12 native glyphs
                string text = bound.text ?? "";
                text = text.Replace("%%c", "Ø").Replace("%%p", "±").Replace("%%d", "°");

                double penX = 0.0;
                Bounds2V2? rawInk = null;
                int? previous = null;

                foreach (int cp in text)
                {
                    var glyph = assets.glyphs.FirstOrDefault(g => string.Equals(g.style_id, def.style_id, StringComparison.OrdinalIgnoreCase) && g.codepoint == cp);
                    if (glyph != null && glyph.reference_height_mm > 0)
                    {
                        double factor = def.height_mm / glyph.reference_height_mm;
                        if (previous.HasValue && assets.kerning != null)
                        {
                            var pair = assets.kerning.FirstOrDefault(k => string.Equals(k.style_id, def.style_id, StringComparison.OrdinalIgnoreCase) && k.left_codepoint == previous.Value && k.right_codepoint == cp);
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
                    continue;
                }
            }

            // Fallback nominal attribute envelope
            double h = def.height_mm > 0 ? def.height_mm : 200.0;
            double w = (bound.text?.Length ?? 0) * h * 0.6 * (def.width_factor > 0 ? def.width_factor : 0.8);
            var attrBox = new Bounds2V2(def.insertion.x_mm, def.insertion.y_mm, def.insertion.x_mm + w, def.insertion.y_mm + h);
            box = UnionBounds(box, attrBox);
        }

        return box;
    }

    internal static AssetV2 ExactAsset(AssetsV2 assets, string name)
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

    private static ShapeRecipeV2 FindShapeRecipe(RulesV2 rules, string recipeId, string profile, string componentKind, string family)
    {
        var candidates = new List<ShapeRecipeV2>();
        foreach (var r in rules.entries ?? [])
        {
            if (r.profiles != null && r.profiles.Length > 0 && !r.profiles.Contains(profile)) continue;
            if (r.shapes == null) continue;
            foreach (var s in r.shapes)
            {
                if (string.Equals(s.id, recipeId, StringComparison.Ordinal) &&
                    string.Equals(s.component_kind, componentKind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.family, family, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(s);
                }
            }
        }

        if (candidates.Count != 1)
        {
            throw new DrawingStageFaultException(
                "SHAPE_RECIPE_BINDING_MISSING",
                recipeId,
                ["shape_recipe_id"],
                "one exact scoped recipe",
                candidates.Count.ToString(CultureInfo.InvariantCulture),
                needsRuleIds: ["NR-02", "NR-04", "NR-08"]);
        }

        return candidates[0];
    }

    private static RuleV2? FindRule(RulesV2 rules, string id, string profile, string component, string shapeKind)
    {
        return (rules.entries ?? []).FirstOrDefault(r =>
            string.Equals(r.id, id, StringComparison.Ordinal) &&
            (r.profiles == null || r.profiles.Length == 0 || r.profiles.Contains(profile)));
    }

    private static double? GetParamNumber(RuleV2? rule, string key)
    {
        return (rule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))?.number;
    }

    internal static Bounds2V2 RotateBounds(Bounds2V2 b, double rad)
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
            minX = Math.Min(minX, rx);
            minY = Math.Min(minY, ry);
            maxX = Math.Max(maxX, rx);
            maxY = Math.Max(maxY, ry);
        }

        return new Bounds2V2(minX, minY, maxX, maxY);
    }

    internal static Bounds2V2 TransformBounds(Bounds2V2 b, Point2V2 offset, double scale)
    {
        double x0 = b.min_x_mm * scale + offset.x_mm;
        double y0 = b.min_y_mm * scale + offset.y_mm;
        double x1 = b.max_x_mm * scale + offset.x_mm;
        double y1 = b.max_y_mm * scale + offset.y_mm;
        return new Bounds2V2(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
    }

    internal static Bounds2V2 UnionBounds(Bounds2V2 a, Bounds2V2 b)
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
