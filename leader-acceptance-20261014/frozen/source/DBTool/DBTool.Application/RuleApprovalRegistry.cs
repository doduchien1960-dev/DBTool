#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;

namespace DBTool.Application.StageContractsV2
{
    /// <summary>
    /// Domain-level exception representing a strict stage fault with diagnostic metadata.
    /// </summary>
    public class StageFault : Exception
    {
        public string Code { get; }
        public string? Subject { get; }
        public string[] FieldPaths { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string[] Sources { get; }
        public string[] NeedsRules { get; }

        public StageFault(
            string code,
            string? subject,
            string[] fieldPaths,
            string expected,
            string actual,
            string[]? sources = null,
            string[]? needsRules = null)
            : base($"{code}: subject={subject ?? "null"}, fields=[{string.Join(", ", fieldPaths)}], expected={expected}, actual={actual}")
        {
            Code = code;
            Subject = subject;
            FieldPaths = fieldPaths ?? [];
            Expected = expected;
            Actual = actual;
            Sources = sources ?? [];
            NeedsRules = needsRules ?? [];
        }

        public DiagnosticV2 ToDiagnostic(string stage, string? manholeId = null, string severity = "Block") =>
            new(Code, stage, manholeId, Subject, FieldPaths, Expected, Actual, Sources, NeedsRules, severity);
    }
}

namespace DBTool.Application
{
    internal sealed class StageFaultException : DBTool.Application.StageContractsV2.StageFault
    {
        public StageFaultException(
            string code,
            string? subject,
            string[] fields,
            string expected,
            string actual,
            string[]? sources = null,
            string[]? needsRuleIds = null,
            string severity = "Block")
            : base(code, subject, fields, expected, actual, sources, needsRuleIds)
        {
        }
    }

    /// <summary>
    /// Loads and validates rule snapshots against owner approval contracts.
    /// Implements IRuleSnapshotReader without generating geometry from proposal prose.
    /// </summary>
    public sealed class RuleApprovalRegistry : IRuleSnapshotReader
    {
        public const string CanonicalSnapshotHash = "07a6ba4aab682dbd3c416a4b3ebc597eeff3674a52778dc133228dc75c45221c";
        public const string ApprovedApprover = "DucBao — chủ nhiệm, trực tiếp trong chat hiện tại";
        public const string ApprovedAtUtc = "2026-10-13T13:15:06.467490+00:00";
        public const string OwnerApprovalSourceId = "OWNER-APPROVAL-23-20261013";

    private static readonly HashSet<string> ApprovedProposalIds = new(StringComparer.Ordinal)
    {
        "NR-01", "NR-02", "NR-03", "NR-04", "NR-05", "NR-06", "NR-07", "NR-08",
        "NR-09", "NR-10", "NR-11", "NR-12", "NR-13", "NR-14", "NR-15", "NR-16",
        "NR-17", "NR-18", "NR-19", "NR-20", "NR-21", "NR-22", "NR-23"
    };

    private static readonly HashSet<string> ValidOperators = new(StringComparer.Ordinal)
    {
        "CONST", "REF", "ADD", "SUB", "MUL", "DIV", "MIN", "MAX",
        "SQRT", "SIN", "COS", "TAN", "ATAN2", "CEIL", "FLOOR", "ROUND",
        "MOD", "EQ", "NE", "LT", "LE", "GT", "GE", "AND", "OR", "NOT"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Loads a RulesV2 snapshot from the specified path or returns the embedded seed rules.
    /// Strictly verifies SHA256 and validates the 23 owner-approved rule headers.
    /// </summary>
    public RulesV2 Load(string snapshotPath, string snapshotSha256, SourceV2[] sources)
    {
        RulesV2 rules;
        string[] sourceIds = sources?.Select(s => s.id).ToArray() ?? [OwnerApprovalSourceId];

        if (!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath))
        {
            byte[] bytes = File.ReadAllBytes(snapshotPath);
            string actualFileSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(snapshotSha256) &&
                !string.Equals(actualFileSha256, snapshotSha256.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                throw new StageFaultException(
                    "RULE_SNAPSHOT_HASH_MISMATCH",
                    snapshotPath,
                    ["rule_snapshot_sha256"],
                    snapshotSha256.ToLowerInvariant(),
                    actualFileSha256,
                    sourceIds,
                    ["NR-15"]);
            }

            try
            {
                rules = JsonSerializer.Deserialize<RulesV2>(bytes, JsonOptions)
                    ?? throw new InvalidOperationException("Deserialized RulesV2 is null.");
            }
            catch (Exception ex)
            {
                throw new StageFaultException(
                    "RULE_SNAPSHOT_DESERIALIZE_FAILED",
                    snapshotPath,
                    ["rules"],
                    "valid RulesV2 JSON",
                    ex.Message,
                    sourceIds);
            }
        }
        else
        {
            rules = CreateApprovedSeed();
        }

        ValidateRules(rules, sourceIds);
        return rules;
    }

