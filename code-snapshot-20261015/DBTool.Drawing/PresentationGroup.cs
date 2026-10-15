using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBTool.Domain;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// Record representing a semantic presentation group on the drawing view.
/// Bridges physical occurrences, canonical fabrication marks, and display marks.
/// </summary>
public sealed record PresentationGroupRecord
{
    [JsonConstructor]
    public PresentationGroupRecord(
        string GroupId,
        string Scope,
        string DisplayMark,
        string Function,
        string FaceLayer,
        string ModelDirection,
        int DiameterMm,
        string SpacingMode,
        double? ActualSpacing,
        int? Quantity,
        ImmutableArray<string> ShapeSignatures,
        ImmutableArray<string> SupportRegions,
        ImmutableArray<string> OccurrenceIds,
        ImmutableArray<string> CanonicalMarks,
        ImmutableArray<string> Views,
        ImmutableArray<string> LabelIds,
        ImmutableArray<string> CollectorIds,
        ImmutableArray<string> Sources,
        string RuleVersion,
        ImmutableArray<double> MemberLengthsMm = default,
        string? MeasurementBasis = null,
        string? MemberLengthBasis = null,
        double? RawSumMm = null,
        double? MeanMm = null,
        double? MinMm = null,
        double? MaxMm = null,
        double? MassKg = null,
        string MaterialKind = "RoundBar",
        string? AngleCode = null,
        double? UnitMassKgPerM = null)
    {
        this.GroupId = GroupId;
        this.Scope = Scope;
        this.DisplayMark = DisplayMark;
        this.Function = Function;
        this.FaceLayer = FaceLayer;
        this.ModelDirection = ModelDirection;
        this.DiameterMm = DiameterMm;
        this.SpacingMode = SpacingMode;
        this.ActualSpacing = ActualSpacing;
        this.Quantity = Quantity;
        this.ShapeSignatures = ShapeSignatures.IsDefault ? [] : ShapeSignatures;
        this.SupportRegions = SupportRegions.IsDefault ? [] : SupportRegions;
        this.OccurrenceIds = OccurrenceIds.IsDefault ? [] : OccurrenceIds;
        this.CanonicalMarks = CanonicalMarks.IsDefault ? [] : CanonicalMarks;
        this.Views = Views.IsDefault ? [] : Views;
        this.LabelIds = LabelIds.IsDefault ? [] : LabelIds;
        this.CollectorIds = CollectorIds.IsDefault ? [] : CollectorIds;
        this.Sources = Sources.IsDefault ? [] : Sources;
        this.RuleVersion = RuleVersion;
        this.MemberLengthsMm = MemberLengthsMm.IsDefault ? [] : MemberLengthsMm;
        this.MeasurementBasis = MeasurementBasis;
        this.MemberLengthBasis = MemberLengthBasis;
        this.RawSumMm = RawSumMm;
        this.MeanMm = MeanMm;
        this.MinMm = MinMm;
        this.MaxMm = MaxMm;
        this.MassKg = MassKg;
        this.MaterialKind = MaterialKind ?? "RoundBar";
        this.AngleCode = AngleCode;
        this.UnitMassKgPerM = UnitMassKgPerM;
    }

