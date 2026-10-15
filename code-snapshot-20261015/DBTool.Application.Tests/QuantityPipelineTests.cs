using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class QuantityPipelineTests
{
    [Fact]
    public void ConcreteOnlyUsesExplicitS2DispatchWithoutSteelOrDrawing()
    {
        var stages = Stages();
        var result = stages.Pipeline.Calculate(stages.Input, stages.Policy,
            new CalculationRequest("S1", [stages.Spec.Input.Id], CalculationScope.ConcreteOnly, "S2Geometry-1", true),
            new PresentationRequest(20, ElevationMode.Symbolic, null, "S2", false), CancellationToken.None);

        Assert.True(result.Success, Details(result.Diagnostics));
        var package = result.Value!;
        Assert.Equal(CalculationStatus.Calculated, package.ConcreteStatus);
        Assert.Equal(CalculationStatus.NotCalculated, package.FormworkStatus);
        Assert.Null(package.SteelSchedule);
        Assert.Empty(package.Occurrences);
        Assert.Null(package.DrawingPackage);
        Assert.Equal(CalculationScope.ConcreteOnly, package.Scope);
    }

    [Fact]
    public void FormworkOnlyIsIndependentFromConcreteAndSteel()
    {
        var stages = Stages();
        var result = stages.Pipeline.Calculate(stages.Input, stages.Policy,
            new CalculationRequest("S1", [stages.Spec.Input.Id], CalculationScope.FormworkOnly, "S2Geometry-1", true),
            new PresentationRequest(20, ElevationMode.Symbolic, null, "S2", false), CancellationToken.None);

        Assert.True(result.Success, Details(result.Diagnostics));
        Assert.Equal(CalculationStatus.NotCalculated, result.Value!.ConcreteStatus);
        Assert.Equal(CalculationStatus.Calculated, result.Value.FormworkStatus);
        Assert.Null(result.Value.SteelSchedule);
        Assert.Null(result.Value.DrawingPackage);
    }

    [Fact]
    public void BothQuantityScopesReturnIndependentPackagesThroughBatchWrapper()
    {
        var stages = Stages();
        var result = stages.Pipeline.CalculateQuantityBatch(stages.Input, stages.Policy,
            [CalculationScope.ConcreteOnly, CalculationScope.FormworkOnly], CancellationToken.None);

        Assert.True(result.Success, Details(result.Diagnostics));
        Assert.Equal(new[] { CalculationScope.ConcreteOnly, CalculationScope.FormworkOnly },
            result.Value!.Packages.Select(x => x.Scope));
        Assert.All(result.Value.Packages, package => Assert.Null(package.SteelSchedule));
        Assert.DoesNotContain(result.Value.Packages, package => package.Scope == CalculationScope.Full);
    }

    [Fact]
    public void S2StageFailureReturnsNullValueAndBlockDiagnostic()
    {
        var stages = Stages(failingConcrete: true);
        var result = stages.Pipeline.Calculate(stages.Input, stages.Policy,
            new CalculationRequest("S1", [stages.Spec.Input.Id], CalculationScope.ConcreteOnly, "S2Geometry-1", true),
            new PresentationRequest(20, ElevationMode.Symbolic, null, "S2", false), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, x => x.Code == "QUANTITY_STAGE_FAILED" && x.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void S2RequestGateIsNotTheS1RequestGate()
    {
        var request = new CalculationRequest("S1", ["S1/GaThamLienKhoi/S1-MONO-001"],
            CalculationScope.ConcreteOnly, "S2Geometry-1", true);

        Assert.False(request.CheckS1().Success);
        Assert.True(request.CheckS2QuantityInput().Success);
    }

    private static StageFixture Stages(bool failingConcrete = false)
    {
        var raw = ManholeValidatorTests.Raw();
        var catalogs = ManholeValidatorTests.Catalogs();
        var policy = CalculationPolicy.Select("S2Geometry-1", true).Value!;
        var request = new CalculationRequest("S1", [ManholeValidatorTests.Id], CalculationScope.ConcreteOnly, "S2Geometry-1", true);
        var validation = new ManholeValidator(new NumericCellReader(), new NumericPolicy("en-US", false, false))
            .Validate(raw, catalogs, request, policy);
        Assert.True(validation.Success, Details(validation.Diagnostics));
        var spec = Assert.Single(validation.Value!.SelectedManholes);
        var input = new ValidatedProjectInput([spec], validation.Value.InputHash, validation.Value.RuleVersion,
            validation.Diagnostics, raw.WorkbookHash);
        var line = new QuantityLine("S2-Concrete", "Base", "Concrete", 1, 0, 0, 1, QuantityUnit.M3, "S2-Q-1", [], []);
        var formwork = new QuantityLine("S2-Formwork", "Base", "Formwork", 0, 1, 0, 1, QuantityUnit.M2, "S2-FW-1", [], []);
        var pipeline = new CalculationPipeline(new GeometryStage(), null!, null!, null!,
            new ConcreteStage(line, failingConcrete), new FormworkStage(formwork));
        return new(policy, spec, input, pipeline);
    }

    private static string Details(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(x => $"{x.Code}: {x.Message}"));

    private sealed record StageFixture(CalculationPolicy Policy, ValidatedManholeSpec Spec,
        ValidatedProjectInput Input, CalculationPipeline Pipeline);

    private sealed class GeometryStage : IGeometryStage
    {
        public OperationResult<GeometryModel> Build(ValidatedManholeSpec spec, CalculationPolicy policy) =>
            new(true, new GeometryModel([], []), []);
    }

    private sealed class ConcreteStage(QuantityLine line, bool failing) : IConcreteQuantityStage
    {
        public OperationResult<ConcreteQuantityResult> Calculate(ValidatedManholeSpec spec, GeometryModel geometry, QuantityPolicy policy) =>
            failing
                ? new(false, null, [new Diagnostic("QUANTITY_STAGE_FAILED", DiagnosticSeverity.Block, DiagnosticScope.Manhole,
                    spec.Input.Id, null, null, null, null, "synthetic concrete stage failure")])
                : new(true, new ConcreteQuantityResult([line], []), []);
    }

    private sealed class FormworkStage(QuantityLine line) : IFormworkQuantityStage
    {
        public OperationResult<FormworkResult> Calculate(ValidatedManholeSpec spec, GeometryModel geometry, FormworkPolicy policy) =>
            new(true, new FormworkResult([line], []), []);
    }
}
