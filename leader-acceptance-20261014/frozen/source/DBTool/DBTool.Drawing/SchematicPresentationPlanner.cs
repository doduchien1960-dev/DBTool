#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// S8 Schematic presentation planner.
/// Plans fixed-shape bending glyphs, role leg dimension texts, and caption labels (display mark + diameter + L/Ltb)
/// grouped by bucket (Body, Neck, Panel, Catch).
/// </summary>
public sealed class SchematicPresentationPlanner
{
    /// <summary>
    /// Plans bending schematic captions for steel presentation groups.
    /// </summary>
    public CaptionV2[] PlanCaptions(
        EffectiveV2 effective,
        GroupsV2 groups,
        AssetsV2 assets,
        RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(rules);

        var items = groups.items ?? [];
        if (items.Length == 0) return [];

        var result = new List<CaptionV2>();

        // Group into buckets: Body, Neck, Panel, Catch
        var bucketGroups = items.GroupBy(g => PhysicalOwnerBucket(g, rules))
            .OrderBy(b => BucketSortKey(b.Key), StringComparer.Ordinal)
            .ToList();

        foreach (var bucket in bucketGroups)
        {
            string bucketKey = bucket.Key;
            string bendingView = Encode(effective.id, "Bending", bucketKey);

            double s = (string.Equals(bucketKey, "Panel", StringComparison.OrdinalIgnoreCase) && effective.panel_scale.HasValue)
                ? effective.panel_scale.Value
                : effective.body_scale;

            if (s <= 0) s = 30.0;
            double scale = s / 100.0;

            var layoutRule = FindRule(rules, "NR-22", effective.profile, bucketKey, "ScheduleLayout");
            double bendingX = 0.0;
            double bendingY = 0.0;

            var orderedGroups = bucket
                .OrderBy(g => g.host_id, StringComparer.Ordinal)
                .ThenBy(g => g.face, StringComparer.Ordinal)
                .ThenBy(g => g.layer, StringComparer.Ordinal)
                .ThenBy(g => g.direction, StringComparer.Ordinal)
                .ThenBy(g => g.region_role, StringComparer.Ordinal)
                .ThenBy(g => g.id, StringComparer.Ordinal)
                .ToList();

            foreach (var group in orderedGroups)
            {
                var captionRule = FindRule(rules, "NR-22", effective.profile, group.family, "Bending");
                string glyph = GetParamText(captionRule, "glyph_asset")
                    ?? ResolveDefaultGlyph(group.shape_recipe_id, group.family);

                string text = string.Equals(group.material_kind, "RoundBar", StringComparison.OrdinalIgnoreCase)
                    ? $"{group.display_mark}-%%c{FormatValue(group.diameter_mm, "mm", rules, "Diameter")}"
                    : $"{group.display_mark}-{group.angle_code}";

                bool hasVariableLength = Math.Abs(group.min_mm - group.max_mm) > 1e-4;
                text += (hasVariableLength ? " Ltb=" : " L=") + FormatValue(group.mean_mm, "mm", rules, "DAI");

                var bendInsert = new Point2V2(bendingX, bendingY);
                var capInk = MeasureBendingInk(glyph, group.mean_roles ?? [], text, bendInsert, scale, assets, captionRule);

                double underlineGap = GetParamNumber(captionRule, "underline_gap_paper_mm") ?? 2.0;
                double underlineY = capInk.min_y_mm - underlineGap * s;

                var underA = new Point2V2(capInk.min_x_mm, underlineY);
                var underB = new Point2V2(capInk.max_x_mm, underlineY);

                capInk = UnionBounds(capInk, StrokeBounds(underA, underB, 0.25 * scale));

                result.Add(new CaptionV2(
                    group_id: group.id,
                    glyph_asset: glyph,
                    text: text,
                    role_values: group.mean_roles ?? [],
                    underline_start: underA,
                    underline_end: underB,
                    ink: capInk,
                    view_id: bendingView,
                    insertion: bendInsert,
                    asset_scale: scale
                ));

                double nextGap = GetParamNumber(captionRule, "next_gap_paper_mm") ?? 15.0;
                bendingX = capInk.max_x_mm + nextGap * s;
            }
        }

        return result.ToArray();
    }

