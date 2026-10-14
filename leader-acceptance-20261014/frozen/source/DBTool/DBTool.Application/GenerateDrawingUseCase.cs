using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Drawing;

namespace DBTool.Application;

/// <summary>
/// Sends an already calculated immutable package to CAD. It deliberately has no calculation-stage dependency.
/// </summary>
public sealed class GenerateDrawingUseCase
{
    private readonly ICadGenerationWriter _writer;
    private readonly ValidatedTemplateManifest _template;

    public GenerateDrawingUseCase(ICadGenerationWriter writer, ValidatedTemplateManifest template)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _template = template ?? throw new ArgumentNullException(nameof(template));
    }

    public async Task<OperationResult<GenerationArtifact>> GenerateAsync(
        CalculationPackage package,
        GenerationRequest request,
        PresentationRequest presentation,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(presentation);

            if (!presentation.IncludeDrawing)
                return UseCaseErrors.Fail<GenerationArtifact>("DRAWING_NOT_REQUESTED", "Presentation does not request a drawing.");
            if (!_template.CanRender)
                return UseCaseErrors.Fail<GenerationArtifact>("TEMPLATE_NOT_RENDERABLE", "The validated template is not authorized for rendering.");
            if (package.DrawingPackage is null)
                return UseCaseErrors.Fail<GenerationArtifact>("DRAWING_PACKAGE_MISSING", "The calculation package has no drawing package.");
            if (!string.Equals(package.InputHash, request.ExpectedInputHash, StringComparison.OrdinalIgnoreCase))
                return UseCaseErrors.Fail<GenerationArtifact>("GENERATION_INPUT_HASH_MISMATCH", "The generation request is stale for this calculation package.");
            if (!string.Equals(package.InputHash, package.DrawingPackage.InputHash, StringComparison.OrdinalIgnoreCase))
                return UseCaseErrors.Fail<GenerationArtifact>("DRAWING_INPUT_HASH_MISMATCH", "The drawing package does not belong to the calculation package.");

            cancellationToken.ThrowIfCancellationRequested();
            if (package.DrawingPackage.Groups.IsEmpty && !package.Occurrences.IsEmpty)
            {
                return UseCaseErrors.Fail<GenerationArtifact>("GROUP_REGISTRY_EMPTY",
                    "S5 group registry is empty while occurrences are present.");
            }
            var coverage=DrawingPackageValidator.ValidateModelCoverage(package.Occurrences,package.DrawingPackage);
            if(!coverage.Success) return new(false,null,coverage.Diagnostics);
            if (package.DrawingPackage.Views.Any(view => Math.Abs(view.ScaleDenominator-presentation.ScaleDenominator)>1e-12))
                return UseCaseErrors.Fail<GenerationArtifact>("PRESENTATION_REGENERATION_REQUIRED",
                    "Scale changed. Regenerate the drawing package from the model with the requested presentation before CAD write; frozen annotations and layout cannot be reused.");
            var drawing = package.DrawingPackage;
            return await _writer.WriteAsync(drawing, _template, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return UseCaseErrors.Fail<GenerationArtifact>("OPERATION_CANCELLED", "Generation cancelled before CAD write completed.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return UseCaseErrors.Fail<GenerationArtifact>("CAD_GENERATION_FAILED", ex.Message);
        }
    }

}
