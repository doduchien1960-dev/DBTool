using DBTool.Domain;

namespace DBTool.Application;

/// <summary>Checks requested S1 behavior against explicit consumers without changing validated input.</summary>
public sealed class FeatureSupportValidator
{
    /// <summary>Registers only the typed S3 consumers present in this vertical slice.</summary>
    public OperationResult<FeatureSupportReport> ValidateS2Rebar(ValidatedManholeSpec spec,
        CalculationScope scope, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(policy);
        if (policy.PolicyId != "S2Rebar-1" || policy.Version != spec.RuleVersion ||
            scope is not (CalculationScope.FullRebar or CalculationScope.Full))
            return FailGlobal("S3_REBAR_SCOPE_UNSUPPORTED", "S3 requires S2Rebar-1 and FullRebar or Full scope.");
        var results = new List<CapabilityResult>
        {
            new("Geometry.S2", true, CapabilityStatus.Supported, null),
            new("Steel.Base", true, CapabilityStatus.Supported, null)
        };
        var diagnostics = new List<Diagnostic>();
        foreach (var feature in spec.Input.FeatureRequests.OrderBy(x => x.FeatureId, StringComparer.Ordinal))
        {
            if (!feature.Requested) continue;
            var main=spec.Input.MainRebar;
            bool supported=feature.FeatureId switch {
                "AngleFrame"=>main?.AngleFrame is not null,
                "WallRebar"=>main is not null && (main.WallOuter is not null || main.WallInner is not null),
                "RoofRebar"=>main is not null && (main.RoofTop is not null || main.RoofBottom is not null),
                "NeckRebar"=>spec.Input.NeckHeight==0 || main is not null && (main.NeckStructure==StructureKind.Other || main.NeckOuter is not null || main.NeckInner is not null),
                "ProductCoverOrPanel"=>spec.Input.PurchasedCoverSpec is not null || spec.Input.CoverPanelSpec is not null,
                "Ladder"=>main?.Ladder is not null,
                "Dowels"=>spec.Input.Profile == Profile.GaThamNapRoi && main?.Accessories?.Dowel is not null &&
                    main.WallSupportCoverMm.HasValue,
                "PipeReinforcement"=>VhgOpeningCompositionPlanner.CanConsume(main?.Accessories?.Pipe),
                "RoofOpeningReinforcement"=>VhgOpeningCompositionPlanner.CanConsume(main?.Accessories?.Roof),
                _=>false };
            if(supported) { results.Add(new(feature.FeatureId,true,CapabilityStatus.Supported,null));continue; }
            var diagnostic = new Diagnostic("FEATURE_UNSUPPORTED", DiagnosticSeverity.Block, DiagnosticScope.Manhole,
                spec.Input.Id, null, feature.Parameters.Values.FirstOrDefault()?.Source, "typed S3 consumer", feature.FeatureId,
                $"Feature {feature.FeatureId} has no complete S3 consumer in this vertical slice.");
            results.Add(new CapabilityResult(feature.FeatureId, true, CapabilityStatus.Unsupported, diagnostic));
            diagnostics.Add(diagnostic);
        }
        return diagnostics.Count == 0
            ? new(true, new FeatureSupportReport(scope, results), [])
            : new(false, null, diagnostics);
    }

    /// <summary>Explicit S2 quantity capability gate; it does not reuse or weaken the S1 validator.</summary>
    public OperationResult<FeatureSupportReport> ValidateS2Quantity(
        ValidatedManholeSpec spec, CalculationScope scope, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);
        bool validScope = scope is CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly or CalculationScope.GeometryOnly;
        if (policy.PolicyId != "S2Geometry-1" || policy.EvidenceStatus != EvidenceStatus.ExperimentalSoftwareFixture ||
            spec.RuleVersion != policy.Version || !validScope)
            return FailGlobal("S2_QUANTITY_SCOPE_UNSUPPORTED", "S2 quantity requires the experimental S2 policy and an explicit quantity scope.");