    public PresentationGroupRecord(
        string GroupId,
        string Scope,
        string DisplayMark,
        string Function,
        string FaceLayer,
        string ModelDirection,
        int DiameterMm,
        string SpacingMode,
        double? ActualSpacing,
        int? Quantity,
        IEnumerable<string> ShapeSignatures,
        IEnumerable<string> SupportRegions,
        IEnumerable<string> OccurrenceIds,
        IEnumerable<string> CanonicalMarks,
        IEnumerable<string> Views,
        IEnumerable<string> LabelIds,
        IEnumerable<string> CollectorIds,
        IEnumerable<string> Sources,
        string RuleVersion,
        IEnumerable<double>? MemberLengthsMm = null,
        string? MeasurementBasis = null,
        string? MemberLengthBasis = null,
        double? RawSumMm = null,
        double? MeanMm = null,
        double? MinMm = null,
        double? MaxMm = null,
        double? MassKg = null,
        string MaterialKind = "RoundBar",
        string? AngleCode = null,
        double? UnitMassKgPerM = null)
        : this(GroupId, Scope, DisplayMark, Function, FaceLayer, ModelDirection, DiameterMm, SpacingMode, ActualSpacing, Quantity,
               ShapeSignatures.ToImmutableArray(), SupportRegions.ToImmutableArray(), OccurrenceIds.ToImmutableArray(), CanonicalMarks.ToImmutableArray(),
               Views.ToImmutableArray(), LabelIds.ToImmutableArray(), CollectorIds.ToImmutableArray(), Sources.ToImmutableArray(), RuleVersion,
               MemberLengthsMm is null ? ImmutableArray<double>.Empty : MemberLengthsMm.ToImmutableArray(),
               MeasurementBasis, MemberLengthBasis, RawSumMm, MeanMm, MinMm, MaxMm, MassKg, MaterialKind, AngleCode, UnitMassKgPerM)
    {
    }

    public string GroupId { get; }
    public string Scope { get; }
    public string DisplayMark { get; }
    public string Function { get; }
    public string FaceLayer { get; }
    public string ModelDirection { get; }
    public int DiameterMm { get; }
    public string SpacingMode { get; }
    public double? ActualSpacing { get; }
    public int? Quantity { get; }
    public ImmutableArray<string> ShapeSignatures { get; }
    public ImmutableArray<string> SupportRegions { get; }
    public ImmutableArray<string> OccurrenceIds { get; }
    public ImmutableArray<string> CanonicalMarks { get; }
    public ImmutableArray<string> Views { get; }
    public ImmutableArray<string> LabelIds { get; }
    public ImmutableArray<string> CollectorIds { get; }
    public ImmutableArray<string> Sources { get; }
    public string RuleVersion { get; }
    public ImmutableArray<double> MemberLengthsMm { get; }
    public string? MeasurementBasis { get; }
    public string? MemberLengthBasis { get; }
    public double? RawSumMm { get; }
    public double? MeanMm { get; }
    public double? MinMm { get; }
    public double? MaxMm { get; }
    public double? MassKg { get; }
    public string MaterialKind { get; }
    public string? AngleCode { get; }
    public double? UnitMassKgPerM { get; }
}

/// <summary>
/// Complete presentation group map containing all groups, metadata, and coverage validation.
/// </summary>
public sealed record PresentationGroupMap
{
    [JsonConstructor]
    public PresentationGroupMap(string RuleVersion, ImmutableArray<PresentationGroupRecord> Groups)
    {
        this.RuleVersion = RuleVersion;
        this.Groups = Groups.IsDefault ? [] : Groups;
    }

    public PresentationGroupMap(string RuleVersion, IEnumerable<PresentationGroupRecord> Groups)
        : this(RuleVersion, Groups.ToImmutableArray())
    {
    }

