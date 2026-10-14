#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// Domain stage fault exception for Drawing operations.
/// </summary>
internal sealed class DrawingStageFaultException : Exception
{
    public string Code { get; }
    public string? Subject { get; }
    public string[] Fields { get; }
    public string Expected { get; }
    public string Actual { get; }
    public string[] Sources { get; }
    public string[] NeedsRuleIds { get; }
    public string Severity { get; }

    public DrawingStageFaultException(
        string code,
        string? subject,
        string[] fields,
        string expected,
        string actual,
        string[]? sources = null,
        string[]? needsRuleIds = null,
        string severity = "Block")
        : base($"{code}: subject={subject ?? "null"}, fields=[{string.Join(", ", fields)}], expected={expected}, actual={actual}")
    {
        Code = code;
        Subject = subject;
        Fields = fields ?? [];
        Expected = expected;
        Actual = actual;
        Sources = sources ?? [];
        NeedsRuleIds = needsRuleIds ?? [];
        Severity = severity;
    }
}

/// <summary>
/// S5 presentation group planner.
/// Forms semantic groups and computes mean geometry/roles before annotation and detailing.
/// Guarantees 1:1 member coverage and strict scoping without fuzzy joins.
/// </summary>
public sealed class PresentationGroupPlanner
{
    private sealed record AccountingItem(
        string Id,
        string MaterialKind,
        string HostId,
        string AssemblyId,
        string Face,
        string Layer,
        string Direction,
        string Family,
        string RegionRole,
        string[] OpeningIds,
        string ShapeRecipeId,
        int? DiameterMm,
        string? AngleCode,
        double? UnitMassKgPerM,
        double LengthMm,
        string MeasurementBasis,
        string PresentationLengthBasis,
        double? RequestedPitchMm,
        string? HookPolicyId,
        ShapeRecipeV2? Recipe,
        RoleValueV2[] RoleValues,
        string[] SourceIds,
        double StationMm,
        int StationIndex
    );

