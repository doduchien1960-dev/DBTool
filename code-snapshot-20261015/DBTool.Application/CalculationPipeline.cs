using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;

namespace DBTool.Application;

public sealed class CalculationPipeline(IGeometryStage geometry,IBaseRebarStage rebar,ISteelScheduleStage schedule,IDrawingStage drawing,
    IConcreteQuantityStage? concrete = null, IFormworkQuantityStage? formwork = null)
{
    public static CapabilitySet HeadlessS1 {get;}=new([
        "Profile.Mono","Geometry.Pad","Geometry.Base","Geometry.Wall","Geometry.Roof",
        "Geometry.RoofOpening.Centered700","Geometry.RoundOpening.T1","Steel.BaseBottom","Steel.BaseTop"]);

    public OperationResult<CalculationPackage> Calculate(ValidatedProjectInput input,CalculationPolicy policy,
        CalculationRequest request,PresentationRequest presentation,CancellationToken token)
    {
        if (policy.PolicyId == "S2Rebar-1")
            return CalculateS3Rebar(input, policy, request, presentation, token);
        if (policy.PolicyId == "S2Geometry-1")
            return CalculateS2Quantity(input, policy, request, token);

        var diagnostics=input.Diagnostics.ToList();
        if(input.SelectedManholes.Length!=1 || input.SourceWorkbookHash is null)
            return UseCaseErrors.Fail<CalculationPackage>("PIPELINE_INPUT_INVALID","One validated manhole and source hash required.");
        var spec=input.SelectedManholes[0];
        token.ThrowIfCancellationRequested();
        var capability=new FeatureSupportValidator().Validate(spec,request.Scope,HeadlessS1,policy);
        if(!Collect(capability))return Failure();
        if(!presentation.IncludeDrawing)return UseCaseErrors.Fail<CalculationPackage>("DRAWING_REQUIRED","S1 requires a drawing package.");
        token.ThrowIfCancellationRequested();
        var g=geometry.Build(spec,policy); if(!Collect(g))return Failure();
        token.ThrowIfCancellationRequested();
        var r=rebar.Plan(spec,g.Value!,policy); if(!Collect(r))return Failure();
        token.ThrowIfCancellationRequested();
        var s=schedule.Calculate(r.Value!,policy); if(!Collect(s))return Failure();
        token.ThrowIfCancellationRequested();
        var groupRegistry = PresentationGroupMap.PlanPreDrawing(r.Value!.Occurrences, r.Value.Angles, s.Value, null);
        var drawingInputs = new DrawingInputs(g.Value!, r.Value!, s.Value!, null, DrawingQuantitySnapshot.NotCalculated,
            spec.Input.MainRebar?.WallOuter, spec.Input.MainRebar?.WallInner,
            spec.Input.BaseTopMesh, spec.Input.BaseBottomMesh,
            spec.Input.MainRebar?.RoofTop, spec.Input.MainRebar?.RoofBottom, spec.Input.MainRebar?.RoofCoverMm,
            spec.Input.Id, spec.NeckSpec ?? spec.Input.NeckSpec,
            spec.Input.MainRebar?.NeckOuter, spec.Input.Profile, spec.Input.MainRebar?.Accessories?.DowelHoleDiameterMm,
            groupRegistry, groupRegistry.RuleVersion,
            spec.Input.SourceRefs.ToImmutableDictionary(k => k.Key, v => v.Value.ToString()));
        var d=drawing.Build(spec,drawingInputs,presentation,new(input.InputHash,input.RuleVersion,request.Scope));
        if(!Collect(d))return Failure();
        var cov = groupRegistry.ValidateCoverage(r.Value.Occurrences, r.Value.Angles);
        if (cov.OrphanOccurrences > 0 || cov.PhantomOccurrences > 0)
            return UseCaseErrors.Fail<CalculationPackage>("PRESENTATION_GROUP_COVERAGE_INVALID", $"Presentation groups coverage incomplete: orphans={cov.OrphanOccurrences}, phantoms={cov.PhantomOccurrences}");
        token.ThrowIfCancellationRequested();
        var package=new CalculationPackage(input.InputHash,input.SourceWorkbookHash,"headless-s1/v1","contract-v1.1",
            input.RuleVersion,policy.PolicyId,request.Scope,g.Value!,r.Value!.Occurrences,s.Value!,
            CalculationStatus.NotCalculated,null,CalculationStatus.NotCalculated,null,d.Value!,diagnostics.Distinct());
        return new(true,package,package.Diagnostics);

        bool Collect<T>(OperationResult<T> result) where T:class {diagnostics.AddRange(result.Diagnostics);return result.Success;}
        OperationResult<CalculationPackage> Failure()=>new(false,null,diagnostics.Distinct());
    }

    public OperationResult<QuantityBatchResult> CalculateQuantityBatch(ValidatedProjectInput input,
        CalculationPolicy policy, IEnumerable<CalculationScope> scopes, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(policy);
        var requested = scopes.ToArray();
        if (requested.Length == 0 || requested.Distinct().Count() != requested.Length ||
            requested.Any(scope => scope is not (CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly)))
            return UseCaseErrors.Fail<QuantityBatchResult>("S2_QUANTITY_BATCH_INVALID", "Only independent ConcreteOnly and FormworkOnly packages are allowed.");

        var packages = new List<CalculationPackage>();
        foreach (var scope in requested.OrderBy(x => x))
        {
            var request = new CalculationRequest(input.SelectedManholes[0].Input.Id,
                [input.SelectedManholes[0].Input.Id], scope, policy.PolicyId, true);
            var result = Calculate(input, policy, request,
                new PresentationRequest(20, input.SelectedManholes[0].Input.ElevationMode,
                    input.SelectedManholes[0].Input.BaseElevationM, "S2", false), token);
            if (!result.Success) return new(false, null, result.Diagnostics);
            packages.Add(result.Value!);
        }
        return new(true, new QuantityBatchResult(packages), []);
    }

    private OperationResult<CalculationPackage> CalculateS2Quantity(ValidatedProjectInput input,
        CalculationPolicy policy, CalculationRequest request, CancellationToken token)
    {
        var diagnostics = input.Diagnostics.ToList();
        if (input.SelectedManholes.Length != 1 || input.SourceWorkbookHash is null)
            return UseCaseErrors.Fail<CalculationPackage>("PIPELINE_INPUT_INVALID", "One validated manhole and source hash required.");
        var spec = input.SelectedManholes[0];
        var capability = new FeatureSupportValidator().ValidateS2Quantity(spec, request.Scope, policy);
        if (!Collect(capability)) return Failure();
        token.ThrowIfCancellationRequested();
        var g = geometry.Build(spec, policy);
        if (!Collect(g)) return Failure();
        token.ThrowIfCancellationRequested();

        ConcreteQuantityResult? concreteResult = null;
        FormworkResult? formworkResult = null;
        if (request.Scope == CalculationScope.ConcreteOnly)
        {
            if (concrete is null) return UseCaseErrors.Fail<CalculationPackage>("S2_CONCRETE_STAGE_MISSING", "Concrete quantity stage is not registered.");
            var result = concrete.Calculate(spec, g.Value!, new QuantityPolicy("S2-QUANTITY-1", policy.Version, tolerancePolicy: policy.TolerancePolicy));
            if (!Collect(result)) return Failure();
            concreteResult = result.Value;
        }
        else if (request.Scope == CalculationScope.FormworkOnly)
        {
            if (formwork is null) return UseCaseErrors.Fail<CalculationPackage>("S2_FORMWORK_STAGE_MISSING", "Formwork quantity stage is not registered.");
            var result = formwork.Calculate(spec, g.Value!, new FormworkPolicy("S2-FORMWORK-1", tolerancePolicy: policy.TolerancePolicy));
            if (!Collect(result)) return Failure();
            formworkResult = result.Value;
        }

        token.ThrowIfCancellationRequested();
        var package = new CalculationPackage(input.InputHash, input.SourceWorkbookHash, "s2-quantity/v1", "contract-v2-quantity",
            input.RuleVersion, policy.PolicyId, request.Scope, g.Value!, [], null,
            concreteResult is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated,
            concreteResult?.Lines,
            formworkResult is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated,
            formworkResult?.Lines, null, diagnostics.Distinct());
        return new(true, package, package.Diagnostics);

        bool Collect<T>(OperationResult<T> result) where T : class
        {
            diagnostics.AddRange(result.Diagnostics);
            return result.Success;
        }
        OperationResult<CalculationPackage> Failure() => new(false, null, diagnostics.Distinct());
    }

    private OperationResult<CalculationPackage> CalculateS3Rebar(ValidatedProjectInput input,
        CalculationPolicy policy, CalculationRequest request, PresentationRequest presentation, CancellationToken token)
    {
        var diagnostics = input.Diagnostics.ToList();
        if (input.SelectedManholes.Length != 1 || input.SourceWorkbookHash is null)
            return UseCaseErrors.Fail<CalculationPackage>("PIPELINE_INPUT_INVALID", "One validated manhole and source hash required.");
        var spec = input.SelectedManholes[0];
        var capability = new FeatureSupportValidator().ValidateS2Rebar(spec, request.Scope, policy);
        if (!Collect(capability)) return Failure();
        token.ThrowIfCancellationRequested();
        var g = geometry.Build(spec, policy); if (!Collect(g)) return Failure();
        token.ThrowIfCancellationRequested();
        var r = rebar.Plan(spec, g.Value!, policy); if (!Collect(r)) return Failure();
        token.ThrowIfCancellationRequested();
        if(r.Value!.Status==LayoutStatus.Partial)
            diagnostics.Add(new("REBAR_COMPOSITION_PARTIAL",DiagnosticSeverity.Review,DiagnosticScope.Manhole,spec.Input.Id,null,
                spec.Sources.FirstOrDefault(),"resolved supplemental and fabrication rules",r.Value.Status.ToString(),
                "All registered consumers returned their occurrences; unresolved supplemental or fabrication rules remain explicitly Partial."));
        var s = schedule.Calculate(r.Value!, policy); if (!Collect(s)) return Failure();

        var angleResult=AngleScheduleCalculator.Calculate(r.Value!.Angles);
        if(!Collect(angleResult)) return Failure();
        ConcreteQuantityResult? concreteResult = null;
        FormworkResult? formworkResult = null;
        if (request.Scope == CalculationScope.Full)
        {
            if (concrete is null) return UseCaseErrors.Fail<CalculationPackage>("S3_CONCRETE_STAGE_MISSING", "Concrete quantity stage is not registered.");
            if (formwork is null) return UseCaseErrors.Fail<CalculationPackage>("S3_FORMWORK_STAGE_MISSING", "Formwork quantity stage is not registered.");
            var concreteStageResult = concrete.Calculate(spec, g.Value!, new QuantityPolicy("S3-QUANTITY-1", policy.Version, tolerancePolicy: policy.TolerancePolicy));
            if (!Collect(concreteStageResult)) return Failure(); concreteResult = concreteStageResult.Value;
            var formworkStageResult = formwork.Calculate(spec, g.Value!, new FormworkPolicy("S3-FORMWORK-1", tolerancePolicy: policy.TolerancePolicy));
            if (!Collect(formworkStageResult)) return Failure(); formworkResult = formworkStageResult.Value;
        }

        DrawingPackage? drawingPackage = null;
        var angleSchedule=r.Value.Angles.IsEmpty?null:angleResult.Value;
        if (presentation.IncludeDrawing)
        {
            token.ThrowIfCancellationRequested();
            var quantities=new DrawingQuantitySnapshot(
                concreteResult is null?CalculationStatus.NotCalculated:CalculationStatus.Calculated,concreteResult?.Lines,
                formworkResult is null?CalculationStatus.NotCalculated:CalculationStatus.Calculated,formworkResult?.Lines);
            var groupRegistry = PresentationGroupMap.PlanPreDrawing(r.Value.Occurrences, r.Value.Angles, s.Value, angleSchedule);
            var drawingInputs = new DrawingInputs(g.Value!, r.Value, s.Value!, angleSchedule, quantities,
                spec.Input.MainRebar?.WallOuter, spec.Input.MainRebar?.WallInner,
                spec.Input.BaseTopMesh, spec.Input.BaseBottomMesh,
                spec.Input.MainRebar?.RoofTop, spec.Input.MainRebar?.RoofBottom, spec.Input.MainRebar?.RoofCoverMm,
                spec.Input.Id, spec.NeckSpec ?? spec.Input.NeckSpec,
                spec.Input.MainRebar?.NeckOuter, spec.Input.Profile, spec.Input.MainRebar?.Accessories?.DowelHoleDiameterMm,
                groupRegistry, groupRegistry.RuleVersion,
                spec.Input.SourceRefs.ToImmutableDictionary(k => k.Key, v => v.Value.ToString()));
            var d = drawing.Build(spec, drawingInputs, presentation,
                new PackageIdentity(input.InputHash, input.RuleVersion, request.Scope));
            if (!Collect(d)) return Failure();
            var cov = groupRegistry.ValidateCoverage(r.Value.Occurrences, r.Value.Angles);
            if (cov.OrphanOccurrences > 0 || cov.PhantomOccurrences > 0)
                return UseCaseErrors.Fail<CalculationPackage>("PRESENTATION_GROUP_COVERAGE_INVALID", $"Presentation groups coverage incomplete: orphans={cov.OrphanOccurrences}, phantoms={cov.PhantomOccurrences}");
            drawingPackage = d.Value;
        }

        var package = new CalculationPackage(input.InputHash, input.SourceWorkbookHash, "s3-rebar/v1", "contract-v3-rebar",
            input.RuleVersion, policy.PolicyId, request.Scope, g.Value!, r.Value!.Occurrences, s.Value!,
            concreteResult is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated, concreteResult?.Lines,
            formworkResult is null ? CalculationStatus.NotCalculated : CalculationStatus.Calculated, formworkResult?.Lines,
            drawingPackage, diagnostics.Distinct(),angleSchedule);
        return new(true, package, package.Diagnostics);

        bool Collect<T>(OperationResult<T> result) where T : class { diagnostics.AddRange(result.Diagnostics); return result.Success; }
        OperationResult<CalculationPackage> Failure() => new(false, null, diagnostics.Distinct());
    }
}

internal static class UseCaseErrors
{
    internal static OperationResult<T> Fail<T>(string code,string message) where T:class =>new(false,null,
        [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