    /// <summary>
    /// Validates all rule entries in the snapshot against approved headers and AST schemas.
    /// </summary>
    public static void ValidateRules(RulesV2 rules, string[] sourceIds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.entries is null || rules.entries.Length == 0)
        {
            throw new StageFaultException(
                "RULE_ENTRIES_EMPTY",
                rules.snapshot_hash,
                ["rules.entries"],
                "non-empty rule entries",
                "0",
                sourceIds);
        }

        var entryMap = new Dictionary<string, RuleV2>(StringComparer.Ordinal);
        foreach (var entry in rules.entries)
        {
            if (entryMap.ContainsKey(entry.id))
            {
                throw new StageFaultException(
                    "RULE_DUPLICATE_ID",
                    entry.id,
                    ["rules.entries.id"],
                    "unique rule IDs",
                    entry.id,
                    entry.source_ids ?? sourceIds);
            }
            entryMap.Add(entry.id, entry);

            if (!string.Equals(entry.status, "Approved", StringComparison.Ordinal))
            {
                throw new StageFaultException(
                    "RULE_UNAPPROVED",
                    entry.id,
                    ["rules.entries.status"],
                    "Approved",
                    entry.status ?? "null",
                    entry.source_ids ?? sourceIds,
                    [entry.id]);
            }

            if (string.IsNullOrWhiteSpace(entry.approver))
            {
                throw new StageFaultException(
                    "RULE_UNAPPROVED",
                    entry.id,
                    ["rules.entries.approver"],
                    "Approved with signer",
                    "null",
                    entry.source_ids ?? sourceIds,
                    [entry.id]);
            }

            if (string.IsNullOrWhiteSpace(entry.approved_at_utc))
            {
                throw new StageFaultException(
                    "RULE_UNAPPROVED",
                    entry.id,
                    ["rules.entries.approved_at_utc"],
                    "Approved with timestamp",
                    "null",
                    entry.source_ids ?? sourceIds,
                    [entry.id]);
            }

            ValidateExpressions(entry);
        }

        foreach (var requiredId in ApprovedProposalIds)
        {
            if (!entryMap.ContainsKey(requiredId))
            {
                throw new StageFaultException(
                    "RULE_APPROVED_PROPOSAL_MISSING",
                    requiredId,
                    ["rules.entries"],
                    $"Approved proposal {requiredId} present",
                    "missing",
                    sourceIds,
                    [requiredId]);
            }
        }
    }

    private static void ValidateExpressions(RuleV2 rule)
    {
        if (rule.expressions is null || rule.expressions.Length == 0) return;

        var nodeMap = rule.expressions.ToDictionary(e => e.id, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in rule.expressions)
        {
            if (!ValidOperators.Contains(node.op))
            {
                throw new StageFaultException(
                    "RULE_EXPRESSION_OP_INVALID",
                    rule.id,
                    [node.id],
                    "valid registered op",
                    node.op,
                    node.source_ids ?? rule.source_ids,
                    [rule.id]);
            }

            if (node.op == "CONST")
            {
                if (node.arg_ids != null && node.arg_ids.Length > 0)
                {
                    throw new StageFaultException(
                        "RULE_EXPRESSION_ARITY_INVALID",
                        rule.id,
                        [node.id],
                        "0 args for CONST",
                        node.arg_ids.Length.ToString(),
                        node.source_ids ?? rule.source_ids,
                        [rule.id]);
                }
                if (!node.constant.HasValue || !double.IsFinite(node.constant.Value))
                {
                    throw new StageFaultException(
                        "RULE_EXPRESSION_NONFINITE_CONST",
                        rule.id,
                        [node.id],
                        "finite constant",
                        node.constant?.ToString() ?? "null",
                        node.source_ids ?? rule.source_ids,
                        [rule.id]);
                }
            }

            DetectCycles(rule, node.id, nodeMap, visiting, visited);
        }
    }