        var results = new List<CapabilityResult>
        {
            new("Geometry.S2", true, CapabilityStatus.Supported, null)
        };
        if (scope == CalculationScope.ConcreteOnly)
            results.Add(new CapabilityResult("Concrete", true, CapabilityStatus.Supported, null));
        if (scope == CalculationScope.FormworkOnly)
            results.Add(new CapabilityResult("Formwork", true, CapabilityStatus.Supported, null));
        return new(true, new FeatureSupportReport(scope, results), []);
    }

    public OperationResult<FeatureSupportReport> Validate(
        ValidatedManholeSpec spec, CalculationScope scope, CapabilitySet capabilities, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));

        if (policy.PolicyId != "S1-Analytic-1" || policy.EvidenceStatus != EvidenceStatus.ExperimentalSoftwareFixture ||
            spec.RuleVersion != policy.Version)
            return FailGlobal("POLICY_MISMATCH", "Approved S1 policy and validated rule version are required.");
        if (scope != CalculationScope.BaseRebarOnly)
            return FailGlobal("S1_SCOPE_UNSUPPORTED", "S1 calculates only BaseRebarOnly.");

        var input = spec.Input;
        var results = new List<CapabilityResult>();
        var diagnostics = new List<Diagnostic>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        SourceRef? Source(params string[] keys)
        {
            foreach (var key in keys)
                if (input.SourceRefs.TryGetValue(key, out var value)) return value;
            return null;
        }

        void Add(string id, bool requested, bool forceUnsupported = false, bool needsRule = false, SourceRef? source = null)
        {
            if (!seen.Add(id)) throw new ArgumentException($"Duplicate feature ID: {id}");
            CapabilityStatus status = !requested ? CapabilityStatus.NotRequested : needsRule ? CapabilityStatus.NeedsRule :
                forceUnsupported || !capabilities.HasConsumer(id) ? CapabilityStatus.Unsupported : CapabilityStatus.Supported;
            Diagnostic? diagnostic = status switch
            {
                CapabilityStatus.NeedsRule => new Diagnostic("FEATURE_NEEDS_RULE", DiagnosticSeverity.Block,
                    DiagnosticScope.Manhole, input.Id, null, source, "Required fabrication rule", id,
                    $"Feature {id} needs a defined rule before calculation."),
                CapabilityStatus.Unsupported => new Diagnostic("FEATURE_UNSUPPORTED", DiagnosticSeverity.Block,
                    DiagnosticScope.Manhole, input.Id, null, source, "Consumer in S1", id,
                    $"Feature {id} has no supported S1 consumer."),
                _ => null
            };
            results.Add(new CapabilityResult(id, requested, status, diagnostic));
            if (diagnostic is not null) diagnostics.Add(diagnostic);
        }

        Add(input.Profile == Profile.GaThamLienKhoi ? "Profile.Mono" : $"Profile.{input.Profile}",
            true, forceUnsupported: input.Profile != Profile.GaThamLienKhoi, source: Source("Row.Name"));
        Add("Geometry.Pad", true, source: Source("Config.PadThickness"));
        Add("Geometry.Base", true, source: Source("Row.BaseThickness"));
        Add("Geometry.Wall", true, source: Source("Row.WallThickness"));
        Add("Geometry.Roof", true, source: Source("Row.RoofThickness"));
        bool centered700 = Math.Abs(input.RoofOpening.MinX + 350) <= policy.TolerancePolicy.GeometryMm &&
            Math.Abs(input.RoofOpening.MaxX - 350) <= policy.TolerancePolicy.GeometryMm &&
            Math.Abs(input.RoofOpening.MinY + 350) <= policy.TolerancePolicy.GeometryMm &&
            Math.Abs(input.RoofOpening.MaxY - 350) <= policy.TolerancePolicy.GeometryMm;
        Add(centered700 ? "Geometry.RoofOpening.Centered700" : "Geometry.RoofOpening.NonStandard", true,
            forceUnsupported: !centered700, source: Source("Config.NeckSize"));
        Add("Geometry.Neck", input.NeckHeight > 0, forceUnsupported: true, source: Source("Row.NeckHeight"));
        Add("Geometry.BaseHaunch", input.BaseHaunch > 0, forceUnsupported: true, source: Source("Config.BaseHaunch"));
        Add("Geometry.TopHaunch", input.TopHaunch > 0, forceUnsupported: true, source: Source("Config.TopHaunch"));
        Add("Geometry.BeddingMortar", input.BeddingMortarMm > 0, forceUnsupported: true, source: Source("Config.BeddingMortar"));
        Add("Geometry.CatchBasin", input.CatchBasinSpec is not null, forceUnsupported: true,
            source: input.CatchBasinSpec?.SourceRefs.Values.FirstOrDefault());

        if (input.PipeConnections.Length != 1)
            Add(input.PipeConnections.IsEmpty ? "Geometry.Pipe.Count" : "Geometry.Pipe.Multiple", true, forceUnsupported: true,
                source: input.PipeConnections.FirstOrDefault()?.SourceRefs.GetValueOrDefault("Code"));
        else
        {
            var pipe = input.PipeConnections[0];
            var catalog = spec.Catalogs.Pipes[pipe.CatalogCode];
            SourceRef? pipeSource = pipe.SourceRefs.TryGetValue("Code", out var codeSource) ? codeSource : null;
            if (catalog.Kind != PipeKind.Round)
                Add(catalog.Kind == PipeKind.Box ? "Geometry.Pipe.Box" : "Geometry.Pipe.DoubleBox", true,
                    forceUnsupported: true, source: pipeSource);
            else
            {
                Add("Geometry.RoundOpening.T1", pipe.WallId == WallId.T1,
                    source: pipeSource);
                if (pipe.WallId != WallId.T1)
                    Add("Geometry.Pipe.OtherWall", true, forceUnsupported: true, source: pipeSource);
                if (Math.Abs(pipe.OffsetMm) > policy.TolerancePolicy.GeometryMm)
                    Add("Geometry.Pipe.Offset", true, forceUnsupported: true,
                        source: Source($"Config.PipeOffset{pipe.WallId}"));
                double radius = catalog.InnerDiameterMm!.Value / 2 + catalog.WallThicknessMm + pipe.MortarMm;
                bool contained = pipe.InvertMm - catalog.WallThicknessMm - pipe.MortarMm >= -policy.TolerancePolicy.GeometryMm &&
                    pipe.InvertMm + catalog.InnerDiameterMm.Value + catalog.WallThicknessMm + pipe.MortarMm <= input.H + policy.TolerancePolicy.GeometryMm &&
                    Math.Abs(pipe.OffsetMm) + radius <= input.L / 2 + policy.TolerancePolicy.GeometryMm;
                if (!contained)
                    Add("Geometry.Pipe.OutsideWall", true, forceUnsupported: true, source: pipeSource);
            }
        }

        Add("Steel.BaseBottom", true, source: Source("Row.BaseBottomMesh"));
        Add("Steel.BaseTop", true, source: Source("Row.BaseTopMesh"));
        if (input.DistributionMode != DistributionMode.Equalized)
            Add($"Steel.Distribution.{input.DistributionMode}", true,
                forceUnsupported: true, source: Source("Config.Distribution"));
        Add("Steel.BaseHook.Bottom", input.HookBaseBottom != HookPattern.Hook0M,
            forceUnsupported: input.HookBaseBottom == HookPattern.HookStartOnly,
            needsRule: policy.BendPolicy is null, source: Source("Config.HookPattern.C"));
        Add("Steel.BaseHook.Top", input.HookBaseTop != HookPattern.Hook0M,
            forceUnsupported: input.HookBaseTop == HookPattern.HookStartOnly,
            needsRule: policy.BendPolicy is null, source: Source("Config.HookPattern.D"));

        // A source field can be active while its component is outside the requested calculation scope.
        Add("WallRebar", false);
        Add("RoofRebar", false);
        Add("Concrete", false);
        Add("Formwork", false);

        foreach (var feature in input.FeatureRequests.OrderBy(x => x.FeatureId, StringComparer.Ordinal))
        {
            if (feature.FeatureId is "WallRebar" or "RoofRebar") continue;
            bool hasNeck = input.NeckHeight > 0;
            bool requested = feature.Requested && (feature.FeatureId is not ("NeckRebar" or "NeckAnchor") || hasNeck);
            string id = feature.FeatureId switch
            {
                "Ties" => "Accessories.Ties",
                "Dowels" => "Accessories.Dowels",
                _ => feature.FeatureId
            };
            Add(id, requested, forceUnsupported: true, source: feature.Parameters.Values.FirstOrDefault()?.Source);
        }

        // Unknown nonblank text or true switches may activate construction. Fail closed until
        // classified; passive numeric settings and the known presentation label are not requests.
        var classifiedKeys = input.FeatureRequests.SelectMany(x => x.Parameters.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, cell) in input.UnsupportedRawFields.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            string? label = cell.Kind == RawCellKind.Text ? cell.Text?.Trim() :
                cell.Kind == RawCellKind.Formula ? cell.CachedText?.Trim() : null;
            bool enabled = cell.Kind == RawCellKind.Boolean && cell.Boolean == true ||
                !string.IsNullOrWhiteSpace(label) && label is not ("0" or "0D10") &&
                !label.Equals("Không", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("No", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("False", StringComparison.OrdinalIgnoreCase);
            if (classifiedKeys.Contains(key) || key is "Config.Elevation.D" or "Config.LiftingSize" || !enabled)
                continue;
            if (input.NeckHeight == 0 && key.StartsWith("Config.Neck", StringComparison.Ordinal))
                continue;
            Add("Raw." + key, true, forceUnsupported: true, source: cell.Source);
        }

        return diagnostics.Count == 0
            ? new(true, new FeatureSupportReport(scope, results), Array.Empty<Diagnostic>())
            : new(false, null, diagnostics);
    }

    private static OperationResult<FeatureSupportReport> FailGlobal(string code, string message) =>
        new(false, null, new[] { new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message) });
}
