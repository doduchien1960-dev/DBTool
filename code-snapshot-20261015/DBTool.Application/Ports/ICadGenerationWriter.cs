using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Drawing;

namespace DBTool.Application;

public interface ICadGenerationWriter
{
    Task<OperationResult<GenerationArtifact>> WriteAsync(
        DrawingPackage drawingPackage,
        ValidatedTemplateManifest template,
        GenerationRequest request,
        CancellationToken cancellationToken);
}
