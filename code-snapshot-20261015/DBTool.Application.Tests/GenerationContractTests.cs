using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class GenerationContractTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void GenerationRequest_preserves_opaque_document_and_write_expectations()
    {
        var request = new GenerationRequest(
            GenerationMode.Replace,
            expectedDocumentToken: "doc-A",
            expectedGenerationId: "generation-1",
            expectedInputHash: Hash,
            insertionPoint: new Point2Mm(10, 20));

        Assert.Equal(GenerationMode.Replace, request.Mode);
        Assert.Equal("doc-A", request.ExpectedDocumentToken);
        Assert.Equal("generation-1", request.ExpectedGenerationId);
        Assert.Equal(Hash, request.ExpectedInputHash);
        Assert.Equal(new Point2Mm(10, 20), request.InsertionPoint);
    }

    [Fact]
    public async Task GenerateDrawingUseCase_does_not_recalculate_and_forwards_same_package_hash()
    {
        var drawing = new DrawingPackage(Hash, "rules/v1", [
            new ViewDefinition("plan", ViewPurpose.Plan, new Point2Mm(0, 0), null, 20)
        ], [], null, CalculationScope.GeometryOnly);
        var package = new CalculationPackage(
            Hash,
            Hash,
            "engine/v1",
            "contract/v1",
            "rules/v1",
            "S1-Analytic-1",
            CalculationScope.GeometryOnly,
            new DBTool.Domain.GeometryModel([], []),
            [],
            null,
            CalculationStatus.NotCalculated,
            null,
            CalculationStatus.NotCalculated,
            null,
            drawing,
            []);
        var writer = new RecordingGenerationWriter();
        var manifest = new DBTool.Application.Templates.TemplateManifest(
            "template-manifest-v1",
            Hash,
            DBTool.Application.Templates.InspectionStatus.Inspected,
            []);
        var validatedManifest = new DBTool.Application.Templates.TemplateContractValidator()
            .Validate(manifest, [], DBTool.Application.Templates.TemplateValidationMode.ProductionRender)
            .Value!;
        var useCase = new GenerateDrawingUseCase(writer, validatedManifest);

        var result = await useCase.GenerateAsync(
            package,
            new GenerationRequest(GenerationMode.Create, "doc-A", null, Hash, new Point2Mm(0, 0)),
            new PresentationRequest(20, ElevationMode.Symbolic, null, "default", true),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, writer.Calls);
        Assert.Equal(Hash, writer.LastPackageHash);
        Assert.Equal(20, writer.LastScale);
    }

    private sealed class RecordingGenerationWriter : ICadGenerationWriter
    {
        public int Calls { get; private set; }
        public string? LastPackageHash { get; private set; }
        public double? LastScale { get; private set; }

        public Task<OperationResult<GenerationArtifact>> WriteAsync(
            DrawingPackage drawingPackage,
            DBTool.Application.Templates.ValidatedTemplateManifest template,
            GenerationRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastPackageHash = drawingPackage.InputHash;
            LastScale = drawingPackage.Views.Single().ScaleDenominator;
            var artifact = new GenerationArtifact(
                request.ExpectedDocumentToken!,
                request.RequestedGenerationId ?? "generation-new",
                drawingPackage.InputHash,
                template.Manifest.SourceFileHash,
                "snapshot/v1",
                [],
                true);
            return Task.FromResult<OperationResult<GenerationArtifact>>(new(true, artifact, []));
        }
    }

}
