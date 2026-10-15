using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class ScenarioOrchestrationTests
{
    [Theory]
    [InlineData(20, "A", ElevationMode.Absolute)]
    [InlineData(30, "B", ElevationMode.Symbolic)]
    public async Task FrozenPackagePresentationDoesNotChangeIdentityOrRecalculate(double scale, string theme, ElevationMode mode)
    {
        var hash = new string('A', 64);
        var drawing = new DrawingPackage(hash, "fixture/v1", [new ViewDefinition("plan", ViewPurpose.Plan, new(0, 0), null, 20)], [], new(0, 0, 100, 100), CalculationScope.GeometryOnly);
        var package = new CalculationPackage(hash, hash, "engine/v1", "contract/v1", "fixture/v1", "S1-Analytic-1", CalculationScope.GeometryOnly, new GeometryModel([], []), [], null, CalculationStatus.NotCalculated, null, CalculationStatus.NotCalculated, null, drawing, []);
        var template = new TemplateContractValidator().Validate(new TemplateManifest("template-manifest-v1", hash, InspectionStatus.Inspected, []), [], TemplateValidationMode.ProductionRender).Value!;
        var writer = new Recorder();
        var useCase = new GenerateDrawingUseCase(writer, template);
        var result = await useCase.GenerateAsync(package, new(GenerationMode.Create, "copy", null, hash, new(0, 0)), new(scale, mode, mode == ElevationMode.Absolute ? new ElevationM(10) : null, theme, true), default);
        if(scale!=20)
        {
            // Updating view metadata alone formerly left the old text/symbol/layout sizes in place.
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>d.Code=="PRESENTATION_REGENERATION_REQUIRED");
            Assert.Equal(0,writer.Calls);
            return;
        }
        Assert.True(result.Success);
        Assert.Equal(1, writer.Calls);
        Assert.Equal(hash, writer.Drawing!.InputHash);
        Assert.Equal(scale, Assert.Single(writer.Drawing.Views).ScaleDenominator);
        Assert.Equal(CalculationStatus.NotCalculated, package.ConcreteStatus);
        Assert.Equal(CalculationStatus.NotCalculated, package.FormworkStatus);
    }

    private sealed class Recorder : ICadGenerationWriter
    {
        public int Calls;
        public DrawingPackage? Drawing;
        public Task<OperationResult<GenerationArtifact>> WriteAsync(DrawingPackage package, ValidatedTemplateManifest template, GenerationRequest request, CancellationToken cancellation)
        {
            Calls++;
            Drawing = package;
            return Task.FromResult(new OperationResult<GenerationArtifact>(true, new("copy", "fixture", package.InputHash, template.Manifest.SourceFileHash, "generation-xdata/v1", [], true), []));
        }
    }
}