    private static void DetectCycles(
        RuleV2 rule,
        string nodeId,
        Dictionary<string, ExprNodeV2> nodeMap,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(nodeId)) return;
        if (visiting.Contains(nodeId))
        {
            throw new StageFaultException(
                "RULE_EXPRESSION_CYCLE",
                rule.id,
                [nodeId],
                "acyclic DAG",
                string.Join(" -> ", visiting) + " -> " + nodeId,
                rule.source_ids,
                [rule.id]);
        }

        visiting.Add(nodeId);
        if (nodeMap.TryGetValue(nodeId, out var node) && node.arg_ids != null)
        {
            foreach (var argId in node.arg_ids)
            {
                DetectCycles(rule, argId, nodeMap, visiting, visited);
            }
        }
        visiting.Remove(nodeId);
        visited.Add(nodeId);
    }

    /// <summary>
    /// Evaluates an expression node in the rule AST with cycle detection.
    /// </summary>
    public static (double Value, string Unit) EvaluateExpression(
        RuleV2 rule,
        string nodeId,
        IReadOnlyDictionary<string, (double Value, string Unit)> environment,
        ISet<string>? visiting = null)
    {
        visiting ??= new HashSet<string>(StringComparer.Ordinal);
        if (visiting.Contains(nodeId))
        {
            throw new StageFaultException(
                "RULE_EXPRESSION_CYCLE",
                rule.id,
                [nodeId],
                "acyclic expression DAG",
                nodeId,
                rule.source_ids,
                [rule.id]);
        }

        visiting.Add(nodeId);
        var node = rule.expressions?.FirstOrDefault(e => e.id == nodeId)
            ?? throw new StageFaultException(
                "RULE_EXPRESSION_NODE_MISSING",
                rule.id,
                [nodeId],
                "present expression node",
                "missing",
                rule.source_ids,
                [rule.id]);

        var args = (node.arg_ids ?? [])
            .Select(arg => EvaluateExpression(rule, arg, environment, visiting))
            .ToArray();

        visiting.Remove(nodeId);

        return node.op switch
        {
            "CONST" => (node.constant!.Value, node.unit),
            "REF" => environment.TryGetValue(node.ref_path ?? "", out var refVal)
                ? refVal
                : throw new StageFaultException("RULE_EXPRESSION_REF_UNRESOLVED", rule.id, [node.ref_path ?? ""], "resolvable ref_path", "not in environment", rule.source_ids, [rule.id]),
            "ADD" => (args[0].Value + args[1].Value, args[0].Unit),
            "SUB" => (args[0].Value - args[1].Value, args[0].Unit),
            "MUL" => (args[0].Value * args[1].Value, node.unit ?? args[0].Unit),
            "DIV" => args[1].Value == 0
                ? throw new StageFaultException("RULE_EXPRESSION_DIV_BY_ZERO", rule.id, [node.id], "nonzero divisor", "0", rule.source_ids, [rule.id])
                : (args[0].Value / args[1].Value, node.unit ?? args[0].Unit),
            "MIN" => (Math.Min(args[0].Value, args[1].Value), args[0].Unit),
            "MAX" => (Math.Max(args[0].Value, args[1].Value), args[0].Unit),
            "SQRT" => args[0].Value < 0
                ? throw new StageFaultException("RULE_EXPRESSION_NEGATIVE_SQRT", rule.id, [node.id], "non-negative argument", args[0].Value.ToString(), rule.source_ids, [rule.id])
                : (Math.Sqrt(args[0].Value), args[0].Unit),
            "SIN" => (Math.Sin(args[0].Value), "dimensionless"),
            "COS" => (Math.Cos(args[0].Value), "dimensionless"),
            "TAN" => (Math.Tan(args[0].Value), "dimensionless"),
            "ATAN2" => (Math.Atan2(args[0].Value, args[1].Value), "rad"),
            "CEIL" => (Math.Ceiling(args[0].Value), args[0].Unit),
            "FLOOR" => (Math.Floor(args[0].Value), args[0].Unit),
            "ROUND" => (Math.Round(args[0].Value, MidpointRounding.AwayFromZero), args[0].Unit),
            "MOD" => (args[0].Value - Math.Floor(args[0].Value / args[1].Value) * args[1].Value, args[0].Unit),
            "EQ" => (args[0].Value == args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "NE" => (args[0].Value != args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "LT" => (args[0].Value < args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "LE" => (args[0].Value <= args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "GT" => (args[0].Value > args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "GE" => (args[0].Value >= args[1].Value ? 1.0 : 0.0, "dimensionless"),
            "AND" => (args[0].Value != 0 && args[1].Value != 0 ? 1.0 : 0.0, "dimensionless"),
            "OR" => (args[0].Value != 0 || args[1].Value != 0 ? 1.0 : 0.0, "dimensionless"),
            "NOT" => (args[0].Value == 0 ? 1.0 : 0.0, "dimensionless"),
            _ => throw new StageFaultException("RULE_EXPRESSION_OP_INVALID", rule.id, [node.id], "valid registered op", node.op, rule.source_ids, [rule.id])
        };
    }

    /// <summary>
    /// Looks up a single scoped rule matching ID, profile, component, and shape kind.
    /// </summary>
    public static RuleV2 FindRule(RulesV2 rules, string id, string profile, string component, string shapeKind)
    {
        var matches = rules.entries
            .Where(r => r.id == id &&
                        (r.profiles.Contains(profile) || r.profiles.Contains("*")) &&
                        (r.components.Contains(component) || r.components.Contains("*")) &&
                        (r.shape_kinds.Contains(shapeKind) || r.shape_kinds.Contains("*")))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new StageFaultException(
                "RULE_SCOPE_MISMATCH",
                id,
                ["rules.entries"],
                "exactly one scoped rule",
                matches.Length.ToString(),
                [],
                [id]);
        }

        var rule = matches[0];
        if (!string.Equals(rule.status, "Approved", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(rule.approver) ||
            string.IsNullOrWhiteSpace(rule.approved_at_utc))
        {
            throw new StageFaultException(
                "RULE_UNAPPROVED",
                id,
                ["rules.entries.status"],
                "Approved with signer/time",
                rule.status ?? "null",
                rule.source_ids,
                [id]);
        }

        return rule;
    }

    /// <summary>
    /// Looks up a typed parameter from a rule.
    /// Raises RULE_BINDING_DATA_MISSING if missing, or RULE_PARAMETER_TYPE on mismatch.
    /// </summary>
    public static ParamV2 GetParam(RuleV2 rule, string key, string kind, string unit)
    {
        var values = (rule.parameters ?? []).Where(p => p.key == key).ToArray();
        if (values.Length != 1)
        {
            throw new StageFaultException(
                "RULE_BINDING_DATA_MISSING",
                rule.id,
                [key],
                "one matching parameter",
                values.Length.ToString(),
                rule.source_ids,
                [rule.id]);
        }

        var param = values[0];
        if (param.kind != kind || param.unit != unit || (param.source_ids != null && param.source_ids.Length == 0))
        {
            throw new StageFaultException(
                "RULE_PARAMETER_TYPE",
                rule.id,
                [key],
                $"{kind}/{unit}",
                $"{param.kind}/{param.unit}",
                param.source_ids ?? rule.source_ids,
                [rule.id]);
        }

        return param;
    }

    public static double GetParamNumber(RuleV2 rule, string key, string unit)
    {
        var param = GetParam(rule, key, "Number", unit);
        return param.number ?? throw new StageFaultException("RULE_BINDING_DATA_MISSING", rule.id, [key], "numeric value", "null", rule.source_ids, [rule.id]);
    }

    public static string GetParamText(RuleV2 rule, string key, string unit = "none")
    {
        var param = GetParam(rule, key, "Text", unit);
        return param.text ?? throw new StageFaultException("RULE_BINDING_DATA_MISSING", rule.id, [key], "text value", "null", rule.source_ids, [rule.id]);
    }

    public static bool GetParamBool(RuleV2 rule, string key, string unit = "none")
    {
        var param = GetParam(rule, key, "Bool", unit);
        return param.flag ?? throw new StageFaultException("RULE_BINDING_DATA_MISSING", rule.id, [key], "boolean value", "null", rule.source_ids, [rule.id]);
    }

    /// <summary>
    /// Looks up a shape recipe by ID within the rule snapshot matching component and family.
    /// </summary>
    public static ShapeRecipeV2 FindShapeRecipe(RulesV2 rules, string id, string profile, string componentKind, string family)
    {
        var candidates = (
            from r in rules.entries
            from s in r.shapes ?? []
            where s.id == id &&
                  (s.component_kind == componentKind || s.component_kind == "*") &&
                  (s.family == family || s.family == "*") &&
                  (r.profiles.Contains(profile) || r.profiles.Contains("*"))
            select (Rule: r, Shape: s)
        ).ToArray();

        if (candidates.Length != 1)
        {
            throw new StageFaultException(
                "SHAPE_RECIPE_BINDING_MISSING",
                id,
                ["shape_recipe_id"],
                "exactly one scoped shape recipe",
                candidates.Length.ToString(),
                [],
                ["NR-02", "NR-04", "NR-08"]);
        }

        return candidates[0].Shape;
    }

    /// <summary>
    /// Constructs the exact 23 owner-approved rules snapshot (OWNER-APPROVAL-23-20261013).
    /// </summary>
    public static RulesV2 CreateApprovedSeed()
    {
        string[] allProfiles = ["GaThamLienKhoi", "GaThamNapRoi", "GaThamThuNapRoi"];
        string[] allComponents = ["*"];
        string[] allShapeKinds = ["*"];
        string[] ownerSources = [OwnerApprovalSourceId];

        var entries = new List<RuleV2>
        {
            new("NR-01", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("convention", "Text", null, "Surface", null, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-02", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-03", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-04", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-05", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-06", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                ["GaThamNapRoi", "GaThamThuNapRoi"], allComponents, allShapeKinds,
                [new("pitch_quantum_mm", "Number", 50.0, null, null, "mm", ownerSources)],
                [], [], ownerSources),

            new("NR-07", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-08", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                ["GaThamThuNapRoi"], allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-09", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                ["GaThamThuNapRoi"], allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-10", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("alias_requires_exact_member_set", "Bool", null, null, true, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-11", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("regenerate_from_members", "Bool", null, null, true, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-12", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-13", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [
                    new("decimals.DAI/mm", "Number", 0.0, null, null, "dimensionless", ownerSources),
                    new("decimals.Leg/mm", "Number", 0.0, null, null, "dimensionless", ownerSources),
                    new("decimals.TDAI/m", "Number", 2.0, null, null, "dimensionless", ownerSources),
                    new("decimals.KL/kg", "Number", 2.0, null, null, "dimensionless", ownerSources),
                    new("midpoint.DAI/mm", "Text", null, "AwayFromZero", null, "none", ownerSources),
                    new("midpoint.Leg/mm", "Text", null, "AwayFromZero", null, "none", ownerSources),
                    new("midpoint.TDAI/m", "Text", null, "AwayFromZero", null, "none", ownerSources),
                    new("midpoint.KL/kg", "Text", null, "AwayFromZero", null, "none", ownerSources)
                ],
                [], [], ownerSources),

            new("NR-14", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [
                    new("bucket_mode", "Text", null, "PhysicalOwner", null, "none", ownerSources),
                    new("summary_diameter_boundary_mm", "Number", 10.0, null, null, "mm", ownerSources)
                ],
                [], [], ownerSources),

            new("NR-15", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("canonical_bundle", "Text", null, "acad-original", null, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-16", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-17", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("missing_named_default_action", "Text", null, "Block", null, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-18", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-19", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-20", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources),

            new("NR-21", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [new("collision_action", "Text", null, "Reject", null, "none", ownerSources)],
                [], [], ownerSources),

            new("NR-22", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds,
                [
                    new("fixed_single_view_overflow_action", "Text", null, "LAYOUT_SINGLE_VIEW_NO_FIT", null, "none", ownerSources),
                    new("auto_requires_request", "Bool", null, null, true, "none", ownerSources)
                ],
                [], [], ownerSources),

            new("NR-23", "owner-20261013-r2", "Approved", ApprovedApprover, ApprovedAtUtc,
                allProfiles, allComponents, allShapeKinds, [], [], [], ownerSources)
        };

        return new RulesV2(
            snapshot_hash: CanonicalSnapshotHash,
            entries: entries.ToArray(),
            geometry_tolerance_mm: null,
            length_tolerance_mm: null,
            paper_tolerance_mm: null,
            mass_tolerance_kg: null,
            basis_tolerance: null,
            angle_tolerance_rad: null,
            area_tolerance_mm2: null,
            pdf_tolerance_points: null
        );
    }
}
}