    internal static string PhysicalOwnerBucket(GroupV2 group, RulesV2 rules)
    {
        string host = group.host_id ?? "";
        if (host.StartsWith("Panel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(group.family, "Panel", StringComparison.OrdinalIgnoreCase))
            return "Panel";

        if (host.StartsWith("Catch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(group.family, "Catch", StringComparison.OrdinalIgnoreCase) ||
            group.region_role.Contains("Catch", StringComparison.OrdinalIgnoreCase))
            return "Catch";

        if (host.StartsWith("Neck", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(group.family, "Neck", StringComparison.OrdinalIgnoreCase))
            return "Neck";

        return "Body";
    }

    private static string BucketSortKey(string bucket) => bucket switch
    {
        "Body" => "1_Body",
        "Neck" => "2_Neck",
        "Panel" => "3_Panel",
        "Catch" => "4_Catch",
        _ => "5_" + bucket
    };

    private static Bounds2V2 MeasureBendingInk(
        string glyphName,
        RoleValueV2[] roles,
        string text,
        Point2V2 insertion,
        double scale,
        AssetsV2 assets,
        RuleV2? recipe)
    {
        Bounds2V2 glyphBounds;
        var assetDef = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, glyphName, StringComparison.Ordinal));
        if (assetDef != null && assetDef.bounds != null)
        {
            glyphBounds = assetDef.bounds;
        }
        else
        {
            glyphBounds = new Bounds2V2(-150.0, -50.0, 150.0, 50.0);
        }

        var transformedGlyph = TransformBounds(glyphBounds, insertion, scale);

        // Text bounds: nominal height 2.5mm paper * 100 native = 250 native mm
        double textHeight = 2.5 * scale * 100.0;
        double textWidth = text.Length * (textHeight * 0.6);
        double textY = transformedGlyph.min_y_mm - textHeight - 2.0 * scale;
        var textBounds = new Bounds2V2(
            transformedGlyph.min_x_mm,
            textY,
            transformedGlyph.min_x_mm + textWidth,
            textY + textHeight
        );

        return UnionBounds(transformedGlyph, textBounds);
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

    private static Bounds2V2 StrokeBounds(Point2V2 a, Point2V2 b, double halfWidth)
    {
        return new Bounds2V2(
            Math.Min(a.x_mm, b.x_mm) - halfWidth,
            Math.Min(a.y_mm, b.y_mm) - halfWidth,
            Math.Max(a.x_mm, b.x_mm) + halfWidth,
            Math.Max(a.y_mm, b.y_mm) + halfWidth
        );
    }

    internal static string FormatValue(double? value, string unit, RulesV2 rules, string role)
    {
        if (!value.HasValue) return "";
        if (!double.IsFinite(value.Value))
            throw new DrawingStageFaultException("FORMAT_NONFINITE", role, ["value"], "finite", value.Value.ToString(CultureInfo.InvariantCulture));

        int decimals = role switch
        {
            "DAI" => 0,
            "Leg" => 0,
            "Diameter" => 0,
            "Pitch" => 0,
            "Count" => 0,
            "TDAI" => 2,
            "KL" => 2,
            "AngleUnitMass" => 2,
            "Elevation" => 3,
            _ => (unit == "mm" || unit == "count") ? 0 : 2
        };

        string key = $"{role}/{unit}";
        if (rules.entries != null)
        {
            var nr13 = rules.entries.FirstOrDefault(r => string.Equals(r.id, "NR-13", StringComparison.Ordinal));
            if (nr13?.parameters != null)
            {
                var decParam = nr13.parameters.FirstOrDefault(p => string.Equals(p.key, $"decimals.{key}", StringComparison.OrdinalIgnoreCase));
                if (decParam != null && decParam.number.HasValue)
                    decimals = (int)decParam.number.Value;
            }
        }

        double rounded = Math.Round(value.Value, decimals, MidpointRounding.AwayFromZero);
        string formatString = decimals == 0 ? "0" : "0." + new string('0', decimals);
        return rounded.ToString(formatString, CultureInfo.InvariantCulture);
    }

    private static string ResolveDefaultGlyph(string recipeId, string family)
    {
        if (family.Contains("Tie", StringComparison.OrdinalIgnoreCase)) return "TK.C";
        if (family.Contains("Lifting", StringComparison.OrdinalIgnoreCase)) return "TK.MC";
        if (recipeId.Contains("U", StringComparison.OrdinalIgnoreCase)) return "TK.U";
        if (recipeId.Contains("L", StringComparison.OrdinalIgnoreCase)) return "TK.L";
        return "TK.T";
    }

    private static RuleV2? FindRule(RulesV2 rules, string id, string profile, string component, string shapeKind)
    {
        return (rules.entries ?? []).FirstOrDefault(r =>
            string.Equals(r.id, id, StringComparison.Ordinal) &&
            (r.profiles == null || r.profiles.Contains(profile)) &&
            (r.components == null || r.components.Contains(component)));
    }

    private static string? GetParamText(RuleV2? rule, string key)
    {
        return (rule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))?.text;
    }

    private static double? GetParamNumber(RuleV2? rule, string key)
    {
        return (rule?.parameters ?? []).FirstOrDefault(p => string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))?.number;
    }

    private static string Encode(params string?[] parts)
    {
        return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