    /// <summary>
    /// Executes S5 planning: creates presentation groups from effective, geometry, rebar, and rules.
    /// </summary>
    public GroupsV2 Plan(EffectiveV2 effective, GeometryV2 geometry, RebarV2 rebar, RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(rebar);
        ArgumentNullException.ThrowIfNull(rules);

        var accountingItems = new List<AccountingItem>();
        var occurrences = rebar.occurrences ?? [];
        var angles = rebar.angles ?? [];

        // 1. Process physical round bar occurrences
        foreach (var b in occurrences)
        {
            if (!string.Equals(b.length_status, "Known", StringComparison.OrdinalIgnoreCase) || !b.fabrication_length_mm.HasValue)
            {
                throw new DrawingStageFaultException(
                    "GROUP_MEMBER_LENGTH_UNRESOLVED",
                    b.id,
                    ["length_status", "fabrication_length_mm"],
                    "known physical length",
                    b.length_status ?? "null",
                    b.source_ids);
            }

            string compKind = ResolveComponentKind(b.host_id, geometry);
            var recipe = FindShapeRecipe(rules, b.shape_recipe_id, effective.profile, compKind, b.family);

            string presBasis = recipe?.presentation_length_basis ?? "Fabrication";
            double li;
            if (string.Equals(presBasis, "Fabrication", StringComparison.OrdinalIgnoreCase))
            {
                li = b.fabrication_length_mm.Value;
            }
            else if (string.Equals(presBasis, "Schedule", StringComparison.OrdinalIgnoreCase))
            {
                li = b.schedule_length_mm ?? b.fabrication_length_mm.Value;
            }
            else
            {
                throw new DrawingStageFaultException(
                    "GROUP_LENGTH_BASIS_UNAPPROVED",
                    b.id,
                    ["presentation_length_basis"],
                    "Fabrication/Schedule",
                    presBasis,
                    b.source_ids,
                    ["NR-02"]);
            }

            accountingItems.Add(new AccountingItem(
                Id: b.id,
                MaterialKind: "RoundBar",
                HostId: b.host_id,
                AssemblyId: b.assembly_id,
                Face: b.face,
                Layer: b.layer,
                Direction: b.direction,
                Family: b.family,
                RegionRole: b.region_role,
                OpeningIds: (b.opening_ids ?? []).Order(StringComparer.Ordinal).ToArray(),
                ShapeRecipeId: b.shape_recipe_id,
                DiameterMm: b.diameter_mm,
                AngleCode: null,
                UnitMassKgPerM: null,
                LengthMm: li,
                MeasurementBasis: b.measurement_basis,
                PresentationLengthBasis: presBasis,
                RequestedPitchMm: b.requested_pitch_mm,
                HookPolicyId: null,
                Recipe: recipe,
                RoleValues: b.role_values ?? [],
                SourceIds: b.source_ids ?? [],
                StationMm: b.station_mm,
                StationIndex: b.station_index
            ));
        }

        // 2. Process angle steel occurrences
        foreach (var a in angles)
        {
            var angleRecipe = FindShapeRecipe(rules, a.shape_recipe_id, effective.profile, "Angle", "Angle");

            accountingItems.Add(new AccountingItem(
                Id: a.id,
                MaterialKind: "Angle",
                HostId: a.host_id,
                AssemblyId: a.panel_instance_id ?? a.host_id,
                Face: "Outer",
                Layer: "Top",
                Direction: "Angle",
                Family: "Angle",
                RegionRole: "Uncut",
                OpeningIds: [],
                ShapeRecipeId: a.shape_recipe_id,
                DiameterMm: null,
                AngleCode: a.catalog_code,
                UnitMassKgPerM: a.unit_mass_kg_per_m,
                LengthMm: a.total_length_mm,
                MeasurementBasis: "Fabrication",
                PresentationLengthBasis: "Fabrication",
                RequestedPitchMm: null,
                HookPolicyId: null,
                Recipe: angleRecipe,
                RoleValues: a.role_values ?? [],
                SourceIds: a.source_ids ?? [],
                StationMm: 0,
                StationIndex: 0
            ));
        }

        // 3. Partition items by exact 05B-F49 key
        var partitions = accountingItems
            .GroupBy(item => BuildPartitionKey(effective.id, item))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var groupItems = new List<GroupV2>();

        foreach (var partition in partitions)
        {
            var members = partition
                .OrderBy(m => m.StationMm)
                .ThenBy(m => m.StationIndex)
                .ThenBy(m => m.Id, StringComparer.Ordinal)
                .ToList();

            var ids = members.Select(m => m.Id).ToArray();
            int distinctCount = ids.Distinct(StringComparer.Ordinal).Count();
            if (ids.Any(id => assigned.Contains(id)) || ids.Length != distinctCount)
            {
                throw new DrawingStageFaultException(
                    "GROUP_MEMBER_DUPLICATE",
                    partition.Key,
                    ["member_ids"],
                    "one primary accounting group",
                    string.Join(",", ids));
            }

            foreach (var id in ids) assigned.Add(id);

            int count = ids.Length;
            if (count == 0) continue;

            double[] lengths = members.Select(m => m.LengthMm).ToArray();
            double sumMm = SumCompensated(lengths);
            double meanMm = sumMm / count;
            double minMm = lengths.Min();
            double maxMm = lengths.Max();

            // Compute mean role values
            var member = members[0];
            var roleNames = members
                .SelectMany(m => m.RoleValues.Select(rv => rv.role))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();

            var roleMeans = new List<RoleValueV2>();
            foreach (var role in roleNames)
            {
                if (members.Any(m => !m.RoleValues.Any(rv => string.Equals(rv.role, role, StringComparison.Ordinal))))
                {
                    throw new DrawingStageFaultException(
                        "GROUP_ROLE_MISSING",
                        partition.Key,
                        [role],
                        "same roles in every member",
                        string.Join(",", ids));
                }

                double[] roleVals = members
                    .Select(m => m.RoleValues.First(rv => string.Equals(rv.role, role, StringComparison.Ordinal)).value_mm)
                    .ToArray();
                double roleSum = SumCompensated(roleVals);
                string[] roleSources = members
                    .SelectMany(m => m.RoleValues.First(rv => string.Equals(rv.role, role, StringComparison.Ordinal)).source_ids ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                roleMeans.Add(new RoleValueV2(role, roleSum / count, roleSources));
            }

            // Determine display mark prefix and suffix
            string prefix = PrefixForExactFaceLayer(effective, member);
            string suffix = SuffixForRegionRole(member, partition.Key, rules);
            string mark = prefix + suffix;

            // Check for scoped alias
            string? alias = ExactScopedAliasIfAny(mark, member.HostId, member.Face, member.Layer, rules, ids);
            if (!string.IsNullOrEmpty(alias))
            {
                mark = alias;
            }

            // Calculate actual mass
            double? mass = SumActualMemberMasses(ids, rebar.canonical ?? [], angles);

            // Canonical row IDs
            string[] canonicalIds = CanonicalIdsFor(ids, rebar.canonical ?? []);

            string[] distinctSources = members
                .SelectMany(m => m.SourceIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            string groupId = ComputeGroupId(partition.Key, ids);

            groupItems.Add(new GroupV2(
                id: groupId,
                host_id: member.HostId,
                face: member.Face,
                layer: member.Layer,
                direction: member.Direction,
                family: member.Family,
                region_role: member.RegionRole,
                opening_ids: member.OpeningIds,
                shape_recipe_id: member.Recipe?.id ?? member.ShapeRecipeId,
                diameter_mm: member.DiameterMm,
                member_ids: ids,
                canonical_ids: canonicalIds,
                member_lengths_mm: lengths,
                count: count,
                raw_sum_mm: sumMm,
                mean_mm: meanMm,
                min_mm: minMm,
                max_mm: maxMm,
                mean_roles: roleMeans.ToArray(),
                display_mark: mark,
                pitch_mm: member.RequestedPitchMm,
                mass_kg: mass,
                measurement_basis: member.MeasurementBasis,
                source_ids: distinctSources,
                material_kind: member.MaterialKind,
                angle_code: member.AngleCode,
                unit_mass_kg_per_m: member.UnitMassKgPerM,
                member_length_basis: member.PresentationLengthBasis,
                length_policy_id: member.Recipe?.id ?? member.ShapeRecipeId
            ));
        }

        // 4. Verify coverage: every physical occurrence and angle must belong to exactly one group
        var expectedAllIds = occurrences.Select(o => o.id)
            .Concat(angles.Select(a => a.id))
            .ToHashSet(StringComparer.Ordinal);

        if (!assigned.SetEquals(expectedAllIds))
        {
            throw new DrawingStageFaultException(
                "GROUP_COVERAGE_MISMATCH",
                effective.id,
                ["member_ids"],
                $"expected {expectedAllIds.Count} members",
                $"assigned {assigned.Count} members");
        }

        // 5. Verify no mark collisions within the same component/face/layer scope
        AssertNoMarkCollisionWithinScope(groupItems);

        // 6. Compute canonical registry hash
        string registryHash = HashGroups(groupItems);

        return new GroupsV2(groupItems.ToArray(), registryHash);
    }

    private static string ResolveComponentKind(string hostId, GeometryV2 geometry)
    {
        var comp = geometry.components?.FirstOrDefault(c => string.Equals(c.id, hostId, StringComparison.OrdinalIgnoreCase));
        if (comp != null && !string.IsNullOrWhiteSpace(comp.kind)) return comp.kind;

        if (hostId.Contains("Base", StringComparison.OrdinalIgnoreCase)) return "Base";
        if (hostId.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return "Wall";
        if (hostId.Contains("Roof", StringComparison.OrdinalIgnoreCase)) return "Roof";
        if (hostId.Contains("Neck", StringComparison.OrdinalIgnoreCase)) return "Neck";
        if (hostId.Contains("Pad", StringComparison.OrdinalIgnoreCase)) return "Pad";
        return hostId;
    }

    private static ShapeRecipeV2? FindShapeRecipe(RulesV2 rules, string? recipeId, string profile, string componentKind, string family)
    {
        if (string.IsNullOrEmpty(recipeId) || rules.entries == null) return null;

        foreach (var r in rules.entries)
        {
            if (r.shapes == null) continue;
            bool profileMatch = r.profiles != null && (r.profiles.Contains(profile) || r.profiles.Contains("*"));
            if (!profileMatch) continue;

            foreach (var s in r.shapes)
            {
                if (string.Equals(s.id, recipeId, StringComparison.Ordinal))
                {
                    bool compMatch = s.component_kind == "*" || string.IsNullOrEmpty(s.component_kind) ||
                                     string.Equals(s.component_kind, componentKind, StringComparison.OrdinalIgnoreCase);
                    bool famMatch = s.family == "*" || string.IsNullOrEmpty(s.family) ||
                                    string.Equals(s.family, family, StringComparison.OrdinalIgnoreCase);
                    if (compMatch && famMatch) return s;
                }
            }
        }

        // Secondary search by exact shape recipe ID without strict component filter
        foreach (var r in rules.entries)
        {
            if (r.shapes == null) continue;
            foreach (var s in r.shapes)
            {
                if (string.Equals(s.id, recipeId, StringComparison.Ordinal)) return s;
            }
        }

        return null;
    }

    private static string BuildPartitionKey(string effectiveId, AccountingItem item)
    {
        var roleNames = item.RoleValues
            .Select(rv => rv.role)
            .OrderBy(r => r, StringComparer.Ordinal);

        return string.Join("|",
            effectiveId,
            item.MaterialKind,
            item.HostId,
            item.AssemblyId,
            item.Face,
            item.Layer,
            item.Direction,
            item.Family,
            item.ShapeRecipeId,
            item.DiameterMm?.ToString(CultureInfo.InvariantCulture) ?? "",
            item.AngleCode ?? "",
            item.UnitMassKgPerM?.ToString(CultureInfo.InvariantCulture) ?? "",
            item.MeasurementBasis,
            item.PresentationLengthBasis,
            item.RequestedPitchMm?.ToString(CultureInfo.InvariantCulture) ?? "",
            item.RegionRole,
            string.Join(",", item.OpeningIds),
            string.Join(",", roleNames));
    }

    private static string PrefixForExactFaceLayer(EffectiveV2 effective, AccountingItem member)
    {
        string? prefixKey = null;

        if (string.Equals(member.HostId, "Base", StringComparison.OrdinalIgnoreCase) ||
            member.HostId.Contains("Base", StringComparison.OrdinalIgnoreCase))
        {
            prefixKey = string.Equals(member.Layer, "Bottom", StringComparison.OrdinalIgnoreCase)
                ? "Annotation.Prefix.C"
                : "Annotation.Prefix.D";
        }
        else if (string.Equals(member.HostId, "Wall", StringComparison.OrdinalIgnoreCase) ||
                 member.HostId.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                 member.HostId.StartsWith("T", StringComparison.OrdinalIgnoreCase))
        {
            prefixKey = string.Equals(member.Face, "Outer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(member.Layer, "Outer", StringComparison.OrdinalIgnoreCase)
                ? "Annotation.Prefix.E"
                : "Annotation.Prefix.F";
        }
        else if (string.Equals(member.HostId, "Roof", StringComparison.OrdinalIgnoreCase) ||
                 member.HostId.Contains("Roof", StringComparison.OrdinalIgnoreCase))
        {
            prefixKey = string.Equals(member.Layer, "Top", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(member.Face, "Top", StringComparison.OrdinalIgnoreCase)
                ? "Annotation.Prefix.G"
                : "Annotation.Prefix.H";
        }
        else if (string.Equals(member.HostId, "Neck", StringComparison.OrdinalIgnoreCase) ||
                 member.HostId.Contains("Neck", StringComparison.OrdinalIgnoreCase))
        {
            prefixKey = string.Equals(member.Face, "Outer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(member.Layer, "Outer", StringComparison.OrdinalIgnoreCase)
                ? "Annotation.Prefix.I"
                : "Annotation.Prefix.J";
        }

        if (prefixKey != null && effective.parsed_fields != null)
        {
            var field = effective.parsed_fields.FirstOrDefault(p =>
                string.Equals(p.field_key, prefixKey, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(p.text));
            if (field != null && !string.IsNullOrWhiteSpace(field.text))
            {
                return field.text.Trim();
            }
        }

        // Check recipe tag bindings or parameters if present
        if (member.Recipe?.tag_bindings != null)
        {
            var tagBinding = member.Recipe.tag_bindings.FirstOrDefault(t =>
                string.Equals(t.tag, "SH", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.tag, "Prefix", StringComparison.OrdinalIgnoreCase));
            if (tagBinding != null && !string.IsNullOrWhiteSpace(tagBinding.literal))
            {
                return tagBinding.literal.Trim();
            }
        }

        // Special components (Panel, Catch, Accessory)
        if (string.Equals(member.MaterialKind, "Angle", StringComparison.OrdinalIgnoreCase))
        {
            return "V";
        }

        if (member.HostId.Contains("Panel", StringComparison.OrdinalIgnoreCase) ||
            member.HostId.Contains("Catch", StringComparison.OrdinalIgnoreCase) ||
            member.Family.Contains("Accessory", StringComparison.OrdinalIgnoreCase))
        {
            if (member.Recipe != null && !string.IsNullOrWhiteSpace(member.Recipe.native_asset))
            {
                return member.Recipe.native_asset;
            }
            throw new DrawingStageFaultException(
                "MARK_PREFIX_BINDING_MISSING",
                member.Id,
                ["display_mark"],
                "configured prefix binding",
                "missing",
                member.SourceIds);
        }

        // Standard structural member fallback prefix
        return prefixKey switch
        {
            "Annotation.Prefix.C" => "1",
            "Annotation.Prefix.D" => "2",
            "Annotation.Prefix.E" => "3",
            "Annotation.Prefix.F" => "4",
            "Annotation.Prefix.G" => "5",
            "Annotation.Prefix.H" => "6",
            "Annotation.Prefix.I" => "7",
            "Annotation.Prefix.J" => "8",
            _ => "D1"
        };
    }

    private static string SuffixForRegionRole(AccountingItem member, string partitionKey, RulesV2 rules)
    {
        if (string.Equals(member.RegionRole, "Left", StringComparison.OrdinalIgnoreCase)) return "1A";
        if (string.Equals(member.RegionRole, "Right", StringComparison.OrdinalIgnoreCase)) return "1B";
        if (string.Equals(member.RegionRole, "Above", StringComparison.OrdinalIgnoreCase)) return "2A";
        if (string.Equals(member.RegionRole, "Below", StringComparison.OrdinalIgnoreCase)) return "2B";

        if (string.Equals(member.RegionRole, "Middle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member.RegionRole, "Multiple", StringComparison.OrdinalIgnoreCase))
        {
            // NR-12 approved explicit naming
            var nr12Rule = rules.entries?.FirstOrDefault(r => string.Equals(r.id, "NR-12", StringComparison.Ordinal));
            var param = nr12Rule?.parameters?.FirstOrDefault(p =>
                string.Equals(p.key, $"suffix.{member.RegionRole}", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.key, "DisplayMark", StringComparison.OrdinalIgnoreCase));

            if (param != null && !string.IsNullOrWhiteSpace(param.text))
            {
                return param.text;
            }

            throw new DrawingStageFaultException(
                "RULE_BINDING_DATA_MISSING",
                "NR-12",
                ["DisplayMark"],
                "approved multi-opening suffix mapping",
                "missing",
                member.SourceIds,
                ["NR-12"]);
        }

        // Uncut bars: check recipe tag bindings or parameter suffix
        if (member.Recipe?.tag_bindings != null)
        {
            var suffixBinding = member.Recipe.tag_bindings.FirstOrDefault(t =>
                string.Equals(t.tag, "Suffix", StringComparison.OrdinalIgnoreCase));
            if (suffixBinding != null && !string.IsNullOrWhiteSpace(suffixBinding.literal))
            {
                return suffixBinding.literal;
            }
        }

        // Default uncut mark: primary mesh bars typically have empty suffix or directional marker
        return "";
    }

    private static string? ExactScopedAliasIfAny(string mark, string hostId, string face, string layer, RulesV2 rules, string[] ids)
    {
        if (rules.entries == null) return null;

        var nr10 = rules.entries.FirstOrDefault(r => string.Equals(r.id, "NR-10", StringComparison.Ordinal));
        if (nr10?.parameters != null)
        {
            var aliasParam = nr10.parameters.FirstOrDefault(p =>
                string.Equals(p.key, $"alias.{hostId}.{face}.{layer}.{mark}", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.key, $"alias.{mark}", StringComparison.OrdinalIgnoreCase));

            if (aliasParam != null && !string.IsNullOrWhiteSpace(aliasParam.text))
            {
                return aliasParam.text;
            }
        }

        return null;
    }

    private static double? SumActualMemberMasses(string[] ids, CanonicalRowV2[] canonical, AngleOccurrenceV2[] angles)
    {
        double totalKg = 0.0;
        bool hasMass = false;

        var angleMap = angles.ToDictionary(a => a.id, a => a.mass_kg, StringComparer.Ordinal);
        var canonicalRows = canonical
            .Where(c => c.member_ids != null && c.member_ids.Any(m => ids.Contains(m)))
            .ToArray();

        foreach (var id in ids)
        {
            if (angleMap.TryGetValue(id, out double aMass))
            {
                totalKg += aMass;
                hasMass = true;
                continue;
            }

            var row = canonicalRows.FirstOrDefault(r => r.member_ids.Contains(id));
            if (row != null && row.mass_kg.HasValue && row.quantity > 0)
            {
                totalKg += row.mass_kg.Value / row.quantity;
                hasMass = true;
            }
        }

        return hasMass ? totalKg : null;
    }

    private static string[] CanonicalIdsFor(string[] ids, CanonicalRowV2[] canonical)
    {
        return canonical
            .Where(c => c.member_ids != null && c.member_ids.Any(m => ids.Contains(m)))
            .Select(c => c.id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertNoMarkCollisionWithinScope(List<GroupV2> groups)
    {
        foreach (var scopeGroup in groups.GroupBy(g => (g.host_id, g.face, g.layer)))
        {
            var duplicates = scopeGroup
                .GroupBy(g => g.display_mark, StringComparer.Ordinal)
                .Where(dg => dg.Count() > 1)
                .ToList();

            if (duplicates.Count > 0)
            {
                var dup = duplicates[0];
                throw new DrawingStageFaultException(
                    "GROUP_MARK_COLLISION",
                    dup.Key,
                    ["display_mark"],
                    "unique display mark within scope",
                    $"{dup.Count()} occurrences of mark '{dup.Key}' in scope ({scopeGroup.Key.host_id}, {scopeGroup.Key.face}, {scopeGroup.Key.layer})");
            }
        }
    }

    private static double SumCompensated(double[] values)
    {
        double sum = 0.0;
        double c = 0.0;
        foreach (double v in values)
        {
            double y = v - c;
            double t = sum + y;
            c = (t - sum) - y;
            sum = t;
        }
        return sum;
    }

    private static string ComputeGroupId(string partitionKey, string[] memberIds)
    {
        string raw = partitionKey + "#" + string.Join(",", memberIds);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return "Group/" + Convert.ToHexString(hash)[..16];
    }

    private static string HashGroups(IEnumerable<GroupV2> groups)
    {
        var ordered = groups.OrderBy(g => g.id, StringComparer.Ordinal).ToArray();
        string json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