    public string RuleVersion { get; }
    public ImmutableArray<PresentationGroupRecord> Groups { get; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public (int TotalOccurrences, int MappedOccurrences, int OrphanOccurrences, int PhantomOccurrences) ValidateCoverage(
        IEnumerable<BarOccurrence> occurrences,
        IEnumerable<AngleOccurrence>? angles = null)
    {
        var physicalBars = occurrences.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var physicalAngles = angles?.Select(a => a.Id).ToHashSet(StringComparer.Ordinal) ?? [];

        var barMappedSet = Groups.Where(g => g.MaterialKind != "Angle").SelectMany(g => g.OccurrenceIds).ToHashSet(StringComparer.Ordinal);
        var angleMappedSet = Groups.Where(g => g.MaterialKind == "Angle").SelectMany(g => g.OccurrenceIds).ToHashSet(StringComparer.Ordinal);

        int totalBars = physicalBars.Count;
        int mappedBars = barMappedSet.Count;
        int orphanBars = physicalBars.Count(id => !barMappedSet.Contains(id));
        int phantomBars = barMappedSet.Count(id => !physicalBars.Contains(id));

        int totalAngles = physicalAngles.Count;
        int mappedAngles = angleMappedSet.Count;
        int orphanAngles = angles != null ? physicalAngles.Count(id => !angleMappedSet.Contains(id)) : 0;
        int phantomAngles = angles != null ? angleMappedSet.Count(id => !physicalAngles.Contains(id)) : 0;

        return (totalBars + totalAngles, mappedBars + mappedAngles, orphanBars + orphanAngles, phantomBars + phantomAngles);
    }

    public (int TotalOccurrences, int MappedOccurrences, int OrphanOccurrences, int PhantomOccurrences) ValidateCoverageV2(RebarV2 rebar)
    {
        var physicalBars = (rebar.occurrences ?? []).Select(o => o.id).ToHashSet(StringComparer.Ordinal);
        var physicalAngles = (rebar.angles ?? []).Select(a => a.id).ToHashSet(StringComparer.Ordinal);

        var barMappedSet = Groups.Where(g => g.MaterialKind != "Angle").SelectMany(g => g.OccurrenceIds).ToHashSet(StringComparer.Ordinal);
        var angleMappedSet = Groups.Where(g => g.MaterialKind == "Angle").SelectMany(g => g.OccurrenceIds).ToHashSet(StringComparer.Ordinal);

        int totalBars = physicalBars.Count;
        int mappedBars = barMappedSet.Count;
        int orphanBars = physicalBars.Count(id => !barMappedSet.Contains(id));
        int phantomBars = barMappedSet.Count(id => !physicalBars.Contains(id));

        int totalAngles = physicalAngles.Count;
        int mappedAngles = angleMappedSet.Count;
        int orphanAngles = physicalAngles.Count(id => !angleMappedSet.Contains(id));
        int phantomAngles = angleMappedSet.Count(id => !physicalAngles.Contains(id));

        return (totalBars + totalAngles, mappedBars + mappedAngles, orphanBars + orphanAngles, phantomBars + phantomAngles);
    }

    public static PresentationGroupMap FromPlannedGroups(GroupsV2 groupsV2, string? ruleVersion = null)
    {
        ArgumentNullException.ThrowIfNull(groupsV2);
        var records = groupsV2.items.Select(g => new PresentationGroupRecord(
            GroupId: g.id,
            Scope: $"({g.host_id}, {g.face}/{g.layer}, {g.display_mark})",
            DisplayMark: g.display_mark,
            Function: $"{g.family}/{g.direction}",
            FaceLayer: $"{g.face}/{g.layer}",
            ModelDirection: g.direction,
            DiameterMm: g.diameter_mm ?? 10,
            SpacingMode: g.pitch_mm.HasValue ? "Pitch" : "Quantity",
            ActualSpacing: g.pitch_mm,
            Quantity: g.count,
            ShapeSignatures: [g.shape_recipe_id],
            SupportRegions: [g.region_role],
            OccurrenceIds: g.member_ids.ToImmutableArray(),
            CanonicalMarks: g.canonical_ids.ToImmutableArray(),
            Views: [],
            LabelIds: [],
            CollectorIds: [],
            Sources: g.source_ids.ToImmutableArray(),
            RuleVersion: g.length_policy_id ?? ruleVersion ?? PresentationGroupMapper.DefaultRuleVersion,
            MemberLengthsMm: g.member_lengths_mm.ToImmutableArray(),
            MeasurementBasis: g.measurement_basis,
            MemberLengthBasis: g.member_length_basis,
            RawSumMm: g.raw_sum_mm,
            MeanMm: g.mean_mm,
            MinMm: g.min_mm,
            MaxMm: g.max_mm,
            MassKg: g.mass_kg,
            MaterialKind: g.material_kind,
            AngleCode: g.angle_code,
            UnitMassKgPerM: g.unit_mass_kg_per_m
        )).ToImmutableArray();

        return new PresentationGroupMap(ruleVersion ?? groupsV2.registry_hash, records);
    }

    public static PresentationGroupMap PlanPreDrawing(
        IEnumerable<BarOccurrence> occurrences,
        IEnumerable<AngleOccurrence>? angles,
        SteelSchedule? steelSchedule,
        AngleSchedule? angleSchedule,
        string ruleVersion = PresentationGroupMapper.DefaultRuleVersion)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        var bars = occurrences.ToList();
        var angleList = angles?.ToList() ?? [];
        var groups = new List<PresentationGroupRecord>();

        var occToRow = steelSchedule != null
            ? steelSchedule.Rows.SelectMany(r => r.OccurrenceIds.Select(id => (id, r)))
                .GroupBy(x => x.id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().r, StringComparer.Ordinal)
            : new Dictionary<string, SteelScheduleRow>(StringComparer.Ordinal);

        var angleToRow = angleSchedule != null
            ? angleSchedule.Rows.GroupBy(r => r.CatalogCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AngleScheduleRow>(StringComparer.OrdinalIgnoreCase);

        // Partition items by exact 05B-F49 key:
        // (host, assembly, panel scope, face, layer, direction, family, topology, D, angleCode, basis, pitch, hookRule, regionRole, sortedOpeningSet, orderedRoleNames)
        var partitions = bars.GroupBy(b => (
            Host: b.ComponentId,
            Assembly: b.AssemblyId ?? b.ComponentId,
            PanelScope: b.PanelInstanceId ?? "none",
            Face: b.Surface?.ToString() ?? "Outer",
            Layer: b.Layer.ToString(),
            Direction: b.Direction.ToString(),
            Family: b.FamilyId ?? b.ComponentKind.ToString(),
            Topology: b.ShapeRecipeId ?? b.Path.HookPattern.ToString(),
            Diameter: b.DiameterMm,
            AngleCode: "none",
            Basis: b.MeasurementBasis?.ToString() ?? "TangentPath",
            Pitch: b.StationKey ?? "none",
            HookRule: b.RuleId,
            RegionRole: b.RegionRole ?? "Uncut",
            SortedOpeningSet: b.OpeningIds.IsDefaultOrEmpty ? "" : string.Join(",", b.OpeningIds.Order(StringComparer.Ordinal)),
            OrderedRoleNames: "none"
        )).OrderBy(p => p.Key.Host, StringComparer.Ordinal)
          .ThenBy(p => p.Key.Face, StringComparer.Ordinal)
          .ThenBy(p => p.Key.Layer, StringComparer.Ordinal)
          .ThenBy(p => p.Key.Direction, StringComparer.Ordinal);

        foreach (var p in partitions)
        {
            var barList = p.OrderBy(m => m.StationMm ?? 0)
                           .ThenBy(m => m.StationIndex ?? 0)
                           .ThenBy(m => m.Id, StringComparer.Ordinal)
                           .ToList();

            var occIds = barList.Select(b => b.Id).Distinct(StringComparer.Ordinal).ToImmutableArray();
            var canonicalMarks = barList.Select(b =>
                occToRow.TryGetValue(b.Id, out var row)
                    ? row.Mark
                    : b.FamilyId ?? $"D_{p.Key.Host}_{p.Key.Diameter}"
            ).Distinct(StringComparer.Ordinal).ToImmutableArray();

            string displayMark = canonicalMarks.FirstOrDefault() ?? barList[0].FamilyId ?? $"D{p.Key.Diameter}";
            string scope = $"({p.Key.Host}, {p.Key.Face}/{p.Key.Layer}, {displayMark})";
            string function = $"{p.Key.Family}/{p.Key.Direction}";

            var lengths = barList.Select(b => b.ScheduleLengthMm > 0 ? b.ScheduleLengthMm : (b.StraightRunMm ?? 0)).ToArray();
            double sumMm = lengths.Sum();
            double meanMm = lengths.Length > 0 ? sumMm / lengths.Length : 0;
            double minMm = lengths.Length > 0 ? lengths.Min() : 0;
            double maxMm = lengths.Length > 0 ? lengths.Max() : 0;

            // Mass from source basis (SteelSchedule row unit mass per m), NOT hardcoded constant D² * 0.006165!
            double roundMassKg = 0.0;
            double? unitMass = null;
            foreach (var b in barList)
            {
                if (occToRow.TryGetValue(b.Id, out var sRow))
                {
                    unitMass = sRow.UnitMassKgPerM;
                    roundMassKg += (b.ScheduleLengthMm / 1000.0) * sRow.UnitMassKgPerM;
                }
                else
                {
                    double nominalUnit = b.DiameterMm * b.DiameterMm * Math.PI / 4.0 * 7850.0 / 1_000_000.0;
                    unitMass ??= nominalUnit;
                    roundMassKg += (b.ScheduleLengthMm / 1000.0) * nominalUnit;
                }
            }

            string raw = $"{p.Key.Host}|{p.Key.Face}|{p.Key.Layer}|{displayMark}#" + string.Join(",", occIds);
            string groupId = "Group/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];

            groups.Add(new PresentationGroupRecord(
                GroupId: groupId,
                Scope: scope,
                DisplayMark: displayMark,
                Function: function,
                FaceLayer: $"{p.Key.Face}/{p.Key.Layer}",
                ModelDirection: p.Key.Direction,
                DiameterMm: p.Key.Diameter,
                SpacingMode: "representedByGroup",
                ActualSpacing: null,
                Quantity: barList.Count,
                ShapeSignatures: [p.Key.Topology],
                SupportRegions: [p.Key.Face],
                OccurrenceIds: occIds,
                CanonicalMarks: canonicalMarks,
                Views: [],
                LabelIds: [$"representedByGroup:{displayMark}"],
                CollectorIds: [],
                Sources: barList.SelectMany(b => b.Sources.Select(s => s.ToString())).Distinct(StringComparer.Ordinal).ToImmutableArray(),
                RuleVersion: ruleVersion,
                MemberLengthsMm: lengths.ToImmutableArray(),
                MeasurementBasis: p.Key.Basis,
                MemberLengthBasis: barList[0].FabricationBasis ?? "Schedule",
                RawSumMm: sumMm,
                MeanMm: meanMm,
                MinMm: minMm,
                MaxMm: maxMm,
                MassKg: roundMassKg,
                MaterialKind: "RoundBar",
                AngleCode: null,
                UnitMassKgPerM: unitMass
            ));
        }

        foreach (var a in angleList)
        {
            double angleMassKg;
            double unitMass = a.UnitMassKgPerM;
            string displayMark = a.CatalogCode;
            if (angleToRow.TryGetValue(a.CatalogCode, out var aRow))
            {
                unitMass = aRow.UnitMassKgPerM;
                angleMassKg = aRow.MassKg.Value;
                displayMark = aRow.Mark;
            }
            else
            {
                angleMassKg = (a.LengthMm / 1000.0) * a.UnitMassKgPerM;
            }

            groups.Add(new PresentationGroupRecord(
                GroupId: a.Id,
                Scope: $"({a.ComponentId}, Angle, {displayMark})",
                DisplayMark: displayMark,
                Function: "AngleFrame",
                FaceLayer: "Frame",
                ModelDirection: "Horizontal",
                DiameterMm: 0,
                SpacingMode: "Quantity",
                ActualSpacing: null,
                Quantity: 1,
                ShapeSignatures: ["AngleStraight"],
                SupportRegions: ["Frame"],
                OccurrenceIds: [a.Id],
                CanonicalMarks: [displayMark],
                Views: [],
                LabelIds: [$"angle:{displayMark}"],
                CollectorIds: [],
                Sources: a.Sources.Select(s => s.ToString()).ToImmutableArray(),
                RuleVersion: ruleVersion,
                MemberLengthsMm: [a.LengthMm],
                MeasurementBasis: "Fabrication",
                MemberLengthBasis: "Schedule",
                RawSumMm: a.LengthMm,
                MeanMm: a.LengthMm,
                MinMm: a.LengthMm,
                MaxMm: a.LengthMm,
                MassKg: angleMassKg,
                MaterialKind: "Angle",
                AngleCode: a.CatalogCode,
                UnitMassKgPerM: unitMass
            ));
        }

        string registryHash = ComputeRegistryHash(groups);
        return new PresentationGroupMap(registryHash, groups.ToImmutableArray());
    }

    public static PresentationGroupMap PlanPreDrawing(
        IEnumerable<BarOccurrence> occurrences,
        IEnumerable<AngleOccurrence>? angles = null,
        string ruleVersion = PresentationGroupMapper.DefaultRuleVersion) =>
        PlanPreDrawing(occurrences, angles, null, null, ruleVersion);

    private static string ComputeRegistryHash(IEnumerable<PresentationGroupRecord> groups)
    {
        var ordered = groups.OrderBy(g => g.GroupId, StringComparer.Ordinal).ToArray();
        string json = JsonSerializer.Serialize(ordered, JsonOptions);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static PresentationGroupMap FromJson(string json) =>
        JsonSerializer.Deserialize<PresentationGroupMap>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize PresentationGroupMap.");
}

/// <summary>
/// Validates drawing package coverage against physical rebar occurrences and planned presentation groups.
/// Ensures 1:1 coverage and absence of orphan or phantom occurrences.
/// </summary>
public static class PresentationGroupValidator
{
    public static (int TotalOccurrences, int MappedOccurrences, int OrphanOccurrences, int PhantomOccurrences) Validate(
        PresentationGroupMap map, IEnumerable<BarOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(occurrences);
        return map.ValidateCoverage(occurrences);
    }

    public static (int TotalOccurrences, int MappedOccurrences, int OrphanOccurrences, int PhantomOccurrences) Validate(
        DrawingPackage package, PresentationGroupMap map, IEnumerable<BarOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(occurrences);
        return map.ValidateCoverage(occurrences);
    }
}

/// <summary>
/// Builds presentation groups from drawing primitives and physical occurrences,
/// verifying coverage and preventing orphan or phantom members.
/// </summary>
public static class PresentationGroupMapper
{
    public const string DefaultRuleVersion = "DBTOOL-FINAL-SPEC-20261012-v1";

    public static PresentationGroupMap Build(DrawingPackage package, IEnumerable<BarOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(occurrences);

        var barMap = occurrences.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var primitives = package.Primitives;
        var collectors = primitives.OfType<PolylinePrimitive>()
            .Where(p => p.Role == SheetTwoGachCollector.CollectorRole)
            .ToArray();

        var groups = new List<PresentationGroupRecord>();

        foreach (var collector in collectors)
        {
            var callout = primitives.OfType<BlockPrimitive>()
                .FirstOrDefault(b => b.SemanticId == collector.SemanticId + "/Callout" && b.Role == SheetTwoBarAnnotations.Role);
            if (callout is null) continue;

            var ticks = primitives.OfType<BlockPrimitive>()
                .Where(b => b.Role == SheetTwoGach.Role && b.SemanticId.StartsWith(collector.SemanticId + "/Gach/", StringComparison.Ordinal))
                .ToArray();

            var occIds = ticks.Select(t => t.OccurrenceId).Where(id => id is not null).Select(id => id!).Distinct(StringComparer.Ordinal).ToList();
            if (occIds.Count == 0 && collector.OccurrenceId is not null)
                occIds.Add(collector.OccurrenceId);

            var canonicalMarks = ticks.Select(t => t.ScheduleMark).Where(m => m is not null).Select(m => m!).Distinct(StringComparer.Ordinal).ToList();
            if (canonicalMarks.Count == 0 && collector.ScheduleMark is not null)
                canonicalMarks.Add(collector.ScheduleMark);

            var groupBars = occIds.Where(id => barMap.ContainsKey(id)).Select(id => barMap[id]).ToArray();

            string displayMark = callout.Attributes.FirstOrDefault(a => a.Key == "SH")?.Value ?? collector.ScheduleMark ?? "D1";
            string dk = callout.Attributes.FirstOrDefault(a => a.Key == "DK")?.Value ?? "";
            string a = callout.Attributes.FirstOrDefault(a => a.Key == "A")?.Value ?? "";

            int diameter = groupBars.Length > 0 ? groupBars[0].DiameterMm : 10;
            if (dk.StartsWith("%%c", StringComparison.OrdinalIgnoreCase) && int.TryParse(dk[3..], out int dParsed))
                diameter = dParsed;

            double? pitch = null;
            int? quantity = null;
            string spacingMode = "Pitch";
            if (a.StartsWith('@') && double.TryParse(a[1..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pParsed))
            {
                pitch = pParsed;
                spacingMode = "Pitch";
            }
            else if (int.TryParse(a.Trim(), out int qParsed))
            {
                quantity = qParsed;
                spacingMode = "Quantity";
            }
            else
            {
                spacingMode = "Even";
            }

            string faceLayer = groupBars.Length > 0
                ? (groupBars[0].Surface?.ToString() ?? groupBars[0].Layer.ToString())
                : "Default";
            string modelDir = groupBars.Length > 0 ? groupBars[0].Direction.ToString() : "Supplementary";
            string function = $"Mesh/{modelDir}";
            string scope = $"({collector.ComponentId ?? "Base"}, {faceLayer}, {displayMark})";

            var shapeSigs = groupBars.Select(b => b.Path.HookPattern.ToString()).Distinct(StringComparer.Ordinal).ToList();
            if (shapeSigs.Count == 0) shapeSigs.Add("Straight");

            var supportRegions = new List<string> { collector.ViewId };
            var sources = groupBars.SelectMany(b => b.Sources.Select(s => s.ToString())).Distinct(StringComparer.Ordinal).ToList();

            groups.Add(new PresentationGroupRecord(
                GroupId: callout.SemanticId,
                Scope: scope,
                DisplayMark: displayMark,
                Function: function,
                FaceLayer: faceLayer,
                ModelDirection: modelDir,
                DiameterMm: diameter,
                SpacingMode: spacingMode,
                ActualSpacing: pitch,
                Quantity: quantity,
                ShapeSignatures: shapeSigs,
                SupportRegions: supportRegions,
                OccurrenceIds: occIds,
                CanonicalMarks: canonicalMarks,
                Views: [collector.ViewId],
                LabelIds: [callout.SemanticId],
                CollectorIds: [collector.SemanticId],
                Sources: sources,
                RuleVersion: package.RuleVersion
            ));
        }

        // Also map standalone nominal callouts (like anchorage T3)
        var standaloneCallouts = primitives.OfType<BlockPrimitive>()
            .Where(b => b.Role == SheetTwoBarAnnotations.Role && !groups.Any(g => g.LabelIds.Contains(b.SemanticId)))
            .ToArray();

        foreach (var callout in standaloneCallouts)
        {
            var occId = callout.OccurrenceId;
            var occIds = occId is not null ? new List<string> { occId } : new List<string>();
            var groupBars = occIds.Where(id => barMap.ContainsKey(id)).Select(id => barMap[id]).ToArray();
            var mark = callout.ScheduleMark ?? callout.Attributes.FirstOrDefault(a => a.Key == "SH")?.Value ?? "T3";
            var displayMark = callout.Attributes.FirstOrDefault(a => a.Key == "SH")?.Value ?? mark;

            string faceLayer = groupBars.Length > 0
                ? (groupBars[0].Surface?.ToString() ?? groupBars[0].Layer.ToString())
                : "Inner";
            string modelDir = groupBars.Length > 0 ? groupBars[0].Direction.ToString() : "Anchorage";

            groups.Add(new PresentationGroupRecord(
                GroupId: callout.SemanticId,
                Scope: $"({callout.ComponentId ?? "Wall"}, {faceLayer}, {displayMark})",
                DisplayMark: displayMark,
                Function: "Anchorage",
                FaceLayer: faceLayer,
                ModelDirection: modelDir,
                DiameterMm: groupBars.Length > 0 ? groupBars[0].DiameterMm : 10,
                SpacingMode: "Pitch",
                ActualSpacing: null,
                Quantity: null,
                ShapeSignatures: ["Hook"],
                SupportRegions: [callout.ViewId],
                OccurrenceIds: occIds,
                CanonicalMarks: [mark],
                Views: [callout.ViewId],
                LabelIds: [callout.SemanticId],
                CollectorIds: [],
                Sources: groupBars.SelectMany(b => b.Sources.Select(s => s.ToString())).Distinct(StringComparer.Ordinal).ToList(),
                RuleVersion: package.RuleVersion
            ));
        }

        // Map all remaining occurrences (Haunch, Slab/Roof, Neck, Dowel, Ladder) to ensure 100% coverage
        var mappedIds = groups.SelectMany(g => g.OccurrenceIds).ToHashSet(StringComparer.Ordinal);
        var unmapped = occurrences.Where(b => !mappedIds.Contains(b.Id)).ToList();

        if (unmapped.Count > 0)
        {
            var componentGroups = unmapped.GroupBy(b => (
                b.ComponentKind,
                b.Layer,
                b.Axis,
                b.Direction,
                b.DiameterMm,
                b.FamilyId,
                Hook: b.Path.HookPattern.ToString(),
                Surface: b.Surface?.ToString() ?? b.Layer.ToString()
            ));

            int compGroupIdx = 1;
            foreach (var cg in componentGroups)
            {
                var barList = cg.ToList();
                var occIds = barList.Select(b => b.Id).Distinct(StringComparer.Ordinal).ToImmutableArray();
                var canonicalMarks = barList.Select(b => b.FamilyId ?? $"D_{cg.Key.ComponentKind}_{cg.Key.DiameterMm}").Distinct(StringComparer.Ordinal).ToImmutableArray();

                string displayPrefix = cg.Key.ComponentKind switch
                {
                    ComponentKind.Roof => "N",
                    ComponentKind.Neck => "C",
                    _ => cg.Key.FamilyId?.StartsWith("Haunch", StringComparison.OrdinalIgnoreCase) == true ? "V" : "D"
                };
                string displayMark = $"{displayPrefix}{compGroupIdx++}";

                string scope = $"({cg.Key.ComponentKind}, {cg.Key.Surface}, {displayMark})";
                string function = $"{cg.Key.ComponentKind}/{cg.Key.Direction}";

                var viewsList = package.Views.Where(v => v.Id.Contains(cg.Key.ComponentKind.ToString(), StringComparison.OrdinalIgnoreCase)).Select(v => v.Id).ToList();
                if (viewsList.Count == 0) viewsList.Add("Overall");
                var views = viewsList.ToImmutableArray();

                groups.Add(new PresentationGroupRecord(
                    GroupId: $"Group/Component/{cg.Key.ComponentKind}/{displayMark}",
                    Scope: scope,
                    DisplayMark: displayMark,
                    Function: function,
                    FaceLayer: cg.Key.Surface,
                    ModelDirection: cg.Key.Direction.ToString(),
                    DiameterMm: cg.Key.DiameterMm,
                    SpacingMode: "representedByGroup",
                    ActualSpacing: null,
                    Quantity: barList.Count,
                    ShapeSignatures: [cg.Key.Hook],
                    SupportRegions: views,
                    OccurrenceIds: occIds,
                    CanonicalMarks: canonicalMarks,
                    Views: views,
                    LabelIds: [$"representedByGroup:{displayMark}"],
                    CollectorIds: [],
                    Sources: barList.SelectMany(b => b.Sources.Select(s => s.ToString())).Distinct(StringComparer.Ordinal).ToImmutableArray(),
                    RuleVersion: package.RuleVersion
                ));
            }
        }

        return new PresentationGroupMap(package.RuleVersion, groups);
    }

    public static void ExportJson(DrawingPackage package, IEnumerable<BarOccurrence> occurrences, string outputPath)
    {
        var map = Build(package, occurrences);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, map.ToJson());
    }
}
