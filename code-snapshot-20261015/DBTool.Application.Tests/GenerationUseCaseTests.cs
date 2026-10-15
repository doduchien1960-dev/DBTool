using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class GenerationUseCaseTests
{
    private const string Hash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    public async Task ScaleChangeCannotReuseFrozenAnnotationPrimitives(double scale)
    {
        var writer=new RecordingWriter();
        var result=await new GenerateDrawingUseCase(writer,RenderableTemplate()).GenerateAsync(Package(),CreateRequest(),
            new PresentationRequest(scale,ElevationMode.Symbolic,null,"default",true),default);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="PRESENTATION_REGENERATION_REQUIRED");
        Assert.Equal(0,writer.Calls);
    }

    [Fact]
    public async Task Cancellation_before_cad_lock_does_not_call_writer()
    {
        var writer = new RecordingWriter();
        var useCase = new GenerateDrawingUseCase(writer, RenderableTemplate());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await useCase.GenerateAsync(Package(), CreateRequest(), Presentation(), cancellation.Token);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("OPERATION_CANCELLED", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task Stale_document_failure_is_block_and_does_not_expose_partial_artifact()
    {
        var writer = new RecordingWriter
        {
            Result = new OperationResult<GenerationArtifact>(false, null,
                [new Diagnostic("DOC_CONTEXT_CHANGED", DiagnosticSeverity.Block, DiagnosticScope.Global,
                    null, null, null, null, null, "Document changed before write.")])
        };
        var useCase = new GenerateDrawingUseCase(writer, RenderableTemplate());

        var result = await useCase.GenerateAsync(Package(), CreateRequest(), Presentation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("DOC_CONTEXT_CHANGED", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Writer_cancellation_is_not_reported_as_committed()
    {
        var writer = new RecordingWriter
        {
            ThrowCancellation = true
        };
        var useCase = new GenerateDrawingUseCase(writer, RenderableTemplate());

        var result = await useCase.GenerateAsync(Package(), CreateRequest(), Presentation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("OPERATION_CANCELLED", Assert.Single(result.Diagnostics).Code);
    }

    private static CalculationPackage Package()
    {
        var drawing = new DrawingPackage(Hash, "rules/v1",
            [new ViewDefinition("plan", ViewPurpose.Plan, new Point2Mm(0, 0), null, 20)],
            [], new Bounds2Mm(0, 0, 100, 100), CalculationScope.GeometryOnly);
        return new CalculationPackage(Hash, Hash, "engine/v1", "contract/v1", "rules/v1", "S1-Analytic-1",
            CalculationScope.GeometryOnly, new GeometryModel([], []), [], null,
            CalculationStatus.NotCalculated, null, CalculationStatus.NotCalculated, null, drawing, []);
    }

    private static GenerationRequest CreateRequest() =>
        new(GenerationMode.Create, "doc-A", null, Hash, new Point2Mm(0, 0));

    private static PresentationRequest Presentation() =>
        new(20, ElevationMode.Symbolic, null, "default", true);

    private static ValidatedTemplateManifest RenderableTemplate() =>
        new TemplateContractValidator().Validate(new TemplateManifest("template-manifest-v1", Hash,
            InspectionStatus.Inspected, []), [], TemplateValidationMode.ProductionRender).Value!;

    private sealed class RecordingWriter : ICadGenerationWriter
    {
        public int Calls { get; private set; }
        public bool ThrowCancellation { get; init; }
        public OperationResult<GenerationArtifact>? Result { get; init; }

        public Task<OperationResult<GenerationArtifact>> WriteAsync(
            DrawingPackage drawingPackage,
            ValidatedTemplateManifest template,
            GenerationRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (ThrowCancellation) throw new OperationCanceledException();
            return Task.FromResult(Result ?? new OperationResult<GenerationArtifact>(true,
                new GenerationArtifact(request.ExpectedDocumentToken!, "generation", drawingPackage.InputHash,
                    template.Manifest.SourceFileHash, "snapshot/v1", [], true), []));
        }
    }
}
